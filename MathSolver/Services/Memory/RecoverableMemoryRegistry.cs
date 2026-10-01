using System.Diagnostics;

namespace MathSolver.Services;

public interface IRecoverableMemoryOwner
{
    /// <returns>Managed bytes whose owning references have been released.</returns>
    Task<long> ReleaseRecoverableMemoryAsync();
}

public interface ITemporaryResourceOwner
{
    void ReleaseTemporaryResources();
}

/// <summary>Weak registrations never extend the lifetime of a page or model.</summary>
public sealed class RecoverableMemoryRegistry
{
    private readonly List<WeakReference<IRecoverableMemoryOwner>> _owners = [];
    private readonly SemaphoreSlim _gate = new(1, 1);

    public void Register(IRecoverableMemoryOwner owner)
    {
        lock (_owners)
        {
            _owners.RemoveAll(reference => !reference.TryGetTarget(out _));
            if (!_owners.Any(reference => reference.TryGetTarget(out var target) && ReferenceEquals(target, owner)))
                _owners.Add(new(owner));
        }
    }

    public async Task<long> ReleaseAsync()
    {
        await _gate.WaitAsync();
        try
        {
            WeakReference<IRecoverableMemoryOwner>[] owners;
            lock (_owners)
            {
                _owners.RemoveAll(reference => !reference.TryGetTarget(out _));
                owners = _owners.ToArray();
            }
            // Unload idle native weights promptly while an independent result
            // archive is streaming to disk. Each owner serializes its own work.
            long[] released = await Task.WhenAll(owners.Select(ReleaseOwnerAsync));
            return released.Sum();
        }
        finally { _gate.Release(); }
    }

    public void ReleaseTemporaryResources()
    {
        WeakReference<IRecoverableMemoryOwner>[] owners;
        lock (_owners) { owners = _owners.ToArray(); }
        foreach (var reference in owners)
        {
            if (!reference.TryGetTarget(out var owner) || owner is not ITemporaryResourceOwner temporary) continue;
            try { temporary.ReleaseTemporaryResources(); }
            catch (Exception error) { Debug.WriteLine($"Temporary cleanup postponed: {error.Message}"); }
        }
    }

    private static async Task<long> ReleaseOwnerAsync(WeakReference<IRecoverableMemoryOwner> reference)
    {
        if (!reference.TryGetTarget(out var owner)) return 0;
        try { return Math.Max(0, await owner.ReleaseRecoverableMemoryAsync()); }
        catch (Exception error)
        {
            Debug.WriteLine($"Memory cleanup postponed: {error.Message}");
            return 0;
        }
    }
}
