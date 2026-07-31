using MathSolver.Models;
using MathSolver.Services;
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
public partial class GeometryCalculatorView : ContentView
{
    private const int MaxDecimalPlaces = 10;
    private const int ScientificDisplayDigitThreshold = 18;
    private const int ScientificDisplaySignificantDigits = 12;

    private const string Int128RangeText =
        "−170,141,183,460,469,231,731,687,303,715,884,105,728 đến " +
        "170,141,183,460,469,231,731,687,303,715,884,105,727";

    private const string DecimalRangeText =
        "−79,228,162,514,264,337,593,543,950,335 đến " +
        "79,228,162,514,264,337,593,543,950,335";

    private const decimal Pi =
        3.1415926535897932384626433833m;

    private const decimal SquareRootOfThree =
        1.7320508075688772935274463415m;

    private GeometryCategory _selectedCategory =
        GeometryCategory.Plane;

    private GeometryNumberType _selectedNumberType =
        GeometryNumberType.Integer;

    private GeometryFormulaItem? _selectedGeometry;

    private bool _isUpdatingEntryText;
    private bool _isLanguageSubscribed;
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

        Loaded +=
            OnLoaded;

        Unloaded +=
            OnUnloaded;

        LocalizationService.Attach(
            this);

        SubscribeLanguageChanged();

        SelectCategory(
            GeometryCategory.Plane);

