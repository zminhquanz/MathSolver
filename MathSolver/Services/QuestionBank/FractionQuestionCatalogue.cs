using MathSolver.Models;
using MathSolver.Services.Core;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

/// <summary>Version 7: exact rational facts, dimensions and reviewed prose. Stars are skill levels, not grades.</summary>
public static class FractionQuestionCatalogue
{
    public const int Version = 7;
    public sealed record Scene(string Id, QuestionKnowledgeGroup Group, ArithmeticOperation Operation,
        bool WholeShare = false, bool CountResult = false, int MinimumStars = 1, string? Formula = null,
        WordProblemQuantity Quantity = WordProblemQuantity.Unspecified)
    {
        private string Unit(string culture, string field) => QuizContentCatalog.Text(culture, $"Fraction.{Id}.{field}");
        public string UnitA => Unit("vi-VN", "UnitA");
        public string UnitB => Unit("vi-VN", "UnitB");
        public string ResultUnit => Unit("vi-VN", "ResultUnit");
        public string EnUnitA => Unit("en-US", "UnitA");
        public string EnUnitB => Unit("en-US", "UnitB");
        public string EnResultUnit => Unit("en-US", "ResultUnit");
    }

    public static IReadOnlyList<Scene> All { get; } = QuizContentCatalog.LoadList<Scene>("FractionScenes");
    public static Scene? Find(string id) => All.FirstOrDefault(s => s.Id == id);
    public static IEnumerable<Scene> Available(QuestionLearningProfile profile, ArithmeticOperation operation, CurriculumTier tier)
        => All.Where(s => s.Group == profile.Group && s.Operation == operation && (int)tier >= s.MinimumStars);
    public static int MaximumDenominator(CurriculumTier tier) => tier switch
    { CurriculumTier.OneStar => 6, CurriculumTier.TwoStars => 12, CurriculumTier.ThreeStars => 12,
        CurriculumTier.FourStars => 20, CurriculumTier.FiveStars => 30, _ => 0 };

    public static BasicQuestionContract Create(QuestionLearningProfile profile, ArithmeticOperation op, CurriculumTier tier,
        AppLanguage language, Random? random = null, string? sceneId = null)
    {
        random ??= Random.Shared;
        var eligible = Available(profile, op, tier).ToArray();
        if (eligible.Length == 0 || language is not (AppLanguage.Vietnamese or AppLanguage.English)) throw new ArgumentException("InvalidFractionProfile");
        var s = sceneId is null ? eligible[random.Next(eligible.Length)] : eligible.Single(x => x.Id == sceneId);
        int max = MaximumDenominator(tier);
        for (int attempt = 0; attempt < 1000; attempt++)
        {
            int da = random.Next(2, max + 1), db = (int)tier == 1 ? da : (int)tier == 2 ? da : random.Next(2, max + 1);
            if ((int)tier == 2 && da % 2 == 0) db = da / 2;
            var a = new ReducedFraction(random.Next(1, da), da);
            var b = new ReducedFraction(random.Next(1, db), db);
            if (op == ArithmeticOperation.Subtract && Compare(a, b) < 0) (a, b) = (b, a);
            if (s.Group == QuestionKnowledgeGroup.Geometry && s.UnitA == "m" && s.UnitB == "m" && Compare(a, b) < 0)
                (a, b) = (b, a);
            if (s.CountResult) a = new(b.Numerator * random.Next(2, 9), b.Denominator);
            if (s.Id == "fraction-motion-distance") a = new(a.Numerator + random.Next(2, 6) * a.Denominator, a.Denominator);
            var c = new BasicQuestionContract(Version, op, tier, language, (int)a.Numerator, (int)b.Numerator,
                QuestionNames.Create(language, random, false), language == AppLanguage.Vietnamese ? s.ResultUnit : s.EnResultUnit, "",
                TopicId: profile.Group.ToString(), SceneId: s.Id, KnowledgeGroup: profile.Group,
                LeftDenominator: (int)a.Denominator, RightDenominator: (int)b.Denominator);
            if (IsValid(c)) return c;
        }
        throw new InvalidOperationException("CouldNotGenerateFractionFacts");
    }

