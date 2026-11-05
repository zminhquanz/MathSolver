using LLama.Common;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Reflection;

internal static class AiAccelerationRuntimeTests
{
    internal static void Run()
    {
        var eligible=typeof(AiAccelerationPolicy).GetMethod("CanUseAndroidKleidiAI",BindingFlags.Static|BindingFlags.Public);
        TestAssert.That(eligible is not null,"Native KleidiAI must be gated by runtime CPU features.");
        const ulong fp16=(1UL<<9)|(1UL<<10),dotprod=1UL<<20,i8mm=1UL<<13;
        const ulong arm82=(1UL<<7)|(1UL<<8)|(1UL<<12),required=arm82|fp16|dotprod;
        TestAssert.That((bool)eligible!.Invoke(null,[true,required,i8mm])!,"Eligible ARM64 CPU should use the optimized variant.");
        foreach(var sample in new[]{(false,required,i8mm),(true,arm82|fp16,i8mm),(true,arm82|dotprod,i8mm),(true,required,0UL)})
            TestAssert.That(!(bool)eligible.Invoke(null,[sample.Item1,sample.Item2,sample.Item3])!,
                "Unsupported architecture/ISA must retain the baseline CPU backend.");
        foreach(int bit in new[]{7,8,9,10,12,20})
            TestAssert.That(!(bool)eligible.Invoke(null,[true,required&~(1UL<<bit),i8mm])!,
                "All extensions enabled by the variant's ARMv8.2 target must be present.");
        var create=typeof(GgufQuestionRuntime).GetMethod("CreateModelParameters",BindingFlags.Static|BindingFlags.NonPublic);
        TestAssert.That(create is not null,"AI runtime must apply the chosen mode to model/context parameters.");
        var cpu=(ModelParams)create!.Invoke(null,["sample.gguf",AiComputeMode.Cpu,"CUDA0",false])!;
        TestAssert.That(cpu.GpuLayerCount==0&&cpu.NoKqvOffload&&cpu.OpOffload==false,
            "CPU selection must disable model, KV and operation offload even in a GPU-capable native library.");
        TestAssert.That(cpu.Devices is {Count:1}&&cpu.Devices[0]=="CPU","CPU selection must constrain model device placement.");
        var gpu=(ModelParams)create.Invoke(null,["sample.gguf",AiComputeMode.Gpu,"Vulkan0",false])!;
        TestAssert.That(gpu.GpuLayerCount>0&&!gpu.NoKqvOffload&&gpu.OpOffload==true
            &&gpu.Devices is {Count:1}&&gpu.Devices[0]=="Vulkan0","GPU selection must use the registered selected backend device.");
        var unavailable=(ModelParams)create.Invoke(null,["sample.gguf",AiComputeMode.Gpu,null,false])!;
        TestAssert.That(unavailable.GpuLayerCount==0&&unavailable.NoKqvOffload&&unavailable.OpOffload==false,
            "Missing GPU device must produce honest CPU parameters rather than unverified offload.");
        foreach(var mode in new[]{AiComputeMode.Cpu,AiComputeMode.Gpu})
        foreach(string? device in new string?[]{null,"CPU","Vulkan0","CUDA0"})
        {
            var android=(ModelParams)create.Invoke(null,["sample.gguf",mode,device,true])!;
            TestAssert.That(android.GpuLayerCount==0&&android.NoKqvOffload&&android.OpOffload==false
                &&android.Devices is {Count:1}&&android.Devices[0]=="CPU"&&android.UBatchSize==128,
                "Android must force CPU for stale/explicit GPU requests, including KV and operations.");
        }
        var runtime=new GgufQuestionRuntime();
        TestAssert.That((AiGpuBackend)typeof(GgufQuestionRuntime).GetProperty("ActiveBackend")!.GetValue(runtime)! == AiGpuBackend.Cpu,
            "An unloaded model must not report an active GPU.");
        Console.WriteLine("PASS AI runtime parameter isolation: CPU disables all offload, GPU binds registered device, unavailable GPU uses CPU.");
    }
}
