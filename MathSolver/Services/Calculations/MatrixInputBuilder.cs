using System.Runtime.InteropServices;

namespace MathSolver.Services.Core;

/// <summary>Bounded CSV staging while the final row count is still unknown.</summary>
internal sealed class MatrixInputBuilder(string? temporaryDirectory) : IDisposable
{
    private List<long>? _values = [];
    private FileStream? _spool;
    private int _count;

    internal void AddRow(ReadOnlySpan<long> row, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        int next = checked(_count + row.Length);
        if (_spool is null && next >= MatrixDraft.FileBackedThresholdCells)
        {
            string path = Path.Combine(temporaryDirectory ?? Path.GetTempPath(), "matrix-import-" + Guid.NewGuid().ToString("N") + ".tmp");
            _spool = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Delete,
                65536, FileOptions.DeleteOnClose | FileOptions.SequentialScan);
            _spool.Write(MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(_values!)));
            _values = null;
        }
        if (_spool is not null) _spool.Write(MemoryMarshal.AsBytes(row));
        else foreach (long value in row) _values!.Add(value);
        _count = next;
    }

    internal IntegerMatrix Complete(int rows, int columns, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_count != checked(rows * columns)) throw new InvalidOperationException("Incomplete matrix import.");
        if (_spool is null) return new IntegerMatrix(rows, columns, _values!.ToArray());
        _spool.Position = 0;
        MatrixInputStore? store = new(_count, temporaryDirectory, filled: true);
        try
        {
            var buffer = new long[MatrixInputStore.BlockLength];
            for (int offset = 0; offset < _count; offset += buffer.Length)
            {
                token.ThrowIfCancellationRequested();
                var panel = buffer.AsSpan(0, Math.Min(buffer.Length, _count - offset));
                _spool.ReadExactly(MemoryMarshal.AsBytes(panel));
                store.WriteValues(offset, panel);
            }
            token.ThrowIfCancellationRequested();
            store.Flush();
            var result = new IntegerMatrix(rows, columns, store);
            store = null;
            return result;
        }
        finally { store?.Dispose(); }
    }

    public void Dispose() { _values = null; _spool?.Dispose(); _spool = null; }
}
