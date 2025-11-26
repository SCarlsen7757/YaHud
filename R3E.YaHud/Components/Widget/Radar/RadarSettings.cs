using R3E.YaHud.Components.Widget.Core;
using R3E.YaHud.Services.Settings;


namespace R3E.YaHud.Components.Widget.Radar
{
    public class RadarSettings : BasicSettings
    {
        public int RadarRange { get; set; } = 12;
        public float RadarOpacity { get; set; } = 0.8f;
        public int RadarFadeRange { get; set; } = 2;
        public string RadarGridSvgPath => $"/img/radar/radar-grid.png";
    }
}
