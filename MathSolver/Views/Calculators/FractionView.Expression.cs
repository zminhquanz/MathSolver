using MathSolver.Models;
using MathSolver.Services;
using System.Globalization;

namespace MathSolver.Views;

public partial class FractionView
{
    private static string ExpressionText(string key) => LocalizationService.TranslateKey("Fraction.Expression." + key);

    private void SetExpressionMode(bool enabled)
    {
        _isExpressionMode = enabled;
        OnPropertyChanged(nameof(IsExpressionMode));
        FractionInputGrid.IsVisible = FractionInputTitleLabel.IsVisible = !enabled;
        FractionExpressionInputPanel.IsVisible = FractionExpressionInputTitleLabel.IsVisible = enabled;
        FullExpressionMathView.ParseArithmeticExpressions = enabled;
    }

    private void OnFractionExpressionClicked(object? sender, EventArgs e)
    {
        SetExpressionMode(true);
        SelectionButtonStyler.Select(FractionExpressionButton, AddButton, SubtractButton, MultiplyButton,
            DivideButton, CommonDenominatorButton, FractionExpressionButton);
        ResetOutput();
        FractionExpressionEditor.Focus();
    }

    private void OnFractionExpressionTextChanged(object? sender, TextChangedEventArgs e)
    {
        string text = e.NewTextValue ?? string.Empty;
        FractionExpressionPreview.IsVisible = !string.IsNullOrWhiteSpace(text) && text.Length <= RationalExpressionEngine.MaximumLength;
        FractionExpressionPreview.Expression = FractionExpressionPreview.IsVisible ? text : string.Empty;
        if (_isExpressionMode) ResetOutput();
    }

    private void CalculateFractionExpression()
    {
        try
        {
            var result = RationalExpressionEngine.Shared.Evaluate(FractionExpressionEditor.Text, preferDecimalNotation: true);
            _expressionExactAnswer = result.Result.ToString();
            FullExpressionMathView.Expression = result.Expression + " = " + _expressionExactAnswer;
            AnswerMathView.Expression = _expressionExactAnswer;
            if (result.Steps.Count == 0)
            {
                var step = new FractionSolutionStep { Title = ExpressionText("Simplify"), IsImportant = true };
                step.MathLines.Add(FullExpressionMathView.Expression);
                SolutionSteps.Add(step);
            }
            else
            {
                for (int i = 0; i < result.Steps.Count; i++)
                {
                    var step = new FractionSolutionStep
                    {
                        Title = string.Format(CultureInfo.CurrentCulture, ExpressionText("Step"), i + 1),
                        IsImportant = i == result.Steps.Count - 1
                    };
                    step.MathLines.Add(result.Steps[i]);
                    SolutionSteps.Add(step);
                }
            }
            ResultBorder.IsVisible = true;
        }
        catch (RationalExpressionException error)
        {
            ShowError(ExpressionText("Error." + error.Error));
            FractionExpressionEditor.Focus();
        }
    }
}
