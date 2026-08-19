using MathSolver.Controls;
using MathSolver.Services;
using MathSolver.Graphics;
using MathSolver.Models;
using System.Collections.ObjectModel;
using Microsoft.Maui.Devices;

namespace MathSolver.Views;

public partial class FormulaPage : ContentPage
{
    private int _mainTabAnimationVersion;

    private bool _isMainTabTransitioning;

    private FormulaSubTab _selectedSubTab = FormulaSubTab.UnknownComponent;

    private bool _isSubTabTransitioning;

    private VisualElement FormulaSubTabAnimationHost
    {
        get
        {
#if ANDROID
            return AndroidFormulaSubTabBar;
#else
            return FormulaSubTabBar;
#endif
        }
    }

    // BindableLayout của Tìm thành phần chưa biết cũng chỉ được tạo một lần.
    // Không tháo/gắn lại ItemsSource khi chuyển tab vì thao tác đó tạo ra một
    // frame tạm nơi sáu card bị co vào cùng một hàng rồi mới giãn lại.
    private bool _isUnknownComponentLayoutInitialized;

    private double _lastUnknownComponentLayoutWidth =
        -1d;

    // GeometryFlexLayout và các GraphicsView chỉ được khởi tạo một lần.
    // Khi rời rồi quay lại tab Công thức, không gán lại ItemsSource nên
    // các GraphicsView cũ được giữ nguyên, tránh vẽ lại và nháy giao diện.
    private bool _isGeometryLayoutInitialized;

    private double _lastPlaneGeometryLayoutWidth =
        -1d;

    private double _lastSolidGeometryLayoutWidth =
        -1d;

    // Được bật khi trang Hình học bị che hoặc rời khỏi visual tree, ví dụ
    // khi mở SettingsMenuPage. Khi quay lại cần khôi phục layout và yêu cầu
    // từng GraphicsView vẽ lại lớp native của nó.
    private bool _geometryNeedsVisualRestore;

    public ObservableCollection<GeometryFormulaItem> PlaneGeometryItems { get; } = [];

    public ObservableCollection<GeometryFormulaItem> SolidGeometryItems { get; } = [];

    public ObservableCollection<UnknownComponentItem> UnknownComponentItems { get; } = [];

    public FormulaPage()
    {
        InitializeComponent();

        InteractiveButtonAnimation.SetIsScopeEnabled(
            this,
            true);

        LocalizationService.Attach(
            this);

        AppLanguageManager.LanguageChanged +=
            OnLanguageChanged;

        BindingContext =
            this;

        BindableLayout.SetItemsSource(
            UnknownComponentFlexLayout,
            UnknownComponentItems);

        BindableLayout.SetItemsSource(
            PlaneGeometryFlexLayout,
            PlaneGeometryItems);

        BindableLayout.SetItemsSource(
            SolidGeometryFlexLayout,
            SolidGeometryItems);

        SelectFormulaSubTab(
            FormulaSubTab.UnknownComponent);

        Dispatcher.Dispatch(
            () => LocalizationService.Attach(
                this));
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

        // OnDisappearing có thể đã ẩn GeometryContent để lớp native của
        // GraphicsView không đè lên trang khác. Khôi phục trạng thái hiển thị
        // ngay khi quay lại, kể cả khi trang đích trước đó là SettingsMenuPage.
        RestoreSelectedSubTabVisualState();

        // Chuẩn bị nội dung đang chọn trước. Không tạo lại GeometryItems nếu
        // visual tree cũ vẫn còn; phần redraw GraphicsView được xử lý riêng.
        PrepareSelectedSubTabForMainAppearance();

        BeginMainTabTransitionIfPending();

        if (_selectedSubTab == FormulaSubTab.Geometry)
        {
            ScheduleGeometryVisualRestore();
        }
    }

    protected override void OnDisappearing()
    {
        _mainTabAnimationVersion++;
        _isMainTabTransitioning =
            false;

        CancelMainTabAnimations();

        // SettingsMenuPage là visual-tree overlay trong suốt. Khi mở nó, FormulaPage vẫn là
        // phần nền đang được nhìn qua lớp scrim, vì vậy tuyệt đối không đặt
        // GeometryContent.Opacity = 0. Chỉ ẩn Hình học khi thật sự rời tab
        // Công thức để chuyển sang một trang/tab chính khác.
        bool keepGeometryVisibleBehindSettings =
            SettingsMenuPage.IsTransparentOverlayActive;

        if (_selectedSubTab ==
            FormulaSubTab.Geometry)
        {
            if (keepGeometryVisibleBehindSettings)
            {
                _geometryNeedsVisualRestore =
                    false;

                GeometryContent.IsVisible =
                    true;

                ResetTransitionTransform(
                    GeometryContent);

                // Giữ lớp native của GraphicsView hoạt động phía dưới scrim.
                InvalidateGeometryGraphicsViews();
            }
            else
            {
                _geometryNeedsVisualRestore =
                    true;

                GeometryContent.Opacity =
                    0d;

                GeometryContent.TranslationX =
                    0d;

                GeometryContent.Scale =
                    1d;
            }
        }
        else
        {
            ResetTransitionTransform(
                GeometryContent);
        }

        ResetTransitionTransform(
            UnknownComponentContent);

        ResetTransitionTransform(
            ProportionContent);

        ResetTransitionTransform(
            MotionContent);

        ResetTransitionTransform(
            FormulaSubTabAnimationHost);

        // Root không còn là animation host. Giữ root ở trạng thái chuẩn để
        // GraphicsView không phải đi qua transform của toàn bộ trang.
        FormulaPageContentRoot.Opacity =
            1d;

        FormulaPageContentRoot.TranslationX =
            0d;

        FormulaPageContentRoot.Scale =
            1d;

        base.OnDisappearing();
    }