    public static ReducedFraction Left(BasicQuestionContract c) => new(c.Left, c.LeftDenominator);
    public static ReducedFraction Right(BasicQuestionContract c) => new(c.Right, c.RightDenominator);
    private static int Compare(ReducedFraction a, ReducedFraction b) => (a.Numerator * b.Denominator).CompareTo(b.Numerator * a.Denominator);
    public static string Expression(BasicQuestionContract c)
        => (Find(c.SceneId)?.Formula ?? "{a} " + BasicArithmeticEngine.GetSymbol(c.Operation) + " {b}")
            .Replace("{a}", Left(c).ToString()).Replace("{b}", Right(c).ToString());
    public static ReducedFraction Answer(BasicQuestionContract c)
    {
        if (!EssayCalculationEvaluator.TryEvaluate(Expression(c), out var answer, out _)) throw new InvalidOperationException("InvalidFractionExpression");
        return new(answer.Numerator, answer.Denominator);
    }
    public static bool IsValid(BasicQuestionContract c)
    {
        var s = Find(c.SceneId);
        if (c.Version != Version || s is null || c.KnowledgeGroup != s.Group || c.TopicId != s.Group.ToString()
            || c.Operation != s.Operation || !Enum.IsDefined(c.Tier) || (int)c.Tier < s.MinimumStars
            || c.Language is not (AppLanguage.Vietnamese or AppLanguage.English) || c.Grade != 0
            || c.UnknownRole != FindXUnknownRole.None || c.Left <= 0 || c.Right <= 0
            || c.LeftDenominator <= 0 || c.RightDenominator <= 0 || c.LeftDenominator > MaximumDenominator(c.Tier)
            || c.RightDenominator > MaximumDenominator(c.Tier) || string.IsNullOrWhiteSpace(c.Subject)
            || c.Subject.Length > 100 || c.Subject.Any(ch => char.IsControl(ch) || char.IsDigit(ch) || ch is '{' or '}')
            || c.Unit != (c.Language == AppLanguage.Vietnamese ? s.ResultUnit : s.EnResultUnit) || c.GroupUnit != ""
            || c.Structure != BasicQuestionStructure.Increase || c.OtherSubject != "" || c.PartA != "" || c.PartB != "") return false;
        var a = Left(c); var b = Right(c);
        if (a.Numerator > a.Denominator * 8 || !s.CountResult && s.Id != "fraction-motion-distance" && a.Numerator >= a.Denominator || b.Numerator >= b.Denominator
            || a.Numerator != c.Left || a.Denominator != c.LeftDenominator || b.Numerator != c.Right || b.Denominator != c.RightDenominator) return false;
        if (c.Operation is ArithmeticOperation.Add or ArithmeticOperation.Subtract)
        {
            if ((int)c.Tier == 1 && a.Denominator != b.Denominator) return false;
            if ((int)c.Tier == 2 && a.Denominator % b.Denominator != 0 && b.Denominator % a.Denominator != 0) return false;
        }
        var result = Answer(c);
        if (result.Numerator <= 0 || s.CountResult && !result.Denominator.IsOne) return false;
        if (s.Id == "fraction-motion-distance" && (Compare(a, new(2, 1)) <= 0 || Compare(a, new(6, 1)) >= 0)) return false;
        if (s.Group == QuestionKnowledgeGroup.Geometry && s.UnitA == "m" && s.UnitB == "m" && Compare(a, b) < 0) return false;
        if (s.Group == QuestionKnowledgeGroup.Geometry && s.UnitA == "m²" && Compare(result, b) > 0) return false;
        if (s.WholeShare && (Compare(a, new(1, 1)) >= 0 || Compare(result, new(1, 1)) > 0)) return false;
        return true;
    }
    internal static ReviewedQuestionProse Prose(BasicQuestionContract c) => QuizStoryTemplates.For(c).Prose;

