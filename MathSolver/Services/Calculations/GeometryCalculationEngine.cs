using MathSolver.Models;
using MathSolver.Numerics;
using System.Numerics;

namespace MathSolver.Services.Core;

/// <summary>
/// Engine hình học độc lập với UI. Cả tab Giải toán/Hình học và bộ sinh
/// toán đố AI đều gọi lớp này, nên công thức và đáp án chỉ có một nguồn đúng.
/// </summary>
public sealed class GeometryCalculationEngine
{
    private static readonly OctoDouble ComparisonTolerance =
        OctoDouble.Parse("1e-100");

    public GeometryCalculationResult CalculateInteger(
        string shapeId,
        IReadOnlyDictionary<string, BigInteger> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shapeId);
        ArgumentNullException.ThrowIfNull(values);

        if (!HasOnlyPositiveValues(values))
        {
            return GeometryCalculationResult.Failure(
                "Tất cả kích thước phải lớn hơn 0.");
        }

        try
        {
            var lines = new List<GeometryCalculationLine>();

            switch (shapeId)
            {
                case "square":
                    {
                        BigInteger a = values["a"];
                        AddInteger(lines, GeometryMeasurement.Perimeter, "Chu vi", "P = a × 4", a * 4);
                        AddInteger(lines, GeometryMeasurement.Area, "Diện tích", "S = a × a", a * a);
                        break;
                    }
                case "rectangle":
                    {
                        BigInteger a = values["a"];
                        BigInteger b = values["b"];
                        AddInteger(lines, GeometryMeasurement.Perimeter, "Chu vi", "P = (a + b) × 2", (a + b) * 2);
                        AddInteger(lines, GeometryMeasurement.Area, "Diện tích", "S = a × b", a * b);
                        break;
                    }
                case "triangle":
                    {
                        BigInteger a = values["a"];
                        BigInteger b = values["b"];
                        BigInteger c = values["c"];
                        BigInteger h = values["h"];

                        if (!IsValidTriangle(a, b, c))
                        {
                            return GeometryCalculationResult.Failure(
                                "Ba cạnh không tạo thành tam giác hợp lệ.");
                        }

                        AddInteger(lines, GeometryMeasurement.Perimeter, "Chu vi", "P = a + b + c", a + b + c);
                        AddRational(lines, GeometryMeasurement.Area, "Diện tích", "S = (a × h) ÷ 2", a * h, 2);
                        break;
                    }
                case "right_triangle":
                    {
                        BigInteger a = values["a"];
                        BigInteger b = values["b"];
                        BigInteger c = values["c"];

                        if (a * a + b * b != c * c)
                        {
                            return GeometryCalculationResult.Failure(
                                "Ba cạnh không thỏa mãn định lý Pythagore a² + b² = c².");
                        }

                        AddInteger(lines, GeometryMeasurement.Perimeter, "Chu vi", "P = a + b + c", a + b + c);
                        AddRational(lines, GeometryMeasurement.Area, "Diện tích", "S = (a × b) ÷ 2", a * b, 2);
                        break;
                    }
                case "equilateral_triangle":
                    {
                        BigInteger integerA = values["a"];
                        OctoDouble a = OctoDouble.FromBigInteger(integerA);
                        AddInteger(lines, GeometryMeasurement.Perimeter, "Chu vi", "P = a × 3", integerA * 3);
                        AddDecimal(lines, GeometryMeasurement.Area, "Diện tích", "S = (a² × √3) ÷ 4", a * a * OctoDouble.SqrtThree / 4d);
                        break;
                    }
                case "circle":
                    {
                        OctoDouble r = OctoDouble.FromBigInteger(values["r"]);
                        AddDecimal(lines, GeometryMeasurement.Perimeter, "Chu vi", "C = 2 × π × r", 2d * OctoDouble.Pi * r);
                        AddDecimal(lines, GeometryMeasurement.Area, "Diện tích", "S = π × r²", OctoDouble.Pi * r * r);
                        break;
                    }
                case "trapezoid":
                    {
                        BigInteger a = values["a"];
                        BigInteger b = values["b"];
                        BigInteger c = values["c"];
                        BigInteger d = values["d"];
                        BigInteger h = values["h"];
                        AddInteger(lines, GeometryMeasurement.Perimeter, "Chu vi", "P = a + b + c + d", a + b + c + d);
                        AddRational(lines, GeometryMeasurement.Area, "Diện tích", "S = ((a + b) × h) ÷ 2", (a + b) * h, 2);
                        break;
                    }
                case "isosceles_trapezoid":
                    {
                        BigInteger a = values["a"];
                        BigInteger b = values["b"];
                        BigInteger c = values["c"];
                        BigInteger h = values["h"];
                        AddInteger(lines, GeometryMeasurement.Perimeter, "Chu vi", "P = a + b + 2c", a + b + 2 * c);
                        AddRational(lines, GeometryMeasurement.Area, "Diện tích", "S = ((a + b) × h) ÷ 2", (a + b) * h, 2);
                        break;
                    }
                case "right_trapezoid":
                    {
                        BigInteger a = values["a"];
                        BigInteger b = values["b"];
                        BigInteger c = values["c"];
                        BigInteger h = values["h"];
                        AddInteger(lines, GeometryMeasurement.Perimeter, "Chu vi", "P = a + b + c + h", a + b + c + h);
                        AddRational(lines, GeometryMeasurement.Area, "Diện tích", "S = ((a + b) × h) ÷ 2", (a + b) * h, 2);
                        break;
                    }
                case "rhombus":
                    {
                        BigInteger a = values["a"];
                        BigInteger d1 = values["d1"];
                        BigInteger d2 = values["d2"];
                        BigInteger h = values["h"];
                        AddInteger(lines, GeometryMeasurement.Perimeter, "Chu vi", "P = a × 4", a * 4);
                        AddRational(lines, GeometryMeasurement.Area, "Diện tích theo đường chéo", "S = (d₁ × d₂) ÷ 2", d1 * d2, 2);
                        AddInteger(lines, GeometryMeasurement.Area, "Diện tích theo đáy và chiều cao", "S = a × h", a * h);
                        break;
                    }
                case "parallelogram":
                    {
                        BigInteger a = values["a"];
                        BigInteger b = values["b"];
                        BigInteger h = values["h"];
                        AddInteger(lines, GeometryMeasurement.Perimeter, "Chu vi", "P = (a + b) × 2", (a + b) * 2);
                        AddInteger(lines, GeometryMeasurement.Area, "Diện tích", "S = a × h", a * h);
                        break;
                    }
                case "cube":
                    {
                        BigInteger a = values["a"];
                        BigInteger square = a * a;
                        AddInteger(lines, GeometryMeasurement.LateralArea, "Diện tích xung quanh", "Sxq = 4 × a²", 4 * square);
                        AddInteger(lines, GeometryMeasurement.TotalArea, "Diện tích toàn phần", "Stp = 6 × a²", 6 * square);
                        AddInteger(lines, GeometryMeasurement.Volume, "Thể tích", "V = a³", square * a);
                        break;
                    }
                case "rectangular_prism":
                    {
                        BigInteger a = values["a"];
                        BigInteger b = values["b"];
                        BigInteger h = values["h"];
                        AddInteger(lines, GeometryMeasurement.LateralArea, "Diện tích xung quanh", "Sxq = 2 × (a + b) × h", 2 * (a + b) * h);
                        AddInteger(lines, GeometryMeasurement.TotalArea, "Diện tích toàn phần", "Stp = 2 × (a × b + a × h + b × h)", 2 * (a * b + a * h + b * h));
                        AddInteger(lines, GeometryMeasurement.Volume, "Thể tích", "V = a × b × h", a * b * h);
                        break;
                    }
                case "sphere":
                    {
                        OctoDouble r = OctoDouble.FromBigInteger(values["r"]);
                        OctoDouble square = r * r;
                        AddDecimal(lines, GeometryMeasurement.SurfaceArea, "Diện tích mặt cầu", "S = 4 × π × r²", 4d * OctoDouble.Pi * square);
                        AddDecimal(lines, GeometryMeasurement.Volume, "Thể tích", "V = (4 × π × r³) ÷ 3", 4d * OctoDouble.Pi * square * r / 3d);
                        break;
                    }
                case "cylinder":
                    {
                        OctoDouble r = OctoDouble.FromBigInteger(values["r"]);
                        OctoDouble h = OctoDouble.FromBigInteger(values["h"]);
                        AddCylinderLines(lines, r, h);
                        break;
                    }
                case "cone":
                    {
                        OctoDouble r = OctoDouble.FromBigInteger(values["r"]);
                        OctoDouble h = OctoDouble.FromBigInteger(values["h"]);
                        OctoDouble l = OctoDouble.FromBigInteger(values["l"]);
                        AddConeLines(lines, r, h, l);
                        break;
                    }
                default:
                    return GeometryCalculationResult.Failure(
                        "Hình học này chưa có bộ tính toán.");
            }

            return new(true, null, lines);
        }
        catch (KeyNotFoundException)
        {
            return GeometryCalculationResult.Failure(
                "Thiếu kích thước cần thiết để tính hình học.");
        }
    }

    public GeometryCalculationResult CalculateDecimal(
        string shapeId,
        IReadOnlyDictionary<string, OctoDouble> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shapeId);
        ArgumentNullException.ThrowIfNull(values);

        if (!HasOnlyPositiveValues(values))
        {
            return GeometryCalculationResult.Failure(
                "Tất cả kích thước phải lớn hơn 0.");
        }

        try
        {
            var lines = new List<GeometryCalculationLine>();

            switch (shapeId)
            {
                case "square":
                    {
                        OctoDouble a = values["a"];
                        AddDecimal(lines, GeometryMeasurement.Perimeter, "Chu vi", "P = a × 4", a * 4d);
                        AddDecimal(lines, GeometryMeasurement.Area, "Diện tích", "S = a × a", a * a);
                        break;
                    }
                case "rectangle":
                    {
                        OctoDouble a = values["a"];
                        OctoDouble b = values["b"];
                        AddDecimal(lines, GeometryMeasurement.Perimeter, "Chu vi", "P = (a + b) × 2", (a + b) * 2d);
                        AddDecimal(lines, GeometryMeasurement.Area, "Diện tích", "S = a × b", a * b);
                        break;
                    }
                case "triangle":
                    {
                        OctoDouble a = values["a"];
                        OctoDouble b = values["b"];
                        OctoDouble c = values["c"];
                        OctoDouble h = values["h"];
                        if (!IsValidTriangle(a, b, c))
                        {
                            return GeometryCalculationResult.Failure(
                                "Ba cạnh không tạo thành tam giác hợp lệ.");
                        }
                        AddDecimal(lines, GeometryMeasurement.Perimeter, "Chu vi", "P = a + b + c", a + b + c);
                        AddDecimal(lines, GeometryMeasurement.Area, "Diện tích", "S = (a × h) ÷ 2", a * h / 2d);
                        break;
                    }
                case "right_triangle":
                    {
                        OctoDouble a = values["a"];
                        OctoDouble b = values["b"];
                        OctoDouble c = values["c"];
                        if (!ApproximatelyEqual(a * a + b * b, c * c))
                        {
                            return GeometryCalculationResult.Failure(
                                "Ba cạnh không thỏa mãn định lý Pythagore a² + b² = c².");
                        }
                        AddDecimal(lines, GeometryMeasurement.Perimeter, "Chu vi", "P = a + b + c", a + b + c);
                        AddDecimal(lines, GeometryMeasurement.Area, "Diện tích", "S = (a × b) ÷ 2", a * b / 2d);
                        break;
                    }
                case "equilateral_triangle":
                    {
                        OctoDouble a = values["a"];
                        AddDecimal(lines, GeometryMeasurement.Perimeter, "Chu vi", "P = a × 3", a * 3d);
                        AddDecimal(lines, GeometryMeasurement.Area, "Diện tích", "S = (a² × √3) ÷ 4", a * a * OctoDouble.SqrtThree / 4d);
                        break;
                    }
                case "circle":
                    {
                        OctoDouble r = values["r"];
                        AddDecimal(lines, GeometryMeasurement.Perimeter, "Chu vi", "C = 2 × π × r", 2d * OctoDouble.Pi * r);
                        AddDecimal(lines, GeometryMeasurement.Area, "Diện tích", "S = π × r²", OctoDouble.Pi * r * r);
                        break;
                    }
                case "trapezoid":
                    {
                        OctoDouble a = values["a"];
                        OctoDouble b = values["b"];
                        OctoDouble c = values["c"];
                        OctoDouble d = values["d"];
                        OctoDouble h = values["h"];
                        AddDecimal(lines, GeometryMeasurement.Perimeter, "Chu vi", "P = a + b + c + d", a + b + c + d);
                        AddDecimal(lines, GeometryMeasurement.Area, "Diện tích", "S = ((a + b) × h) ÷ 2", (a + b) * h / 2d);
                        break;
                    }
                case "isosceles_trapezoid":
                    {
                        OctoDouble a = values["a"];
                        OctoDouble b = values["b"];
                        OctoDouble c = values["c"];
                        OctoDouble h = values["h"];
                        AddDecimal(lines, GeometryMeasurement.Perimeter, "Chu vi", "P = a + b + 2c", a + b + 2d * c);
                        AddDecimal(lines, GeometryMeasurement.Area, "Diện tích", "S = ((a + b) × h) ÷ 2", (a + b) * h / 2d);
                        break;
                    }
                case "right_trapezoid":
                    {
                        OctoDouble a = values["a"];
                        OctoDouble b = values["b"];
                        OctoDouble c = values["c"];
                        OctoDouble h = values["h"];
                        AddDecimal(lines, GeometryMeasurement.Perimeter, "Chu vi", "P = a + b + c + h", a + b + c + h);
                        AddDecimal(lines, GeometryMeasurement.Area, "Diện tích", "S = ((a + b) × h) ÷ 2", (a + b) * h / 2d);
                        break;
                    }
                case "rhombus":
                    {
                        OctoDouble a = values["a"];
                        OctoDouble d1 = values["d1"];
                        OctoDouble d2 = values["d2"];
                        OctoDouble h = values["h"];
                        AddDecimal(lines, GeometryMeasurement.Perimeter, "Chu vi", "P = a × 4", a * 4d);
                        AddDecimal(lines, GeometryMeasurement.Area, "Diện tích theo đường chéo", "S = (d₁ × d₂) ÷ 2", d1 * d2 / 2d);
                        AddDecimal(lines, GeometryMeasurement.Area, "Diện tích theo đáy và chiều cao", "S = a × h", a * h);
                        break;
                    }
                case "parallelogram":
                    {
                        OctoDouble a = values["a"];
                        OctoDouble b = values["b"];
                        OctoDouble h = values["h"];
                        AddDecimal(lines, GeometryMeasurement.Perimeter, "Chu vi", "P = (a + b) × 2", (a + b) * 2d);
                        AddDecimal(lines, GeometryMeasurement.Area, "Diện tích", "S = a × h", a * h);
                        break;
                    }
                case "cube":
                    {
                        OctoDouble a = values["a"];
                        OctoDouble square = a * a;
                        AddDecimal(lines, GeometryMeasurement.LateralArea, "Diện tích xung quanh", "Sxq = 4 × a²", 4d * square);
                        AddDecimal(lines, GeometryMeasurement.TotalArea, "Diện tích toàn phần", "Stp = 6 × a²", 6d * square);
                        AddDecimal(lines, GeometryMeasurement.Volume, "Thể tích", "V = a³", square * a);
                        break;
                    }
                case "rectangular_prism":
                    {
                        OctoDouble a = values["a"];
                        OctoDouble b = values["b"];
                        OctoDouble h = values["h"];
                        AddDecimal(lines, GeometryMeasurement.LateralArea, "Diện tích xung quanh", "Sxq = 2 × (a + b) × h", 2d * (a + b) * h);
                        AddDecimal(lines, GeometryMeasurement.TotalArea, "Diện tích toàn phần", "Stp = 2 × (a × b + a × h + b × h)", 2d * (a * b + a * h + b * h));
                        AddDecimal(lines, GeometryMeasurement.Volume, "Thể tích", "V = a × b × h", a * b * h);
                        break;
                    }
                case "sphere":
                    {
                        OctoDouble r = values["r"];
                        OctoDouble square = r * r;
                        AddDecimal(lines, GeometryMeasurement.SurfaceArea, "Diện tích mặt cầu", "S = 4 × π × r²", 4d * OctoDouble.Pi * square);
                        AddDecimal(lines, GeometryMeasurement.Volume, "Thể tích", "V = (4 × π × r³) ÷ 3", 4d * OctoDouble.Pi * square * r / 3d);
                        break;
                    }
                case "cylinder":
                    AddCylinderLines(lines, values["r"], values["h"]);
                    break;
                case "cone":
                    AddConeLines(lines, values["r"], values["h"], values["l"]);
                    break;
                default:
                    return GeometryCalculationResult.Failure(
                        "Hình học này chưa có bộ tính toán.");
            }

            return new(true, null, lines);
        }
        catch (KeyNotFoundException)
        {
            return GeometryCalculationResult.Failure(
                "Thiếu kích thước cần thiết để tính hình học.");
        }
    }

    private static void AddCylinderLines(
        List<GeometryCalculationLine> lines,
        OctoDouble r,
        OctoDouble h)
    {
        OctoDouble baseArea = OctoDouble.Pi * r * r;
        AddDecimal(lines, GeometryMeasurement.BaseArea, "Diện tích đáy", "Sđ = π × r²", baseArea);
        AddDecimal(lines, GeometryMeasurement.LateralArea, "Diện tích xung quanh", "Sxq = 2 × π × r × h", 2d * OctoDouble.Pi * r * h);
        AddDecimal(lines, GeometryMeasurement.TotalArea, "Diện tích toàn phần", "Stp = 2 × π × r × (r + h)", 2d * OctoDouble.Pi * r * (r + h));
        AddDecimal(lines, GeometryMeasurement.Volume, "Thể tích", "V = π × r² × h", baseArea * h);
    }

    private static void AddConeLines(
        List<GeometryCalculationLine> lines,
        OctoDouble r,
        OctoDouble h,
        OctoDouble l)
    {
        OctoDouble baseArea = OctoDouble.Pi * r * r;
        AddDecimal(lines, GeometryMeasurement.BaseArea, "Diện tích đáy", "Sđ = π × r²", baseArea);
        AddDecimal(lines, GeometryMeasurement.LateralArea, "Diện tích xung quanh", "Sxq = π × r × l", OctoDouble.Pi * r * l);
        AddDecimal(lines, GeometryMeasurement.TotalArea, "Diện tích toàn phần", "Stp = π × r × (r + l)", OctoDouble.Pi * r * (r + l));
        AddDecimal(lines, GeometryMeasurement.Volume, "Thể tích", "V = (π × r² × h) ÷ 3", baseArea * h / 3d);
    }

    private static void AddInteger(
        List<GeometryCalculationLine> lines,
        GeometryMeasurement measurement,
        string titleKey,
        string formula,
        BigInteger value) =>
        lines.Add(
            new(measurement, titleKey, formula, value, default));

    private static void AddDecimal(
        List<GeometryCalculationLine> lines,
        GeometryMeasurement measurement,
        string titleKey,
        string formula,
        OctoDouble value) =>
        lines.Add(
            new(measurement, titleKey, formula, null, value));

    private static void AddRational(
        List<GeometryCalculationLine> lines,
        GeometryMeasurement measurement,
        string titleKey,
        string formula,
        BigInteger numerator,
        int denominator)
    {
        BigInteger quotient = BigInteger.DivRem(
            numerator,
            denominator,
            out BigInteger remainder);

        if (remainder.IsZero)
        {
            AddInteger(lines, measurement, titleKey, formula, quotient);
        }
        else
        {
            AddDecimal(
                lines,
                measurement,
                titleKey,
                formula,
                OctoDouble.FromRational(numerator, denominator));
        }
    }

    private static bool HasOnlyPositiveValues(
        IReadOnlyDictionary<string, BigInteger> values) =>
        values.Count > 0 &&
        values.Values.All(value => value > BigInteger.Zero);

    private static bool HasOnlyPositiveValues(
        IReadOnlyDictionary<string, OctoDouble> values) =>
        values.Count > 0 &&
        values.Values.All(value => value > OctoDouble.Zero);

    private static bool IsValidTriangle(
        BigInteger a,
        BigInteger b,
        BigInteger c) =>
        a + b > c &&
        a + c > b &&
        b + c > a;

    private static bool IsValidTriangle(
        OctoDouble a,
        OctoDouble b,
        OctoDouble c) =>
        a + b > c &&
        a + c > b &&
        b + c > a;

    private static bool ApproximatelyEqual(
        OctoDouble left,
        OctoDouble right)
    {
        OctoDouble difference = OctoDouble.Abs(left - right);
        OctoDouble scale = OctoDouble.Max(
            OctoDouble.One,
            OctoDouble.Max(
                OctoDouble.Abs(left),
                OctoDouble.Abs(right)));

        return difference <= ComparisonTolerance * scale;
    }
}
