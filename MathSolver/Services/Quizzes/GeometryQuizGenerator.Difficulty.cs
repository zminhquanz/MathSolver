using MathSolver.Models;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Services;

public sealed partial class GeometryQuizGenerator
{
    internal static string[] RequiredDimensionKeys(string shape, GeometryMeasurement measurement) => (shape, measurement) switch
    {
        ("square" or "cube", _) => ["a"], ("circle", _) => ["r"], ("rectangle", _) => ["a", "b"],
        ("triangle", GeometryMeasurement.Perimeter) => ["a", "b", "c"], ("triangle", _) => ["a", "h"],
        ("trapezoid", GeometryMeasurement.Perimeter) => ["a", "b", "c", "d"], ("trapezoid", _) => ["a", "b", "h"],
        ("rhombus", GeometryMeasurement.Perimeter) => ["a"], ("rhombus", _) => ["d1", "d2"],
        ("parallelogram", GeometryMeasurement.Perimeter) => ["a", "b"], ("parallelogram", _) => ["a", "h"],
        ("rectangular_prism", _) => ["a", "b", "h"], _ => throw new ArgumentOutOfRangeException(nameof(shape))
    };

    private IReadOnlyDictionary<string, BigInteger> CreateTieredDimensions(string shape, GeometryMeasurement measurement, CurriculumTier tier, int cap)
    {
        int level = (int)tier;
        int Size(int divisor = 1)
        {
            int ceiling = Math.Max(2, Math.Min(cap / divisor, 4 + level * 4));
            return _random.Next(Math.Min(ceiling, 1 + level), ceiling + 1);
        }
        int Scaled(int divisor, int quantum) => quantum * _random.Next(1, Math.Max(2, cap / divisor / quantum + 1));
        int k = level == 5 ? Scaled(5, 10) : Size(5);
        if (shape == "triangle") return new Dictionary<string, BigInteger>
            { ["a"] = 4 * k, ["b"] = 3 * k, ["c"] = 5 * k, ["h"] = 3 * k };
        if (shape == "trapezoid")
        {
            k = level == 5 ? Scaled(10, 5) : Math.Max(1, Size(10));
            return new Dictionary<string, BigInteger>
                { ["a"] = 10 * k, ["b"] = 2 * k, ["c"] = 5 * k, ["d"] = 5 * k, ["h"] = 3 * k };
        }
        if (shape == "rhombus")
        {
            k = level == 5 ? Scaled(8, 5) : Size(8);
            // Half-diagonals form a 3-4-5 triangle. The oblique height is not
            // a supplied dimension and need not be rounded to an integer.
            return new Dictionary<string, BigInteger> { ["a"] = 5 * k, ["d1"] = 8 * k, ["d2"] = 6 * k };
        }
        if (shape == "circle")
        {
            int quantum = measurement == GeometryMeasurement.Perimeter ? 25 : 10;
            int minimum = level == 5 ? 2 : 1;
            return new Dictionary<string, BigInteger> { ["r"] = quantum * _random.Next(minimum, Math.Max(minimum + 1, cap / quantum + 1)) };
        }
        int b = level == 5 ? Scaled(shape == "parallelogram" ? 3 : 2, 10) : Size(shape == "parallelogram" ? 3 : 2);
        return shape switch
        {
            "square" or "cube" => new Dictionary<string, BigInteger> { ["a"] = level == 5 ? 10 * _random.Next(2, Math.Max(3, cap / 10 + 1)) : Size() },
            "rectangle" => new Dictionary<string, BigInteger> { ["a"] = 2 * b, ["b"] = b },
            "parallelogram" => new Dictionary<string, BigInteger> { ["a"] = 3 * b, ["b"] = 2 * b, ["h"] = b },
            "rectangular_prism" => new Dictionary<string, BigInteger> { ["a"] = 2 * b, ["b"] = b, ["h"] = level == 5 ? Scaled(1, 10) : Size() },
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        };
    }