    private void RestoreSelectedSubTabVisualState()
    {
        UnknownComponentContent.IsVisible =
            _selectedSubTab ==
            FormulaSubTab.UnknownComponent;

        ProportionContent.IsVisible =
            _selectedSubTab ==
            FormulaSubTab.Proportion;

        MotionContent.IsVisible =
            _selectedSubTab ==
            FormulaSubTab.Motion;

        GeometryContent.IsVisible =
            _selectedSubTab ==
            FormulaSubTab.Geometry;

        ResetTransitionTransform(
            FormulaSubTabAnimationHost);

        ResetTransitionTransform(
            GetFormulaSubTabContent(
                _selectedSubTab));

        UpdateSubTabButtonStyles();
    }

    private void ScheduleGeometryVisualRestore()
    {
        if (!_isGeometryLayoutInitialized ||
            _selectedSubTab !=
            FormulaSubTab.Geometry ||
            _isMainTabTransitioning)
        {
            return;
        }

        // Khi trang xuất hiện lần đầu cờ có thể chưa bật, nhưng invalidate vẫn
        // an toàn. Khi quay lại từ Settings, cờ này xác nhận visual native đã
        // từng bị ẩn và cần khôi phục đầy đủ.
        bool requiresFullRestore =
            _geometryNeedsVisualRestore;

        // Đợi MAUI gắn lại trang vào Window/Shell. Sau đó ép layout và
        // invalidate từng GraphicsView. Lượt Dispatch thứ hai xử lý trường hợp
        // WinUI vừa tạo lại composition surface sau khi đóng SettingsMenuPage.
        Dispatcher.Dispatch(
            () =>
            {
                RestoreGeometryVisualTree(
                    requiresFullRestore);

                Dispatcher.Dispatch(
                    () => RestoreGeometryVisualTree(
                        requiresFullRestore));
            });
    }

    private void RestoreGeometryVisualTree(
        bool requiresFullRestore)
    {
        if (_selectedSubTab !=
            FormulaSubTab.Geometry)
        {
            return;
        }

        GeometryContent.IsVisible =
            true;

        ResetTransitionTransform(
            GeometryContent);

        // Fallback hiếm: chỉ gắn lại ItemsSource của nhóm bị mất visual tree.
        // Mỗi nhóm được giữ độc lập để hình phẳng và hình không gian không làm
        // tạo lại GraphicsView của nhau.
        if (requiresFullRestore)
        {
            RestoreGeometryLayoutItemsIfNeeded(
                PlaneGeometryFlexLayout,
                PlaneGeometryItems);

            RestoreGeometryLayoutItemsIfNeeded(
                SolidGeometryFlexLayout,
                SolidGeometryItems);
        }

        _lastPlaneGeometryLayoutWidth =
            -1d;

        _lastSolidGeometryLayoutWidth =
            -1d;

        UpdateGeometryCardWidthsIfNeeded(
            force: true);

        PlaneGeometryFlexLayout.InvalidateMeasure();
        SolidGeometryFlexLayout.InvalidateMeasure();
        GeometryContent.InvalidateMeasure();

        InvalidateGeometryGraphicsViews();

        _geometryNeedsVisualRestore =
            false;
    }

    private static void RestoreGeometryLayoutItemsIfNeeded(
        FlexLayout layout,
        ObservableCollection<GeometryFormulaItem> items)
    {
        if (items.Count == 0 ||
            layout.Children.Count > 0)
        {
            return;
        }

        BindableLayout.SetItemsSource(
            layout,
            null);

        BindableLayout.SetItemsSource(
            layout,
            items);
    }


    private void InvalidateGeometryGraphicsViews()
    {
        InvalidateGeometryGraphicsViews(
            PlaneGeometryFlexLayout);

        InvalidateGeometryGraphicsViews(
            SolidGeometryFlexLayout);
    }

    private static void InvalidateGeometryGraphicsViews(
        FlexLayout layout)
    {
        foreach (IView cardView
                 in layout.Children)
        {
            if (cardView is not Border card ||
                card.Content is not Grid cardGrid)
            {
                continue;
            }

            foreach (IView childView
                     in cardGrid.Children)
            {
                if (childView is GraphicsView graphicsView)
                {
                    graphicsView.Invalidate();
                }
            }
        }
    }


    private void PrepareSelectedSubTabForMainAppearance()
    {
        if (_selectedSubTab ==
            FormulaSubTab.UnknownComponent)
        {
            if (UnknownComponentItems.Count == 0)
            {
                CreateUnknownComponentItems();
            }

            RefreshUnknownComponentLayout(
                force: true);
            return;
        }

        if (_selectedSubTab ==
            FormulaSubTab.Geometry)
        {
            EnsureGeometryLayoutInitialized();
        }

        // Không gọi UpdateGeometryCardWidthsIfNeeded ở mỗi OnAppearing.
        // Width đã được lưu và chỉ cập nhật khi SizeChanged thật sự xảy ra.
    }

