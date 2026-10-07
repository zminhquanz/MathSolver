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
        if (c.Version == FractionQuestionCatalogue.Version) return FractionQuestionCatalogue.Draft(c);
        if (c.Version == FindXQuestionCatalogue.Version) return FindXQuestionCatalogue.Draft(c);
        if (c.Version == AppliedQuestionCatalogue.Version) return AppliedQuestionCatalogue.Draft(c);
        if (c.Version == AdditionQuestionCatalogue.Version) return AdditionQuestionCatalogue.Example(c) with { UnitId = unitId ?? QuestionUnits.Find(c)?.Id };
        if (c.Version == ArithmeticQuestionCatalogue.Version) return ArithmeticQuestionCatalogue.Example(c) with { UnitId = unitId ?? QuestionUnits.Find(c)?.Id };
        bool vi = c.Language == AppLanguage.Vietnamese;
        var clauses = c.Structure switch
        {
            BasicQuestionStructure.SubComparisonLess => (QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.001"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.002"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.003"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.004")),
            BasicQuestionStructure.SubComparisonInverse => (QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.005"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.006"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.007"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.008")),
            BasicQuestionStructure.FindPart => (QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.009"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.010"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.011"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.012")),
            BasicQuestionStructure.CompareFactor => (QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.013"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.014"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.015"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.016")),
            BasicQuestionStructure.Increase => (QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.017"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.018"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.019"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.020")),
            BasicQuestionStructure.Combine => (QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.021"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.022"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.023"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.024")),
            BasicQuestionStructure.RecoverInitial => (QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.025"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.026"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.027"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.028")),
            BasicQuestionStructure.Remaining => (QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.029"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.030"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.031"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.032")),
            BasicQuestionStructure.Difference => (QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.033"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.034"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.035"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.036")),
            BasicQuestionStructure.MissingPart => (QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.037"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.038"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.039"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.040")),
            BasicQuestionStructure.EqualGroups => (QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.041"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.042"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.043"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.044")),
            BasicQuestionStructure.TimesAsMany => (QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.045"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.046"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.047"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.048")),
            BasicQuestionStructure.EqualShare => (QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.049"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.050"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.051"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.052")),
            BasicQuestionStructure.CountGroups => (QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.053"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.054"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.055"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.056")),
            _ => (QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.057"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.058"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.059"), QuizContentCatalog.Text(c.Language, "BasicQuestionTemplates.Example.060"))
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
        if (c.Version == FractionQuestionCatalogue.Version) return FractionQuestionCatalogue.Render(template, c);
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
