using MathSolver.Models;
using System.Numerics;

namespace MathSolver.Services;

/// <summary>
/// Sinh ba dạng phần trăm cơ bản: tỉ số phần trăm, giá trị của một số phần
/// trăm, và biết giá trị phần trăm để tìm toàn bộ.
/// </summary>
public sealed partial class PercentageQuizGenerator
{
    private sealed record ItemContext(string Unit, string Subject, string RatioPart);

    private IReadOnlyList<ItemContext> Contexts(AppLanguage language) => NarrativeContextExpansion.Load<ItemContext>("PercentageQuizGenerator.Contexts", language, _expandNarratives);

    private readonly Random _random;
    private readonly bool _expandNarratives;

    public PercentageQuizGenerator(Random? random = null, bool expandNarratives = true)
    {
        _random = random ?? Random.Shared;
        _expandNarratives = expandNarratives;
    }

    public ArithmeticQuizQuestion GenerateAlgorithm(
        ArithmeticQuizMode mode,
        PercentageQuizType? requestedType,
        AppLanguage language,
        QuizCurriculumContext? curriculumContext = null)
    {
        PercentageQuizContract contract = CreateContract(
            requestedType,
            language,
            curriculumContext);
        return CreateQuestion(mode, contract, includeWordProblem: false);
    }

    public ArithmeticQuizQuestion GenerateContract(
        ArithmeticQuizMode mode,
        PercentageQuizType? requestedType,
        AppLanguage language,
        QuizCurriculumContext? curriculumContext = null)
    {
        PercentageQuizContract contract = CreateContract(
            requestedType,
            language,
            curriculumContext);
        return CreateQuestion(mode, contract, includeWordProblem: false);
    }

