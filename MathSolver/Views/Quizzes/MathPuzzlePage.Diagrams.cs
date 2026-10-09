using MathSolver.Graphics;
using MathSolver.Controls;
using MathSolver.Models;
using MathSolver.Services;
using Microsoft.Maui.Graphics;

namespace MathSolver.Views;

public partial class MathPuzzlePage
{
    private ArithmeticQuizQuestion? _diagramQuestion;
    private bool _diagramExpanded;
    private bool _diagramPreviewOpen;
    private bool _wideDiagramLayout;
    private int _diagramScrollVersion;

    private void UpdateQuizDiagram()
    {
        if (_currentQuestion is null) { ResetQuizDiagram(); return; }
        QuestionCurriculumLabel.Text = PrimaryCurriculumCatalog.Describe(_currentQuestion, AppLanguageManager.CurrentLanguage);
        QuestionCurriculumLabel.IsVisible = !string.IsNullOrWhiteSpace(QuestionCurriculumLabel.Text);
        if (!ReferenceEquals(_diagramQuestion, _currentQuestion))
        {
            _diagramQuestion = _currentQuestion;
            _diagramExpanded = _currentQuestion.GeometryProblem is not null;
        }
        QuizVisualData? essential = _currentQuestion.ElementaryProblem?.Visual;
        QuizDiagram? diagram = QuizDiagramBuilder.Build(_currentQuestion, AppLanguageManager.CurrentLanguage, _questionAnswered);
        bool mandatory = essential is not null;
        bool available = mandatory || diagram is not null;
        // A revealed solution opens supporting diagrams, but cannot make required data collapsible.
        bool visible = available && (mandatory || _diagramExpanded || _questionAnswered);
        QuizDiagramToggleButton.IsVisible = available && !mandatory && !_questionAnswered;
        QuizDiagramToggleButton.Text = TranslateQuiz(_diagramExpanded ? "Quiz.HideDiagram" : "Quiz.ShowDiagram");
        QuizDiagramPanel.IsVisible = visible;
        QuizVisualView.IsVisible = visible;
        QuizVisualView.Drawable = mandatory ? new ElementaryQuizDrawable(QuizChartPresentation.ForDisplay(essential!, _questionAnswered))
            : diagram is not null ? new QuizDiagramDrawable(diagram) : null;
        QuizVisualView.Invalidate();
        QuizDiagramCaptionLabel.Text = diagram?.Caption ?? "";
        bool fractionCaption = TextbookFractionParser.ParseLine(QuizDiagramCaptionLabel.Text)
            .Any(fragment => fragment.Math is not null);
        QuizDiagramCaptionLabel.IsVisible = diagram is not null && !mandatory && !fractionCaption;
        QuizDiagramCaptionFractionView.Expression = QuizDiagramCaptionLabel.Text;
        QuizDiagramCaptionFractionView.IsVisible = diagram is not null && !mandatory && fractionCaption;
        QuizVisualDataLabel.IsVisible = visible && essential?.Kind is "table" or "bar" or "pie";
        QuizVisualDataLabel.Text = QuizVisualDataLabel.IsVisible && essential is not null
            ? QuizChartPresentation.DescribeValues(essential, _questionAnswered) : "";
        QuizDiagramNoteLabel.IsVisible = visible &&
            (essential?.Polygons is { Count: > 0 } || essential is { Kind: "bar", HiddenValueIndices.Count: > 0 }
                || diagram?.Kind is "geometry" or "motion" or "bars");
        QuizDiagramExplanationLabel.Text = diagram?.Explanation ?? "";
        bool showExplanation = visible && _questionAnswered && !string.IsNullOrWhiteSpace(diagram?.Explanation);
        QuizDiagramExplanationLabel.IsVisible = showExplanation && !_currentQuestion.UsesFractionFormatting;
        QuizDiagramExplanationFractionView.Expression = diagram?.Explanation ?? "";
        QuizDiagramExplanationFractionView.IsVisible = showExplanation && _currentQuestion.UsesFractionFormatting;
        SemanticProperties.SetDescription(QuizVisualView, QuizDiagramDescriptionFormatter.Format(
            diagram, essential, _questionAnswered, AppLanguageManager.CurrentLanguage));
        AutomationProperties.SetIsInAccessibleTree(QuizVisualView, true);
        UpdateQuestionDiagramLayout();
    }

    private async void OnQuizDiagramToggleClicked(object? sender, EventArgs e)
    {
        int version = ++_diagramScrollVersion;
        _diagramExpanded = !_diagramExpanded;
        UpdateQuizDiagram();

        if (!_diagramExpanded || !QuizDiagramPanel.IsVisible) return;
        ArithmeticQuizQuestion? question = _currentQuestion;

        // Opening changes both the panel visibility and the Grid columns on
        // wide Windows layouts. Wait for two matching layout snapshots instead
        // of assuming the native scroll extent has settled after a fixed delay.
        (Rect Diagram, Size Content)? previousLayout = null;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            await Task.Delay(32);
            if (version != _diagramScrollVersion || !_diagramExpanded ||
                !ReferenceEquals(question, _currentQuestion) ||
                !QuizDiagramPanel.IsVisible || !QuizVisualView.IsVisible ||
                MathPuzzleScrollView.Handler is null || Shell.Current?.CurrentPage != this)
                return;

#if WINDOWS
            if (MathPuzzleScrollView.Handler.PlatformView is Microsoft.UI.Xaml.Controls.ScrollViewer viewer)
                viewer.UpdateLayout();
#endif

            var layout = (QuizDiagramPanel.Bounds, MathPuzzleScrollView.ContentSize);
            bool arranged = QuizDiagramPanel.Height > 0 &&
                MathPuzzleScrollView.Height > 0 && MathPuzzleScrollView.ContentSize.Height > 0;
            if (arranged && previousLayout == layout) break;
            previousLayout = layout;
        }

