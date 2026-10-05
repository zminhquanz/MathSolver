using MathSolver.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

public enum BasicQuestionStructure
{
    Increase, Combine, RecoverInitial, Remaining, Difference, MissingPart,
    EqualGroups, TimesAsMany, EqualShare, CountGroups, TimesFewer,
    AddComparisonMore, AddComparisonInverse,
    SubComparisonLess, SubComparisonInverse, FindPart, CompareFactor
}

public sealed record QuestionUnit(string Id, string Vietnamese, string Singular, string Plural,
    string VietnameseGroup, string GroupSingular, string GroupPlural)
{
    public string Item(AppLanguage language, bool singular = false) => language == AppLanguage.Vietnamese
        ? Vietnamese : singular ? Singular : Plural;
    public string Group(AppLanguage language, bool singular = false) => language == AppLanguage.Vietnamese
        ? VietnameseGroup : singular ? GroupSingular : GroupPlural;
    public string GroupFor(BasicQuestionContract c, bool singular = false)
    {
        if (c.Version == ArithmeticQuestionCatalogue.Version) return ArithmeticQuestionCatalogue.GroupFor(c, this, singular);
        int perGroup = c.Structure switch
        {
            BasicQuestionStructure.EqualGroups => c.Left,
            BasicQuestionStructure.EqualShare => c.Right > 0 ? c.Left / c.Right : 0,
            BasicQuestionStructure.CountGroups => c.Right,
            _ => 0
        };
        // Five-digit curriculum operands must not imply thousands of books in a small box.
        return perGroup > 100 ? c.Language == AppLanguage.Vietnamese ? "kho" : singular ? "warehouse" : "warehouses"
            : Group(c.Language, singular);
    }
}

/// <summary>Countable objects with compatible containers. The model chooses an ID, never a new dimension.</summary>
public static class QuestionUnits
{
    public static IReadOnlyList<QuestionUnit> All { get; } = Array.AsReadOnly(new QuestionUnit[]
    {
        new("books", "quyển sách", "book", "books", "thùng", "box", "boxes"),
        new("notebooks", "quyển vở", "notebook", "notebooks", "gói", "pack", "packs"),
        new("pencils", "bút chì", "pencil", "pencils", "hộp", "box", "boxes"),
        new("candies", "viên kẹo", "sweet", "sweets", "túi", "bag", "bags"),
        new("apples", "quả táo", "apple", "apples", "giỏ", "basket", "baskets"),
        new("oranges", "quả cam", "orange", "oranges", "giỏ", "basket", "baskets"),
        new("flowers", "bông hoa", "flower", "flowers", "bó", "bunch", "bunches"),
        new("cards", "tấm thiệp", "card", "cards", "hộp", "box", "boxes"),
        new("balls", "quả bóng", "ball", "balls", "túi", "bag", "bags"),
        new("stickers", "nhãn dán", "sticker", "stickers", "gói", "pack", "packs"),
        new("cakes", "chiếc bánh", "cake", "cakes", "hộp", "box", "boxes")
    });
    public static QuestionUnit? Find(string? id) => All.Concat(AdditionQuestionCatalogue.ExtraUnits).FirstOrDefault(u => u.Id == id);
    public static QuestionUnit? Find(BasicQuestionContract c) => (c.Version is AdditionQuestionCatalogue.Version or ArithmeticQuestionCatalogue.Version
        ? All.Concat(AdditionQuestionCatalogue.ExtraUnits) : All).FirstOrDefault(u =>
        u.Item(c.Language) == c.Unit && u.GroupFor(c) == c.GroupUnit);
}

/// <summary>Mathematical structures and roles belong to C#; generated text contains no concrete facts.</summary>
public static class BasicQuestionTemplates
{
    public static ArithmeticOperation Operation(BasicQuestionStructure structure) => structure switch
    {
        BasicQuestionStructure.Increase or BasicQuestionStructure.Combine or BasicQuestionStructure.RecoverInitial
            or BasicQuestionStructure.AddComparisonMore or BasicQuestionStructure.AddComparisonInverse => ArithmeticOperation.Add,
        BasicQuestionStructure.Remaining or BasicQuestionStructure.Difference or BasicQuestionStructure.MissingPart
            or BasicQuestionStructure.SubComparisonLess or BasicQuestionStructure.SubComparisonInverse or BasicQuestionStructure.FindPart => ArithmeticOperation.Subtract,
        BasicQuestionStructure.EqualGroups or BasicQuestionStructure.TimesAsMany => ArithmeticOperation.Multiply,
        _ => ArithmeticOperation.Divide
    };

