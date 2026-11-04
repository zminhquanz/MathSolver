using System.Reflection;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static class AiCpuRuntimeReportTests
{
    internal static void Run()
    {
        var type=typeof(AiAccelerationPolicy).Assembly.GetType("MathSolver.Services.QuestionBank.AiCpuRuntimeReport");
        TestAssert.That(type is not null,"CPU runtime report must describe native evidence rather than host CPU capability.");
        object Report(string info,params string[] logs)=>type!.GetMethod("Create")!.Invoke(null,[info,logs])!;
        string Value(object report,string name)=>(string)type!.GetProperty(name)!.GetValue(report)!;
        bool Flag(object report,string name)=>(bool)type!.GetProperty(name)!.GetValue(report)!;
        string[] Features(object report)=>((IEnumerable<string>)type!.GetProperty("Features")!.GetValue(report)!).ToArray();
        var avx2=Report("Vulkan : AVX512 = 1 | CPU : AVX = 1 | AVX2 = 1 | AVX512 = 0 | FMA = 1 | REPACK = 1 |");
        TestAssert.That(Value(avx2,"InstructionSet")=="AVX2 (256-bit)"&&!Features(avx2).Contains("AVX-512"),
            "GPU sections and disabled AVX-512 must not upgrade the CPU runtime.");
        var avx512=Report("CPU : AVX2 = 1 | AVX512 = 1 | Vulkan : AVX512 = 0 |");
        TestAssert.That(Value(avx512,"InstructionSet")=="AVX-512 (512-bit)","Native CPU AVX-512 must be recognized.");
        var arm=Report("CPU : NEON = 1 | DOTPROD = 1 | MATMUL_INT8 = 1 | KLEIDIAI = 1 |",
            "kleidiai: primary q4 kernel feature I8MM\nkleidiai: primary q8 kernel feature I8MM\nkleidiai: SME disabled\n",
            "load_tensors: CPU_KLEIDIAI model buffer size = 2116.43 MiB");
        TestAssert.That(Flag(arm,"UsesKleidiAI")&&Value(arm,"Backend")=="ARM KleidiAI"
            &&Value(arm,"KernelSelection")=="Q4: I8MM · Q8: I8MM", "Selected I8MM kernels require actual KleidiAI tensor buffers.");
        TestAssert.That(Features(arm).SequenceEqual(new[]{"NEON","DotProd","I8MM"})
            &&Value(arm,"UnreportedArmFeatures")=="SME · SME2 · SVE · SVE2",
            "Do not infer missing ARM features from CPU names or a compiled KleidiAI option.");
        foreach(string feature in new[]{"SME","SME2","SVE","SVE2","DOTPROD","NEON","I8MM"})
        {
            var sample=Report($"CPU : KLEIDIAI = 1 | {feature} = 1 |",$"kleidiai: primary q4 kernel feature {feature}",
                "load_tensors: CPU_KLEIDIAI model buffer size = 1.00 MiB");
            TestAssert.That(Value(sample,"KernelSelection").Contains(feature=="DOTPROD"?"DotProd":feature,StringComparison.Ordinal),
                "Preserve explicit native ARM kernel family selection.");
        }
        foreach(string log in new[]{"CPU_KLEIDIAI model buffer size = 0.00 MiB","Vulkan0 model buffer size = 100 MiB",""})
        {
            var sample=Report("CPU : KLEIDIAI = 1 | NEON = 1 |","kleidiai: primary q4 kernel feature I8MM",log);
            TestAssert.That(!Flag(sample,"UsesKleidiAI")&&Value(sample,"KernelSelection")=="",
                "Library/selected kernel alone is not model tensor-placement proof.");
        }
        var unavailable=Report("CUDA : AVX512 = 1 |", "CPU_KLEIDIAI model buffer size = 100 MiB");
        TestAssert.That(!Flag(unavailable,"HasCpuFeatures")&&!Flag(unavailable,"UsesKleidiAI")
            &&Value(unavailable,"InstructionSet")=="","Unknown CPU evidence must stay unknown.");
        var sve=Report("CPU : SVE = 1 | SME = 1 | NEON = 1 |");
        TestAssert.That(!Features(sve).Contains("SVE2")&&!Features(sve).Contains("SME2"),"No implied ARM second-generation feature support.");
        var runtime=new GgufQuestionRuntime();
        TestAssert.That(typeof(GgufQuestionRuntime).GetProperty("CpuRuntimeReport")!.GetValue(runtime) is null,
            "Opening hardware information before native initialization must not invent a runtime.");
        var pending=AiCpuRuntimePresentation.Format(null,false,true);
        TestAssert.That(pending.Details.Contains("Chưa khởi tạo",StringComparison.Ordinal),"VI pending state must be clear.");
        var last=AiCpuRuntimePresentation.Format((AiCpuRuntimeReport)arm,false,true);
        var current=AiCpuRuntimePresentation.Format((AiCpuRuntimeReport)avx2,true,false);
        TestAssert.That(last.Title.Contains("lần nạp gần nhất",StringComparison.Ordinal)&&last.Details.Contains("Q4: I8MM",StringComparison.Ordinal)
            &&current.Title.Contains("AVX2 (256-bit)",StringComparison.Ordinal)&&!current.Title.Contains("last model load",StringComparison.Ordinal),
            "Localize current/last-load information without dropping selected kernels.");
        typeof(GgufQuestionRuntime).GetProperty("CpuRuntimeReport")!.SetValue(runtime,arm);
        typeof(GgufQuestionRuntime).GetMethod("DisposeWeights",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(runtime,null);
        TestAssert.That(ReferenceEquals(arm,runtime.CpuRuntimeReport),"Model release must retain the last successful runtime snapshot.");
        foreach(string text in new[]{"CPU : NEON = 1 | KLEIDIAI = 0 |","CPU : NEON = 1 |"})
            TestAssert.That(!AiCpuRuntimeReport.Create(text,["CPU_KLEIDIAI model buffer size = 1 MiB"]).UsesKleidiAI,
                "A stale buffer line cannot enable KleidiAI in a non-KleidiAI native backend.");
        Console.WriteLine("PASS native CPU runtime evidence: AVX2/AVX-512, CPU section isolation, ARM feature aliases and KleidiAI kernel/tensor proof.");
    }

    internal static void RunNative()
    {
        typeof(GgufQuestionRuntime).GetMethod("ConfigureNativeAcceleration",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);
        _=LLama.Native.NativeApi.llama_supports_gpu_offload();
        string info=System.Runtime.InteropServices.Marshal.PtrToStringUTF8(LLama.Native.NativeApi.llama_print_system_info())??"";
        var report=AiCpuRuntimeReport.Create(info,[]);
        TestAssert.That(report.HasCpuFeatures&&report.Features.Count>0,"The actual loaded native CPU feature table must be readable.");
        Console.WriteLine("NATIVE "+info);
        Console.WriteLine("REPORT "+AiCpuRuntimePresentation.Format(report,true,false).Title);
        Console.WriteLine("PASS actual native feature query without loading model weights.");
    }

    internal static void RunAndroidLog(string path)
    {
        // Expected feature-table flags for the existing pinned I8MM variant;
        // the kernel and positive tensor-buffer evidence is the real saved log.
        var report=AiCpuRuntimeReport.Create("CPU : NEON = 1 | DOTPROD = 1 | MATMUL_INT8 = 1 | KLEIDIAI = 1 |",File.ReadLines(path));
        TestAssert.That(report.UsesKleidiAI&&report.KernelSelection=="Q4: I8MM · Q8: I8MM",
            "Archived V2419A native log must retain actual Q4/Q8 selection and tensor placement.");
        Console.WriteLine(AiCpuRuntimePresentation.Format(report,false,true));
        Console.WriteLine("PASS archived Android native-log parser fixture (expected build flags, no new physical run).");
    }
}
