using R3E.Core.Interfaces;
using R3E.Data;

namespace R3E.Core.Replay
{
    /// <summary>
    /// The composite <see cref="ISharedSource"/> that makes live↔replay switchable at runtime.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registered as the singleton <see cref="ISharedSource"/>, it holds the live source (shared
    /// memory or UDP, chosen by the existing OS/<c>--force-udp</c> logic) and a
    /// <see cref="FileSharedSource"/>, and forwards events from whichever is active.
    /// </para>
    /// <para>
    /// This matters because <see cref="ISharedSource"/> is otherwise bound at DI registration time.
    /// <c>ITelemetryService</c> subscribes to the switch once at construction and never notices a
    /// swap, so mode switching costs no restart. Recording is disabled while replaying.
    /// </para>
    /// <para>Implemented by agent B3.</para>
    /// </remarks>
    public sealed class SharedSourceSwitch : ISharedSource, IDisposable
    {
        /// <summary>
        /// Creates the switch, starting in live mode.
        /// </summary>
        /// <param name="liveSource">The live source, already registered as a hosted service.</param>
        /// <param name="replaySource">The file-backed source used for replay.</param>
        /// <param name="logger">Optional logger.</param>
        /// <remarks>Implemented by agent B3.</remarks>
        public SharedSourceSwitch(ISharedSource liveSource, FileSharedSource replaySource, ILogger<SharedSourceSwitch>? logger = null)
            => throw new NotImplementedException("Implemented by agent B3");

        /// <inheritdoc />
#pragma warning disable CS0067 // Events are never used - raised by agent B3
        public event Action<Shared>? DataUpdated;

        /// <inheritdoc />
        public event Action<ReadOnlyMemory<byte>>? RawFrameReceived;

        /// <inheritdoc />
        public event Action<int>? StartLightsChanged;

        /// <summary>Raised after the active source changes, so consumers can react to the mode swap.</summary>
        public event Action? ActiveSourceChanged;
#pragma warning restore CS0067

        /// <inheritdoc />
        public Shared Data { get; } = new();

        /// <summary>The source currently being forwarded.</summary>
        public ISharedSource Active { get; } = null!;

        /// <summary>The live source, regardless of which one is active.</summary>
        public ISharedSource Live { get; } = null!;

        /// <summary>The file-backed replay source, regardless of which one is active.</summary>
        public FileSharedSource Replay { get; } = null!;

        /// <summary><see langword="true"/> while the replay source is active.</summary>
        public bool IsReplaying { get; }

        /// <summary>
        /// Switches back to the live source. Unloads the replay file and raises the reset path so
        /// accumulated feature state is discarded.
        /// </summary>
        /// <remarks>Implemented by agent B3.</remarks>
        public void ActivateLive()
            => throw new NotImplementedException("Implemented by agent B3");

        /// <summary>
        /// Loads a recording and switches to the replay source. Raises the reset path so accumulated
        /// feature state is discarded.
        /// </summary>
        /// <param name="path">Path to a <c>.yhtl</c> file.</param>
        /// <remarks>Implemented by agent B3.</remarks>
        public void ActivateReplay(string path)
            => throw new NotImplementedException("Implemented by agent B3");

        /// <inheritdoc />
        /// <remarks>Implemented by agent B3.</remarks>
        public void Dispose()
            => throw new NotImplementedException("Implemented by agent B3");
    }
}
