using MathSolver.Controls;
using MathSolver.Services;
using MathSolver.Services.Core;
using Microsoft.Maui.Controls.Shapes;
using System.Globalization;

namespace MathSolver.Views;

public sealed partial class MatrixView
{
    private Border _resultCard = null!;
    private readonly Label _resultOperation = TextLabel(), _resultSize = TextLabel(),
        _resultDuration = TextLabel(), _resultMode = TextLabel(), _technicalBackend = TextLabel(),
        _technicalStorage = TextLabel(), _technicalBatches = TextLabel(), _solutionHeading = TextLabel(),
        _solutionPrompt = TextLabel();
    private readonly VerticalStackLayout _technical = new() { IsVisible = false, Spacing = 8 },
        _solutionSteps = new() { Spacing = 16 };

    private Border BuildResultCard()
    {
        var content = new VerticalStackLayout { Spacing = 12 };
        content.Add(Caption("ResultTitle", "SolverSectionTitleStyle"));
        var metrics = FlowGrid(200, 2);
        metrics.Add(ResultMetric("ResultOperation", _resultOperation));
        metrics.Add(ResultMetric("ResultSize", _resultSize));
        metrics.Add(ResultMetric("ResultDuration", _resultDuration));
        metrics.Add(ResultMetric("ResultMode", _resultMode));
        content.Add(metrics);
        var exact = Caption("ExactResult");
        exact.SetDynamicResource(Label.TextColorProperty, "WallpaperTextSecondaryColor");
        content.Add(exact);

        var toggle = new EmojiActionButton { Style = GetStyle("SolverSecondaryActionStyle"),
            MinimumHeightRequest = 46, FontSize = 15, Padding = new Thickness(10, 8) };
        toggle.AutomationId = "MatrixTechnicalDetails";
        void RefreshToggle()
        {
            toggle.Emoji = _technical.IsVisible ? "▾" : "▸";
            toggle.Caption = T("TechnicalDetails");
            SemanticProperties.SetDescription(toggle, T("TechnicalDetails") + ". " + T(_technical.IsVisible ? "Expanded" : "Collapsed"));
        }
        toggle.Clicked += (_, _) => { _technical.IsVisible = !_technical.IsVisible; RefreshToggle(); };
        _translations.Add(RefreshToggle);
        content.Add(toggle);
        _technical.Add(_technicalBackend);
        _technical.Add(_technicalStorage);
        _technical.Add(_technicalBatches);
        _technical.Add(Caption("BackendHelp"));
        content.Add(_technical);
        _resultCard = Card(content);
        _resultCard.IsVisible = false;
        return _resultCard;
    }

    private View ResultMetric(string key, Label value)
    {
        var caption = Caption(key);
        caption.FontSize = 13;
        caption.SetDynamicResource(Label.TextColorProperty, "WallpaperTextSecondaryColor");
        value.FontSize = 18;
        value.FontAttributes = FontAttributes.Bold;
        value.LineBreakMode = LineBreakMode.CharacterWrap;
        var host = new Border { Padding = new Thickness(12, 10), StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Content = new VerticalStackLayout { Spacing = 4, Children = { caption, value } } };
        host.SetDynamicResource(BackgroundColorProperty, "WallpaperSurfaceAltColor");
        return host;
    }

    private Border BuildSolutionCard()
    {
        _solutionHeading.Style = GetStyle("SolverSectionTitleStyle");
        _solutionPrompt.SetDynamicResource(Label.TextColorProperty, "WallpaperTextSecondaryColor");
        return Card(new VerticalStackLayout { Spacing = 12,
            Children = { _solutionHeading, _solutionPrompt, _solutionSteps } });
    }

