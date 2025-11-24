using R3E.API.TimeGap;
using R3E.Data;

namespace R3E.API
{
    public class TelemetryService : IDisposable
    {
        private readonly ISharedSource sharedSource;
        private readonly TimeGapService dataPointService;
        private readonly ILogger<TelemetryService> logger;
        private bool disposed;

        public event Action<TelemetryData>? DataUpdated;
        public event Action<TelemetryData>? NewLap;
        public event Action<TelemetryData>? NewSession;

        public TelemetryData Data { get; init; }
        public TimeGapService DataPoints { get => dataPointService; }

        public TelemetryService(ILogger<TelemetryService> logger,
                                ISharedSource sharedSource,
                                TimeGapService dataPointService)
        {
            this.logger = logger;
            this.sharedSource = sharedSource;
            this.dataPointService = dataPointService;
            Data = new TelemetryData(dataPointService);

            sharedSource.DataUpdated += OnRawDataUpdated;
        }

        private void OnRawDataUpdated(Shared raw)
        {
            Data.Raw = raw;
            DataUpdated?.Invoke(Data);
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