using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Numerics;
using System.Text.Json;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckStructuralDifficulty()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        int checkedCount = 0;
        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        foreach (CurriculumTier tier in Enum.GetValues<CurriculumTier>())
        foreach (var (kind, subtype) in AllSubtypes().Where(item => item.Kind is
            QuizProblemKind.Proportion or QuizProblemKind.Motion or QuizProblemKind.Average or QuizProblemKind.Percentage))
        for (int sample = 0; sample < 40; sample++)
        {
            AppLanguageManager.CurrentLanguage = language;
            int level = (int)tier;
            var context = new QuizCurriculumContext(tier, false);
            int seed = 17100 + sample;
            var mode = Enum.GetValues<ArithmeticQuizMode>()[sample % 3];
            ArithmeticQuizQuestion GenerateSource(bool algorithm) => kind switch
            {
                QuizProblemKind.Proportion => algorithm
                    ? new ProportionQuizGenerator(new Random(seed)).GenerateAlgorithm(mode, (ProportionQuizType)subtype, language, context)
                    : new ProportionQuizGenerator(new Random(seed)).GenerateContract(mode, (ProportionQuizType)subtype, language, context),
                QuizProblemKind.Motion => algorithm
                    ? new MotionQuizGenerator(new Random(seed)).GenerateAlgorithm(mode, language, (MotionQuizType)subtype, context)
                    : new MotionQuizGenerator(new Random(seed)).GenerateContract(mode, language, (MotionQuizType)subtype, context),
                QuizProblemKind.Average => algorithm
                    ? new AverageQuizGenerator(new Random(seed)).GenerateAlgorithm(mode, (AverageQuizType)subtype, language, context)
                    : new AverageQuizGenerator(new Random(seed)).GenerateContract(mode, (AverageQuizType)subtype, language, context),
                _ => algorithm
                    ? new PercentageQuizGenerator(new Random(seed)).GenerateAlgorithm(mode, (PercentageQuizType)subtype, language, context)
                    : new PercentageQuizGenerator(new Random(seed)).GenerateContract(mode, (PercentageQuizType)subtype, language, context)
            };
            var question = GenerateSource(true);
            Require(JsonSerializer.Serialize(question) == JsonSerializer.Serialize(GenerateSource(false)),
                "Algorithm and contracts must use identical difficulty rules.");
            CheckIntegerContract(kind, subtype, question, $"{kind}/{subtype}/{tier}");
            string story, subject, unit;
            if (question.ProportionProblem is { } proportion)
            {
                (story, subject, unit) = (proportion.ProblemText, proportion.SubjectName, proportion.AnswerUnit);
                if (proportion.IsDirect)
                {
                    Require(level < 4 || proportion.B % proportion.A != 0, "Advanced direct proportion lost its rational unit rate.");
                    Require(level < 4 || proportion.Scenario is not (ProportionScenarioKind.StudentsPlanting or
                        ProportionScenarioKind.ProductionItems or ProportionScenarioKind.Shopping),
                        "Rational rates must not imply fractional trees/items or fractional dong prices.");
                    Require(level > 2 || proportion.C % proportion.A == 0, "Easy proportion should use a whole-number scale factor.");
                }
                if (level <= 2) Require(!proportion.AsksForAdditionalPeople, "Easy inverse proportion must not ask for an extra subtraction.");
                if (level == 5 && !proportion.IsDirect)
                    Require(proportion.AsksForAdditionalPeople || proportion.InverseChangesSecondQuantity,
                        "Advanced inverse proportion should require an additional quantity or solving the changed deadline.");
            }
            else if (question.AverageProblem is { } average)
            {
                (story, subject, unit) = (average.ProblemText, average.SubjectName, average.AnswerUnit);
                BigInteger expected = average.Type switch
                {
                    AverageQuizType.Direct => average.Facts.Skip(1).Sum() / average.Facts[0],
                    AverageQuizType.TotalToAverage => average.Facts[1] / average.Facts[0],
                    AverageQuizType.AverageToTotal => average.Facts[0] * average.Facts[1],
                    AverageQuizType.MissingValue => average.RepresentativeLeft - average.KnownScores!.Sum(),
                    AverageQuizType.IndirectData => (3 * average.Facts[0] + 2 * average.Facts[1] - average.Facts[2]) / 3,
                    _ => (average.Facts[0] * average.Facts[1] + average.Facts[2] * average.Facts[3]) / (average.Facts[0] + average.Facts[2])
                };
                Require(expected == question.CorrectAnswer, "Independent average calculation failed.");
                if (average.Type == AverageQuizType.Direct)
                    Require(average.Facts[0] == level + 1, "The number of quantities to combine must increase with difficulty.");
                if (average.Type == AverageQuizType.MissingValue)
                    Require(average.KnownScores!.Count == level && average.KnownScores.All(score => score is >= 1 and <= 10)
                        && average.CorrectAnswer >= 1 && average.CorrectAnswer <= 10, "Score counts must grow without inventing impossible test scores.");
                if (average.Type == AverageQuizType.TwoGroups)
                    Require(level > 2 ? level < 4 || average.Facts[0] != average.Facts[2] && average.Facts[1] != average.Facts[3]
                        : average.Facts[0] == average.Facts[2], "Weighted group structure did not match the tier.");
            }
            else if (question.MotionProblem is { } motion)
            {
                (story, subject, unit) = (motion.ProblemText, motion.SubjectName, motion.AnswerUnit);
                int conversion = motion.RequiredProblemUnits.Any(value => value is "phút" or "minutes") ? 60 : 1;
                var f = motion.Facts;
                BigInteger expected = motion.QuestionKind switch
                {
                    MotionQuestionKind.BasicDistance => f[0] * f[1] / conversion,
                    MotionQuestionKind.BasicSpeed => f[0] * conversion / f[1],
                    MotionQuestionKind.BasicTime => f[1] * conversion / f[0],
                    MotionQuestionKind.BasicRestDistance => f[0] * (f[1] - f[2]) / conversion,
                    MotionQuestionKind.CatchUpTime => f[0] * conversion / (f[2] - f[1]),
                    MotionQuestionKind.MeetingTime => f[0] * conversion / (f[1] + f[2]),
                    MotionQuestionKind.RiverDownstreamSpeed => f[0] + f[1],
                    MotionQuestionKind.RiverUpstreamSpeed => f[0] - f[1],
                    MotionQuestionKind.RiverBoatSpeed => (f[0] + f[1]) / 2,
                    _ => (f[0] - f[1]) / 2
                };
                Require(expected == motion.CorrectAnswer, $"{motion.QuestionKind}/{tier}/{language}: independent motion calculation failed for [{string.Join(",", f)}].");
                if (level <= 2) Require(conversion == 1, "Easy motion should not need minutes-to-hours conversion.");
                if (level == 1 && motion.Type == MotionQuizType.Basic)
                    Require(motion.QuestionKind == MotionQuestionKind.BasicDistance, "First-tier basic motion should use distance = speed × time.");
                if (level == 5 && motion.Type == MotionQuizType.Basic)
                    Require(motion.QuestionKind == MotionQuestionKind.BasicRestDistance && conversion == 60,
                        "Advanced basic motion should deduct rest and convert units.");
                if (level >= 4 && motion.Type is MotionQuizType.Chasing or MotionQuizType.Meeting)
                    Require(conversion == 60, "Advanced relative motion should use a unit conversion.");
            }
            else
            {
                var percentage = question.PercentageProblem!;
                (story, subject, unit) = (percentage.ProblemText, percentage.SubjectName, percentage.AnswerUnit);
                Require(level < 4 ? percentage.CombinedQuantities is null
                    : percentage.CombinedQuantities?.Count == (level == 4 ? 2 : 3),
                    "Advanced percentages must first combine independent quantities.");
                if (level == 1) Require(percentage.Facts[^1] == 50 || percentage.Type == PercentageQuizType.FindPercentageRatio
                    && percentage.CorrectAnswer == 50, "Easy percentages should start with halves.");
            }
            if (mode == ArithmeticQuizMode.MultipleChoice)
                Require(question.Choices.Count == 4 && question.Choices.Distinct().Count() == 4 &&
                    question.Choices.Count(value => value == question.CorrectAnswer) == 1,
                    "Difficulty changes must preserve unambiguous choices.");

            if (sample < 4)
            {
                string solution = BuildSolutionSentence(question, language);
                string equation = BuildEquation(question) + " " + unit;
                string answer = question.CorrectAnswer + " " + unit;
                if (question.AverageProblem?.Type == AverageQuizType.IndirectData)
                {
                    var parsed = EssayCombinedInputParser.Parse(question.AverageProblem.SolutionText, true, true);
                    (solution, equation, answer) = (parsed.Solution, parsed.Equation, parsed.Answer);
                }
                Require(grader.Validate(question, solution, equation, answer).IsCorrect,
                    $"{kind}/{subtype}/{tier}: authoritative solution failed grading: {equation}");
                if (question.PercentageProblem?.CombinedQuantities is { } groups)
                {
                    string sum = $"({string.Join(" + ", groups.Reverse())})";
                    int second = question.PercentageProblem.Facts[^1];
                    string alternative = question.PercentageProblem.Type switch
                    {
                        PercentageQuizType.FindPercentageRatio => $"100 * {second} / {sum}",
                        PercentageQuizType.FindPercentageValue => $"{second} * {sum} / 100",
                        _ => $"100 * {sum} / {second}"
                    };
                    Require(grader.Validate(question, solution, alternative + " = " + answer, answer).IsCorrect,
                        $"{kind}/{subtype}/{tier}/{language}: reordered percentage calculation {alternative} failed: {grader.Validate(question, solution, alternative + " = " + answer, answer)}");
                }
            }
            checkedCount += 2;
        }
        CheckDifficultySelection();
        Console.WriteLine($"  Checked {checkedCount} bilingual algorithm/contracts across all five structural tiers, independent answers and grading.");
    }

    private static void CheckDifficultySelection()
    {
        var low = new QuizCurriculumContext(CurriculumTier.OneStar, false);
        var high = new QuizCurriculumContext(CurriculumTier.FiveStars, false);
        for (int seed = 0; seed < 100; seed++)
        {
            var averages = new AverageQuizGenerator(new Random(seed));
            Require(averages.GenerateContract(ArithmeticQuizMode.Essay, null, AppLanguage.English, low).AverageProblem!.Type
                is AverageQuizType.Direct or AverageQuizType.TotalToAverage, "Low mixed averages selected an advanced relation.");
            Require(averages.GenerateContract(ArithmeticQuizMode.Essay, null, AppLanguage.English, high).AverageProblem!.Type
                is AverageQuizType.TwoGroups or AverageQuizType.IndirectData or AverageQuizType.MissingValue,
                "High mixed averages selected only a one-step problem.");
            Require(new ProportionQuizGenerator(new Random(seed)).GenerateContract(ArithmeticQuizMode.Essay, null, AppLanguage.English, low)
                .ProportionProblem!.IsDirect, "Low mixed proportion should begin with direct proportionality.");
            Require(new MotionQuizGenerator(new Random(seed)).GenerateContract(ArithmeticQuizMode.Essay, AppLanguage.English, null, low)
                .MotionProblem!.Type == MotionQuizType.Basic, "Low mixed motion should begin with one moving subject.");
            Require(new PercentageQuizGenerator(new Random(seed)).GenerateContract(ArithmeticQuizMode.Essay, null, AppLanguage.English, low)
                .PercentageProblem!.Type == PercentageQuizType.FindPercentageValue, "Low mixed percentages should begin with a percentage value.");
        }
    }
}
