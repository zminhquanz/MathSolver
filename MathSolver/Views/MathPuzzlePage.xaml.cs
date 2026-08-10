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
    private readonly Gemma4ModelDownloadService
        _gemma4ModelDownloadService = new();
    private readonly ModelFileLocationService
        _modelFileLocationService = new();

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
    private bool _isDownloadingModel;
    private bool _showFriendlyGreetingForCurrentLoad;
    private bool _isUpdatingOperationPicker;
    private int _llmProgressVersion;
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

        // Nếu quay lại trong grace period thì giữ nguyên GGUF weights đang
        // nằm trong RAM; câu kế tiếp chỉ cần tạo context/KV mới.
        _localLlmQuizGenerator.CancelScheduledModelUnload();

        BeginMainTabTransitionIfPending();

        if (_currentQuestion is null)
        {
            if (_generationSource == QuizGenerationSource.Algorithm)
            {
                GenerateAlgorithmQuestion();
            }
            else
            {
                PrepareLlmQuestionForGeneration(
                    cancelPending: false);
            }
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
        // SettingsMenuPage chỉ là một modal trong suốt phủ lên trang hiện tại.
        // Constructor của nó bật cờ này trước khi PushModalAsync làm
        // MathPuzzlePage nhận OnDisappearing, vì vậy không được coi đây là
        // thao tác rời tab lớn: giữ nguyên câu hỏi, lựa chọn và điểm số.
        if (SettingsMenuPage.IsTransparentOverlayActive)
        {
            base.OnDisappearing();
            return;
        }

        bool wasGeneratingWithLlm =
            _isGeneratingWithLlm;

        CancelLlmGeneration();

        if (wasGeneratingWithLlm)
        {
            ShowLlmStatus(
                Translate("Quiz.GenerationCancelled"),
                isRunning: false);
        }

        // Không unload weights ngay khi đổi tab. Context/KV của lượt sinh
        // hiện tại sẽ được hủy khi cancellation hoàn tất; weights chỉ được
        // giải phóng nếu người dùng không quay lại sau 60 giây.
        _localLlmQuizGenerator.ScheduleModelUnload(
            ClearLlmQuestionAfterDelayedUnloadAsync);

        // Mỗi lần rời tab lớn Toán đố là kết thúc toàn bộ phiên luyện tập.
        // Model đã chọn và weights cache vẫn tuân theo grace period 60 giây;
        // chỉ câu hỏi, đáp án, phản hồi và điểm số được đưa về trạng thái đầu.
        ResetQuizSessionState();

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
                // Đổi ngôn ngữ bắt đầu một phiên luyện tập mới để câu hỏi,
                // đáp án và toàn bộ nhãn điểm không bị trộn hai ngôn ngữ.
                CancelLlmGeneration();
                ResetQuizSessionState();

                UpdateOperationPickerItems();
                UpdateGenerationSourceStyles();
                UpdateLlmModelUi();
                UpdateScoreLabels();

                if (_generationSource ==
                        QuizGenerationSource.LocalLlm)
                {
                    // Đề AI phụ thuộc chương trình/ngôn ngữ tại thời điểm sinh.
                    PrepareLlmQuestionForGeneration(
                        cancelPending: false);
                }
            });
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
        ResetQuizSessionState();
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

        bool hasQuestion = _currentQuestion is not null;

        TrueFalseAnswerGrid.IsVisible =
            hasQuestion &&
            _selectedMode == ArithmeticQuizMode.TrueFalse;

        MultipleChoiceAnswerGrid.IsVisible =
            hasQuestion &&
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

    private void GenerateAlgorithmQuestion(
        bool advanceQuestionNumber = false)
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

            AdvanceQuestionNumberIfNeeded(
                advanceQuestionNumber);
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

        ClearMultipleChoiceAnswers();
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
        int progressVersion =
            BeginLlmProgress(cancellation);
        SetLlmBusy(true);
        ShowLlmStatus(
            Translate("Quiz.ImportingModel"),
            isRunning: true);

        try
        {
            string? previousModelPath =
                _llmModelPath;

            string selectedModelPath =
                await _llmModelStore.ImportAsync(
                    fileResult,
                    cancellation.Token);

            // Chọn file chỉ kiểm tra/lưu đường dẫn. Nếu người dùng đổi sang
            // file khác thì giải phóng cache cũ; weights mới chỉ được nạp khi
            // bấm Tạo đề bằng AI.
            if (!string.Equals(
                    previousModelPath,
                    selectedModelPath,
                    OperatingSystem.IsWindows()
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal))
            {
                await _localLlmQuizGenerator.UnloadModelAsync(
                    cancellation.Token);
            }

            _llmModelPath = selectedModelPath;
            _llmModelStore.SaveModelPath(
                selectedModelPath);
            UpdateLlmModelUi();

            CompleteLlmProgress(progressVersion);
            PrepareLlmQuestionForGeneration(
                cancelPending: false);
            ShowLlmStatus(
                Translate("Quiz.ModelReady"),
                isRunning: false);
        }
        catch (OperationCanceledException)
        {
            CompleteLlmProgress(progressVersion);
            ShowLlmStatus(
                Translate("Quiz.GenerationCancelled"),
                isRunning: false);
        }
        catch (QuizLlmModelTooLargeException)
        {
            CompleteLlmProgress(progressVersion);
            ShowLlmStatus(
                Translate("Quiz.ModelTooLarge"),
                isRunning: false);
        }
        catch (UnsupportedQuizLlmModelException)
        {
            CompleteLlmProgress(progressVersion);
            ShowLlmStatus(
                Translate("Quiz.UnsupportedModelFamily"),
                isRunning: false);
        }
        catch (InvalidDataException)
        {
            CompleteLlmProgress(progressVersion);
            ShowLlmStatus(
                Translate("Quiz.InvalidModelFile"),
                isRunning: false);
        }
        catch (Exception exception)
        {
            CompleteLlmProgress(progressVersion);
            System.Diagnostics.Debug.WriteLine(
                $"Local LLM file selection failed: {exception}");

            ShowLlmStatus(
                Translate("Quiz.ModelImportError"),
                isRunning: false);
        }
        finally
        {
            CompleteLlmProgress(progressVersion);

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

    private async void OnDownloadGemma4Clicked(
        object? sender,
        EventArgs e)
    {
        if (_isDownloadingModel)
        {
            CancelLlmGeneration();
            return;
        }

        if (_isGeneratingWithLlm)
        {
            return;
        }

        string e2BOption =
            Translate("Quiz.DownloadE2BOption");
        string e4BOption =
            Translate("Quiz.DownloadE4BOption");

        string? selection =
            await DisplayActionSheetAsync(
                Translate("Quiz.ChooseDownloadModelTitle"),
                Translate("Quiz.Cancel"),
                null,
                e2BOption,
                e4BOption);

        Gemma4ModelDescriptor? model =
            selection == e2BOption
                ? Gemma4ModelDownloadService.E2B
                : selection == e4BOption
                    ? Gemma4ModelDownloadService.E4B
                    : null;

        if (model is null)
        {
            return;
        }

        bool confirmed =
            await DisplayAlertAsync(
                Translate("Quiz.DownloadConfirmTitle"),
                string.Format(
                    CultureInfo.CurrentCulture,
                    Translate("Quiz.DownloadConfirmMessage"),
                    model.DisplayName,
                    FormatDownloadGigabytes(
                        model.ApproximateSizeBytes)),
                Translate("Quiz.DownloadAction"),
                Translate("Quiz.Cancel"));

        if (!confirmed)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        int progressVersion =
            BeginLlmProgress(cancellation);

        _isDownloadingModel = true;
        LlmDownloadProgressBar.Progress = 0;
        LlmDownloadProgressBar.IsVisible = true;
        SetLlmBusy(true);
        ShowLlmStatus(
            string.Format(
                CultureInfo.CurrentCulture,
                Translate("Quiz.DownloadingModel"),
                model.DisplayName),
            isRunning: false);

        try
        {
            var progress =
                new Progress<Gemma4ModelDownloadProgress>(
                    value =>
                    {
                        if (progressVersion !=
                                Volatile.Read(ref _llmProgressVersion) ||
                            cancellation.IsCancellationRequested ||
                            !ReferenceEquals(
                                _llmGenerationCancellation,
                                cancellation))
                        {
                            return;
                        }

                        UpdateGemma4DownloadProgress(
                            model,
                            value);
                    });

            string? previousModelPath =
                _llmModelPath;

            string downloadedModelPath =
                await _gemma4ModelDownloadService.DownloadAsync(
                    model,
                    progress,
                    cancellation.Token);

            if (!string.Equals(
                    previousModelPath,
                    downloadedModelPath,
                    OperatingSystem.IsWindows()
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal))
            {
                await _localLlmQuizGenerator.UnloadModelAsync(
                    cancellation.Token);
            }

            cancellation.Token.ThrowIfCancellationRequested();

            _llmModelPath = downloadedModelPath;
            _llmModelStore.SaveModelPath(
                downloadedModelPath);

            CompleteLlmProgress(progressVersion);
            PrepareLlmQuestionForGeneration(
                cancelPending: false);
            ShowLlmStatus(
                string.Format(
                    CultureInfo.CurrentCulture,
                    Translate("Quiz.DownloadComplete"),
                    model.DisplayName),
                isRunning: false);
        }
        catch (OperationCanceledException)
        {
            CompleteLlmProgress(progressVersion);
            ShowLlmStatus(
                Translate("Quiz.DownloadCancelled"),
                isRunning: false);
        }
        catch (QuizLlmModelTooLargeException)
        {
            CompleteLlmProgress(progressVersion);
            ShowLlmStatus(
                Translate("Quiz.ModelTooLarge"),
                isRunning: false);
        }
        catch (InvalidDataException)
        {
            CompleteLlmProgress(progressVersion);
            ShowLlmStatus(
                Translate("Quiz.DownloadInvalid"),
                isRunning: false);
        }
        catch (HttpRequestException exception)
        {
            CompleteLlmProgress(progressVersion);
            System.Diagnostics.Debug.WriteLine(
                $"Gemma 4 download failed: {exception}");

            string key =
                exception.StatusCode is
                    System.Net.HttpStatusCode.Unauthorized or
                    System.Net.HttpStatusCode.Forbidden
                    ? "Quiz.DownloadAccessDenied"
                    : "Quiz.DownloadFailed";

            ShowLlmStatus(
                Translate(key),
                isRunning: false);
        }
        catch (Exception exception)
        {
            CompleteLlmProgress(progressVersion);
            System.Diagnostics.Debug.WriteLine(
                $"Gemma 4 download failed: {exception}");

            ShowLlmStatus(
                Translate("Quiz.DownloadFailed"),
                isRunning: false);
        }
        finally
        {
            CompleteLlmProgress(progressVersion);
            _isDownloadingModel = false;
            LlmDownloadProgressBar.IsVisible = false;
            LlmDownloadProgressBar.Progress = 0;

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

    private void UpdateGemma4DownloadProgress(
        Gemma4ModelDescriptor model,
        Gemma4ModelDownloadProgress progress)
    {
        long totalBytes =
            progress.TotalBytes is > 0
                ? progress.TotalBytes.Value
                : model.ApproximateSizeBytes;

        double fraction =
            totalBytes > 0
                ? Math.Clamp(
                    (double)progress.BytesReceived / totalBytes,
                    0d,
                    1d)
                : 0d;

        int percentage =
            (int)Math.Round(
                fraction * 100d,
                MidpointRounding.AwayFromZero);

        LlmDownloadProgressBar.Progress = fraction;
        ShowLlmStatus(
            string.Format(
                CultureInfo.CurrentCulture,
                Translate("Quiz.DownloadingModelProgress"),
                model.DisplayName,
                percentage,
                FormatDownloadGigabytes(
                    progress.BytesReceived),
                FormatDownloadGigabytes(totalBytes)),
            isRunning: false);
    }

    private static string FormatDownloadGigabytes(
        long bytes)
    {
        return (bytes / 1_000_000_000d).ToString(
            "0.00",
            CultureInfo.CurrentCulture);
    }

    private async void OnEjectLlmModelClicked(
        object? sender,
        EventArgs e)
    {
        if (_isGeneratingWithLlm ||
            _llmModelPath is null)
        {
            return;
        }

        CancelLlmGeneration();

        var cancellation = new CancellationTokenSource();
        int progressVersion =
            BeginLlmProgress(cancellation);

        SetLlmBusy(true);
        ShowLlmStatus(
            Translate("Quiz.DisposingModel"),
            isRunning: true);

        try
        {
            await _localLlmQuizGenerator.UnloadModelAsync(
                cancellation.Token);

            cancellation.Token.ThrowIfCancellationRequested();

            _llmModelStore.ClearSavedModelPath();
            _llmModelPath = null;
            ResetQuizSessionCounters();

            CompleteLlmProgress(progressVersion);
            PrepareLlmQuestionForGeneration(
                cancelPending: false);
            ShowLlmStatus(
                Translate("Quiz.ModelEjected"),
                isRunning: false);
        }
        catch (OperationCanceledException)
        {
            CompleteLlmProgress(progressVersion);
            ShowLlmStatus(
                Translate("Quiz.GenerationCancelled"),
                isRunning: false);
        }
        catch (Exception exception)
        {
            CompleteLlmProgress(progressVersion);
            System.Diagnostics.Debug.WriteLine(
                $"Local LLM ejection failed: {exception}");

            ShowLlmStatus(
                Translate("Quiz.ModelRuntimeError"),
                isRunning: false);
        }
        finally
        {
            CompleteLlmProgress(progressVersion);

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

    private async void OnOpenLlmModelFolderClicked(
        object? sender,
        EventArgs e)
    {
        if (_isGeneratingWithLlm ||
            !QuizLlmModelStore.IsSupportedModelPath(
                _llmModelPath))
        {
            UpdateLlmModelUi();
            return;
        }

        string modelPath = _llmModelPath!;
        bool opened =
            await _modelFileLocationService
                .TryOpenContainingFolderAsync(modelPath);

        if (opened)
        {
            return;
        }

        await DisplayAlertAsync(
            Translate("Quiz.ModelLocationTitle"),
            string.Format(
                CultureInfo.CurrentCulture,
                Translate("Quiz.ModelLocationUnavailable"),
                modelPath),
            Translate("Common.OK"));
    }

    private async void OnCreateLlmQuestionClicked(
        object? sender,
        EventArgs e)
    {
        // Nút này vừa tạo câu đầu tiên vừa cho phép bỏ qua/tạo lại câu hiện
        // tại. Tạo lại không tăng số câu và không tính là đúng hoặc sai.
        await GenerateLlmQuestionAsync(
            advanceQuestionNumber: false);
    }

    private async Task GenerateLlmQuestionAsync(
        bool advanceQuestionNumber)
    {
        if (_isGeneratingWithLlm ||
            (advanceQuestionNumber &&
             (!_questionAnswered ||
              _currentQuestion is null)))
        {
            return;
        }

        if (!QuizLlmModelStore.IsSupportedModelPath(
                _llmModelPath))
        {
            _llmModelStore.ClearSavedModelPath();
            _llmModelPath = null;
            UpdateLlmModelUi();
            PrepareLlmQuestionForGeneration();
            return;
        }

        CancelLlmGeneration();

        var cancellation = new CancellationTokenSource();
        int progressVersion =
            BeginLlmProgress(cancellation);
        _showFriendlyGreetingForCurrentLoad =
            _llmModelStore.ShouldShowFirstGreeting();

        _currentQuestion = null;
        _questionAnswered = false;
        _lastAnswerWasCorrect = null;
        FeedbackBorder.IsVisible = false;
        SolutionBorder.IsVisible = false;
        PresentedAnswerLabel.IsVisible = false;
        NextQuestionButton.IsEnabled = false;
        ClearMultipleChoiceAnswers();
        UpdateModeStyles();
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
                CreateLlmProgress(
                    cancellation,
                    progressVersion);

            LlmQuizGenerationResult result =
                await _localLlmQuizGenerator.GenerateAsync(
                    _llmModelPath,
                    _selectedMode,
                    GetSelectedOperation(),
                    AppLanguageManager.CurrentLanguage,
                    progress,
                    cancellation.Token);

            // Vô hiệu hóa callback Progress<T> đang chờ trên UI thread trước
            // khi hiển thị trạng thái cuối. Nếu không, ModelLoaded/Validating
            // đến muộn có thể bật spinner trở lại sau khi tác vụ đã hoàn tất.
            CompleteLlmProgress(progressVersion);

            if (result.ModelWasLoaded &&
                _showFriendlyGreetingForCurrentLoad)
            {
                _llmModelStore.MarkFirstGreetingShown();
            }

            if (result.Question is not null)
            {
                _currentQuestion = result.Question;
                AdvanceQuestionNumberIfNeeded(
                    advanceQuestionNumber);
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
            CompleteLlmProgress(progressVersion);
            ShowLlmStatus(
                Translate("Quiz.GenerationCancelled"),
                isRunning: false);
        }
        finally
        {
            CompleteLlmProgress(progressVersion);

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
        if (!QuizLlmModelStore.IsSupportedModelPath(
                _llmModelPath))
        {
            _llmModelStore.ClearSavedModelPath();
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

        EjectLlmModelButton.IsEnabled =
            !_isGeneratingWithLlm &&
            _llmModelPath is not null;

        OpenLlmModelFolderButton.IsEnabled =
            !_isGeneratingWithLlm &&
            _llmModelPath is not null;

        DownloadGemma4Button.IsEnabled =
            !_isGeneratingWithLlm ||
            _isDownloadingModel;

        DownloadGemma4Button.Text =
            Translate(
                _isDownloadingModel
                    ? "Quiz.StopModelDownload"
                    : "Quiz.DownloadGemma4");
    }

    private void SetLlmBusy(
        bool isBusy)
    {
        _isGeneratingWithLlm = isBusy;
        SelectLlmModelButton.IsEnabled = !isBusy;
        DownloadGemma4Button.IsEnabled =
            !isBusy ||
            _isDownloadingModel;
        DownloadGemma4Button.Text =
            Translate(
                _isDownloadingModel
                    ? "Quiz.StopModelDownload"
                    : "Quiz.DownloadGemma4");
        EjectLlmModelButton.IsEnabled =
            !isBusy &&
            _llmModelPath is not null;
        OpenLlmModelFolderButton.IsEnabled =
            !isBusy &&
            _llmModelPath is not null;
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

    private int BeginLlmProgress(
        CancellationTokenSource cancellation)
    {
        _llmGenerationCancellation = cancellation;

        return Interlocked.Increment(
            ref _llmProgressVersion);
    }

    private IProgress<LlmQuizProgress> CreateLlmProgress(
        CancellationTokenSource cancellation,
        int progressVersion)
    {
        return new Progress<LlmQuizProgress>(
            progress =>
            {
                if (progressVersion !=
                        Volatile.Read(ref _llmProgressVersion) ||
                    cancellation.IsCancellationRequested ||
                    !ReferenceEquals(
                        _llmGenerationCancellation,
                        cancellation))
                {
                    return;
                }

                UpdateLlmProgress(progress);
            });
    }

    private void CompleteLlmProgress(
        int progressVersion)
    {
        Interlocked.CompareExchange(
            ref _llmProgressVersion,
            progressVersion + 1,
            progressVersion);
    }

    private void CancelLlmGeneration()
    {
        Interlocked.Increment(
            ref _llmProgressVersion);

        _llmGenerationCancellation?.Cancel();
        LlmActivityIndicator.IsRunning = false;
        LlmActivityIndicator.IsVisible = false;
    }

    private Task ClearLlmQuestionAfterDelayedUnloadAsync()
    {
        return Microsoft.Maui.ApplicationModel.MainThread
            .InvokeOnMainThreadAsync(
                () =>
                {
                    if (_generationSource !=
                            QuizGenerationSource.LocalLlm ||
                        _isGeneratingWithLlm)
                    {
                        return;
                    }

                    PrepareLlmQuestionForGeneration(
                        cancelPending: false);
                });
    }

    private void ClearMultipleChoiceAnswers()
    {
        foreach (Button button in ChoiceButtons)
        {
            button.Text = string.Empty;
            button.CommandParameter = null;
            button.IsEnabled = false;
            ApplyNeutralAnswerStyle(button);
        }
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
            GenerateAlgorithmQuestion(
                advanceQuestionNumber: true);
        }
        else
        {
            await GenerateLlmQuestionAsync(
                advanceQuestionNumber: true);
        }
    }

    private void AdvanceQuestionNumberIfNeeded(
        bool advanceQuestionNumber)
    {
        // Câu đầu tiên bắt đầu ở 1. Sau đó chỉ hành động Câu tiếp theo hợp lệ
        // mới được tăng số thứ tự; đổi cấu hình hoặc tạo lại không được nhảy câu.
        if (_questionCount == 0 ||
            advanceQuestionNumber)
        {
            _questionCount++;
        }
    }

    private void ResetQuizSessionCounters()
    {
        _questionCount = 0;
        _correctCount = 0;
        _incorrectCount = 0;
        UpdateScoreLabels();
    }

    private void ResetCurrentQuestionState()
    {
        _currentQuestion = null;
        _questionAnswered = false;
        _lastAnswerWasCorrect = null;

        QuestionExpressionLabel.Text = string.Empty;
        PresentedAnswerLabel.Text = string.Empty;
        PresentedAnswerLabel.IsVisible = false;
        FeedbackLabel.Text = string.Empty;
        FeedbackBorder.IsVisible = false;
        SolutionLabel.Text = string.Empty;
        SolutionBorder.IsVisible = false;
        NextQuestionButton.IsEnabled = false;

        ClearMultipleChoiceAnswers();
        SetAnswerControlsEnabled(false);
        UpdateModeStyles();

        LlmActivityIndicator.IsRunning = false;
        LlmActivityIndicator.IsVisible = false;
        LlmStatusLabel.Text = string.Empty;
        LlmProgressGrid.IsVisible = false;
    }

    private void ResetQuizSessionState()
    {
        ResetCurrentQuestionState();
        ResetQuizSessionCounters();
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
