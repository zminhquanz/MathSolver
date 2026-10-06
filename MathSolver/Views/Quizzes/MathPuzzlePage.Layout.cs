using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;

namespace MathSolver.Views;

public partial class MathPuzzlePage
{
    private bool _practiceSettingsExpanded = true;
    private bool? _compactSettings;
    private (bool Inline, bool StackActions)? _summaryLayout;
    private int _practiceScrollVersion;
    private bool _practicePageVisible;
    private bool _practiceLayoutLoaded;
    private int _practiceLayoutVersion;
    private Window? _practiceLayoutWindow;
    private readonly Dictionary<Button, (string Icon, string Key)> _choiceStatuses = [];

    private bool CanUpdatePracticeLayout => _practicePageVisible && _practiceLayoutLoaded && IsLoaded
        && _practiceLayoutWindow is not null && ReferenceEquals(Window, _practiceLayoutWindow)
        && Handler?.MauiContext is not null && _practiceLayoutWindow.Handler?.MauiContext is not null
        && PracticeSummaryGrid.Handler?.MauiContext is not null
        && ChangePracticeSettingsButton.Handler?.MauiContext is not null
        && OpenAiQuestionBankButton.Handler?.MauiContext is not null
        && PracticeSummaryLabel.Handler?.MauiContext is not null;

    private void OnPracticePageLoaded(object? sender, EventArgs e)
    {
        _practiceLayoutLoaded = false;
        _practiceLayoutVersion++;
        DetachPracticeLayoutWindow();
        _practiceLayoutWindow = Window;
        if (_practiceLayoutWindow is null) return;
        _practiceLayoutWindow.Destroying += OnPracticeWindowDestroying;
        _practiceLayoutLoaded = true;
        _summaryLayout = null;
        QueuePracticeSummaryLayout();
    }

    private void OnPracticePageUnloaded(object? sender, EventArgs e)
    {
        _practiceLayoutLoaded = false;
        _practiceLayoutVersion++;
        _practiceScrollVersion++;
        DetachPracticeLayoutWindow();
    }

    private void OnPracticeWindowDestroying(object? sender, EventArgs e) => OnPracticePageUnloaded(sender, e);

    private void DetachPracticeLayoutWindow()
    {
        if (_practiceLayoutWindow is not null)
            _practiceLayoutWindow.Destroying -= OnPracticeWindowDestroying;
        _practiceLayoutWindow = null;
    }

    private void QueuePracticeSummaryLayout()
    {
        if (!CanUpdatePracticeLayout) return;
        int version = ++_practiceLayoutVersion;
        var handler = Handler;
        Dispatcher.Dispatch(() =>
        {
            // A queued callback belongs to one visible page/handler lifetime.
            if (version == _practiceLayoutVersion && ReferenceEquals(handler, Handler))
                OnPracticeLayoutSizeChanged(this, EventArgs.Empty);
        });
    }

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
        if (PracticeFormatPanel.IsVisible && PracticeFormatPicker.SelectedItem is { } practiceFormat)
            selections.Add(practiceFormat.ToString()!);
        if (LearningProfilePanel.IsVisible)
        {
            if (CurrentLearningProfile is not null && LearningGroupPicker.SelectedItem is { } group) selections.Add(group.ToString()!);
        }
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
        ChangePracticeSettingsButton.Text = TranslateQuiz(_practiceSettingsExpanded
            ? "Quiz.HideSettingsCompact" : "Quiz.ChangeSettingsCompact");
        string settingsDescription = TranslateQuiz(_practiceSettingsExpanded ? "Quiz.HideSettings" : "Quiz.ChangeSettings");
        SemanticProperties.SetDescription(ChangePracticeSettingsButton, settingsDescription);
        ToolTipProperties.SetText(ChangePracticeSettingsButton, settingsDescription);
        QueuePracticeSummaryLayout();
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

    private async void OnEssayWorkFocused(object? sender, FocusEventArgs e)
    {
        if (_currentQuestion is null) return;
        await ScrollToPracticeElementAsync(EssayWorkEditor, ScrollToPosition.MakeVisible);
    }

