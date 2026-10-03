using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckAverageIndirectSteps()
    {
        var validator = new EssayAnswerValidator(new BasicArithmeticEngine());
        foreach (AppLanguage language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        for (int seed = 0; seed < 60; seed++)
        {
            var algorithm = new AverageQuizGenerator(new Random(seed)).GenerateAlgorithm(
                ArithmeticQuizMode.Essay, AverageQuizType.IndirectData, language);
            AverageQuizContract average = algorithm.AverageProblem!;
            AverageIndirectData data = average.IndirectData!;
            int first = data.FirstQuantity, more = data.Increase, less = data.Decrease;
                int second = first + more, third = second - less, total = first + second + third;
            string unit = average.AnswerUnit;
            string lead = language == AppLanguage.Vietnamese ? $"Số {unit} trung bình là:" : $"The average ({unit}) is:";
            string answer = $"{average.CorrectAnswer} {unit}";
            var contextual = algorithm with
            {
                WordProblem = new MathWordProblem(average.ProblemText, lead, unit, average.SubjectName)
            };
            foreach (var question in new[] { algorithm, contextual })
            {
                string[] validSolutions =
                [
                    $"{average.EquationText} {unit}",
                    $"{first}+{more}={second}\n({first}+{second}+({second}-{less}))/3={answer}",
                    $"{first}+{more}-{less}={third}\n({first}+({first}+{more})+{third})/3={answer}",
                    $"{first}+{more}={second}\n{second}-{less}={third}\n({first}+{second}+{third})/3={answer}",
                    $"{first}+{more}={second}\n{first}+{second}+({second}-{less})={total}\n{total}/3={answer}",
                    $"{first}+{more}={second}\n{second}-{less}={third}\n{first}+{second}+{third}={total}\n{total}/3={answer}",
                    $"{first}+(2*{more}-{less})/3={answer}",
                    $"{first}+{more}={more}+{first}={second}\n{second}-{less}={third}\n({first}+{second}+{third})/3={answer}",
                    $"{first}/3={Third(first)}\n({first}+{more})/3={Third(second)}\n({first}+{more}-{less})/3={Third(third)}\n({Third(first)})+({Third(second)})+({Third(third)})={answer}"
                ];
                foreach (string calculations in validSolutions)
                {
                    var parts = EssayCombinedInputParser.Split($"{lead}\r\n{calculations}", true, true);
                    var result = validator.Validate(question, parts.Solution, parts.Equation, answer);
                    Require(result.IsCorrect && result.Steps.Count == calculations.Split('\n').Length,
                        $"{language}/{seed} rejected valid steps: {calculations}; " +
                        $"{result.SolutionError}/{result.EquationError}/{result.AnswerError}; " +
                        string.Join(", ", result.Steps.Select(step => $"{step.Number}:{step.Error}")));
                    var submission = EssayCombinedInputParser.Parse(
                        $"{lead}\n{calculations}\nAnswer: {answer}", true, true);
                    var unifiedResult = validator.Validate(question, submission.Solution,
                        submission.Equation, submission.Answer);
                    Require(unifiedResult.IsCorrect && unifiedResult.Steps.Count == result.Steps.Count,
                        "The unified submission lost an indirect-average calculation step.");
                }

                string inline = $"{lead} {first}+{more}={second}\n{lead} {second}-{less}={third}\n{lead} ({first}+{second}+{third})/3={answer}";
                var inlineParts = EssayCombinedInputParser.Split(inline, true, true);
                Require(validator.Validate(question, inlineParts.Solution, inlineParts.Equation, answer).IsCorrect,
                    "Inline step headings were not separated from the calculations.");

                // A correct final equation must not hide an earlier arithmetic mistake.
                string wrongFirst = $"{first}+{more}={second + 1}\n{average.EquationText} {unit}";
                var failed = validator.Validate(question, lead, wrongFirst, answer);
                Require(!failed.IsCorrect && failed.Steps[0].Error == EssayAnswerError.WrongEquationResult &&
                        failed.Steps[1].IsCorrect,
                    "An earlier incorrect calculation was ignored or poisoned the independent final step.");
                string feedback = EssayFeedbackFormatter.Format(question, failed, lead, wrongFirst,
                    answer, language, CultureInfo.InvariantCulture);
                Require(feedback.Contains(language == AppLanguage.Vietnamese ? "Bước 1" : "Step 1") &&
                        feedback.Contains(second.ToString()) && feedback.Contains((second + 1).ToString()),
                    "Feedback did not identify the bad step and its computed/written values.");

                string guessed = $"999+55=1054\n{average.EquationText} {unit}";
                Require(validator.Validate(question, lead, guessed, answer).Steps[0].Error ==
                        EssayAnswerError.WrongOperandsOrOperation,
                    "A calculation unrelated to the problem facts was accepted.");
                Require(!validator.Validate(question, lead, $"{average.CorrectAnswer}+0={answer}", answer).IsCorrect,
                    "Copying the answer without deriving the average was accepted.");
                var chainFailure = validator.Validate(question, lead,
                    $"{first}+{more}={second + 1}={second}\n{average.EquationText} {unit}", answer);
                Require(chainFailure.Steps[0].WrittenValue == (second + 1).ToString(),
                    "Equality-chain feedback must identify the actual mismatched value.");
                Require(validator.Validate(question, lead, average.EquationText, answer).EquationError ==
                        EssayAnswerError.WrongEquationUnit,
                    "The final calculation must still include its unit.");
                Require(validator.Validate(question, lead, validSolutions[3], average.CorrectAnswer.ToString()).AnswerError ==
                        EssayAnswerError.WrongAnswerUnit,
                    "The separate final answer must still include its unit.");
                string wrongUnit = $"{first}+{more}={second} invalidunit\n{average.EquationText} {unit}";
                Require(validator.Validate(question, lead, wrongUnit, answer).Steps[0].Error ==
                        EssayAnswerError.WrongEquationUnit,
                    "An explicitly wrong intermediate unit was ignored.");
                Require(validator.Validate(question, "", validSolutions[3], answer).SolutionError ==
                        EssayAnswerError.MissingSolution,
                    "At least one solution sentence is still required.");

                string example = question.WordProblem is null ? average.SolutionText
                    : ElementaryWordProblemSolutionFormatter.Format(question, language, CultureInfo.InvariantCulture);
                var exampleParts = EssayCombinedInputParser.Split(example, true, true);
                Require(validator.Validate(question, exampleParts.Solution, exampleParts.Equation, answer).IsCorrect,
                    "The three-step worked example was rejected by the same grader.");
            }
        }

        static string Third(int value)
        {
            int divisor = value % 3 == 0 ? 3 : 1;
            return divisor == 3 ? (value / 3).ToString() : $"{value}/3";
        }
    }
}
