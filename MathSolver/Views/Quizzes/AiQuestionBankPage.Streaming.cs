using MathSolver.Services.Appearance;
using MathSolver.Services.QuestionBank;
using System.Globalization;

namespace MathSolver.Views;

public partial class AiQuestionBankPage
{
    private AiPresentation ResolvePresentation(AiJobSnapshot snapshot, int pending)
    {
        var active = snapshot.Items.LastOrDefault();
        return AiPresentationPolicy.Resolve(new()
        {
            Managing = _bank.IsManaging, Running = snapshot.IsRunning,
            DeletingAll = _bank.Generation.IsDeletingAll, Saving = _saving, ConfirmingStop = _confirmingStop,
            CanGenerate = _bank.Runtime.CanGenerate, Loaded = _bank.Runtime.IsLoaded,
            HasAttempt = active?.Attempts.LastOrDefault() is not null,
            ItemCount = snapshot.Items.Count, Pending = pending, ActiveNumber = active?.Number,
            JobState = snapshot.State.ToString(), ManagementStatus = _bank.ManagementStatus,
            ManagementError = _bank.ManagementError, ActiveError = active?.Error, JobError = snapshot.Error
        });
    }

    private void RenderGenerationStatus(AiJobSnapshot snapshot, AiPresentation presentation)
    {
        ModelBusyIndicator.IsRunning = ModelBusyIndicator.IsVisible = presentation.Busy;
        var active = snapshot.Items.LastOrDefault();
        var attempt = active?.Attempts.LastOrDefault();
        ModelTokenSpeedLabel.IsVisible = presentation.ShowSpeed;
        ModelTokenSpeedLabel.Text = attempt?.Metrics?.TokensPerSecond is double speed
            ? string.Format(CultureInfo.CurrentCulture, T("GenerationSpeed"), speed)
            : T("GenerationSpeedWaiting");
        string status = presentation.IsPhase
            ? string.Format(CultureInfo.CurrentCulture, T("GenerationPhase"), active!.Number,
                snapshot.Options?.Count ?? 1, attempt?.Number ?? 1, T(presentation.LiveStatusKey))
            : T(presentation.LiveStatusKey);
        if (presentation.Error is not null)
            status += "\n" + string.Format(CultureInfo.CurrentCulture, T("GenerationIssue"), ErrorText(presentation.Error));
        if (presentation.AdditionalError is not null)
            status += "\n" + ErrorText(presentation.AdditionalError);
        ModelGenerationStatusLabel.Text = status;
        ModelLiveStatusPanel.IsVisible = presentation.ShowLiveStatus;
        ModelGenerationStatusLabel.SetDynamicResource(Label.TextColorProperty, presentation.ColorResource);
    }
}
