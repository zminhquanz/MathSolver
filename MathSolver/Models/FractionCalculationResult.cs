using System.Collections.ObjectModel;

namespace MathSolver.Models;

public sealed class FractionCalculationResult
{
    public bool IsSuccess { get; init; }

    // Chỉ phần đáp số, ví dụ: 2 hoặc 23/20
    public string ResultExpression { get; init; } =
        string.Empty;

    // Toàn bộ phép tính, ví dụ: 4/5 + 6/5 = 2
    public string FullExpression { get; init; } =
        string.Empty;

    public string ErrorMessage { get; init; } =
        string.Empty;

    public ObservableCollection<FractionSolutionStep>
        Steps
    { get; } = [];
}