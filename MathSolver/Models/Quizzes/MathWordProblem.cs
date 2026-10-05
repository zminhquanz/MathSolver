namespace MathSolver.Models;

public enum WordProblemQuantity { Unspecified, Count, Money, Mass, Distance, Speed, Time, Capacity, Area, Volume }

/// <summary>Presentation text for a word problem whose facts and answer are owned by C#.</summary>
public sealed record MathWordProblem(
    string ProblemText,
    string SolutionLead,
    string AnswerUnit,
    string SubjectName,
    WordProblemQuantity Quantity = WordProblemQuantity.Unspecified,
    string? ConversionStep = null,
    AppliedArithmeticReasoning? ArithmeticReasoning = null,
    QuestionFactTable? FactTable = null);

public sealed record QuestionFactTable(string LabelHeader, string ValueHeader, IReadOnlyList<QuestionFactRow> Rows);
public sealed record QuestionFactRow(string Label, string Value);

/// <summary>Algorithm-owned interpretation of the two facts, independent of AI prose.</summary>
public enum AppliedArithmeticRule { Ordinary, RectanglePerimeter, Remainder, MinimumGroups }
public sealed record AppliedArithmeticReasoning(int Left, int Right, AppliedArithmeticRule Rule)
{
    public System.Numerics.BigInteger Answer => Rule switch {
        AppliedArithmeticRule.RectanglePerimeter => 2L * (Left + Right),
        AppliedArithmeticRule.Remainder => Left % Right,
        AppliedArithmeticRule.MinimumGroups => ((long)Left + Right - 1) / Right,
        _ => throw new InvalidOperationException("Unsupported applied arithmetic rule.") };
    public string Equation => Rule switch {
        AppliedArithmeticRule.RectanglePerimeter => $"({Left} + {Right}) × 2",
        AppliedArithmeticRule.Remainder => $"{Left} − {Left / Right} × {Right}",
        AppliedArithmeticRule.MinimumGroups => $"({Left} − {Left % Right}) ÷ {Right} + 1",
        _ => throw new InvalidOperationException("Unsupported applied arithmetic rule.") };
    public bool Matches(IntegerArithmeticExpression expression) => Left > 0 && Right > 0
        && expression.LeftOperand == Left && expression.RightOperand == Right
        && (Rule == AppliedArithmeticRule.RectanglePerimeter && expression.Operation == ArithmeticOperation.Add
            || Rule is AppliedArithmeticRule.Remainder or AppliedArithmeticRule.MinimumGroups
                && expression.Operation == ArithmeticOperation.Divide && Left > Right && Left % Right != 0);
}
