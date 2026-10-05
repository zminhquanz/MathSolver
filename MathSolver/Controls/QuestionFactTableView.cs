using MathSolver.Models;

namespace MathSolver.Controls;

/// <summary>A small factual table shared by practice and the read-only AI preview.</summary>
public sealed class QuestionFactTableView : Grid
{
    public static readonly BindableProperty TableProperty = BindableProperty.Create(nameof(Table), typeof(QuestionFactTable),
        typeof(QuestionFactTableView), null, propertyChanged: (view, _, _) => ((QuestionFactTableView)view).Render());
    public QuestionFactTable? Table { get => (QuestionFactTable?)GetValue(TableProperty); set => SetValue(TableProperty, value); }
    public QuestionFactTableView()
    {
        ColumnDefinitions = [new() { Width = GridLength.Star }, new() { Width = GridLength.Star }];
        RowSpacing = 0;
        ColumnSpacing = 0;
        MaximumWidthRequest = 600;
        HorizontalOptions = LayoutOptions.Fill;
        IsVisible = false;
    }
    private void Render()
    {
        Children.Clear();
        RowDefinitions.Clear();
        IsVisible = Table is not null;
        if (Table is not { } table) return;
        AddRow(table.LabelHeader, table.ValueHeader, true);
        foreach (var row in table.Rows) AddRow(row.Label, row.Value, false);
    }
    private void AddRow(string left, string right, bool header)
    {
        int row = RowDefinitions.Count;
        RowDefinitions.Add(new() { Height = GridLength.Auto });
        foreach (var (text, column) in new[] { (left, 0), (right, 1) })
        {
            var label = new Label { Text = text, FontSize = 15, LineBreakMode = LineBreakMode.WordWrap,
                FontAttributes = header ? FontAttributes.Bold : FontAttributes.None, VerticalTextAlignment = TextAlignment.Center };
            label.SetDynamicResource(Label.TextColorProperty, "WallpaperTextPrimaryColor");
            var cell = new Border { Content = label, Padding = new Thickness(12, 8), StrokeThickness = 1 };
            cell.SetDynamicResource(Border.StrokeProperty, "WallpaperPrimaryBorderBrush");
            cell.SetDynamicResource(Border.BackgroundColorProperty, header ? "WallpaperPrimarySoftColor" : "WallpaperSurfaceColor");
            SetColumn((BindableObject)cell, column);
            SetRow((BindableObject)cell, row);
            Children.Add(cell);
        }
    }
}