    private void BeginMainTabTransitionIfPending()
    {
        VisualElement activeContent =
            GetFormulaSubTabContent(
                _selectedSubTab);

        if (Shell.Current is not AppShell appShell ||
            !appShell.TryConsumeMainTabTransition(
                "FormulaPage",
                out int direction))
        {
            // Trường hợp quay lại từ Settings hoặc lần xuất hiện không phải
            // đổi tab chính: khôi phục vùng Hình học đã được ẩn khi rời trang.
            ResetTransitionTransform(
                FormulaSubTabAnimationHost);

            ResetTransitionTransform(
                activeContent);

            return;
        }

        int animationVersion =
            ++_mainTabAnimationVersion;

        _isMainTabTransitioning =
            true;

        direction =
            direction >= 0
                ? 1
                : -1;

        CancelMainTabAnimations();

        // Không animate FormulaPageContentRoot. Khi Hình học đang hiện,
        // GraphicsView có thể không đi cùng transform/opacity của root trên
        // mọi handler. Animate trực tiếp thanh tab con và vùng nội dung đang
        // hiện — đây cũng chính là host đã hoạt động ổn khi đổi tab con.
        PrepareMainTabAnimationHost(
            FormulaSubTabAnimationHost,
            direction);

        PrepareMainTabAnimationHost(
            activeContent,
            direction);

        Dispatcher.Dispatch(
            async () =>
                await PlayPreparedMainTabTransitionAsync(
                    animationVersion,
                    activeContent));
    }

    private async Task PlayPreparedMainTabTransitionAsync(
        int animationVersion,
        VisualElement activeContent)
    {
        // Nhường một lượt để Shell gắn trang đích. Hai host vẫn đang ẩn nên
        // không có frame UI hoàn chỉnh xuất hiện trước animation.
        await Task.Yield();

        if (animationVersion !=
            _mainTabAnimationVersion)
        {
            return;
        }

        if (_selectedSubTab ==
            FormulaSubTab.UnknownComponent)
        {
            UpdateUnknownComponentCardWidthsIfNeeded(
                force: true);

            // Content vẫn Opacity = 0 ở thời điểm này. Chờ layout hoàn tất
            // rồi mới bắt đầu fade/slide để không lộ frame card bị co nhỏ.
            await Task.Yield();

            if (animationVersion !=
                _mainTabAnimationVersion)
            {
                return;
            }
        }

        try
        {
            await Task.WhenAll(
                AnimatePreparedMainTabHostAsync(
                    FormulaSubTabAnimationHost),

                AnimatePreparedMainTabHostAsync(
                    activeContent));
        }
        finally
        {
            if (animationVersion ==
                _mainTabAnimationVersion)
            {
                ResetTransitionTransform(
                    FormulaSubTabAnimationHost);

                ResetTransitionTransform(
                    activeContent);

                _isMainTabTransitioning =
                    false;

                // Chỉ kiểm tra lại kích thước sau animation. Không tái tạo
                // GeometryItems. Riêng tab Hình học sẽ invalidate GraphicsView
                // sau khi animation đã kết thúc để không làm mất hiệu ứng.
                RefreshSelectedFormulaSubTabLayout();

                if (_selectedSubTab ==
                    FormulaSubTab.Geometry)
                {
                    ScheduleGeometryVisualRestore();
                }
            }
        }
    }

    private static void PrepareMainTabAnimationHost(
        VisualElement host,
        int direction)
    {
        host.CancelAnimations();

        host.Opacity =
            0d;

        host.TranslationX =
            direction *
            44d;

        host.Scale =
            0.985d;
    }

    private static Task AnimatePreparedMainTabHostAsync(
        VisualElement host)
    {
        return Task.WhenAll(
            host.FadeToAsync(
                1d,
                175,
                Easing.CubicOut),

            host.TranslateToAsync(
                0d,
                0d,
                250,
                Easing.CubicOut),

            host.ScaleToAsync(
                1d,
                250,
                Easing.CubicOut));
    }

    private void CancelMainTabAnimations()
    {
        FormulaSubTabAnimationHost.CancelAnimations();
        UnknownComponentContent.CancelAnimations();
        ProportionContent.CancelAnimations();
        MotionContent.CancelAnimations();
        GeometryContent.CancelAnimations();
    }

    private void RefreshUnknownComponentLayout(
        bool force = false)
    {
        if (UnknownComponentItems.Count == 0)
        {
            CreateUnknownComponentItems();
        }

        _isUnknownComponentLayoutInitialized =
            true;

        // Cập nhật ngay nếu layout đã có Width, trước khi animation làm nội
        // dung hiện ra. Các lượt Dispatch chỉ hoàn thiện phép đo, tuyệt đối
        // không tạo lại card hoặc tháo/gắn lại ItemsSource.
        UpdateUnknownComponentCardWidthsIfNeeded(
            force);

        Dispatcher.Dispatch(
            () =>
            {
                UpdateUnknownComponentCardWidthsIfNeeded(
                    force: true);

                Dispatcher.Dispatch(
                    () =>
                    {
                        UpdateUnknownComponentCardWidthsIfNeeded(
                            force: true);

                        LocalizationService.Attach(
                            this);
                    });
            });
    }

    private void UpdateUnknownComponentCardWidthsIfNeeded(
        bool force = false)
    {
        if (!_isUnknownComponentLayoutInitialized)
        {
            return;
        }

        double availableWidth =
            UnknownComponentFlexLayout.Width;

        if (availableWidth <= 0d)
        {
            return;
        }

        if (!force &&
            UnknownComponentFlexLayout.Children.Count > 0 &&
            Math.Abs(
                availableWidth -
                _lastUnknownComponentLayoutWidth) < 0.5d)
        {
            return;
        }

        _lastUnknownComponentLayoutWidth =
            availableWidth;

        UpdateUnknownComponentCardWidths(
            availableWidth);
    }

