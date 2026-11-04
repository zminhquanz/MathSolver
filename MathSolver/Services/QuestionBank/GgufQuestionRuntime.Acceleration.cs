using LLama;
using LLama.Abstractions;
using LLama.Common;
using LLama.Exceptions;
using LLama.Native;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

public sealed partial class GgufQuestionRuntime
{
    private static readonly object NativeConfigurationLock=new();
    private static bool _nativeConfigured;
    private static long _nativeLogSequence;
    internal static Action<string>? NativeDiagnosticObserver {get;set;}
    private static readonly ConcurrentQueue<(long Sequence,string Text)> NativeMessages=new();
    // KleidiAI selects these once per process; model logs can evict them from
    // the general bounded queue before a subsequent model load.
    private static readonly ConcurrentQueue<string> NativeCpuKernelMessages=new();
    private AiComputeMode _loadedMode;
    public AiGpuBackend ActiveBackend {get;private set;}=AiGpuBackend.Cpu;
    public int GpuLayerCount {get;private set;}
    public string ActiveDeviceName {get;private set;}="";
    public string AccelerationFallbackReason {get;private set;}="";
    public IReadOnlyList<string> NativeLoadDiagnostics {get;private set;}=[];
    public IReadOnlyList<AiNativeGpuDevice> BackendDevices {get;private set;}=[];
    public AiCpuRuntimeReport? CpuRuntimeReport {get;private set;}
    public string NativeCpuVariant =>
#if ANDROID
        AndroidAiCpuBackend.Description;
#else
        "Packaged CPU";
#endif

    public void ClearAccelerationFallback()=>AccelerationFallbackReason="";

    private static void ConfigureNativeAcceleration()
    {
        lock(NativeConfigurationLock)
        {
            if(_nativeConfigured)return;
            // Native DLL selection cannot be changed after any NativeApi use.
            // Existing external hosts may already have configured/loaded it.
            if(!NativeLibraryConfig.LLama.LibraryHasLoaded)
            {
                NativeLibraryConfig.All.WithLogCallback((level,text)=>
                {
                    NativeMessages.Enqueue((Interlocked.Increment(ref _nativeLogSequence),text));
                    if(text.StartsWith("kleidiai: primary ",StringComparison.Ordinal))
                    {
                        NativeCpuKernelMessages.Enqueue(text);
                        while(NativeCpuKernelMessages.Count>16)NativeCpuKernelMessages.TryDequeue(out _);
                    }
                    try {NativeDiagnosticObserver?.Invoke(text);} catch { }
                    while(NativeMessages.Count>4096)NativeMessages.TryDequeue(out _);
                    Debug.WriteLine("[LLama] "+text);
                });
#if ANDROID
                string directory=Android.App.Application.Context.ApplicationInfo!.NativeLibraryDir!;
                System.Runtime.InteropServices.NativeLibrary.Load(Path.Combine(directory,"libggml-base.so"));
                AndroidAiCpuBackend.Load(directory);
                System.Runtime.InteropServices.NativeLibrary.Load(Path.Combine(directory,"libggml.so"));
                NativeLibraryConfig.LLama.WithLibrary(Path.Combine(directory,"libllama.so"));
                NativeLibraryConfig.Mtmd.WithLibrary(Path.Combine(directory,"libmtmd.so"));
                NativeLibraryConfig.All.WithCuda(false).WithVulkan(false);
#else
                var preferred=AiAccelerationManager.PreferredBackend;
                NativeLibraryConfig.All.WithCuda(preferred==AiGpuBackend.Cuda)
                    .WithVulkan(preferred is AiGpuBackend.Cuda or AiGpuBackend.Vulkan)
                    .WithAutoFallback(true).WithSelectingPolicy(new PackagedWindowsBackendPolicy());
#endif
            }
            _nativeConfigured=true;
        }
    }

