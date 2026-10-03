using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class AverageQuizGenerator
{
    private AverageQuizContract CreateTieredMissingValue(AppLanguage language, int level)
    {
        int count = level + 1;
        int target = 8;
        int[] scores = Enumerable.Repeat(target, count).ToArray();
        for (int attempt = 0; attempt < 128; attempt++)
        {
            target = _random.Next(6, 10);
            scores = CreateValuesWithAverage(count, target, Math.Min(level, 4), 2);
            if (scores.All(score => score is >= 1 and <= 10)) break;
            scores = Enumerable.Repeat(target, count).ToArray();
        }
        int[] known = scores[..^1];
        int missing = scores[^1], total = target * count, knownTotal = known.Sum();
        bool vi = language == AppLanguage.Vietnamese;
        string unit = vi ? "điểm" : "points";
        string problem = vi
            ? $"An có điểm của {known.Length} bài đầu lần lượt là {JoinValues(known)}. Bài thứ {count} An cần bao nhiêu điểm để điểm trung bình của {count} bài là {target}?"
            : $"Alex scores {JoinValues(known)} on the first {known.Length} tests. What score is needed on test {count} for an average of {target} across {count} tests?";
        string equation = $"{target} × {count} − ({string.Join(" + ", known)}) = {missing}";
        string lead = vi ? $"Điểm bài thứ {count} cần có là:" : $"The score needed on test {count} is:";
        return new(AverageQuizType.MissingValue,
            vi ? [known.Length, .. known, count, count, target] : [.. known, known.Length, count, target, count],
            missing, unit, vi ? "điểm bài còn thiếu" : "missing test score", problem, equation,
            FormatSolution(lead, equation, unit, missing, language), total, ArithmeticOperation.Subtract,
            knownTotal, KnownScores: known);
    }
}
