using MathSolver.Graphics;
using MathSolver.Services;
using System.Globalization;
using System.Text;

namespace MathSolver.Views;

public partial class QuadraticEquationView : ContentView
{
    // Phạm vi đầy đủ của kiểu decimal.
    private const decimal MinSupportedValue =
        -79_228_162_514_264_337_593_543_950_335m;

    private const decimal MaxSupportedValue =
        79_228_162_514_264_337_593_543_950_335m;

    private const string SupportedDecimalRangeText =
        "−79,228,162,514,264,337,593,543,950,335 đến " +
        "79,228,162,514,264,337,593,543,950,335";

    private const int MaxResultDecimalPlaces =
        10;

    // Giống các tab nhập số khác: từ 19 chữ số trở lên, giao diện
    // rút gọn sang dạng khoa học nhưng vẫn giữ giá trị decimal chính xác.
    private const int ScientificDisplayDigitThreshold =
        18;

    private const int ScientificDisplaySignificantDigits =
        12;

    // Lưu biểu diễn dạng code, ví dụ 1e19, khi Entry đang hiển thị 10¹⁹.
    private readonly Dictionary<Entry, string>
        _coefficientScientificCodeValues =
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

        Loaded +=
            OnQuadraticViewLoaded;

        Unloaded +=
            OnQuadraticViewUnloaded;

        ApplyCurrentGraphTheme();

        LocalizationService.Attach(
            this);

        UpdateGraphStatus();

        ConfigureExpandedLayout();

        _isCompactLayout =
            false;

