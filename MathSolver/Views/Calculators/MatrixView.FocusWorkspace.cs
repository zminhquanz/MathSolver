using MathSolver.Controls;
using MathSolver.Services;

namespace MathSolver.Views;

public sealed partial class MatrixView
{
    private readonly Grid _tableSlot = new(), _editorWorkArea = new() { ColumnSpacing = 12 };
    private readonly Grid _matrixMain = new() { RowSpacing = 8, RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
    private readonly ScrollView _setupScroll = new(), _inspectorScroll = new();
    private readonly Label _inspectorTitle = TextLabel(), _resultSummary = TextLabel();
    private Grid _secondaryTools = null!, _inspectorHeading = null!;
    private readonly RowDefinition _inspectorContentRow = new(GridLength.Star);
    private Border _inspectorCard = null!, _solutionCard = null!;
    private VisualElement _matrixHero = null!;
    private EmojiActionButton _expandTable = null!, _technicalDetailsToggle = null!;
    private Button _closeDetails = null!, _rangeInMore = null!;
    private bool _tableExpanded, _detailsRequested, _technicalRequested, _toolsRequested, _detailsDismissed, _phoneDetailsOpen, _updatingMatrixLayout;
    private int _detailMatrix = -1, _detailRow = -1, _detailColumn = -1;

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
        // The common toolbar spans the whole workspace; table and inspector start on the same row.
        var workspace = new Grid { RowSpacing = 8, RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        workspace.Add(header); workspace.Add(_editorWorkArea, 0, 1);
        _matrixMain.Add(setupCard); _matrixMain.Add(workspace, 0, 1);
        root.Add(_matrixMain, 0, 1);
        _matrixMain.SizeChanged += (_, _) => RefreshMatrixLayout();
        _editorWorkArea.SizeChanged += (_, _) => RefreshMatrixLayout();
        HandlerChanging += (_, _) => DetachMatrixDetailsKeys();
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
            if (!HasInputs) _tableExpanded = false;
            bool shortWindow = Height > 0 && Height < 480 * ResponsiveLayoutPolicy.TextScale;
            bool minimalChrome = _tableExpanded || shortWindow;
            // Expanding the table must not change the page header.
            _matrixHero.IsVisible = !shortWindow;
            bool side = ResponsiveLayoutPolicy.ForSolver(_editorWorkArea.Width, Height, ResponsiveLayoutPolicy.TextScale).UseSideBySide;
            double available = Math.Max(1, _matrixMain.Height);
            _setupScroll.MaximumHeightRequest = Math.Max(72, Math.Min(260, available * .30));
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

            if (_detailMatrix != _selected)
            {
                _detailsRequested = _technicalRequested = _detailsDismissed = false;
                CloseMatrixTools(); _detailMatrix = _selected;
            }
            if (_detailRow != _viewport.SelectedRow || _detailColumn != _viewport.SelectedColumn)
            {
                _detailsDismissed = false; _detailRow = _viewport.SelectedRow; _detailColumn = _viewport.SelectedColumn;
            }
            // Read-only status details remain usable after failed recreation,
            // when no input drafts exist. They do not depend on a selected cell.
            bool statusDetails = _toolsRequested && _statusDetailsCard.IsVisible
                && !_moreTools.IsVisible && !_coordinatePanel.IsVisible;
            if (_busy || !HasInputs)
            {
                _detailsRequested = _technicalRequested = false;
                if (!statusDetails) CloseMatrixTools();
            }
            if (_technicalRequested && _result is null) _detailsRequested = _technicalRequested = false;
            bool show = statusDetails || (!_busy && HasInputs && (_toolsRequested || _detailsRequested || (side && _selected == 2 && _result is not null && !_detailsDismissed)));
            _phoneDetailsOpen = show && !side;
            _editorCard.IsEnabled = !_phoneDetailsOpen;
            _inspectorCard.IsVisible = show;
            _inspectorTitle.Text = statusDetails ? T("Workspace.Details") : _toolsRequested ? T(_coordinatePanel.IsVisible ? "Go" : "Workspace.MoreTools")
                : _technicalRequested ? T("TechnicalDetails") : _selection.Text;
            _workspaceScroll.IsVisible = show && _toolsRequested;
            _inspectorScroll.IsVisible = show && !_toolsRequested;
            _technical.IsVisible = show && !_toolsRequested && _technicalRequested;
            _cellValuePanel.IsVisible = show && !_toolsRequested && !_technicalRequested;
            _solutionCard.IsVisible = show && !_toolsRequested && !_technicalRequested && _selected == 2 && _result is not null;
            // A/B contain one Int64 value: hug its content instead of filling the
            // entire table height. Keep a bounded scroller for enlarged text.
            bool valueOnly = show && !_toolsRequested && !_technicalRequested && _selected != 2;
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
        button.Caption = ""; button.Emoji = icon;
        button.WidthRequest = Math.Max(40, DisplayDensityManager.Metrics.ControlHeight);
        SemanticProperties.SetDescription(button, T(key)); ToolTipProperties.SetText(button, T(key));
    }

    private void OpenMatrixCellDetails()
    {
        if (!CanTouchUi || !_active || _busy || !HasInputs || !_viewport.FinishEdit()) return;
        CloseMatrixTools(); _detailsRequested = true; _technicalRequested = _detailsDismissed = false;
        RefreshWorkspacePresentation();
        if (_phoneDetailsOpen) _closeDetails.Focus();
    }

    private void OpenMatrixTechnicalDetails()
    {
        if (!CanTouchUi || !_active || _busy || _result is null) return;
        CloseMatrixTools(); _detailsRequested = _technicalRequested = true; _detailsDismissed = false;
        RefreshWorkspacePresentation();
        if (_phoneDetailsOpen) _closeDetails.Focus();
    }

    public bool TryCloseMatrixDetails()
    {
        if (_inspectorCard?.IsVisible != true) return false;
        CloseMatrixTools(); _detailsRequested = _technicalRequested = false; _detailsDismissed = true;
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
