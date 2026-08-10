using MathSolver.Models;
using MathSolver.Services;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Views;

public partial class MathPuzzlePage : ContentPage
{
    private readonly BasicArithmeticEngine _arithmeticEngine = new();
    private readonly ArithmeticQuizGenerator _quizGenerator;

    private ArithmeticQuizMode _selectedMode =
        ArithmeticQuizMode.TrueFalse;

    private ArithmeticQuizQuestion? _currentQuestion;
    private bool _questionAnswered;
    private bool? _lastAnswerWasCorrect;
    private bool _isUpdatingOperationPicker;
    private int _questionCount;
    private int _correctCount;
    private int _incorrectCount;
    private int _mainTabAnimationVersion;

    private Button[] ChoiceButtons =>
    [
        ChoiceAButton,
        ChoiceBButton,
        ChoiceCButton,
        ChoiceDButton
    ];

    public MathPuzzlePage()
    {
        InitializeComponent();

        _quizGenerator =
            new ArithmeticQuizGenerator(
                _arithmeticEngine);

        // Trang mới dùng hoàn toàn stable localization keys. Không để facade
        // legacy ghi đè các chuỗi động như câu hỏi và điểm số.
        LocalizationService.ExcludeSubtreeFromLegacyTracking(
            this);

        LocalizationService.CultureChanged +=
            OnCultureChanged;

        UpdateOperationPickerItems();
        UpdateModeStyles();
        UpdateScoreLabels();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        BeginMainTabTransitionIfPending();

        if (_currentQuestion is null)
        {
            GenerateQuestion();
        }
        else
        {
            RenderCurrentQuestion(
                resetAnswerControls: false);
            UpdateScoreLabels();
        }
    }

    protected override void OnDisappearing()
    {
        _mainTabAnimationVersion++;

        MathPuzzlePageContentRoot.CancelAnimations();
        ResetMainTabRoot();

        base.OnDisappearing();
    }

