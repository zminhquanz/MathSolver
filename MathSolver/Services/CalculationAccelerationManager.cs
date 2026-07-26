using System.Numerics;

namespace MathSolver.Services;

/// <summary>
/// Lưu lựa chọn tăng tốc tính toán dùng chung cho toàn ứng dụng.
/// Bật: dùng SIMD khi phần cứng hỗ trợ.
/// Tắt: luôn dùng đường xử lý Scalar.
/// </summary>
public static class CalculationAccelerationManager
{
    private const string PreferenceKey =
        "CalculationAcceleration.UseSimd";

    private static bool _initialized;
    private static bool _useSimd;

    public static event EventHandler? AccelerationChanged;

    public static bool IsSimdAvailable =>
        Vector.IsHardwareAccelerated &&
        Vector<double>.Count >
        1;

    public static bool UseSimd
    {
        get
        {
            Initialize();
            return
                _useSimd &&
                IsSimdAvailable;
        }
    }

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized =
            true;

        _useSimd =
            Preferences.Default.Get(
                PreferenceKey,
                true);

        if (!IsSimdAvailable)
        {
            _useSimd =
                false;
        }
    }

    public static void SetUseSimd(
        bool useSimd)
    {
        Initialize();

        bool normalizedValue =
            useSimd &&
            IsSimdAvailable;

        if (_useSimd ==
            normalizedValue)
        {
            return;
        }

        _useSimd =
            normalizedValue;

        Preferences.Default.Set(
            PreferenceKey,
            _useSimd);

        AccelerationChanged?.Invoke(
            null,
            EventArgs.Empty);
    }

    public static void ResetToDefault()
    {
        SetUseSimd(
            true);
    }
}
