
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.Extensions.Logging;
using R3E.API.Models;
using R3E.Data;
using R3E.Extensions;
using R3E.YaHud.Components.Widget.Radar;

namespace R3E.API.Radar
{
    /// <summary>
    /// Processes telemetry updates and writes a simple snapshot into <see cref="RadarData"/>.
    /// Widgets should only read <see cref="RadarData"/>; no further processing should occur in UI.
    /// </summary>
    public class RadarService : IDisposable
    {
        private readonly ILogger<RadarService> logger;
        private readonly ITelemetryService telemetryService;
        private readonly RadarData radarData;
        private readonly object sync = new();

        private float trackLength = 0f;

        public RadarService(ILogger<RadarService> logger, ITelemetryService telemetryService, RadarData radarData)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            this.radarData = radarData ?? throw new ArgumentNullException(nameof(radarData));

            telemetryService.DataUpdated += OnDataUpdated;
            telemetryService.SessionTypeChanged += OnSessionTypeChanged;

            logger.LogInformation("RadarService initialized");
        }

        private void OnSessionTypeChanged(TelemetryData data)
        {
            lock (sync)
            {
                logger.LogInformation("RadarService: session changed - clearing radar state.");
                radarData.DriverStates = new Dictionary<int, RadarDriverSnapshot>();
                radarData.ClosestDistance = null;
                radarData.CloseLeft = false;
                radarData.CloseRight = false;
                radarData.LastUpdatedUtc = DateTime.UtcNow;

                if (data.Raw.LayoutLength > 0)
                {
                    trackLength = data.Raw.LayoutLength;
                }
            }
        }

        private void OnDataUpdated(TelemetryData data)
        {
            var raw = data.Raw;
            if (raw.DriverData == null) return;


            var rotationMatrix = RadarCalculator.RotationMatrixFromEuler(raw.Player.Orientation);

            var snapshot = new Dictionary<int, RadarDriverSnapshot>(raw.NumCars);

            double? closest = null;
            bool closeLeft = false;
            bool closeRight = false;

            // Find player driver entry
            var ownDriverCandidates = raw.DriverData.Where(d => d.DriverInfo.UserId == raw.Player.UserId).ToList();
            if (ownDriverCandidates.Count == 0)
            {
                // No player; write raw and empty snapshot
                lock (sync)
                {
                    radarData.Raw = raw;
                    radarData.DriverStates = snapshot;
                    radarData.ClosestDistance = null;
                    radarData.CloseLeft = false;
                    radarData.CloseRight = false;
                    radarData.LastUpdatedUtc = DateTime.UtcNow;
                }
                return;
            }

            var ownDriver = ownDriverCandidates[0];
            var ownPlace = ownDriver.Place;

            // Ensure entries for all slots
            foreach (var d in raw.DriverData)
            {
                int slot = d.DriverInfo.SlotId;
                if (slot < 0) continue;

                if (ownPlace != d.Place)
                {
                    // Compute vector from player to the other car (other.Position - own.Position),
                    // then rotate into player-local coordinates. Using the reverse sign produced
                    // opponents flipped to the wrong side.
                    var relPos = RadarCalculator.RotateVector(rotationMatrix, RadarCalculator.SubtractVector(d.Position, ownDriver.Position));

                    // FIX: rotate produced a mirrored X; flip X to match in-game left/right.
                    relPos = new Vector3(-relPos.X, relPos.Y, relPos.Z);

                    var relOri = RadarCalculator.SubtractVector(d.Orientation, ownDriver.Orientation);
                    double distance = RadarCalculator.DistanceFromZero(relPos);
                    double yaw = relOri.Y;

                    if (closest == null || distance < closest)
                        closest = distance;

                    if (relPos.X < 0 && RadarCalculator.IsCarClose(relPos.Z, relPos.X, d.DriverInfo.CarLength, d.DriverInfo.CarWidth))
                        closeLeft = true;
                    if (relPos.X > 0 && RadarCalculator.IsCarClose(relPos.Z, relPos.X, d.DriverInfo.CarLength, d.DriverInfo.CarWidth))
                        closeRight = true;

                    snapshot[slot] = new RadarDriverSnapshot
                    {
                        SlotId = slot,
                        RelativePos = relPos,
                        RelativeOri = new Vector3(relOri.X, relOri.Y, relOri.Z),
                        Distance = distance,
                        RotationYaw = yaw,
                        CarWidth = d.DriverInfo.CarWidth,
                        CarLength = d.DriverInfo.CarLength,
                        IsSelf = false,
                        DriverData = d
                    };
                }
                else
                {
                    // self
                    snapshot[slot] = new RadarDriverSnapshot
                    {
                        SlotId = slot,
                        RelativePos = Vector3.Zero,
                        RelativeOri = Vector3.Zero,
                        Distance = 0,
                        RotationYaw = 0,
                        CarWidth = d.DriverInfo.CarWidth,
                        CarLength = d.DriverInfo.CarLength,
                        IsSelf = true,
                        DriverData = d
                    };
                }
            }

            lock (sync)
            {
                radarData.Raw = raw;
                radarData.DriverStates = snapshot;
                radarData.ClosestDistance = closest;
                radarData.CloseLeft = closeLeft;
                radarData.CloseRight = closeRight;
                radarData.LastUpdatedUtc = DateTime.UtcNow;
            }
        }

        public void Dispose()
        {
            telemetryService.DataUpdated -= OnDataUpdated;
            telemetryService.SessionTypeChanged -= OnSessionTypeChanged;
            GC.SuppressFinalize(this);
        }

    }
}

