using MathSolver.Services.Appearance;

namespace MathSolver.Views;

public partial class AiQuestionBankPage
{
    private void RenderWorkflow(bool running, AiPresentation presentation)
    {
        WorkflowStatusLabel.Text = T(presentation.WorkflowKey);
        CancelModelButton.IsVisible = _bank.IsManaging;
        StopButton.IsVisible = running;
        Grid.SetColumnSpan(GenerateButton, running ? 1 : 2);
        JobStatusLabel.IsVisible = presentation.ShowJobStatus;
        RefreshWorkflowDisclosures();
    }

    private void OnGuidanceStepsSizeChanged(object? sender, EventArgs e)
    {
        if (GuidanceSteps.Width <= 0) return;
        int columns = MathSolver.Services.ResponsiveLayoutPolicy.UseStackedLayout(GuidanceSteps.Width, 1040)
            ? MathSolver.Services.ResponsiveLayoutPolicy.UseStackedLayout(GuidanceSteps.Width, 560) ? 1 : 2 : 4;
        if (GuidanceSteps.ColumnDefinitions.Count == columns) return;
        // Keep the same controls and reading order as the window/text size changes.
        for (int i = 0; i < GuidanceSteps.Children.Count; i++)
        { Grid.SetRow((View)GuidanceSteps.Children[i], 0); Grid.SetColumn((View)GuidanceSteps.Children[i], 0); }
        GuidanceSteps.ColumnDefinitions.Clear();
        for (int i = 0; i < columns; i++) GuidanceSteps.ColumnDefinitions.Add(new(GridLength.Star));
        GuidanceSteps.RowDefinitions.Clear();
        for (int i = 0; i < (GuidanceSteps.Children.Count + columns - 1) / columns; i++)
            GuidanceSteps.RowDefinitions.Add(new(GridLength.Auto));
        for (int i = 0; i < GuidanceSteps.Children.Count; i++)
        { Grid.SetRow((View)GuidanceSteps.Children[i], i / columns); Grid.SetColumn((View)GuidanceSteps.Children[i], i % columns); }
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
#if WINDOWS
        GuidanceOverview.IsVisible = GuidanceSteps.IsVisible = GuidancePanel.IsVisible;
        string guidanceKey = "Desktop.Guidance";
#else
        string guidanceKey = "Workflow.Details";
#endif
        ModelToolsToggleButton.Text = (ModelToolsPanel.IsVisible ? "▾ " : "▸ ") + T("Workflow.ModelTools");
        GuidanceToggleButton.Text = (GuidancePanel.IsVisible ? "▾ " : "▸ ") + T(guidanceKey);
        SemanticProperties.SetDescription(ModelToolsToggleButton,
            T("Workflow.ModelTools") + ". " + T(ModelToolsPanel.IsVisible ? "Workflow.Expanded" : "Workflow.Collapsed"));
        SemanticProperties.SetDescription(GuidanceToggleButton,
            T(guidanceKey) + ". " + T(GuidancePanel.IsVisible ? "Workflow.Expanded" : "Workflow.Collapsed"));
    }
}
