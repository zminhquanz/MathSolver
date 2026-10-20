using System.Numerics;

namespace MathSolver.Services.Core;

public enum MatrixOperation { Add, Subtract, Multiply }
public enum MatrixBackend { Auto, Scalar, Sse2, Avx2, Avx512 }

public sealed class IntegerMatrix
{
    public IntegerMatrix(int rows, int columns, long[]? data = null)
    {
        if (rows is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(rows));
        if (columns is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(columns));
        if (data is not null && data.Length != rows * columns)
            throw new ArgumentException("The data length must equal rows times columns.", nameof(data));
        Rows = rows;
        Columns = columns;
        Data = data ?? new long[rows * columns];
    }
    public int Rows { get; }
    public int Columns { get; }
    /// <summary>Row-major mutable input. Callers must not edit it during Calculate.</summary>
    public long[] Data { get; }
}

public sealed class MatrixCalculation
{
    internal MatrixCalculation(int rows, int columns, MatrixOperation operation, MatrixBackend backend,
        long vectorBatches, Int128[]? wideValues, BigInteger[]? bigValues)
    {
        Rows = rows;
        Columns = columns;
        Operation = operation;
        Backend = backend;
        VectorBatches = vectorBatches;
        WideValues = wideValues;
        BigValues = bigValues;
    }
    public int Rows { get; }
    public int Columns { get; }
    public MatrixOperation Operation { get; }
    public MatrixBackend Backend { get; }
    /// <summary>Number of vector batches actually executed, excluding scalar tails.</summary>
    public long VectorBatches { get; }
    public Int128[]? WideValues { get; }
    public BigInteger[]? BigValues { get; }
    public BigInteger GetValue(int row, int column)
    {
        if ((uint)row >= (uint)Rows) throw new ArgumentOutOfRangeException(nameof(row));
        if ((uint)column >= (uint)Columns) throw new ArgumentOutOfRangeException(nameof(column));
        int index = row * Columns + column;
        return BigValues is not null ? BigValues[index] : (BigInteger)WideValues![index];
    }
}
