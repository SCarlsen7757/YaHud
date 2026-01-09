using R3E.Core.Interfaces;
using R3E.Core.Services;

namespace R3E.Features.Sector
{
    public class SectorService : IDisposable, ISectorServiceEvents
    {
        private readonly ITelemetryService telemetry;
        private readonly ITelemetryEventBus eventBus;
        private readonly ILogger<SectorService> logger;

        public event Action<SectorData, int>? SectorCompleted;

        public SectorData SectorData { get; init; }
        private int lastSectorIndex = -1;

        public SectorService(
            ITelemetryService telemetry,
            ITelemetryEventBus eventBus,
            ILogger<SectorService> logger)
        {
            this.telemetry = telemetry;
            this.eventBus = eventBus;
            this.logger = logger;

            SectorData = new SectorData(telemetry.Data);

            // Subscribe to core telemetry events
            telemetry.DataUpdated += OnDataUpdated;
            telemetry.SessionTypeChanged += OnSessionChanged;
        }

        private void OnSessionChanged(TelemetryData data)
        {
            lastSectorIndex = -1;
        }

        private void OnDataUpdated(TelemetryData data)
        {
            if (lastSectorIndex != SectorData.CurrentSectorIndexSelf)
            {
                switch (SectorData.CurrentSectorIndexSelf)
                {
                    case 0:
                        this.logger.LogInformation("Sector Completed. Sector Index: 2");
                        PublishSectorCompleted(SectorData, 2);
                        break;
                    case 1:
                        this.logger.LogInformation("Sector Completed. Sector Index: 0");
                        PublishSectorCompleted(SectorData, 0);
                        break;
                    case 2:
                        this.logger.LogInformation("Sector Completed. Sector Index: 1");
                        PublishSectorCompleted(SectorData, 1);
                        break;
                }

                lastSectorIndex = SectorData.CurrentSectorIndexSelf;
            }
        }

        public void PublishSectorCompleted(SectorData sectorData, int sectorIndex)
        {
            SectorCompleted?.Invoke(sectorData, sectorIndex);
            eventBus.PublishSectorCompleted(sectorData, sectorIndex);
        }

        public void Dispose()
        {
            telemetry.DataUpdated -= OnDataUpdated;
            telemetry.SessionTypeChanged -= OnSessionChanged;
            GC.SuppressFinalize(this);
        }
    }
}
