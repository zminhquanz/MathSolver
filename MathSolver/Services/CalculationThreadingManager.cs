using Microsoft.Maui.Storage;

namespace MathSolver.Services;

/// <summary>
/// Lưu lựa chọn đa luồng dùng chung cho benchmark và các bài toán
/// có thể chia thành nhiều tác vụ độc lập trong tương lai.
/// </summary>
public static class CalculationThreadingManager
{
    private const string PreferenceKey =
        "CalculationThreading.UseMultithreading";

    private static bool _initialized;
    private static bool _useMultithreading;

    public static event EventHandler? ThreadingChanged;

    public static bool IsMultithreadingAvailable =>
        Environment.ProcessorCount >
        1;

    public static int RecommendedWorkerCount =>
        IsMultithreadingAvailable
            ? Math.Max(
                1,
                Environment.ProcessorCount)
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
}
