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
        string UnitA, string UnitB, string ResultUnit, string EnUnitA, string EnUnitB, string EnResultUnit,
        string ViA, string ViB, string ViQ, string ViLead, string EnA, string EnB, string EnQ, string EnLead,
        bool WholeShare = false, bool CountResult = false, int MinimumStars = 1, string? Formula = null,
        WordProblemQuantity Quantity = WordProblemQuantity.Unspecified);

    public static IReadOnlyList<Scene> All { get; } = Build().AsReadOnly();
    public static Scene? Find(string id) => All.FirstOrDefault(s => s.Id == id);
    public static IEnumerable<Scene> Available(QuestionLearningProfile profile, ArithmeticOperation operation, CurriculumTier tier)
        => All.Where(s => s.Group == profile.Group && s.Operation == operation && (int)tier >= s.MinimumStars);
    public static int MaximumDenominator(CurriculumTier tier) => tier switch
    { CurriculumTier.OneStar => 6, CurriculumTier.TwoStars => 12, CurriculumTier.ThreeStars => 12,
        CurriculumTier.FourStars => 20, CurriculumTier.FiveStars => 30, _ => 0 };

    private static List<Scene> Build()
    {
        var scenes = new List<Scene>();
        // Each setting has its own noun/unit. Fractional budgets and work shares refer to one common whole.
        (QuestionKnowledgeGroup Group, string Id, string Vi, string En, string Unit, string EnUnit, bool Share)[] contexts = [
            (QuestionKnowledgeGroup.Objects, "bread", "bánh mì", "bread", "ổ", "loaves", false),
            (QuestionKnowledgeGroup.Objects, "cake", "bánh", "cake", "chiếc", "cakes", false),
            (QuestionKnowledgeGroup.Money, "supplies-budget", "mua đồ dùng học tập", "school supplies", "phần ngân sách chung", "of the total budget", true),
            (QuestionKnowledgeGroup.Money, "trip-budget", "chuyến đi", "the trip", "phần ngân sách chung", "of the total budget", true),
            (QuestionKnowledgeGroup.Time, "reading", "thời gian đọc sách", "reading time", "giờ", "hours", false),
            (QuestionKnowledgeGroup.Time, "exercise", "thời gian tập thể dục", "exercise time", "giờ", "hours", false),
            (QuestionKnowledgeGroup.Measurement, "water", "nước", "water", "lít", "litres", false),
            (QuestionKnowledgeGroup.Measurement, "flour", "bột", "flour", "kg", "kg", false),
            (QuestionKnowledgeGroup.Measurement, "ribbon", "ruy băng", "ribbon", "m", "m", false),
            (QuestionKnowledgeGroup.Geometry, "garden-edge", "mép vườn", "garden edge", "m", "m", false),
            (QuestionKnowledgeGroup.Geometry, "frame-edge", "cạnh khung tranh", "picture-frame edge", "m", "m", false),
            (QuestionKnowledgeGroup.Packaging, "bottle", "nước đóng chai", "bottled water", "lít", "litres", false),
            (QuestionKnowledgeGroup.Packaging, "rice-bag", "gạo đóng túi", "bagged rice", "kg", "kg", false),
            (QuestionKnowledgeGroup.Production, "garden-work", "công việc chăm sóc vườn", "gardening task", "phần công việc", "of the task", true),
            (QuestionKnowledgeGroup.Production, "decoration-work", "công việc trang trí lớp", "classroom-decoration task", "phần công việc", "of the task", true),
            (QuestionKnowledgeGroup.Data, "plant-data", "chiều cao cây đo được", "recorded plant height", "m", "m", false),
            (QuestionKnowledgeGroup.Data, "mass-data", "khối lượng mẫu đo được", "recorded sample mass", "kg", "kg", false),
            (QuestionKnowledgeGroup.Motion, "walking", "quãng đường đi bộ", "walking distance", "km", "km", false),
            (QuestionKnowledgeGroup.Motion, "cycling", "quãng đường đạp xe", "cycling distance", "km", "km", false)
        ];
        foreach (var x in contexts)
        foreach (var op in Enum.GetValues<ArithmeticOperation>())
        {
            string va, vb, vq, vl, ea, eb, eq, el;
            string amountVi = x.Group is QuestionKnowledgeGroup.Time or QuestionKnowledgeGroup.Geometry or QuestionKnowledgeGroup.Data or QuestionKnowledgeGroup.Motion
                ? x.Vi : x.Group == QuestionKnowledgeGroup.Production ? "phần " + x.Vi : "lượng " + x.Vi;
            string unitB = x.Unit, enUnitB = x.EnUnit, result = x.Unit, enResult = x.EnUnit;
            bool count = x.Group == QuestionKnowledgeGroup.Packaging && op == ArithmeticOperation.Divide;
            if (op is ArithmeticOperation.Add or ArithmeticOperation.Subtract)
            {
                bool add = op == ArithmeticOperation.Add;
                if (x.Share)
                {
                    bool work = x.Group == QuestionKnowledgeGroup.Production;
                    va = work ? $"Đối với {x.Vi}, {{name}} đã hoàn thành {{a}} {{unit_a}},"
                        : $"{{name}} đã chi {{a}} {{unit_a}} cho {x.Vi},";
                    vb = work ? (add ? "sau đó {name} hoàn thành thêm {b} {unit_b}." : "trong đó {b} {unit_b} được hoàn thành ở giai đoạn đầu.")
                        : (add ? "sau đó {name} chi thêm {b} {unit_b}." : "trong đó {b} {unit_b} được chi ở giai đoạn đầu.");
                    vq = work ? (add ? "Hỏi tổng cộng đã hoàn thành bao nhiêu {unit}?" : "Hỏi giai đoạn sau đã hoàn thành bao nhiêu {unit}?")
                        : (add ? "Hỏi tổng cộng đã chi bao nhiêu {unit}?" : "Hỏi giai đoạn sau đã chi bao nhiêu {unit}?");
                    ea = work ? $"For the {x.En}, {{name}} has completed {{a}} {{unit_a}}," : $"{{name}} has spent {{a}} {{unit_a}} on {x.En},";
                    eb = work ? (add ? "then {name} completes another {b} {unit_b}." : "of which {b} {unit_b} was completed in the first stage.")
                        : (add ? "then {name} spends another {b} {unit_b}." : "of which {b} {unit_b} was spent in the first stage.");
                    eq = work ? (add ? $"What fraction of the {x.En} has been completed altogether?" : $"What fraction of the {x.En} was completed in the later stage?")
                        : (add ? $"What fraction of the {x.En} has been spent altogether?" : $"What fraction of the {x.En} was spent in the later stage?");
                }
                else
                {
                    va = $"Số đo thứ nhất về {x.Vi} là {{a}} {{unit_a}},";
                    vb = "số đo thứ hai là {b} {unit_b}.";
                    vq = add ? $"Hỏi tổng hai số đo về {x.Vi} là bao nhiêu {{unit}}?" : $"Hỏi số đo thứ nhất về {x.Vi} lớn hơn số đo thứ hai bao nhiêu {{unit}}?";
                    ea = $"The first measurement of {x.En} is {{a}} {{unit_a}},";
                    eb = "the second measurement is {b} {unit_b}.";
                    eq = add ? $"What is the sum of these {x.En} measurements in {{unit}}?" : $"How much larger is the first {x.En} measurement in {{unit}}?";
                    if (x.Group == QuestionKnowledgeGroup.Objects)
                    {
                        va = $"{{name}} có {{a}} {{unit_a}} {x.Vi},";
                        vb = $"một bạn khác có {{b}} {{unit_b}} {x.Vi}.";
                        vq = add ? $"Hỏi hai bạn có tất cả bao nhiêu {{unit}} {x.Vi}?" : $"Hỏi {{name}} có nhiều hơn bạn kia bao nhiêu {{unit}} {x.Vi}?";
                        ea = "{name} has {a} {unit_a},"; eb = "another friend has {b} {unit_b}.";
                        eq = add ? "How many {unit} do the two friends have altogether?" : "How many more {unit} does {name} have than the other friend?";
                    }
                    else if (x.Group == QuestionKnowledgeGroup.Time)
                    {
                        va = $"Buổi sáng, {{name}} dành {{a}} {{unit_a}} cho {x.Vi},";
                        vb = "buổi chiều dành {b} {unit_b} cho cùng hoạt động.";
                        vq = add ? "Hỏi cả hai buổi dành bao nhiêu {unit} cho hoạt động này?" : "Hỏi buổi sáng dành nhiều hơn buổi chiều bao nhiêu {unit}?";
                        ea = $"In the morning, {{name}} spends {{a}} {{unit_a}} on {x.En},";
                        eb = "in the afternoon, {name} spends {b} {unit_b} on the same activity.";
                        eq = add ? "How many {unit} are spent on this activity altogether?" : "How much longer is the morning session in {unit}?";
                    }
                    else if (x.Group == QuestionKnowledgeGroup.Motion)
                    {
                        va = $"Ở chặng đầu của {x.Vi}, {{name}} đi {{a}} {{unit_a}},";
                        vb = "ở chặng tiếp theo đi {b} {unit_b}.";
                        vq = add ? "Hỏi cả hai chặng dài bao nhiêu {unit}?" : "Hỏi chặng đầu dài hơn chặng tiếp theo bao nhiêu {unit}?";
                        ea = $"On the first stage of the {x.En}, {{name}} travels {{a}} {{unit_a}},";
                        eb = "on the next stage, {name} travels {b} {unit_b}.";
                        eq = add ? "What is the total distance of both stages in {unit}?" : "How much longer is the first stage in {unit}?";
                    }
                }
                vl = add ? $"Tổng lượng {x.Vi} là:" : $"Phần chênh lệch về {x.Vi} là:";
                el = add ? $"The total {x.En} is:" : $"The difference in {x.En} is:";
                if (x.Group == QuestionKnowledgeGroup.Money)
                {
                    vl = add ? "Tổng ngân sách đã chi là:" : "Ngân sách đã chi trong giai đoạn sau là:";
                    el = add ? "The total budget spent is:" : "The budget spent in the later stage is:";
                    eq = add ? $"What fraction of the total budget has been spent on {x.En} altogether?"
                        : $"What fraction of the total budget was spent on {x.En} in the later stage?";
                }
            }
            else if (op == ArithmeticOperation.Multiply)
            {
                va = $"{amountVi} ban đầu là {{a}} {{unit_a}},";
                vb = "phần được chọn bằng {b} lượng ban đầu.";
                vq = $"Hỏi phần {x.Vi} được chọn là bao nhiêu {{unit}}?";
                vl = $"{amountVi} được chọn là:";
                ea = $"The initial amount of {x.En} is {{a}} {{unit_a}},";
                eb = "the selected part is {b} of that initial amount.";
                eq = $"How much {x.En} is selected, in {{unit}}?";
                el = $"The selected amount of {x.En} is:";
                unitB = enUnitB = "";
            }
            else if (count)
            {
                bool water = x.Id == "bottle";
                result = water ? "chai" : "túi"; enResult = water ? "bottles" : "bags";
                va = $"{{name}} có {{a}} {{unit_a}} {x.Vi},";
                vb = water ? "mỗi chai được đổ đầy với {b} {unit_b}." : "mỗi túi được đóng đủ {b} {unit_b}.";
                vq = water ? "Hỏi đóng đầy được bao nhiêu {unit}?" : "Hỏi đóng đủ được bao nhiêu {unit}?";
                vl = "Số {unit} đóng được là:";
                ea = $"{{name}} has {{a}} {{unit_a}} of {x.En},";
                eb = water ? "each bottle is filled with {b} {unit_b}." : "each bag is packed with {b} {unit_b}.";
                eq = water ? "How many full {unit} can be filled?" : "How many full {unit} can be packed?";
                el = "The number of full {unit} is:";
            }
            else
            {
                va = $"Phần đã biết của {x.Vi} là {{a}} {{unit_a}},";
                vb = "phần này bằng {b} lượng ban đầu.";
                vq = $"Hỏi {amountVi} ban đầu là bao nhiêu {{unit}}?";
                vl = $"{amountVi} ban đầu là:";
                ea = $"The known part of {x.En} is {{a}} {{unit_a}},";
                eb = "this part is {b} of the initial amount.";
                eq = $"What was the initial amount of {x.En}, in {{unit}}?";
                el = $"The initial amount of {x.En} was:";
                unitB = enUnitB = "";
            }
            if (x.Share && op is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
            {
                bool multiply = op == ArithmeticOperation.Multiply;
                bool work = x.Group == QuestionKnowledgeGroup.Production;
                va = work
                    ? multiply ? $"Trong {x.Vi}, {{name}} được giao {{a}} {{unit_a}}," : $"Trong {x.Vi}, {{name}} đã hoàn thành {{a}} {{unit_a}},"
                    : multiply ? $"Ngân sách phân bổ cho {x.Vi} tương ứng {{a}} {{unit_a}}," : $"Khoản đã chi cho {x.Vi} tương ứng {{a}} {{unit_a}},";
                vb = work
                    ? multiply ? "{name} hoàn thành {b} phần được giao." : "phần hoàn thành chiếm {b} phần được giao."
                    : multiply ? "người ta chi {b} ngân sách được phân bổ." : "khoản đã chi chiếm {b} ngân sách được phân bổ.";
                vq = work
                    ? multiply ? "Hỏi đã hoàn thành bao nhiêu {unit}?" : "Hỏi phần được giao tương ứng bao nhiêu {unit}?"
                    : multiply ? "Hỏi đã chi bao nhiêu {unit}?" : "Hỏi ngân sách phân bổ tương ứng bao nhiêu {unit}?";
                vl = work ? multiply ? "Phần đã hoàn thành là:" : "Phần được giao là:"
                    : multiply ? "Ngân sách đã chi là:" : "Ngân sách được phân bổ là:";
                ea = work
                    ? multiply ? $"For the {x.En}, {{name}} is assigned {{a}} {{unit_a}}," : $"For the {x.En}, {{name}} has completed {{a}} {{unit_a}},"
                    : multiply ? $"The allocation for {x.En} is {{a}} {{unit_a}}," : $"The spending on {x.En} is {{a}} {{unit_a}},";
                eb = work ? multiply ? "{name} completes {b} of the assigned part." : "the completed part represents {b} of the assigned part."
                    : multiply ? "{b} of that allocation is spent." : "this spending represents {b} of the allocation.";
                eq = work ? multiply ? "What fraction of the total task is completed?" : "What fraction of the total task was assigned?"
                    : multiply ? "What fraction of the total budget is spent?" : "What fraction of the total budget was allocated?";
                el = work ? multiply ? "The completed part is:" : "The assigned part is:"
                    : multiply ? "The budget spent is:" : "The budget allocation is:";
            }
            scenes.Add(new($"fraction-{x.Id}-{op}", x.Group, op, x.Unit, unitB, result, x.EnUnit, enUnitB, enResult,
                va, vb, vq, vl, ea, eb, eq, el, x.Share, count));
        }
        foreach (string noun in new[] { "garden", "glass" })
        {
            string viNoun = noun == "garden" ? "mảnh vườn" : "tấm kính";
            scenes.Add(new($"fraction-{noun}-area", QuestionKnowledgeGroup.Geometry, ArithmeticOperation.Multiply,
                "m", "m", "m²", "m", "m", "m²",
                $"{viNoun} hình chữ nhật có chiều dài {{a}} {{unit_a}},", "chiều rộng là {b} {unit_b}.",
                $"Hỏi diện tích {viNoun} là bao nhiêu {{unit}}?", $"Diện tích {viNoun} là:",
                $"A rectangular {noun} is {{a}} {{unit_a}} long,", "its width is {b} {unit_b}.",
                $"What is the area of this {noun} in {{unit}}?", $"The area of the {noun} is:", Quantity: WordProblemQuantity.Area));
            scenes.Add(new($"fraction-{noun}-width", QuestionKnowledgeGroup.Geometry, ArithmeticOperation.Divide,
                "m²", "m", "m", "m²", "m", "m",
                $"Diện tích {viNoun} hình chữ nhật là {{a}} {{unit_a}},", "chiều dài là {b} {unit_b}.",
                $"Hỏi chiều rộng {viNoun} là bao nhiêu {{unit}}?", $"Chiều rộng {viNoun} là:",
                $"The area of a rectangular {noun} is {{a}} {{unit_a}},", "its length is {b} {unit_b}.",
                $"What is its width in {{unit}}?", "The width is:", Quantity: WordProblemQuantity.Distance));
            scenes.Add(new($"fraction-{noun}-perimeter", QuestionKnowledgeGroup.Geometry, ArithmeticOperation.Add,
                "m", "m", "m", "m", "m", "m",
                $"{viNoun} hình chữ nhật có chiều dài {{a}} {{unit_a}},", "chiều rộng là {b} {unit_b}.",
                $"Hỏi chu vi {viNoun} là bao nhiêu {{unit}}?", $"Chu vi {viNoun} là:",
                $"A rectangular {noun} is {{a}} {{unit_a}} long,", "its width is {b} {unit_b}.",
                $"What is its perimeter in {{unit}}?", "The perimeter is:", MinimumStars: 4,
                Formula: "({a} + {b}) × 2", Quantity: WordProblemQuantity.Distance));
        }
        scenes.Add(new("fraction-data-mean", QuestionKnowledgeGroup.Data, ArithmeticOperation.Add,
            "m", "m", "m", "m", "m", "m",
            "Lần đo chiều cao cây đầu tiên ghi nhận {a} {unit_a},", "lần đo thứ hai ghi nhận {b} {unit_b}.",
            "Hỏi chiều cao trung bình của hai lần đo là bao nhiêu {unit}?", "Chiều cao trung bình là:",
            "The first recorded plant height is {a} {unit_a},", "the second recorded height is {b} {unit_b}.",
            "What is the average of these two heights in {unit}?", "The average height is:", MinimumStars: 4, Formula: "({a} + {b}) ÷ 2"));
        scenes.Add(new("fraction-motion-distance", QuestionKnowledgeGroup.Motion, ArithmeticOperation.Multiply,
            "km/giờ", "giờ", "km", "km/h", "hours", "km",
            "{name} đi với vận tốc không đổi {a} {unit_a},", "thời gian di chuyển là {b} {unit_b}.",
            "Hỏi {name} đi được bao nhiêu {unit}?", "Quãng đường đi được là:",
            "{name} travels at a constant speed of {a} {unit_a},", "the journey takes {b} {unit_b}.",
            "How far does {name} travel in {unit}?", "The distance travelled is:", MinimumStars: 4, Quantity: WordProblemQuantity.Distance));
        return scenes;
    }

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

    internal static ReviewedQuestionProse Prose(BasicQuestionContract c)
    {
        var s = Find(c.SceneId)!; bool vi = c.Language == AppLanguage.Vietnamese;
        string a = vi ? s.ViA : s.EnA, b = vi ? s.ViB : s.EnB, q = vi ? s.ViQ : s.EnQ;
        string lead = vi ? s.ViLead : s.EnLead;
        if (!lead.Contains("{unit}", StringComparison.Ordinal)) lead = lead.TrimEnd(':') + " ({unit}):";
        // Rephrase factual clauses, rather than padding them with meta-text about the exercise.
        return new(ClauseVariants(a, vi), ClauseVariants(b, vi), [q],
            [lead], s.Id);
    }
    private static string[] ClauseVariants(string text, bool vi)
    {
        (string From, string To)[] phrases = vi
            ? [(" có ", " đang có "), (" có ", " có sẵn "), (" là ", " bằng "), (" là ", " được xác định là "),
                ("đã chi", "đã dùng"), ("đã chi", "đã sử dụng"), ("chi thêm", "dùng thêm"), ("được chi", "được sử dụng"),
                ("hoàn thành", "làm xong"), ("dành", "dùng"), ("ghi nhận", "ghi lại"),
                ("tương ứng", "tương đương"), ("được giao", "được phân công"), ("chiếm", "bằng"), ("người ta chi", "người ta sử dụng"),
                ("phần được chọn bằng {b} lượng ban đầu", "người ta chọn {b} lượng ban đầu"),
                ("phần này bằng", "phần này chiếm"), ("mỗi chai được đổ đầy với", "để đổ đầy một chai cần"),
                ("mỗi túi được đóng đủ", "để đóng đủ một túi cần"), ("ở chặng tiếp theo đi", "chặng tiếp theo dài")]
            : [(" has {a}", " currently has {a}"), (" has {b}", " currently has {b}"),
                ("has spent", "has already spent"), ("has completed", "has finished"), ("completes another", "finishes another"),
                ("spends another", "uses another"), ("was spent", "was used"), ("spends", "uses"),
                ("is assigned", "is given"), ("represents", "equals"), ("completes {b}", "finishes {b}"),
                ("{b} of that allocation is spent", "the spent fraction of that allocation is {b}"),
                ("is {a} {unit_a} long", "has a length of {a} {unit_a}"),
                ("is {a} {unit_a},", "equals {a} {unit_a},"), ("is {b} {unit_b}", "equals {b} {unit_b}"),
                ("the selected part is {b} of that initial amount", "a part equal to {b} of that initial amount is selected"),
                ("this part is {b} of the initial amount", "this part represents {b} of the initial amount"),
                ("each bottle is filled with", "filling one bottle requires"), ("each bag is packed with", "packing one bag requires"),
                ("travels {a}", "covers {a}"), ("travels {b}", "covers {b}"), ("the journey takes", "the travel time is")];
        return new[] { text }.Concat(phrases.Select(p => text.Replace(p.From, p.To)))
            .Distinct(StringComparer.Ordinal).Take(8).ToArray();
    }
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
            bool Same(string a, string b) => string.Equals(Regex.Replace(a.Trim(), @"\s+", " "), Regex.Replace(b.Trim(), @"\s+", " "), StringComparison.OrdinalIgnoreCase);
            if (d.UnitId != pool.UnitId) return new(null, "ChangedUnits");
            if (!pool.GivenA.Any(t => Same(t, d.GivenA)) || !pool.GivenB.Any(t => Same(t, d.GivenB))
                || !pool.Questions.Any(t => Same(t, d.Question)) || !pool.Leads.Any(t => Same(t, d.SolutionLead!))) return new(null, "ChangedRelationOrTarget");
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
        => (c.Language == AppLanguage.Vietnamese
            ? "Viết mẫu bài toán phân số theo bối cảnh này. C# tạo tử, mẫu và tính lời giải; chỉ dùng biến, không sinh số hoặc đáp án. Giữ nguyên toàn thể tham chiếu, vai trò đại lượng, đơn vị và câu hỏi."
            : "Write a fraction word-problem template for this scene. C# supplies rational facts and calculates the answer. Use placeholders, never numbers or answers. Preserve the reference whole, quantity roles, units and target.")
            + "\nScene: " + c.SceneId + "; group=" + c.KnowledgeGroup + "; stars=" + (int)c.Tier
            + "\nUse different approved phrasing from excluded stories. Example:\n" + QuestionBankStore.SerializeDraft(example ?? Draft(c))
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
