using MathSolver.Models;
using System.Numerics;

namespace MathSolver.Services;

/// <summary>C# math puzzle data and rules.</summary>
public sealed partial class AverageQuizGenerator
{
    private sealed record DirectContext(
        string ViAction, string EnAction,
        string ViUnit, string EnUnit,
        string ViSubject, string EnSubject);

    private sealed record DistributionContext(
        string ViGroup, string EnGroup,
        string ViUnit, string EnUnit,
        string ViSubject, string EnSubject);

    private sealed record TwoGroupContext(
        string ViMember, string EnMember,
        string ViUnit, string EnUnit,
        string ViSubject, string EnSubject);

    private static readonly DirectContext[] DirectContexts =
    [
        new("một cửa hàng bán", "a store sells", "quyển vở", "notebooks", "số vở trung bình mỗi ngày", "average notebooks per day"),
        new("một thư viện cho mượn", "a library lends", "quyển sách", "books", "số sách trung bình mỗi ngày", "average books per day"),
        new("một trang trại thu hoạch", "a farm harvests", "kg cam", "kg of oranges", "khối lượng cam trung bình mỗi ngày", "average kilograms of oranges per day"),
        new("một xưởng đóng gói", "a workshop packs", "hộp bút", "pen boxes", "số hộp bút trung bình mỗi ngày", "average pen boxes per day")
    ];

    private static readonly DistributionContext[] DistributionContexts =
    [
        new("lớp", "classes", "cây", "trees", "số cây trung bình mỗi lớp", "average trees per class"),
        new("hộp", "boxes", "chiếc bút", "pens", "số bút trung bình mỗi hộp", "average pens per box"),
        new("kệ", "shelves", "quyển sách", "books", "số sách trung bình mỗi kệ", "average books per shelf"),
        new("đội", "teams", "kg giấy", "kg of paper", "khối lượng giấy trung bình mỗi đội", "average kilograms of paper per team")
    ];

    private static readonly TwoGroupContext[] TwoGroupContexts =
    [
        new("bạn", "students", "điểm", "points", "điểm trung bình chung", "combined average score"),
        new("bạn", "readers", "trang sách", "pages", "số trang trung bình chung", "combined average pages"),
        new("thành viên", "members", "viên bi", "marbles", "số viên bi trung bình chung", "combined average marbles")
    ];

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
        int count = level.HasValue ? level.Value + 1 : _random.Next(3, 6);
        int average = level.HasValue ? _random.Next(5 * level.Value, 15 * level.Value + 1) : _random.Next(20, 81);
        int[] values = CreateValuesWithAverage(count, average, level ?? 4, level.HasValue ? 3 * level.Value : 18);
        int total = values.Sum();
        string list = JoinValues(values);
        DirectContext context = DirectContexts[_random.Next(DirectContexts.Length)];

        string problem = language == AppLanguage.Vietnamese
            ? $"Trong {count} ngày, {context.ViAction} lần lượt {list} {context.ViUnit}. Trung bình mỗi ngày là bao nhiêu {context.ViUnit}?"
            : $"Over {count} days, {context.EnAction} {list} {context.EnUnit}, respectively. What is the average number of {context.EnUnit} per day?";
        string equation = $"({string.Join(" + ", values)}) ÷ {count} = {average}";
        string solution = FormatSolution(
            language == AppLanguage.Vietnamese
                ? $"Trung bình mỗi ngày ({context.ViUnit}) là:"
                : $"The average per day ({context.EnUnit}) is:",
            equation,
            language == AppLanguage.Vietnamese ? context.ViUnit : context.EnUnit,
            average,
            language);

