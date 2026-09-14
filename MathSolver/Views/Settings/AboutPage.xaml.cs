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
        UpdateSettingsIconTint();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        Shell.SetTabBarIsVisible(
            this,
            false);

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

    protected override void OnDisappearing()
    {
        Shell.SetTabBarIsVisible(
            this,
            true);

        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        _ =
            CloseAsync();

        return true;
    }

    private void PreparePageEntryAnimation()
    {
#if ANDROID
        // Material shared-axis style: enter from the trailing edge without
        // scaling the page surface. This keeps the motion lighter on phones.
        AboutPageContentRoot.Opacity =
            0d;

        AboutPageContentRoot.TranslationX =
            24d;

        AboutPageContentRoot.Scale =
            1d;
#else
        AboutPageContentRoot.Opacity =
            0d;

        AboutPageContentRoot.TranslationX =
            42d;

        AboutPageContentRoot.Scale =
            0.995d;
#endif
    }

    private async Task PlayPageEntryAnimationAsync()
    {
        AboutPageContentRoot.CancelAnimations();

#if ANDROID
        await Task.WhenAll(
            AboutPageContentRoot.FadeToAsync(
                1d,
                170,
                Easing.CubicOut),

            AboutPageContentRoot.TranslateToAsync(
                0d,
                0d,
                220,
                Easing.CubicOut));
#else
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
#endif
    }

    private async Task PlayPageExitAnimationAsync()
    {
        AboutPageContentRoot.CancelAnimations();

#if ANDROID
        await Task.WhenAll(
            AboutPageContentRoot.FadeToAsync(
                0d,
                110,
                Easing.CubicIn),

            AboutPageContentRoot.TranslateToAsync(
                24d,
                0d,
                150,
                Easing.CubicIn));
#else
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
#endif
    }

    private void UpdateSettingsIconTint()
    {
        if (!TryGetThemeColor(
                "TextPrimaryColor",
                out Color tintColor))
        {
            return;
        }

        GitHubIconTintBehavior.TintColor = tintColor;

    }

    private static bool TryGetThemeColor(
        string resourceKey,
        out Color color)
    {
        color =
            Colors.Transparent;

        if (Application.Current?.Resources is not
            ResourceDictionary resources)
        {
            return false;
        }

        return TryGetThemeColorNextStep(
            resources,
            resourceKey,
            out color);
    }

    private static bool TryGetThemeColorNextStep(
        ResourceDictionary resources,
        string resourceKey,
        out Color color)
    {
        // Mỗi phương thức có tham số out phải gán giá trị trên mọi
        // nhánh thoát, kể cả khi không tìm thấy resource.
        color =
            Colors.Transparent;

        if (resources.TryGetValue(
                resourceKey,
                out object? resourceValue))
        {
            if (resourceValue is Color resourceColor)
            {
                color =
                    resourceColor;

                return true;
            }

            if (resourceValue is SolidColorBrush resourceBrush)
            {
                color =
                    resourceBrush.Color;

                return true;
            }
        }

        // MergedDictionaries có kiểu ICollection<ResourceDictionary>,
        // nên không thể truy cập trực tiếp bằng toán tử [index].
        // Chép sang List để duyệt ngược theo đúng thứ tự ưu tiên.
        var mergedDictionaries =
            new List<ResourceDictionary>(
                resources.MergedDictionaries);

        for (int index =
                 mergedDictionaries.Count - 1;
             index >= 0;
             index--)
        {
            if (TryGetThemeColorNextStep(
                    mergedDictionaries[index],
                    resourceKey,
                    out color))
            {
                return true;
            }
        }

        return false;
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
#if ANDROID
            // Do not gate ACTION_VIEW behind Launcher.CanOpenAsync on Android.
            // Android package visibility can make the query return false even
            // though the system can resolve a browser when the intent is sent.
            // Start the standard browsable ACTION_VIEW intent directly.
            var androidUri =
                Android.Net.Uri.Parse(
                    url);

            using var intent =
                new Android.Content.Intent(
                    Android.Content.Intent.ActionView,
                    androidUri);

            intent.AddCategory(
                Android.Content.Intent.CategoryBrowsable);

            var activity =
                Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;

            if (activity is not null)
            {
                activity.StartActivity(
                    intent);
            }
            else
            {
                intent.AddFlags(
                    Android.Content.ActivityFlags.NewTask);

                Android.App.Application.Context.StartActivity(
                    intent);
            }

            return;
#else
            var uri =
                new Uri(
                    url);

            bool opened =
                await Launcher.Default.OpenAsync(
                    uri);

            if (opened)
            {
                return;
            }
#endif
        }
        catch
        {
            // Hiển thị thông báo thân thiện bên dưới.
        }

        await MaterialDialogService.ShowAlertAsync(
            this,
            LocalizationService.TranslateKey(
                "About.LinkErrorTitle"),

            LocalizationService.TranslateKey(
                "About.LinkErrorMessage"),

            LocalizationService.TranslateKey(
                "Common.OK"));
    }
}
