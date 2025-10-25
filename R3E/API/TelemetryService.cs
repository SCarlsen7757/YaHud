using R3E.Data;

namespace R3E.API
{
    public class TelemetryService : IDisposable, IAsyncDisposable
    {
        private readonly ISharedSource sharedSource;
        private readonly Microsoft.Extensions.Logging.ILogger<TelemetryService> logger;
        private bool disposed;

        public event Action<TelemetryData>? DataUpdated;

        private readonly TelemetryData data = new();
        public TelemetryData Data { get => data; }

        public TelemetryService(ISharedSource sharedSource, Microsoft.Extensions.Logging.ILogger<TelemetryService>? logger = null)
        {
            this.sharedSource = sharedSource ?? throw new ArgumentNullException(nameof(sharedSource));
            this.logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TelemetryService>.Instance;
            Data.Raw = sharedSource.Data;
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

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}