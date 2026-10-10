using MathSolver.Controls;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.Localization;
using MathSolver.Services.QuestionBank;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Views;

public partial class MathPuzzlePage : ContentPage
{
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
    private readonly AverageQuizGenerator _averageQuizGenerator;
    private readonly PercentageQuizGenerator _percentageQuizGenerator;
    private readonly QuizProblemTypeCatalog _quizProblemTypeCatalog = new();
    private readonly ExpressionQuizGenerator _expressionQuizGenerator = new();
    private readonly ElementaryQuizGenerator _elementaryQuizGenerator = new();
    private ElementaryQuizType? _selectedElementaryType;
    private bool _selectedBasicComparison;
    private bool _selectedFractionComparison;
    private readonly List<ElementaryQuizType?> _elementaryTypePickerValues = [];
    private QuizProblemKind? _elementaryPickerKind;
    private AppLanguage? _elementaryPickerLanguage;
    private bool _singleColumnChoices;
    private ExpressionQuizType? _selectedExpressionType;
    private readonly EssayAnswerValidator _essayAnswerValidator;
    private ArithmeticQuizMode _selectedMode =
        ArithmeticQuizMode.TrueFalse;

    private CurriculumTier _selectedCurriculumTier =
        CurriculumTier.ThreeStars;

    private ArithmeticQuizQuestion? _currentQuestion;
    private QuizProblemRequest? _activeProblemRequest;
    private bool _questionAnswered;
    private bool? _lastAnswerWasCorrect;
    private bool _isUpdatingOperationPicker;
    private ArithmeticOperation? _selectedBasicOperation =
        ArithmeticOperation.Add;
    private FractionOperation? _selectedFractionOperation =
        FractionOperation.Add;
    private ProportionQuizType? _selectedProportionType =
        ProportionQuizType.Direct;
    private AverageQuizType? _selectedAverageType;
    private PercentageQuizType? _selectedPercentageType;
    private ArithmeticOperation? _selectedFindXOperation;
    private GeometryQuizShape? _selectedGeometryShape;
    private readonly List<GeometryQuizShape?> _geometryShapePickerValues = [];
    private GeometryMeasurement? _selectedGeometryMeasurement;
    private readonly List<GeometryMeasurement?> _geometryMeasurementPickerValues = [];
    private MotionQuizType? _selectedMotionType;
    private bool _isUpdatingSubtypePickers;
    private int _questionCount;
    private int _correctCount;
    private int _incorrectCount;
    private int _mainTabAnimationVersion;
    private int _questionGenerationVersion;
    private bool _openingAiQuestionBank;

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
        Loaded += OnPracticePageLoaded;
        Unloaded += OnPracticePageUnloaded;

        // Fraction overlays share their accessible description with the button.
        foreach (var view in ChoiceFractionViews) AutomationProperties.SetExcludedWithChildren(view, true);

        IllustratedQuizPicker.Attach(OperationPicker, "Choice.SelectProblem");
        foreach (var picker in new[] { ElementaryTypePicker, ExpressionTypePicker, AverageTypePicker,
                     PercentageTypePicker, FindXTypePicker, GeometryShapePicker, GeometryMeasurementPicker, MotionTypePicker })
            IllustratedQuizPicker.Attach(picker, "Choice.SelectSubtype");

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

        _averageQuizGenerator =
            new AverageQuizGenerator();

        _percentageQuizGenerator =
            new PercentageQuizGenerator();

        LocalizationService.ExcludeSubtreeFromLegacyTracking(
            this);

        LocalizationService.CultureChanged +=
            OnCultureChanged;

        // MathPuzzlePage là ShellContent sống suốt vòng đời tab. Khi đổi theme
        // từ Settings popup, trang có thể nhận OnDisappearing nhưng không bị
        // destroy; vì vậy giữ subscription ở cùng lifetime với CultureChanged.
        AppThemeManager.ThemeChanged +=
            OnThemeChanged;

