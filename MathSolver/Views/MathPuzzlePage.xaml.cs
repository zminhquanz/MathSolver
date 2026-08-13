using MathSolver.Controls;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.Localization;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Views;

public partial class MathPuzzlePage : ContentPage
{
    private readonly BasicArithmeticEngine _arithmeticEngine = new();
    private readonly GeometryCalculationEngine _geometryEngine = new();
    private readonly ArithmeticQuizGenerator _quizGenerator;
    private readonly GeometryQuizGenerator _geometryQuizGenerator;
    private readonly QuizProblemTypeCatalog _quizProblemTypeCatalog = new();
    private readonly SortedDictionary<int, string> _llmRawOutputs = new();
    private readonly List<LlmQuizDiagnostic> _llmValidationDiagnostics = [];
    private readonly EssayAnswerValidator _essayAnswerValidator;
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
    private QuizProblemRequest? _activeProblemRequest;
    private CancellationTokenSource? _llmGenerationCancellation;
    private string? _llmModelPath;
    private bool _questionAnswered;
    private bool? _lastAnswerWasCorrect;
    private bool _isGeneratingWithLlm;
    private bool _isDownloadingModel;
    private bool _showFriendlyGreetingForCurrentLoad;
    private bool _isUpdatingOperationPicker;
    private bool _isAiDiagnosticsVisible;
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

        InteractiveButtonAnimation.SetIsScopeEnabled(
            this,
            true);

        _quizGenerator =
            new ArithmeticQuizGenerator(
                _arithmeticEngine);

        _essayAnswerValidator =
            new EssayAnswerValidator(
                _arithmeticEngine);

        _geometryQuizGenerator =
            new GeometryQuizGenerator(
                _geometryEngine);

