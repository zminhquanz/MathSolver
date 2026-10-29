using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace MathSolver.Services.QuestionBank;

/// <summary>Enumerates DXGI hardware without WMI or loading an inference backend.</summary>
public static class WindowsAiGpuDetector
{
    public static IReadOnlyList<AiGpuAdapter> GetAdapters()
    {
        if (!OperatingSystem.IsWindows()) return Array.Empty<AiGpuAdapter>();
        try { return EnumerateAdapters(); }
        catch (Exception) { return Array.Empty<AiGpuAdapter>(); }
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<AiGpuAdapter> EnumerateAdapters()
    {
        var adapters = new List<AiGpuAdapter>();
        nint factory = 0;
        try
        {
            Guid factoryId = new("770aae78-f26f-4dba-a829-253c83d1b387");
            if (CreateDXGIFactory1(ref factoryId, out factory) < 0 || factory == 0)
                return Array.Empty<AiGpuAdapter>();

            // IUnknown(3), IDXGIObject(4), IDXGIFactory(5), then EnumAdapters1.
            var enumerate = GetMethod<EnumAdaptersDelegate>(factory, 12);
            for (uint index = 0; ; index++)
            {
                nint adapter = 0;
                try
                {
                    int result = enumerate(factory, index, out adapter);
                    if (result == unchecked((int)0x887a0002)) break; // DXGI_ERROR_NOT_FOUND
                    if (result < 0 || adapter == 0) return Array.Empty<AiGpuAdapter>();

                    // IUnknown(3), IDXGIObject(4), IDXGIAdapter(3), then GetDesc1.
                    var describe = GetMethod<GetDescriptionDelegate>(adapter, 10);
                    if (describe(adapter, out AdapterDescription description) < 0) continue;
                    adapters.Add(new(description.Description?.TrimEnd('\0').Trim() ?? string.Empty,
                        description.VendorId, description.DedicatedVideoMemory.ToUInt64(),
                        (description.Flags & 2) != 0)); // DXGI_ADAPTER_FLAG_SOFTWARE
                }
                finally
                {
                    if (adapter != 0) Marshal.Release(adapter);
                }
            }
        }
        finally
        {
            if (factory != 0) Marshal.Release(factory);
        }
        return adapters.AsReadOnly();
    }

    private static T GetMethod<T>(nint instance, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));

    [DllImport("dxgi.dll", ExactSpelling = true)]
    private static extern int CreateDXGIFactory1(ref Guid interfaceId, out nint factory);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumAdaptersDelegate(nint factory, uint index, out nint adapter);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDescriptionDelegate(nint adapter, out AdapterDescription description);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct AdapterDescription
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string? Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSystemId;
        public uint Revision;
        public UIntPtr DedicatedVideoMemory;
        public UIntPtr DedicatedSystemMemory;
        public UIntPtr SharedSystemMemory;
        public uint AdapterLuidLow;
        public int AdapterLuidHigh;
        public uint Flags;
    }
}
