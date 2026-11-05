using MathSolver.Services.Appearance;

namespace MathPuzzleUnitTests;

internal static class AiPresentationPolicyTests
{
    public static void Run()
    {
        var idle = new AiPresentationInput();
        Check(AiPresentationPolicy.Resolve(idle).WorkflowKey == "Workflow.NeedModel", "Idle needs a model");
        Check(AiPresentationPolicy.Resolve(idle).ModelStatusKey == "NoModel", "Missing model caption");
        Check(AiPresentationPolicy.Resolve(idle with { CanGenerate = true }).ModelStatusKey == "SelectedModel", "Selected model caption");
        Check(AiPresentationPolicy.Resolve(idle with { Loaded = true, CanGenerate = true }).ModelStatusKey == "Loaded", "Loaded outranks selected");
        Check(AiPresentationPolicy.Resolve(idle with { Managing = true, ManagementError = "failure" }).ManagementStatusKey == "ModelActionFailed", "Management detail error outranks in-progress caption");
        var managing = AiPresentationPolicy.Resolve(idle with { Managing = true, Running = true, CanGenerate = true, ManagementError = "download", JobError = "job", ActiveError = "item", HasAttempt = true });
        Check(managing.WorkflowKey == "Workflow.Managing" && managing.LiveStatusKey == "Loading", "Management outranks generation");
        Check(managing.Error is null && !managing.ShowSpeed && managing.Busy && managing.ShowLiveStatus, "Management suppresses generation issue/speed");
        var retry = AiPresentationPolicy.Resolve(idle with { Running = true, JobState = "Generating", ActiveNumber = 2, ActiveError = "item", JobError = "job" });
        Check(retry.IsPhase && retry.LiveStatusKey == "GenerationRetrying" && retry.Error == "item" && retry.AdditionalError == "job", "Retry retains item and distinct job errors");
        Check(AiPresentationPolicy.Resolve(idle with { JobState = "Failed", ActiveError = "same", JobError = "same" }).AdditionalError is null, "Identical errors appear once");
        Check(AiPresentationPolicy.Resolve(idle with { ManagementError = "management", ActiveError = "item", JobError = "job" }).Error == "management", "Idle prioritizes management error");
        Check(AiPresentationPolicy.Resolve(idle with { Running = true }).LiveStatusKey == "Loading", "Unloaded running job without item is loading");
        Check(AiPresentationPolicy.Resolve(idle with { Running = true, Loaded = true }).LiveStatusKey == "Job.Idle", "Loaded running job retains job status");
        Check(AiPresentationPolicy.Resolve(idle with { CanGenerate = true, Pending = 1 }).WorkflowKey == "Workflow.Review", "Review outranks ready");
        Check(AiPresentationPolicy.Resolve(idle with { CanGenerate = true }).CanGenerate, "Selected model enables generate");
        foreach (var blocked in new[] { idle with { DeletingAll = true }, idle with { Saving = true }, idle with { Managing = true }, idle with { Running = true } })
            Check(!AiPresentationPolicy.Resolve(blocked with { CanGenerate = true }).CanGenerate, "Busy or saving blocks generate");
        var deleting = AiPresentationPolicy.Resolve(idle with { DeletingAll = true, CanGenerate = true });
        Check(deleting.Busy && deleting.WorkflowKey == "Workflow.Ready" && !deleting.ShowJobStatus, "Deletion does not masquerade as generation");
        Check(!AiPresentationPolicy.Resolve(idle with { Running = true, ConfirmingStop = true }).CanStop, "Confirmation blocks duplicate stop");
        Check(AiPresentationPolicy.Resolve(idle with { HasAttempt = true }).ShowSpeed, "Completed attempt retains speed");
        Check(AiPresentationPolicy.Resolve(idle with { JobError = "failure" }).ShowJobStatus, "Job failure remains visible without items");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
