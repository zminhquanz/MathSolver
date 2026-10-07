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
        QuizVisualView.Drawable = mandatory ? new ElementaryQuizDrawable(_questionAnswered ? essential! with { HiddenValueIndices = null } : essential)
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
            ? string.Join(" · ", essential.Labels.Select((label, index) => $"{label}: {(essential.HiddenValueIndices?.Contains(index) == true && !_questionAnswered ? "?" : essential.Values[index].ToString())} {essential.Unit}")) : "";
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
        var page = new ContentPage();
        page.SetDynamicResource(BackgroundColorProperty, "WallpaperSurfaceColor");
        var close = new Button { Text = TranslateQuiz("Quiz.CloseDiagram"), CornerRadius = 12 };
        View caption;
        if (QuizDiagramCaptionFractionView.IsVisible)
        {
            var formatted = new FractionExpressionView { Expression = QuizDiagramCaptionLabel.Text,
                MathFontSize = 18, ParseArithmeticExpressions = true, WrapContent = true,
                TokenSpacing = 4, HorizontalTextAlignment = TextAlignment.Center };
            formatted.SetDynamicResource(FractionExpressionView.MathColorProperty, "WallpaperTextPrimaryColor");
            caption = formatted;
        }
        else
        {
            var label = new Label { Text = QuizDiagramCaptionLabel.Text, FontSize = 18,
                HorizontalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.WordWrap };
            label.SetDynamicResource(Label.TextColorProperty, "WallpaperTextPrimaryColor");
            caption = label;
        }
        var image = new GraphicsView { WidthRequest = 720, HeightRequest = 520,
            Drawable = new DiagramPreviewDrawable(source, 2) };
        SemanticProperties.SetDescription(image, SemanticProperties.GetDescription(QuizVisualView));
        AutomationProperties.SetIsInAccessibleTree(image, true);
        var zoom = new Slider { Minimum = 1, Maximum = 3, Value = 2 };
        void SetZoom(double factor)
        {
            image.WidthRequest = 360 * factor;
            image.HeightRequest = 260 * factor;
            image.Drawable = new DiagramPreviewDrawable(source, (float)factor);
            image.Invalidate();
        }
        zoom.ValueChanged += (_, args) => SetZoom(args.NewValue);
        double pinchStart = 2;
        var pinch = new PinchGestureRecognizer();
        pinch.PinchUpdated += (_, args) =>
        {
            if (args.Status == GestureStatus.Started) pinchStart = zoom.Value;
            else if (args.Status == GestureStatus.Running)
            {
                pinchStart = Math.Clamp(pinchStart * args.Scale, 1, 3);
                zoom.Value = pinchStart;
            }
        };
        image.GestureRecognizers.Add(pinch);
        var zoomLabel = new Label { Text = TranslateQuiz("Quiz.DiagramZoom") };
        zoomLabel.SetDynamicResource(Label.TextColorProperty, "WallpaperTextSecondaryColor");
        var content = new Grid { Padding = new Thickness(16), RowSpacing = 10,
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
        content.Add(close, 0, 0); content.Add(caption, 0, 1);
        content.Add(new VerticalStackLayout { Children = { zoomLabel, zoom } }, 0, 2);
        content.Add(new ScrollView { Orientation = ScrollOrientation.Both, Content = image }, 0, 3);
        bool showExplanation = QuizDiagramExplanationLabel.IsVisible || QuizDiagramExplanationFractionView.IsVisible;
        View explanation;
        if (_currentQuestion?.UsesFractionFormatting == true)
        {
            var formatted = new FractionExpressionView { Expression = QuizDiagramExplanationLabel.Text,
                ParseArithmeticExpressions = _currentQuestion?.ElementaryProblem?.Kind == QuizProblemKind.FractionSkills,
                IsVisible = showExplanation, MathFontSize = 15, WrapContent = true, TokenSpacing = 4 };
            formatted.SetDynamicResource(FractionExpressionView.MathColorProperty, "WallpaperTextPrimaryColor");
            explanation = formatted;
        }
        else
        {
            var label = new Label { Text = QuizDiagramExplanationLabel.Text, IsVisible = showExplanation,
                FontSize = 15, LineBreakMode = LineBreakMode.WordWrap };
            label.SetDynamicResource(Label.TextColorProperty, "WallpaperTextPrimaryColor");
            explanation = label;
        }
        content.Add(new ScrollView { Content = explanation, MaximumHeightRequest = 150 }, 0, 4);
        page.Content = content;
        close.Clicked += async (_, _) => await Navigation.PopModalAsync();
        page.Disappearing += (_, _) => _diagramPreviewOpen = false;
        try { await Navigation.PushModalAsync(page); }
        catch { _diagramPreviewOpen = false; throw; }
    }

    private sealed class DiagramPreviewDrawable(IDrawable source, float scale) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.SaveState();
            try
            {
                canvas.Scale(scale, scale);
                source.Draw(canvas, new RectF(0, 0, dirtyRect.Width / scale, dirtyRect.Height / scale));
            }
            finally { canvas.RestoreState(); }
        }
    }
}
