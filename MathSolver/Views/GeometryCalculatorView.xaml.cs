using MathSolver.Models;
using MathSolver.Numerics;
using MathSolver.Services;
using MathSolver.Views.Base;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace MathSolver.Views;

/// <summary>
/// Màn hình tính toán hình học được nhúng trong CalculationPage.
/// Công thức và hình minh họa được lấy trực tiếp từ GeometryFormulaItem.
/// Phần tính toán được tách riêng theo GeometryCategory.Plane/Solid.
/// </summary>
public partial class GeometryCalculatorView : LocalizedSolverView
{
    private const int MaxDecimalPlaces = 10;
    private const int ScientificDisplayDigitThreshold = 18;
    private const int ScientificDisplaySignificantDigits = 12;

    // Chiều cao tối thiểu của một card nhập liệu khi BindableLayout vừa tạo
    // children và WinUI chưa kịp arrange hàng mới. Giá trị này chỉ là fallback;
    // sau khi card được đo, chiều cao thực (kể cả label xuống dòng) vẫn được dùng.
    private const double GeometryInputCardMinimumHeight = 116d;

    // OctoDouble vẫn tính nội bộ với khoảng 127-128 chữ số có nghĩa,
    // nhưng giao diện chỉ hiển thị tối đa 10 chữ số sau dấu thập phân.
    private const int OctoDoubleScientificSignificantDigits =
        MaxDecimalPlaces + 1;

    private const string Int128RangeText =
        "−170,141,183,460,469,231,731,687,303,715,884,105,728 đến " +
        "170,141,183,460,469,231,731,687,303,715,884,105,727";

    // Thiết kế số nguyên:
    // - Kích thước người dùng nhập phải nằm trong phạm vi Int128.
    // - Sau khi parse thành công, hệ số mới được chuyển sang BigInteger.
    // - Chỉ kết quả tính toán nguyên dùng BigInteger để không tràn Int128.
    private const string DecimalRangeText =
        "−79,228,162,514,264,337,593,543,950,335 đến " +
        "79,228,162,514,264,337,593,543,950,335";

    // OctoDouble giữ khoảng 127-128 chữ số có nghĩa. Hằng số π và √3
    // được cung cấp trực tiếp bởi MathSolver.Numerics.OctoDouble.
    private static readonly OctoDouble OctoComparisonTolerance =
        OctoDouble.Parse("1e-100");

    private GeometryCategory _selectedCategory =
        GeometryCategory.Plane;

    private GeometryNumberType _selectedNumberType =
        GeometryNumberType.Integer;

    private GeometryFormulaItem? _selectedGeometry;

    private bool _isUpdatingEntryText;
    private bool _isUpdatingResponsiveLayout;
    private bool _isSynchronizingFormulaPreviewHeight;

    public ObservableCollection<GeometryFormulaItem> GeometryItems { get; } =
        [];

    public ObservableCollection<GeometryInputField> InputFields { get; } =
        [];

    public ObservableCollection<GeometryResultLine> Results { get; } =
        [];

    public GeometryFormulaItem? SelectedGeometry
    {
        get => _selectedGeometry;
        private set
        {
            if (ReferenceEquals(
                    _selectedGeometry,
                    value))
            {
                return;
            }

            _selectedGeometry =
                value;

            OnPropertyChanged();
        }
    }

    public GeometryCalculatorView()
    {
        InitializeComponent();

        BindingContext =
            this;

        InitializeLocalization();

        SelectCategory(
            GeometryCategory.Plane);

        SelectNumberType(
            GeometryNumberType.Integer,
            clearInputs: false);
    }

    protected override void OnSolverLoaded()
    {
        GeometryDiagramView.Invalidate();

        Dispatcher.Dispatch(
            () =>
            {
                UpdateMainResponsiveLayout();
                UpdateInputFieldWidths();
                SynchronizeFormulaPreviewHeight();

                Dispatcher.Dispatch(
                    () =>
                    {
                        UpdateMainResponsiveLayout();
                        UpdateInputFieldWidths();
                        SynchronizeFormulaPreviewHeight();
                    });
            });
    }

    protected override void RefreshLocalizedContent()
    {
        base.RefreshLocalizedContent();

        string? selectedId =
            SelectedGeometry?.Id;

        Dictionary<string, string> currentInputs =
            InputFields.ToDictionary(
                field => field.Key,
                field => field.RawText,
                StringComparer.Ordinal);

        SelectCategory(
            _selectedCategory,
            selectedId);

        foreach (GeometryInputField field
                 in InputFields)
        {
            if (!currentInputs.TryGetValue(
                    field.Key,
                    out string? rawText))
            {
                continue;
            }

            field.RawText =
                rawText;

            field.Text =
                rawText;
        }
    }

    private static string T(
        string source)
    {
        return LocalizationService.Translate(
            source);
    }

    private void OnPlaneGeometryClicked(
        object? sender,
        EventArgs e)
    {
        SelectCategory(
            GeometryCategory.Plane);
    }

    private void OnSolidGeometryClicked(
        object? sender,
        EventArgs e)
    {
        SelectCategory(
            GeometryCategory.Solid);
    }

    private void SelectCategory(
        GeometryCategory category,
        string? preferredGeometryId = null)
    {
        _selectedCategory =
            category;

        GeometryShapePicker.SelectedIndex =
            -1;

        GeometryItems.Clear();

        foreach (GeometryFormulaItem item
                 in GeometryFormulaCatalog.CreateByCategory(
                     category,
                     T))
        {
            GeometryItems.Add(
                item);
        }

        UpdateCategoryButtonStyles();

        int selectedIndex =
            0;

        if (!string.IsNullOrWhiteSpace(
                preferredGeometryId))
        {
            int matchingIndex =
                GeometryItems
                    .Select(
                        (item, index) =>
                            new
                            {
                                item.Id,
                                Index = index
                            })
                    .FirstOrDefault(
                        candidate =>
                            string.Equals(
                                candidate.Id,
                                preferredGeometryId,
                                StringComparison.OrdinalIgnoreCase))
                    ?.Index ??
                -1;

            if (matchingIndex >= 0)
            {
                selectedIndex =
                    matchingIndex;
            }
        }

        GeometryShapePicker.SelectedIndex =
            GeometryItems.Count == 0
                ? -1
                : selectedIndex;

        if (GeometryItems.Count > 0 &&
            GeometryShapePicker.SelectedIndex < 0)
        {
            GeometryShapePicker.SelectedIndex =
                0;
        }
    }

    private void UpdateCategoryButtonStyles()
    {
        ApplySelectionButtonStyle(
            PlaneGeometryButton,
            _selectedCategory ==
            GeometryCategory.Plane);

        ApplySelectionButtonStyle(
            SolidGeometryButton,
            _selectedCategory ==
            GeometryCategory.Solid);
    }

    private void OnGeometryShapeSelected(
        object? sender,
        EventArgs e)
    {
        if (GeometryShapePicker.SelectedItem
            is not GeometryFormulaItem selectedGeometry)
        {
            SelectedGeometry =
                null;

            GeometryDiagramView.Drawable =
                null;

            InputFields.Clear();

            ClearOutput();

            return;
        }

        SelectedGeometry =
            selectedGeometry;

        GeometryDiagramView.Drawable =
            selectedGeometry.Diagram;

        GeometryDiagramView.Invalidate();

        BuildInputFields(
            selectedGeometry.Id);

        ClearOutput();

        Dispatcher.Dispatch(
            () =>
            {
                UpdateInputFieldWidths();
                SynchronizeFormulaPreviewHeight();
                ScheduleInputFlexHeightUpdate();

                // BindableLayout tạo công thức, chú thích và các ô nhập
                // sau một lượt layout. Đo lại lần hai để kích thước cuối
                // của từng card và chiều cao FlexLayout đều chính xác.
                Dispatcher.Dispatch(
                    () =>
                    {
                        UpdateInputFieldWidths();
                        SynchronizeFormulaPreviewHeight();
                        ScheduleInputFlexHeightUpdate();
                    });
            });
    }

    private void BuildInputFields(
        string geometryId)
    {
        // Không giữ HeightRequest của hình trước. Nếu hình cũ chỉ có một hàng
        // nhưng hình mới cần hai hàng, chiều cao cũ sẽ cắt hàng dưới trước khi
        // WinUI kịp đo các card mới và tạo ra vòng lặp không thể tự mở rộng.
        GeometryInputFlexLayout.HeightRequest =
            -1d;

        InputFields.Clear();

        foreach (GeometryInputFieldDefinition definition
                 in GetInputDefinitions(
                     geometryId))
        {
            InputFields.Add(
                new GeometryInputField
                {
                    Key =
                        definition.Key,

                    Label =
                        T(
                            definition.Label),

                    Placeholder =
                        _selectedNumberType ==
                        GeometryNumberType.Integer
                            ? T("Ví dụ: 12")
                            : T("Ví dụ: 12.5")
                });
        }

        GeometryInputFlexLayout.InvalidateMeasure();
        ScheduleInputFlexHeightUpdate();
    }

