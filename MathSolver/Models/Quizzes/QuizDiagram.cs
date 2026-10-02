namespace MathSolver.Models;

/// <summary>Presentation data, separate from grading. Hidden results never enter an ungraded diagram.</summary>
public sealed record QuizDiagramSegment(string Text, float Parts = 1, bool Highlight = false);

public sealed record QuizDiagramRow(string Label, IReadOnlyList<QuizDiagramSegment> Segments,
    int Direction = 0, int? FractionNumerator = null, int? FractionDenominator = null);

public sealed record QuizDiagram(string Kind, string Caption, IReadOnlyList<QuizDiagramRow> Rows,
    string? GeometryShape = null, IReadOnlyDictionary<string, string>? DimensionLabels = null,
    string? Explanation = null);
