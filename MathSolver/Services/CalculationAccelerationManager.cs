using Microsoft.Maui.Storage;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Runtime.InteropServices;
#if ANDROID
using MathSolver.Platforms.Android;
#endif

namespace MathSolver.Services;

public enum CalculationSimdMode
{
    Portable,
    Sse,
    AvxAvx2,
    Avx512,
    ArmNeon,
    ArmSve,
    ArmSme
}

/// <summary>
/// Lưu trạng thái bật/tắt SIMD và tập lệnh benchmark đã chọn.
/// Tùy chọn mode chỉ điều khiển benchmark. Các thuật toán khác vẫn có thể
/// kiểm tra UseSimd để tự chọn đường xử lý thích hợp. Engine NTT/CRT lũy thừa
/// dùng scalar khi Hardware acceleration tắt. Khi bật trên Windows/x86 có
/// AVX2, NTT/CRT <=10M và large-mode >10M đều có thể dùng butterfly AVX2 +
/// cached Shoup twiddle; large-mode vẫn giữ PersistentStatic scheduling. Đường SIMD
/// production khác dùng cho đồ thị Parabol và decimal formatting base-10,000
/// sau Carry. TXT export dùng AVX2 trên Windows/x86 và NEON/AdvSIMD trên
/// Android ARM64, tất cả cùng chịu công tắc Hardware acceleration.
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

    public static bool IsArmNeonAdvSimdAvailable =>
        AdvSimd.IsSupported;

    /// <summary>
    /// Managed NEON backend. Prefer AdvSimd intrinsics; Vector128 is the
    /// managed ARM64 fallback when the runtime exposes 128-bit SIMD through
    /// the generic intrinsic API. No native .so fallback is used.
    /// </summary>
    public static bool IsArmNeonManagedAvailable =>
        IsArmNeonAdvSimdAvailable ||
        (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 &&
         Vector128.IsHardwareAccelerated);

#if ANDROID
    /// <summary>
    /// Android UI availability is based on the real ARM64 HWCAP flag.
    /// Debug builds disable the Mono interpreter so the benchmark methods
    /// are JIT-compiled and can expose AdvSimd/Vector128 intrinsics.
    /// </summary>
    public static bool IsArmNeonHardwareAvailable =>
        RuntimeInformation.ProcessArchitecture == Architecture.Arm64 &&
        AndroidCpuInfo.HasAdvSimd;

    public static bool IsArmNeonAvailable =>
        IsArmNeonHardwareAvailable;
#else
    public static bool IsArmNeonHardwareAvailable => false;

    public static bool IsArmNeonAvailable =>
        IsArmNeonManagedAvailable;
#endif

    // SVE/SVE2 and SME/SME2 remain hardware-information-only on Android.
    // The benchmark intentionally exposes only the stable managed NEON path.
    public static bool IsArmSveManagedAvailable => false;
    public static bool IsArmSve2ManagedAvailable => false;
    public static bool IsArmSveAvailable => false;
    public static bool IsArmSve2Available => false;

    // .NET 10 chưa expose System.Runtime.Intrinsics.Arm.Sme/Sme2.
    // CPU capability vẫn được Hardware Information phát hiện riêng qua HWCAP.
    public static bool IsArmSmeRuntimeAvailable => false;

    public static bool IsPortableSimdAvailable =>
        Vector.IsHardwareAccelerated &&
        Vector<double>.Count >
        1;

    public static bool IsSimdAvailable =>
#if ANDROID
        IsArmNeonAvailable;
#else
        IsAvx512Available ||
        IsAvxAvx2Available ||
        IsSseAvailable ||
        IsPortableSimdAvailable;
#endif

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

#if ANDROID
            if (IsArmNeonAvailable)
            {
                modes.Add(
                    CalculationSimdMode.ArmNeon);
            }
