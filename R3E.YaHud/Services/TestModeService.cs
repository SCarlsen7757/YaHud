using Microsoft.Extensions.Logging.Abstractions;

namespace R3E.YaHud.Services
{
    /// <summary>
    /// Service for managing test mode state across the application.
    /// When test mode is active, widgets can display static test data instead of live telemetry.
    /// </summary>
    public class TestModeService : IDisposable
    {
        private readonly ILogger<TestModeService> logger;
        private bool disposed;

        /// <summary>
        /// Event raised when test mode state changes.
        /// </summary>
        public event Action<bool>? OnTestModeChanged;

        /// <summary>
        /// Gets the current test mode state.
        /// </summary>
        public bool TestMode { get; private set; }

        public TestModeService(ILogger<TestModeService>? logger = null)
        {
            this.logger = logger ?? NullLogger<TestModeService>.Instance;
            this.logger.LogDebug("TestModeService initialized");
        }

        /// <summary>
        /// Toggles test mode on/off.
        /// </summary>
        public void ToggleTestMode()
        {
            SetTestMode(!TestMode);
        }

        /// <summary>
        /// Sets the test mode state.
        /// </summary>
        /// <param name="enabled">True to enable test mode, false to disable.</param>
        public void SetTestMode(bool enabled)
        {
            if (TestMode == enabled) return;

            TestMode = enabled;
            logger.LogDebug("Test mode {State}", enabled ? "enabled" : "disabled");
            OnTestModeChanged?.Invoke(TestMode);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            logger.LogDebug("TestModeService disposed");
            GC.SuppressFinalize(this);
        }
    }
}
