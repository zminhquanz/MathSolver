using System.Runtime.InteropServices;

namespace MathSolver.Services.Core;

/// <summary>Current OS allocation headroom, rather than a previous GC's high-load threshold.</summary>
internal static class MatrixSystemMemory
{
    internal static (long PhysicalAvailable, long CommitAvailable)? Read()
    {
        if (!OperatingSystem.IsWindows()) return null;
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref status)) return null;
        return ((long)Math.Min(status.AvailablePhysical, (ulong)long.MaxValue),
            (long)Math.Min(status.AvailablePageFile, (ulong)long.MaxValue));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        internal uint Length, MemoryLoad;
        internal ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile,
            TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
}
