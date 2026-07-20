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
                Title = _currentMode == TableMode.Multiply
                    ? $"Bảng nhân {i}"
                    : $"Bảng chia {i}",
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
        string modeText = _currentMode == TableMode.Multiply ? "bảng nhân" : "bảng chia";
        int count = end - start + 1;

        StatusLabel.Text = $"Đang hiển thị {modeText} từ {start} đến {end} • {count} bảng";
    }

    private void UpdateOperationButtons()
    {
        bool isDark = Application.Current?.RequestedTheme != AppTheme.Light;

        ApplyOperationButtonStyle(MultiplyButton, _currentMode == TableMode.Multiply, isDark);
        ApplyOperationButtonStyle(DivideButton, _currentMode == TableMode.Divide, isDark);
    }

    private void ApplyOperationButtonStyle(Button button, bool isSelected, bool isDark)
    {
        button.BorderWidth = 1.5;

        if (isSelected)
        {
            button.BackgroundColor = Color.FromArgb("#18B65A");
            button.TextColor = Colors.White;
            button.BorderColor = Color.FromArgb("#18B65A");
        }
        else
        {
            button.BackgroundColor = isDark
                ? Color.FromArgb("#152744")
                : Colors.White;

            button.TextColor = isDark
                ? Colors.White
                : Color.FromArgb("#22324A");

            button.BorderColor = isDark
                ? Color.FromArgb("#253A57")
                : Color.FromArgb("#D6DFEC");
        }
    }

    private void UpdateRangeCards()
    {
        bool isDark = Application.Current?.RequestedTheme != AppTheme.Light;

        ApplyRangeStyle(Range1To10Border, _currentRange == TableRange.OneToTen, isDark);
        ApplyRangeStyle(Range11To20Border, _currentRange == TableRange.ElevenToTwenty, isDark);
        ApplyRangeStyle(RangeAllBorder, _currentRange == TableRange.All, isDark);
    }

    private void ApplyRangeStyle(Border border, bool isSelected, bool isDark)
    {
        if (isSelected)
        {
            border.BackgroundColor = isDark
                ? Color.FromArgb("#103D2F")
                : Color.FromArgb("#ECF9F1");

            border.Stroke = Color.FromArgb("#1FA95E");
            border.StrokeThickness = 1.4;
        }
        else
        {
            border.BackgroundColor = isDark
                ? Color.FromArgb("#0D1C35")
                : Colors.White;

            border.Stroke = isDark
                ? Color.FromArgb("#233755")
                : Color.FromArgb("#D6DFEC");

            border.StrokeThickness = 1.2;
        }
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