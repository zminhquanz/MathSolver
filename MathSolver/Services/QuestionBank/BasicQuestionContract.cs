using MathSolver.Models;
using MathSolver.Services.Core;
using System.Globalization;
using System.Numerics;
using System.Text.Json.Serialization;

namespace MathSolver.Services.QuestionBank;

/// <summary>Versioned, serializable facts. The model never supplies an answer.</summary>
public sealed record BasicQuestionContract(
    int Version, ArithmeticOperation Operation, CurriculumTier Tier, AppLanguage Language,
    int Left, int Right, string Subject, string Unit, string GroupUnit,
    BasicQuestionStructure Structure = BasicQuestionStructure.Increase, string OtherSubject = "",
    string TopicId = "", string SceneId = "", string PartA = "", string PartB = "",
    int Grade = 0, QuestionKnowledgeGroup KnowledgeGroup = QuestionKnowledgeGroup.Objects,
    FindXUnknownRole UnknownRole = FindXUnknownRole.None, int LeftDenominator = 1, int RightDenominator = 1)
{
    public ReasoningStorySeed? Story { get; init; }
    public const int CurrentVersion = 2;
    [JsonIgnore] public int BankVariant => Story?.Variant ?? (int)UnknownRole;
    [JsonIgnore] public BankQuestionFamily Family => Version == ReasoningStoryCatalogue.Version && Story is not null ? Story.Family : Version == FractionQuestionCatalogue.Version ? BankQuestionFamily.Fraction : Version == FindXQuestionCatalogue.Version ? BankQuestionFamily.FindX : BankQuestionFamily.Arithmetic;
    [JsonIgnore] public string AnswerText => Version == ReasoningStoryCatalogue.Version ? ReasoningStoryCatalogue.Lesson(this).Answer : Version == FractionQuestionCatalogue.Version ? FractionQuestionCatalogue.Answer(this).ToString() : Answer.ToString(CultureInfo.InvariantCulture);
    [JsonIgnore] public BigInteger Answer => Version == ReasoningStoryCatalogue.Version ? ReasoningStoryCatalogue.Lesson(this).QuestionModel.ElementaryProblem?.Answers[0].Value.Numerator ?? ReasoningStoryCatalogue.Lesson(this).QuestionModel.CorrectAnswer : Version == FractionQuestionCatalogue.Version
        ? FractionQuestionCatalogue.Answer(this) is { } fraction && fraction.Denominator.IsOne ? fraction.Numerator : throw new InvalidOperationException("UseExactFractionAnswer")
        : Version == AppliedQuestionCatalogue.Version
        && AppliedQuestionCatalogue.Reasoning(this) is { } reasoning ? reasoning.Answer
        : new BasicArithmeticEngine().CalculateInteger(Expression).Result;
    [JsonIgnore] public IntegerArithmeticExpression Expression => Version == FindXQuestionCatalogue.Version
        ? FindXQuestionCatalogue.Equation(this).SolutionExpression : new(Left, Operation, Right);
    [JsonIgnore] public bool IsTemplate => Version == ReasoningStoryCatalogue.Version || Version == FractionQuestionCatalogue.Version || Version == FindXQuestionCatalogue.Version || Version is CurrentVersion or AdditionQuestionCatalogue.Version or ArithmeticQuestionCatalogue.Version or AppliedQuestionCatalogue.Version;
    [JsonIgnore] public string AnswerUnit => Version == ReasoningStoryCatalogue.Version ? Unit : Version == FractionQuestionCatalogue.Version ? Unit : Version == FindXQuestionCatalogue.Version
        ? AppliedQuestionCatalogue.ResultUnit(FindXQuestionCatalogue.AsApplied(this))
        : Version == AppliedQuestionCatalogue.Version
        ? AppliedQuestionCatalogue.ResultUnit(this)
        : !IsTemplate ? Unit : Structure == BasicQuestionStructure.CompareFactor
        ? Language == AppLanguage.Vietnamese ? "lần" : "times" : Structure == BasicQuestionStructure.CountGroups
        ? QuestionUnits.Find(this)?.GroupFor(this, Answer.IsOne) ?? GroupUnit
        : QuestionUnits.Find(this)?.Item(Language, Answer.IsOne) ?? Unit;
    [JsonIgnore] public bool IsValid => Version == ReasoningStoryCatalogue.Version ? ReasoningStoryCatalogue.IsValid(this) : Story is null && (Version == FractionQuestionCatalogue.Version ? FractionQuestionCatalogue.IsValid(this)
        : LeftDenominator == 1 && RightDenominator == 1 && (Version == FindXQuestionCatalogue.Version ? FindXQuestionCatalogue.IsValid(this)
        : UnknownRole == FindXUnknownRole.None && (Version == AppliedQuestionCatalogue.Version
        ? Enum.IsDefined(Operation) && Enum.IsDefined(Tier) && Language is AppLanguage.Vietnamese or AppLanguage.English
            && Left > 0 && Right > 0 && (Operation != ArithmeticOperation.Divide || Left % Right == 0
                || AppliedQuestionCatalogue.Reasoning(this) is { } reasoning && reasoning.Matches(Expression))
            && (Operation != ArithmeticOperation.Subtract || Left > Right)
            && ValidActor(Subject) && ValidActor(OtherSubject) && Subject != OtherSubject && AppliedQuestionCatalogue.IsValid(this)
        : Grade == 0 && KnowledgeGroup == QuestionKnowledgeGroup.Objects
        && Version is 1 or CurrentVersion or AdditionQuestionCatalogue.Version or ArithmeticQuestionCatalogue.Version && Enum.IsDefined(Operation)
        && Enum.IsDefined(Tier) && Language is AppLanguage.Vietnamese or AppLanguage.English
        && Left > 0 && Right > 0 && !string.IsNullOrWhiteSpace(Subject)
        && Subject.Length <= 100 && !Subject.Any(c => char.IsControl(c) || char.IsDigit(c) || c is '{' or '}')
        && (Version is AdditionQuestionCatalogue.Version or ArithmeticQuestionCatalogue.Version || string.IsNullOrEmpty(TopicId) && string.IsNullOrEmpty(SceneId)
            && string.IsNullOrEmpty(PartA) && string.IsNullOrEmpty(PartB))
        && (IsTemplate ? Enum.IsDefined(Structure) && BasicQuestionTemplates.Operation(Structure) == Operation
            && (Version == AdditionQuestionCatalogue.Version ? AdditionQuestionCatalogue.IsValid(this)
                : Version == ArithmeticQuestionCatalogue.Version ? ArithmeticQuestionCatalogue.IsValid(this)
                : BasicQuestionTemplates.Allowed(Operation, Tier).Contains(Structure))
            && QuestionUnits.Find(this) is not null && !string.IsNullOrWhiteSpace(OtherSubject)
            && OtherSubject.Length <= 100 && !OtherSubject.Any(c => char.IsControl(c) || char.IsDigit(c) || c is '{' or '}')
            && Subject != OtherSubject && Left <= 99999 && Right <= 99999
            : Language == AppLanguage.Vietnamese ? Unit == "quyển sách" && GroupUnit == "thùng" : Unit == "books" && GroupUnit == "box")
        && (Operation != ArithmeticOperation.Subtract || Left >= Right)
        && (Operation != ArithmeticOperation.Divide || Left % Right == 0))));

    private static bool ValidActor(string actor) => !string.IsNullOrWhiteSpace(actor) && actor.Length <= 100
        && !actor.Any(c => char.IsControl(c) || char.IsDigit(c) || c is '{' or '}');

    [JsonIgnore] public string SolutionLead => Version == FractionQuestionCatalogue.Version
        ? FractionQuestionCatalogue.Render(FractionQuestionCatalogue.Draft(this).SolutionLead!, this) : Version == FindXQuestionCatalogue.Version
        ? FindXQuestionCatalogue.Render(FindXQuestionCatalogue.Draft(this).SolutionLead!, this)
        : Version == AppliedQuestionCatalogue.Version
        ? AppliedQuestionCatalogue.Render(AppliedQuestionCatalogue.Draft(this).SolutionLead!, this)
        : Language == AppLanguage.Vietnamese
        ? $"Số {Unit} {(Operation == ArithmeticOperation.Divide ? "trong mỗi " + GroupUnit : "cần tìm")} là:"
        : $"The number of {Unit} {(Operation == ArithmeticOperation.Divide ? "in each " + GroupUnit : "requested")} is:";

    [JsonIgnore] public string Solution => Version == ReasoningStoryCatalogue.Version ? ReasoningStoryCatalogue.Solution(this) : Version == FractionQuestionCatalogue.Version
        ? $"{SolutionLead}\n{FractionQuestionCatalogue.Expression(this)} = {AnswerText} {AnswerUnit}" : Version == FindXQuestionCatalogue.Version
        ? $"{SolutionLead}\n{Expression.LeftOperand} {BasicArithmeticEngine.GetSymbol(Expression.Operation)} {Expression.RightOperand} = {Answer} {AnswerUnit}"
        : (Version == AppliedQuestionCatalogue.Version && AppliedQuestionCatalogue.ConversionStep(this) is { } step ? step + "\n" : "")
        + $"{SolutionLead}\n{(Version == AppliedQuestionCatalogue.Version ? AppliedQuestionCatalogue.Reasoning(this)?.Equation : null) ?? $"{Left} {BasicArithmeticEngine.GetSymbol(Operation)} {Right}"} = {Answer} {AnswerUnit}";

    public static BasicQuestionContract Create(ArithmeticOperation operation, CurriculumTier tier,
        AppLanguage language, Random? random = null)
    {
        if (!Enum.IsDefined(operation) || !Enum.IsDefined(tier)
            || language is not (AppLanguage.Vietnamese or AppLanguage.English))
            throw new ArgumentOutOfRangeException(nameof(operation));
        random ??= Random.Shared;
        var generator = new ArithmeticQuizGenerator(new BasicArithmeticEngine(), random);
        ArithmeticQuizQuestion question;
        do { question = generator.Generate(ArithmeticQuizMode.Essay, operation, new(tier, false)); }
        while (question.Expression.LeftOperand <= 0 || question.Expression.RightOperand <= 0);
        bool vi = language == AppLanguage.Vietnamese;
        string[] names = vi ? ["An", "Bình", "Lan", "Mai", "Hoa", "Nam"] : ["Alex", "Sam", "Emma", "Mia", "Leo", "Ben"];
        string subject = Math.Max((int)question.Expression.LeftOperand, (int)question.Expression.RightOperand) > 100
            ? (vi ? "kho sách" : "the book warehouse") : names[random.Next(names.Length)];
        return new(1, operation, tier, language, (int)question.Expression.LeftOperand,
            (int)question.Expression.RightOperand, subject, vi ? "quyển sách" : "books", vi ? "thùng" : "box");
    }

    /// <summary>Fresh preview/practice facts. AI never sees or generates these numeric values.</summary>
    public static BasicQuestionContract CreateTemplate(ArithmeticOperation operation, CurriculumTier tier,
        AppLanguage language, Random? random = null)
    {
        if (!Enum.IsDefined(operation) || !Enum.IsDefined(tier)
            || language is not (AppLanguage.Vietnamese or AppLanguage.English))
            throw new ArgumentOutOfRangeException(nameof(operation));
        random ??= Random.Shared;
        // Reuse the existing curriculum's operand buckets and exact arithmetic rules.
        var numeric = Create(operation, tier, language, random);
        int a = numeric.Left, b = numeric.Right;
        bool large = Math.Max(a, b) > 100 || operation == ArithmeticOperation.Multiply && a * b > 100;
        string name = QuestionNames.Create(language, random, large), other = name;
        for (int attempt = 0; attempt < 32 && QuestionNames.GivenName(other) == QuestionNames.GivenName(name); attempt++)
            other = QuestionNames.Create(language, random, large);
        if (QuestionNames.GivenName(other) == QuestionNames.GivenName(name)) other = language == AppLanguage.Vietnamese
            ? (QuestionNames.GivenName(name) == "An" ? "Bình" : "An") : (QuestionNames.GivenName(name) == "Emma" ? "James" : "Emma");
        var structures = BasicQuestionTemplates.Allowed(operation, tier);
        var unit = QuestionUnits.All[random.Next(QuestionUnits.All.Count)];
        var preview = new BasicQuestionContract(CurrentVersion, operation, tier, language, a, b, name, unit.Item(language), unit.Group(language),
            structures[random.Next(structures.Length)], other);
        return BasicQuestionTemplates.ApplyUnit(preview, unit);
    }

    public BasicQuestionContract FreshFacts(Random? random = null)
    {
        if (!IsTemplate) return this;
        if (Version == ReasoningStoryCatalogue.Version) return ReasoningStoryCatalogue.Fresh(this, random);
        if (Version == FractionQuestionCatalogue.Version) return FractionQuestionCatalogue.Create(new(KnowledgeGroup), Operation, Tier, Language, random, SceneId);
        if (Version == FindXQuestionCatalogue.Version) return FindXQuestionCatalogue.Create(new(KnowledgeGroup), Operation, Tier, Language, random, UnknownRole, SceneId);
        if (Version == AppliedQuestionCatalogue.Version) return AppliedQuestionCatalogue.Create(new(KnowledgeGroup), Operation, Tier, Language, random, SceneId);
        if (Version == AdditionQuestionCatalogue.Version) return AdditionQuestionCatalogue.Refresh(this, random);
        if (Version == ArithmeticQuestionCatalogue.Version) return ArithmeticQuestionCatalogue.Refresh(this, random);
        var fresh = CreateTemplate(Operation, Tier, Language, random) with { Structure = Structure };
        return BasicQuestionTemplates.ApplyUnit(fresh, QuestionUnits.Find(this)!);
    }

    public ArithmeticQuizQuestion ToPracticeQuestion(MathWordProblem text, ArithmeticQuizMode mode, Random? random = null)
    {
        if (!IsValid) throw new InvalidOperationException("Invalid stored arithmetic contract.");
        // A structured story can have multiple answers/steps. It must not fall
        // through the legacy two-operand adapter and become an unrelated 1 + 1.
        if (Version == ReasoningStoryCatalogue.Version) throw new InvalidOperationException("UseStructuredReasoningDraft");
        random ??= Random.Shared;
        if (Version == FractionQuestionCatalogue.Version) return FractionQuestionCatalogue.ToPractice(this, text, mode, random);
        BigInteger answer = Answer;
        FindXQuizContract? findX = Family == BankQuestionFamily.FindX ? FindXQuestionCatalogue.Equation(this) : null;
        if (mode == ArithmeticQuizMode.Essay) return new(Expression, mode, answer, null, null, [], text, FindXProblem: findX);
        if (mode == ArithmeticQuizMode.TrueFalse)
        {
            BigInteger presented = random.Next(2) == 0 ? answer : answer + random.Next(1, 10);
            return new(Expression, mode, answer, presented, presented == answer, [], text, FindXProblem: findX);
        }
        var choices = new List<BigInteger> { answer, answer + 1, answer + 2, answer + 3 };
        random.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(choices));
        return new(Expression, mode, answer, null, null, choices, text, FindXProblem: findX);
    }
}

