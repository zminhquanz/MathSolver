using MathSolver.Models;
using MathSolver.Services;
using System.Collections.ObjectModel;
using System.Numerics;

namespace MathSolver.Views;

public partial class FractionView : ContentView
{
    private bool _isCompact;

    public ObservableCollection<FractionSolutionStep> SolutionSteps { get; } = [];

    private FractionOperation _selectedOperation = FractionOperation.Add;

    public FractionView()
    {
        InitializeComponent();

        SelectOperation(FractionOperation.Add);
    }

    private void SelectOperation(
    FractionOperation operation)
    {
        _selectedOperation = operation;

        ResetOperationButtonStyles();

        Button selectedButton;

        switch (operation)
        {
            case FractionOperation.Add:
                selectedButton = AddButton;

                OperatorLabel.Text = "+";
                OperatorLabel.IsVisible = true;
                break;

            case FractionOperation.Subtract:
                selectedButton = SubtractButton;

                OperatorLabel.Text = "−";
                OperatorLabel.IsVisible = true;
                break;

            case FractionOperation.Multiply:
                selectedButton = MultiplyButton;

                OperatorLabel.Text = "×";
                OperatorLabel.IsVisible = true;
                break;

            case FractionOperation.Divide:
                selectedButton = DivideButton;

                OperatorLabel.Text = "÷";
                OperatorLabel.IsVisible = true;
                break;

            case FractionOperation.CommonDenominator:
                selectedButton = CommonDenominatorButton;
                OperatorLabel.Text = "Và";

                OperatorLabel.IsVisible = true;

                break;

            default:
                selectedButton = AddButton;

                OperatorLabel.Text = "+";
                OperatorLabel.IsVisible = true;
                break;
        }

        selectedButton.SetDynamicResource(
            Button.BackgroundColorProperty,
            "PrimaryColor");

        selectedButton.SetDynamicResource(
            Button.TextColorProperty,
            "OnPrimaryColor");

        ResetOutput();
    }

    private void OnAddClicked(
    object? sender,
    EventArgs e)
    {
        SelectOperation(
            FractionOperation.Add);
    }

    private void OnSubtractClicked(
        object? sender,
        EventArgs e)
    {
        SelectOperation(
            FractionOperation.Subtract);
    }

    private void OnMultiplyClicked(
        object? sender,
        EventArgs e)
    {
        SelectOperation(
            FractionOperation.Multiply);
    }

    private void OnDivideClicked(
        object? sender,
        EventArgs e)
    {
        SelectOperation(
            FractionOperation.Divide);
    }

    private void OnCommonDenominatorClicked(
        object? sender,
        EventArgs e)
    {
        SelectOperation(
            FractionOperation.CommonDenominator);
    }

    private void OnCalculateClicked(
        object? sender,
        EventArgs e)
    {
        ResetOutput();

        if (!TryReadInteger(
                FirstNumeratorEntry.Text,
                "Tử số của phân số thứ nhất",
                out BigInteger numerator1) ||
            !TryReadInteger(
                FirstDenominatorEntry.Text,
                "Mẫu số của phân số thứ nhất",
                out BigInteger denominator1) ||
            !TryReadInteger(
                SecondNumeratorEntry.Text,
                "Tử số của phân số thứ hai",
                out BigInteger numerator2) ||
            !TryReadInteger(
                SecondDenominatorEntry.Text,
                "Mẫu số của phân số thứ hai",
                out BigInteger denominator2))
        {
            return;
        }

        FractionCalculationResult result =
    FractionCalculator.Calculate(
        numerator1,
        denominator1,
        numerator2,
        denominator2,
        _selectedOperation);

        if (!result.IsSuccess)
        {
            ShowError(result.ErrorMessage);
            return;
        }

        FullExpressionMathView.Expression =
        result.FullExpression;

        AnswerMathView.Expression =
            result.ResultExpression;

        foreach (FractionSolutionStep step in result.Steps)
        {
            SolutionSteps.Add(step);
        }

        ResultBorder.IsVisible = true;
    }

    private void OnClearClicked(
        object? sender,
        EventArgs e)
    {
        FirstNumeratorEntry.Text = string.Empty;
        FirstDenominatorEntry.Text = string.Empty;
        SecondNumeratorEntry.Text = string.Empty;
        SecondDenominatorEntry.Text = string.Empty;

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

        FullExpressionMathView.Expression =
            string.Empty;

        AnswerMathView.Expression =
            string.Empty;

        SolutionSteps.Clear();
    }

    private void ShowError(
    string message)
    {
        ErrorLabel.Text = message;
        ErrorBorder.IsVisible = true;

        ResultBorder.IsVisible = false;

        FullExpressionMathView.Expression =
            string.Empty;

        AnswerMathView.Expression =
            string.Empty;

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

            Grid.SetRow(OperatorLabel, 1);
            Grid.SetColumn(OperatorLabel, 0);

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

            Grid.SetRow(OperatorLabel, 0);
            Grid.SetColumn(OperatorLabel, 1);

            Grid.SetRow(SecondFractionCard, 0);
            Grid.SetColumn(SecondFractionCard, 2);

            FractionInputGrid.RowSpacing = 0;
            FractionInputGrid.ColumnSpacing = 16;
        }
    }

    private void ResetOperationButtonStyles()
    {
        Button[] buttons =
        [
            AddButton,
            SubtractButton,
            MultiplyButton,
            DivideButton,
            CommonDenominatorButton
        ];

        foreach (Button button in buttons)
        {
            button.SetDynamicResource(
                Button.BackgroundColorProperty,
                "SurfaceAltColor");

            button.SetDynamicResource(
                Button.TextColorProperty,
                "TextPrimaryColor");
        }
    }
}
