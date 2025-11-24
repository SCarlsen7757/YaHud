using R3E.Data;
using R3E.Extensions;

namespace R3E.API
{
    public class TelemetryService : ITelemetryService, IDisposable, IAsyncDisposable
    {
        private readonly ISharedSource sharedSource;
        private readonly ILogger<TelemetryService> logger;
        private bool disposed;

        public event Action<TelemetryData>? DataUpdated;
        public event Action<TelemetryData>? NewLap;
        public event Action<TelemetryData>? SessionTypeChanged;
        public event Action<TelemetryData>? SessionPhaseChanged;
        public event Action<TelemetryData>? CarPositionChanged;
        public event Action<TelemetryData>? TrackChanged;
        public event Action<TelemetryData>? CarChanged;

        private readonly TelemetryData data = new();
        public TelemetryData Data { get => data; }

        private int lastTick = 0;
        private int lastLapNumber = 0;
        private Constant.Session lastSessionType = Constant.Session.Unavailable;
        private Constant.SessionPhase sessionPhase = Constant.SessionPhase.Unavailable;
        private int trackId = 0;
        private int carId = 0;
        private int playerPosition = 0;


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
            if (sessionType != lastSessionType || tick < lastTick)
            {
                lastSessionType = sessionType;
                lastLapNumber = -1;
                this.logger.LogInformation("New session detected: {SessionType}", lastSessionType);
                SessionTypeChanged?.Invoke(Data);
            }
            lastTick = tick;

            var sessionPhase = (Constant.SessionPhase)raw.SessionPhase;
            if (sessionPhase != this.sessionPhase)
            {
                this.sessionPhase = sessionPhase;
                if (sessionPhase == Constant.SessionPhase.Green)
                {
                    Data.PlayerStartPosition = raw.Position;
                }
                playerPosition = raw.Position;
                this.logger.LogInformation("Session phase changed: {SessionPhase}", this.sessionPhase);
                SessionPhaseChanged?.Invoke(Data);
            }

            var completedLaps = raw.CompletedLaps;
            if (completedLaps != lastLapNumber)
            {
                lastLapNumber = completedLaps;
                if (lastLapNumber > 0)
                {
                    this.logger.LogInformation("Starting lap number: {LapNumber}", lastLapNumber + 1);
                    NewLap?.Invoke(Data);
                }
            }

            var position = raw.Position;
            if (position != playerPosition)
            {
                playerPosition = position;
                this.logger.LogInformation("Player position changed: {Position}", position);
                CarPositionChanged?.Invoke(Data);
            }

            var trackId = raw.TrackId;
            if (trackId != this.trackId && trackId > 0)
            {
                this.trackId = trackId;
                this.logger.LogInformation("Track changed detected. ID: {TrackId}, Name: {TrackName}", trackId, raw.TrackName.ToNullTerminatedString());
                TrackChanged?.Invoke(Data);
            }

            var carId = raw.VehicleInfo.CarNumber;
            if (carId != this.carId && carId > 0)
            {
                this.carId = carId;
                this.logger.LogInformation("Car changed detected. ID: {CarId}, Name: {CarName}", carId, raw.VehicleInfo.Name.ToNullTerminatedString());
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