    private void EnsureGeometryLayoutInitialized()
    {
        if (_isGeometryLayoutInitialized)
        {
            return;
        }

        if (PlaneGeometryItems.Count == 0 &&
            SolidGeometryItems.Count == 0)
        {
            CreateGeometryItems();
        }

        // Hai ItemsSource đã được gắn một lần trong constructor. Không tháo/gắn
        // lại khi đổi tab để giữ nguyên toàn bộ GraphicsView đã được tạo.
        _isGeometryLayoutInitialized =
            true;

        Dispatcher.Dispatch(
            () =>
            {
                UpdateGeometryCardWidthsIfNeeded(
                    force: true);

                Dispatcher.Dispatch(
                    () =>
                    {
                        UpdateGeometryCardWidthsIfNeeded(
                            force: true);

                        LocalizationService.Attach(
                            this);
                    });
            });
    }


    private void UpdateGeometryCardWidthsIfNeeded(
        bool force = false)
    {
        UpdateGeometryCardWidthsIfNeeded(
            PlaneGeometryFlexLayout,
            ref _lastPlaneGeometryLayoutWidth,
            force);

        UpdateGeometryCardWidthsIfNeeded(
            SolidGeometryFlexLayout,
            ref _lastSolidGeometryLayoutWidth,
            force);
    }

    private void UpdateGeometryCardWidthsIfNeeded(
        FlexLayout layout,
        ref double lastLayoutWidth,
        bool force)
    {
        double availableWidth =
            layout.Width;

        if (availableWidth <= 0d)
        {
            return;
        }

        if (!force &&
            Math.Abs(
                availableWidth -
                lastLayoutWidth) < 0.5d)
        {
            return;
        }

        lastLayoutWidth =
            availableWidth;

        UpdateGeometryCardWidths(
            layout,
            availableWidth);
    }


    private async void OnUnknownComponentTabClicked(
        object? sender,
        EventArgs e)
    {
        await SwitchFormulaSubTabAsync(
            FormulaSubTab.UnknownComponent);
    }

    private async void OnGeometryTabClicked(
        object? sender,
        EventArgs e)
    {
        await SwitchFormulaSubTabAsync(
            FormulaSubTab.Geometry);
    }

    private async void OnProportionTabClicked(
        object? sender,
        EventArgs e)
    {
        await SwitchFormulaSubTabAsync(
            FormulaSubTab.Proportion);
    }

    private async void OnMotionTabClicked(
        object? sender,
        EventArgs e)
    {
        await SwitchFormulaSubTabAsync(
            FormulaSubTab.Motion);
    }

    private async Task SwitchFormulaSubTabAsync(
        FormulaSubTab selectedTab)
    {
        if (_isSubTabTransitioning ||
            _isMainTabTransitioning)
        {
            return;
        }

        if (_selectedSubTab ==
            selectedTab)
        {
            return;
        }

        _isSubTabTransitioning =
            true;

        try
        {
            FormulaSubTab previousTab =
                _selectedSubTab;

            VisualElement outgoingContent =
                GetFormulaSubTabContent(
                    previousTab);

            VisualElement incomingContent =
                GetFormulaSubTabContent(
                    selectedTab);

            int direction =
                (int)selectedTab >
                (int)previousTab
                    ? 1
                    : -1;

            PrepareFormulaSubTabContent(
                selectedTab);

            outgoingContent.CancelAnimations();
            incomingContent.CancelAnimations();

            _selectedSubTab =
                selectedTab;

            UpdateSubTabButtonStyles();

            incomingContent.IsVisible =
                true;

            incomingContent.Opacity =
                0d;

            incomingContent.TranslationX =
                direction *
                28d;

            incomingContent.Scale =
                0.995d;

            // Cho BindableLayout của tab đích tạo children trước khi
            // bắt đầu fade/slide để hình học không xuất hiện trễ.
            await Task.Yield();

            if (selectedTab ==
                FormulaSubTab.UnknownComponent)
            {
                UpdateUnknownComponentCardWidthsIfNeeded(
                    force: true);

                // Vẫn giữ incomingContent ẩn cho đến khi card đã về đúng
                // ba cột, tránh hiện thoáng qua sáu card bị co trên một hàng.
                await Task.Yield();
            }

            await Task.WhenAll(
                outgoingContent.FadeToAsync(
                    0d,
                    85,
                    Easing.CubicIn),

                outgoingContent.TranslateToAsync(
                    direction *
                    -18d,
                    0d,
                    85,
                    Easing.CubicIn));

            outgoingContent.IsVisible =
                false;

            ResetTransitionTransform(
                outgoingContent);

            await Task.WhenAll(
                incomingContent.FadeToAsync(
                    1d,
                    150,
                    Easing.CubicOut),

                incomingContent.TranslateToAsync(
                    0d,
                    0d,
                    190,
                    Easing.CubicOut),

                incomingContent.ScaleToAsync(
                    1d,
                    190,
                    Easing.CubicOut));

            RefreshSelectedFormulaSubTabLayout();

#if ANDROID
            await ScrollAndroidFormulaSubTabIntoViewAsync(
                selectedTab);
#endif
        }
        finally
        {
            _isSubTabTransitioning =
                false;
        }
    }