        UpdateQuestionModeLayout();
        RefreshLearningPickers();
        UpdateOperationPickerItems();
        UpdateModeStyles();
        UpdateCurriculumTierStyles();
        UpdateRegenerateQuestionButtonState();
        UpdateScoreLabels();
    }

    protected override void OnAppearing()
    {
        _practicePageVisible = true;
        base.OnAppearing();
        LiveWallpaper.Resume();
        Shell.SetTabBarIsVisible(this, true);
        AiQuestionBank.Current.Generation.Changed += OnAiQuestionBankProgress;
        UpdateAiQuestionBankProgress();
        RefreshStatefulButtonTheme();
        BeginMainTabTransitionIfPending();

        if (_currentQuestion is null)
            GenerateAlgorithmQuestion();
        else
        {
            RenderCurrentQuestion(resetAnswerControls: false);
            UpdateScoreLabels();
        }
        // Shell can show an already loaded page without raising Loaded again.
        if (IsLoaded) OnPracticePageLoaded(this, EventArgs.Empty);
    }

    protected override void OnDisappearing()
    {
        _practicePageVisible = false;
        _practiceLayoutVersion++;
        _practiceScrollVersion++;
        _diagramScrollVersion++;
        AiQuestionBank.Current.Generation.Changed -= OnAiQuestionBankProgress;
        LiveWallpaper.Pause();
        if (_openingAiQuestionBank || _diagramPreviewOpen || SettingsMenuPage.IsTransparentOverlayActive)
        {
            base.OnDisappearing();
            return;
        }

        ResetQuizSessionState();
        _mainTabAnimationVersion++;
        SetPracticeSettingsExpanded(true);
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
        // lại DynamicResource để các nút giữ đúng màu của theme hiện tại.
        Dispatcher.Dispatch(
            RefreshStatefulButtonTheme);
    }

    private void RefreshStatefulButtonTheme()
    {

        UpdateModeStyles();
        UpdateCurriculumTierStyles();
        UpdateProblemOperationPanel();
        RefreshTrueFalseAnswerButtonTheme();
        RefreshQuestionActionButtonTheme();
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
            "WallpaperSuccessSoftColor",
            "SuccessColor");

        ApplySemanticAnswerButtonTheme(
            FalseAnswerButton,
            "WallpaperDangerSoftColor",
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
                backgroundResourceKey == "WallpaperSuccessSoftColor"
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

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        Dispatcher.Dispatch(() =>
        {
            ResetQuizSessionState();
            RefreshLearningPickers();
            UpdateOperationPickerItems();
            UpdateCurriculumTierStyles();
            UpdateScoreLabels();
            GenerateAlgorithmQuestion();
        });
    }

    private void UpdateQuestionModeLayout()
    {
        // Logical width also covers Android tablets and narrow Windows windows.
        bool compact = _compactSettings ?? true;
        QuestionModeGrid.ColumnDefinitions.Clear();
        QuestionModeGrid.RowDefinitions.Clear();

        QuestionModeGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Star
            });

        if (!compact) QuestionModeGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        QuestionModeGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Star
            });

        if (compact) QuestionModeGrid.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        QuestionModeGrid.RowDefinitions.Add(
            new RowDefinition
            {
                Height = GridLength.Auto
            });

        Grid.SetRow(TrueFalseModeButton, 0);
        Grid.SetColumn(TrueFalseModeButton, 0);
        Grid.SetColumnSpan(TrueFalseModeButton, 1);

        Grid.SetRow(MultipleChoiceModeButton, 0);
        Grid.SetColumn(MultipleChoiceModeButton, 1);
        Grid.SetColumnSpan(MultipleChoiceModeButton, 1);

        Grid.SetRow(EssayModeButton, compact ? 1 : 0);
        Grid.SetColumn(EssayModeButton, compact ? 0 : 2);
        Grid.SetColumnSpan(EssayModeButton, compact ? 2 : 1);
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

        _selectedMode = mode;
        ResetQuizSessionState();
        UpdateModeStyles();

        GenerateAlgorithmQuestion();
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

    private void OnCurriculumOneStarClicked(object? sender, EventArgs e) =>
        SelectCurriculumTier(CurriculumTier.OneStar);

    private void OnCurriculumTwoStarsClicked(object? sender, EventArgs e) =>
        SelectCurriculumTier(CurriculumTier.TwoStars);

    private void OnCurriculumThreeStarsClicked(object? sender, EventArgs e) =>
        SelectCurriculumTier(CurriculumTier.ThreeStars);

    private void OnCurriculumFourStarsClicked(object? sender, EventArgs e) =>
        SelectCurriculumTier(CurriculumTier.FourStars);

    private void OnCurriculumFiveStarsClicked(object? sender, EventArgs e) =>
        SelectCurriculumTier(CurriculumTier.FiveStars);

    private void SelectCurriculumTier(CurriculumTier tier)
    {
        if (_selectedCurriculumTier == tier)
        {
            return;
        }

        _selectedCurriculumTier = tier;
        ResetQuizSessionState();
        UpdateCurriculumTierStyles();
        UpdateProblemOperationPanel();
        UpdateEssayAnswerPresentation();

        // Không rebuild Picker/subtype khi đổi sao. Skill Mode luôn giữ nguyên
        // các lựa chọn hiện có; số sao chỉ thay đổi constraint của generator.
        // Cách này tránh re-entrant SelectionChanged/flyout trên WinUI.
        GenerateAlgorithmQuestion();
    }

    private QuizCurriculumContext GetCurriculumContext() =>
        new(
            _selectedCurriculumTier,
            IsMixedProblemSelection());

    private bool IsMixedProblemSelection() =>
        _quizProblemTypeCatalog
            .GetFixedRequest(OperationPicker.SelectedIndex) is null;

    private void EnsureCurriculumTierAvailableForSelection()
    {
        // Skill Mode cho phép đủ ★..★★★★★. Mixed Mode tự giới hạn skill
        // trong QuizCurriculumLayer.ResolveMixedRequest, không khóa UI.
    }

    private void NormalizeSelectedSkillForCurriculum()
    {
        // Không thay đổi selection của subtype theo sao. Đây là chủ ý: khi
        // người dùng đã chọn một skill, generator điều chỉnh dữ kiện và cấu
        // trúc suy luận theo sao, giữ nguyên subtype được chọn.
    }

    private void UpdateCurriculumTierStyles()
    {
        Button[] buttons =
        [
            CurriculumOneStarButton,
            CurriculumTwoStarsButton,
            CurriculumThreeStarsButton,
            CurriculumFourStarsButton,
            CurriculumFiveStarsButton
        ];

        SelectionButtonStyler.Select(
            buttons[(int)_selectedCurriculumTier - 1],
            buttons);

        bool mixed = IsMixedProblemSelection();
        CurriculumHintLabel.Text = TranslateQuiz(
            _quizProblemTypeCatalog.GetFixedRequest(OperationPicker.SelectedIndex)?.Kind == QuizProblemKind.Expression
                ? "Quiz.ExpressionDifficultyHint" :
            mixed
                ? "Quiz.CurriculumHintMixed"
                : "Quiz.CurriculumHintSkill");
    }

    private string GetQuestionPromptTitle()
    {
        if (_currentQuestion?.ElementaryProblem is ElementaryQuizContract elementary)
            return elementary.IsComparison
                ? TranslateQuiz(_currentQuestion.Mode == ArithmeticQuizMode.TrueFalse
                    ? "Quiz.ComparisonTrueFalseTitle" : "Quiz.ComparisonQuestionTitle")
                : TranslateQuiz("Quiz.Problem" + elementary.Kind);
        if (_currentQuestion?.ExpressionProblem is not null)
            return TranslateQuiz("Quiz.ExpressionQuestionTitle");
        if (_currentQuestion?.FractionProblem is not null)
        {
            return TranslateQuiz("Quiz.FractionQuestionTitle");
        }

        if (_currentQuestion?.FindXProblem is not null)
        {
            return TranslateQuiz("Quiz.FindXQuestionTitle");
        }

        if (_currentQuestion?.GeometryProblem is not null)
        {
            return Translate("Quiz.GeometryQuestionTitle");
        }

        if (_currentQuestion?.ProportionProblem is not null)
        {
            return TranslateQuiz("Quiz.ProportionQuestionTitle");
        }

        if (_currentQuestion?.MotionProblem is not null)
        {
            return TranslateQuiz("Quiz.MotionQuestionTitle");
        }

        if (_currentQuestion?.AverageProblem is not null)
        {
            return TranslateQuiz("Quiz.AverageQuestionTitle");
        }

        if (_currentQuestion?.PercentageProblem is not null)
        {
            return TranslateQuiz("Quiz.PercentageQuestionTitle");
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
        ArithmeticQuizQuestion? question = _currentQuestion;
        bool requiresSolution =
            question is not null &&
            EssayAnswerValidator.RequiresSolution(question);
        // Mixed can resolve to any kind on each new question. Once generated,
        // the question contract, not the picker, owns the essay presentation.
        bool isFindX = question is not null
            ? question.FindXProblem is not null && question.WordProblem is null
            : IsFindXProblemSelected();
        bool isFraction = question is not null
            ? question.UsesFractionFormatting
            : IsFractionProblemSelected();
        bool isGeometry = question is not null
            ? question.GeometryProblem is not null
            : IsGeometryProblemSelected();
        bool isProportion = question is not null
            ? question.ProportionProblem is not null
            : IsProportionProblemSelected();
        bool isMotion = question is not null
            ? question.MotionProblem is not null
            : IsMotionProblemSelected();
        bool isAverage = question?.AverageProblem is not null;
        PercentageQuizType? percentageType = question?.PercentageProblem?.Type;

        EssayWorkLabel.Text = TranslateQuiz(
            requiresSolution
                ? "Quiz.EssayCombinedLabel"
                : "Quiz.EssayCombinedNumericLabel");

        string expectedEquationUnit = question is null
            ? string.Empty
            : EssayAnswerValidator.GetExpectedUnit(question);

        EssayValidationHintLabel.Text =
            TranslateQuiz(
                question?.AverageProblem?.Type == AverageQuizType.IndirectData
                    ? "Quiz.AverageIndirectEssayHint"
                    : question?.ExpressionProblem is not null
                    ? "Quiz.ExpressionEssayHint"
                    : isFindX
                    ? "Quiz.FindXEssayValidationHint"
                    : isGeometry
                    ? "Quiz.GeometryEssayValidationHint"
                    : isFraction
                    ? "Quiz.FractionEssayValidationHint"
                    : isProportion
                    ? "Quiz.ProportionEssayValidationHint"
                    : isMotion
                    ? "Quiz.MotionEssayValidationHint"
                    : requiresSolution
                        ? "Quiz.EssayValidationHintAlgorithmWordProblem"
                        : "Quiz.EssayValidationHintAlgorithm");
        if (!string.IsNullOrWhiteSpace(expectedEquationUnit))
        {
            EssayValidationHintLabel.Text += " " +
                TranslateQuiz("Quiz.EssayEquationUnitHint");
        }
        EssayValidationHintLabel.Text += " " +
            TranslateQuiz("Quiz.EssayCombinedAnswerHint");

        string equationPlaceholder =
            question?.ExpressionProblem is not null
                ? TranslateQuiz(isFraction ? "Quiz.ExpressionFractionPlaceholder" : "Quiz.ExpressionIntegerPlaceholder")
                : isFindX
                ? TranslateQuiz("Quiz.FindXEssayEquationPlaceholder")
                : isFraction
                ? TranslateQuiz("Quiz.FractionEssayEquationPlaceholder")
                : isGeometry
                ? Translate("Quiz.GeometryEssayEquationPlaceholder")
                : isProportion
                ? TranslateQuiz("Quiz.ProportionEssayEquationPlaceholder")
                : isMotion
                ? TranslateQuiz("Quiz.MotionEssayEquationPlaceholder")
                : isAverage
                ? TranslateQuiz("Quiz.AverageEssayEquationPlaceholder")
                : percentageType == PercentageQuizType.FindPercentageRatio
                ? TranslateQuiz("Quiz.PercentageRatioEssayEquationPlaceholder")
                : percentageType == PercentageQuizType.FindPercentageValue
                ? TranslateQuiz("Quiz.PercentageValueEssayEquationPlaceholder")
                : percentageType == PercentageQuizType.FindWholeFromPercentageValue
                ? TranslateQuiz("Quiz.PercentageWholeEssayEquationPlaceholder")
                : Translate("Quiz.EssayEquationPlaceholder");
        if (!string.IsNullOrWhiteSpace(expectedEquationUnit))
        {
            equationPlaceholder +=
                expectedEquationUnit == "%"
                    ? "%"
                    : " " + expectedEquationUnit;
        }

        EssayWorkEditor.Placeholder = requiresSolution
            ? TranslateQuiz("Quiz.EssayCombinedPlaceholder") +
              Environment.NewLine + equationPlaceholder
            : equationPlaceholder;

        EssayWorkEditor.Placeholder += Environment.NewLine +
            TranslateQuiz("Quiz.EssayCombinedAnswerPlaceholder") +
            (string.IsNullOrWhiteSpace(expectedEquationUnit) ? string.Empty : " " + expectedEquationUnit);
        if (question?.ElementaryProblem is ElementaryQuizContract elementary)
        {
            if (elementary.Kind != QuizProblemKind.Decimal)
            {
                EssayValidationHintLabel.Text = TranslateQuiz("Quiz.ElementaryEssayHint");
                EssayWorkEditor.Placeholder = TranslateQuiz(requiresSolution ? "Quiz.ElementaryEssayPlaceholder" : "Quiz.ElementaryAnswerPlaceholder");
            }
            if (elementary.IsComparison)
            {
                if (elementary.Kind == QuizProblemKind.Decimal)
                    EssayWorkLabel.Text = TranslateQuiz("Quiz.EssayAnswerLabel");
                EssayValidationHintLabel.Text = TranslateQuiz("Quiz.ComparisonEssayHint");
                EssayWorkEditor.Placeholder = TranslateQuiz("Quiz.ComparisonEssayPlaceholder");
            }
            else if (elementary.Type == ElementaryQuizType.ReadClock)
            {
                if (!requiresSolution) EssayWorkLabel.Text = TranslateQuiz("Quiz.ClockAnswerLabel");
                EssayValidationHintLabel.Text = TranslateQuiz(requiresSolution ? "Quiz.ClockEssayHint" : "Quiz.ClockAnswerHint");
                EssayWorkEditor.Placeholder = TranslateQuiz(requiresSolution ? "Quiz.ClockEssayPlaceholder" : "Quiz.ClockAnswerPlaceholder");
            }
            else if (elementary.IsDecimalArithmetic && !elementary.IsNumericDecimalCalculation)
            {
                EssayValidationHintLabel.Text = TranslateQuiz("Quiz.ElementaryEssayHint");
                EssayWorkEditor.Placeholder = TranslateQuiz("Quiz.ElementaryEssayPlaceholder");
            }
            else if (elementary.IsNumericDecimalCalculation)
            {
                string unit = string.IsNullOrWhiteSpace(expectedEquationUnit) ? "" : " " + expectedEquationUnit;
                EssayWorkEditor.Placeholder = (requiresSolution
                    ? TranslateQuiz("Quiz.EssayCombinedPlaceholder") + Environment.NewLine : "")
                    + elementary.FormatDecimalCalculation("…" + unit) + Environment.NewLine
                    + TranslateQuiz("Quiz.EssayCombinedAnswerPlaceholder") + unit;
            }
            else if (elementary.Type == ElementaryQuizType.DecimalRound)
            {
                EssayWorkLabel.Text = TranslateQuiz("Quiz.EssayAnswerLabel");
                EssayValidationHintLabel.Text = TranslateQuiz("Quiz.EssayCombinedAnswerHint");
                EssayWorkEditor.Placeholder = TranslateQuiz("Quiz.EssayCombinedAnswerPlaceholder");
            }
            else if (!elementary.RequiresSolution && (!elementary.RequiresCalculation || elementary.Answers.All(answer => answer.IsText)))
            {
                EssayWorkLabel.Text = TranslateQuiz("Quiz.EssayAnswerLabel");
                EssayValidationHintLabel.Text = QuizContentCatalog.Text(elementary.Language, "Curriculum.AnswerHint");
                EssayWorkEditor.Placeholder = TranslateQuiz("Quiz.EssayCombinedAnswerPlaceholder");
            }
        }
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
            IllustratedQuizPicker.SetKeys(OperationPicker, _quizProblemTypeCatalog.Options.Select(option => option.LocalizationKey));

            if (selectedIndex >= OperationPicker.Items.Count)
            {
                selectedIndex = 0;
            }

            OperationPicker.SelectedIndex =
                Math.Clamp(
                    selectedIndex,
                    0,
                    OperationPicker.Items.Count - 1);

            EnsureCurriculumTierAvailableForSelection();
            NormalizeSelectedSkillForCurriculum();
            UpdateSubtypePickerItems();

            _activeProblemRequest =
                GetSelectedFixedProblemRequest();

            UpdateCurriculumTierStyles();
            UpdateProblemOperationPanel();
        }
        finally
        {
            _isUpdatingOperationPicker = false;
        }
    }

    private void UpdateSubtypePickerItems()
    {
        _isUpdatingSubtypePickers = true;
        try
        {
            ExpressionTypePicker.Items.Clear();
            ExpressionTypePicker.Items.Add(TranslateQuiz("Quiz.SubtypeMixed"));
            foreach (string key in new[] { "Quiz.ExpressionInteger", "Quiz.ExpressionIntegerBrackets",
                         "Quiz.ExpressionFraction", "Quiz.ExpressionFractionBrackets" })
                ExpressionTypePicker.Items.Add(TranslateQuiz(key));
            IllustratedQuizPicker.SetKeys(ExpressionTypePicker, new[] { "Quiz.SubtypeMixed", "Quiz.ExpressionInteger",
                "Quiz.ExpressionIntegerBrackets", "Quiz.ExpressionFraction", "Quiz.ExpressionFractionBrackets" });
            ExpressionTypePicker.SelectedIndex = _selectedExpressionType.HasValue
                ? (int)_selectedExpressionType.Value + 1 : 0;
            AverageTypePicker.Items.Clear();
            string[] averageKeys =
            [
                "Quiz.SubtypeMixed",
                "Quiz.AverageDirect",
                "Quiz.AverageTotalToAverage",
                "Quiz.AverageAverageToTotal",
                "Quiz.AverageMissingValue",
                "Quiz.AverageIndirectData",
                "Quiz.AverageTwoGroups"
            ];
            foreach (string key in averageKeys)
            {
                AverageTypePicker.Items.Add(TranslateQuiz(key));
            }
            IllustratedQuizPicker.SetKeys(AverageTypePicker, averageKeys);
            AverageTypePicker.SelectedIndex = _selectedAverageType switch
            {
                null => 0,
                AverageQuizType.Direct => 1,
                AverageQuizType.TotalToAverage => 2,
                AverageQuizType.AverageToTotal => 3,
                AverageQuizType.MissingValue => 4,
                AverageQuizType.IndirectData => 5,
                AverageQuizType.TwoGroups => 6,
                _ => 0
            };

            PercentageTypePicker.Items.Clear();
            string[] percentageKeys =
            [
                "Quiz.SubtypeMixed",
                "Quiz.PercentageRatio",
                "Quiz.PercentageValue",
                "Quiz.PercentageWhole"
            ];
            foreach (string key in percentageKeys)
            {
                PercentageTypePicker.Items.Add(TranslateQuiz(key));
            }
            IllustratedQuizPicker.SetKeys(PercentageTypePicker, percentageKeys);
            PercentageTypePicker.SelectedIndex = _selectedPercentageType switch
            {
                null => 0,
                PercentageQuizType.FindPercentageRatio => 1,
                PercentageQuizType.FindPercentageValue => 2,
                PercentageQuizType.FindWholeFromPercentageValue => 3,
                _ => 0
            };

            MotionTypePicker.Items.Clear();
            string[] motionKeys =
            [
                "Quiz.SubtypeMixed",
                "Quiz.MotionBasic",
                "Quiz.MotionChasing",
                "Quiz.MotionMeeting",
                "Quiz.MotionRiver"
            ];
            foreach (string key in motionKeys)
            {
                MotionTypePicker.Items.Add(TranslateQuiz(key));
            }
            IllustratedQuizPicker.SetKeys(MotionTypePicker, motionKeys);
            MotionTypePicker.SelectedIndex = _selectedMotionType switch
            {
                null => 0,
                MotionQuizType.Basic => 1,
                MotionQuizType.Chasing => 2,
                MotionQuizType.Meeting => 3,
                MotionQuizType.River => 4,
                _ => 0
            };

            FindXTypePicker.Items.Clear();
            string[] findXKeys =
            [
                "Quiz.SubtypeMixed",
                "Quiz.FindXSum",
                "Quiz.FindXDifference",
                "Quiz.FindXProduct",
                "Quiz.FindXQuotient"
            ];
            foreach (string key in findXKeys)
            {
                FindXTypePicker.Items.Add(TranslateQuiz(key));
            }
            IllustratedQuizPicker.SetKeys(FindXTypePicker, findXKeys);
            FindXTypePicker.SelectedIndex = _selectedFindXOperation switch
            {
                null => 0,
                ArithmeticOperation.Add => 1,
                ArithmeticOperation.Subtract => 2,
                ArithmeticOperation.Multiply => 3,
                ArithmeticOperation.Divide => 4,
                _ => 0
            };

            // Danh sách hình học là tĩnh ở Skill Mode. Không clear/filter theo
            // số sao; Curriculum chỉ lọc Geometry khi chính Mixed Mode chọn
            // skill. Điều này tránh thay ItemsSource trong SelectionChanged
            // và loại bỏ một nguồn treo native Picker trên WinUI.
            GeometryShapePicker.Items.Clear();
            _geometryShapePickerValues.Clear();

            (GeometryQuizShape? Shape, string Key)[] geometryOptions =
            [
                (null, "Quiz.SubtypeMixed"),
                (GeometryQuizShape.Square, "Quiz.GeometrySquare"),
                (GeometryQuizShape.Rectangle, "Quiz.GeometryRectangle"),
                (GeometryQuizShape.Triangle, "Quiz.GeometryTriangle"),
                (GeometryQuizShape.Trapezoid, "Quiz.GeometryTrapezoid"),
                (GeometryQuizShape.Rhombus, "Quiz.GeometryRhombus"),
                (GeometryQuizShape.Parallelogram, "Quiz.GeometryParallelogram"),
                (GeometryQuizShape.Circle, "Quiz.GeometryCircle"),
                (GeometryQuizShape.Cube, "Quiz.GeometryCube"),
                (GeometryQuizShape.RectangularPrism, "Quiz.GeometryRectangularPrism")
            ];

            foreach ((GeometryQuizShape? shape, string key) in geometryOptions)
            {
                GeometryShapePicker.Items.Add(TranslateQuiz(key));
                _geometryShapePickerValues.Add(shape);
            }
            IllustratedQuizPicker.SetKeys(GeometryShapePicker, geometryOptions.Select(option => option.Key));

            int geometrySelectedIndex =
                _geometryShapePickerValues.IndexOf(_selectedGeometryShape);
            GeometryShapePicker.SelectedIndex =
                geometrySelectedIndex >= 0 ? geometrySelectedIndex : 0;
            UpdateGeometryMeasurementPickerItems();
        }
        finally
        {
            _isUpdatingSubtypePickers = false;
        }
    }

    private void UpdateGeometryMeasurementPickerItems()
    {
        bool wasUpdating = _isUpdatingSubtypePickers;
        _isUpdatingSubtypePickers = true;
        try
        {
            IReadOnlyList<GeometryMeasurement> available =
                GeometryQuizGenerator.GetAvailableMeasurements(_selectedGeometryShape);
            if (_selectedGeometryMeasurement.HasValue && !available.Contains(_selectedGeometryMeasurement.Value))
                _selectedGeometryMeasurement = null;

            GeometryMeasurementPicker.Items.Clear();
            _geometryMeasurementPickerValues.Clear();
            GeometryMeasurementPicker.Items.Add(TranslateQuiz("Quiz.SubtypeMixed"));
            _geometryMeasurementPickerValues.Add(null);
            var illustratedMeasurementKeys = new List<string> { "Quiz.SubtypeMixed" };
            foreach (GeometryMeasurement measurement in available)
            {
                string key = measurement switch
                {
                    GeometryMeasurement.Perimeter => "Quiz.GeometryPerimeter",
                    GeometryMeasurement.Area => "Quiz.GeometryArea",
                    GeometryMeasurement.Volume => "Quiz.GeometryVolume",
                    GeometryMeasurement.LateralArea => "Quiz.GeometryLateralArea",
                    GeometryMeasurement.TotalArea => "Quiz.GeometryTotalArea",
                    _ => throw new ArgumentOutOfRangeException(nameof(measurement))
                };
                GeometryMeasurementPicker.Items.Add(TranslateQuiz(key));
                illustratedMeasurementKeys.Add(key);
                _geometryMeasurementPickerValues.Add(measurement);
            }
            IllustratedQuizPicker.SetKeys(GeometryMeasurementPicker, illustratedMeasurementKeys);
            GeometryMeasurementPicker.SelectedIndex =
                _geometryMeasurementPickerValues.IndexOf(_selectedGeometryMeasurement);
        }
        finally
        {
            _isUpdatingSubtypePickers = wasUpdating;
        }
    }

    private void OnGeometryMeasurementChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingSubtypePickers)
            return;
        int index = GeometryMeasurementPicker.SelectedIndex;
        GeometryMeasurement? selected = index >= 0 && index < _geometryMeasurementPickerValues.Count
            ? _geometryMeasurementPickerValues[index] : null;
        if (selected == _selectedGeometryMeasurement)
            return;
        _selectedGeometryMeasurement = selected;
        OnSubtypeSelectionChanged(QuizProblemKind.Geometry);
    }

    private void OnAverageTypeChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingSubtypePickers)
        {
            return;
        }

        AverageQuizType? selected = AverageTypePicker.SelectedIndex switch
        {
            1 => AverageQuizType.Direct,
            2 => AverageQuizType.TotalToAverage,
            3 => AverageQuizType.AverageToTotal,
            4 => AverageQuizType.MissingValue,
            5 => AverageQuizType.IndirectData,
            6 => AverageQuizType.TwoGroups,
            _ => null
        };

        if (_selectedAverageType == selected)
        {
            return;
        }

        _selectedAverageType = selected;
        OnSubtypeSelectionChanged(QuizProblemKind.Average);
    }

    private void OnPercentageTypeChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingSubtypePickers)
        {
            return;
        }

        PercentageQuizType? selected = PercentageTypePicker.SelectedIndex switch
        {
            1 => PercentageQuizType.FindPercentageRatio,
            2 => PercentageQuizType.FindPercentageValue,
            3 => PercentageQuizType.FindWholeFromPercentageValue,
            _ => null
        };

        if (_selectedPercentageType == selected)
        {
            return;
        }

        _selectedPercentageType = selected;
        OnSubtypeSelectionChanged(QuizProblemKind.Percentage);
    }

    private void OnMotionTypeChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingSubtypePickers)
        {
            return;
        }

        MotionQuizType? selected = MotionTypePicker.SelectedIndex switch
        {
            1 => MotionQuizType.Basic,
            2 => MotionQuizType.Chasing,
            3 => MotionQuizType.Meeting,
            4 => MotionQuizType.River,
            _ => null
        };

        if (_selectedMotionType == selected)
        {
            return;
        }

        _selectedMotionType = selected;
        OnSubtypeSelectionChanged(QuizProblemKind.Motion);
    }

    private void OnFindXTypeChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingSubtypePickers)
        {
            return;
        }

        ArithmeticOperation? selected = FindXTypePicker.SelectedIndex switch
        {
            1 => ArithmeticOperation.Add,
            2 => ArithmeticOperation.Subtract,
            3 => ArithmeticOperation.Multiply,
            4 => ArithmeticOperation.Divide,
            _ => null
        };

        if (_selectedFindXOperation == selected)
        {
            return;
        }

        _selectedFindXOperation = selected;
        OnSubtypeSelectionChanged(QuizProblemKind.FindX);
    }

    private void OnGeometryShapeChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingSubtypePickers)
        {
            return;
        }

        GeometryQuizShape? selected =
            GeometryShapePicker.SelectedIndex >= 0 &&
            GeometryShapePicker.SelectedIndex < _geometryShapePickerValues.Count
                ? _geometryShapePickerValues[GeometryShapePicker.SelectedIndex]
                : null;

        if (_selectedGeometryShape == selected)
        {
            return;
        }

        _selectedGeometryShape = selected;
        UpdateGeometryMeasurementPickerItems();
        OnSubtypeSelectionChanged(
            QuizProblemKind.Geometry,
            refreshSubtypePickers: false);
    }

    private void OnSubtypeSelectionChanged(
        QuizProblemKind expectedKind,
        bool refreshSubtypePickers = false)
    {
        QuizProblemKind? kind = _quizProblemTypeCatalog
            .GetFixedRequest(OperationPicker.SelectedIndex)
            ?.Kind;
        if (kind != expectedKind)
        {
            return;
        }

        NormalizeSelectedSkillForCurriculum();

        if (refreshSubtypePickers)
        {
            UpdateSubtypePickerItems();
        }

        ResetQuizSessionState();
        _activeProblemRequest = GetSelectedFixedProblemRequest();
        UpdateEssayAnswerPresentation();

        GenerateAlgorithmQuestion();
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

        ResetQuizSessionState();

        EnsureCurriculumTierAvailableForSelection();
        NormalizeSelectedSkillForCurriculum();

        // Các subtype là danh sách tĩnh ở Skill Mode. Không rebuild Picker
        // ngay trong OperationPicker.SelectionChanged để tránh re-entrant UI.
        _activeProblemRequest =
            GetSelectedFixedProblemRequest();

        UpdateCurriculumTierStyles();
        UpdateProblemOperationPanel();

        UpdateEssayAnswerPresentation();

        GenerateAlgorithmQuestion();
    }

    private QuizProblemRequest ResolveSelectedProblem() =>
        _quizProblemTypeCatalog.Resolve(
            OperationPicker.SelectedIndex,
            _selectedBasicOperation,
            _selectedFractionOperation,
            _selectedProportionType,
            _selectedAverageType,
            _selectedPercentageType,
            _selectedFindXOperation,
            _selectedGeometryShape,
            _selectedMotionType,
            _selectedCurriculumTier,
            includeExpressions: true,
            expressionType: _selectedExpressionType,
            geometryMeasurement: _selectedGeometryMeasurement,
            elementaryType: _selectedElementaryType,
            basicComparison: _selectedBasicComparison,
            fractionComparison: _selectedFractionComparison);

    private QuizProblemRequest? GetSelectedFixedProblemRequest()
    {
        QuizProblemRequest? request =
            _quizProblemTypeCatalog.GetFixedRequest(
                OperationPicker.SelectedIndex);

        return request?.Kind switch
        {
            QuizProblemKind.Expression => request.Value with { ExpressionType = _selectedExpressionType },
            QuizProblemKind.Arithmetic =>
                request.Value with
                {
                    ArithmeticOperation = _selectedBasicOperation,
                    IsComparison = _selectedBasicComparison
                },
            QuizProblemKind.Fraction =>
                request.Value with
                {
                    FractionOperation = _selectedFractionOperation,
                    IsComparison = _selectedFractionComparison
                },
            QuizProblemKind.Proportion =>
                request.Value with
                {
                    ProportionType = _selectedProportionType
                },
            QuizProblemKind.Average =>
                request.Value with
                {
                    AverageType = _selectedAverageType
                },
            QuizProblemKind.Percentage =>
                request.Value with
                {
                    PercentageType = _selectedPercentageType
                },
            QuizProblemKind.FindX =>
                request.Value with
                {
                    FindXOperation = _selectedFindXOperation
                },
            QuizProblemKind.Geometry =>
                request.Value with
                {
                    GeometryShape = _selectedGeometryShape,
                    GeometryMeasurement = _selectedGeometryMeasurement
                },
            QuizProblemKind.Motion =>
                request.Value with
                {
                    MotionType = _selectedMotionType
                },
            _ when request.HasValue && ElementaryQuizGenerator.Supports(request.Value.Kind) => request.Value with { ElementaryType = _selectedElementaryType },
            _ => request
        };
    }

    private void UpdateElementaryPicker(QuizProblemKind? kind)
    {
        ElementaryTypePanel.IsVisible = kind.HasValue && ElementaryQuizGenerator.Supports(kind.Value);
        if (!ElementaryTypePanel.IsVisible) return;
        ElementaryTypePicker.IsEnabled = true;
        if (_elementaryPickerKind == kind && _elementaryPickerLanguage == AppLanguageManager.CurrentLanguage
            && ElementaryTypePicker.Items.Count > 0) return;
        bool updating = _isUpdatingSubtypePickers;
        _isUpdatingSubtypePickers = true;
        try
        {
            if (_elementaryPickerKind != kind) _selectedElementaryType = null;
            _elementaryPickerKind = kind;
            _elementaryPickerLanguage = AppLanguageManager.CurrentLanguage;
            _elementaryTypePickerValues.Clear();
            _elementaryTypePickerValues.Add(null);
            _elementaryTypePickerValues.AddRange(ElementaryQuizGenerator.Types(kind!.Value).Select(type => (ElementaryQuizType?)type));
            ElementaryTypePicker.Items.Clear();
            foreach (var type in _elementaryTypePickerValues)
                ElementaryTypePicker.Items.Add(TranslateQuiz(type.HasValue ? "Quiz.Elementary." + type.Value : "Quiz.OperationMixed"));
            IllustratedQuizPicker.SetKeys(ElementaryTypePicker, _elementaryTypePickerValues.Select(type =>
                type.HasValue ? "Quiz.Elementary." + type.Value : "Quiz.OperationMixed"));
            ElementaryTypePicker.SelectedIndex = Math.Max(0, _elementaryTypePickerValues.IndexOf(_selectedElementaryType));
            ElementaryTypePicker.IsEnabled = true;
        }
        finally { _isUpdatingSubtypePickers = updating; }
    }

    private void OnElementaryTypeChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingSubtypePickers || ElementaryTypePicker.SelectedIndex < 0) return;
        _selectedElementaryType = _elementaryTypePickerValues[ElementaryTypePicker.SelectedIndex];

        UpdateProblemOperationPanel();
        ResetQuizSessionState();
        GenerateAlgorithmQuestion();
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
        bool showAverageType =
            kind == QuizProblemKind.Average;
        bool showPercentageType =
            kind == QuizProblemKind.Percentage;
        bool showFindXType =
            kind == QuizProblemKind.FindX;
        bool showGeometryShape =
            kind == QuizProblemKind.Geometry;
        bool showMotionType =
            kind == QuizProblemKind.Motion;

        ProblemOperationPanel.IsVisible = showOperations;
        UpdateElementaryPicker(kind);
        PracticeFormatPanel.IsVisible = kind == QuizProblemKind.FindX
            || kind == QuizProblemKind.Arithmetic && !_selectedBasicComparison
            || kind == QuizProblemKind.Fraction && !_selectedFractionComparison
            || kind == QuizProblemKind.Decimal && _selectedElementaryType is
                (ElementaryQuizType.DecimalAdd or ElementaryQuizType.DecimalSubtract
                or ElementaryQuizType.DecimalMultiply or ElementaryQuizType.DecimalDivide);
        RefreshPracticeFormatPicker(kind);
        LearningProfilePanel.IsVisible = PracticeFormatPanel.IsVisible && kind != QuizProblemKind.Decimal && UsesWordProblems(kind);
        ProportionTypePanel.IsVisible = showProportionType;
        AverageTypePanel.IsVisible = showAverageType;
        PercentageTypePanel.IsVisible = showPercentageType;
        FindXTypePanel.IsVisible = showFindXType;
        GeometryShapePanel.IsVisible = showGeometryShape;
        MotionTypePanel.IsVisible = showMotionType;
        ExpressionTypePanel.IsVisible = kind == QuizProblemKind.Expression;

        if (showProportionType)
        {
            MixedProportionButton.IsEnabled = true;
            DirectProportionButton.IsEnabled = true;
            InverseProportionButton.IsEnabled = true;
            MixedProportionButton.Opacity = 1d;
            DirectProportionButton.Opacity = 1d;
            InverseProportionButton.Opacity = 1d;

            SelectionButtonStyler.Select(
                _selectedProportionType switch
                {
                    ProportionQuizType.Direct => DirectProportionButton,
                    ProportionQuizType.Inverse => InverseProportionButton,
                    _ => MixedProportionButton
                },
                MixedProportionButton,
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

        ArithmeticOperation? operation = kind == QuizProblemKind.Fraction
            ? (_selectedFractionOperation.HasValue
                ? MapFractionOperation(_selectedFractionOperation.Value)
                : null)
            : _selectedBasicOperation;

        // Skill Mode không khóa phép tính theo sao. Nút Hỗn hợp ở đây chỉ
        // random subtype bên trong skill hiện tại (+ - × ÷), hoàn toàn khác
        // với mục Hỗn hợp các dạng ở OperationPicker.
        ProblemMixedButton.IsEnabled = true;
        ProblemAddButton.IsEnabled = true;
        ProblemSubtractButton.IsEnabled = true;
        ProblemMultiplyButton.IsEnabled = true;
        ProblemDivideButton.IsEnabled = true;
        ProblemCompareButton.IsEnabled = true;
        ProblemMixedButton.Opacity = 1d;
        ProblemAddButton.Opacity = 1d;
        ProblemSubtractButton.Opacity = 1d;
        ProblemMultiplyButton.Opacity = 1d;
        ProblemDivideButton.Opacity = 1d;
        ProblemCompareButton.Opacity = 1d;

        bool comparison = kind == QuizProblemKind.Fraction ? _selectedFractionComparison : _selectedBasicComparison;
        Button selected = comparison ? ProblemCompareButton : operation switch
        {
            null => ProblemMixedButton,
            ArithmeticOperation.Add => ProblemAddButton,
            ArithmeticOperation.Subtract => ProblemSubtractButton,
            ArithmeticOperation.Multiply => ProblemMultiplyButton,
            ArithmeticOperation.Divide => ProblemDivideButton,
            _ => ProblemMixedButton
        };

        SelectionButtonStyler.Select(
            selected,
            ProblemMixedButton,
            ProblemAddButton,
            ProblemSubtractButton,
            ProblemMultiplyButton,
            ProblemDivideButton,
            ProblemCompareButton);
    }

    private void OnProblemMixedClicked(object? sender, EventArgs e) =>
        SelectProblemOperation(null);

    private void OnProblemAddClicked(object? sender, EventArgs e) =>
        SelectProblemOperation(ArithmeticOperation.Add);

    private void OnProblemSubtractClicked(object? sender, EventArgs e) =>
        SelectProblemOperation(ArithmeticOperation.Subtract);

    private void OnProblemMultiplyClicked(object? sender, EventArgs e) =>
        SelectProblemOperation(ArithmeticOperation.Multiply);

    private void OnProblemDivideClicked(object? sender, EventArgs e) =>
        SelectProblemOperation(ArithmeticOperation.Divide);

    private void OnProblemCompareClicked(object? sender, EventArgs e) =>
        SelectProblemOperation(null, comparison: true);

    private void SelectProblemOperation(ArithmeticOperation? operation, bool comparison = false)
    {
        QuizProblemKind? kind =
            _quizProblemTypeCatalog
                .GetFixedRequest(OperationPicker.SelectedIndex)
                ?.Kind;

        if (kind != QuizProblemKind.Arithmetic &&
            kind != QuizProblemKind.Fraction)
        {
            return;
        }

        bool changed;
        if (kind == QuizProblemKind.Arithmetic)
        {
            if (operation is { } selected && CurrentLearningProfile is { } profile && !profile.Allows(selected)) return;
            changed = _selectedBasicOperation != operation || _selectedBasicComparison != comparison;
            _selectedBasicOperation = operation;
            _selectedBasicComparison = comparison;
        }
        else
        {
            FractionOperation? fractionOperation = operation.HasValue
                ? MapArithmeticOperation(operation.Value)
                : null;
            changed = _selectedFractionOperation != fractionOperation || _selectedFractionComparison != comparison;
            _selectedFractionOperation = fractionOperation;
            _selectedFractionComparison = comparison;
        }

        UpdateProblemOperationPanel();
        if (!changed)
        {
            return;
        }

        ResetQuizSessionState();
        _activeProblemRequest = GetSelectedFixedProblemRequest();
        UpdateEssayAnswerPresentation();

        GenerateAlgorithmQuestion();
    }

    private void OnMixedProportionClicked(object? sender, EventArgs e) =>
        SelectProportionType(null);

    private void OnDirectProportionClicked(object? sender, EventArgs e) =>
        SelectProportionType(ProportionQuizType.Direct);

    private void OnInverseProportionClicked(object? sender, EventArgs e) =>
        SelectProportionType(ProportionQuizType.Inverse);

    private void SelectProportionType(ProportionQuizType? type)
    {
        QuizProblemKind? kind =
            _quizProblemTypeCatalog
                .GetFixedRequest(OperationPicker.SelectedIndex)
                ?.Kind;

        if (kind != QuizProblemKind.Proportion)
        {
            return;
        }

        IReadOnlyList<ProportionQuizType> allowedTypes =
            QuizCurriculumLayer.GetAllowedProportionTypes(
                new QuizCurriculumContext(
                    _selectedCurriculumTier,
                    IsMixedMode: false));

        if (allowedTypes.Count == 0 || type.HasValue && !allowedTypes.Contains(type.Value))
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

        ResetQuizSessionState();
        _activeProblemRequest = GetSelectedFixedProblemRequest();
        UpdateEssayAnswerPresentation();

        GenerateAlgorithmQuestion();
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
        if (_currentQuestion?.UsesFractionFormatting == true)
        {
            return true;
        }

        QuizProblemRequest? request =
            _activeProblemRequest ??
            GetSelectedFixedProblemRequest();

        return request?.Kind == QuizProblemKind.Fraction ||
            request?.Kind == QuizProblemKind.Expression &&
            _selectedExpressionType is ExpressionQuizType.Fraction or ExpressionQuizType.FractionWithBrackets;
    }

    private void OnExpressionTypeChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingSubtypePickers || ExpressionTypePicker.SelectedIndex < 0)
            return;
        _selectedExpressionType = ExpressionTypePicker.SelectedIndex == 0
            ? null : (ExpressionQuizType)(ExpressionTypePicker.SelectedIndex - 1);
        OnSubtypeSelectionChanged(QuizProblemKind.Expression);
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

    private async void GenerateAlgorithmQuestion(
        int? questionNumberOnSuccess = null)
    {
        int version = ++_questionGenerationVersion;
        SetAnswerControlsEnabled(false);
        _questionAnswered = false;
        _lastAnswerWasCorrect = null;
        NextQuestionButton.IsEnabled = false;

        try
        {
            QuizProblemRequest problemRequest =
                ResolveSelectedProblem();

            var learning = _quizProblemTypeCatalog.GetFixedRequest(OperationPicker.SelectedIndex)?.Kind == QuizProblemKind.Arithmetic
                && !problemRequest.IsComparison && _arithmeticWordProblems ? CurrentLearningProfile : null;
            if (learning is not null && (problemRequest.ArithmeticOperation is not { } operation || !learning.Allows(operation)))
            {
                var allowed = Enum.GetValues<ArithmeticOperation>().Where(learning.Allows).ToArray();
                problemRequest = problemRequest with { ArithmeticOperation = allowed[Random.Shared.Next(allowed.Length)] };
            }

            _activeProblemRequest = problemRequest;

            QuizCurriculumContext curriculumContext =
                GetCurriculumContext();

            _currentQuestion =
                problemRequest.Kind switch
                {
                    _ when problemRequest.IsComparison => _elementaryQuizGenerator.GenerateComparison(
                        _selectedMode, problemRequest.Kind, AppLanguageManager.CurrentLanguage, curriculumContext.Tier),
                    QuizProblemKind.Decimal when problemRequest.ElementaryType is
                        (ElementaryQuizType.DecimalAdd or ElementaryQuizType.DecimalSubtract
                        or ElementaryQuizType.DecimalMultiply or ElementaryQuizType.DecimalDivide)
                        && GetPracticeFormat(QuizProblemKind.Decimal) != PracticeQuestionFormat.Mixed =>
                        _elementaryQuizGenerator.GenerateDecimalArithmetic(_selectedMode, problemRequest.ElementaryType.Value,
                            AppLanguageManager.CurrentLanguage, curriculumContext.Tier, _decimalWordProblems),
                    _ when ElementaryQuizGenerator.Supports(problemRequest.Kind) => _elementaryQuizGenerator.Generate(
                        _selectedMode, problemRequest.Kind, problemRequest.ElementaryType,
                        AppLanguageManager.CurrentLanguage, curriculumContext.Tier),
                    QuizProblemKind.Expression => _expressionQuizGenerator.Generate(
                        _selectedMode, problemRequest.ExpressionType, curriculumContext.Tier),
                    QuizProblemKind.Geometry =>
                        _geometryQuizGenerator.GenerateAlgorithm(
                            _selectedMode,
                            AppLanguageManager.CurrentLanguage,
                            problemRequest.GeometryShape,
                            curriculumContext,
                            problemRequest.GeometryMeasurement),
                    QuizProblemKind.Arithmetic =>
                        _quizGenerator.Generate(
                            _selectedMode,
                            problemRequest.ArithmeticOperation,
                            curriculumContext),
                    QuizProblemKind.Fraction =>
                        _fractionQuizGenerator.Generate(
                            _selectedMode,
                            problemRequest.FractionOperation,
                            curriculumContext),
                    QuizProblemKind.FindX =>
                        _findXQuizGenerator.Generate(
                            _selectedMode,
                            problemRequest.FindXOperation,
                            curriculumContext),
                    QuizProblemKind.Proportion =>
                        _proportionQuizGenerator.GenerateAlgorithm(
                            _selectedMode,
                            problemRequest.ProportionType,
                            AppLanguageManager.CurrentLanguage,
                            curriculumContext),
                    QuizProblemKind.Motion =>
                        _motionQuizGenerator.GenerateAlgorithm(
                            _selectedMode,
                            AppLanguageManager.CurrentLanguage,
                            problemRequest.MotionType,
                            curriculumContext),
                    QuizProblemKind.Average =>
                        _averageQuizGenerator.GenerateAlgorithm(
                            _selectedMode,
                            problemRequest.AverageType,
                            AppLanguageManager.CurrentLanguage,
                            curriculumContext),
                    QuizProblemKind.Percentage =>
                        _percentageQuizGenerator.GenerateAlgorithm(
                            _selectedMode,
                            problemRequest.PercentageType,
                            AppLanguageManager.CurrentLanguage,
                            curriculumContext),
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(problemRequest))
                };

            if (problemRequest.Kind == QuizProblemKind.Arithmetic && !problemRequest.IsComparison)
            {
                var selected = await AiQuestionBank.Current.Practice.SelectAsync(_currentQuestion,
                    curriculumContext.Tier, AppLanguageManager.CurrentLanguage, profile: learning,
                    format: GetPracticeFormat(QuizProblemKind.Arithmetic));
                // Selection, language and answer mode can change during the SQLite read.
                if (version != _questionGenerationVersion) return;
                _currentQuestion = selected;
            }

            if (problemRequest.Kind == QuizProblemKind.FindX)
            {
                var selected = await AiQuestionBank.Current.Practice.SelectFindXAsync(_currentQuestion,
                    curriculumContext.Tier, AppLanguageManager.CurrentLanguage, CurrentLearningProfile,
                    format: GetPracticeFormat(QuizProblemKind.FindX));
                if (version != _questionGenerationVersion) return;
                _currentQuestion = selected;
            }

            if (problemRequest.Kind == QuizProblemKind.Fraction && !problemRequest.IsComparison && _fractionWordProblems
                && GetSelectedFixedProblemRequest()?.Kind == QuizProblemKind.Fraction)
            {
                var selected = await AiQuestionBank.Current.Practice.SelectFractionAsync(_currentQuestion,
                    curriculumContext.Tier, AppLanguageManager.CurrentLanguage, CurrentLearningProfile);
                if (version != _questionGenerationVersion) return;
                _currentQuestion = selected;
            }

            if (problemRequest.Kind is QuizProblemKind.TwoNumbers or QuizProblemKind.Average or QuizProblemKind.Percentage or QuizProblemKind.MultiStep or QuizProblemKind.Motion or QuizProblemKind.Proportion or QuizProblemKind.Remainder or QuizProblemKind.Geometry or QuizProblemKind.Probability
                || _currentQuestion.ElementaryProblem is { Kind: QuizProblemKind.Data, DataChart: not null }
                || _currentQuestion.ElementaryProblem is { Kind: QuizProblemKind.Decimal, IsDecimalArithmetic: true, StoryContextId: not null }
                || _currentQuestion.ElementaryProblem is { Kind: QuizProblemKind.Time } time
                    && ElementaryQuizGenerator.TimeStoryTypes.Contains(time.Type)
                || _currentQuestion.ElementaryProblem is { Kind: QuizProblemKind.FractionSkills } fractionQuantity
                    && ElementaryQuizGenerator.FractionQuantityStoryTypes.Contains(fractionQuantity.Type)
                || _currentQuestion.ElementaryProblem is { Kind: QuizProblemKind.Measurement } measurement
                    && ElementaryQuizGenerator.MeasurementBankTypes.Contains(measurement.Type))
            {
                var selected = await AiQuestionBank.Current.Practice.SelectReasoningAsync(_currentQuestion,
                    curriculumContext.Tier, AppLanguageManager.CurrentLanguage);
                if (version != _questionGenerationVersion) return;
                _currentQuestion = selected;
            }

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
            UpdateRegenerateQuestionButtonState();
            UpdatePracticeSummary();
            if (_currentQuestion is null) SetPracticeSettingsExpanded(true);
        }
    }

    private void OnRegenerateQuestionClicked(object? sender, EventArgs e)
    {
        if (_questionAnswered) return;
        // Replace the current question without changing its number or score.
        GenerateAlgorithmQuestion();
    }

    private async void OnOpenAiQuestionBankClicked(object? sender, EventArgs e)
    {
        if (_openingAiQuestionBank) return;
        _openingAiQuestionBank = true;
        try { await Shell.Current.GoToAsync(nameof(AiQuestionBankPage)); }
        finally { _openingAiQuestionBank = false; }
    }

    private void OnAiQuestionBankProgress(object? sender, EventArgs e) => Dispatcher.Dispatch(UpdateAiQuestionBankProgress);
    private void UpdateAiQuestionBankProgress()
    {
        var job = AiQuestionBank.Current.Generation.Snapshot;
        AiQuestionBankProgressLabel.IsVisible = job.IsRunning;
        AiQuestionBankProgressLabel.Text = string.Format(CultureInfo.CurrentCulture,
            LocalizationService.TranslateKey("AiBank.PracticeProgress"), job.Items.Count(i => i.Question is not null), job.Options?.Count ?? 0);
    }

    private void UpdateRegenerateQuestionButtonState()
    {
        RegenerateQuestionButton.IsEnabled = !_questionAnswered;
        RefreshQuestionActionButtonTheme();
    }

    private void RefreshQuestionActionButtonTheme()
    {
        foreach (Button button in new[] { RegenerateQuestionButton, NextQuestionButton, SubmitEssayAnswerButton })
        {
            button.SetDynamicResource(Button.BackgroundColorProperty, "PrimaryColor");
            button.SetDynamicResource(Button.TextColorProperty, "OnPrimaryColor");
        }
    }

    private void ClearMultipleChoiceAnswers()
    {
        _choiceStatuses.Clear();
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
        if (_currentQuestion?.FindXProblem is not null) text = FindXDisplayText.Format(text);
        NumericResultLabel.IsVisible = false;
        QuestionComparisonFractionView.IsVisible = false;
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
        EssayWorkEditor.IsEnabled = isEnabled;
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
        UpdatePracticeSummary();

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
        PracticeFactTable.Table = wordProblem?.FactTable;
        FindXQuizContract? findXProblem =
            _currentQuestion.FindXProblem;
        FractionQuizContract? fractionProblem =
            _currentQuestion.FractionProblem;
        ProportionQuizContract? proportionProblem =
            _currentQuestion.ProportionProblem;
        MotionQuizContract? motionProblem =
            _currentQuestion.MotionProblem;
        AverageQuizContract? averageProblem =
            _currentQuestion.AverageProblem;
        PercentageQuizContract? percentageProblem =
            _currentQuestion.PercentageProblem;

        ElementaryQuizContract? elementary = _currentQuestion.ElementaryProblem;
        bool parseFractionExpressions = _currentQuestion.UsesFractionFormatting && _currentQuestion.WordProblem is not null || elementary?.Kind == QuizProblemKind.FractionSkills ||
            elementary?.Type is ElementaryQuizType.TimeAddition or ElementaryQuizType.SumRatio or ElementaryQuizType.DifferenceRatio;
        foreach (var view in new[] { QuestionFractionExpressionView, QuestionComparisonFractionView,
            PresentedAnswerFractionView, FeedbackFractionView, SolutionFractionView, QuizDiagramExplanationFractionView }
            .Concat(ChoiceFractionViews))
            view.ParseArithmeticExpressions = parseFractionExpressions;
        UpdateQuizDiagram();
        if (elementary is not null)
        {
            QuestionPromptLabel.Text = GetQuestionPromptTitle();
            PresentedAnswerFractionView.IsVisible = false;
            if (elementary.IsComparison)
            {
                SetQuestionContent(wordProblem?.ProblemText ?? elementary.ProblemText, 21,
                    "WallpaperTextPrimaryColor", useFractionFormatting: elementary.UsesFractionFormatting);
                string displayedSymbol = _currentQuestion.Mode == ArithmeticQuizMode.TrueFalse
                    ? elementary.PresentedText ?? "?" : "?";
                string comparisonText = elementary.FormatComparison(displayedSymbol);
                if (elementary.Type == ElementaryQuizType.CompareFractions)
                {
                    QuestionComparisonFractionView.Expression = comparisonText;
                    QuestionComparisonFractionView.IsVisible = true;
                }
                else
                {
                    NumericResultLabel.Text = comparisonText;
                    NumericResultLabel.IsVisible = true;
                }
                PresentedAnswerLabel.IsVisible = false;
                PresentedAnswerLabel.Text = string.Empty;
            }
            else if (wordProblem is null && elementary.IsNumericDecimalCalculation)
            {
                string displayedAnswer = _currentQuestion.Mode == ArithmeticQuizMode.TrueFalse
                    ? elementary.PresentedText ?? "" : "?";
                SetQuestionContent(elementary.FormatDecimalCalculation(displayedAnswer), 34,
                    "PrimaryColor", useFractionFormatting: false);
                PresentedAnswerLabel.IsVisible = false;
                PresentedAnswerLabel.Text = string.Empty;
            }
            else if (wordProblem is null && elementary.Type == ElementaryQuizType.DecimalRound)
            {
                SetQuestionContent(elementary.ProblemText, 21,
                    "WallpaperTextPrimaryColor", useFractionFormatting: false);
                string displayedResult = _currentQuestion.Mode == ArithmeticQuizMode.TrueFalse
                    ? elementary.PresentedText ?? "?" : "?";
                NumericResultLabel.Text = $"{elementary.Facts[0]} ≈ {displayedResult}";
                NumericResultLabel.IsVisible = true;
                PresentedAnswerLabel.IsVisible = false;
                PresentedAnswerLabel.Text = string.Empty;
            }
            else
            {
                SetQuestionContent(wordProblem?.ProblemText ?? elementary.ProblemText, 21,
                    "WallpaperTextPrimaryColor", useFractionFormatting: elementary.UsesFractionFormatting);
                bool showPresented = _currentQuestion.Mode == ArithmeticQuizMode.TrueFalse;
                string presentedText = FormatPresentedAnswerForDisplay(elementary.PresentedText ?? "");
                PresentedAnswerLabel.IsVisible = showPresented && !elementary.UsesFractionFormatting;
                PresentedAnswerLabel.Text = showPresented ? presentedText : "";
                PresentedAnswerFractionView.IsVisible = showPresented && elementary.UsesFractionFormatting;
                PresentedAnswerFractionView.Expression = showPresented
                    ? FormatElementaryAnswerForDisplay(elementary, presentedText) : "";
            }
        }
        else if (wordProblem is not null)
        {
            QuestionPromptLabel.Text =
                GetQuestionPromptTitle();
            SetQuestionContent(
                wordProblem.ProblemText,
                21,
                "WallpaperTextPrimaryColor",
                useFractionFormatting:
                    _currentQuestion.UsesFractionFormatting);

            if (_currentQuestion.Mode ==
                ArithmeticQuizMode.TrueFalse)
            {
                string presentedAnswer =
                    _currentQuestion.ExpressionProblem?.PresentedAnswer?.ToString() ??
                    fractionProblem?.PresentedAnswer?.ToString() ??
                    _currentQuestion.PresentedAnswer
                        .GetValueOrDefault()
                        .ToString("N0", CultureInfo.CurrentCulture);

                string presentedText = FormatPresentedAnswerForDisplay(presentedAnswer, wordProblem.AnswerUnit);

                if (_currentQuestion.UsesFractionFormatting)
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
                "WallpaperTextPrimaryColor",
                useFractionFormatting: false);

            if (_currentQuestion.Mode ==
                ArithmeticQuizMode.TrueFalse)
            {
                string presentedAnswer =
                    _currentQuestion.PresentedAnswer
                        .GetValueOrDefault()
                        .ToString("N0", CultureInfo.CurrentCulture);

                PresentedAnswerFractionView.IsVisible = false;
                PresentedAnswerLabel.Text = FormatPresentedAnswerForDisplay(presentedAnswer, proportionProblem.AnswerUnit);
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
                "WallpaperTextPrimaryColor",
                useFractionFormatting: false);

            if (_currentQuestion.Mode ==
                ArithmeticQuizMode.TrueFalse)
            {
                string presentedAnswer =
                    _currentQuestion.PresentedAnswer
                        .GetValueOrDefault()
                        .ToString("N0", CultureInfo.CurrentCulture);

                PresentedAnswerFractionView.IsVisible = false;
                PresentedAnswerLabel.Text = FormatPresentedAnswerForDisplay(presentedAnswer, motionProblem.AnswerUnit);
                PresentedAnswerLabel.IsVisible = true;
            }
            else
            {
                PresentedAnswerLabel.IsVisible = false;
                PresentedAnswerFractionView.IsVisible = false;
            }
        }
        else if (averageProblem is not null)
        {
            QuestionPromptLabel.Text = GetQuestionPromptTitle();
            SetQuestionContent(
                averageProblem.ProblemText,
                21,
                "WallpaperTextPrimaryColor",
                useFractionFormatting: false);

            RenderPresentedContractAnswer(averageProblem.AnswerUnit);
        }
        else if (percentageProblem is not null)
        {
            QuestionPromptLabel.Text = GetQuestionPromptTitle();
            SetQuestionContent(
                percentageProblem.ProblemText,
                21,
                "WallpaperTextPrimaryColor",
                useFractionFormatting: false);

            RenderPresentedContractAnswer(percentageProblem.AnswerUnit);
        }
        else if (_currentQuestion.ExpressionProblem is ExpressionQuizContract expressionProblem)
        {
            PresentedAnswerLabel.IsVisible = false;
            PresentedAnswerFractionView.IsVisible = false;
            SetQuestionContent($"{expressionProblem.ExpressionText} = " +
                (_currentQuestion.Mode == ArithmeticQuizMode.TrueFalse
                    ? expressionProblem.PresentedAnswer.ToString() : "?"),
                expressionProblem.UsesFractions ? 26 : 28, "PrimaryColor",
                useFractionFormatting: expressionProblem.UsesFractions);
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
                button.MinimumHeightRequest = 54;

                button.HeightRequest =
                    _currentQuestion.UsesFractionFormatting
                        ? 72
                        : 54;
                button.LineBreakMode = LineBreakMode.WordWrap;

                char prefix =
                    (char)('A' + index);

                string choiceUnit =
                    wordProblem is not null
                        ? $" {wordProblem.AnswerUnit}"
                        : proportionProblem is not null
                            ? $" {proportionProblem.AnswerUnit}"
                            : motionProblem is not null
                                ? $" {motionProblem.AnswerUnit}"
                                : averageProblem is not null
                                    ? $" {averageProblem.AnswerUnit}"
                                    : percentageProblem is not null
                                        ? $" {percentageProblem.AnswerUnit}"
                                        : string.Empty;

                if (elementary is not null)
                {
                    string choice = elementary.ChoiceTexts![index];
                    button.Text = elementary.UsesFractionFormatting ? string.Empty : $"{prefix}. {choice}";
                    button.CommandParameter = choice;
                    ChoiceFractionViews[index].Expression = $"{prefix}. {FormatElementaryAnswerForDisplay(elementary, choice)}";
                    ChoiceFractionViews[index].IsVisible = elementary.UsesFractionFormatting;
                    button.HeightRequest = elementary.Answers.Count > 1
                        ? DeviceInfo.Platform == DevicePlatform.Android ? 120 : 86 : 72;
                    if (elementary.UsesFractionFormatting)
                    {
                        // Let the overlay determine the row height for stacked
                        // fractions, mixed numbers and two labeled answers.
                        button.MinimumHeightRequest = button.HeightRequest;
                        button.HeightRequest = -1;
                    }
                }
                else if (_currentQuestion.UsesFractionFormatting)
                {
                    ReducedFraction choice =
                        fractionProblem?.Choices[index] ?? _currentQuestion.ExpressionProblem!.Choices[index];
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
        UpdateElementaryChoiceLayout(elementary);
        UpdateChoiceDescriptions();
    }

    private void ResetAnswerControls()
    {
        _choiceStatuses.Clear();
        SetAnswerControlsEnabled(true);

        EssayWorkEditor.Text = string.Empty;

        foreach (Button button in ChoiceButtons)
        {
            ApplyNeutralAnswerStyle(button);
        }

        FeedbackBorder.IsVisible = false;
        SolutionBorder.IsVisible = false;
        NextQuestionButton.IsEnabled = false;
    }

    private static string FormatElementaryAnswerForDisplay(ElementaryQuizContract contract, string text) =>
        contract.Answers.Count > 1 ? text.Replace("; ", Environment.NewLine, StringComparison.Ordinal) : text;

    // Label only the proposed value; never substitute the computed answer in True/False mode.
    private string FormatPresentedAnswerForDisplay(string answer, string answerUnit = "") =>
        string.Format(CultureInfo.CurrentCulture, Translate("Quiz.PresentedAnswer"), answer, answerUnit).TrimEnd();

    private void UpdateElementaryChoiceLayout(ElementaryQuizContract? contract)
    {
        double width = MultipleChoiceAnswerGrid.Width;
        if (width <= 0) width = Math.Max(0, QuizContent.Width - QuizContent.Padding.HorizontalThickness - 36);
        int longestChoice = ChoiceButtons.Select((button, index) => ChoiceFractionViews[index].IsVisible
            ? ChoiceFractionViews[index].Expression.Length : button.Text?.Length ?? 0).Max();
        bool singleColumn = QuizResponsiveLayout.UseSingleColumnChoices(width, CurrentTextScale,
            longestChoice, contract?.Answers.Count > 1);
        if (_singleColumnChoices == singleColumn) return;
        _singleColumnChoices = singleColumn;
        MultipleChoiceAnswerGrid.ColumnDefinitions.Clear();
        MultipleChoiceAnswerGrid.RowDefinitions.Clear();
        for (int column = 0; column < (singleColumn ? 1 : 2); column++)
            MultipleChoiceAnswerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        for (int row = 0; row < (singleColumn ? 4 : 2); row++)
            MultipleChoiceAnswerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int index = 0; index < ChoiceButtons.Length; index++)
        {
            int row = singleColumn ? index : index / 2, column = singleColumn ? 0 : index % 2;
            Grid.SetRow(ChoiceButtons[index], row); Grid.SetColumn(ChoiceButtons[index], column);
            Grid.SetRow(ChoiceFractionViews[index], row); Grid.SetColumn(ChoiceFractionViews[index], column);
        }
    }

    private void RenderPresentedContractAnswer(string answerUnit)
    {
        if (_currentQuestion is null ||
            _currentQuestion.Mode != ArithmeticQuizMode.TrueFalse)
        {
            PresentedAnswerLabel.IsVisible = false;
            PresentedAnswerFractionView.IsVisible = false;
            return;
        }

        string presentedAnswer = _currentQuestion.PresentedAnswer
            .GetValueOrDefault()
            .ToString("N0", CultureInfo.CurrentCulture);

        PresentedAnswerFractionView.IsVisible = false;
        PresentedAnswerLabel.Text = FormatPresentedAnswerForDisplay(presentedAnswer, answerUnit);
        PresentedAnswerLabel.IsVisible = true;
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

        if (_currentQuestion.ElementaryProblem is not null)
        {
            CompleteAnswer(ElementaryEssayValidator.CheckAnswers(_currentQuestion, button.CommandParameter?.ToString()), button);
            return;
        }
        if (_currentQuestion.UsesFractionFormatting)
        {
            if (!ReducedFraction.TryParse(
                    button.CommandParameter?.ToString(),
                    out ReducedFraction selectedFraction))
            {
                return;
            }

            CompleteAnswer(
                selectedFraction == _currentQuestion.ExactAnswer,
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

        (string solutionText, string equationText, string answerText) =
            EssayCombinedInputParser.Parse(
                EssayWorkEditor.Text,
                EssayAnswerValidator.RequiresSolution(_currentQuestion),
                preserveAllCalculations: _currentQuestion.WordProblem?.ConversionStep is not null
                    || _currentQuestion.GeometryProblem?.Reasoning is not null || _currentQuestion.ElementaryProblem is not null
                    || _currentQuestion.AverageProblem?.Type == AverageQuizType.IndirectData,
                requireAnswerLabel: _currentQuestion.ElementaryProblem?.Type == ElementaryQuizType.ReadClock,
                allowTextAnswer: _currentQuestion.ElementaryProblem?.Answers.Any(answer => answer.IsText) == true);

        EssayAnswerValidationResult validation =
            _essayAnswerValidator.Validate(
                _currentQuestion,
                solutionText,
                equationText,
                answerText);

        CompleteAnswer(
            validation.IsCorrect,
            selectedButton: null,
            feedbackOverride:
                BuildEssayFeedback(
                    _currentQuestion,
                    validation,
                    solutionText,
                    equationText,
                    answerText));
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
        UpdateQuizDiagram();

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
                _currentQuestion.ElementaryProblem is not null
                    ? ElementaryEssayValidator.CheckAnswers(_currentQuestion, button.CommandParameter?.ToString())
                    : _currentQuestion.UsesFractionFormatting
                    ? ReducedFraction.TryParse(
                        button.CommandParameter?.ToString(),
                        out ReducedFraction fractionAnswer) &&
                      fractionAnswer == _currentQuestion.ExactAnswer
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
            if (_currentQuestion.FindXProblem is not null) feedbackOverride = FindXDisplayText.Format(feedbackOverride);
            FeedbackLabel.Text = feedbackOverride;
            FeedbackFractionView.Expression = feedbackOverride;
            FeedbackLabel.IsVisible = !_currentQuestion.UsesFractionFormatting;
            FeedbackFractionView.IsVisible = _currentQuestion.UsesFractionFormatting;
            FeedbackFractionView.HorizontalTextAlignment = isCorrect ? TextAlignment.Center : TextAlignment.Start;
            FeedbackLabel.HorizontalTextAlignment = isCorrect
                ? TextAlignment.Center
                : TextAlignment.Start;
        }

        if (_currentQuestion.ElementaryProblem is ElementaryQuizContract elementary)
        {
            SetSolutionContent(elementary.SolutionText, useFractionFormatting: elementary.UsesFractionFormatting);
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
                    _currentQuestion.UsesFractionFormatting);
            SolutionBorder.IsVisible = true;
        }
        else if (_currentQuestion.ExpressionProblem is not null || EssayAnswerValidator.RequiresSolution(_currentQuestion))
        {
            string solutionText =
                FormatPlainEssaySolution(
                    _currentQuestion);

            SetSolutionContent(
                solutionText,
                useFractionFormatting:
                    _currentQuestion.UsesFractionFormatting);
            SolutionBorder.IsVisible = true;
        }

        NextQuestionButton.IsEnabled = true;
        UpdateRegenerateQuestionButtonState();
        UpdateScoreLabels();
        string feedback = FeedbackFractionView.IsVisible ? FeedbackFractionView.Expression : FeedbackLabel.Text;
        SemanticScreenReader.Default.Announce(AccessibleMathText.Format(feedback, AppLanguageManager.CurrentLanguage));
    }

    private static string BuildEssayFeedback(
        ArithmeticQuizQuestion question,
        EssayAnswerValidationResult validation,
        string? solutionText,
        string? equationText,
        string? answerText)
    {
        if (validation.IsCorrect)
        {
            return TranslateQuiz(
                EssayAnswerValidator.RequiresSolution(question)
                    ? "Quiz.EssayCorrectFeedback"
                    : "Quiz.EssayCorrectFeedbackNumeric");
        }

        return EssayFeedbackFormatter.Format(
            question,
            validation,
            solutionText,
            equationText,
            answerText,
            AppLanguageManager.CurrentLanguage,
            CultureInfo.CurrentCulture);
    }

    private static string FormatPlainEssaySolution(
        ArithmeticQuizQuestion question)
    {
        if (question.ElementaryProblem is ElementaryQuizContract elementary) return elementary.SolutionText;
        if (question.ExpressionProblem is ExpressionQuizContract expressionProblem)
        {
            string label = AppLanguageManager.CurrentLanguage == AppLanguage.Vietnamese ? "Đáp số" : "Answer";
            return $"{expressionProblem.ExpressionText} = {expressionProblem.CorrectAnswer}" +
                Environment.NewLine + $"{label}: {expressionProblem.CorrectAnswer}";
        }
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

        if (question.AverageProblem is AverageQuizContract average)
        {
            return average.SolutionText;
        }

        if (question.PercentageProblem is PercentageQuizContract percentage)
        {
            return percentage.SolutionText;
        }

        if (question.ProportionProblem is ProportionQuizContract proportion)
        {
            return ProportionQuizSolutionFormatter.Format(
                proportion,
                AppLanguageManager.CurrentLanguage,
                CultureInfo.CurrentCulture);
        }

        if (question.MotionProblem is MotionQuizContract motion)
        {
            string motionAnswerLabel =
                AppLanguageManager.CurrentLanguage == AppLanguage.Vietnamese
                    ? "Đáp số"
                    : "Answer";

            return $"{motion.SolutionText}{Environment.NewLine}" +
                   $"{motionAnswerLabel}: {motion.CorrectAnswer:N0} {motion.AnswerUnit}";
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

        if (question.WordProblem is null && question.FindXProblem is
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

    private void ShowFeedback(bool isCorrect)
    {
        string answerText =
            _currentQuestion?.ElementaryProblem?.AnswerText ??
            _currentQuestion?.ExpressionProblem?.CorrectAnswer.ToString() ??
            _currentQuestion?.FractionProblem?.CorrectAnswer.ToString() ??
            _currentQuestion?.CorrectAnswer.ToString(
                "N0",
                CultureInfo.CurrentCulture) ??
            string.Empty;

        if (_currentQuestion?.ElementaryProblem is not null)
        {
            // Each answer already carries its own unit.
        }
        else if (_currentQuestion?.WordProblem is
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
        else if (_currentQuestion?.AverageProblem is
                 AverageQuizContract averageProblem)
        {
            answerText += $" {averageProblem.AnswerUnit}";
        }
        else if (_currentQuestion?.PercentageProblem is
                 PercentageQuizContract percentageProblem)
        {
            answerText += $" {percentageProblem.AnswerUnit}";
        }

        string feedbackText = string.Format(
            CultureInfo.CurrentCulture,
            Translate(
                isCorrect
                    ? "Quiz.CorrectFeedback"
                    : "Quiz.IncorrectFeedback"),
            answerText);

        bool useFractionFormatting =
            _currentQuestion?.UsesFractionFormatting == true;

        FeedbackLabel.HorizontalTextAlignment = TextAlignment.Center;
        FeedbackFractionView.HorizontalTextAlignment = TextAlignment.Center;
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
                ? "WallpaperSuccessSoftColor"
                : "WallpaperDangerSoftColor");

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
        if (_currentQuestion?.FindXProblem is not null) text = FindXDisplayText.Format(text);
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

    private void OnNextQuestionClicked(object? sender, EventArgs e)
    {
        if (!_questionAnswered) return;
        GenerateAlgorithmQuestion(questionNumberOnSuccess: checked(_questionCount + 1));
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
        ResetQuizDiagram();
        QuestionExpressionLabel.IsVisible = true;
        QuestionFractionExpressionView.Expression = string.Empty;
        QuestionFractionExpressionView.IsVisible = false;
        QuestionComparisonFractionView.Expression = string.Empty;
        QuestionComparisonFractionView.IsVisible = false;
        PresentedAnswerLabel.Text = string.Empty;
        PresentedAnswerLabel.IsVisible = false;
        NumericResultLabel.Text = string.Empty;
        NumericResultLabel.IsVisible = false;
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
        EssayWorkEditor.Text = string.Empty;
        NextQuestionButton.IsEnabled = false;

        ClearMultipleChoiceAnswers();
        SetAnswerControlsEnabled(false);
        UpdateModeStyles();

    }

    private void ResetQuizSessionState()
    {
        _questionGenerationVersion++;
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
            "WallpaperSurfaceAltColor");
        button.SetDynamicResource(
            Button.BorderColorProperty,
            "WallpaperBorderColor");
        button.SetDynamicResource(
            Button.TextColorProperty,
            "WallpaperTextPrimaryColor");
        SetChoiceFractionColor(button, "WallpaperTextPrimaryColor");
    }

    private void ApplyCorrectAnswerStyle(
        Button button)
    {
        button.SetDynamicResource(
            Button.BackgroundColorProperty,
            "WallpaperSuccessSoftColor");
        button.SetDynamicResource(
            Button.BorderColorProperty,
            "SuccessColor");
        button.SetDynamicResource(
            Button.TextColorProperty,
            "SuccessColor");
        SetChoiceFractionColor(button, "SuccessColor");
        SetChoiceStatus(button, "✓", "Quiz.CorrectAnswerDescription");
    }

    private void ApplyIncorrectAnswerStyle(
        Button button)
    {
        button.SetDynamicResource(
            Button.BackgroundColorProperty,
            "WallpaperDangerSoftColor");
        button.SetDynamicResource(
            Button.BorderColorProperty,
            "DangerColor");
        button.SetDynamicResource(
            Button.TextColorProperty,
            "DangerColor");
        SetChoiceFractionColor(button, "DangerColor");
        SetChoiceStatus(button, "✗", "Quiz.IncorrectAnswerDescription");
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
