using MathSolver.Controls;
using MathSolver.Services;
using System.Collections.ObjectModel;

namespace MathSolver.Views;

public partial class MultiplicationTablePage : ContentPage
{
    private int _mainTabAnimationVersion;

    // RadioButton.CheckedChanged có thể chạy ngay khi constructor gán
    // IsChecked = true. Khóa này bảo đảm danh sách chỉ được dựng một lần.
    private bool _isInitializing = true;

    public ObservableCollection<TableCardModel> TableCards { get; } = new();

    private TableMode _currentMode = TableMode.Multiply;
    private TableRange _currentRange = TableRange.OneToTen;

    public MultiplicationTablePage()
    {
        InitializeComponent();

        InteractiveButtonAnimation.SetIsScopeEnabled(
            this,
            true);

        // This page uses stable-key bindings for static text and rebuilds
        // dynamic card text itself. Keep the legacy visual-tree translator
        // away from CollectionView cells because cell recycling can otherwise
        // overwrite a bound title with text from another card.
        LocalizationService.ExcludeSubtreeFromLegacyTracking(
            this);

        // Initialize the JSON localization system before building the
        // dynamic table-card models.
        LocalizationService.Initialize();

        // Rebuild dynamic text only after the active language pack has
        // actually finished changing.
        LocalizationService.CultureChanged +=
            OnLanguageChanged;

        AppThemeManager.ThemeChanged +=
            OnThemeChanged;

        BindingContext = this;

        Range1To10Radio.IsChecked = true;

        // Dựng trạng thái và CollectionView đúng một lần. Sự kiện
        // CheckedChanged phát sinh trong lúc gán IsChecked sẽ bị bỏ qua.
        UpdateOperationButtons();
        UpdateRangeCards();
        BuildTables();

        _isInitializing = false;
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

        // Ổn định trạng thái hiển thị trước khi root được chuẩn bị cho
        // animation, tránh style/range thay đổi giữa lúc fade-in.
        UpdateOperationButtons();
        UpdateRangeCards();

        BeginMainTabTransitionIfPending();
    }

    protected override void OnDisappearing()
    {
        // Hủy transition đang chạy ở trang sắp bị ẩn. Khi quay lại trang,
        // một phiếu mới sẽ tạo một animation mới thay vì nối tiếp animation cũ.
        _mainTabAnimationVersion++;

        MultiplicationPageContentRoot.CancelAnimations();
        ResetMainTabRoot();

        base.OnDisappearing();
    }