    private void SelectFormulaSubTab(
        FormulaSubTab selectedTab)
    {
        _selectedSubTab =
            selectedTab;

        UnknownComponentContent.IsVisible =
            selectedTab ==
            FormulaSubTab.UnknownComponent;

        ProportionContent.IsVisible =
            selectedTab ==
            FormulaSubTab.Proportion;

        MotionContent.IsVisible =
            selectedTab ==
            FormulaSubTab.Motion;

        GeometryContent.IsVisible =
            selectedTab ==
            FormulaSubTab.Geometry;

        ResetTransitionTransform(
            UnknownComponentContent);

        ResetTransitionTransform(
            ProportionContent);

        ResetTransitionTransform(
            MotionContent);

        ResetTransitionTransform(
            GeometryContent);

        UpdateSubTabButtonStyles();

        PrepareFormulaSubTabContent(
            selectedTab);
    }

    private void PrepareFormulaSubTabContent(
        FormulaSubTab selectedTab)
    {
        if (selectedTab ==
            FormulaSubTab.UnknownComponent)
        {
            if (UnknownComponentItems.Count == 0)
            {
                CreateUnknownComponentItems();
            }

            RefreshUnknownComponentLayout(
                force: true);
            return;
        }

        if (selectedTab ==
            FormulaSubTab.Geometry)
        {
            // Chỉ tạo card và GraphicsView ở lần mở tab Hình học đầu tiên.
            // Những lần chuyển tab sau chỉ đổi IsVisible và chạy animation.
            EnsureGeometryLayoutInitialized();
        }
    }

    private void RefreshSelectedFormulaSubTabLayout()
    {
        if (_selectedSubTab ==
            FormulaSubTab.UnknownComponent)
        {
            UpdateUnknownComponentCardWidthsIfNeeded();
        }
        else if (_selectedSubTab ==
                 FormulaSubTab.Geometry)
        {
            UpdateGeometryCardWidthsIfNeeded();
        }
    }

    private VisualElement GetFormulaSubTabContent(
        FormulaSubTab tab)
    {
        return tab switch
        {
            FormulaSubTab.UnknownComponent =>
                UnknownComponentContent,

            FormulaSubTab.Proportion =>
                ProportionContent,

            FormulaSubTab.Motion =>
                MotionContent,

            FormulaSubTab.Geometry =>
                GeometryContent,

            _ =>
                UnknownComponentContent
        };
    }

    private static void ResetTransitionTransform(
        VisualElement content)
    {
        content.Opacity =
            1d;

        content.TranslationX =
            0d;

        content.Scale =
            1d;
    }

    private void UpdateSubTabButtonStyles()
    {
#if ANDROID
        ApplyAndroidSubTabState(
            AndroidUnknownComponentTabButton,
            AndroidUnknownComponentTabIndicator,
            _selectedSubTab == FormulaSubTab.UnknownComponent);

        ApplyAndroidSubTabState(
            AndroidProportionTabButton,
            AndroidProportionTabIndicator,
            _selectedSubTab == FormulaSubTab.Proportion);

        ApplyAndroidSubTabState(
            AndroidMotionTabButton,
            AndroidMotionTabIndicator,
            _selectedSubTab == FormulaSubTab.Motion);

        ApplyAndroidSubTabState(
            AndroidFormulaGeometryTabButton,
            AndroidFormulaGeometryTabIndicator,
            _selectedSubTab == FormulaSubTab.Geometry);
#else
        ResetSubTabButton(UnknownComponentTabButton);
        ResetSubTabButton(ProportionTabButton);
        ResetSubTabButton(MotionTabButton);
        ResetSubTabButton(GeometryTabButton);

        Button selectedButton =
            _selectedSubTab switch
            {
                FormulaSubTab.UnknownComponent => UnknownComponentTabButton,
                FormulaSubTab.Proportion => ProportionTabButton,
                FormulaSubTab.Motion => MotionTabButton,
                FormulaSubTab.Geometry => GeometryTabButton,
                _ => UnknownComponentTabButton
            };

        selectedButton.SetDynamicResource(
            Button.BackgroundColorProperty,
            "PrimaryColor");

        selectedButton.SetDynamicResource(
            Button.TextColorProperty,
            "OnPrimaryColor");
#endif
    }

#if ANDROID
    private static void ApplyAndroidSubTabState(
        Button button,
        BoxView indicator,
        bool isSelected)
    {
        button.SetDynamicResource(
            Button.TextColorProperty,
            isSelected
                ? "PrimaryColor"
                : "TextSecondaryColor");

        button.BackgroundColor =
            Microsoft.Maui.Graphics.Colors.Transparent;

        // Android Material/DevCheck state must be deterministic.  Keep the
        // indicator permanently bound to the accent color and only toggle
        // visibility.  This avoids the Transparent -> DynamicResource +
        // opacity animation race that could leave later tabs with no line.
        indicator.SetDynamicResource(
            BoxView.BackgroundColorProperty,
            "PrimaryColor");

        indicator.CancelAnimations();
        indicator.Opacity = 1d;
        indicator.Scale = 1d;
        indicator.IsVisible = isSelected;
    }

    private Button GetAndroidFormulaSubTabButton(
        FormulaSubTab tab)
    {
        return tab switch
        {
            FormulaSubTab.UnknownComponent => AndroidUnknownComponentTabButton,
            FormulaSubTab.Proportion => AndroidProportionTabButton,
            FormulaSubTab.Motion => AndroidMotionTabButton,
            FormulaSubTab.Geometry => AndroidFormulaGeometryTabButton,
            _ => AndroidUnknownComponentTabButton
        };
    }

