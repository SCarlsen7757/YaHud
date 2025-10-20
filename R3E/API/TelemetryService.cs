using R3E.Data;

namespace R3E.API
{
    public class TelemetryService : IDisposable, IAsyncDisposable
    {
        private readonly SharedMemoryService sharedMemoryService;
        private readonly Microsoft.Extensions.Logging.ILogger<TelemetryService> logger;

        public event Action<TelemetryData>? DataUpdated;

        public TelemetryData Data { get; private set; }

        public TelemetryService(SharedMemoryService sharedMemoryService, Microsoft.Extensions.Logging.ILogger<TelemetryService>? logger = null)
        {
            this.sharedMemoryService = sharedMemoryService ?? throw new ArgumentNullException(nameof(sharedMemoryService));
            this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TelemetryService>.Instance;
            Data = new TelemetryData(sharedMemoryService.Data);
            sharedMemoryService.DataUpdated += OnRawDataUpdated;
        }

        private void OnRawDataUpdated(Shared raw)
        {
            Data = new TelemetryData(raw);
            DataUpdated?.Invoke(Data);
            logger.LogDebug("Telemetry data updated");
        }

        public void Dispose()
        {
            sharedMemoryService.DataUpdated -= OnRawDataUpdated;
            GC.SuppressFinalize(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            GC.SuppressFinalize(this);
            return ValueTask.CompletedTask;
        }
    }
}