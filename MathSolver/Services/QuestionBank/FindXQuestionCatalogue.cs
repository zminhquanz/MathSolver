using MathSolver.Models;
using MathSolver.Services.Core;
using System.Numerics;
using System.Text.Json;

namespace MathSolver.Services.QuestionBank;

public enum BankQuestionFamily { Arithmetic, FindX, Fraction, TwoNumbers, Average, Percentage, MultiStep, Motion, Proportion, Decimal, Measurement, Remainder, Time, FractionQuantity }
public enum FindXUnknownRole { None, Addend, Minuend, Subtrahend, Factor, Dividend, Divisor }

/// <summary>The same reviewed situation, with an explicit equation/unknown role.
/// SourceScene owns the activity, dimensions, real-world capacity and bilingual prose.
/// Left/Right are the two GIVEN values in the solution, never the unknown answer.</summary>
public sealed record FindXQuestionScene(string Id, AppliedQuestionScene SourceScene, FindXUnknownRole Role,
    ArithmeticOperation EquationOperation, string KnownARole, string KnownBRole, string TargetRole)
{
    public QuestionKnowledgeGroup Group => SourceScene.Group is QuestionKnowledgeGroup.Mass or QuestionKnowledgeGroup.Length
        or QuestionKnowledgeGroup.Transport ? QuestionKnowledgeGroup.Measurement : SourceScene.Group;
    public int MinimumStars => SourceScene.MinimumStars;
    public int Capacity => SourceScene.Capacity;
    public bool RequiresExactDivision => SourceScene.Operation == ArithmeticOperation.Divide || EquationOperation == ArithmeticOperation.Divide;
}

/// <summary>Version 6: one-step integer Find-X stories sharing the nine applied knowledge groups.</summary>
public static class FindXQuestionCatalogue
{
    public const int Version = 6;
    public static IReadOnlyList<FindXQuestionScene> All { get; } = Build().AsReadOnly();
    private static List<FindXQuestionScene> Build()
    {
        var result = new List<FindXQuestionScene>();
        foreach (var source in AppliedQuestionCatalogue.All)
        {
            // Conversions, remainders, rounding and compound formulae need their own
            // multi-step contracts. Do not disguise them as a single integer equation.
            if (source.Conversion != 1 || source.RightDisplayFactor != 1
                || source.Id is "garden-perimeter" || source.Id.Contains("remainder", StringComparison.Ordinal)
                || source.Id.Contains("minimum", StringComparison.Ordinal)) continue;
            void Add(FindXUnknownRole role, ArithmeticOperation op)
            {
                var roles = FactRoles(source);
                result.Add(new($"findx-{role}-{source.Id}", source, role, op, roles.A, roles.B, roles.Target));
            }
            switch (source.Operation)
            {
                case ArithmeticOperation.Add:
                    Add(FindXUnknownRole.Minuend, ArithmeticOperation.Subtract);
                    break;
                case ArithmeticOperation.Subtract:
                    Add(FindXUnknownRole.Addend, ArithmeticOperation.Add);
                    Add(FindXUnknownRole.Subtrahend, ArithmeticOperation.Subtract);
                    break;
                case ArithmeticOperation.Multiply:
                    Add(FindXUnknownRole.Dividend, ArithmeticOperation.Divide);
                    break;
                case ArithmeticOperation.Divide:
                    Add(FindXUnknownRole.Factor, ArithmeticOperation.Multiply);
                    Add(FindXUnknownRole.Divisor, ArithmeticOperation.Divide);
                    break;
            }
        }
        return result;
    }