        SelectNumberType(
            GeometryNumberType.Integer,
            clearInputs: false);
    }

    private void OnLoaded(
        object? sender,
        EventArgs e)
    {
        SubscribeLanguageChanged();

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

    private void OnUnloaded(
        object? sender,
        EventArgs e)
    {
        UnsubscribeLanguageChanged();
    }

    private void SubscribeLanguageChanged()
    {
        if (_isLanguageSubscribed)
        {
            return;
        }

        AppLanguageManager.LanguageChanged +=
            OnLanguageChanged;

        _isLanguageSubscribed =
            true;
    }

    private void UnsubscribeLanguageChanged()
    {
        if (!_isLanguageSubscribed)
        {
            return;
        }

        AppLanguageManager.LanguageChanged -=
            OnLanguageChanged;

        _isLanguageSubscribed =
            false;
    }

    private void OnLanguageChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Dispatch(
            () =>
            {
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

                LocalizationService.Attach(
                    this);
            });
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

        field.RawText =
            NormalizeRawInput(
                newText);

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

        SetEntryText(
            entry,
            field,
            field.RawText,
            updateRawText: false);
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
            if (!BigInteger.TryParse(
                    rawText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out BigInteger integerValue))
            {
                return;
            }

            formattedText =
                FormatBigIntegerForDisplay(
                    integerValue);
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
        bool updateRawText)
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

        try
        {
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
        }
        catch (OverflowException)
        {
            ShowDecimalOverflow();

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
                    BigInteger a =
                        value["a"];

                    AddIntegerResult(
                        "Chu vi",
                        "P = a × 4",
                        a * 4);

                    AddIntegerResult(
                        "Diện tích",
                        "S = a × a",
                        a * a);

                    return true;
                }

            case "rectangle":
                {
                    BigInteger a =
                        value["a"];

                    BigInteger b =
                        value["b"];

                    AddIntegerResult(
                        "Chu vi",
                        "P = (a + b) × 2",
                        (a + b) * 2);

                    AddIntegerResult(
                        "Diện tích",
                        "S = a × b",
                        a * b);

                    return true;
                }

            case "triangle":
                {
                    BigInteger a =
                        value["a"];

                    BigInteger b =
                        value["b"];

                    BigInteger c =
                        value["c"];

                    BigInteger h =
                        value["h"];

                    if (!IsValidTriangle(
                            a,
                            b,
                            c))
                    {
                        ShowError(
                            T(
                                "Ba cạnh không tạo thành tam giác hợp lệ."));

                        return false;
                    }

                    AddIntegerResult(
                        "Chu vi",
                        "P = a + b + c",
                        a + b + c);

                    if (!AddRationalIntegerResult(
                            "Diện tích",
                            "S = (a × h) ÷ 2",
                            a * h,
                            2,
                            ref usesDecimalResult))
                    {
                        return false;
                    }

                    return true;
                }

            case "right_triangle":
                {
                    BigInteger a =
                        value["a"];

                    BigInteger b =
                        value["b"];

                    BigInteger c =
                        value["c"];

                    if (a * a +
                        b * b !=
                        c * c)
                    {
                        ShowError(
                            T(
                                "Ba cạnh không thỏa mãn định lý Pythagore a² + b² = c²."));

                        return false;
                    }

                    AddIntegerResult(
                        "Chu vi",
                        "P = a + b + c",
                        a + b + c);

                    if (!AddRationalIntegerResult(
                            "Diện tích",
                            "S = (a × b) ÷ 2",
                            a * b,
                            2,
                            ref usesDecimalResult))
                    {
                        return false;
                    }

                    return true;
                }

            case "equilateral_triangle":
                {
                    BigInteger a =
                        value["a"];

                    AddIntegerResult(
                        "Chu vi",
                        "P = a × 3",
                        a * 3);

                    if (!TryBigIntegerToDecimal(
                            a,
                            out decimal decimalA))
                    {
                        ShowDecimalOverflow();

                        return false;
                    }

                    decimal area =
                        checked(
                            decimalA *
                            decimalA *
                            SquareRootOfThree /
                            4m);

                    AddDecimalResult(
                        "Diện tích",
                        "S = (a² × √3) ÷ 4",
                        area);

                    usesDecimalResult =
                        true;

                    return true;
                }

            case "circle":
                {
                    if (!TryBigIntegerToDecimal(
                            value["r"],
                            out decimal r))
                    {
                        ShowDecimalOverflow();

                        return false;
                    }

                    AddDecimalResult(
                        "Chu vi",
                        "C = 2 × π × r",
                        checked(
                            2m *
                            Pi *
                            r));

                    AddDecimalResult(
                        "Diện tích",
                        "S = π × r²",
                        checked(
                            Pi *
                            r *
                            r));

                    usesDecimalResult =
                        true;

                    return true;
                }

            case "trapezoid":
                {
                    BigInteger a =
                        value["a"];

                    BigInteger b =
                        value["b"];

                    BigInteger c =
                        value["c"];

                    BigInteger d =
                        value["d"];

                    BigInteger h =
                        value["h"];

                    AddIntegerResult(
                        "Chu vi",
                        "P = a + b + c + d",
                        a + b + c + d);

                    if (!AddRationalIntegerResult(
                            "Diện tích",
                            "S = ((a + b) × h) ÷ 2",
                            (a + b) * h,
                            2,
                            ref usesDecimalResult))
                    {
                        return false;
                    }

                    return true;
                }

            case "isosceles_trapezoid":
                {
                    BigInteger a =
                        value["a"];

                    BigInteger b =
                        value["b"];

                    BigInteger c =
                        value["c"];

                    BigInteger h =
                        value["h"];

                    AddIntegerResult(
                        "Chu vi",
                        "P = a + b + 2c",
                        a + b + 2 * c);

                    if (!AddRationalIntegerResult(
                            "Diện tích",
                            "S = ((a + b) × h) ÷ 2",
                            (a + b) * h,
                            2,
                            ref usesDecimalResult))
                    {
                        return false;
                    }

                    return true;
                }

            case "right_trapezoid":
                {
                    BigInteger a =
                        value["a"];

                    BigInteger b =
                        value["b"];

                    BigInteger c =
                        value["c"];

                    BigInteger h =
                        value["h"];

                    AddIntegerResult(
                        "Chu vi",
                        "P = a + b + c + h",
                        a + b + c + h);

                    if (!AddRationalIntegerResult(
                            "Diện tích",
                            "S = ((a + b) × h) ÷ 2",
                            (a + b) * h,
                            2,
                            ref usesDecimalResult))
                    {
                        return false;
                    }

                    return true;
                }

            case "rhombus":
                {
                    BigInteger a =
                        value["a"];

                    BigInteger d1 =
                        value["d1"];

                    BigInteger d2 =
                        value["d2"];

                    BigInteger h =
                        value["h"];

                    AddIntegerResult(
                        "Chu vi",
                        "P = a × 4",
                        a * 4);

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
                    BigInteger a =
                        value["a"];

                    BigInteger b =
                        value["b"];

                    BigInteger h =
                        value["h"];

                    AddIntegerResult(
                        "Chu vi",
                        "P = (a + b) × 2",
                        (a + b) * 2);

                    AddIntegerResult(
                        "Diện tích",
                        "S = a × h",
                        a * h);

                    return true;
                }

            default:
                ShowError(
                    T(
                        "Hình học mặt phẳng này chưa có bộ tính toán."));

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
                    BigInteger a =
                        value["a"];

                    BigInteger square =
                        a * a;

                    AddIntegerResult(
                        "Diện tích xung quanh",
                        "Sxq = 4 × a²",
                        4 * square);

                    AddIntegerResult(
                        "Diện tích toàn phần",
                        "Stp = 6 × a²",
                        6 * square);

                    AddIntegerResult(
                        "Thể tích",
                        "V = a³",
                        square * a);

                    return true;
                }

            case "rectangular_prism":
                {
                    BigInteger a =
                        value["a"];

                    BigInteger b =
                        value["b"];

                    BigInteger h =
                        value["h"];

                    AddIntegerResult(
                        "Diện tích xung quanh",
                        "Sxq = 2 × (a + b) × h",
                        2 *
                        (a + b) *
                        h);

                    AddIntegerResult(
                        "Diện tích toàn phần",
                        "Stp = 2 × (a × b + a × h + b × h)",
                        2 *
                        (a * b +
                         a * h +
                         b * h));

                    AddIntegerResult(
                        "Thể tích",
                        "V = a × b × h",
                        a *
                        b *
                        h);

                    return true;
                }

            case "sphere":
                {
                    if (!TryBigIntegerToDecimal(
                            value["r"],
                            out decimal r))
                    {
                        ShowDecimalOverflow();

                        return false;
                    }

                    AddDecimalResult(
                        "Diện tích mặt cầu",
                        "S = 4 × π × r²",
                        checked(
                            4m *
                            Pi *
                            r *
                            r));

                    AddDecimalResult(
                        "Thể tích",
                        "V = (4 × π × r³) ÷ 3",
                        checked(
                            4m *
                            Pi *
                            r *
                            r *
                            r /
                            3m));

                    usesDecimalResult =
                        true;

                    return true;
                }

            case "cylinder":
                {
                    if (!TryBigIntegerToDecimal(
                            value["r"],
                            out decimal r) ||
                        !TryBigIntegerToDecimal(
                            value["h"],
                            out decimal h))
                    {
                        ShowDecimalOverflow();

                        return false;
                    }

                    decimal baseArea =
                        checked(
                            Pi *
                            r *
                            r);

                    AddDecimalResult(
                        "Diện tích đáy",
                        "Sđ = π × r²",
                        baseArea);

                    AddDecimalResult(
                        "Diện tích xung quanh",
                        "Sxq = 2 × π × r × h",
                        checked(
                            2m *
                            Pi *
                            r *
                            h));

                    AddDecimalResult(
                        "Diện tích toàn phần",
                        "Stp = 2 × π × r × (r + h)",
                        checked(
                            2m *
                            Pi *
                            r *
                            (r + h)));

                    AddDecimalResult(
                        "Thể tích",
                        "V = π × r² × h",
                        checked(
                            baseArea *
                            h));

                    usesDecimalResult =
                        true;

                    return true;
                }

            case "cone":
                {
                    if (!TryBigIntegerToDecimal(
                            value["r"],
                            out decimal r) ||
                        !TryBigIntegerToDecimal(
                            value["h"],
                            out decimal h) ||
                        !TryBigIntegerToDecimal(
                            value["l"],
                            out decimal l))
                    {
                        ShowDecimalOverflow();

                        return false;
                    }

                    decimal baseArea =
                        checked(
                            Pi *
                            r *
                            r);

                    AddDecimalResult(
                        "Diện tích đáy",
                        "Sđ = π × r²",
                        baseArea);

                    AddDecimalResult(
                        "Diện tích xung quanh",
                        "Sxq = π × r × l",
                        checked(
                            Pi *
                            r *
                            l));

                    AddDecimalResult(
                        "Diện tích toàn phần",
                        "Stp = π × r × (r + l)",
                        checked(
                            Pi *
                            r *
                            (r + l)));

                    AddDecimalResult(
                        "Thể tích",
                        "V = (π × r² × h) ÷ 3",
                        checked(
                            baseArea *
                            h /
                            3m));

                    usesDecimalResult =
                        true;

                    return true;
                }

            default:
                ShowError(
                    T(
                        "Hình học không gian này chưa có bộ tính toán."));

                return false;
        }
    }

    private bool CalculateDecimalGeometry(
        GeometryFormulaItem geometry)
    {
        if (!TryReadDecimalInputs(
                out Dictionary<string, decimal> value))
        {
            return false;
        }

        try
        {
            checked
            {
                switch (geometry.Category)
                {
                    case GeometryCategory.Plane:
                        if (!CalculatePlaneDecimal(
                                geometry.Id,
                                value))
                        {
                            return false;
                        }

                        break;

                    case GeometryCategory.Solid:
                        if (!CalculateSolidDecimal(
                                geometry.Id,
                                value))
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
            }
        }
        catch (OverflowException)
        {
            ShowDecimalOverflow();

            return false;
        }

        CalculationExplanationLabel.Text =
            BuildExplanation();

        return Results.Count > 0;
    }

    private bool CalculatePlaneDecimal(
        string geometryId,
        IReadOnlyDictionary<string, decimal> value)
    {
        switch (geometryId)
        {
            case "square":
                {
                    decimal a =
                        value["a"];

                    AddDecimalResult(
                        "Chu vi",
                        "P = a × 4",
                        a * 4m);

                    AddDecimalResult(
                        "Diện tích",
                        "S = a × a",
                        a * a);

                    return true;
                }

            case "rectangle":
                {
                    decimal a =
                        value["a"];

                    decimal b =
                        value["b"];

                    AddDecimalResult(
                        "Chu vi",
                        "P = (a + b) × 2",
                        (a + b) * 2m);

                    AddDecimalResult(
                        "Diện tích",
                        "S = a × b",
                        a * b);

                    return true;
                }

            case "triangle":
                {
                    decimal a =
                        value["a"];

                    decimal b =
                        value["b"];

                    decimal c =
                        value["c"];

                    decimal h =
                        value["h"];

                    if (!IsValidTriangle(
                            a,
                            b,
                            c))
                    {
                        ShowError(
                            T(
                                "Ba cạnh không tạo thành tam giác hợp lệ."));

                        return false;
                    }

                    AddDecimalResult(
                        "Chu vi",
                        "P = a + b + c",
                        a + b + c);

                    AddDecimalResult(
                        "Diện tích",
                        "S = (a × h) ÷ 2",
                        a * h / 2m);

                    return true;
                }

            case "right_triangle":
                {
                    decimal a =
                        value["a"];

                    decimal b =
                        value["b"];

                    decimal c =
                        value["c"];

                    decimal left =
                        a * a +
                        b * b;

                    decimal right =
                        c * c;

                    if (!ApproximatelyEqual(
                            left,
                            right))
                    {
                        ShowError(
                            T(
                                "Ba cạnh không thỏa mãn định lý Pythagore a² + b² = c²."));

                        return false;
                    }

                    AddDecimalResult(
                        "Chu vi",
                        "P = a + b + c",
                        a + b + c);

                    AddDecimalResult(
                        "Diện tích",
                        "S = (a × b) ÷ 2",
                        a * b / 2m);

                    return true;
                }

            case "equilateral_triangle":
                {
                    decimal a =
                        value["a"];

                    AddDecimalResult(
                        "Chu vi",
                        "P = a × 3",
                        a * 3m);

                    AddDecimalResult(
                        "Diện tích",
                        "S = (a² × √3) ÷ 4",
                        a *
                        a *
                        SquareRootOfThree /
                        4m);

                    return true;
                }

            case "circle":
                {
                    decimal r =
                        value["r"];

                    AddDecimalResult(
                        "Chu vi",
                        "C = 2 × π × r",
                        2m *
                        Pi *
                        r);

                    AddDecimalResult(
                        "Diện tích",
                        "S = π × r²",
                        Pi *
                        r *
                        r);

                    return true;
                }

            case "trapezoid":
                {
                    decimal a =
                        value["a"];

                    decimal b =
                        value["b"];

                    decimal c =
                        value["c"];

                    decimal d =
                        value["d"];

                    decimal h =
                        value["h"];

                    AddDecimalResult(
                        "Chu vi",
                        "P = a + b + c + d",
                        a + b + c + d);

                    AddDecimalResult(
                        "Diện tích",
                        "S = ((a + b) × h) ÷ 2",
                        (a + b) *
                        h /
                        2m);

                    return true;
                }

            case "isosceles_trapezoid":
                {
                    decimal a =
                        value["a"];

                    decimal b =
                        value["b"];

                    decimal c =
                        value["c"];

                    decimal h =
                        value["h"];

                    AddDecimalResult(
                        "Chu vi",
                        "P = a + b + 2c",
                        a + b + 2m * c);

                    AddDecimalResult(
                        "Diện tích",
                        "S = ((a + b) × h) ÷ 2",
                        (a + b) *
                        h /
                        2m);

                    return true;
                }

            case "right_trapezoid":
                {
                    decimal a =
                        value["a"];

                    decimal b =
                        value["b"];

                    decimal c =
                        value["c"];

                    decimal h =
                        value["h"];

                    AddDecimalResult(
                        "Chu vi",
                        "P = a + b + c + h",
                        a + b + c + h);

                    AddDecimalResult(
                        "Diện tích",
                        "S = ((a + b) × h) ÷ 2",
                        (a + b) *
                        h /
                        2m);

                    return true;
                }

            case "rhombus":
                {
                    decimal a =
                        value["a"];

                    decimal d1 =
                        value["d1"];

                    decimal d2 =
                        value["d2"];

                    decimal h =
                        value["h"];

                    AddDecimalResult(
                        "Chu vi",
                        "P = a × 4",
                        a * 4m);

                    AddDecimalResult(
                        "Diện tích theo đường chéo",
                        "S = (d₁ × d₂) ÷ 2",
                        d1 *
                        d2 /
                        2m);

                    AddDecimalResult(
                        "Diện tích theo đáy và chiều cao",
                        "S = a × h",
                        a *
                        h);

                    return true;
                }

            case "parallelogram":
                {
                    decimal a =
                        value["a"];

                    decimal b =
                        value["b"];

                    decimal h =
                        value["h"];

                    AddDecimalResult(
                        "Chu vi",
                        "P = (a + b) × 2",
                        (a + b) * 2m);

                    AddDecimalResult(
                        "Diện tích",
                        "S = a × h",
                        a * h);

                    return true;
                }

            default:
                ShowError(
                    T(
                        "Hình học mặt phẳng này chưa có bộ tính toán."));

                return false;
        }
    }

    private bool CalculateSolidDecimal(
        string geometryId,
        IReadOnlyDictionary<string, decimal> value)
    {
        switch (geometryId)
        {
            case "cube":
                {
                    decimal a =
                        value["a"];

                    decimal square =
                        a * a;

                    AddDecimalResult(
                        "Diện tích xung quanh",
                        "Sxq = 4 × a²",
                        4m * square);

                    AddDecimalResult(
                        "Diện tích toàn phần",
                        "Stp = 6 × a²",
                        6m * square);

                    AddDecimalResult(
                        "Thể tích",
                        "V = a³",
                        square * a);

                    return true;
                }

            case "rectangular_prism":
                {
                    decimal a =
                        value["a"];

                    decimal b =
                        value["b"];

                    decimal h =
                        value["h"];

                    AddDecimalResult(
                        "Diện tích xung quanh",
                        "Sxq = 2 × (a + b) × h",
                        2m *
                        (a + b) *
                        h);

                    AddDecimalResult(
                        "Diện tích toàn phần",
                        "Stp = 2 × (a × b + a × h + b × h)",
                        2m *
                        (a * b +
                         a * h +
                         b * h));

                    AddDecimalResult(
                        "Thể tích",
                        "V = a × b × h",
                        a *
                        b *
                        h);

                    return true;
                }

            case "sphere":
                {
                    decimal r =
                        value["r"];

                    AddDecimalResult(
                        "Diện tích mặt cầu",
                        "S = 4 × π × r²",
                        4m *
                        Pi *
                        r *
                        r);

                    AddDecimalResult(
                        "Thể tích",
                        "V = (4 × π × r³) ÷ 3",
                        4m *
                        Pi *
                        r *
                        r *
                        r /
                        3m);

                    return true;
                }

            case "cylinder":
                {
                    decimal r =
                        value["r"];

                    decimal h =
                        value["h"];

                    decimal baseArea =
                        Pi *
                        r *
                        r;

                    AddDecimalResult(
                        "Diện tích đáy",
                        "Sđ = π × r²",
                        baseArea);

                    AddDecimalResult(
                        "Diện tích xung quanh",
                        "Sxq = 2 × π × r × h",
                        2m *
                        Pi *
                        r *
                        h);

                    AddDecimalResult(
                        "Diện tích toàn phần",
                        "Stp = 2 × π × r × (r + h)",
                        2m *
                        Pi *
                        r *
                        (r + h));

                    AddDecimalResult(
                        "Thể tích",
                        "V = π × r² × h",
                        baseArea *
                        h);

                    return true;
                }

            case "cone":
                {
                    decimal r =
                        value["r"];

                    decimal h =
                        value["h"];

                    decimal l =
                        value["l"];

                    decimal baseArea =
                        Pi *
                        r *
                        r;

                    AddDecimalResult(
                        "Diện tích đáy",
                        "Sđ = π × r²",
                        baseArea);

                    AddDecimalResult(
                        "Diện tích xung quanh",
                        "Sxq = π × r × l",
                        Pi *
                        r *
                        l);

                    AddDecimalResult(
                        "Diện tích toàn phần",
                        "Stp = π × r × (r + l)",
                        Pi *
                        r *
                        (r + l));

                    AddDecimalResult(
                        "Thể tích",
                        "V = (π × r² × h) ÷ 3",
                        baseArea *
                        h /
                        3m);

                    return true;
                }

            default:
                ShowError(
                    T(
                        "Hình học không gian này chưa có bộ tính toán."));

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

            values[field.Key] =
                (BigInteger)parsedValue;
        }

        return true;
    }

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
            AddIntegerResult(
                title,
                formula,
                quotient);

            return true;
        }

        if (!TryBigIntegerToDecimal(
                numerator,
                out decimal decimalNumerator))
        {
            ShowDecimalOverflow();

            return false;
        }

        decimal decimalResult =
            checked(
                decimalNumerator /
                denominator);

        AddDecimalResult(
            title,
            formula,
            decimalResult);

        usesDecimalResult =
            true;

        return true;
    }

    private void AddDecimalResult(
        string title,
        string formula,
        decimal value)
    {
        Results.Add(
            new GeometryResultLine
            {
                Title =
                    T(title),

                Formula =
                    formula,

                Value =
                    FormatDecimalForDisplay(
                        value),

                IsDecimal =
                    true
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

    private static bool TryBigIntegerToDecimal(
        BigInteger value,
        out decimal result)
    {
        if (value <
                (BigInteger)decimal.MinValue ||
            value >
                (BigInteger)decimal.MaxValue)
        {
            result =
                0m;

            return false;
        }

        result =
            (decimal)value;

        return true;
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
        decimal a,
        decimal b,
        decimal c)
    {
        return a + b > c &&
               a + c > b &&
               b + c > a;
    }

    private static bool ApproximatelyEqual(
        decimal first,
        decimal second)
    {
        decimal difference =
            decimal.Abs(
                first -
                second);

        decimal magnitude =
            Math.Max(
                decimal.Abs(
                    first),
                decimal.Abs(
                    second));

        decimal tolerance =
            Math.Max(
                0.0000000001m,
                magnitude *
                0.0000000001m);

        return difference <=
               tolerance;
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

    private void ShowDecimalOverflow()
    {
        ShowError(
            T(
                "Kết quả thập phân vượt quá phạm vi Decimal mà ứng dụng hỗ trợ."));
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

        const double marginPerCard =
            10d;

        const double rightSafetySpace =
            4d;

        double requestedWidth =
            Math.Floor(
                (availableWidth -
                 columnCount *
                 marginPerCard -
                 rightSafetySpace) /
                columnCount);

        requestedWidth =
            Math.Max(
                150d,
                requestedWidth);

        foreach (IView child
                 in GeometryInputFlexLayout.Children)
        {
            if (child is not VisualElement element)
            {
                continue;
            }

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

        bool hasUnmeasuredCard =
            false;

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
                hasUnmeasuredCard =
                    true;

                continue;
            }

            int rowIndex =
                index /
                columnCount;

            rowHeights[rowIndex] =
                Math.Max(
                    rowHeights[rowIndex],
                    cardHeight);
        }

        if (hasUnmeasuredCard ||
            rowHeights.Any(
                height => height <= 0d))
        {
            return;
        }

        // Mỗi card dùng Margin="5", nên mỗi hàng cần thêm 10 px.
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
