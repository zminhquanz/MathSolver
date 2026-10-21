using MathSolver.Controls;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Views.Base;
using System.Globalization;

namespace MathSolver.Views;

/// <summary>Exact integer matrix editor. Only one bounded viewport is instantiated.</summary>
public sealed partial class MatrixView : LocalizedSolverView
{
    private MatrixDraft _a = new(3, 3), _b = new(3, 3);
    private MatrixCalculation? _result;
    private MatrixOperation _operation;
    private int _selected, _version;
    private bool _busy, _active = true;
    private CancellationTokenSource? _work;
    private readonly MatrixViewport _viewport = new();
    private readonly Entry _aRows = NumberEntry("3"), _aColumns = NumberEntry("3"),
        _bRows = NumberEntry("3"), _bColumns = NumberEntry("3");
    private readonly Entry _row = NumberEntry("1"), _column = NumberEntry("1"), _value = NumberEntry("");
    private readonly Label _selection = TextLabel(), _inputStatus = TextLabel(), _message = TextLabel(),
        _range = TextLabel();
    private readonly Label _heroSymbol = new();
    private readonly ProgressBar _progress = new() { IsVisible = false };
    private readonly ActivityIndicator _activity = new() { IsVisible = false, WidthRequest = 24, HeightRequest = 24 };
    private readonly List<Action> _translations = [];
    private readonly List<Button> _lockDuringWork = [];
    private readonly Button[] _operations = new Button[3], _matrices = new Button[3];
    private readonly Button _apply, _fill, _paste, _import, _export, _calculate, _stop;
    private readonly Grid _dimensions = new() { ColumnSpacing = 12, RowSpacing = 12 };
    private string? _statusKey;
    private double? _elapsed;
    private int _editorMatrix = -1, _editorRow, _editorColumn;
    private bool _updatingEditor;
    private Window? _hostWindow;
    private bool _constructed;
    private string? _errorKey;
    private object[] _errorArguments = [];

    public MatrixView()
    {
        LocalizationService.ExcludeSubtreeFromLegacyTracking(this);
        BackgroundColor = Colors.Transparent;
        var hero = new VerticalStackLayout { Style = GetStyle("SolverHeroStyle"), Children =
        {
            _heroSymbol,
            Caption("Title", "SolverHeroTitleStyle"), Caption("Subtitle", "SolverHeroSubtitleStyle")
        }};
        _heroSymbol.Style = GetStyle("SolverHeroSymbolStyle");
        var fixedHeader = new VerticalStackLayout { Style = GetStyle("SolverFixedHeaderStyle"), Children = { hero } };
        var body = new VerticalStackLayout { Style = GetStyle("SolverScrollableContentStyle"), Spacing = 14 };

        var setup = new VerticalStackLayout { Spacing = 12 };
        setup.Add(Caption("Operation", "SolverSectionTitleStyle"));
        var operationGrid = FlowGrid(150, 3);
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            _operations[i] = ActionButton(new[] { "Add", "Subtract", "Multiply" }[i], new[] { "+", "−", "×" }[i],
                (_, _) => { if (!CommitPendingCell()) return; _operation = (MatrixOperation)index; InvalidateResult(); RefreshSelection(); });
            operationGrid.Add(_operations[i]);
        }
        setup.Add(operationGrid);
        setup.Add(Caption("DimensionsHelp"));
        AdaptiveColumns.SetMaximumColumns(_dimensions, 2);
        AdaptiveColumns.SetMinimumColumnWidth(_dimensions, 270);
        _dimensions.Add(DimensionPanel("A", _aRows, _aColumns));
        _dimensions.Add(DimensionPanel("B", _bRows, _bColumns));
        setup.Add(_dimensions);
        setup.Add(ActionButton("Create", "▦", OnCreate));
        body.Add(Card(setup));