    private void RefreshExecution()
    {
        _resultCard.IsVisible = _result is not null && _elapsed is not null;
        if (_result is { } result && _elapsed is { } elapsed)
        {
            _resultOperation.Text = T(result.Operation switch
            { MatrixOperation.Subtract => "Subtract", MatrixOperation.Multiply => "Multiply", _ => "Add" });
            _resultSize.Text = F("ResultSizeValue", result.Rows, result.Columns);
            _resultDuration.Text = elapsed < 0.001 ? T("UnderMillisecond")
                : elapsed < 1 ? F("Milliseconds", (elapsed * 1000).ToString("0.##", CultureInfo.CurrentCulture))
                : F("Seconds", elapsed.ToString("0.###", CultureInfo.CurrentCulture));
            _resultMode.Text = T(result.VectorBatches > 0 ? "SimdMode" : "ScalarMode");
            string backend = result.Backend switch
            {
                MatrixBackend.Sse2 => "SSE2 · 128-bit", MatrixBackend.Avx2 => "AVX2 · 256-bit",
                MatrixBackend.Avx512 => "AVX-512F · 512-bit", _ => "Scalar"
            };
            _technicalBackend.Text = F("RecordedBackend", backend);
            _technicalStorage.Text = F("ResultStorage", result.Operation == MatrixOperation.Multiply ? "BigInteger" : "Int128");
            _technicalBatches.Text = F("VectorBatches", result.VectorBatches);
        }
        RefreshSolution();
    }

    private void RefreshSolution()
    {
        _solutionSteps.Clear();
        bool hasCell = _selected == 2 && _result is not null;
        _solutionPrompt.IsVisible = !hasCell;
        _solutionSteps.IsVisible = hasCell;
        _solutionHeading.Text = T("CellSolution");
        _solutionPrompt.Text = T(_result is null ? "CellSolutionHelp" : "SelectResultCell");
        if (!hasCell) return;

        int row = _viewport.SelectedRow, column = _viewport.SelectedColumn;
        var solution = MatrixSolutionPresentation.ForCell(_a.Matrix, _b.Matrix, _result!, row, column);
        _solutionHeading.Text = F("SolutionForCell", row + 1, column + 1);
        if (_result!.Operation == MatrixOperation.Multiply)
        {
            _solutionSteps.Add(SolutionStep(1, "ChooseRowColumn", F("RowColumnRule", row + 1, column + 1, _a.Columns)));
            var products = SolutionStep(2, "MultiplyElements");
            if (solution.RemainingCount > 0) products.Add(SolutionText(F("ProductPreview", solution.Terms.Count, _a.Columns), secondary: true));
            foreach (var term in solution.Terms)
            {
                products.Add(SolutionText(term.Formula, secondary: true));
                products.Add(SolutionText(term.Calculation));
            }
            _solutionSteps.Add(products);
            var sum = SolutionStep(3, "SumProducts");
            if (solution.RemainingCount > 0)
            {
                sum.Add(SolutionText(F("LeadingSubtotal", solution.Terms.Count, solution.LeadingSum)));
                sum.Add(SolutionText(F("RemainingSubtotal", solution.RemainingCount, solution.RemainingSum)));
            }
            sum.Add(SolutionText(solution.Calculation));
            _solutionSteps.Add(sum);
        }
        else
        {
            _solutionSteps.Add(SolutionStep(1, "ChooseSamePosition", F("SamePositionRule", row + 1, column + 1), solution.Formula));
            _solutionSteps.Add(SolutionStep(2, "SubstituteAndCalculate", solution.Calculation));
        }
        var answer = SolutionText($"{solution.Cell} = {solution.Answer.ToString(CultureInfo.InvariantCulture)}");
        answer.FontSize = 22;
        answer.FontAttributes = FontAttributes.Bold;
        answer.SetDynamicResource(Label.TextColorProperty, "PrimaryColor");
        _solutionSteps.Add(new VerticalStackLayout { Spacing = 6, Children = { SolutionText(T("CellAnswer"), secondary: true), answer } });
    }

    private VerticalStackLayout SolutionStep(int number, string title, params string[] lines)
    {
        var heading = SolutionText(F("StepTitle", number, T(title)));
        heading.FontAttributes = FontAttributes.Bold;
        heading.SetDynamicResource(Label.TextColorProperty, "PrimaryColor");
        var step = new VerticalStackLayout { Spacing = 6, Children = { heading } };
        foreach (string line in lines) step.Add(SolutionText(line));
        return step;
    }

    private static Label SolutionText(string text, bool secondary = false)
    {
        var label = TextLabel();
        label.Text = text;
        label.LineBreakMode = LineBreakMode.CharacterWrap;
        label.LineHeight = 1.2;
        if (secondary) label.SetDynamicResource(Label.TextColorProperty, "WallpaperTextSecondaryColor");
        return label;
    }
}
