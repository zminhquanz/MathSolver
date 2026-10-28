using System.ComponentModel;
using System.Globalization;
using MathSolver.Controls;
using MathSolver.Services;

namespace MathSolver.Views;

public partial class PowerRootView
{
    private bool _refreshingWorkspace;
    private bool _workspaceSuspended;
    private bool _calculationWasStopped;

    private void InitializePowerRootWorkspace()
    {
        ResultBorder.PropertyChanged += OnWorkspaceResultChanged;
        RootResultBorder.PropertyChanged += OnWorkspaceResultChanged;
        ExportTextButton.PropertyChanged += OnWorkspaceResultChanged;
        RefreshPowerRootWorkspace();
    }

    private void OnWorkspaceResultChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == IsVisibleProperty.PropertyName) RefreshPowerRootWorkspace();
    }

    private void RefreshPowerRootWorkspace()
    {
        if (_refreshingWorkspace || _workspaceSuspended || WindowUiLifetime.IsClosing(Window)) return;
        _refreshingWorkspace = true;
        try
        {
            bool powerResult = _isPowerMode && ResultBorder.IsVisible && _calculationState is not null;
            bool rootResult = !_isPowerMode && RootResultBorder.IsVisible && _rootCalculationState is not null;
            PowerResults.IsVisible = powerResult;
            RootResults.IsVisible = rootResult;
            ProgressBorder.IsVisible = _isCalculating;
            PowerIdleActions.IsVisible = !_isCalculating && _isPowerMode;
            RootIdleActions.IsVisible = !_isCalculating && !_isPowerMode;
            CopyResultButtonContainer.IsVisible = powerResult && !_isCalculating;
            ExportTextButton.IsVisible = powerResult && _calculationState!.DigitCount >= ExportDigitThreshold && !_isCalculating;
            RootCopyResultButtonContainer.IsVisible = rootResult && !_isCalculating;

            FooterStatusLabel.IsVisible = !_isCalculating && (powerResult || rootResult || (_isPowerMode && _calculationWasStopped));
            FooterStatusLabel.Text = _isExporting ? Translate("PowerRoot.ExportPreparing")
                : powerResult ? Format("PowerRoot.Workspace.CompletedSummary",
                    _calculationState!.DigitCount.ToString("N0", CultureInfo.CurrentCulture), Duration(_calculationState.Elapsed))
                : rootResult ? D("Completed")
                : _calculationWasStopped ? Translate("PowerRoot.ProgressStopped") : string.Empty;

            ReflowFooterActions(PowerIdleActions);
            ReflowFooterActions(RootIdleActions);
        }
        finally { _refreshingWorkspace = false; }
    }

    private void OnFooterActionsSizeChanged(object? sender, EventArgs e)
    {
        if (!_workspaceSuspended && sender is Grid grid) ReflowFooterActions(grid);
    }

    private static void ReflowFooterActions(Grid grid)
    {
        if (grid.Width <= 0) return;
        var actions = grid.Children.OfType<VisualElement>().Where(view => view.IsVisible).ToArray();
        if (actions.Length == 0) return;
        int columns = Math.Min(actions.Length, ResponsiveLayoutPolicy.Columns(
            grid.Width - grid.Padding.HorizontalThickness, 140, 4, grid.ColumnSpacing));
        int rows = (actions.Length + columns - 1) / columns;
        if (grid.ColumnDefinitions.Count != columns || grid.RowDefinitions.Count != rows)
        {
            grid.ColumnDefinitions.Clear(); grid.RowDefinitions.Clear();
            for (int i = 0; i < columns; i++) grid.ColumnDefinitions.Add(new(GridLength.Star));
            for (int i = 0; i < rows; i++) grid.RowDefinitions.Add(new(GridLength.Auto));
        }
        for (int i = 0; i < actions.Length; i++)
        {
            Grid.SetRow(actions[i], i / columns); Grid.SetColumn(actions[i], i % columns);
            Grid.SetColumnSpan(actions[i], 1);
        }
    }
}
