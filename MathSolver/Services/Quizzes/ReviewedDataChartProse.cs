using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

/// <summary>Same reviewed phrases as AI sampling, checked against rebuilt C# chart roles.</summary>
internal static class ReviewedDataChartProse
{
    internal static bool Matches(ElementaryQuizContract candidate)
    {
        var chart = candidate.DataChart!;
        using var capture = new QuizNarrativeCapture(ReviewedNarrativePhrasings.For(chart.Profile.Language,
            ReviewedNarrativePhrasings.DataListName));
        var expected = ElementaryQuizGenerator.RebuildDataChart(chart);
        string template = capture.Finish(expected.ProblemText);
        string[] parts = Regex.Split(candidate.ProblemText.Trim(), @"(?<=[.!?])\s+");
        string[] source = Regex.Split(template.Trim(), @"(?<=[.!?])\s+");
        if (parts.Length != source.Length || candidate.Reasoning!.Steps.Count != expected.Reasoning!.Steps.Count) return false;
        string Render(string text) => Regex.Replace(text, @"\{(f\d+)\}", m => capture.Slots[m.Groups[1].Value].Value);
        bool Match(string text, string original) => capture.Phrasings(original).Any(choice =>
            SemanticProseRules.MatchesRendered(text, [Render(choice)], chart.Profile.Language));
        for (int i = 0; i < parts.Length; i++) if (!Match(parts[i], source[i])) return false;
        return candidate.Reasoning.Steps.Select((step, i) => Match(step.Label.TrimEnd(':', ' '),
            capture.Project(expected.Reasoning.Steps[i].Label).TrimEnd(':', ' '))).All(matched => matched);
    }
}
