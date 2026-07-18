using System.Collections.ObjectModel;

namespace MathSolver.Models;

public sealed class FractionSolutionStep
{
    public string Title { get; init; } =
        string.Empty;

    public string Description { get; init; } =
        string.Empty;

    public ObservableCollection<string> MathLines { get; } = [];

    public bool IsImportant { get; init; }
}