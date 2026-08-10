using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Localization;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Views;

public partial class MathPuzzlePage : ContentPage
{
    private readonly BasicArithmeticEngine _arithmeticEngine = new();
    private readonly ArithmeticQuizGenerator _quizGenerator;
    private readonly LocalLlmQuizGenerator _localLlmQuizGenerator;
    private readonly QuizLlmModelStore _llmModelStore = new();

    private ArithmeticQuizMode _selectedMode =
        ArithmeticQuizMode.TrueFalse;

    private QuizGenerationSource _generationSource =
        QuizGenerationSource.Algorithm;

    private ArithmeticQuizQuestion? _currentQuestion;
    private CancellationTokenSource? _llmGenerationCancellation;
    private string? _llmModelPath;
    private bool _questionAnswered;
    private bool? _lastAnswerWasCorrect;
    private bool _isGeneratingWithLlm;
    private bool _showFriendlyGreetingForCurrentLoad;
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

        _localLlmQuizGenerator =
            new LocalLlmQuizGenerator(
                _quizGenerator,
                _arithmeticEngine);

        _llmModelPath =
            _llmModelStore.GetSavedModelPath();

        LocalizationService.ExcludeSubtreeFromLegacyTracking(
            this);

        LocalizationService.CultureChanged +=
            OnCultureChanged;

        UpdateOperationPickerItems();
        UpdateGenerationSourceStyles();
        UpdateModeStyles();
        UpdateLlmModelUi();
        UpdateScoreLabels();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        BeginMainTabTransitionIfPending();

