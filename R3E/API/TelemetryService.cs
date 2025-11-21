using R3E.Data;

namespace R3E.API
{
    public class TelemetryService : IDisposable
    {
        private readonly ISharedSource sharedSource;
        private readonly DataPointService dataPointService;
        private readonly ILogger<TelemetryService> logger;
        private bool disposed;

        public event Action<TelemetryData>? DataUpdated;

        private readonly TelemetryData data = new();
        public TelemetryData Data { get => data; }
        public DataPointService DataPoints { get => dataPointService; }

        public TelemetryService(ILogger<TelemetryService> logger,
                                ISharedSource sharedSource,
                                DataPointService dataPointService)
        {
            this.logger = logger;
            this.sharedSource = sharedSource;
            this.dataPointService = dataPointService;
            Data.Raw = sharedSource.Data;
            Data.SetDataPointService(dataPointService);
            sharedSource.DataUpdated += OnRawDataUpdated;
        }

        private void OnRawDataUpdated(Shared raw)
        {
            Data.Raw = raw;
            DataUpdated?.Invoke(Data);
            logger.LogDebug("Telemetry data updated");
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            sharedSource.DataUpdated -= OnRawDataUpdated;
            GC.SuppressFinalize(this);
        }
    }
}