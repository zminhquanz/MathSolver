using Microsoft.Maui.Storage;
using System.Runtime.InteropServices;

namespace MathSolver.Services;

/// <summary>
/// Lưu lựa chọn đa luồng dùng chung cho benchmark và engine tính toán.
/// Tab lũy thừa đọc thiết lập này khi bắt đầu mỗi phép tính lớn: tắt dùng
/// BigInteger.Pow một luồng, bật dùng engine NTT/CRT có phép nhân nội bộ
/// chạy song song với ngân sách theo toàn bộ bộ xử lý logic. Engine lũy thừa
/// tự chia ngân sách này thành hai nhánh để tận dụng cả hai luồng SMT của mỗi
/// nhân, rồi dùng lại toàn bộ ngân sách cho phép nhân ghép cuối.
/// </summary>
public static class CalculationThreadingManager
{
    private const string PreferenceKey =
        "CalculationThreading.UseMultithreading";

    private static bool _initialized;
    private static bool _useMultithreading;

    private static readonly Lazy<int> PhysicalCoreCountValue =
        new(DetectPhysicalCoreCount);

    public static event EventHandler? ThreadingChanged;

    public static bool IsMultithreadingAvailable =>
        Environment.ProcessorCount >
        1;

    public static int LogicalProcessorCount =>
        Math.Max(
            1,
            Environment.ProcessorCount);

    public static int PhysicalCoreCount =>
        PhysicalCoreCountValue.Value;

    public static int RecommendedWorkerCount =>
        IsMultithreadingAvailable
            ? LogicalProcessorCount
            : 1;

    public static bool UseMultithreading
    {
        get
        {
            Initialize();

            return
                _useMultithreading &&
                IsMultithreadingAvailable;
        }
    }

    public static int MaxDegreeOfParallelism =>
        UseMultithreading
            ? RecommendedWorkerCount
            : 1;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized =
            true;

        _useMultithreading =
            Preferences.Default.Get(
                PreferenceKey,
                true);

        if (!IsMultithreadingAvailable)
        {
            _useMultithreading =
                false;
        }
    }

    public static void SetUseMultithreading(
        bool useMultithreading)
    {
        Initialize();

        bool normalizedValue =
            useMultithreading &&
            IsMultithreadingAvailable;

        if (_useMultithreading ==
            normalizedValue)
        {
            return;
        }

        _useMultithreading =
            normalizedValue;

        Preferences.Default.Set(
            PreferenceKey,
            _useMultithreading);

        ThreadingChanged?.Invoke(
            null,
            EventArgs.Empty);
    }

    public static ParallelOptions CreateParallelOptions()
    {
        return new ParallelOptions
        {
            MaxDegreeOfParallelism =
                MaxDegreeOfParallelism
        };
    }

    public static void ResetToDefault()
    {
        SetUseMultithreading(
            true);
    }

    private static int DetectPhysicalCoreCount()
    {
        int logicalProcessorCount =
            LogicalProcessorCount;

        if (!OperatingSystem.IsWindows())
        {
            return logicalProcessorCount;
        }

        const int relationProcessorCore = 0;
        uint bufferLength = 0;

        _ = GetLogicalProcessorInformationEx(
            relationProcessorCore,
            IntPtr.Zero,
            ref bufferLength);

        if (bufferLength == 0)
        {
            return logicalProcessorCount;
        }

        IntPtr buffer =
            Marshal.AllocHGlobal(
                checked((int)bufferLength));

        try
        {
            if (!GetLogicalProcessorInformationEx(
                    relationProcessorCore,
                    buffer,
                    ref bufferLength))
            {
                return logicalProcessorCount;
            }

            int offset = 0;
            int physicalCoreCount = 0;

            while (offset < bufferLength)
            {
                IntPtr entry =
                    IntPtr.Add(
                        buffer,
                        offset);

                int relationship =
                    Marshal.ReadInt32(
                        entry,
                        0);

                int entrySize =
                    Marshal.ReadInt32(
                        entry,
                        sizeof(int));

                if (entrySize <
                    sizeof(int) * 2)
                {
                    return logicalProcessorCount;
                }

                if (relationship ==
                    relationProcessorCore)
                {
                    physicalCoreCount++;
                }

                offset =
                    checked(
                        offset +
                        entrySize);
            }

            return physicalCoreCount > 0
                ? physicalCoreCount
                : logicalProcessorCount;
        }
        catch
        {
            return logicalProcessorCount;
        }
        finally
        {
            Marshal.FreeHGlobal(
                buffer);
        }
    }

    [DllImport(
        "kernel32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLogicalProcessorInformationEx(
        int relationshipType,
        IntPtr buffer,
        ref uint returnedLength);
}
