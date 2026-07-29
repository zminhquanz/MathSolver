using MathSolver.Services;
using MathSolver.Graphics;
using MathSolver.Models;
using System.Collections.ObjectModel;

namespace MathSolver.Views;

public partial class FormulaPage : ContentPage
{
    private int _mainTabAnimationVersion;

    private bool _isMainTabTransitioning;

    private FormulaSubTab _selectedSubTab = FormulaSubTab.UnknownComponent;

    private bool _isSubTabTransitioning;

    // GeometryFlexLayout và các GraphicsView chỉ được khởi tạo một lần.
    // Khi rời rồi quay lại tab Công thức, không gán lại ItemsSource nên
    // các GraphicsView cũ được giữ nguyên, tránh vẽ lại và nháy giao diện.
    private bool _isGeometryLayoutInitialized;

    private double _lastGeometryLayoutWidth = -1d;

    // Được bật khi trang Hình học bị che hoặc rời khỏi visual tree, ví dụ
    // khi mở SettingsMenuPage. Khi quay lại cần khôi phục layout và yêu cầu
    // từng GraphicsView vẽ lại lớp native của nó.
    private bool _geometryNeedsVisualRestore;

    public ObservableCollection<GeometryFormulaItem> GeometryItems { get; } = [];

    public ObservableCollection<UnknownComponentItem> UnknownComponentItems { get; } = [];

    public FormulaPage()
    {
        InitializeComponent();

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
            GeometryFlexLayout,
            GeometryItems);

        SelectFormulaSubTab(
            FormulaSubTab.UnknownComponent);

        Dispatcher.Dispatch(
            () => LocalizationService.Attach(
                this));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

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

