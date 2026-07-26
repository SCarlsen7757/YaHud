using Microsoft.Extensions.Logging.Abstractions;
using R3E.Core.Interfaces;
using R3E.Data;
using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;

namespace R3E.Core.Recording
{
    /// <summary>
    /// Subscribes to <see cref="ISharedSource.RawFrameReceived"/> and persists raw telemetry to
    /// <c>.yhtl</c> files, one file per session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ingest loop must never wait on disk. <c>RawFrameReceived</c> fires synchronously on the
    /// polling thread, so a disk hiccup, AV scan or full buffer would otherwise stall telemetry and
    /// visibly freeze the HUD. The write path is therefore: truncate into an <c>ArrayPool</c> buffer,
    /// push onto a bounded channel with <c>DropWrite</c>, and let a background writer task compress
    /// and write. For a debug recorder, dropping frames beats stuttering the HUD, and every frame is
    /// independently decodable, so a gap only shows as a longer inter-frame delta on replay.
    /// </para>
    /// <para>
    /// One recording run produces one file per session. The recorder watches <c>SessionType</c> and
    /// <c>TrackId</c> straight out of the raw buffer — no marshalling — and on a change finalises the
    /// current file and opens a new one on the next frame. Files live in a per-run folder so the
    /// sessions of a stint stay together.
    /// </para>
    /// </remarks>
    public sealed class TelemetryRecorder : IHostedService, IAsyncDisposable
    {
        /// <summary>
        /// Bounded channel capacity, in records. Four seconds of headroom at the 60 Hz polling rate:
        /// enough to ride out an AV scan or a stalled write without letting the pooled backlog grow
        /// without bound (worst case ≈ 240 × 44 KB ≈ 10 MB of rented buffers).
        /// </summary>
        private const int ChannelCapacity = 240;

        /// <summary>Minimum interval between <see cref="StateChanged"/> notifications from the writer.</summary>
        private static readonly TimeSpan StateNotifyInterval = TimeSpan.FromMilliseconds(500);

        /// <summary>How long <see cref="Start"/> waits for a previous run to finish finalising.</summary>
        private static readonly TimeSpan PreviousRunDrainTimeout = TimeSpan.FromSeconds(10);

        // Offsets into the raw frame, resolved once so the hot path never marshals. Mirrors the
        // pattern SharedMemoryService already uses for GameSimulationTicks.
        private static readonly int offsetSessionType;
        private static readonly int offsetSessionPhase;
        private static readonly int offsetTrackId;
        private static readonly int offsetCompletedLaps;
        private static readonly int offsetDriverClassId;

        private readonly ISharedSource sharedSource;
        private readonly RecordingOptions options;
        private readonly ILogger<TelemetryRecorder> logger;
        private readonly string yaHudVersion;
        private readonly Lock stateLock = new();
        private readonly HashSet<int> observedClasses = [];
        private readonly Stopwatch stopwatch = new();

        private Channel<QueuedRecord>? channel;
        private Task? runTask;
        private volatile bool isRecording;
        private bool subscribed;
        private bool disposed;

        // Run state. Written under stateLock on start/stop, otherwise only by the writer task.
        private string? runFolder;
        private string? explicitRunFolder;
        private DateTime runStartUtc;
        private TimeSpan finalElapsed;

        private long frameCount;
        private long bytesWrittenClosed;
        private long droppedFrames;

        // Writer-task-only state.
        private TelemetryRecordingWriter? writer;
        private int fileIndex;
        private long fileStartMs;
        private int currentSessionType;
        private int currentTrackId;
        private int lastSessionPhase;
        private int lastCompletedLaps;
        private bool fileOpenFailed;
        private long lastNotifyMs;

        static TelemetryRecorder()
        {
            offsetSessionType = (int)Marshal.OffsetOf<Shared>(nameof(Shared.SessionType));
            offsetSessionPhase = (int)Marshal.OffsetOf<Shared>(nameof(Shared.SessionPhase));
            offsetTrackId = (int)Marshal.OffsetOf<Shared>(nameof(Shared.TrackId));
            offsetCompletedLaps = (int)Marshal.OffsetOf<Shared>(nameof(Shared.CompletedLaps));
            offsetDriverClassId =
                (int)Marshal.OffsetOf<DriverData>(nameof(DriverData.DriverInfo))
                + (int)Marshal.OffsetOf<DriverInfo>(nameof(DriverInfo.ClassId));
        }

