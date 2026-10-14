using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;
using System.Text.Json;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckMixedTime()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        int cases = 0;
        bool carriedSeconds = false, carriedMinutes = false, borrowed = false, overDay = false, dividedRemainder = false;
        var modes = new HashSet<string>();
        var wording = new HashSet<string>();
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var type in ElementaryQuizGenerator.MixedTimeTypes)
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        for (int seed = 0; seed < 70; seed++)
        {
            var q = new ElementaryQuizGenerator(new Random(seed)).Generate(mode, QuizProblemKind.Time, type, language, tier);
            var c = q.ElementaryProblem!;
            int Given(string role) => int.Parse(c.Reasoning!.Givens.Single(g => g.Role == role).Value, CultureInfo.InvariantCulture);
            int left = Given("left-duration"), right = type is ElementaryQuizType.MixedTimeAdd or ElementaryQuizType.MixedTimeSubtract ? Given("right-duration") : Given("factor");
            int expected = type switch { ElementaryQuizType.MixedTimeAdd => left + right, ElementaryQuizType.MixedTimeSubtract => left - right,
                ElementaryQuizType.MixedTimeMultiply => left * right, _ => left / right };
            Require(c.Type == type && c.Answers.Count == 1 && c.Answers[0].Value == new ReducedFraction(expected, 1), "Mixed time arithmetic disagrees with independent integer arithmetic.");
            Require(expected >= 0 && (type != ElementaryQuizType.MixedTimeDivide || left % right == 0), "Mixed time division was truncated or result is negative.");
            foreach (string input in new[] { c.AnswerText, "Đáp số: " + c.AnswerText, "Answer: " + c.AnswerText,
                c.SolutionText, expected + "s", $"{expected / 3600}h{expected / 60 % 60}m{expected % 60}s",
                "Lời giải tùy chọn\n1 + 2 = " + c.AnswerText })
                Require(grader.ValidateSubmission(q, input).IsCorrect, $"Mixed time rejected {type}/{tier}/{language}: {input}");
            foreach (string bad in new[] { "", "99999999s", (expected + 1) + "s", expected.ToString(), expected + "kg", "-" + c.AnswerText,
                c.AnswerText + "\n1s", c.AnswerText + "\nĐáp số: 1s", "Đáp số: " + c.AnswerText + "\nĐáp số: 1s", c.AnswerText + "wrong", "1h1h" })
                Require(!grader.ValidateSubmission(q, bad).IsCorrect, "Mixed time accepted invalid or conflicting input: " + bad);
            Require(c.ChoiceTexts!.Distinct().Count() == 4 && c.ChoiceTexts!.Count(choice => ElementaryEssayValidator.CheckAnswers(q, choice)) == 1,
                "Mixed time choices are ambiguous.");
            Require(ElementaryEssayValidator.CheckAnswers(q, c.PresentedText) == q.PresentedEquationIsCorrect, "Wrong mixed time true/false assertion.");
            Require(!c.RequiresSolution && !c.RequiresCalculation && EssayAnswerValidator.IsResultOnlyPractice(q), "Mixed time unexpectedly requires working.");
            if (type == ElementaryQuizType.MixedTimeAdd)
            {
                carriedSeconds |= left % 60 + right % 60 >= 60;
                carriedMinutes |= left / 60 % 60 + right / 60 % 60 >= 60;
            }
            if (type == ElementaryQuizType.MixedTimeSubtract) borrowed |= left % 60 < right % 60 || left / 60 % 60 < right / 60 % 60;
            if (type == ElementaryQuizType.MixedTimeDivide) dividedRemainder |= left / 3600 % right != 0;
            overDay |= expected > 86400;
            modes.Add(c.Reasoning!.ScenarioId);
            wording.Add(c.ProblemText);
            cases++;
        }
        Require(carriedSeconds && carriedMinutes && borrowed && overDay && dividedRemainder && modes.Count == 3 && wording.Count > 100,
            "Mixed time coverage missed regrouping, long durations, units or wording.");
        foreach (var (input, expected) in new (string, int)[] { ("2 giờ 30 phút",9000), ("2h30",9000), ("2 hours 30 minutes",9000),
            ("2h30m15s",9015), ("1 phút 30 giây",90), ("1m30",90), ("90 seconds",90), ("150 phút",9000),
            ("0h0m0s",0), ("30 giờ",108000), ("Kết quả: 3h5m",11100), ("2 giờ, 30 phút",9000) })
            Require(MixedTimeAnswerRules.TryRead(input, out int actual) && actual == expected, "Duration keyboard fixture rejected: " + input);
        foreach (string bad in new[] { "2:30", "1 ngày", "2h60", "1.5h", "1h-30m", "1h30kg", "1s1h", "2h30 foo", "999999999999s", "2h\n30m" })
            Require(!MixedTimeAnswerRules.TryRead(bad, out _), "Duration parser accepted malformed input: " + bad);
        string borrow = MixedTimeAnswerRules.WorkedSolution(ElementaryQuizType.MixedTimeSubtract, 3*3600+10*60+5, 1*3600+25*60+40, 1, true, true, AppLanguage.Vietnamese);
        Require(borrow.Contains("2 giờ 69 phút 65 giây") && borrow.Contains("1 giờ 44 phút 25 giây"), "Borrowed duration explanation is incorrect.");
        string carry = MixedTimeAnswerRules.WorkedSolution(ElementaryQuizType.MixedTimeAdd, 2*3600+45*60+50, 1*3600+30*60+20, 1, true, true, AppLanguage.Vietnamese);
        Require(carry.Contains("70 giây = 1 phút 10 giây") && carry.Contains("76 phút = 1 giờ 16 phút") && carry.Contains("4 giờ 16 phút 10 giây"), "Carry explanation is incorrect.");
        Require(borrow.Split('\n').Distinct().Count() == borrow.Split('\n').Length, "Borrowing solution repeats its final calculation.");
        using (var stream = typeof(QuizContentCatalog).Assembly.GetManifestResourceStream("QuizContent.catalogues.json")!)
        using (var json = JsonDocument.Parse(stream))
        {
            var lists = json.RootElement.GetProperty("Lists");
            var pack = new QuizContentPack { Lists = new()
            {
                ["MixedTimePracticeProfiles"] = lists.GetProperty("MixedTimePracticeProfiles").Clone(),
                ["MixedTimeTemplates"] = lists.GetProperty("MixedTimeTemplates").Clone()
            } };
            QuizContentValidation.CheckMixedTimeLists(pack);
            foreach (string invalid in new[] { "[]", "[{\"Star\":0,\"MaximumMajor\":1,\"MaximumFactor\":2,\"Regroup\":true,\"UnitModes\":[\"hours-minutes\"]}]",
                "[{\"Star\":1,\"MaximumMajor\":1,\"MaximumFactor\":2,\"Regroup\":true,\"UnitModes\":[\"hours-days\"]}]" })
            {
                using var document = JsonDocument.Parse(invalid);
                pack.Lists["MixedTimePracticeProfiles"] = document.RootElement.Clone();
                bool rejected = false;
                try { QuizContentValidation.CheckMixedTimeLists(pack); }
                catch (InvalidDataException) { rejected = true; }
                Require(rejected, "Accepted an invalid or incomplete mixed time list profile.");
            }
        }
        var reused = new ElementaryQuizGenerator(new Random(976));
        foreach (var type in ElementaryQuizGenerator.MixedTimeTypes)
        foreach (var tier in new[] { CurriculumTier.OneStar, CurriculumTier.FiveStars, CurriculumTier.TwoStars, CurriculumTier.FourStars, CurriculumTier.OneStar })
        {
            var q = reused.Generate(ArithmeticQuizMode.Essay, QuizProblemKind.Time, type, AppLanguage.Vietnamese, tier);
            string units = q.ElementaryProblem!.Reasoning!.ScenarioId["mixed-time-".Length..];
            Require(q.ElementaryProblem.Reasoning.Tier == tier && QuizContentCatalog.LoadList<MixedTimePracticeProfile>("MixedTimePracticeProfiles").Single(p => p.Star == (int)tier).UnitModes.Contains(units),
                "Reusing the time generator retained a stale difficulty or unit layout.");
        }
        Console.WriteLine($"  Checked {cases} mixed time cases, all four operations, three unit layouts, carry/borrow, exact division, keyboard formats and invalid inputs.");
    }
}
