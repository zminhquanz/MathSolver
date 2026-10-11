using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckResultOnlyPractice()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;
        void Check(ArithmeticQuizQuestion question, string answer)
        {
            Require(EssayAnswerValidator.IsResultOnlyPractice(question), "Numeric practice was treated as a story.");
            foreach (string input in new[] { answer, $"x = {answer}", $"1 + 0 = {answer}",
                         $"8 : 2 = {answer}", $"8 ÷ 2 = {answer}", $"8 / 2 = {answer}",
                         $"Đáp số: {answer}", $"Answer: {answer}", $"Ta có:\nx = {answer}",
                         $"1 + 0 = 99\n{answer}", $"1 + 0 = 99\nAnswer: {answer}" })
            {
                var result = grader.ValidateSubmission(question, input, out var parts);
                Require(result.IsCorrect && result.Steps.Count == 0 && parts.Answer == answer,
                    $"Result-only practice rejected {input}: {result}");
                count++;
            }
            // An explicit result wins over optional work; never calculate a
            // missing result for the learner or conceal a wrong final answer.
            string wrong = question.ExactAnswer == new ReducedFraction(0, 1) ? "1" : "0";
            foreach (string input in new[] { "", " ", "8 : 2", "x =", "không biết", wrong,
                         $"x = {wrong}", $"1 + 0 = {answer}\nAnswer: {wrong}",
                         $"Answer: {wrong}\nAnswer: {answer}" })
            {
                Require(!grader.ValidateSubmission(question, input).IsCorrect,
                    $"Missing, malformed or wrong result was accepted: {input}");
                count++;
            }
        }

        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        {
            AppLanguageManager.CurrentLanguage = language;
            var context = new QuizCurriculumContext(tier, false);
            foreach (var operation in Enum.GetValues<ArithmeticOperation>())
            {
                var integer = new ArithmeticQuizGenerator(new BasicArithmeticEngine(), new Random(602)).Generate(
                    ArithmeticQuizMode.Essay, operation, context);
                Check(integer, integer.ExactAnswer.ToString());
                var findX = new FindXQuizGenerator(new FindXEngine(), new Random(607)).Generate(
                    ArithmeticQuizMode.Essay, operation, context);
                Check(findX, findX.ExactAnswer.ToString());
                var fraction = new FractionQuizGenerator(new FractionCalculationEngine(), new Random(613)).Generate(
                    ArithmeticQuizMode.Essay, (FractionOperation)operation, context);
                Check(fraction, fraction.ExactAnswer.ToString());
                // Identical numeric families become worked essays when their
                // quantities are presented in a word problem.
                foreach (var numeric in new[] { integer, findX, fraction })
                {
                    var story = numeric with { WordProblem = new("", "Số quả cam là:", "quả cam", "Lan") };
                    Require(!EssayAnswerValidator.IsResultOnlyPractice(story)
                        && !grader.ValidateSubmission(story, $"Đáp số: {numeric.ExactAnswer} quả cam").IsCorrect,
                        "Word-problem arithmetic lost its solution/calculation requirements.");
                }
            }
            foreach (var type in Enum.GetValues<ExpressionQuizType>())
            {
                var expression = new ExpressionQuizGenerator(new Random(617)).Generate(ArithmeticQuizMode.Essay, type, tier);
                Check(expression, expression.ExactAnswer.ToString());
            }
            foreach (var type in new[] { ElementaryQuizType.DecimalAdd, ElementaryQuizType.DecimalSubtract,
                         ElementaryQuizType.DecimalMultiply, ElementaryQuizType.DecimalDivide })
            {
                var generator = new ElementaryQuizGenerator(new Random(619));
                var numeric = generator.GenerateDecimalArithmetic(ArithmeticQuizMode.Essay, type, language, tier, false);
                Check(numeric, numeric.ElementaryProblem!.AnswerText);
                Require(grader.ValidateSubmission(numeric, numeric.ElementaryProblem.AnswerText.Replace('.', ',')).IsCorrect,
                    "Comma decimal notation was rejected in direct practice.");
                var story = generator.GenerateDecimalArithmetic(ArithmeticQuizMode.Essay, type, language, tier, true);
                Require(!EssayAnswerValidator.IsResultOnlyPractice(story)
                    && !grader.ValidateSubmission(story, "Answer: " + story.ElementaryProblem!.AnswerText).IsCorrect
                    && grader.ValidateSubmission(story, story.ElementaryProblem.SolutionText).IsCorrect,
                    "Decimal stories and numeric practice share the wrong grading policy.");
            }
            var rounding = new ElementaryQuizGenerator(new Random(623)).Generate(ArithmeticQuizMode.Essay,
                QuizProblemKind.Decimal, ElementaryQuizType.DecimalRound, language, tier);
            Check(rounding, rounding.ElementaryProblem!.AnswerText);
            var letters = new ElementaryQuizGenerator(new Random(627)).Generate(ArithmeticQuizMode.Essay,
                QuizProblemKind.NumberSense, ElementaryQuizType.LetterExpression, language, tier);
            var letterContract = letters.ElementaryProblem!;
            string letterAnswer = letterContract.AnswerText;
            Check(letters, letterAnswer);
            foreach (string input in new[] { letterContract.Answers[0].Expression + " = " + letterAnswer,
                (tier <= CurriculumTier.TwoStars ? "a + b" : "(a + b) × c") + " = "
                    + letterContract.Answers[0].Expression + " = " + letterAnswer,
                letterContract.SolutionText })
                Require(grader.ValidateSubmission(letters, input).IsCorrect,
                    "A complete letter-expression calculation was rejected: " + input);
            foreach (string input in new[] { "a + b", letterContract.Answers[0].Expression,
                "a + b =", "Đáp số:", letterAnswer + " m" })
                Require(!grader.ValidateSubmission(letters, input).IsCorrect,
                    "Missing result or an unrelated unit passed letter-expression grading: " + input);
            // Independent worked validation remains available for generated examples.
            var workedLetters = EssayCombinedInputParser.Parse(letterContract.SolutionText, false, true);
            Require(grader.Validate(letters, workedLetters.Solution, workedLetters.Equation, workedLetters.Answer).IsCorrect,
                "Letter-expression examples lost independent calculation validation.");
            var clock = new ElementaryQuizGenerator(new Random(631)).Generate(ArithmeticQuizMode.Essay,
                QuizProblemKind.Time, ElementaryQuizType.ReadClock, language, tier);
            Require(!EssayAnswerValidator.IsResultOnlyPractice(clock)
                && grader.ValidateSubmission(clock, clock.ElementaryProblem!.AnswerText).IsCorrect
                    == !clock.ElementaryProblem.RequiresSolution
                && grader.ValidateSubmission(clock, clock.ElementaryProblem.SolutionText).IsCorrect,
                "Clock readings accept direct answers; time-interval problems retain worked grading.");
        }

        // All other essay contracts keep the existing calculation and answer policy.
        foreach (var (kind, subtype) in AllSubtypes())
        {
            var question = Generate(kind, subtype, ArithmeticQuizMode.Essay, AppLanguage.Vietnamese, 641);
            if (EssayAnswerValidator.IsResultOnlyPractice(question)) continue;
            Require(!grader.ValidateSubmission(question, "Answer: " + question.ExactAnswer).IsCorrect,
                $"Contextual {kind}/{subtype} unexpectedly accepts only a result.");
        }
        Console.WriteLine($"  Checked {count} numeric submissions in both languages and all five stars; word problems retain worked grading.");
    }
}
