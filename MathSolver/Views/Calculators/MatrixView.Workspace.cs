using MathSolver.Controls;
using MathSolver.Services;
using Microsoft.Maui.Layouts;
using System.Diagnostics;

namespace MathSolver.Views;

public sealed partial class MatrixView
{
    private readonly ScrollView _workspaceScroll = new();
    private readonly VerticalStackLayout _setupPanel = new() { Spacing = 12 };
    private readonly VerticalStackLayout _coordinatePanel = new() { IsVisible = false, Spacing = 8 };
    private readonly VerticalStackLayout _moreTools = new() { IsVisible = false, Spacing = 8 };
    private readonly Label _setupSummary = TextLabel(), _statusDetails = TextLabel();
    private Button _setupToggle = null!, _moreToolsToggle = null!, _statusDetailsButton = null!;
    private EmojiActionButton _coordinateToggle = null!;
    private Grid _footerStatusRow = null!;
    private Border _statusDetailsCard = null!;
    private int _workspaceRevealVersion;

    private Border BuildSetupCard()
    {
        _setupSummary.FontAttributes = FontAttributes.Bold;
        _setupSummary.MaxLines = 2;
        _setupSummary.LineBreakMode = LineBreakMode.TailTruncation;
        _setupToggle = ActionButton("Workspace.EditSetup", "▸", (_, _) =>
        {
            _workspaceRevealVersion++;
            _setupPanel.IsVisible = !_setupPanel.IsVisible;
            RefreshWorkspacePresentation();
        }, lockDuringWork: false);
        var heading = new Grid { ColumnSpacing = 10, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        _setupSummary.VerticalTextAlignment = TextAlignment.Center;
        heading.Add(_setupSummary); heading.Add(_setupToggle, 1, 0);
        return Card(new VerticalStackLayout { Spacing = 8, Children = { heading, _setupPanel } });
    }

    private FlexLayout BuildCellToolbar()
    {
        _coordinateToggle = new EmojiActionButton
        {
            Emoji = "↪", FontSize = 14, MaximumWidthRequest = 180,
            Style = GetStyle("SolverSecondaryActionStyle"), LineBreakMode = LineBreakMode.TailTruncation
        };
        DisplayDensityManager.BindControl(_coordinateToggle);
        _coordinateToggle.SetBinding(EmojiActionButton.CaptionProperty, new Binding(nameof(Label.Text), source: _selection));
        _coordinateToggle.Clicked += (_, _) =>
        {
            _workspaceRevealVersion++;
            _coordinatePanel.IsVisible = !_coordinatePanel.IsVisible;
            if (_coordinatePanel.IsVisible) _moreTools.IsVisible = false;
            RefreshWorkspacePresentation();
        };
        _translations.Add(() =>
        {
            SemanticProperties.SetHint(_coordinateToggle, T("Go"));
            ToolTipProperties.SetText(_coordinateToggle, T("Go"));
            ToolTipProperties.SetText(_pasteCells, T("PasteCells"));
        });
        _selectedValue.FontSize = 18;
        _selectedValue.MaxLines = 2;
        _selectedValue.LineBreakMode = LineBreakMode.TailTruncation;
        _selectedValue.VerticalTextAlignment = TextAlignment.Center;
        _selectedValue.Padding = new Thickness(8, 2);
        var cellInfo = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, AutomationId = "MatrixSelectedCell" };
        cellInfo.Add(_coordinateToggle); cellInfo.Add(_selectedValue, 1, 0);

        var navigation = FlowGrid(130, 3);
        navigation.AutomationId = "MatrixCoordinates";
        navigation.Add(Labeled("Row", _row)); navigation.Add(Labeled("Column", _column));
        var go = ActionButton("Go", "↪", OnGo, lockDuringWork: false);
        go.SetDynamicResource(MinimumHeightRequestProperty, "Density.InputFrameHeight");
        go.VerticalOptions = LayoutOptions.End;
        navigation.Add(go);
        _coordinatePanel.Add(navigation);

        var clipboard = FlowGrid(145, 4);
        clipboard.Add(ActionButton("SelectAll", "▦", (_, _) => _viewport.SelectAll()));
        var copy = ActionButton("Workspace.Copy", "📋", OnCopySelection);
        _translations.Add(() => ToolTipProperties.SetText(copy, T("CopySelection")));
        clipboard.Add(copy); clipboard.Add(_pasteCells);
        _moreToolsToggle = ActionButton("Workspace.MoreTools", "⋯", (_, _) =>
        {
            _workspaceRevealVersion++;
            _moreTools.IsVisible = !_moreTools.IsVisible;
            if (_moreTools.IsVisible) _coordinatePanel.IsVisible = false;
            RefreshWorkspacePresentation();
        }, lockDuringWork: false);
        clipboard.Add(_moreToolsToggle);
        var toolbar = new FlexLayout
        {
            Direction = FlexDirection.Row, Wrap = FlexWrap.Wrap,
            AlignItems = FlexAlignItems.Start, AlignContent = FlexAlignContent.Start,
            AutomationId = "MatrixToolbar"
        };
        FlexLayout.SetBasis(cellInfo, new FlexBasis(240));
        FlexLayout.SetGrow(cellInfo, 1);
        FlexLayout.SetShrink(cellInfo, 1);
        FlexLayout.SetBasis(clipboard, new FlexBasis(620));
        FlexLayout.SetGrow(clipboard, 1);
        FlexLayout.SetShrink(clipboard, 1);
        cellInfo.Margin = new Thickness(0, 0, 8, 8);
        toolbar.Children.Add(cellInfo); toolbar.Children.Add(clipboard);
        return toolbar;
    }