        /// <summary>
        /// Creates the recorder. It stays inert until <see cref="Start"/> is called, or immediately
        /// starts when <see cref="RecordingOptions.AutoStart"/> is set.
        /// </summary>
        /// <param name="sharedSource">The source whose raw frames are captured.</param>
        /// <param name="options">Recording settings from configuration and launch arguments.</param>
        /// <param name="logger">Optional logger.</param>
        public TelemetryRecorder(ISharedSource sharedSource, RecordingOptions options, ILogger<TelemetryRecorder>? logger = null)
        {
            this.sharedSource = sharedSource ?? throw new ArgumentNullException(nameof(sharedSource));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.logger = logger ?? NullLogger<TelemetryRecorder>.Instance;
            yaHudVersion = ResolveYaHudVersion();
        }

        /// <summary>Raised whenever any of the state below changes, so the UI can re-render.</summary>
        public event Action? StateChanged;

        /// <summary>
        /// Whether the source is currently replaying a recording rather than delivering live
        /// telemetry. Recording a replay would write a near-duplicate of the file being played, so
        /// frames are dropped for as long as this holds.
        /// </summary>
        /// <remarks>
        /// The recorder is handed the <see cref="ISharedSource"/> registration, which resolves to
        /// the live/replay switch rather than the live source directly - so without this guard the
        /// recorder happily captures replayed frames.
        /// </remarks>
        private bool IsSourceReplaying => sharedSource is ISwitchableSharedSource { IsReplaying: true };

        /// <summary><see langword="true"/> while frames are being captured.</summary>
        public bool IsRecording => isRecording;

        /// <summary>
        /// Path of the file currently being written, or <see langword="null"/> when not recording or
        /// when no frame has arrived yet — the file is created on the first frame.
        /// </summary>
        public string? CurrentFile => writer?.Path;

        /// <summary>Time since recording started.</summary>
        public TimeSpan Elapsed => isRecording ? stopwatch.Elapsed : finalElapsed;

        /// <summary>Frames written across this recording run.</summary>
        public long FrameCount => Interlocked.Read(ref frameCount);

        /// <summary>Bytes written across this recording run.</summary>
        public long BytesWritten
        {
            get
            {
                var closed = Interlocked.Read(ref bytesWrittenClosed);
                var current = writer;
                return current is null || current.IsClosed ? closed : closed + current.BytesWritten;
            }
        }

        /// <summary>
        /// Frames dropped because the bounded channel was full. Non-zero means the disk could not
        /// keep up; the HUD was protected rather than stalled.
        /// </summary>
        public long DroppedFrames => Interlocked.Read(ref droppedFrames);

        /// <summary>Folder of the current recording run, containing this run's session files.</summary>
        public string? CurrentRunFolder => runFolder;

        /// <summary>
        /// Starts recording. Nothing is written until the first frame arrives.
        /// </summary>
        /// <param name="path">
        /// Optional run folder. When null, a timestamped folder is created under the configured
        /// recordings directory.
        /// </param>
        public void Start(string? path = null)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            lock (stateLock)
            {
                if (isRecording)
                {
                    return;
                }

                WaitForPreviousRun();

                explicitRunFolder = string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
                runFolder = explicitRunFolder;
                runStartUtc = DateTime.UtcNow;
                finalElapsed = TimeSpan.Zero;
                fileIndex = 0;
                fileOpenFailed = false;
                lastNotifyMs = 0;
                observedClasses.Clear();

                Interlocked.Exchange(ref frameCount, 0);
                Interlocked.Exchange(ref bytesWrittenClosed, 0);
                Interlocked.Exchange(ref droppedFrames, 0);

                var bounded = new BoundedChannelOptions(ChannelCapacity)
                {
                    FullMode = BoundedChannelFullMode.DropWrite,
                    SingleReader = true,
                    SingleWriter = false,
                };

                // The drop callback is the only place a DropWrite rejection is observable: TryWrite
                // still returns true, so without it the rented buffer would leak.
                channel = Channel.CreateBounded<QueuedRecord>(bounded, OnItemDropped);

                stopwatch.Restart();
                isRecording = true;
                runTask = Task.Run(WriterLoopAsync);

                logger.LogInformation("Telemetry recording started; run folder resolves on the first frame");
            }

            RaiseStateChanged();
        }

