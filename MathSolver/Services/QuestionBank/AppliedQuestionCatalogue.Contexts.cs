using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

public static partial class AppliedQuestionCatalogue
{

    private static readonly IReadOnlyDictionary<string, (string Vi, string En, string Singular)> ExpandedUnits =
        new Dictionary<string, (string, string, string)> {
            ["years"] = ("tuổi", "years", "year"), ["minutes"] = ("phút", "minutes", "minute"),
            ["litres"] = ("l", "litres", "litre"), ["packs"] = ("gói", "packs", "pack"),
            ["products"] = ("sản phẩm", "products", "product"), ["visits"] = ("lượt tham gia", "visits", "visit"),
            ["students"] = ("học sinh", "students", "student"), ["points"] = ("điểm", "points", "point"),
            ["chickens"] = ("con gà", "chickens", "chicken"), ["pages"] = ("trang sách", "pages", "page"),
            ["seedlings"] = ("cây con", "seedlings", "seedling"), ["bottles"] = ("chai nhựa", "plastic bottles", "plastic bottle"),
            ["rolls"] = ("ổ bánh mì", "bread rolls", "bread roll"), ["mangoes"] = ("quả xoài", "mangoes", "mango"),
            ["meals"] = ("suất ăn", "meals", "meal"), ["eggs"] = ("quả trứng", "eggs", "egg"),
            ["paper-flowers"] = ("bông hoa giấy", "paper flowers", "paper flower"),
            ["chairs"] = ("chiếc ghế", "chairs", "chair"), ["responses"] = ("phiếu trả lời", "responses", "response"),
            ["gift-packs"] = ("gói quà", "gift packs", "gift pack"),
            ["passenger-journeys"] = ("lượt khách", "passenger journeys", "passenger journey"),
            ["litres-water"] = ("lít nước", "litres of water", "litre of water") };
    private static string? ExpandedUnit(string id, AppLanguage language, bool singular) => ExpandedUnits.TryGetValue(id, out var u)
        ? language == AppLanguage.Vietnamese ? u.Vi : singular ? u.Singular : u.En : null;
    private static string? ExpandedSingular(string unit) => ExpandedUnits.Values.FirstOrDefault(u => u.En == unit).Singular;

