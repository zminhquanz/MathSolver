using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Views;

public partial class MathPuzzlePage
{
    private bool _practiceSettingsExpanded = true;
    private bool? _compactSettings;
    private int _practiceScrollVersion;
    private readonly Dictionary<Button, (string Icon, string Key)> _choiceStatuses = [];

    private static double CurrentTextScale
    {
        get
        {
#if ANDROID
            return Math.Max(1, Android.App.Application.Context.Resources?.Configuration?.FontScale ?? 1);
#elif WINDOWS
            return Math.Max(1, new global::Windows.UI.ViewManagement.UISettings().TextScaleFactor);
#else
            return 1;
#endif
        }
    }

    private void UpdatePracticeSummary()
    {
        string mode = Translate(_selectedMode switch
        {
            ArithmeticQuizMode.Essay => "Quiz.EssayMode",
            ArithmeticQuizMode.MultipleChoice => "Quiz.MultipleChoiceMode",
            _ => "Quiz.TrueFalseMode"
        });
        var selections = new List<string> { OperationPicker.SelectedItem?.ToString() ?? "" };
        foreach (var (panel, picker) in new (View Panel, Picker Picker)[]
        {
            (ElementaryTypePanel, ElementaryTypePicker), (ExpressionTypePanel, ExpressionTypePicker),
            (MotionTypePanel, MotionTypePicker), (AverageTypePanel, AverageTypePicker),
            (PercentageTypePanel, PercentageTypePicker), (FindXTypePanel, FindXTypePicker),
            (GeometryShapePanel, GeometryShapePicker)
        })
            if (panel.IsVisible && picker.SelectedItem is { } selected) selections.Add(selected.ToString()!);
        if (GeometryShapePanel.IsVisible && GeometryMeasurementPicker.SelectedItem is { } measurement)
            selections.Add(measurement.ToString()!);
        if (ProportionTypePanel.IsVisible)
            selections.Add((_selectedProportionType switch
            {
                ProportionQuizType.Direct => DirectProportionButton,
                ProportionQuizType.Inverse => InverseProportionButton,
                _ => MixedProportionButton
            }).Text);
        if (ProblemOperationPanel.IsVisible)
            selections.Add(GetSelectedFixedProblemRequest()?.IsComparison == true
                ? ProblemCompareButton.Text
                : (GetSelectedFixedProblemRequest()?.Kind == QuizProblemKind.Fraction
                    ? _selectedFractionOperation switch { FractionOperation.Add => "+", FractionOperation.Subtract => "−",
                        FractionOperation.Multiply => "×", FractionOperation.Divide => "÷", _ => ProblemMixedButton.Text }
                    : _selectedBasicOperation is { } operation ? BasicArithmeticEngine.GetSymbol(operation) : ProblemMixedButton.Text));
        PracticeSummaryLabel.Text = $"{mode} · {new string('★', (int)_selectedCurriculumTier)} · " +
            string.Join(" · ", selections.Where(value => !string.IsNullOrWhiteSpace(value)));
        ChangePracticeSettingsButton.Text = TranslateQuiz(_practiceSettingsExpanded ? "Quiz.HideSettings" : "Quiz.ChangeSettings");
        StartPracticeButton.IsEnabled = _currentQuestion is not null;
    }

    private void SetPracticeSettingsExpanded(bool expanded)
    {
        _practiceScrollVersion++;
        _practiceSettingsExpanded = expanded;
        PracticeSettingsPanel.IsVisible = expanded;
        UpdatePracticeSummary();
    }

    private void OnChangePracticeSettingsClicked(object? sender, EventArgs e)
    {
        SetPracticeSettingsExpanded(!_practiceSettingsExpanded);
        if (_practiceSettingsExpanded) TrueFalseModeButton.Focus();
    }

    private async void OnStartPracticeClicked(object? sender, EventArgs e)
    {
        if (_currentQuestion is null) return;
        SetPracticeSettingsExpanded(false);
        ChangePracticeSettingsButton.Focus();
        await ScrollToPracticeElementAsync(QuestionCard, ScrollToPosition.Start);
    }

    private async void OnEssayWorkFocused(object? sender, FocusEventArgs e)
    {
        if (!_practiceSettingsExpanded || _currentQuestion is null) return;
        SetPracticeSettingsExpanded(false);
        await ScrollToPracticeElementAsync(EssayWorkEditor, ScrollToPosition.MakeVisible);
    }

    private async void OnPracticeViewportSizeChanged(object? sender, EventArgs e)
    {
        // AdjustResize and rotation can move an already focused editor while
        // the question and draft stay intact.
        if (EssayWorkEditor.IsFocused && !_practiceSettingsExpanded)
            await ScrollToPracticeElementAsync(EssayWorkEditor, ScrollToPosition.MakeVisible);
    }