    public static BasicQuestionStructure[] Allowed(ArithmeticOperation operation, CurriculumTier tier)
        => Enum.GetValues<BasicQuestionStructure>().Where(s => s is not (BasicQuestionStructure.AddComparisonMore or BasicQuestionStructure.AddComparisonInverse)
            && !OneStepRelationRules.IsExtended(s)
            && Operation(s) == operation && (int)tier >= MinimumStars(s)).ToArray();
    public static BasicQuestionStructure[] AllowedContextual(ArithmeticOperation operation, CurriculumTier tier)
        => Allowed(operation, tier).Concat(Enum.GetValues<BasicQuestionStructure>().Where(s => OneStepRelationRules.IsExtended(s)
            && Operation(s) == operation && (int)tier >= MinimumStars(s))).ToArray();
    public static int MinimumStars(BasicQuestionStructure s) => s switch
    {
        BasicQuestionStructure.Combine or BasicQuestionStructure.MissingPart or BasicQuestionStructure.CountGroups
            or BasicQuestionStructure.SubComparisonLess or BasicQuestionStructure.FindPart => 2,
        BasicQuestionStructure.RecoverInitial or BasicQuestionStructure.Difference or BasicQuestionStructure.TimesAsMany or BasicQuestionStructure.TimesFewer
            or BasicQuestionStructure.SubComparisonInverse or BasicQuestionStructure.CompareFactor => 3,
        _ => 1
    };

