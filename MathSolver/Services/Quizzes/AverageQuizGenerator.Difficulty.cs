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
        string unit = QuizContentCatalog.Text(language, "AverageQuizGenerator.Difficulty.CreateTieredMissingValue.001");
        string problem = QuizContentCatalog.Text(language, "AverageQuizGenerator.Difficulty.CreateTieredMissingValue.002", ("known_Length", $"{known.Length}"), ("JoinValues_known", $"{JoinValues(known)}"), ("count", $"{count}"), ("target", $"{target}"));
        string equation = $"{target} × {count} − ({string.Join(" + ", known)}) = {missing}";
        string lead = QuizContentCatalog.Text(language, "AverageQuizGenerator.Difficulty.CreateTieredMissingValue.003", ("count", $"{count}"));
        return new(AverageQuizType.MissingValue,
            vi ? [known.Length, .. known, count, count, target] : [.. known, known.Length, count, target, count],
            missing, unit, QuizContentCatalog.Text(language, "AverageQuizGenerator.Difficulty.CreateTieredMissingValue.004"), problem, equation,
            FormatSolution(lead, equation, unit, missing, language), total, ArithmeticOperation.Subtract,
            knownTotal, KnownScores: known);
    }
}
