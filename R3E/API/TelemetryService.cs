using R3E.Data;

namespace R3E.API
{
    public class TelemetryService : ITelemetryService, IDisposable, IAsyncDisposable
    {
        private readonly ISharedSource sharedSource;
        private readonly ILogger<TelemetryService> logger;
        private bool disposed;

        public event Action<TelemetryData>? DataUpdated;
        public event Action<TelemetryData>? NewLap;
        public event Action<TelemetryData>? NewSession;
        public event Action<TelemetryData>? CarPositionChanged;
        public event Action<TelemetryData>? TrackChanged;
        public event Action<TelemetryData>? CarChanged;

        private readonly TelemetryData data = new();
        public TelemetryData Data { get => data; }

        private int lastTick = 0;
        private int lastLapNumber = -1;
        private Constant.Session lastSessionType = Constant.Session.Unavailable;
        private int trackId = 0;
        private int carId = 0;


        public TelemetryService(ILogger<TelemetryService> logger,
                                ISharedSource sharedSource)
        {
            this.logger = logger;
            this.sharedSource = sharedSource;
            Data.Raw = sharedSource.Data;
            sharedSource.DataUpdated += OnRawDataUpdated;
        }

        private void OnRawDataUpdated(Shared raw)
        {
            Data.Raw = raw;

            var tick = raw.Player.GameSimulationTicks;
            var sessionType = (Constant.Session)raw.SessionType;
            var completedLaps = raw.CompletedLaps;

            if (sessionType != lastSessionType || tick < lastTick)
            {
                lastSessionType = sessionType;
                lastLapNumber = -1;
                this.logger.LogInformation("New session detected: {SessionType}", lastSessionType);
                NewSession?.Invoke(Data);
            }
            else if (completedLaps != lastLapNumber)
            {
                lastLapNumber = completedLaps;
                if (lastLapNumber > 0)
                {
                    this.logger.LogInformation("New lap detected: {LapNumber}", lastLapNumber);
                    NewLap?.Invoke(Data);
                }
            }
            lastTick = tick;

            var trackId = raw.TrackId;
            if (trackId != this.trackId)
            {
                this.trackId = trackId;
                this.logger.LogInformation("Track changed detected: {TrackId}", trackId);
                TrackChanged?.Invoke(Data);
            }

            var carId = raw.VehicleInfo.CarNumber;
            if (carId != this.carId)
            {
                this.carId = carId;
                this.logger.LogInformation("Car changed detected: {CarId}", carId);
                CarChanged?.Invoke(Data);
            }

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

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}