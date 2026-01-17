namespace R3E.Features.Driver
{
    /// <summary>
    /// Represents a driver relative to the player with calculated gaps.
    /// </summary>
    public readonly record struct DriverInfo
    {
        public Data.DriverData DriverData { get; init; }
        public bool IsPlayer { get; init; }
        public int LapDifference { get; init; }
        public float DistanceGap { get; init; }
        public TimeSpan TimeGap { get; init; }
    }
}