        return new(
            AverageQuizType.Direct,
            [count, .. values],
            average,
            language == AppLanguage.Vietnamese ? context.ViUnit : context.EnUnit,
            language == AppLanguage.Vietnamese ? context.ViSubject : context.EnSubject,
            problem,
            equation,
            solution,
            total,
            ArithmeticOperation.Divide,
            count);
    }

    private AverageQuizContract CreateTotalToAverage(AppLanguage language, int? level)
    {
        int count = level.HasValue ? _random.Next(2 + level.Value, 4 + 2 * level.Value) : _random.Next(3, 9);
        int average = level.HasValue ? _random.Next(3 * level.Value, 12 * level.Value + 1) : _random.Next(12, 51);
        int total = count * average;
        DistributionContext context = DistributionContexts[_random.Next(DistributionContexts.Length)];
        string problem = language == AppLanguage.Vietnamese
            ? $"{count} {context.ViGroup} có tổng cộng {total} {context.ViUnit}. Trung bình mỗi {context.ViGroup} có bao nhiêu {context.ViUnit}?"
            : $"{count} {context.EnGroup} have {total} {context.EnUnit} in total. How many {context.EnUnit} are there per group on average?";
        string equation = $"{total} ÷ {count} = {average}";
        string solution = FormatSolution(
            language == AppLanguage.Vietnamese
                ? $"Trung bình mỗi {context.ViGroup} ({context.ViUnit}) là:"
                : $"The average per group ({context.EnUnit}) is:",
            equation,
            language == AppLanguage.Vietnamese ? context.ViUnit : context.EnUnit,
            average,
            language);

        return new(
            AverageQuizType.TotalToAverage,
            [count, total],
            average,
            language == AppLanguage.Vietnamese ? context.ViUnit : context.EnUnit,
            language == AppLanguage.Vietnamese ? context.ViSubject : context.EnSubject,
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
        DistributionContext context = DistributionContexts[_random.Next(DistributionContexts.Length)];
        string problem = language == AppLanguage.Vietnamese
            ? $"Có {count} {context.ViGroup}, trung bình mỗi {context.ViGroup} có {average} {context.ViUnit}. Tất cả có bao nhiêu {context.ViUnit}?"
            : $"There are {count} {context.EnGroup}, with an average of {average} {context.EnUnit} per group. How many {context.EnUnit} are there altogether?";
        string equation = $"{average} × {count} = {total}";
        string solution = FormatSolution(
            language == AppLanguage.Vietnamese
                ? $"Tổng số {context.ViUnit} là:"
                : $"The total number of {context.EnUnit} is:",
            equation,
            language == AppLanguage.Vietnamese ? context.ViUnit : context.EnUnit,
            total,
            language);

        return new(
            AverageQuizType.AverageToTotal,
            [count, average],
            total,
            language == AppLanguage.Vietnamese ? context.ViUnit : context.EnUnit,
            language == AppLanguage.Vietnamese ? $"tổng {context.ViSubject}" : $"total {context.EnSubject}",
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
            string problem = language == AppLanguage.Vietnamese
                ? $"{name} có điểm của 3 bài đầu lần lượt là {a}, {b}, {c}. Bài thứ {count} {name} cần bao nhiêu điểm để điểm trung bình của {count} bài là {targetAverage}?"
                : $"{name} scores {a}, {b}, and {c} on the first 3 tests. What score is needed on test {count} for an average of {targetAverage} across {count} tests?";
            string equation = $"{targetAverage} × {count} − ({a} + {b} + {c}) = {missing}";
            string solution = FormatSolution(
                language == AppLanguage.Vietnamese
                    ? $"Điểm bài thứ {count} cần có là:"
                    : $"The score needed on test {count} is:",
                equation,
                language == AppLanguage.Vietnamese ? "điểm" : "points",
                missing,
                language);

            IReadOnlyList<int> facts = language == AppLanguage.Vietnamese
                ? [3, a, b, c, count, count, targetAverage]
                : [a, b, c, 3, count, targetAverage, count];

            return new(
                AverageQuizType.MissingValue,
                facts,
                missing,
                language == AppLanguage.Vietnamese ? "điểm" : "points",
                language == AppLanguage.Vietnamese ? "điểm bài còn thiếu" : "missing test score",
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
            DirectContext context = DirectContexts[_random.Next(DirectContexts.Length)];
            string problem = language == AppLanguage.Vietnamese
                ? $"Lan có {lan} {context.ViUnit}, Mai nhiều hơn Lan {more} {context.ViUnit}, Hoa ít hơn Mai {less} {context.ViUnit}. Trung bình mỗi bạn có bao nhiêu {context.ViUnit}?"
                : $"Lan has {lan} {context.EnUnit}. Mai has {more} more {context.EnUnit} than Lan, and Hoa has {less} fewer {context.EnUnit} than Mai. How many {context.EnUnit} does each person have on average?";
            string equation =
                $"({lan} + ({lan} + {more}) + ({lan} + {more} − {less})) ÷ 3 = {average}";
            var contract = new AverageQuizContract(
                AverageQuizType.IndirectData,
                [lan, more, less],
                average,
                language == AppLanguage.Vietnamese ? context.ViUnit : context.EnUnit,
                language == AppLanguage.Vietnamese ? $"{context.ViSubject} mỗi bạn" : $"{context.EnSubject} per person",
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
            TwoGroupContext context = TwoGroupContexts[_random.Next(TwoGroupContexts.Length)];
            string problem = language == AppLanguage.Vietnamese
                ? $"Nhóm A có {countA} {context.ViMember}, trung bình mỗi người có {averageA} {context.ViUnit}. Nhóm B có {countB} {context.ViMember}, trung bình mỗi người có {averageB} {context.ViUnit}. Trung bình chung của cả hai nhóm là bao nhiêu {context.ViUnit}?"
                : $"Group A has {countA} {context.EnMember}, averaging {averageA} {context.EnUnit} each. Group B has {countB} {context.EnMember}, averaging {averageB} {context.EnUnit} each. What is the combined average in {context.EnUnit}?";
            string equation =
                $"({countA} × {averageA} + {countB} × {averageB}) ÷ ({countA} + {countB}) = {average}";
            string solution = FormatSolution(
                language == AppLanguage.Vietnamese
                    ? $"Trung bình chung ({context.ViUnit}) là:"
                    : $"The combined average ({context.EnUnit}) is:",
                equation,
                language == AppLanguage.Vietnamese ? context.ViUnit : context.EnUnit,
                average,
                language);

            return new(
                AverageQuizType.TwoGroups,
                [countA, averageA, countB, averageB],
                average,
                language == AppLanguage.Vietnamese ? context.ViUnit : context.EnUnit,
                language == AppLanguage.Vietnamese ? context.ViSubject : context.EnSubject,
                problem,
                equation,
                solution,
                totalPoints,
                ArithmeticOperation.Divide,
                totalCount);
        }

        // Fallback deterministic and integer.
        int ca = 4, aa = 8, cb = 4, ab = 6, answer = 7;
        string fallbackProblem = language == AppLanguage.Vietnamese
            ? "Nhóm A có 4 bạn, điểm trung bình là 8. Nhóm B có 4 bạn, điểm trung bình là 6. Điểm trung bình chung của cả hai nhóm là bao nhiêu?"
            : "Group A has 4 students with an average score of 8. Group B has 4 students with an average score of 6. What is the combined average score?";
        string fallbackEquation = "(4 × 8 + 4 × 6) ÷ (4 + 4) = 7";
        string fallbackSolution = FormatSolution(
            language == AppLanguage.Vietnamese
                ? "Điểm trung bình chung là:"
                : "The combined average score is:",
            fallbackEquation,
            language == AppLanguage.Vietnamese ? "điểm" : "points",
            answer,
            language);
        return new(
            AverageQuizType.TwoGroups,
            [ca, aa, cb, ab],
            answer,
            language == AppLanguage.Vietnamese ? "điểm" : "points",
            language == AppLanguage.Vietnamese ? "điểm trung bình chung" : "combined average score",
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

    private int[] CreateValuesWithAverage(int count, int average, int minOffset, int maxOffset)
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
            if (values[^1] > 0 && values[^1] <= average + maxOffset * 2)
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
        string answerLabel = language == AppLanguage.Vietnamese ? "Đáp số" : "Answer";
        return $"{lead}{Environment.NewLine}" +
               $"{equation} {unit}{Environment.NewLine}" +
               $"{answerLabel}: {answer} {unit}";
    }

    private static string BuildSolutionLead(AverageQuizContract contract) =>
        AppLanguageManager.CurrentLanguage == AppLanguage.Vietnamese
            ? $"{contract.SubjectName} là:"
            : $"The {contract.SubjectName} is:";

    private void Shuffle<T>(IList<T> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
