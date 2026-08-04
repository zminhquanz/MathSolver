using System.Runtime.InteropServices;

namespace MathSolver.Services;

/// <summary>
/// Phat hien so nhan CPU vat ly ma khong can them package rieng cho tung nen tang.
/// Neu he dieu hanh khong cung cap topology, gia tri logical processor duoc dung
/// lam fallback an toan.
/// </summary>
public static class PhysicalCoreDetector
{
    private const int RelationProcessorCore = 0;

    public static int GetPhysicalCoreCount()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return Math.Max(
                    1,
                    GetWindowsPhysicalCoreCount());
            }

            if (OperatingSystem.IsAndroid() ||
                OperatingSystem.IsLinux())
            {
                int detectedCoreCount =
                    GetLinuxPhysicalCoreCount();

                // Android SoC khong dung SMT; moi logical processor ma app
                // nhin thay tuong ung mot nhan vat ly. Mot so kernel lai bao
                // trung core_id cho nhieu cum CPU, nen lay gia tri lon hon.
                if (OperatingSystem.IsAndroid())
                {
                    detectedCoreCount =
                        Math.Max(
                            detectedCoreCount,
                            Environment.ProcessorCount);
                }

                return Math.Max(
                    1,
                    detectedCoreCount);
            }

            if (OperatingSystem.IsIOS() ||
                OperatingSystem.IsMacCatalyst() ||
                OperatingSystem.IsMacOS())
            {
                return Math.Max(
                    1,
                    GetApplePhysicalCoreCount());
            }
        }
        catch
        {
            // Mot so sandbox di dong chan topology CPU. Fallback ben duoi
            // van giu cho phep tinh hoat dong.
        }

        return Math.Max(
            1,
            Environment.ProcessorCount);
    }

    public static int GetRecommendedWorkerCount()
    {
        int physicalCoreCount =
            GetPhysicalCoreCount();

        return Math.Max(
            1,
            physicalCoreCount / 2);
    }

    private static int GetWindowsPhysicalCoreCount()
    {
        uint bufferLength = 0;

        _ = GetLogicalProcessorInformationEx(
            RelationProcessorCore,
            IntPtr.Zero,
            ref bufferLength);

        if (bufferLength == 0)
        {
            return Environment.ProcessorCount;
        }

        IntPtr buffer =
            Marshal.AllocHGlobal(
                checked((int)bufferLength));

        try
        {
            if (!GetLogicalProcessorInformationEx(
                    RelationProcessorCore,
                    buffer,
                    ref bufferLength))
            {
                return Environment.ProcessorCount;
            }

            int coreCount = 0;
            int offset = 0;
            int totalBufferLength =
                checked((int)bufferLength);

            while (offset + 8 <= totalBufferLength)
            {
                int relationship =
                    Marshal.ReadInt32(
                        buffer,
                        offset);

                int recordSize =
                    Marshal.ReadInt32(
                        buffer,
                        offset + 4);

                if (recordSize <= 0 ||
                    offset + recordSize > totalBufferLength)
                {
                    break;
                }

                if (relationship ==
                    RelationProcessorCore)
                {
                    coreCount++;
                }

                offset +=
                    recordSize;
            }

            return coreCount > 0
                ? coreCount
                : Environment.ProcessorCount;
        }
        finally
        {
            Marshal.FreeHGlobal(
                buffer);
        }
    }

    private static int GetLinuxPhysicalCoreCount()
    {
        const string CpuRoot =
            "/sys/devices/system/cpu";

        if (!Directory.Exists(
                CpuRoot))
        {
            return Environment.ProcessorCount;
        }

        var cores =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (string cpuDirectory in
                 Directory.EnumerateDirectories(
                     CpuRoot,
                     "cpu*",
                     SearchOption.TopDirectoryOnly))
        {
            string cpuName =
                Path.GetFileName(
                    cpuDirectory);

            if (cpuName.Length <= 3 ||
                !cpuName.AsSpan(3).ToString().All(
                    char.IsDigit))
            {
                continue;
            }

            string topologyDirectory =
                Path.Combine(
                    cpuDirectory,
                    "topology");

            string coreIdPath =
                Path.Combine(
                    topologyDirectory,
                    "core_id");

            if (!File.Exists(
                    coreIdPath))
            {
                continue;
            }

            string packageIdPath =
                Path.Combine(
                    topologyDirectory,
                    "physical_package_id");

            string packageId =
                File.Exists(
                    packageIdPath)
                    ? File.ReadAllText(
                            packageIdPath)
                        .Trim()
                    : "0";

            string coreId =
                File.ReadAllText(
                        coreIdPath)
                    .Trim();

            cores.Add(
                $"{packageId}:{coreId}");
        }

        return cores.Count > 0
            ? cores.Count
            : Environment.ProcessorCount;
    }

    private static int GetApplePhysicalCoreCount()
    {
        nuint valueSize =
            (nuint)sizeof(int);

        IntPtr valueBuffer =
            Marshal.AllocHGlobal(
                sizeof(int));

        try
        {
            int result;

            try
            {
                result =
                    SysctlByNameInternal(
                        "hw.physicalcpu",
                        valueBuffer,
                        ref valueSize,
                        IntPtr.Zero,
                        0);
            }
            catch (DllNotFoundException)
            {
                valueSize =
                    (nuint)sizeof(int);

                result =
                    SysctlByNameLibSystem(
                        "hw.physicalcpu",
                        valueBuffer,
                        ref valueSize,
                        IntPtr.Zero,
                        0);
            }

            return result == 0
                ? Marshal.ReadInt32(
                    valueBuffer)
                : Environment.ProcessorCount;
        }
        finally
        {
            Marshal.FreeHGlobal(
                valueBuffer);
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

    [DllImport(
        "__Internal",
        EntryPoint = "sysctlbyname",
        SetLastError = true)]
    private static extern int SysctlByNameInternal(
        string name,
        IntPtr oldValue,
        ref nuint oldLength,
        IntPtr newValue,
        nuint newLength);

    [DllImport(
        "/usr/lib/libSystem.B.dylib",
        EntryPoint = "sysctlbyname",
        SetLastError = true)]
    private static extern int SysctlByNameLibSystem(
        string name,
        IntPtr oldValue,
        ref nuint oldLength,
        IntPtr newValue,
        nuint newLength);
}
