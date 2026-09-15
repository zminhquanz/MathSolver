using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MathSolver.Services;

/// <summary>Physical RAM only: never includes swap, zram or the GC heap budget.</summary>
public static class PhysicalMemoryInfo
{
    public static long? ReadTotalBytes()
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
            using var manager = Android.App.Application.Context.GetSystemService(
                Android.Content.Context.ActivityService) as Android.App.ActivityManager;
            if (manager is null) return null;
            using var info = new Android.App.ActivityManager.MemoryInfo();
            manager.GetMemoryInfo(info);
            // API 34 reports installed/advertised RAM including hardware reservations.
            // Older versions expose kernel-visible physical RAM; do not round it up.
            long bytes = OperatingSystem.IsAndroidVersionAtLeast(34) && info.AdvertisedMem > 0
                ? info.AdvertisedMem : info.TotalMem;
            return bytes > 0 ? bytes : null;
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

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalMemoryInKilobytes);
}
