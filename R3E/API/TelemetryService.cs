using R3E.Data;

namespace R3E.API
{
    public class TelemetryService : IDisposable, IAsyncDisposable
    {
        private readonly ISharedSource sharedSource;
        private readonly Microsoft.Extensions.Logging.ILogger<TelemetryService> logger;

        public event Action<TelemetryData>? DataUpdated;

        public TelemetryData Data { get; private set; }

        public TelemetryService(ISharedSource sharedSource, Microsoft.Extensions.Logging.ILogger<TelemetryService>? logger = null)
        {
            this.sharedSource = sharedSource ?? throw new ArgumentNullException(nameof(sharedSource));
            this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TelemetryService>.Instance;
            Data = new TelemetryData(sharedSource.Data);
            sharedSource.DataUpdated += OnRawDataUpdated;
        }

        private void OnRawDataUpdated(Shared raw)
        {
            Data = new TelemetryData(raw);
            DataUpdated?.Invoke(Data);
            logger.LogDebug("Telemetry data updated");
        }

        public void Dispose()
        {
            sharedSource.DataUpdated -= OnRawDataUpdated;
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