using System.CommandLine;

namespace R3E.Utilities
{
    /// <summary>
    /// Launch argument definitions shared by the HUD and the relay, so the two ends of the
    /// telemetry link cannot drift apart on naming, defaults or validation.
    /// </summary>
    public static class LaunchOptions
    {
        public const int MinPort = 1;
        public const int MaxPort = 65535;

        /// <summary>
        /// Creates a port option that only accepts a valid TCP/UDP port number.
        /// </summary>
        public static Option<int> CreatePortOption(string name, string description, int defaultPort)
            => CreateIntOption(name, description, defaultPort, MinPort, MaxPort, "port");

        /// <summary>
        /// Creates an integer option that only accepts a value inside
        /// <paramref name="min"/>..<paramref name="max"/>.
        /// </summary>
        public static Option<int> CreateIntOption(string name, string description, int defaultValue, int min, int max, string helpName)
        {
            var option = new Option<int>(name)
            {
                Description = description,
                DefaultValueFactory = _ => defaultValue,
                HelpName = helpName,
            };

            option.Validators.Add(result =>
            {
                // Validate the raw token rather than the converted value: reading a value that
                // failed conversion (e.g. --web-port=abc) throws instead of reporting an error.
                var token = result.Tokens.Count > 0 ? result.Tokens[0].Value : null;
                if (token is null)
                {
                    return;
                }

                if (!int.TryParse(token, out var value))
                {
                    result.AddError($"{name} must be a whole number between {min} and {max}, but was '{token}'.");
                }
                else if (value < min || value > max)
                {
                    result.AddError($"{name} must be between {min} and {max}, but was {value}.");
                }
            });

            return option;
        }

        /// <summary>
        /// Validates a port that came from configuration rather than the command line, so a typo in
        /// appsettings.json reports the same friendly error instead of an unhandled conversion
        /// exception. An absent or empty value falls back to <paramref name="defaultPort"/>.
        /// </summary>
        public static bool TryReadPort(string? rawValue, string source, int defaultPort, out int port, out string? error)
        {
            error = null;
            port = defaultPort;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return true;
            }

            if (!int.TryParse(rawValue, out var value))
            {
                error = $"{source} must be a whole number between {MinPort} and {MaxPort}, but was '{rawValue}'.";
                return false;
            }

            if (value is < MinPort or > MaxPort)
            {
                error = $"{source} must be between {MinPort} and {MaxPort}, but was {value}.";
                return false;
            }

            port = value;
            return true;
        }

        /// <summary>
        /// Validates an integer that came from configuration rather than the command line, so a typo
        /// in appsettings.json reports the same friendly error instead of an unhandled conversion
        /// exception. An absent or empty value falls back to <paramref name="defaultValue"/>.
        /// </summary>
        public static bool TryReadInt(string? rawValue, string source, int defaultValue, int min, int max, out int value, out string? error)
        {
            error = null;
            value = defaultValue;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return true;
            }

            if (!int.TryParse(rawValue, out var parsed))
            {
                error = $"{source} must be a whole number between {min} and {max}, but was '{rawValue}'.";
                return false;
            }

            if (parsed < min || parsed > max)
            {
                error = $"{source} must be between {min} and {max}, but was {parsed}.";
                return false;
            }

            value = parsed;
            return true;
        }

        /// <summary>
        /// Validates a boolean that came from configuration rather than the command line, for the
        /// same reason as <see cref="TryReadInt"/>: a typo such as <c>"Enabled": "yess"</c> would
        /// otherwise bind to <see langword="false"/> silently or throw. An absent or empty value
        /// falls back to <paramref name="defaultValue"/>.
        /// </summary>
        public static bool TryReadBool(string? rawValue, string source, bool defaultValue, out bool value, out string? error)
        {
            error = null;
            value = defaultValue;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return true;
            }

            if (!bool.TryParse(rawValue, out var parsed))
            {
                error = $"{source} must be true or false, but was '{rawValue}'.";
                return false;
            }

            value = parsed;
            return true;
        }
    }
}
