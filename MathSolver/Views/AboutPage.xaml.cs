using MathSolver.Services;

namespace MathSolver.Views;

public partial class AboutPage : ContentPage
{
    private const string GitHubUrl =
        "https://github.com/zminhquanz/MathSolver";

    private const string KoFiUrl =
        "https://ko-fi.com/quanvu96";

    private const string FallbackDisplayVersion =
        "0.1.1";

    private bool _isClosing;
    private bool _hasPlayedEntryAnimation;

    public AboutPage()
    {
        InitializeComponent();

        Shell.SetNavBarIsVisible(
            this,
            true);

        Shell.SetTabBarIsVisible(
            this,
            false);

        LocalizationService.ExcludeSubtreeFromLegacyTracking(
            this);

        LocalizationService.Initialize();

        UpdateAppInformation();
        PreparePageEntryAnimation();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        UpdateAppInformation();

        if (!_hasPlayedEntryAnimation)
        {
            _hasPlayedEntryAnimation =
                true;

            Dispatcher.Dispatch(
                async () =>
                    await PlayPageEntryAnimationAsync());
        }
    }

    protected override bool OnBackButtonPressed()
    {
        _ =
            CloseAsync();

        return true;
    }

    private void PreparePageEntryAnimation()
    {
        AboutPageContentRoot.Opacity =
            0d;

        AboutPageContentRoot.TranslationX =
            42d;

        AboutPageContentRoot.Scale =
            0.995d;
    }

    private async Task PlayPageEntryAnimationAsync()
    {
        AboutPageContentRoot.CancelAnimations();

        await Task.WhenAll(
            AboutPageContentRoot.FadeToAsync(
                1d,
                190,
                Easing.CubicOut),

            AboutPageContentRoot.TranslateToAsync(
                0d,
                0d,
                240,
                Easing.CubicOut),

            AboutPageContentRoot.ScaleToAsync(
                1d,
                240,
                Easing.CubicOut));
    }

    private async Task PlayPageExitAnimationAsync()
    {
        AboutPageContentRoot.CancelAnimations();

        await Task.WhenAll(
            AboutPageContentRoot.FadeToAsync(
                0d,
                125,
                Easing.CubicIn),

            AboutPageContentRoot.TranslateToAsync(
                34d,
                0d,
                155,
                Easing.CubicIn),

            AboutPageContentRoot.ScaleToAsync(
                0.995d,
                155,
                Easing.CubicIn));
    }

    private void UpdateAppInformation()
    {
        string displayVersion =
            GetDisplayVersion();

        VersionHeaderLabel.Text =
            $"v{displayVersion}";

        VersionValueLabel.Text =
            $"v{displayVersion}";
    }

    /// <summary>
    /// Windows có thể trả VersionString theo dạng bốn thành phần,
    /// chẳng hạn 0.1.1.0. Phiên bản công khai của Math Solver
    /// dùng ba thành phần nên AboutPage chỉ hiển thị 0.1.1.
    /// </summary>
    private static string GetDisplayVersion()
    {
        string rawVersion =
            AppInfo.Current.VersionString;

        if (string.IsNullOrWhiteSpace(
                rawVersion))
        {
            return FallbackDisplayVersion;
        }

        string normalized =
            rawVersion.Trim()
                .TrimStart(
                    'v',
                    'V');

        string[] parts =
            normalized.Split(
                '.',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

        if (parts.Length >=
            3)
        {
            return string.Join(
                '.',
                parts.Take(
                    3));
        }

        return parts.Length >
               0
            ? string.Join(
                '.',
                parts)
            : FallbackDisplayVersion;
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
            AboutBackButton.IsEnabled =
                false;

            AboutPageContentRoot.InputTransparent =
                true;

            await PlayPageExitAnimationAsync();

            if (Navigation.ModalStack.Contains(
                    this))
            {
                await Navigation.PopModalAsync(
                    animated:
                        false);

                return;
            }

            if (Shell.Current is not null)
            {
                await Shell.Current.GoToAsync(
                    "..",
                    animate:
                        false);
            }
        }
        finally
        {
            _isClosing =
                false;

            AboutBackButton.IsEnabled =
                true;

            AboutPageContentRoot.InputTransparent =
                false;
        }
    }

    private async void OnGitHubClicked(
        object? sender,
        EventArgs e)
    {
        await OpenExternalUrlAsync(
            GitHubUrl);
    }

    private async void OnKoFiClicked(
        object? sender,
        EventArgs e)
    {
        await OpenExternalUrlAsync(
            KoFiUrl);
    }

    private async Task OpenExternalUrlAsync(
        string url)
    {
        try
        {
            var uri =
                new Uri(
                    url);

            bool canOpen =
                await Launcher.Default.CanOpenAsync(
                    uri);

            bool opened =
                canOpen &&
                await Launcher.Default.OpenAsync(
                    uri);

            if (opened)
            {
                return;
            }
        }
        catch
        {
            // Hiển thị thông báo thân thiện bên dưới.
        }

        await DisplayAlertAsync(
            LocalizationService.TranslateKey(
                "About.LinkErrorTitle"),

            LocalizationService.TranslateKey(
                "About.LinkErrorMessage"),

            LocalizationService.TranslateKey(
                "Common.OK"));
    }
}
