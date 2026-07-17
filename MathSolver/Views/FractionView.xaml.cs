using System.Collections.ObjectModel;
using System.Numerics;
using MathSolver.Models;
using MathSolver.Services;

namespace MathSolver.Views;

public partial class FractionView : ContentView
{
    private bool _isCompact;

    public ObservableCollection<FractionSolutionStep>
        SolutionSteps { get; } = [];

    public FractionView()
    {
        InitializeComponent();
    }

    private void OnCalculateClicked(
        object? sender,
        EventArgs e)
    {
        ResetOutput();

        if (!TryReadInteger(
                FirstNumeratorEntry.Text,
                "tử số của phân số thứ nhất",
                out BigInteger numerator1) ||
            !TryReadInteger(
                FirstDenominatorEntry.Text,
                "mẫu số của phân số thứ nhất",
                out BigInteger denominator1) ||
            !TryReadInteger(
                SecondNumeratorEntry.Text,
                "tử số của phân số thứ hai",
                out BigInteger numerator2) ||
            !TryReadInteger(
                SecondDenominatorEntry.Text,
                "mẫu số của phân số thứ hai",
                out BigInteger denominator2))
        {
            return;
        }

        FractionOperation operation =
            OperationPicker.SelectedIndex switch
            {
                0 => FractionOperation.Add,
                1 => FractionOperation.Subtract,
                2 => FractionOperation.Multiply,
                3 => FractionOperation.Divide,
                4 => FractionOperation.CommonDenominator,
                _ => FractionOperation.Add
            };

        FractionCalculationResult result =
            FractionCalculator.Calculate(
                numerator1,
                denominator1,
                numerator2,
                denominator2,
                operation);

        if (!result.IsSuccess)
        {
            ShowError(result.ErrorMessage);
            return;
        }

        ResultLabel.Text = result.ResultText;

        foreach (FractionSolutionStep step in result.Steps)
        {
            SolutionSteps.Add(step);
        }

        ResultBorder.IsVisible = true;
        SolutionTitle.IsVisible = true;
    }

    private void OnClearClicked(
        object? sender,
        EventArgs e)
    {
        FirstNumeratorEntry.Text = string.Empty;
        FirstDenominatorEntry.Text = string.Empty;
        SecondNumeratorEntry.Text = string.Empty;
        SecondDenominatorEntry.Text = string.Empty;

        OperationPicker.SelectedIndex = 0;

        ResetOutput();
        FirstNumeratorEntry.Focus();
    }

    private bool TryReadInteger(
        string? text,
        string fieldName,
        out BigInteger value)
    {
        string normalized =
            (text ?? string.Empty).Trim();

        if (normalized.Length == 0)
        {
            value = BigInteger.Zero;
            ShowError($"Vui lòng nhập {fieldName}.");
            return false;
        }

        if (!BigInteger.TryParse(
                normalized,
                out value))
        {
            ShowError(
                $"{fieldName} phải là một số nguyên hợp lệ.");
            return false;
        }

        return true;
    }

    private void ResetOutput()
    {
        ErrorBorder.IsVisible = false;
        ErrorLabel.Text = string.Empty;

        ResultBorder.IsVisible = false;
        ResultLabel.Text = string.Empty;

        SolutionTitle.IsVisible = false;
        SolutionSteps.Clear();
    }

    private void ShowError(
        string message)
    {
        ErrorLabel.Text = message;
        ErrorBorder.IsVisible = true;

        ResultBorder.IsVisible = false;
        SolutionTitle.IsVisible = false;
        SolutionSteps.Clear();
    }

    protected override void OnSizeAllocated(
        double width,
        double height)
    {
        base.OnSizeAllocated(width, height);

        bool shouldBeCompact =
            width > 0 &&
            width < 720;

        if (_isCompact == shouldBeCompact)
        {
            return;
        }

        _isCompact = shouldBeCompact;

        if (shouldBeCompact)
        {
            FractionInputGrid.ColumnDefinitions.Clear();
            FractionInputGrid.RowDefinitions.Clear();

            FractionInputGrid.ColumnDefinitions.Add(
                new ColumnDefinition(GridLength.Star));

            FractionInputGrid.RowDefinitions.Add(
                new RowDefinition(GridLength.Auto));

            FractionInputGrid.RowDefinitions.Add(
                new RowDefinition(GridLength.Auto));

            FractionInputGrid.RowDefinitions.Add(
                new RowDefinition(GridLength.Auto));

            Grid.SetRow(FirstFractionCard, 0);
            Grid.SetColumn(FirstFractionCard, 0);

            Grid.SetRow(OperationPanel, 1);
            Grid.SetColumn(OperationPanel, 0);

            Grid.SetRow(SecondFractionCard, 2);
            Grid.SetColumn(SecondFractionCard, 0);

            FractionInputGrid.RowSpacing = 10;
            FractionInputGrid.ColumnSpacing = 0;
        }
        else
        {
            FractionInputGrid.RowDefinitions.Clear();
            FractionInputGrid.ColumnDefinitions.Clear();

            FractionInputGrid.ColumnDefinitions.Add(
                new ColumnDefinition(GridLength.Star));

            FractionInputGrid.ColumnDefinitions.Add(
                new ColumnDefinition(
                    new GridLength(180)));

            FractionInputGrid.ColumnDefinitions.Add(
                new ColumnDefinition(GridLength.Star));

            Grid.SetRow(FirstFractionCard, 0);
            Grid.SetColumn(FirstFractionCard, 0);

            Grid.SetRow(OperationPanel, 0);
            Grid.SetColumn(OperationPanel, 1);

            Grid.SetRow(SecondFractionCard, 0);
            Grid.SetColumn(SecondFractionCard, 2);

            FractionInputGrid.RowSpacing = 0;
            FractionInputGrid.ColumnSpacing = 16;
        }
    }
}