        /// <summary>
        /// Stops recording and finalises the current file — footer, summary and index patch.
        /// Idempotent.
        /// </summary>
        /// <remarks>
        /// Returns as soon as the ingest path is detached. Finalisation completes on the background
        /// writer task, which raises <see cref="StateChanged"/> when the file is closed; blocking here
        /// would put a disk flush on the UI thread. <see cref="StopAsync"/> and
        /// <see cref="DisposeAsync"/> await that task.
        /// </remarks>
        public void Stop()
        {
            lock (stateLock)
            {
                if (!isRecording)
                {
                    return;
                }

                isRecording = false;
                finalElapsed = stopwatch.Elapsed;
                stopwatch.Stop();

                // Completing the writer half lets the loop drain what is already queued and then
                // finalise the file; nothing is cancelled, so no captured frame is thrown away.
                channel?.Writer.TryComplete();

                logger.LogInformation(
                    "Telemetry recording stopped after {Elapsed} with {Frames} frames ({Dropped} dropped)",
                    finalElapsed,
                    FrameCount,
                    DroppedFrames);
            }

            RaiseStateChanged();
        }

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!subscribed)
            {
                sharedSource.RawFrameReceived += OnRawFrameReceived;
                sharedSource.StartLightsChanged += OnStartLightsChanged;
                subscribed = true;
            }

            if (options.AutoStart)
            {
                Start();
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Unsubscribe();
            Stop();
            await AwaitRunAsync().ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Unsubscribe();
            Stop();
            await AwaitRunAsync().ConfigureAwait(false);
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            subscribed = false;
            sharedSource.RawFrameReceived -= OnRawFrameReceived;
            sharedSource.StartLightsChanged -= OnStartLightsChanged;
        }

        private async Task AwaitRunAsync()
        {
            var task = runTask;
            if (task is null)
            {
                return;
            }

            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Telemetry recording writer task faulted");
            }
        }

        private void WaitForPreviousRun()
        {
            var previous = runTask;
            if (previous is null || previous.IsCompleted)
            {
                return;
            }

            // A stop immediately followed by a start: let the previous file finish its footer so the
            // two runs cannot interleave writer state. Finalising is a single block flush.
            if (!previous.Wait(PreviousRunDrainTimeout))
            {
                logger.LogWarning("Previous recording run did not finish finalising within {Timeout}", PreviousRunDrainTimeout);
            }
        }

        // ---------------------------------------------------------------------------------------
        // Ingest — runs on the polling thread. Must never block, allocate heavily, or throw.
        // ---------------------------------------------------------------------------------------

        private void OnRawFrameReceived(ReadOnlyMemory<byte> frame)
        {
            if (!isRecording || IsSourceReplaying)
            {
                return;
            }

            byte[]? buffer = null;
            try
            {
                var span = frame.Span;
                if (span.Length != FrameTruncation.SizeOfShared)
                {
                    return;
                }

                var truncated = FrameTruncation.Truncate(span, out var length);

                // The event's memory is over a reused buffer, so the copy has to happen here,
                // synchronously, before the record is handed to the writer task.
                buffer = ArrayPool<byte>.Shared.Rent(length);
                truncated.CopyTo(buffer);

                Enqueue(new QueuedRecord(RecordingRecordType.Frame, buffer, length, 0, stopwatch.ElapsedMilliseconds));
                buffer = null;
            }
            catch (Exception ex)
            {
                // A recorder fault must not propagate into the polling loop.
                logger.LogError(ex, "Failed to capture a raw telemetry frame");
            }
            finally
            {
                if (buffer is not null)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        private void OnStartLightsChanged(int value)
        {
            if (!isRecording || IsSourceReplaying)
            {
                return;
            }

            try
            {
                Enqueue(new QueuedRecord(RecordingRecordType.StartLights, null, 0, value, stopwatch.ElapsedMilliseconds));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to capture a start-light sample");
            }
        }

        private void Enqueue(in QueuedRecord record)
        {
            var target = channel;
            if (target is null || !target.Writer.TryWrite(record))
            {
                // Channel already completed (a concurrent Stop). Nothing will read this record.
                OnItemDropped(record);
            }
        }

        private void OnItemDropped(QueuedRecord record)
        {
            ReturnBuffer(record);

            if (record.Type == RecordingRecordType.Frame)
            {
                Interlocked.Increment(ref droppedFrames);
            }
        }

        private static void ReturnBuffer(in QueuedRecord record)
        {
            if (record.Buffer is not null)
            {
                ArrayPool<byte>.Shared.Return(record.Buffer);
            }
        }

        // ---------------------------------------------------------------------------------------
        // Writer task — owns the file, the block index and every offset read past the ingest hop.
        // ---------------------------------------------------------------------------------------

        private async Task WriterLoopAsync()
        {
            var reader = channel!.Reader;

            try
            {
                while (await reader.WaitToReadAsync().ConfigureAwait(false))
                {
                    while (reader.TryRead(out var record))
                    {
                        try
                        {
                            Process(record);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to write a telemetry record");
                        }
                        finally
                        {
                            ReturnBuffer(record);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Telemetry recording writer loop failed");
            }
            finally
            {
                // Anything still queued after a fault must give its buffer back to the pool.
                while (reader.TryRead(out var leftover))
                {
                    ReturnBuffer(leftover);
                }

                CloseCurrentFile();
                RaiseStateChanged();
            }
        }

        private void Process(in QueuedRecord record)
        {
            if (record.Type == RecordingRecordType.StartLights)
            {
                // Start lights sampled before the first frame have no file to land in; the header
                // cannot exist yet, so the sample is discarded rather than opening an empty file.
                writer?.WriteStartLights(record.StartLights, ToFileElapsedMs(record.TimestampMs));
                return;
            }

            if (record.Buffer is null)
            {
                return;
            }

            var frame = record.Buffer.AsSpan(0, record.Length);
            var sessionType = ReadInt32(frame, offsetSessionType);
            var trackId = ReadInt32(frame, offsetTrackId);

            if (writer is not null && (sessionType != currentSessionType || trackId != currentTrackId))
            {
                logger.LogInformation(
                    "Session changed ({OldSession}/{OldTrack} -> {NewSession}/{NewTrack}); finalising recording file",
                    currentSessionType,
                    currentTrackId,
                    sessionType,
                    trackId);

                CloseCurrentFile();
            }

            if (writer is null)
            {
                if (fileOpenFailed && sessionType == currentSessionType && trackId == currentTrackId)
                {
                    // Opening this session's file already failed; do not retry it 60 times a second.
                    return;
                }

                if (!TryOpenFile(frame, sessionType, trackId, record.TimestampMs))
                {
                    return;
                }
            }

            var elapsedMs = ToFileElapsedMs(record.TimestampMs);

            writer!.WriteFrame(frame, elapsedMs);
            Interlocked.Increment(ref frameCount);

            WriteMarkers(frame, elapsedMs);
            ObserveClasses(frame);
            NotifyThrottled(record.TimestampMs);
        }

        private void WriteMarkers(ReadOnlySpan<byte> frame, uint elapsedMs)
        {
            var phase = ReadInt32(frame, offsetSessionPhase);
            if (phase != lastSessionPhase)
            {
                lastSessionPhase = phase;
                writer!.AddMarker(RecordingMarkerType.PhaseChange, phase, elapsedMs);
            }

            var completedLaps = ReadInt32(frame, offsetCompletedLaps);
            if (completedLaps != lastCompletedLaps)
            {
                lastCompletedLaps = completedLaps;

                if (completedLaps >= 0)
                {
                    writer!.AddMarker(RecordingMarkerType.LapStart, completedLaps, elapsedMs);
                }
            }
        }

        private void ObserveClasses(ReadOnlySpan<byte> frame)
        {
            var numCars = FrameTruncation.ReadNumCars(frame);
            var driversStart = FrameTruncation.MinTruncatedLength;

            for (var i = 0; i < numCars; i++)
            {
                var offset = driversStart + (i * FrameTruncation.DriverDataSize) + offsetDriverClassId;
                if (offset + sizeof(int) > frame.Length)
                {
                    break;
                }

                var classId = BitConverter.ToInt32(frame[offset..]);
                if (classId > 0 && observedClasses.Add(classId))
                {
                    writer!.ObserveClass(classId);
                }
            }
        }

        private bool TryOpenFile(ReadOnlySpan<byte> frame, int sessionType, int trackId, long timestampMs)
        {
            currentSessionType = sessionType;
            currentTrackId = trackId;

            try
            {
                if (!FrameTruncation.MatchesLayout(frame))
                {
                    logger.LogWarning(
                        "Raw frame reports a different Shared layout than this build; the recording may not replay");
                }

                // FromFirstFrame documents a full-size frame, so rebuild the zero-filled tail once
                // per file. One allocation per session file is not worth pooling.
                var full = FrameTruncation.Restore(frame);
                var header = TelemetryRecordingHeader.FromFirstFrame(full, yaHudVersion);

                if (header.StartUtcTicks == 0)
                {
                    header.StartUtcTicks = DateTime.UtcNow.Ticks;
                }

                var folder = ResolveRunFolder(header);
                Directory.CreateDirectory(folder);

                fileIndex++;
                var path = Path.Combine(folder, BuildFileName(fileIndex, sessionType));

                writer = new TelemetryRecordingWriter(path, header, Math.Max(0.1d, options.BlockSeconds));
                fileStartMs = timestampMs;
                lastSessionPhase = ReadInt32(frame, offsetSessionPhase);
                lastCompletedLaps = ReadInt32(frame, offsetCompletedLaps);
                fileOpenFailed = false;
                observedClasses.Clear();

                logger.LogInformation("Recording session {SessionType} to {Path}", sessionType, path);
                RaiseStateChanged();
                return true;
            }
            catch (Exception ex)
            {
                fileOpenFailed = true;
                writer = null;
                logger.LogError(ex, "Failed to open a recording file for session {SessionType}", sessionType);
                return false;
            }
        }

        private void CloseCurrentFile()
        {
            var current = writer;
            if (current is null)
            {
                return;
            }

            writer = null;

            try
            {
                current.Close();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to finalise recording file {Path}", current.Path);
            }
            finally
            {
                try
                {
                    Interlocked.Add(ref bytesWrittenClosed, current.BytesWritten);
                    current.Dispose();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to dispose recording file {Path}", current.Path);
                }
            }
        }

        private string ResolveRunFolder(TelemetryRecordingHeader header)
        {
            if (runFolder is not null)
            {
                return runFolder;
            }

            // Named from the run's start time, not the first frame's, so the folder still reads as
            // "when I pressed record"; the track slug only becomes knowable here.
            var name = new StringBuilder(runStartUtc.ToLocalTime().ToString("yyyy-MM-dd_HH-mm"));
            var slug = Slugify(header.TrackName);

            if (slug.Length > 0)
            {
                name.Append('-').Append(slug);
            }

            var resolved = Path.Combine(options.ResolveDirectory(), name.ToString());
            runFolder = resolved;
            return resolved;
        }

        private static string BuildFileName(int index, int sessionType)
        {
            var session = Enum.IsDefined(typeof(Constant.Session), sessionType)
                ? ((Constant.Session)sessionType).ToString().ToLowerInvariant()
                : "session";

            return $"{index:00}-{session}{TelemetryRecordingHeader.FileExtension}";
        }

        private static string Slugify(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (char.IsAsciiLetterOrDigit(c))
                {
                    builder.Append(char.ToLowerInvariant(c));
                }
                else if (builder.Length > 0 && builder[^1] != '-')
                {
                    builder.Append('-');
                }
            }

            return builder.ToString().Trim('-');
        }

        private uint ToFileElapsedMs(long timestampMs)
        {
            var delta = timestampMs - fileStartMs;
            return delta <= 0 ? 0u : (uint)delta;
        }

        private void NotifyThrottled(long timestampMs)
        {
            if (timestampMs - lastNotifyMs < StateNotifyInterval.TotalMilliseconds)
            {
                return;
            }

            lastNotifyMs = timestampMs;
            RaiseStateChanged();
        }

        private void RaiseStateChanged()
        {
            try
            {
                StateChanged?.Invoke();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "A recorder StateChanged handler threw");
            }
        }

        private static string ResolveYaHudVersion()
        {
            var assembly = Assembly.GetEntryAssembly() ?? typeof(TelemetryRecorder).Assembly;
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "unknown";
        }

        /// <summary>
        /// One queued record. <see cref="Buffer"/> is rented from <see cref="ArrayPool{T}.Shared"/>
        /// by the ingest thread and returned by whoever consumes or drops the record.
        /// </summary>
        /// <param name="Type">Which kind of record this is.</param>
        /// <param name="Buffer">Pooled truncated frame bytes, or null for a start-light sample.</param>
        /// <param name="Length">Meaningful length of <paramref name="Buffer"/>; the rental may be longer.</param>
        /// <param name="StartLights">Start-light count, for <see cref="RecordingRecordType.StartLights"/>.</param>
        /// <param name="TimestampMs">Run-relative milliseconds captured at ingest, so writer lag cannot skew cadence.</param>
        private readonly record struct QueuedRecord(
            RecordingRecordType Type,
            byte[]? Buffer,
            int Length,
            int StartLights,
            long TimestampMs);

        private static int ReadInt32(ReadOnlySpan<byte> frame, int offset)
            => offset + sizeof(int) <= frame.Length ? BitConverter.ToInt32(frame[offset..]) : 0;
    }
}
