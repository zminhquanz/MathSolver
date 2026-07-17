using System.Collections.ObjectModel;

namespace MathSolver.Views;

public partial class MultiplicationTablePage : ContentPage
{
    private readonly ObservableCollection<MultiplicationTableModel> _tables = [];

    private TableMode _selectedMode = TableMode.Multiplication;

    public MultiplicationTablePage()
    {
        InitializeComponent();

        TablesCollectionView.ItemsSource = _tables;

        GenerateTables();
    }

    private void OnMultiplicationClicked(object sender, EventArgs e)
    {
        SelectMode(TableMode.Multiplication);
    }

    private void OnDivisionClicked(object sender, EventArgs e)
    {
        SelectMode(TableMode.Division);
    }

    private void SelectMode(TableMode mode)
    {
        _selectedMode = mode;

        UpdateButtonStyles();
        GenerateTables();
    }

    private void UpdateButtonStyles()
    {
        Color selectedBackground =
            Color.FromArgb("#2563EB");

        Color normalBackground =
            Color.FromArgb("#E8EEF6");

        Color normalText =
            Color.FromArgb("#334155");

        MultiplicationButton.BackgroundColor =
            normalBackground;

        MultiplicationButton.TextColor =
            normalText;

        DivisionButton.BackgroundColor =
            normalBackground;

        DivisionButton.TextColor =
            normalText;

        if (_selectedMode == TableMode.Multiplication)
        {
            MultiplicationButton.BackgroundColor =
                selectedBackground;

            MultiplicationButton.TextColor =
                Colors.White;
        }
        else
        {
            DivisionButton.BackgroundColor =
                selectedBackground;

            DivisionButton.TextColor =
                Colors.White;
        }
    }

    private void GenerateTables()
    {
        _tables.Clear();

        for (int tableNumber = 1; tableNumber <= 10; tableNumber++)
        {
            MultiplicationTableModel table = new()
            {
                Title = _selectedMode ==
                        TableMode.Multiplication
                    ? $"Bảng nhân {tableNumber}"
                    : $"Bảng chia {tableNumber}"
            };

            for (int number = 1; number <= 10; number++)
            {
                string expression;

                if (_selectedMode ==
                    TableMode.Multiplication)
                {
                    expression =
                        $"{tableNumber} × {number} = " +
                        $"{tableNumber * number}";
                }
                else
                {
                    int dividend =
                        tableNumber * number;

                    expression =
                        $"{dividend} ÷ {tableNumber} = " +
                        $"{number}";
                }

                table.Rows.Add(expression);
            }

            _tables.Add(table);
        }
    }
}

public enum TableMode
{
    Multiplication,
    Division
}

public sealed class MultiplicationTableModel
{
    public string Title { get; set; } =
        string.Empty;

    public ObservableCollection<string> Rows { get; } =
        [];
}