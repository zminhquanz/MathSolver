using MathSolver.Services;

namespace MathSolver.Views;

public partial class DeveloperModePage : ContentPage
{
    private bool _isLoadingState;
    private bool _isClosing;
    private bool _hasPlayedEntryAnimation;

    public DeveloperModePage()
    {
        InitializeComponent();

        Shell.SetTabBarIsVisible(
            this,
            false);

        UpdateState();
        PreparePageEntryAnimation();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        AppLanguageManager.LanguageChanged +=
            OnStateChanged;

        DeveloperModeManager.DeveloperModeChanged +=
            OnStateChanged;

        UpdateState();

        if (!_hasPlayedEntryAnimation)
        {
            _hasPlayedEntryAnimation =
                true;

            Dispatcher.Dispatch(
                async () =>
                    await PlayPageEntryAnimationAsync());
        }
    }

    protected override void OnDisappearing()
    {
        AppLanguageManager.LanguageChanged -=
            OnStateChanged;

        DeveloperModeManager.DeveloperModeChanged -=
            OnStateChanged;

        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        _ =
            CloseAsync();

        return true;
    }

    private void OnStateChanged(
        object? sender,
        EventArgs e)
    {
        UpdateState();
    }

    private void UpdateState()
    {
        bool useEnglish =
            AppLanguageManager.CurrentLanguage ==
            AppLanguage.English;

        bool isEnabled =
            DeveloperModeManager.IsEnabled;

        _isLoadingState =
            true;

        DeveloperModeSwitch.IsToggled =
            isEnabled;

        _isLoadingState =
            false;

        Title =
            useEnglish
                ? "Developer mode"
                : "Chế độ nhà phát triển";

        PageTitleLabel.Text =
            useEnglish
                ? "DEVELOPER MODE"
                : "CHẾ ĐỘ NHÀ PHÁT TRIỂN";

        PageSubtitleLabel.Text =
            useEnglish
                ? "Manage JSON, validation logs, and technical details"
                : "Quản lý JSON, log kiểm tra và chi tiết kỹ thuật";

        SettingsSectionTitleLabel.Text =
            useEnglish
                ? "Developer tools"
                : "Công cụ dành cho nhà phát triển";

        DeveloperModeTitleLabel.Text =
            useEnglish
                ? "Developer mode"
                : "Chế độ nhà phát triển";

        DeveloperModeDescriptionLabel.Text =
            useEnglish
                ? "Show diagnostics used to inspect algorithms and LLM output."
                : "Hiện dữ liệu chẩn đoán phục vụ kiểm tra thuật toán và LLM.";

        DeveloperModeStateLabel.Text =
            (useEnglish, isEnabled) switch
            {
                (true, true) => "✓ ENABLED",
                (true, false) => "○ DISABLED",
                (false, true) => "✓ ĐANG BẬT",
                _ => "○ ĐANG TẮT"
            };

        DeveloperModeStateBadge.SetDynamicResource(
            Border.BackgroundColorProperty,
            isEnabled
                ? "PrimarySoftColor"
                : "SurfaceAltColor");

        DeveloperModeStateBadge.SetDynamicResource(
            Border.StrokeProperty,
            isEnabled
                ? "PrimaryBorderBrush"
                : "BorderBrush");

        DeveloperModeStateLabel.SetDynamicResource(
            Label.TextColorProperty,
            isEnabled
                ? "PrimaryColor"
                : "TextSecondaryColor");

        DefaultStateNoteLabel.Text =
            useEnglish
                ? "Debug builds default to on; Release/Publish builds default to off. Your choice is remembered."
                : "Bản Debug mặc định bật; bản Release/Publish mặc định tắt. Lựa chọn của bạn sẽ được ghi nhớ.";

        VisibleToolsTitleLabel.Text =
            useEnglish
                ? "Content shown while enabled"
                : "Nội dung được hiển thị khi bật";

        LlmToolsTitleLabel.Text =
            useEnglish
                ? "AI JSON and validation logs"
                : "JSON và log kiểm tra AI";

        LlmToolsDescriptionLabel.Text =
            useEnglish
                ? "Show LLM-generated JSON and each C# validation step."
                : "Hiện JSON do LLM tạo và từng bước validation của C#.";

        PowerToolsTitleLabel.Text =
            useEnglish
                ? "Power and root details"
                : "Chi tiết lũy thừa và căn bậc";

        PowerToolsDescriptionLabel.Text =
            useEnglish
                ? "Show the toggle and technical analysis of the calculation process."
                : "Hiện nút và nội dung phân tích kỹ thuật của quá trình tính toán.";

        SemanticProperties.SetDescription(
            DeveloperModeBackButton,
            useEnglish
                ? "Go back"
                : "Quay lại");

        SemanticProperties.SetDescription(
            DeveloperModeSwitch,
            useEnglish
                ? "Turn developer mode on or off"
                : "Bật hoặc tắt chế độ nhà phát triển");

        SemanticProperties.SetHint(
            DeveloperModeSwitch,
            (useEnglish, isEnabled) switch
            {
                (true, true) => "Developer mode is enabled. Activate to disable it.",
                (true, false) => "Developer mode is disabled. Activate to enable it.",
                (false, true) => "Chế độ nhà phát triển đang bật. Nhấn để tắt.",
                _ => "Chế độ nhà phát triển đang tắt. Nhấn để bật."
            });
    }

    private void OnDeveloperModeToggled(
        object? sender,
        ToggledEventArgs e)
    {
        if (_isLoadingState)
        {
            return;
        }

        DeveloperModeManager.SetEnabled(
            e.Value);

        UpdateState();
    }

    private void PreparePageEntryAnimation()
    {
        DeveloperModePageContentRoot.Opacity =
            0d;

        DeveloperModePageContentRoot.TranslationX =
            42d;

        DeveloperModePageContentRoot.Scale =
            0.995d;
    }

    private async Task PlayPageEntryAnimationAsync()
    {
        DeveloperModePageContentRoot.CancelAnimations();

        await Task.WhenAll(
            DeveloperModePageContentRoot.FadeToAsync(
                1d,
                190,
                Easing.CubicOut),

            DeveloperModePageContentRoot.TranslateToAsync(
                0d,
                0d,
                240,
                Easing.CubicOut),

            DeveloperModePageContentRoot.ScaleToAsync(
                1d,
                240,
                Easing.CubicOut));
    }

    private async Task PlayPageExitAnimationAsync()
    {
        DeveloperModePageContentRoot.CancelAnimations();

        await Task.WhenAll(
            DeveloperModePageContentRoot.FadeToAsync(
                0d,
                125,
                Easing.CubicIn),

            DeveloperModePageContentRoot.TranslateToAsync(
                34d,
                0d,
                155,
                Easing.CubicIn),

            DeveloperModePageContentRoot.ScaleToAsync(
                0.995d,
                155,
                Easing.CubicIn));
    }

    private async void OnCloseClicked(
        object? sender,
        EventArgs e)
    {
        await CloseAsync();
    }

    private async Task CloseAsync()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing =
            true;

        try
        {
            await PlayPageExitAnimationAsync();

            if (Shell.Current is AppShell appShell)
            {
                await appShell.CloseSettingsAsync(
                    this);

                return;
            }

            // Fallback only when this page is hosted without AppShell.
            // Pop the navigation stack directly; don't use Shell URI-back.
            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopAsync(
                    animated: false);
            }
        }
        finally
        {
            _isClosing =
                false;
        }
    }
}
