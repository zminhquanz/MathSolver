using MathSolver.Services;
using System.Collections.ObjectModel;

namespace MathSolver.Views;

public partial class MultiplicationTablePage : ContentPage
{
    public ObservableCollection<TableCardModel> TableCards { get; } = new();

    private TableMode _currentMode = TableMode.Multiply;
    private TableRange _currentRange = TableRange.OneToTen;

    public MultiplicationTablePage()
    {
        InitializeComponent();

        LocalizationService.Attach(
            this);

        AppLanguageManager.LanguageChanged +=
            OnLanguageChanged;

        AppThemeManager.ThemeChanged +=
            OnThemeChanged;

        BindingContext = this;

        Range1To10Radio.IsChecked = true;

        UpdateOperationButtons();
        UpdateRangeCards();
        BuildTables();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateOperationButtons();
        UpdateRangeCards();
    }

    private void OnLanguageChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Dispatch(
            () =>
            {
                BuildTables();
                LocalizationService.Attach(
                    this);
            });
    }

    private void OnThemeChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Dispatch(
            () =>
            {
                UpdateOperationButtons();
                UpdateRangeCards();
            });
    }

    private void OnMultiplyClicked(object sender, EventArgs e)
    {
        _currentMode = TableMode.Multiply;
        UpdateOperationButtons();
        BuildTables();
    }

    private void OnDivideClicked(object sender, EventArgs e)
    {
        _currentMode = TableMode.Divide;
        UpdateOperationButtons();
        BuildTables();
    }

    private void OnRangeChanged(object sender, CheckedChangedEventArgs e)
    {
        if (!e.Value)
            return;

        if (sender == Range1To10Radio)
            _currentRange = TableRange.OneToTen;
        else if (sender == Range11To20Radio)
            _currentRange = TableRange.ElevenToTwenty;
        else
            _currentRange = TableRange.All;

        UpdateRangeCards();
        BuildTables();
    }

    private void BuildTables()
    {
        TableCards.Clear();

        var (start, end) = GetRangeBounds();

        for (int i = start; i <= end; i++)
        {
            var lines = new List<string>();

            for (int j = 1; j <= 10; j++)
            {
                if (_currentMode == TableMode.Multiply)
                {
                    lines.Add($"{i} × {j} = {i * j}");
                }
                else
                {
                    int dividend = i * j;
                    lines.Add($"{dividend} ÷ {i} = {j}");
                }
            }

            TableCards.Add(new TableCardModel
            {
                Title = LocalizationService.Translate(
                    _currentMode == TableMode.Multiply
                        ? $"Bảng nhân {i}"
                        : $"Bảng chia {i}"),
                Lines = lines
            });
        }

        UpdateStatusText(start, end);
    }

    private (int start, int end) GetRangeBounds()
    {
        return _currentRange switch
        {
            TableRange.OneToTen => (1, 10),
            TableRange.ElevenToTwenty => (11, 20),
            _ => (1, 20)
        };
    }

    private void UpdateStatusText(int start, int end)
    {
        string modeText =
            _currentMode == TableMode.Multiply
                ? "bảng nhân"
                : "bảng chia";

        int count =
            end - start + 1;

        StatusLabel.Text =
            LocalizationService.Translate(
                $"Đang hiển thị {modeText} từ {start} đến {end} • {count} bảng");
    }

    private void UpdateOperationButtons()
    {
        ApplyOperationButtonStyle(
            MultiplyButton,
            _currentMode ==
            TableMode.Multiply);

        ApplyOperationButtonStyle(
            DivideButton,
            _currentMode ==
            TableMode.Divide);
    }

    private static void ApplyOperationButtonStyle(
        Button button,
        bool isSelected)
    {
        button.SetDynamicResource(
            Button.BackgroundColorProperty,
            isSelected
                ? "PrimaryColor"
                : "SurfaceAltColor");

        button.SetDynamicResource(
            Button.TextColorProperty,
            isSelected
                ? "OnPrimaryColor"
                : "TextPrimaryColor");

        button.SetDynamicResource(
            Button.BorderColorProperty,
            isSelected
                ? "PrimaryColor"
                : "BorderColor");

        button.BorderWidth =
            1;

        button.CornerRadius =
            12;
    }

    private void UpdateRangeCards()
    {
        ApplyRangeStyle(
            Range1To10Border,
            _currentRange ==
            TableRange.OneToTen);

        ApplyRangeStyle(
            Range11To20Border,
            _currentRange ==
            TableRange.ElevenToTwenty);

        ApplyRangeStyle(
            RangeAllBorder,
            _currentRange ==
            TableRange.All);
    }

    private static void ApplyRangeStyle(
        Border border,
        bool isSelected)
    {
        border.SetDynamicResource(
            Border.BackgroundColorProperty,
            isSelected
                ? "SurfaceAltColor"
                : "SurfaceColor");

        border.SetDynamicResource(
            Border.StrokeProperty,
            isSelected
                ? "PrimaryColor"
                : "BorderBrush");

        border.StrokeThickness =
            isSelected
                ? 1.6
                : 1;
    }

    private enum TableMode
    {
        Multiply,
        Divide
    }

    private enum TableRange
    {
        OneToTen,
        ElevenToTwenty,
        All
    }

    public class TableCardModel
    {
        public string Title { get; set; } = string.Empty;
        public List<string> Lines { get; set; } = new();
    }
}