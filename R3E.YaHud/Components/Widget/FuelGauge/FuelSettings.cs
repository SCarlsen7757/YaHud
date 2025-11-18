using R3E.YaHud.Components.Widget.Core;

namespace R3E.YaHud.Components.Widget.FuelGauge
{
    public class FuelSettings : BasicSettings
    {
        public string red { get; set; } = "rgb(255, 0, 0)";
        public string yellow { get; set; } = "rgb(255, 251, 0)";
        public string orange { get; set; } = "rgb(255, 102, 0)";
        public string green { get; set; } = "rgb(0, 128, 0)";
    }
}
