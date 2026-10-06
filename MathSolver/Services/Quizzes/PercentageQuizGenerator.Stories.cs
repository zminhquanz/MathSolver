using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class PercentageQuizGenerator
{
    private sealed record PercentageStory(string Id, string ViWhole, string EnWhole,
        string ViPart, string EnPart, string ViUnit, string EnUnit, int Capacity, bool Money = false);
    private static readonly PercentageStory[] PercentageStories = [
        new("tourism", "ngân sách chuyến tham quan", "the trip budget", "tiền mua vé", "the amount spent on tickets", "đồng", "dollars", 300000, true),
        new("community", "ngân sách mua quà", "the gift budget", "tiền mua sách", "the amount spent on books", "đồng", "dollars", 300000, true),
        new("production-quality", "số sản phẩm đã kiểm tra", "the number of inspected products", "số sản phẩm đạt yêu cầu", "the number of acceptable products", "sản phẩm", "products", 5000),
        new("craft", "số hoa giấy cần hoàn thành", "the planned number of paper flowers", "số hoa giấy đã gấp", "the number of completed paper flowers", "bông hoa giấy", "paper flowers", 200),
        new("survey", "số phiếu khảo sát", "the number of survey responses", "số phiếu chọn đọc sách", "the number choosing reading", "phiếu", "responses", 500),
        new("kitchen", "số suất ăn đã chuẩn bị", "the number of prepared meals", "số suất ăn chay", "the number of vegetarian meals", "suất ăn", "meals", 1000),
        new("environment", "lượng giấy cần thu gom", "the target mass of paper", "lượng giấy đã thu gom", "the mass of collected paper", "kg", "kg", 200),
        new("water", "lượng nước trong bể ban đầu", "the initial water volume", "lượng nước đã dùng", "the used water volume", "lít", "litres", 1000)
    ];
    private PercentageQuizContract CreateStoryPercentage(PercentageQuizType type, AppLanguage language, int? level)
    {
        var s = PercentageStories[_random.Next(PercentageStories.Length)];
        bool vi = language == AppLanguage.Vietnamese;
        int[] choices = QuizDifficultyPolicy.Percentages(level ?? 3);
        int percentage = choices[_random.Next(choices.Length)];
        int quantum = 100 / (int)System.Numerics.BigInteger.GreatestCommonDivisor(100, percentage);
        int max = Math.Min(s.Capacity, new[] { 0, 40, 100, 300, 700, 1200 }[level ?? 3]);
        int whole = quantum * _random.Next(1, Math.Max(2, max / quantum + 1));
        if (s.Money && vi) whole *= 100;
        int part = whole * percentage / 100;
        string totalLabel = vi ? s.ViWhole : s.EnWhole, partLabel = vi ? s.ViPart : s.EnPart, unit = vi ? s.ViUnit : s.EnUnit;
        string Capital(string text) => char.ToUpperInvariant(text[0]) + text[1..];
        string problem, combined, equation, lead, subject, answerUnit;
        int answer, left, right;
        int[] facts;
        if (type == PercentageQuizType.FindPercentageRatio)
        {
            string Question() => vi ? $"{Capital(partLabel)} chiếm bao nhiêu phần trăm {totalLabel}?" : $"What percentage of {totalLabel} is {partLabel}?";
            problem = vi ? $"{Capital(totalLabel)} là {whole} {unit}, trong đó {partLabel} là {part} {unit}. {Question()}"
                : $"{Capital(totalLabel)} is {whole} {unit}, of which {partLabel} is {part} {unit}. {Question()}";
            combined = vi ? $"{Capital(totalLabel)} bằng tổng của {{quantity}} {unit}, trong đó {partLabel} là {part} {unit}. {Question()}"
                : $"{Capital(totalLabel)} equals the sum of {{quantity}} {unit}, of which {partLabel} is {part} {unit}. {Question()}";
            facts = [whole, part]; answer = percentage; answerUnit = "%"; subject = partLabel;
            equation = $"{part} ÷ {whole} × 100 = {percentage}"; left = part * 100; right = whole;
            lead = vi ? $"Tỉ lệ phần trăm của {partLabel} là:" : $"The percentage for {partLabel} is:";
        }
        else if (type == PercentageQuizType.FindPercentageValue)
        {
            string Question() => vi ? $"{Capital(partLabel)} là bao nhiêu {unit}?" : $"What is {partLabel} in {unit}?";
            string relation = vi ? $"{Capital(partLabel)} bằng {percentage}% {totalLabel}. " : $"{Capital(partLabel)} is {percentage}% of {totalLabel}. ";
            problem = (vi ? $"{Capital(totalLabel)} là {whole} {unit}. " : $"{Capital(totalLabel)} is {whole} {unit}. ") + relation + Question();
            combined = (vi ? $"{Capital(totalLabel)} bằng tổng của {{quantity}} {unit}. " : $"{Capital(totalLabel)} equals the sum of {{quantity}} {unit}. ") + relation + Question();
            facts = [whole, percentage]; answer = part; answerUnit = unit; subject = partLabel;
            equation = $"{whole} × {percentage} ÷ 100 = {part}"; left = whole * percentage; right = 100;
            lead = vi ? $"{Capital(partLabel)} ({unit}) là:" : $"{Capital(partLabel)} ({unit}) is:";
        }
        else
        {
            string relation = vi ? $"bằng {percentage}% {totalLabel}. Hỏi {totalLabel} là bao nhiêu {unit}?"
                : $"representing {percentage}% of {totalLabel}. What is {totalLabel} in {unit}?";
            problem = $"{Capital(partLabel)} " + (vi ? $"là {part} {unit}, " : $"is {part} {unit}, ") + relation;
            combined = $"{Capital(partLabel)} " + (vi ? $"bằng tổng của {{quantity}} {unit}, " : $"equals the sum of {{quantity}} {unit}, ") + relation;
            facts = [part, percentage]; answer = whole; answerUnit = unit; subject = totalLabel;
            equation = $"{part} × 100 ÷ {percentage} = {whole}"; left = part * 100; right = percentage;
            lead = vi ? $"{Capital(totalLabel)} ({unit}) là:" : $"{Capital(totalLabel)} ({unit}) is:";
        }
        return new(type, facts, answer, answerUnit, subject, problem,
            equation, FormatSolution(lead, equation, answerUnit, answer, language), left, ArithmeticOperation.Divide, right)
            { StoryContextId = s.Id, CombinedProblemTemplate = combined };
    }
}
