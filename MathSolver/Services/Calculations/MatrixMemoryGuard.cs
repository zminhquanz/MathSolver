using System.Diagnostics;

namespace MathSolver.Services.Core;

public readonly record struct MatrixMemoryCheck(long RequiredBytes, long AvailableBytes)
{
    public bool CanAllocate => RequiredBytes <= AvailableBytes;
}

public sealed class MatrixMemoryException(long requiredBytes, long availableBytes)
    : OutOfMemoryException($"The matrix operation requires {requiredBytes} additional bytes; the available budget is {availableBytes} bytes.")
{
    public long RequiredBytes { get; } = requiredBytes;
    public long AvailableBytes { get; } = availableBytes;
}

/// <summary>Rejects large allocations that exceed managed-memory or process address-space headroom.</summary>
public static class MatrixMemoryGuard
{
    internal static bool IsMemoryFailure(Exception error)
    {
        if (error is OutOfMemoryException) return true;
        if (error is not AggregateException aggregate || aggregate.InnerExceptions.Count == 0) return false;
        foreach (var inner in aggregate.InnerExceptions)
            if (!IsMemoryFailure(inner)) return false;
        return true;
    }

    private const long MinimumGuardedAllocation = 16L * 1024 * 1024;
    private const long MaximumReserve = 256L * 1024 * 1024;
    private const long Maximum32BitProcessBytes = 1536L * 1024 * 1024;

    public static void EnsureAvailable(long additionalBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(additionalBytes);
        if (additionalBytes < MinimumGuardedAllocation) return;
        var check = CheckAvailable(additionalBytes);
        if (!check.CanAllocate) throw new MatrixMemoryException(check.RequiredBytes, check.AvailableBytes);
    }

    /// <summary>Checks expected resource limits without throwing or allocating matrix buffers.</summary>
    public static MatrixMemoryCheck CheckAvailable(long additionalBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(additionalBytes);
        var memory = GC.GetGCMemoryInfo();
        long available = Math.Max(0, memory.TotalAvailableMemoryBytes - GC.GetTotalMemory(false));
        // Respect machine memory pressure too; other processes do not count toward the managed heap.
        if (memory.MemoryLoadBytes > 0 && memory.HighMemoryLoadThresholdBytes > 0)
            available = Math.Min(available, Math.Max(0, memory.HighMemoryLoadThresholdBytes - memory.MemoryLoadBytes));
        using var process = Process.GetCurrentProcess();
        return CheckAvailable(additionalBytes, available, process.PrivateMemorySize64, Environment.Is64BitProcess);
    }

    // Separate the policy from machine measurements so its boundaries can be verified without large allocations.
    internal static void EnsureAvailable(long additionalBytes, long availableBytes, long currentProcessBytes, bool is64BitProcess)
    {
        var check = CheckAvailable(additionalBytes, availableBytes, currentProcessBytes, is64BitProcess);
        if (!check.CanAllocate) throw new MatrixMemoryException(check.RequiredBytes, check.AvailableBytes);
    }

    internal static MatrixMemoryCheck CheckAvailable(long additionalBytes, long availableBytes, long currentProcessBytes, bool is64BitProcess)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(additionalBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(availableBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(currentProcessBytes);
        long budget = availableBytes - Math.Min(availableBytes / 5, MaximumReserve);
        if (!is64BitProcess) budget = Math.Min(budget, Math.Max(0, Maximum32BitProcessBytes - currentProcessBytes));
        return new(additionalBytes, budget);
    }
}
