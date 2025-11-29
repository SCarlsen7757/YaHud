using R3E.Data;

namespace R3E.API
{
    public interface ISharedSource
    {
        /// <summary>
        /// Occurs when the shared data is updated.
        /// </summary>
        /// <remarks>Subscribers are notified whenever the underlying shared data changes. The event
        /// provides the updated <see cref="Shared"/> instance as an argument. Event handlers should be thread-safe if
        /// the shared data can be updated from multiple threads.</remarks>
        event Action<Shared>? DataUpdated;

        /// <summary>
        /// Occurs when the number of start lights changes.
        /// </summary>
        /// <remarks>Subscribers receive the new count of start lights as an integer parameter. This event
        /// is typically raised when the start light configuration is updated or when the system detects a change in the
        /// number of available start lights.</remarks>
        event Action<int>? StartLightsChanged;

        /// <summary>
        /// Gets the shared data associated with this instance.
        /// </summary>
        Shared Data { get; }
    }
}
