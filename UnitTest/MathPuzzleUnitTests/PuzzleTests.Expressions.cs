using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckExpressions()
    {
        var engine = new BasicArithmeticEngine();
        var validator = new EssayAnswerValidator(engine);
        var generator = new ExpressionQuizGenerator(new Random(71031));
        int count = 0;
        var operations = new HashSet<char>();
        var bracketTypes = new HashSet<char>();
        foreach (CurriculumTier tier in Enum.GetValues<CurriculumTier>())
        foreach (ExpressionQuizType type in Enum.GetValues<ExpressionQuizType>())
        foreach (ArithmeticQuizMode mode in Enum.GetValues<ArithmeticQuizMode>())
        for (int index = 0; index < 60; index++)
        {
            ArithmeticQuizQuestion question = generator.Generate(mode, type, tier);
            ExpressionQuizContract contract = question.ExpressionProblem!;
            bool fractions = type is ExpressionQuizType.Fraction or ExpressionQuizType.FractionWithBrackets;
            bool brackets = type is ExpressionQuizType.IntegerWithBrackets or ExpressionQuizType.FractionWithBrackets;
            string label = $"{tier}/{type}/{mode}/{index}";
            string text = contract.ExpressionText;
            Require(Regex.Matches(text, @"\d+(?:/\d+)?").Count == (int)tier + 2 &&
                    contract.OperandCount == (int)tier + 2,
                $"{label}: wrong operand count: {text}");
            Require(text.Count(character => character is '(' or '[' or '{') == (brackets ? (int)tier : 0) &&
                    contract.BracketPairCount == (brackets ? (int)tier : 0),
                $"{label}: wrong bracket count: {text}");
            Require(!EssayAnswerValidator.RequiresSolution(question) &&
                    EssayAnswerValidator.GetExpectedUnit(question).Length == 0 &&
                    question.WordProblem is null && question.UsesFractionFormatting == fractions,
                $"{label}: numeric questions acquired word-problem requirements.");
            if (fractions)
                Require(Regex.Matches(text, @"\d+/\d+").Count == contract.OperandCount,
                    $"{label}: each operand should be a fraction.");
            else
                Require(contract.CorrectAnswer.Denominator.IsOne, $"{label}: fractional integer answer.");

            // Independently verify with the solve tab's postfix engine. Its
            // ordinary slash is division, so wrap each fraction literal first.
            string checkText = Regex.Replace(text, @"\d+/\d+", match => $"({match.Value})");
            IntegerExpressionResult checkedResult = engine.EvaluateIntegerExpression(checkText);
            Require(new ReducedFraction(checkedResult.ResultNumerator, checkedResult.ResultDenominator) ==
                    contract.CorrectAnswer && question.ExactAnswer == contract.CorrectAnswer,
                $"{label}: independent arithmetic disagrees: {text}");
            if (brackets && tier != CurriculumTier.OneStar)
            {
                string ungrouped = string.Concat(text.Where(character => character is not
                    ('(' or ')' or '[' or ']' or '{' or '}')));
                Require(!EssayCalculationEvaluator.TryEvaluate(ungrouped, out var plain, out _) ||
                        new ReducedFraction(plain.Numerator, plain.Denominator) != contract.CorrectAnswer,
                    $"{label}: all brackets are decorative.");
            }
            foreach (char character in text)
            {
                if (character is '+' or '−' or '×' or '÷') operations.Add(character);
                if (character is '(' or '[' or '{') bracketTypes.Add(character);
            }

            if (mode == ArithmeticQuizMode.TrueFalse)
                Require(contract.PresentedAnswer.HasValue &&
                        question.PresentedEquationIsCorrect == (contract.PresentedAnswer == contract.CorrectAnswer),
                    $"{label}: inconsistent true/false answer.");
            else if (mode == ArithmeticQuizMode.MultipleChoice)
                Require(contract.Choices.Count == 4 && contract.Choices.Distinct().Count() == 4 &&
                        contract.Choices.Count(choice => choice == contract.CorrectAnswer) == 1 &&
                        (fractions || question.Choices.SequenceEqual(contract.Choices.Select(choice => choice.Numerator))),
                    $"{label}: invalid choices.");
            else
            {
                string equivalent = $"{contract.CorrectAnswer.Numerator * 2}/{contract.CorrectAnswer.Denominator * 2}";
                string answer = fractions ? equivalent : contract.CorrectAnswer.ToString();
                string equation = text + " = " + answer;
                var parts = EssayCombinedInputParser.Split(equation, requiresSolution: false);
                Require(validator.Validate(question, parts.Solution, parts.Equation, answer).IsCorrect,
                    $"{label}: a correct numeric submission was rejected: {equation}");
                string aliases = text.Replace('×', '*').Replace('−', '-').Replace('÷', ':')
                    .Replace('[', '(').Replace(']', ')').Replace('{', '(').Replace('}', ')');
                Require(validator.Validate(question, "", aliases + " = " + answer, answer).IsCorrect,
                    $"{label}: equivalent bracket/operator notation was rejected.");
                Require(validator.Validate(question, "", text + " =\n" + contract.CorrectAnswer + " = " + answer,
                        answer).IsCorrect,
                    $"{label}: an exact multiline equality chain was rejected.");
                var wrong = new ReducedFraction(contract.CorrectAnswer.Numerator + contract.CorrectAnswer.Denominator,
                    contract.CorrectAnswer.Denominator);
                Require(validator.Validate(question, "", text + " = " + wrong, wrong.ToString()) is
                        { EquationIsCorrect: false, AnswerIsCorrect: false, SolutionIsCorrect: true },
                    $"{label}: incorrect result was accepted.");
                Require(validator.Validate(question, "", "0 + " + answer + " = " + answer, answer)
                        .EquationError == EssayAnswerError.WrongOperandsOrOperation,
                    $"{label}: unrelated expression with a coincidentally correct answer was accepted.");
                foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
                {
                    var validation = validator.Validate(question, "", text + " = " + wrong, wrong.ToString());
                    string feedback = EssayFeedbackFormatter.Format(question, validation, "",
                        text + " = " + wrong, wrong.ToString(), language, CultureInfo.InvariantCulture);
                    Require(feedback.Contains(contract.CorrectAnswer.ToString(), StringComparison.Ordinal),
                        $"{label}/{language}: feedback lost the correct rational result.");
                }
            }
            count++;
        }
        Require(operations.Count == 4 && bracketTypes.Count == 3,
            "The expression generator did not exercise all operations/bracket types.");

        CheckOneStarExpressionVariety();

        foreach (string invalid in new[] { "[1 + 2)", "{1 + [2 × 3})", "1 ÷ (2 − 2)", "((1 + 2)" })
            Require(!EssayCalculationEvaluator.TryEvaluate(invalid, out _, out _),
                $"Invalid expression was accepted: {invalid}");
        Require(EssayCalculationEvaluator.TryEvaluate("{[3/4 − (1/2 + 1/8)]} ÷ 1/4", out var nested, out _) &&
                nested == EssayCalculationEvaluator.Value.Create(1, 2),
            "Nested bracket/fraction evaluation is wrong.");

        var catalog = new QuizProblemTypeCatalog(new Random(904));
        Require(catalog.Options.Any(option => option.FixedRequest?.Kind == QuizProblemKind.Expression),
            "Expressions must appear in the problem catalog.");
        int expressionIndex = catalog.Options.ToList().FindIndex(option => option.FixedRequest?.Kind == QuizProblemKind.Expression);
        foreach (ExpressionQuizType type in Enum.GetValues<ExpressionQuizType>())
        {
            QuizProblemRequest selected = catalog.Resolve(expressionIndex, null, null, ProportionQuizType.Direct,
                null, null, null, null, null, CurriculumTier.FiveStars, true, type);
            Require(selected.Kind == QuizProblemKind.Expression && selected.ExpressionType == type,
                "The catalog lost an explicitly selected expression subtype.");
        }
        Require(catalog.Resolve(expressionIndex, null, null, ProportionQuizType.Direct, null, null,
                null, null, null, CurriculumTier.FiveStars) is
                { Kind: QuizProblemKind.Expression, ExpressionType: null },
            "Expression Mixed was not passed to word-problem generation.");
        foreach (CurriculumTier tier in Enum.GetValues<CurriculumTier>())
        {
            bool found = false;
            for (int index = 0; index < 200; index++)
            {
                QuizProblemRequest algorithm = QuizCurriculumLayer.ResolveMixedRequest(tier, new Random(5000 + index), true);
                found |= algorithm.Kind == QuizProblemKind.Expression;
                if (tier < CurriculumTier.FourStars && algorithm.Kind == QuizProblemKind.Expression)
                    Require(algorithm.ExpressionType is ExpressionQuizType.Integer or ExpressionQuizType.IntegerWithBrackets,
                        "Mixed fractions should keep their existing curriculum gate.");
                Require(QuizCurriculumLayer.ResolveMixedRequest(tier, new Random(5000 + index)) == algorithm,
                    "Algorithm and word-problem mixed pools must match.");
                Require(QuizCurriculumLayer.ResolveMixedRequest(tier, new Random(5000 + index), false).Kind !=
                        QuizProblemKind.Expression, "Explicit expression exclusion was ignored.");
            }
            Require(found, $"{tier}: Algorithm mixed mode never selected expressions.");
        }
        Console.WriteLine($"  Checked {count} numeric expressions, independent arithmetic, grading and source isolation.");
    }

    private static void CheckOneStarExpressionVariety()
    {
        foreach (ExpressionQuizType type in Enum.GetValues<ExpressionQuizType>())
        foreach (ArithmeticQuizMode mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var generator = new ExpressionQuizGenerator(new Random(71032 + (int)type * 10 + (int)mode));
            var operations = new HashSet<char>();
            var outerOperations = new HashSet<char>();
            bool foundGroupedAddition = false;
            bool brackets = type is ExpressionQuizType.IntegerWithBrackets or ExpressionQuizType.FractionWithBrackets;
            for (int sample = 0; sample < 256; sample++)
            {
                ExpressionQuizContract contract = generator.Generate(mode, type, CurriculumTier.OneStar).ExpressionProblem!;
                string text = contract.ExpressionText;
                Require(Regex.Matches(text, @"\d+(?:/\d+)?").Count == 3 &&
                        text.Count(character => character is '(' or '[' or '{') == (brackets ? 1 : 0),
                    $"OneStar/{type}/{mode}: variety changed operand or bracket count: {text}");
                int depth = 0;
                foreach (char character in text)
                {
                    if (character is '(' or '[' or '{') depth++;
                    else if (character is ')' or ']' or '}') depth--;
                    else if (character is '+' or '−' or '×' or '÷')
                    {
                        operations.Add(character);
                        if (depth == 0) outerOperations.Add(character);
                    }
                }
                foundGroupedAddition |= brackets && text.Contains('+') &&
                    text.All(character => character is not ('−' or '×' or '÷'));
            }
            Require(operations.SetEquals(new[] { '+', '−', '×', '÷' }),
                $"OneStar/{type}/{mode}: missing one of the four operations.");
            if (brackets)
                Require(outerOperations.SetEquals(new[] { '+', '−', '×', '÷' }) && foundGroupedAddition,
                    $"OneStar/{type}/{mode}: grouped expressions still force the outer operation or exclude sums.");
        }
    }
}