    private void BeginMainTabTransitionIfPending()
    {
        if (Shell.Current is not AppShell appShell ||
            !appShell.TryConsumeMainTabTransition(
                "MultiplicationTablePage",
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

        // Chuẩn bị ngay trong OnAppearing, trước frame đầu tiên của trang.
        // Không để trang hiện hoàn chỉnh rồi mới reset Opacity về 0.
        MultiplicationPageContentRoot.CancelAnimations();

        MultiplicationPageContentRoot.Opacity =
            0d;

        MultiplicationPageContentRoot.TranslationX =
            direction *
            44d;

        MultiplicationPageContentRoot.Scale =
            0.985d;

        Dispatcher.Dispatch(
            async () =>
                await PlayPreparedMainTabTransitionAsync(
                    animationVersion));
    }

    private async Task PlayPreparedMainTabTransitionAsync(
        int animationVersion)
    {
        // CollectionView dựng cell theo cơ chế ảo hóa. Giữ toàn bộ root ở
        // trạng thái ẩn cho tới khi layout và nhóm cell đầu tiên ổn định,
        // nếu không phần header hiện trước rồi danh sách hiện sau sẽ trông
        // như animation chạy hai lần.
        bool layoutReady =
            await WaitForTableLayoutAsync(
                animationVersion);

        if (!layoutReady ||
            animationVersion !=
            _mainTabAnimationVersion)
        {
            return;
        }

        try
        {
            await Task.WhenAll(
                MultiplicationPageContentRoot.FadeToAsync(
                    1d,
                    175,
                    Easing.CubicOut),

                MultiplicationPageContentRoot.TranslateToAsync(
                    0d,
                    0d,
                    250,
                    Easing.CubicOut),

                MultiplicationPageContentRoot.ScaleToAsync(
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

    private async Task<bool> WaitForTableLayoutAsync(
        int animationVersion)
    {
        const int maximumLayoutFrames =
            8;

        for (int frame = 0;
             frame < maximumLayoutFrames;
             frame++)
        {
            await Task.Yield();

            if (animationVersion !=
                _mainTabAnimationVersion)
            {
                return false;
            }

            bool collectionReady =
                TableCards.Count > 0 &&
                TablesCollectionView.Handler is not null &&
                TablesCollectionView.Width > 0d &&
                TablesCollectionView.Height > 0d;

            if (collectionReady)
            {
                // Cho CollectionView thêm một frame để hiện thực hóa các
                // item đầu tiên. Root vẫn Opacity = 0 nên không gây nháy.
                await Task.Delay(
                    16);

                await Task.Yield();

                return animationVersion ==
                       _mainTabAnimationVersion;
            }

            await Task.Delay(
                16);
        }

        // Không giữ trang ẩn vô thời hạn nếu một nền tảng báo kích thước
        // chậm; sau giới hạn này vẫn chạy transition bình thường.
        return animationVersion ==
               _mainTabAnimationVersion;
    }

    private void ResetMainTabRoot()
    {
        MultiplicationPageContentRoot.Opacity =
            1d;

        MultiplicationPageContentRoot.TranslationX =
            0d;

        MultiplicationPageContentRoot.Scale =
            1d;
    }

    private void OnLanguageChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Dispatch(
            BuildTables);
    }

    private void OnThemeChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Dispatch(
            () =>
            {
                UpdateOperationButtons();
                UpdateRangeCards();
            });
    }

    private void OnMultiplyClicked(object? sender, EventArgs e)
    {
        _currentMode = TableMode.Multiply;
        UpdateOperationButtons();
        BuildTables();
    }

    private void OnDivideClicked(object? sender, EventArgs e)
    {
        _currentMode = TableMode.Divide;
        UpdateOperationButtons();
        BuildTables();
    }

    private void OnRangeChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (_isInitializing ||
            !e.Value)
        {
            return;
        }

        if (sender == Range1To10Radio)
            _currentRange = TableRange.OneToTen;
        else if (sender == Range11To20Radio)
            _currentRange = TableRange.ElevenToTwenty;
        else
            _currentRange = TableRange.All;

        UpdateRangeCards();
        BuildTables();
    }

    private void BuildTables()
    {
        TableCards.Clear();

        var (start, end) = GetRangeBounds();

        string titleKey =
            _currentMode == TableMode.Multiply
                ? "dynamic.times_table_multiplication_word"
                : "dynamic.times_table_division_word";

        for (int i = start; i <= end; i++)
        {
            var lines = new List<string>();

            for (int j = 1; j <= 10; j++)
            {
                if (_currentMode == TableMode.Multiply)
                {
                    lines.Add(
                        $"{i} × {j} = {i * j}");
                }
                else
                {
                    int dividend =
                        i * j;

                    lines.Add(
                        $"{dividend} ÷ {i} = {j}");
                }
            }

            string title =
                LocalizationService.FormatTemplate(
                    titleKey,
                    new Dictionary<string, object?>
                    {
                        ["number"] =
                            i
                    });

            TableCards.Add(
                new TableCardModel
                {
                    Title =
                        title,

                    Lines =
                        lines
                });
        }

        UpdateStatusText(start, end);
    }

    private (int start, int end) GetRangeBounds()
    {
        return _currentRange switch
        {
            TableRange.OneToTen => (1, 10),
            TableRange.ElevenToTwenty => (11, 20),
            _ => (1, 20)
        };
    }

    private void UpdateStatusText(
        int start,
        int end)
    {
        string statusKey =
            _currentMode == TableMode.Multiply
                ? "dynamic.times_table_showing_multiplication"
                : "dynamic.times_table_showing_division";

        int count =
            end - start + 1;

        StatusLabel.Text =
            LocalizationService.FormatTemplate(
                statusKey,
                new Dictionary<string, object?>
                {
                    ["first"] =
                        start,

                    ["last"] =
                        end,

                    ["count"] =
                        count
                });
    }

    private void UpdateOperationButtons()
    {
        ApplyOperationButtonStyle(
            MultiplyButton,
            _currentMode ==
            TableMode.Multiply);

        ApplyOperationButtonStyle(
            DivideButton,
            _currentMode ==
            TableMode.Divide);
    }

    private static void ApplyOperationButtonStyle(
        Button button,
        bool isSelected)
    {
        button.SetDynamicResource(
            Button.BackgroundColorProperty,
            isSelected
                ? "PrimaryColor"
                : "SurfaceAltColor");

        button.SetDynamicResource(
            Button.TextColorProperty,
            isSelected
                ? "OnPrimaryColor"
                : "TextPrimaryColor");

        button.SetDynamicResource(
            Button.BorderColorProperty,
            isSelected
                ? "PrimaryColor"
                : "BorderColor");

        button.BorderWidth =
            1;

        button.CornerRadius =
            12;
    }

    private void UpdateRangeCards()
    {
        ApplyRangeStyle(
            Range1To10Border,
            _currentRange ==
            TableRange.OneToTen);

        ApplyRangeStyle(
            Range11To20Border,
            _currentRange ==
            TableRange.ElevenToTwenty);

        ApplyRangeStyle(
            RangeAllBorder,
            _currentRange ==
            TableRange.All);
    }

    private static void ApplyRangeStyle(
        Border border,
        bool isSelected)
    {
        border.SetDynamicResource(
            Border.BackgroundColorProperty,
            isSelected
                ? "SurfaceAltColor"
                : "SurfaceColor");

        border.SetDynamicResource(
            Border.StrokeProperty,
            isSelected
                ? "PrimaryColor"
                : "BorderBrush");

        border.StrokeThickness =
            isSelected
                ? 1.6
                : 1;
    }

    private enum TableMode
    {
        Multiply,
        Divide
    }

    private enum TableRange
    {
        OneToTen,
        ElevenToTwenty,
        All
    }

    public class TableCardModel
    {
        public string Title { get; set; } = string.Empty;
        public List<string> Lines { get; set; } = new();
    }
}