        var editor = new VerticalStackLayout { Spacing = 10 };
        var matrixTabs = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) } };
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            _matrices[i] = ActionButton("Matrix" + "ABC"[i], "▦", (_, _) =>
            { if (!CommitPendingCell()) return; _selected = index; RefreshSource(); });
            matrixTabs.Add(_matrices[i], i, 0);
        }
        editor.Add(matrixTabs);
        editor.Add(_inputStatus);
        _viewport.SetInlineEditor(_value);
        _viewport.CommitEdit = CommitPendingCell;
        _viewport.CanInteract = () => _active;
        _viewport.CanEdit = () => !_busy && _selected != 2;
        _viewport.EditCancelled += () =>
        {
            if (!CanTouchUi || _busy) return;
            RefreshCell();
            if (_errorKey == "InvalidCell") { _errorKey = null; _message.Text = ""; }
        };
        editor.Add(_viewport);
        editor.Add(_range);
        AddSizeControls(editor);
        editor.Add(Caption("NavigationHelp"));
        var navigation = FlowGrid(130, 3);
        navigation.Add(Labeled("Row", _row, 56)); navigation.Add(Labeled("Column", _column, 56));
        var go = ActionButton("Go", "↪", OnGo, lockDuringWork: false);
        go.HeightRequest = 56;
        go.VerticalOptions = LayoutOptions.End;
        navigation.Add(go);
        editor.Add(navigation);
        editor.Add(_selection);
        _value.Keyboard = Keyboard.Default; // Includes a minus key on mobile.
        _value.MaxLength = 64;
        _value.FontSize = 14;
        _value.Completed += (_, _) =>
        {
#if !WINDOWS
            if (CanTouchUi && !_busy) _viewport.Navigate(MatrixCellMove.Down);
#endif
        };
        _value.Unfocused += (_, _) => { if (CanTouchUi && !_updatingEditor && !_busy) CommitPendingCell(); };
        _value.TextChanged += (_, _) =>
        { if (!_updatingEditor && !_busy && _selected != 2) InvalidateResult(); };
        SemanticProperties.SetDescription(_value, T("CellValue"));
        var cellActions = FlowGrid(140, 3);
        _apply = ActionButton("Apply", "✓", (_, _) => ApplyCell(advance: true));
        cellActions.Add(_apply);
        cellActions.Add(ActionButton("Previous", "←", (_, _) => MoveSelection(-1), lockDuringWork: false));
        cellActions.Add(ActionButton("Next", "→", (_, _) => MoveSelection(1), lockDuringWork: false));
        editor.Add(cellActions);
        var transfers = FlowGrid(180, 3);
        _fill = ActionButton("FillZero", "0", (_, _) =>
        { if (!CommitPendingCell()) return; CurrentDraft.FillMissingWithZero(); InvalidateResult(); RefreshSource(); });
        _paste = ActionButton("Paste", "📋", OnPaste);
        _import = ActionButton("Import", "📂", OnImport);
        transfers.Add(_fill); transfers.Add(_paste); transfers.Add(_import);
        editor.Add(transfers);
        body.Add(Card(editor));

        var outcome = new VerticalStackLayout { Spacing = 10 };
        _activity.SetDynamicResource(ActivityIndicator.ColorProperty, "PrimaryColor");
        var statusLine = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        statusLine.Add(_activity, 0, 0); statusLine.Add(_message, 1, 0);
        outcome.Add(statusLine);
        outcome.Add(_progress);
        var run = FlowGrid(160, 3);
        _calculate = ActionButton("Calculate", "🧮", OnCalculate, primary: true);
        _stop = ActionButton("Stop", "■", (_, _) => _work?.Cancel(), lockDuringWork: false);
        _export = ActionButton("Export", "💾", OnExport);
        run.Add(_calculate); run.Add(_stop); run.Add(_export);
        outcome.Add(run);
        body.Add(Card(outcome));
        body.Add(BuildResultCard());
        body.Add(BuildSolutionCard());
        var root = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        root.Add(fixedHeader);
        root.Add(new ScrollView { Content = body }, 0, 1);
        Content = root;
        _viewport.CellSelected += (_, _) => RefreshCell();
        _viewport.ViewportChanged += RefreshRange;
        InteractiveButtonAnimation.SetIsScopeEnabled(this, true);
        RefreshLocalizedContent();
        RefreshSource();
        _constructed = true;
    }

    private MatrixDraft CurrentDraft => _selected == 1 ? _b : _a;
    private static string T(string key) => LocalizationService.TranslateKey("Matrix." + key);
    private static string F(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, T(key), args);
    private static Style GetStyle(string key) => (Style)Application.Current!.Resources[key];
    private static Label TextLabel()
    {
        var label = new Label { FontSize = 15, LineBreakMode = LineBreakMode.WordWrap };
        label.SetDynamicResource(Label.TextColorProperty, "WallpaperTextPrimaryColor");
        return label;
    }
    private static Entry NumberEntry(string text) => MatrixInputField.CreateEntry(text);
    private Label Caption(string key, string? style = null)
    {
        var label = TextLabel();
        if (style is not null) label.Style = GetStyle(style);
        _translations.Add(() => label.Text = T(key));
        return label;
    }
    private View Labeled(string key, Entry entry, double? height = null)
    {
        _translations.Add(() => SemanticProperties.SetDescription(entry, T(key)));
        var field = MatrixInputField.Wrap(entry);
        if (height is { } fixedHeight) field.HeightRequest = fixedHeight;
        return new VerticalStackLayout { Spacing = 4, Children = { Caption(key), field } };
    }
    private View DimensionPanel(string name, Entry rows, Entry columns)
    {
        var grid = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) } };
        grid.Add(Labeled("Rows", rows), 0, 0); grid.Add(Labeled("Columns", columns), 1, 0);
        return new VerticalStackLayout { Spacing = 5, Children = { Caption("Matrix" + name), grid } };
    }
    private static Grid FlowGrid(double minimum, int maximum)
    {
        var grid = new Grid { RowSpacing = 8, ColumnSpacing = 8 };
        AdaptiveColumns.SetMaximumColumns(grid, maximum);
        AdaptiveColumns.SetMinimumColumnWidth(grid, minimum);
        return grid;
    }
    private static Border Card(View content) => new() { Style = GetStyle("SolverCardStyle"), Content = content };
    private Button ActionButton(string key, string emoji, EventHandler handler, bool primary = false, bool lockDuringWork = true)
    {
        var button = new EmojiActionButton { Emoji = emoji, Style = GetStyle(primary ? "SolverPrimaryActionStyle" : "SolverSecondaryActionStyle"),
            FontSize = 15, MinimumHeightRequest = 46, Padding = new Thickness(10, 8) };
        _translations.Add(() => button.Caption = T(key));
        button.Clicked += handler;
        if (lockDuringWork) _lockDuringWork.Add(button);
        return button;
    }

    protected override void RefreshLocalizedContent()
    {
        if (!CanRefreshLocalization) return;
        bool preserveInvalidEntry = !CommitPendingCell();
        foreach (var update in _translations) update();
        if (_calculate is null) return;
        _viewport.SetAccessibility(T("TableHelp"));
        SemanticProperties.SetDescription(_value, T("CellValue"));
        RefreshSelection();
        if (!preserveInvalidEntry) RefreshCell();
        else _selection.Text = F("Selected", "ABC"[_selected], _viewport.SelectedRow + 1, _viewport.SelectedColumn + 1);
        RefreshRange(); RefreshExecution();
        if (_statusKey is not null) _message.Text = T(_statusKey);
        else if (_errorKey is not null) _message.Text = FormatError();
        _viewport.Refresh();
    }
}