        if (_currentQuestion is null &&
            _generationSource == QuizGenerationSource.Algorithm)
        {
            GenerateAlgorithmQuestion();
        }
        else
        {
            if (_currentQuestion is not null)
            {
                RenderCurrentQuestion(
                    resetAnswerControls: false);
            }

            UpdateScoreLabels();
        }
    }

    protected override void OnDisappearing()
    {
        CancelLlmGeneration();

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
                UpdateGenerationSourceStyles();
                UpdateLlmModelUi();
                UpdateScoreLabels();

                if (_generationSource ==
                        QuizGenerationSource.LocalLlm)
                {
                    // Đề AI phụ thuộc chương trình/ngôn ngữ tại thời điểm sinh.
                    PrepareLlmQuestionForGeneration();
                }
                else if (_currentQuestion is not null)
                {
                    RenderCurrentQuestion(
                        resetAnswerControls: false);

                    if (_questionAnswered &&
                        _lastAnswerWasCorrect is bool wasCorrect)
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
        SelectSubTab(showPractice: true);
    }

    private void OnLearnTabClicked(
        object? sender,
        EventArgs e)
    {
        SelectSubTab(showPractice: false);
    }

    private void SelectSubTab(
        bool showPractice)
    {
        PracticeContent.IsVisible = showPractice;
        LearnContent.IsVisible = !showPractice;

        SelectionButtonStyler.Select(
            showPractice
                ? PracticeTabButton
                : LearnTabButton,
            PracticeTabButton,
            LearnTabButton);
    }

    private void OnAlgorithmSourceClicked(
        object? sender,
        EventArgs e)
    {
        SelectGenerationSource(
            QuizGenerationSource.Algorithm);
    }

    private void OnLocalLlmSourceClicked(
        object? sender,
        EventArgs e)
    {
        SelectGenerationSource(
            QuizGenerationSource.LocalLlm);
    }

    private void SelectGenerationSource(
        QuizGenerationSource source)
    {
        if (_generationSource == source)
        {
            return;
        }

        CancelLlmGeneration();
        _generationSource = source;
        UpdateGenerationSourceStyles();

        if (source == QuizGenerationSource.Algorithm)
        {
            GenerateAlgorithmQuestion();
        }
        else
        {
            PrepareLlmQuestionForGeneration();
        }
    }

    private void UpdateGenerationSourceStyles()
    {
        SelectionButtonStyler.Select(
            _generationSource == QuizGenerationSource.Algorithm
                ? AlgorithmSourceButton
                : LocalLlmSourceButton,
            AlgorithmSourceButton,
            LocalLlmSourceButton);

        LlmSettingsBorder.IsVisible =
            _generationSource == QuizGenerationSource.LocalLlm;
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

        _selectedMode = mode;
        UpdateModeStyles();

        if (_generationSource == QuizGenerationSource.Algorithm)
        {
            GenerateAlgorithmQuestion();
        }
        else
        {
            PrepareLlmQuestionForGeneration();
        }
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
            _currentQuestion?.WordProblem is not null
                ? Translate("Quiz.WordProblemTitle")
                : Translate(
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

        _isUpdatingOperationPicker = true;

        try
        {
            OperationPicker.Items.Clear();
            OperationPicker.Items.Add(
                Translate("Quiz.OperationMixed"));
            OperationPicker.Items.Add(
                Translate("Quiz.OperationAddition"));
            OperationPicker.Items.Add(
                Translate("Quiz.OperationSubtraction"));
            OperationPicker.Items.Add(
                Translate("Quiz.OperationMultiplication"));
            OperationPicker.Items.Add(
                Translate("Quiz.OperationDivision"));

            OperationPicker.SelectedIndex =
                Math.Clamp(
                    selectedIndex,
                    0,
                    OperationPicker.Items.Count - 1);
        }
        finally
        {
            _isUpdatingOperationPicker = false;
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

        if (_generationSource == QuizGenerationSource.Algorithm)
        {
            GenerateAlgorithmQuestion();
        }
        else
        {
            PrepareLlmQuestionForGeneration();
        }
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

    private void GenerateAlgorithmQuestion()
    {
        CancelLlmGeneration();
        _questionAnswered = false;
        _lastAnswerWasCorrect = null;
        NextQuestionButton.IsEnabled = false;

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
            _currentQuestion = null;
            QuestionExpressionLabel.Text =
                Translate("Quiz.GenerationError");
        }
    }

    private void PrepareLlmQuestionForGeneration(
        bool cancelPending = true)
    {
        if (cancelPending)
        {
            CancelLlmGeneration();
        }

        _currentQuestion = null;
        _questionAnswered = false;
        _lastAnswerWasCorrect = null;

        QuestionPromptLabel.Text =
            Translate("Quiz.WordProblemTitle");

        QuestionExpressionLabel.FontSize = 20;
        QuestionExpressionLabel.SetDynamicResource(
            Label.TextColorProperty,
            "TextSecondaryColor");

        QuestionExpressionLabel.Text =
            _llmModelPath is null
                ? Translate("Quiz.SelectModelFirst")
                : Translate("Quiz.LlmReady");

        PresentedAnswerLabel.IsVisible = false;
        FeedbackBorder.IsVisible = false;
        SolutionBorder.IsVisible = false;
        NextQuestionButton.IsEnabled = false;

        SetAnswerControlsEnabled(false);
        UpdateModeStyles();
        UpdateLlmModelUi();
        ShowLlmStatus(
            _llmModelPath is null
                ? Translate("Quiz.SelectModelFirst")
                : Translate("Quiz.LlmReady"),
            isRunning: false);
    }

    private async void OnSelectLlmModelClicked(
        object? sender,
        EventArgs e)
    {
        if (_isGeneratingWithLlm)
        {
            return;
        }

        FileResult? fileResult =
            await FilePicker.Default.PickAsync(
                new PickOptions
                {
                    PickerTitle =
                        Translate("Quiz.SelectModelPickerTitle")
                });

        if (fileResult is null)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _llmGenerationCancellation = cancellation;
        SetLlmBusy(true);
        ShowLlmStatus(
            Translate("Quiz.ImportingModel"),
            isRunning: true);

        try
        {
            _llmModelPath =
                await _llmModelStore.ImportAsync(
                    fileResult,
                    cancellation.Token);

            UpdateLlmModelUi();
            PrepareLlmQuestionForGeneration(
                cancelPending: false);
            ShowLlmStatus(
                Translate("Quiz.ModelReady"),
                isRunning: false);
        }
        catch (OperationCanceledException)
        {
            ShowLlmStatus(
                Translate("Quiz.GenerationCancelled"),
                isRunning: false);
        }
        catch (InvalidDataException)
        {
            ShowLlmStatus(
                Translate("Quiz.InvalidModelFile"),
                isRunning: false);
        }
        catch (Exception)
        {
            ShowLlmStatus(
                Translate("Quiz.ModelImportError"),
                isRunning: false);
        }
        finally
        {
            if (ReferenceEquals(
                    _llmGenerationCancellation,
                    cancellation))
            {
                _llmGenerationCancellation = null;
                SetLlmBusy(false);
            }

            cancellation.Dispose();
        }
    }

    private async void OnCreateLlmQuestionClicked(
        object? sender,
        EventArgs e)
    {
        await GenerateLlmQuestionAsync();
    }

    private async Task GenerateLlmQuestionAsync()
    {
        if (_isGeneratingWithLlm)
        {
            return;
        }

        if (_llmModelPath is null ||
            !File.Exists(_llmModelPath))
        {
            _llmModelPath = null;
            UpdateLlmModelUi();
            PrepareLlmQuestionForGeneration();
            return;
        }

        CancelLlmGeneration();

        var cancellation = new CancellationTokenSource();
        _llmGenerationCancellation = cancellation;
        _showFriendlyGreetingForCurrentLoad =
            _llmModelStore.ShouldShowFirstGreeting();

        _currentQuestion = null;
        _questionAnswered = false;
        _lastAnswerWasCorrect = null;
        FeedbackBorder.IsVisible = false;
        SolutionBorder.IsVisible = false;
        PresentedAnswerLabel.IsVisible = false;
        NextQuestionButton.IsEnabled = false;
        SetAnswerControlsEnabled(false);
        SetLlmBusy(true);

        QuestionPromptLabel.Text =
            Translate("Quiz.WordProblemTitle");
        QuestionExpressionLabel.FontSize = 21;
        QuestionExpressionLabel.SetDynamicResource(
            Label.TextColorProperty,
            "TextPrimaryColor");
        QuestionExpressionLabel.Text =
            Translate("Quiz.LoadingModel");

        ShowLlmStatus(
            _showFriendlyGreetingForCurrentLoad
                ? Translate("Quiz.FirstModelGreeting")
                : Translate("Quiz.LoadingModel"),
            isRunning: true);

        try
        {
            var progress =
                new Progress<LlmQuizProgress>(
                    UpdateLlmProgress);

            LlmQuizGenerationResult result =
                await _localLlmQuizGenerator.GenerateAsync(
                    _llmModelPath,
                    _selectedMode,
                    GetSelectedOperation(),
                    AppLanguageManager.CurrentLanguage,
                    progress,
                    cancellation.Token);

            if (result.ModelWasLoaded &&
                _showFriendlyGreetingForCurrentLoad)
            {
                _llmModelStore.MarkFirstGreetingShown();
            }

            if (result.Question is not null)
            {
                _currentQuestion = result.Question;
                _questionCount++;
                RenderCurrentQuestion(
                    resetAnswerControls: true);
                UpdateScoreLabels();
                ShowLlmStatus(
                    Translate("Quiz.GenerationSucceeded"),
                    isRunning: false);
            }
            else
            {
                ShowLlmGenerationFailure(result);
            }
        }
        catch (OperationCanceledException)
        {
            ShowLlmStatus(
                Translate("Quiz.GenerationCancelled"),
                isRunning: false);
        }
        finally
        {
            if (ReferenceEquals(
                    _llmGenerationCancellation,
                    cancellation))
            {
                _llmGenerationCancellation = null;
                SetLlmBusy(false);
            }

            cancellation.Dispose();
        }
    }

    private void UpdateLlmProgress(
        LlmQuizProgress progress)
    {
        if (_isGeneratingWithLlm &&
            !string.IsNullOrWhiteSpace(
                progress.ProblemPreview))
        {
            ShowGeneratedProblemPreview(
                progress.ProblemPreview);
        }

        string status =
            progress.Stage switch
            {
                LlmQuizProgressStage.LoadingModel
                    when _showFriendlyGreetingForCurrentLoad =>
                    Translate("Quiz.FirstModelGreeting"),
                LlmQuizProgressStage.LoadingModel =>
                    Translate("Quiz.LoadingModel"),
                LlmQuizProgressStage.ModelLoaded =>
                    Translate("Quiz.ModelLoaded"),
                LlmQuizProgressStage.Generating =>
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Translate("Quiz.GeneratingAttempt"),
                        progress.Attempt,
                        progress.MaximumAttempts),
                LlmQuizProgressStage.Validating =>
                    Translate("Quiz.ValidatingProblem"),
                LlmQuizProgressStage.Retrying =>
                    Translate("Quiz.RetryingProblem"),
                LlmQuizProgressStage.DisposingModel =>
                    Translate("Quiz.DisposingModel"),
                _ => Translate("Quiz.LoadingModel")
            };

        ShowLlmStatus(
            status,
            isRunning: progress.Stage !=
                LlmQuizProgressStage.DisposingModel);
    }

    private void ShowGeneratedProblemPreview(
        string problemText)
    {
        QuestionPromptLabel.Text =
            Translate("Quiz.WordProblemTitle");

        QuestionExpressionLabel.FontSize = 21;
        QuestionExpressionLabel.SetDynamicResource(
            Label.TextColorProperty,
            "TextPrimaryColor");
        QuestionExpressionLabel.Text = problemText;

        PresentedAnswerLabel.IsVisible = false;
        FeedbackBorder.IsVisible = false;
        SolutionBorder.IsVisible = false;
        SetAnswerControlsEnabled(false);
    }

    private void ShowLlmGenerationFailure(
        LlmQuizGenerationResult result)
    {
        string key =
            result.ErrorCode switch
            {
                "ModelFileNotFound" => "Quiz.SelectModelFirst",
                "NotEnoughMemory" => "Quiz.NotEnoughMemory",
                "ModelRuntimeError" => "Quiz.ModelRuntimeError",
                _ when result.Attempts >=
                    LocalLlmQuizGenerator.MaximumAttempts =>
                    "Quiz.GenerationFailedAfterRetries",
                _ => "Quiz.GenerationError"
            };

        string message = Translate(key);

        QuestionExpressionLabel.FontSize = 20;
        QuestionExpressionLabel.SetDynamicResource(
            Label.TextColorProperty,
            "DangerColor");
        QuestionExpressionLabel.Text = message;
        ShowLlmStatus(message, isRunning: false);
    }

    private void UpdateLlmModelUi()
    {
        if (_llmModelPath is null ||
            !File.Exists(_llmModelPath))
        {
            _llmModelPath = null;
            LlmModelNameLabel.Text =
                Translate("Quiz.NoModelSelected");
            LlmModelRecommendationLabel.Text =
                Translate("Quiz.ModelRecommendation");
        }
        else
        {
            LlmModelNameLabel.Text =
                Path.GetFileName(_llmModelPath);

            LlmModelRecommendationLabel.Text =
                QuizLlmModelStore.IsRecommendedQuantization(
                    _llmModelPath)
                    ? Translate("Quiz.RecommendedModelDetected")
                    : Translate("Quiz.ModelRecommendation");
        }

        CreateLlmQuestionButton.IsEnabled =
            !_isGeneratingWithLlm &&
            _llmModelPath is not null;
    }

    private void SetLlmBusy(
        bool isBusy)
    {
        _isGeneratingWithLlm = isBusy;
        SelectLlmModelButton.IsEnabled = !isBusy;
        AlgorithmSourceButton.IsEnabled = !isBusy;
        LocalLlmSourceButton.IsEnabled = !isBusy;
        TrueFalseModeButton.IsEnabled = !isBusy;
        MultipleChoiceModeButton.IsEnabled = !isBusy;
        OperationPicker.IsEnabled = !isBusy;

        CreateLlmQuestionButton.IsEnabled =
            !isBusy &&
            _llmModelPath is not null;
    }

    private void ShowLlmStatus(
        string message,
        bool isRunning)
    {
        LlmProgressGrid.IsVisible = true;
        LlmActivityIndicator.IsRunning = isRunning;
        LlmActivityIndicator.IsVisible = isRunning;
        LlmStatusLabel.Text = message;
    }

    private void CancelLlmGeneration()
    {
        _llmGenerationCancellation?.Cancel();
    }

    private void SetAnswerControlsEnabled(
        bool isEnabled)
    {
        TrueAnswerButton.IsEnabled = isEnabled;
        FalseAnswerButton.IsEnabled = isEnabled;

        foreach (Button button in ChoiceButtons)
        {
            button.IsEnabled = isEnabled;
        }
    }

    private void RenderCurrentQuestion(
        bool resetAnswerControls)
    {
        if (_currentQuestion is null)
        {
            return;
        }

        UpdateModeStyles();

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

        MathWordProblem? wordProblem =
            _currentQuestion.WordProblem;

        if (wordProblem is not null)
        {
            QuestionPromptLabel.Text =
                Translate("Quiz.WordProblemTitle");
            QuestionExpressionLabel.Text =
                wordProblem.ProblemText;
            QuestionExpressionLabel.FontSize = 21;
            QuestionExpressionLabel.SetDynamicResource(
                Label.TextColorProperty,
                "TextPrimaryColor");

            if (_currentQuestion.Mode ==
                ArithmeticQuizMode.TrueFalse)
            {
                string presentedAnswer =
                    _currentQuestion.PresentedAnswer
                        .GetValueOrDefault()
                        .ToString(
                            "N0",
                            CultureInfo.CurrentCulture);

                PresentedAnswerLabel.Text =
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Translate("Quiz.PresentedAnswer"),
                        presentedAnswer,
                        wordProblem.AnswerUnit);
                PresentedAnswerLabel.IsVisible = true;
            }
            else
            {
                PresentedAnswerLabel.IsVisible = false;
            }
        }
        else
        {
            PresentedAnswerLabel.IsVisible = false;
            QuestionExpressionLabel.FontSize = 34;
            QuestionExpressionLabel.SetDynamicResource(
                Label.TextColorProperty,
                "PrimaryColor");

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
            }
        }

        if (_currentQuestion.Mode ==
            ArithmeticQuizMode.MultipleChoice)
        {
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
        SetAnswerControlsEnabled(true);

        foreach (Button button in ChoiceButtons)
        {
            ApplyNeutralAnswerStyle(button);
        }

        FeedbackBorder.IsVisible = false;
        SolutionBorder.IsVisible = false;
        NextQuestionButton.IsEnabled = false;
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

        _questionAnswered = true;
        _lastAnswerWasCorrect = isCorrect;

        if (isCorrect)
        {
            _correctCount++;
        }
        else
        {
            _incorrectCount++;
        }

        SetAnswerControlsEnabled(false);

        foreach (Button button in ChoiceButtons)
        {
            if (_currentQuestion.Mode ==
                    ArithmeticQuizMode.MultipleChoice &&
                BigInteger.TryParse(
                    button.CommandParameter?.ToString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out BigInteger answer) &&
                answer == _currentQuestion.CorrectAnswer)
            {
                ApplyCorrectAnswerStyle(button);
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

        if (_currentQuestion.WordProblem is not null)
        {
            SolutionLabel.Text =
                ElementaryWordProblemSolutionFormatter.Format(
                    _currentQuestion,
                    AppLanguageManager.CurrentLanguage,
                    CultureInfo.CurrentCulture);
            SolutionBorder.IsVisible = true;
        }

        NextQuestionButton.IsEnabled = true;
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

        if (_currentQuestion?.WordProblem is
            MathWordProblem wordProblem)
        {
            answerText +=
                $" {wordProblem.AnswerUnit}";
        }

        FeedbackLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                Translate(
                    isCorrect
                        ? "Quiz.CorrectFeedback"
                        : "Quiz.IncorrectFeedback"),
                answerText);

        FeedbackBorder.IsVisible = true;

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

    private async void OnNextQuestionClicked(
        object? sender,
        EventArgs e)
    {
        if (!_questionAnswered)
        {
            return;
        }

        if (_generationSource == QuizGenerationSource.Algorithm)
        {
            GenerateAlgorithmQuestion();
        }
        else
        {
            await GenerateLlmQuestionAsync();
        }
    }

    private void OnResetScoreClicked(
        object? sender,
        EventArgs e)
    {
        _correctCount = 0;
        _incorrectCount = 0;
        UpdateScoreLabels();
    }

    private void UpdateScoreLabels()
    {
        QuestionCounterLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                Translate("Quiz.QuestionCounter"),
                _questionCount);

        CorrectScoreLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                Translate("Quiz.CorrectCounter"),
                _correctCount);

        IncorrectScoreLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                Translate("Quiz.IncorrectCounter"),
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
        return LocalizationService.TranslateKey(key);
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
        MathPuzzlePageContentRoot.TranslationX =
            direction * 44d;
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
