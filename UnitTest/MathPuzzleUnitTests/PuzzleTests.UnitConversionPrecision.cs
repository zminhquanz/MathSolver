using MathSolver.Services;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckUnitConversionExactFactors()
    {
        var cases = new (string Category, string From, string To, decimal Input, decimal Expected)[]
        {
            ("time", "hour", "minute", 1m, 60m),
            ("time", "hour", "second", 1m, 3600m),
            ("time", "minute", "second", 1m, 60m),
            ("time", "day", "hour", 1m, 24m),
            ("time", "week", "day", 1m, 7m),
            ("time", "week", "second", 1m, 604800m),
            ("time", "second", "hour", 3600m, 1m),
            ("time", "hour", "minute", 0.1m, 6m),
            ("length", "km", "m", 1.2345678901m, 1234.5678901m),
            ("mass", "kg", "g", 0.1m, 100m),
            ("area", "ha", "m2", 0.0000000001m, 0.000001m),
            ("volume", "m3", "cm3", 0.0000000001m, 0.0001m),
            ("volume", "m3", "mm3", 0.0000000001m, 0.1m),
            ("capacity", "l", "ml", 0.1m, 100m),
            ("speed", "kmh", "mps", 36m, 10m),
            ("speed", "knot", "kmh", 1m, 1.852m),
            ("speed", "kmh", "knot", 46.3m, 25m),
            ("speed", "kmh", "knot", 46300000000000000000000000000m, 25000000000000000000000000000m),
            ("speed", "kmh", "knot", -46300000000000000000000000000m, -25000000000000000000000000000m),
            ("temperature", "celsius", "fahrenheit", 0m, 32m),
            ("temperature", "fahrenheit", "celsius", 32m, 0m),
            ("temperature", "kelvin", "celsius", 273.15m, 0m),
            ("temperature", "celsius", "fahrenheit", -40m, -40m)
        };
        foreach (var c in cases)
        {
            var units = MeasurementEngine.GetCategory(c.Category).Units;
            decimal actual = MeasurementEngine.Convert(c.Input, units.Single(u => u.Id == c.From), units.Single(u => u.Id == c.To));
            Require(actual == c.Expected, $"Conversion {c.Input} {c.From} → {c.To}: expected {c.Expected}, got {actual}");
        }
        foreach (var category in MeasurementEngine.Categories)
        foreach (var unit in category.Units)
            Require(MeasurementEngine.Convert(decimal.MaxValue, unit, unit) == decimal.MaxValue,
                "An unchanged unit must not overflow or round through a base unit: " + unit.Id);
        var length = MeasurementEngine.GetCategory("length").Units;
        Require(!MeasurementEngine.TryConvert(decimal.MaxValue, length.Single(u => u.Id == "km"),
            length.Single(u => u.Id == "mm"), out _), "A genuinely overflowing conversion must fail");
    }

    internal static void CheckUnitConversionDecimalDisplay()
    {
        foreach (var (value, expected) in new (decimal, string)[]
        {
            (60m, "60"), (1m / 3m, "0.3333333333"),
            (1.23456789014m, "1.2345678901"), (1.23456789015m, "1.2345678902"),
            (-1.23456789015m, "-1.2345678902"), (0.00000000001m, "0"),
            (-0.00000000001m, "0"), (0.00000000005m, "0.0000000001"),
            (59.999999999999999999999999532m, "60"), (1234.5m, "1,234.5")
        })
            Require(MeasurementEngine.FormatValue(value) == expected,
                $"Conversion display {value}: expected {expected}, got {MeasurementEngine.FormatValue(value)}");
    }

    internal static void CheckUnitConversionInputPrecision()
    {
        var previous = AppLanguageManager.CurrentLanguage;
        try
        {
            foreach (var language in Enum.GetValues<AppLanguage>())
            {
                AppLanguageManager.CurrentLanguage = language;
                foreach (var (text, expected) in new (string, decimal)[]
                {
                    ("0.1234567890", 0.123456789m), ("0,1234567890", 0.123456789m),
                    ("0,123", 0.123m), ("0,001", 0.001m), ("-0,123", -0.123m),
                    ("1,234.5678901234", 1234.5678901234m),
                    ("1.234,5678901234", 1234.5678901234m),
                    ("1e-10", 0.0000000001m), ("1.23456789012e1", 12.3456789012m),
                    ("-40", -40m), ("1,000", 1000m)
                })
                    Require(MeasurementEngine.TryParseInput(text, out decimal actual) && actual == expected,
                        "Valid conversion input rejected/changed: " + language + "/" + text);
                foreach (string invalid in new[] { "0.12345678901", "0,12345678901", "1e-11", "1.12345678901e-1",
                    "0.12345678901234567890123456789", "12345678901234567890.1234567891",
                    "1.2.3", "1,2,3", "NaN", "Infinity" })
                    Require(!MeasurementEngine.TryParseInput(invalid, out _), "Invalid/overprecision conversion input accepted: " + language + "/" + invalid);
            }
        }
        finally { AppLanguageManager.CurrentLanguage = previous; }
    }
}
