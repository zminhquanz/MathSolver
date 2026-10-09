using MathSolver.Models;
using MathSolver.Services;
using System.Text.Json;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckMotionRealism()
    {
        int count = 0;
        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        foreach (MotionQuizType type in Enum.GetValues<MotionQuizType>())
        for (int seed = 0; seed < 200; seed++)
        {
            MotionQuizContract contract = new MotionQuizGenerator(new Random(seed))
                .GenerateContract(ArithmeticQuizMode.Essay, language, type)
                .MotionProblem!;
            string label = $"{language}/{type}/{seed}";
            string story = contract.ProblemText.ToLowerInvariant();
            bool HasKind(string kind) => NarrativeContextExpansion.Load<JsonElement>("MotionQuizGenerator.MovingSubjects", language, expanded: true)
                .Where(row => row.GetProperty("Kind").GetString() == kind)
                .Any(row => contract.NarrativeActors.Contains(row.GetProperty("Name").GetString()!, StringComparer.OrdinalIgnoreCase));
            if (HasKind("Pedestrian"))
            {
                int[] speeds = contract.QuestionKind switch
                {
                    MotionQuestionKind.BasicDistance or MotionQuestionKind.BasicTime or
                    MotionQuestionKind.BasicRestDistance => [contract.Facts[0]],
                    MotionQuestionKind.BasicSpeed => [(int)contract.CorrectAnswer],
                    MotionQuestionKind.CatchUpTime => [contract.Facts[1], contract.Facts[2]],
                    MotionQuestionKind.MeetingTime => [contract.Facts[1], contract.Facts[2]],
                    _ => []
                };
                Require(speeds.All(speed => speed is >= 1 and <= 2),
                    $"{label}: walking speed is implausible: {string.Join(", ", speeds)} m/s.");
            }

            if (HasKind("Train"))
            {
                Require(type == MotionQuizType.Basic,
                    $"{label}: a train was paired with a road vehicle.");
                int speed = contract.QuestionKind == MotionQuestionKind.BasicSpeed
                    ? (int)contract.CorrectAnswer
                    : contract.Facts[0];
                int minimum = story.Contains("mph", StringComparison.Ordinal) ? 40 : 60;
                Require(speed >= minimum,
                    $"{label}: train speed is implausibly low: {speed}.");
            }

            if (story.Contains("m/s", StringComparison.Ordinal) && HasKind("MotorVehicle"))
            {
                int[] speeds = contract.QuestionKind switch
                {
                    MotionQuestionKind.BasicSpeed => [(int)contract.CorrectAnswer],
                    MotionQuestionKind.CatchUpTime or MotionQuestionKind.MeetingTime =>
                        [contract.Facts[1], contract.Facts[2]],
                    _ => [contract.Facts[0]]
                };
                Require(speeds.All(speed => speed is >= 8 and <= 23) &&
                        contract.RequiredProblemUnits.Contains("m"),
                    $"{label}: vehicle speed/distance is inconsistent with m/s.");
            }

            if (type == MotionQuizType.River)
            {
                Require(story.Contains(language == AppLanguage.Vietnamese
                        ? "ca nô" : "motorboat", StringComparison.Ordinal) ||
                    story.Contains(language == AppLanguage.Vietnamese
                        ? "xuồng máy" : "speedboat", StringComparison.Ordinal),
                    $"{label}: river speed was assigned to an unpowered craft.");
                int currentSpeed = contract.QuestionKind switch
                {
                    MotionQuestionKind.RiverDownstreamSpeed or
                    MotionQuestionKind.RiverUpstreamSpeed => contract.Facts[1],
                    _ => (contract.Facts[0] - contract.Facts[1]) / 2
                };
                int maximum = story.Contains("m/s", StringComparison.Ordinal) ? 2 : 5;
                Require(currentSpeed is >= 1 && currentSpeed <= maximum,
                    $"{label}: river current speed is implausible: {currentSpeed}.");

            }
            Require(contract.RequiredProblemUnits.Count > 0 && !string.IsNullOrWhiteSpace(contract.AnswerUnit),
                $"{label}: motion units are missing.");
            count++;
        }
        Require(count == 1600, "The bilingual motion matrix is incomplete.");
        Console.WriteLine($"  Checked {count} bilingual motion stories, physical speeds and units.");
    }
}
