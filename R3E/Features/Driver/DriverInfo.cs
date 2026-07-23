namespace R3E.Features.Driver
{
    /// <summary>
    /// Represents a driver with all relevant data for display widgets.
    /// Contains raw telemetry data plus enriched/calculated fields.
    /// </summary>
    public readonly record struct DriverInfo
    {
        public string Name { get; init; }
        public int CarNumber { get; init; }
        public int Position { get; init; }
        public int ClassPosition { get; init; }
        public float Rating { get; init; }
        public float Reputation { get; init; }
        public Data.DriverData DriverData { get; init; }
        public bool IsPlayer { get; init; }
        public int LapDifference { get; init; }
        public float DistanceGap { get; init; }
        public TimeSpan TimeGap { get; init; }
        public TimeSpan BestLapTime { get; init; }
        public Constant.TireType TireTypeFront { get; init; }
        public Constant.TireType TireTypeRear { get; init; }
        public Constant.TireSubtype TireSubtypeFront { get; init; }
        public Constant.TireSubtype TireSubtypeRear { get; init; }
        public bool IsInPitLane { get; init; }
        public int NumPitStops { get; init; }
        public bool IsCurrentLapValid { get; init; }
        public string ManufacturerImageUrl { get; init; }
        public string ClassImageUrl { get; init; }
        public int ManufacturerId { get; init; }
        public int ClassId { get; init; }
    }
}
