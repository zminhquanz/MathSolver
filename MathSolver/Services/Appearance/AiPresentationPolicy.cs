namespace MathSolver.Services.Appearance;
public sealed record AiPresentationInput
{
    public bool Managing { get; init; }
    public bool Running { get; init; }
    public bool DeletingAll { get; init; }
    public bool Saving { get; init; }
    public bool ConfirmingStop { get; init; }
    public bool CanGenerate { get; init; }
    public bool Loaded { get; init; }
    public bool HasAttempt { get; init; }
    public int ItemCount { get; init; }
    public int Pending { get; init; }
    public int? ActiveNumber { get; init; }
    public string JobState { get; init; } = "Idle";
    public string ManagementStatus { get; init; } = "Loading";
    public string? ManagementError { get; init; }
    public string? ActiveError { get; init; }
    public string? JobError { get; init; }
}
public sealed record AiPresentation(bool Busy, bool CanGenerate, bool CanStop, bool ShowSpeed,
    bool ShowLiveStatus, bool ShowJobStatus, string WorkflowKey, string LiveStatusKey,
    bool IsPhase, string? Error, string? AdditionalError, string ColorResource)
{
    public string ModelStatusKey { get; init; } = "";
    public string? ManagementStatusKey { get; init; }
}
public static class AiPresentationPolicy
{
    public static AiPresentation Resolve(AiPresentationInput input)
    {
        bool busy = input.Running || input.Managing || input.DeletingAll;
        string? error = input.Managing ? null : input.JobState == "Idle"
            ? input.ManagementError : input.ActiveError ?? input.JobError;
        bool phase = !input.Managing && input.Running && input.ActiveNumber.HasValue;
        string status = input.Managing ? input.ManagementStatus
            : phase && input.ActiveError is not null && input.JobState == "Generating" ? "GenerationRetrying"
            : !phase && input.Running && !input.Loaded ? "Loading" : "Job." + input.JobState;
        string workflow = input.Managing ? "Workflow.Managing" : input.Running ? "Workflow.Generating"
            : input.Pending > 0 ? "Workflow.Review" : input.CanGenerate ? "Workflow.Ready" : "Workflow.NeedModel";
        return new(busy, !busy && !input.Saving && input.CanGenerate,
            input.Running && !input.ConfirmingStop, !input.Managing && input.HasAttempt,
            busy || error is not null || input.HasAttempt,
            input.Running || input.ItemCount > 0 || input.JobError is not null, workflow, status, phase,
            error, error is not null && input.JobError is not null && input.JobError != error ? input.JobError : null,
            error is not null ? "ContentDangerTextColor" : busy ? "ContentAccentTextColor" : "WallpaperTextSecondaryColor")
        {
            ModelStatusKey = input.Loaded ? "Loaded" : input.CanGenerate ? "SelectedModel" : "NoModel",
            ManagementStatusKey = input.ManagementError is not null ? "ModelActionFailed"
                : input.Managing ? input.ManagementStatus : null
        };
    }
}
