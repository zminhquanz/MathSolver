using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>App lifetime, independent of Shell pages and their handlers.</summary>
public sealed class AiQuestionBank
{
    private static readonly Lazy<AiQuestionBank> Instance = new(() => new());
    public static AiQuestionBank Current => Instance.Value;
    public GgufQuestionRuntime Runtime { get; } = new();
    public AiModelLibrary Models { get; } = new(Path.Combine(FileSystem.AppDataDirectory, "QuestionModels"));
    public QuestionBankStore Store { get; } = new(Path.Combine(FileSystem.AppDataDirectory, "QuestionBank", "questions.db3"));
    public AiQuestionGenerationService Generation { get; }
    public BasicPracticeQuestionProvider Practice { get; }
    public bool IsManaging { get; private set; }
    public string ManagementStatus { get; private set; } = "";
    public string? ManagementError { get; private set; }
    public AiDownloadProgress? DownloadProgress { get; private set; }
    public event EventHandler? Changed;
    private CancellationTokenSource? _managementCancellation;
    private readonly object _managementLock = new();
    private AiQuestionBank()
    {
        Generation = new(Runtime, Store);
        Practice = new(Store);
    }

    public async Task ManageAsync(string status, Func<CancellationToken, Task> action)
    {
        lock (_managementLock)
        {
            if (IsManaging || Generation.IsRunning) throw new InvalidOperationException("JobAlreadyRunning");
            _managementCancellation?.Dispose();
            _managementCancellation = new();
            IsManaging = true;
            ManagementStatus = status;
            ManagementError = null;
            DownloadProgress = null;
        }
        Changed?.Invoke(this, EventArgs.Empty);
        try { await action(_managementCancellation.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) { ManagementStatus = "Stopped"; }
        catch (Exception error) { ManagementError = error.Message; }
        finally
        {
            IsManaging = false;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public IProgress<AiDownloadProgress> CreateDownloadProgress() => new Progress<AiDownloadProgress>(value =>
    { DownloadProgress = value; Changed?.Invoke(this, EventArgs.Empty); });
    public void CancelManagement() => _managementCancellation?.Cancel();

    public static void StopForBackground()
    {
        // Switching app tabs never calls this. Android may suspend/kill a process
        // after Home/lock; finish already committed rows and discard partial JSON.
        if (Instance.IsValueCreated && OperatingSystem.IsAndroid())
        {
            Instance.Value.Generation.Stop();
            Instance.Value.CancelManagement();
        }
    }
}
