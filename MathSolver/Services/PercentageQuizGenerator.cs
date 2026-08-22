using MathSolver.Models;
using System.Numerics;

namespace MathSolver.Services;

/// <summary>
/// Sinh ba dạng phần trăm cơ bản: tỉ số phần trăm, giá trị của một số phần
/// trăm, và biết giá trị phần trăm để tìm toàn bộ.
/// </summary>
public sealed class PercentageQuizGenerator
{
    private sealed record ItemContext(
        string ViUnit,
        string EnUnit,
        string ViSubject,
        string EnSubject,
        string ViRatioPart,
        string EnRatioPart);

    private static readonly ItemContext[] Contexts =
    [
        new("quyển sách", "books", "số sách", "number of books", "quyển truyện", "story books"),
        new("cây", "trees", "số cây", "number of trees", "cây xoài", "mango trees"),
        new("học sinh", "students", "số học sinh", "number of students", "học sinh nữ", "female students"),
        new("viên bi", "marbles", "số viên bi", "number of marbles", "viên bi đỏ", "red marbles")
    ];

    private readonly Random _random;

    public PercentageQuizGenerator(Random? random = null)
    {
        _random = random ?? Random.Shared;
    }

    public ArithmeticQuizQuestion GenerateAlgorithm(
        ArithmeticQuizMode mode,
        PercentageQuizType? requestedType,
        AppLanguage language)
    {
        PercentageQuizContract contract = CreateContract(requestedType, language);
        return CreateQuestion(mode, contract, includeWordProblem: false);
    }

    public ArithmeticQuizQuestion GenerateContract(
        ArithmeticQuizMode mode,
        PercentageQuizType? requestedType,
        AppLanguage language)
    {
        PercentageQuizContract contract = CreateContract(requestedType, language);
        return CreateQuestion(mode, contract, includeWordProblem: false);
    }

    private PercentageQuizContract CreateContract(
        PercentageQuizType? requestedType,
        AppLanguage language)
    {
        PercentageQuizType type = requestedType ??
            (PercentageQuizType)_random.Next(Enum.GetValues<PercentageQuizType>().Length);

        return type switch
        {
            PercentageQuizType.FindPercentageRatio => CreateRatio(language),
            PercentageQuizType.FindPercentageValue => CreateValue(language),
            PercentageQuizType.FindWholeFromPercentageValue => CreateWhole(language),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    private PercentageQuizContract CreateRatio(AppLanguage language)
    {
        int[] percentages = [10, 20, 25, 30, 40, 50, 60, 75, 80, 90];
        int percentage = percentages[_random.Next(percentages.Length)];
        int whole = PickMultipleOf(20, 40, 200);
        int part = whole * percentage / 100;
        ItemContext item = Contexts[_random.Next(Contexts.Length)];
        string unit = language == AppLanguage.Vietnamese ? "%" : "%";
        string problem = language == AppLanguage.Vietnamese
            ? $"Có tất cả {whole} {item.ViUnit}, trong đó có {part} {item.ViRatioPart}. Hỏi số {item.ViRatioPart} chiếm bao nhiêu phần trăm tổng số {item.ViUnit}?"
            : $"There are {whole} {item.EnUnit} in total, including {part} {item.EnRatioPart}. What percentage of the total are {item.EnRatioPart}?";
        string solution = language == AppLanguage.Vietnamese
            ? $"Tỉ số phần trăm: {part} × 100 ÷ {whole} = {percentage}%"
            : $"Percentage ratio: {part} × 100 ÷ {whole} = {percentage}%";

        return new(
            PercentageQuizType.FindPercentageRatio,
            [whole, part],
            percentage,
            unit,
            language == AppLanguage.Vietnamese ? "tỉ số phần trăm" : "percentage ratio",
            problem,
            $"{part * 100} ÷ {whole} = {percentage}",
            solution,
            part * 100,
            ArithmeticOperation.Divide,
            whole);
    }

    private PercentageQuizContract CreateValue(AppLanguage language)
    {
        int[] percentages = [10, 20, 25, 30, 40, 50, 60, 75, 80];
        int percentage = percentages[_random.Next(percentages.Length)];
        int whole = PickMultipleOf(100, 100, 600);
        int value = whole * percentage / 100;
        ItemContext item = Contexts[_random.Next(Contexts.Length)];
        string unit = language == AppLanguage.Vietnamese ? item.ViUnit : item.EnUnit;
        string subject = language == AppLanguage.Vietnamese ? item.ViSubject : item.EnSubject;
        string problem = language == AppLanguage.Vietnamese
            ? $"Có tất cả {whole} {item.ViUnit}. Số được chọn bằng {percentage}% tổng số. Hỏi số được chọn là bao nhiêu {item.ViUnit}?"
            : $"There are {whole} {item.EnUnit} in total. The selected amount is {percentage}% of the total. How many {item.EnUnit} are selected?";
        string solution = language == AppLanguage.Vietnamese
            ? $"Giá trị {percentage}% của {whole}: {whole} ÷ 100 × {percentage} = {value} {item.ViUnit}"
            : $"{percentage}% of {whole}: {whole} ÷ 100 × {percentage} = {value} {item.EnUnit}";

        return new(
            PercentageQuizType.FindPercentageValue,
            [whole, percentage],
            value,
            unit,
            subject,
            problem,
            $"{whole / 100} × {percentage} = {value}",
            solution,
            whole / 100,
            ArithmeticOperation.Multiply,
            percentage);
    }

    private PercentageQuizContract CreateWhole(AppLanguage language)
    {
        int[] percentages = [10, 20, 25, 40, 50, 75, 80];
        int percentage = percentages[_random.Next(percentages.Length)];
        int whole = PickMultipleOf(100, 100, 600);
        int value = whole * percentage / 100;
        ItemContext item = Contexts[_random.Next(Contexts.Length)];
        string unit = language == AppLanguage.Vietnamese ? item.ViUnit : item.EnUnit;
        string subject = language == AppLanguage.Vietnamese ? $"tổng {item.ViSubject}" : $"total {item.EnSubject}";
        string problem = language == AppLanguage.Vietnamese
            ? $"{value} {item.ViUnit} chiếm {percentage}% tổng số. Hỏi có tất cả bao nhiêu {item.ViUnit}?"
            : $"{value} {item.EnUnit} make up {percentage}% of the total. How many {item.EnUnit} are there altogether?";
        string solution = language == AppLanguage.Vietnamese
            ? $"Toàn bộ số lượng: {value} × 100 ÷ {percentage} = {whole} {item.ViUnit}"
            : $"Whole amount: {value} × 100 ÷ {percentage} = {whole} {item.EnUnit}";

        return new(
            PercentageQuizType.FindWholeFromPercentageValue,
            [value, percentage],
            whole,
            unit,
            subject,
            problem,
            $"{value * 100} ÷ {percentage} = {whole}",
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
