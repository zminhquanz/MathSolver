using MathSolver.Models;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Services;

/// <summary>Quiz compatibility facade over the calculator's shared exact-rational evaluator.</summary>
internal static class EssayCalculationEvaluator
{
    internal static string Format(Value value) => value.Denominator.IsOne
        ? value.Numerator.ToString(CultureInfo.InvariantCulture)
        : $"{value.Numerator.ToString(CultureInfo.InvariantCulture)}/{value.Denominator.ToString(CultureInfo.InvariantCulture)}";

    internal readonly record struct Value(BigInteger Numerator, BigInteger Denominator)
    {
        internal static Value Create(BigInteger numerator, BigInteger denominator)
        {
            var fraction = new ReducedFraction(numerator, denominator);
            return new(fraction.Numerator, fraction.Denominator);
        }
    }

    internal static bool TryEvaluate(string text, out Value value, out bool hasOperation, bool preferDecimalNotation = false)
    {
        value = default;
        if (!RationalExpressionEngine.Shared.TryEvaluate(text, out var rational, out hasOperation, preferDecimalNotation))
            return false;
        value = new(rational.Numerator, rational.Denominator);
        return true;
    }

    internal static bool TryGetStructure(string text, out string structure, bool fractionLiterals, bool preferDecimalNotation = false)
        => RationalExpressionEngine.Shared.TryGetStructure(text, out structure, fractionLiterals, preferDecimalNotation);
}