    // Examples anchor roles, not sentence wording. Every structure remains one exact integer operation.
    public static BasicQuestionDraft Example(BasicQuestionContract c, string? unitId = null)
    {
        if (c.Version == FindXQuestionCatalogue.Version) return FindXQuestionCatalogue.Draft(c);
        if (c.Version == AppliedQuestionCatalogue.Version) return AppliedQuestionCatalogue.Draft(c);
        if (c.Version == AdditionQuestionCatalogue.Version) return AdditionQuestionCatalogue.Example(c) with { UnitId = unitId ?? QuestionUnits.Find(c)?.Id };
        if (c.Version == ArithmeticQuestionCatalogue.Version) return ArithmeticQuestionCatalogue.Example(c) with { UnitId = unitId ?? QuestionUnits.Find(c)?.Id };
        bool vi = c.Language == AppLanguage.Vietnamese;
        var clauses = c.Structure switch
        {
            BasicQuestionStructure.SubComparisonLess => vi
                ? ("{other} có {a} {unit}.", "{name} có ít hơn {other} là {b} {unit}.", "Hỏi {name} có bao nhiêu {unit}?", "Số {unit} mà {name} có là:")
                : ("{other} has {a} {unit}.", "{name} has {b} fewer {unit} than {other}.", "How many {unit} does {name} have?", "The number of {unit} that {name} has is:"),
            BasicQuestionStructure.SubComparisonInverse => vi
                ? ("{other} có {a} {unit}.", "{other} có nhiều hơn {name} là {b} {unit}.", "Hỏi {name} có bao nhiêu {unit}?", "Số {unit} mà {name} có là:")
                : ("{other} has {a} {unit}.", "{other} has {b} more {unit} than {name}.", "How many {unit} does {name} have?", "The number of {unit} that {name} has is:"),
            BasicQuestionStructure.FindPart => vi
                ? ("{name} và {other} có tổng cộng {a} {unit}.", "{other} có {b} {unit}.", "Hỏi {name} có bao nhiêu {unit}?", "Số {unit} mà {name} có là:")
                : ("{name} and {other} have a total of {a} {unit}.", "{other} has {b} {unit}.", "How many {unit} does {name} have?", "The number of {unit} that {name} has is:"),
            BasicQuestionStructure.CompareFactor => vi
                ? ("{other} có {a} {unit}.", "{name} có {b} {unit}.", "Hỏi số {unit} của {other} gấp mấy lần số {unit} của {name}?", "Số lần lượng của {other} gấp lượng của {name} là:")
                : ("{other} has {a} {unit}.", "{name} has {b} {unit}.", "How many times as many {unit} does {other} have as {name}?", "The number of times the amount of {other} is that of {name} is:"),
            BasicQuestionStructure.Increase => vi
                ? ("{name} có {a} {unit}.", "{name} nhận thêm {b} {unit}.", "Hỏi {name} có tất cả bao nhiêu {unit}?", "Số {unit} mà {name} có tất cả là:")
                : ("{name} has {a} {unit}.", "{name} receives {b} more {unit}.", "How many {unit} does {name} have in total?", "The total number of {unit} that {name} has is:"),
            BasicQuestionStructure.Combine => vi
                ? ("{name} có {a} {unit}.", "{other} có {b} {unit}.", "Hỏi cả {name} và {other} có tổng cộng bao nhiêu {unit}?", "Tổng số {unit} của {name} và {other} là:")
                : ("{name} has {a} {unit}.", "{other} has {b} {unit}.", "How many {unit} do {name} and {other} have altogether?", "The total number of {unit} they have is:"),
            BasicQuestionStructure.RecoverInitial => vi
                ? ("{name} còn lại {a} {unit}.", "Trước đó, {name} đã cho đi {b} {unit}.", "Hỏi lúc đầu {name} có bao nhiêu {unit}?", "Số {unit} mà {name} có lúc đầu là:")
                : ("{name} has {a} {unit} left.", "Earlier, {name} gave away {b} {unit}.", "How many {unit} did {name} have originally?", "The original number of {unit} that {name} had is:"),
            BasicQuestionStructure.Remaining => vi
                ? ("{name} có {a} {unit}.", "{name} cho đi {b} {unit}.", "Hỏi {name} còn lại bao nhiêu {unit}?", "Số {unit} mà {name} còn lại là:")
                : ("{name} has {a} {unit}.", "{name} gives away {b} {unit}.", "How many {unit} does {name} have left?", "The number of {unit} remaining is:"),
            BasicQuestionStructure.Difference => vi
                ? ("{name} có {a} {unit}.", "{other} có {b} {unit}.", "Hỏi {name} có nhiều hơn {other} bao nhiêu {unit}?", "Số {unit} mà {name} có nhiều hơn {other} là:")
                : ("{name} has {a} {unit}.", "{other} has {b} {unit}.", "How many more {unit} does {name} have than {other}?", "The difference in their numbers of {unit} is:"),
            BasicQuestionStructure.MissingPart => vi
                ? ("{name} cần {a} {unit}.", "{name} đã có {b} {unit}.", "Hỏi {name} cần thêm bao nhiêu {unit} cho đủ?", "Số {unit} mà {name} cần thêm là:")
                : ("{name} needs {a} {unit}.", "{name} already has {b} {unit}.", "How many more {unit} does {name} need?", "The number of additional {unit} needed is:"),
            BasicQuestionStructure.EqualGroups => vi
                ? ("Mỗi {group_one} chứa {a} {unit}.", "{name} có {b} {group} như nhau.", "Hỏi {name} có tất cả bao nhiêu {unit}?", "Tổng số {unit} trong các {group} là:")
                : ("Each {group_one} holds {a} {unit}.", "{name} has {b} identical {group}.", "How many {unit} does {name} have altogether?", "The total number of {unit} in the {group} is:"),
            BasicQuestionStructure.TimesAsMany => vi
                ? ("{other} có {a} {unit}.", "{name} có số {unit} gấp {b} lần số {unit} của {other}.", "Hỏi {name} có bao nhiêu {unit}?", "Số {unit} mà {name} có là:")
                : ("{other} has {a} {unit}.", "{name} has {b} times as many {unit} as {other}.", "How many {unit} does {name} have?", "The number of {unit} that {name} has is:"),
            BasicQuestionStructure.EqualShare => vi
                ? ("{name} có {a} {unit}.", "{name} xếp đều vào {b} {group}.", "Hỏi mỗi {group_one} chứa bao nhiêu {unit}?", "Số {unit} trong mỗi {group_one} là:")
                : ("{name} has {a} {unit}.", "{name} packs them equally into {b} {group}.", "How many {unit} are in each {group_one}?", "The number of {unit} in each {group_one} is:"),
            BasicQuestionStructure.CountGroups => vi
                ? ("{name} có {a} {unit}.", "{name} xếp vào các {group}, mỗi {group_one} chứa {b} {unit}.", "Hỏi {name} xếp được bao nhiêu {group}?", "Số {group} mà {name} xếp được là:")
                : ("{name} has {a} {unit}.", "{name} packs them in {group}, with {b} {unit} in each {group_one}.", "How many {group} can {name} fill?", "The number of {group} filled is:"),
            _ => vi
                ? ("{other} có {a} {unit}.", "{other} có số {unit} gấp {b} lần số {unit} của {name}.", "Hỏi {name} có bao nhiêu {unit}?", "Số {unit} mà {name} có là:")
                : ("{other} has {a} {unit}.", "{other} has {b} times as many {unit} as {name}.", "How many {unit} does {name} have?", "The number of {unit} that {name} has is:")
        };
        return new(clauses.Item1.TrimEnd('.') + ",", char.ToLowerInvariant(clauses.Item2[0]) + clauses.Item2[1..], clauses.Item3, clauses.Item4,
            unitId ?? QuestionUnits.Find(c)?.Id ?? "books");
    }

