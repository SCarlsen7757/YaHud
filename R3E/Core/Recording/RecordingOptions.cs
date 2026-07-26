namespace R3E.Core.Recording
{
    /// <summary>
    /// Recording settings, mapping one-to-one onto the <c>YaHud:Recording</c> configuration section
    /// and its launch-argument equivalents.
    /// </summary>
    /// <remarks>Populated by agent B5 from configuration and launch arguments.</remarks>
    public sealed class RecordingOptions
    {
        /// <summary>Configuration section these options bind to.</summary>
        public const string SectionName = "YaHud:Recording";

        /// <summary>Default compression block duration in seconds.</summary>
        public const int DefaultBlockSeconds = 3;

        /// <summary>
        /// Smallest accepted <see cref="BlockSeconds"/>. Below this the footer index grows faster
        /// than the seek granularity it buys, and every block restarts the Brotli dictionary.
        /// </summary>
        public const int MinBlockSeconds = 1;

        /// <summary>
        /// Largest accepted <see cref="BlockSeconds"/>. Above this a seek has to decode an
        /// unreasonable amount of data to reach its target.
        /// </summary>
        public const int MaxBlockSeconds = 60;

        /// <summary>
        /// Whether recording is available at all (<c>--record</c>). When false the recorder is not
        /// registered and the UI toggle is hidden. Opt-in so nobody writes multi-GB files by accident.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Begin recording at launch without pressing the button (<c>--record-autostart</c>), for
        /// scripted or headless capture. Implies <see cref="Enabled"/>.
        /// </summary>
        public bool AutoStart { get; set; }

        /// <summary>
        /// Output folder (<c>--recording-dir</c>). When null, defaults to
        /// <c>LocalApplicationData/YaHud/recordings</c>, which resolves on both Windows and Linux.
        /// </summary>
        public string? Directory { get; set; }

        /// <summary>Compression block duration in seconds (<c>--recording-block-seconds</c>).</summary>
        public int BlockSeconds { get; set; } = DefaultBlockSeconds;

        /// <summary>
        /// Resolves <see cref="Directory"/>, falling back to the platform default when unset.
        /// </summary>
        /// <returns>An absolute path to the recordings root folder.</returns>
        public string ResolveDirectory()
        {
            if (!string.IsNullOrWhiteSpace(Directory))
            {
                return System.IO.Path.GetFullPath(Directory);
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return System.IO.Path.Combine(localAppData, "YaHud", "recordings");
        }
    }
}
