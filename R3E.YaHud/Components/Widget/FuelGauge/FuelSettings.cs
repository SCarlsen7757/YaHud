using R3E.YaHud.Components.Widget.Core;

namespace R3E.YaHud.Components.Widget.FuelGauge
{
    public class FuelSettings : BasicSettings
    {
        private string Red { get; set; } = "rgb(255, 0, 0)";
        private string Yellow { get; set; } = "rgb(255, 251, 0)";
        private string Orange { get; set; } = "rgb(255, 102, 0)";
        private string Green { get; set; } = "rgb(0, 128, 0)";
        private string DimmedYellow { get; set; } = "rgb(127, 127, 43)";
        public string FuelPerLapColor =>  DimmedYellow;
        public string LapsEstimatedLeftColor =>  Green;
        public string FuelToAddColor => Green;
        public string LastLapFuelUsageColor => DimmedYellow;
        public string ColorHigh => Green;
        public string ColorMidHigh => Yellow;
        public string ColorMidLow => Orange;
        public string ColorLow => Green;

    }
}
