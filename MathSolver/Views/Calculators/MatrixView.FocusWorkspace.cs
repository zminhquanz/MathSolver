using MathSolver.Controls;
using MathSolver.Services;

namespace MathSolver.Views;

public sealed partial class MatrixView
{
    private readonly Grid _tableSlot = new(), _editorWorkArea = new() { ColumnSpacing = 12 };
    private readonly Grid _matrixMain = new() { RowSpacing = 8, RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
    private readonly ScrollView _setupScroll = new(), _inspectorScroll = new();
    private readonly Label _inspectorTitle = TextLabel(), _resultSummary = TextLabel();
    private Grid _secondaryTools = null!;
    private Border _inspectorCard = null!, _solutionCard = null!;
    private VisualElement _matrixHero = null!;
    private EmojiActionButton _expandTable = null!;
    private EmojiActionButton _technicalDetailsToggle = null!;
    private Button _closeDetails = null!, _rangeInMore = null!;
    private bool _tableExpanded, _detailsRequested, _technicalRequested, _detailsDismissed, _phoneDetailsOpen, _updatingMatrixLayout;
    private int _detailMatrix = -1, _detailRow = -1, _detailColumn = -1;

    private void BuildFocusedWorkspace(Grid root, VisualElement hero, Border setupCard, VerticalStackLayout header, Grid matrixTabs)
    {
        _matrixHero = hero;
        _expandTable = (EmojiActionButton)ActionButton("Workspace.ExpandTable", "⤢", (_, _) =>
        {
            if (!HasInputs || !_viewport.FinishEdit()) return;
            _tableExpanded = !_tableExpanded;
            if (_tableExpanded) { _setupPanel.IsVisible = false; _moreTools.IsVisible = _coordinatePanel.IsVisible = false; }
            RefreshWorkspacePresentation();
        }, lockDuringWork: false);
        _expandTable.AutomationId = "MatrixExpandTable";
        // More remains reachable in the tab row even when expanded mode hides the secondary row.
        ((Grid)_moreToolsToggle.Parent).Children.Remove(_moreToolsToggle);
        var heading = new Grid { ColumnSpacing = 6, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto) } };
        heading.Add(matrixTabs); heading.Add(_expandTable, 1); heading.Add(_moreToolsToggle, 2); heading.Add(_technicalDetailsToggle, 3);
        header.Children.Insert(0, heading);

        _rangeInMore = ActionButton("RangeSelection.Start", "▧", (_, _) => ((IButtonController)_rangeSelectionToggle).SendClicked());
        _moreTools.Children.Insert(0, _rangeInMore);
        var tools = new VerticalStackLayout { Spacing = 8, Children = { _coordinatePanel, _moreTools, BuildStatusDetails() } };
        _workspaceScroll.Content = tools;
        _workspaceScroll.IsVisible = false;
        _workspaceScroll.AutomationId = "MatrixAuxiliaryTools";
        foreach (var panel in new VisualElement[] { _coordinatePanel, _moreTools, _statusDetailsCard })
            panel.PropertyChanged += (_, e) => { if (e.PropertyName == IsVisibleProperty.PropertyName) RefreshMatrixLayout(); };

