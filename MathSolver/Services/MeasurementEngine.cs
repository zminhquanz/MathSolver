using System.Globalization;

namespace MathSolver.Services;

/// <summary>
/// Central conversion engine for the Formula → Units tab.
/// Uses decimal arithmetic so school-level metric conversions stay exact.
/// Currency is intentionally excluded because it is locale/rate dependent.
/// </summary>
public static class MeasurementEngine
{
    public sealed record MeasurementUnit(
        string Id,
        string NameKey,
        string Symbol,
        Func<decimal, decimal> ToBase,
        Func<decimal, decimal> FromBase)
    {
        public string DisplayName =>
            $"{LocalizationService.TranslateKey(NameKey)} ({Symbol})";
    }

    public sealed record MeasurementCategory(
        string Id,
        string NameKey,
        string Icon,
        string BaseUnitId,
        string DefaultFromUnitId,
        string DefaultToUnitId,
        IReadOnlyList<MeasurementUnit> Units,
        string ReferenceKey)
    {
        public string DisplayName =>
            $"{Icon}  {LocalizationService.TranslateKey(NameKey)}";
    }

    private static MeasurementUnit Linear(
        string id,
        string nameKey,
        string symbol,
        decimal unitsPerBaseUnit)
    {
        if (unitsPerBaseUnit <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitsPerBaseUnit));
        }

        return new MeasurementUnit(
            id,
            nameKey,
            symbol,
            value => value / unitsPerBaseUnit,
            baseValue => baseValue * unitsPerBaseUnit);
    }

    private static readonly IReadOnlyList<MeasurementCategory> CategoriesValue =
    [
        new(
            "length",
            "Formula.Measurement.Category.Length",
            "↔",
            "m",
            "km",
            "m",
            [
                Linear("km", "Formula.Measurement.Unit.Kilometre", "km", 0.001m),
                Linear("hm", "Formula.Measurement.Unit.Hectometre", "hm", 0.01m),
                Linear("dam", "Formula.Measurement.Unit.Decametre", "dam", 0.1m),
                Linear("m", "Formula.Measurement.Unit.Metre", "m", 1m),
                Linear("dm", "Formula.Measurement.Unit.Decimetre", "dm", 10m),
                Linear("cm", "Formula.Measurement.Unit.Centimetre", "cm", 100m),
                Linear("mm", "Formula.Measurement.Unit.Millimetre", "mm", 1000m)
            ],
            "Formula.Measurement.Reference.Length"),

        new(
            "mass",
            "Formula.Measurement.Category.Mass",
            "⚖",
            "kg",
            "t",
            "kg",
            [
                Linear("t", "Formula.Measurement.Unit.Tonne", "t", 0.001m),
                Linear("q", "Formula.Measurement.Unit.Quintal", "q", 0.01m),
                Linear("y", "Formula.Measurement.Unit.Yen", "yến", 0.1m),
                Linear("kg", "Formula.Measurement.Unit.Kilogram", "kg", 1m),
                Linear("hg", "Formula.Measurement.Unit.Hectogram", "hg", 10m),
                Linear("dag", "Formula.Measurement.Unit.Decagram", "dag", 100m),
                Linear("g", "Formula.Measurement.Unit.Gram", "g", 1000m),
                Linear("dg", "Formula.Measurement.Unit.Decigram", "dg", 10000m),
                Linear("cg", "Formula.Measurement.Unit.Centigram", "cg", 100000m),
                Linear("mg", "Formula.Measurement.Unit.Milligram", "mg", 1000000m)
            ],
            "Formula.Measurement.Reference.Mass"),

        new(
            "time",
            "Formula.Measurement.Category.Time",
            "◷",
            "s",
            "hour",
            "minute",
            [
                Linear("week", "Formula.Measurement.Unit.Week", "week", 1m / 604800m),
                Linear("day", "Formula.Measurement.Unit.Day", "day", 1m / 86400m),
                Linear("hour", "Formula.Measurement.Unit.Hour", "h", 1m / 3600m),
                Linear("minute", "Formula.Measurement.Unit.Minute", "min", 1m / 60m),
                Linear("second", "Formula.Measurement.Unit.Second", "s", 1m),
                Linear("millisecond", "Formula.Measurement.Unit.Millisecond", "ms", 1000m)
            ],
            "Formula.Measurement.Reference.Time"),

        new(
            "area",
            "Formula.Measurement.Category.Area",
            "□",
            "m2",
            "m2",
            "cm2",
            [
                Linear("km2", "Formula.Measurement.Unit.SquareKilometre", "km²", 0.000001m),
                Linear("ha", "Formula.Measurement.Unit.Hectare", "ha", 0.0001m),
                Linear("hm2", "Formula.Measurement.Unit.SquareHectometre", "hm²", 0.0001m),
                Linear("dam2", "Formula.Measurement.Unit.SquareDecametre", "dam²", 0.01m),
                Linear("m2", "Formula.Measurement.Unit.SquareMetre", "m²", 1m),
                Linear("dm2", "Formula.Measurement.Unit.SquareDecimetre", "dm²", 100m),
                Linear("cm2", "Formula.Measurement.Unit.SquareCentimetre", "cm²", 10000m),
                Linear("mm2", "Formula.Measurement.Unit.SquareMillimetre", "mm²", 1000000m)
            ],
            "Formula.Measurement.Reference.Area"),

        new(
            "volume",
            "Formula.Measurement.Category.Volume",
            "▣",
            "m3",
            "m3",
            "dm3",
            [
                Linear("m3", "Formula.Measurement.Unit.CubicMetre", "m³", 1m),
                Linear("dm3", "Formula.Measurement.Unit.CubicDecimetre", "dm³", 1000m),
                Linear("cm3", "Formula.Measurement.Unit.CubicCentimetre", "cm³", 1000000m),
                Linear("mm3", "Formula.Measurement.Unit.CubicMillimetre", "mm³", 1000000000m)
            ],
            "Formula.Measurement.Reference.Volume"),

        new(
            "capacity",
            "Formula.Measurement.Category.Capacity",
            "◒",
            "l",
            "l",
            "ml",
            [
                Linear("kl", "Formula.Measurement.Unit.Kilolitre", "kL", 0.001m),
                Linear("hl", "Formula.Measurement.Unit.Hectolitre", "hL", 0.01m),
                Linear("dal", "Formula.Measurement.Unit.Decalitre", "daL", 0.1m),
                Linear("l", "Formula.Measurement.Unit.Litre", "L", 1m),
                Linear("dl", "Formula.Measurement.Unit.Decilitre", "dL", 10m),
                Linear("cl", "Formula.Measurement.Unit.Centilitre", "cL", 100m),
                Linear("ml", "Formula.Measurement.Unit.Millilitre", "mL", 1000m)
            ],
            "Formula.Measurement.Reference.Capacity"),

        new(
            "speed",
            "Formula.Measurement.Category.Speed",
            "➜",
            "mps",
            "kmh",
            "mps",
            [
                Linear("kmh", "Formula.Measurement.Unit.KilometrePerHour", "km/h", 3.6m),
                Linear("mps", "Formula.Measurement.Unit.MetrePerSecond", "m/s", 1m),
                Linear("knot", "Formula.Measurement.Unit.Knot", "kn", 1m / 0.5144444444444444444444444444m)
            ],
            "Formula.Measurement.Reference.Speed"),

        new(
            "temperature",
            "Formula.Measurement.Category.Temperature",
            "℃",
            "celsius",
            "celsius",
            "fahrenheit",
            [
                new(
                    "celsius",
                    "Formula.Measurement.Unit.Celsius",
                    "°C",
                    value => value,
                    baseValue => baseValue),
                new(
                    "fahrenheit",
                    "Formula.Measurement.Unit.Fahrenheit",
                    "°F",
                    value => (value - 32m) * 5m / 9m,
                    baseValue => baseValue * 9m / 5m + 32m),
                new(
                    "kelvin",
                    "Formula.Measurement.Unit.Kelvin",
                    "K",
                    value => value - 273.15m,
                    baseValue => baseValue + 273.15m)
            ],
            "Formula.Measurement.Reference.Temperature")
    ];

    public static IReadOnlyList<MeasurementCategory> Categories =>
        CategoriesValue;

    public static MeasurementCategory GetCategory(
        string categoryId) =>
        CategoriesValue.First(
            category =>
                string.Equals(
                    category.Id,
                    categoryId,
                    StringComparison.OrdinalIgnoreCase));

    public static decimal Convert(
        decimal value,
        MeasurementUnit from,
        MeasurementUnit to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        decimal baseValue = from.ToBase(value);
        return to.FromBase(baseValue);
    }

    public static bool TryConvert(
        decimal value,
        MeasurementUnit from,
        MeasurementUnit to,
        out decimal result)
    {
        try
        {
            result = Convert(
                value,
                from,
                to);
            return true;
        }
        catch (OverflowException)
        {
            result = 0m;
            return false;
        }
        catch (DivideByZeroException)
        {
            result = 0m;
            return false;
        }
    }

    public static bool TryParseInput(
        string? text,
        out decimal value)
    {
        value = 0m;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string normalized = text.Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\u00A0", string.Empty, StringComparison.Ordinal);

        CultureInfo primaryCulture =
            AppLanguageManager.CurrentLanguage == AppLanguage.Vietnamese
                ? CultureInfo.GetCultureInfo("vi-VN")
                : CultureInfo.GetCultureInfo("en-US");

        const NumberStyles styles =
            NumberStyles.Number |
            NumberStyles.AllowExponent;

        if (decimal.TryParse(
                normalized,
                styles,
                primaryCulture,
                out value))
        {
            return true;
        }

        // Friendly fallback: accept either decimal separator when there is
        // no ambiguity with grouping.
        if (normalized.Contains(',') &&
            !normalized.Contains('.'))
        {
            normalized = normalized.Replace(',', '.');
        }

        return decimal.TryParse(
            normalized,
            styles,
            CultureInfo.InvariantCulture,
            out value);
    }

    public static string FormatValue(
        decimal value)
    {
        CultureInfo culture =
            AppLanguageManager.CurrentLanguage == AppLanguage.Vietnamese
                ? CultureInfo.GetCultureInfo("vi-VN")
                : CultureInfo.GetCultureInfo("en-US");

        return value.ToString(
            "0.############################",
            culture);
    }
}
