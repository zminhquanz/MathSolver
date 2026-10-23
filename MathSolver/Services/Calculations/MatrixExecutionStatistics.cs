namespace MathSolver.Services.Core;

public enum MatrixMultiplyKernel { Auto, SingleOutput, PairedOutputs }
public enum MatrixCalculationStage { Preparing, CheckingInputs, Packing, Multiplying, Encoding, Writing, Finalizing, Completed }
public readonly record struct MatrixCalculationProgress(double Fraction, MatrixCalculationStage Stage);

/// <summary>Worker durations; they overlap and must not be added to obtain wall time.</summary>
public readonly record struct MatrixStorageStatistics(TimeSpan EncodingTime, TimeSpan LockWaitTime, TimeSpan WriteTime, long BlocksWritten);

/// <summary>Observed work only, with packing and storage distinguished from arithmetic.</summary>
public readonly record struct MatrixExecutionStatistics(TimeSpan PackingTime, TimeSpan KernelTime,
    TimeSpan ResultConversionTime, long PackedAElements, long PackedBElements, long PairVectorBatches,
    MatrixStorageStatistics Storage);