    private static (string A, string B, string Target) FactRoles(AppliedQuestionScene s)
    {
        if (s.Id.EndsWith("recover-groups", StringComparison.Ordinal)) return ("group count", "amount per group", "original total");
        if (s.Id == "motion-distance") return ("constant speed", "travel duration", "travel distance");
        if (s.Id == "motion-speed") return ("travel distance", "travel duration", "constant speed");
        if (s.Id is "garden-area-groups") return ("length", "width", "area");
        if (s.Id is "garden-side-share") return ("area", "width", "length");
        if (s.Id is "tank-volume-groups") return ("base area", "height", "volume");
        if (s.Id is "tank-height-share") return ("volume", "base area", "height");
        if (s.Id is "shopping-unit-price") return ("total price", "notebook count", "price per notebook");
        if (s.Id is "shopping-price") return ("price per notebook", "notebook count", "total price");
        if (s.Id is "visitors-average") return ("total visits", "recorded day count", "average visits per day");
        if (s.Id is "visitors-average-groups") return ("average visits per day", "recorded day count", "total visits");
        if (s.Id is "age-past-total") return ("age at an earlier birthday", "elapsed years", "current age");
        if (s.Id is "age-future-left") return ("age at a future birthday", "years until that birthday", "current age");
        return s.Structure switch
        {
            BasicQuestionStructure.Combine => ("first separate amount", "second separate amount", "combined amount"),
            BasicQuestionStructure.RecoverInitial => ("remaining amount", "removed amount", "original amount"),
            BasicQuestionStructure.AddComparisonMore => ("reference amount", "excess", "larger amount"),
            BasicQuestionStructure.AddComparisonInverse => ("smaller amount", "difference", "larger amount"),
            BasicQuestionStructure.Difference => ("larger amount", "smaller amount", "difference"),
            BasicQuestionStructure.FindPart => ("total", "known part", "missing part"),
            BasicQuestionStructure.SubComparisonLess or BasicQuestionStructure.SubComparisonInverse => ("larger/reference amount", "difference", "smaller amount"),
            BasicQuestionStructure.Remaining => ("original/target amount", "removed/completed amount", "remaining amount"),
            BasicQuestionStructure.EqualGroups => ("amount per group", "equal group count", "total amount"),
            BasicQuestionStructure.CountGroups => ("total amount", "amount per group", "group count"),
            BasicQuestionStructure.TimesAsMany => ("reference amount", "multiplier", "larger amount"),
            BasicQuestionStructure.TimesFewer => ("reference amount", "ratio", "smaller amount"),
            BasicQuestionStructure.EqualShare => ("total amount", "equal group count", "amount per group"),
            _ => ("initial amount", "increase", "final amount")
        };
    }

    public static FindXQuestionScene? Find(string id) => All.FirstOrDefault(s => s.Id == id);
    public static IEnumerable<FindXQuestionScene> Available(QuestionLearningProfile profile, ArithmeticOperation operation,
        CurriculumTier tier, FindXUnknownRole role = FindXUnknownRole.None)
        => All.Where(s => profile.IsValid && Enum.IsDefined(tier) && s.Group == profile.Group
            && s.EquationOperation == operation && (int)tier >= s.MinimumStars
            && (role == FindXUnknownRole.None || s.Role == role));

    public static BasicQuestionContract AsApplied(BasicQuestionContract c)
    {
        var scene = Find(c.SceneId) ?? throw new ArgumentException("InvalidContract");
        return c with { Version = AppliedQuestionCatalogue.Version, Operation = scene.SourceScene.Operation,
            SceneId = scene.SourceScene.Id, UnknownRole = FindXUnknownRole.None };
    }

    public static BasicQuestionContract Create(QuestionLearningProfile profile, ArithmeticOperation operation, CurriculumTier tier,
        AppLanguage language, Random? random = null, FindXUnknownRole role = FindXUnknownRole.None, string? sceneId = null)
    {
        random ??= Random.Shared;
        var scenes = Available(profile, operation, tier, role).Where(s => sceneId is null || s.Id == sceneId).ToArray();
        if (scenes.Length == 0) throw new ArgumentException("InvalidLearningProfile");
        var scene = scenes[random.Next(scenes.Length)];
        var facts = AppliedQuestionCatalogue.Create(profile, scene.SourceScene.Operation, tier, language, random, scene.SourceScene.Id);
        var c = facts with { Version = Version, Operation = operation, SceneId = scene.Id, UnknownRole = scene.Role };
        if (!IsValid(c)) throw new InvalidOperationException("InvalidContract");
        return c;
    }

    public static FindXQuizContract Equation(BasicQuestionContract c)
    {
        var scene = Find(c.SceneId) ?? throw new ArgumentException("InvalidContract");
        var expression = new IntegerArithmeticExpression(c.Left, scene.SourceScene.Operation, c.Right);
        var answer = new BasicArithmeticEngine().CalculateInteger(expression).Result;
        // Each equality follows from the reviewed source story's two known values.
        (BigInteger known, BigInteger result, bool left) = scene.Role switch
        {
            FindXUnknownRole.Addend => (c.Right, c.Left, false),       // b + x = a
            FindXUnknownRole.Minuend => (c.Right, c.Left, true),       // x - b = a
            FindXUnknownRole.Subtrahend => (c.Right, c.Left, false),   // a - x = b: known must be a
            FindXUnknownRole.Factor => (c.Right, c.Left, false),       // b * x = a
            FindXUnknownRole.Dividend => (c.Right, c.Left, true),      // x / b = a
            FindXUnknownRole.Divisor => (c.Right, c.Left, false),      // a / x = b: known must be a
            _ => throw new ArgumentOutOfRangeException(nameof(c))
        };
        if (scene.Role is FindXUnknownRole.Subtrahend or FindXUnknownRole.Divisor) (known, result) = (c.Left, c.Right);
        return new(known, result, scene.EquationOperation, left, answer, expression);
    }

