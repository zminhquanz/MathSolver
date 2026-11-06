using MathSolver.Controls;
using MathSolver.Services;

namespace MathSolver.Views;

public sealed partial class MatrixView
{
    private readonly Grid _tableSlot = new(), _editorWorkArea = new() { ColumnSpacing = 12 };
    private readonly Grid _matrixMain = new() { RowSpacing = 8, RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
    private readonly ScrollView _setupScroll = new(), _inspectorScroll = new();
    private readonly ScrollView _workspaceChrome = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Default };
    private readonly Label _inspectorTitle = TextLabel(), _resultSummary = TextLabel();
    private Grid _secondaryTools = null!, _inspectorHeading = null!;
    private readonly RowDefinition _inspectorContentRow = new(GridLength.Star);
    private Border _inspectorCard = null!, _solutionCard = null!;
    private VisualElement _matrixHero = null!;
    private EmojiActionButton _expandTable = null!, _technicalDetailsToggle = null!;
    private Button _closeDetails = null!, _rangeInMore = null!;
    private bool _tableExpanded, _detailsRequested, _technicalRequested, _toolsRequested, _detailsDismissed, _phoneDetailsOpen, _updatingMatrixLayout;
    private int _detailMatrix = -1, _detailRow = -1, _detailColumn = -1;
#if WINDOWS
    private Grid _desktopSetupHeading = null!;
    private bool _desktopCompactSpacing;
#endif

    private void BuildFocusedWorkspace(Grid root, VisualElement hero, Border setupCard, VerticalStackLayout header, Grid matrixTabs)
    {
        _matrixHero = hero;
        // Match the shared header/footer and other calculator bodies without a page scroller.
        _matrixMain.HorizontalOptions = LayoutOptions.Center;
        _matrixMain.MaximumWidthRequest = 1320;
        // Clamp the measured width too: MaximumWidthRequest alone can leave a Grid's
        // children measured at the larger WidthRequest on WinUI.
        root.SizeChanged += (_, _) =>
        { if (root.Width > 0) _matrixMain.WidthRequest = Math.Min(1320, root.Width); };
        _matrixMain.SetDynamicResource(Grid.PaddingProperty, "Density.ScrollPadding");
        _expandTable = (EmojiActionButton)ActionButton("Workspace.ExpandTable", "⤢", (_, _) =>
        {
            if (!HasInputs || !_viewport.FinishEdit()) return;
            _tableExpanded = !_tableExpanded;
            if (_tableExpanded) { _setupPanel.IsVisible = false; CloseMatrixTools(); }
            RefreshWorkspacePresentation();
        }, lockDuringWork: false);
        _expandTable.AutomationId = "MatrixExpandTable";
        ((Grid)_moreToolsToggle.Parent).Children.Remove(_moreToolsToggle);
        ((Grid)_rangeSelectionToggle.Parent).Children.Remove(_rangeSelectionToggle);
        _rangeInMore = _rangeSelectionToggle;
        _moreTools.Children.Insert(0, _rangeSelectionToggle);
        // Reserve all three action slots so publishing C cannot resize the A/B/C tabs.
        var heading = new Grid { ColumnSpacing = 6, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto) } };
        heading.Add(matrixTabs); heading.Add(_expandTable, 1); heading.Add(_moreToolsToggle, 2); heading.Add(_technicalDetailsToggle, 3);
        BuildPhoneWorkspace(root, setupCard, heading);
        header.Children.Insert(0, heading);
        header.Spacing = 6;

        _workspaceScroll.Content = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(0, 4, 24, 4), Children = { _coordinatePanel, _moreTools, BuildStatusDetails() } };
        _workspaceScroll.IsVisible = false;
        _workspaceScroll.AutomationId = "MatrixAuxiliaryTools";
        _tableSlot.Add(_viewport);
        _viewport.VerticalOptions = LayoutOptions.Start;
        _tableSlot.SizeChanged += (_, _) =>
        {
            if (CanTouchUi && _active) _viewport.ViewportHeightLimit = Math.Max(1, _tableSlot.Height);
        };
        _editorCard = Card(_tableSlot);
        _editorCard.Padding = 0;
        _editorCard.BackgroundColor = Colors.Transparent; _editorCard.StrokeThickness = 0; _editorCard.Shadow = new Shadow { Opacity = 0 };

        _solutionCard = BuildSolutionCard();
        _closeDetails = ActionButton("Workspace.CloseDetails", "←", (_, _) => TryCloseMatrixDetails(), lockDuringWork: false);
        _closeDetails.AutomationId = "MatrixCloseDetails";
        _inspectorTitle.FontAttributes = FontAttributes.Bold; _inspectorTitle.FontSize = 17;
        SemanticProperties.SetHeadingLevel(_inspectorTitle, SemanticHeadingLevel.Level2);
        var detailHeading = _inspectorHeading = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        detailHeading.Add(_inspectorTitle); detailHeading.Add(_closeDetails, 1);
        detailHeading.SizeChanged += (_, _) => RefreshMatrixLayout();
        _fullCellTitle.IsVisible = false; // The panel heading already identifies the selected cell.
        _inspectorScroll.Content = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(0, 4, 24, 4), Children = { _cellValuePanel, _solutionCard, _technical } };
        var detailBody = new Grid();
        detailBody.Add(_inspectorScroll); detailBody.Add(_workspaceScroll);
        var inspector = new Grid { RowSpacing = 8, RowDefinitions = { new(GridLength.Auto), _inspectorContentRow } };
        inspector.Add(detailHeading); inspector.Add(detailBody, 0, 1);
        _inspectorCard = Card(inspector); _inspectorCard.IsVisible = false; _inspectorCard.ZIndex = 1;
        _inspectorCard.AutomationId = "MatrixInspector";
        _editorWorkArea.Add(_editorCard); _editorWorkArea.Add(_inspectorCard);
        // Auto-sized configuration/tool rows can consume the entire star row on
        // phones. Bound only this chrome; the table keeps its own pixel scrolling.
        _workspaceChrome.AutomationId = "MatrixWorkspaceChrome";
