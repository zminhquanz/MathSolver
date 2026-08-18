using CommunityToolkit.Maui.Storage;
using MathSolver.Controls;
using MathSolver.Services;
using System.Globalization;

namespace MathSolver.Views;

public partial class GemmaModelCatalogPage : ContentPage
{
    internal static bool IsTransparentOverlayActive { get; private set; }

    private readonly TaskCompletionSource<Gemma4ModelDownloadSelection?>
        _downloadSelection =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

    private Gemma4ModelDescriptor _selectedModel =
        Gemma4ModelDownloadService.E2B;

    private Gemma4ModelDescriptor? _pendingDownloadModel;
    private string _downloadDirectory =
        Gemma4ModelDownloadService.GetDefaultModelsDirectory();

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
        PrepareDownloadConfirmationAnimation();
    }

    public Task<Gemma4ModelDownloadSelection?> WaitForDownloadSelectionAsync() =>
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

#if ANDROID
        const double catalogHorizontalInset = 16d;
        const double catalogVerticalInset = 16d;
        const double confirmHorizontalInset = 24d;
#else
        const double catalogHorizontalInset = 32d;
        const double catalogVerticalInset = 32d;
        const double confirmHorizontalInset = 40d;
#endif

        CatalogPanel.WidthRequest =
            Math.Max(
                320d,
                Math.Min(
                    980d,
                    width - catalogHorizontalInset));

        CatalogPanel.MaximumHeightRequest =
            Math.Max(
                320d,
                height - catalogVerticalInset);

        DownloadConfirmPanel.WidthRequest =
            Math.Max(
                300d,
                Math.Min(
                    560d,
                    width - confirmHorizontalInset));

        bool useCompactLayout = width < 860d;

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
                new ColumnDefinition(new GridLength(330d)));
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
        if (DownloadConfirmOverlay.IsVisible)
        {
            _ = HideDownloadConfirmationAsync();
            return true;
        }

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
                UpdateDownloadConfirmationContent();
            });
    }

    private void RefreshLocalizedContent()
    {
        CatalogTitleLabel.Text = T("Quiz.ModelCatalogTitle");
        CatalogSubtitleLabel.Text = T("Quiz.ModelCatalogSubtitle");
        GuideTitleLabel.Text = T("Quiz.ModelCatalogGuideTitle");
        GuideBodyLabel.Text = T("Quiz.ModelCatalogGuideBody");
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

        E2BCardHintLabel.Text = T("Quiz.ModelCatalogE2BRecommendation");
        E4BCardHintLabel.Text = T("Quiz.ModelCatalogE4BRecommendation");

        RowInteractionHintLabel.Text =
            T("Quiz.ModelCatalogInteractionHint");

        ReadmeMathSolverHeadingLabel.Text =
            T("Quiz.ModelCatalogMathSolverHeading");
        ReadmeMathSolverBodyLabel.Text =
            T("Quiz.ModelCatalogMathSolverBody");
        ReadmeRecommendationHeadingLabel.Text =
            T("Quiz.ModelCatalogRecommendationHeading");
        ReadmeFileHeadingLabel.Text = T("Quiz.ModelCatalogFileHeading");
        FooterHintLabel.Text = T("Quiz.ModelCatalogFooterHint");

        DownloadE2BButton.Text = T("Quiz.DownloadActionShort");
        DownloadE4BButton.Text = T("Quiz.DownloadActionShort");

        DownloadConfirmTitleLabel.Text = T("Quiz.DownloadPathPopupTitle");
        DownloadConfirmSubtitleLabel.Text = T("Quiz.DownloadPathPopupSubtitle");
        DownloadConfirmModelHeadingLabel.Text = T("Quiz.DownloadPathPopupModelHeading");
        DownloadConfirmPathHeadingLabel.Text = T("Quiz.DownloadPathPopupPathHeading");
        ChooseDownloadFolderButton.Text = T("Quiz.DownloadPathPopupChooseFolder");
        ResetDownloadFolderButton.Text = T("Quiz.DownloadPathPopupResetDefault");
        DownloadConfirmFinalFileHeadingLabel.Text = T("Quiz.DownloadPathPopupFinalFileHeading");
        DownloadConfirmNoteLabel.Text = T("Quiz.DownloadPathPopupNote");
        ConfirmCancelButton.Text = T("Quiz.Cancel");
        ConfirmDownloadButton.Text = T("Quiz.DownloadAction");

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

    private void UpdateDownloadConfirmationContent()
    {
        if (_pendingDownloadModel is null)
        {
            return;
        }

        DownloadConfirmModelNameLabel.Text = _pendingDownloadModel.DisplayName;
        DownloadConfirmModelMetaLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                T("Quiz.DownloadPathPopupModelMeta"),
                FormatGigabytes(_pendingDownloadModel.ApproximateSizeBytes),
                _pendingDownloadModel.FileName);
        DownloadConfirmFolderValueLabel.Text =
            _downloadDirectory;

        DownloadConfirmPathValueLabel.Text =
            Gemma4ModelDownloadService.GetDestinationPath(
                _pendingDownloadModel,
                _downloadDirectory);
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
        await ShowDownloadConfirmationAsync(
            Gemma4ModelDownloadService.E2B);

    private async void OnDownloadE4BClicked(
        object? sender,
        EventArgs e) =>
        await ShowDownloadConfirmationAsync(
            Gemma4ModelDownloadService.E4B);

    private async Task ShowDownloadConfirmationAsync(
        Gemma4ModelDescriptor model)
    {
        _pendingDownloadModel = model;
        SelectModel(model);

        if (string.IsNullOrWhiteSpace(_downloadDirectory))
        {
            _downloadDirectory =
                Gemma4ModelDownloadService.GetDefaultModelsDirectory();
        }

        UpdateDownloadConfirmationContent();

        if (DownloadConfirmOverlay.IsVisible)
        {
            return;
        }

        DownloadConfirmOverlay.IsVisible = true;
        DownloadConfirmOverlay.InputTransparent = false;
        DownloadConfirmPanel.Scale = 0.96d;
        DownloadConfirmPanel.TranslationY = 16d;

        await Task.WhenAll(
            DownloadConfirmOverlay.FadeToAsync(1d, 140, Easing.CubicOut),
            DownloadConfirmPanel.FadeToAsync(1d, 140, Easing.CubicOut),
            DownloadConfirmPanel.ScaleToAsync(1d, 180, Easing.CubicOut),
            DownloadConfirmPanel.TranslateToAsync(0d, 0d, 180, Easing.CubicOut));
    }

    private async Task HideDownloadConfirmationAsync()
    {
        if (!DownloadConfirmOverlay.IsVisible)
        {
            return;
        }

        DownloadConfirmOverlay.InputTransparent = true;

        await Task.WhenAll(
            DownloadConfirmOverlay.FadeToAsync(0d, 110, Easing.CubicIn),
            DownloadConfirmPanel.FadeToAsync(0d, 110, Easing.CubicIn),
            DownloadConfirmPanel.ScaleToAsync(0.98d, 130, Easing.CubicIn),
            DownloadConfirmPanel.TranslateToAsync(0d, 10d, 130, Easing.CubicIn));

        _pendingDownloadModel = null;
        DownloadConfirmOverlay.IsVisible = false;
        PrepareDownloadConfirmationAnimation();
    }

    private async void OnChooseDownloadFolderClicked(
        object? sender,
        EventArgs e)
    {
        try
        {
            string initialPath =
                Directory.Exists(_downloadDirectory)
                    ? _downloadDirectory
                    : FileSystem.AppDataDirectory;

            FolderPickerResult result =
                await FolderPicker.Default.PickAsync(
                    initialPath,
                    CancellationToken.None);

            if (!result.IsSuccessful)
            {
                if (result.Exception is not null &&
                    result.Exception is not OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Folder picker failed: {result.Exception}");

                    await DisplayAlertAsync(
                        T("Quiz.DownloadPathPopupFolderPickerFailedTitle"),
                        T("Quiz.DownloadPathPopupFolderPickerFailedMessage"),
                        T("Quiz.ModelCatalogClose"));
                }

                return;
            }

            string selectedDirectory =
                result.Folder.Path;

            if (!await CanWriteToDirectoryAsync(selectedDirectory))
            {
                await DisplayAlertAsync(
                    T("Quiz.DownloadPathPopupFolderNotWritableTitle"),
                    T("Quiz.DownloadPathPopupFolderNotWritableMessage"),
                    T("Quiz.ModelCatalogClose"));
                return;
            }

            _downloadDirectory =
                Path.GetFullPath(selectedDirectory);
            UpdateDownloadConfirmationContent();
        }
        catch (OperationCanceledException)
        {
            // User closed the native folder picker. Keep the current path.
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Could not choose model download folder: {exception}");

            await DisplayAlertAsync(
                T("Quiz.DownloadPathPopupFolderPickerFailedTitle"),
                T("Quiz.DownloadPathPopupFolderPickerFailedMessage"),
                T("Quiz.ModelCatalogClose"));
        }
    }

    private void OnResetDownloadFolderClicked(
        object? sender,
        EventArgs e)
    {
        _downloadDirectory =
            Gemma4ModelDownloadService.GetDefaultModelsDirectory();
        UpdateDownloadConfirmationContent();
    }

    private static async Task<bool> CanWriteToDirectoryAsync(
        string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return false;
        }

        string? probePath = null;

        try
        {
            string fullPath =
                Path.GetFullPath(directoryPath);

            if (!Directory.Exists(fullPath))
            {
                return false;
            }

            probePath =
                Path.Combine(
                    fullPath,
                    $".mathsolver-write-test-{Guid.NewGuid():N}.tmp");

            await using (var probe =
                new FileStream(
                    probePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    1,
                    useAsync: true))
            {
                await probe.WriteAsync(new byte[] { 0 });
                await probe.FlushAsync();
            }

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(probePath) &&
                File.Exists(probePath))
            {
                try
                {
                    File.Delete(probePath);
                }
                catch
                {
                    // Best effort cleanup only.
                }
            }
        }
    }

    private async void OnConfirmDownloadClicked(
        object? sender,
        EventArgs e)
    {
        if (_pendingDownloadModel is null)
        {
            return;
        }

        await CloseAsync(
            new Gemma4ModelDownloadSelection(
                _pendingDownloadModel,
                _downloadDirectory));
    }

    private async void OnDownloadConfirmOverlayTapped(
        object? sender,
        TappedEventArgs e) =>
        await HideDownloadConfirmationAsync();

    private async void OnCancelDownloadClicked(
        object? sender,
        EventArgs e) =>
        await HideDownloadConfirmationAsync();

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
        TappedEventArgs e)
    {
        if (DownloadConfirmOverlay.IsVisible)
        {
            await HideDownloadConfirmationAsync();
            return;
        }

        await CloseAsync(null);
    }

    private async void OnCloseClicked(
        object? sender,
        EventArgs e)
    {
        if (DownloadConfirmOverlay.IsVisible)
        {
            await HideDownloadConfirmationAsync();
            return;
        }

        await CloseAsync(null);
    }

    private void PrepareOpenAnimation()
    {
        OverlayScrim.Opacity = 0d;
        CatalogPanel.Opacity = 0d;
        CatalogPanel.Scale = 0.96d;
        CatalogPanel.TranslationY = 18d;
    }

    private void PrepareDownloadConfirmationAnimation()
    {
        DownloadConfirmOverlay.Opacity = 0d;
        DownloadConfirmPanel.Opacity = 0d;
        DownloadConfirmPanel.Scale = 0.96d;
        DownloadConfirmPanel.TranslationY = 18d;
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

    private async Task CloseAsync(Gemma4ModelDownloadSelection? selection)
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
            DownloadConfirmOverlay.CancelAnimations();
            DownloadConfirmPanel.CancelAnimations();

            if (DownloadConfirmOverlay.IsVisible)
            {
                DownloadConfirmOverlay.Opacity = 0d;
                DownloadConfirmOverlay.IsVisible = false;
            }

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
        $"GGUF • QAT Q4_0 • {FormatGigabytes(model.ApproximateSizeBytes)} GB";

    private static string T(string key) =>
        LocalizationService.TranslateKey(key);
}
