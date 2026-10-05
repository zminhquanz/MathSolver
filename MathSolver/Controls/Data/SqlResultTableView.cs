using System.Globalization;

namespace MathSolver.Controls;

public sealed class SqlResultCellEventArgs(int rowNumber, string columnName, string value, int columnIndex) : EventArgs
{
    public int RowNumber { get; } = rowNumber;
    public string ColumnName { get; } = columnName;
    public string Value { get; } = value;
    public int ColumnIndex { get; } = columnIndex;
}

/// <summary>A bounded table with a fixed header and recycled rows; cell actions are owned by its page.</summary>
public sealed class SqlResultTableView : ContentView
{
    private const double RowHeight = 44;
    private const double NumberWidth = 52;
    private readonly Grid _header = new() { ColumnSpacing = 0, RowSpacing = 0, HeightRequest = RowHeight };
    private readonly CollectionView _rows = new()
    {
        SelectionMode = SelectionMode.None,
        ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 0 },
        ItemSizingStrategy = ItemSizingStrategy.MeasureFirstItem,
        VerticalScrollBarVisibility = ScrollBarVisibility.Always,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Never
    };
    private readonly Grid _table = new() { RowSpacing = 0, RowDefinitions = { new(RowHeight), new(GridLength.Star) } };
    private readonly MouseDragScrollView _scroll = new()
    {
        Orientation = ScrollOrientation.Horizontal,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Always,
        VerticalScrollBarVisibility = ScrollBarVisibility.Never
    };

    private sealed record ResultRow(int Number, string[] Values);
    public event EventHandler<SqlResultCellEventArgs>? CellSelected;

    public SqlResultTableView()
    {
        _table.Add(_header);
        _table.Add(_rows, 0, 1);
        _scroll.Content = _table;
        Content = _scroll;
    }

    public void Clear()
    {
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
        for (int i = 0; i < widths.Length; i++)
            _header.ColumnDefinitions.Add(new ColumnDefinition(widths[i]));
        _header.Add(CreateCell("#", true), 0, 0);
        for (int i = 0; i < columns.Length; i++)
            _header.Add(CreateCell(columns[i], true), i + 1, 0);

        _table.WidthRequest = widths.Sum();
        // Both axes have an explicit viewport; the outer page never measures all rows.
        HeightRequest = rows.Count == 0 ? RowHeight + 20 : Math.Min(360, RowHeight * (Math.Max(2, rows.Count) + 1) + 20);
        _table.HeightRequest = HeightRequest - 20;
        _rows.ItemTemplate = new DataTemplate(() => CreateRow(columns, widths));
        _rows.ItemsSource = rows.Select((values, index) => new ResultRow(index + 1, values)).ToArray();
        IsVisible = true;
        // A new result starts at its first column; header and body share this viewport.
        _ = _scroll.ScrollToAsync(0, 0, false);
    }

    private Grid CreateRow(string[] columns, double[] widths)
    {
        var grid = new Grid { ColumnSpacing = 0, RowSpacing = 0, HeightRequest = RowHeight };
        foreach (double width in widths) grid.ColumnDefinitions.Add(new ColumnDefinition(width));
        var number = CreateCell("", false);
        grid.Add(number);
        var labels = new Label[columns.Length];
        for (int i = 0; i < columns.Length; i++)
        {
            int column = i;
            var cell = CreateCell("", false);
            labels[i] = (Label)cell.Content!;
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                // Read the current recycled binding context, not the original row.
                if (grid.BindingContext is ResultRow row)
                    CellSelected?.Invoke(this, new(row.Number, columns[column], row.Values[column], column));
            };
            cell.GestureRecognizers.Add(tap);
            grid.Add(cell, i + 1, 0);
        }
        grid.BindingContextChanged += (_, _) =>
        {
            if (grid.BindingContext is not ResultRow row) return;
            grid.SetDynamicResource(BackgroundColorProperty, row.Number % 2 == 0 ? "SurfaceAltColor" : "SurfaceColor");
            ((Label)number.Content!).Text = row.Number.ToString(CultureInfo.CurrentCulture);
            for (int i = 0; i < columns.Length; i++)
                labels[i].Text = row.Values[i].Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
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