    // The default loader only tries the installed toolkit's CUDA major. The
    // app ships CUDA12, so try that package even without a toolkit or when a
    // newer toolkit is installed. Never call DryRun: in 0.30 it locks config.
    private sealed class PackagedWindowsBackendPolicy:INativeLibrarySelectingPolicy
    {
        public IEnumerable<INativeLibrary> Apply(NativeLibraryConfig.Description description,LLama.Native.SystemInfo systemInfo,
            NativeLogConfig.LLamaLogCallback? logCallback)
        {
            if(OperatingSystem.IsWindows()&&description.UseCuda)
                yield return new NativeLibraryWithCuda(12,description.Library,description.AvxLevel,false);
            foreach(var library in new DefaultNativeLibrarySelectingPolicy().Apply(description,systemInfo,logCallback))
                yield return library;
        }
    }

    internal static ModelParams CreateModelParameters(string path,AiComputeMode mode,string? device,bool? android=null)
    {
        bool gpu=!(android??OperatingSystem.IsAndroid())&&mode==AiComputeMode.Gpu&&!string.IsNullOrWhiteSpace(device)
            &&(device.StartsWith("CUDA",StringComparison.OrdinalIgnoreCase)||device.StartsWith("Vulkan",StringComparison.OrdinalIgnoreCase));
        return new(path)
        {
            ContextSize=MaximumContextTokens,BatchSize=256,UBatchSize=128,
            GpuLayerCount=gpu?int.MaxValue:0,NoKqvOffload=!gpu,OpOffload=gpu,
            Devices=[gpu?device!:"CPU"],SplitMode=GPUSplitMode.None,
            LoadMode=LLamaLoadMode.MemoryMap,Embeddings=false,SwaFull=false,
            Threads=GetInferenceThreadCount(Environment.ProcessorCount),BatchThreads=GetInferenceThreadCount(Environment.ProcessorCount)
        };
    }

