using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Services.Localization;
using MathSolver.Services.Printing;

namespace MathSolver.Views;

public partial class MathPuzzlePage
{
    private bool _openingPdf;
    private async void OnExportQuizPdfClicked(object? sender, EventArgs e)
    {
        if (_openingPdf) return;
        _openingPdf = true;
        try
        {
            // Initial values only: the PDF page owns all subsequent changes.
            var selected = GetSelectedFixedProblemRequest();
            if (selected is { } request)
            {
                selected = request.Kind switch
                {
                    QuizProblemKind.Arithmetic => request with { ArithmeticOperation = _selectedBasicComparison ? null : _selectedBasicOperation, IsComparison = _selectedBasicComparison },
                    QuizProblemKind.Fraction => request with { FractionOperation = _selectedFractionComparison ? null : _selectedFractionOperation, IsComparison = _selectedFractionComparison },
                    QuizProblemKind.FindX => request with { FindXOperation = _selectedFindXOperation },
                    QuizProblemKind.Geometry => request with { GeometryShape = _selectedGeometryShape, GeometryMeasurement = _selectedGeometryMeasurement },
                    QuizProblemKind.Proportion => request with { ProportionType = _selectedProportionType },
                    QuizProblemKind.Average => request with { AverageType = _selectedAverageType },
                    QuizProblemKind.Percentage => request with { PercentageType = _selectedPercentageType },
                    QuizProblemKind.Motion => request with { MotionType = _selectedMotionType },
                    QuizProblemKind.Expression => request with { ExpressionType = _selectedExpressionType },
                    _ => request with { ElementaryType = _selectedElementaryType }
                };
            }
            var settings = new QuizWorksheetSettings(OperationPicker.SelectedIndex, selected, GetCurriculumContext().Tier,
                AppLanguageManager.CurrentLanguage, _learningGroup, _arithmeticWordProblems, _findXWordProblems,
                _fractionWordProblems, _decimalWordProblems, _measurementWordProblems);
            await Navigation.PushAsync(new QuizPdfPage(settings, new PracticeQuestionFactory(AiQuestionBank.Current.Practice)));
        }
        catch (Exception error)
        {
            System.Diagnostics.Debug.WriteLine(error);
            await DisplayAlertAsync(LocalizationService.TranslateKey("QuizPdf.Title"), LocalizationService.TranslateKey("QuizPdf.Failed"), "OK");
        }
        finally { _openingPdf = false; }
    }
}
