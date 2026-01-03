using R3E.YaHud.Components.Widget.Core;

namespace R3E.YaHud.Components.Widget.FuelCalc
{
    public class FuelSettings : BasicSettings
    {
        private const string Red  = "rgb(255, 0, 0)";
        private const string Yellow  = "rgb(255, 251, 0)";
        private const string Orange = "rgb(255, 102, 0)";
        private const string Green  = "rgb(0, 128, 0)";
        private const string DimmedYellow = "rgb(127, 127, 43)";
        public string FuelPerLapColor =>  DimmedYellow;
        public string LapsEstimatedLeftColor =>  Green;
        public string FuelToAddColor => Green;
        public string LastLapFuelUsageColor => DimmedYellow;
        public string ColorHigh => Green;
        public string ColorMidHigh => Yellow;
        public string ColorMidLow => Orange;
        public string ColorLow => Red;

    }
}
