namespace MathSolver.Services.QuestionBank;

/// <summary>Owns partial native allocations until model and GPU context validation succeed.</summary>
internal static class AiModelLoadCoordinator
{
    internal static async Task<(T Resource,bool UsesGpu,Exception? GpuFailure)> LoadAsync<T>(bool useGpu,
        Func<bool,CancellationToken,Task<T>> load,Action<T> validateGpuContext,Func<Exception,bool> recoverable,
        CancellationToken cancellationToken) where T:class,IDisposable
    {
        T? resource=null;
        try
        {
            Exception? failure=null;
            try
            {
                resource=await load(useGpu,cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if(useGpu)validateGpuContext(resource);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch(Exception error) when(useGpu&&error is not OperationCanceledException
                &&!cancellationToken.IsCancellationRequested&&recoverable(error))
            {
                // Dispose model weights before retrying: KV/scratch allocation
                // can fail even when all model tensors fit device memory.
                resource?.Dispose();resource=null;
                failure=error;useGpu=false;
                resource=await load(false,cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            var result=(resource!,useGpu,failure);
            resource=null;
            return result;
        }
        catch(Exception) when(cancellationToken.IsCancellationRequested)
        {throw new OperationCanceledException(cancellationToken);}
        finally {resource?.Dispose();}
    }
}
