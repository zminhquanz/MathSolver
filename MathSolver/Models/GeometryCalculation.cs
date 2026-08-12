using MathSolver.Numerics;
using System.Numerics;

namespace MathSolver.Models;

public enum GeometryMeasurement
{
    Perimeter,
    Area,
    Volume,
    BaseArea,
    LateralArea,
    TotalArea,
    SurfaceArea
}

public enum GeometryLengthUnit
{
    Kilometer,
    Meter,
    Decimeter,
    Centimeter,
    Millimeter
}

public sealed record GeometryCalculationLine(
    GeometryMeasurement Measurement,
    string TitleKey,
    string Formula,
    BigInteger? IntegerValue,
    OctoDouble DecimalValue)
{
    public bool IsDecimal =>
        IntegerValue is null;
}

public sealed record GeometryCalculationResult(
    bool IsSuccess,
    string? ErrorKey,
    IReadOnlyList<GeometryCalculationLine> Lines)
{
    public static GeometryCalculationResult Failure(
        string errorKey) =>
        new(false, errorKey, []);
}

public sealed record GeometryQuizContract(
    string ShapeId,
    GeometryMeasurement Measurement,
    IReadOnlyDictionary<string, BigInteger> Dimensions,
    GeometryLengthUnit LengthUnit,
    string ObjectName,
    string ShapeName,
    BigInteger CorrectAnswer,
    string Formula,
    string SubstitutionExpression)
{
    public int UnitPower =>
        Measurement switch
        {
            GeometryMeasurement.Perimeter => 1,
            GeometryMeasurement.Volume => 3,
            _ => 2
        };

    public string LengthUnitSymbol =>
        GeometryUnitFormatter.GetLengthSymbol(
            LengthUnit);

    public string AnswerUnit =>
        GeometryUnitFormatter.GetMeasurementSymbol(
            LengthUnit,
            UnitPower);

    public string EquationText =>
        $"{SubstitutionExpression} = {CorrectAnswer}";
}

public static class GeometryUnitFormatter
{
    public static string GetLengthSymbol(
        GeometryLengthUnit unit) =>
        unit switch
        {
            GeometryLengthUnit.Kilometer => "km",
            GeometryLengthUnit.Meter => "m",
            GeometryLengthUnit.Decimeter => "dm",
            GeometryLengthUnit.Centimeter => "cm",
            GeometryLengthUnit.Millimeter => "mm",
            _ => throw new ArgumentOutOfRangeException(
                nameof(unit))
        };

    public static string GetMeasurementSymbol(
        GeometryLengthUnit unit,
        int power)
    {
        string symbol = GetLengthSymbol(unit);

        return power switch
        {
            1 => symbol,
            2 => $"{symbol}²",
            3 => $"{symbol}³",
            _ => throw new ArgumentOutOfRangeException(
                nameof(power))
        };
    }
}