        _localLlmQuizGenerator =
            new LocalLlmQuizGenerator(
                _quizGenerator,
                _arithmeticEngine,
                _geometryQuizGenerator);

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
        ResetLlmDiagnostics();
        UpdateAiDiagnosticsVisibility();
        UpdateScoreLabels();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // WinUI can restore its native blue accent when a Button leaves the
        // Disabled visual state. Reattach the app's dynamic theme resources
        // whenever this page returns from Settings so the current accent is
        // applied immediately.
        RefreshLlmActionButtonTheme();

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
        // Settings và thư viện Gemma chỉ là modal trong suốt phủ lên trang.
        // Constructor của overlay bật cờ trước khi PushModalAsync làm trang
        // nhận OnDisappearing, nên không được coi đây là thao tác rời tab lớn:
        // giữ nguyên câu hỏi, lựa chọn, điểm số và model đang nằm trong RAM.
        if (SettingsMenuPage.IsTransparentOverlayActive ||
            GemmaModelCatalogPage.IsTransparentOverlayActive)
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
                        QuizGenerationSource.Algorithm)
                {
                    GenerateAlgorithmQuestion();
                }
                else
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
        UpdateOperationPickerItems();
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

        bool isLocalLlm =
            _generationSource == QuizGenerationSource.LocalLlm;

        LlmSettingsBorder.IsVisible = isLocalLlm;
        CreateOrRegenerateQuestionButton.Text =
            TranslateQuiz(
                isLocalLlm
                    ? "Quiz.CreateWithAi"
                    : "Quiz.RegenerateQuestion");

        // Cả hai nguồn dùng chung nút bên trái: AI tạo đề bằng model cục bộ,
        // còn Thuật toán tạo lại một câu cùng cấu hình để học sinh có thể
        // bỏ qua câu đang quá khó mà không làm thay đổi điểm hay số thứ tự.
        Grid.SetColumn(NextQuestionButton, 1);
        Grid.SetColumnSpan(NextQuestionButton, 1);

        UpdateEssayAnswerPresentation();
        UpdateCreateOrRegenerateQuestionButtonState();
        UpdateAiDiagnosticsVisibility();
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

    private void OnEssayModeClicked(
        object? sender,
        EventArgs e)
    {
        SelectMode(
            ArithmeticQuizMode.Essay);
    }

    private void SelectMode(
        ArithmeticQuizMode mode)
    {
        if (_selectedMode == mode)
        {
            return;
        }

        CancelLlmGeneration();
        _selectedMode = mode;
        ResetQuizSessionState();
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
            _selectedMode switch
            {
                ArithmeticQuizMode.TrueFalse =>
                    TrueFalseModeButton,
                ArithmeticQuizMode.MultipleChoice =>
                    MultipleChoiceModeButton,
                ArithmeticQuizMode.Essay =>
                    EssayModeButton,
                _ => throw new ArgumentOutOfRangeException()
            },
            TrueFalseModeButton,
            MultipleChoiceModeButton,
            EssayModeButton);

        bool hasQuestion = _currentQuestion is not null;

        TrueFalseAnswerGrid.IsVisible =
            hasQuestion &&
            _selectedMode == ArithmeticQuizMode.TrueFalse;

        MultipleChoiceAnswerGrid.IsVisible =
            hasQuestion &&
            _selectedMode == ArithmeticQuizMode.MultipleChoice;

        EssayAnswerLayout.IsVisible =
            hasQuestion &&
            _selectedMode == ArithmeticQuizMode.Essay;

        UpdateEssayAnswerPresentation();

        QuestionPromptLabel.Text = GetQuestionPromptTitle();
    }

    private string GetQuestionPromptTitle()
    {
        if (_currentQuestion?.GeometryProblem is not null &&
            _generationSource == QuizGenerationSource.Algorithm)
        {
            return Translate("Quiz.GeometryQuestionTitle");
        }

        if (_currentQuestion?.WordProblem is not null)
        {
            return Translate("Quiz.WordProblemTitle");
        }

        return Translate(
            _selectedMode switch
            {
                ArithmeticQuizMode.TrueFalse =>
                    "Quiz.QuestionTitle",
                ArithmeticQuizMode.MultipleChoice =>
                    "Quiz.MultipleChoiceQuestionTitle",
                ArithmeticQuizMode.Essay =>
                    "Quiz.EssayQuestionTitle",
                _ => "Quiz.QuestionTitle"
            });
    }

    private void UpdateEssayAnswerPresentation()
    {
        bool isWordProblemSource =
            _generationSource == QuizGenerationSource.LocalLlm;

        // Lời giải bằng câu văn chỉ có ý nghĩa với toán đố do AI tạo.
        // Nguồn Thuật toán dùng biểu thức hoặc đề hình học ngắn, nên học sinh
        // chỉ cần nhập phép tính và đáp số.
        EssaySolutionSection.IsVisible =
            isWordProblemSource;

        EssayValidationHintLabel.Text =
            TranslateQuiz(
                IsGeometryProblemSelected()
                    ? "Quiz.GeometryEssayValidationHint"
                    : isWordProblemSource
                        ? "Quiz.EssayValidationHint"
                        : "Quiz.EssayValidationHintAlgorithm");

        EssayEquationEntry.Placeholder =
            IsGeometryProblemSelected()
                ? Translate("Quiz.GeometryEssayEquationPlaceholder")
                : Translate("Quiz.EssayEquationPlaceholder");
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
            foreach (QuizProblemOption option in
                     _quizProblemTypeCatalog.Options)
            {
                OperationPicker.Items.Add(
                    Translate(option.LocalizationKey));
            }

            if (selectedIndex >= OperationPicker.Items.Count)
            {
                selectedIndex = 0;
            }

            OperationPicker.SelectedIndex =
                Math.Clamp(
                    selectedIndex,
                    0,
                    OperationPicker.Items.Count - 1);

            _activeProblemRequest =
                _quizProblemTypeCatalog.GetFixedRequest(
                    OperationPicker.SelectedIndex);
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

        _activeProblemRequest =
            _quizProblemTypeCatalog.GetFixedRequest(
                OperationPicker.SelectedIndex);

        UpdateEssayAnswerPresentation();

        if (_generationSource == QuizGenerationSource.Algorithm)
        {
            GenerateAlgorithmQuestion();
        }
        else
        {
            PrepareLlmQuestionForGeneration();
        }
    }

    private QuizProblemRequest ResolveSelectedProblem() =>
        _quizProblemTypeCatalog.Resolve(
            OperationPicker.SelectedIndex);

    private bool IsGeometryProblemSelected()
    {
        if (_currentQuestion?.GeometryProblem is not null)
        {
            return true;
        }

        QuizProblemRequest? request =
            _activeProblemRequest ??
            _quizProblemTypeCatalog.GetFixedRequest(
                OperationPicker.SelectedIndex);

        return request?.Kind == QuizProblemKind.Geometry;
    }

    private void GenerateAlgorithmQuestion(
        int? questionNumberOnSuccess = null)
    {
        CancelLlmGeneration();
        _questionAnswered = false;
        _lastAnswerWasCorrect = null;
        NextQuestionButton.IsEnabled = false;

        try
        {
            QuizProblemRequest problemRequest =
                ResolveSelectedProblem();

            _activeProblemRequest = problemRequest;

            _currentQuestion =
                problemRequest.Kind switch
                {
                    QuizProblemKind.Geometry =>
                        _geometryQuizGenerator.GenerateAlgorithm(
                            _selectedMode,
                            AppLanguageManager.CurrentLanguage),
                    QuizProblemKind.Arithmetic =>
                        _quizGenerator.Generate(
                            _selectedMode,
                            problemRequest.ArithmeticOperation),
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(problemRequest))
                };

            CommitGeneratedQuestionNumber(
                questionNumberOnSuccess);
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
        finally
        {
            // Câu vừa được tạo mới hoặc tạo lại nên trạng thái đã trả lời đã
            // được xóa. Bật lại nút Tạo đề lại; nếu không, trạng thái Disabled
            // của câu trước sẽ còn giữ nguyên sau khi bấm Câu tiếp theo.
            UpdateCreateOrRegenerateQuestionButtonState();
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
        _activeProblemRequest =
            _quizProblemTypeCatalog.GetFixedRequest(
                OperationPicker.SelectedIndex);
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
        ResetLlmTokenSpeed();
        ResetLlmDiagnostics();
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

        var catalogPage =
            new GemmaModelCatalogPage();

        await Navigation.PushModalAsync(
            catalogPage,
            animated: false);

        Gemma4ModelDescriptor? model =
            await catalogPage.WaitForDownloadSelectionAsync();

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

    private async void OnCreateOrRegenerateQuestionClicked(
        object? sender,
        EventArgs e)
    {
        if (_questionAnswered)
        {
            UpdateCreateOrRegenerateQuestionButtonState();
            return;
        }

        if (_generationSource == QuizGenerationSource.Algorithm)
        {
            // Tạo lại câu hiện tại, không tăng bộ đếm và không tính điểm.
            GenerateAlgorithmQuestion(
                questionNumberOnSuccess: null);
            return;
        }

        // Nút này vừa tạo câu đầu tiên vừa cho phép bỏ qua/tạo lại câu hiện
        // tại trước khi trả lời. Sau khi đã trả lời, người dùng phải chuyển
        // sang câu tiếp theo; tạo lại không tăng số câu và không tính điểm.
        await GenerateLlmQuestionAsync(
            questionNumberOnSuccess: null);
    }

    private async Task GenerateLlmQuestionAsync(
        int? questionNumberOnSuccess)
    {
        if (_isGeneratingWithLlm ||
            (!questionNumberOnSuccess.HasValue &&
             _questionAnswered) ||
            (questionNumberOnSuccess.HasValue &&
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

        QuizProblemRequest problemRequest =
            ResolveSelectedProblem();

        _activeProblemRequest = problemRequest;

        ResetLlmDiagnostics();

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

        ResetLlmTokenSpeed();

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
                    problemRequest,
                    AppLanguageManager.CurrentLanguage,
                    progress,
                    cancellation.Token);

            // Vô hiệu hóa callback Progress<T> đang chờ trên UI thread trước
            // khi hiển thị trạng thái cuối. Nếu không, ModelLoaded/Validating
            // đến muộn có thể bật spinner trở lại sau khi tác vụ đã hoàn tất.
            CompleteLlmProgress(progressVersion);

            ApplyLlmAttemptReports(
                result.AttemptReports);

            if (result.Question is null &&
                result.ErrorCode is
                    "ModelFileNotFound" or
                    "NotEnoughMemory" or
                    "ModelRuntimeError")
            {
                AppendLlmDiagnostic(
                    new(
                        LlmQuizDiagnosticEvent.RuntimeError,
                        Math.Max(1, result.Attempts),
                        LocalLlmQuizGenerator.MaximumAttempts,
                        result.ErrorCode));
            }

            if (result.ModelWasLoaded &&
                _showFriendlyGreetingForCurrentLoad)
            {
                _llmModelStore.MarkFirstGreetingShown();
            }

            if (result.Question is not null)
            {
                _currentQuestion = result.Question;
                CommitGeneratedQuestionNumber(
                    questionNumberOnSuccess);
                RenderCurrentQuestion(
                    resetAnswerControls: true);
                UpdateScoreLabels();
                ShowLlmStatus(
                    Translate("Quiz.GenerationSucceeded"),
                    isRunning: false);
                ShowLlmTokenSpeed(
                    result.TokensPerSecond);
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
        if (progress.RawModelOutput is not null &&
            progress.Attempt > 0)
        {
            UpdateLlmRawOutput(
                progress.Attempt,
                progress.MaximumAttempts,
                progress.RawModelOutput);
        }

        if (progress.Diagnostic is not null)
        {
            AppendLlmDiagnostic(
                progress.Diagnostic);
        }

        if (_isGeneratingWithLlm &&
            !string.IsNullOrWhiteSpace(
                progress.ProblemPreview))
        {
            ShowGeneratedProblemPreview(
                progress.ProblemPreview);
        }

        if (progress.TokensPerSecond > 0d)
        {
            ShowLlmTokenSpeed(
                progress.TokensPerSecond);
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
        ShowLlmTokenSpeed(
            result.TokensPerSecond);
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

        UpdateCreateOrRegenerateQuestionButtonState();

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

        RefreshLlmActionButtonTheme();
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
        EssayModeButton.IsEnabled = !isBusy;
        OperationPicker.IsEnabled = !isBusy;

        UpdateCreateOrRegenerateQuestionButtonState();
    }

    private void UpdateCreateOrRegenerateQuestionButtonState()
    {
        CreateOrRegenerateQuestionButton.IsEnabled =
            !_isGeneratingWithLlm &&
            (_generationSource == QuizGenerationSource.Algorithm ||
             _llmModelPath is not null) &&
            !_questionAnswered;

        // Reapply after the Enabled/Disabled transition. On Windows this
        // transition can otherwise replace the DynamicResource with the
        // platform's default blue accent.
        RefreshLlmActionButtonTheme();
    }

    private void RefreshLlmActionButtonTheme()
    {
        // WinUI có thể khôi phục màu accent mặc định (xanh dương) sau khi
        // Button đi qua visual state Disabled/Enabled. Áp lại cả hai nút chọn
        // nguồn để nút đang chọn luôn theo accent hiện tại của ứng dụng.
        SelectionButtonStyler.Select(
            _generationSource == QuizGenerationSource.Algorithm
                ? AlgorithmSourceButton
                : LocalLlmSourceButton,
            AlgorithmSourceButton,
            LocalLlmSourceButton);

        DownloadGemma4Button.SetDynamicResource(
            Button.BackgroundColorProperty,
            "PrimaryColor");
        DownloadGemma4Button.SetDynamicResource(
            Button.TextColorProperty,
            "OnPrimaryColor");

        OpenLlmModelFolderButton.SetDynamicResource(
            Button.BackgroundColorProperty,
            "SurfaceColor");
        OpenLlmModelFolderButton.SetDynamicResource(
            Button.BorderColorProperty,
            "PrimaryBorderColor");
        OpenLlmModelFolderButton.SetDynamicResource(
            Button.TextColorProperty,
            "PrimaryColor");

        CreateOrRegenerateQuestionButton.SetDynamicResource(
            Button.BackgroundColorProperty,
            "PrimaryColor");
        CreateOrRegenerateQuestionButton.SetDynamicResource(
            Button.TextColorProperty,
            "OnPrimaryColor");

        // This button follows the same disabled-to-enabled lifecycle after
        // an answer is selected, so keep it on the active accent as well.
        NextQuestionButton.SetDynamicResource(
            Button.BackgroundColorProperty,
            "PrimaryColor");
        NextQuestionButton.SetDynamicResource(
            Button.TextColorProperty,
            "OnPrimaryColor");

        SubmitEssayAnswerButton.SetDynamicResource(
            Button.BackgroundColorProperty,
            "PrimaryColor");
        SubmitEssayAnswerButton.SetDynamicResource(
            Button.TextColorProperty,
            "OnPrimaryColor");
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

    private void ShowLlmTokenSpeed(
        double tokensPerSecond)
    {
        if (!double.IsFinite(tokensPerSecond) ||
            tokensPerSecond <= 0d)
        {
            ResetLlmTokenSpeed();
            return;
        }

        LlmTokenSpeedLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                TranslateQuiz("Quiz.GenerationSpeed"),
                tokensPerSecond.ToString(
                    "0.0",
                    CultureInfo.CurrentCulture));

        LlmTokenSpeedLabel.IsVisible = true;
    }

    private void ResetLlmTokenSpeed()
    {
        LlmTokenSpeedLabel.Text = string.Empty;
        LlmTokenSpeedLabel.IsVisible = false;
    }

    private void OnAiDiagnosticsToggleClicked(
        object? sender,
        EventArgs e)
    {
        _isAiDiagnosticsVisible =
            !_isAiDiagnosticsVisible;

        UpdateAiDiagnosticsVisibility();
    }

    private void UpdateAiDiagnosticsVisibility()
    {
        AiDiagnosticsBorder.IsVisible =
            _isAiDiagnosticsVisible;

        AiDiagnosticsToggleButton.Text =
            TranslateQuiz(
                _isAiDiagnosticsVisible
                    ? "Quiz.HideAiDiagnostics"
                    : "Quiz.ShowAiDiagnostics");
    }

    private void ResetLlmDiagnostics()
    {
        _llmRawOutputs.Clear();
        _llmValidationDiagnostics.Clear();

        LlmRawJsonEditor.Text =
            TranslateQuiz("Quiz.DiagnosticsNoJson");

        LlmValidationLogEditor.Text =
            TranslateQuiz("Quiz.DiagnosticsNoLog");
    }

    private void UpdateLlmRawOutput(
        int attempt,
        int maximumAttempts,
        string rawModelOutput)
    {
        _llmRawOutputs[attempt] =
            rawModelOutput;

        LlmRawJsonEditor.Text =
            string.Join(
                Environment.NewLine +
                Environment.NewLine,
                _llmRawOutputs.Select(entry =>
                    string.Format(
                        CultureInfo.CurrentCulture,
                        TranslateQuiz(
                            "Quiz.DiagnosticsAttemptHeader"),
                        entry.Key,
                        maximumAttempts) +
                    Environment.NewLine +
                    entry.Value));
    }

    private void AppendLlmDiagnostic(
        LlmQuizDiagnostic diagnostic)
    {
        if (_llmValidationDiagnostics.Contains(diagnostic))
        {
            return;
        }

        _llmValidationDiagnostics.Add(diagnostic);
        RenderLlmValidationLog();
    }

    private void ApplyLlmAttemptReports(
        IReadOnlyList<LlmQuizAttemptReport>? reports)
    {
        if (reports is null || reports.Count == 0)
        {
            return;
        }

        _llmRawOutputs.Clear();
        _llmValidationDiagnostics.Clear();

        foreach (LlmQuizAttemptReport report in
                 reports.OrderBy(report => report.Attempt))
        {
            _llmRawOutputs[report.Attempt] =
                report.RawModelOutput;

            _llmValidationDiagnostics.AddRange(
                report.Diagnostics);
        }

        int maximumAttempts =
            reports.Max(report =>
                report.MaximumAttempts);

        LlmRawJsonEditor.Text =
            string.Join(
                Environment.NewLine +
                Environment.NewLine,
                _llmRawOutputs.Select(entry =>
                    string.Format(
                        CultureInfo.CurrentCulture,
                        TranslateQuiz(
                            "Quiz.DiagnosticsAttemptHeader"),
                        entry.Key,
                        maximumAttempts) +
                    Environment.NewLine +
                    entry.Value));

        RenderLlmValidationLog();
    }

    private void RenderLlmValidationLog()
    {
        LlmValidationLogEditor.Text =
            _llmValidationDiagnostics.Count == 0
                ? TranslateQuiz(
                    "Quiz.DiagnosticsNoLog")
                : string.Join(
                    Environment.NewLine,
                    _llmValidationDiagnostics.Select(
                        FormatLlmDiagnostic));
    }

    private static string FormatLlmDiagnostic(
        LlmQuizDiagnostic diagnostic)
    {
        string key =
            diagnostic.Event switch
            {
                LlmQuizDiagnosticEvent.AttemptStarted =>
                    "Quiz.DiagnosticsAttemptStarted",
                LlmQuizDiagnosticEvent.JsonReceived =>
                    "Quiz.DiagnosticsJsonReceived",
                LlmQuizDiagnosticEvent.ParseSucceeded =>
                    "Quiz.DiagnosticsParseSucceeded",
                LlmQuizDiagnosticEvent.ParseFailed =>
                    "Quiz.DiagnosticsParseFailed",
                LlmQuizDiagnosticEvent.ValidationSucceeded =>
                    "Quiz.DiagnosticsValidationSucceeded",
                LlmQuizDiagnosticEvent.ValidationFailed =>
                    "Quiz.DiagnosticsValidationFailed",
                LlmQuizDiagnosticEvent.RetryScheduled =>
                    "Quiz.DiagnosticsRetryScheduled",
                LlmQuizDiagnosticEvent.GenerationFailed =>
                    "Quiz.DiagnosticsGenerationFailed",
                LlmQuizDiagnosticEvent.RuntimeError =>
                    "Quiz.DiagnosticsRuntimeError",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(diagnostic))
            };

        return string.Format(
            CultureInfo.CurrentCulture,
            TranslateQuiz(key),
            diagnostic.Attempt,
            diagnostic.MaximumAttempts,
            diagnostic.Event ==
                LlmQuizDiagnosticEvent.JsonReceived
                    ? diagnostic.CharacterCount
                    : diagnostic.Detail ?? string.Empty);
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
        EssaySolutionEditor.IsEnabled = isEnabled;
        EssayEquationEntry.IsEnabled = isEnabled;
        EssayAnswerEntry.IsEnabled = isEnabled;
        SubmitEssayAnswerButton.IsEnabled = isEnabled;

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
                GetQuestionPromptTitle();
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

                string choiceUnit =
                    wordProblem is null
                        ? string.Empty
                        : $" {wordProblem.AnswerUnit}";

                button.Text =
                    $"{prefix}. {choice.ToString("N0", CultureInfo.CurrentCulture)}{choiceUnit}";

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

        EssaySolutionEditor.Text = string.Empty;
        EssayEquationEntry.Text = string.Empty;
        EssayAnswerEntry.Text = string.Empty;

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

    private void OnSubmitEssayAnswerClicked(
        object? sender,
        EventArgs e)
    {
        if (_questionAnswered ||
            _currentQuestion is null ||
            _currentQuestion.Mode != ArithmeticQuizMode.Essay)
        {
            return;
        }

        EssayAnswerValidationResult validation =
            _essayAnswerValidator.Validate(
                _currentQuestion,
                EssayEquationEntry.Text,
                EssayAnswerEntry.Text);

        CompleteAnswer(
            validation.IsCorrect,
            selectedButton: null,
            feedbackOverride:
                BuildEssayFeedback(validation));
    }

    private void CompleteAnswer(
        bool isCorrect,
        Button? selectedButton,
        string? feedbackOverride = null)
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

        if (!string.IsNullOrWhiteSpace(
                feedbackOverride))
        {
            FeedbackLabel.Text = feedbackOverride;
        }

        if (_currentQuestion.WordProblem is not null)
        {
            SolutionLabel.Text =
                ElementaryWordProblemSolutionFormatter.Format(
                    _currentQuestion,
                    AppLanguageManager.CurrentLanguage,
                    CultureInfo.CurrentCulture);
            SolutionBorder.IsVisible = true;
        }
        else if (_currentQuestion.Mode ==
                 ArithmeticQuizMode.Essay)
        {
            SolutionLabel.Text =
                FormatPlainEssaySolution(
                    _currentQuestion);
            SolutionBorder.IsVisible = true;
        }

        NextQuestionButton.IsEnabled = true;
        UpdateCreateOrRegenerateQuestionButtonState();
        UpdateScoreLabels();
    }

    private static string BuildEssayFeedback(
        EssayAnswerValidationResult validation)
    {
        if (validation.IsCorrect)
        {
            return TranslateQuiz(
                "Quiz.EssayCorrectFeedback");
        }

        if (!validation.EquationIsCorrect &&
            !validation.AnswerIsCorrect)
        {
            return TranslateQuiz(
                "Quiz.EssayEquationAndAnswerIncorrect");
        }

        if (!validation.EquationIsCorrect)
        {
            return validation.EquationError ==
                    EssayAnswerError.InvalidEquationFormat
                ? TranslateQuiz(
                    "Quiz.EssayEquationFormatIncorrect")
                : TranslateQuiz(
                    "Quiz.EssayEquationIncorrect");
        }

        return validation.AnswerError ==
                EssayAnswerError.WrongAnswerUnit
            ? TranslateQuiz(
                "Quiz.EssayAnswerUnitIncorrect")
            : TranslateQuiz(
                "Quiz.EssayAnswerIncorrect");
    }

    private static string FormatPlainEssaySolution(
        ArithmeticQuizQuestion question)
    {
        string left =
            question.Expression.LeftOperand.ToString(
                "N0",
                CultureInfo.CurrentCulture);

        string right =
            question.Expression.RightOperand.ToString(
                "N0",
                CultureInfo.CurrentCulture);

        string answer =
            question.CorrectAnswer.ToString(
                "N0",
                CultureInfo.CurrentCulture);

        string symbol =
            BasicArithmeticEngine.GetSymbol(
                question.Expression.Operation);

        string answerLabel =
            AppLanguageManager.CurrentLanguage ==
                AppLanguage.Vietnamese
                ? "Đáp số"
                : "Answer";

        return
            $"{left} {symbol} {right} = {answer}" +
            Environment.NewLine +
            $"{answerLabel}: {answer}";
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

        // Số câu kế tiếp được chốt từ bộ đếm hiện tại, hoàn toàn độc lập
        // với việc câu vừa trả lời là đúng hay sai. Chỉ khi tạo câu mới
        // thành công thì giá trị này mới được commit.
        int nextQuestionNumber =
            checked(_questionCount + 1);

        if (_generationSource == QuizGenerationSource.Algorithm)
        {
            GenerateAlgorithmQuestion(
                questionNumberOnSuccess:
                    nextQuestionNumber);
        }
        else
        {
            await GenerateLlmQuestionAsync(
                questionNumberOnSuccess:
                    nextQuestionNumber);
        }
    }

    private void CommitGeneratedQuestionNumber(
        int? questionNumberOnSuccess)
    {
        if (questionNumberOnSuccess is int questionNumber)
        {
            _questionCount = questionNumber;
        }
        else if (_questionCount == 0)
        {
            // Câu đầu tiên của một phiên luôn bắt đầu ở 1. Đổi cấu hình hoặc
            // tạo lại đề hiện tại sau đó không làm nhảy số câu.
            _questionCount = 1;
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
        _activeProblemRequest =
            _quizProblemTypeCatalog.GetFixedRequest(
                OperationPicker.SelectedIndex);
        _questionAnswered = false;
        _lastAnswerWasCorrect = null;

        QuestionExpressionLabel.Text = string.Empty;
        PresentedAnswerLabel.Text = string.Empty;
        PresentedAnswerLabel.IsVisible = false;
        FeedbackLabel.Text = string.Empty;
        FeedbackBorder.IsVisible = false;
        SolutionLabel.Text = string.Empty;
        SolutionBorder.IsVisible = false;
        EssaySolutionEditor.Text = string.Empty;
        EssayEquationEntry.Text = string.Empty;
        EssayAnswerEntry.Text = string.Empty;
        NextQuestionButton.IsEnabled = false;

        ClearMultipleChoiceAnswers();
        SetAnswerControlsEnabled(false);
        UpdateModeStyles();

        LlmActivityIndicator.IsRunning = false;
        LlmActivityIndicator.IsVisible = false;
        LlmStatusLabel.Text = string.Empty;
        LlmProgressGrid.IsVisible = false;
        ResetLlmTokenSpeed();
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

    private static string TranslateQuiz(
        string key)
    {
        string culture =
            AppLanguageManager.CurrentLanguage ==
                AppLanguage.Vietnamese
                ? "vi-VN"
                : "en-US";

        return QuizLocalizationOverrides.TryGetValue(
                key,
                culture,
                out string value)
            ? value
            : Translate(key);
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
