using R3E.Core.Recording;

namespace R3E.YaHud.Components.UI.Components.Settings
{
    /// <summary>
    /// One <c>.yhtl</c> file in the recordings browser, described by its header alone.
    /// </summary>
    /// <param name="Path">Absolute path to the file.</param>
    /// <param name="FileName">File name, as shown in the list.</param>
    /// <param name="SizeBytes">Size on disk.</param>
    /// <param name="Header">The parsed header, or <see langword="null"/> when it could not be read.</param>
    /// <param name="Error">Why the header could not be read, or <see langword="null"/> when it could.</param>
    public sealed record RecordingFileEntry(
        string Path,
        string FileName,
        long SizeBytes,
        TelemetryRecordingHeader? Header,
        string? Error);

    /// <summary>One recording run folder, holding the session files of a single stint.</summary>
    /// <param name="Name">Folder name, as shown in the list.</param>
    /// <param name="Path">Absolute path to the folder.</param>
    /// <param name="Files">The run's session files, ordered by name.</param>
    public sealed record RecordingRunEntry(string Name, string Path, IReadOnlyList<RecordingFileEntry> Files);

    /// <summary>
    /// Everything the browser can only learn by opening the file: the footer summary.
    /// </summary>
    /// <param name="Duration">Total recorded duration.</param>
    /// <param name="TotalFrames">Frame records in the file.</param>
    /// <param name="MaxNumCars">Highest driver count observed over the whole session.</param>
    /// <param name="ClassIds">Every car class observed.</param>
    /// <param name="LapCount">Number of lap-start markers.</param>
    /// <param name="IndexWasRebuilt">The footer was missing, so the recording was cut short by a crash.</param>
    public sealed record RecordingDetails(
        TimeSpan Duration,
        long TotalFrames,
        int MaxNumCars,
        IReadOnlyList<int> ClassIds,
        int LapCount,
        bool IndexWasRebuilt);

    /// <summary>
    /// Enumerates the recordings directory for the settings-panel browser.
    /// </summary>
    /// <remarks>
    /// Listing uses <see cref="TelemetryRecordingHeader.ReadFrom(string)"/>, which is header-only:
    /// a whole folder can be described without decoding a single block. The footer summary — the
    /// duration and the observed class set — is only read for the file the user actually selects.
    /// </remarks>
    public static class RecordingsCatalog
    {
        /// <summary>Name given to the pseudo-run holding files that sit loose in the root.</summary>
        private const string LooseFilesRunName = "(loose files)";

        /// <summary>
        /// Scans a recordings root for runs and their session files.
        /// </summary>
        /// <param name="root">The recordings directory. It need not exist.</param>
        /// <returns>The runs found, newest first. Empty when the directory does not exist.</returns>
        public static IReadOnlyList<RecordingRunEntry> Scan(string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return [];
            }

            var runs = new List<RecordingRunEntry>();

            try
            {
                foreach (var folder in Directory.EnumerateDirectories(root).OrderDescending(StringComparer.Ordinal))
                {
                    var files = ScanFolder(folder);
                    if (files.Count > 0)
                    {
                        runs.Add(new RecordingRunEntry(Path.GetFileName(folder), folder, files));
                    }
                }

                // A --recording-dir pointed straight at a folder of files, or files moved by hand,
                // should still be listed rather than silently ignored.
                var loose = ScanFolder(root);
                if (loose.Count > 0)
                {
                    runs.Add(new RecordingRunEntry(LooseFilesRunName, root, loose));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A directory that cannot be walked reads as "no recordings" rather than a crashed
                // settings panel; the browser shows the resolved path so the cause is visible.
                return runs;
            }

            return runs;
        }

        /// <summary>
        /// Reads the footer summary of a single recording.
        /// </summary>
        /// <param name="path">Path to a <c>.yhtl</c> file.</param>
        /// <param name="details">The summary, when it could be read.</param>
        /// <param name="error">Why it could not be read, when it could not.</param>
        /// <returns><see langword="true"/> when <paramref name="details"/> was produced.</returns>
        public static bool TryReadDetails(string path, out RecordingDetails? details, out string? error)
        {
            details = null;
            error = null;

            try
            {
                using var reader = new TelemetryRecordingReader(path);
                details = new RecordingDetails(
                    reader.Duration,
                    reader.TotalFrames,
                    reader.MaxNumCars,
                    reader.ClassIds,
                    reader.Markers.Count(m => m.Type == RecordingMarkerType.LapStart),
                    reader.IndexWasRebuilt);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static List<RecordingFileEntry> ScanFolder(string folder)
        {
            var files = new List<RecordingFileEntry>();

            foreach (var path in Directory
                .EnumerateFiles(folder, "*" + TelemetryRecordingHeader.FileExtension)
                .Order(StringComparer.Ordinal))
            {
                files.Add(Describe(path));
            }

            return files;
        }

        private static RecordingFileEntry Describe(string path)
        {
            var name = Path.GetFileName(path);
            long size = 0;

            try
            {
                size = new FileInfo(path).Length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Size is cosmetic; a locked file still gets a row.
            }

            try
            {
                return new RecordingFileEntry(path, name, size, TelemetryRecordingHeader.ReadFrom(path), null);
            }
            catch (Exception ex)
            {
                // A version mismatch throws with a message written to be shown to a user. Surface it
                // on the row rather than letting it take the panel down.
                return new RecordingFileEntry(path, name, size, null, ex.Message);
            }
        }
    }
}
