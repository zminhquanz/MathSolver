using MathSolver.Controls;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.Localization;
using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace MathSolver.Views;

public partial class MathPuzzlePage : ContentPage
{
    private static readonly JsonSerializerOptions PrettyJsonOptions =
        new()
        {
            Encoder =
                JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };

    private readonly BasicArithmeticEngine _arithmeticEngine = new();
    private readonly FractionCalculationEngine _fractionEngine = new();
    private readonly GeometryCalculationEngine _geometryEngine = new();
    private readonly FindXEngine _findXEngine = new();
    private readonly ArithmeticQuizGenerator _quizGenerator;
    private readonly FractionQuizGenerator _fractionQuizGenerator;
    private readonly GeometryQuizGenerator _geometryQuizGenerator;
    private readonly FindXQuizGenerator _findXQuizGenerator;
    private readonly ProportionQuizGenerator _proportionQuizGenerator;
    private readonly MotionQuizGenerator _motionQuizGenerator;
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
    private ArithmeticOperation _selectedBasicOperation =
        ArithmeticOperation.Add;
    private FractionOperation _selectedFractionOperation =
        FractionOperation.Add;
    private ProportionQuizType _selectedProportionType =
        ProportionQuizType.Direct;
    private bool _isAiDiagnosticsVisible;
    private bool _isDeveloperModeSubscribed;
    private int _llmProgressVersion;
    // Khi bấm Câu tiếp theo, số câu mới chỉ được commit sau khi AI tạo được
    // đề hợp lệ. Nếu cả ba attempt đều thất bại, giữ lại số này để lần bấm
    // Tạo lại kế tiếp vẫn hoàn tất đúng câu đang chờ thay vì đứng ở câu cũ.
    private int? _pendingLlmQuestionNumberOnSuccess;
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

