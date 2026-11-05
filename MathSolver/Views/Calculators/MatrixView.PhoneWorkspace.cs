using MathSolver.Controls;
using MathSolver.Services;

namespace MathSolver.Views;

public sealed partial class MatrixView
{
    private readonly Label _phoneTitle = new()
    {
        FontSize = 18, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center,
        MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation, Margin = new Thickness(12, 4), IsVisible = false
    };
    private Border _setupCard = null!;
    private Grid _phoneMoreActions = null!;
    private EmojiActionButton _phoneSetup = null!;
    private bool _phoneEntryWorkspace;
#if ANDROID
    private EmojiActionButton _phoneRangeToggle = null!;
    private EmojiActionButton _phoneExpandToggle = null!;
    private Button _phoneNavigationAction = null!;
    private Button _phoneInputDone = null!;
    private Grid _phoneStandardActions = null!;
    private Border _phoneActionBar = null!;
    private bool _phoneKeyboardEntry;
#endif

    private void BuildPhoneWorkspace(Grid root, Border setup, Grid heading)
    {
        _setupCard = setup;
        _phoneTitle.SetDynamicResource(Label.TextColorProperty, "WallpaperTextPrimaryColor");
        root.Add(_phoneTitle);
        _phoneSetup = (EmojiActionButton)MirrorAction("Workspace.EditSetup", "⚙", _setupToggle);
        _phoneSetup.AutomationId = "MatrixPhoneSetup";
        _phoneSetup.IsVisible = false;
        heading.ColumnDefinitions.Add(new(GridLength.Auto));
        heading.Add(_phoneSetup, 4);
#if ANDROID
        _phoneRangeToggle = (EmojiActionButton)MirrorAction("RangeSelection.Start", "▧", _rangeSelectionToggle);
        _phoneRangeToggle.AutomationId = "MatrixPhoneSelectRange";
        _phoneRangeToggle.FontSize = 13;
        _phoneRangeToggle.FontAttributes = FontAttributes.None;
        _phoneRangeToggle.WidthRequest = 104;
        _phoneRangeToggle.MinimumHeightRequest = 48;
        _phoneRangeToggle.RemoveDynamicResource(Button.PaddingProperty);
        _phoneRangeToggle.Padding = new Thickness(8, 6);
        _phoneRangeToggle.IsVisible = false;
        heading.Add(_phoneRangeToggle, 1); // Replaces Expand; that remains in More.
#endif
        _phoneMoreActions = FlowGrid(120, 2);
        _phoneMoreActions.IsVisible = false;
        _phoneMoreActions.Add(MirrorAction("Workspace.Copy", "📋", _copySelection));
        _phoneMoreActions.Add(MirrorAction("Workspace.Paste", "", _pasteCells));
        _phoneMoreActions.Add(MirrorAction("Workspace.Clear", "", _clearCells));
        _phoneMoreActions.Add(MirrorAction("TechnicalDetails", "ⓘ", _technicalDetailsToggle));
        _moreTools.Children.Insert(0, _phoneMoreActions);
#if ANDROID
        _phoneExpandToggle = (EmojiActionButton)MirrorAction("Workspace.ExpandTable", "⤢", _expandTable);
        _phoneMoreActions.Add(_phoneExpandToggle);
        _phoneNavigationAction = ActionButton("Workspace.ShowNavigation", "☰", (_, _) =>
        { TryCloseMatrixDetails(); TryRestorePhoneNavigation(); }, lockDuringWork: false);
        _phoneMoreActions.Add(_phoneNavigationAction);
#endif
    }

    private Button MirrorAction(string key, string icon, Button original)
    {
        var action = ActionButton(key, icon, (_, _) =>
        {
            // A phone inspector disables pointer input to the table. Close it
            // before invoking tools that finish an edit or change the selection.
            TryCloseMatrixDetails();
            ((IButtonController)original).SendClicked();
        }, lockDuringWork: false);
        action.SetBinding(IsEnabledProperty, new Binding(nameof(Button.IsEnabled), source: original));
        return action;
    }