        if (QuizDiagramPanel.Height <= 0 || MathPuzzleScrollView.Height <= 0) return;

        // MakeVisible deliberately does nothing when the panel already fits in
        // the viewport. Start explicitly focuses the diagram even in that case;
        // the platform clamps the offset to the end of shorter content.
        await MathPuzzleScrollView.ScrollToAsync(
            QuizDiagramPanel, ScrollToPosition.Start, animated: false);
    }

    private void OnQuestionDiagramSizeChanged(object? sender, EventArgs e) => UpdateQuestionDiagramLayout();

    private void UpdateQuestionDiagramLayout()
    {
        bool wide = QuizDiagramPanel.IsVisible &&
            QuizResponsiveLayout.PlaceDiagramBesideQuestion(QuestionDiagramGrid.Width, CurrentTextScale);
        if (_wideDiagramLayout == wide) return;
        _wideDiagramLayout = wide;
        QuestionDiagramGrid.ColumnDefinitions.Clear();
        QuestionDiagramGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        if (wide) QuestionDiagramGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        Grid.SetRow(QuizDiagramPanel, wide ? 0 : 1);
        Grid.SetColumn(QuizDiagramPanel, wide ? 1 : 0);
    }

    private void ResetQuizDiagram()
    {
        QuestionCurriculumLabel.Text = "";
        QuestionCurriculumLabel.IsVisible = false;
        _diagramScrollVersion++;
        _diagramQuestion = null;
        _diagramExpanded = false;
        QuizDiagramToggleButton.IsVisible = false;
        QuizDiagramPanel.IsVisible = false;
        QuizVisualView.IsVisible = false;
        QuizVisualView.Drawable = null;
        QuizDiagramCaptionLabel.Text = "";
        QuizDiagramCaptionFractionView.IsVisible = false;
        QuizDiagramCaptionFractionView.Expression = "";
        QuizVisualDataLabel.IsVisible = false;
        QuizVisualDataLabel.Text = "";
        QuizDiagramExplanationLabel.IsVisible = false;
        QuizDiagramExplanationLabel.Text = "";
        QuizDiagramExplanationFractionView.IsVisible = false;
        QuizDiagramExplanationFractionView.Expression = "";
        UpdateQuestionDiagramLayout();
    }

    private async void OnEnlargeQuizDiagramClicked(object? sender, EventArgs e)
    {
        if (_diagramPreviewOpen || QuizVisualView.Drawable is not IDrawable source) return;
        _diagramPreviewOpen = true;
        var details = new VerticalStackLayout { Spacing = 10, Padding = new Thickness(4, 4, 20, 4) };
        void AddText(string text, bool fraction = false, bool arithmetic = false)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (fraction)
            {
                var formatted = new FractionExpressionView { Expression = text, MathFontSize = 16,
                    ParseArithmeticExpressions = arithmetic, WrapContent = true, TokenSpacing = 4 };
                formatted.SetDynamicResource(FractionExpressionView.MathColorProperty, "TextPrimaryColor");
                details.Add(formatted);
            }
            else
            {
                var label = new Label { Text = text, FontSize = 15, LineBreakMode = LineBreakMode.WordWrap };
                label.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
                details.Add(label);
            }
        }
        // Freeze the same display-safe values used in the question. The enlarged
        // view must never reveal an answer or a masked chart value before grading.
        if (QuizDiagramCaptionLabel.IsVisible || QuizDiagramCaptionFractionView.IsVisible)
            AddText(QuizDiagramCaptionLabel.Text, QuizDiagramCaptionFractionView.IsVisible, true);
        if (QuizVisualDataLabel.IsVisible) AddText(QuizVisualDataLabel.Text);
        if (QuizDiagramNoteLabel.IsVisible) AddText(QuizDiagramNoteLabel.Text);
        if (QuizDiagramExplanationLabel.IsVisible || QuizDiagramExplanationFractionView.IsVisible)
            AddText(QuizDiagramExplanationLabel.Text, _currentQuestion?.UsesFractionFormatting == true,
                _currentQuestion?.ElementaryProblem?.Kind == QuizProblemKind.FractionSkills);
        var page = new DiagramPreviewPage(source, QuizVisualView.Width, QuizVisualView.Height,
            SemanticProperties.GetDescription(QuizVisualView) ?? "", details.Children.Count == 0 ? null : details);
        page.Disappearing += (_, _) => _diagramPreviewOpen = false;
        try
        {
            await Navigation.PushModalAsync(page);
        }
        catch
        {
            _diagramPreviewOpen = false;
            throw;
        }
    }
}