        // SettingsMenuPage là modal trong suốt. Khi mở nó, FormulaPage vẫn là
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
            FormulaSubTabBar);

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
        bool showGeometry =
            _selectedSubTab ==
            FormulaSubTab.Geometry;

        UnknownComponentContent.IsVisible =
            !showGeometry;

        GeometryContent.IsVisible =
            showGeometry;

        ResetTransitionTransform(
            FormulaSubTabBar);

        ResetTransitionTransform(
            showGeometry
                ? GeometryContent
                : UnknownComponentContent);

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

        // Fallback hiếm: nếu nền tảng đã bỏ visual children khi mở Settings,
        // chỉ lúc đó mới gắn lại ItemsSource để tạo lại các card.
        if (requiresFullRestore &&
            GeometryItems.Count > 0 &&
            GeometryFlexLayout.Children.Count == 0)
        {
            BindableLayout.SetItemsSource(
                GeometryFlexLayout,
                null);

            BindableLayout.SetItemsSource(
                GeometryFlexLayout,
                GeometryItems);
        }

        _lastGeometryLayoutWidth =
            -1d;

        UpdateGeometryCardWidthsIfNeeded(
            force: true);

        GeometryFlexLayout.InvalidateMeasure();
        GeometryContent.InvalidateMeasure();

        InvalidateGeometryGraphicsViews();

        _geometryNeedsVisualRestore =
            false;
    }

    private void InvalidateGeometryGraphicsViews()
    {
        foreach (IView cardView
                 in GeometryFlexLayout.Children)
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

            RefreshUnknownComponentLayout();
            return;
        }

        EnsureGeometryLayoutInitialized();

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
                FormulaSubTabBar);

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
            FormulaSubTabBar,
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

        try
        {
            await Task.WhenAll(
                AnimatePreparedMainTabHostAsync(
                    FormulaSubTabBar),

                AnimatePreparedMainTabHostAsync(
                    activeContent));
        }
        finally
        {
            if (animationVersion ==
                _mainTabAnimationVersion)
            {
                ResetTransitionTransform(
                    FormulaSubTabBar);

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
        FormulaSubTabBar.CancelAnimations();
        UnknownComponentContent.CancelAnimations();
        GeometryContent.CancelAnimations();
    }

    private void RefreshUnknownComponentLayout()
    {
        BindableLayout.SetItemsSource(
            UnknownComponentFlexLayout,
            null);

        BindableLayout.SetItemsSource(
            UnknownComponentFlexLayout,
            UnknownComponentItems);

        Dispatcher.Dispatch(
            () =>
            {
                UpdateUnknownComponentCardWidths(
                    UnknownComponentFlexLayout.Width);

                Dispatcher.Dispatch(
                    () =>
                    {
                        UpdateUnknownComponentCardWidths(
                            UnknownComponentFlexLayout.Width);

                        LocalizationService.Attach(
                            this);
                    });
            });
    }

    private void EnsureGeometryLayoutInitialized()
    {
        if (_isGeometryLayoutInitialized)
        {
            return;
        }

        if (GeometryItems.Count == 0)
        {
            CreateGeometryItems();
        }

        // ItemsSource đã được gắn một lần trong constructor. Không gán null
        // rồi gắn lại vì thao tác đó hủy toàn bộ card và tạo GraphicsView mới.
        _isGeometryLayoutInitialized =
            true;

        Dispatcher.Dispatch(
            () =>
            {
                UpdateGeometryCardWidthsIfNeeded(
                    force: true);

                // Lần đầu BindableLayout có thể vừa mới tạo children. Chỉ
                // đo lại kích thước, không thay ItemsSource và không tái tạo
                // GraphicsView.
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
        double availableWidth =
            GeometryFlexLayout.Width;

        if (availableWidth <= 0)
        {
            return;
        }

        if (!force &&
            Math.Abs(
                availableWidth -
                _lastGeometryLayoutWidth) < 0.5d)
        {
            return;
        }

        _lastGeometryLayoutWidth =
            availableWidth;

        UpdateGeometryCardWidths(
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

    private async Task SwitchFormulaSubTabAsync(
        FormulaSubTab selectedTab)
    {
        if (_isSubTabTransitioning ||
            _isMainTabTransitioning)
        {
            return;
        }

        Button selectedButton =
            GetFormulaSubTabButton(
                selectedTab);

        if (_selectedSubTab ==
            selectedTab)
        {
            await AnimateFormulaSubTabButtonAsync(
                selectedButton);

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
                    Easing.CubicOut),

                AnimateFormulaSubTabButtonAsync(
                    selectedButton));

            RefreshSelectedFormulaSubTabLayout();
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

        GeometryContent.IsVisible =
            selectedTab ==
            FormulaSubTab.Geometry;

        ResetTransitionTransform(
            UnknownComponentContent);

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

            RefreshUnknownComponentLayout();
            return;
        }

        // Chỉ tạo card và GraphicsView ở lần mở tab Hình học đầu tiên.
        // Những lần chuyển tab sau chỉ đổi IsVisible và chạy animation.
        EnsureGeometryLayoutInitialized();
    }

    private void RefreshSelectedFormulaSubTabLayout()
    {
        if (_selectedSubTab ==
            FormulaSubTab.UnknownComponent)
        {
            RefreshUnknownComponentLayout();
        }
        else
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

            FormulaSubTab.Geometry =>
                GeometryContent,

            _ =>
                UnknownComponentContent
        };
    }

    private Button GetFormulaSubTabButton(
        FormulaSubTab tab)
    {
        return tab switch
        {
            FormulaSubTab.UnknownComponent =>
                UnknownComponentTabButton,

            FormulaSubTab.Geometry =>
                GeometryTabButton,

            _ =>
                UnknownComponentTabButton
        };
    }

    private static async Task AnimateFormulaSubTabButtonAsync(
        Button button)
    {
        button.CancelAnimations();

        await button.ScaleToAsync(
            0.94d,
            65,
            Easing.CubicOut);

        await button.ScaleToAsync(
            1d,
            105,
            Easing.CubicOut);
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
        ResetSubTabButton(UnknownComponentTabButton);
        ResetSubTabButton(GeometryTabButton);

        Button selectedButton =
            _selectedSubTab switch
            {
                FormulaSubTab.UnknownComponent => UnknownComponentTabButton,
                FormulaSubTab.Geometry => GeometryTabButton,
                _ => UnknownComponentTabButton
            };

        selectedButton.SetDynamicResource(
            Button.BackgroundColorProperty,
            "PrimaryColor");

        selectedButton.SetDynamicResource(
            Button.TextColorProperty,
            "OnPrimaryColor");
    }

    private static void ResetSubTabButton(Button button)
    {
        button.SetDynamicResource(
            Button.BackgroundColorProperty,
            "SurfaceAltColor");

        button.SetDynamicResource(
            Button.TextColorProperty,
            "TextPrimaryColor");
    }

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
                    UpdateUnknownComponentCardWidths(
                        UnknownComponentFlexLayout.Width);
                }
                else
                {
                    UpdateGeometryCardWidthsIfNeeded();
                }
            });
    }

    private void OnUnknownComponentFlexLayoutSizeChanged(
        object? sender,
        EventArgs e)
    {
        if (sender is FlexLayout layout)
        {
            UpdateUnknownComponentCardWidths(
                layout.Width);
        }
    }

    private void UpdateUnknownComponentCardWidths(
        double availableWidth)
    {
        UpdateFlexCardWidths(
            UnknownComponentFlexLayout,
            availableWidth);
    }

    private void OnGeometryFlexLayoutSizeChanged(
        object? sender,
        EventArgs e)
    {
        if (!_isGeometryLayoutInitialized)
        {
            return;
        }

        UpdateGeometryCardWidthsIfNeeded();
    }

    private void UpdateGeometryCardWidths(
        double availableWidth)
    {
        UpdateFlexCardWidths(
            GeometryFlexLayout,
            availableWidth);
    }

    private static void UpdateFlexCardWidths(
        FlexLayout layout,
        double availableWidth)
    {
        if (availableWidth <= 0 ||
            layout.Children.Count == 0)
        {
            return;
        }

        // Mỗi Border có Margin="6" ở cả hai bên.
        const double horizontalMarginPerCard =
            12;

        // WidthRequest của Border cần chừa thêm phần padding và stroke.
        const double borderPaddingAndStroke =
            30;

        // Chừa khoảng trống cho scrollbar, sai số làm tròn và mép phải.
        const double rightSafetySpace =
            20;

        int columnCount =
            availableWidth switch
            {
                // Ba cột khi vùng nội dung đủ rộng để công thức
                // và chú thích không bị ép xuống quá nhiều dòng.
                >= 1040 => 3,

                // Tablet hoặc cửa sổ Windows cỡ vừa.
                >= 680 => 2,

                // Điện thoại hoặc cửa sổ hẹp.
                _ => 1
            };

        double totalMargins =
            columnCount *
            horizontalMarginPerCard;

        double outerWidthPerCard =
            Math.Floor(
                (availableWidth -
                 totalMargins -
                 rightSafetySpace) /
                columnCount);

        double requestedWidth =
            outerWidthPerCard -
            borderPaddingAndStroke;

        requestedWidth =
            Math.Clamp(
                requestedWidth,
                190,
                380);

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
                    requestedWidth) < 1)
            {
                continue;
            }

            element.MinimumWidthRequest =
                0;

            element.WidthRequest =
                requestedWidth;

            sizeChanged =
                true;
        }

        if (!sizeChanged)
        {
            return;
        }

        layout.InvalidateMeasure();

        if (layout.Parent
            is VisualElement parent)
        {
            parent.InvalidateMeasure();
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

                // Đổi ngôn ngữ là trường hợp duy nhất cần tạo lại dữ liệu
                // hình học vì các chuỗi công thức đã thay đổi. ItemsSource
                // vẫn giữ nguyên nên không có thao tác tháo/gắn lại layout.
                if (_isGeometryLayoutInitialized)
                {
                    CreateGeometryItems();
                    _lastGeometryLayoutWidth =
                        -1d;
                }

                if (_selectedSubTab ==
                    FormulaSubTab.UnknownComponent)
                {
                    RefreshUnknownComponentLayout();
                }
                else
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
        GeometryItems.Clear();

        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    T("Hình vuông"),
                CardHeight = 500,

                Diagram =
                    GeometryDrawableCache.Square,

                Formulas =
                {
                    T("Chu vi: P = a × 4"),
                    T("Diện tích: S = a × a")
                },

                Symbols =
                {
                    T("a: độ dài một cạnh")
                }
            });

        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    T("Hình chữ nhật"),
                CardHeight = 500,

                Diagram =
                    GeometryDrawableCache.Rectangle,

                Formulas =
                {
                    T("Chu vi: P = (a + b) × 2"),
                    T("Diện tích: S = a × b")
                },

                Symbols =
                {
                    T("a: chiều dài"),
                    T("b: chiều rộng")
                }
            });

        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    T("Hình tam giác"),
                CardHeight = 520,

                Diagram =
                    GeometryDrawableCache.Triangle,

                Formulas =
                {
                    T("Chu vi: P = a + b + c"),
                    T("Diện tích: S = (a × h) ÷ 2")
                },

                Symbols =
                {
                    T("a: độ dài đáy"),
                    T("b, c: hai cạnh còn lại"),
                    T("h: chiều cao tương ứng với đáy a")
                }
            });

        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    T("Hình tròn"),
                CardHeight = 530,

                Diagram =
                    GeometryDrawableCache.Circle,

                Formulas =
                {
                    T("Chu vi: C = 2 × π × r"),
                    T("Hoặc: C = π × d"),
                    T("Diện tích: S = π × r × r")
                },

                Symbols =
                {
                    T("r: bán kính"),
                    T("d: đường kính, d = 2 × r"),
                    T("π ≈ 3,14")
                }
            });

        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    T("Hình thang"),
                CardHeight = 530,

                Diagram =
                    GeometryDrawableCache.Trapezoid,

                Formulas =
                {
                    T("Chu vi: P = a + b + c + d"),
                    T("Diện tích: S = ((a + b) × h) ÷ 2")
                },

                Symbols =
                {
                    T("a, b: hai đáy song song"),
                    T("c, d: hai cạnh bên"),
                    T("h: chiều cao")
                }
            });

        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    T("Hình thoi"),
                CardHeight = 550,

                Diagram =
                    GeometryDrawableCache.Rhombus,

                Formulas =
                {
                    T("Chu vi: P = a × 4"),
                    T("Diện tích: S = (d₁ × d₂) ÷ 2"),
                    T("Hoặc: S = a × h")
                },

                Symbols =
                {
                    T("a: độ dài một cạnh"),
                    T("d₁, d₂: hai đường chéo"),
                    T("h: chiều cao")
                }
            });

        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    T("Hình bình hành"),
                CardHeight = 530,

                Diagram =
                    GeometryDrawableCache.Parallelogram,

                Formulas =
                {
                    T("Chu vi: P = (a + b) × 2"),
                    T("Diện tích: S = a × h")
                },

                Symbols =
                {
                    T("a: độ dài đáy"),
                    T("b: độ dài cạnh bên"),
                    T("h: chiều cao tương ứng với đáy a")
                }
            });

        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    T("Hình lập phương"),
                CardHeight = 550,

                Diagram =
                    GeometryDrawableCache.Cube,

                Formulas =
                {
                    T("Diện tích xung quanh: Sxq = 4 x a x a = 4a²"),
                    T("Diện tích toàn phần: Stp = 6 x a x a = 6a²"),
                    T("Thể tích: V = a x a x a = a³")
                },

                Symbols =
                {
                    T("a: độ dài một cạnh"),
                    T("Có 6 mặt là các hình vuông bằng nhau"),
                    T("Sxq gồm 4 mặt bên; Stp gồm cả 6 mặt")
                }
            });
        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    T("Hình hộp chữ nhật"),
                CardHeight = 570,

                Diagram =
                    GeometryDrawableCache.RectangularPrism,

                Formulas =
                {
                    T("Diện tích xung quanh: Sxq = 2 × (a + b) × h"),
                    T("Diện tích toàn phần: Stp = 2 × (a × b + a × h + b × h)"),
                    T("Thể tích: V = a × b × h")
                },

                Symbols =
                {
                    T("a: chiều dài"),
                    T("b: chiều rộng"),
                    T("h: chiều cao")
                }
            });
        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    T("Hình cầu"),
                CardHeight = 520,

                Diagram =
                    GeometryDrawableCache.Sphere,

                Formulas =
                {
                    T("Diện tích mặt cầu: S = 4 × π × r²"),
                    T("Thể tích: V = (4 × π × r³) ÷ 3")
                },

                Symbols =
                {
                    T("r: bán kính hình cầu"),
                    T("π ≈ 3,14")
                }
            });
        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    T("Hình trụ"),
                CardHeight = 590,

                Diagram =
                    GeometryDrawableCache.Cylinder,

                Formulas =
                {
                    T("Diện tích đáy: Sđ = π × r²"),
                    T("Diện tích xung quanh: Sxq = 2 × π × r × h"),
                    T("Diện tích toàn phần: Stp = 2 × π × r × (r + h)"),
                    T("Thể tích: V = π × r² × h")
                },

                Symbols =
                {
                    T("r: bán kính đáy"),
                    T("h: chiều cao"),
                    T("π ≈ 3,14")
                }
            });
        GeometryItems.Add(
            new GeometryFormulaItem
            {
                Name =
                    T("Hình nón"),
                CardHeight = 610,

                Diagram =
                    GeometryDrawableCache.Cone,

                Formulas =
                {
                    T("Diện tích đáy: Sđ = π × r²"),
                    T("Diện tích xung quanh: Sxq = π × r × l"),
                    T("Diện tích toàn phần: Stp = π × r × (r + l)"),
                    T("Thể tích: V = (π × r² × h) ÷ 3")
                },

                Symbols =
                {
                    T("r: bán kính đáy"),
                    T("h: chiều cao"),
                    T("l: đường sinh"),
                    T("π ≈ 3,14")
                }
            });
    }

    private enum FormulaSubTab
    {
        UnknownComponent,
        Geometry
    }
}

internal static class GeometryDrawableCache
{
    public static GeometryShapeDrawable Square { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Square
        };

    public static GeometryShapeDrawable Rectangle { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Rectangle
        };

    public static GeometryShapeDrawable Triangle { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Triangle
        };

    public static GeometryShapeDrawable Circle { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Circle
        };

    public static GeometryShapeDrawable Trapezoid { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Trapezoid
        };

    public static GeometryShapeDrawable Rhombus { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Rhombus
        };

    public static GeometryShapeDrawable Parallelogram { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Parallelogram
        };

    public static GeometryShapeDrawable Cube { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Cube
        };
    public static GeometryShapeDrawable RectangularPrism { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.RectangularPrism
        };

    public static GeometryShapeDrawable Sphere { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Sphere
        };

    public static GeometryShapeDrawable Cylinder { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Cylinder
        };

    public static GeometryShapeDrawable Cone { get; } =
        new()
        {
            ShapeType =
                GeometryShapeType.Cone
        };
}