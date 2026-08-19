#if ANDROID
using System.Runtime.InteropServices;
using System.Text;

namespace MathSolver.Platforms.Android;

internal static class AndroidCpuInfo
{
    private const nuint AtHwCap = 16;
    private const nuint AtHwCap2 = 26;

    private const ulong HwCapAsimd = 1UL << 1;
    private const ulong HwCapSve = 1UL << 22;

    private const ulong HwCap2Sve2 = 1UL << 1;
    private const ulong HwCap2Sme = 1UL << 23;
    private const ulong HwCap2Sme2 = 1UL << 37;

    private const int PrSveGetVl = 51;
    private const int PrSmeGetVl = 64;
    private const int VectorLengthMask = 0xffff;

    private const int AndroidPropertyBufferSize = 256;

    internal readonly record struct ArmSimdCapabilities(
        bool AdvSimd,
        bool Sve,
        bool Sve2,
        bool Sme,
        bool Sme2,
        int SveVectorWidthBits,
        int SmeVectorWidthBits)
    {
        public bool HasSimd =>
            AdvSimd ||
            Sve ||
            Sve2 ||
            Sme ||
            Sme2;

        public int MaximumVectorWidthBits
        {
            get
            {
                int width =
                    AdvSimd
                        ? 128
                        : 0;

                width =
                    Math.Max(
                        width,
                        SveVectorWidthBits);

                width =
                    Math.Max(
                        width,
                        SmeVectorWidthBits);

                if (width == 0 &&
                    (Sve || Sve2 || Sme || Sme2))
                {
                    // SVE and SME have a minimum architectural vector length
                    // of 128 bits. PR_*_GET_VL should normally provide the
                    // current process value, but keep this conservative
                    // fallback for OEM kernels that restrict the query.
                    width = 128;
                }

                return width;
            }
        }
    }

    public static string? GetProcessorName()
    {
        string? socModel =
            FirstUsefulValue(
                ReadAndroidSystemProperty("ro.soc.model"),
                ReadAndroidSystemProperty("ro.mediatek.platform"),
                ReadAndroidSystemProperty("ro.board.platform"));

        string? socManufacturer =
            FirstUsefulValue(
                ReadAndroidSystemProperty("ro.soc.manufacturer"));

        if (!string.IsNullOrWhiteSpace(socModel))
        {
            socManufacturer ??=
                InferSocManufacturer(
                    socModel);

            if (!string.IsNullOrWhiteSpace(socManufacturer) &&
                !socModel.Contains(
                    socManufacturer,
                    StringComparison.OrdinalIgnoreCase))
            {
                return $"{socManufacturer} {socModel}";
            }

            return socModel;
        }

        string? hardware =
            FirstUsefulValue(
                ReadAndroidSystemProperty("ro.hardware"),
                ReadAndroidSystemProperty("ro.boot.hardware"));

        return hardware;
    }

    public static ArmSimdCapabilities GetArmSimdCapabilities()
    {
        ulong hwCap =
            ReadAuxValue(
                AtHwCap);

        ulong hwCap2 =
            ReadAuxValue(
                AtHwCap2);

        HashSet<string> cpuFeatures =
            ReadCpuFeatureTokens();

        bool advSimd =
            (hwCap & HwCapAsimd) != 0 ||
            cpuFeatures.Contains("asimd") ||
            cpuFeatures.Contains("neon");

        bool sve =
            (hwCap & HwCapSve) != 0 ||
            cpuFeatures.Contains("sve");

        bool sve2 =
            (hwCap2 & HwCap2Sve2) != 0 ||
            cpuFeatures.Contains("sve2");

        bool sme =
            (hwCap2 & HwCap2Sme) != 0 ||
            cpuFeatures.Contains("sme");

        bool sme2 =
            (hwCap2 & HwCap2Sme2) != 0 ||
            cpuFeatures.Contains("sme2");

        // Newer extensions imply their base extension even when an OEM
        // kernel omits a redundant token in /proc/cpuinfo.
        if (sve2)
        {
            sve = true;
        }

        if (sme2)
        {
            sme = true;
        }

        int sveWidthBits =
            sve
                ? ReadCurrentVectorWidthBits(
                    PrSveGetVl)
                : 0;

        int smeWidthBits =
            sme
                ? ReadCurrentVectorWidthBits(
                    PrSmeGetVl)
                : 0;

        return new ArmSimdCapabilities(
            advSimd,
            sve,
            sve2,
            sme,
            sme2,
            sveWidthBits,
            smeWidthBits);
    }