    private static IReadOnlyList<GeometryInputFieldDefinition>
        GetInputDefinitions(
            string geometryId)
    {
        return geometryId switch
        {
            "square" =>
            [
                Field(
                    "a",
                    "Độ dài cạnh a")
            ],

            "rectangle" =>
            [
                Field(
                    "a",
                    "Chiều dài a"),
                Field(
                    "b",
                    "Chiều rộng b")
            ],

            "triangle" =>
            [
                Field(
                    "a",
                    "Độ dài đáy a"),
                Field(
                    "b",
                    "Độ dài cạnh b"),
                Field(
                    "c",
                    "Độ dài cạnh c"),
                Field(
                    "h",
                    "Chiều cao h")
            ],

            "right_triangle" =>
            [
                Field(
                    "a",
                    "Cạnh góc vuông a"),
                Field(
                    "b",
                    "Cạnh góc vuông b"),
                Field(
                    "c",
                    "Cạnh huyền c")
            ],

            "equilateral_triangle" =>
            [
                Field(
                    "a",
                    "Độ dài cạnh a")
            ],

            "circle" =>
            [
                Field(
                    "r",
                    "Bán kính r")
            ],

            "trapezoid" =>
            [
                Field(
                    "a",
                    "Đáy a"),
                Field(
                    "b",
                    "Đáy b"),
                Field(
                    "c",
                    "Cạnh bên c"),
                Field(
                    "d",
                    "Cạnh bên d"),
                Field(
                    "h",
                    "Chiều cao h")
            ],

            "isosceles_trapezoid" =>
            [
                Field(
                    "a",
                    "Đáy a"),
                Field(
                    "b",
                    "Đáy b"),
                Field(
                    "c",
                    "Cạnh bên c"),
                Field(
                    "h",
                    "Chiều cao h")
            ],

            "right_trapezoid" =>
            [
                Field(
                    "a",
                    "Đáy a"),
                Field(
                    "b",
                    "Đáy b"),
                Field(
                    "c",
                    "Cạnh bên c"),
                Field(
                    "h",
                    "Chiều cao h")
            ],

            "rhombus" =>
            [
                Field(
                    "a",
                    "Độ dài cạnh a"),
                Field(
                    "d1",
                    "Đường chéo d₁"),
                Field(
                    "d2",
                    "Đường chéo d₂"),
                Field(
                    "h",
                    "Chiều cao h")
            ],

            "parallelogram" =>
            [
                Field(
                    "a",
                    "Độ dài đáy a"),
                Field(
                    "b",
                    "Độ dài cạnh bên b"),
                Field(
                    "h",
                    "Chiều cao h")
            ],

            "cube" =>
            [
                Field(
                    "a",
                    "Độ dài cạnh a")
            ],

            "rectangular_prism" =>
            [
                Field(
                    "a",
                    "Chiều dài a"),
                Field(
                    "b",
                    "Chiều rộng b"),
                Field(
                    "h",
                    "Chiều cao h")
            ],

            "sphere" =>
            [
                Field(
                    "r",
                    "Bán kính r")
            ],

            "cylinder" =>
            [
                Field(
                    "r",
                    "Bán kính đáy r"),
                Field(
                    "h",
                    "Chiều cao h")
            ],

            "cone" =>
            [
                Field(
                    "r",
                    "Bán kính đáy r"),
                Field(
                    "h",
                    "Chiều cao h"),
                Field(
                    "l",
                    "Đường sinh l")
            ],

            _ =>
            []
        };
    }

    private static GeometryInputFieldDefinition Field(
        string key,
        string label)
    {
        return new GeometryInputFieldDefinition(
            key,
            label);
    }

    private void OnIntegerNumberClicked(
        object? sender,
        EventArgs e)
    {
        SelectNumberType(
            GeometryNumberType.Integer,
            clearInputs: true);
    }

    private void OnDecimalNumberClicked(
        object? sender,
        EventArgs e)
    {
        SelectNumberType(
            GeometryNumberType.Decimal,
            clearInputs: true);
    }

    private void SelectNumberType(
        GeometryNumberType numberType,
        bool clearInputs)
    {
        _selectedNumberType =
            numberType;

        ApplySelectionButtonStyle(
            IntegerNumberButton,
            numberType ==
            GeometryNumberType.Integer);

        ApplySelectionButtonStyle(
            DecimalNumberButton,
            numberType ==
            GeometryNumberType.Decimal);

        if (clearInputs &&
            SelectedGeometry is not null)
        {
            BuildInputFields(
                SelectedGeometry.Id);

            /*
             * BuildInputFields tạo lại toàn bộ BindableLayout children.
             * Ngay tại thời điểm này, các card mới vẫn đang dùng WidthRequest
             * mặc định từ DataTemplate và chưa được đo theo chiều rộng thật
             * của GeometryInputFlexLayout.
             *
             * Đợi hai lượt UI layout giống OnGeometryShapeSelected, rồi tính
             * lại WidthRequest và HeightRequest. Điều này giữ kích thước card
             * ổn định khi đổi qua lại giữa Số nguyên và Số thập phân.
             */
            Dispatcher.Dispatch(
                () =>
                {
                    UpdateInputFieldWidths();
                    ScheduleInputFlexHeightUpdate();

                    Dispatcher.Dispatch(
                        () =>
                        {
                            UpdateInputFieldWidths();
                            ScheduleInputFlexHeightUpdate();
                        });
                });
        }

        ClearOutput();
    }

    private static void ApplySelectionButtonStyle(
        Button button,
        bool selected)
    {
        button.SetDynamicResource(
            Button.BackgroundColorProperty,
            selected
                ? "PrimaryColor"
                : "SurfaceAltColor");

        button.SetDynamicResource(
            Button.TextColorProperty,
            selected
                ? "OnPrimaryColor"
                : "TextPrimaryColor");

        button.SetDynamicResource(
            Button.BorderColorProperty,
            selected
                ? "PrimaryColor"
                : "BorderColor");

        button.BorderWidth =
            selected
                ? 1.5d
                : 1d;
    }

    private void OnGeometryEntryTextChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        if (_isUpdatingEntryText ||
            sender is not Entry entry ||
            entry.BindingContext
            is not GeometryInputField field)
        {
            return;
        }

        string newText =
            e.NewTextValue ??
            string.Empty;

        if (!IsValidInputWhileTyping(
                newText))
        {
            string oldText =
                e.OldTextValue ??
                string.Empty;

            SetEntryText(
                entry,
                field,
                oldText,
                updateRawText: true);

            ShowError(
                _selectedNumberType ==
                GeometryNumberType.Integer
                    ? T(
                        "Số nguyên chỉ được chứa chữ số, một dấu âm ở đầu " +
                        "và tối đa 39 chữ số.")
                    : T(
                        "Số thập phân chỉ được chứa chữ số, một dấu âm ở đầu, " +
                        "một dấu chấm và tối đa 10 chữ số sau dấu chấm."));

            return;
        }

        string formattedText =
            IntegerInputFormatter.FormatWhileTyping(
                newText,
                allowDecimal:
                    _selectedNumberType ==
                    GeometryNumberType.Decimal);

        field.RawText =
            NormalizeRawInput(
                formattedText);

        if (!string.Equals(
                formattedText,
                newText,
                StringComparison.Ordinal))
        {
            int oldCursorPosition =
                Math.Clamp(
                    entry.CursorPosition,
                    0,
                    newText.Length);

            int logicalPosition =
                IntegerInputFormatter.CountLogicalCharacters(
                    newText,
                    oldCursorPosition);

            SetEntryText(
                entry,
                field,
                formattedText,
                updateRawText: false,
                cursorPosition:
                    IntegerInputFormatter.FindCursorPosition(
                        formattedText,
                        logicalPosition));
        }