    private void OnCultureChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Dispatch(
            () =>
            {
                UpdateOperationPickerItems();
                UpdateScoreLabels();

                if (_currentQuestion is not null)
                {
                    RenderCurrentQuestion(
                        resetAnswerControls: false);

                    if (_questionAnswered &&
                        _lastAnswerWasCorrect is
                            bool wasCorrect)
                    {
                        ShowFeedback(
                            wasCorrect,
                            _currentQuestion.CorrectAnswer);
                    }
                }
            });
    }

    private void OnPracticeTabClicked(
        object? sender,
        EventArgs e)
    {
        SelectSubTab(
            showPractice: true);
    }

    private void OnLearnTabClicked(
        object? sender,
        EventArgs e)
    {
        SelectSubTab(
            showPractice: false);
    }

    private void SelectSubTab(
        bool showPractice)
    {
        PracticeContent.IsVisible =
            showPractice;

        LearnContent.IsVisible =
            !showPractice;

        SelectionButtonStyler.Select(
            showPractice
                ? PracticeTabButton
                : LearnTabButton,
            PracticeTabButton,
            LearnTabButton);
    }

    private void OnTrueFalseModeClicked(
        object? sender,
        EventArgs e)
    {
        SelectMode(
            ArithmeticQuizMode.TrueFalse);
    }

    private void OnMultipleChoiceModeClicked(
        object? sender,
        EventArgs e)
    {
        SelectMode(
            ArithmeticQuizMode.MultipleChoice);
    }

    private void SelectMode(
        ArithmeticQuizMode mode)
    {
        if (_selectedMode == mode &&
            _currentQuestion is not null)
        {
            return;
        }

        _selectedMode =
            mode;

        UpdateModeStyles();
        GenerateQuestion();
    }

    private void UpdateModeStyles()
    {
        SelectionButtonStyler.Select(
            _selectedMode == ArithmeticQuizMode.TrueFalse
                ? TrueFalseModeButton
                : MultipleChoiceModeButton,
            TrueFalseModeButton,
            MultipleChoiceModeButton);

        TrueFalseAnswerGrid.IsVisible =
            _selectedMode == ArithmeticQuizMode.TrueFalse;

        MultipleChoiceAnswerGrid.IsVisible =
            _selectedMode == ArithmeticQuizMode.MultipleChoice;

        QuestionPromptLabel.Text =
            Translate(
                _selectedMode == ArithmeticQuizMode.TrueFalse
                    ? "Quiz.QuestionTitle"
                    : "Quiz.MultipleChoiceQuestionTitle");
    }

    private void UpdateOperationPickerItems()
    {
        int selectedIndex =
            OperationPicker.SelectedIndex < 0
                ? 0
                : OperationPicker.SelectedIndex;

        _isUpdatingOperationPicker =
            true;

        try
        {
            OperationPicker.Items.Clear();
            OperationPicker.Items.Add(
                Translate(
                    "Quiz.OperationMixed"));
            OperationPicker.Items.Add(
                Translate(
                    "Quiz.OperationAddition"));
            OperationPicker.Items.Add(
                Translate(
                    "Quiz.OperationSubtraction"));
            OperationPicker.Items.Add(
                Translate(
                    "Quiz.OperationMultiplication"));
            OperationPicker.Items.Add(
                Translate(
                    "Quiz.OperationDivision"));

            OperationPicker.SelectedIndex =
                Math.Clamp(
                    selectedIndex,
                    0,
                    OperationPicker.Items.Count - 1);
        }
        finally
        {
            _isUpdatingOperationPicker =
                false;
        }
    }

    private void OnOperationChanged(
        object? sender,
        EventArgs e)
    {
        if (_isUpdatingOperationPicker ||
            OperationPicker.SelectedIndex < 0)
        {
            return;
        }

        GenerateQuestion();
    }

    private ArithmeticOperation? GetSelectedOperation()
    {
        return OperationPicker.SelectedIndex switch
        {
            1 => ArithmeticOperation.Add,
            2 => ArithmeticOperation.Subtract,
            3 => ArithmeticOperation.Multiply,
            4 => ArithmeticOperation.Divide,
            _ => null
        };
    }

    private void GenerateQuestion()
    {
        _questionAnswered =
            false;

        _lastAnswerWasCorrect =
            null;

        NextQuestionButton.IsEnabled =
            false;

        try
        {
            _currentQuestion =
                _quizGenerator.Generate(
                    _selectedMode,
                    GetSelectedOperation());

            _questionCount++;

            RenderCurrentQuestion(
                resetAnswerControls: true);
            UpdateScoreLabels();
        }
        catch (InvalidOperationException)
        {
            _currentQuestion =
                null;

            QuestionExpressionLabel.Text =
                Translate(
                    "Quiz.GenerationError");
        }
    }

    private void RenderCurrentQuestion(
        bool resetAnswerControls)
    {
        if (_currentQuestion is null)
        {
            return;
        }

        string left =
            _currentQuestion.Expression.LeftOperand.ToString(
                "N0",
                CultureInfo.CurrentCulture);

        string right =
            _currentQuestion.Expression.RightOperand.ToString(
                "N0",
                CultureInfo.CurrentCulture);

        string symbol =
            BasicArithmeticEngine.GetSymbol(
                _currentQuestion.Expression.Operation);

        if (_currentQuestion.Mode ==
            ArithmeticQuizMode.TrueFalse)
        {
            string presentedAnswer =
                _currentQuestion.PresentedAnswer
                    .GetValueOrDefault()
                    .ToString(
                        "N0",
                        CultureInfo.CurrentCulture);

            QuestionExpressionLabel.Text =
                $"{left} {symbol} {right} = {presentedAnswer}";
        }
        else
        {
            QuestionExpressionLabel.Text =
                $"{left} {symbol} {right} = ?";

            for (int index = 0;
                 index < ChoiceButtons.Length;
                 index++)
            {
                BigInteger choice =
                    _currentQuestion.Choices[index];

                Button button =
                    ChoiceButtons[index];

                char prefix =
                    (char)('A' + index);

                button.Text =
                    $"{prefix}. {choice.ToString("N0", CultureInfo.CurrentCulture)}";

                button.CommandParameter =
                    choice.ToString(
                        CultureInfo.InvariantCulture);
            }
        }

        if (resetAnswerControls)
        {
            ResetAnswerControls();
        }
    }

    private void ResetAnswerControls()
    {
        TrueAnswerButton.IsEnabled =
            true;

        FalseAnswerButton.IsEnabled =
            true;

        foreach (Button button in ChoiceButtons)
        {
            button.IsEnabled =
                true;

            ApplyNeutralAnswerStyle(
                button);
        }

        FeedbackBorder.IsVisible =
            false;

        NextQuestionButton.IsEnabled =
            false;
    }

    private void OnTrueFalseAnswerClicked(
        object? sender,
        EventArgs e)
    {
        if (_questionAnswered ||
            _currentQuestion?.PresentedEquationIsCorrect is not
                bool expectedAnswer ||
            sender is not Button button ||
            !bool.TryParse(
                button.CommandParameter?.ToString(),
                out bool selectedAnswer))
        {
            return;
        }

        CompleteAnswer(
            selectedAnswer == expectedAnswer,
            selectedButton: null);
    }

    private void OnChoiceAnswerClicked(
        object? sender,
        EventArgs e)
    {
        if (_questionAnswered ||
            _currentQuestion is null ||
            sender is not Button button ||
            !BigInteger.TryParse(
                button.CommandParameter?.ToString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out BigInteger selectedAnswer))
        {
            return;
        }

        CompleteAnswer(
            selectedAnswer ==
                _currentQuestion.CorrectAnswer,
            button);
    }

    private void CompleteAnswer(
        bool isCorrect,
        Button? selectedButton)
    {
        if (_currentQuestion is null)
        {
            return;
        }

        _questionAnswered =
            true;

        _lastAnswerWasCorrect =
            isCorrect;

        if (isCorrect)
        {
            _correctCount++;
        }
        else
        {
            _incorrectCount++;
        }

        TrueAnswerButton.IsEnabled =
            false;

        FalseAnswerButton.IsEnabled =
            false;

        foreach (Button button in ChoiceButtons)
        {
            button.IsEnabled =
                false;

            if (_currentQuestion.Mode ==
                    ArithmeticQuizMode.MultipleChoice &&
                BigInteger.TryParse(
                    button.CommandParameter?.ToString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out BigInteger answer) &&
                answer == _currentQuestion.CorrectAnswer)
            {
                ApplyCorrectAnswerStyle(
                    button);
            }
        }

        if (!isCorrect &&
            selectedButton is not null)
        {
            ApplyIncorrectAnswerStyle(
                selectedButton);
        }

        ShowFeedback(
            isCorrect,
            _currentQuestion.CorrectAnswer);

        NextQuestionButton.IsEnabled =
            true;

        UpdateScoreLabels();
    }

    private void ShowFeedback(
        bool isCorrect,
        BigInteger correctAnswer)
    {
        string answerText =
            correctAnswer.ToString(
                "N0",
                CultureInfo.CurrentCulture);

        FeedbackLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                Translate(
                    isCorrect
                        ? "Quiz.CorrectFeedback"
                        : "Quiz.IncorrectFeedback"),
                answerText);

        FeedbackBorder.IsVisible =
            true;

        FeedbackBorder.SetDynamicResource(
            Border.BackgroundColorProperty,
            isCorrect
                ? "SuccessSoftColor"
                : "DangerSoftColor");

        FeedbackBorder.SetDynamicResource(
            Border.StrokeProperty,
            isCorrect
                ? "SuccessBorderBrush"
                : "DangerBorderBrush");

        FeedbackLabel.SetDynamicResource(
            Label.TextColorProperty,
            isCorrect
                ? "SuccessColor"
                : "DangerColor");
    }

    private void OnNextQuestionClicked(
        object? sender,
        EventArgs e)
    {
        if (!_questionAnswered)
        {
            return;
        }

        GenerateQuestion();
    }

    private void OnResetScoreClicked(
        object? sender,
        EventArgs e)
    {
        _correctCount =
            0;

        _incorrectCount =
            0;

        UpdateScoreLabels();
    }

    private void UpdateScoreLabels()
    {
        QuestionCounterLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                Translate(
                    "Quiz.QuestionCounter"),
                _questionCount);

        CorrectScoreLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                Translate(
                    "Quiz.CorrectCounter"),
                _correctCount);

        IncorrectScoreLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                Translate(
                    "Quiz.IncorrectCounter"),
                _incorrectCount);
    }

    private static void ApplyNeutralAnswerStyle(
        Button button)
    {
        button.SetDynamicResource(
            Button.BackgroundColorProperty,
            "SurfaceAltColor");

        button.SetDynamicResource(
            Button.BorderColorProperty,
            "BorderColor");

        button.SetDynamicResource(
            Button.TextColorProperty,
            "TextPrimaryColor");
    }

    private static void ApplyCorrectAnswerStyle(
        Button button)
    {
        button.SetDynamicResource(
            Button.BackgroundColorProperty,
            "SuccessSoftColor");

        button.SetDynamicResource(
            Button.BorderColorProperty,
            "SuccessColor");

        button.SetDynamicResource(
            Button.TextColorProperty,
            "SuccessColor");
    }

    private static void ApplyIncorrectAnswerStyle(
        Button button)
    {
        button.SetDynamicResource(
            Button.BackgroundColorProperty,
            "DangerSoftColor");

        button.SetDynamicResource(
            Button.BorderColorProperty,
            "DangerColor");

        button.SetDynamicResource(
            Button.TextColorProperty,
            "DangerColor");
    }

    private static string Translate(
        string key)
    {
        return LocalizationService.TranslateKey(
            key);
    }

    private void BeginMainTabTransitionIfPending()
    {
        if (Shell.Current is not AppShell appShell ||
            !appShell.TryConsumeMainTabTransition(
                "MathPuzzlePage",
                out int direction))
        {
            return;
        }

        int animationVersion =
            ++_mainTabAnimationVersion;

        direction =
            direction >= 0
                ? 1
                : -1;

        MathPuzzlePageContentRoot.CancelAnimations();
        MathPuzzlePageContentRoot.Opacity = 0d;
        MathPuzzlePageContentRoot.TranslationX = direction * 44d;
        MathPuzzlePageContentRoot.Scale = 0.985d;

        Dispatcher.Dispatch(
            async () =>
                await PlayPreparedMainTabTransitionAsync(
                    animationVersion));
    }

    private async Task PlayPreparedMainTabTransitionAsync(
        int animationVersion)
    {
        await Task.Yield();

        if (animationVersion !=
            _mainTabAnimationVersion)
        {
            return;
        }

        try
        {
            await Task.WhenAll(
                MathPuzzlePageContentRoot.FadeToAsync(
                    1d,
                    175,
                    Easing.CubicOut),

                MathPuzzlePageContentRoot.TranslateToAsync(
                    0d,
                    0d,
                    250,
                    Easing.CubicOut),

                MathPuzzlePageContentRoot.ScaleToAsync(
                    1d,
                    250,
                    Easing.CubicOut));
        }
        finally
        {
            if (animationVersion ==
                _mainTabAnimationVersion)
            {
                ResetMainTabRoot();
            }
        }
    }

    private void ResetMainTabRoot()
    {
        MathPuzzlePageContentRoot.Opacity = 1d;
        MathPuzzlePageContentRoot.TranslationX = 0d;
        MathPuzzlePageContentRoot.Scale = 1d;
    }
}