    private async void OnPracticeViewportSizeChanged(object? sender, EventArgs e)
    {
        // AdjustResize and rotation can move an already focused editor while
        // the question and draft stay intact.
        if (EssayWorkEditor.IsFocused)
            await ScrollToPracticeElementAsync(EssayWorkEditor, ScrollToPosition.MakeVisible);
    }

    private async Task ScrollToPracticeElementAsync(View target, ScrollToPosition position)
    {
        int version = ++_practiceScrollVersion;
        var question = _currentQuestion;
        (Rect Bounds, Size Content, double Height)? previous = null;
        // Focus, keyboard resizing and visibility changes invalidate the layout.
        // Wait for stable bounds without changing the user's settings visibility.
        for (int attempt = 0; attempt < 6; attempt++)
        {
            await Task.Delay(16);
            if (version != _practiceScrollVersion ||
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
        if (!CanUpdatePracticeLayout) return;
        double width = PracticeSummaryGrid.Width;
        if (width <= 0) return;
        UpdatePracticeSummaryLayout();
        if (!CanUpdatePracticeLayout) return;
        bool compact = QuizResponsiveLayout.UseCompactSettings(width, CurrentTextScale);
        if (_compactSettings != compact)
        {
            _compactSettings = compact;
            UpdateQuestionModeLayout();
            UpdateDifficultyLayout(compact);
        }
        UpdateElementaryChoiceLayout(_currentQuestion?.ElementaryProblem);
    }

    private void UpdatePracticeSummaryLayout()
    {
        if (!CanUpdatePracticeLayout) return;
        double width = PracticeSummaryGrid.Width;
        if (width <= 0) return;
        // Measure the actual localized labels, including system font scaling.
        double actionsWidth, summaryWidth;
        try
        {
            actionsWidth = ((IView)ChangePracticeSettingsButton).Measure(double.PositiveInfinity, double.PositiveInfinity).Width
                + ((IView)OpenAiQuestionBankButton).Measure(double.PositiveInfinity, double.PositiveInfinity).Width
                + PracticeSummaryActionsGrid.ColumnSpacing;
            summaryWidth = ((IView)PracticeSummaryLabel).Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
        }
        catch (ObjectDisposedException)
        {
            // Window services may already be disposed before Unloaded is raised.
            // Skip this cosmetic pass; Loaded/Appearing will request fresh layout.
            _practiceLayoutVersion++;
            _practiceLayoutLoaded = false;
            return;
        }
        bool inline = width >= actionsWidth + PracticeSummaryGrid.ColumnSpacing + Math.Min(summaryWidth, 320 * CurrentTextScale);
        bool stackActions = width < actionsWidth;
        if (_summaryLayout == (inline, stackActions)) return;
        _summaryLayout = (inline, stackActions);

        Grid.SetColumnSpan(PracticeSummaryLabel, inline ? 1 : 2);
        Grid.SetRow(PracticeSummaryActionsGrid, inline ? 0 : 1);
        Grid.SetColumn(PracticeSummaryActionsGrid, inline ? 1 : 0);
        Grid.SetColumnSpan(PracticeSummaryActionsGrid, inline ? 1 : 2);
        PracticeSummaryActionsGrid.Margin = inline ? Thickness.Zero : new Thickness(0, 6, 0, 0);
        PracticeSummaryActionsGrid.ColumnDefinitions.Clear();
        PracticeSummaryActionsGrid.RowDefinitions.Clear();
        PracticeSummaryActionsGrid.ColumnDefinitions.Add(new ColumnDefinition(inline ? GridLength.Auto : GridLength.Star));
        if (!stackActions)
            PracticeSummaryActionsGrid.ColumnDefinitions.Add(new ColumnDefinition(inline ? GridLength.Auto : GridLength.Star));
        PracticeSummaryActionsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        if (stackActions) PracticeSummaryActionsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Grid.SetRow(OpenAiQuestionBankButton, stackActions ? 1 : 0);
        Grid.SetColumn(OpenAiQuestionBankButton, stackActions ? 0 : 1);
        Grid.SetRow(AiQuestionBankProgressLabel, inline ? 1 : 2);
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
