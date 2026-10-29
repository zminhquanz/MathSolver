using LLama.Common;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Reflection;

internal static class AiAccelerationRuntimeTests
{
    internal static void Run()
    {
        var create=typeof(GgufQuestionRuntime).GetMethod("CreateModelParameters",BindingFlags.Static|BindingFlags.NonPublic);
        TestAssert.That(create is not null,"AI runtime must apply the chosen mode to model/context parameters.");
        var cpu=(ModelParams)create!.Invoke(null,["sample.gguf",AiComputeMode.Cpu,"CUDA0"])!;
        TestAssert.That(cpu.GpuLayerCount==0&&cpu.NoKqvOffload&&cpu.OpOffload==false,
            "CPU selection must disable model, KV and operation offload even in a GPU-capable native library.");
        TestAssert.That(cpu.Devices is {Count:1}&&cpu.Devices[0]=="CPU","CPU selection must constrain model device placement.");
        var gpu=(ModelParams)create.Invoke(null,["sample.gguf",AiComputeMode.Gpu,"Vulkan0"])!;
        TestAssert.That(gpu.GpuLayerCount>0&&!gpu.NoKqvOffload&&gpu.OpOffload==true
            &&gpu.Devices is {Count:1}&&gpu.Devices[0]=="Vulkan0","GPU selection must use the registered selected backend device.");
        var unavailable=(ModelParams)create.Invoke(null,["sample.gguf",AiComputeMode.Gpu,null])!;
        TestAssert.That(unavailable.GpuLayerCount==0&&unavailable.NoKqvOffload&&unavailable.OpOffload==false,
            "Missing GPU device must produce honest CPU parameters rather than unverified offload.");
        var runtime=new GgufQuestionRuntime();
        TestAssert.That((AiGpuBackend)typeof(GgufQuestionRuntime).GetProperty("ActiveBackend")!.GetValue(runtime)! == AiGpuBackend.Cpu,
            "An unloaded model must not report an active GPU.");
        Console.WriteLine("PASS AI runtime parameter isolation: CPU disables all offload, GPU binds registered device, unavailable GPU uses CPU.");
    }
}
