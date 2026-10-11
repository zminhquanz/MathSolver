using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckDirectClockInput()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        for (int seed = 0; seed < 20; seed++)
        {
            var question = new ElementaryQuizGenerator(new Random(seed)).Generate(ArithmeticQuizMode.Essay,
                QuizProblemKind.Time, ElementaryQuizType.ReadClock, language, tier);
            Check(question);
        }

        // Exact screenshot regression, including a zero minute value.
        var sample = new ElementaryQuizGenerator(new Random(17)).Generate(ArithmeticQuizMode.Essay,
            QuizProblemKind.Time, ElementaryQuizType.ReadClock, AppLanguage.Vietnamese, CurriculumTier.OneStar);
        Check(sample with { ElementaryProblem = sample.ElementaryProblem! with
        {
            Answers = [new ElementaryAnswer("Giờ", new(3, 1), "", "3"), new("Phút", new(0, 1), "", "0")]
        } });

        Require(EssayCombinedInputParser.Parse("Giờ: 3, phút: 0", false).Answer.Length == 0,
            "Clock-specific inference must not affect other question types.");
        Console.WriteLine($"  Checked {count} clock submissions across both languages and five stars; incomplete, conflicting and wrong readings rejected, interval work retained.");

        void Check(ArithmeticQuizQuestion question)
        {
            var contract = question.ElementaryProblem!;
            string hour = contract.Answers[0].Value.ToString(), minute = contract.Answers[1].Value.ToString();
            var worked = EssayCombinedInputParser.Parse(contract.SolutionText, contract.RequiresSolution, true,
                requireAnswerLabel: true);
            string work = contract.RequiresSolution ? worked.Solution + "\n" + worked.Equation + "\n" : "";
            string[] answers = [
                $"{hour} giờ {minute} phút", $"giờ : {hour}, phút : {minute}",
                $"PHÚT: {minute}; GIỜ: {hour}", $"Giờ: {hour}\nPhút: {minute}",
                $"{hour} gio {minute} phut", $"{hour}:{minute.PadLeft(2, '0')}",
                $"{hour} hours {minute} minutes", $"Hour: {hour}, minute: {minute}",
                $"Minute: {minute}\nHour: {hour}"
            ];
            foreach (string answer in answers)
            foreach (string prefix in new[] { "", "Đáp số: ", "Answer: " })
            {
                string input = work + prefix + answer;
                var result = grader.ValidateSubmission(question, input);
                Require(result.IsCorrect, "Clock input rejected: " + input + "\n" + string.Join(" | ", result.Details));
                count++;
            }
            if (contract.RequiresSolution)
                Require(!grader.ValidateSubmission(question, answers[0]).IsCorrect,
                    "An interval problem lost its required worked solution.");
            else
                Require(grader.ValidateSubmission(question, contract.AnswerText).IsCorrect,
                    "The generated hour/minute answer should be accepted directly.");

            string wrongMinute = (contract.Answers[1].Value.Numerator + 1).ToString();
            foreach (string answer in new[] { "", $"{hour} giờ", $"Giờ: {hour}",
                $"{hour} giờ {wrongMinute} phút", $"Giờ: {hour}, giờ: {minute}",
                $"Giờ: {hour}, phút: {minute}, giây: 0", $"{hour} giờ {minute} phút\n{hour} giờ {wrongMinute} phút",
                $"Đáp số: {hour} giờ {minute} phút\nGiờ: {hour}, phút: {wrongMinute}",
                $"Giờ: {hour}\nPhút: {minute}\nPhút: {wrongMinute}" })
                Require(!grader.ValidateSubmission(question, work + answer).IsCorrect,
                    "An incomplete, wrong or conflicting clock answer passed: " + answer);
        }
    }
}
