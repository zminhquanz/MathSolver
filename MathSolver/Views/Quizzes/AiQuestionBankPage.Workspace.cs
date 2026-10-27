using MathSolver.Services;

namespace MathSolver.Views;

public partial class AiQuestionBankPage
{
    private bool _modelDetailsExpanded, _workspaceRefreshQueued;
    private string? _summaryModelPath;
    private int _workspaceGeneration;

    private void InitializeAiWorkspace()
    {
        WorkflowScroll.SizeChanged += QueueAiWorkspaceRefresh;
        WorkflowScroll.Loaded += QueueAiWorkspaceRefresh;
        WorkflowScroll.Unloaded += (_, _) => InvalidateAiWorkspaceRefresh();
        WorkflowScroll.HandlerChanging += (_, _) => InvalidateAiWorkspaceRefresh();
        LanguagePicker.SelectedIndexChanged += (_, _) => RefreshOtherOptionsSummary();
        CreativeProseSwitch.Toggled += (_, _) => RefreshOtherOptionsSummary();
        AutoInsertSwitch.Toggled += (_, _) => RefreshOtherOptionsSummary();
    }

    private void InvalidateAiWorkspaceRefresh()
    { _workspaceGeneration++; _workspaceRefreshQueued = false; }

    private void QueueAiWorkspaceRefresh(object? sender, EventArgs e)
    {
        if (_workspaceRefreshQueued) return;
        _workspaceRefreshQueued = true;
        int generation = _workspaceGeneration;
        if (!Dispatcher.Dispatch(() =>
        {
            if (generation != _workspaceGeneration) return;
            _workspaceRefreshQueued = false;
            if (!_appeared || !IsLoaded || WorkflowScroll.Handler?.MauiContext is null
                || Window is not { Handler.MauiContext: not null } window || WindowUiLifetime.IsClosing(window)) return;
            RefreshAiWorkspace();
        })) _workspaceRefreshQueued = false;
    }

    private void RefreshAiWorkspace()
    {
        if (_summaryModelPath != _bank.Runtime.ModelPath)
        { _summaryModelPath = _bank.Runtime.ModelPath; _modelDetailsExpanded = false; }
        bool narrow = WorkflowScroll.Width > 0
            && ResponsiveLayoutPolicy.UseStackedLayout(WorkflowScroll.Width, 760);
        bool selected = _bank.Runtime.CanGenerate;
        bool needsAttention = _bank.IsManaging || _bank.ManagementError is not null;
        bool summarized = narrow && selected && !needsAttention;
        ModelSummaryHeader.IsVisible = summarized;
        ModelDetailsPanel.IsVisible = !summarized || _modelDetailsExpanded;
        ModelSummaryLabel.Text = string.Format(System.Globalization.CultureInfo.CurrentCulture,
            T("Workspace.ModelSummary"), _bank.Runtime.ModelName);
        ToolTipProperties.SetText(ModelSummaryLabel, ModelSummaryLabel.Text);
        ModelDetailsToggleButton.Text = T(ModelDetailsPanel.IsVisible ? "Workspace.CollapseModel" : "Workspace.ChangeModel");
        ModelDetailsToggleButton.IsEnabled = !_bank.Generation.IsRunning && !_bank.IsManaging && !_saving
            && !_bank.Generation.IsDeletingAll;
        SemanticProperties.SetDescription(ModelDetailsToggleButton, ModelDetailsToggleButton.Text + ". "
            + T(ModelDetailsPanel.IsVisible ? "Workflow.Expanded" : "Workflow.Collapsed"));
        RefreshOtherOptionsSummary();
    }

    private void OnToggleModelDetailsClicked(object? sender, EventArgs e)
    {
        if (!_appeared || _bank.Generation.IsRunning || _bank.IsManaging || _saving || _bank.Generation.IsDeletingAll) return;
        _modelDetailsExpanded = !_modelDetailsExpanded;
        RefreshAiWorkspace();
    }

    private void OnToggleOtherOptionsClicked(object? sender, EventArgs e)
    {
        if (!ConfigurationPanel.IsEnabled) return;
        OtherOptionsPanel.IsVisible = !OtherOptionsPanel.IsVisible;
        RefreshOtherOptionsSummary();
    }

    private void RefreshOtherOptionsSummary()
    {
        if (OtherOptionsToggleButton is null || LanguagePicker is null) return;
        OtherOptionsToggleButton.Text = (OtherOptionsPanel.IsVisible ? "▾ " : "▸ ") + T("Workspace.OtherOptions");
        SemanticProperties.SetDescription(OtherOptionsToggleButton, T("Workspace.OtherOptions") + ". "
            + T(OtherOptionsPanel.IsVisible ? "Workflow.Expanded" : "Workflow.Collapsed"));
        string language = LanguagePicker.SelectedItem?.ToString() ?? "";
        OtherOptionsSummaryLabel.Text = string.Join(" · ", language,
            T(AutoInsertSwitch.IsToggled ? "Workspace.AutoSaveOn" : "Workspace.AutoSaveOff"),
            T(SupportsCreativeProse && CreativeProseSwitch.IsToggled ? "Workspace.CreativeOn" : "Workspace.CreativeOff"));
    }
}
