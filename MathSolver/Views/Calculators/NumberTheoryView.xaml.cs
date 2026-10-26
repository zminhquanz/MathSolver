using System.Globalization;
using System.Numerics;
using MathSolver.Controls;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Views.Base;

namespace MathSolver.Views;

public partial class NumberTheoryView : LocalizedSolverView
{
    private bool _findLcm;
    private NumberTheoryCalculation? _calculation;
    private PrimeFactorLesson? _primeLesson;
    private string? _errorKey;

    public NumberTheoryView()
    {
        InitializeComponent();
        LocalizationService.ExcludeSubtreeFromLegacyTracking(this);
        InitializeLocalization();
        SelectMode(false);
    }

    protected override void RefreshLocalizedContent()
    {
        base.RefreshLocalizedContent();
        if (_calculation is not null) RenderResult();
        if (_errorKey is not null) ErrorLabel.Text = T(_errorKey);
        SemanticProperties.SetDescription(FirstEntry, T("NumberTheory.FirstNumber"));
        SemanticProperties.SetDescription(SecondEntry, T("NumberTheory.SecondNumber"));
    }

    private static string T(string key) => LocalizationService.TranslateKey(key);
    private static string F(string key, params object[] values) =>
        string.Format(CultureInfo.CurrentCulture, T(key), values);
    private static string N(BigInteger value) => value.ToString(CultureInfo.InvariantCulture);

    private void OnGcdClicked(object? sender, EventArgs e) => SelectMode(false);
    private void OnLcmClicked(object? sender, EventArgs e) => SelectMode(true);

    private void SelectMode(bool findLcm)
    {
        _findLcm = findLcm;
        SelectionButtonStyler.Select(findLcm ? LcmButton : GcdButton, GcdButton, LcmButton);
        CopyButton.IsCopied = false;
        if (_calculation is not null) RenderResult();
    }

    private void OnInputChanged(object? sender, TextChangedEventArgs e)
    {
        _calculation = null;
        _primeLesson = null;
        _errorKey = null;
        // TextChanged can run while XAML is still creating the input controls.
        if (ResultBorder is null) return;
        ResultBorder.IsVisible = false;
        ErrorBorder.IsVisible = false;
        CopyButton.IsCopied = false;
        StepsLayout.Clear();
        PrimeStepsLayout.Clear();
    }

    private void OnFirstCompleted(object? sender, EventArgs e) => SecondEntry.Focus();

    private void OnCalculateClicked(object? sender, EventArgs e)
    {
        if (!NumberTheoryEngine.TryParseInput(FirstEntry.Text, out var a))
        {
            ShowError("NumberTheory.InvalidFirst");
            return;
        }
        if (!NumberTheoryEngine.TryParseInput(SecondEntry.Text, out var b))
        {
            ShowError("NumberTheory.InvalidSecond");
            return;
        }
        if (a.IsZero && b.IsZero)
        {
            ShowError("NumberTheory.BothZero");
            return;
        }
        _errorKey = null;
        ErrorBorder.IsVisible = false;
        CopyButton.IsCopied = false;
        _calculation = NumberTheoryEngine.Calculate(a, b);
        _primeLesson = a.IsZero || b.IsZero ? null : NumberTheoryEngine.ExplainByPrimeFactors(a, b);
        RenderResult();
        SolverWorkspace.RevealResult(ResultBorder);
    }

    private void ShowError(string key)
    {
        _calculation = null;
        _primeLesson = null;
        _errorKey = key;
        ResultBorder.IsVisible = false;
        ErrorLabel.Text = T(key);
        ErrorBorder.IsVisible = true;
    }

    private void RenderResult()
    {
        var result = _calculation!;
        string a = N(result.First), b = N(result.Second), gcd = N(result.Gcd);
        string symbol = T(_findLcm ? "NumberTheory.Lcm" : "NumberTheory.Gcd");
        ResultTitle.Text = $"{symbol}({a}; {b})";
        ResultLabel.Text = N(_findLcm ? result.Lcm : result.Gcd);
        StepsLayout.Clear();
        var AddStep = CreateStepAppender(StepsLayout);

        if (result.First.IsZero || result.Second.IsZero)
        {
            AddStep(F("NumberTheory.ZeroGcd", gcd));
        }
        else
        {
            AddStep(T("NumberTheory.EuclidIntro"));
            foreach (var division in result.Divisions)
                AddStep($"{N(division.Dividend)} = {N(division.Divisor)} × {N(division.Quotient)} + {N(division.Remainder)}");
            AddStep(F("NumberTheory.GcdConclusion", gcd));
        }

        if (_findLcm)
        {
            if (result.Lcm.IsZero) AddStep(T("NumberTheory.ZeroLcm"));
            else
            {
                AddStep(T("NumberTheory.LcmFormula"));
                AddStep($"{symbol}({a}; {b}) = {a} ÷ {gcd} × {b}\n"
                    + $"= {N(result.First / result.Gcd)} × {b} = {N(result.Lcm)}");
            }
        }
        RenderPrimeSteps(result, symbol);
        ResultBorder.IsVisible = true;
    }

