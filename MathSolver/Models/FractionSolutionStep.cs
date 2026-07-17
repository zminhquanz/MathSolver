namespace MathSolver.Models;

public sealed class FractionSolutionStep
{
    public required string Title { get; init; }

    public required string Content { get; init; }

    public bool IsImportant { get; init; }
}
