namespace R3E.Converters
{
    public static class UnitConverter
    {
        // Speed conversions (canonical base: meters per second)
        public static double Convert(double value, SpeedUnit from, SpeedUnit to)
        {
            if (from == to) return value;

            // convert to base (meters/second)
            double baseValue = from switch
            {
                SpeedUnit.MetersPerSecond => value,
                SpeedUnit.KilometersPerHour => value / 3.6,
                SpeedUnit.MilesPerHour => value / 2.2369362920544, // 1 m/s = ~2.2369362920544 mph
                _ => throw new InvalidOperationException($"Unsupported speed unit: {from}")
            };

            // convert from base to target
            return to switch
            {
                SpeedUnit.MetersPerSecond => baseValue,
                SpeedUnit.KilometersPerHour => baseValue * 3.6,
                SpeedUnit.MilesPerHour => baseValue * 2.2369362920544,
                _ => throw new InvalidOperationException($"Unsupported speed unit: {to}")
            };
        }

        // Angular conversions (canonical base: radians per second)
        public static double Convert(double value, AngularUnit from, AngularUnit to)
        {
            if (from == to) return value;

            // to base (rad/s)
            double baseValue = from switch
            {
                AngularUnit.RadiansPerSecond => value,
                AngularUnit.RevolutionsPerMinute => value * (Math.PI / 30.0), // rpm -> rad/s
                _ => throw new InvalidOperationException($"Unsupported angular unit: {from}")
            };

            // base -> target
            return to switch
            {
                AngularUnit.RadiansPerSecond => baseValue,
                AngularUnit.RevolutionsPerMinute => baseValue * (30.0 / Math.PI), // rad/s -> rpm
                _ => throw new InvalidOperationException($"Unsupported angular unit: {to}")
            };
        }

        // Temperature conversions (canonical base: Celsius)
        public static double Convert(double value, TemperatureUnit from, TemperatureUnit to)
        {
            if (from == to) return value;

            // to base (Celsius)
            double baseValue = from switch
            {
                TemperatureUnit.Celsius => value,
                TemperatureUnit.Fahrenheit => (value - 32.0) * 5.0 / 9.0,
                _ => throw new InvalidOperationException($"Unsupported temperature unit: {from}")
            };

            // base -> target
            return to switch
            {
                TemperatureUnit.Celsius => baseValue,
                TemperatureUnit.Fahrenheit => baseValue * 9.0 / 5.0 + 32.0,
                _ => throw new InvalidOperationException($"Unsupported temperature unit: {to}")
            };
        }
    }
}