    private static Action<string> CreateStepAppender(VerticalStackLayout layout)
    {
        int number = 0;
        return text =>
        {
            var title = new Label { Text = F("NumberTheory.Step", ++number), FontSize = 16, FontAttributes = FontAttributes.Bold };
            title.SetDynamicResource(Label.TextColorProperty, "ContentAccentTextColor");
            var body = new Label { Text = text, FontSize = 18, LineBreakMode = LineBreakMode.CharacterWrap };
            body.SetDynamicResource(Label.TextColorProperty, "WallpaperTextPrimaryColor");
            var stack = new VerticalStackLayout { Spacing = 6, Padding = 12, Children = { title, body } };
            stack.SetDynamicResource(BackgroundColorProperty, "WallpaperSurfaceAltColor");
            layout.Add(stack);
        };
    }

    private static string PrimeProduct(IReadOnlyList<PrimePower> factors, bool expand = false)
    {
        if (factors.Count == 0) return "1";
        if (expand && factors.Sum(f => f.Exponent) <= 16)
            return string.Join(" × ", factors.SelectMany(f => Enumerable.Repeat(N(f.Prime), f.Exponent)));
        return string.Join(" × ", factors.Select(f => N(f.Prime) + (f.Exponent == 1 ? ""
            : string.Concat(f.Exponent.ToString(CultureInfo.InvariantCulture).Select(c => "⁰¹²³⁴⁵⁶⁷⁸⁹"[c - '0'])))));
    }

    private void RenderPrimeSteps(NumberTheoryCalculation calculation, string symbol)
    {
        PrimeStepsLayout.Clear();
        var addStep = CreateStepAppender(PrimeStepsLayout);
        if (_primeLesson is not { } lesson)
        {
            addStep(T("NumberTheory.PrimeZero"));
            addStep(F("NumberTheory.ZeroGcd", N(calculation.Gcd)));
            if (_findLcm) addStep(T("NumberTheory.ZeroLcm"));
            return;
        }

        foreach (var factorization in new[] { lesson.First, lesson.Second })
        {
            string equation;
            if (factorization.Value.IsOne) equation = T("NumberTheory.PrimeOne");
            else if (factorization.IsComplete)
            {
                string powers = PrimeProduct(factorization.Factors);
                string expanded = PrimeProduct(factorization.Factors, expand: true);
                equation = N(factorization.Value) + " = " + (expanded == powers ? "" : expanded + " = ") + powers;
            }
            else
            {
                string known = factorization.Factors.Count == 0 ? "" : PrimeProduct(factorization.Factors) + " × ";
                equation = $"{N(factorization.Value)} = {known}({N(factorization.UnfactoredPart)})\n"
                    + F("NumberTheory.PrimeUnfinished", N(factorization.UnfactoredPart));
            }
            addStep(T("NumberTheory.PrimeDecompose") + "\n" + equation);
        }

        if (!lesson.IsComplete)
        {
            addStep(T("NumberTheory.PrimeUseEuclid"));
            return;
        }
        var chosenFactors = _findLcm ? lesson.AllFactors : lesson.CommonFactors;
        addStep(T(_findLcm ? "NumberTheory.PrimeLcmRule"
            : chosenFactors.Count == 0 ? "NumberTheory.PrimeNoCommon" : "NumberTheory.PrimeGcdRule"));
        string chosen = PrimeProduct(chosenFactors), answer = N(_findLcm ? calculation.Lcm : calculation.Gcd);
        addStep($"{symbol}({N(calculation.First)}; {N(calculation.Second)}) = {chosen}"
            + (chosen == answer ? "" : " = " + answer));
    }

    private async void OnCopyClicked(object? sender, EventArgs e) =>
        await ResultClipboardService.CopyAsync(CopyButton, _calculation is { } result
            ? N(_findLcm ? result.Lcm : result.Gcd) : null);

    private void OnClearClicked(object? sender, EventArgs e)
    {
        FirstEntry.Text = string.Empty;
        SecondEntry.Text = string.Empty;
        OnInputChanged(this, new TextChangedEventArgs(null, null));
        FirstEntry.Focus();
    }

    private void OnInputsSizeChanged(object? sender, EventArgs e)
    {
        bool narrow = ResponsiveLayoutPolicy.UseStackedLayout(InputsGrid.Width, 540d);
        InputsGrid.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : GridLength.Star;
        Grid.SetColumn(SecondInput, narrow ? 0 : 1);
        Grid.SetRow(SecondInput, narrow ? 1 : 0);
    }

    private void OnMethodsSizeChanged(object? sender, EventArgs e)
    {
        bool narrow = ResponsiveLayoutPolicy.UseStackedLayout(MethodsGrid.Width, 900d);
        MethodsGrid.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : GridLength.Star;
        Grid.SetColumn(EuclidMethodPanel, narrow ? 0 : 1);
        Grid.SetRow(EuclidMethodPanel, narrow ? 1 : 0);
    }
}