    private GeometryQuizReasoning CreateReasoning(GeometryQuizContract contract, CurriculumTier tier, AppLanguage language)
    {
        int level = (int)tier;
        bool vi = language == AppLanguage.Vietnamese;
        string unit = contract.LengthUnitSymbol;
        var givens = new List<GeometryGivenFact>();
        var steps = new List<GeometryInferenceStep>();
        var relations = new List<string>();
        var hidden = new HashSet<string>();
        var expressions = new Dictionary<string, string>();
        string[] keys = RequiredDimensionKeys(contract.ShapeId, contract.Measurement);
        string Name(string key) => GeometryReasoningText.DimensionName(contract.ShapeId, key, language);
        string N(BigInteger value) => value.ToString(CultureInfo.InvariantCulture);
        void Given(string id, BigInteger value, string givenUnit, string clause) => givens.Add(new(id, value, givenUnit, clause));
        void Direct(string key)
        {
            BigInteger value = contract.Dimensions[key];
            Given(key, value, unit, QuizContentCatalog.Text(language, "GeometryQuizGenerator.Difficulty.CreateReasoning.001", ("Name_key", $"{Name(key)}"), ("value", $"{value}"), ("unit", $"{unit}")));
            expressions[key] = N(value);
        }
        void Infer(string key, string expression, string label, BigInteger value)
        {
            steps.Add(new(key, label, expression, value, unit));
            expressions[key] = expression;
            if (contract.Dimensions.ContainsKey(key)) hidden.Add(key);
        }
        // Returns an expression in the contract's base length unit, with an explicit conversion step.
        string ConvertDifference(BigInteger value, string clausePrefix)
        {
            string inputUnit = unit switch { "km" => "m", "m" => "dm", "dm" => "cm", "cm" => "mm", _ => "cm" };
            int factor = unit == "km" ? 1000 : 10;
            BigInteger raw = unit == "mm" ? value / factor : value * factor;
            if (unit == "mm" && value % factor != 0) throw new InvalidOperationException("Converted geometry givens must stay integral.");
            Given("difference", raw, inputUnit, $"{clausePrefix} {raw} {inputUnit}.");
            string expression = unit == "mm" ? $"{raw}*{factor}" : $"{raw}/{factor}";
            Infer("converted", expression, QuizContentCatalog.Text(language, "GeometryQuizGenerator.Difficulty.CreateReasoning.002"), value);
            return expression;
        }
        string first = keys[0];
        BigInteger firstValue = contract.Dimensions[first];
        string scenario;
        if (level == 1)
        {
            foreach (string key in keys) Direct(key);
            scenario = "direct";
        }
        else if (level == 2 && keys.Length == 1 && contract.ShapeId is "square" or "cube" && contract.Measurement != GeometryMeasurement.Perimeter)
        {
            BigInteger p = 4 * firstValue;
            string face = contract.ShapeId == "cube" ? QuizContentCatalog.Text(language, "GeometryQuizGenerator.Difficulty.CreateReasoning.003") : contract.ShapeName;
            Given("perimeter", p, unit, QuizContentCatalog.Text(language, "GeometryQuizGenerator.Difficulty.CreateReasoning.004", ("face", $"{face}"), ("p", $"{p}"), ("unit", $"{unit}")));
            Infer(first, $"{p}/4", Name(first), firstValue);
            scenario = "side-from-perimeter";
        }
        else if (level == 2 && contract.ShapeId == "circle")
        {
            Given("diameter", firstValue * 2, unit, QuizContentCatalog.Text(language, "GeometryQuizGenerator.Difficulty.CreateReasoning.005", ("firstValue_2", $"{firstValue * 2}"), ("unit", $"{unit}")));
            Infer(first, $"{firstValue * 2}/2", Name(first), firstValue);
            scenario = "radius-from-diameter";
        }
        else if (level == 3 && contract.ShapeId == "rectangle" && contract.Measurement == GeometryMeasurement.Area)
        {
            BigInteger width = contract.Dimensions["b"], p = 2 * (firstValue + width);
            Given("perimeter", p, unit, QuizContentCatalog.Text(language, "GeometryQuizGenerator.Difficulty.CreateReasoning.006", ("p", $"{p}"), ("unit", $"{unit}")));
            Direct("b");
            Infer("semiperimeter", $"{p}/2", QuizContentCatalog.Text(language, "GeometryQuizGenerator.Difficulty.CreateReasoning.007"), p / 2);
            Infer("a", $"{p}/2-{width}", Name("a"), firstValue);
            scenario = "length-from-perimeter-and-width";
        }
        else if (level <= 3)
        {
            int count = level == 2 ? 2 : 3;
            BigInteger x = BigInteger.Max(1, firstValue / count), y = count == 2 ? firstValue - x : BigInteger.Max(1, (firstValue - x) / 2);
            BigInteger z = firstValue - x - y;
            Given("part-x", x, unit, QuizContentCatalog.Text(language, "GeometryQuizGenerator.Difficulty.CreateReasoning.008", ("Name_first", $"{Name(first)}"), ("x", $"{x}"), ("unit", $"{unit}")));
            Given("part-y", y, unit, QuizContentCatalog.Text(language, "GeometryQuizGenerator.Difficulty.CreateReasoning.009", ("y", $"{y}"), ("unit", $"{unit}")));
            if (count == 3)
            {
                Given("part-z", z, unit, QuizContentCatalog.Text(language, "GeometryQuizGenerator.Difficulty.CreateReasoning.010", ("z", $"{z}"), ("unit", $"{unit}")));
                Infer("partial", $"{x}+{y}", QuizContentCatalog.Text(language, "GeometryQuizGenerator.Difficulty.CreateReasoning.011"), x + y);
            }
            Infer(first, count == 2 ? $"{x}+{y}" : $"{x}+{y}+{z}", Name(first), firstValue);
            foreach (string key in keys.Skip(1)) Direct(key);
            scenario = count == 2 ? "two-segment-length" : "three-segment-length";
        }
        else
        {
            bool chain = keys.Length > 1 && (contract.Measurement == GeometryMeasurement.Perimeter ||
                contract.ShapeId == "trapezoid" || contract.Measurement == GeometryMeasurement.LateralArea);
            string second = keys.Length > 1 ? keys[1] : "reference";
            BigInteger secondValue = keys.Length > 1 ? contract.Dimensions[second] : (level == 5 ? firstValue - 10 : BigInteger.Max(1, firstValue / 2));
            BigInteger difference = firstValue - secondValue;
            string prefix = keys.Length == 1
                ? QuizContentCatalog.Text(language, "GeometryQuizGenerator.Difficulty.CreateReasoning.012")
                : QuizContentCatalog.Text(language, "GeometryQuizGenerator.Difficulty.CreateReasoning.013", ("Name_first", $"{Name(first)}"), ("Name_second", $"{Name(second)}"));
            string differenceExpression;
            if (chain)
            {
                BigInteger x = BigInteger.Max(1, secondValue / 2), y = secondValue - x;
                Given("part-x", x, unit, QuizContentCatalog.Text(language, "GeometryQuizGenerator.Difficulty.CreateReasoning.014", ("Name_second", $"{Name(second)}"), ("x", $"{x}"), ("unit", $"{unit}")));
                Given("part-y", y, unit, QuizContentCatalog.Text(language, "GeometryQuizGenerator.Difficulty.CreateReasoning.015", ("y", $"{y}"), ("unit", $"{unit}")));
                Infer(second, $"{x}+{y}", Name(second), secondValue);
                if (level == 5) differenceExpression = ConvertDifference(difference, prefix);
                else { Given("difference", difference, unit, $"{prefix} {difference} {unit}."); differenceExpression = N(difference); }
                Infer(first, $"({x}+{y})+({differenceExpression})", Name(first), firstValue);
                scenario = "linked-dimensions";
            }
            else
            {
                BigInteger sum = firstValue + secondValue;
                string sumClause = keys.Length == 1
                    ? QuizContentCatalog.Text(language, "GeometryQuizGenerator.Difficulty.CreateReasoning.016", ("sum", $"{sum}"), ("unit", $"{unit}"))
                    : QuizContentCatalog.Text(language, "GeometryQuizGenerator.Difficulty.CreateReasoning.017", ("Name_first", $"{Name(first)}"), ("Name_second", $"{Name(second)}"), ("sum", $"{sum}"), ("unit", $"{unit}"));
                Given("sum", sum, unit, sumClause);
                if (level == 5) differenceExpression = ConvertDifference(difference, prefix);
                else { Given("difference", difference, unit, $"{prefix} {difference} {unit}."); differenceExpression = N(difference); }
                Infer(first, $"({sum}+({differenceExpression}))/2", Name(first), firstValue);
                if (keys.Length > 1) Infer(second, $"({sum}-({differenceExpression}))/2", Name(second), secondValue);
                else relations.Add(QuizContentCatalog.Text(language, "GeometryQuizGenerator.Difficulty.CreateReasoning.018", ("Name_first", $"{Name(first)}")));
                scenario = "sum-and-difference";
            }
            foreach (string key in keys.Skip(2)) Direct(key);
            if (level == 5) scenario += "-conversion";
        }
        string E(string key) => $"({expressions[key]})";
        string combined = (contract.ShapeId, contract.Measurement) switch
        {
            ("square" or "rhombus", GeometryMeasurement.Perimeter) => $"4*{E("a")}",
            ("square", _) => $"{E("a")}*{E("a")}",
            ("rectangle" or "parallelogram", GeometryMeasurement.Perimeter) => $"2*({E("a")}+{E("b")})",
            ("rectangle", _) => $"{E("a")}*{E("b")}",
            ("triangle", GeometryMeasurement.Perimeter) => $"{E("a")}+{E("b")}+{E("c")}",
            ("triangle", _) => $"{E("a")}*{E("h")}/2",
            ("trapezoid", GeometryMeasurement.Perimeter) => $"{E("a")}+{E("b")}+{E("c")}+{E("d")}",
            ("trapezoid", _) => $"({E("a")}+{E("b")})*{E("h")}/2",
            ("rhombus", _) => $"{E("d1")}*{E("d2")}/2",
            ("parallelogram", _) => $"{E("a")}*{E("h")}",
            ("circle", GeometryMeasurement.Perimeter) => $"628*{E("r")}/100",
            ("circle", _) => $"314*{E("r")}*{E("r")}/100",
            ("cube", GeometryMeasurement.Volume) => $"{E("a")}*{E("a")}*{E("a")}",
            ("cube", _) => $"{(contract.Measurement == GeometryMeasurement.LateralArea ? 4 : 6)}*{E("a")}*{E("a")}",
            ("rectangular_prism", GeometryMeasurement.Volume) => $"{E("a")}*{E("b")}*{E("h")}",
            ("rectangular_prism", GeometryMeasurement.LateralArea) => $"2*({E("a")}+{E("b")})*{E("h")}",
            ("rectangular_prism", _) => $"2*({E("a")}*{E("b")}+{E("a")}*{E("h")}+{E("b")}*{E("h")})",
            _ => throw new ArgumentOutOfRangeException(nameof(contract))
        };
        string question = QuizContentCatalog.Text(language, "GeometryQuizGenerator.Difficulty.CreateReasoning.019", ("value0", $"{GeometryReasoningText.MeasurementName(contract.Measurement, language)}"), ("contract_ShapeName", $"{contract.ShapeName}"), ("contract_AnswerUnit", $"{contract.AnswerUnit}"));
        string math = string.Join(" ", givens.Select(fact => fact.Clause).Concat(relations)) + " " + question;
        if (contract.ShapeId == "circle") math += QuizContentCatalog.Text(language, "GeometryQuizGenerator.Difficulty.CreateReasoning.020");
        string problem = QuizContentCatalog.Text(language, "GeometryQuizGenerator.Difficulty.CreateReasoning.021", ("contract_ObjectName", $"{contract.ObjectName}"), ("contract_ShapeName", $"{contract.ShapeName}")) + math;
        return new(tier, scenario, language, givens, relations, steps, hidden, math, problem, combined);
    }
}