    private PercentageQuizContract CreateContract(
        PercentageQuizType? requestedType,
        AppLanguage language,
        QuizCurriculumContext? curriculumContext)
    {
        IReadOnlyList<PercentageQuizType> allowedTypes =
            curriculumContext.HasValue
                ? QuizCurriculumLayer.GetAllowedPercentageTypes(
                    curriculumContext.Value)
                : Enum.GetValues<PercentageQuizType>();

        if (allowedTypes.Count == 0)
        {
            throw new InvalidOperationException(
                "Percentage problems are not available at the selected curriculum tier.");
        }

        int? level = QuizDifficultyPolicy.Level(curriculumContext);
        allowedTypes = QuizDifficultyPolicy.Prefer(allowedTypes, requestedType, level,
            QuizDifficultyPolicy.PercentageTypes);
        PercentageQuizType type =
            requestedType.HasValue &&
            allowedTypes.Contains(requestedType.Value)
                ? requestedType.Value
                : allowedTypes[_random.Next(allowedTypes.Count)];

        PercentageQuizContract contract = _random.Next(2) == 0 ? CreateStoryPercentage(type, language, level) : type switch
        {
            PercentageQuizType.FindPercentageRatio => CreateRatio(language, level),
            PercentageQuizType.FindPercentageValue => CreateValue(language, level),
            PercentageQuizType.FindWholeFromPercentageValue => CreateWhole(language, level),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        return level is >= 4 ? CombineKnownQuantity(contract, language, level.Value) : contract;
    }

    private PercentageQuizContract CreateRatio(AppLanguage language, int? level)
    {
        int[] percentages = [10, 20, 25, 30, 40, 50, 60, 75, 80, 90];
        if (level.HasValue) percentages = QuizDifficultyPolicy.Percentages(level.Value);
        int percentage = percentages[_random.Next(percentages.Length)];
        int whole = level.HasValue ? PickPercentageWhole(percentage, level.Value) : PickMultipleOf(20, 40, 200);
        int part = whole * percentage / 100;
        ItemContext item = Contexts(language)[_random.Next(Contexts(language).Count)];
        const string unit = "%";
        string problem = QuizContentCatalog.Text(language, "PercentageQuizGenerator.CreateRatio.012", ("whole", $"{whole}"), ("item_ViUnit", $"{item.Unit}"), ("part", $"{part}"), ("item_ViRatioPart", $"{item.RatioPart}"), ("item_EnUnit", $"{item.Unit}"), ("item_EnRatioPart", $"{item.RatioPart}"));
        string equation = $"{part} ÷ {whole} × 100 = {percentage}";
        string solution = FormatSolution(
            QuizContentCatalog.Text(language, "PercentageQuizGenerator.CreateRatio.013", ("item_ViRatioPart", $"{item.RatioPart}"), ("item_ViUnit", $"{item.Unit}"), ("item_EnRatioPart", $"{item.RatioPart}"), ("item_EnUnit", $"{item.Unit}")),
            equation,
            unit,
            percentage,
            language);

        return new(
            PercentageQuizType.FindPercentageRatio,
            [whole, part],
            percentage,
            unit,
            item.RatioPart,
            problem,
            equation,
            solution,
            part * 100,
            ArithmeticOperation.Divide,
            whole);
    }

    private PercentageQuizContract CreateValue(AppLanguage language, int? level)
    {
        int[] percentages = [10, 20, 25, 30, 40, 50, 60, 75, 80];
        if (level.HasValue) percentages = QuizDifficultyPolicy.Percentages(level.Value);
        int percentage = percentages[_random.Next(percentages.Length)];
        int whole = level.HasValue ? PickPercentageWhole(percentage, level.Value) : PickMultipleOf(100, 100, 600);
        int value = whole * percentage / 100;
        ItemContext item = Contexts(language)[_random.Next(Contexts(language).Count)];
        string unit = item.RatioPart;
        string subject = QuizContentCatalog.Text(language, "PercentageQuizGenerator.CreateValue.014", ("item_ViRatioPart", $"{item.RatioPart}"), ("item_EnRatioPart", $"{item.RatioPart}"));
        string problem = QuizContentCatalog.Text(language, "PercentageQuizGenerator.CreateValue.015", ("whole", $"{whole}"), ("item_ViUnit", $"{item.Unit}"), ("percentage", $"{percentage}"), ("item_ViRatioPart", $"{item.RatioPart}"), ("item_EnUnit", $"{item.Unit}"), ("item_EnRatioPart", $"{item.RatioPart}"));
        string equation = $"{whole} × {percentage} ÷ 100 = {value}";
        string solution = FormatSolution(
            QuizContentCatalog.Text(language, "PercentageQuizGenerator.CreateValue.016", ("item_ViRatioPart", $"{item.RatioPart}"), ("item_EnRatioPart", $"{item.RatioPart}")),
            equation,
            unit,
            value,
            language);

        return new(
            PercentageQuizType.FindPercentageValue,
            [whole, percentage],
            value,
            unit,
            subject,
            problem,
            equation,
            solution,
            whole * percentage,
            ArithmeticOperation.Divide,
            100);
    }

    private PercentageQuizContract CreateWhole(AppLanguage language, int? level)
    {
        int[] percentages = [10, 20, 25, 40, 50, 75, 80];
        if (level.HasValue) percentages = QuizDifficultyPolicy.Percentages(level.Value);
        int percentage = percentages[_random.Next(percentages.Length)];
        int whole = level.HasValue ? PickPercentageWhole(percentage, level.Value) : PickMultipleOf(100, 100, 600);
        int value = whole * percentage / 100;
        ItemContext item = Contexts(language)[_random.Next(Contexts(language).Count)];
        string unit = item.Unit;
        string subject = QuizContentCatalog.Text(language, "PercentageQuizGenerator.CreateWhole.017", ("item_ViSubject", $"{item.Subject}"), ("item_EnSubject", $"{item.Subject}"));
        string problem = QuizContentCatalog.Text(language, "PercentageQuizGenerator.CreateWhole.018", ("value", $"{value}"), ("item_ViRatioPart", $"{item.RatioPart}"), ("percentage", $"{percentage}"), ("item_ViUnit", $"{item.Unit}"), ("item_EnRatioPart", $"{item.RatioPart}"), ("item_EnUnit", $"{item.Unit}"));
        string equation = $"{value} × 100 ÷ {percentage} = {whole}";
        string solution = FormatSolution(
            QuizContentCatalog.Text(language, "PercentageQuizGenerator.CreateWhole.019", ("item_ViUnit", $"{item.Unit}"), ("item_EnUnit", $"{item.Unit}")),
            equation,
            unit,
            whole,
            language);

        return new(
            PercentageQuizType.FindWholeFromPercentageValue,
            [value, percentage],
            whole,
            unit,
            subject,
            problem,
            equation,
            solution,
            value * 100,
            ArithmeticOperation.Divide,
            percentage);
    }

    private ArithmeticQuizQuestion CreateQuestion(
        ArithmeticQuizMode mode,
        PercentageQuizContract contract,
        bool includeWordProblem)
    {
        var expression = new IntegerArithmeticExpression(
            contract.RepresentativeLeft,
            contract.RepresentativeOperation,
            contract.RepresentativeRight);
        MathWordProblem? wordProblem = includeWordProblem
            ? new(
                contract.ProblemText,
                BuildSolutionLead(contract),
                contract.AnswerUnit,
                contract.SubjectName)
            : null;

        return mode switch
        {
            ArithmeticQuizMode.TrueFalse => CreateTrueFalse(expression, contract, wordProblem),
            ArithmeticQuizMode.MultipleChoice => CreateMultipleChoice(expression, contract, wordProblem),
            ArithmeticQuizMode.Essay => new(
                expression,
                mode,
                contract.CorrectAnswer,
                null,
                null,
                [],
                wordProblem,
                PercentageProblem: contract),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    private ArithmeticQuizQuestion CreateTrueFalse(
        IntegerArithmeticExpression expression,
        PercentageQuizContract contract,
        MathWordProblem? wordProblem)
    {
        bool correct = _random.Next(2) == 0;
        BigInteger presented = correct
            ? contract.CorrectAnswer
            : CreateDistractors(contract.CorrectAnswer, 1)[0];
        return new(
            expression,
            ArithmeticQuizMode.TrueFalse,
            contract.CorrectAnswer,
            presented,
            presented == contract.CorrectAnswer,
            [],
            wordProblem,
            PercentageProblem: contract);
    }

    private ArithmeticQuizQuestion CreateMultipleChoice(
        IntegerArithmeticExpression expression,
        PercentageQuizContract contract,
        MathWordProblem? wordProblem)
    {
        var choices = new List<BigInteger> { contract.CorrectAnswer };
        choices.AddRange(CreateDistractors(contract.CorrectAnswer, 3));
        Shuffle(choices);
        return new(
            expression,
            ArithmeticQuizMode.MultipleChoice,
            contract.CorrectAnswer,
            null,
            null,
            choices,
            wordProblem,
            PercentageProblem: contract);
    }

    private IReadOnlyList<BigInteger> CreateDistractors(BigInteger answer, int count)
    {
        var values = new HashSet<BigInteger>();
        int step = answer >= 100 ? 10 : answer >= 20 ? 5 : 1;
        int[] offsets = [-3, -2, -1, 1, 2, 3, 5, 10];
        foreach (int offset in offsets.OrderBy(_ => _random.Next()))
        {
            BigInteger candidate = answer + step * offset;
            if (candidate >= 0 && candidate != answer)
            {
                values.Add(candidate);
            }
            if (values.Count >= count)
            {
                break;
            }
        }
        return values.Take(count).ToArray();
    }

    private int PickMultipleOf(int multiple, int min, int max)
    {
        int minFactor = Math.Max(1, (min + multiple - 1) / multiple);
        int maxFactor = Math.Max(minFactor + 1, max / multiple + 1);
        return _random.Next(minFactor, maxFactor) * multiple;
    }

    private static string BuildSolutionLead(PercentageQuizContract contract) =>
        QuizContentCatalog.Text(AppLanguageManager.CurrentLanguage, "PercentageQuizGenerator.BuildSolutionLead.001", ("contract_SubjectName", $"{contract.SubjectName}"));

    private static string FormatSolution(
        string lead,
        string equation,
        string unit,
        int answer,
        AppLanguage language)
    {
        string answerLabel = QuizContentCatalog.Text(language, "PercentageQuizGenerator.FormatSolution.002");
        string unitSuffix = unit == "%" ? unit : $" {unit}";
        return $"{lead}{Environment.NewLine}" +
               $"{equation}{unitSuffix}{Environment.NewLine}" +
               $"{answerLabel}: {answer}{unitSuffix}";
    }

    private void Shuffle<T>(IList<T> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
