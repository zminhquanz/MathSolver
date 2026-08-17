using MathSolver.Graphics;
using MathSolver.Controls;
using MathSolver.Numerics;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Views.Base;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace MathSolver.Views;

public partial class QuadraticEquationView : LocalizedSolverView
{
    private enum EquationMode
    {
        Linear,
        Quadratic
    }

    private readonly LinearEquationEngine _linearEngine = new();
    private readonly QuadraticEquationEngine _quadraticEngine = new();
    private EquationMode _equationMode = EquationMode.Quadratic;
    private const string Int128RangeText =
        "−170,141,183,460,469,231,731,687,303,715,884,105,728 đến " +
        "170,141,183,460,469,231,731,687,303,715,884,105,727";

    private const int MaxResultDecimalPlaces =
        10;

    private const int OctoDoubleDisplaySignificantDigits =
        OctoDouble.SignificantDigits;

    private const int QuadDoubleDisplaySignificantDigits =
        QuadDouble.SignificantDigits;

    // Từ 19 chữ số trở lên, giao diện rút gọn sang dạng khoa học.
    // Dictionary vẫn giữ chuỗi Int128 đầy đủ để khi focus hoặc tính toán,
    // ứng dụng không phải đọc ngược giá trị đã làm tròn trên giao diện.
    private const int ScientificDisplayDigitThreshold =
        18;

    private const int ScientificDisplaySignificantDigits =
        12;

    private readonly Dictionary<Entry, string>
        _coefficientExactIntegerValues =
            new();

    private bool _isUpdatingText;
    private bool? _isCompactLayout;

    // Khi khôi phục OldTextValue, MAUI có thể phát sinh thêm một
    // TextChanged sau khi SetEntryText đã hoàn tất. Nếu không ghi nhớ
    // sự kiện này, nhánh input hợp lệ sẽ gọi HideResultAndError và
    // làm thông báo lỗi vừa hiện biến mất ngay lập tức.
    private readonly Dictionary<Entry, string>
        _pendingRestoredEntryTexts =
            new();

    private readonly ParabolaGraphDrawable
        _parabolaGraphDrawable =
            new();

    private readonly LinearEquationGraphDrawable
        _linearGraphDrawable =
            new();

    private Microsoft.Maui.Graphics.PointF?
        _lastGraphPointer;

    private bool _isGraphThemeSubscribed;

#if WINDOWS
    private Microsoft.UI.Xaml.FrameworkElement?
        _windowsGraphElement;

    private Microsoft.UI.Xaml.Controls.ScrollViewer?
        _windowsQuadraticScrollViewer;
#endif

    public QuadraticEquationView()
    {
        InitializeComponent();

        ParabolaGraphicsView.Drawable =
            _parabolaGraphDrawable;

        ParabolaGraphicsView.HandlerChanged +=
            OnParabolaGraphicsViewHandlerChanged;

        QuadraticScrollView.HandlerChanged +=
            OnQuadraticScrollViewHandlerChanged;

#if WINDOWS
        CoefficientAEntry.HandlerChanged +=
            OnCoefficientEntryHandlerChanged;

        CoefficientBEntry.HandlerChanged +=
            OnCoefficientEntryHandlerChanged;

        CoefficientCEntry.HandlerChanged +=
            OnCoefficientEntryHandlerChanged;
#endif

        ApplyCurrentGraphTheme();

        InitializeLocalization();

        ApplyEquationMode(
            EquationMode.Quadratic,
            clearResults: false);

        UpdateGraphStatus();

        ConfigureExpandedLayout();

        _isCompactLayout =
            false;

        UpdateEquationPreview();
    }

    protected override void RefreshLocalizedContent()
    {
        bool hadVisibleResult =
            ResultBorder?.IsVisible == true;

        base.RefreshLocalizedContent();
        ApplyModeLocalizedText();

        if (hadVisibleResult)
        {
            OnCalculateClicked(
                this,
                EventArgs.Empty);
        }
    }

    private static string T(string key) =>
        LocalizationService.TranslateKey(key);

    private void OnLinearModeClicked(
        object? sender,
        EventArgs e)
    {
        ApplyEquationMode(
            EquationMode.Linear,
            clearResults: true);
    }

    private void OnQuadraticModeClicked(
        object? sender,
        EventArgs e)
    {
        ApplyEquationMode(
            EquationMode.Quadratic,
            clearResults: true);
    }

    private void ApplyEquationMode(
        EquationMode mode,
        bool clearResults)
    {
        _equationMode = mode;

        SelectionButtonStyler.Select(
            mode == EquationMode.Linear
                ? LinearModeButton
                : QuadraticModeButton,
            LinearModeButton,
            QuadraticModeButton);

        bool isLinear =
            mode == EquationMode.Linear;

        CoefficientCPanel.IsVisible =
            !isLinear;

        ParabolaGraphicsView.Drawable =
            isLinear
                ? _linearGraphDrawable
                : _parabolaGraphDrawable;

        ApplyModeLocalizedText();

        if (_isCompactLayout == true)
        {
            ConfigureCompactLayout();
        }
        else
        {
            ConfigureExpandedLayout();
        }

        if (clearResults)
        {
            HideResultAndError();
            GraphBorder.IsVisible = false;
        }

        UpdateEquationPreview();
        UpdateGraphStatus();
        ParabolaGraphicsView.Invalidate();
    }

    private void ApplyModeLocalizedText()
    {
        bool isLinear =
            _equationMode == EquationMode.Linear;

        HeroSymbolLabel.Text =
            isLinear
                ? "ax + b = 0"
                : "ax² + bx + c = 0";

        HeroTitleLabel.Text = T(
            isLinear
                ? "Equation.Linear.Title"
                : "Equation.Quadratic.Title");

        HeroSubtitleLabel.Text = T(
            isLinear
                ? "Equation.Linear.Subtitle"
                : "Equation.Quadratic.Subtitle");

        CoefficientADescriptionLabel.Text = T(
            isLinear
                ? "Equation.Linear.CoefficientA.Description"
                : "Equation.Quadratic.CoefficientA.Description");

        CoefficientBDescriptionLabel.Text = T(
            isLinear
                ? "Equation.Linear.CoefficientB.Description"
                : "Equation.Quadratic.CoefficientB.Description");

        ResultMetricCaptionLabel.Text = T(
            isLinear
                ? "Equation.Linear.ResultMetric"
                : "Equation.Quadratic.ResultMetric");

        GraphTitleLabel.Text = T(
            isLinear
                ? "Equation.Linear.Graph.Title"
                : "Equation.Quadratic.Graph.Title");

        GraphHelpLabel.Text = T(
            isLinear
                ? "Equation.Linear.Graph.Help"
                : "Equation.Quadratic.Graph.Help");
    }

    protected override void OnSolverLoaded()
    {
        SubscribeGraphThemeChanges();
        ApplyCurrentGraphTheme();

#if WINDOWS
        AttachWindowsQuadraticScrollViewer();
        ConfigureWindowsCoefficientEntries();
        AttachWindowsGraphMouseWheel();
#endif
    }

    protected override void OnSolverUnloaded()
    {
        UnsubscribeGraphThemeChanges();

#if WINDOWS
        DetachWindowsQuadraticScrollViewer();
        DetachWindowsGraphMouseWheel();
#endif
    }

    private void OnQuadraticScrollViewHandlerChanged(
        object? sender,
        EventArgs e)
    {
#if WINDOWS
        AttachWindowsQuadraticScrollViewer();
#endif
    }

    private void ClearTransientFocus()
    {
        if (CoefficientAEntry.IsFocused)
        {
            CoefficientAEntry.Unfocus();
        }

        if (CoefficientBEntry.IsFocused)
        {
            CoefficientBEntry.Unfocus();
        }

        if (CoefficientCEntry.IsFocused)
        {
            CoefficientCEntry.Unfocus();
        }

        if (CalculateQuadraticButton.IsFocused)
        {
            CalculateQuadraticButton.Unfocus();
        }

        if (GraphResetZoomButton.IsFocused)
        {
            GraphResetZoomButton.Unfocus();
        }
    }

#if WINDOWS
    private void OnCoefficientEntryHandlerChanged(
        object? sender,
        EventArgs e)
    {
        if (sender is Entry entry)
        {
            ConfigureWindowsCoefficientEntry(
                entry);
        }
    }

    private void ConfigureWindowsCoefficientEntries()
    {
        ConfigureWindowsCoefficientEntry(
            CoefficientAEntry);

        ConfigureWindowsCoefficientEntry(
            CoefficientBEntry);

        ConfigureWindowsCoefficientEntry(
            CoefficientCEntry);

        ConfigureWindowsFocusableElement(
            CalculateQuadraticButton);

        ConfigureWindowsFocusableElement(
            GraphResetZoomButton);
    }

