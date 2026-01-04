using System.Numerics;

namespace R3E.YaHud.Components.UI.Helpers
{
    /// <summary>
    /// Lightweight per-slot state container used by the Radar widget.
    /// Holds the computed relative position/orientation for a driver and the original telemetry payload.
    /// The widget updates instances of this class each frame; it is intended as a short-lived, mutable holder
    /// for display calculations only.
    /// </summary>
    public class DriverState {
        /// <summary>
        /// Position of the other driver relative to the player, in the radar coordinate frame.
        /// Units: meters. This value is typically produced by rotating world positions by the player's orientation.
        /// Example: used to compute translate3d(x,y,0) for the marker.
        /// </summary>
        public Vector3 RelativePos { get; set; }

        /// <summary>
        /// Relative orientation of the other driver (as a Vector3 to match telemetry vectors).
        /// Units: radians. Use the Y component for yaw when rendering the car rotation.
        /// </summary>
        public Vector3 RelativeOri { get; set; }

        /// <summary>
        /// Original telemetry payload for this driver (may be null if not yet populated).
        /// The widget reads properties such as <c>DriverInfo</c>, <c>CarWidth</c>, <c>CarLength</c>, and <c>CarSpeed</c>.
        /// </summary>
        public R3E.Data.DriverData DriverData { get; set; } 
    } 

}