    private FractionExpressionView[] ChoiceFractionViews =>
    [
        ChoiceAFractionView,
        ChoiceBFractionView,
        ChoiceCFractionView,
        ChoiceDFractionView
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

        _fractionQuizGenerator =
            new FractionQuizGenerator(
                _fractionEngine);

        _essayAnswerValidator =
            new EssayAnswerValidator(
                _arithmeticEngine);

        _geometryQuizGenerator =
            new GeometryQuizGenerator(
                _geometryEngine);

        _findXQuizGenerator =
            new FindXQuizGenerator(
                _findXEngine);

        _proportionQuizGenerator =
            new ProportionQuizGenerator();

        _motionQuizGenerator =
            new MotionQuizGenerator();

        _localLlmQuizGenerator =
            new LocalLlmQuizGenerator(
                _quizGenerator,
                _arithmeticEngine,
                _fractionQuizGenerator,
                _geometryQuizGenerator,
                _findXQuizGenerator,
                _proportionQuizGenerator,
                _motionQuizGenerator);

        _llmModelPath =
            _llmModelStore.GetSavedModelPath();

        LocalizationService.ExcludeSubtreeFromLegacyTracking(
            this);

        LocalizationService.CultureChanged +=
            OnCultureChanged;

        // MathPuzzlePage là ShellContent sống suốt vòng đời tab. Khi đổi theme
        // từ Settings popup, trang có thể nhận OnDisappearing nhưng không bị
        // destroy; vì vậy giữ subscription ở cùng lifetime với CultureChanged.
        AppThemeManager.ThemeChanged +=
            OnThemeChanged;

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

        // Main page luôn là nguồn sự thật cuối cùng cho Shell TabBar. Nếu
        // WinUI vừa hoàn tất một Settings Pop theo thứ tự native bất thường,
        // re-assert này sửa chrome ngay trong lifecycle của trang chính.
        Shell.SetTabBarIsVisible(
            this,
            true);

        SubscribeDeveloperModeChanged();
        UpdateAiDiagnosticsVisibility();

        // WinUI can keep the old theme brush on stateful Buttons after a
        // ResourceDictionary swap or after returning from Settings. Reattach
        // every selection group's DynamicResource to the current palette.
        RefreshStatefulButtonTheme();
        UpdateAiTeacherState();

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
        UnsubscribeDeveloperModeChanged();

        // Settings và thư viện Gemma chỉ là overlay trong suốt phủ lên trang.
        // Constructor của overlay bật cờ trước khi popup làm trang
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

    private void OnThemeChanged(
        object? sender,
        EventArgs e)
    {
        // AppThemeManager đã thay palette trước khi phát event. Dispatch sang
        // UI queue giúp WinUI hoàn tất state transition của Button rồi mới gắn
        // lại DynamicResource, tránh hai nút Thuật toán / AI-LLM giữ màu cũ.
        Dispatcher.Dispatch(
            RefreshStatefulButtonTheme);
    }

    private void RefreshStatefulButtonTheme()
    {
        UpdateGenerationSourceStyles();
        UpdateModeStyles();
        UpdateProblemOperationPanel();
        RefreshTrueFalseAnswerButtonTheme();
    }

    private void RefreshTrueFalseAnswerButtonTheme()
    {
        // Hai nút này dùng màu semantic Success/Danger thay vì màu selected
        // của SelectionButtonStyler. Trên WinUI, Button đang ở visual state
        // hiện tại có thể giữ brush cũ sau khi ResourceDictionary đổi theme.
        // Gán trực tiếp màu palette hiện tại để Dark -> Light và Light -> Dark
        // cập nhật ngay cả khi câu hỏi đã được render trước lúc đổi theme.
        ApplySemanticAnswerButtonTheme(
            TrueAnswerButton,
            "SuccessSoftColor",
            "SuccessColor");

        ApplySemanticAnswerButtonTheme(
            FalseAnswerButton,
            "DangerSoftColor",
            "DangerColor");
    }

    private static void ApplySemanticAnswerButtonTheme(
        Button button,
        string backgroundResourceKey,
        string foregroundResourceKey)
    {
        button.BackgroundColor =
            ThemeResource.GetColor(
                backgroundResourceKey,
                backgroundResourceKey == "SuccessSoftColor"
                    ? "#F0FDF4"
                    : "#FEF2F2");

        Color foreground =
            ThemeResource.GetColor(
                foregroundResourceKey,
                foregroundResourceKey == "SuccessColor"
                    ? "#15803D"
                    : "#B91C1C");

        button.BorderColor = foreground;
        button.TextColor = foreground;
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
        if (_currentQuestion?.FractionProblem is not null &&
            _generationSource == QuizGenerationSource.Algorithm)
        {
            return TranslateQuiz("Quiz.FractionQuestionTitle");
        }

        if (_currentQuestion?.FindXProblem is not null &&
            _generationSource == QuizGenerationSource.Algorithm)
        {
            return TranslateQuiz("Quiz.FindXQuestionTitle");
        }

        if (_currentQuestion?.GeometryProblem is not null &&
            _generationSource == QuizGenerationSource.Algorithm)
        {
            return Translate("Quiz.GeometryQuestionTitle");
        }

        if (_currentQuestion?.ProportionProblem is not null &&
            _generationSource == QuizGenerationSource.Algorithm)
        {
            return TranslateQuiz("Quiz.ProportionQuestionTitle");
        }

        if (_currentQuestion?.MotionProblem is not null &&
            _generationSource == QuizGenerationSource.Algorithm)
        {
            return TranslateQuiz("Quiz.MotionQuestionTitle");
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
        bool isFindX = IsFindXProblemSelected();
        bool isFraction = IsFractionProblemSelected();
        bool isProportion = IsProportionProblemSelected();
        bool isMotion = IsMotionProblemSelected();

        // Lời giải bằng câu văn chỉ có ý nghĩa với toán đố do AI tạo.
        // Nguồn Thuật toán dùng biểu thức hoặc đề hình học ngắn, nên học sinh
        // chỉ cần nhập phép tính và đáp số.
        EssaySolutionSection.IsVisible =
            isWordProblemSource;

        EssayValidationHintLabel.Text =
            TranslateQuiz(
                isWordProblemSource && isFindX
                    ? "Quiz.FindXEssayValidationHintAi"
                    : isWordProblemSource && IsGeometryProblemSelected()
                    ? "Quiz.GeometryEssayValidationHintAi"
                    : isWordProblemSource && isFraction
                    ? "Quiz.FractionEssayValidationHintAi"
                    : isWordProblemSource && isProportion
                    ? "Quiz.ProportionEssayValidationHintAi"
                    : isWordProblemSource && isMotion
                    ? "Quiz.MotionEssayValidationHintAi"
                    : isFindX
                    ? "Quiz.FindXEssayValidationHint"
                    : IsGeometryProblemSelected()
                    ? "Quiz.GeometryEssayValidationHint"
                    : isFraction
                    ? "Quiz.FractionEssayValidationHint"
                    : isProportion
                    ? "Quiz.ProportionEssayValidationHint"
                    : isMotion
                    ? "Quiz.MotionEssayValidationHint"
                    : isWordProblemSource
                        ? "Quiz.EssayValidationHint"
                        : "Quiz.EssayValidationHintAlgorithm");

        EssayEquationEntry.Placeholder =
            isFindX
                ? TranslateQuiz("Quiz.FindXEssayEquationPlaceholder")
                : IsFractionProblemSelected()
                ? TranslateQuiz("Quiz.FractionEssayEquationPlaceholder")
                : IsGeometryProblemSelected()
                ? Translate("Quiz.GeometryEssayEquationPlaceholder")
                : isProportion
                ? TranslateQuiz("Quiz.ProportionEssayEquationPlaceholder")
                : isMotion
                ? TranslateQuiz("Quiz.MotionEssayEquationPlaceholder")
                : Translate("Quiz.EssayEquationPlaceholder");

        EssayAnswerEntry.Placeholder =
            isFraction
                ? TranslateQuiz("Quiz.FractionEssayAnswerPlaceholder")
                : Translate("Quiz.EssayAnswerPlaceholder");
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
                    TranslateQuiz(option.LocalizationKey));
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
                GetSelectedFixedProblemRequest();

            UpdateProblemOperationPanel();
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

        // Đổi dạng bài toán bắt đầu một phiên luyện tập mới, giống hệt đổi
        // kiểu câu hỏi hoặc đổi tab chính. Không giữ lại số câu/đúng/sai của
        // dạng trước vì chúng không còn cùng một cấu hình luyện tập.
        CancelLlmGeneration();
        ResetQuizSessionState();

        _activeProblemRequest =
            GetSelectedFixedProblemRequest();

        UpdateProblemOperationPanel();

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
            OperationPicker.SelectedIndex,
            _selectedBasicOperation,
            _selectedFractionOperation,
            _selectedProportionType);

    private QuizProblemRequest? GetSelectedFixedProblemRequest()
    {
        QuizProblemRequest? request =
            _quizProblemTypeCatalog.GetFixedRequest(
                OperationPicker.SelectedIndex);

        return request?.Kind switch
        {
            QuizProblemKind.Arithmetic =>
                request.Value with
                {
                    ArithmeticOperation = _selectedBasicOperation
                },
            QuizProblemKind.Fraction =>
                request.Value with
                {
                    FractionOperation = _selectedFractionOperation
                },
            QuizProblemKind.Proportion =>
                request.Value with
                {
                    ProportionType = _selectedProportionType
                },
            _ => request
        };
    }

    private void UpdateProblemOperationPanel()
    {
        QuizProblemKind? kind =
            _quizProblemTypeCatalog
                .GetFixedRequest(OperationPicker.SelectedIndex)
                ?.Kind;

        bool showOperations =
            kind is QuizProblemKind.Arithmetic or QuizProblemKind.Fraction;
        bool showProportionType =
            kind == QuizProblemKind.Proportion;

        ProblemOperationPanel.IsVisible = showOperations;
        ProportionTypePanel.IsVisible = showProportionType;

        if (showProportionType)
        {
            SelectionButtonStyler.Select(
                _selectedProportionType == ProportionQuizType.Direct
                    ? DirectProportionButton
                    : InverseProportionButton,
                DirectProportionButton,
                InverseProportionButton);
        }

        if (!showOperations)
        {
            return;
        }

        ProblemOperationTitleLabel.Text = TranslateQuiz(
            kind == QuizProblemKind.Fraction
                ? "Quiz.FractionOperationTitle"
                : "Quiz.BasicOperationTitle");

        ArithmeticOperation operation =
            kind == QuizProblemKind.Fraction
                ? MapFractionOperation(_selectedFractionOperation)
                : _selectedBasicOperation;

        Button selected = operation switch
        {
            ArithmeticOperation.Add => ProblemAddButton,
            ArithmeticOperation.Subtract => ProblemSubtractButton,
            ArithmeticOperation.Multiply => ProblemMultiplyButton,
            ArithmeticOperation.Divide => ProblemDivideButton,
            _ => ProblemAddButton
        };

        SelectionButtonStyler.Select(
            selected,
            ProblemAddButton,
            ProblemSubtractButton,
            ProblemMultiplyButton,
            ProblemDivideButton);
    }

    private void OnProblemAddClicked(object? sender, EventArgs e) =>
        SelectProblemOperation(ArithmeticOperation.Add);

    private void OnProblemSubtractClicked(object? sender, EventArgs e) =>
        SelectProblemOperation(ArithmeticOperation.Subtract);

    private void OnProblemMultiplyClicked(object? sender, EventArgs e) =>
        SelectProblemOperation(ArithmeticOperation.Multiply);

    private void OnProblemDivideClicked(object? sender, EventArgs e) =>
        SelectProblemOperation(ArithmeticOperation.Divide);

    private void SelectProblemOperation(ArithmeticOperation operation)
    {
        QuizProblemKind? kind =
            _quizProblemTypeCatalog
                .GetFixedRequest(OperationPicker.SelectedIndex)
                ?.Kind;

        bool changed;
        if (kind == QuizProblemKind.Arithmetic)
        {
            changed = _selectedBasicOperation != operation;
            _selectedBasicOperation = operation;
        }
        else if (kind == QuizProblemKind.Fraction)
        {
            FractionOperation fractionOperation =
                MapArithmeticOperation(operation);
            changed = _selectedFractionOperation != fractionOperation;
            _selectedFractionOperation = fractionOperation;
        }
        else
        {
            return;
        }

        UpdateProblemOperationPanel();
        if (!changed)
        {
            return;
        }

        CancelLlmGeneration();
        ResetQuizSessionState();
        _activeProblemRequest = GetSelectedFixedProblemRequest();
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

    private void OnDirectProportionClicked(object? sender, EventArgs e) =>
        SelectProportionType(ProportionQuizType.Direct);

    private void OnInverseProportionClicked(object? sender, EventArgs e) =>
        SelectProportionType(ProportionQuizType.Inverse);

    private void SelectProportionType(ProportionQuizType type)
    {
        QuizProblemKind? kind =
            _quizProblemTypeCatalog
                .GetFixedRequest(OperationPicker.SelectedIndex)
                ?.Kind;

        if (kind != QuizProblemKind.Proportion)
        {
            return;
        }

        bool changed = _selectedProportionType != type;
        _selectedProportionType = type;
        UpdateProblemOperationPanel();

        if (!changed)
        {
            return;
        }

        CancelLlmGeneration();
        ResetQuizSessionState();
        _activeProblemRequest = GetSelectedFixedProblemRequest();
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

    private static FractionOperation MapArithmeticOperation(
        ArithmeticOperation operation) =>
        operation switch
        {
            ArithmeticOperation.Add => FractionOperation.Add,
            ArithmeticOperation.Subtract => FractionOperation.Subtract,
            ArithmeticOperation.Multiply => FractionOperation.Multiply,
            ArithmeticOperation.Divide => FractionOperation.Divide,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

    private static ArithmeticOperation MapFractionOperation(
        FractionOperation operation) =>
        operation switch
        {
            FractionOperation.Add => ArithmeticOperation.Add,
            FractionOperation.Subtract => ArithmeticOperation.Subtract,
            FractionOperation.Multiply => ArithmeticOperation.Multiply,
            FractionOperation.Divide => ArithmeticOperation.Divide,
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

    private bool IsGeometryProblemSelected()
    {
        if (_currentQuestion?.GeometryProblem is not null)
        {
            return true;
        }

        QuizProblemRequest? request =
            _activeProblemRequest ??
            GetSelectedFixedProblemRequest();

        return request?.Kind == QuizProblemKind.Geometry;
    }

    private bool IsFindXProblemSelected()
    {
        if (_currentQuestion?.FindXProblem is not null)
        {
            return true;
        }

        QuizProblemRequest? request =
            _activeProblemRequest ??
            GetSelectedFixedProblemRequest();

        return request?.Kind == QuizProblemKind.FindX;
    }

    private bool IsFractionProblemSelected()
    {
        if (_currentQuestion?.FractionProblem is not null)
        {
            return true;
        }

        QuizProblemRequest? request =
            _activeProblemRequest ??
            GetSelectedFixedProblemRequest();

        return request?.Kind == QuizProblemKind.Fraction;
    }

    private bool IsProportionProblemSelected()
    {
        if (_currentQuestion?.ProportionProblem is not null)
        {
            return true;
        }

        QuizProblemRequest? request =
            _activeProblemRequest ??
            GetSelectedFixedProblemRequest();

        return request?.Kind == QuizProblemKind.Proportion;
    }

    private bool IsMotionProblemSelected()
    {
        if (_currentQuestion?.MotionProblem is not null)
        {
            return true;
        }

        QuizProblemRequest? request =
            _activeProblemRequest ??
            GetSelectedFixedProblemRequest();

        return request?.Kind == QuizProblemKind.Motion;
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
                    QuizProblemKind.Fraction =>
                        _fractionQuizGenerator.Generate(
                            _selectedMode,
                            problemRequest.FractionOperation),
                    QuizProblemKind.FindX =>
                        _findXQuizGenerator.Generate(
                            _selectedMode),
                    QuizProblemKind.Proportion =>
                        _proportionQuizGenerator.GenerateAlgorithm(
                            _selectedMode,
                            problemRequest.ProportionType ?? _selectedProportionType,
                            AppLanguageManager.CurrentLanguage),
                    QuizProblemKind.Motion =>
                        _motionQuizGenerator.GenerateAlgorithm(
                            _selectedMode,
                            AppLanguageManager.CurrentLanguage),
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
            SetQuestionContent(
                Translate("Quiz.GenerationError"),
                20,
                "DangerColor",
                useFractionFormatting: false);
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
            GetSelectedFixedProblemRequest();
        _questionAnswered = false;
        _lastAnswerWasCorrect = null;

        QuestionPromptLabel.Text =
            Translate("Quiz.WordProblemTitle");

        string readyMessage =
            _llmModelPath is null
                ? Translate("Quiz.SelectModelFirst")
                : Translate("Quiz.LlmReady");

        SetQuestionContent(
            readyMessage,
            20,
            "TextSecondaryColor",
            useFractionFormatting: false);

        PresentedAnswerLabel.IsVisible = false;
        PresentedAnswerFractionView.IsVisible = false;
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

        Gemma4ModelDownloadSelection? selection =
            await catalogPage.WaitForDownloadSelectionAsync();

        if (selection is null)
        {
            return;
        }

        Gemma4ModelDescriptor model =
            selection.Model;

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
                    selection.DestinationDirectory,
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
        // tại trước khi trả lời. Tạo lại thông thường không tăng số câu; riêng
        // khi lần sinh từ nút Câu tiếp theo đã thất bại, GenerateLlmQuestionAsync
        // tiếp tục dùng số câu đang chờ và chỉ commit khi đề hợp lệ.
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

        if (questionNumberOnSuccess.HasValue)
        {
            _pendingLlmQuestionNumberOnSuccess =
                questionNumberOnSuccess;
        }

        // Một lần Tạo lại sau khi AI đã thất bại đủ ba attempt phải tiếp tục
        // commit số câu của lần bấm Câu tiếp theo trước đó. Tạo lại một câu
        // hiện có vẫn truyền null và không làm tăng bộ đếm.
        int? resolvedQuestionNumberOnSuccess =
            questionNumberOnSuccess ??
            _pendingLlmQuestionNumberOnSuccess;

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

        _localLlmQuizGenerator.CancelScheduledModelUnload();
        _showFriendlyGreetingForCurrentLoad =
            !_localLlmQuizGenerator.IsModelLoaded(
                _llmModelPath);

        HideAiTeacherGreeting();

        _currentQuestion = null;
        _questionAnswered = false;
        _lastAnswerWasCorrect = null;
        FeedbackBorder.IsVisible = false;
        SolutionBorder.IsVisible = false;
        PresentedAnswerLabel.IsVisible = false;
        PresentedAnswerFractionView.IsVisible = false;
        NextQuestionButton.IsEnabled = false;
        ClearMultipleChoiceAnswers();
        UpdateModeStyles();
        SetAnswerControlsEnabled(false);
        SetLlmBusy(true);

        QuestionPromptLabel.Text =
            Translate("Quiz.WordProblemTitle");
        SetQuestionContent(
            Translate("Quiz.LoadingModel"),
            21,
            "TextPrimaryColor",
            useFractionFormatting: false);

        ResetLlmTokenSpeed();

        ShowLlmStatus(
            Translate("Quiz.LoadingModel"),
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

            if (result.Question is not null)
            {
                _currentQuestion = result.Question;
                CommitGeneratedQuestionNumber(
                    resolvedQuestionNumberOnSuccess);
                _pendingLlmQuestionNumberOnSuccess =
                    null;
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
        if (progress.Stage == LlmQuizProgressStage.ModelLoaded &&
            _showFriendlyGreetingForCurrentLoad)
        {
            ShowAiTeacherGreeting();
        }

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

        SetQuestionContent(
            problemText,
            21,
            "TextPrimaryColor",
            useFractionFormatting:
                IsFractionProblemSelected());

        PresentedAnswerLabel.IsVisible = false;
        PresentedAnswerFractionView.IsVisible = false;
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

        SetQuestionContent(
            message,
            20,
            "DangerColor",
            useFractionFormatting: false);
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
        UpdateAiTeacherState();
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
        UpdateAiTeacherState();
    }

    private void UpdateAiTeacherState()
    {
        bool hasSelectedModel =
            !string.IsNullOrWhiteSpace(_llmModelPath);

        bool modelIsLoaded =
            hasSelectedModel &&
            _localLlmQuizGenerator.IsModelLoaded(
                _llmModelPath);

        string key;
        string colorKey;

        if (_isGeneratingWithLlm)
        {
            key = "Quiz.AiTeacherStateWorking";
            colorKey = "PrimaryColor";
        }
        else if (modelIsLoaded)
        {
            key = "Quiz.AiTeacherStateReady";
            colorKey = "SuccessColor";
        }
        else if (hasSelectedModel)
        {
            key = "Quiz.AiTeacherStateModelSelected";
            colorKey = "WarningColor";
        }
        else
        {
            key = "Quiz.AiTeacherStateNoModel";
            colorKey = "TextSecondaryColor";
        }

        AiTeacherStateLabel.Text =
            TranslateQuiz(key);

        AiTeacherStateLabel.SetDynamicResource(
            Label.TextColorProperty,
            colorKey);

        AiTeacherStateDot.SetDynamicResource(
            BoxView.ColorProperty,
            colorKey);
    }

    private void ShowAiTeacherGreeting()
    {
        AiTeacherGreetingLabel.Text =
            TranslateQuiz("Quiz.FirstModelGreeting");

        AiTeacherGreetingBorder.IsVisible = true;
    }

    private void HideAiTeacherGreeting()
    {
        AiTeacherGreetingBorder.IsVisible = false;
        AiTeacherGreetingLabel.Text = string.Empty;
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
        if (!DeveloperModeManager.IsEnabled)
        {
            return;
        }

        _isAiDiagnosticsVisible =
            !_isAiDiagnosticsVisible;

        UpdateAiDiagnosticsVisibility();
    }

    private void UpdateAiDiagnosticsVisibility()
    {
        bool developerModeEnabled =
            DeveloperModeManager.IsEnabled;

        AiDiagnosticsSectionBorder.IsVisible =
            developerModeEnabled;

        if (!developerModeEnabled)
        {
            _isAiDiagnosticsVisible = false;
        }

        AiDiagnosticsBorder.IsVisible =
            developerModeEnabled &&
            _isAiDiagnosticsVisible;

        AiDiagnosticsToggleButton.IsVisible =
            developerModeEnabled;

        AiDiagnosticsToggleButton.Text =
            TranslateQuiz(
                _isAiDiagnosticsVisible
                    ? "Quiz.HideAiDiagnostics"
                    : "Quiz.ShowAiDiagnostics");
    }

    private void SubscribeDeveloperModeChanged()
    {
        if (_isDeveloperModeSubscribed)
        {
            return;
        }

        DeveloperModeManager.DeveloperModeChanged +=
            OnDeveloperModeChanged;

        _isDeveloperModeSubscribed = true;
    }

    private void UnsubscribeDeveloperModeChanged()
    {
        if (!_isDeveloperModeSubscribed)
        {
            return;
        }

        DeveloperModeManager.DeveloperModeChanged -=
            OnDeveloperModeChanged;

        _isDeveloperModeSubscribed = false;
    }

    private void OnDeveloperModeChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Dispatch(
            UpdateAiDiagnosticsVisibility);
    }

    private void ResetLlmDiagnostics()
    {
        _llmRawOutputs.Clear();
        _llmValidationDiagnostics.Clear();
        LlmRawJsonEditor.Text =
            TranslateQuiz("Quiz.DiagnosticsNoJson");

        LlmValidationLogEditor.Text =
            TranslateQuiz("Quiz.DiagnosticsNoLog");

        AiValidationStatusBorder.IsVisible = false;
        AiValidationStatusTitleLabel.Text = string.Empty;
        AiValidationStatusDetailLabel.Text = string.Empty;
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
                    FormatLlmJsonForDisplay(entry.Value)));
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
        UpdateLlmValidationStatus(diagnostic);
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
                    FormatLlmJsonForDisplay(entry.Value)));

        RenderLlmValidationLog();

        LlmQuizDiagnostic? lastDiagnostic =
            reports
                .OrderBy(report => report.Attempt)
                .SelectMany(report => report.Diagnostics)
                .LastOrDefault();

        if (lastDiagnostic is not null)
        {
            UpdateLlmValidationStatus(lastDiagnostic);
        }
    }

    private void UpdateLlmValidationStatus(
        LlmQuizDiagnostic diagnostic)
    {
        switch (diagnostic.Event)
        {
            case LlmQuizDiagnosticEvent.JsonReceived:
            case LlmQuizDiagnosticEvent.ParseSucceeded:
                ShowLlmValidationStatus(
                    "⏳",
                    "Quiz.AiValidationCheckingTitle",
                    string.Format(
                        CultureInfo.CurrentCulture,
                        TranslateQuiz("Quiz.AiValidationCheckingDetail"),
                        diagnostic.Attempt,
                        diagnostic.MaximumAttempts),
                    "PrimarySoftColor",
                    "PrimaryBorderBrush",
                    "PrimaryColor");
                break;

            case LlmQuizDiagnosticEvent.ParseFailed:
            case LlmQuizDiagnosticEvent.ValidationFailed:
                ShowLlmValidationStatus(
                    "✕",
                    "Quiz.AiValidationInvalidTitle",
                    diagnostic.Detail ??
                        TranslateQuiz("Quiz.AiValidationInvalidFallback"),
                    "DangerSoftColor",
                    "DangerBorderBrush",
                    "DangerColor");
                break;

            case LlmQuizDiagnosticEvent.RetryScheduled:
                ShowLlmValidationStatus(
                    "↻",
                    "Quiz.AiValidationRetryTitle",
                    diagnostic.Detail ??
                        TranslateQuiz("Quiz.AiValidationInvalidFallback"),
                    "WarningSoftColor",
                    "WarningBorderBrush",
                    "WarningColor");
                break;

            case LlmQuizDiagnosticEvent.ValidationSucceeded:
                ShowLlmValidationStatus(
                    "✓",
                    "Quiz.AiValidationValidTitle",
                    string.Format(
                        CultureInfo.CurrentCulture,
                        TranslateQuiz("Quiz.AiValidationValidDetail"),
                        diagnostic.Attempt,
                        diagnostic.MaximumAttempts),
                    "SuccessSoftColor",
                    "SuccessBorderBrush",
                    "SuccessColor");
                break;

            case LlmQuizDiagnosticEvent.GenerationFailed:
                ShowLlmValidationStatus(
                    "✕",
                    "Quiz.AiValidationGenerationFailedTitle",
                    diagnostic.Detail ??
                        TranslateQuiz("Quiz.AiValidationInvalidFallback"),
                    "DangerSoftColor",
                    "DangerBorderBrush",
                    "DangerColor");
                break;

            case LlmQuizDiagnosticEvent.RuntimeError:
                ShowLlmValidationStatus(
                    "!",
                    "Quiz.AiValidationRuntimeErrorTitle",
                    diagnostic.Detail ??
                        TranslateQuiz("Quiz.AiValidationRuntimeErrorFallback"),
                    "DangerSoftColor",
                    "DangerBorderBrush",
                    "DangerColor");
                break;
        }
    }

    private void ShowLlmValidationStatus(
        string icon,
        string titleKey,
        string detail,
        string backgroundResourceKey,
        string borderResourceKey,
        string foregroundResourceKey)
    {
        AiValidationStatusBorder.IsVisible = true;
        AiValidationStatusIconLabel.Text = icon;
        AiValidationStatusTitleLabel.Text = TranslateQuiz(titleKey);
        AiValidationStatusDetailLabel.Text = detail;

        AiValidationStatusBorder.SetDynamicResource(
            Border.BackgroundColorProperty,
            backgroundResourceKey);
        AiValidationStatusBorder.SetDynamicResource(
            Border.StrokeProperty,
            borderResourceKey);
        AiValidationStatusTitleLabel.SetDynamicResource(
            Label.TextColorProperty,
            foregroundResourceKey);
    }

    private static string FormatLlmJsonForDisplay(
        string rawModelOutput)
    {
        string trimmed = rawModelOutput.Trim();
        int objectStart = trimmed.IndexOf('{');
        int objectEnd = trimmed.LastIndexOf('}');

        if (objectStart < 0 ||
            objectEnd <= objectStart)
        {
            return rawModelOutput;
        }

        string json =
            trimmed[objectStart..(objectEnd + 1)];

        try
        {
            using JsonDocument document =
                JsonDocument.Parse(json);

            return JsonSerializer.Serialize(
                document.RootElement,
                PrettyJsonOptions);
        }
        catch (JsonException)
        {
            // Khi model còn streaming, JSON chưa đóng đủ ngoặc. Giữ nguyên
            // nội dung tạm thời và tự định dạng ở lần cập nhật hoàn chỉnh.
            return rawModelOutput;
        }
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
        for (int index = 0;
             index < ChoiceButtons.Length;
             index++)
        {
            Button button = ChoiceButtons[index];
            button.Text = string.Empty;
            button.CommandParameter = null;
            button.IsEnabled = false;
            ApplyNeutralAnswerStyle(button);

            FractionExpressionView fractionView =
                ChoiceFractionViews[index];
            fractionView.Expression = string.Empty;
            fractionView.IsVisible = false;
        }
    }

    private void SetQuestionContent(
        string text,
        double fontSize,
        string colorResource,
        bool useFractionFormatting)
    {
        QuestionExpressionLabel.IsVisible =
            !useFractionFormatting;
        QuestionFractionExpressionView.IsVisible =
            useFractionFormatting;

        if (useFractionFormatting)
        {
            QuestionFractionExpressionView.Expression = text;
            QuestionFractionExpressionView.MathFontSize = fontSize;
            QuestionFractionExpressionView.SetDynamicResource(
                FractionExpressionView.MathColorProperty,
                colorResource);
            return;
        }

        QuestionExpressionLabel.Text = text;
        QuestionExpressionLabel.FontSize = fontSize;
        QuestionExpressionLabel.SetDynamicResource(
            Label.TextColorProperty,
            colorResource);
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
        FindXQuizContract? findXProblem =
            _currentQuestion.FindXProblem;
        FractionQuizContract? fractionProblem =
            _currentQuestion.FractionProblem;
        ProportionQuizContract? proportionProblem =
            _currentQuestion.ProportionProblem;
        MotionQuizContract? motionProblem =
            _currentQuestion.MotionProblem;

        if (wordProblem is not null)
        {
            QuestionPromptLabel.Text =
                GetQuestionPromptTitle();
            SetQuestionContent(
                wordProblem.ProblemText,
                21,
                "TextPrimaryColor",
                useFractionFormatting:
                    fractionProblem is not null);

            if (_currentQuestion.Mode ==
                ArithmeticQuizMode.TrueFalse)
            {
                string presentedAnswer =
                    fractionProblem?.PresentedAnswer?.ToString() ??
                    _currentQuestion.PresentedAnswer
                        .GetValueOrDefault()
                        .ToString("N0", CultureInfo.CurrentCulture);

                string presentedText = string.Format(
                    CultureInfo.CurrentCulture,
                    Translate("Quiz.PresentedAnswer"),
                    presentedAnswer,
                    wordProblem.AnswerUnit);

                if (fractionProblem is not null)
                {
                    PresentedAnswerLabel.IsVisible = false;
                    PresentedAnswerFractionView.Expression =
                        presentedText;
                    PresentedAnswerFractionView.IsVisible = true;
                }
                else
                {
                    PresentedAnswerFractionView.IsVisible = false;
                    PresentedAnswerLabel.Text = presentedText;
                    PresentedAnswerLabel.IsVisible = true;
                }
            }
            else
            {
                PresentedAnswerLabel.IsVisible = false;
                PresentedAnswerFractionView.IsVisible = false;
            }
        }
        else if (proportionProblem is not null)
        {
            QuestionPromptLabel.Text =
                GetQuestionPromptTitle();
            SetQuestionContent(
                proportionProblem.ProblemText,
                21,
                "TextPrimaryColor",
                useFractionFormatting: false);

            if (_currentQuestion.Mode ==
                ArithmeticQuizMode.TrueFalse)
            {
                string presentedAnswer =
                    _currentQuestion.PresentedAnswer
                        .GetValueOrDefault()
                        .ToString("N0", CultureInfo.CurrentCulture);

                PresentedAnswerFractionView.IsVisible = false;
                PresentedAnswerLabel.Text = string.Format(
                    CultureInfo.CurrentCulture,
                    Translate("Quiz.PresentedAnswer"),
                    presentedAnswer,
                    proportionProblem.AnswerUnit);
                PresentedAnswerLabel.IsVisible = true;
            }
            else
            {
                PresentedAnswerLabel.IsVisible = false;
                PresentedAnswerFractionView.IsVisible = false;
            }
        }
        else if (motionProblem is not null)
        {
            QuestionPromptLabel.Text =
                GetQuestionPromptTitle();
            SetQuestionContent(
                motionProblem.ProblemText,
                21,
                "TextPrimaryColor",
                useFractionFormatting: false);

            if (_currentQuestion.Mode ==
                ArithmeticQuizMode.TrueFalse)
            {
                string presentedAnswer =
                    _currentQuestion.PresentedAnswer
                        .GetValueOrDefault()
                        .ToString("N0", CultureInfo.CurrentCulture);

                PresentedAnswerFractionView.IsVisible = false;
                PresentedAnswerLabel.Text = string.Format(
                    CultureInfo.CurrentCulture,
                    Translate("Quiz.PresentedAnswer"),
                    presentedAnswer,
                    motionProblem.AnswerUnit);
                PresentedAnswerLabel.IsVisible = true;
            }
            else
            {
                PresentedAnswerLabel.IsVisible = false;
                PresentedAnswerFractionView.IsVisible = false;
            }
        }
        else if (fractionProblem is not null)
        {
            PresentedAnswerLabel.IsVisible = false;
            PresentedAnswerFractionView.IsVisible = false;

            SetQuestionContent(
                _currentQuestion.Mode == ArithmeticQuizMode.TrueFalse
                    ? $"{fractionProblem.ExpressionText} = {fractionProblem.PresentedAnswer}"
                    : $"{fractionProblem.ExpressionText} = ?",
                32,
                "PrimaryColor",
                useFractionFormatting: true);
        }
        else if (findXProblem is not null)
        {
            PresentedAnswerLabel.IsVisible = false;
            PresentedAnswerFractionView.IsVisible = false;

            SetQuestionContent(
                _currentQuestion.Mode == ArithmeticQuizMode.TrueFalse
                    ? $"{findXProblem.EquationText}{Environment.NewLine}" +
                      $"x = {_currentQuestion.PresentedAnswer.GetValueOrDefault().ToString("N0", CultureInfo.CurrentCulture)}"
                    : findXProblem.EquationText,
                32,
                "PrimaryColor",
                useFractionFormatting: false);
        }
        else
        {
            PresentedAnswerLabel.IsVisible = false;
            PresentedAnswerFractionView.IsVisible = false;

            if (_currentQuestion.Mode ==
                ArithmeticQuizMode.TrueFalse)
            {
                string presentedAnswer =
                    _currentQuestion.PresentedAnswer
                        .GetValueOrDefault()
                        .ToString(
                            "N0",
                            CultureInfo.CurrentCulture);

                SetQuestionContent(
                    $"{left} {symbol} {right} = {presentedAnswer}",
                    34,
                    "PrimaryColor",
                    useFractionFormatting: false);
            }
            else
            {
                SetQuestionContent(
                    $"{left} {symbol} {right} = ?",
                    34,
                    "PrimaryColor",
                    useFractionFormatting: false);
            }
        }

        if (_currentQuestion.Mode ==
            ArithmeticQuizMode.MultipleChoice)
        {
            for (int index = 0;
                 index < ChoiceButtons.Length;
                 index++)
            {
                Button button =
                    ChoiceButtons[index];

                button.HeightRequest =
                    fractionProblem is not null
                        ? 72
                        : 54;

                char prefix =
                    (char)('A' + index);

                string choiceUnit =
                    wordProblem is not null
                        ? $" {wordProblem.AnswerUnit}"
                        : proportionProblem is not null
                            ? $" {proportionProblem.AnswerUnit}"
                            : motionProblem is not null
                                ? $" {motionProblem.AnswerUnit}"
                                : string.Empty;

                if (fractionProblem is not null)
                {
                    ReducedFraction choice =
                        fractionProblem.Choices[index];
                    button.Text = string.Empty;
                    button.CommandParameter = choice.ToString();

                    FractionExpressionView fractionView =
                        ChoiceFractionViews[index];
                    fractionView.Expression =
                        $"{prefix}. {choice}{choiceUnit}";
                    fractionView.IsVisible = true;
                }
                else
                {
                    ChoiceFractionViews[index].IsVisible = false;
                    BigInteger choice =
                        _currentQuestion.Choices[index];
                    button.Text =
                        $"{prefix}. {choice.ToString("N0", CultureInfo.CurrentCulture)}{choiceUnit}";
                    button.CommandParameter =
                        choice.ToString(CultureInfo.InvariantCulture);
                }
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
            sender is not Button button)
        {
            return;
        }

        if (_currentQuestion.FractionProblem is FractionQuizContract fraction)
        {
            if (!ReducedFraction.TryParse(
                    button.CommandParameter?.ToString(),
                    out ReducedFraction selectedFraction))
            {
                return;
            }

            CompleteAnswer(
                selectedFraction == fraction.CorrectAnswer,
                button);
            return;
        }

        if (!BigInteger.TryParse(
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
                EssaySolutionEditor.Text,
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
            bool isCorrectChoice =
                _currentQuestion.FractionProblem is FractionQuizContract fraction
                    ? ReducedFraction.TryParse(
                        button.CommandParameter?.ToString(),
                        out ReducedFraction fractionAnswer) &&
                      fractionAnswer == fraction.CorrectAnswer
                    : BigInteger.TryParse(
                        button.CommandParameter?.ToString(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out BigInteger answer) &&
                      answer == _currentQuestion.CorrectAnswer;

            if (_currentQuestion.Mode ==
                    ArithmeticQuizMode.MultipleChoice &&
                isCorrectChoice)
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

        ShowFeedback(isCorrect);

        if (!string.IsNullOrWhiteSpace(
                feedbackOverride))
        {
            FeedbackLabel.Text = feedbackOverride;
            FeedbackLabel.IsVisible = true;
            FeedbackFractionView.IsVisible = false;
        }

        if (_currentQuestion.WordProblem is MathWordProblem motionWordProblem &&
            _currentQuestion.MotionProblem is MotionQuizContract motionProblem)
        {
            string answerLabel =
                AppLanguageManager.CurrentLanguage == AppLanguage.Vietnamese
                    ? "Đáp số"
                    : "Answer";

            string solutionText =
                $"{motionWordProblem.SolutionLead}{Environment.NewLine}" +
                $"{motionProblem.SolutionText}{Environment.NewLine}" +
                $"{answerLabel}: {motionProblem.CorrectAnswer:N0} {motionWordProblem.AnswerUnit}";

            SetSolutionContent(
                solutionText,
                useFractionFormatting: false);
            SolutionBorder.IsVisible = true;
        }
        else if (_currentQuestion.WordProblem is not null)
        {
            string solutionText =
                ElementaryWordProblemSolutionFormatter.Format(
                    _currentQuestion,
                    AppLanguageManager.CurrentLanguage,
                    CultureInfo.CurrentCulture);

            SetSolutionContent(
                solutionText,
                useFractionFormatting:
                    _currentQuestion.FractionProblem is not null);
            SolutionBorder.IsVisible = true;
        }
        else if (_currentQuestion.ProportionProblem is not null ||
                 _currentQuestion.MotionProblem is not null ||
                 _currentQuestion.Mode == ArithmeticQuizMode.Essay)
        {
            string solutionText =
                FormatPlainEssaySolution(
                    _currentQuestion);

            SetSolutionContent(
                solutionText,
                useFractionFormatting:
                    _currentQuestion.FractionProblem is not null);
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

        if (!validation.SolutionIsCorrect)
        {
            return TranslateQuiz(
                validation.SolutionError ==
                    EssayAnswerError.MissingSolution
                    ? "Quiz.EssaySolutionRequired"
                    : "Quiz.EssaySolutionContentIncorrect");
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
        if (question.FractionProblem is FractionQuizContract fraction)
        {
            string fractionAnswerLabel =
                AppLanguageManager.CurrentLanguage == AppLanguage.Vietnamese
                    ? "Đáp số"
                    : "Answer";

            return
                $"{fraction.ExpressionText} = {fraction.CorrectAnswer}" +
                Environment.NewLine +
                $"{fractionAnswerLabel}: {fraction.CorrectAnswer}";
        }

        if (question.ProportionProblem is ProportionQuizContract proportion)
        {
            return FormatProportionEssaySolution(proportion);
        }

        if (question.MotionProblem is MotionQuizContract motion)
        {
            return motion.SolutionText;
        }

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

        if (question.FindXProblem is
            FindXQuizContract findX)
        {
            string xLabel =
                AppLanguageManager.CurrentLanguage ==
                    AppLanguage.Vietnamese
                    ? "Giá trị của x"
                    : "The value of x";

            return
                $"{findX.EquationText}{Environment.NewLine}" +
                $"x = {left} {symbol} {right}{Environment.NewLine}" +
                $"x = {answer}{Environment.NewLine}" +
                $"{xLabel}: {answer}";
        }

        return
            $"{left} {symbol} {right} = {answer}" +
            Environment.NewLine +
            $"{answerLabel}: {answer}";
    }

    private static string FormatProportionEssaySolution(
        ProportionQuizContract contract)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        bool vi = AppLanguageManager.CurrentLanguage == AppLanguage.Vietnamese;
        string answer = contract.CorrectAnswer.ToString("N0", culture);
        string answerLabel = vi ? "Đáp số" : "Answer";

        if (contract.IsDirect)
        {
            int unitRate = contract.B / contract.A;
            return vi
                ? $"Giá trị ứng với 1 đơn vị là:{Environment.NewLine}" +
                  $"{contract.B:N0} ÷ {contract.A:N0} = {unitRate:N0}{Environment.NewLine}" +
                  $"Giá trị ứng với {contract.C:N0} đơn vị là:{Environment.NewLine}" +
                  $"{unitRate:N0} × {contract.C:N0} = {answer}{Environment.NewLine}" +
                  $"{answerLabel}: {answer} {contract.AnswerUnit}"
                : $"Value for 1 unit:{Environment.NewLine}" +
                  $"{contract.B:N0} ÷ {contract.A:N0} = {unitRate:N0}{Environment.NewLine}" +
                  $"Value for {contract.C:N0} units:{Environment.NewLine}" +
                  $"{unitRate:N0} × {contract.C:N0} = {answer}{Environment.NewLine}" +
                  $"{answerLabel}: {answer} {contract.AnswerUnit}";
        }

        int total = contract.A * contract.B;
        if (contract.AsksForAdditionalPeople)
        {
            int newPeople = total / contract.C;
            return vi
                ? $"Tổng số người-ngày không đổi:{Environment.NewLine}" +
                  $"{contract.A:N0} × {contract.B:N0} = {total:N0}{Environment.NewLine}" +
                  $"Số người thực tế là:{Environment.NewLine}" +
                  $"{total:N0} ÷ {contract.C:N0} = {newPeople:N0}{Environment.NewLine}" +
                  $"Số người đến thêm là:{Environment.NewLine}" +
                  $"{newPeople:N0} − {contract.A:N0} = {answer}{Environment.NewLine}" +
                  $"{answerLabel}: {answer} {contract.AnswerUnit}"
                : $"The total person-days stays constant:{Environment.NewLine}" +
                  $"{contract.A:N0} × {contract.B:N0} = {total:N0}{Environment.NewLine}" +
                  $"Actual number of people:{Environment.NewLine}" +
                  $"{total:N0} ÷ {contract.C:N0} = {newPeople:N0}{Environment.NewLine}" +
                  $"Additional people:{Environment.NewLine}" +
                  $"{newPeople:N0} − {contract.A:N0} = {answer}{Environment.NewLine}" +
                  $"{answerLabel}: {answer} {contract.AnswerUnit}";
        }

        return vi
            ? $"Tích của hai đại lượng tỉ lệ nghịch không đổi:{Environment.NewLine}" +
              $"{contract.A:N0} × {contract.B:N0} = {total:N0}{Environment.NewLine}" +
              $"Giá trị cần tìm là:{Environment.NewLine}" +
              $"{total:N0} ÷ {contract.C:N0} = {answer}{Environment.NewLine}" +
              $"{answerLabel}: {answer} {contract.AnswerUnit}"
            : $"The product of the inversely proportional quantities stays constant:{Environment.NewLine}" +
              $"{contract.A:N0} × {contract.B:N0} = {total:N0}{Environment.NewLine}" +
              $"Required value:{Environment.NewLine}" +
              $"{total:N0} ÷ {contract.C:N0} = {answer}{Environment.NewLine}" +
              $"{answerLabel}: {answer} {contract.AnswerUnit}";
    }

    private void ShowFeedback(bool isCorrect)
    {
        string answerText =
            _currentQuestion?.FractionProblem?.CorrectAnswer.ToString() ??
            _currentQuestion?.CorrectAnswer.ToString(
                "N0",
                CultureInfo.CurrentCulture) ??
            string.Empty;

        if (_currentQuestion?.WordProblem is
            MathWordProblem wordProblem)
        {
            answerText +=
                $" {wordProblem.AnswerUnit}";
        }
        else if (_currentQuestion?.ProportionProblem is
                 ProportionQuizContract proportionProblem)
        {
            answerText +=
                $" {proportionProblem.AnswerUnit}";
        }
        else if (_currentQuestion?.MotionProblem is
                 MotionQuizContract motionProblem)
        {
            answerText +=
                $" {motionProblem.AnswerUnit}";
        }

        string feedbackText = string.Format(
            CultureInfo.CurrentCulture,
            Translate(
                isCorrect
                    ? "Quiz.CorrectFeedback"
                    : "Quiz.IncorrectFeedback"),
            answerText);

        bool useFractionFormatting =
            _currentQuestion?.FractionProblem is not null;

        FeedbackLabel.IsVisible = !useFractionFormatting;
        FeedbackFractionView.IsVisible = useFractionFormatting;

        if (useFractionFormatting)
        {
            FeedbackFractionView.Expression = feedbackText;
        }
        else
        {
            FeedbackLabel.Text = feedbackText;
        }

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

        FeedbackFractionView.SetDynamicResource(
            FractionExpressionView.MathColorProperty,
            isCorrect
                ? "SuccessColor"
                : "DangerColor");
    }

    private void SetSolutionContent(
        string text,
        bool useFractionFormatting)
    {
        SolutionLabel.IsVisible = !useFractionFormatting;
        SolutionFractionView.IsVisible = useFractionFormatting;

        if (useFractionFormatting)
        {
            SolutionFractionView.Expression = text;
        }
        else
        {
            SolutionLabel.Text = text;
        }
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
            GetSelectedFixedProblemRequest();
        _questionAnswered = false;
        _lastAnswerWasCorrect = null;

        QuestionExpressionLabel.Text = string.Empty;
        QuestionExpressionLabel.IsVisible = true;
        QuestionFractionExpressionView.Expression = string.Empty;
        QuestionFractionExpressionView.IsVisible = false;
        PresentedAnswerLabel.Text = string.Empty;
        PresentedAnswerLabel.IsVisible = false;
        PresentedAnswerFractionView.Expression = string.Empty;
        PresentedAnswerFractionView.IsVisible = false;
        FeedbackLabel.Text = string.Empty;
        FeedbackLabel.IsVisible = true;
        FeedbackFractionView.Expression = string.Empty;
        FeedbackFractionView.IsVisible = false;
        FeedbackBorder.IsVisible = false;
        SolutionLabel.Text = string.Empty;
        SolutionLabel.IsVisible = true;
        SolutionFractionView.Expression = string.Empty;
        SolutionFractionView.IsVisible = false;
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
        HideAiTeacherGreeting();
    }

    private void ResetQuizSessionState()
    {
        _pendingLlmQuestionNumberOnSuccess =
            null;
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

    private void ApplyNeutralAnswerStyle(
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
        SetChoiceFractionColor(button, "TextPrimaryColor");
    }

    private void ApplyCorrectAnswerStyle(
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
        SetChoiceFractionColor(button, "SuccessColor");
    }

    private void ApplyIncorrectAnswerStyle(
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
        SetChoiceFractionColor(button, "DangerColor");
    }

    private void SetChoiceFractionColor(
        Button button,
        string colorResource)
    {
        int index = Array.IndexOf(ChoiceButtons, button);
        if ((uint)index >= (uint)ChoiceFractionViews.Length)
        {
            return;
        }

        ChoiceFractionViews[index].SetDynamicResource(
            FractionExpressionView.MathColorProperty,
            colorResource);
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
