using MathSolver.Models;
using MathSolver.Services.Core;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

public sealed record AppliedQuestionScene(string Id, QuestionKnowledgeGroup Group, ArithmeticOperation Operation,
    int MinimumStars, int Capacity, int MaxFactor, string UnitId,
    int Conversion = 1, int RightDisplayFactor = 1, string? StoryContextId = null)
{
    public BasicQuestionStructure Structure => Operation switch
    {
        ArithmeticOperation.Add when Id.EndsWith("inverse-larger", StringComparison.Ordinal) => BasicQuestionStructure.AddComparisonInverse,
        ArithmeticOperation.Add when Id.EndsWith("larger", StringComparison.Ordinal) => BasicQuestionStructure.AddComparisonMore,
        ArithmeticOperation.Add when Id.EndsWith("original", StringComparison.Ordinal) => BasicQuestionStructure.RecoverInitial,
        ArithmeticOperation.Add when Id.EndsWith("total", StringComparison.Ordinal) => BasicQuestionStructure.Combine,
        ArithmeticOperation.Add => BasicQuestionStructure.Increase,
        ArithmeticOperation.Subtract when Id.EndsWith("smaller", StringComparison.Ordinal) => BasicQuestionStructure.SubComparisonLess,
        ArithmeticOperation.Subtract when Id.EndsWith("inverse-comparison", StringComparison.Ordinal) => BasicQuestionStructure.SubComparisonInverse,
        ArithmeticOperation.Subtract when Id.EndsWith("difference", StringComparison.Ordinal) => BasicQuestionStructure.Difference,
        ArithmeticOperation.Subtract when Id.EndsWith("part", StringComparison.Ordinal) => BasicQuestionStructure.FindPart,
        ArithmeticOperation.Subtract => BasicQuestionStructure.Remaining,
        ArithmeticOperation.Multiply when Id.EndsWith("times", StringComparison.Ordinal) => BasicQuestionStructure.TimesAsMany,
        ArithmeticOperation.Multiply => BasicQuestionStructure.EqualGroups,
        ArithmeticOperation.Divide when Id.EndsWith("times-fewer", StringComparison.Ordinal) => BasicQuestionStructure.TimesFewer,
        ArithmeticOperation.Divide when Id.EndsWith("count-groups", StringComparison.Ordinal) => BasicQuestionStructure.CountGroups,
        _ => BasicQuestionStructure.EqualShare
    };
}

/// <summary>Version 5: knowledge group, star difficulty, context capacity and dimensions.</summary>
public static partial class AppliedQuestionCatalogue
{
    public const int Version = 5;
    public static IReadOnlyList<AppliedQuestionScene> All { get; } = QuizContentCatalog.LoadList<AppliedQuestionScene>("AppliedScenes");

