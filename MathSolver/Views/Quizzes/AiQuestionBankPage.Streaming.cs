using MathSolver.Services.QuestionBank;
using System.Globalization;

namespace MathSolver.Views;

public partial class AiQuestionBankPage
{
    private void RenderGenerationStatus(AiJobSnapshot snapshot, bool busy)
    {
        ModelBusyIndicator.IsRunning = ModelBusyIndicator.IsVisible = busy;
        var active = snapshot.Items.LastOrDefault();
        var attempt = active?.Attempts.LastOrDefault();
        ModelTokenSpeedLabel.IsVisible = !_bank.IsManaging && attempt is not null;
        ModelTokenSpeedLabel.Text = attempt?.Metrics?.TokensPerSecond is double speed
            ? string.Format(CultureInfo.CurrentCulture, T("GenerationSpeed"), speed)
            : T("GenerationSpeedWaiting");
        string? error = null;
        string status;
        if (_bank.IsManaging)
            status = T(_bank.ManagementStatus);
        else
        {
            error = snapshot.State == AiJobState.Idle ? _bank.ManagementError
                : active?.Error ?? snapshot.Error;
            if (snapshot.IsRunning && active is not null)
            {
                bool retrying = active.Error is not null && snapshot.State == AiJobState.Generating;
                status = string.Format(CultureInfo.CurrentCulture, T("GenerationPhase"), active.Number,
                    snapshot.Options?.Count ?? 1, active.Attempts.LastOrDefault()?.Number ?? 1,
                    T(retrying ? "GenerationRetrying" : "Job." + snapshot.State));
            }
            else status = T("Job." + snapshot.State);
            if (error is not null)
            {
                status += "\n" + string.Format(CultureInfo.CurrentCulture, T("GenerationIssue"), ErrorText(error));
                if (snapshot.Error is not null && snapshot.Error != error)
                    status += "\n" + ErrorText(snapshot.Error);
            }
        }
        ModelGenerationStatusLabel.Text = status;
        ModelGenerationStatusLabel.SetDynamicResource(Label.TextColorProperty,
            error is not null ? "DangerColor" : busy ? "PrimaryColor" : "TextSecondaryColor");
    }
}
