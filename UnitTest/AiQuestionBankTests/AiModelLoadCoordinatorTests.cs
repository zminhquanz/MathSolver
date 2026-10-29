using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static class AiModelLoadCoordinatorTests
{
    internal static async Task RunAsync()
    {
        var calls=new List<string>();var gpu=new Resource(()=>calls.Add("dispose-gpu"));var cpu=new Resource(()=>calls.Add("dispose-cpu"));
        var result=await AiModelLoadCoordinator.LoadAsync(true,(useGpu,_)=>
        {
            calls.Add(useGpu?"load-gpu":"load-cpu");
            if(!useGpu)TestAssert.That(gpu.Disposed,"GPU weights must be released before allocating CPU fallback.");
            return Task.FromResult(useGpu?gpu:cpu);
        },_=>{calls.Add("context-gpu");throw new OutOfMemoryException("KV allocation");},e=>e is OutOfMemoryException,CancellationToken.None);
        TestAssert.That(ReferenceEquals(result.Resource,cpu)&&!result.UsesGpu&&result.GpuFailure is OutOfMemoryException
            &&calls.SequenceEqual(new[]{"load-gpu","context-gpu","dispose-gpu","load-cpu"})&&!cpu.Disposed,
            "Context allocation failure must dispose GPU weights and transfer ownership of successful CPU model.");
        result.Resource.Dispose();
        using var cancelled=new CancellationTokenSource();var pending=new Resource(()=>calls.Add("dispose-cancelled"));int retries=0;
        bool stopped=false;
        try
        {
            await AiModelLoadCoordinator.LoadAsync(true,(useGpu,_)=>{if(!useGpu)retries++;return Task.FromResult(pending);},
                _=>{cancelled.Cancel();throw new OutOfMemoryException();},_=>true,cancelled.Token);
        }
        catch(OperationCanceledException){stopped=true;}
        TestAssert.That(stopped&&pending.Disposed&&retries==0,"Cancellation must release partial weights without starting CPU fallback.");
        int contexts=0;var direct=new Resource(()=>{});
        var loaded=await AiModelLoadCoordinator.LoadAsync(false,(_,_)=>Task.FromResult(direct),_=>contexts++,_=>true,CancellationToken.None);
        TestAssert.That(!loaded.UsesGpu&&loaded.GpuFailure is null&&contexts==0&&!direct.Disposed,"CPU mode must not allocate a GPU context.");loaded.Resource.Dispose();
        var failure=new Resource(()=>{});bool invalidStopped=false;
        try{await AiModelLoadCoordinator.LoadAsync(true,(_,_)=>Task.FromResult(failure),_=>throw new InvalidDataException(),_=>false,CancellationToken.None);}
        catch(InvalidDataException){invalidStopped=true;}
        TestAssert.That(invalidStopped&&failure.Disposed,"Nonrecoverable errors must dispose weights and not silently fallback.");
        Console.WriteLine("PASS GPU context fallback: ordered disposal before CPU allocation, cancellation, CPU-only path and nonrecoverable cleanup.");
    }

    private sealed class Resource(Action release):IDisposable
    {
        internal bool Disposed {get;private set;}
        public void Dispose(){TestAssert.That(!Disposed,"Native resource disposed twice.");Disposed=true;release();}
    }
}
