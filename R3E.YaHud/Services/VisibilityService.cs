using Microsoft.Extensions.Logging.Abstractions;

namespace R3E.YaHud.Services
{
    /// <summary>
    /// Service for managing the visibility of all widgets.
    /// Unlike conditional rendering, this approach keeps widgets in the DOM but hides them with CSS,
    /// preserving component state, event handlers, and DotNet object references.
    /// </summary>
    public class VisibilityService : IDisposable
    {
        private readonly ILogger<VisibilityService> logger;
        private bool disposed;
        private bool widgetsVisible = true;

        /// <summary>
        /// Event raised when widget visibility state changes.
        /// </summary>
        public event Action<bool>? OnVisibilityChanged;

        /// <summary>
        /// Gets whether widgets are currently visible.
        /// </summary>
        public bool WidgetsVisible => widgetsVisible;

        public VisibilityService(ILogger<VisibilityService>? logger = null)
        {
            this.logger = logger ?? NullLogger<VisibilityService>.Instance;
            this.logger.LogDebug("VisibilityService initialized");
        }

        /// <summary>
        /// Toggles the visibility of all widgets.
        /// </summary>
        public void ToggleVisibility()
        {
            SetVisibility(!widgetsVisible);
        }

        /// <summary>
        /// Sets the visibility state of all widgets.
        /// </summary>
        /// <param name="visible">True to show widgets, false to hide them.</param>
        public void SetVisibility(bool visible)
        {
            if (widgetsVisible == visible) return;

            widgetsVisible = visible;
            logger.LogInformation("Widgets visibility set to {State}", visible ? "visible" : "hidden");
            OnVisibilityChanged?.Invoke(widgetsVisible);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            OnVisibilityChanged = null;
            logger.LogDebug("VisibilityService disposed");
            GC.SuppressFinalize(this);
        }
    }
}