    private void ConfigureWindowsFocusableElement(
        VisualElement element)
    {
        if (element.Handler?.PlatformView is not
            Microsoft.UI.Xaml.FrameworkElement frameworkElement)
        {
            return;
        }

        Microsoft.UI.Xaml.Controls.ScrollViewer
            .SetBringIntoViewOnFocusChange(
                frameworkElement,
                false);

        frameworkElement.BringIntoViewRequested -=
            OnWindowsBringIntoViewRequested;

        frameworkElement.BringIntoViewRequested +=
            OnWindowsBringIntoViewRequested;
    }

    private void ConfigureWindowsCoefficientEntry(
        Entry entry)
    {
        if (entry.Handler?.PlatformView is not
            Microsoft.UI.Xaml.Controls.TextBox textBox)
        {
            return;
        }

        // Entry vẫn nhận focus bằng chuột và phím Tab, nhưng chính
        // TextBox không được yêu cầu ScrollViewer đưa nó vào vùng nhìn
        // khi Windows khôi phục focus sau Win key/minimize/restore.
        textBox.AllowFocusOnInteraction =
            true;

        textBox.IsTabStop =
            true;

        Microsoft.UI.Xaml.Controls.ScrollViewer
            .SetBringIntoViewOnFocusChange(
                textBox,
                false);

        textBox.BringIntoViewRequested -=
            OnWindowsBringIntoViewRequested;

        textBox.BringIntoViewRequested +=
            OnWindowsBringIntoViewRequested;
    }

    private void OnWindowsBringIntoViewRequested(
        Microsoft.UI.Xaml.UIElement sender,
        Microsoft.UI.Xaml.BringIntoViewRequestedEventArgs e)
    {
        // Chỉ chặn khi người dùng đang ở phần dưới của trang.
        // Ở đầu trang, keyboard navigation và focus hoạt động bình thường.
        if (_windowsQuadraticScrollViewer is not null &&
            _windowsQuadraticScrollViewer.VerticalOffset > 0.5d)
        {
            e.Handled =
                true;
        }
    }

    private void AttachWindowsQuadraticScrollViewer()
    {
        Microsoft.UI.Xaml.Controls.ScrollViewer?
            currentScrollViewer =
                QuadraticScrollView.Handler?.PlatformView
                as Microsoft.UI.Xaml.Controls.ScrollViewer;

        if (ReferenceEquals(
                currentScrollViewer,
                _windowsQuadraticScrollViewer))
        {
            ConfigureWindowsQuadraticScrollViewer();

            return;
        }

        DetachWindowsQuadraticScrollViewer();

        _windowsQuadraticScrollViewer =
            currentScrollViewer;

        ConfigureWindowsQuadraticScrollViewer();
    }

    private void ConfigureWindowsQuadraticScrollViewer()
    {
        if (_windowsQuadraticScrollViewer is null)
        {
            return;
        }

        // Không cho WinUI tự BringIntoView khi Windows trả focus
        // cho Entry, Button hoặc GraphicsView sau Alt+Tab, Win key,
        // minimize/restore hay resize cửa sổ.
        _windowsQuadraticScrollViewer
            .BringIntoViewOnFocusChange =
            false;

        // ScrollViewer không nằm trong chuỗi Tab, nhưng Entry con vẫn
        // được phép nhận focus trực tiếp bằng chuột.
        _windowsQuadraticScrollViewer
            .IsTabStop =
            false;

        _windowsQuadraticScrollViewer.BringIntoViewRequested -=
            OnWindowsBringIntoViewRequested;

        _windowsQuadraticScrollViewer.BringIntoViewRequested +=
            OnWindowsBringIntoViewRequested;
    }

    private void DetachWindowsQuadraticScrollViewer()
    {
        if (_windowsQuadraticScrollViewer is null)
        {
            return;
        }

        _windowsQuadraticScrollViewer.BringIntoViewRequested -=
            OnWindowsBringIntoViewRequested;

        _windowsQuadraticScrollViewer =
            null;
    }
#endif

    private void SubscribeGraphThemeChanges()
    {
        if (_isGraphThemeSubscribed ||
            Application.Current is not Application application)
        {
            return;
        }

        application.RequestedThemeChanged +=
            OnRequestedThemeChanged;

        _isGraphThemeSubscribed =
            true;
    }

    private void UnsubscribeGraphThemeChanges()
    {
        if (!_isGraphThemeSubscribed ||
            Application.Current is not Application application)
        {
            return;
        }

        application.RequestedThemeChanged -=
            OnRequestedThemeChanged;

        _isGraphThemeSubscribed =
            false;
    }

    private void OnRequestedThemeChanged(
        object? sender,
        AppThemeChangedEventArgs e)
    {
        Dispatcher.Dispatch(
            () =>
            {
                bool isDarkTheme =
                    e.RequestedTheme ==
                    AppTheme.Dark;

                _parabolaGraphDrawable.SetDarkTheme(
                    isDarkTheme);

                _linearGraphDrawable.SetDarkTheme(
                    isDarkTheme);

                ParabolaGraphicsView.Invalidate();
            });
    }

    private void ApplyCurrentGraphTheme()
    {
        AppTheme requestedTheme =
            Application.Current?.RequestedTheme ??
            AppTheme.Light;

        bool isDarkTheme =
            requestedTheme ==
            AppTheme.Dark;

        _parabolaGraphDrawable.SetDarkTheme(
            isDarkTheme);

        _linearGraphDrawable.SetDarkTheme(
            isDarkTheme);
    }

    private bool CurrentGraphHasEquation =>
        _equationMode == EquationMode.Linear
            ? _linearGraphDrawable.HasEquation
            : _parabolaGraphDrawable.HasEquation;

    private int CurrentGraphZoomPercent =>
        _equationMode == EquationMode.Linear
            ? _linearGraphDrawable.ZoomPercent
            : _parabolaGraphDrawable.ZoomPercent;

    private bool ZoomCurrentGraphAtPixel(
        float pixelX,
        float pixelY,
        bool zoomIn) =>
        _equationMode == EquationMode.Linear
            ? _linearGraphDrawable.ZoomAtPixel(
                pixelX,
                pixelY,
                zoomIn)
            : _parabolaGraphDrawable.ZoomAtPixel(
                pixelX,
                pixelY,
                zoomIn);

    private bool PanCurrentGraphByPixels(
        float deltaX,
        float deltaY) =>
        _equationMode == EquationMode.Linear
            ? _linearGraphDrawable.PanByPixels(
                deltaX,
                deltaY)
            : _parabolaGraphDrawable.PanByPixels(
                deltaX,
                deltaY);

    private void ZoomCurrentGraphIn()
    {
        if (_equationMode == EquationMode.Linear)
        {
            _linearGraphDrawable.ZoomIn();
        }
        else
        {
            _parabolaGraphDrawable.ZoomIn();
        }
    }

    private void ZoomCurrentGraphOut()
    {
        if (_equationMode == EquationMode.Linear)
        {
            _linearGraphDrawable.ZoomOut();
        }
        else
        {
            _parabolaGraphDrawable.ZoomOut();
        }
    }

    private void ResetCurrentGraphZoom()
    {
        if (_equationMode == EquationMode.Linear)
        {
            _linearGraphDrawable.ResetZoom();
        }
        else
        {
            _parabolaGraphDrawable.ResetZoom();
        }
    }

    private void OnParabolaGraphicsViewHandlerChanged(
        object? sender,
        EventArgs e)
    {
#if WINDOWS
        AttachWindowsGraphMouseWheel();
#endif
    }

#if WINDOWS
    private void AttachWindowsGraphMouseWheel()
    {
        Microsoft.UI.Xaml.FrameworkElement?
            currentElement =
                ParabolaGraphicsView.Handler?.PlatformView
                as Microsoft.UI.Xaml.FrameworkElement;

        if (ReferenceEquals(
                currentElement,
                _windowsGraphElement))
        {
            return;
        }

        DetachWindowsGraphMouseWheel();

        _windowsGraphElement =
            currentElement;

        if (_windowsGraphElement is null)
        {
            return;
        }

        // GraphicsView chỉ nhận pointer để pan/zoom, không nhận keyboard
        // focus. Nhờ đó WinUI không phát sinh StartBringIntoView ở lần
        // rê chuột đầu tiên sau khi cửa sổ được kích hoạt lại.
        _windowsGraphElement.AllowFocusOnInteraction =
            false;

        _windowsGraphElement.IsTabStop =
            false;

        Microsoft.UI.Xaml.Controls.ScrollViewer
            .SetBringIntoViewOnFocusChange(
                _windowsGraphElement,
                false);

        _windowsGraphElement.BringIntoViewRequested -=
            OnWindowsBringIntoViewRequested;

        _windowsGraphElement.BringIntoViewRequested +=
            OnWindowsBringIntoViewRequested;

        _windowsGraphElement.PointerWheelChanged +=
            OnWindowsGraphPointerWheelChanged;
    }