    private async Task ScrollAndroidFormulaSubTabIntoViewAsync(
        FormulaSubTab tab)
    {
        try
        {
            await AndroidFormulaSubTabScrollView.ScrollToAsync(
                GetAndroidFormulaSubTabButton(tab),
                ScrollToPosition.Center,
                true);
        }
        catch (InvalidOperationException)
        {
            // Trang có thể vừa rời visual tree khi đổi tab chính.
        }
    }
#else
    private static void ResetSubTabButton(Button button)
    {
        button.SetDynamicResource(
            Button.BackgroundColorProperty,
            "SurfaceAltColor");

        button.SetDynamicResource(
            Button.TextColorProperty,
            "TextPrimaryColor");
    }
#endif

    protected override void OnSizeAllocated(
        double width,
        double height)
    {
        base.OnSizeAllocated(
            width,
            height);

        if (width <= 0)
        {
            return;
        }

        Dispatcher.Dispatch(
            () =>
            {
                if (_selectedSubTab ==
                    FormulaSubTab.UnknownComponent)
                {
                    UpdateUnknownComponentCardWidthsIfNeeded();
                }
                else if (_selectedSubTab ==
                         FormulaSubTab.Geometry)
                {
                    UpdateGeometryCardWidthsIfNeeded();
                }
            });
    }

    private void OnUnknownComponentFlexLayoutSizeChanged(
        object? sender,
        EventArgs e)
    {
        if (!_isUnknownComponentLayoutInitialized ||
            sender is not FlexLayout)
        {
            return;
        }

        UpdateUnknownComponentCardWidthsIfNeeded();
    }

    private void UpdateUnknownComponentCardWidths(
        double availableWidth)
    {
        if (availableWidth <= 0)
        {
            return;
        }

        // Giữ đúng ba thẻ mỗi hàng trên desktop. Khi cửa sổ thật sự hẹp mới
        // hạ xuống hai hoặc một cột để nội dung không bị ép và mất khả năng đọc.
        int columnCount =
            availableWidth switch
            {
                >= 900d => 3,
                >= 580d => 2,
                _ => 1
            };

        UpdateFlexCardWidths(
            UnknownComponentFlexLayout,
            availableWidth,
            columnCount,
            horizontalMarginPerCard: 14d,
            minimumCardWidth: 250d);

        ScheduleUnknownComponentFlexHeightUpdate(
            columnCount);
    }

    private void ScheduleUnknownComponentFlexHeightUpdate(
        int columnCount)
    {
        if (columnCount <= 0 ||
            UnknownComponentFlexLayout.Children.Count == 0)
        {
            return;
        }

        // Lượt đầu dùng kích thước hiện có; lượt sau chạy khi các card đã được
        // đo lại theo WidthRequest mới. Nhờ vậy phần lưu ý bám sát hàng card cuối.
        Dispatcher.Dispatch(
            () =>
            {
                UpdateUnknownComponentFlexHeight(
                    columnCount);

                Dispatcher.Dispatch(
                    () => UpdateUnknownComponentFlexHeight(
                        columnCount));
            });
    }

    private void UpdateUnknownComponentFlexHeight(
        int columnCount)
    {
        int childCount =
            UnknownComponentFlexLayout.Children.Count;

        if (childCount == 0 ||
            columnCount <= 0)
        {
            return;
        }

        const double verticalMarginPerCard =
            14d;

        const double fallbackCardHeight =
            360d;

        double maximumCardHeight =
            fallbackCardHeight;

        foreach (IView childView
                 in UnknownComponentFlexLayout.Children)
        {
            if (childView is not VisualElement card)
            {
                continue;
            }

            double measuredHeight =
                card.Height;

            if (!double.IsFinite(
                    measuredHeight) ||
                measuredHeight <= 0d)
            {
                measuredHeight =
                    Math.Max(
                        fallbackCardHeight,
                        card.MinimumHeightRequest);
            }

            maximumCardHeight =
                Math.Max(
                    maximumCardHeight,
                    measuredHeight);
        }

        int rowCount =
            (int)Math.Ceiling(
                childCount /
                (double)columnCount);

        double requestedHeight =
            Math.Ceiling(
                rowCount *
                (maximumCardHeight +
                 verticalMarginPerCard));

        if (Math.Abs(
                UnknownComponentFlexLayout.HeightRequest -
                requestedHeight) < 1d)
        {
            return;
        }

        UnknownComponentFlexLayout.HeightRequest =
            requestedHeight;

        UnknownComponentFlexLayout.InvalidateMeasure();

        if (UnknownComponentFlexLayout.Parent
            is VisualElement parent)
        {
            parent.InvalidateMeasure();
        }
    }

    private void OnGeometryFlexLayoutSizeChanged(
        object? sender,
        EventArgs e)
    {
        if (!_isGeometryLayoutInitialized ||
            sender is not FlexLayout layout)
        {
            return;
        }

        if (ReferenceEquals(
                layout,
                PlaneGeometryFlexLayout))
        {
            UpdateGeometryCardWidthsIfNeeded(
                layout,
                ref _lastPlaneGeometryLayoutWidth,
                force: false);
        }
        else if (ReferenceEquals(
                     layout,
                     SolidGeometryFlexLayout))
        {
            UpdateGeometryCardWidthsIfNeeded(
                layout,
                ref _lastSolidGeometryLayoutWidth,
                force: false);
        }
    }


