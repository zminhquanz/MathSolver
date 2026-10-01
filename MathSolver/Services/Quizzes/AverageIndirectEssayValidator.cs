using MathSolver.Models;
using System.Globalization;
using V = MathSolver.Services.EssayCalculationEvaluator.Value;

namespace MathSolver.Services;

/// <summary>Checks the student's chosen steps, tracking how each result derives from the facts.</summary>
internal static class AverageIndirectEssayValidator
{
    private static readonly V Zero = V.Create(0, 1);
    private static readonly V One = V.Create(1, 1);
    private sealed record Proof(V Constant, V First, V Increase, V Decrease)
    {
        internal bool IsConstant => First == Zero && Increase == Zero && Decrease == Zero;
        internal static Proof Scalar(V value) => new(value, Zero, Zero, Zero);
        internal Proof Add(Proof other) => new(Sum(Constant, other.Constant), Sum(First, other.First),
            Sum(Increase, other.Increase), Sum(Decrease, other.Decrease));
        internal Proof Scale(V factor) => new(Product(Constant, factor), Product(First, factor),
            Product(Increase, factor), Product(Decrease, factor));
    }

    internal static (bool IsCorrect, EssayAnswerError Error) Validate(
        ArithmeticQuizQuestion question, string? equations, out IReadOnlyList<EssayStepValidationResult> steps)
    {
        var reports = new List<EssayStepValidationResult>();
        steps = reports;
        AverageQuizContract contract = question.AverageProblem!;
        AverageIndirectData? data = contract.IndirectData ?? (contract.Facts.Count == 3
            ? new(contract.Facts[0], contract.Facts[1], contract.Facts[2]) : null);
        string[] lines = (equations ?? string.Empty).Replace("\r", "", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (data is null || data.PersonCount != 3 || lines.Length == 0)
            return (false, EssayAnswerError.InvalidEquationFormat);

        var known = new Dictionary<V, HashSet<Proof>>();
        Remember(V.Create(data.FirstQuantity, 1), new(Zero, One, Zero, Zero));
        Remember(V.Create(data.Increase, 1), new(Zero, Zero, One, Zero));
        Remember(V.Create(data.Decrease, 1), new(Zero, Zero, Zero, One));
        foreach (int scalar in new[] { 0, 1, 2, 3 })
            Remember(V.Create(scalar, 1), Proof.Scalar(V.Create(scalar, 1)));
        var expectedProof = new Proof(Zero, One, V.Create(2, 3), V.Create(-1, 3));
        V expectedValue = V.Create(contract.CorrectAnswer, 1);

        for (int index = 0; index < lines.Length; index++)
        {
            var report = CheckStep(lines[index], index + 1, index == lines.Length - 1);
            reports.Add(report);
        }
        EssayStepValidationResult? failure = reports.FirstOrDefault(step => !step.IsCorrect);
        return failure is null ? (true, EssayAnswerError.None) : (false, failure.Error);

        void Remember(V value, Proof proof)
        {
            if (!known.TryGetValue(value, out var proofs)) known[value] = proofs = [];
            proofs.Add(proof);
        }

        EssayStepValidationResult CheckStep(string line, int number, bool final)
        {
            var (calculation, unit) = EssayAnswerValidator.SplitEquationResult(question, line, allowFractionResult: true);
            string[] members = calculation.Split('=', StringSplitOptions.TrimEntries);
            V result = default;
            string? computed = null;
            string? written = members.Length > 1 ? members[^1] : null;
            EssayStepValidationResult Fail(EssayAnswerError error) =>
                new(number, line, false, error, computed, written, unit);
            if (members.Length < 2 || members.Any(string.IsNullOrWhiteSpace))
                return Fail(EssayAnswerError.InvalidEquationFormat);
            HashSet<Proof>? provenance = null;
            for (int member = 0; member < members.Length; member++)
            {
                if (!EssayCalculationEvaluator.TryEvaluate(members[member], out V value, out bool operation))
                    return Fail(EssayAnswerError.InvalidEquationFormat);
                if (member == 0)
                {
                    result = value;
                    computed = Format(value);
                }
                else if (value != result)
                {
                    written = members[member];
                    return Fail(EssayAnswerError.WrongEquationResult);
                }

                // A bare right-hand result is the value being proved, not a new
                // input fact. Other equality members must have the same origin.
                if (member == 0 || operation && member < members.Length - 1)
                {
                    if (!EssayCalculationEvaluator.TryGetStructure(members[member], out string tree, fractionLiterals: false))
                        return Fail(EssayAnswerError.InvalidEquationFormat);
                    var candidates = new ProofReader(tree, known).Read();
                    if (provenance is null) provenance = candidates;
                    else provenance.IntersectWith(candidates);
                    if (provenance.Count == 0) return Fail(EssayAnswerError.WrongOperandsOrOperation);
                }
            }
            if (final && (result != expectedValue || provenance?.Contains(expectedProof) != true))
                return Fail(result != expectedValue ? EssayAnswerError.WrongEquationResult : EssayAnswerError.WrongOperandsOrOperation);

            // Correct arithmetic remains available to following steps even when
            // an explicitly written unit is wrong; report that unit separately.
            foreach (Proof proof in provenance!) Remember(result, proof);
            if ((final || unit.Length > 0) && !EssayAnswerValidator.IsExpectedUnitForFeedback(question, unit))
                return Fail(EssayAnswerError.WrongEquationUnit);
            return new(number, line, true, EssayAnswerError.None, computed, written, unit);
        }
    }

    private sealed class ProofReader(string text, Dictionary<V, HashSet<Proof>> known)
    {
        private int _position;
        internal HashSet<Proof> Read()
        {
            HashSet<Proof> result = ReadNode();
            return _position == text.Length ? result : [];
        }
        private HashSet<Proof> ReadNode()
        {
            int opening = text.IndexOf('(', _position);
            if (opening < 0) return [];
            string kind = text[_position..opening];
            _position = opening + 1;
            if (kind == "n")
            {
                int closing = text.IndexOf(')', _position);
                if (closing < 0) return [];
                string token = text[_position..closing];
                _position = closing + 1;
                return EssayCalculationEvaluator.TryEvaluate(token, out V value, out _) && known.TryGetValue(value, out var sources)
                    ? [.. sources] : [];
            }
            int leftStart = _position;
            var left = ReadNode();
            int leftEnd = _position;
            if (kind == "neg")
            {
                if (!Take(')')) return [];
                return left.Select(proof => proof.Scale(V.Create(-1, 1))).ToHashSet();
            }
            if (!Take(',')) return [];
            int rightStart = _position;
            var right = ReadNode();
            int rightEnd = _position;
            if (!Take(')')) return [];
            var results = new HashSet<Proof>();
            // A numeric fraction may name a value proved by an earlier step;
            // its numerator need not itself have been calculated separately.
            if (kind == "/" && TryGetNumber(text[leftStart..leftEnd], out V numerator)
                && TryGetNumber(text[rightStart..rightEnd], out V denominator)
                && !denominator.Numerator.IsZero)
            {
                V fraction = Product(numerator, V.Create(denominator.Denominator, denominator.Numerator));
                if (known.TryGetValue(fraction, out var sources)) results.UnionWith(sources);
            }
            foreach (Proof a in left)
            foreach (Proof b in right)
            {
                Proof? result = kind switch
                {
                    "+" => a.Add(b),
                    "-" => a.Add(b.Scale(V.Create(-1, 1))),
                    "*" when a.IsConstant => b.Scale(a.Constant),
                    "*" when b.IsConstant => a.Scale(b.Constant),
                    "/" when b.IsConstant && !b.Constant.Numerator.IsZero =>
                        a.Scale(V.Create(b.Constant.Denominator, b.Constant.Numerator)),
                    _ => null
                };
                if (result is not null) results.Add(result);
                // Bound ambiguity from repeated equal-valued quantities rather
                // than silently dropping a possible correct interpretation.
                if (results.Count > 256) return [];
            }
            return results;
        }
        private static bool TryGetNumber(string node, out V value)
        {
            value = default;
            return node.StartsWith("n(", StringComparison.Ordinal) && node.EndsWith(')')
                && EssayCalculationEvaluator.TryEvaluate(node[2..^1], out value, out _);
        }
        private bool Take(char expected) => _position < text.Length && text[_position++] == expected;
    }

    private static V Sum(V a, V b) => V.Create(a.Numerator * b.Denominator + b.Numerator * a.Denominator,
        a.Denominator * b.Denominator);
    private static V Product(V a, V b) => V.Create(a.Numerator * b.Numerator, a.Denominator * b.Denominator);
    internal static string Format(V value) => value.Denominator.IsOne
        ? value.Numerator.ToString(CultureInfo.InvariantCulture)
        : $"{value.Numerator.ToString(CultureInfo.InvariantCulture)}/{value.Denominator.ToString(CultureInfo.InvariantCulture)}";
}