    private void DetachWindowsGraphMouseWheel()
    {
        if (_windowsGraphElement is null)
        {
            return;
        }

        _windowsGraphElement.BringIntoViewRequested -=
            OnWindowsBringIntoViewRequested;

        _windowsGraphElement.PointerWheelChanged -=
            OnWindowsGraphPointerWheelChanged;

        _windowsGraphElement =
            null;
    }

    private void OnWindowsGraphPointerWheelChanged(
        object? sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!CurrentGraphHasEquation ||
            _windowsGraphElement is null)
        {
            return;
        }

        var pointerPoint =
            e.GetCurrentPoint(
                _windowsGraphElement);

        if (pointerPoint.Properties.IsHorizontalMouseWheel)
        {
            return;
        }

        int wheelDelta =
            pointerPoint.Properties.MouseWheelDelta;

        if (wheelDelta == 0)
        {
            return;
        }

        bool zoomChanged =
            ZoomCurrentGraphAtPixel(
                (float)pointerPoint.Position.X,
                (float)pointerPoint.Position.Y,
                zoomIn:
                    wheelDelta >
                    0);

        if (!zoomChanged)
        {
            return;
        }

        UpdateGraphStatus();

        ParabolaGraphicsView.Invalidate();

        // Không để ScrollView cha cuộn trang khi con trỏ đang nằm
        // trên đồ thị và người dùng chủ động zoom bằng con lăn.
        e.Handled =
            true;
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

        bool useCompactLayout =
            width < 700;

        if (_isCompactLayout ==
            useCompactLayout)
        {
            return;
        }

        _isCompactLayout =
            useCompactLayout;