#if WINDOWS
        // Configuration and tools share one bounded inspector. The primary
        // toolbar has no scrolling ancestor, so wheel input belongs to the table.
        var setupBody = (VerticalStackLayout)setupCard.Content!;
        setupBody.Remove(_desktopSetupHeading);
        setupBody.Remove(_setupScroll);
        detailBody.Add(_setupScroll);
        _workspaceChrome.IsVisible = false;
        var fixedTools = new VerticalStackLayout { Spacing = 8, AutomationId = "MatrixFixedTools",
            Children = { _desktopSetupHeading, header } };
        _matrixMain.Add(fixedTools);
#else
        _workspaceChrome.Content = new VerticalStackLayout { Spacing = 8, Children = { setupCard, header } };
        _matrixMain.Add(_workspaceChrome);
#endif
        _matrixMain.Add(_editorWorkArea, 0, 1);
        root.Add(_matrixMain, 0, 1);
        _matrixMain.SizeChanged += (_, _) => RefreshMatrixLayout();
        _editorWorkArea.SizeChanged += (_, _) => RefreshMatrixLayout();
        HandlerChanging += (_, _) => { DetachMatrixDetailsKeys(); DetachMatrixKeyboard(); };
    }

    private void CloseMatrixTools()
    {
        _toolsRequested = false;
        _moreTools.IsVisible = _coordinatePanel.IsVisible = false;
    }

    private void OpenMatrixTools()
    {
        _toolsRequested = true;
        _detailsRequested = _technicalRequested = false;
        _detailsDismissed = true;
        _setupPanel.IsVisible = false;
    }

    private void RefreshMatrixLayout()
    {
        if (_updatingMatrixLayout || _inspectorCard is null || (!CanTouchUi && _constructed)) return;
        _updatingMatrixLayout = true;
        try
        {
            var space = WorkspaceLayoutCapture.Capture(this);
            bool shortWindow = space.AvailableHeight > 0 && space.EffectiveHeight < 480;
            bool phone = HasInputs && !_setupPanel.IsVisible && space.EffectiveWidth < 600;
            bool side = ResponsiveLayoutPolicy.ForSolver(WorkspaceLayoutCapture.Capture(_editorWorkArea)).UseSideBySide;
            var state = MatrixPresentationPolicy.Resolve(new MatrixPresentationInput
            {
                HasInputs = HasInputs, HasResult = _result is not null, SelectedMatrix = _selected,
                SideBySide = side, Busy = _busy, SetupRequested = _setupPanel.IsVisible,
#if WINDOWS
                IsWindows = true,
#endif
                DetailsRequested = _detailsRequested, TechnicalRequested = _technicalRequested,
                ToolsRequested = _toolsRequested, DetailsDismissed = _detailsDismissed,
                TableExpanded = _tableExpanded, StatusDetailsAvailable = _statusDetailsCard.IsVisible,
                MoreToolsVisible = _moreTools.IsVisible, CoordinateVisible = _coordinatePanel.IsVisible,
                SelectionChanged = _detailMatrix != _selected,
                CellChanged = _detailRow != _viewport.SelectedRow || _detailColumn != _viewport.SelectedColumn
            });
            _detailsRequested = state.DetailsRequested; _technicalRequested = state.TechnicalRequested;
            _detailsDismissed = state.DetailsDismissed; _tableExpanded = state.TableExpanded;
            if (!state.ToolsRequested) CloseMatrixTools();
            _detailMatrix = _selected; _detailRow = _viewport.SelectedRow; _detailColumn = _viewport.SelectedColumn;
#if ANDROID
            // Rotating a phone does not turn its touch workspace into desktop.
            phone |= HasInputs && !_setupPanel.IsVisible && DeviceInfo.Current.Idiom == DeviceIdiom.Phone;
#endif
#if WINDOWS
            phone = false; // Narrow desktop uses the same inspector, preserving its fixed toolbar.
#endif
            bool minimalChrome = _tableExpanded || shortWindow;
            // Expanding the table must not change the page header.
#if WINDOWS
            _matrixHero.IsVisible = true; // Shared compact header keeps the topic visible.
            _desktopSetupHeading.IsVisible = !shortWindow;
            _phoneSetup.IsVisible = shortWindow;
            SetMatrixToolPresentation(_phoneSetup, "Workspace.EditSetup", "⚙");
            if (_desktopCompactSpacing != shortWindow)
            {
                _desktopCompactSpacing = shortWindow;
                if (shortWindow)
                {
                    _matrixMain.RemoveDynamicResource(Grid.PaddingProperty);
                    double inset = DisplayDensityManager.Metrics.PagePadding;
                    _matrixMain.Padding = new Thickness(inset, 0, inset, 4);
                }
                else _matrixMain.SetDynamicResource(Grid.PaddingProperty, "Density.ScrollPadding");
            }
            _matrixMain.RowSpacing = shortWindow ? 4 : 8;
            // Routine notices repeat the setup summary. Preserve errors and
            // running status, but spend this height on data rows while editing.
            if (_footerStatusRow is not null)
                _footerStatusRow.IsVisible = (_message.IsVisible || _statusDetailsButton.IsVisible)
                    && (!shortWindow || _busy || _errorKey is not null);
#else
            _matrixHero.IsVisible = !shortWindow && !phone;
#endif
            double available = Math.Max(1, _matrixMain.Height);
            var tableSpace = WorkspaceLayoutCapture.Capture(_matrixMain,
                _matrixMain.Padding.HorizontalThickness, _matrixMain.Padding.VerticalThickness + _matrixMain.RowSpacing);
            double usable = Math.Max(1, tableSpace.AvailableHeight);
            double rowHeight = DisplayDensityManager.Metrics.MatrixRowHeight * Math.Clamp(ResponsiveLayoutPolicy.TextScale, 1, 1.6);
            // Reserve a header plus at least one row before measuring optional
            // chrome. When the keyboard leaves very little room, keep the table.
            double reserved = Math.Min(usable, Math.Clamp(usable / 2, rowHeight * 2, rowHeight * 3));
            double chromeLimit = usable - reserved;
            // While editing dimensions, the form owns the space (including with
            // the IME open). Collapse it after Create before reserving the table.
#if !WINDOWS
            _workspaceChrome.IsVisible = _setupPanel.IsVisible || chromeLimit >= DisplayDensityManager.Metrics.ControlHeight;
            _workspaceChrome.MaximumHeightRequest = Math.Max(1, _setupPanel.IsVisible ? usable : chromeLimit);
            _setupScroll.MaximumHeightRequest = _setupPanel.IsVisible
                ? double.PositiveInfinity : Math.Max(72, Math.Min(260, available * .30));
#else
            _setupScroll.MaximumHeightRequest = double.PositiveInfinity;
#endif
            _inputStatus.IsVisible = _range.IsVisible = !minimalChrome;
            _secondaryTools.IsVisible = false;
            _rangeInMore.IsVisible = true;
            ((EmojiActionButton)_rangeInMore).Caption = T(_viewport.IsRangeSelectionMode ? "RangeSelection.Cancel" : "RangeSelection.Start");
            _rangeInMore.IsEnabled = _rangeSelectionToggle.IsEnabled;
            string expandKey = _tableExpanded ? "Workspace.RestoreTable" : "Workspace.ExpandTable";
            SetMatrixToolPresentation(_expandTable, expandKey, _tableExpanded ? "⤡" : "⤢");
            SetMatrixToolPresentation((EmojiActionButton)_moreToolsToggle, _moreTools.IsVisible ? "Workspace.FewerTools" : "Workspace.MoreTools", "⋯");
            SetMatrixToolPresentation(_technicalDetailsToggle, "TechnicalDetails", "ⓘ");
            _technicalDetailsToggle.IsVisible = true;
            _technicalDetailsToggle.IsEnabled = !_busy && _result is not null;
#if !WINDOWS
            RefreshPhoneWorkspace(phone);
#else
            _copySelection.IsVisible = HasInputs;
            _range.IsVisible = _viewport.HasRangeSelection;
#endif

            // A single projection owns inspector visibility; view flags cannot
            // independently expose overlapping content.
            bool show = state.IsOpen;
            _phoneDetailsOpen = !state.EditorEnabled;
            _editorCard.IsEnabled = state.EditorEnabled;
            _inspectorCard.IsVisible = show;
            _inspectorTitle.Text = state.Panel switch
            {
                MatrixPanel.Setup => T("Workspace.SetupTitle"),
                MatrixPanel.Status => T("Workspace.Details"),
                MatrixPanel.Coordinates => T("Go"),
                MatrixPanel.Tools => T("Workspace.MoreTools"),
                MatrixPanel.Technical => T("TechnicalDetails"),
                _ => _selection.Text
            };
            _workspaceScroll.IsVisible = state.UsesTools;
            _inspectorScroll.IsVisible = state.Panel is MatrixPanel.Cell or MatrixPanel.Technical;
            _technical.IsVisible = state.ShowTechnical;
            _cellValuePanel.IsVisible = state.Panel == MatrixPanel.Cell;
            _solutionCard.IsVisible = state.ShowSolution;
            // A/B contain one Int64 value: hug its content, with a bounded scroller.
            bool valueOnly = state.ValueOnly;
            _inspectorCard.VerticalOptions = valueOnly ? LayoutOptions.Start : LayoutOptions.Fill;
            _inspectorContentRow.Height = valueOnly ? GridLength.Auto : GridLength.Star;
            _inspectorScroll.MaximumHeightRequest = valueOnly
                ? Math.Max(1, _editorWorkArea.Height - Math.Max(0, _inspectorHeading.Height)
                    - _inspectorCard.Padding.VerticalThickness - 8)
                : double.PositiveInfinity;
            if (_editorWorkArea.ColumnDefinitions.Count != (show && side ? 2 : 1))
            {
                _editorWorkArea.ColumnDefinitions.Clear(); _editorWorkArea.ColumnDefinitions.Add(new(GridLength.Star));
                if (show && side) _editorWorkArea.ColumnDefinitions.Add(new(new GridLength(360 * Math.Min(ResponsiveLayoutPolicy.TextScale, 1.4))));
            }
            Grid.SetColumn(_inspectorCard, show && side ? 1 : 0);
        }
        finally { _updatingMatrixLayout = false; }
    }

    private static void SetMatrixToolPresentation(EmojiActionButton button, string key, string icon)
    {
        button.VectorIconKey = key;
#if WINDOWS
        button.Padding = Thickness.Zero;
#endif
        button.Caption = ""; button.Emoji = icon;
        button.WidthRequest = Math.Max(40, DisplayDensityManager.Metrics.ControlHeight);
        SemanticProperties.SetDescription(button, T(key)); ToolTipProperties.SetText(button, T(key));
    }

    private void OpenMatrixCellDetails()
    {
        if (!CanTouchUi || !_active || _busy || !HasInputs || !_viewport.FinishEdit()) return;
        CloseMatrixTools(); _detailsRequested = true; _technicalRequested = _detailsDismissed = false;
#if WINDOWS
        _setupPanel.IsVisible = false;
#endif
        RefreshWorkspacePresentation();
        if (_phoneDetailsOpen) _closeDetails.Focus();
    }

    private void OpenMatrixTechnicalDetails()
    {
        if (!CanTouchUi || !_active || _busy || _result is null) return;
        CloseMatrixTools(); _detailsRequested = _technicalRequested = true; _detailsDismissed = false;
#if WINDOWS
        _setupPanel.IsVisible = false;
#endif
        RefreshWorkspacePresentation();
        if (_phoneDetailsOpen) _closeDetails.Focus();
    }

    public bool TryCloseMatrixDetails()
    {
        if (_inspectorCard?.IsVisible != true) return false;
        CloseMatrixTools(); _detailsRequested = _technicalRequested = false; _detailsDismissed = true;
#if WINDOWS
        _setupPanel.IsVisible = false;
#endif
        RefreshWorkspacePresentation();
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