        HideError();
        ClearResultsOnly();
    }

    private bool IsValidInputWhileTyping(
        string text)
    {
        string normalized =
            NormalizeRawInput(
                text);

        if (normalized.Length == 0 ||
            normalized is "-" or "−")
        {
            return true;
        }

        int startIndex =
            normalized[0] == '-'
                ? 1
                : 0;

        if (startIndex ==
            normalized.Length)
        {
            return true;
        }

        int digitCount =
            0;

        int decimalPointCount =
            0;

        int decimalPlaces =
            0;

        bool afterDecimalPoint =
            false;

        for (int index = startIndex;
             index < normalized.Length;
             index++)
        {
            char character =
                normalized[index];

            if (char.IsDigit(
                    character))
            {
                digitCount++;

                if (afterDecimalPoint)
                {
                    decimalPlaces++;
                }

                continue;
            }

            if (_selectedNumberType ==
                    GeometryNumberType.Decimal &&
                character == '.')
            {
                decimalPointCount++;

                if (decimalPointCount > 1)
                {
                    return false;
                }

                afterDecimalPoint =
                    true;

                continue;
            }

            return false;
        }

        if (_selectedNumberType ==
            GeometryNumberType.Integer)
        {
            return digitCount <=
                   39;
        }

        return digitCount <=
               29 &&
               decimalPlaces <=
               MaxDecimalPlaces;
    }

    private void OnGeometryEntryFocused(
        object? sender,
        FocusEventArgs e)
    {
        if (sender is not Entry entry ||
            entry.BindingContext
            is not GeometryInputField field)
        {
            return;
        }

        string rawText =
            NormalizeRawInput(
                field.RawText);

        if (string.IsNullOrEmpty(
                rawText))
        {
            return;
        }

        string formattedText =
            IntegerInputFormatter.FormatWhileTyping(
                rawText,
                allowDecimal:
                    _selectedNumberType ==
                    GeometryNumberType.Decimal);

        SetEntryText(
            entry,
            field,
            formattedText,
            updateRawText: false,
            cursorPosition:
                formattedText.Length);
    }

    private void OnGeometryEntryUnfocused(
        object? sender,
        FocusEventArgs e)
    {
        if (sender is not Entry entry ||
            entry.BindingContext
            is not GeometryInputField field)
        {
            return;
        }

        string rawText =
            NormalizeRawInput(
                field.RawText);

        if (string.IsNullOrWhiteSpace(
                rawText) ||
            rawText is "-" or "−")
        {
            return;
        }

        string formattedText;

        if (_selectedNumberType ==
            GeometryNumberType.Integer)
        {
            if (!Int128.TryParse(
                    rawText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out Int128 integerValue))
            {
                ShowError(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        T("{0} phải là số nguyên hợp lệ trong phạm vi Int128 từ {1}."),
                        field.Label,
                        Int128RangeText));

                return;
            }

            formattedText =
                FormatBigIntegerForDisplay(
                    (BigInteger)integerValue);
        }
        else
        {
            if (!decimal.TryParse(
                    rawText,
                    NumberStyles.AllowLeadingSign |
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out decimal decimalValue))
            {
                return;
            }

            formattedText =
                FormatDecimalForDisplay(
                    decimalValue);
        }

        SetEntryText(
            entry,
            field,
            formattedText,
            updateRawText: false);
    }

    private void SetEntryText(
        Entry entry,
        GeometryInputField field,
        string text,
        bool updateRawText,
        int? cursorPosition = null)
    {
        _isUpdatingEntryText =
            true;

        try
        {
            if (updateRawText)
            {
                field.RawText =
                    NormalizeRawInput(
                        text);
            }

            field.Text =
                text;

            entry.Text =
                text;

            if (cursorPosition.HasValue)
            {
                entry.CursorPosition =
                    Math.Clamp(
                        cursorPosition.Value,
                        0,
                        text.Length);
            }
        }
        finally
        {
            _isUpdatingEntryText =
                false;
        }
    }

    private static string NormalizeRawInput(
        string? text)
    {
        return (text ??
                string.Empty)
            .Trim()
            .Replace(
                "−",
                "-",
                StringComparison.Ordinal)
            .Replace(
                ",",
                string.Empty,
                StringComparison.Ordinal);
    }

    private void OnCalculateClicked(
        object? sender,
        EventArgs e)
    {
        HideError();
        ClearResultsOnly();

        if (SelectedGeometry is null)
        {
            ShowError(
                T(
                    "Vui lòng chọn một hình học."));

            return;
        }

        bool succeeded =
            _selectedNumberType ==
            GeometryNumberType.Integer
                ? CalculateIntegerGeometry(
                    SelectedGeometry)
                : CalculateDecimalGeometry(
                    SelectedGeometry);

        if (!succeeded)
        {
            return;
        }

        ResultShapeLabel.Text =
            SelectedGeometry.Name;

        ResultBorder.IsVisible =
            true;
    }

    private bool CalculateIntegerGeometry(
        GeometryFormulaItem geometry)
    {
        if (!TryReadIntegerInputs(
                out Dictionary<string, BigInteger> values))
        {
            return false;
        }

        bool usesDecimalResult =
            false;

        switch (geometry.Category)
        {
            case GeometryCategory.Plane:
                if (!CalculatePlaneInteger(
                        geometry.Id,
                        values,
                        ref usesDecimalResult))
                {
                    return false;
                }

                break;

            case GeometryCategory.Solid:
                if (!CalculateSolidInteger(
                        geometry.Id,
                        values,
                        ref usesDecimalResult))
                {
                    return false;
                }

                break;

            default:
                ShowError(
                    T(
                        "Loại hình học chưa được hỗ trợ."));

                return false;
        }

        CalculationExplanationLabel.Text =
            BuildExplanation();

        return Results.Count > 0;
    }

    private bool CalculatePlaneInteger(
        string geometryId,
        IReadOnlyDictionary<string, BigInteger> value,
        ref bool usesDecimalResult)
    {
        switch (geometryId)
        {
            case "square":
                {
                    BigInteger a = value["a"];

                    AddIntegerResult("Chu vi", "P = a × 4", a * 4);
                    AddIntegerResult("Diện tích", "S = a × a", a * a);
                    return true;
                }

            case "rectangle":
                {
                    BigInteger a = value["a"];
                    BigInteger b = value["b"];

                    AddIntegerResult("Chu vi", "P = (a + b) × 2", (a + b) * 2);
                    AddIntegerResult("Diện tích", "S = a × b", a * b);
                    return true;
                }

            case "triangle":
                {
                    BigInteger a = value["a"];
                    BigInteger b = value["b"];
                    BigInteger c = value["c"];
                    BigInteger h = value["h"];

                    if (!IsValidTriangle(a, b, c))
                    {
                        ShowError(T("Ba cạnh không tạo thành tam giác hợp lệ."));
                        return false;
                    }

                    AddIntegerResult("Chu vi", "P = a + b + c", a + b + c);
                    return AddRationalIntegerResult(
                        "Diện tích",
                        "S = (a × h) ÷ 2",
                        a * h,
                        2,
                        ref usesDecimalResult);
                }

            case "right_triangle":
                {
                    BigInteger a = value["a"];
                    BigInteger b = value["b"];
                    BigInteger c = value["c"];

                    if (a * a + b * b != c * c)
                    {
                        ShowError(T("Ba cạnh không thỏa mãn định lý Pythagore a² + b² = c²."));
                        return false;
                    }

                    AddIntegerResult("Chu vi", "P = a + b + c", a + b + c);
                    return AddRationalIntegerResult(
                        "Diện tích",
                        "S = (a × b) ÷ 2",
                        a * b,
                        2,
                        ref usesDecimalResult);
                }

            case "equilateral_triangle":
                {
                    BigInteger integerA = value["a"];
                    OctoDouble a = OctoDouble.FromBigInteger(integerA);

                    AddIntegerResult("Chu vi", "P = a × 3", integerA * 3);
                    AddOctoDoubleResult(
                        "Diện tích",
                        "S = (a² × √3) ÷ 4",
                        a * a * OctoDouble.SqrtThree / 4d);

                    usesDecimalResult = true;
                    return true;
                }

            case "circle":
                {
                    OctoDouble r = OctoDouble.FromBigInteger(value["r"]);

                    AddOctoDoubleResult(
                        "Chu vi",
                        "C = 2 × π × r",
                        2d * OctoDouble.Pi * r);

                    AddOctoDoubleResult(
                        "Diện tích",
                        "S = π × r²",
                        OctoDouble.Pi * r * r);

                    usesDecimalResult = true;
                    return true;
                }

            case "trapezoid":
                {
                    BigInteger a = value["a"];
                    BigInteger b = value["b"];
                    BigInteger c = value["c"];
                    BigInteger d = value["d"];
                    BigInteger h = value["h"];

                    AddIntegerResult("Chu vi", "P = a + b + c + d", a + b + c + d);
                    return AddRationalIntegerResult(
                        "Diện tích",
                        "S = ((a + b) × h) ÷ 2",
                        (a + b) * h,
                        2,
                        ref usesDecimalResult);
                }

            case "isosceles_trapezoid":
                {
                    BigInteger a = value["a"];
                    BigInteger b = value["b"];
                    BigInteger c = value["c"];
                    BigInteger h = value["h"];

                    AddIntegerResult("Chu vi", "P = a + b + 2c", a + b + 2 * c);
                    return AddRationalIntegerResult(
                        "Diện tích",
                        "S = ((a + b) × h) ÷ 2",
                        (a + b) * h,
                        2,
                        ref usesDecimalResult);
                }

            case "right_trapezoid":
                {
                    BigInteger a = value["a"];
                    BigInteger b = value["b"];
                    BigInteger c = value["c"];
                    BigInteger h = value["h"];

                    AddIntegerResult("Chu vi", "P = a + b + c + h", a + b + c + h);
                    return AddRationalIntegerResult(
                        "Diện tích",
                        "S = ((a + b) × h) ÷ 2",
                        (a + b) * h,
                        2,
                        ref usesDecimalResult);
                }

            case "rhombus":
                {
                    BigInteger a = value["a"];
                    BigInteger d1 = value["d1"];
                    BigInteger d2 = value["d2"];
                    BigInteger h = value["h"];

                    AddIntegerResult("Chu vi", "P = a × 4", a * 4);

                    if (!AddRationalIntegerResult(
                            "Diện tích theo đường chéo",
                            "S = (d₁ × d₂) ÷ 2",
                            d1 * d2,
                            2,
                            ref usesDecimalResult))
                    {
                        return false;
                    }

                    AddIntegerResult(
                        "Diện tích theo đáy và chiều cao",
                        "S = a × h",
                        a * h);

                    return true;
                }

            case "parallelogram":
                {
                    BigInteger a = value["a"];
                    BigInteger b = value["b"];
                    BigInteger h = value["h"];

                    AddIntegerResult("Chu vi", "P = (a + b) × 2", (a + b) * 2);
                    AddIntegerResult("Diện tích", "S = a × h", a * h);
                    return true;
                }

            default:
                ShowError(T("Hình học mặt phẳng này chưa có bộ tính toán."));
                return false;
        }
    }

    private bool CalculateSolidInteger(
        string geometryId,
        IReadOnlyDictionary<string, BigInteger> value,
        ref bool usesDecimalResult)
    {
        switch (geometryId)
        {
            case "cube":
                {
                    BigInteger a = value["a"];
                    BigInteger square = a * a;

                    AddIntegerResult("Diện tích xung quanh", "Sxq = 4 × a²", 4 * square);
                    AddIntegerResult("Diện tích toàn phần", "Stp = 6 × a²", 6 * square);
                    AddIntegerResult("Thể tích", "V = a³", square * a);
                    return true;
                }

            case "rectangular_prism":
                {
                    BigInteger a = value["a"];
                    BigInteger b = value["b"];
                    BigInteger h = value["h"];

                    AddIntegerResult(
                        "Diện tích xung quanh",
                        "Sxq = 2 × (a + b) × h",
                        2 * (a + b) * h);

                    AddIntegerResult(
                        "Diện tích toàn phần",
                        "Stp = 2 × (a × b + a × h + b × h)",
                        2 * (a * b + a * h + b * h));

                    AddIntegerResult("Thể tích", "V = a × b × h", a * b * h);
                    return true;
                }

            case "sphere":
                {
                    OctoDouble r = OctoDouble.FromBigInteger(value["r"]);
                    OctoDouble square = r * r;

                    AddOctoDoubleResult(
                        "Diện tích mặt cầu",
                        "S = 4 × π × r²",
                        4d * OctoDouble.Pi * square);

                    AddOctoDoubleResult(
                        "Thể tích",
                        "V = (4 × π × r³) ÷ 3",
                        4d * OctoDouble.Pi * square * r / 3d);

                    usesDecimalResult = true;
                    return true;
                }

            case "cylinder":
                {
                    OctoDouble r = OctoDouble.FromBigInteger(value["r"]);
                    OctoDouble h = OctoDouble.FromBigInteger(value["h"]);
                    OctoDouble baseArea = OctoDouble.Pi * r * r;

                    AddOctoDoubleResult("Diện tích đáy", "Sđ = π × r²", baseArea);
                    AddOctoDoubleResult(
                        "Diện tích xung quanh",
                        "Sxq = 2 × π × r × h",
                        2d * OctoDouble.Pi * r * h);
                    AddOctoDoubleResult(
                        "Diện tích toàn phần",
                        "Stp = 2 × π × r × (r + h)",
                        2d * OctoDouble.Pi * r * (r + h));
                    AddOctoDoubleResult("Thể tích", "V = π × r² × h", baseArea * h);

                    usesDecimalResult = true;
                    return true;
                }

            case "cone":
                {
                    OctoDouble r = OctoDouble.FromBigInteger(value["r"]);
                    OctoDouble h = OctoDouble.FromBigInteger(value["h"]);
                    OctoDouble l = OctoDouble.FromBigInteger(value["l"]);
                    OctoDouble baseArea = OctoDouble.Pi * r * r;

                    AddOctoDoubleResult("Diện tích đáy", "Sđ = π × r²", baseArea);
                    AddOctoDoubleResult(
                        "Diện tích xung quanh",
                        "Sxq = π × r × l",
                        OctoDouble.Pi * r * l);
                    AddOctoDoubleResult(
                        "Diện tích toàn phần",
                        "Stp = π × r × (r + l)",
                        OctoDouble.Pi * r * (r + l));
                    AddOctoDoubleResult(
                        "Thể tích",
                        "V = (π × r² × h) ÷ 3",
                        baseArea * h / 3d);

                    usesDecimalResult = true;
                    return true;
                }

            default:
                ShowError(T("Hình học không gian này chưa có bộ tính toán."));
                return false;
        }
    }

    private bool CalculateDecimalGeometry(
        GeometryFormulaItem geometry)
    {
        /*
         * Ranh giới kiểu dữ liệu của chế độ Số thập phân:
         *
         * 1. Người dùng vẫn nhập và được kiểm tra bằng Decimal.
         * 2. Ngay sau khi toàn bộ kích thước hợp lệ, chuyển chúng sang
         *    OctoDouble đúng một lần.
         * 3. Từ đây trở đi, mọi công thức và kết quả đều dùng OctoDouble.
         *
         * Vì vậy kết quả không còn bị giới hạn bởi phạm vi Decimal,
         * trong khi quy tắc nhập liệu Decimal vẫn được giữ nguyên.
         */
        if (!TryReadDecimalInputs(
                out Dictionary<string, decimal> decimalInputs))
        {
            return false;
        }

        Dictionary<string, OctoDouble> values =
            ConvertDecimalInputsToOctoDouble(
                decimalInputs);

        switch (geometry.Category)
        {
            case GeometryCategory.Plane:
                if (!CalculatePlaneDecimal(
                        geometry.Id,
                        values))
                {
                    return false;
                }

                break;

            case GeometryCategory.Solid:
                if (!CalculateSolidDecimal(
                        geometry.Id,
                        values))
                {
                    return false;
                }

                break;

            default:
                ShowError(T("Loại hình học chưa được hỗ trợ."));
                return false;
        }

        CalculationExplanationLabel.Text =
            BuildExplanation();

        return Results.Count > 0;
    }

    private static Dictionary<string, OctoDouble>
        ConvertDecimalInputsToOctoDouble(
            IReadOnlyDictionary<string, decimal> decimalInputs)
    {
        var values =
            new Dictionary<string, OctoDouble>(
                decimalInputs.Count,
                StringComparer.Ordinal);

        foreach (KeyValuePair<string, decimal> input
                 in decimalInputs)
        {
            values[input.Key] =
                OctoDouble.FromDecimal(
                    input.Value);
        }

        return values;
    }

    private bool CalculatePlaneDecimal(
        string geometryId,
        IReadOnlyDictionary<string, OctoDouble> value)
    {
        switch (geometryId)
        {
            case "square":
                {
                    OctoDouble a = value["a"];
                    AddOctoDoubleResult("Chu vi", "P = a × 4", a * 4d);
                    AddOctoDoubleResult("Diện tích", "S = a × a", a * a);
                    return true;
                }

            case "rectangle":
                {
                    OctoDouble a = value["a"];
                    OctoDouble b = value["b"];
                    AddOctoDoubleResult("Chu vi", "P = (a + b) × 2", (a + b) * 2d);
                    AddOctoDoubleResult("Diện tích", "S = a × b", a * b);
                    return true;
                }

            case "triangle":
                {
                    OctoDouble a = value["a"];
                    OctoDouble b = value["b"];
                    OctoDouble c = value["c"];
                    OctoDouble h = value["h"];

                    if (!IsValidTriangle(a, b, c))
                    {
                        ShowError(T("Ba cạnh không tạo thành tam giác hợp lệ."));
                        return false;
                    }

                    AddOctoDoubleResult("Chu vi", "P = a + b + c", a + b + c);
                    AddOctoDoubleResult("Diện tích", "S = (a × h) ÷ 2", a * h / 2d);
                    return true;
                }

            case "right_triangle":
                {
                    OctoDouble a = value["a"];
                    OctoDouble b = value["b"];
                    OctoDouble c = value["c"];
                    OctoDouble left = a * a + b * b;
                    OctoDouble right = c * c;

                    if (!ApproximatelyEqual(left, right))
                    {
                        ShowError(T("Ba cạnh không thỏa mãn định lý Pythagore a² + b² = c²."));
                        return false;
                    }

                    AddOctoDoubleResult("Chu vi", "P = a + b + c", a + b + c);
                    AddOctoDoubleResult("Diện tích", "S = (a × b) ÷ 2", a * b / 2d);
                    return true;
                }

            case "equilateral_triangle":
                {
                    OctoDouble a = value["a"];
                    AddOctoDoubleResult("Chu vi", "P = a × 3", a * 3d);
                    AddOctoDoubleResult(
                        "Diện tích",
                        "S = (a² × √3) ÷ 4",
                        a * a * OctoDouble.SqrtThree / 4d);
                    return true;
                }

            case "circle":
                {
                    OctoDouble r = value["r"];
                    AddOctoDoubleResult("Chu vi", "C = 2 × π × r", 2d * OctoDouble.Pi * r);
                    AddOctoDoubleResult("Diện tích", "S = π × r²", OctoDouble.Pi * r * r);
                    return true;
                }

            case "trapezoid":
                {
                    OctoDouble a = value["a"];
                    OctoDouble b = value["b"];
                    OctoDouble c = value["c"];
                    OctoDouble d = value["d"];
                    OctoDouble h = value["h"];

                    AddOctoDoubleResult("Chu vi", "P = a + b + c + d", a + b + c + d);
                    AddOctoDoubleResult(
                        "Diện tích",
                        "S = ((a + b) × h) ÷ 2",
                        (a + b) * h / 2d);
                    return true;
                }

            case "isosceles_trapezoid":
                {
                    OctoDouble a = value["a"];
                    OctoDouble b = value["b"];
                    OctoDouble c = value["c"];
                    OctoDouble h = value["h"];

                    AddOctoDoubleResult("Chu vi", "P = a + b + 2c", a + b + 2d * c);
                    AddOctoDoubleResult(
                        "Diện tích",
                        "S = ((a + b) × h) ÷ 2",
                        (a + b) * h / 2d);
                    return true;
                }

            case "right_trapezoid":
                {
                    OctoDouble a = value["a"];
                    OctoDouble b = value["b"];
                    OctoDouble c = value["c"];
                    OctoDouble h = value["h"];

                    AddOctoDoubleResult("Chu vi", "P = a + b + c + h", a + b + c + h);
                    AddOctoDoubleResult(
                        "Diện tích",
                        "S = ((a + b) × h) ÷ 2",
                        (a + b) * h / 2d);
                    return true;
                }

            case "rhombus":
                {
                    OctoDouble a = value["a"];
                    OctoDouble d1 = value["d1"];
                    OctoDouble d2 = value["d2"];
                    OctoDouble h = value["h"];

                    AddOctoDoubleResult("Chu vi", "P = a × 4", a * 4d);
                    AddOctoDoubleResult(
                        "Diện tích theo đường chéo",
                        "S = (d₁ × d₂) ÷ 2",
                        d1 * d2 / 2d);
                    AddOctoDoubleResult(
                        "Diện tích theo đáy và chiều cao",
                        "S = a × h",
                        a * h);
                    return true;
                }

            case "parallelogram":
                {
                    OctoDouble a = value["a"];
                    OctoDouble b = value["b"];
                    OctoDouble h = value["h"];
                    AddOctoDoubleResult("Chu vi", "P = (a + b) × 2", (a + b) * 2d);
                    AddOctoDoubleResult("Diện tích", "S = a × h", a * h);
                    return true;
                }

            default:
                ShowError(T("Hình học mặt phẳng này chưa có bộ tính toán."));
                return false;
        }
    }

    private bool CalculateSolidDecimal(
        string geometryId,
        IReadOnlyDictionary<string, OctoDouble> value)
    {
        switch (geometryId)
        {
            case "cube":
                {
                    OctoDouble a = value["a"];
                    OctoDouble square = a * a;
                    AddOctoDoubleResult("Diện tích xung quanh", "Sxq = 4 × a²", 4d * square);
                    AddOctoDoubleResult("Diện tích toàn phần", "Stp = 6 × a²", 6d * square);
                    AddOctoDoubleResult("Thể tích", "V = a³", square * a);
                    return true;
                }

            case "rectangular_prism":
                {
                    OctoDouble a = value["a"];
                    OctoDouble b = value["b"];
                    OctoDouble h = value["h"];

                    AddOctoDoubleResult(
                        "Diện tích xung quanh",
                        "Sxq = 2 × (a + b) × h",
                        2d * (a + b) * h);
                    AddOctoDoubleResult(
                        "Diện tích toàn phần",
                        "Stp = 2 × (a × b + a × h + b × h)",
                        2d * (a * b + a * h + b * h));
                    AddOctoDoubleResult("Thể tích", "V = a × b × h", a * b * h);
                    return true;
                }

            case "sphere":
                {
                    OctoDouble r = value["r"];
                    OctoDouble square = r * r;
                    AddOctoDoubleResult("Diện tích mặt cầu", "S = 4 × π × r²", 4d * OctoDouble.Pi * square);
                    AddOctoDoubleResult(
                        "Thể tích",
                        "V = (4 × π × r³) ÷ 3",
                        4d * OctoDouble.Pi * square * r / 3d);
                    return true;
                }

            case "cylinder":
                {
                    OctoDouble r = value["r"];
                    OctoDouble h = value["h"];
                    OctoDouble baseArea = OctoDouble.Pi * r * r;
                    AddOctoDoubleResult("Diện tích đáy", "Sđ = π × r²", baseArea);
                    AddOctoDoubleResult("Diện tích xung quanh", "Sxq = 2 × π × r × h", 2d * OctoDouble.Pi * r * h);
                    AddOctoDoubleResult("Diện tích toàn phần", "Stp = 2 × π × r × (r + h)", 2d * OctoDouble.Pi * r * (r + h));
                    AddOctoDoubleResult("Thể tích", "V = π × r² × h", baseArea * h);
                    return true;
                }

            case "cone":
                {
                    OctoDouble r = value["r"];
                    OctoDouble h = value["h"];
                    OctoDouble l = value["l"];
                    OctoDouble baseArea = OctoDouble.Pi * r * r;
                    AddOctoDoubleResult("Diện tích đáy", "Sđ = π × r²", baseArea);
                    AddOctoDoubleResult("Diện tích xung quanh", "Sxq = π × r × l", OctoDouble.Pi * r * l);
                    AddOctoDoubleResult("Diện tích toàn phần", "Stp = π × r × (r + l)", OctoDouble.Pi * r * (r + l));
                    AddOctoDoubleResult("Thể tích", "V = (π × r² × h) ÷ 3", baseArea * h / 3d);
                    return true;
                }

            default:
                ShowError(T("Hình học không gian này chưa có bộ tính toán."));
                return false;
        }
    }

    private bool TryReadIntegerInputs(
        out Dictionary<string, BigInteger> values)
    {
        values =
            new Dictionary<string, BigInteger>(
                StringComparer.Ordinal);

        foreach (GeometryInputField field
                 in InputFields)
        {
            string rawText =
                NormalizeRawInput(
                    field.RawText);

            if (string.IsNullOrWhiteSpace(
                    rawText))
            {
                ShowError(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        T("Vui lòng nhập {0}."),
                        field.Label));

                return false;
            }

            if (!Int128.TryParse(
                    rawText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out Int128 parsedValue))
            {
                ShowError(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        T("{0} phải là số nguyên hợp lệ trong phạm vi Int128 từ {1}."),
                        field.Label,
                        Int128RangeText));

                return false;
            }

            if (parsedValue <=
                Int128.Zero)
            {
                ShowError(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        T("{0} phải lớn hơn 0."),
                        field.Label));

                return false;
            }

            // Hệ số vẫn bị giới hạn bởi Int128. Chỉ sau khi đã
            // hợp lệ mới nâng sang BigInteger để công thức và kết quả
            // nguyên không bị tràn phạm vi Int128.
            values[field.Key] =
                (BigInteger)parsedValue;
        }

        return true;
    }

    // Chỉ phần nhập kích thước dùng Decimal. Thông báo phạm vi Decimal
    // ở đây chỉ dành cho dữ liệu đầu vào, không áp dụng cho kết quả.
    private bool TryReadDecimalInputs(
        out Dictionary<string, decimal> values)
    {
        values =
            new Dictionary<string, decimal>(
                StringComparer.Ordinal);

        foreach (GeometryInputField field
                 in InputFields)
        {
            string rawText =
                NormalizeRawInput(
                    field.RawText);

            if (string.IsNullOrWhiteSpace(
                    rawText))
            {
                ShowError(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        T("Vui lòng nhập {0}."),
                        field.Label));

                return false;
            }

            if (CountDecimalPlaces(
                    rawText) >
                MaxDecimalPlaces)
            {
                ShowError(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        T("{0} chỉ được có tối đa 10 chữ số sau dấu chấm."),
                        field.Label));

                return false;
            }

            if (!decimal.TryParse(
                    rawText,
                    NumberStyles.AllowLeadingSign |
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out decimal parsedValue))
            {
                ShowError(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        T("{0} phải là số thập phân hợp lệ trong phạm vi Decimal từ {1}."),
                        field.Label,
                        DecimalRangeText));

                return false;
            }

            if (parsedValue <=
                0m)
            {
                ShowError(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        T("{0} phải lớn hơn 0."),
                        field.Label));

                return false;
            }

            values[field.Key] =
                parsedValue;
        }

        return true;
    }

    private static int CountDecimalPlaces(
        string text)
    {
        int decimalPointIndex =
            text.IndexOf(
                '.');

        return decimalPointIndex < 0
            ? 0
            : text.Length -
              decimalPointIndex -
              1;
    }

    // Kết quả nguyên được giữ bằng BigInteger. Hàm này không kiểm tra
    // giới hạn Int128 vì giới hạn đó chỉ áp dụng cho hệ số đầu vào.
    private void AddIntegerResult(
        string title,
        string formula,
        BigInteger value)
    {
        Results.Add(
            new GeometryResultLine
            {
                Title =
                    T(title),

                Formula =
                    formula,

                Value =
                    FormatBigIntegerForDisplay(
                        value),

                IsDecimal =
                    false
            });
    }

    private bool AddRationalIntegerResult(
        string title,
        string formula,
        BigInteger numerator,
        int denominator,
        ref bool usesDecimalResult)
    {
        BigInteger quotient =
            BigInteger.DivRem(
                numerator,
                denominator,
                out BigInteger remainder);

        if (remainder.IsZero)
        {
            AddIntegerResult(title, formula, quotient);
            return true;
        }

        AddOctoDoubleResult(
            title,
            formula,
            OctoDouble.FromRational(numerator, denominator));

        usesDecimalResult = true;
        return true;
    }

    private void AddOctoDoubleResult(
        string title,
        string formula,
        OctoDouble value)
    {
        Results.Add(
            new GeometryResultLine
            {
                Title = T(title),
                Formula = formula,
                Value = FormatOctoDoubleForDisplay(value),
                IsDecimal = true
            });
    }

    private string BuildExplanation()
    {
        var builder =
            new StringBuilder();

        for (int index = 0;
             index < Results.Count;
             index++)
        {
            GeometryResultLine result =
                Results[index];

            if (index > 0)
            {
                builder.AppendLine();
            }

            builder.Append(
                result.Title);

            builder.Append(
                ": ");

            builder.Append(
                result.Formula);

            builder.Append(
                " = ");

            builder.Append(
                result.Value);
        }

        return builder.ToString();
    }

    private static string FormatOctoDoubleForDisplay(
        OctoDouble value)
    {
        /*
         * OctoDouble chỉ được làm tròn ở bước hiển thị. Mọi phép tính trước
         * đó vẫn giữ đầy đủ khoảng 127-128 chữ số có nghĩa.
         *
         * Dạng thường:
         * - Số chữ số có nghĩa = số chữ số phần nguyên + 10.
         * - Vì vậy kết quả có tối đa 10 chữ số sau dấu thập phân.
         *
         * Dạng khoa học:
         * - Một chữ số trước dấu chấm và tối đa 10 chữ số phía sau.
         * - Tổng cộng tối đa 11 chữ số có nghĩa ở phần định trị.
         */
        int displaySignificantDigits =
            GetOctoDoubleDisplaySignificantDigits(
                value);

        string text =
            value.ToGeneralString(
                displaySignificantDigits,
                ScientificDisplayDigitThreshold,
                -MaxDecimalPlaces);

        int exponentMarker =
            text.IndexOfAny(new[] { 'e', 'E' });

        if (exponentMarker >= 0)
        {
            string mantissa =
                text[..exponentMarker]
                    .Replace(
                        "-",
                        "−",
                        StringComparison.Ordinal);

            if (int.TryParse(
                    text[(exponentMarker + 1)..],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int exponent))
            {
                return $"{mantissa} × 10{ToSuperscript(exponent)}";
            }
        }

        bool negative =
            text.StartsWith(
                "-",
                StringComparison.Ordinal);

        string unsignedText =
            negative
                ? text[1..]
                : text;

        int decimalPoint =
            unsignedText.IndexOf(
                ".",
                StringComparison.Ordinal);

        string integerPart =
            decimalPoint >= 0
                ? unsignedText[..decimalPoint]
                : unsignedText;

        string fractionPart =
            decimalPoint >= 0
                ? unsignedText[decimalPoint..]
                : string.Empty;

        if (integerPart.Length <=
            ScientificDisplayDigitThreshold)
        {
            integerPart =
                AddThousandsSeparators(
                    integerPart);
        }

        return (negative ? "−" : string.Empty) +
               integerPart +
               fractionPart;
    }

    private static int GetOctoDoubleDisplaySignificantDigits(
        OctoDouble value)
    {
        if (!value.IsFinite ||
            value.IsZero)
        {
            return 1;
        }

        double approximateValue =
            Math.Abs(
                value.ToDouble());

        if (approximateValue == 0d ||
            double.IsNaN(
                approximateValue) ||
            double.IsInfinity(
                approximateValue))
        {
            return OctoDoubleScientificSignificantDigits;
        }

        int exponent =
            (int)Math.Floor(
                Math.Log10(
                    approximateValue));

        if (exponent >=
                ScientificDisplayDigitThreshold ||
            exponent <=
                -MaxDecimalPlaces)
        {
            return OctoDoubleScientificSignificantDigits;
        }

        /*
         * Ví dụ:
         * 12.345... có exponent = 1:
         * 2 chữ số phần nguyên + 10 chữ số thập phân = 12 chữ số có nghĩa.
         *
         * 0.001234... có exponent = -3:
         * cần 8 chữ số có nghĩa để làm tròn đúng tại chữ số thập phân thứ 10.
         */
        return Math.Clamp(
            exponent +
            1 +
            MaxDecimalPlaces,
            1,
            OctoDouble.SignificantDigits);
    }

    private static string AddThousandsSeparators(
        string digits)
    {
        if (digits.Length <= 3)
        {
            return digits;
        }

        int firstGroupLength =
            digits.Length % 3;

        if (firstGroupLength == 0)
        {
            firstGroupLength = 3;
        }

        var builder =
            new StringBuilder(
                digits.Length +
                digits.Length / 3);

        builder.Append(
            digits.AsSpan(
                0,
                firstGroupLength));

        for (int index = firstGroupLength;
             index < digits.Length;
             index += 3)
        {
            builder.Append(',');
            builder.Append(
                digits.AsSpan(
                    index,
                    3));
        }

        return builder.ToString();
    }

    private static bool IsValidTriangle(
        BigInteger a,
        BigInteger b,
        BigInteger c)
    {
        return a + b > c &&
               a + c > b &&
               b + c > a;
    }

    private static bool IsValidTriangle(
        OctoDouble a,
        OctoDouble b,
        OctoDouble c)
    {
        return a + b > c &&
               a + c > b &&
               b + c > a;
    }

    private static bool ApproximatelyEqual(
        OctoDouble first,
        OctoDouble second)
    {
        OctoDouble difference =
            OctoDouble.Abs(
                first - second);

        OctoDouble magnitude =
            OctoDouble.Max(
                OctoDouble.Abs(first),
                OctoDouble.Abs(second));

        OctoDouble tolerance =
            OctoDouble.Max(
                OctoComparisonTolerance,
                magnitude * OctoComparisonTolerance);

        return difference <= tolerance;
    }

    private static string FormatBigIntegerForDisplay(
        BigInteger value)
    {
        string digits =
            BigInteger.Abs(
                    value)
                .ToString(
                    CultureInfo.InvariantCulture);

        if (digits.Length <=
            ScientificDisplayDigitThreshold)
        {
            return value.ToString(
                "N0",
                CultureInfo.InvariantCulture);
        }

        string sign =
            value.Sign < 0
                ? "−"
                : string.Empty;

        int exponent =
            digits.Length -
            1;

        int keptDigits =
            Math.Min(
                ScientificDisplaySignificantDigits,
                digits.Length);

        string mantissaDigits =
            digits[..keptDigits];

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

        return $"{sign}{mantissa} × 10{ToSuperscript(exponent)}";
    }

    private static string FormatDecimalForDisplay(
        decimal value)
    {
        decimal rounded =
            decimal.Round(
                value,
                MaxDecimalPlaces,
                MidpointRounding.AwayFromZero);

        if (rounded ==
            0m)
        {
            rounded =
                0m;
        }

        string plainDigits =
            decimal.Abs(
                    rounded)
                .ToString(
                    "0.############################",
                    CultureInfo.InvariantCulture)
                .Replace(
                    ".",
                    string.Empty,
                    StringComparison.Ordinal)
                .TrimStart(
                    '0');

        if (plainDigits.Length >
            ScientificDisplayDigitThreshold)
        {
            return FormatDecimalScientific(
                rounded);
        }

        return rounded.ToString(
            "#,0.##########",
            CultureInfo.InvariantCulture);
    }

    private static string FormatDecimalScientific(
        decimal value)
    {
        if (value ==
            0m)
        {
            return "0";
        }

        string sign =
            value < 0m
                ? "−"
                : string.Empty;

        decimal absoluteValue =
            decimal.Abs(
                value);

        int exponent =
            0;

        while (absoluteValue >=
               10m)
        {
            absoluteValue /=
                10m;

            exponent++;
        }

        while (absoluteValue <
               1m)
        {
            absoluteValue *=
                10m;

            exponent--;
        }

        decimal mantissa =
            decimal.Round(
                absoluteValue,
                ScientificDisplaySignificantDigits -
                1,
                MidpointRounding.AwayFromZero);

        if (mantissa >=
            10m)
        {
            mantissa /=
                10m;

            exponent++;
        }

        string mantissaText =
            mantissa.ToString(
                    "0.###########",
                    CultureInfo.InvariantCulture)
                .TrimEnd(
                    '0')
                .TrimEnd(
                    '.');

        return $"{sign}{mantissaText} × 10{ToSuperscript(exponent)}";
    }

    private static string ToSuperscript(
        int value)
    {
        const string superscriptDigits =
            "⁰¹²³⁴⁵⁶⁷⁸⁹";

        string normalDigits =
            Math.Abs(
                    value)
                .ToString(
                    CultureInfo.InvariantCulture);

        var builder =
            new StringBuilder();

        if (value < 0)
        {
            builder.Append(
                '⁻');
        }

        foreach (char digit
                 in normalDigits)
        {
            builder.Append(
                superscriptDigits[
                    digit -
                    '0']);
        }

        return builder.ToString();
    }

    private void OnClearClicked(
        object? sender,
        EventArgs e)
    {
        _isUpdatingEntryText =
            true;

        try
        {
            foreach (GeometryInputField field
                     in InputFields)
            {
                field.RawText =
                    string.Empty;

                field.Text =
                    string.Empty;
            }
        }
        finally
        {
            _isUpdatingEntryText =
                false;
        }

        ClearOutput();
    }

    private void ClearOutput()
    {
        HideError();
        ClearResultsOnly();
    }

    private void ClearResultsOnly()
    {
        Results.Clear();

        ResultShapeLabel.Text =
            string.Empty;

        CalculationExplanationLabel.Text =
            string.Empty;

        ResultBorder.IsVisible =
            false;
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
    }

    private void HideError()
    {
        ErrorLabel.Text =
            string.Empty;

        ErrorBorder.IsVisible =
            false;
    }


    private void OnGeometryMainResponsiveGridSizeChanged(
        object? sender,
        EventArgs e)
    {
        UpdateMainResponsiveLayout();
    }

    private void UpdateMainResponsiveLayout()
    {
        if (_isUpdatingResponsiveLayout)
        {
            return;
        }

        double availableWidth =
            GeometryMainResponsiveGrid.Width;

        if (availableWidth <=
            0d)
        {
            return;
        }

        _isUpdatingResponsiveLayout =
            true;

        try
        {
            bool useTwoMainColumns =
                availableWidth >=
                900d;

            GeometryMainResponsiveGrid
                .ColumnDefinitions
                .Clear();

            GeometryMainResponsiveGrid
                .RowDefinitions
                .Clear();

            if (useTwoMainColumns)
            {
                const double leftStar =
                    1d;

                const double rightStar =
                    1d;

                GeometryMainResponsiveGrid
                    .ColumnDefinitions
                    .Add(
                        new ColumnDefinition
                        {
                            Width =
                                new GridLength(
                                    leftStar,
                                    GridUnitType.Star)
                        });

                GeometryMainResponsiveGrid
                    .ColumnDefinitions
                    .Add(
                        new ColumnDefinition
                        {
                            Width =
                                new GridLength(
                                    rightStar,
                                    GridUnitType.Star)
                        });

                GeometryMainResponsiveGrid
                    .RowDefinitions
                    .Add(
                        new RowDefinition
                        {
                            Height =
                                GridLength.Auto
                        });

                Grid.SetRow(
                    GeometryControlColumn,
                    0);

                Grid.SetColumn(
                    GeometryControlColumn,
                    0);

                Grid.SetRow(
                    FormulaPreviewBorder,
                    0);

                Grid.SetColumn(
                    FormulaPreviewBorder,
                    1);

                GeometryMainResponsiveGrid.ColumnSpacing =
                    14d;

                GeometryMainResponsiveGrid.RowSpacing =
                    0d;
            }
            else
            {
                GeometryMainResponsiveGrid
                    .ColumnDefinitions
                    .Add(
                        new ColumnDefinition
                        {
                            Width =
                                GridLength.Star
                        });

                GeometryMainResponsiveGrid
                    .RowDefinitions
                    .Add(
                        new RowDefinition
                        {
                            Height =
                                GridLength.Auto
                        });

                GeometryMainResponsiveGrid
                    .RowDefinitions
                    .Add(
                        new RowDefinition
                        {
                            Height =
                                GridLength.Auto
                        });

                Grid.SetRow(
                    GeometryControlColumn,
                    0);

                Grid.SetColumn(
                    GeometryControlColumn,
                    0);

                Grid.SetRow(
                    FormulaPreviewBorder,
                    1);

                Grid.SetColumn(
                    FormulaPreviewBorder,
                    0);

                GeometryMainResponsiveGrid.ColumnSpacing =
                    0d;

                GeometryMainResponsiveGrid.RowSpacing =
                    14d;
            }

            UpdateFormulaPreviewContentLayout(
                availableWidth,
                useTwoMainColumns);
        }
        finally
        {
            _isUpdatingResponsiveLayout =
                false;
        }

        GeometryMainResponsiveGrid.InvalidateMeasure();

        Dispatcher.Dispatch(
            SynchronizeFormulaPreviewHeight);
    }

    private void UpdateFormulaPreviewContentLayout(
        double availableWidth,
        bool useTwoMainColumns)
    {
        FormulaPreviewContentGrid
            .ColumnDefinitions
            .Clear();

        FormulaPreviewContentGrid
            .RowDefinitions
            .Clear();

        // Khi phần preview nằm bên phải, luôn dùng bố cục ngang gọn:
        // hình minh họa bên trái, công thức và chú thích bên phải.
        if (useTwoMainColumns)
        {
            FormulaPreviewContentGrid
                .ColumnDefinitions
                .Add(
                    new ColumnDefinition
                    {
                        Width =
                            new GridLength(
                                11d,
                                GridUnitType.Star)
                    });

            FormulaPreviewContentGrid
                .ColumnDefinitions
                .Add(
                    new ColumnDefinition
                    {
                        Width =
                            new GridLength(
                                9d,
                                GridUnitType.Star)
                    });

            FormulaPreviewContentGrid
                .RowDefinitions
                .Add(
                    new RowDefinition
                    {
                        Height =
                            GridLength.Star
                    });

            Grid.SetRow(
                FormulaDiagramPanel,
                0);

            Grid.SetColumn(
                FormulaDiagramPanel,
                0);

            Grid.SetRow(
                FormulaTextPanel,
                0);

            Grid.SetColumn(
                FormulaTextPanel,
                1);

            FormulaPreviewContentGrid.ColumnSpacing =
                10d;

            FormulaPreviewContentGrid.RowSpacing =
                0d;

            return;
        }

        // Tablet nhỏ vẫn có đủ chiều rộng để giữ hình và công thức cạnh nhau.
        if (availableWidth >=
            650d)
        {
            FormulaPreviewContentGrid
                .ColumnDefinitions
                .Add(
                    new ColumnDefinition
                    {
                        Width =
                            new GridLength(
                                11d,
                                GridUnitType.Star)
                    });

            FormulaPreviewContentGrid
                .ColumnDefinitions
                .Add(
                    new ColumnDefinition
                    {
                        Width =
                            new GridLength(
                                9d,
                                GridUnitType.Star)
                    });

            FormulaPreviewContentGrid
                .RowDefinitions
                .Add(
                    new RowDefinition
                    {
                        Height =
                            GridLength.Star
                    });

            Grid.SetRow(
                FormulaDiagramPanel,
                0);

            Grid.SetColumn(
                FormulaDiagramPanel,
                0);

            Grid.SetRow(
                FormulaTextPanel,
                0);

            Grid.SetColumn(
                FormulaTextPanel,
                1);

            FormulaPreviewContentGrid.ColumnSpacing =
                10d;

            FormulaPreviewContentGrid.RowSpacing =
                0d;

            return;
        }

        // Điện thoại: xếp dọc để nội dung không bị quá hẹp.
        FormulaPreviewContentGrid
            .ColumnDefinitions
            .Add(
                new ColumnDefinition
                {
                    Width =
                        GridLength.Star
                });

        FormulaPreviewContentGrid
            .RowDefinitions
            .Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Star
                });

        FormulaPreviewContentGrid
            .RowDefinitions
            .Add(
                new RowDefinition
                {
                    Height =
                        GridLength.Auto
                });

        Grid.SetRow(
            FormulaDiagramPanel,
            0);

        Grid.SetColumn(
            FormulaDiagramPanel,
            0);

        Grid.SetRow(
            FormulaTextPanel,
            1);

        Grid.SetColumn(
            FormulaTextPanel,
            0);

        FormulaPreviewContentGrid.ColumnSpacing =
            0d;

        FormulaPreviewContentGrid.RowSpacing =
            8d;
    }

    private void SynchronizeFormulaPreviewHeight()
    {
        if (_isSynchronizingFormulaPreviewHeight)
        {
            return;
        }

        double availableWidth =
            GeometryMainResponsiveGrid.Width;

        if (availableWidth <=
            0d)
        {
            return;
        }

        _isSynchronizingFormulaPreviewHeight =
            true;

        try
        {
            bool useTwoMainColumns =
                availableWidth >=
                900d;

            if (useTwoMainColumns)
            {
                double controlColumnHeight =
                    GeometryControlColumn.Height;

                if (!double.IsFinite(
                        controlColumnHeight) ||
                    controlColumnHeight <=
                        0d)
                {
                    return;
                }

                // Ba card bên trái quyết định chiều cao duy nhất của hàng.
                // Preview không còn tự cao lên khi đổi sang hình nón.
                double requestedHeight =
                    Math.Ceiling(
                        controlColumnHeight);

                FormulaPreviewBorder.MinimumHeightRequest =
                    requestedHeight;

                FormulaPreviewBorder.MaximumHeightRequest =
                    requestedHeight;

                FormulaPreviewBorder.HeightRequest =
                    requestedHeight;

                // Phần hình chiếm toàn bộ chiều cao còn lại sau tiêu đề.
                double diagramHeight =
                    Math.Clamp(
                        requestedHeight -
                        104d,
                        210d,
                        330d);

                GeometryDiagramView.HeightRequest =
                    diagramHeight;

                GeometryDiagramView.MinimumHeightRequest =
                    diagramHeight;
            }
            else
            {
                // Màn hình hẹp dùng chiều cao cố định theo trường hợp nhiều
                // nội dung nhất là hình nón. Vì vậy chuyển hình không làm
                // phần Nhập kích thước nhảy lên/xuống.
                bool useHorizontalPreview =
                    availableWidth >=
                    650d;

                double requestedHeight =
                    useHorizontalPreview
                        ? 430d
                        : 590d;

                FormulaPreviewBorder.MinimumHeightRequest =
                    requestedHeight;

                FormulaPreviewBorder.MaximumHeightRequest =
                    requestedHeight;

                FormulaPreviewBorder.HeightRequest =
                    requestedHeight;

                double diagramHeight =
                    useHorizontalPreview
                        ? 280d
                        : 230d;

                GeometryDiagramView.HeightRequest =
                    diagramHeight;

                GeometryDiagramView.MinimumHeightRequest =
                    diagramHeight;
            }
        }
        finally
        {
            _isSynchronizingFormulaPreviewHeight =
                false;
        }

        FormulaPreviewBorder.InvalidateMeasure();
        FormulaPreviewContentGrid.InvalidateMeasure();
        GeometryDiagramView.Invalidate();
    }

    private void OnGeometryInputFlexLayoutSizeChanged(
        object? sender,
        EventArgs e)
    {
        UpdateInputFieldWidths();
        ScheduleInputFlexHeightUpdate();
    }

    private void UpdateInputFieldWidths()
    {
        double availableWidth =
            GeometryInputFlexLayout.Width;

        int fieldCount =
            GeometryInputFlexLayout.Children.Count;

        if (availableWidth <=
                0d ||
            fieldCount ==
                0)
        {
            return;
        }

        int columnCount =
            GetInputColumnCount(
                availableWidth,
                fieldCount);

        // Khoảng cách chỉ nằm giữa hai card. Card đầu tiên và cuối cùng của
        // mỗi hàng không có lề ngoài, nhờ đó cạnh trái/phải của vùng nhập
        // trùng chính xác với hàng nút Tính toán / Xóa phía dưới.
        const double columnSpacing =
            10d;

        double requestedWidth =
            Math.Floor(
                (availableWidth -
                 (columnCount - 1) *
                 columnSpacing) /
                columnCount);

        requestedWidth =
            Math.Max(
                150d,
                requestedWidth);

        for (int index = 0;
             index < fieldCount;
             index++)
        {
            IView child =
                GeometryInputFlexLayout.Children[index];

            if (child is not Microsoft.Maui.Controls.View element)
            {
                continue;
            }

            bool isLastCardInRow =
                (index + 1) % columnCount == 0 ||
                index == fieldCount - 1;

            element.Margin =
                new Thickness(
                    0d,
                    5d,
                    isLastCardInRow
                        ? 0d
                        : columnSpacing,
                    5d);

            element.MinimumWidthRequest =
                0d;

            element.MaximumWidthRequest =
                double.PositiveInfinity;

            element.WidthRequest =
                requestedWidth;
        }

        GeometryInputFlexLayout.InvalidateMeasure();

        if (GeometryInputFlexLayout.Parent
            is VisualElement parent)
        {
            parent.InvalidateMeasure();
        }

        ScheduleInputFlexHeightUpdate(
            columnCount);
    }

    private void ScheduleInputFlexHeightUpdate(
        int? knownColumnCount = null)
    {
        if (GeometryInputFlexLayout.Children.Count ==
            0)
        {
            GeometryInputFlexLayout.HeightRequest =
                -1d;

            return;
        }

        Dispatcher.Dispatch(
            () =>
            {
                UpdateInputFlexHeight(
                    knownColumnCount);

                Dispatcher.Dispatch(
                    () => UpdateInputFlexHeight(
                        knownColumnCount));
            });
    }

    private void UpdateInputFlexHeight(
        int? knownColumnCount = null)
    {
        int fieldCount =
            GeometryInputFlexLayout.Children.Count;

        double availableWidth =
            GeometryInputFlexLayout.Width;

        if (fieldCount == 0 ||
            availableWidth <= 0d)
        {
            return;
        }

        int columnCount =
            knownColumnCount ??
            GetInputColumnCount(
                availableWidth,
                fieldCount);

        columnCount =
            Math.Clamp(
                columnCount,
                1,
                fieldCount);

        int rowCount =
            (int)Math.Ceiling(
                fieldCount /
                (double)columnCount);

        double[] rowHeights =
            new double[rowCount];

        for (int index = 0;
             index < fieldCount;
             index++)
        {
            if (GeometryInputFlexLayout.Children[index]
                is not VisualElement card)
            {
                continue;
            }

            double cardHeight =
                card.Height;

            if (!double.IsFinite(
                    cardHeight) ||
                cardHeight <= 0d)
            {
                // Card ở hàng mới có thể chưa được arrange vì HeightRequest
                // trước đó chỉ đủ một hàng. Dùng chiều cao yêu cầu/tối thiểu
                // để mở khung trước; lượt layout kế tiếp sẽ cập nhật bằng
                // chiều cao thật nếu label cần xuống dòng.
                cardHeight =
                    double.IsFinite(
                            card.HeightRequest) &&
                        card.HeightRequest > 0d
                            ? card.HeightRequest
                            : Math.Max(
                                GeometryInputCardMinimumHeight,
                                card.MinimumHeightRequest);
            }

            int rowIndex =
                index /
                columnCount;

            rowHeights[rowIndex] =
                Math.Max(
                    rowHeights[rowIndex],
                    cardHeight);
        }

        for (int rowIndex = 0;
             rowIndex < rowHeights.Length;
             rowIndex++)
        {
            rowHeights[rowIndex] =
                Math.Max(
                    GeometryInputCardMinimumHeight,
                    rowHeights[rowIndex]);
        }

        // Mỗi card dùng lề dọc 5 px ở trên và dưới, nên mỗi hàng cần
        // thêm tổng cộng 10 px vào chiều cao FlexLayout.
        const double verticalMarginPerRow =
            10d;

        double requestedHeight =
            Math.Ceiling(
                rowHeights.Sum() +
                rowCount *
                verticalMarginPerRow);

        if (Math.Abs(
                GeometryInputFlexLayout.HeightRequest -
                requestedHeight) < 1d)
        {
            return;
        }

        GeometryInputFlexLayout.HeightRequest =
            requestedHeight;

        GeometryInputFlexLayout.InvalidateMeasure();

        if (GeometryInputFlexLayout.Parent
            is VisualElement parent)
        {
            parent.InvalidateMeasure();
        }
    }

    private static int GetInputColumnCount(
        double availableWidth,
        int fieldCount)
    {
        int maximumColumnCount =
            availableWidth switch
            {
                >= 1500d => 5,
                >= 1120d => 4,
                >= 760d => 3,
                >= 520d => 2,
                _ => 1
            };

        int columnCount =
            Math.Max(
                1,
                Math.Min(
                    fieldCount,
                    maximumColumnCount));

        if (fieldCount >
                columnCount &&
            columnCount >
                2 &&
            fieldCount %
                columnCount ==
                1)
        {
            columnCount--;
        }

        return columnCount;
    }

    private enum GeometryNumberType
    {
        Integer,
        Decimal
    }

    private readonly record struct GeometryInputFieldDefinition(
        string Key,
        string Label);
}

public sealed class GeometryInputField :
    INotifyPropertyChanged
{
    private string _text =
        string.Empty;

    public string Key { get; init; } =
        string.Empty;

    public string Label { get; init; } =
        string.Empty;

    public string Placeholder { get; init; } =
        string.Empty;

    public string RawText { get; set; } =
        string.Empty;

    public string Text
    {
        get => _text;
        set
        {
            if (string.Equals(
                    _text,
                    value,
                    StringComparison.Ordinal))
            {
                return;
            }

            _text =
                value;

            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(
                    nameof(Text)));
        }
    }

    public event PropertyChangedEventHandler?
        PropertyChanged;
}

public sealed class GeometryResultLine
{
    public string Title { get; init; } =
        string.Empty;

    public string Formula { get; init; } =
        string.Empty;

    public string Value { get; init; } =
        string.Empty;

    public bool IsDecimal { get; init; }
}
