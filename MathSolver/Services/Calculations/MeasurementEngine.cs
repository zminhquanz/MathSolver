using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

/// <summary>
/// Central conversion engine for the Formula → Units tab.
/// Uses decimal arithmetic so school-level metric conversions stay exact.
/// Currency is intentionally excluded because it is locale/rate dependent.
/// </summary>
public static class MeasurementEngine
{
    public const int MaximumDecimalPlaces = 10;

    private static readonly BigInteger MaximumDecimalCoefficient = (BigInteger.One << 96) - 1;

    private static readonly Regex InvariantGroupedNumber = new(
        @"^[+-]?[1-9][0-9]{0,2}(,[0-9]{3})+(\.[0-9]+)?([eE][+-]?[0-9]+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CommaDecimalGroupedNumber = new(
        @"^[+-]?[0-9]{1,3}(\.[0-9]{3})+,[0-9]+([eE][+-]?[0-9]+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DecimalNumber = new(
        @"^[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public sealed record MeasurementUnit(
        string Id,
        string NameKey,
        string Symbol,
        Func<decimal, decimal> ToBase,
        Func<decimal, decimal> FromBase)
    {
        internal (decimal Numerator, decimal Denominator)? BaseRatio { get; init; }

        // Symbol stays canonical for quiz contracts and arithmetic metadata.
        // Only presentation translates units written as words.
        internal string? SymbolKey { get; init; }

        public string GetDisplaySymbol(decimal quantity = 1m)
        {
            if (SymbolKey is null) return Symbol;
            decimal displayed = decimal.Round(quantity, MaximumDecimalPlaces, MidpointRounding.AwayFromZero);
            return LocalizationService.TranslateKey(
                SymbolKey + (displayed is 1m or -1m ? ".One" : ".Other"));
        }

        public string DisplayName
        {
            get
            {
                string name = LocalizationService.TranslateKey(NameKey);
                string symbol = GetDisplaySymbol();
                return string.Equals(name, symbol, StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith(symbol + " (", StringComparison.OrdinalIgnoreCase)
                    ? name : $"{name} ({symbol})";
            }
        }
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

        return Ratio(id, nameKey, symbol, 1m, unitsPerBaseUnit);
    }

    private static MeasurementUnit Ratio(string id, string nameKey, string symbol,
        decimal numerator, decimal denominator = 1m) =>
        new(id, nameKey, symbol,
            value => Scale(value, numerator, denominator),
            value => Scale(value, denominator, numerator))
        { BaseRatio = (numerator, denominator) };

    private static (BigInteger Numerator, BigInteger Denominator) DecimalParts(decimal value)
    {
        int[] bits = decimal.GetBits(value);
        BigInteger coefficient = ((BigInteger)(uint)bits[2] << 64)
            | ((BigInteger)(uint)bits[1] << 32) | (uint)bits[0];
        if (bits[3] < 0) coefficient = -coefficient;
        return (coefficient, BigInteger.Pow(10, (bits[3] >> 16) & 0xFF));
    }

    private static decimal Scale(decimal value, decimal numerator, decimal denominator)
    {
        if (numerator == denominator) return value;
        if (numerator == 1m) return value / denominator;
        if (denominator == 1m) return value * numerator;

        // Metric and integral time ratios reduce to one decimal operation.
        // Keep this common path inexpensive for the shared practice generators.
        decimal a = numerator, b = denominator;
        while (b != 0m) (a, b) = (b, a % b);
        numerator /= a;
        denominator /= a;
        if (numerator == 1m) return value / denominator;
        if (denominator == 1m) return value * numerator;

        // Retain the exact ratio through multiplication, including when the
        // intermediate decimal product would overflow but the final value fits.
        var v = DecimalParts(value);
        var n = DecimalParts(numerator);
        var d = DecimalParts(denominator);
        BigInteger top = BigInteger.Abs(v.Numerator * n.Numerator * d.Denominator);
        BigInteger bottom = v.Denominator * n.Denominator * d.Numerator;
        if (top > MaximumDecimalCoefficient * bottom) throw new OverflowException();
        for (int scale = 28; scale >= 0; scale--)
        {
            BigInteger coefficient = BigInteger.DivRem(top * BigInteger.Pow(10, scale), bottom, out var remainder);
            int midpoint = (remainder * 2).CompareTo(bottom);
            if (midpoint > 0 || midpoint == 0 && !coefficient.IsEven) coefficient++;
            if (coefficient > MaximumDecimalCoefficient) continue;
            return new decimal(unchecked((int)(uint)(coefficient & uint.MaxValue)),
                unchecked((int)(uint)((coefficient >> 32) & uint.MaxValue)),
                unchecked((int)(uint)((coefficient >> 64) & uint.MaxValue)), value < 0m, (byte)scale);
        }
        throw new OverflowException();
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
                Linear("y", "Formula.Measurement.Unit.Yen", "yến", 0.1m)
                    with { SymbolKey = "Formula.Measurement.OutputUnit.Yen" },
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
                Ratio("week", "Formula.Measurement.Unit.Week", "week", 604800m)
                    with { SymbolKey = "Formula.Measurement.OutputUnit.Week" },
                Ratio("day", "Formula.Measurement.Unit.Day", "day", 86400m)
                    with { SymbolKey = "Formula.Measurement.OutputUnit.Day" },
                Ratio("hour", "Formula.Measurement.Unit.Hour", "h", 3600m)
                    with { SymbolKey = "Formula.Measurement.OutputUnit.Hour" },
                Ratio("minute", "Formula.Measurement.Unit.Minute", "min", 60m)
                    with { SymbolKey = "Formula.Measurement.OutputUnit.Minute" },
                Linear("second", "Formula.Measurement.Unit.Second", "s", 1m)
                    with { SymbolKey = "Formula.Measurement.OutputUnit.Second" },
                Linear("millisecond", "Formula.Measurement.Unit.Millisecond", "ms", 1000m)
                    with { SymbolKey = "Formula.Measurement.OutputUnit.Millisecond" }
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
                Ratio("knot", "Formula.Measurement.Unit.Knot", "kn", 463m, 900m)
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

        if (ReferenceEquals(from, to)) return value;
        if (from.BaseRatio is { } source && to.BaseRatio is { } target)
            return Scale(value, source.Numerator * target.Denominator,
                source.Denominator * target.Numerator);

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
            .Replace("\u00A0", string.Empty, StringComparison.Ordinal)
            .Replace("\u202F", string.Empty, StringComparison.Ordinal);

        // Normalize strict grouping first; never retry an overprecision decimal
        // as an integer with grouping separators in another culture.
        if (InvariantGroupedNumber.IsMatch(normalized)) normalized = normalized.Replace(",", "");
        else if (CommaDecimalGroupedNumber.IsMatch(normalized)) normalized = normalized.Replace(".", "").Replace(',', '.');
        else if (!normalized.Contains('.')) normalized = normalized.Replace(',', '.');
        if (!DecimalNumber.IsMatch(normalized)) return false;

        int exponentIndex = normalized.IndexOfAny(['e', 'E']);
        string mantissa = exponentIndex < 0 ? normalized : normalized[..exponentIndex];
        int exponent = 0;
        if (exponentIndex >= 0 && !int.TryParse(normalized.AsSpan(exponentIndex + 1),
            NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent)) return false;
        int point = mantissa.IndexOf('.');
        long places = (point < 0 ? 0 : mantissa.Length - point - 1L) - exponent;
        if (places > MaximumDecimalPlaces || places < -28) return false;
        if (!decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal parsed)) return false;

        // decimal.TryParse can silently round a mantissa with too many significant
        // digits. Accept only inputs that the decimal value represents exactly.
        var digits = BigInteger.Parse(mantissa.Replace(".", "").TrimStart('+', '-'), CultureInfo.InvariantCulture);
        var actual = DecimalParts(parsed);
        BigInteger top = digits * BigInteger.Pow(10, (int)Math.Max(0, -places));
        BigInteger bottom = BigInteger.Pow(10, (int)Math.Max(0, places));
        if (top * actual.Denominator != BigInteger.Abs(actual.Numerator) * bottom) return false;
        value = parsed;
        return true;
    }

    public static string FormatQuantity(decimal value, MeasurementUnit unit) =>
        $"{FormatValue(value)} {unit.GetDisplaySymbol(value)}";

    public static string FormatValue(
        decimal value)
    {
        decimal rounded = decimal.Round(value, MaximumDecimalPlaces, MidpointRounding.AwayFromZero);
        return (rounded == 0m ? 0m : rounded).ToString(
            "#,0.##########",
            CultureInfo.InvariantCulture);
    }
}
