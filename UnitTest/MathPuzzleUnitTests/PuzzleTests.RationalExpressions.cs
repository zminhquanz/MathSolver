using MathSolver.Models;
using MathSolver.Services;
using System.Numerics;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckRationalExpressions()
    {
        var engine = RationalExpressionEngine.Shared;
        var cases = new (string Input, ReducedFraction Expected)[]
        {
            ("1/2 + 1/3 × 3/4", new(3, 4)),
            ("(1/2 + 1/3) × 3/4", new(5, 8)),
            ("1/2 ÷ 3/4", new(2, 3)),
            ("1/2 : 3/4", new(2, 3)),
            ("1/2 x 3/4", new(3, 8)),
            ("24/4/2", new(3, 1)),
            ("{[3/4 − (1/2 + 1/8)]} ÷ 1/4", new(1, 2)),
            ("-1/2 × (-3/4) + 1/8", new(1, 2)),
            ("1/-2 + 3/4", new(1, 4)),
            ("2/4", new(1, 2)),
            ("1/2 - 1/2", new(0, 1)),
            ("50% + (1/2)²", new(3, 4)),
            ("123456789012345678901234567890/7 × 7", new(BigInteger.Parse("123456789012345678901234567890"), 1))
        };
        foreach (var (input, expected) in cases)
        {
            var result = engine.Evaluate(input);
            Require(result.Result == expected, $"Incorrect exact fraction: {input}");
            Require(EssayCalculationEvaluator.TryEvaluate(input, out var quiz, out _) &&
                quiz.Numerator == result.Result.Numerator && quiz.Denominator == result.Result.Denominator,
                $"Calculator and quiz evaluation diverged: {input}");
        }
        var traced = engine.Evaluate("1/2 + 1/3 × 3/4");
        Require(traced.Steps.SequenceEqual(new[] { "1/3 × 3/4 = 1/4", "1/2 + 1/4 = 3/4" }),
            "Worked steps did not follow precedence or preserve fractional operands.");
        Require(engine.Evaluate("0.125 + 1/8", preferDecimalNotation: true).Result == new ReducedFraction(1, 4),
            "Decimal input must remain exact.");
        foreach (var (input, error) in new[]
        {
            ("", RationalExpressionError.Empty),
            (new string('1', RationalExpressionEngine.MaximumLength + 1), RationalExpressionError.TooLong),
            ("1/(2-2)", RationalExpressionError.DivisionByZero),
            ("1/0", RationalExpressionError.DivisionByZero),
            ("1/2 +", RationalExpressionError.InvalidExpression),
            ("[1/2 + 3/4)", RationalExpressionError.InvalidExpression),
            ("1/2 3/4", RationalExpressionError.InvalidExpression),
            ("1+abc", RationalExpressionError.InvalidExpression)
        })
        {
            try { engine.Evaluate(input); throw new InvalidOperationException($"Accepted invalid expression: {input}"); }
            catch (RationalExpressionException exception)
            { Require(exception.Error == error, $"Incorrect error for '{input}': {exception.Error}"); }
            Require(!engine.TryEvaluate(input, out _, out _), "Invalid input escaped the non-throwing quiz API.");
        }
        string tooLarge = "9";
        for (int i = 0; i < 12; i++) tooLarge = "(" + tooLarge + ")³";
        try { engine.Evaluate(tooLarge); throw new InvalidOperationException("Unbounded intermediate growth was accepted."); }
        catch (RationalExpressionException exception)
        { Require(exception.Error == RationalExpressionError.TooLarge, "Intermediate-size limit reported the wrong error."); }
        Parallel.For(0, 100, i =>
        {
            Require(engine.Evaluate($"{i}/3 + 1/3").Result == new ReducedFraction(i + 1, 3),
                "Shared evaluator leaked state between concurrent calculator/quiz calls.");
        });
    }
}