    /// <summary>Join the facts as clauses while preserving the spelling of actor names.</summary>
    public static string RenderProblem(string givenA, string givenB, string question, BasicQuestionContract c)
    {
        string a = Render(givenA.Trim(), c), b = Render(givenB.Trim(), c), q = Render(question.Trim(), c);
        if (a.Length > 0 && b.Length > 0)
        {
            a = a.TrimEnd('.', ',', '!', '?', ':', ';', ' ') + ",";
            // Ordinary openings and Vietnamese role/group labels become lowercase.
            // Bare names and English titles such as Mary / Uncle John stay intact.
            bool properActor = new[] { c.Subject, c.OtherSubject }.Any(actor => actor.Length > 0
                && char.IsUpper(actor[0]) && b.StartsWith(actor, StringComparison.Ordinal)
                && (b.Length == actor.Length || !char.IsLetter(b[actor.Length])));
            if (!properActor) b = char.ToLowerInvariant(b[0]) + b[1..];
        }
        return string.Join(" ", new[] { a, b, q }.Where(s => s.Length > 0));
    }

    public static string Render(string template, BasicQuestionContract c)
    {
        if (c.Version == FindXQuestionCatalogue.Version) return FindXQuestionCatalogue.Render(template, c);
        if (c.Version == AppliedQuestionCatalogue.Version) return AppliedQuestionCatalogue.Render(template, c);
        var unit = QuestionUnits.Find(c);
        string text = template.Replace("{part_a}", c.PartA).Replace("{part_b}", c.PartB)
            .Replace("{name}", c.Subject).Replace("{other}", c.OtherSubject)
            .Replace("{a}", c.Left.ToString(CultureInfo.InvariantCulture)).Replace("{b}", c.Right.ToString(CultureInfo.InvariantCulture))
            .Replace("{unit}", c.Unit).Replace("{group}", c.GroupUnit)
            .Replace("{group_one}", unit?.GroupFor(c, true) ?? c.GroupUnit);
        if (unit is not null && c.Language == AppLanguage.English)
        {
            // Quantity-aware singulars, without replacing unrelated words in the prose.
            text = Regex.Replace(text, @"\b1 " + Regex.Escape(unit.Plural) + @"\b", "1 " + unit.Singular);
            text = Regex.Replace(text, @"\b1 (identical |equal |such )?" + Regex.Escape(unit.GroupFor(c)) + @"\b",
                m => "1 " + m.Groups[1].Value + unit.GroupFor(c, true));
            if (c.Version == AdditionQuestionCatalogue.Version)
            {
                text = Regex.Replace(text, @"\b1 (more |fewer |additional |new )" + Regex.Escape(unit.Plural) + @"\b",
                    m => "1 " + m.Groups[1].Value + unit.Singular);
                if (AdditionQuestionCatalogue.Find(c.SceneId)?.Kind == AdditionSceneKind.Arrivals)
                {
                    text = Regex.Replace(text, @"\bthere are 1 " + Regex.Escape(unit.Singular) + @"\b", "There is 1 " + unit.Singular, RegexOptions.IgnoreCase);
                    text = Regex.Replace(text, @"\b1 " + Regex.Escape(unit.Singular) + @"\s+(fly|arrive|join|come|enter|are)\b",
                        m => "1 " + unit.Singular + " " + (m.Groups[1].Value switch { "fly" => "flies", "come" => "comes", "are" => "is", var verb => verb + "s" }));
                }
            }
        }
        // Native text can contain a space before punctuation or a repeated comma.
        // Tidy displayed/live prose without changing stored JSON or its hash.
        text = Regex.Replace(text, @"\s+([,.!?:;])", "$1");
        text = Regex.Replace(text, @",(?:\s*,)+", ",");
        return Regex.Replace(text, @"(^|[.!?]\s+)(\p{Ll})", m => m.Groups[1].Value + m.Groups[2].Value.ToUpperInvariant());
    }

    public static BasicQuestionContract ApplyUnit(BasicQuestionContract c, QuestionUnit unit)
        => c with { Unit = unit.Item(c.Language), GroupUnit = unit.GroupFor(c) };
}
