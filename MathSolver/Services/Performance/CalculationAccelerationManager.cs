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
    ArmSme,
    Auto
}

public enum PowerNttSseIsa
{
    None = 0,
    Sse2,
    Sse3,
    Ssse3,
    Sse41,
    Sse42
}

/// <summary>Shared SIMD preference and effective kernel policy for the application.</summary>
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
        // The selectable 256-bit backend is shared by floating-point and
        // integer benchmarks. AVX alone is sufficient for Float/Double, but
        // 256-bit integer ALU instructions require AVX2.
        Avx.IsSupported &&
        Avx2.IsSupported &&
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

    /// <summary>
    /// Android execution policy shared by NTT, parabola, TXT export and benchmarks.
    /// Hardware detection remains independent so Debug can still list CPU features.
    /// </summary>
    public static bool IsAndroidNeonExecutionAllowed =>
#if ANDROID && MATHSOLVER_ANDROID_RELEASE && !DEBUG
        IsAndroidBuildOptimized && IsArmNeonHardwareAvailable &&
        (AdvSimd.Arm64.IsSupported || Vector128.IsHardwareAccelerated);
#else
        false;
#endif

#if ANDROID && MATHSOLVER_ANDROID_RELEASE && !DEBUG
    private static readonly bool IsAndroidBuildOptimized =
        Attribute.GetCustomAttribute(typeof(CalculationAccelerationManager).Assembly,
            typeof(System.Diagnostics.DebuggableAttribute)) is not
            System.Diagnostics.DebuggableAttribute { IsJITOptimizerDisabled: true };
#endif

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
        IsArmNeonManagedAvailable ||
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
                new List<CalculationSimdMode> { CalculationSimdMode.Auto };

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

            if (IsArmNeonManagedAvailable)
            {
                modes.Add(CalculationSimdMode.ArmNeon);
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
#if ANDROID
                IsAndroidNeonExecutionAllowed &&
#endif
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
        IsAndroidNeonExecutionAllowed;
#else
        Sse2.IsSupported &&
        Vector128.IsHardwareAccelerated &&
        (RuntimeInformation.ProcessArchitecture == Architecture.X64 ||
         RuntimeInformation.ProcessArchitecture == Architecture.X86);
#endif

    /// <summary>
    /// Effective SIMD state for the base-10,000 decimal formatter used by
    /// large-power TXT export, respecting both the switch and selected mode.
    /// </summary>
    public static bool UsePowerExportSimd =>
        UseSimd &&
        (EffectiveSimdMode is CalculationSimdMode.ArmNeon or CalculationSimdMode.Sse or
            CalculationSimdMode.AvxAvx2 or CalculationSimdMode.Avx512) &&
        IsPowerExportAccelerationAvailable;

    /// <summary>
    /// NTT/CRT acceleration policy. x86 SSE2+ is available for both the legacy
    /// &lt;=10M path and the memory-bounded &gt;10M path. AVX2/AVX-512 remain the
    /// preferred wider backends when selected and supported.
    /// </summary>
    public static bool IsPowerNttAccelerationAvailable =>
#if ANDROID
        IsAndroidNeonExecutionAllowed;
#else
        Sse2.IsSupported &&
        Vector128.IsHardwareAccelerated &&
        (RuntimeInformation.ProcessArchitecture == Architecture.X64 ||
         RuntimeInformation.ProcessArchitecture == Architecture.X86);
#endif

    public static bool UsePowerNttAvx2 =>
        AllowAvx &&
        Avx2.IsSupported &&
        IsPowerNttAccelerationAvailable;

    /// <summary>Managed ARM64 NEON for Android's &lt;=10M NTT cache-local butterflies.</summary>
    public static bool UsePowerNttNeon =>
#if ANDROID
        UseSimd && EffectiveSimdMode == CalculationSimdMode.ArmNeon &&
        IsPowerNttAccelerationAvailable;
#else
        false;
#endif

    /// <summary>SSE-family fallback for cache-resident NTT butterflies and CRT through 100M.</summary>
    public static bool UsePowerNttSse =>
#if ANDROID
        false;
#else
        UseSimd && IsSseAvailable && !UsePowerNttAvx2 &&
        (EffectiveSimdMode is CalculationSimdMode.Sse or
            CalculationSimdMode.AvxAvx2 or CalculationSimdMode.Avx512);
#endif

    /// <summary>
    /// Highest x86 SSE generation available to the 128-bit NTT/CRT backend.
    /// SSE3 and SSE4.2 do not add useful packed integer multiply/reduction
    /// instructions for this kernel; they still identify the CPU generation,
    /// while SSSE3/SSE4.1 enable their useful sub-kernels where applicable.
    /// </summary>
    public static PowerNttSseIsa EffectivePowerNttSseIsa
    {
        get
        {
#if ANDROID
            return PowerNttSseIsa.None;
#else
            if (!UsePowerNttSse) return PowerNttSseIsa.None;
            if (Sse42.IsSupported) return PowerNttSseIsa.Sse42;
            if (Sse41.IsSupported) return PowerNttSseIsa.Sse41;
            if (Ssse3.IsSupported) return PowerNttSseIsa.Ssse3;
            if (Sse3.IsSupported) return PowerNttSseIsa.Sse3;
            return Sse2.IsSupported ? PowerNttSseIsa.Sse2 : PowerNttSseIsa.None;
#endif
        }
    }

    public static string PowerNttSseBackendName =>
        EffectivePowerNttSseIsa switch
        {
            PowerNttSseIsa.Sse42 => "SSE4.2",
            PowerNttSseIsa.Sse41 => "SSE4.1",
            PowerNttSseIsa.Ssse3 => "SSSE3",
            PowerNttSseIsa.Sse3 => "SSE3",
            PowerNttSseIsa.Sse2 => "SSE2",
            _ => "Scalar"
        };

    public static CalculationSimdMode SelectedSimdMode
    {
        get
        {
            Initialize();

            return NormalizeMode(
                _selectedSimdMode);
        }
    }

    public static CalculationSimdMode EffectiveSimdMode =>
        SelectedSimdMode == CalculationSimdMode.Auto ? GetBestAvailableMode() : SelectedSimdMode;

    public static bool AllowAvx => UseSimd &&
        EffectiveSimdMode is CalculationSimdMode.AvxAvx2 or CalculationSimdMode.Avx512;

    public static bool AllowAvx512 => UseSimd &&
        EffectiveSimdMode == CalculationSimdMode.Avx512 && IsAvx512Available;

    public static int SimdVectorWidthBits =>
        EffectiveSimdMode switch
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
                : CalculationSimdMode.Auto;

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
            CalculationSimdMode.Auto => true,
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
            CalculationSimdMode.Auto => AppLanguageManager.CurrentLanguage == AppLanguage.English
                ? "Automatic" : "Tự động",
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

        SetSelectedSimdMode(CalculationSimdMode.Auto);

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

        if (IsArmNeonManagedAvailable)
        {
            return CalculationSimdMode.ArmNeon;
        }

        return CalculationSimdMode.Portable;
#endif
    }
}