    private async Task ScrollToPracticeElementAsync(View target, ScrollToPosition position)
    {
        int version = ++_practiceScrollVersion;
        var question = _currentQuestion;
        (Rect Bounds, Size Content, double Height)? previous = null;
        // Visibility changes invalidate the layout. Wait for stable bounds so
        // scrolling uses the collapsed position, not the old settings height.
        for (int attempt = 0; attempt < 6; attempt++)
        {
            await Task.Delay(16);
            if (version != _practiceScrollVersion || _practiceSettingsExpanded ||
                !ReferenceEquals(question, _currentQuestion) || MathPuzzleScrollView.Handler is null ||
                Shell.Current?.CurrentPage != this) return;
#if WINDOWS
            if (MathPuzzleScrollView.Handler.PlatformView is Microsoft.UI.Xaml.Controls.ScrollViewer viewer)
                viewer.UpdateLayout();
#endif
            var bounds = (target.Bounds, MathPuzzleScrollView.ContentSize, MathPuzzleScrollView.Height);
            if (target.Height > 0 && bounds == previous) break;
            previous = bounds;
        }
        await MathPuzzleScrollView.ScrollToAsync(target, position, false);
    }

    private void OnPracticeLayoutSizeChanged(object? sender, EventArgs e)
    {
        PracticeSettingsPanel.WidthRequest = Math.Min(1120,
            Math.Max(0, QuizContent.Width - QuizContent.Padding.HorizontalThickness));
        double width = PracticeSummaryGrid.Width;
        if (width <= 0) return;
        bool compact = QuizResponsiveLayout.UseCompactSettings(width, CurrentTextScale);
        if (_compactSettings != compact)
        {
            _compactSettings = compact;
            PracticeSummaryGrid.ColumnDefinitions.Clear();
            PracticeSummaryGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            PracticeSummaryGrid.ColumnDefinitions.Add(new ColumnDefinition(compact ? GridLength.Star : GridLength.Auto));
            if (!compact) PracticeSummaryGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            Grid.SetColumnSpan(PracticeSummaryLabel, compact ? 2 : 1);
            Grid.SetRow(ChangePracticeSettingsButton, compact ? 1 : 0);
            Grid.SetColumn(ChangePracticeSettingsButton, compact ? 0 : 1);
            Grid.SetRow(OpenAiQuestionBankButton, compact ? 1 : 0);
            Grid.SetColumn(OpenAiQuestionBankButton, compact ? 1 : 2);
            Grid.SetRow(AiQuestionBankProgressLabel, compact ? 2 : 1);
            Grid.SetColumnSpan(AiQuestionBankProgressLabel, compact ? 2 : 3);
            UpdateQuestionModeLayout();
            UpdateDifficultyLayout(compact);
        }
        UpdateElementaryChoiceLayout(_currentQuestion?.ElementaryProblem);
    }

    private void UpdateDifficultyLayout(bool compact)
    {
        CurriculumGrid.ColumnDefinitions.Clear();
        CurriculumGrid.RowDefinitions.Clear();
        for (int i = 0; i < (compact ? 6 : 5); i++)
            CurriculumGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (int i = 0; i < (compact ? 2 : 1); i++)
            CurriculumGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        var buttons = new[] { CurriculumOneStarButton, CurriculumTwoStarsButton, CurriculumThreeStarsButton,
            CurriculumFourStarsButton, CurriculumFiveStarsButton };
        for (int i = 0; i < buttons.Length; i++)
        {
            Grid.SetRow(buttons[i], compact && i >= 3 ? 1 : 0);
            Grid.SetColumn(buttons[i], compact ? (i < 3 ? i * 2 : (i - 3) * 3) : i);
            Grid.SetColumnSpan(buttons[i], compact ? (i < 3 ? 2 : 3) : 1);
            SemanticProperties.SetDescription(buttons[i], string.Format(TranslateQuiz("Quiz.DifficultyDescription"), i + 1));
        }
    }

    private void OnAnswerGridSizeChanged(object? sender, EventArgs e) =>
        UpdateElementaryChoiceLayout(_currentQuestion?.ElementaryProblem);

    private void UpdateChoiceDescriptions()
    {
        for (int i = 0; i < ChoiceButtons.Length; i++)
        {
            if (_choiceStatuses.TryGetValue(ChoiceButtons[i], out var status))
            {
                SetChoiceStatus(ChoiceButtons[i], status.Icon, status.Key);
                continue;
            }
            string text = ChoiceFractionViews[i].IsVisible ? ChoiceFractionViews[i].Expression : ChoiceButtons[i].Text;
            SemanticProperties.SetDescription(ChoiceButtons[i], AccessibleMathText.Format(text, AppLanguageManager.CurrentLanguage));
        }
        SemanticProperties.SetDescription(EssayWorkEditor, EssayWorkLabel.Text);
    }

    private void SetChoiceStatus(Button button, string icon, string key)
    {
        _choiceStatuses[button] = (icon, key);
        int index = Array.IndexOf(ChoiceButtons, button);
        if (index < 0) return;
        bool fraction = ChoiceFractionViews[index].IsVisible;
        string text = (fraction ? ChoiceFractionViews[index].Expression : button.Text).TrimStart('✓', '✗', ' ');
        if (fraction) ChoiceFractionViews[index].Expression = $"{icon} {text}";
        else button.Text = $"{icon} {text}";
        SemanticProperties.SetDescription(button, TranslateQuiz(key) + ". " +
            AccessibleMathText.Format(text, AppLanguageManager.CurrentLanguage));
    }
}
