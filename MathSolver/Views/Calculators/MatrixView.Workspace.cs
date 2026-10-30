using MathSolver.Controls;
using MathSolver.Services;
using System.Diagnostics;

namespace MathSolver.Views;

public sealed partial class MatrixView
{
    private Button _copySelection = null!;

    private readonly ScrollView _workspaceScroll = new();
    private readonly VerticalStackLayout _setupPanel = new() { Spacing = 12 };
    private readonly VerticalStackLayout _coordinatePanel = new() { IsVisible = false, Spacing = 8 };
    private readonly VerticalStackLayout _moreTools = new() { IsVisible = false, Spacing = 8 };
    private readonly Label _setupSummary = TextLabel(), _statusDetails = TextLabel();
    private readonly Label _rangeSelectionHint = TextLabel();
    private readonly VerticalStackLayout _cellValuePanel = new() { IsVisible = false, Spacing = 6 };
    private readonly Label _fullCellTitle = TextLabel(), _fullCellValue = TextLabel();
    private EmojiActionButton _cellValueToggle = null!;
    private Button _clearCells = null!;
    private Button _rangeSelectionToggle = null!;
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
            if (_setupPanel.IsVisible) _tableExpanded = false;
            RefreshWorkspacePresentation();
        }, lockDuringWork: false);
        var heading = new Grid { ColumnSpacing = 10, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        _setupSummary.VerticalTextAlignment = TextAlignment.Center;
        heading.Add(_setupSummary); heading.Add(_setupToggle, 1, 0);
        // Native vertical scrollbars can overlay the content. Keep the form
        // borders and buttons clear of the full scrollbar track, including hover.
        _setupPanel.Padding = new Thickness(0, 4, 24, 4);
        _setupScroll.Content = _setupPanel;
        _setupScroll.SetBinding(IsVisibleProperty, new Binding(nameof(IsVisible), source: _setupPanel));
        return Card(new VerticalStackLayout { Spacing = 8, Children = { heading, _setupScroll } });
    }

    private Grid BuildCellToolbar()
    {
        _coordinateToggle = new EmojiActionButton
        {
            Emoji = "↪", FontSize = 14, MaximumWidthRequest = 148,
            Style = GetStyle("SolverSecondaryActionStyle"), LineBreakMode = LineBreakMode.TailTruncation
        };
        DisplayDensityManager.BindControl(_coordinateToggle);
        _coordinateToggle.SetBinding(EmojiActionButton.CaptionProperty, new Binding(nameof(Label.Text), source: _selection));
        _coordinateToggle.Clicked += (_, _) =>
        {
            _workspaceRevealVersion++;
            _coordinatePanel.IsVisible = !_coordinatePanel.IsVisible;
            if (_coordinatePanel.IsVisible) { _moreTools.IsVisible = false; OpenMatrixTools(); }
            else _toolsRequested = false;
            RefreshWorkspacePresentation();
        };
        _translations.Add(() =>
        {
            SemanticProperties.SetHint(_coordinateToggle, T("Go"));
            ToolTipProperties.SetText(_coordinateToggle, T("Go"));
            ToolTipProperties.SetText(_pasteCells, T("PasteCells"));
        });
        _cellValueToggle = new EmojiActionButton
        {
            Emoji = "⤢", FontSize = 14, Style = GetStyle("SolverSecondaryActionStyle"),
            LineBreakMode = LineBreakMode.TailTruncation, AutomationId = "MatrixViewCellValue"
        };
        DisplayDensityManager.BindControl(_cellValueToggle);
        _cellValueToggle.SetBinding(EmojiActionButton.CaptionProperty, new Binding(nameof(Label.Text), source: _selectedValue));
        _cellValueToggle.Clicked += (_, _) => OpenMatrixCellDetails();
        _fullCellTitle.FontSize = 13;
        _fullCellValue.FontSize = 18;
        _fullCellValue.LineBreakMode = LineBreakMode.CharacterWrap;
        _cellValuePanel.Add(_fullCellTitle);
        _cellValuePanel.Add(_fullCellValue);
        var cellInfo = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, AutomationId = "MatrixSelectedCell" };
        cellInfo.Add(_coordinateToggle); cellInfo.Add(_cellValueToggle, 1, 0);

        var navigation = FlowGrid(130, 3);
        navigation.AutomationId = "MatrixCoordinates";
        navigation.Add(Labeled("Row", _row)); navigation.Add(Labeled("Column", _column));
        var go = ActionButton("Go", "↪", OnGo, lockDuringWork: false);
        go.SetDynamicResource(MinimumHeightRequestProperty, "Density.InputFrameHeight");
        go.VerticalOptions = LayoutOptions.End;
        navigation.Add(go);
        _coordinatePanel.Add(navigation);

        var clipboard = FlowGrid(90, 2);
        clipboard.AutomationId = "MatrixPrimaryTools";
        var copy = _copySelection = ActionButton("Workspace.Copy", "📋", OnCopySelection);
        _copySelection.SetDynamicResource(BackgroundColorProperty, "WallpaperDividerColor");
        _copySelection.AutomationId = "MatrixCopySelection";
        _clearCells = ActionButton("Workspace.Clear", "", (_, _) =>
        {
            if (!CanTouchUi || !_active || _busy || !HasInputs || _selected == 2) return;
            _viewport.ClearSelection();
        });
        _clearCells.AutomationId = "MatrixClearSelection";
        _translations.Add(() =>
        {
            ToolTipProperties.SetText(copy, T("CopySelection"));
            ToolTipProperties.SetText(_clearCells, T("Workspace.ClearSelection"));
            SemanticProperties.SetDescription(_clearCells, T("Workspace.ClearSelection"));
        });
        foreach (var button in new[] { _pasteCells, _clearCells })
        {
            button.FontSize = 14;
            button.LineBreakMode = LineBreakMode.WordWrap;
            // Keep the density-backed touch height; narrow tools need less
            // horizontal padding so captions fit without adding another line.
            button.RemoveDynamicResource(Button.PaddingProperty);
            button.Padding = new Thickness(8, 6);
            clipboard.Add(button);
        }
        var secondary = _secondaryTools = FlowGrid(136, 2);
        _rangeSelectionToggle = ActionButton("RangeSelection.Start", "▧", (_, _) =>
        {
            if (_viewport.IsRangeSelectionMode) _viewport.CancelRangeSelection();
            else if (_viewport.BeginRangeSelection())
            {
                _workspaceRevealVersion++;
                CloseMatrixTools();
                RefreshWorkspacePresentation();
            }
        });
        _rangeSelectionToggle.AutomationId = "MatrixRangeSelection";
        _rangeSelectionToggle.FontSize = 14;
        _rangeSelectionToggle.RemoveDynamicResource(Button.PaddingProperty);
        _rangeSelectionToggle.Padding = new Thickness(8, 6);
        _rangeSelectionToggle.LineBreakMode = LineBreakMode.WordWrap;
        _rangeSelectionHint.AutomationId = "MatrixRangeSelectionHint";
        _rangeSelectionHint.FontSize = 13;
        _rangeSelectionHint.IsVisible = false;
        _rangeSelectionHint.SetDynamicResource(Label.TextColorProperty, "WallpaperTextSecondaryColor");
        _viewport.RangeSelectionModeChanged += () =>
        { if (CanRefreshLocalization) RefreshRangeSelectionControls(); };
        secondary.Add(_rangeSelectionToggle);
        _moreTools.Add(ActionButton("SelectAll", "▦", (_, _) => _viewport.SelectAll()));
        _moreToolsToggle = ActionButton("Workspace.MoreTools", "⋯", (_, _) =>
        {
            _workspaceRevealVersion++;
            _moreTools.IsVisible = !_moreTools.IsVisible;
            if (_moreTools.IsVisible) { _coordinatePanel.IsVisible = false; OpenMatrixTools(); }
            else _toolsRequested = false;
            RefreshWorkspacePresentation();
        }, lockDuringWork: false);
        _moreToolsToggle.FontSize = 14;
        _moreToolsToggle.RemoveDynamicResource(Button.PaddingProperty);
        _moreToolsToggle.Padding = new Thickness(8, 6);
        _moreToolsToggle.LineBreakMode = LineBreakMode.WordWrap;
        secondary.Add(_moreToolsToggle);
        // One shared reflow policy for groups and their buttons. Horizontal item
        // margins in FlexLayout consumed narrow touch widths and caused extra rows.
        var toolbar = FlowGrid(286, 2);
        toolbar.AutomationId = "MatrixToolbar";
        toolbar.RowSpacing = 4;
        toolbar.Add(cellInfo); toolbar.Add(clipboard);
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
        _statusDetailsButton = ActionButton("Workspace.Details", "ⓘ", (_, _) =>
        { OpenMatrixTools(); _moreTools.IsVisible = _coordinatePanel.IsVisible = false; RefreshWorkspacePresentation(); }, lockDuringWork: false);
        _footerStatusRow = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        _footerStatusRow.Add(_message); _footerStatusRow.Add(_statusDetailsButton, 1, 0);
        _activity.SetDynamicResource(ActivityIndicator.ColorProperty, "PrimaryColor");
        _progressText.FontSize = 13;
        _progressText.IsVisible = false;
        _progressText.SetDynamicResource(Label.TextColorProperty, "WallpaperTextSecondaryColor");
        var progressing = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        progressing.Add(_activity); progressing.Add(_progressText, 1, 0);
        progressing.SetBinding(IsVisibleProperty, new Binding(nameof(Label.IsVisible), source: _progressText));
        var idle = new Grid { Style = GetStyle("SolverFooterActionButtonsStyle") };
        idle.Add(_calculate); idle.Add(_clearAll); idle.Add(_copySelection);
        idle.SetBinding(IsVisibleProperty, new Binding(nameof(Button.IsVisible), source: _calculate));
        var run = new Grid();
        _calculate.AutomationId = "MatrixCalculate";
        _stop.AutomationId = "MatrixStop";
        run.Add(idle); run.Add(_stop);
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
        _message.IsVisible = !(_statusKey == "Completed" && _result is not null) && !string.IsNullOrEmpty(_message.Text)
            && (!_busy || _calculationStage is null || _errorKey is not null);
        _statusDetailsButton.IsVisible = _statusDetailsCard.IsVisible = _errorKey is not null;
        _footerStatusRow.IsVisible = _message.IsVisible || _statusDetailsButton.IsVisible;
        _clearCells.IsEnabled = !_busy && _active && HasInputs && _selected != 2
            && !ReferenceEquals(_failedReadDraft, CurrentDraft);
        _cellValueToggle.IsEnabled = !_busy && _active && HasInputs;
        _fullCellTitle.Text = HasInputs ? F("Workspace.ValueTitle", "ABC"[_selected], _viewport.SelectedRow + 1, _viewport.SelectedColumn + 1) : "";
        _fullCellValue.Text = _selectedValue.Text;
        ToolTipProperties.SetText(_cellValueToggle, T("CellValue"));
        SemanticProperties.SetHint(_cellValueToggle, T("CellValue") + ". " + T(_cellValuePanel.IsVisible ? "HelpExpanded" : "HelpCollapsed"));
        RefreshRangeSelectionControls();
        RefreshMatrixLayout();
    }

    private void RefreshRangeSelectionControls()
    {
        if (_rangeSelectionToggle is null) return;
        bool selecting = _viewport.IsRangeSelectionMode;
        ((EmojiActionButton)_rangeSelectionToggle).Caption = T(selecting ? "RangeSelection.Cancel" : "RangeSelection.Start");
        SelectionButtonStyler.Apply(_rangeSelectionToggle, selecting);
        _rangeSelectionToggle.IsEnabled = HasInputs && !_busy;
        if (_rangeInMore is EmojiActionButton moreRange)
        {
            moreRange.Caption = T(selecting ? "RangeSelection.Cancel" : "RangeSelection.Start");
            moreRange.IsEnabled = _rangeSelectionToggle.IsEnabled;
        }
        _rangeSelectionHint.IsVisible = selecting;
        _rangeSelectionHint.Text = selecting && _viewport.IsAwaitingRangeEnd
            ? F("RangeSelection.LastHint", "ABC"[_selected], _viewport.SelectedRow + 1, _viewport.SelectedColumn + 1)
            : T("RangeSelection.FirstHint");
        ToolTipProperties.SetText(_rangeSelectionToggle, selecting ? _rangeSelectionHint.Text : T("RangeSelection.FirstHint"));
        SemanticProperties.SetHint(_rangeSelectionToggle, _rangeSelectionHint.Text);
    }

    private void CollapseSetupAfterCreation()
    {
        _setupPanel.IsVisible = false;
        RefreshWorkspacePresentation();

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
