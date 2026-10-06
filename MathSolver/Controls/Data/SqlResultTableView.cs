using System.Globalization;
using System.Collections.ObjectModel;

namespace MathSolver.Controls;

public sealed class SqlResultCellEventArgs(int rowNumber, string columnName, string value, int columnIndex) : EventArgs
{
    public int RowNumber { get; } = rowNumber;
    public string ColumnName { get; } = columnName;
    public string Value { get; } = value;
    public int ColumnIndex { get; } = columnIndex;
}

public sealed record SqlInsertColumn(string Name, string InitialValue, bool IsKey = false);

/// <summary>A bounded table with a fixed header and recycled rows; cell actions are owned by its page.</summary>
public sealed class SqlResultTableView : ContentView
{
    private const double RowHeight = 44;
    private const double NumberWidth = 52;
    private const double InsertRowHeight = 116;
    private readonly Grid _insertRow = new() { ColumnSpacing = 0, RowSpacing = 0, IsVisible = false };
    private readonly Dictionary<string, DraftCell> _insertCells = [];
    private readonly List<(InputView Input, CheckBox? Null, SqlInsertColumn Column)> _insertInputs = [];
    private readonly Dictionary<string, DraftCell> _editCells = [];
    private readonly List<(InputView Input, CheckBox? Null, SqlInsertColumn Column)> _editInputs = [];
    private ObservableCollection<ResultRow> _resultRows = [];
    private SqlInsertColumn[] _editColumns = [];
    private string _nullText = "NULL";
    private string _keyPlaceholder = "";
    private string? _focusColumn;
    private double[] _columnWidths = [];
    private readonly Grid _header = new() { ColumnSpacing = 0, RowSpacing = 0, HeightRequest = RowHeight };
    private readonly CollectionView _rows = new()
    {
        SelectionMode = SelectionMode.None,
        ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 0 },
        ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems,
        VerticalScrollBarVisibility = ScrollBarVisibility.Always,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Never
    };
    private readonly Grid _table = new() { RowSpacing = 0, RowDefinitions = { new(RowHeight), new(0), new(GridLength.Star) } };
    private readonly MouseDragScrollView _scroll = new()
    {
        ScrollInPointerDirection = true,
        Orientation = ScrollOrientation.Horizontal,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Always,
        VerticalScrollBarVisibility = ScrollBarVisibility.Never
    };

    private sealed record ResultRow(int Number, string[] Values, bool Editing = false);
    private sealed class DraftCell : BindableObject
    {
        public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(DraftCell), "");
        public static readonly BindableProperty IsNullProperty = BindableProperty.Create(nameof(IsNull), typeof(bool), typeof(DraftCell), false,
            propertyChanged: (target, _, _) => ((DraftCell)target).OnPropertyChanged(nameof(CanEdit)));
        public string Text { get => (string?)GetValue(TextProperty) ?? ""; set => SetValue(TextProperty, value); }
        public bool IsNull { get => (bool)GetValue(IsNullProperty); set => SetValue(IsNullProperty, value); }
        public bool CanEdit => !IsNull;
    }
    public bool IsInserting => _insertCells.Count > 0;
    public int EditingRowNumber { get; private set; }
    public bool IsEditing => EditingRowNumber > 0;
    public event EventHandler<SqlResultCellEventArgs>? CellSelected;
    public event EventHandler? EditChanged;

    public SqlResultTableView()
    {
        _table.Add(_header);
        _table.Add(_insertRow, 0, 1);
        _table.Add(_rows, 0, 2);
        _scroll.Content = _table;
        Content = _scroll;
    }

    public void Clear()
    {
        CancelInsert();
        CancelEdit();
        _resultRows.Clear();
        _columnWidths = [];
        _rows.ItemsSource = null;
        _rows.ItemTemplate = null;
        _header.Children.Clear();
        _header.ColumnDefinitions.Clear();
        _table.WidthRequest = -1;
        IsVisible = false;
    }

    public void SetData(string[] columns, IReadOnlyList<string[]> rows)
    {
        Clear();
        if (columns.Length == 0) return;
        // Fixed widths keep recycled rows aligned with the header, including duplicate aliases.
        double[] widths = columns.Select((column, index) => Math.Clamp(
            Math.Max(column.Length, rows.Count == 0 ? 0 : rows.Max(row => Math.Min(row[index].Length, 40))) * 7d + 28,
            110d, 308d)).Prepend(NumberWidth).ToArray();
        _columnWidths = widths;
        for (int i = 0; i < widths.Length; i++)
            _header.ColumnDefinitions.Add(new ColumnDefinition(widths[i]));
        _header.Add(CreateCell("#", true), 0, 0);
        for (int i = 0; i < columns.Length; i++)
            _header.Add(CreateCell(columns[i], true), i + 1, 0);

        _table.WidthRequest = widths.Sum();
        // Both axes have an explicit viewport; the outer page never measures all rows.
        _rows.ItemTemplate = new DataTemplate(() => CreateRow(columns, widths));
        _resultRows = new(rows.Select((values, index) => new ResultRow(index + 1, values)));
        _rows.ItemsSource = _resultRows;
        UpdateViewport();
        IsVisible = true;
        // A new result starts at its first column; header and body share this viewport.
        _ = _scroll.ScrollToAsync(0, 0, false);
    }

    /// <summary>Add an editable draft aligned with the current projection, without writing SQLite.</summary>
    public void BeginInsert(IReadOnlyList<SqlInsertColumn> columns, string nullText, string keyPlaceholder)
    {
        if (columns.Count == 0 || _columnWidths.Length != columns.Count + 1)
            throw new ArgumentException("Insert columns must match the displayed projection.", nameof(columns));
        CancelInsert();
        CancelEdit();
        _nullText = nullText;
        _keyPlaceholder = keyPlaceholder;
        foreach (double width in _columnWidths) _insertRow.ColumnDefinitions.Add(new(width));
        _insertRow.Add(CreateCell("+", true), 0, 0);
        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            // Repeated source columns/aliases edit the same value, rather than silently diverging.
            if (!_insertCells.TryGetValue(column.Name, out var field))
                _insertCells[column.Name] = field = new() { Text = column.InitialValue };
            _insertRow.Add(CreateDraftCell(column, field, _insertInputs, focusOnLoad: i == 0), i + 1, 0);
        }
        _insertRow.IsVisible = true;
        _table.RowDefinitions[1].Height = new(InsertRowHeight);
        UpdateViewport();
        _ = _scroll.ScrollToAsync(0, 0, false);
    }

    public IReadOnlyDictionary<string, string?> GetInsertValues() => _insertCells.ToDictionary(
        cell => cell.Key, cell => cell.Value.IsNull ? null : cell.Value.Text);

    public void UpdateInsertLabels(string nullText, string keyPlaceholder)
    {
        _nullText = nullText;
        _keyPlaceholder = keyPlaceholder;
        foreach (var (input, nullToggle, column) in _insertInputs.Concat(_editInputs))
        {
            if (column.IsKey) input.Placeholder = keyPlaceholder;
            if (nullToggle is not null) SemanticProperties.SetDescription(nullToggle, column.Name + ": " + nullText);
        }
    }

    public void CancelInsert()
    {
        _insertRow.IsVisible = false;
        _insertRow.Children.Clear();
        _insertRow.ColumnDefinitions.Clear();
        _insertCells.Clear();
        _insertInputs.Clear();
        _table.RowDefinitions[1].Height = new(0);
        UpdateViewport();
    }

    private void UpdateViewport()
    {
        double draftHeight = IsInserting ? InsertRowHeight : 0;
        double rowsHeight = _resultRows.Count * RowHeight + (IsEditing ? InsertRowHeight - RowHeight : 0);
        HeightRequest = Math.Min(360, RowHeight + draftHeight + rowsHeight + 20);
        _table.HeightRequest = HeightRequest - 20;
    }

    public void BeginEdit(int rowNumber, SqlInsertColumn[] columns, IReadOnlyDictionary<string, object?> values,
        string nullText, string keyPlaceholder)
    {
        if (rowNumber < 1 || rowNumber > _resultRows.Count || columns.Length + 1 != _columnWidths.Length)
            throw new ArgumentException("Edit row must match the displayed projection.");
        CancelInsert();
        CancelEdit();
        _nullText = nullText;
        _keyPlaceholder = keyPlaceholder;
        _editColumns = columns;
        foreach (var column in columns.DistinctBy(c => c.Name))
        {
            object? value = values[column.Name];
            var field = new DraftCell { Text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "", IsNull = value is null };
            field.PropertyChanged += (_, change) =>
            {
                if (change.PropertyName is nameof(DraftCell.Text) or nameof(DraftCell.IsNull)) EditChanged?.Invoke(this, EventArgs.Empty);
            };
            _editCells[column.Name] = field;
        }
        EditingRowNumber = rowNumber;
        _resultRows[rowNumber - 1] = _resultRows[rowNumber - 1] with { Editing = true };
        UpdateViewport();
    }

    public IReadOnlyDictionary<string, string?> GetEditValues() => _editCells.ToDictionary(
        cell => cell.Key, cell => cell.Value.IsNull ? null : cell.Value.Text);

    public void FocusEditCell(int columnIndex)
    {
        if (!IsEditing || columnIndex < 0 || columnIndex >= _editColumns.Length || _editColumns[columnIndex].IsKey) return;
        _focusColumn = _editColumns[columnIndex].Name;
        var editor = _editInputs.FirstOrDefault(i => i.Column.Name == _focusColumn);
        if (editor.Input?.Handler is not null)
        {
            if (editor.Input.IsEnabled ? editor.Input.Focus() : editor.Null?.Focus() == true)
                _focusColumn = null;
        }
    }

    public void CancelEdit()
    {
        int index = EditingRowNumber - 1;
        EditingRowNumber = 0;
        _focusColumn = null;
        if (index >= 0 && index < _resultRows.Count) _resultRows[index] = _resultRows[index] with { Editing = false };
        _editCells.Clear();
        _editInputs.Clear();
        _editColumns = [];
        UpdateViewport();
    }

    private Border CreateDraftCell(SqlInsertColumn column, DraftCell field,
        List<(InputView Input, CheckBox? Null, SqlInsertColumn Column)> inputs,
        Action? selected = null, bool focusOnLoad = false)
    {
        InputView input = column.Name.EndsWith("Json", StringComparison.Ordinal)
            ? new Editor { AutoSize = EditorAutoSizeOption.Disabled, HeightRequest = 60 }
            : new Entry { MinimumHeightRequest = 48, Placeholder = column.IsKey ? _keyPlaceholder : null };
        input.FontSize = 13;
        input.SetDynamicResource(InputView.FontFamilyProperty, "AppFontFamily");
        input.SetDynamicResource(InputView.TextColorProperty, "TextPrimaryColor");
        input.SetDynamicResource(BackgroundColorProperty, "SurfaceColor");
        input.SetBinding(InputView.TextProperty, new Binding(nameof(DraftCell.Text), BindingMode.TwoWay, source: field));
        input.SetBinding(IsEnabledProperty, new Binding(nameof(DraftCell.CanEdit), source: field));
        SemanticProperties.SetDescription(input, column.Name);
        input.Focused += (_, _) => selected?.Invoke();
        input.Loaded += (_, _) =>
        {
            if (focusOnLoad || IsEnabled && _focusColumn == column.Name)
            {
                if (input.Focus()) _focusColumn = null;
                focusOnLoad = false;
            }
        };
        var contents = new Grid { RowDefinitions = { new(GridLength.Star), new(column.IsKey ? 0 : 48) }, RowSpacing = 0 };
        contents.Add(input);
        CheckBox? nullToggle = null;
        if (!column.IsKey)
        {
            nullToggle = new() { MinimumHeightRequest = 48, MinimumWidthRequest = 48 };
            nullToggle.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(DraftCell.IsNull), BindingMode.TwoWay, source: field));
            SemanticProperties.SetDescription(nullToggle, column.Name + ": " + _nullText);
            nullToggle.Focused += (_, _) => selected?.Invoke();
            var nullLabel = new Label { Text = "NULL", FontSize = 12, VerticalOptions = LayoutOptions.Center };
            nullLabel.SetDynamicResource(Label.TextColorProperty, "TextSecondaryColor");
            contents.Add(new HorizontalStackLayout { Spacing = 0, Children = { nullToggle, nullLabel } }, 0, 1);
        }
        inputs.Add((input, nullToggle, column));
        var cell = new Border { Content = contents, Padding = new Thickness(4), StrokeThickness = 1 };
        cell.SetDynamicResource(Border.StrokeProperty, "PrimaryBorderBrush");
        cell.SetDynamicResource(BackgroundColorProperty, "PrimarySoftColor");
        return cell;
    }

    private Grid CreateRow(string[] columns, double[] widths)
    {
        var grid = new Grid { ColumnSpacing = 0, RowSpacing = 0, HeightRequest = RowHeight };
        foreach (double width in widths) grid.ColumnDefinitions.Add(new ColumnDefinition(width));
        grid.BindingContextChanged += (_, _) =>
        {
            grid.Children.Clear();
            if (grid.BindingContext is not ResultRow row) return;
            bool editing = row.Editing && row.Number == EditingRowNumber && _editColumns.Length == columns.Length
                && row.Number <= _resultRows.Count && ReferenceEquals(_resultRows[row.Number - 1], row);
            grid.HeightRequest = editing ? InsertRowHeight : RowHeight;
            grid.SetDynamicResource(BackgroundColorProperty, editing ? "PrimarySoftColor"
                : row.Number % 2 == 0 ? "SurfaceAltColor" : "SurfaceColor");
            grid.Add(CreateCell(row.Number.ToString(CultureInfo.CurrentCulture), editing));
            if (editing) _editInputs.Clear();
            for (int i = 0; i < columns.Length; i++)
            {
                int index = i;
                void SelectCell() => CellSelected?.Invoke(this, new(row.Number, columns[index], row.Values[index], index));
                Border cell;
                if (editing && !_editColumns[i].IsKey)
                    cell = CreateDraftCell(_editColumns[i], _editCells[_editColumns[i].Name], _editInputs, SelectCell);
                else
                {
                    string value = editing ? _editCells[_editColumns[i].Name].Text : row.Values[i];
                    cell = CreateCell(value.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t"), false);
                    var tap = new TapGestureRecognizer();
                    tap.Tapped += (_, _) => SelectCell();
                    cell.GestureRecognizers.Add(tap);
                }
                grid.Add(cell, i + 1, 0);
            }
        };
        return grid;
    }

    private static Border CreateCell(string text, bool header)
    {
        var label = new Label
        {
            Text = text, FontSize = 13,
            FontAttributes = header ? FontAttributes.Bold : FontAttributes.None,
            VerticalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1
        };
        label.SetDynamicResource(Label.FontFamilyProperty, "AppFontFamily");
        label.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
        var cell = new Border { Content = label, Padding = new Thickness(10, 0), StrokeThickness = 0.5,
            BackgroundColor = Colors.Transparent };
        cell.SetDynamicResource(Border.StrokeProperty, "BorderBrush");
        if (header) cell.SetDynamicResource(BackgroundColorProperty, "PrimarySoftColor");
        return cell;
    }
}
