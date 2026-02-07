using R3E.Core.Services;
using R3E.Data;

namespace R3E.Features.TireWidget
{
    public class TireWidgetData
    {
        private readonly TelemetryData telemetryData;
        public Shared Raw => telemetryData.Raw;

        public int FrontTireAge { get; internal set; }
        public int RearTireAge { get; internal set; } 

        public float FrontLeftTireTemp { get; internal set; }
        public float FrontRightTireTemp { get; internal set; }
        public float RearLeftTireTemp { get; internal set; }
        public float RearRightTireTemp { get; internal set; }

        public TireData<float> TireWear { get; internal set; }



        public TireWidgetData(TelemetryData telemetryData) 
        { 
            this.telemetryData = telemetryData;
            TireWear = new TireData<float>();
        }
    }
}