    private static (string Vi, string En) CountedActivityGroup(string prefix) => prefix switch {
        "classroom" => ("nhóm học sinh", "student group"), "poultry" => ("chuồng", "pen"),
        "reading" => ("ngày đọc sách", "reading day"), "sports" => ("trận đấu", "match"),
        "visits" => ("ngày mở cửa", "opening day"), "planting" => ("đội trồng cây", "planting team"),
        "recycling" => ("đội thu gom", "recycling team"), "bakery" => ("lò bánh", "bakery"),
        "harvest" => ("nông trại", "farm"), _ => ("nhóm", "group") };
    private static readonly string[] CountedPrefixes = ["classroom", "poultry", "reading", "sports",
        "visits", "planting", "recycling", "bakery", "harvest"];
    private static string? CountedPrefix(AppliedQuestionScene s) => CountedPrefixes.FirstOrDefault(p => s.Id.StartsWith(p + "-", StringComparison.Ordinal));
    private static string? SceneGroupUnit(AppliedQuestionScene s, AppLanguage language)
    {
        if (s.StoryContextId is { } id) return QuizStoryContextCatalog.Find(id).Period(language);
        if (s.Group == QuestionKnowledgeGroup.Packaging) return QuizContentCatalog.Text(language, "AppliedQuestionCatalogue.Contexts.SceneGroupUnit.001");
        if (s.Group == QuestionKnowledgeGroup.Objects && CountedPrefix(s) is { } prefix) {
            var group = CountedActivityGroup(prefix);
            return language == AppLanguage.Vietnamese ? group.Vi : group.En;
        }
        return null;
    }
    private static (string A, string B)? ExpandedInputUnits(AppliedQuestionScene s, AppLanguage lang)
    {
        bool vi = lang == AppLanguage.Vietnamese;
        string unit = AnswerUnit(s, lang);
        if (s.StoryContextId is { } id) {
            var context = QuizStoryContextCatalog.Find(id);
            return s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide
                ? (unit, vi ? context.ViPeriod : PluralGroup(context.EnPeriod)) : (unit, unit);
        }
        if (s.Group == QuestionKnowledgeGroup.Production && s.Id.EndsWith("recover-groups", StringComparison.Ordinal)) return (QuizContentCatalog.Text(lang, "AppliedQuestionCatalogue.Contexts.ExpandedInputUnits.002"), unit);
        if (s.Id is "age-past-total" or "age-future-left") return (unit, QuizContentCatalog.Text(lang, "AppliedQuestionCatalogue.Contexts.ExpandedInputUnits.003"));
        if (s.Group == QuestionKnowledgeGroup.Objects && CountedPrefix(s) is { } prefix
            && s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide) {
            var group = CountedActivityGroup(prefix);
            return (unit, vi ? group.Vi : PluralGroup(group.En));
        }
        if (s.Group == QuestionKnowledgeGroup.Time && s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
            return (unit, QuizContentCatalog.Text(lang, "AppliedQuestionCatalogue.Contexts.ExpandedInputUnits.004"));
        if (s.Group == QuestionKnowledgeGroup.Measurement)
            return (s.Conversion == 1 ? unit : "l", s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide ? QuizContentCatalog.Text(lang, "AppliedQuestionCatalogue.Contexts.ExpandedInputUnits.005") : unit);
        if (s.Group == QuestionKnowledgeGroup.Geometry)
            return s.Id switch {
                "garden-area-groups" or "garden-perimeter" => ("m", "m"),
                "garden-side-share" => ("m²", "m"), "tank-volume-groups" => ("m²", "m"),
                "tank-height-share" => ("m³", "m²"), _ => (unit, unit) };
        if (s.Group == QuestionKnowledgeGroup.Packaging && s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
            return (AnswerUnit(s with { UnitId = "notebooks" }, lang), s.Id.EndsWith("count-groups", StringComparison.Ordinal)
                || HasDerivedAnswer(s) ? AnswerUnit(s with { UnitId = "notebooks" }, lang) : QuizContentCatalog.Text(lang, "AppliedQuestionCatalogue.Contexts.ExpandedInputUnits.006"));
        if (s.Group == QuestionKnowledgeGroup.Production && s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
            return (unit, QuizContentCatalog.Text(lang, "AppliedQuestionCatalogue.Contexts.ExpandedInputUnits.007"));
        if (s.Group == QuestionKnowledgeGroup.Data && s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
            return (unit, QuizContentCatalog.Text(lang, "AppliedQuestionCatalogue.Contexts.ExpandedInputUnits.008"));
        return null;
    }
    private static string PluralGroup(string singular) => singular.EndsWith("bakery", StringComparison.Ordinal)
        ? singular[..^1] + "ies" : singular.EndsWith("match", StringComparison.Ordinal) ? singular + "es" : singular + "s";
    public static AppliedArithmeticReasoning? Reasoning(BasicQuestionContract c)
    {
        var rule = c.SceneId switch { "garden-perimeter" => AppliedArithmeticRule.RectanglePerimeter,
            "packing-remainder" => AppliedArithmeticRule.Remainder, "packing-minimum" => AppliedArithmeticRule.MinimumGroups,
            _ => AppliedArithmeticRule.Ordinary };
        return rule == AppliedArithmeticRule.Ordinary ? null : new(c.Left, c.Right, rule);
    }
    private static bool HasDerivedAnswer(AppliedQuestionScene s) => s.Id is "garden-perimeter" or "packing-remainder" or "packing-minimum";
    private static (int, int)? ExpandedNumbers(AppliedQuestionScene s, CurriculumTier tier, int cap, int factor, Random random)
    {
        if (s.StoryContextId is not null && s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide) {
            int count = random.Next(2, Math.Min(factor, cap / 2) + 1);
            int per = random.Next(1, Math.Max(2, cap / count + 1));
            return s.Operation == ArithmeticOperation.Divide ? (per * count, count) : (per, count);
        }
        if (s.Group == QuestionKnowledgeGroup.Measurement && s.Conversion != 1
            && s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide) {
            int step = tier == CurriculumTier.FiveStars ? 100 : 1000;
            int count = random.Next(2, Math.Min(factor, cap / step) + 1);
            int per = random.Next(1, Math.Min(20000, cap / count) / step + 1) * step;
            return s.Operation == ArithmeticOperation.Divide ? (per * count, count) : (per, count);
        }
        if (s.Id == "garden-perimeter") {
            int width = random.Next(1, Math.Max(2, cap / 8));
            return (random.Next(width, Math.Max(width + 1, cap / 2 - width + 1)), width);
        }
        if (s.Id is "packing-remainder" or "packing-minimum") {
            int size = random.Next(2, Math.Min(factor, cap / 3) + 1);
            int full = random.Next(1, Math.Max(2, (cap - size + 1) / size));
            return (full * size + random.Next(1, size), size);
        }
        if (s.Id is "garden-area-groups" or "garden-side-share" or "tank-volume-groups" or "tank-height-share") {
            if (s.Id == "tank-height-share") {
                int area = random.Next(2, Math.Min(factor, 30) + 1);
                return (area * random.Next(1, 6), area);
            }
            int width = random.Next(2, Math.Min((int)Math.Sqrt(cap), Math.Min(factor, s.Id.StartsWith("tank-", StringComparison.Ordinal) ? 5 : 20)) + 1);
            int length = random.Next(width, Math.Max(width + 1, Math.Min(cap / width, s.Id.StartsWith("tank-", StringComparison.Ordinal) ? 30 : 100) + 1));
            return s.Operation == ArithmeticOperation.Divide ? (width * length, width) : (length, width);
        }
        if (s.Conversion == 1 && s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide
            && s.Group is QuestionKnowledgeGroup.Time or QuestionKnowledgeGroup.Measurement or QuestionKnowledgeGroup.Production or QuestionKnowledgeGroup.Data or QuestionKnowledgeGroup.Packaging) {
            int count = random.Next(2, Math.Min(factor, cap / 2) + 1);
            int perMax = s.Group switch { QuestionKnowledgeGroup.Time => 60, QuestionKnowledgeGroup.Measurement => 20,
                QuestionKnowledgeGroup.Packaging => 100, _ => 5000 };
            int per = random.Next(1, Math.Max(2, Math.Min(perMax, cap / count) + 1));
            return s.Operation == ArithmeticOperation.Divide ? (per * count, count) : (per, count);
        }
        return null;
    }
    private static bool ExpandedFactsValid(BasicQuestionContract c, AppliedQuestionScene s)
    {
        if (s.StoryContextId is { } id) return c.Left <= QuizStoryContextCatalog.Find(id).MaximumPerPeriod;
        if (Reasoning(c) is { } r) return r.Matches(c.Expression) && (s.Id != "garden-perimeter" || c.Left >= c.Right);
        if (s.Id is "garden-area-groups" or "garden-side-share")
            return (c.Operation == ArithmeticOperation.Divide ? c.Answer : c.Left) >= c.Right && c.Right <= 20;
        if (s.Id == "tank-volume-groups") return c.Right <= 5 && c.Left <= 30;
        if (s.Id == "tank-height-share") return c.Answer <= 5 && c.Right <= 30;
        if (s.Group == QuestionKnowledgeGroup.Time && c.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
            return (c.Operation == ArithmeticOperation.Divide ? c.Answer : c.Left) <= 60;
        if (s.Group == QuestionKnowledgeGroup.Measurement && s.Conversion == 1 && c.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
            return (c.Operation == ArithmeticOperation.Divide ? c.Answer : c.Left) <= 20;
        if (s.Group == QuestionKnowledgeGroup.Measurement && s.Conversion != 1 && c.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
            return (c.Operation == ArithmeticOperation.Divide ? c.Answer : c.Left) <= 20000;
        return true;
    }
    private static void SetSceneActors(AppliedQuestionScene s, AppLanguage lang, int a, int b, ref string name, ref string other)
    {
        bool vi = lang == AppLanguage.Vietnamese;
        // Age problems do not randomly imply that a child is older than a parent.
        if (s.Id.StartsWith("age-", StringComparison.Ordinal)) { name = QuizContentCatalog.Text(lang, "AppliedQuestionCatalogue.Contexts.SetSceneActors.009"); other = QuizContentCatalog.Text(lang, "AppliedQuestionCatalogue.Contexts.SetSceneActors.010"); return; }
        if (s.Group == QuestionKnowledgeGroup.Packaging && Math.Max(a, b) > 100) {
            name = QuizContentCatalog.Text(lang, "AppliedQuestionCatalogue.Contexts.SetSceneActors.011"); other = QuizContentCatalog.Text(lang, "AppliedQuestionCatalogue.Contexts.SetSceneActors.012"); return;
        }
        string? actor = s.UnitId switch {
            "students" => QuizContentCatalog.Text(lang, "AppliedQuestionCatalogue.Contexts.SetSceneActors.013"), "chickens" => QuizContentCatalog.Text(lang, "AppliedQuestionCatalogue.Contexts.SetSceneActors.014"),
            "visits" => QuizContentCatalog.Text(lang, "AppliedQuestionCatalogue.Contexts.SetSceneActors.015"), "seedlings" => QuizContentCatalog.Text(lang, "AppliedQuestionCatalogue.Contexts.SetSceneActors.016"),
            "bottles" => QuizContentCatalog.Text(lang, "AppliedQuestionCatalogue.Contexts.SetSceneActors.017"), "rolls" => QuizContentCatalog.Text(lang, "AppliedQuestionCatalogue.Contexts.SetSceneActors.018"),
            "mangoes" => QuizContentCatalog.Text(lang, "AppliedQuestionCatalogue.Contexts.SetSceneActors.019"), "points" => QuizContentCatalog.Text(lang, "AppliedQuestionCatalogue.Contexts.SetSceneActors.020"), _ => null };
        if (s.UnitId == "pages" && Math.Max(a, b) > 100) actor = QuizContentCatalog.Text(lang, "AppliedQuestionCatalogue.Contexts.SetSceneActors.021");
        if (actor is not null) {
            name = vi ? actor + " thứ nhất" : "the first " + actor;
            other = vi ? actor + " thứ hai" : "the second " + actor;
        }
    }

    public static QuestionFactTable? FactTable(BasicQuestionContract c)
    {
        if (c.KnowledgeGroup != QuestionKnowledgeGroup.Data) return null;
        bool vi = c.Language == AppLanguage.Vietnamese;
        if (Find(c.SceneId)!.StoryContextId is { } id) {
            var context = QuizStoryContextCatalog.Find(id);
            string firstLabel = c.Operation switch {
                ArithmeticOperation.Add => QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.Contexts.FactTable.022"),
                ArithmeticOperation.Subtract => QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.Contexts.FactTable.023"),
                ArithmeticOperation.Multiply => vi ? "Lượng mỗi " + context.ViPeriod : "Quantity per " + context.EnPeriod,
                _ => QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.Contexts.FactTable.024") };
            string secondLabel = c.Operation switch {
                ArithmeticOperation.Add => QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.Contexts.FactTable.025"),
                ArithmeticOperation.Subtract => QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.Contexts.FactTable.026"),
                _ => vi ? "Số " + context.ViPeriod : "Number of " + QuizStoryContextCatalog.PluralPeriod(context.EnPeriod) };
            return new(QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.Contexts.FactTable.027"), QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.Contexts.FactTable.028"), [
                new(firstLabel, c.Left.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new(secondLabel, c.Right.ToString(System.Globalization.CultureInfo.InvariantCulture)) ]);
        }
        if (c.SceneId.StartsWith("survey-", StringComparison.Ordinal))
            return new(QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.Contexts.FactTable.029"), QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.Contexts.FactTable.030"), [
                new(QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.Contexts.FactTable.031"), c.Left.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new(QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.Contexts.FactTable.032"), c.Right.ToString(System.Globalization.CultureInfo.InvariantCulture)) ]);
        string first = c.SceneId == "visitors-average-groups" ? QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.Contexts.FactTable.033")
            : (c.Operation == ArithmeticOperation.Multiply ? QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.Contexts.FactTable.037") : QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.Contexts.FactTable.038"));
        return new(QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.Contexts.FactTable.034"), QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.Contexts.FactTable.035"), [
            new(first, c.Left.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(QuizContentCatalog.Text(c.Language, "AppliedQuestionCatalogue.Contexts.FactTable.036"), c.Right.ToString(System.Globalization.CultureInfo.InvariantCulture)) ]);
    }
}
