using System.Collections.ObjectModel;

namespace MathSolver.Models;

public sealed class FractionCalculationResult
{
    public bool IsSuccess { get; init; }

    public string ResultText { get; init; } = string.Empty;

    public string ErrorMessage { get; init; } = string.Empty;

    public ObservableCollection<FractionSolutionStep> Steps { get; } = [];
}