    public static string GetSupportedArmSimdInstructionSets()
    {
        ArmSimdCapabilities capabilities =
            GetArmSimdCapabilities();

        var supported =
            new List<string>();

        if (capabilities.AdvSimd)
        {
            supported.Add(
                "NEON (AdvSIMD)");
        }

        if (capabilities.Sve)
        {
            supported.Add(
                "SVE");
        }

        if (capabilities.Sve2)
        {
            supported.Add(
                "SVE2");
        }

        if (capabilities.Sme)
        {
            supported.Add(
                "SME");
        }

        if (capabilities.Sme2)
        {
            supported.Add(
                "SME2");
        }

        return string.Join(
            ", ",
            supported);
    }

    public static int GetMaximumVectorWidthBits() =>
        GetArmSimdCapabilities()
            .MaximumVectorWidthBits;

    public static bool HasAdvSimd =>
        GetArmSimdCapabilities()
            .AdvSimd;

    public static bool HasArmSimd =>
        GetArmSimdCapabilities()
            .HasSimd;

    private static ulong ReadAuxValue(
        nuint type)
    {
        try
        {
            return (ulong)GetAuxValue(
                type);
        }
        catch
        {
            return 0UL;
        }
    }

    private static int ReadCurrentVectorWidthBits(
        int option)
    {
        try
        {
            int result =
                Prctl(
                    option,
                    0,
                    0,
                    0,
                    0);

            if (result <= 0)
            {
                return 0;
            }

            int vectorLengthBytes =
                result &
                VectorLengthMask;

            return vectorLengthBytes > 0
                ? vectorLengthBytes * 8
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static HashSet<string> ReadCpuFeatureTokens()
    {
        var tokens =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        try
        {
            if (!File.Exists(
                    "/proc/cpuinfo"))
            {
                return tokens;
            }

            foreach (string line
                     in File.ReadLines(
                         "/proc/cpuinfo"))
            {
                int separatorIndex =
                    line.IndexOf(':');

                if (separatorIndex <= 0)
                {
                    continue;
                }

                string key =
                    line[..separatorIndex]
                        .Trim();

                if (!key.Equals(
                        "Features",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string value =
                    line[(separatorIndex + 1)..];

                foreach (string token
                         in value.Split(
                             [' ', '\t'],
                             StringSplitOptions.RemoveEmptyEntries |
                             StringSplitOptions.TrimEntries))
                {
                    tokens.Add(
                        token);
                }
            }
        }
        catch
        {
            // Auxv remains the primary source. cpuinfo is only a fallback.
        }

        return tokens;
    }

    private static string? ReadAndroidSystemProperty(
        string name)
    {
        IntPtr buffer =
            IntPtr.Zero;

        try
        {
            buffer =
                Marshal.AllocHGlobal(
                    AndroidPropertyBufferSize);

            for (int index = 0;
                 index < AndroidPropertyBufferSize;
                 index++)
            {
                Marshal.WriteByte(
                    buffer,
                    index,
                    0);
            }

            int length =
                SystemPropertyGet(
                    name,
                    buffer);

            if (length <= 0)
            {
                return null;
            }

            return NormalizePropertyValue(
                Marshal.PtrToStringAnsi(
                    buffer,
                    length));
        }
        catch
        {
            return null;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(
                    buffer);
            }
        }
    }


    private static string? InferSocManufacturer(
        string socModel)
    {
        if (socModel.StartsWith(
                "MT",
                StringComparison.OrdinalIgnoreCase))
        {
            return "MediaTek";
        }

        if (socModel.StartsWith(
                "SM",
                StringComparison.OrdinalIgnoreCase) ||
            socModel.StartsWith(
                "QCM",
                StringComparison.OrdinalIgnoreCase) ||
            socModel.StartsWith(
                "QCS",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Qualcomm";
        }

        if (socModel.Contains(
                "Exynos",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Samsung";
        }

        if (socModel.Contains(
                "Tensor",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Google";
        }

        return null;
    }

    private static string? FirstUsefulValue(
        params string?[] values)
    {
        foreach (string? value
                 in values)
        {
            string? normalized =
                NormalizePropertyValue(
                    value);

            if (!string.IsNullOrWhiteSpace(
                    normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    private static string? NormalizePropertyValue(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return null;
        }

        string normalized =
            value.Trim();

        if (normalized is
            "0" or
            "unknown" or
            "Unknown" or
            "UNKNOWN" or
            "null" or
            "N/A")
        {
            return null;
        }

        return normalized;
    }

    [DllImport(
        "libc",
        EntryPoint = "getauxval")]
    private static extern nuint GetAuxValue(
        nuint type);

    [DllImport(
        "libc",
        EntryPoint = "prctl",
        SetLastError = true)]
    private static extern int Prctl(
        int option,
        nuint argument2,
        nuint argument3,
        nuint argument4,
        nuint argument5);

    [DllImport(
        "libc",
        EntryPoint = "__system_property_get")]
    private static extern int SystemPropertyGet(
        [MarshalAs(UnmanagedType.LPStr)] string name,
        IntPtr value);
}
#endif