        _tableSlot.Add(_viewport);
        _viewport.VerticalOptions = LayoutOptions.Start;
        _tableSlot.SizeChanged += (_, _) =>
        {
            if (CanTouchUi && _active)
                _viewport.ViewportHeightLimit = Math.Max(1, _tableSlot.Height);
        };
        var editor = new Grid { RowSpacing = 6, RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star) } };
        editor.Add(header); editor.Add(_workspaceScroll, 0, 1); editor.Add(_tableSlot, 0, 2);
        _editorCard = Card(editor);
        _editorCard.BackgroundColor = Colors.Transparent; _editorCard.StrokeThickness = 0; _editorCard.Shadow = new Shadow { Opacity = 0 };

        _solutionCard = BuildSolutionCard();
        _closeDetails = ActionButton("Workspace.CloseDetails", "←", (_, _) => TryCloseMatrixDetails(), lockDuringWork: false);
        _closeDetails.AutomationId = "MatrixCloseDetails";
        _inspectorTitle.FontAttributes = FontAttributes.Bold; _inspectorTitle.FontSize = 17;
        SemanticProperties.SetHeadingLevel(_inspectorTitle, SemanticHeadingLevel.Level2);
        var detailHeading = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        detailHeading.Add(_inspectorTitle); detailHeading.Add(_closeDetails, 1);
        _inspectorScroll.Content = new VerticalStackLayout { Spacing = 10, Children = { _cellValuePanel, _solutionCard, _technical } };
        var inspector = new Grid { RowSpacing = 8, RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        inspector.Add(detailHeading); inspector.Add(_inspectorScroll, 0, 1);
        _inspectorCard = Card(inspector); _inspectorCard.IsVisible = false; _inspectorCard.ZIndex = 1;
        _inspectorCard.AutomationId = "MatrixInspector";
        _editorWorkArea.Add(_editorCard); _editorWorkArea.Add(_inspectorCard);
        _matrixMain.Add(setupCard); _matrixMain.Add(_editorWorkArea, 0, 1);
        root.Add(_matrixMain, 0, 1);
        _matrixMain.SizeChanged += (_, _) => RefreshMatrixLayout();
        _editorWorkArea.SizeChanged += (_, _) => RefreshMatrixLayout();
        HandlerChanging += (_, _) => DetachMatrixDetailsKeys();
    }

    private void RefreshMatrixLayout()
    {
        if (_updatingMatrixLayout || _inspectorCard is null || (!CanTouchUi && _constructed)) return;
        _updatingMatrixLayout = true;
        try
        {
            if (!HasInputs) _tableExpanded = false;
            // A short window uses the same reduced chrome without changing the
            // user's expanded-mode preference. The height is the stable host
            // height, not the area that changes when the hero is hidden.
            bool minimalChrome = _tableExpanded || (Height > 0 && Height < 480 * ResponsiveLayoutPolicy.TextScale);
            _matrixHero.IsVisible = !minimalChrome;
            bool compact = ResponsiveLayoutPolicy.UseStackedLayout(_editorWorkArea.Width, 760);
            bool side = ResponsiveLayoutPolicy.ForSolver(_editorWorkArea.Width, Height, ResponsiveLayoutPolicy.TextScale).UseSideBySide;
            double available = Math.Max(1, _matrixMain.Height);
            _setupScroll.MaximumHeightRequest = Math.Max(72, Math.Min(260, available * .30));
            _workspaceScroll.MaximumHeightRequest = Math.Max(64, Math.Min(220, available * .30));
            _workspaceScroll.IsVisible = _moreTools.IsVisible || _coordinatePanel.IsVisible || _statusDetailsCard.IsVisible;
            _inputStatus.IsVisible = _range.IsVisible = !minimalChrome;
            _secondaryTools.IsVisible = !minimalChrome;
            _rangeInMore.IsVisible = minimalChrome;
            ((EmojiActionButton)_rangeInMore).Caption = T(_viewport.IsRangeSelectionMode ? "RangeSelection.Cancel" : "RangeSelection.Start");
            _rangeInMore.IsEnabled = _rangeSelectionToggle.IsEnabled;
            string expandKey = _tableExpanded ? "Workspace.RestoreTable" : "Workspace.ExpandTable";
            _expandTable.Caption = compact ? "" : T(expandKey);
            _expandTable.Emoji = _tableExpanded ? "⤡" : "⤢";
            _expandTable.WidthRequest = compact ? Math.Max(40, DisplayDensityManager.Metrics.ControlHeight) : -1;
            SemanticProperties.SetDescription(_expandTable, T(expandKey)); ToolTipProperties.SetText(_expandTable, T(expandKey));
            ((EmojiActionButton)_moreToolsToggle).Caption = compact ? "" : T(_moreTools.IsVisible ? "Workspace.FewerTools" : "Workspace.MoreTools");
            _moreToolsToggle.WidthRequest = compact ? Math.Max(40, DisplayDensityManager.Metrics.ControlHeight) : -1;
            SemanticProperties.SetDescription(_moreToolsToggle, T("Workspace.MoreTools")); ToolTipProperties.SetText(_moreToolsToggle, T("Workspace.MoreTools"));
            _technicalDetailsToggle.IsVisible = _result is not null;
            _technicalDetailsToggle.IsEnabled = !_busy;
            _technicalDetailsToggle.Caption = compact ? "" : T("TechnicalDetails");
            _technicalDetailsToggle.WidthRequest = compact ? Math.Max(40, DisplayDensityManager.Metrics.ControlHeight) : -1;
            ToolTipProperties.SetText(_technicalDetailsToggle, T("TechnicalDetails"));

            if (_detailMatrix != _selected)
            {
                _detailsRequested = _technicalRequested = _detailsDismissed = false;
                _detailMatrix = _selected;
            }
            if (_detailRow != _viewport.SelectedRow || _detailColumn != _viewport.SelectedColumn)
            {
                _detailsDismissed = false; _detailRow = _viewport.SelectedRow; _detailColumn = _viewport.SelectedColumn;
            }
            if (_busy || !HasInputs) _detailsRequested = _technicalRequested = false;
            if (_technicalRequested && _result is null) _detailsRequested = _technicalRequested = false;
            bool show = !_busy && HasInputs && (_detailsRequested || (side && _selected == 2 && _result is not null && !_detailsDismissed));
            _phoneDetailsOpen = show && !side;
            _editorCard.IsEnabled = !_phoneDetailsOpen;
            _inspectorCard.IsVisible = show;
            _inspectorTitle.Text = T(_technicalRequested ? "TechnicalDetails" : "CellValue");
            _technical.IsVisible = show && _technicalRequested;
            _cellValuePanel.IsVisible = show && !_technicalRequested;
            _solutionCard.IsVisible = show && !_technicalRequested && _selected == 2 && _result is not null;
            if (_editorWorkArea.ColumnDefinitions.Count != (show && side ? 2 : 1))
            {
                _editorWorkArea.ColumnDefinitions.Clear(); _editorWorkArea.ColumnDefinitions.Add(new(GridLength.Star));
                if (show && side) _editorWorkArea.ColumnDefinitions.Add(new(new GridLength(360 * Math.Min(ResponsiveLayoutPolicy.TextScale, 1.4))));
            }
            Grid.SetColumn(_inspectorCard, show && side ? 1 : 0);
        }
        finally { _updatingMatrixLayout = false; }
    }

    private void OpenMatrixCellDetails()
    {
        if (!CanTouchUi || !_active || _busy || !HasInputs || !_viewport.FinishEdit()) return;
        _detailsRequested = true; _technicalRequested = _detailsDismissed = false;
        RefreshMatrixLayout();
        if (_phoneDetailsOpen) _closeDetails.Focus();
    }

    private void OpenMatrixTechnicalDetails()
    {
        if (!CanTouchUi || !_active || _busy || _result is null) return;
        _detailsRequested = _technicalRequested = true; _detailsDismissed = false;
        RefreshMatrixLayout();
        if (_phoneDetailsOpen) _closeDetails.Focus();
    }

    public bool TryCloseMatrixDetails()
    {
        if (_inspectorCard?.IsVisible != true) return false;
        _detailsRequested = _technicalRequested = false; _detailsDismissed = true;
        RefreshMatrixLayout();
        if (CanTouchUi && _active) _viewport.Refresh();
        if (CanTouchUi && _active) _cellValueToggle.Focus();
        return true;
    }

    private bool HandleMatrixDetailsEscape()
    {
        if (_inspectorCard?.IsVisible != true) return false;
        // On desktop the table keeps focus. Preserve its edit/range cancellation
        // before dismissing the inspector; narrow details behave as their own panel.
        if (!_phoneDetailsOpen)
        {
            if (_viewport.IsEditing) return false;
            if (_viewport.IsRangeSelectionMode) { _viewport.CancelRangeSelection(); return true; }
            if (_viewport.HasRangeSelection) { _viewport.Select(_viewport.SelectedRow, _viewport.SelectedColumn); return true; }
        }
        return TryCloseMatrixDetails();
    }

#if WINDOWS
    private Microsoft.UI.Xaml.UIElement? _matrixDetailsNative;
    private void AttachMatrixDetailsKeys()
    {
        DetachMatrixDetailsKeys();
        _matrixDetailsNative = Handler?.PlatformView as Microsoft.UI.Xaml.UIElement;
        if (_matrixDetailsNative is not null) _matrixDetailsNative.PreviewKeyDown += OnMatrixDetailsKeyDown;
    }
    private void DetachMatrixDetailsKeys()
    {
        if (_matrixDetailsNative is not null) _matrixDetailsNative.PreviewKeyDown -= OnMatrixDetailsKeyDown;
        _matrixDetailsNative = null;
    }
    private void OnMatrixDetailsKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == global::Windows.System.VirtualKey.Escape && HandleMatrixDetailsEscape()) e.Handled = true;
    }
#else
    private void AttachMatrixDetailsKeys() { }
    private void DetachMatrixDetailsKeys() { }
#endif
}
