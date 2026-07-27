using Microsoft.Maui.Storage;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Services;

public enum CalculationSimdMode
{
    Portable,
    Sse,
    AvxAvx2,
    Avx512
}

/// <summary>
/// Lưu trạng thái bật/tắt SIMD và tập lệnh benchmark đã chọn.
/// Tùy chọn mode chỉ điều khiển benchmark. Các thuật toán khác vẫn có thể
/// kiểm tra UseSimd như trước để tự chọn đường xử lý thích hợp.
/// </summary>
public static class CalculationAccelerationManager
{
    private const string UseSimdPreferenceKey =
        "CalculationAcceleration.UseSimd";

    private const string SimdModePreferenceKey =
        "CalculationAcceleration.SimdMode";

    private static bool _initialized;
    private static bool _useSimd;
    private static CalculationSimdMode _selectedSimdMode;

    public static event EventHandler? AccelerationChanged;

    public static bool IsAvx512Available =>
        Avx512F.IsSupported &&
        Vector512.IsHardwareAccelerated;

    public static bool IsAvxAvx2Available =>
        Avx.IsSupported &&
        Vector256.IsHardwareAccelerated;

    public static bool IsSseAvailable =>
        Sse2.IsSupported &&
        Vector128.IsHardwareAccelerated;

    public static bool IsArmNeonAvailable =>
        AdvSimd.IsSupported &&
        Vector128.IsHardwareAccelerated;

    public static bool IsPortableSimdAvailable =>
        Vector.IsHardwareAccelerated &&
        Vector<double>.Count >
        1;

    public static bool IsSimdAvailable =>
        IsAvx512Available ||
        IsAvxAvx2Available ||
        IsSseAvailable ||
        IsArmNeonAvailable ||
        IsPortableSimdAvailable;

    /// <summary>
    /// Các nhóm x86 xuất hiện trong selectbox. Nhóm không được CPU hỗ trợ
    /// sẽ không được thêm vào danh sách.
    /// </summary>
    public static IReadOnlyList<CalculationSimdMode>
        AvailableSelectableModes
    {
        get
        {
            var modes =
                new List<CalculationSimdMode>();

            if (IsAvx512Available)
            {
                modes.Add(
                    CalculationSimdMode.Avx512);
            }

            if (IsAvxAvx2Available)
            {
                modes.Add(
                    CalculationSimdMode.AvxAvx2);
            }

            if (IsSseAvailable)
            {
                modes.Add(
                    CalculationSimdMode.Sse);
            }

            return modes;
        }
    }

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

    public static CalculationSimdMode SelectedSimdMode
    {
        get
        {
            Initialize();

            return NormalizeMode(
                _selectedSimdMode);
        }
    }

    public static int SimdVectorWidthBits =>
        SelectedSimdMode switch
        {
            CalculationSimdMode.Avx512 =>
                512,

            CalculationSimdMode.AvxAvx2 =>
                256,

            CalculationSimdMode.Sse =>
                128,

            _ when IsArmNeonAvailable =>
                128,

            _ =>
                Vector<byte>.Count *
                8
        };

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
                UseSimdPreferenceKey,
                true);

        string storedMode =
            Preferences.Default.Get(
                SimdModePreferenceKey,
                string.Empty);

        _selectedSimdMode =
            Enum.TryParse(
                storedMode,
                ignoreCase:
                    true,
                out CalculationSimdMode parsedMode)
                ? parsedMode
                : GetBestAvailableMode();

        _selectedSimdMode =
            NormalizeMode(
                _selectedSimdMode);

        if (!IsSimdAvailable)
        {
            _useSimd =
                false;
        }
    }

    public static bool IsModeAvailable(
        CalculationSimdMode mode)
    {
        return mode switch
        {
            CalculationSimdMode.Avx512 =>
                IsAvx512Available,

            CalculationSimdMode.AvxAvx2 =>
                IsAvxAvx2Available,

            CalculationSimdMode.Sse =>
                IsSseAvailable,

            CalculationSimdMode.Portable =>
                IsArmNeonAvailable ||
                IsPortableSimdAvailable,

            _ =>
                false
        };
    }

    public static string GetModeDisplayName(
        CalculationSimdMode mode)
    {
        return mode switch
        {
            CalculationSimdMode.Avx512 =>
                "AVX512",

            CalculationSimdMode.AvxAvx2 =>
                "AVX/AVX2",

            CalculationSimdMode.Sse =>
                "SSE2/SSE3/SSSE3/SSE4.1/SSE4.2",

            _ when IsArmNeonAvailable =>
                "ARM NEON",

            _ =>
                "SIMD"
        };
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
            UseSimdPreferenceKey,
            _useSimd);

        AccelerationChanged?.Invoke(
            null,
            EventArgs.Empty);
    }

    public static void SetSelectedSimdMode(
        CalculationSimdMode mode)
    {
        Initialize();

        CalculationSimdMode normalizedMode =
            NormalizeMode(
                mode);

        if (_selectedSimdMode ==
            normalizedMode)
        {
            return;
        }

        _selectedSimdMode =
            normalizedMode;

        Preferences.Default.Set(
            SimdModePreferenceKey,
            _selectedSimdMode.ToString());

        AccelerationChanged?.Invoke(
            null,
            EventArgs.Empty);
    }

    public static void ResetToDefault()
    {
        Initialize();

        _selectedSimdMode =
            GetBestAvailableMode();

        Preferences.Default.Set(
            SimdModePreferenceKey,
            _selectedSimdMode.ToString());

        SetUseSimd(
            true);
    }

    private static CalculationSimdMode NormalizeMode(
        CalculationSimdMode mode)
    {
        return IsModeAvailable(
                   mode)
            ? mode
            : GetBestAvailableMode();
    }

    private static CalculationSimdMode GetBestAvailableMode()
    {
        if (IsAvx512Available)
        {
            return CalculationSimdMode.Avx512;
        }

        if (IsAvxAvx2Available)
        {
            return CalculationSimdMode.AvxAvx2;
        }

        if (IsSseAvailable)
        {
            return CalculationSimdMode.Sse;
        }

        return CalculationSimdMode.Portable;
    }
}
