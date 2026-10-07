using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class PercentageQuizGenerator
{
    private sealed record PercentageStory(string Id, string Whole, string Part, string Unit, int Capacity, bool Money = false);
    private static IReadOnlyList<PercentageStory> PercentageStories(AppLanguage language) => QuizContentCatalog.LoadList<PercentageStory>("PercentageQuizGenerator.PercentageStories", QuizContentCatalog.Culture(language));
    private PercentageQuizContract CreateStoryPercentage(PercentageQuizType type, AppLanguage language, int? level)
    {
        var s = PercentageStories(language)[_random.Next(PercentageStories(language).Count)];
        bool vi = language == AppLanguage.Vietnamese;
        int[] choices = QuizDifficultyPolicy.Percentages(level ?? 3);
        int percentage = choices[_random.Next(choices.Length)];
        int quantum = 100 / (int)System.Numerics.BigInteger.GreatestCommonDivisor(100, percentage);
        int max = Math.Min(s.Capacity, new[] { 0, 40, 100, 300, 700, 1200 }[level ?? 3]);
        int whole = quantum * _random.Next(1, Math.Max(2, max / quantum + 1));
        if (s.Money && vi) whole *= 100;
        int part = whole * percentage / 100;
        string totalLabel = s.Whole, partLabel = s.Part, unit = s.Unit;
        string Capital(string text) => char.ToUpperInvariant(text[0]) + text[1..];
        string problem, combined, equation, lead, subject, answerUnit;
        int answer, left, right;
        int[] facts;
        if (type == PercentageQuizType.FindPercentageRatio)
        {
            string Question() => QuizContentCatalog.Text(language, "PercentageQuizGenerator.Stories.CreateStoryPercentage.012", ("Capital_partLabel", $"{Capital(partLabel)}"), ("totalLabel", $"{totalLabel}"), ("partLabel", $"{partLabel}"));
            problem = QuizContentCatalog.Text(language, "PercentageQuizGenerator.Stories.CreateStoryPercentage.001", ("Capital_totalLabel", $"{Capital(totalLabel)}"), ("whole", $"{whole}"), ("unit", $"{unit}"), ("partLabel", $"{partLabel}"), ("part", $"{part}"), ("Question", $"{Question()}"));
            combined = QuizContentCatalog.Text(language, "PercentageQuizGenerator.Stories.CreateStoryPercentage.002", ("Capital_totalLabel", $"{Capital(totalLabel)}"), ("unit", $"{unit}"), ("partLabel", $"{partLabel}"), ("part", $"{part}"), ("Question", $"{Question()}"));
            facts = [whole, part]; answer = percentage; answerUnit = "%"; subject = partLabel;
            equation = $"{part} ÷ {whole} × 100 = {percentage}"; left = part * 100; right = whole;
            lead = QuizContentCatalog.Text(language, "PercentageQuizGenerator.Stories.CreateStoryPercentage.003", ("partLabel", $"{partLabel}"));
        }
        else if (type == PercentageQuizType.FindPercentageValue)
        {
            string Question() => QuizContentCatalog.Text(language, "PercentageQuizGenerator.Stories.CreateStoryPercentage.013", ("Capital_partLabel", $"{Capital(partLabel)}"), ("unit", $"{unit}"), ("partLabel", $"{partLabel}"));
            string relation = QuizContentCatalog.Text(language, "PercentageQuizGenerator.Stories.CreateStoryPercentage.004", ("Capital_partLabel", $"{Capital(partLabel)}"), ("percentage", $"{percentage}"), ("totalLabel", $"{totalLabel}"));
            problem = (QuizContentCatalog.Text(language, "PercentageQuizGenerator.Stories.CreateStoryPercentage.005", ("Capital_totalLabel", $"{Capital(totalLabel)}"), ("whole", $"{whole}"), ("unit", $"{unit}"))) + relation + Question();
            combined = (QuizContentCatalog.Text(language, "PercentageQuizGenerator.Stories.CreateStoryPercentage.006", ("Capital_totalLabel", $"{Capital(totalLabel)}"), ("unit", $"{unit}"))) + relation + Question();
            facts = [whole, percentage]; answer = part; answerUnit = unit; subject = partLabel;
            equation = $"{whole} × {percentage} ÷ 100 = {part}"; left = whole * percentage; right = 100;
            lead = QuizContentCatalog.Text(language, "PercentageQuizGenerator.Stories.CreateStoryPercentage.007", ("Capital_partLabel", $"{Capital(partLabel)}"), ("unit", $"{unit}"));
        }
        else
        {
            string relation = QuizContentCatalog.Text(language, "PercentageQuizGenerator.Stories.CreateStoryPercentage.008", ("percentage", $"{percentage}"), ("totalLabel", $"{totalLabel}"), ("unit", $"{unit}"));
            problem = $"{Capital(partLabel)} " + (QuizContentCatalog.Text(language, "PercentageQuizGenerator.Stories.CreateStoryPercentage.009", ("part", $"{part}"), ("unit", $"{unit}"))) + relation;
            combined = $"{Capital(partLabel)} " + (QuizContentCatalog.Text(language, "PercentageQuizGenerator.Stories.CreateStoryPercentage.010", ("unit", $"{unit}"))) + relation;
            facts = [part, percentage]; answer = whole; answerUnit = unit; subject = totalLabel;
            equation = $"{part} × 100 ÷ {percentage} = {whole}"; left = part * 100; right = percentage;
            lead = QuizContentCatalog.Text(language, "PercentageQuizGenerator.Stories.CreateStoryPercentage.011", ("Capital_totalLabel", $"{Capital(totalLabel)}"), ("unit", $"{unit}"));
        }
        return new(type, facts, answer, answerUnit, subject, problem,
            equation, FormatSolution(lead, equation, answerUnit, answer, language), left, ArithmeticOperation.Divide, right)
            { StoryContextId = s.Id, CombinedProblemTemplate = combined };
    }
}
