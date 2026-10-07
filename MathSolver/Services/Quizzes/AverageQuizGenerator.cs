using MathSolver.Models;
using System.Numerics;

namespace MathSolver.Services;

/// <summary>C# math puzzle data and rules.</summary>
public sealed partial class AverageQuizGenerator
{
    private sealed record DirectContext(string Action, string Unit, string Subject, string Id, string Period, int Capacity);

    private sealed record DistributionContext(string Group, string Unit, string Subject);

    private sealed record TwoGroupContext(string Member, string Unit, string Subject);

    private static IReadOnlyList<DirectContext> DirectContexts(AppLanguage language) => QuizContentCatalog.LoadList<DirectContext>("AverageQuizGenerator.DirectContexts", QuizContentCatalog.Culture(language));

    private static IReadOnlyList<DistributionContext> DistributionContexts(AppLanguage language) => QuizContentCatalog.LoadList<DistributionContext>("AverageQuizGenerator.DistributionContexts", QuizContentCatalog.Culture(language));

    private static IReadOnlyList<TwoGroupContext> TwoGroupContexts(AppLanguage language) => QuizContentCatalog.LoadList<TwoGroupContext>("AverageQuizGenerator.TwoGroupContexts", QuizContentCatalog.Culture(language));

    private readonly Random _random;

    public AverageQuizGenerator(Random? random = null)
    {
        _random = random ?? Random.Shared;
    }

    public ArithmeticQuizQuestion GenerateAlgorithm(
        ArithmeticQuizMode mode,
        AverageQuizType? requestedType,
        AppLanguage language,
        QuizCurriculumContext? curriculumContext = null)
    {
        AverageQuizContract contract = CreateContract(
            requestedType,
            language,
            curriculumContext);
        return CreateQuestion(mode, contract, includeWordProblem: false);
    }

    public ArithmeticQuizQuestion GenerateContract(
        ArithmeticQuizMode mode,
        AverageQuizType? requestedType,
        AppLanguage language,
        QuizCurriculumContext? curriculumContext = null)
    {
        AverageQuizContract contract = CreateContract(
            requestedType,
            language,
            curriculumContext);
        return CreateQuestion(mode, contract, includeWordProblem: false);
    }