    public static bool IsValid(BasicQuestionContract c)
    {
        var scene = Find(c.SceneId);
        if (c.Version != Version || scene is null || c.UnknownRole != scene.Role || c.Operation != scene.EquationOperation
            || c.Grade != 0 || !Available(new(c.KnowledgeGroup), c.Operation, c.Tier, c.UnknownRole).Contains(scene)) return false;
        var source = AsApplied(c);
        if (!source.IsValid || AppliedQuestionCatalogue.Reasoning(source) is not null) return false;
        var equation = Equation(c);
        var engine = new FindXEngine();
        var solved = engine.SolveInteger(equation.KnownValue, equation.ResultValue, equation.Operation, equation.UnknownIsLeftOperand);
        if (solved.Kind != FindXCoreSolutionKind.Unique || solved.Denominator != BigInteger.One || solved.Numerator != equation.CorrectAnswer) return false;
        var evaluated = engine.EvaluateIntegerLeftSide(solved.Numerator, solved.Denominator,
            equation.KnownValue, equation.Operation, equation.UnknownIsLeftOperand);
        return evaluated.Denominator == BigInteger.One && evaluated.Numerator == equation.ResultValue;
    }

    public static BasicQuestionDraft Draft(BasicQuestionContract c, int variant = 0) => AppliedQuestionCatalogue.Draft(AsApplied(c), variant);
    public static string Render(string template, BasicQuestionContract c) => AppliedQuestionCatalogue.Render(template, AsApplied(c));
    public static string Grammar(BasicQuestionContract c) => AppliedQuestionCatalogue.Grammar(AsApplied(c));
    public static BasicDraftValidation Validate(string raw, BasicQuestionContract c)
    {
        if (!IsValid(c)) return new(null, "InvalidContract");
        // Reviewed full clauses bind the quantities, actors, input dimensions and
        // target. A swapped unit/group, exposed answer or extra fact cannot pass.
        var validation = AppliedQuestionCatalogue.Validate(raw, AsApplied(c));
        return validation.IsValid ? new(validation.Draft, null, c) : validation with { Contract = null };
    }

    public static string Prompt(BasicQuestionContract c, string? correction, BasicQuestionDraft? example = null)
    {
        var s = Find(c.SceneId)!;
        var source = AsApplied(c);
        var units = AppliedQuestionCatalogue.InputUnits(source);
        return (QuizContentCatalog.Text(c.Language, "FindXQuestionCatalogue.Prompt.001"))
            + $"\nScene={s.SourceScene.Id}; group={s.Group}; stars={(int)c.Tier}; unknown={s.Role}."
            + $"\nGiven A: {s.KnownARole} [{units.A}]; given B: {s.KnownBRole} [{units.B}]; target: {s.TargetRole} [{c.AnswerUnit}]."
            + "\nUse only the placeholders and roles of this selected example; keep its target and affirmative facts:\n"
            + QuestionBankStore.SerializeDraft(example ?? Draft(c))
            + "\nReturn only JSON: given_a, given_b, question, solution_lead, unit_id."
            + (correction is null ? "" : "\nCorrect rejected output: " + correction);
    }
}

/// <summary>Rotate unknown roles and settings; the same catalogue serves C# and AI.</summary>
public sealed class FindXQuestionCycle(Random? random = null)
{
    private readonly Random _random = random ?? Random.Shared;
    private readonly Dictionary<(QuestionKnowledgeGroup, ArithmeticOperation, CurriculumTier, AppLanguage, FindXUnknownRole), Queue<string>> _cycles = [];
    public BasicQuestionContract Next(QuestionLearningProfile profile, ArithmeticOperation operation, CurriculumTier tier,
        AppLanguage language, FindXUnknownRole role = FindXUnknownRole.None)
    {
        var key = (profile.Group, operation, tier, language, role);
        if (!_cycles.TryGetValue(key, out var cycle) || cycle.Count == 0)
        {
            var scenes = FindXQuestionCatalogue.Available(profile, operation, tier, role).ToArray();
            if (scenes.Length == 0) throw new ArgumentException("InvalidLearningProfile");
            _random.Shuffle(scenes);
            cycle = new(scenes.Select(s => s.Id));
            _cycles[key] = cycle;
        }
        return FindXQuestionCatalogue.Create(profile, operation, tier, language, _random, sceneId: cycle.Dequeue());
    }
}
