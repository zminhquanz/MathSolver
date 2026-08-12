using MathSolver.Models;
using System.Numerics;

namespace MathSolver.Services;

/// <summary>
/// Sinh dữ kiện hình học thực tế trước khi gọi LLM. LLM chỉ diễn đạt lại
/// hợp đồng này; đáp án luôn được lấy từ GeometryCalculationEngine.
/// </summary>
public sealed class GeometryQuizGenerator
{
    private readonly GeometryCalculationEngine _engine;
    private readonly Random _random;

    public GeometryQuizGenerator(
        GeometryCalculationEngine engine,
        Random? random = null)
    {
        _engine = engine ??
            throw new ArgumentNullException(nameof(engine));
        _random = random ?? Random.Shared;
    }

    public ArithmeticQuizQuestion Generate(
        ArithmeticQuizMode mode,
        AppLanguage language)
    {
        GeometryStoryTemplate template =
            Templates[_random.Next(Templates.Length)];

        IReadOnlyDictionary<string, BigInteger> dimensions =
            CreateDimensions(template.ShapeId);

        GeometryCalculationResult calculation =
            _engine.CalculateInteger(
                template.ShapeId,
                dimensions);

        GeometryCalculationLine line =
            calculation.Lines.FirstOrDefault(candidate =>
                candidate.Measurement == template.Measurement) ??
            throw new InvalidOperationException(
                "The geometry engine did not return the requested measurement.");

        if (!calculation.IsSuccess ||
            line.IntegerValue is not BigInteger correctAnswer)
        {
            throw new InvalidOperationException(
                "The geometry quiz contract must have an exact integer answer.");
        }

        var contract = new GeometryQuizContract(
            template.ShapeId,
            template.Measurement,
            dimensions,
            template.Unit,
            language == AppLanguage.Vietnamese
                ? template.VietnameseObject
                : template.EnglishObject,
            language == AppLanguage.Vietnamese
                ? template.VietnameseShape
                : template.EnglishShape,
            correctAnswer,
            line.Formula,
            BuildSubstitutionExpression(
                template.ShapeId,
                template.Measurement,
                dimensions));

        return CreateQuestion(mode, contract);
    }