    private void RefreshPhoneWorkspace(bool phone)
    {
        if (_phoneEntryWorkspace != phone)
        {
            _phoneEntryWorkspace = phone;
            if (phone)
            {
                _matrixMain.RemoveDynamicResource(Grid.PaddingProperty);
                _matrixMain.Padding = new Thickness(8, 4);
            }
            else _matrixMain.SetDynamicResource(Grid.PaddingProperty, "Density.ScrollPadding");
            _viewport.FitSmallCells = phone;
            _viewport.Refresh();
        }
        _phoneTitle.IsVisible = phone;
        _phoneTitle.Text = T("Title") + "  ·  " + _heroSymbol.Text;
        _setupCard.IsVisible = !phone;
        _phoneSetup.IsVisible = phone;
        SetMatrixToolPresentation(_phoneSetup, "Workspace.EditSetup", "⚙");
        _technicalDetailsToggle.IsVisible = !phone;
        _phoneMoreActions.IsVisible = phone;
        _clipboardTools.IsVisible = !phone;
        _cellInfo.IsVisible = !phone || !_tableExpanded;
        if (phone) _inputStatus.IsVisible = _range.IsVisible = false;
        foreach (EmojiActionButton tab in _matrices) tab.Emoji = phone ? "" : "▦";
        // On phones the footer has one row; clipboard tools remain in More.
        _copySelection.IsVisible = HasInputs && !phone;
        _footerStatusRow.IsVisible = (_message.IsVisible || _statusDetailsButton.IsVisible)
            && (!phone || _errorKey is not null);
#if ANDROID
        RefreshPhoneInputActions(phone);
        _expandTable.IsVisible = !phone;
        _phoneRangeToggle.IsVisible = phone;
        if (phone && IsPhoneLandscape) _cellInfo.IsVisible = false;
        string expandKey = _tableExpanded ? "Workspace.RestoreTable" : "Workspace.ExpandTable";
        _phoneExpandToggle.Caption = T(expandKey);
        SemanticProperties.SetDescription(_phoneExpandToggle, T(expandKey));
        RefreshPhoneRangeAction();
        if (_phoneKeyboardEntry)
        {
            _matrixMain.Padding = new Thickness(8, 0);
            _matrixMain.RowSpacing = 0;
            // The inline editor already shows the value. Spend the remaining
            // height on rows rather than repeating tabs and value controls.
            _workspaceChrome.IsVisible = false;
            _phoneTitle.FontSize = 14;
            _phoneTitle.Margin = new Thickness(8, 0);
            _phoneTitle.Text = T("Title") + " · " + _selection.Text;
            if (IsPhoneLandscape) _phoneTitle.IsVisible = false;
        }
        else
        {
            if (phone) _matrixMain.Padding = new Thickness(8, 4);
            _matrixMain.RowSpacing = 8;
            _phoneTitle.FontSize = 18;
            _phoneTitle.Margin = new Thickness(12, 4);
        }
        _phoneInputDone.Text = "✓ " + T("Workspace.FinishInput")
            + (_phoneKeyboardEntry && IsPhoneLandscape ? " · " + _selection.Text : "");
        RefreshMatrixPhoneChrome();
        _phoneNavigationAction.IsVisible = phone && IsPhoneLandscape;
#endif
    }

#if ANDROID
    private void BuildPhoneInputActions(Border bar, VerticalStackLayout content, Grid normalActions)
    {
        _phoneActionBar = bar;
        _phoneStandardActions = normalActions;
        _phoneInputDone = ActionButton("Workspace.FinishInput", "✓", async (_, _) =>
        {
            if (!CanTouchUi || !_active || _busy || !_viewport.FinishEdit()) return;
            await _value.HideSoftInputAsync(CancellationToken.None);
            if (!CanTouchUi || !_active) return;
            _value.Unfocus();
            RefreshWorkspacePresentation();
        }, primary: true, lockDuringWork: false);
        _phoneInputDone.AutomationId = "MatrixFinishInput";
        _phoneInputDone.FontSize = 14;
        _phoneInputDone.MinimumHeightRequest = 48;
        _phoneInputDone.IsVisible = false;
        content.Add(_phoneInputDone);
    }

    private void RefreshPhoneInputActions(bool phone)
    {
        if (_phoneInputDone is null) return;
        bool entering = phone && _matrixKeyboardVisible && _viewport.IsEditing && !_busy;
        _phoneInputDone.IsVisible = entering;
        _phoneStandardActions.IsVisible = !entering;
        bool changed = _phoneKeyboardEntry != entering;
        _phoneKeyboardEntry = entering;
        if (entering)
        {
            if (changed) _phoneActionBar.RemoveDynamicResource(Border.PaddingProperty);
            var padding = new Thickness(8, IsPhoneLandscape ? 0 : 4);
            if (_phoneActionBar.Padding != padding) _phoneActionBar.Padding = padding;
        }
        else if (changed) _phoneActionBar.SetDynamicResource(Border.PaddingProperty, "Density.ActionPadding");
    }

    private void RefreshPhoneRangeAction()
    {
        if (_phoneRangeToggle is null) return;
        bool selecting = _viewport.IsRangeSelectionMode;
        _phoneRangeToggle.Caption = T(selecting ? "RangeSelection.CancelShort" : "RangeSelection.Start");
        SelectionButtonStyler.Apply(_phoneRangeToggle, selecting);
        SemanticProperties.SetDescription(_phoneRangeToggle, T(selecting ? "RangeSelection.Cancel" : "RangeSelection.Start"));
        _rangeSelectionHint.IsVisible = selecting || (_phoneEntryWorkspace && !_phoneKeyboardEntry && !IsPhoneLandscape);
        _rangeSelectionHint.FontAttributes = FontAttributes.None;
        _rangeSelectionHint.FontSize = 12;
        if (!selecting) _rangeSelectionHint.Text = T("Workspace.HoldHint");
    }
#endif
}
