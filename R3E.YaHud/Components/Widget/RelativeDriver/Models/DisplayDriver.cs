namespace R3E.YaHud.Components.Widget.RelativeDriver.Models
{
    internal readonly record struct DisplayDriver
    {
        public string Name { get; init; }
        public int CarNumber { get; init; }
        public float Rating { get; init; }
        public float Reputation { get; init; }
        public TimeSpan TimeGap { get; init; }
        public float DistanceGap { get; init; }
        public bool IsPlayer { get; init; }
        public int LapDifference { get; init; }
        public bool IsOutLap { get; init; }
        public bool IsInvalidLap { get; init; }
        public string ClassImageUrl { get; init; }
        public string ManufacturerImageUrl { get; init; }
    }
}
