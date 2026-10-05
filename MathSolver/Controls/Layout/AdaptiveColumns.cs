using MathSolver.Services;

namespace MathSolver.Controls;

/// <summary>Reflows a grid of independent cards/buttons without recreating their content.</summary>
public static class AdaptiveColumns
{
    public static readonly BindableProperty MinimumColumnWidthProperty = BindableProperty.CreateAttached(
        "MinimumColumnWidth", typeof(double), typeof(AdaptiveColumns), 0d, propertyChanged: OnWidthChanged);
    public static readonly BindableProperty MaximumColumnsProperty = BindableProperty.CreateAttached(
        "MaximumColumns", typeof(int), typeof(AdaptiveColumns), 2);

    public static double GetMinimumColumnWidth(BindableObject target) => (double)target.GetValue(MinimumColumnWidthProperty);
    public static void SetMinimumColumnWidth(BindableObject target, double value) => target.SetValue(MinimumColumnWidthProperty, value);
    public static int GetMaximumColumns(BindableObject target) => (int)target.GetValue(MaximumColumnsProperty);
    public static void SetMaximumColumns(BindableObject target, int value) => target.SetValue(MaximumColumnsProperty, value);

    private static void OnWidthChanged(BindableObject target, object oldValue, object newValue)
    {
        if (target is not Grid grid)
            return;
        grid.SizeChanged -= OnSizeChanged;
        if ((double)newValue > 0d)
        {
            grid.SizeChanged += OnSizeChanged;
            Update(grid);
        }
    }

    private static void OnSizeChanged(object? sender, EventArgs e)
    {
        if (sender is Grid grid)
            Update(grid);
    }

    private static void Update(Grid grid)
    {
        if (grid.Width <= 0d || grid.Children.Count == 0)
            return;
        double width = grid.Width - grid.Padding.HorizontalThickness;
        int columns = ResponsiveLayoutPolicy.Columns(width, GetMinimumColumnWidth(grid),
            Math.Max(1, GetMaximumColumns(grid)), grid.ColumnSpacing);
        int rows = (grid.Children.Count + columns - 1) / columns;
        if (grid.ColumnDefinitions.Count == columns && grid.RowDefinitions.Count == rows)
            return;

        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        for (int i = 0; i < columns; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (int i = 0; i < rows; i++)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (int i = 0; i < grid.Children.Count; i++)
        {
            grid.SetRow(grid.Children[i], i / columns);
            grid.SetColumn(grid.Children[i], i % columns);
        }
    }
}
