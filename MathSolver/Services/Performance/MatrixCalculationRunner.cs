using MathSolver.Services.Core;

namespace MathSolver.Services;

/// <summary>Applies the shared hardware preference at the application boundary, leaving the matrix engine reusable.</summary>
public static class MatrixCalculationRunner
{
    public static MatrixCalculationAttempt TryCalculate(IntegerMatrix a, IntegerMatrix b, MatrixOperation operation,
        MatrixBackend backend = MatrixBackend.Auto, CancellationToken cancellationToken = default,
        IProgress<double>? progress = null, MatrixExecutionOptions? executionOptions = null,
        IProgress<MatrixCalculationProgress>? calculationProgress = null) =>
        MatrixEngine.TryCalculate(a, b, operation, backend, cancellationToken, progress,
            maximumDegreeOfParallelism: CalculationThreadingManager.MaxDegreeOfParallelism,
            executionOptions: executionOptions, calculationProgress: calculationProgress);
}
