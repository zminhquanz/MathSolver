using MathSolver.Controls;
using MathSolver.Services;
using Microsoft.Maui.Controls;
using System.Globalization;

namespace MathSolver.Views;

public sealed partial class MatrixView
{
    private readonly Label _sizeSelection = TextLabel(), _columnSize = TextLabel(), _rowSize = TextLabel();

    private void AddSizeControls(VerticalStackLayout editor)
    {
        var panel = new VerticalStackLayout { IsVisible = false, Spacing = 8 };
        var toggle = ActionButton("CellSize", "↔", (_, _) => panel.IsVisible = !panel.IsVisible);
        editor.Add(toggle);
        panel.Add(_sizeSelection);
        var dimensions = FlowGrid(240, 2);
        dimensions.Add(SizeAdjustment(_columnSize, true));
        dimensions.Add(SizeAdjustment(_rowSize, false));
        panel.Add(dimensions);
        var reset = ActionButton("ResetCellSize", "↺", (_, _) =>
            _viewport.ResizeSelectedDimensions(_viewport.SelectedColumnWidth, _viewport.SelectedRowHeight, reset: true));
        panel.Add(reset);
        var help = Caption("CellSizeHelp");
        help.FontSize = 13;
        help.SetDynamicResource(Label.TextColorProperty, "WallpaperTextSecondaryColor");
        panel.Add(help);
        editor.Add(panel);
        _translations.Add(RefreshSizeControls);
    }

    private Grid SizeAdjustment(Label label, bool column)
    {
        var grid = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) } };
        label.VerticalOptions = LayoutOptions.Center;
        grid.Add(label, 0);
        for (int i = 0; i < 2; i++)
        {
            int direction = i == 0 ? -1 : 1;
            var button = new Button { Text = i == 0 ? "−" : "+", Style = GetStyle("SolverSecondaryActionStyle"),
                MinimumHeightRequest = 48, MinimumWidthRequest = 48, Padding = new Thickness(8) };
            DisplayDensityManager.BindControl(button, square: true);
            _translations.Add(() => SemanticProperties.SetDescription(button,
                T((column ? "ColumnWidth" : "RowHeight") + (direction < 0 ? "Decrease" : "Increase"))));
            button.Clicked += (_, _) =>
            {
                if (!CanTouchUi || _busy) return;
                _viewport.ResizeSelectedDimensions(_viewport.SelectedColumnWidth + (column ? direction * 16 : 0),
                    _viewport.SelectedRowHeight + (column ? 0 : direction * 8));
            };
            _lockDuringWork.Add(button);
            grid.Add(button, i + 1);
        }
        return grid;
    }

    private void RefreshSizeControls()
    {
        _sizeSelection.Text = F("Selected", "ABC"[_selected], _viewport.SelectedRow + 1, _viewport.SelectedColumn + 1);
        _columnSize.Text = F("ColumnWidth", _viewport.SelectedColumnWidth.ToString("0", CultureInfo.InvariantCulture));
        _rowSize.Text = F("RowHeight", _viewport.SelectedRowHeight.ToString("0", CultureInfo.InvariantCulture));
    }
}