        UpdateEquationPreview();
    }

    private void OnQuadraticViewLoaded(
        object? sender,
        EventArgs e)
    {
        SubscribeGraphThemeChanges();
        ApplyCurrentGraphTheme();

#if WINDOWS
        AttachWindowsQuadraticScrollViewer();
        ConfigureWindowsCoefficientEntries();
        AttachWindowsGraphMouseWheel();
#endif
    }

    private void OnQuadraticViewUnloaded(
        object? sender,
        EventArgs e)
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
                _parabolaGraphDrawable.SetDarkTheme(
                    e.RequestedTheme ==
                    AppTheme.Dark);

                ParabolaGraphicsView.Invalidate();
            });
    }

    private void ApplyCurrentGraphTheme()
    {
        AppTheme requestedTheme =
            Application.Current?.RequestedTheme ??
            AppTheme.Light;

        _parabolaGraphDrawable.SetDarkTheme(
            requestedTheme ==
            AppTheme.Dark);
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
        object sender,
        Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_parabolaGraphDrawable.HasEquation ||
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
            _parabolaGraphDrawable.ZoomAtPixel(
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

        for (int index = 0;
             index < 3;
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

        SetCoefficientPanelPosition(
            CoefficientCPanel,
            row: 2,
            column: 0);

        CoefficientGrid.ColumnSpacing =
            0;

        CoefficientGrid.RowSpacing =
            10;
    }

    private void ConfigureExpandedLayout()
    {
        CoefficientGrid.ColumnDefinitions.Clear();
        CoefficientGrid.RowDefinitions.Clear();

        for (int index = 0;
             index < 3;
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

        SetCoefficientPanelPosition(
            CoefficientCPanel,
            row: 0,
            column: 2);

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
        _coefficientScientificCodeValues.Remove(
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
                $"{fieldName} chỉ được nhập số nguyên; " +
                "không được dùng dấu chấm (.) hoặc dấu phẩy (,), " +
                "chữ cái hay ký tự khác.");

            return;
        }

        string normalized =
            NormalizeIntegerText(
                newText);

        if (IsCompleteIntegerText(
                normalized) &&
            (!decimal.TryParse(
                 normalized,
                 NumberStyles.AllowLeadingSign,
                 CultureInfo.InvariantCulture,
                 out decimal typedValue) ||
             !IsWithinSupportedDecimalRange(
                 typedValue)))
        {
            RejectCoefficientInput(
                entry,
                e.OldTextValue,
                $"{fieldName} phải nằm trong phạm vi từ " +
                $"{SupportedDecimalRangeText}.");

            return;
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
            return "Hệ số a";
        }

        if (ReferenceEquals(
                entry,
                CoefficientBEntry))
        {
            return "Hệ số b";
        }

        return "Hệ số c";
    }

    private void OnCoefficientEntryFocused(
        object? sender,
        FocusEventArgs e)
    {
        if (sender is not Entry entry)
        {
            return;
        }

        if (_coefficientScientificCodeValues.TryGetValue(
                entry,
                out string? scientificCode) &&
            decimal.TryParse(
                scientificCode,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out decimal exactValue))
        {
            _coefficientScientificCodeValues.Remove(
                entry);

            // Khi focus, trả về chuỗi số nguyên đầy đủ, không có dấu phẩy,
            // để người dùng có thể sửa trực tiếp từng chữ số.
            SetEntryText(
                entry,
                exactValue.ToString(
                    "0",
                    CultureInfo.InvariantCulture));

            return;
        }

        string normalized =
            NormalizeIntegerText(
                entry.Text);

        if (normalized.Length == 0)
        {
            return;
        }

        SetEntryText(
            entry,
            normalized);
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
                out decimal value))
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

        if (!TryReadCoefficient(
                CoefficientAEntry,
                "hệ số a",
                out decimal a))
        {
            CoefficientAEntry.Focus();
            return;
        }

        if (!TryReadCoefficient(
                CoefficientBEntry,
                "hệ số b",
                out decimal b))
        {
            CoefficientBEntry.Focus();
            return;
        }

        if (!TryReadCoefficient(
                CoefficientCEntry,
                "hệ số c",
                out decimal c))
        {
            CoefficientCEntry.Focus();
            return;
        }

        if (a == 0m)
        {
            ShowError(
                "Hệ số a phải khác 0. Khi a = 0, " +
                "biểu thức không còn là phương trình bậc hai.");

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

        if (!TryCalculateDelta(
                a,
                b,
                c,
                out decimal delta))
        {
            ShowError(
                $"Kết quả Δ nằm ngoài phạm vi từ " +
                $"{SupportedDecimalRangeText}. " +
                "Ứng dụng không thể tiếp tục tính toán.");

            return;
        }

        ShowSolution(
            a,
            b,
            c,
            delta);

        ClearTransientFocus();
    }

    private void OnClearClicked(
        object? sender,
        EventArgs e)
    {
        _pendingRestoredEntryTexts.Clear();
        _coefficientScientificCodeValues.Clear();

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
        out decimal value)
    {
        value =
            0m;

        string normalized =
            NormalizeIntegerText(
                GetCoefficientInputText(
                    entry));

        if (normalized.Length == 0 ||
            normalized == "-")
        {
            ShowError(
                $"Vui lòng nhập {fieldName}.");

            return false;
        }

        if (!TryParseCoefficientValue(
                normalized,
                out value))
        {
            ShowError(
                $"{fieldName} phải là số nguyên hợp lệ.");

            value =
                0m;

            return false;
        }

        if (!IsWithinSupportedDecimalRange(
                value))
        {
            ShowError(
                $"{fieldName} phải nằm trong phạm vi từ " +
                $"{SupportedDecimalRangeText}.");

            value =
                0m;

            return false;
        }

        return true;
    }

    private string? GetCoefficientInputText(
        Entry entry)
    {
        if (_coefficientScientificCodeValues.TryGetValue(
                entry,
                out string? scientificCode))
        {
            return scientificCode;
        }

        return entry.Text;
    }

    private static bool TryParseCoefficientText(
        string? text,
        out decimal value)
    {
        return TryParseCoefficientValue(
                   text,
                   out value) &&
               IsWithinSupportedDecimalRange(
                   value);
    }

    private static bool TryParseCoefficientValue(
        string? text,
        out decimal value)
    {
        value =
            0m;

        string normalized =
            NormalizeIntegerText(
                text);

        if (normalized.Length == 0 ||
            normalized == "-")
        {
            return false;
        }

        NumberStyles styles =
            normalized.Contains(
                "e",
                StringComparison.OrdinalIgnoreCase)
                ? NumberStyles.Float
                : NumberStyles.AllowLeadingSign;

        return decimal.TryParse(
                   normalized,
                   styles,
                   CultureInfo.InvariantCulture,
                   out value) &&
               decimal.Truncate(
                   value) ==
               value;
    }

    private static bool IsValidIntegerWhileTyping(
        string text)
    {
        if (text.Length == 0 ||
            text == "-" ||
            text == "−")
        {
            return true;
        }

        int startIndex =
            text[0] == '-' ||
            text[0] == '−'
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
            char character =
                text[index];

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

        if (decimal.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out decimal value))
        {
            return FormatNumber(
                value);
        }

        return normalized.Replace(
            "-",
            "−",
            StringComparison.Ordinal);
    }

    private static bool TryCalculateDelta(
        decimal a,
        decimal b,
        decimal c,
        out decimal delta)
    {
        delta =
            0m;

        try
        {
            // Scalar decimal thông thường:
            // Δ = b² − 4ac
            decimal bSquared =
                checked(
                    b * b);

            decimal fourAc =
                checked(
                    checked(
                        4m * a) *
                    c);

            delta =
                checked(
                    bSquared -
                    fourAc);

            return IsWithinSupportedDecimalRange(
                delta);
        }
        catch (OverflowException)
        {
            delta =
                0m;

            return false;
        }
    }

    private static bool TryCalculateDoubleRoot(
        decimal a,
        decimal b,
        out decimal root)
    {
        root =
            0m;

        try
        {
            // Chia b cho 2 trước để tránh lấy đối của
            // decimal.MinValue và không tạo 2a bị tràn.
            root =
                checked(
                    -checked(
                        checked(
                            b / 2m) /
                        a));

            return IsWithinSupportedDecimalRange(
                root);
        }
        catch (OverflowException)
        {
            root =
                0m;

            return false;
        }
    }

    private static bool TryCalculateDistinctRoots(
        decimal a,
        decimal b,
        decimal delta,
        out decimal squareRootDelta,
        out decimal firstRoot,
        out decimal secondRoot)
    {
        squareRootDelta =
            0m;

        firstRoot =
            0m;

        secondRoot =
            0m;

        try
        {
            squareRootDelta =
                CalculateDecimalSquareRoot(
                    delta);

            // Chia cho 2 trước để không tạo 2a bị tràn
            // và tránh lấy đối trực tiếp của decimal.MinValue.
            decimal center =
                checked(
                    -checked(
                        checked(
                            b / 2m) /
                        a));

            decimal offset =
                checked(
                    checked(
                        squareRootDelta / 2m) /
                    a);

            firstRoot =
                checked(
                    center +
                    offset);

            secondRoot =
                checked(
                    center -
                    offset);

            return IsWithinSupportedDecimalRange(
                       firstRoot) &&
                   IsWithinSupportedDecimalRange(
                       secondRoot);
        }
        catch (OverflowException)
        {
            squareRootDelta =
                0m;

            firstRoot =
                0m;

            secondRoot =
                0m;

            return false;
        }
    }

    private static decimal CalculateDecimalSquareRoot(
        decimal value)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value));
        }

        if (value == 0m)
        {
            return 0m;
        }

        decimal current =
            value >= 1m
                ? value / 2m
                : 1m;

        for (int iteration = 0;
             iteration < 128;
             iteration++)
        {
            decimal next =
                checked(
                    (current +
                     value / current) /
                    2m);

            if (next == current ||
                decimal.Round(
                    next,
                    MaxResultDecimalPlaces + 2,
                    MidpointRounding.ToEven) ==
                decimal.Round(
                    current,
                    MaxResultDecimalPlaces + 2,
                    MidpointRounding.ToEven))
            {
                return next;
            }

            current =
                next;
        }

        return current;
    }

    private void ShowSolution(
        decimal a,
        decimal b,
        decimal c,
        decimal delta)
    {
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
            FormatNumber(
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

        if (delta < 0m)
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
        else if (delta == 0m)
        {
            if (!TryCalculateDoubleRoot(
                    a,
                    b,
                    out decimal doubleRoot))
            {
                ShowError(
                    $"Nghiệm nằm ngoài phạm vi từ " +
                    $"{SupportedDecimalRangeText}. " +
                    "Ứng dụng không thể tiếp tục tính toán.");

                return;
            }

            SetResultStateColors(
                hasRealRoots: true);

            string rootText =
                FormatNumber(
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
            if (!TryCalculateDistinctRoots(
                    a,
                    b,
                    delta,
                    out decimal squareRootDelta,
                    out decimal firstRoot,
                    out decimal secondRoot))
            {
                ShowError(
                    $"Nghiệm nằm ngoài phạm vi từ " +
                    $"{SupportedDecimalRangeText}. " +
                    "Ứng dụng không thể tiếp tục tính toán.");

                return;
            }

            SetResultStateColors(
                hasRealRoots: true);

            string squareRootText =
                FormatNumber(
                    squareRootDelta);

            string firstRootText =
                FormatNumber(
                    firstRoot);

            string secondRootText =
                FormatNumber(
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

            Step4BodyLabel.Text =
                $"x₁ = (−b + √Δ) / (2a)\n" +
                $"x₁ = (−({bText}) + {squareRootText}) / " +
                $"(2 × ({aText}))\n" +
                $"x₁ = {firstRootText}\n\n" +
                $"x₂ = (−b − √Δ) / (2a)\n" +
                $"x₂ = (−({bText}) − {squareRootText}) / " +
                $"(2 × ({aText}))\n" +
                $"x₂ = {secondRootText}";
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

        if (_parabolaGraphDrawable.PanByPixels(
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

    private void ShowParabolaGraph(
        decimal a,
        decimal b,
        decimal c)
    {
        _lastGraphPointer =
            null;

        _parabolaGraphDrawable.SetEquation(
            a,
            b,
            c);

        _parabolaGraphDrawable.ResetZoom();

        UpdateGraphPrecisionWarning();

        GraphBorder.IsVisible =
            true;

        UpdateGraphStatus();

        ParabolaGraphicsView.Invalidate();
    }

    private void UpdateGraphPrecisionWarning()
    {
        bool hasWarning =
            _parabolaGraphDrawable.HasReducedPrecision;

        GraphPrecisionWarningBorder.IsVisible =
            hasWarning;

        if (!hasWarning)
        {
            GraphPrecisionWarningLabel.Text =
                string.Empty;

            return;
        }

        GraphPrecisionWarningLabel.Text =
            $"Đồ thị dùng kiểu double, chỉ bảo đảm tối đa khoảng " +
            $"{ParabolaGraphDrawable.GuaranteedSignificantDigits} chữ số có nghĩa. " +
            $"Hệ số lớn nhất có " +
            $"{_parabolaGraphDrawable.MaximumCoefficientSignificantDigits} chữ số " +
            $"nên khi chuyển sang double, phần vượt quá " +
            $"{ParabolaGraphDrawable.GuaranteedSignificantDigits} chữ số có thể " +
            $"bị làm tròn và hình vẽ parabol có thể giảm độ chính xác. " +
            $"Kết quả giải phương trình vẫn được tính bằng decimal.";
    }

    private void OnGraphZoomInClicked(
        object? sender,
        EventArgs e)
    {
        if (!_parabolaGraphDrawable.HasEquation)
        {
            return;
        }

        _parabolaGraphDrawable.ZoomIn();

        UpdateGraphStatus();

        ParabolaGraphicsView.Invalidate();
    }

    private void OnGraphZoomOutClicked(
        object? sender,
        EventArgs e)
    {
        if (!_parabolaGraphDrawable.HasEquation)
        {
            return;
        }

        _parabolaGraphDrawable.ZoomOut();

        UpdateGraphStatus();

        ParabolaGraphicsView.Invalidate();
    }

    private void OnGraphResetZoomClicked(
        object? sender,
        EventArgs e)
    {
        if (!_parabolaGraphDrawable.HasEquation)
        {
            return;
        }

        _lastGraphPointer =
            null;

        _parabolaGraphDrawable.ResetZoom();

        UpdateGraphStatus();

        ParabolaGraphicsView.Invalidate();
    }

    private void UpdateGraphStatus()
    {
        int zoomPercent =
            _parabolaGraphDrawable.ZoomPercent;

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

    private static string BuildEquationText(
        decimal a,
        decimal b,
        decimal c)
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
        decimal coefficient,
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
        decimal coefficient,
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

        decimal absoluteCoefficient =
            Math.Abs(
                coefficient);

        if (absoluteCoefficient != 1 ||
            variable.Length == 0)
        {
            builder.Append(
                FormatNumber(
                    absoluteCoefficient));
        }

        builder.Append(
            variable);
    }

    private void ApplyCoefficientEntryDisplayValue(
        Entry entry,
        decimal value)
    {
        string standardText =
            FormatInputInteger(
                value);

        if (CountNumericDigits(
                standardText) <=
            ScientificDisplayDigitThreshold)
        {
            _coefficientScientificCodeValues.Remove(
                entry);

            SetEntryText(
                entry,
                standardText);

            return;
        }

        _coefficientScientificCodeValues[entry] =
            FormatScientificForCode(
                value);

        SetEntryText(
            entry,
            FormatScientificForDisplay(
                value));
    }

    private static string FormatInputInteger(
        decimal value)
    {
        return value.ToString(
            "#,##0",
            CultureInfo.InvariantCulture);
    }

    private static string FormatNumber(
        decimal value)
    {
        decimal rounded =
            decimal.Round(
                value,
                MaxResultDecimalPlaces,
                MidpointRounding.AwayFromZero);

        if (rounded == 0m)
        {
            rounded =
                0m;
        }

        string standardText =
            rounded.ToString(
                "#,##0.##########",
                CultureInfo.InvariantCulture);

        if (CountNumericDigits(
                standardText) >
            ScientificDisplayDigitThreshold)
        {
            return FormatScientificForDisplay(
                rounded);
        }

        return standardText.Replace(
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
        decimal value)
    {
        string code =
            FormatScientificForCode(
                value);

        int exponentSeparatorIndex =
            code.IndexOf(
                'e');

        string exactMantissaText =
            code[..exponentSeparatorIndex];

        int exponent =
            int.Parse(
                code[(exponentSeparatorIndex + 1)..],
                CultureInfo.InvariantCulture);

        decimal exactMantissa =
            decimal.Parse(
                exactMantissaText,
                NumberStyles.Float,
                CultureInfo.InvariantCulture);

        int mantissaDecimalPlaces =
            Math.Max(
                0,
                ScientificDisplaySignificantDigits - 1);

        decimal roundedMantissa =
            Math.Round(
                exactMantissa,
                mantissaDecimalPlaces,
                MidpointRounding.AwayFromZero);

        if (Math.Abs(
                roundedMantissa) >=
            10m)
        {
            roundedMantissa /=
                10m;

            exponent++;
        }

        bool wasRounded =
            roundedMantissa !=
            exactMantissa;

        string mantissaText =
            roundedMantissa.ToString(
                "0.###########",
                CultureInfo.InvariantCulture);

        string approximation =
            wasRounded
                ? "≈ "
                : string.Empty;

        if (mantissaText == "1")
        {
            return
                $"{approximation}10{ToSuperscript(exponent)}";
        }

        if (mantissaText == "-1")
        {
            return
                $"{approximation}−10{ToSuperscript(exponent)}";
        }

        mantissaText =
            mantissaText.Replace(
                "-",
                "−",
                StringComparison.Ordinal);

        return
            $"{approximation}{mantissaText} × " +
            $"10{ToSuperscript(exponent)}";
    }

    private static string FormatScientificForCode(
        decimal value)
    {
        if (value == 0m)
        {
            return "0e0";
        }

        string scientificText =
            value.ToString(
                "0.############################E+0",
                CultureInfo.InvariantCulture);

        int exponentIndex =
            scientificText.IndexOf(
                'E');

        string mantissa =
            scientificText[..exponentIndex];

        string exponent =
            scientificText[(exponentIndex + 1)..]
                .TrimStart('+');

        return
            $"{mantissa}e{exponent}";
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
        string text)
    {
        _isUpdatingText =
            true;

        try
        {
            entry.Text =
                text;

            entry.CursorPosition =
                text.Length;

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

        GraphPrecisionWarningBorder.IsVisible =
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

        GraphPrecisionWarningBorder.IsVisible =
            false;
    }

    private static bool IsWithinSupportedDecimalRange(
        decimal value)
    {
        return value >=
               MinSupportedValue &&
               value <=
               MaxSupportedValue;
    }

}