using MathSolver.Models;
using MathSolver.Services;

namespace MathSolver.Views;

public partial class MathPuzzlePage
{
    private Button? _pendingAnswerButton;
    private bool _answerInputsEnabled;
    private int? _activeQuestionGeneration;
    private bool IsGeneratingQuestion => _activeQuestionGeneration == _questionGenerationVersion;

    private void UpdatePracticeActionState()
    {
        bool answering = _currentQuestion is not null && !_questionAnswered && _answerInputsEnabled && !IsGeneratingQuestion;
        SubmitEssayAnswerButton.IsVisible = !_questionAnswered;
        SubmitEssayAnswerButton.IsEnabled = answering && (_currentQuestion!.Mode == ArithmeticQuizMode.Essay
            ? !string.IsNullOrWhiteSpace(EssayWorkEditor.Text) : _pendingAnswerButton is not null);
        NextQuestionButton.IsVisible = _questionAnswered;
        NextQuestionButton.IsEnabled = _currentQuestion is not null && _questionAnswered && !IsGeneratingQuestion;
        RegenerateQuestionButton.IsVisible = !_questionAnswered;
        RegenerateQuestionButton.IsEnabled = !_questionAnswered && !IsGeneratingQuestion;
    }

    private void OnEssayWorkTextChanged(object? sender, TextChangedEventArgs e) => UpdatePracticeActionState();

    private void OnTrueFalseAnswerClicked(object? sender, EventArgs e)
    {
        if (_currentQuestion?.Mode == ArithmeticQuizMode.TrueFalse && sender is Button button)
            SelectPendingAnswer(button);
    }

    private void OnChoiceAnswerClicked(object? sender, EventArgs e)
    {
        if (_currentQuestion?.Mode == ArithmeticQuizMode.MultipleChoice && sender is Button button)
            SelectPendingAnswer(button);
    }

    private void SelectPendingAnswer(Button button)
    {
        if (_questionAnswered || !_answerInputsEnabled || IsGeneratingQuestion) return;
        _pendingAnswerButton = button;
        RefreshPendingAnswerSelection();
        UpdatePracticeActionState();
    }

    private void RefreshPendingAnswerSelection()
    {
        if (_questionAnswered) return;
        RefreshTrueFalseAnswerButtonTheme();
        foreach (Button button in ChoiceButtons.Concat(new[] { TrueAnswerButton, FalseAnswerButton }))
        {
            bool selected = ReferenceEquals(button, _pendingAnswerButton);
            if (ChoiceButtons.Contains(button)) ApplyNeutralAnswerStyle(button);
            button.BorderWidth = selected ? 3 : 1;
            SemanticProperties.SetHint(button, selected ? TranslateQuiz("Choice.Selected") : string.Empty);
            if (!selected) continue;
            button.SetDynamicResource(Button.BackgroundColorProperty, "PrimarySoftColor");
            button.SetDynamicResource(Button.BorderColorProperty, "PrimaryColor");
            button.SetDynamicResource(Button.TextColorProperty, "TextPrimaryColor");
            SetChoiceFractionColor(button, "TextPrimaryColor");
        }
    }

    private async void OnCheckAnswerClicked(object? sender, EventArgs e)
    {
        if (!CanUpdatePracticeLayout || !SubmitEssayAnswerButton.IsEnabled || _questionAnswered || _currentQuestion is null) return;
        switch (_currentQuestion.Mode)
        {
            case ArithmeticQuizMode.Essay:
                OnSubmitEssayAnswerClicked(sender, e);
                break;
            case ArithmeticQuizMode.TrueFalse when _pendingAnswerButton is not null:
                GradeTrueFalseAnswer(_pendingAnswerButton, e);
                break;
            case ArithmeticQuizMode.MultipleChoice when _pendingAnswerButton is not null:
                GradeChoiceAnswer(_pendingAnswerButton, e);
                break;
        }
        if (!_questionAnswered) return;
        EssayWorkEditor.Unfocus();
        await ScrollToPracticeElementAsync(FeedbackBorder, ScrollToPosition.MakeVisible);
    }
}