    public static IEnumerable<AppliedQuestionScene> Available(QuestionLearningProfile p, ArithmeticOperation operation, CurriculumTier tier)
        => All.Where(s => p.Allows(operation) && Enum.IsDefined(tier) && p.Includes(s.Group) && s.Operation == operation
            && (int)tier >= s.MinimumStars);
    public static AppliedQuestionScene? Find(string id) => All.FirstOrDefault(s => s.Id == id);
    public static WordProblemQuantity Quantity(BasicQuestionContract c) => Find(c.SceneId)!.Group switch
    {
        QuestionKnowledgeGroup.Objects => WordProblemQuantity.Count,
        QuestionKnowledgeGroup.Money => WordProblemQuantity.Money,
        QuestionKnowledgeGroup.Mass or QuestionKnowledgeGroup.Transport => WordProblemQuantity.Mass,
        QuestionKnowledgeGroup.Motion when c.SceneId.StartsWith("motion-speed", StringComparison.Ordinal) => WordProblemQuantity.Speed,
        QuestionKnowledgeGroup.Time => WordProblemQuantity.Time,
        QuestionKnowledgeGroup.Measurement => WordProblemQuantity.Capacity,
        QuestionKnowledgeGroup.Geometry when Find(c.SceneId)!.UnitId.Contains('³') => WordProblemQuantity.Volume,
        QuestionKnowledgeGroup.Geometry when Find(c.SceneId)!.UnitId.Contains('²') => WordProblemQuantity.Area,
        QuestionKnowledgeGroup.Geometry => WordProblemQuantity.Distance,
        QuestionKnowledgeGroup.Packaging or QuestionKnowledgeGroup.Production or QuestionKnowledgeGroup.Data => WordProblemQuantity.Count,
        _ => WordProblemQuantity.Distance
    };
    public static string? ConversionStep(BasicQuestionContract c)
    {
        var scene = Find(c.SceneId)!;
        if (scene.RightDisplayFactor != 1)
            return (c.Right * scene.RightDisplayFactor).ToString(CultureInfo.InvariantCulture)
                + (QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.ConversionStep.001")) + c.Right.ToString(CultureInfo.InvariantCulture)
                + (c.Language == AppLanguage.Vietnamese ? " giờ" : c.Right == 1 ? " hour" : " hours");
        if (scene.Conversion == 1) return null;
        var units = InputUnits(c);
        return Render("{a} {unit_a}", c) + " = " + c.Left.ToString(CultureInfo.InvariantCulture) + " " + scene.UnitId;
    }
    public static BasicQuestionContract Create(QuestionLearningProfile profile, ArithmeticOperation operation, CurriculumTier tier,
        AppLanguage language, Random? random = null, string? sceneId = null)
    {
        random ??= Random.Shared;
        var choices = Available(profile, operation, tier).Where(s => sceneId is null || s.Id == sceneId).ToArray();
        if (choices.Length == 0) throw new ArgumentException("InvalidLearningProfile");
        var scene = choices[random.Next(choices.Length)];
        int cap = Ceiling(profile, scene, tier, language);
        int factorMax = Math.Min(scene.MaxFactor, profile.MaximumFactor(tier));
        (int a, int b) = Numbers(scene, tier, cap, factorMax, random);
        string subject = QuestionNames.Create(language, random), other = QuestionNames.Create(language, random);
        if (other == subject) other = QuizContentCatalog.Text(language, "AppliedQuestionCatalogue.Create.002");
        if (other == subject) other = QuizContentCatalog.Text(language, "AppliedQuestionCatalogue.Create.003");
        if (profile.Group == QuestionKnowledgeGroup.Objects && Math.Max(a, b) > 100)
        { subject = QuizContentCatalog.Text(language, "AppliedQuestionCatalogue.Create.004");
          other = QuizContentCatalog.Text(language, "AppliedQuestionCatalogue.Create.005"); }
        if (scene.Group == QuestionKnowledgeGroup.Mass && scene.Conversion == 1 && Math.Max(a, b) > 50)
        { subject = QuizContentCatalog.Text(language, "AppliedQuestionCatalogue.Create.006");
          other = QuizContentCatalog.Text(language, "AppliedQuestionCatalogue.Create.007"); }
        if (scene.Group == QuestionKnowledgeGroup.Transport)
        { subject = QuizContentCatalog.Text(language, "AppliedQuestionCatalogue.Create.008");
          other = QuizContentCatalog.Text(language, "AppliedQuestionCatalogue.Create.009"); }
        if (profile.Group == QuestionKnowledgeGroup.Motion)
        { subject = QuizContentCatalog.Text(language, "AppliedQuestionCatalogue.Create.010");
          other = QuizContentCatalog.Text(language, "AppliedQuestionCatalogue.Create.011"); }
        SetSceneActors(scene, language, a, b, ref subject, ref other);
        string unit = AnswerUnit(scene, language);
        int perGroup = scene.Structure == BasicQuestionStructure.CountGroups ? b : operation == ArithmeticOperation.Divide ? a / b : a;
        string groupUnit = profile.Group == QuestionKnowledgeGroup.Objects && perGroup > 100
            ? QuizContentCatalog.Text(language, "AppliedQuestionCatalogue.Create.012")
            : QuizContentCatalog.Text(language, "AppliedQuestionCatalogue.Create.013");
        groupUnit = SceneGroupUnit(scene, language) ?? groupUnit;
        return new(Version, operation, tier, language, a, b, subject, unit,
            groupUnit,
            scene.Structure,
            other, profile.Group.ToString(), scene.Id, KnowledgeGroup: profile.Group);
    }
    private static int Capacity(AppliedQuestionScene s, AppLanguage language) => s.Group == QuestionKnowledgeGroup.Money
        && language == AppLanguage.English ? 300 : s.Capacity;
    private static int Ceiling(QuestionLearningProfile p, AppliedQuestionScene s, CurriculumTier tier, AppLanguage language)
        => s.Group == QuestionKnowledgeGroup.Motion ? Capacity(s, language)
            : s.Group == QuestionKnowledgeGroup.Money
            ? Math.Min(language == AppLanguage.Vietnamese
                ? new[] { 20000, 50000, 100000, 300000, 500000 }[(int)tier - 1]
                : new[] { 10, 30, 100, 200, 300 }[(int)tier - 1], Capacity(s, language))
            : Math.Min(p.ArithmeticCeiling(tier), Capacity(s, language));
    public static string AnswerUnit(AppliedQuestionScene s, AppLanguage language) => s.StoryContextId is { } id
        ? QuizStoryContextCatalog.Find(id).Unit(language) : s.UnitId == "currency"
        ? QuizContentCatalog.Text(language, "AppliedQuestionCatalogue.AnswerUnit.014")
        : ExpandedUnit(s.UnitId, language, false) ?? QuestionUnits.Find(s.UnitId)?.Item(language) ?? s.UnitId;
    public static string ResultUnit(BasicQuestionContract c)
    {
        if (Find(c.SceneId)!.StoryContextId is not null)
            return c.Language == AppLanguage.English && c.Answer.IsOne ? ExpandedSingular(c.Unit) ?? c.Unit : c.Unit;
        if (c.Structure == BasicQuestionStructure.CountGroups)
            return c.Language == AppLanguage.Vietnamese || c.Answer.IsOne ? c.GroupUnit : c.GroupUnit + "s";
        if (c.Language == AppLanguage.English && c.Answer.IsOne)
            return ExpandedUnit(Find(c.SceneId)!.UnitId, c.Language, true) ?? QuestionUnits.Find(Find(c.SceneId)!.UnitId)?.Item(c.Language, true) ?? (c.Unit == "dollars" ? "dollar" : c.Unit);
        return c.Unit;
    }
    public static (string A, string B) InputUnits(BasicQuestionContract c)
    {
        var s = Find(c.SceneId)!;
        bool vi = c.Language == AppLanguage.Vietnamese;
        if (ExpandedInputUnits(s, c.Language) is { } expanded) return expanded;
        if (s.Id.EndsWith("recover-groups", StringComparison.Ordinal)) return (c.GroupUnit, c.Unit);
        if (s.Structure == BasicQuestionStructure.CountGroups) return (c.Unit, c.Unit);
        if (s.Structure is BasicQuestionStructure.TimesAsMany or BasicQuestionStructure.TimesFewer)
            return (c.Unit, QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.InputUnits.015"));
        if (s.Conversion != 1) return (s.UnitId == "g" ? "kg" : s.UnitId == "ml" ? "l" : "km",
            c.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide
                ? s.Group == QuestionKnowledgeGroup.Mass ? QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.InputUnits.016") : QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.InputUnits.017")
                : s.UnitId);
        string timeUnit = s.RightDisplayFactor == 60 ? QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.InputUnits.018") : QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.InputUnits.019");
        if (s.Id.StartsWith("motion-distance", StringComparison.Ordinal)) return ("km/h", timeUnit);
        if (s.Id.StartsWith("motion-speed", StringComparison.Ordinal)) return ("km", timeUnit);
        if (c.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
            return (c.Unit, s.Group == QuestionKnowledgeGroup.Money ? QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.InputUnits.020")
                : s.Group == QuestionKnowledgeGroup.Objects ? c.GroupUnit
                : s.Group == QuestionKnowledgeGroup.Length ? QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.InputUnits.021")
                : s.Group == QuestionKnowledgeGroup.Mass ? QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.InputUnits.022") : QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.InputUnits.023"));
        return (c.Unit, c.Unit);
    }
    private static (int, int) Numbers(AppliedQuestionScene s, CurriculumTier tier, int cap, int factorMax, Random random)
    {
        if (ExpandedNumbers(s, tier, cap, factorMax, random) is { } numbers) return numbers;
        if (s.Conversion != 1)
        {
            // Five stars can have fractional kg/km, with an exact integer g/m answer.
            int step = tier == CurriculumTier.FiveStars ? 100 : 1000;
            if (s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
            {
                int factor = random.Next(2, Math.Min(factorMax, cap / step) + 1);
                int per = random.Next(1, Math.Max(2, cap / (factor * step) + 1)) * step;
                return s.Operation == ArithmeticOperation.Multiply ? (per, factor) : (per * factor, factor);
            }
            int a = random.Next(1, Math.Max(2, cap / (2 * step))) * step;
            return (a, random.Next(1, Math.Max(2, s.Operation == ArithmeticOperation.Subtract ? a : cap - a)));
        }
        if (s.Id.StartsWith("motion-distance", StringComparison.Ordinal)) return (random.Next(20, 91), random.Next(1, Math.Min(8, factorMax) + 1));
        if (s.Id.StartsWith("motion-speed", StringComparison.Ordinal)) { int hours = random.Next(1, Math.Min(6, factorMax) + 1); return (random.Next(20, 91) * hours, hours); }
        int moneyStep = s.Group == QuestionKnowledgeGroup.Money && cap >= 1000 ? 1000 : 1;
        int bound = Math.Max(3, cap / moneyStep);
        if (s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
        {
            int factor = random.Next(2, Math.Min(factorMax, bound / 2) + 1);
            int perCeiling = bound / factor;
            if (s.Group == QuestionKnowledgeGroup.Money) perCeiling = Math.Min(perCeiling, moneyStep == 1000 ? 30 : 15);
            if (s.Group == QuestionKnowledgeGroup.Transport) perCeiling = Math.Min(perCeiling, 2000);
            if (s.Group == QuestionKnowledgeGroup.Mass) perCeiling = Math.Min(perCeiling, 50);
            int minimum = s.Group == QuestionKnowledgeGroup.Money && moneyStep == 1000 ? 3 : 1;
            int per = random.Next(minimum, Math.Max(minimum + 1, perCeiling + 1)) * moneyStep;
            return s.Operation == ArithmeticOperation.Multiply ? (per, factor) : (per * factor, factor);
        }
        int desired = (int)tier - 1;
        int maxCarries = desired;
        (int A, int B) best = (moneyStep, moneyStep);
        int bestCount = -1;
        for (int attempt = 0; attempt < 128; attempt++)
        {
            int total = random.Next(2, bound + 1), part = random.Next(1, total);
            int a = (s.Operation == ArithmeticOperation.Add ? total - part : total) * moneyStep, b = part * moneyStep;
            int count = s.Operation == ArithmeticOperation.Add ? AdditionQuestionCatalogue.CountCarries(a, b) : ArithmeticQuestionCatalogue.CountBorrows(a, b);
            if (count > maxCarries) continue;
            if (count > bestCount) { best = (a, b); bestCount = count; }
            if (count >= desired) return (a, b);
        }
        // Guaranteed no-carry/no-borrow fallback, within every supported capacity.
        return bestCount >= 0 ? best : s.Operation == ArithmeticOperation.Add ? (moneyStep, moneyStep) : (2 * moneyStep, moneyStep);
    }
    public static bool IsValid(BasicQuestionContract c)
    {
        var p = new QuestionLearningProfile(c.KnowledgeGroup);
        var s = Find(c.SceneId);
        if (s is null || !Available(p, c.Operation, c.Tier).Contains(s) || c.TopicId != p.Group.ToString()
            || c.Unit != AnswerUnit(s, c.Language) || !ValidGroup(c)
            || c.PartA != "" || c.PartB != "" || c.Structure != s.Structure) return false;
        // Grade is historical metadata only. Old saved templates retain their valid
        // preview values; FreshFacts regenerates them under the star/group policy.
        if (c.Grade is < 0 or > 5) return false;
        long max = c.Grade == 0 ? Ceiling(p, s, c.Tier, c.Language) : Capacity(s, c.Language);
        if (s.Group == QuestionKnowledgeGroup.Motion) max = s.Capacity;
        if (c.Left <= 0 || c.Right <= 0 || c.Left > max || c.Right > max || c.Answer <= 0 || c.Answer > max
            || !ExpandedFactsValid(c, s)) return false;
        if (s.Conversion != 1 && (c.Left % (c.Tier == CurriculumTier.FiveStars ? 100 : 1000) != 0)) return false;
        if (c.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
        {
            if (c.Right > (c.Grade == 0 ? Math.Min(s.MaxFactor, p.MaximumFactor(c.Tier)) : s.MaxFactor)) return false;
            int per = c.Operation == ArithmeticOperation.Divide ? (int)c.Answer : c.Left;
            if (s.Group == QuestionKnowledgeGroup.Transport && per > 2000
                || s.Group == QuestionKnowledgeGroup.Mass && s.Conversion == 1 && per > 50) return false;
        }
        if (s.Id.StartsWith("motion-distance", StringComparison.Ordinal) && c.Left is < 20 or > 90
            || s.Id.StartsWith("motion-speed", StringComparison.Ordinal) && (c.Answer < 20 || c.Answer > 90)) return false;
        if (c.Grade == 0 && s.Conversion == 1 && !HasDerivedAnswer(s) && c.Operation is ArithmeticOperation.Add or ArithmeticOperation.Subtract)
        {
            int count = c.Operation == ArithmeticOperation.Add ? AdditionQuestionCatalogue.CountCarries(c.Left, c.Right)
                : ArithmeticQuestionCatalogue.CountBorrows(c.Left, c.Right);
            if (count > (int)c.Tier - 1) return false;
        }
        if (c.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide && s.Group == QuestionKnowledgeGroup.Money)
        {
            int price = c.Operation == ArithmeticOperation.Divide ? (int)c.Answer : c.Left;
            if (price > (c.Language == AppLanguage.Vietnamese ? 30000 : 15)
                || c.Language == AppLanguage.Vietnamese && (price < 3000 || price % 1000 != 0)) return false;
        }
        return true;
    }
    private static bool ValidGroup(BasicQuestionContract c)
    {
        if (SceneGroupUnit(Find(c.SceneId)!, c.Language) is { } group) return c.GroupUnit == group;
        int per = c.Structure == BasicQuestionStructure.CountGroups ? c.Right : c.Operation == ArithmeticOperation.Divide ? c.Left / c.Right : c.Left;
        string expected = c.KnowledgeGroup == QuestionKnowledgeGroup.Objects && per > 100
            ? QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.ValidGroup.024")
            : QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.ValidGroup.025");
        return c.GroupUnit == expected;
    }
    public static BasicQuestionDraft Draft(BasicQuestionContract c, int variant = 0)
    {
        var examples = QuizStoryTemplates.For(c).Examples;
        return examples[variant == 0 ? 0 : Math.Min(1, examples.Length - 1)];
    }

    public static BasicDraftValidation Validate(string raw, BasicQuestionContract c)
    {
        if (!c.IsValid) return new(null, "InvalidContract");
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 12000) return new(null, "InvalidJson");
        try
        {
            using var json = JsonDocument.Parse(raw.Trim(), new() { MaxDepth = 8 });
            string[] keys = ["given_a", "given_b", "question", "solution_lead", "unit_id"];
            if (json.RootElement.ValueKind != JsonValueKind.Object) return new(null, "InvalidJson");
            var props = json.RootElement.EnumerateObject().ToArray();
            if (props.Length != 5 || props.Any(p => !keys.Contains(p.Name) || p.Value.ValueKind != JsonValueKind.String)
                || props.Select(p => p.Name).Distinct().Count() != 5) return new(null, "InvalidFields");
            string F(string key) => json.RootElement.GetProperty(key).GetString()!;
            var d = new BasicQuestionDraft(F(keys[0]), F(keys[1]), F(keys[2]), F(keys[3]), F(keys[4]));
            var languageError = QuestionProseLanguage.ValidateAndNormalize(d, c.Language, out d);
            if (languageError is not null) return new(null, languageError);
            if (d.UnitId != Find(c.SceneId)!.UnitId) return new(null, "ChangedUnits");
            // Compare ordered role/relation anchors against the C# situation.
            // Numeric/unit/actor slots and their order bind the roles for mixed dimensions.
            var prose = ReviewedQuestionProse.For(c)!;
            string mismatches = SemanticProseRules.MismatchedFields(c.Language, ("given_a", d.GivenA, prose.GivenA),
                ("given_b", d.GivenB, prose.GivenB), ("question", d.Question, prose.Questions), ("solution_lead", d.SolutionLead, prose.Leads));
            if (mismatches.Length > 0) return new(null, "ChangedRelationOrTarget", ErrorDetails: mismatches);
            return new(d, null, c);
        }
        catch (JsonException) { return new(null, "InvalidJson"); }
    }
    public static string Render(string template, BasicQuestionContract c)
    {
        var s = Find(c.SceneId)!;
        var units = InputUnits(c);
        string left = ((decimal)c.Left / s.Conversion).ToString("0.###", c.Language == AppLanguage.Vietnamese
            ? CultureInfo.GetCultureInfo("vi-VN") : CultureInfo.InvariantCulture);
        string Singular(string unit, decimal number) => c.Language != AppLanguage.English || number != 1 ? unit
            : ExpandedSingular(unit) ?? unit switch { "books" => "book", "dollars" => "dollar", "notebooks" => "notebook", "hours" => "hour", "minutes" => "minute",
                "trucks" => "truck", "journeys" => "journey", "bags" => "bag", "legs" => "leg",
                "pencils" => "pencil", "cards" => "card", _ => unit };
        // Quantity-specific units; question/solution units remain plural.
        template = template.Replace("{a} {unit}", "{a} " + Singular(c.Unit, (decimal)c.Left / s.Conversion))
            .Replace("{b} {unit}", "{b} " + Singular(c.Unit, c.Right));
        template = template.Replace("{a} {unit_a}", "{a} " + Singular(units.A, (decimal)c.Left / s.Conversion))
            .Replace("{b} {unit_a}", "{b} " + Singular(units.A, c.Right * s.RightDisplayFactor))
            .Replace("{a} {unit_b}", "{a} " + Singular(units.B, (decimal)c.Left / s.Conversion))
            .Replace("{b} {unit_b}", "{b} " + Singular(units.B, c.Right * s.RightDisplayFactor));
        string text = template.Replace("{unit_a}", units.A).Replace("{unit_b}", units.B)
            .Replace("{unit}", c.Unit).Replace("{group}", c.GroupUnit)
            .Replace("{groups}", c.Language == AppLanguage.Vietnamese ? c.GroupUnit : c.GroupUnit + "s")
            .Replace("{name}", c.Subject).Replace("{other}", c.OtherSubject)
            .Replace("{a}", left).Replace("{b}", (c.Right * s.RightDisplayFactor).ToString(CultureInfo.InvariantCulture));
        if (c.Language == AppLanguage.English)
            text = Regex.Replace(text, @"\b1 (more |fewer |additional )?" + Regex.Escape(c.Unit) + @"\b",
                m => "1 " + m.Groups[1].Value + Singular(c.Unit, 1));
        return Regex.Replace(text, @"(^|[.!?]\s+)(\p{Ll})", m => m.Groups[1].Value + m.Groups[2].Value.ToUpperInvariant());
    }
    public static string Prompt(BasicQuestionContract c, string? correction, BasicQuestionDraft? example = null)
        => (QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.Prompt.026"))
            + "\nScene: " + c.SceneId + "; group=" + c.KnowledgeGroup + "; stars=" + (int)c.Tier
            + (Find(c.SceneId)!.StoryContextId is { } id ? "; activity=" + QuizStoryContextCatalog.Find(id).Setting(c.Language) : "")
            + "; input units=" + JsonSerializer.Serialize(new { GivenA = InputUnits(c).A, GivenB = InputUnits(c).B },
                new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "; answer unit=" + c.AnswerUnit
            + "\nPreserve the facts and target of this example. Equivalent wording is allowed; do not copy wording rejected as duplicate:\n"
            + QuestionBankStore.SerializeDraft(example ?? Draft(c))
            + "\nReturn only JSON with given_a, given_b, question, solution_lead, unit_id."
            + (correction is null ? "" : "\nCorrect the rejected output: " + correction);
    public static string Grammar(BasicQuestionContract c)
        => ReviewedQuestionProse.For(c)!.Grammar(c, new HashSet<string>(StringComparer.Ordinal));
}