#else
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
#endif

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

    /// <summary>
    /// Concrete SIMD backend availability for the base-10,000 decimal
    /// formatter used by large-power TXT export. Keep this predicate shared
    /// by both the export path and Hardware Information so the status text can
    /// never drift from the backend that will actually be executed.
    /// </summary>
    public static bool IsPowerExportAccelerationAvailable =>
#if ANDROID
        AdvSimd.Arm64.IsSupported;
#else
        Avx2.IsSupported &&
        Vector256.IsHardwareAccelerated &&
        (RuntimeInformation.ProcessArchitecture == Architecture.X64 ||
         RuntimeInformation.ProcessArchitecture == Architecture.X86);
#endif

    /// <summary>
    /// Effective SIMD state for the base-10,000 decimal formatter used by
    /// large-power TXT export. The shared Hardware acceleration switch is the
    /// only user-facing gate, exactly like the NTT/CRT and Parabola backends.
    /// </summary>
    public static bool UsePowerExportSimd =>
        UseSimd &&
        IsPowerExportAccelerationAvailable;

    /// <summary>
    /// AVX2 butterfly backend for the production in-place DIF/DIT NTT used by
    /// power calculations. The Hardware acceleration switch is the only
    /// user-facing gate. Benchmark mode selection remains independent, and the
    /// segmented >10M path reuses the same accepted cache-resident kernels.
    /// </summary>
    public static bool IsPowerNttAccelerationAvailable =>
#if ANDROID
        false;
#else
        Avx2.IsSupported &&
        Vector256.IsHardwareAccelerated &&
        (RuntimeInformation.ProcessArchitecture == Architecture.X64 ||
         RuntimeInformation.ProcessArchitecture == Architecture.X86);
#endif

    public static bool UsePowerNttAvx2 =>
        UseSimd &&
        IsPowerNttAccelerationAvailable;

    /// <summary>
    /// Experimental AVX2 backend for the single-threaded BigInteger power
    /// strategy. The implementation uses a bounded base-2^16 SIMD
    /// multiplication/squaring window and falls back to System.Numerics
    /// BigInteger when operands grow beyond that window.
    /// </summary>
    public static bool IsSingleThreadBigIntegerAccelerationAvailable =>
#if ANDROID
        false;
#else
        Avx2.IsSupported &&
        Vector256.IsHardwareAccelerated &&
        (RuntimeInformation.ProcessArchitecture == Architecture.X64 ||
         RuntimeInformation.ProcessArchitecture == Architecture.X86);
#endif

    public static bool UseSingleThreadBigIntegerAvx2 =>
        UseSimd &&
        IsSingleThreadBigIntegerAccelerationAvailable;

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

            CalculationSimdMode.ArmNeon =>
                128,

            CalculationSimdMode.ArmSve =>
                Vector<byte>.Count *
                8,

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

            CalculationSimdMode.ArmNeon =>
                IsArmNeonAvailable,

            CalculationSimdMode.ArmSve =>
                false,

            CalculationSimdMode.ArmSme =>
                IsArmSmeRuntimeAvailable,

            CalculationSimdMode.Portable =>
#if ANDROID
                false,
#else
                IsPortableSimdAvailable,
#endif

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
                "AVX-512",

            CalculationSimdMode.AvxAvx2 =>
                "AVX/AVX2",

            CalculationSimdMode.Sse =>
                "SSE2/SSE3/SSSE3/SSE4.1/SSE4.2",

            CalculationSimdMode.ArmNeon =>
                "NEON/AdvSIMD",

            CalculationSimdMode.ArmSve =>
                "SVE/SVE2",

            CalculationSimdMode.ArmSme =>
                "SME/SME2",

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
#if ANDROID
        // Android benchmark intentionally uses only NEON/AdvSIMD.
        if (IsArmNeonAvailable)
        {
            return CalculationSimdMode.ArmNeon;
        }

        return CalculationSimdMode.Portable;
#else
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
#endif
    }
}
