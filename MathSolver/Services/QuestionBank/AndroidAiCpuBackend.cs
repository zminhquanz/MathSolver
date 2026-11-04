#if ANDROID
using System.Runtime.InteropServices;

namespace MathSolver.Services.QuestionBank;

// Select before libggml/libllama is loaded. The optional variant advertises
// libggml-cpu.so as SONAME so the Android linker reuses it for those dependencies.
internal static class AndroidAiCpuBackend
{
    private static IntPtr _handle; // Native backend has process lifetime.
    internal static bool DisableForProbe {get;set;} // Set only before native initialization.
    public static string Description {get;private set;}="CPU baseline";

    public static void Load(string directory)
    {
        if(_handle!=IntPtr.Zero)return;
        bool arm64=RuntimeInformation.ProcessArchitecture==Architecture.Arm64;
        ulong hwcap=0,hwcap2=0;
        if(arm64)
            try {hwcap=(ulong)GetAuxValue(16);hwcap2=(ulong)GetAuxValue(26);}
            catch(EntryPointNotFoundException) { }
        string optimized=Path.Combine(directory,"libggml-cpu-kleidiai.so");
        if(!DisableForProbe&&AiAccelerationPolicy.CanUseAndroidKleidiAI(arm64,hwcap,hwcap2)&&File.Exists(optimized)
            &&NativeLibrary.TryLoad(optimized,out _handle))
        {
            Description="CPU KleidiAI (NEON dotprod/I8MM)";
            return;
        }
        _handle=NativeLibrary.Load(Path.Combine(directory,"libggml-cpu.so"));
        Description="CPU baseline";
    }

    [DllImport("libc",EntryPoint="getauxval",CallingConvention=CallingConvention.Cdecl)]
    private static extern nuint GetAuxValue(nuint type);
}
#endif