    private AverageQuizContract CreateContract(
        AverageQuizType? requestedType,
        AppLanguage language,
        QuizCurriculumContext? curriculumContext)
    {
        IReadOnlyList<AverageQuizType> allowedTypes =
            curriculumContext.HasValue
                ? QuizCurriculumLayer.GetAllowedAverageTypes(
                    curriculumContext.Value)
                : Enum.GetValues<AverageQuizType>();

        if (allowedTypes.Count == 0)
        {
            throw new InvalidOperationException(
                "Average problems are not available at the selected curriculum tier.");
        }

        int? level = QuizDifficultyPolicy.Level(curriculumContext);
        allowedTypes = QuizDifficultyPolicy.Prefer(allowedTypes, requestedType, level,
            QuizDifficultyPolicy.AverageTypes);

        AverageQuizType type =
            requestedType.HasValue &&
            allowedTypes.Contains(requestedType.Value)
                ? requestedType.Value
                : allowedTypes[_random.Next(allowedTypes.Count)];

        return type switch
        {
            AverageQuizType.Direct => CreateDirect(language, level),
            AverageQuizType.TotalToAverage => CreateTotalToAverage(language, level),
            AverageQuizType.AverageToTotal => CreateAverageToTotal(language, level),
            AverageQuizType.MissingValue => CreateMissingValue(language, level),
            AverageQuizType.IndirectData => CreateIndirectData(language, level),
            AverageQuizType.TwoGroups => CreateTwoGroups(language, level),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    private AverageQuizContract CreateDirect(AppLanguage language, int? level)
    {
        DirectContext context = DirectContexts(language)[_random.Next(DirectContexts(language).Count)];
        int count = level.HasValue ? level.Value + 1 : _random.Next(3, 6);
        int ceiling = Math.Min(context.Capacity, level.HasValue ? 15 * level.Value : 80);
        int average = _random.Next(Math.Min(ceiling, level.HasValue ? 5 * level.Value : 20), ceiling + 1);
        int[] values = CreateValuesWithAverage(count, average, level ?? 4, level.HasValue ? 3 * level.Value : 18, context.Capacity);
        int total = values.Sum();
        string list = JoinValues(values);

        string problem = QuizContentCatalog.Text(language, "AverageQuizGenerator.CreateDirect.013", ("count", $"{count}"), ("context_ViPeriod", $"{context.Period}"), ("context_ViAction", $"{context.Action}"), ("list", $"{list}"), ("context_ViUnit", $"{context.Unit}"), ("value1", $"{QuizStoryContextCatalog.PluralPeriod(context.Period)}"), ("context_EnAction", $"{context.Action}"), ("context_EnUnit", $"{context.Unit}"), ("context_EnPeriod", $"{context.Period}"));
        string equation = $"({string.Join(" + ", values)}) ÷ {count} = {average}";
        string solution = FormatSolution(
            QuizContentCatalog.Text(language, "AverageQuizGenerator.CreateDirect.014", ("context_ViPeriod", $"{context.Period}"), ("context_ViUnit", $"{context.Unit}"), ("context_EnPeriod", $"{context.Period}"), ("context_EnUnit", $"{context.Unit}")),
            equation,
            context.Unit,
            average,
            language);

        return new AverageQuizContract(
            AverageQuizType.Direct,
            [count, .. values],
            average,
            context.Unit,
            context.Subject,
            problem,
            equation,
            solution,
            total,
            ArithmeticOperation.Divide,
            count) { StoryContextId = context.Id };
    }

    private AverageQuizContract CreateTotalToAverage(AppLanguage language, int? level)
    {
        int count = level.HasValue ? _random.Next(2 + level.Value, 4 + 2 * level.Value) : _random.Next(3, 9);
        int average = level.HasValue ? _random.Next(3 * level.Value, 12 * level.Value + 1) : _random.Next(12, 51);
        int total = count * average;
        DistributionContext context = DistributionContexts(language)[_random.Next(DistributionContexts(language).Count)];
        string problem = QuizContentCatalog.Text(language, "AverageQuizGenerator.CreateTotalToAverage.015", ("count", $"{count}"), ("context_ViGroup", $"{context.Group}"), ("total", $"{total}"), ("context_ViUnit", $"{context.Unit}"), ("context_EnGroup", $"{context.Group}"), ("context_EnUnit", $"{context.Unit}"));
        string equation = $"{total} ÷ {count} = {average}";
        string solution = FormatSolution(
            QuizContentCatalog.Text(language, "AverageQuizGenerator.CreateTotalToAverage.016", ("context_ViGroup", $"{context.Group}"), ("context_ViUnit", $"{context.Unit}"), ("context_EnUnit", $"{context.Unit}")),
            equation,
            context.Unit,
            average,
            language);

        return new(
            AverageQuizType.TotalToAverage,
            [count, total],
            average,
            context.Unit,
            context.Subject,
            problem,
            equation,
            solution,
            total,
            ArithmeticOperation.Divide,
            count);
    }

    private AverageQuizContract CreateAverageToTotal(AppLanguage language, int? level)
    {
        int count = level.HasValue ? _random.Next(2 + level.Value, 4 + 2 * level.Value) : _random.Next(3, 9);
        int average = level.HasValue ? _random.Next(3 * level.Value, 10 * level.Value + 1) : _random.Next(8, 31);
        int total = count * average;
        DistributionContext context = DistributionContexts(language)[_random.Next(DistributionContexts(language).Count)];
        string problem = QuizContentCatalog.Text(language, "AverageQuizGenerator.CreateAverageToTotal.017", ("count", $"{count}"), ("context_ViGroup", $"{context.Group}"), ("average", $"{average}"), ("context_ViUnit", $"{context.Unit}"), ("context_EnGroup", $"{context.Group}"), ("context_EnUnit", $"{context.Unit}"));
        string equation = $"{average} × {count} = {total}";
        string solution = FormatSolution(
            QuizContentCatalog.Text(language, "AverageQuizGenerator.CreateAverageToTotal.018", ("context_ViUnit", $"{context.Unit}"), ("context_EnUnit", $"{context.Unit}")),
            equation,
            context.Unit,
            total,
            language);

        return new(
            AverageQuizType.AverageToTotal,
            [count, average],
            total,
            context.Unit,
            QuizContentCatalog.Text(language, "AverageQuizGenerator.CreateAverageToTotal.019", ("context_ViSubject", $"{context.Subject}"), ("context_EnSubject", $"{context.Subject}")),
            problem,
            equation,
            solution,
            average,
            ArithmeticOperation.Multiply,
            count);
    }

    private AverageQuizContract CreateMissingValue(AppLanguage language, int? level)
    {
        if (level.HasValue) return CreateTieredMissingValue(language, level.Value);
        const int count = 4;

        for (int attempt = 0; attempt < 64; attempt++)
        {
            int targetAverage = _random.Next(6, 10);
            int a = _random.Next(5, 11);
            int b = _random.Next(5, 11);
            int c = _random.Next(5, 11);
            int targetTotal = targetAverage * count;
            int knownTotal = a + b + c;
            int missing = targetTotal - knownTotal;

            if (missing is < 1 or > 10)
            {
                continue;
            }

            string[] viNames = ["An", "Bình", "Lan", "Minh"];
            string[] enNames = ["Alex", "Ben", "Lina", "Mia"];
            string name = language == AppLanguage.Vietnamese
                ? viNames[_random.Next(viNames.Length)]
                : enNames[_random.Next(enNames.Length)];
            string problem = QuizContentCatalog.Text(language, "AverageQuizGenerator.CreateMissingValue.001", ("name", $"{name}"), ("a", $"{a}"), ("b", $"{b}"), ("c", $"{c}"), ("count", $"{count}"), ("targetAverage", $"{targetAverage}"));
            string equation = $"{targetAverage} × {count} − ({a} + {b} + {c}) = {missing}";
            string solution = FormatSolution(
                QuizContentCatalog.Text(language, "AverageQuizGenerator.CreateMissingValue.002", ("count", $"{count}")),
                equation,
                QuizContentCatalog.Text(language, "AverageQuizGenerator.CreateMissingValue.003"),
                missing,
                language);

            IReadOnlyList<int> facts = language == AppLanguage.Vietnamese
                ? [3, a, b, c, count, count, targetAverage]
                : [a, b, c, 3, count, targetAverage, count];

            return new(
                AverageQuizType.MissingValue,
                facts,
                missing,
                QuizContentCatalog.Text(language, "AverageQuizGenerator.CreateMissingValue.004"),
                QuizContentCatalog.Text(language, "AverageQuizGenerator.CreateMissingValue.005"),
                problem,
                equation,
                solution,
                targetTotal,
                ArithmeticOperation.Subtract,
                knownTotal);
        }

        throw new InvalidOperationException("Could not create an average missing-value problem.");
    }

    private AverageQuizContract CreateIndirectData(AppLanguage language, int? level)
    {
        for (int attempt = 0; attempt < 64; attempt++)
        {
            int lan = level.HasValue ? _random.Next(8 * level.Value, 20 * level.Value + 1) : _random.Next(12, 41);
            int more = _random.Next(2, level.HasValue ? 3 * level.Value + 1 : 9);
            int less = _random.Next(1, level.HasValue ? 2 * level.Value + 1 : 8);
            int mai = lan + more;
            int hoa = mai - less;
            int total = lan + mai + hoa;

            if (hoa <= 0 || total % 3 != 0)
            {
                continue;
            }

            int average = total / 3;
            DirectContext context = DirectContexts(language)[_random.Next(DirectContexts(language).Count)];
            string problem = QuizContentCatalog.Text(language, "AverageQuizGenerator.CreateIndirectData.020", ("lan", $"{lan}"), ("context_ViUnit", $"{context.Unit}"), ("more", $"{more}"), ("less", $"{less}"), ("context_EnUnit", $"{context.Unit}"));
            string equation =
                $"({lan} + ({lan} + {more}) + ({lan} + {more} − {less})) ÷ 3 = {average}";
            var contract = new AverageQuizContract(
                AverageQuizType.IndirectData,
                [lan, more, less],
                average,
                context.Unit,
                QuizContentCatalog.Text(language, "AverageQuizGenerator.CreateIndirectData.021", ("context_ViSubject", $"{context.Subject}"), ("context_EnSubject", $"{context.Subject}")),
                problem,
                equation,
                string.Empty,
                total,
                ArithmeticOperation.Divide,
                3,
                new(lan, more, less));
            return contract with { SolutionText = AverageIndirectSolutionFormatter.Format(contract, language) };
        }

        throw new InvalidOperationException("Could not create an indirect average problem.");
    }

    private AverageQuizContract CreateTwoGroups(AppLanguage language, int? level)
    {
        for (int attempt = 0; attempt < 1024; attempt++)
        {
            int countA = _random.Next(2, level.HasValue ? 4 + 2 * level.Value : 9);
            int countB = level is <= 2 ? countA : _random.Next(2, level.HasValue ? 4 + 2 * level.Value : 9);
            if (level is >= 4 && countA == countB) continue;
            int averageA = _random.Next(6, 11);
            int averageB = _random.Next(6, 11);
            int totalCount = countA + countB;
            int totalPoints = countA * averageA + countB * averageB;

            if (totalPoints % totalCount != 0 || (level is >= 4 && averageA == averageB))
            {
                continue;
            }

            int average = totalPoints / totalCount;
            TwoGroupContext context = TwoGroupContexts(language)[_random.Next(TwoGroupContexts(language).Count)];
            string problem = QuizContentCatalog.Text(language, "AverageQuizGenerator.CreateTwoGroups.022", ("countA", $"{countA}"), ("context_ViMember", $"{context.Member}"), ("averageA", $"{averageA}"), ("context_ViUnit", $"{context.Unit}"), ("countB", $"{countB}"), ("averageB", $"{averageB}"), ("context_EnMember", $"{context.Member}"), ("context_EnUnit", $"{context.Unit}"));
            string equation =
                $"({countA} × {averageA} + {countB} × {averageB}) ÷ ({countA} + {countB}) = {average}";
            string solution = FormatSolution(
                QuizContentCatalog.Text(language, "AverageQuizGenerator.CreateTwoGroups.023", ("context_ViUnit", $"{context.Unit}"), ("context_EnUnit", $"{context.Unit}")),
                equation,
                context.Unit,
                average,
                language);

            return new(
                AverageQuizType.TwoGroups,
                [countA, averageA, countB, averageB],
                average,
                context.Unit,
                context.Subject,
                problem,
                equation,
                solution,
                totalPoints,
                ArithmeticOperation.Divide,
                totalCount);
        }

        // Fallback deterministic and integer.
        int ca = 4, aa = 8, cb = 4, ab = 6, answer = 7;
        string fallbackProblem = QuizContentCatalog.Text(language, "AverageQuizGenerator.CreateTwoGroups.006");
        string fallbackEquation = "(4 × 8 + 4 × 6) ÷ (4 + 4) = 7";
        string fallbackSolution = FormatSolution(
            QuizContentCatalog.Text(language, "AverageQuizGenerator.CreateTwoGroups.007"),
            fallbackEquation,
            QuizContentCatalog.Text(language, "AverageQuizGenerator.CreateTwoGroups.008"),
            answer,
            language);
        return new(
            AverageQuizType.TwoGroups,
            [ca, aa, cb, ab],
            answer,
            QuizContentCatalog.Text(language, "AverageQuizGenerator.CreateTwoGroups.009"),
            QuizContentCatalog.Text(language, "AverageQuizGenerator.CreateTwoGroups.010"),
            fallbackProblem,
            fallbackEquation,
            fallbackSolution,
            56,
            ArithmeticOperation.Divide,
            8);
    }

    private ArithmeticQuizQuestion CreateQuestion(
        ArithmeticQuizMode mode,
        AverageQuizContract contract,
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
                AverageProblem: contract),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    private ArithmeticQuizQuestion CreateTrueFalse(
        IntegerArithmeticExpression expression,
        AverageQuizContract contract,
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
            AverageProblem: contract);
    }

    private ArithmeticQuizQuestion CreateMultipleChoice(
        IntegerArithmeticExpression expression,
        AverageQuizContract contract,
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
            AverageProblem: contract);
    }

