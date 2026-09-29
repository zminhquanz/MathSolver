using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace MathSolver.Services;

/// <summary>
/// Installed physical RAM, not currently available RAM. Swap, zram, free
/// memory and the GC heap budget are excluded. Android falls back to the
/// kernel-visible physical total when its bootloader size is unavailable.
/// </summary>
public static class PhysicalMemoryInfo
{
    public static long? ReadInstalledBytes()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return GetPhysicallyInstalledSystemMemory(out ulong kilobytes) && kilobytes > 0
                    ? checked((long)kilobytes * 1024L)
                    : null;
            }

#if ANDROID
            long? installed = ReadAndroidBootDdrBytes();
            using var manager = Android.App.Application.Context.GetSystemService(
                Android.Content.Context.ActivityService) as Android.App.ActivityManager;
            if (manager is null) return installed;
            using var info = new Android.App.ActivityManager.MemoryInfo();
            manager.GetMemoryInfo(info);
            // AdvertisedMem is a retail figure and can include RAM expansion.
            // On a measured 12-GiB device it reported 16 GB, while the
            // bootloader reported 12 GiB. Prefer physical DDR size when the
            // vendor exposes it; otherwise TotalMem is a conservative lower
            // bound. Neither value depends on currently free memory.
            if (installed is > 0 &&
                (info.TotalMem <= 0 || installed >= info.TotalMem))
            {
                return installed;
            }

            return info.TotalMem > 0 ? info.TotalMem : installed;
#elif IOS || MACCATALYST
            ulong bytes = Foundation.NSProcessInfo.ProcessInfo.PhysicalMemory;
            return bytes > 0 ? checked((long)bytes) : null;
#else
            return null;
#endif
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Debug.WriteLine($"Physical RAM query failed: {exception.Message}");
            return null;
        }
    }

#if ANDROID
    private static long? ReadAndroidBootDdrBytes()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "/system/bin/getprop",
                Arguments = "ro.boot.ddr_size",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            if (process is null) return null;
            if (!process.WaitForExit(2000))
            {
                process.Kill();
                return null;
            }

            string output = process.StandardOutput.ReadToEnd().Trim();
            return process.ExitCode == 0 &&
                   long.TryParse(output, NumberStyles.None,
                       CultureInfo.InvariantCulture, out long bytes) &&
                   bytes is > 0 and <= (1L << 40)
                ? bytes
                : null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Debug.WriteLine($"Physical DDR query failed: {exception.Message}");
            return null;
        }
    }
#endif

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalMemoryInKilobytes);
}