    private async Task LoadForComputeModeAsync(string path,CancellationToken cancellationToken)
    {
        var requested=AiAccelerationManager.Mode;
        AccelerationFallbackReason="";
        long start=Volatile.Read(ref _nativeLogSequence);
        _=NativeApi.llama_supports_gpu_offload();
        var library=NativeApi.GetLoadedNativeLibrary(NativeLibraryName.LLama);
        var available=RegisteredDevices();
        BackendDevices=available;
        var nativeBackend=library?.Metadata?.UseCuda==true?AiGpuBackend.Cuda
            :library?.Metadata?.UseVulkan==true?AiGpuBackend.Vulkan:AiGpuBackend.Cpu;
        string? gpuDevice=requested!=AiComputeMode.Gpu?null:AiAccelerationPolicy.SelectNativeDevice(available,nativeBackend,AiAccelerationManager.AdapterName)?.Name;
        var parameters=CreateModelParameters(path,requested,gpuDevice);
        if(requested==AiComputeMode.Gpu&&gpuDevice is null)
            AccelerationFallbackReason="GpuBackendOrDeviceUnavailable";
        LLamaWeights? weights=null;
        try
        {
            var loaded=await AiModelLoadCoordinator.LoadAsync(gpuDevice is not null,
                (gpu,token)=>
                {
                    parameters=CreateModelParameters(path,gpu?AiComputeMode.Gpu:AiComputeMode.Cpu,gpu?gpuDevice:null);
                    return LLamaWeights.LoadFromFileAsync(parameters,token);
                },model=>
                {
                    // Allocate the same maximum context/KV/scratch budget used
                    // by generation before claiming that GPU use is available.
                    using var preflight=model.CreateContext(parameters);
                },error=>error is LoadWeightsFailedException or RuntimeError or OutOfMemoryException,cancellationToken).ConfigureAwait(false);
            weights=loaded.Resource;
            if(loaded.GpuFailure is { } error)
            {
                AccelerationFallbackReason="GpuModelLoadFailed: "+error.GetType().Name;
                gpuDevice=null;
            }
            cancellationToken.ThrowIfCancellationRequested();
            string[] diagnostics=NativeMessages.Where(x=>x.Sequence>start).Select(x=>x.Text).ToArray();
            if(gpuDevice is not null)
            {
                int layers=ReadOffloadedLayers(diagnostics);
                bool gpuBuffer=diagnostics.Any(text=>
                {
                    var match=Regex.Match(text,@"\b"+Regex.Escape(gpuDevice)+@"\s+model buffer size\s*=\s*([0-9.]+)",
                        RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
                    return match.Success&&double.TryParse(match.Groups[1].Value,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out double size)&&size>0;
                });
                if(layers>0&&gpuBuffer)
                {
                    ActiveBackend=gpuDevice.StartsWith("CUDA",StringComparison.OrdinalIgnoreCase)?AiGpuBackend.Cuda:AiGpuBackend.Vulkan;
                    GpuLayerCount=layers;ActiveDeviceName=gpuDevice;
                }
                else
                {
                    // A library with GPU capability is not proof of model
                    // placement. Release uncertain allocations and load CPU.
                    weights.Dispose();weights=null;
                    AccelerationFallbackReason="GpuOffloadNotConfirmed";
                    parameters=CreateModelParameters(path,AiComputeMode.Cpu,null);
                    weights=await LLamaWeights.LoadFromFileAsync(parameters,cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            if(GpuLayerCount==0){ActiveBackend=AiGpuBackend.Cpu;ActiveDeviceName="CPU";}
            NativeLoadDiagnostics=NativeMessages.Where(x=>x.Sequence>start).Select(x=>x.Text).ToArray();
            CaptureCpuRuntimeReport();
            _loadedMode=requested;_parameters=parameters;_weights=weights;weights=null;
        }
        finally {weights?.Dispose();}
    }

    private void CaptureCpuRuntimeReport()
    {
        try
        {
            string info;
            // Native returns a shared string. Copy on the inference worker;
            // hardware-page rendering must never initialize or call native code.
            lock(NativeConfigurationLock)
                info=System.Runtime.InteropServices.Marshal.PtrToStringUTF8(NativeApi.llama_print_system_info())??"";
            CpuRuntimeReport=AiCpuRuntimeReport.Create(info,NativeCpuKernelMessages.Concat(NativeLoadDiagnostics));
        }
        catch(Exception error) when(error is EntryPointNotFoundException or DllNotFoundException or RegexMatchTimeoutException)
        {
            CpuRuntimeReport=null;
            Debug.WriteLine("[AI CPU runtime] Feature information unavailable: "+error.Message);
        }
    }

    private static AiNativeGpuDevice[] RegisteredDevices()
    {
        int count=checked((int)NativeApi.ggml_backend_dev_count());
        if(count is <0 or >1024)throw new InvalidOperationException("InvalidNativeDeviceCount");
        var devices=new List<AiNativeGpuDevice>();
        for(int index=0;index<count;index++)
        {
            var device=NativeApi.ggml_backend_dev_get((nuint)index);
            string? name=System.Runtime.InteropServices.Marshal.PtrToStringUTF8(NativeApi.ggml_backend_dev_name(device));
            if(!string.IsNullOrWhiteSpace(name))devices.Add(
#if ANDROID
                new(name,"CPU",0));
#else
                WindowsAiNativeDeviceInspector.Describe(name,device));
#endif
        }
        return devices.ToArray();
    }

    private static int ReadOffloadedLayers(IEnumerable<string> diagnostics)
    {
        int count=0;
        foreach(string text in diagnostics)
        {
            var match=Regex.Match(text,@"offloaded\s+(\d+)\s*/\s*\d+\s+layers to GPU",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
            if(match.Success&&int.TryParse(match.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out int value))count=Math.Max(count,value);
        }
        return count;
    }
}