    public static BasicQuestionDraft Draft(BasicQuestionContract c) => Prose(c).Stories().First();
    public static BasicDraftValidation Validate(string raw, BasicQuestionContract c)
    {
        if (!IsValid(c)) return new(null, "InvalidContract");
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 12000) return new(null, "InvalidJson");
        try
        {
            using var json = JsonDocument.Parse(raw, new() { MaxDepth = 8 });
            string[] keys = ["given_a", "given_b", "question", "solution_lead", "unit_id"];
            if (json.RootElement.ValueKind != JsonValueKind.Object) return new(null, "InvalidFields");
            var props = json.RootElement.EnumerateObject().ToArray();
            if (props.Length != 5 || props.Any(p => !keys.Contains(p.Name) || p.Value.ValueKind != JsonValueKind.String)
                || props.Select(p => p.Name).Distinct().Count() != 5) return new(null, "InvalidFields");
            string F(string k) => json.RootElement.GetProperty(k).GetString()!;
            var d = new BasicQuestionDraft(F(keys[0]), F(keys[1]), F(keys[2]), F(keys[3]), F(keys[4]));
            string? error = QuestionProseLanguage.ValidateAndNormalize(d, c.Language, out d);
            if (error is not null) return new(null, error);
            var pool = Prose(c);
            if (d.UnitId != pool.UnitId) return new(null, "ChangedUnits");
            string mismatches = SemanticProseRules.MismatchedFields(c.Language, ("given_a", d.GivenA, pool.GivenA),
                ("given_b", d.GivenB, pool.GivenB), ("question", d.Question, pool.Questions), ("solution_lead", d.SolutionLead, pool.Leads));
            if (mismatches.Length > 0) return new(null, "ChangedRelationOrTarget", ErrorDetails: mismatches);
            return new(d, null, c);
        }
        catch (JsonException) { return new(null, "InvalidJson"); }
    }
    public static string Render(string text, BasicQuestionContract c)
    {
        var s = Find(c.SceneId)!; bool vi = c.Language == AppLanguage.Vietnamese;
        string rendered = text.Replace("{a}", Left(c).ToString()).Replace("{b}", Right(c).ToString()).Replace("{name}", c.Subject)
            .Replace("{unit_a}", vi ? s.UnitA : s.EnUnitA).Replace("{unit_b}", vi ? s.UnitB : s.EnUnitB).Replace("{unit}", c.Unit);
        return rendered.Length == 0 ? rendered : char.ToUpperInvariant(rendered[0]) + rendered[1..];
    }
    public static string Prompt(BasicQuestionContract c, string? correction, BasicQuestionDraft? example)
        => (QuizContentCatalog.Text(c.Language, "FractionQuestionCatalogue.Prompt.001"))
            + "\nScene: " + c.SceneId + "; group=" + c.KnowledgeGroup + "; stars=" + (int)c.Tier
            + "\n" + SemanticProseRules.PromptGuidance(c.Language) + "\n" + QuestionBankStore.SerializeDraft(example ?? Draft(c))
            + "\nReturn only JSON with given_a, given_b, question, solution_lead, unit_id."
            + (correction is null ? "" : "\nCorrect the rejected output: " + correction);

    public static ArithmeticQuizQuestion ToPractice(BasicQuestionContract c, MathWordProblem text, ArithmeticQuizMode mode, Random random)
    {
        var scene = Find(c.SceneId)!;
        var answer = Answer(c); ReducedFraction? presented = null;
        var wrong = mode == ArithmeticQuizMode.Essay ? [] : Distractors(answer, scene);
        if (mode == ArithmeticQuizMode.TrueFalse) presented = random.Next(2) == 0 ? answer : wrong[random.Next(wrong.Length)];
        ReducedFraction[] choices = mode == ArithmeticQuizMode.MultipleChoice ? [answer, .. wrong] : [];
        random.Shuffle(choices);
        var fraction = new FractionQuizContract(Left(c), (FractionOperation)c.Operation, Right(c), answer, presented, choices);
        // Multistep expressions retain exact rational grading and cannot be replaced by the first operation's answer.
        var expression = scene.Formula is null ? null : new ExpressionQuizContract(ExpressionQuizType.FractionWithBrackets,
            c.Tier, Expression(c), 3, 1, answer, presented, choices);
        if (expression is not null)
        {
            string a = Left(c).ToString(), b = Right(c).ToString();
            expression = expression with { EquivalentExpressions = scene.Formula == "({a} + {b}) × 2"
                ? [$"2 × ({a} + {b})", $"2 × ({b} + {a})", $"({b} + {a}) × 2"]
                : [$"({b} + {a}) ÷ 2"] };
        }
        return new(new(0, c.Operation, 1), mode, 0, null, presented.HasValue ? presented == answer : null, [],
            text, FractionProblem: expression is null ? fraction : null, ExpressionProblem: expression);
    }

    private static ReducedFraction[] Distractors(ReducedFraction answer, Scene scene)
    {
        var values = new List<ReducedFraction>();
        // Stay inside the reference whole; impossible budgets must not reveal the answer.
        for (int scale = 1; values.Count < 3; scale *= 2)
        foreach (int offset in new[] { -1, 1, -2, 2, -3, 3 })
        {
            var candidate = new ReducedFraction(answer.Numerator * scale + offset, answer.Denominator * scale);
            if (candidate.Numerator <= 0 || candidate == answer || values.Contains(candidate)
                || scene.WholeShare && candidate.Numerator > candidate.Denominator
                || scene.CountResult && !candidate.Denominator.IsOne) continue;
            values.Add(candidate);
            if (values.Count == 3) break;
        }
        return values.ToArray();
    }
}

/// <summary>Rotate scenes within a bounded history; used by C# practice and background AI jobs.</summary>
public sealed class FractionQuestionCycle(Random? random = null)
{
    private readonly Random _random = random ?? Random.Shared;
    private readonly Queue<string> _history = new();
    public BasicQuestionContract Next(QuestionLearningProfile profile, ArithmeticOperation op, CurriculumTier tier, AppLanguage language)
    {
        var scene = FractionQuestionCatalogue.Available(profile, op, tier).OrderBy(_ => _random.Next())
            .OrderBy(s => _history.Count(id => id == s.Id)).First();
        var c = FractionQuestionCatalogue.Create(profile, op, tier, language, _random, scene.Id);
        _history.Enqueue(scene.Id); if (_history.Count > 64) _history.Dequeue(); return c;
    }
}