    private Border BuildStatusDetails()
    {
        _statusDetails.FontSize = 13;
        _statusDetails.SetDynamicResource(Label.TextColorProperty, "ContentDangerTextColor");
        _statusDetails.SetBinding(Label.TextProperty, new Binding(nameof(Label.Text), source: _message));
        _statusDetailsCard = Card(_statusDetails);
        _statusDetailsCard.IsVisible = false;
        return _statusDetailsCard;
    }

    private Border BuildWorkspaceActions()
    {
        _message.FontSize = 13;
        _message.MaxLines = 2;
        _message.LineBreakMode = LineBreakMode.TailTruncation;
        _message.SetBinding(ToolTipProperties.TextProperty, new Binding(nameof(Label.Text), source: _message));
        _statusDetailsButton = ActionButton("Workspace.Details", "ⓘ", (_, _) => QueueWorkspaceReveal(_statusDetailsCard), lockDuringWork: false);
        _footerStatusRow = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        _footerStatusRow.Add(_message); _footerStatusRow.Add(_statusDetailsButton, 1, 0);
        _activity.SetDynamicResource(ActivityIndicator.ColorProperty, "PrimaryColor");
        _progressText.FontSize = 13;
        _progressText.IsVisible = false;
        _progressText.SetDynamicResource(Label.TextColorProperty, "WallpaperTextSecondaryColor");
        var progressing = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        progressing.Add(_activity); progressing.Add(_progressText, 1, 0);
        progressing.SetBinding(IsVisibleProperty, new Binding(nameof(Label.IsVisible), source: _progressText));
        var run = new Grid();
        _calculate.AutomationId = "MatrixCalculate";
        _stop.AutomationId = "MatrixStop";
        run.Add(_calculate); run.Add(_stop);
        var content = new VerticalStackLayout { Spacing = 6, Children = { _footerStatusRow, progressing, _progress, run } };
        return new Border { Style = GetStyle("SolverActionBarStyle"), Content = content, AutomationId = "MatrixActionBar" };
    }

    private void RefreshWorkspacePresentation()
    {
        if (_setupToggle is null || _moreToolsToggle is null || _footerStatusRow is null) return;
        if (!HasInputs) _setupPanel.IsVisible = true;
        _setupSummary.Text = _a is { } a && _b is { } b
            ? F("Workspace.SetupSummary", T(new[] { "Add", "Subtract", "Multiply" }[(int)_operation]), a.Rows, a.Columns, b.Rows, b.Columns)
            : T("Workspace.SetupTitle");
        ((EmojiActionButton)_setupToggle).Caption = T(_setupPanel.IsVisible ? "Workspace.HideSetup" : "Workspace.EditSetup");
        ((EmojiActionButton)_setupToggle).Emoji = _setupPanel.IsVisible ? "▾" : "▸";
        _setupToggle.IsVisible = HasInputs;
        _setupToggle.IsEnabled = !_busy;
        ((EmojiActionButton)_moreToolsToggle).Caption = T(_moreTools.IsVisible ? "Workspace.FewerTools" : "Workspace.MoreTools");
        _message.IsVisible = !string.IsNullOrEmpty(_message.Text)
            && (!_busy || _calculationStage is null || _errorKey is not null);
        _statusDetailsButton.IsVisible = _statusDetailsCard.IsVisible = _errorKey is not null;
        _footerStatusRow.IsVisible = _message.IsVisible || _statusDetailsButton.IsVisible;
    }

    private void CollapseSetupAfterCreation()
    {
        _setupPanel.IsVisible = false;
        RefreshWorkspacePresentation();
        QueueWorkspaceReveal(_editorCard);
    }

    private void QueueWorkspaceReveal(VisualElement target)
    {
        int request = ++_workspaceRevealVersion, version = _version;
        Dispatcher.Dispatch(async () =>
        {
            // Match native settlement for the existing complex calculator pages.
            await Task.Delay(64);
            if (request != _workspaceRevealVersion || !CanPublish(version) || !target.IsVisible
                || _workspaceScroll.Handler is null || ReferenceEquals(target, _editorCard) && _setupPanel.IsVisible) return;
            try { await _workspaceScroll.ScrollToAsync(target, ScrollToPosition.Start, false); }
            catch (Exception error) when (error is ObjectDisposedException or System.Runtime.InteropServices.COMException)
            { Debug.WriteLine(error); }
        });
    }
}