public sealed record AiGenerationOptions(ArithmeticOperation Operation, CurriculumTier Tier,
    AppLanguage Language, int Count, bool AutoInsert, QuestionLearningProfile? Profile = null,
    BankQuestionFamily Family = BankQuestionFamily.Arithmetic, FindXUnknownRole UnknownRole = FindXUnknownRole.None, int StoryVariant = 0)
{
    public void Validate()
    {
        if (ReasoningStoryCatalogue.Supports(Family) && !ReasoningStoryCatalogue.Variants(Family, Tier).Contains(StoryVariant)
            || !Enum.IsDefined(Family) || !Enum.IsDefined(UnknownRole)
            || Family != BankQuestionFamily.FindX && UnknownRole != FindXUnknownRole.None
            || Family == BankQuestionFamily.FindX && UnknownRole == FindXUnknownRole.None
            || Family == BankQuestionFamily.FindX && !FindXQuestionCatalogue.Available(Profile ?? new(QuestionKnowledgeGroup.Objects), Operation, Tier, UnknownRole).Any()
            || Family == BankQuestionFamily.Fraction && !FractionQuestionCatalogue.Available(Profile ?? new(QuestionKnowledgeGroup.Objects), Operation, Tier).Any()
            || !Enum.IsDefined(Operation) || !Enum.IsDefined(Tier)
            || Language is not (AppLanguage.Vietnamese or AppLanguage.English) || Count is < 1 or > 100
            || !ReasoningStoryCatalogue.Supports(Family) && Profile is not null && !Profile.Allows(Operation))
            throw new ArgumentOutOfRangeException(nameof(Count));
    }
}
