using R3E.Core.Services;
using R3E.Data;
using System.Numerics;

namespace R3E.Features.Radar
{
    /// <summary>
    /// Contains processed radar data for widgets. Widgets should only read this for rendering;
    /// no calculations should be done in the widget itself.
    /// </summary>
    public class RadarData
    {
        private readonly TelemetryData telemetryData;

        private Shared Raw => telemetryData.Raw;

        public RadarData(TelemetryData telemetryData)
        {
            this.telemetryData = telemetryData;
        }

        /// <summary>
        /// Snapshot of processed per-slot radar values (keyed by slot id).
        /// </summary>
        public IReadOnlyDictionary<int, RadarDriverSnapshot> DriverStates { get; internal set; } = new Dictionary<int, RadarDriverSnapshot>();

        /// <summary>
        /// Closest distance among drivers in the snapshot (meters), or null if none.
        /// </summary>
        public double? ClosestDistance { get; internal set; }

        public DateTime LastUpdatedUtc { get; internal set; }
    }

    public sealed class RadarDriverSnapshot
    {
        public int SlotId { get; init; }
        public Vector3 RelativePos { get; init; } = Vector3.Zero;   // X = left/right, Z = front/back
        public Vector3 RelativeOri { get; init; } = Vector3.Zero;
        public double Distance { get; init; }
        public double RotationYaw { get; init; } // radians
        public double CarWidth { get; init; }
        public double CarLength { get; init; }
        public bool IsSelf { get; init; }
        public bool CloseLeft { get; init; }
        public bool CloseRight { get; init; }
        public DriverData DriverData { get; init; } = default!;
    }
}