    private ArithmeticQuizQuestion CreateQuestion(
        ArithmeticQuizMode mode,
        GeometryQuizContract contract)
    {
        BigInteger answer = contract.CorrectAnswer;

        // Expression chỉ giữ tương thích với model câu hỏi hiện có. Mọi xử
        // lý hình học đều nhận biết GeometryProblem và không dùng biểu thức này.
        var compatibilityExpression =
            new IntegerArithmeticExpression(
                answer,
                ArithmeticOperation.Add,
                BigInteger.Zero);

        return mode switch
        {
            ArithmeticQuizMode.TrueFalse =>
                CreateTrueFalseQuestion(
                    compatibilityExpression,
                    contract),
            ArithmeticQuizMode.MultipleChoice =>
                new ArithmeticQuizQuestion(
                    compatibilityExpression,
                    mode,
                    answer,
                    null,
                    null,
                    CreateChoices(answer),
                    GeometryProblem: contract),
            ArithmeticQuizMode.Essay =>
                new ArithmeticQuizQuestion(
                    compatibilityExpression,
                    mode,
                    answer,
                    null,
                    null,
                    [],
                    GeometryProblem: contract),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    private ArithmeticQuizQuestion CreateTrueFalseQuestion(
        IntegerArithmeticExpression expression,
        GeometryQuizContract contract)
    {
        bool isCorrect = _random.Next(2) == 0;
        BigInteger presented = isCorrect
            ? contract.CorrectAnswer
            : CreateDistractors(contract.CorrectAnswer, 1)[0];

        return new ArithmeticQuizQuestion(
            expression,
            ArithmeticQuizMode.TrueFalse,
            contract.CorrectAnswer,
            presented,
            presented == contract.CorrectAnswer,
            [],
            GeometryProblem: contract);
    }

    private IReadOnlyList<BigInteger> CreateChoices(
        BigInteger correctAnswer)
    {
        var choices = new List<BigInteger> { correctAnswer };
        choices.AddRange(CreateDistractors(correctAnswer, 3));

        for (int index = choices.Count - 1; index > 0; index--)
        {
            int swapIndex = _random.Next(index + 1);
            (choices[index], choices[swapIndex]) =
                (choices[swapIndex], choices[index]);
        }

        return choices;
    }

    private IReadOnlyList<BigInteger> CreateDistractors(
        BigInteger correctAnswer,
        int count)
    {
        var distractors = new HashSet<BigInteger>();
        BigInteger scale = BigInteger.Max(
            BigInteger.One,
            correctAnswer / 10);

        int[] multipliers = [-2, -1, 1, 2, 3, 5];

        foreach (int multiplier in multipliers
                     .OrderBy(_ => _random.Next()))
        {
            BigInteger candidate =
                correctAnswer + scale * multiplier;

            if (candidate > 0 && candidate != correctAnswer)
            {
                distractors.Add(candidate);
            }

            if (distractors.Count == count)
            {
                break;
            }
        }

        for (BigInteger offset = 1;
             distractors.Count < count;
             offset++)
        {
            BigInteger candidate = correctAnswer + offset;
            if (candidate != correctAnswer)
            {
                distractors.Add(candidate);
            }
        }

        return distractors.ToArray();
    }

    private IReadOnlyDictionary<string, BigInteger> CreateDimensions(
        string shapeId)
    {
        int Value(int minimum = 2, int maximum = 21) =>
            _random.Next(minimum, maximum);

        return shapeId switch
        {
            "square" =>
                new Dictionary<string, BigInteger>
                {
                    ["a"] = Value()
                },
            "rectangle" =>
                new Dictionary<string, BigInteger>
                {
                    ["a"] = Value(4, 31),
                    ["b"] = Value(2, 20)
                },
            "cube" =>
                new Dictionary<string, BigInteger>
                {
                    ["a"] = Value(2, 16)
                },
            "rectangular_prism" =>
                new Dictionary<string, BigInteger>
                {
                    ["a"] = Value(4, 21),
                    ["b"] = Value(2, 16),
                    ["h"] = Value(2, 13)
                },
            _ => throw new ArgumentOutOfRangeException(nameof(shapeId))
        };
    }

    private static string BuildSubstitutionExpression(
        string shapeId,
        GeometryMeasurement measurement,
        IReadOnlyDictionary<string, BigInteger> value)
    {
        string a = value.TryGetValue("a", out BigInteger av)
            ? av.ToString()
            : string.Empty;
        string b = value.TryGetValue("b", out BigInteger bv)
            ? bv.ToString()
            : string.Empty;
        string h = value.TryGetValue("h", out BigInteger hv)
            ? hv.ToString()
            : string.Empty;

        return (shapeId, measurement) switch
        {
            ("square", GeometryMeasurement.Perimeter) =>
                $"{a} × 4",
            ("square", GeometryMeasurement.Area) =>
                $"{a} × {a}",
            ("rectangle", GeometryMeasurement.Perimeter) =>
                $"({a} + {b}) × 2",
            ("rectangle", GeometryMeasurement.Area) =>
                $"{a} × {b}",
            ("cube", GeometryMeasurement.TotalArea) =>
                $"6 × {a} × {a}",
            ("cube", GeometryMeasurement.Volume) =>
                $"{a} × {a} × {a}",
            ("rectangular_prism", GeometryMeasurement.TotalArea) =>
                $"2 × ({a} × {b} + {a} × {h} + {b} × {h})",
            ("rectangular_prism", GeometryMeasurement.Volume) =>
                $"{a} × {b} × {h}",
            _ => throw new ArgumentOutOfRangeException(nameof(measurement))
        };
    }

    private static readonly GeometryStoryTemplate[] Templates =
    [
        new("rectangle", GeometryMeasurement.Perimeter, GeometryLengthUnit.Kilometer,
            "khu bảo tồn", "nature reserve", "hình chữ nhật", "rectangle"),
        new("rectangle", GeometryMeasurement.Area, GeometryLengthUnit.Meter,
            "mảnh vườn", "garden", "hình chữ nhật", "rectangle"),
        new("rectangle", GeometryMeasurement.Perimeter, GeometryLengthUnit.Decimeter,
            "mặt bàn", "tabletop", "hình chữ nhật", "rectangle"),
        new("rectangle", GeometryMeasurement.Area, GeometryLengthUnit.Centimeter,
            "tấm bìa", "sheet of cardboard", "hình chữ nhật", "rectangle"),
        new("rectangle", GeometryMeasurement.Area, GeometryLengthUnit.Millimeter,
            "tấm kim loại nhỏ", "small metal plate", "hình chữ nhật", "rectangle"),
        new("square", GeometryMeasurement.Perimeter, GeometryLengthUnit.Meter,
            "sân chơi", "playground", "hình vuông", "square"),
        new("square", GeometryMeasurement.Area, GeometryLengthUnit.Centimeter,
            "viên gạch", "tile", "hình vuông", "square"),
        new("square", GeometryMeasurement.Area, GeometryLengthUnit.Millimeter,
            "miếng nhãn", "label", "hình vuông", "square"),
        new("cube", GeometryMeasurement.TotalArea, GeometryLengthUnit.Decimeter,
            "thùng hình lập phương", "cube-shaped box", "hình lập phương", "cube"),
        new("cube", GeometryMeasurement.Volume, GeometryLengthUnit.Centimeter,
            "hộp quà", "gift box", "hình lập phương", "cube"),
        new("cube", GeometryMeasurement.Volume, GeometryLengthUnit.Millimeter,
            "khối mô hình nhỏ", "small model block", "hình lập phương", "cube"),
        new("rectangular_prism", GeometryMeasurement.TotalArea, GeometryLengthUnit.Meter,
            "bể chứa", "storage tank", "hình hộp chữ nhật", "rectangular prism"),
        new("rectangular_prism", GeometryMeasurement.Volume, GeometryLengthUnit.Meter,
            "hồ bơi", "swimming pool", "hình hộp chữ nhật", "rectangular prism"),
        new("rectangular_prism", GeometryMeasurement.Volume, GeometryLengthUnit.Decimeter,
            "bể cá", "aquarium", "hình hộp chữ nhật", "rectangular prism"),
        new("rectangular_prism", GeometryMeasurement.Volume, GeometryLengthUnit.Centimeter,
            "hộp đựng đồ", "storage box", "hình hộp chữ nhật", "rectangular prism")
    ];

    private sealed record GeometryStoryTemplate(
        string ShapeId,
        GeometryMeasurement Measurement,
        GeometryLengthUnit Unit,
        string VietnameseObject,
        string EnglishObject,
        string VietnameseShape,
        string EnglishShape);
}