    private IReadOnlyList<BigInteger> CreateDistractors(BigInteger answer, int count)
    {
        var values = new HashSet<BigInteger>();
        int[] offsets = [-5, -3, -2, -1, 1, 2, 3, 5, 10];
        foreach (int offset in offsets.OrderBy(_ => _random.Next()))
        {
            BigInteger candidate = answer + offset;
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

    private int[] CreateValuesWithAverage(int count, int average, int minOffset, int maxOffset, int capacity = int.MaxValue)
    {
        for (int attempt = 0; attempt < 64; attempt++)
        {
            var values = new int[count];
            int partial = 0;
            for (int i = 0; i < count - 1; i++)
            {
                int offset = _random.Next(-minOffset, maxOffset + 1);
                values[i] = Math.Max(1, average + offset);
                partial += values[i];
            }
            values[^1] = average * count - partial;
            if (values[^1] > 0 && values[^1] <= average + maxOffset * 2 && values.All(value => value <= capacity))
            {
                Shuffle(values);
                return values;
            }
        }

        return Enumerable.Repeat(average, count).ToArray();
    }

    private static string JoinValues(IReadOnlyList<int> values) =>
        string.Join(", ", values);

    private static string FormatSolution(
        string lead,
        string equation,
        string unit,
        int answer,
        AppLanguage language)
    {
        string answerLabel = QuizContentCatalog.Text(language, "AverageQuizGenerator.FormatSolution.011");
        return $"{lead}{Environment.NewLine}" +
               $"{equation} {unit}{Environment.NewLine}" +
               $"{answerLabel}: {answer} {unit}";
    }

    private static string BuildSolutionLead(AverageQuizContract contract) =>
        QuizContentCatalog.Text(AppLanguageManager.CurrentLanguage, "AverageQuizGenerator.BuildSolutionLead.012", ("contract_SubjectName", $"{contract.SubjectName}"));

    private void Shuffle<T>(IList<T> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