        if (useCompactLayout)
        {
            ConfigureCompactLayout();
        }
        else
        {
            ConfigureExpandedLayout();
        }
    }

    private void ConfigureCompactLayout()
    {
        CoefficientGrid.ColumnDefinitions.Clear();
        CoefficientGrid.RowDefinitions.Clear();

        CoefficientGrid.ColumnDefinitions.Add(
            new ColumnDefinition(
                GridLength.Star));

        int coefficientCount =
            _equationMode == EquationMode.Linear
                ? 2
                : 3;

        for (int index = 0;
             index < coefficientCount;
             index++)
        {
            CoefficientGrid.RowDefinitions.Add(
                new RowDefinition(
                    GridLength.Auto));
        }

        SetCoefficientPanelPosition(
            CoefficientAPanel,
            row: 0,
            column: 0);

        SetCoefficientPanelPosition(
            CoefficientBPanel,
            row: 1,
            column: 0);

        if (_equationMode == EquationMode.Quadratic)
        {
            SetCoefficientPanelPosition(
                CoefficientCPanel,
                row: 2,
                column: 0);
        }

        CoefficientGrid.ColumnSpacing =
            0;

        CoefficientGrid.RowSpacing =
            10;
    }

    private void ConfigureExpandedLayout()
    {
        CoefficientGrid.ColumnDefinitions.Clear();
        CoefficientGrid.RowDefinitions.Clear();

        int coefficientCount =
            _equationMode == EquationMode.Linear
                ? 2
                : 3;

        for (int index = 0;
             index < coefficientCount;
             index++)
        {
            CoefficientGrid.ColumnDefinitions.Add(
                new ColumnDefinition(
                    GridLength.Star));
        }

        CoefficientGrid.RowDefinitions.Add(
            new RowDefinition(
                GridLength.Auto));

        SetCoefficientPanelPosition(
            CoefficientAPanel,
            row: 0,
            column: 0);

        SetCoefficientPanelPosition(
            CoefficientBPanel,
            row: 0,
            column: 1);

        if (_equationMode == EquationMode.Quadratic)
        {
            SetCoefficientPanelPosition(
                CoefficientCPanel,
                row: 0,
                column: 2);
        }

        CoefficientGrid.ColumnSpacing =
            12;

        CoefficientGrid.RowSpacing =
            0;
    }

    private static void SetCoefficientPanelPosition(
        BindableObject panel,
        int row,
        int column)
    {
        Grid.SetRow(
            panel,
            row);

        Grid.SetColumn(
            panel,
            column);
    }

    private void OnCoefficientEntryTextChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        if (sender is not Entry entry)
        {
            return;
        }

        string newText =
            e.NewTextValue ??
            string.Empty;

        // SetEntryText khôi phục OldTextValue sẽ tạo thêm TextChanged.
        // Bỏ qua đúng sự kiện khôi phục này để thông báo lỗi không bị
        // HideResultAndError xóa ngay sau khi vừa hiển thị.
        if (_pendingRestoredEntryTexts.TryGetValue(
                entry,
                out string? restoredText))
        {
            _pendingRestoredEntryTexts.Remove(
                entry);

            if (string.Equals(
                    newText,
                    restoredText,
                    StringComparison.Ordinal))
            {
                return;
            }
        }

        if (_isUpdatingText)
        {
            return;
        }

        // Khi người dùng bắt đầu sửa, giá trị đang nhập trực tiếp trở thành
        // nguồn dữ liệu mới; bỏ bản mã khoa học cũ của Entry.
        _coefficientExactIntegerValues.Remove(
            entry);

        string fieldName =
            GetCoefficientFieldName(
                entry);

        // Kiểm tra chuỗi gốc trước khi Normalize:
        // chỉ chấp nhận chữ số 0-9 và một dấu âm ở đầu.
        if (!IsValidIntegerWhileTyping(
                newText))
        {
            RejectCoefficientInput(
                entry,
                e.OldTextValue,
                string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizationService.TranslateKey(
                        "Quadratic.IntegerOnly"),
                    fieldName));

            return;
        }

        string normalized =
            NormalizeIntegerText(
                newText);

        if (IsCompleteIntegerText(
                normalized) &&
            !Int128.TryParse(
                normalized,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out _))
        {
            RejectCoefficientInput(
                entry,
                e.OldTextValue,
                string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizationService.TranslateKey(
                        "Quadratic.Int128Range"),
                    fieldName,
                    Int128RangeText));

            return;
        }

        string formattedText =
            IntegerInputFormatter.FormatWhileTyping(
                newText);

        if (!string.Equals(
                formattedText,
                newText,
                StringComparison.Ordinal))
        {
            int logicalPosition =
                IntegerInputFormatter.CountLogicalCharacters(
                    newText,
                    entry.CursorPosition);

            SetEntryText(
                entry,
                formattedText,
                IntegerInputFormatter.FindCursorPosition(
                    formattedText,
                    logicalPosition));
        }

        // Chỉ xóa lỗi khi đây là nội dung hợp lệ do người dùng
        // thực sự nhập, không phải TextChanged do khôi phục giá trị cũ.
        HideResultAndError();
        UpdateEquationPreview();
    }

    private void RejectCoefficientInput(
        Entry entry,
        string? previousText,
        string message)
    {
        string restoredText =
            previousText ??
            string.Empty;

        // Hiển thị lỗi trước để người dùng thấy ngay.
        ShowError(
            message);

        // Ghi nhớ nội dung sắp khôi phục. TextChanged tiếp theo có đúng
        // nội dung này sẽ được bỏ qua và không thể ẩn ErrorBorder.
        _pendingRestoredEntryTexts[entry] =
            restoredText;

        SetEntryText(
            entry,
            restoredText);
    }

    private string GetCoefficientFieldName(
        Entry entry)
    {
        if (ReferenceEquals(
                entry,
                CoefficientAEntry))
        {
            return LocalizationService.TranslateKey(
                "Quadratic.CoefficientA");
        }

        if (ReferenceEquals(
                entry,
                CoefficientBEntry))
        {
            return LocalizationService.TranslateKey(
                "Quadratic.CoefficientB");
        }

        return LocalizationService.TranslateKey(
            "Quadratic.CoefficientC");
    }

    private void OnCoefficientEntryFocused(
        object? sender,
        FocusEventArgs e)
    {
        if (sender is not Entry entry)
        {
            return;
        }

        if (_coefficientExactIntegerValues.TryGetValue(
                entry,
                out string? exactIntegerText) &&
            Int128.TryParse(
                exactIntegerText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out Int128 exactValue))
        {
            _coefficientExactIntegerValues.Remove(
                entry);

            // Khi focus, trả về chuỗi Int128 đầy đủ và vẫn giữ phân nhóm
            // hàng nghìn để người dùng sửa trực tiếp mà không đổi cách nhìn.
            SetEntryText(
                entry,
                FormatInputInteger(
                    exactValue));

            return;
        }

        string normalized =
            NormalizeIntegerText(
                entry.Text);

        if (normalized.Length == 0)
        {
            return;
        }

        if (Int128.TryParse(
                normalized,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out Int128 value))
        {
            SetEntryText(
                entry,
                FormatInputInteger(
                    value));
        }
    }

    private void OnCoefficientEntryUnfocused(
        object? sender,
        FocusEventArgs e)
    {
        if (sender is not Entry entry)
        {
            return;
        }

        if (!TryParseCoefficientText(
                GetCoefficientInputText(
                    entry),
                out Int128 value))
        {
            return;
        }

        ApplyCoefficientEntryDisplayValue(
            entry,
            value);

        UpdateEquationPreview();
    }

    private void OnCalculateClicked(
        object? sender,
        EventArgs e)
    {
        HideResultAndError();

        if (_equationMode == EquationMode.Linear)
        {
            CalculateLinearEquation();
        }
        else
        {
            CalculateQuadraticEquation();
        }
    }

    private void CalculateLinearEquation()
    {
        if (!TryReadCoefficient(
                CoefficientAEntry,
                GetCoefficientFieldName(
                    CoefficientAEntry),
                out Int128 a))
        {
            CoefficientAEntry.Focus();
            return;
        }

        if (!TryReadCoefficient(
                CoefficientBEntry,
                GetCoefficientFieldName(
                    CoefficientBEntry),
                out Int128 b))
        {
            CoefficientBEntry.Focus();
            return;
        }

        if (a == Int128.Zero)
        {
            ShowError(
                T("Equation.Linear.ANonZero"));

            CoefficientAEntry.Focus();
            return;
        }

        ApplyCoefficientEntryDisplayValue(
            CoefficientAEntry,
            a);

        ApplyCoefficientEntryDisplayValue(
            CoefficientBEntry,
            b);

        LinearEquationResult calculation =
            _linearEngine.Solve(a, b);

        if (!calculation.IsFinite)
        {
            ShowError(
                T("Equation.Linear.RootNotFiniteQuadDouble"));
            return;
        }

        ShowLinearSolution(
            a,
            b,
            calculation);

        ClearTransientFocus();
    }

    private void CalculateQuadraticEquation()
    {
        if (!TryReadCoefficient(
                CoefficientAEntry,
                GetCoefficientFieldName(
                    CoefficientAEntry),
                out Int128 a))
        {
            CoefficientAEntry.Focus();
            return;
        }

        if (!TryReadCoefficient(
                CoefficientBEntry,
                GetCoefficientFieldName(
                    CoefficientBEntry),
                out Int128 b))
        {
            CoefficientBEntry.Focus();
            return;
        }

        if (!TryReadCoefficient(
                CoefficientCEntry,
                GetCoefficientFieldName(
                    CoefficientCEntry),
                out Int128 c))
        {
            CoefficientCEntry.Focus();
            return;
        }

        if (a == Int128.Zero)
        {
            ShowError(
                LocalizationService.TranslateKey(
                    "Quadratic.ANonZero"));

            CoefficientAEntry.Focus();
            return;
        }

        ApplyCoefficientEntryDisplayValue(
            CoefficientAEntry,
            a);

        ApplyCoefficientEntryDisplayValue(
            CoefficientBEntry,
            b);

        ApplyCoefficientEntryDisplayValue(
            CoefficientCEntry,
            c);

        QuadraticEquationResult calculation =
            _quadraticEngine.Solve(a, b, c);

        if (!calculation.IsFinite)
        {
            ShowError(
                LocalizationService.TranslateKey(
                    "Quadratic.RootNotFiniteOctoDouble"));

            return;
        }

        ShowSolution(
            a,
            b,
            c,
            calculation);

        ClearTransientFocus();
    }

    public void RefreshNumberDisplay()
    {
        if (ResultBorder.IsVisible)
        {
            OnCalculateClicked(
                this,
                EventArgs.Empty);
        }
    }

    private async void OnQuadraticCopyResultClicked(
        object? sender,
        EventArgs e)
    {
        string resultText =
            string.Join(
                Environment.NewLine,
                new[]
                {
                    ResultEquationLabel.Text,
                    DeltaValueLabel.Text,
                    ClassificationLabel.Text,
                    RootsLabel.Text
                }
                .Where(
                    text =>
                        !string.IsNullOrWhiteSpace(
                            text)));

        await ResultClipboardService.CopyAsync(
            QuadraticCopyResultButton,
            resultText);
    }

    private void OnClearClicked(
        object? sender,
        EventArgs e)
    {
        _pendingRestoredEntryTexts.Clear();
        _coefficientExactIntegerValues.Clear();

        SetEntryText(
            CoefficientAEntry,
            string.Empty);

        SetEntryText(
            CoefficientBEntry,
            string.Empty);

        SetEntryText(
            CoefficientCEntry,
            string.Empty);

        HideResultAndError();
        UpdateEquationPreview();

        CoefficientAEntry.Focus();
    }

    private bool TryReadCoefficient(
        Entry entry,
        string fieldName,
        out Int128 value)
    {
        value =
            Int128.Zero;

        string normalized =
            NormalizeIntegerText(
                GetCoefficientInputText(
                    entry));

        if (normalized.Length == 0 ||
            normalized == "-")
        {
            ShowError(
                string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizationService.TranslateKey(
                        "Quadratic.RequiredCoefficient"),
                    fieldName));

            return false;
        }

        if (!TryParseCoefficientValue(
                normalized,
                out value))
        {
            ShowError(
                string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizationService.TranslateKey(
                        "Quadratic.Int128Range"),
                    fieldName,
                    Int128RangeText));

            value =
                Int128.Zero;

            return false;
        }

        return true;
    }

    private string? GetCoefficientInputText(
        Entry entry)
    {
        if (_coefficientExactIntegerValues.TryGetValue(
                entry,
                out string? scientificCode))
        {
            return scientificCode;
        }

        return entry.Text;
    }

    private static bool TryParseCoefficientText(
        string? text,
        out Int128 value)
    {
        return TryParseCoefficientValue(
            text,
            out value);
    }

    private static bool TryParseCoefficientValue(
        string? text,
        out Int128 value)
    {
        value =
            Int128.Zero;

        string normalized =
            NormalizeIntegerText(
                text);

        if (normalized.Length == 0 ||
            normalized == "-")
        {
            return false;
        }

        return Int128.TryParse(
            normalized,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static bool IsValidIntegerWhileTyping(
        string text)
    {
        string normalizedText =
            text.Replace(
                    ",",
                    string.Empty,
                    StringComparison.Ordinal)
                .Replace(
                    '−',
                    '-');

        if (normalizedText.Length == 0 ||
            normalizedText == "-")
        {
            return true;
        }

        int startIndex =
            normalizedText[0] == '-'
                ? 1
                : 0;

        if (startIndex ==
            normalizedText.Length)
        {
            return false;
        }

        for (int index = startIndex;
             index < normalizedText.Length;
             index++)
        {
            char character =
                normalizedText[index];

            // Chỉ nhận chữ số ASCII 0–9.
            if (character < '0' ||
                character > '9')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsCompleteIntegerText(
        string text)
    {
        if (text.Length == 0 ||
            text == "-")
        {
            return false;
        }

        int startIndex =
            text[0] == '-'
                ? 1
                : 0;

        if (startIndex ==
            text.Length)
        {
            return false;
        }

        for (int index = startIndex;
             index < text.Length;
             index++)
        {
            if (!char.IsDigit(
                    text[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeIntegerText(
        string? text)
    {
        return
            (text ??
             string.Empty)
            .Trim()
            .Replace(
                ",",
                string.Empty)
            .Replace(
                '−',
                '-');
    }

    private void UpdateEquationPreview()
    {
        string aText =
            GetPreviewCoefficientText(
                CoefficientAEntry,
                "a");

        string bText =
            GetPreviewCoefficientText(
                CoefficientBEntry,
                "b");

        if (_equationMode == EquationMode.Linear)
        {
            EquationPreviewLabel.Text =
                $"({aText})x + ({bText}) = 0";
            return;
        }

        string cText =
            GetPreviewCoefficientText(
                CoefficientCEntry,
                "c");

        EquationPreviewLabel.Text =
            $"({aText})x² + ({bText})x + ({cText}) = 0";
    }

    private string GetPreviewCoefficientText(
        Entry entry,
        string fallback)
    {
        string normalized =
            NormalizeIntegerText(
                GetCoefficientInputText(
                    entry));

        if (normalized.Length == 0 ||
            normalized == "-")
        {
            return fallback;
        }

        if (Int128.TryParse(
                normalized,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out Int128 value))
        {
            return FormatNumber(
                value);
        }

        return normalized.Replace(
            "-",
            "−",
            StringComparison.Ordinal);
    }

    private void ResetStep4MathPresentation()
    {
        Step4BodyLabel.IsVisible =
            true;

        Step4MathLayout.Children.Clear();

        Step4MathScrollView.IsVisible =
            false;
    }

    private void ShowDistinctRootStep4Presentation(
        Int128 a,
        Int128 b,
        BigInteger delta,
        OctoDouble squareRootDelta,
        OctoDouble firstRoot,
        OctoDouble secondRoot)
    {
        ResetStep4MathPresentation();

        string aText =
            FormatNumber(
                a);

        string bText =
            FormatNumber(
                b);

        string deltaText =
            FormatIntegerForDisplay(
                delta);

        string squareRootText =
            FormatOctoDouble(
                squareRootDelta);

        string firstRootText =
            FormatOctoDouble(
                firstRoot);

        string secondRootText =
            FormatOctoDouble(
                secondRoot);

        string denominatorText =
            FormatOctoDouble(
                2d *
                OctoDouble.FromInt128(
                    a));

        Step4BodyLabel.Text =
            "Ta áp dụng công thức nghiệm của phương trình bậc hai:";

        Step4MathLayout.Children.Add(
            CreateGeneralRootFormulaPairView());

        Step4MathLayout.Children.Add(
            CreateMathDescriptionLabel(
                $"Thay a = {aText}, b = {bText}, Δ = {deltaText} vào công thức:"));

        Step4MathLayout.Children.Add(
            CreateRootComputationSection(
                variableName: "x₁",
                substitutionNumerator: CreateSubstitutedNumeratorView(
                    bText,
                    deltaText,
                    usePlus: true),
                simplifiedNumerator: CreateSimplifiedNumeratorView(
                    bText,
                    squareRootText,
                    usePlus: true),
                substitutionDenominatorText: $"2 × ({aText})",
                simplifiedDenominatorText: denominatorText,
                resultText: firstRootText));

        Step4MathLayout.Children.Add(
            CreateRootComputationSection(
                variableName: "x₂",
                substitutionNumerator: CreateSubstitutedNumeratorView(
                    bText,
                    deltaText,
                    usePlus: false),
                simplifiedNumerator: CreateSimplifiedNumeratorView(
                    bText,
                    squareRootText,
                    usePlus: false),
                substitutionDenominatorText: $"2 × ({aText})",
                simplifiedDenominatorText: denominatorText,
                resultText: secondRootText));

        Step4MathScrollView.IsVisible =
            true;
    }

    private View CreateGeneralRootFormulaPairView()
    {
        var formulaRow =
            new HorizontalStackLayout
            {
                Spacing = 12,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Center
            };

        formulaRow.Children.Add(
            CreateRootFractionEquationView(
                variableName: "x₁",
                numeratorView: CreateGeneralNumeratorView(
                    usePlus: true),
                denominatorView: CreateMathLabel(
                    "2a",
                    fontSize: 22)));

        formulaRow.Children.Add(
            CreateMathLabel(
                "và",
                fontSize: 19,
                bold: false));

        formulaRow.Children.Add(
            CreateRootFractionEquationView(
                variableName: "x₂",
                numeratorView: CreateGeneralNumeratorView(
                    usePlus: false),
                denominatorView: CreateMathLabel(
                    "2a",
                    fontSize: 22)));

        return formulaRow;
    }

    private View CreateRootComputationSection(
        string variableName,
        View substitutionNumerator,
        View simplifiedNumerator,
        string substitutionDenominatorText,
        string simplifiedDenominatorText,
        string resultText)
    {
        var sectionLayout =
            new VerticalStackLayout
            {
                Spacing = 8,
                HorizontalOptions = LayoutOptions.Start
            };

        sectionLayout.Children.Add(
            CreateMathDescriptionLabel(
                $"Tính {variableName}:"));

        sectionLayout.Children.Add(
            CreateRootFractionEquationView(
                variableName,
                substitutionNumerator,
                CreateMathLabel(
                    substitutionDenominatorText,
                    fontSize: 21)));

        sectionLayout.Children.Add(
            CreateRootFractionEquationView(
                variableName,
                simplifiedNumerator,
                CreateMathLabel(
                    simplifiedDenominatorText,
                    fontSize: 21)));

        sectionLayout.Children.Add(
            CreateRootResultView(
                variableName,
                resultText));

        return sectionLayout;
    }

    private View CreateGeneralNumeratorView(
        bool usePlus)
    {
        return CreateInlineMathLayout(
            CreateMathLabel(
                "−b",
                fontSize: 22),
            CreateMathLabel(
                usePlus ? "+" : "−",
                fontSize: 22),
            CreateRadicalView(
                radicandText: "Δ",
                fontSize: 20,
                degreeFontSize: 12));
    }

    private View CreateSubstitutedNumeratorView(
        string bText,
        string deltaText,
        bool usePlus)
    {
        return CreateInlineMathLayout(
            CreateMathLabel(
                $"−({bText})",
                fontSize: 21),
            CreateMathLabel(
                usePlus ? "+" : "−",
                fontSize: 21),
            CreateRadicalView(
                radicandText: FormatRadicandForQuadratic(
                    deltaText),
                fontSize: 19,
                degreeFontSize: 12));
    }

    private View CreateSimplifiedNumeratorView(
        string bText,
        string squareRootText,
        bool usePlus)
    {
        return CreateInlineMathLayout(
            CreateMathLabel(
                $"−({bText})",
                fontSize: 21),
            CreateMathLabel(
                usePlus ? "+" : "−",
                fontSize: 21),
            CreateMathLabel(
                squareRootText,
                fontSize: 21));
    }

    private View CreateRootFractionEquationView(
        string variableName,
        View numeratorView,
        View denominatorView)
    {
        var equationLayout =
            new HorizontalStackLayout
            {
                Spacing = 8,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Center
            };

        equationLayout.Children.Add(
            CreateMathLabel(
                $"{variableName} =",
                fontSize: 22));

        equationLayout.Children.Add(
            CreateFractionLayout(
                numeratorView,
                denominatorView));

        return equationLayout;
    }

    private View CreateRootResultView(
        string variableName,
        string resultText)
    {
        var resultLabel =
            CreateMathLabel(
                $"{variableName} = {resultText}",
                fontSize: 22);

        resultLabel.SetDynamicResource(
            Label.TextColorProperty,
            "SuccessColor");

        return resultLabel;
    }

    private View CreateFractionLayout(
        View numeratorView,
        View denominatorView)
    {
        var fractionGrid =
            new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition(
                        GridLength.Auto),
                    new RowDefinition(
                        new GridLength(2)),
                    new RowDefinition(
                        GridLength.Auto)
                },
                RowSpacing = 4,
                ColumnSpacing = 0,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Center,
                MinimumWidthRequest = 78
            };

        numeratorView.HorizontalOptions =
            LayoutOptions.Center;

        denominatorView.HorizontalOptions =
            LayoutOptions.Center;

        var fractionBar =
            new BoxView
            {
                HeightRequest = 2,
                HorizontalOptions = LayoutOptions.Fill
            };

        fractionBar.SetDynamicResource(
            BoxView.BackgroundColorProperty,
            "TextPrimaryColor");

        fractionGrid.Add(
            numeratorView,
            0,
            0);

        fractionGrid.Add(
            fractionBar,
            0,
            1);

        fractionGrid.Add(
            denominatorView,
            0,
            2);

        return fractionGrid;
    }

    private View CreateInlineMathLayout(
        params View[] children)
    {
        var layout =
            new HorizontalStackLayout
            {
                Spacing = 6,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Center
            };

        foreach (View child in children)
        {
            layout.Children.Add(
                child);
        }

        return layout;
    }

    private Label CreateMathLabel(
        string text,
        double fontSize,
        bool bold = true)
    {
        var label =
            new Label
            {
                Text = text,
                FontSize = fontSize,
                FontAttributes =
                    bold
                        ? FontAttributes.Bold
                        : FontAttributes.None,
                VerticalTextAlignment = TextAlignment.Center,
                HorizontalTextAlignment = TextAlignment.Start,
                LineBreakMode = LineBreakMode.NoWrap
            };

        label.SetDynamicResource(
            Label.TextColorProperty,
            "TextPrimaryColor");

        return label;
    }

    private Label CreateMathDescriptionLabel(
        string text)
    {
        var label =
            new Label
            {
                Text = text,
                FontSize = 16,
                LineHeight = 1.3,
                LineBreakMode = LineBreakMode.WordWrap
            };

        label.SetDynamicResource(
            Label.TextColorProperty,
            "TextPrimaryColor");

        return label;
    }

    private View CreateRadicalView(
        string radicandText,
        double fontSize,
        double degreeFontSize)
    {
        var radicalView =
            new TextbookRadicalExpressionView
            {
                Degree = 2,
                RadicandText = radicandText,
                FontSize = fontSize,
                DegreeFontSize = degreeFontSize,
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center
            };

        radicalView.SetDynamicResource(
            TextbookRadicalExpressionView.LineColorProperty,
            "TextPrimaryColor");

        radicalView.SetDynamicResource(
            TextbookRadicalExpressionView.TextColorProperty,
            "TextPrimaryColor");

        return radicalView;
    }

    private static string FormatRadicandForQuadratic(
        string radicandText)
    {
        if (string.IsNullOrWhiteSpace(
                radicandText))
        {
            return string.Empty;
        }

        return radicandText.StartsWith(
                "−",
                StringComparison.Ordinal)
            ? $"({radicandText})"
            : radicandText;
    }

    private void ShowLinearSolution(
        Int128 a,
        Int128 b,
        LinearEquationResult calculation)
    {
        string aText =
            FormatNumber(a);

        string bText =
            FormatNumber(b);

        string rootText =
            FormatQuadDouble(
                calculation.Root);

        string equation =
            BuildLinearEquationText(
                a,
                b);

        ResultEquationLabel.Text =
            equation;

        ResultMetricCaptionLabel.Text =
            T("Equation.Linear.ResultMetric");

        DeltaValueLabel.Text =
            "x = −b ÷ a";

        ClassificationLabel.Text =
            T("Equation.Linear.UniqueRoot");

        RootsLabel.Text =
            $"x = {rootText}";

        SetResultStateColors(
            hasRealRoots: true);

        Step1TitleLabel.Text =
            T("Equation.Linear.Step1.Title");

        Step1BodyLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                T("Equation.Linear.Step1.Body"),
                aText,
                bText,
                equation);

        Step2TitleLabel.Text =
            T("Equation.Linear.Step2.Title");

        Step2BodyLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                T("Equation.Linear.Step2.Body"),
                aText,
                bText,
                FormatIntegerForDisplay(
                    -(BigInteger)b));

        Step3Border.IsVisible =
            true;

        Step3TitleLabel.Text =
            T("Equation.Linear.Step3.Title");

        Step3BodyLabel.Text =
            string.Format(
                CultureInfo.CurrentCulture,
                T("Equation.Linear.Step3.Body"),
                FormatIntegerForDisplay(
                    -(BigInteger)b),
                aText,
                rootText);

        Step4Border.IsVisible =
            false;

        ResetStep4MathPresentation();

        ShowLinearGraph(
            a,
            b);

        ErrorBorder.IsVisible =
            false;

        ResultBorder.IsVisible =
            true;

        SolutionBorder.IsVisible =
            true;
    }

    private void ShowSolution(
        Int128 a,
        Int128 b,
        Int128 c,
        QuadraticEquationResult calculation)
    {
        BigInteger delta = calculation.Delta;
        string aText =
            FormatNumber(
                a);

        string bText =
            FormatNumber(
                b);

        string cText =
            FormatNumber(
                c);

        string deltaText =
            FormatIntegerForDisplay(
                delta);

        string equation =
            BuildEquationText(
                a,
                b,
                c);

        ResultEquationLabel.Text =
            equation;

        DeltaValueLabel.Text =
            $"Δ = {deltaText}";

        Step1TitleLabel.Text =
            "Bước 1. Xác định các hệ số";

        Step1BodyLabel.Text =
            $"Phương trình có dạng ax² + bx + c = 0.\n" +
            $"Ta có: a = {aText}, b = {bText}, c = {cText}.\n" +
            $"Phương trình: {equation}";

        Step2TitleLabel.Text =
            "Bước 2. Tính biệt thức Δ";

        Step2BodyLabel.Text =
            $"Δ = b² − 4ac\n" +
            $"Δ = ({bText})² − 4 × ({aText}) × ({cText})\n" +
            $"Δ = {deltaText}";

        Step3Border.IsVisible =
            true;

        Step4Border.IsVisible =
            true;

        ResetStep4MathPresentation();

        if (calculation.Kind == QuadraticSolutionKind.NoRealRoots)
        {
            SetResultStateColors(
                hasRealRoots: false);

            ClassificationLabel.Text =
                "Δ < 0: phương trình vô nghiệm trong tập số thực.";

            RootsLabel.Text =
                "Vô nghiệm";

            Step3TitleLabel.Text =
                "Bước 3. Xét dấu của Δ";

            Step3BodyLabel.Text =
                $"Vì Δ = {deltaText} < 0 nên √Δ không phải " +
                "là một số thực.";

            Step4TitleLabel.Text =
                "Bước 4. Kết luận";

            Step4BodyLabel.Text =
                "Phương trình vô nghiệm trong tập số thực ℝ.";
        }
        else if (calculation.Kind == QuadraticSolutionKind.DoubleRoot)
        {
            OctoDouble doubleRoot = calculation.FirstRoot;

            SetResultStateColors(
                hasRealRoots: true);

            string rootText =
                FormatOctoDouble(
                    doubleRoot);

            ClassificationLabel.Text =
                "Δ = 0: phương trình có nghiệm kép.";

            RootsLabel.Text =
                $"x₁ = x₂ = {rootText}";

            Step3TitleLabel.Text =
                "Bước 3. Tính nghiệm kép";

            Step3BodyLabel.Text =
                $"x = −b / (2a)\n" +
                $"x = −({bText}) / " +
                $"(2 × ({aText}))\n" +
                $"x = {rootText}";

            Step4TitleLabel.Text =
                "Bước 4. Kết luận";

            Step4BodyLabel.Text =
                $"Phương trình có nghiệm kép " +
                $"x₁ = x₂ = {rootText}.";
        }
        else
        {
            OctoDouble squareRootDelta = calculation.SquareRootDelta;
            OctoDouble firstRoot = calculation.FirstRoot;
            OctoDouble secondRoot = calculation.SecondRoot;

            SetResultStateColors(
                hasRealRoots: true);

            string squareRootText =
                FormatOctoDouble(
                    squareRootDelta);

            string firstRootText =
                FormatOctoDouble(
                    firstRoot);

            string secondRootText =
                FormatOctoDouble(
                    secondRoot);

            ClassificationLabel.Text =
                "Δ > 0: phương trình có hai nghiệm phân biệt.";

            RootsLabel.Text =
                $"x₁ = {firstRootText}\n" +
                $"x₂ = {secondRootText}";

            Step3TitleLabel.Text =
                "Bước 3. Tính căn bậc hai của Δ";

            Step3BodyLabel.Text =
                $"√Δ = √({deltaText}) = {squareRootText}";

            Step4TitleLabel.Text =
                "Bước 4. Tính hai nghiệm";

            ShowDistinctRootStep4Presentation(
                a,
                b,
                delta,
                squareRootDelta,
                firstRoot,
                secondRoot);
        }

        ShowParabolaGraph(
            a,
            b,
            c);

        ErrorBorder.IsVisible =
            false;

        ResultBorder.IsVisible =
            true;

        SolutionBorder.IsVisible =
            true;
    }

    private void OnGraphStartInteraction(
        object? sender,
        TouchEventArgs e)
    {
        if (e.Touches.Length == 0)
        {
            _lastGraphPointer =
                null;

            return;
        }

        _lastGraphPointer =
            e.Touches[0];
    }

    private void OnGraphDragInteraction(
        object? sender,
        TouchEventArgs e)
    {
        if (!_lastGraphPointer.HasValue ||
            e.Touches.Length == 0)
        {
            return;
        }

        Microsoft.Maui.Graphics.PointF currentPoint =
            e.Touches[0];

        Microsoft.Maui.Graphics.PointF previousPoint =
            _lastGraphPointer.Value;

        float deltaX =
            currentPoint.X -
            previousPoint.X;

        float deltaY =
            currentPoint.Y -
            previousPoint.Y;

        _lastGraphPointer =
            currentPoint;

        if (PanCurrentGraphByPixels(
                deltaX,
                deltaY))
        {
            ParabolaGraphicsView.Invalidate();
        }
    }

    private void OnGraphEndInteraction(
        object? sender,
        TouchEventArgs e)
    {
        _lastGraphPointer =
            null;
    }

    private void OnGraphCancelInteraction(
        object? sender,
        EventArgs e)
    {
        _lastGraphPointer =
            null;
    }

    private void ShowLinearGraph(
        Int128 a,
        Int128 b)
    {
        _lastGraphPointer =
            null;

        ParabolaGraphicsView.Drawable =
            _linearGraphDrawable;

        _linearGraphDrawable.SetEquation(
            a,
            b);

        _linearGraphDrawable.ResetZoom();

        GraphBorder.IsVisible =
            true;

        UpdateGraphStatus();

        ParabolaGraphicsView.Invalidate();
    }

    private void ShowParabolaGraph(
        Int128 a,
        Int128 b,
        Int128 c)
    {
        _lastGraphPointer =
            null;

        ParabolaGraphicsView.Drawable =
            _parabolaGraphDrawable;

        _parabolaGraphDrawable.SetEquation(
            a,
            b,
            c);

        _parabolaGraphDrawable.ResetZoom();

        GraphBorder.IsVisible =
            true;

        UpdateGraphStatus();

        ParabolaGraphicsView.Invalidate();
    }

    private void OnGraphZoomInClicked(
        object? sender,
        EventArgs e)
    {
        if (!CurrentGraphHasEquation)
        {
            return;
        }

        ZoomCurrentGraphIn();

        UpdateGraphStatus();

        ParabolaGraphicsView.Invalidate();
    }

    private void OnGraphZoomOutClicked(
        object? sender,
        EventArgs e)
    {
        if (!CurrentGraphHasEquation)
        {
            return;
        }

        ZoomCurrentGraphOut();

        UpdateGraphStatus();

        ParabolaGraphicsView.Invalidate();
    }

    private void OnGraphResetZoomClicked(
        object? sender,
        EventArgs e)
    {
        if (!CurrentGraphHasEquation)
        {
            return;
        }

        _lastGraphPointer =
            null;

        ResetCurrentGraphZoom();

        UpdateGraphStatus();

        ParabolaGraphicsView.Invalidate();
    }

    private void UpdateGraphStatus()
    {
        int zoomPercent =
            CurrentGraphZoomPercent;

        GraphStatusLabel.Text =
            $"Zoom: {zoomPercent}%";

        GraphResetZoomButton.Text =
            $"{zoomPercent}%";
    }

    private void SetResultStateColors(
        bool hasRealRoots)
    {
        string stateColorResource =
            hasRealRoots
                ? "SuccessColor"
                : "ErrorColor";

        ClassificationLabel.SetDynamicResource(
            Label.TextColorProperty,
            stateColorResource);

        RootsLabel.SetDynamicResource(
            Label.TextColorProperty,
            hasRealRoots
                ? "TextPrimaryColor"
                : "ErrorColor");
    }

    private static string BuildLinearEquationText(
        Int128 a,
        Int128 b)
    {
        var builder =
            new StringBuilder();

        AppendLeadingTerm(
            builder,
            a,
            "x");

        AppendFollowingTerm(
            builder,
            b,
            string.Empty);

        builder.Append(
            " = 0");

        return builder.ToString();
    }

    private static string BuildEquationText(
        Int128 a,
        Int128 b,
        Int128 c)
    {
        var builder =
            new StringBuilder();

        AppendLeadingTerm(
            builder,
            a,
            "x²");

        AppendFollowingTerm(
            builder,
            b,
            "x");

        AppendFollowingTerm(
            builder,
            c,
            string.Empty);

        builder.Append(
            " = 0");

        return builder.ToString();
    }

    private static void AppendLeadingTerm(
        StringBuilder builder,
        Int128 coefficient,
        string variable)
    {
        if (coefficient == -1 &&
            variable.Length > 0)
        {
            builder.Append(
                '−');

            builder.Append(
                variable);

            return;
        }

        if (coefficient == 1 &&
            variable.Length > 0)
        {
            builder.Append(
                variable);

            return;
        }

        builder.Append(
            FormatNumber(
                coefficient));

        builder.Append(
            variable);
    }

    private static void AppendFollowingTerm(
        StringBuilder builder,
        Int128 coefficient,
        string variable)
    {
        if (coefficient == 0)
        {
            return;
        }

        bool isNegative =
            coefficient < 0;

        builder.Append(
            isNegative
                ? " − "
                : " + ");

        BigInteger absoluteCoefficient =
            BigInteger.Abs(
                (BigInteger)coefficient);

        if (absoluteCoefficient != BigInteger.One ||
            variable.Length == 0)
        {
            builder.Append(
                FormatIntegerForDisplay(
                    absoluteCoefficient));
        }

        builder.Append(
            variable);
    }

    private void ApplyCoefficientEntryDisplayValue(
        Entry entry,
        Int128 value)
    {
        string standardText =
            FormatInputInteger(
                value);

        if (CountNumericDigits(
                standardText) <=
            ScientificDisplayDigitThreshold)
        {
            _coefficientExactIntegerValues.Remove(
                entry);

            SetEntryText(
                entry,
                standardText);

            return;
        }

        _coefficientExactIntegerValues[entry] =
            value.ToString(
                CultureInfo.InvariantCulture);

        SetEntryText(
            entry,
            FormatScientificForDisplay(
                value));
    }

    private static string FormatInputInteger(
        Int128 value)
    {
        return ((BigInteger)value).ToString(
            "N0",
            CultureInfo.InvariantCulture);
    }

    private static string FormatQuadDouble(
        QuadDouble value)
    {
        string text =
            value.ToGeneralString(
                QuadDoubleDisplaySignificantDigits,
                scientificUpperExponent:
                ResultNumberDisplayMode.ShowFullNumbers
                    ? int.MaxValue
                    : ScientificDisplayDigitThreshold,
                scientificLowerExponent:
                -10);

        int exponentSeparatorIndex =
            text.IndexOf(
                'e');

        if (exponentSeparatorIndex >= 0)
        {
            string mantissaText =
                RoundDecimalText(
                    text[..exponentSeparatorIndex],
                    MaxResultDecimalPlaces);

            int exponent =
                int.Parse(
                    text[(exponentSeparatorIndex + 1)..],
                    CultureInfo.InvariantCulture);

            if (mantissaText == "10")
            {
                mantissaText = "1";
                exponent++;
            }
            else if (mantissaText == "-10")
            {
                mantissaText = "-1";
                exponent++;
            }

            if (mantissaText == "1")
            {
                return $"10{ToSuperscript(exponent)}";
            }

            if (mantissaText == "-1")
            {
                return $"−10{ToSuperscript(exponent)}";
            }

            mantissaText =
                mantissaText.Replace(
                    "-",
                    "−",
                    StringComparison.Ordinal);

            return
                $"{mantissaText} × " +
                $"10{ToSuperscript(exponent)}";
        }

        text =
            RoundDecimalText(
                text,
                MaxResultDecimalPlaces);

        bool isNegative =
            text.StartsWith(
                '-');

        string unsignedText =
            isNegative
                ? text[1..]
                : text;

        int decimalSeparatorIndex =
            unsignedText.IndexOf(
                '.');

        string integerPart =
            decimalSeparatorIndex >= 0
                ? unsignedText[..decimalSeparatorIndex]
                : unsignedText;

        string fractionPart =
            decimalSeparatorIndex >= 0
                ? unsignedText[decimalSeparatorIndex..]
                : string.Empty;

        return
            (isNegative ? "−" : string.Empty) +
            GroupIntegerDigits(
                integerPart) +
            fractionPart;
    }

    private static string FormatOctoDouble(
        OctoDouble value)
    {
        // Octo Double giữ khoảng 127-128 chữ số có nghĩa trong toàn bộ
        // quá trình tính toán. Chỉ bước trình bày cuối cùng mới làm tròn,
        // giới hạn tối đa 10 chữ số sau dấu thập phân.
        string text =
            value.ToGeneralString(
                OctoDoubleDisplaySignificantDigits,
                scientificUpperExponent:
                ResultNumberDisplayMode.ShowFullNumbers
                    ? int.MaxValue
                    : ScientificDisplayDigitThreshold,
                scientificLowerExponent:
                -10);

        int exponentSeparatorIndex =
            text.IndexOf(
                'e');

        if (exponentSeparatorIndex >= 0)
        {
            string mantissaText =
                RoundDecimalText(
                    text[..exponentSeparatorIndex],
                    MaxResultDecimalPlaces);

            int exponent =
                int.Parse(
                    text[(exponentSeparatorIndex + 1)..],
                    CultureInfo.InvariantCulture);

            // 9.99999999996 làm tròn thành 10. Khi đó chuẩn hóa lại
            // thành 1 × 10^(n + 1) để dạng khoa học luôn đúng.
            if (mantissaText == "10")
            {
                mantissaText =
                    "1";

                exponent++;
            }
            else if (mantissaText == "-10")
            {
                mantissaText =
                    "-1";

                exponent++;
            }

            if (mantissaText == "1")
            {
                return
                    $"10{ToSuperscript(exponent)}";
            }

            if (mantissaText == "-1")
            {
                return
                    $"−10{ToSuperscript(exponent)}";
            }

            mantissaText =
                mantissaText.Replace(
                    "-",
                    "−",
                    StringComparison.Ordinal);

            return
                $"{mantissaText} × " +
                $"10{ToSuperscript(exponent)}";
        }

        text =
            RoundDecimalText(
                text,
                MaxResultDecimalPlaces);

        bool isNegative =
            text.StartsWith(
                '-');

        string unsignedText =
            isNegative
                ? text[1..]
                : text;

        int decimalSeparatorIndex =
            unsignedText.IndexOf(
                '.');

        string integerPart =
            decimalSeparatorIndex >= 0
                ? unsignedText[..decimalSeparatorIndex]
                : unsignedText;

        string fractionPart =
            decimalSeparatorIndex >= 0
                ? unsignedText[decimalSeparatorIndex..]
                : string.Empty;

        string sign =
            isNegative
                ? "−"
                : string.Empty;

        return
            sign +
            GroupIntegerDigits(
                integerPart) +
            fractionPart;
    }

    /// <summary>
    /// Làm tròn chuỗi thập phân theo MidpointRounding.AwayFromZero mà không
    /// chuyển ngược về double/decimal. Nhờ vậy phép tính vẫn tận dụng đủ
    /// độ chính xác Octo Double và chỉ kết quả hiển thị bị giới hạn.
    /// </summary>
    private static string RoundDecimalText(
        string text,
        int maximumDecimalPlaces)
    {
        maximumDecimalPlaces =
            Math.Max(
                0,
                maximumDecimalPlaces);

        if (string.IsNullOrEmpty(
                text))
        {
            return
                "0";
        }

        bool isNegative =
            text[0] ==
            '-';

        string unsignedText =
            isNegative
                ? text[1..]
                : text;

        int decimalSeparatorIndex =
            unsignedText.IndexOf(
                '.');

        if (decimalSeparatorIndex < 0)
        {
            return
                text;
        }

        string integerPart =
            unsignedText[..decimalSeparatorIndex];

        string fractionPart =
            unsignedText[(decimalSeparatorIndex + 1)..];

        bool mustRoundUp =
            fractionPart.Length >
                maximumDecimalPlaces &&
            fractionPart[maximumDecimalPlaces] >=
                '5';

        string keptFraction =
            fractionPart.Length <=
                maximumDecimalPlaces
                ? fractionPart
                : fractionPart[..maximumDecimalPlaces];

        var keptDigits =
            new StringBuilder(
                integerPart.Length +
                keptFraction.Length +
                1);

        keptDigits.Append(
            integerPart);

        keptDigits.Append(
            keptFraction);

        if (keptDigits.Length == 0)
        {
            keptDigits.Append(
                '0');
        }

        if (mustRoundUp)
        {
            int index =
                keptDigits.Length -
                1;

            while (index >= 0 &&
                   keptDigits[index] ==
                   '9')
            {
                keptDigits[index] =
                    '0';

                index--;
            }

            if (index >= 0)
            {
                keptDigits[index]++;
            }
            else
            {
                keptDigits.Insert(
                    0,
                    '1');
            }
        }

        int keptFractionLength =
            keptFraction.Length;

        int integerLength =
            keptDigits.Length -
            keptFractionLength;

        if (integerLength <= 0)
        {
            keptDigits.Insert(
                0,
                "0",
                1 -
                integerLength);

            integerLength =
                1;
        }

        string roundedIntegerPart =
            keptDigits.ToString(
                0,
                integerLength);

        string roundedFractionPart =
            keptFractionLength > 0
                ? keptDigits.ToString(
                    integerLength,
                    keptDigits.Length -
                    integerLength)
                : string.Empty;

        roundedFractionPart =
            roundedFractionPart.TrimEnd(
                '0');

        bool isZero =
            roundedIntegerPart.All(
                character =>
                    character ==
                    '0') &&
            roundedFractionPart.All(
                character =>
                    character ==
                    '0');

        string sign =
            isNegative &&
            !isZero
                ? "-"
                : string.Empty;

        if (roundedFractionPart.Length == 0)
        {
            return
                sign +
                roundedIntegerPart;
        }

        return
            sign +
            roundedIntegerPart +
            "." +
            roundedFractionPart;
    }

    private static string FormatNumber(
        Int128 value)
    {
        return FormatIntegerForDisplay(
            (BigInteger)value);
    }

    private static string FormatIntegerForDisplay(
        BigInteger value)
    {
        string digits =
            BigInteger.Abs(
                    value)
                .ToString(
                    CultureInfo.InvariantCulture);

        if (!ResultNumberDisplayMode.ShowFullNumbers &&
            digits.Length >
            ScientificDisplayDigitThreshold)
        {
            return FormatScientificForDisplay(
                value);
        }

        return value.ToString(
                "N0",
                CultureInfo.InvariantCulture)
            .Replace(
                "-",
                "−",
                StringComparison.Ordinal);
    }

    private static int CountNumericDigits(
        string text)
    {
        int count =
            0;

        foreach (char character in text)
        {
            if (character >= '0' &&
                character <= '9')
            {
                count++;
            }
        }

        return count;
    }

    private static string FormatScientificForDisplay(
        Int128 value)
    {
        return FormatScientificForDisplay(
            (BigInteger)value);
    }

    private static string FormatScientificForDisplay(
        BigInteger value)
    {
        if (value.IsZero)
        {
            return "0";
        }

        bool isNegative =
            value.Sign < 0;

        string digits =
            BigInteger.Abs(
                    value)
                .ToString(
                    CultureInfo.InvariantCulture);

        int exponent =
            digits.Length -
            1;

        int keptDigits =
            Math.Min(
                ScientificDisplaySignificantDigits,
                digits.Length);

        string mantissaDigits =
            digits[..keptDigits];

        bool mustRoundUp =
            digits.Length >
                keptDigits &&
            digits[keptDigits] >=
                '5';

        if (mustRoundUp)
        {
            BigInteger rounded =
                BigInteger.Parse(
                    mantissaDigits,
                    CultureInfo.InvariantCulture) +
                BigInteger.One;

            mantissaDigits =
                rounded.ToString(
                    CultureInfo.InvariantCulture);

            if (mantissaDigits.Length >
                keptDigits)
            {
                exponent++;
                mantissaDigits =
                    mantissaDigits[..keptDigits];
            }
            else
            {
                mantissaDigits =
                    mantissaDigits.PadLeft(
                        keptDigits,
                        '0');
            }
        }

        string mantissa =
            mantissaDigits.Length == 1
                ? mantissaDigits
                : mantissaDigits[0] +
                  "." +
                  mantissaDigits[1..]
                      .TrimEnd(
                          '0');

        mantissa =
            mantissa.TrimEnd(
                '.');

        string approximation =
            digits.Length >
                keptDigits
                ? "≈ "
                : string.Empty;

        string sign =
            isNegative
                ? "−"
                : string.Empty;

        if (mantissa == "1")
        {
            return
                $"{approximation}{sign}10{ToSuperscript(exponent)}";
        }

        return
            $"{approximation}{sign}{mantissa} × " +
            $"10{ToSuperscript(exponent)}";
    }

    private static string GroupIntegerDigits(
        string digits)
    {
        if (digits.Length <= 3)
        {
            return digits;
        }

        var builder =
            new StringBuilder(
                digits.Length +
                digits.Length / 3);

        int firstGroupLength =
            digits.Length %
            3;

        if (firstGroupLength == 0)
        {
            firstGroupLength =
                3;
        }

        builder.Append(
            digits.AsSpan(
                0,
                firstGroupLength));

        for (int index = firstGroupLength;
             index < digits.Length;
             index += 3)
        {
            builder.Append(
                ',');

            builder.Append(
                digits.AsSpan(
                    index,
                    3));
        }

        return builder.ToString();
    }

    private static string ToSuperscript(
        int exponent)
    {
        string exponentText =
            exponent.ToString(
                CultureInfo.InvariantCulture);

        var builder =
            new StringBuilder(
                exponentText.Length);

        foreach (char character
                 in exponentText)
        {
            builder.Append(
                character switch
                {
                    '-' => '⁻',
                    '0' => '⁰',
                    '1' => '¹',
                    '2' => '²',
                    '3' => '³',
                    '4' => '⁴',
                    '5' => '⁵',
                    '6' => '⁶',
                    '7' => '⁷',
                    '8' => '⁸',
                    '9' => '⁹',
                    _ => character
                });
        }

        return builder.ToString();
    }

    private void SetEntryText(
        Entry entry,
        string text,
        int? cursorPosition = null)
    {
        _isUpdatingText =
            true;

        try
        {
            entry.Text =
                text;

            entry.CursorPosition =
                Math.Clamp(
                    cursorPosition ??
                    text.Length,
                    0,
                    text.Length);

            entry.SelectionLength =
                0;
        }
        finally
        {
            _isUpdatingText =
                false;
        }
    }

    private void ShowError(
        string message)
    {
        ErrorLabel.Text =
            message;

        ErrorBorder.IsVisible =
            true;

        ResultBorder.IsVisible =
            false;

        SolutionBorder.IsVisible =
            false;

        GraphBorder.IsVisible =
            false;

    }

    private void HideResultAndError()
    {
        ErrorBorder.IsVisible =
            false;

        ResultBorder.IsVisible =
            false;

        SolutionBorder.IsVisible =
            false;

        GraphBorder.IsVisible =
            false;

    }


}
