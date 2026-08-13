using MathSolver.Controls;
using MathSolver.Services;
using System.Globalization;

namespace MathSolver.Views;

public partial class GemmaModelCatalogPage : ContentPage
{
    internal static bool IsTransparentOverlayActive { get; private set; }

    private readonly TaskCompletionSource<Gemma4ModelDescriptor?>
        _downloadSelection =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

    private Gemma4ModelDescriptor _selectedModel =
        Gemma4ModelDownloadService.E2B;

    private bool _isClosing;
    private bool _hasPlayedOpenAnimation;
    private bool? _isCompactLayout;

    public GemmaModelCatalogPage()
    {
        IsTransparentOverlayActive = true;

        InitializeComponent();

        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, false);

        InteractiveButtonAnimation.SetIsScopeEnabled(
            this,
            true);

        LocalizationService.CultureChanged += OnCultureChanged;

        RefreshLocalizedContent();
        SelectModel(Gemma4ModelDownloadService.E2B);
        PrepareOpenAnimation();
    }

    public Task<Gemma4ModelDescriptor?> WaitForDownloadSelectionAsync() =>
        _downloadSelection.Task;

    protected override void OnAppearing()
    {
        base.OnAppearing();

        IsTransparentOverlayActive = true;

        if (_hasPlayedOpenAnimation)
        {
            return;
        }

        _hasPlayedOpenAnimation = true;

        Dispatcher.Dispatch(
            async () =>
                await PlayOpenAnimationAsync());
    }

    protected override void OnDisappearing()
    {
        IsTransparentOverlayActive = false;
        LocalizationService.CultureChanged -= OnCultureChanged;

        if (!_isClosing)
        {
            _downloadSelection.TrySetResult(null);
        }

        base.OnDisappearing();
    }

    protected override void OnSizeAllocated(
        double width,
        double height)
    {
        base.OnSizeAllocated(width, height);

        CatalogPanel.WidthRequest =
            Math.Max(
                300d,
                Math.Min(
                    940d,
                    width - 32d));

        CatalogPanel.MaximumHeightRequest =
            Math.Max(
                300d,
                height - 32d);

        bool useCompactLayout = width < 760d;

        if (_isCompactLayout == useCompactLayout)
        {
            return;
        }

        _isCompactLayout = useCompactLayout;

        CatalogBodyGrid.ColumnDefinitions.Clear();
        CatalogBodyGrid.RowDefinitions.Clear();

        if (useCompactLayout)
        {
            CatalogBodyGrid.ColumnDefinitions.Add(
                new ColumnDefinition(GridLength.Star));
            CatalogBodyGrid.RowDefinitions.Add(
                new RowDefinition(GridLength.Auto));
            CatalogBodyGrid.RowDefinitions.Add(
                new RowDefinition(GridLength.Star));

            Grid.SetColumn(ModelListPanel, 0);
            Grid.SetRow(ModelListPanel, 0);
            Grid.SetColumn(ReadmePanel, 0);
            Grid.SetRow(ReadmePanel, 1);
        }
        else
        {
            CatalogBodyGrid.ColumnDefinitions.Add(
                new ColumnDefinition(new GridLength(310d)));
            CatalogBodyGrid.ColumnDefinitions.Add(
                new ColumnDefinition(GridLength.Star));
            CatalogBodyGrid.RowDefinitions.Add(
                new RowDefinition(GridLength.Star));

            Grid.SetColumn(ModelListPanel, 0);
            Grid.SetRow(ModelListPanel, 0);
            Grid.SetColumn(ReadmePanel, 1);
            Grid.SetRow(ReadmePanel, 0);
        }
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    private void OnCultureChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Dispatch(
            () =>
            {
                RefreshLocalizedContent();
                UpdateReadme();
            });
    }

    private void RefreshLocalizedContent()
    {
        CatalogTitleLabel.Text = T("Quiz.ModelCatalogTitle");
        CatalogSubtitleLabel.Text = T("Quiz.ModelCatalogSubtitle");
        ModelsHeadingLabel.Text = T("Quiz.ModelCatalogModelsHeading");
        ReadmeHeadingLabel.Text = T("Quiz.ModelCatalogReadmeHeading");

        E2BTitleLabel.Text = T("Quiz.ModelCatalogE2BTitle");
        E2BDescriptionLabel.Text = T("Quiz.ModelCatalogE2BDescription");
        E4BTitleLabel.Text = T("Quiz.ModelCatalogE4BTitle");
        E4BDescriptionLabel.Text = T("Quiz.ModelCatalogE4BDescription");

        E2BMetadataLabel.Text =
            FormatModelMetadata(
                Gemma4ModelDownloadService.E2B);

        E4BMetadataLabel.Text =
            FormatModelMetadata(
                Gemma4ModelDownloadService.E4B);

        RowInteractionHintLabel.Text =
            T("Quiz.ModelCatalogInteractionHint");

        ReadmeMathSolverHeadingLabel.Text =
            T("Quiz.ModelCatalogMathSolverHeading");
        ReadmeMathSolverBodyLabel.Text =
            T("Quiz.ModelCatalogMathSolverBody");
        ReadmeRecommendationHeadingLabel.Text =
            T("Quiz.ModelCatalogRecommendationHeading");
        ReadmeFileHeadingLabel.Text =
            T("Quiz.ModelCatalogFileHeading");

        UpdateAccessibilityText();
    }

    private void SelectModel(Gemma4ModelDescriptor model)
    {
        _selectedModel = model;

        bool isE2B = model.Variant == Gemma4ModelVariant.E2B;

        ApplyRowStyle(E2BModelBorder, isE2B);
        ApplyRowStyle(E4BModelBorder, !isE2B);

        UpdateReadme();
        UpdateAccessibilityText();
    }

    private static void ApplyRowStyle(
        Border border,
        bool isSelected)
    {
        border.SetDynamicResource(
            Border.BackgroundColorProperty,
            isSelected
                ? "PrimarySoftColor"
                : "SurfaceAltColor");

        border.SetDynamicResource(
            Border.StrokeProperty,
            isSelected
                ? "PrimaryBorderBrush"
                : "BorderBrush");

        border.StrokeThickness = isSelected ? 2d : 1d;
    }

    private void UpdateReadme()
    {
        bool isE2B =
            _selectedModel.Variant == Gemma4ModelVariant.E2B;

        ReadmeModelTitleLabel.Text = _selectedModel.DisplayName;
        ReadmeSummaryLabel.Text =
            T(isE2B
                ? "Quiz.ModelCatalogE2BSummary"
                : "Quiz.ModelCatalogE4BSummary");

        ReadmeRecommendationBodyLabel.Text =
            T(isE2B
                ? "Quiz.ModelCatalogE2BRecommendation"
                : "Quiz.ModelCatalogE4BRecommendation");

        ReadmeFileBodyLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                T("Quiz.ModelCatalogFileBody"),
                _selectedModel.FileName,
                FormatGigabytes(_selectedModel.ApproximateSizeBytes));
    }

    private void UpdateAccessibilityText()
    {
        string openPageText =
            string.Format(
                CultureInfo.CurrentCulture,
                T("Quiz.ModelCatalogOpenPage"),
                GetVariantName(_selectedModel));

        ToolTipProperties.SetText(OpenModelPageButton, openPageText);
        SemanticProperties.SetDescription(OpenModelPageButton, openPageText);

        SetDownloadAccessibility(
            DownloadE2BButton,
            Gemma4ModelDownloadService.E2B);

        SetDownloadAccessibility(
            DownloadE4BButton,
            Gemma4ModelDownloadService.E4B);
    }

    private static void SetDownloadAccessibility(
        Button button,
        Gemma4ModelDescriptor model)
    {
        string text =
            string.Format(
                CultureInfo.CurrentCulture,
                T("Quiz.ModelCatalogDownloadModel"),
                GetVariantName(model));

        ToolTipProperties.SetText(button, text);
        SemanticProperties.SetDescription(button, text);
    }

    private void OnE2BRowTapped(
        object? sender,
        TappedEventArgs e) =>
        SelectModel(Gemma4ModelDownloadService.E2B);

    private void OnE4BRowTapped(
        object? sender,
        TappedEventArgs e) =>
        SelectModel(Gemma4ModelDownloadService.E4B);

    private async void OnDownloadE2BClicked(
        object? sender,
        EventArgs e) =>
        await CloseAsync(Gemma4ModelDownloadService.E2B);

    private async void OnDownloadE4BClicked(
        object? sender,
        EventArgs e) =>
        await CloseAsync(Gemma4ModelDownloadService.E4B);

    private async void OnOpenModelPageClicked(
        object? sender,
        EventArgs e)
    {
        try
        {
            bool canOpen =
                await Launcher.Default.CanOpenAsync(
                    _selectedModel.ModelPageUri);

            if (canOpen &&
                await Launcher.Default.OpenAsync(
                    _selectedModel.ModelPageUri))
            {
                return;
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Could not open the Gemma 4 model website: {exception}");
        }

        await DisplayAlertAsync(
            T("Quiz.ModelCatalogOpenFailedTitle"),
            T("Quiz.OpenModelWebsiteFailed"),
            T("Quiz.ModelCatalogClose"));
    }

    private async void OnOutsideTapped(
        object? sender,
        TappedEventArgs e) =>
        await CloseAsync(null);

    private async void OnCloseClicked(
        object? sender,
        EventArgs e) =>
        await CloseAsync(null);

    private void PrepareOpenAnimation()
    {
        OverlayScrim.Opacity = 0d;
        CatalogPanel.Opacity = 0d;
        CatalogPanel.Scale = 0.96d;
        CatalogPanel.TranslationY = 18d;
    }

    private async Task PlayOpenAnimationAsync()
    {
        OverlayScrim.CancelAnimations();
        CatalogPanel.CancelAnimations();

        await Task.WhenAll(
            OverlayScrim.FadeToAsync(1d, 160, Easing.CubicOut),
            CatalogPanel.FadeToAsync(1d, 150, Easing.CubicOut),
            CatalogPanel.ScaleToAsync(1d, 210, Easing.CubicOut),
            CatalogPanel.TranslateToAsync(0d, 0d, 210, Easing.CubicOut));
    }

    private async Task CloseAsync(Gemma4ModelDescriptor? selection)
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        OverlayRoot.InputTransparent = true;

        try
        {
            OverlayScrim.CancelAnimations();
            CatalogPanel.CancelAnimations();

            await Task.WhenAll(
                OverlayScrim.FadeToAsync(0d, 125, Easing.CubicIn),
                CatalogPanel.FadeToAsync(0d, 115, Easing.CubicIn),
                CatalogPanel.ScaleToAsync(0.97d, 145, Easing.CubicIn),
                CatalogPanel.TranslateToAsync(0d, 14d, 145, Easing.CubicIn));

            if (Navigation.ModalStack.Contains(this))
            {
                await Navigation.PopModalAsync(animated: false);
            }

            _downloadSelection.TrySetResult(selection);
        }
        finally
        {
            _isClosing = false;
        }
    }

    private static string GetVariantName(Gemma4ModelDescriptor model) =>
        model.Variant == Gemma4ModelVariant.E2B
            ? "E2B"
            : "E4B";

    private static string FormatGigabytes(long bytes) =>
        (bytes / 1_000_000_000d).ToString(
            "0.00",
            CultureInfo.CurrentCulture);

    private static string FormatModelMetadata(
        Gemma4ModelDescriptor model) =>
        $"GGUF  •  QAT Q4_0  •  " +
        $"{FormatGigabytes(model.ApproximateSizeBytes)} GB";

    private static string T(string key) =>
        LocalizationService.TranslateKey(key);
}