    private void UpdateGeometryCardWidths(
        FlexLayout layout,
        double availableWidth)
    {
        if (availableWidth <= 0d)
        {
            return;
        }

        int columnCount =
            GetGeometryColumnCount(
                availableWidth);

        double requestedWidth =
            UpdateFlexCardWidths(
                layout,
                availableWidth,
                columnCount,
                horizontalMarginPerCard: 10d,
                minimumCardWidth: 112d);

        if (requestedWidth <= 0d)
        {
            return;
        }

        UpdateGeometryCardVisualSizes(
            layout,
            requestedWidth);

        ScheduleGeometryFlexHeightUpdate(
            layout,
            columnCount);
    }


    private void ScheduleGeometryFlexHeightUpdate(
        FlexLayout layout,
        int columnCount)
    {
        if (columnCount <= 0 ||
            layout.Children.Count == 0)
        {
            return;
        }

        // WidthRequest và chiều cao GraphicsView vừa thay đổi. Đo hai lượt:
        // lượt đầu sau layout hiện tại, lượt sau khi card đã có Height cuối cùng.
        Dispatcher.Dispatch(
            () =>
            {
                UpdateGeometryFlexHeight(
                    layout,
                    columnCount);

                Dispatcher.Dispatch(
                    () => UpdateGeometryFlexHeight(
                        layout,
                        columnCount));
            });
    }


    private static void UpdateGeometryFlexHeight(
        FlexLayout layout,
        int columnCount)
    {
        int childCount =
            layout.Children.Count;

        if (childCount == 0 ||
            columnCount <= 0)
        {
            return;
        }

        int rowCount =
            (int)Math.Ceiling(
                childCount /
                (double)columnCount);

        double[] rowHeights =
            new double[rowCount];

        bool hasUnmeasuredRow =
            false;

        for (int childIndex = 0;
             childIndex < childCount;
             childIndex++)
        {
            if (layout.Children[childIndex]
                is not VisualElement card)
            {
                continue;
            }

            double measuredHeight =
                card.Height;

            if (!double.IsFinite(
                    measuredHeight) ||
                measuredHeight <= 0d)
            {
                hasUnmeasuredRow =
                    true;

                continue;
            }

            int rowIndex =
                childIndex /
                columnCount;

            rowHeights[rowIndex] =
                Math.Max(
                    rowHeights[rowIndex],
                    measuredHeight);
        }

        // Không đặt HeightRequest bằng một giá trị ước lượng khi card vẫn chưa
        // được đo, vì điều đó có thể cắt nội dung trong frame đầu.
        if (hasUnmeasuredRow ||
            rowHeights.Any(
                height => height <= 0d))
        {
            return;
        }

        // GeometryCardTemplate dùng Margin="5": mỗi hàng cần thêm 5 phía trên
        // và 5 phía dưới ngoài chiều cao thực của card.
        const double verticalMarginPerRow =
            10d;

        double requestedHeight =
            Math.Ceiling(
                rowHeights.Sum() +
                rowCount *
                verticalMarginPerRow);

        if (Math.Abs(
                layout.HeightRequest -
                requestedHeight) < 1d)
        {
            return;
        }

        layout.HeightRequest =
            requestedHeight;

        layout.InvalidateMeasure();

        if (layout.Parent
            is VisualElement parent)
        {
            parent.InvalidateMeasure();
        }
    }


    private static int GetGeometryColumnCount(
        double availableWidth)
    {
        // Điện thoại luôn dùng hai cột, kể cả khi xoay ngang.
        if (DeviceInfo.Idiom ==
            DeviceIdiom.Phone)
        {
            return 2;
        }

        // Cửa sổ quá hẹp cũng dùng hai cột để tránh card bị bóp nhỏ.
        if (availableWidth < 600d)
        {
            return 2;
        }

        // Desktop rộng: 5 card. Laptop và tablet lớn: 4 card.
        // Tablet 10 inch trở xuống: 3 card.
        if (DeviceInfo.Idiom ==
                DeviceIdiom.Desktop &&
            availableWidth >= 1500d)
        {
            return 5;
        }

        if (availableWidth >= 950d)
        {
            return 4;
        }

        return 3;
    }

    private static double UpdateFlexCardWidths(
        FlexLayout layout,
        double availableWidth,
        int columnCount,
        double horizontalMarginPerCard,
        double minimumCardWidth)
    {
        if (availableWidth <= 0 ||
            layout.Children.Count == 0 ||
            columnCount <= 0)
        {
            return 0d;
        }

        const double rightSafetySpace =
            6d;

        double totalMargins =
            columnCount *
            horizontalMarginPerCard;

        double requestedWidth =
            Math.Floor(
                (availableWidth -
                 totalMargins -
                 rightSafetySpace) /
                columnCount);

        requestedWidth =
            Math.Max(
                minimumCardWidth,
                requestedWidth);

        bool sizeChanged =
            false;

        foreach (IView child
                 in layout.Children)
        {
            if (child is not VisualElement element)
            {
                continue;
            }

            if (Math.Abs(
                    element.WidthRequest -
                    requestedWidth) < 1d)
            {
                continue;
            }

            element.MinimumWidthRequest =
                0d;

            element.WidthRequest =
                requestedWidth;

            sizeChanged =
                true;
        }

        if (sizeChanged)
        {
            layout.InvalidateMeasure();

            if (layout.Parent
                is VisualElement parent)
            {
                parent.InvalidateMeasure();
            }
        }

        return requestedWidth;
    }

