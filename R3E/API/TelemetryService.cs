using R3E.Data;

namespace R3E.API
{
    public class TelemetryService : IDisposable, IAsyncDisposable
    {
        private readonly SharedMemoryService sharedMemoryService;

        public event Action<TelemetryData>? DataUpdated;

        public TelemetryData Data { get; private set; }

        public TelemetryService(SharedMemoryService sharedMemoryService)
        {
            this.sharedMemoryService = sharedMemoryService ?? throw new ArgumentNullException(nameof(sharedMemoryService));
            Data = new TelemetryData(sharedMemoryService.Data);
            sharedMemoryService.DataUpdated += OnRawDataUpdated;
        }

        private void OnRawDataUpdated(Shared raw)
        {
            Data = new TelemetryData(raw);
            DataUpdated?.Invoke(Data);
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