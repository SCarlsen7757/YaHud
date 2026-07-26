namespace R3E.Core.Interfaces
{
    /// <summary>
    /// A <see cref="ISharedSource"/> that can swap the underlying source at runtime, so the HUD can
    /// move between live telemetry and a replayed recording without restarting.
    /// </summary>
    /// <remarks>
    /// Consumers that accumulate state across frames need to know a swap happened: the two sources
    /// are unrelated streams, so everything derived from the old one is stale. The swap deliberately
    /// carries no frame with it - fabricating a zeroed <c>Shared</c> to force a discontinuity would
    /// push a zero session type and zero track length through every feature service, which is the
    /// exact "plausible but wrong" failure this reset exists to prevent.
    /// </remarks>
    public interface ISwitchableSharedSource : ISharedSource
    {
        /// <summary>Raised after the active source has been swapped, before any frame from it is forwarded.</summary>
        event Action? ActiveSourceChanged;

        /// <summary>Whether the active source is currently a recording rather than live telemetry.</summary>
        bool IsReplaying { get; }
    }
}