    private static void UpdateGeometryCardVisualSizes(
        FlexLayout layout,
        double cardWidth)
    {
        // Ở điện thoại, mỗi hàng vẫn có hai card nên hình minh họa phải co theo
        // chiều rộng card. Trên tablet/laptop/desktop giữ tối đa 190 để tránh
        // card cao quá mức.
        double graphHeight =
            Math.Clamp(
                cardWidth *
                0.72d,
                112d,
                190d);

        foreach (IView cardView
                 in layout.Children)
        {
            if (cardView is not Border card ||
                card.Content is not Grid cardGrid)
            {
                continue;
            }

            card.Padding =
                cardWidth < 180d
                    ? new Thickness(
                        9d)
                    : new Thickness(
                        12d);

            foreach (IView childView
                     in cardGrid.Children)
            {
                if (childView is GraphicsView graphicsView)
                {
                    graphicsView.HeightRequest =
                        graphHeight;
                }
            }
        }
    }


    private void OnLanguageChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Dispatch(
            () =>
            {
                CreateUnknownComponentItems();

                // Đổi ngôn ngữ là trường hợp duy nhất cần tạo lại catalog
                // đã bản địa hóa. Hai ItemsSource vẫn được giữ nguyên.
                if (_isGeometryLayoutInitialized)
                {
                    CreateGeometryItems();

                    _lastPlaneGeometryLayoutWidth =
                        -1d;

                    _lastSolidGeometryLayoutWidth =
                        -1d;
                }

                if (_selectedSubTab ==
                    FormulaSubTab.UnknownComponent)
                {
                    _isUnknownComponentLayoutInitialized =
                        true;

                    RefreshUnknownComponentLayout(
                        force: true);
                }
                else if (_selectedSubTab ==
                         FormulaSubTab.Geometry)
                {
                    EnsureGeometryLayoutInitialized();
                    UpdateGeometryCardWidthsIfNeeded(
                        force: true);
                }

                LocalizationService.Attach(
                    this);
            });
    }

    private static string T(
        string text)
    {
        return LocalizationService.Translate(
            text);
    }

    private void CreateUnknownComponentItems()
    {
        _lastUnknownComponentLayoutWidth =
            -1d;

        UnknownComponentItems.Clear();

        UnknownComponentItems.Add(
            new UnknownComponentItem
            {
                Title =
                    T("Tìm số hạng chưa biết"),

                OperationSymbol =
                    T("+"),

                Structure =
                    T("x + b = c"),

                Rule =
                    T("Muốn tìm một số hạng, lấy tổng trừ đi số hạng đã biết."),

                Example =
                    T("x + 8 = 15"),

                ExampleSolution =
                    T("x = 15 − 8\nx = 7")
            });

        UnknownComponentItems.Add(
            new UnknownComponentItem
            {
                Title =
                    T("Tìm số bị trừ"),

                OperationSymbol =
                    T("−"),

                Structure =
                    T("x − b = c"),

                Rule =
                    T("Muốn tìm số bị trừ, lấy hiệu cộng với số trừ."),

                Example =
                    T("x − 6 = 10"),

                ExampleSolution =
                    T("x = 10 + 6\nx = 16")
            });

        UnknownComponentItems.Add(
            new UnknownComponentItem
            {
                Title =
                    T("Tìm số trừ"),

                OperationSymbol =
                    T("−"),

                Structure =
                    T("a − x = c"),

                Rule =
                    T("Muốn tìm số trừ, lấy số bị trừ trừ đi hiệu."),

                Example =
                    T("18 − x = 7"),

                ExampleSolution =
                    T("x = 18 − 7\nx = 11")
            });

        UnknownComponentItems.Add(
            new UnknownComponentItem
            {
                Title =
                    T("Tìm thừa số chưa biết"),

                OperationSymbol =
                    T("×"),

                Structure =
                    T("x × b = c"),

                Rule =
                    T("Muốn tìm một thừa số, lấy tích chia cho thừa số đã biết."),

                Example =
                    T("x × 4 = 28"),

                ExampleSolution =
                    T("x = 28 ÷ 4\nx = 7")
            });

        UnknownComponentItems.Add(
            new UnknownComponentItem
            {
                Title =
                    T("Tìm số bị chia"),

                OperationSymbol =
                    T("÷"),

                Structure =
                    T("x ÷ b = c"),

                Rule =
                    T("Muốn tìm số bị chia, lấy thương nhân với số chia."),

                Example =
                    T("x ÷ 5 = 9"),

                ExampleSolution =
                    T("x = 9 × 5\nx = 45")
            });

        UnknownComponentItems.Add(
            new UnknownComponentItem
            {
                Title =
                    T("Tìm số chia"),

                OperationSymbol =
                    T("÷"),

                Structure =
                    T("a ÷ x = c"),

                Rule =
                    T("Muốn tìm số chia, lấy số bị chia chia cho thương."),

                Example =
                    T("42 ÷ x = 6"),

                ExampleSolution =
                    T("x = 42 ÷ 6\nx = 7")
            });
    }

    private void CreateGeometryItems()
    {
        IReadOnlyList<GeometryFormulaItem> allItems =
            GeometryFormulaCatalog.CreateAll(
                T);

        PlaneGeometryItems.Clear();
        SolidGeometryItems.Clear();

        foreach (GeometryFormulaItem item
                 in allItems)
        {
            if (item.Category ==
                GeometryCategory.Plane)
            {
                PlaneGeometryItems.Add(
                    item);
            }
            else
            {
                SolidGeometryItems.Add(
                    item);
            }
        }

        _lastPlaneGeometryLayoutWidth =
            -1d;

        _lastSolidGeometryLayoutWidth =
            -1d;
    }


    private enum FormulaSubTab
    {
        UnknownComponent,
        Proportion,
        Motion,
        Geometry
    }
}
