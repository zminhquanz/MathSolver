namespace MathSolver.Views;

public partial class AiQuestionBankPage
{
    private void RenderWorkflow(bool running, int pending)
    {
        WorkflowStatusLabel.Text = T(_bank.IsManaging ? "Workflow.Managing"
            : running ? "Workflow.Generating"
            : pending > 0 ? "Workflow.Review"
            : _bank.Runtime.CanGenerate ? "Workflow.Ready" : "Workflow.NeedModel");
        WorkflowNextButton.Text = T(_bank.IsManaging ? "Workflow.ViewModel"
            : running ? "Workflow.ViewPreview"
            : pending > 0 ? "Review.Open"
            : _bank.Runtime.CanGenerate ? "Workflow.ChooseProblem" : "Workflow.ViewModel");
        WorkflowNextButton.IsEnabled = !_saving && !_bank.Generation.IsDeletingAll;
        CancelModelButton.IsVisible = _bank.IsManaging;
        StopButton.IsVisible = running;
        Grid.SetColumnSpan(GenerateButton, running ? 1 : 2);
        JobStatusLabel.IsVisible = running || _bank.Generation.Snapshot.Items.Count > 0
            || _bank.Generation.Snapshot.Error is not null;
        RefreshWorkflowDisclosures();
    }

    private async void OnWorkflowNextClicked(object? sender, EventArgs e)
    {
        if (!_appeared || _saving || _bank.Generation.IsDeletingAll) return;
        if (!_bank.IsManaging && !_bank.Generation.IsRunning
            && _bank.Generation.Snapshot.Items.Any(item => item.State == MathSolver.Services.QuestionBank.AiItemState.PendingReview))
        {
            OnReviewClicked(sender, e);
            return;
        }
        VisualElement target = _bank.IsManaging || !_bank.Runtime.CanGenerate ? ModelCard
            : _bank.Generation.IsRunning ? PreviewCard : ProblemCard;
        // Navigation only. Generation always requires the explicit Generate action.
        if (WorkflowScroll.Handler is null || target.Handler is null) return;
        try { await WorkflowScroll.ScrollToAsync(target, ScrollToPosition.Start, false); }
        catch (Exception error) when (error is ObjectDisposedException or System.Runtime.InteropServices.COMException)
        { System.Diagnostics.Debug.WriteLine($"AI workflow navigation ended with its native view: {error}"); }
    }

    private void OnToggleModelToolsClicked(object? sender, EventArgs e)
    {
        ModelToolsPanel.IsVisible = !ModelToolsPanel.IsVisible;
        RefreshWorkflowDisclosures();
    }

    private void OnToggleGuidanceClicked(object? sender, EventArgs e)
    {
        GuidancePanel.IsVisible = !GuidancePanel.IsVisible;
        RefreshWorkflowDisclosures();
    }

    private void RefreshWorkflowDisclosures()
    {
        ModelToolsToggleButton.Text = (ModelToolsPanel.IsVisible ? "▾ " : "▸ ") + T("Workflow.ModelTools");
        GuidanceToggleButton.Text = (GuidancePanel.IsVisible ? "▾ " : "▸ ") + T("Workflow.Guide");
        SemanticProperties.SetDescription(ModelToolsToggleButton,
            T("Workflow.ModelTools") + ". " + T(ModelToolsPanel.IsVisible ? "Workflow.Expanded" : "Workflow.Collapsed"));
        SemanticProperties.SetDescription(GuidanceToggleButton,
            T("Workflow.Guide") + ". " + T(GuidancePanel.IsVisible ? "Workflow.Expanded" : "Workflow.Collapsed"));
    }
}
