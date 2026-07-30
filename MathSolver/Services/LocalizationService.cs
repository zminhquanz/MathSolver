using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

/// <summary>
/// Localizes both static XAML text and text generated later by calculation
/// code. Vietnamese remains the source language, while English is produced
/// at presentation time so mathematical values and formulas are untouched.
/// </summary>
public static class LocalizationService
{
    private sealed class TrackedProperty
    {
        public required string PropertyName { get; init; }

        public required Func<string?> Getter { get; init; }

        public required Action<string> Setter { get; init; }

        public string SourceText { get; set; } =
            string.Empty;
    }

    private sealed class TrackedObject
    {
        public List<TrackedProperty> Properties { get; } =
            [];
    }

    private static readonly ConditionalWeakTable<BindableObject, TrackedObject>
        TrackedObjects =
            new();

    private static readonly List<WeakReference<BindableObject>>
        TrackedObjectReferences =
            [];

    private static readonly List<WeakReference<Element>>
        RootReferences =
            [];

    private static readonly Dictionary<string, string>
        ExactEnglish =
            BuildEnglishMap();

    private static readonly IReadOnlyList<KeyValuePair<string, string>>
        PhraseEnglish =
            BuildPhraseMap()
            .OrderByDescending(
                item => item.Key.Length)
            .ToArray();

    private static bool _initialized;
    private static bool _isApplying;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        AppLanguageManager.Initialize();
        AppLanguageManager.LanguageChanged +=
            OnLanguageChanged;
    }

    public static string Translate(
        string? source)
    {
        if (string.IsNullOrEmpty(source) ||
            AppLanguageManager.CurrentLanguage ==
            AppLanguage.Vietnamese)
        {
            return source ?? string.Empty;
        }

        if (ExactEnglish.TryGetValue(
                source,
                out string? exactTranslation))
        {
            return exactTranslation;
        }

        string translated =
            source;

        translated = Regex.Replace(
            translated,
            @"\bBảng nhân\s+(\d+)\b",
            "Multiplication Table $1");

        translated = Regex.Replace(
            translated,
            @"\bBảng chia\s+(\d+)\b",
            "Division Table $1");

        translated = Regex.Replace(
            translated,
            @"Đang hiển thị bảng nhân từ (\d+) đến (\d+) • (\d+) bảng",
            "Showing multiplication tables $1 to $2 • $3 tables");

        translated = Regex.Replace(
            translated,
            @"Đang hiển thị bảng chia từ (\d+) đến (\d+) • (\d+) bảng",
            "Showing division tables $1 to $2 • $3 tables");

        translated = ApplyDynamicEnglish(
            translated);

        foreach ((string vietnamese, string english)
                 in PhraseEnglish)
        {
            translated = translated.Replace(
                vietnamese,
                english,
                StringComparison.Ordinal);
        }

        return translated;
    }

    public static void Attach(
        Element root)
    {
        ArgumentNullException.ThrowIfNull(
            root);

        Initialize();

        if (!RootReferences.Any(
                reference =>
                    reference.TryGetTarget(
                        out Element? existing) &&
                    ReferenceEquals(
                        existing,
                        root)))
        {
            RootReferences.Add(
                new WeakReference<Element>(
                    root));
        }

        AttachRecursive(
            root,
            new HashSet<Element>(
                ReferenceEqualityComparer.Instance));
    }

    public static void RefreshAll()
    {
        void Refresh()
        {
            for (int index =
                     RootReferences.Count - 1;
                 index >= 0;
                 index--)
            {
                if (!RootReferences[index]
                        .TryGetTarget(
                            out Element? root))
                {
                    RootReferences.RemoveAt(
                        index);
                    continue;
                }

                AttachRecursive(
                    root,
                    new HashSet<Element>(
                        ReferenceEqualityComparer.Instance));
            }

            for (int index =
                     TrackedObjectReferences.Count - 1;
                 index >= 0;
                 index--)
            {
                if (!TrackedObjectReferences[index]
                        .TryGetTarget(
                            out BindableObject? bindableObject))
                {
                    TrackedObjectReferences.RemoveAt(
                        index);
                    continue;
                }

                if (TrackedObjects.TryGetValue(
                        bindableObject,
                        out TrackedObject? trackedObject))
                {
                    ApplyTrackedObject(
                        bindableObject,
                        trackedObject);
                }
            }
        }

        if (MainThread.IsMainThread)
        {
            Refresh();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(
                Refresh);
        }
    }

    private static void OnLanguageChanged(
        object? sender,
        EventArgs e)
    {
        RefreshAll();
    }

    private static void AttachRecursive(
        Element element,
        HashSet<Element> visited)
    {
        if (!visited.Add(
                element))
        {
            return;
        }

        TrackBindableObject(
            element);

        if (element is Microsoft.Maui.IVisualTreeElement visualElement)
        {
            foreach (Microsoft.Maui.IVisualTreeElement child
                     in visualElement.GetVisualChildren())
            {
                if (child is Element childElement)
                {
                    AttachRecursive(
                        childElement,
                        visited);
                }
            }
        }

        if (element is Shell shell)
        {
            foreach (ShellItem item in shell.Items)
            {
                AttachRecursive(
                    item,
                    visited);
            }
        }

        if (element is ShellItem shellItem)
        {
            foreach (ShellSection section in shellItem.Items)
            {
                AttachRecursive(
                    section,
                    visited);
            }
        }

        if (element is ShellSection shellSection)
        {
            foreach (ShellContent content in shellSection.Items)
            {
                AttachRecursive(
                    content,
                    visited);
            }
        }
    }

    private static void TrackBindableObject(
        BindableObject bindableObject)
    {
        if (TrackedObjects.TryGetValue(
                bindableObject,
                out TrackedObject? existing))
        {
            ApplyTrackedObject(
                bindableObject,
                existing);
            return;
        }

        var trackedObject =
            new TrackedObject();

        AddTrackedProperties(
            bindableObject,
            trackedObject);

        if (trackedObject.Properties.Count == 0)
        {
            return;
        }

        TrackedObjects.Add(
            bindableObject,
            trackedObject);

        TrackedObjectReferences.Add(
            new WeakReference<BindableObject>(
                bindableObject));

        bindableObject.PropertyChanged +=
            OnTrackedObjectPropertyChanged;

        ApplyTrackedObject(
            bindableObject,
            trackedObject);
    }

    private static void AddTrackedProperties(
        BindableObject bindableObject,
        TrackedObject trackedObject)
    {
        switch (bindableObject)
        {
            case Label label:
                AddProperty(
                    trackedObject,
                    nameof(Label.Text),
                    () => label.Text,
                    value => label.Text = value);
                break;

            case Button button:
                AddProperty(
                    trackedObject,
                    nameof(Button.Text),
                    () => button.Text,
                    value => button.Text = value);
                break;

            case Entry entry:
                AddProperty(
                    trackedObject,
                    nameof(Entry.Placeholder),
                    () => entry.Placeholder,
                    value => entry.Placeholder = value);
                break;

            case Editor editor:
                AddProperty(
                    trackedObject,
                    nameof(Editor.Placeholder),
                    () => editor.Placeholder,
                    value => editor.Placeholder = value);
                break;

            case SearchBar searchBar:
                AddProperty(
                    trackedObject,
                    nameof(SearchBar.Placeholder),
                    () => searchBar.Placeholder,
                    value => searchBar.Placeholder = value);
                break;

            case Picker picker:
                AddProperty(
                    trackedObject,
                    nameof(Picker.Title),
                    () => picker.Title,
                    value => picker.Title = value);
                break;

            case RadioButton radioButton
                when radioButton.Content is string:
                AddProperty(
                    trackedObject,
                    nameof(RadioButton.Content),
                    () => radioButton.Content as string,
                    value => radioButton.Content = value);
                break;

            case MenuItem menuItem:
                AddProperty(
                    trackedObject,
                    nameof(MenuItem.Text),
                    () => menuItem.Text,
                    value => menuItem.Text = value);
                break;
        }

        if (bindableObject is Page page)
        {
            AddProperty(
                trackedObject,
                nameof(Page.Title),
                () => page.Title,
                value => page.Title = value);
        }
        else if (bindableObject is BaseShellItem shellItem)
        {
            AddProperty(
                trackedObject,
                nameof(BaseShellItem.Title),
                () => shellItem.Title,
                value => shellItem.Title = value);
        }
    }

    private static void AddProperty(
        TrackedObject trackedObject,
        string propertyName,
        Func<string?> getter,
        Action<string> setter)
    {
        string? currentText =
            getter();

        trackedObject.Properties.Add(
            new TrackedProperty
            {
                PropertyName =
                    propertyName,

                Getter =
                    getter,

                Setter =
                    setter,

                SourceText =
                    currentText ??
                    string.Empty
            });
    }

    private static void OnTrackedObjectPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (_isApplying ||
            sender is not BindableObject bindableObject ||
            !TrackedObjects.TryGetValue(
                bindableObject,
                out TrackedObject? trackedObject))
        {
            return;
        }

        IEnumerable<TrackedProperty> changedProperties =
            string.IsNullOrEmpty(
                e.PropertyName)
                ? trackedObject.Properties
                : trackedObject.Properties.Where(
                    property =>
                        property.PropertyName ==
                        e.PropertyName);

        foreach (TrackedProperty property
                 in changedProperties)
        {
            string? currentText =
                property.Getter();

            if (string.IsNullOrEmpty(
                    currentText))
            {
                continue;
            }

            string expectedText =
                Translate(
                    property.SourceText);

            if (string.Equals(
                    currentText,
                    expectedText,
                    StringComparison.Ordinal))
            {
                continue;
            }

            property.SourceText =
                currentText;

            ApplyTrackedProperty(
                property);
        }
    }

    private static void ApplyTrackedObject(
        BindableObject bindableObject,
        TrackedObject trackedObject)
    {
        foreach (TrackedProperty property
                 in trackedObject.Properties)
        {
            ApplyTrackedProperty(
                property);
        }
    }

    private static void ApplyTrackedProperty(
        TrackedProperty property)
    {
        string translatedText =
            Translate(
                property.SourceText);

        if (string.Equals(
                property.Getter(),
                translatedText,
                StringComparison.Ordinal))
        {
            return;
        }

        _isApplying =
            true;

        try
        {
            property.Setter(
                translatedText);
        }
        finally
        {
            _isApplying =
                false;
        }
    }

    private static string ApplyDynamicEnglish(
        string text)
    {
        text = Regex.Replace(
            text,
            @"Vui lòng nhập hệ số (?<name>[abc])\.",
            "Please enter coefficient ${name}.",
            RegexOptions.IgnoreCase);

        text = Regex.Replace(
            text,
            @"hệ số (?<name>[abc]) phải là số nguyên hợp lệ\.",
            "Coefficient ${name} must be a valid integer.",
            RegexOptions.IgnoreCase);

        text = Regex.Replace(
            text,
            @"hệ số (?<name>[abc]) chỉ được có tối đa (?<digits>\d+) chữ số\.",
            "Coefficient ${name} may contain at most ${digits} digits.",
            RegexOptions.IgnoreCase);

        text = Regex.Replace(
            text,
            @"hệ số (?<name>[abc]) không nằm trong phạm vi số nguyên mà ứng dụng hỗ trợ\.",
            "Coefficient ${name} is outside the integer range supported by the app.",
            RegexOptions.IgnoreCase);

        text = Regex.Replace(
            text,
            @"Hệ số (?<name>[abc]) chỉ được nhập số nguyên; không được dùng dấu chấm \(\.\) hoặc dấu phẩy \(,\), chữ cái hay ký tự khác\.",
            "Coefficient ${name} must be an integer; decimal points, commas, letters, and other characters are not allowed.",
            RegexOptions.IgnoreCase);

        text = Regex.Replace(
            text,
            @"hệ số (?<name>[abc]) phải nằm trong phạm vi từ −79,228,162,514,264,337,593,543,950,335 đến 79,228,162,514,264,337,593,543,950,335\.",
            "Coefficient ${name} must be from −79,228,162,514,264,337,593,543,950,335 to 79,228,162,514,264,337,593,543,950,335.",
            RegexOptions.IgnoreCase);

        text = Regex.Replace(
            text,
            @"Vì Δ = (?<delta>.+?) < 0 nên √Δ không phải là một số thực\.",
            "Because Δ = ${delta} < 0, √Δ is not a real number.");

        text = Regex.Replace(
            text,
            @"Phương trình có nghiệm kép x₁ = x₂ = (?<root>.+?)\.",
            "The equation has one repeated real root: x₁ = x₂ = ${root}.");

        text = Regex.Replace(
            text,
            @"(?<field>[^.\n]+) phải là số nguyên hợp lệ trong phạm vi từ −170,141,183,460,469,231,731,687,303,715,884,105,728 đến 170,141,183,460,469,231,731,687,303,715,884,105,727\.",
            match =>
                $"{TranslateFieldName(match.Groups["field"].Value)} must be a valid integer within the range from −170,141,183,460,469,231,731,687,303,715,884,105,728 to 170,141,183,460,469,231,731,687,303,715,884,105,727.");

        text = Regex.Replace(
            text,
            @"(?<field>[^.\n]+) phải nằm trong phạm vi từ −170,141,183,460,469,231,731,687,303,715,884,105,728 đến 170,141,183,460,469,231,731,687,303,715,884,105,727\.",
            match =>
                $"{TranslateFieldName(match.Groups["field"].Value)} must be within the range from −170,141,183,460,469,231,731,687,303,715,884,105,728 to 170,141,183,460,469,231,731,687,303,715,884,105,727.");

        text = Regex.Replace(
            text,
            @"(?<field>[^.\n]+) phải là số thập phân hợp lệ trong phạm vi từ −79,228,162,514,264,337,593,543,950,335 đến 79,228,162,514,264,337,593,543,950,335\.",
            match =>
                $"{TranslateFieldName(match.Groups["field"].Value)} must be a valid decimal within the range from −79,228,162,514,264,337,593,543,950,335 to 79,228,162,514,264,337,593,543,950,335.");

        text = Regex.Replace(
            text,
            @"(?<field>[^.\n]+) phải nằm trong phạm vi từ −79,228,162,514,264,337,593,543,950,335 đến 79,228,162,514,264,337,593,543,950,335\.",
            match =>
                $"{TranslateFieldName(match.Groups["field"].Value)} must be within the range from −79,228,162,514,264,337,593,543,950,335 to 79,228,162,514,264,337,593,543,950,335.");

        text = Regex.Replace(
            text,
            @"Giá trị ""(?<value>[^""]+)"" phải là số nguyên hợp lệ trong phạm vi từ −170,141,183,460,469,231,731,687,303,715,884,105,728 đến 170,141,183,460,469,231,731,687,303,715,884,105,727\.",
            "The value \"${value}\" must be a valid integer within the range from −170,141,183,460,469,231,731,687,303,715,884,105,728 to 170,141,183,460,469,231,731,687,303,715,884,105,727.");

        text = Regex.Replace(
            text,
            @"Giá trị ""(?<value>[^""]+)"" phải nằm trong phạm vi decimal từ −79,228,162,514,264,337,593,543,950,335 đến 79,228,162,514,264,337,593,543,950,335\.",
            "The value \"${value}\" must be within the decimal range from −79,228,162,514,264,337,593,543,950,335 to 79,228,162,514,264,337,593,543,950,335.");

        text = Regex.Replace(
            text,
            @"Vui lòng nhập (?<field>[^.]+)\.",
            match =>
                $"Please enter {TranslateFieldName(match.Groups["field"].Value)}.");

        text = Regex.Replace(
            text,
            @"Giá trị ""(?<value>[^""]+)"" không phải là số nguyên hợp lệ\.",
            "The value \"${value}\" is not a valid integer.");

        text = Regex.Replace(
            text,
            @"Giá trị ""(?<value>[^""]+)"" không phải là số thập phân hợp lệ\.",
            "The value \"${value}\" is not a valid decimal.");

        text = Regex.Replace(
            text,
            @"Giá trị ""(?<value>[^""]+)"" không phải là số hợp lệ\.",
            "The value \"${value}\" is not a valid number.");

        text = Regex.Replace(
            text,
            @"(?<field>[^.\n]+) không phải là một số hợp lệ\.",
            match =>
                $"{TranslateFieldName(match.Groups["field"].Value)} is not a valid number.");

        text = Regex.Replace(
            text,
            @"(?<field>[^.\n]+) phải là một số nguyên hợp lệ\.",
            match =>
                $"{TranslateFieldName(match.Groups["field"].Value)} must be a valid integer.");

        text = Regex.Replace(
            text,
            @"Ta lấy (?<first>.+?) cộng với (?<second>.+?)\.",
            "Add ${first} and ${second}.");

        text = Regex.Replace(
            text,
            @"Ta lấy (?<first>.+?) trừ đi (?<second>.+?)\.",
            "Subtract ${second} from ${first}.");

        text = Regex.Replace(
            text,
            @"Ta lấy (?<first>.+?) nhân với (?<second>.+?)\.",
            "Multiply ${first} by ${second}.");

        text = Regex.Replace(
            text,
            @"Ta lấy (?<first>.+?) chia cho (?<second>.+?)\.",
            "Divide ${first} by ${second}.");

        text = Regex.Replace(
            text,
            @"Ta thực hiện phép chia (?<first>.+?) cho (?<second>.+?)\.",
            "Divide ${first} by ${second}.");

        text = Regex.Replace(
            text,
            @"Vậy kết quả là (?<result>.+?)\.",
            "Therefore, the result is ${result}.");

        text = Regex.Replace(
            text,
            @"Thương là (?<result>.+?)\.",
            "The quotient is ${result}.");

        text = Regex.Replace(
            text,
            @"Vậy thương là (?<quotient>.+?) và số dư là (?<remainder>.+?)\.",
            "Therefore, the quotient is ${quotient} and the remainder is ${remainder}.");

        text = Regex.Replace(
            text,
            @"Hai phân số đã có cùng mẫu số (?<denominator>[^,.\n]+),?\s*nên không cần quy đồng\.",
            "The fractions already have the same denominator ${denominator}, so no conversion is needed.");

        text = Regex.Replace(
            text,
            @"Để đổi mẫu số (?<old>\S+) thành (?<new>\S+), ta nhân cả tử và mẫu với (?<factor>\S+)\.",
            "To change the denominator from ${old} to ${new}, multiply both numerator and denominator by ${factor}.");

        text = Regex.Replace(
            text,
            @"Hai phân số đã có cùng mẫu số (?<denominator>\S+)\. Ta giữ nguyên mẫu số và (?<verb>cộng|trừ) hai tử số\.",
            match =>
            {
                string verb =
                    match.Groups["verb"].Value == "cộng"
                        ? "add"
                        : "subtract";

                return $"The fractions have the same denominator {match.Groups["denominator"].Value}. Keep the denominator and {verb} the numerators.";
            });

        text = Regex.Replace(
            text,
            @"Giữ nguyên mẫu số (?<denominator>\S+)\.",
            "Keep the denominator ${denominator}.");

        text = Regex.Replace(
            text,
            @"Không thể chia cho (?<fraction>[^,]+), vì phân số này bằng 0\.",
            "Cannot divide by ${fraction}, because that fraction is 0.");

        text = Regex.Replace(
            text,
            @"Số chia (?<divisor>.+?) có (?<places>\d+) chữ số ở phần thập phân\.",
            "The divisor ${divisor} has ${places} decimal places.");

        text = Regex.Replace(
            text,
            @"Bảng nhân (?<number>\d+)",
            "Multiplication Table ${number}");

        text = Regex.Replace(
            text,
            @"Bảng chia (?<number>\d+)",
            "Division Table ${number}");

        text = Regex.Replace(
            text,
            @"Ta thực hiện (?<operation>phép cộng|phép trừ) hai phân số\.",
            match =>
                match.Groups["operation"].Value == "phép cộng"
                    ? "Add the two fractions."
                    : "Subtract the two fractions.");

        text = Regex.Replace(
            text,
            @"(?<resultName>Tổng|Hiệu|Tích|Thương|Kết quả) của phép tính (?<expression>.+?) vượt quá phạm vi số mà ứng dụng đang hỗ trợ\.",
            match =>
                $"The {TranslateFieldName(match.Groups["resultName"].Value)} of {match.Groups["expression"].Value} exceeds the numeric range supported by the app.");

        text = Regex.Replace(
            text,
            @"Phép tính (?<expression>.+?) cho kết quả cần nhiều hơn (?<places>\d+) chữ số sau dấu chấm hoặc không thể biểu diễn chính xác trong giới hạn hiện tại\. Ứng dụng không làm tròn kết quả để tránh sai lệch\.",
            "The calculation ${expression} requires more than ${places} decimal places or cannot be represented exactly within the current limit. The app does not round the result, to avoid loss of accuracy.");

        text = Regex.Replace(
            text,
            @"Số nguyên chỉ được chứa chữ số, một dấu âm ở đầu và tối đa (?<digits>\d+) chữ số\. Dấu phẩy phân nhóm được ứng dụng thêm tự động\.",
            "An integer may contain only digits, one leading minus sign, and at most ${digits} digits. Thousands separators are added automatically.");

        text = Regex.Replace(
            text,
            @"Số thập phân chỉ được chứa chữ số, một dấu âm ở đầu, tối đa một dấu chấm, tối đa (?<places>\d+) chữ số sau dấu chấm và tối đa (?<digits>\d+) chữ số tổng cộng; dấu phẩy được thêm tự động\.",
            "A decimal may contain only digits, one leading minus sign, at most one decimal point, at most ${places} decimal places, and at most ${digits} digits in total. Thousands separators are added automatically.");

        text = Regex.Replace(
            text,
            @"Dùng dấu chấm cho phần thập phân, tối đa (?<places>\d+) chữ số sau dấu chấm; dấu phẩy phân nhóm hàng nghìn được thêm tự động, ví dụ: (?<example>.+?)\.",
            "Use a decimal point, with at most ${places} decimal places; thousands separators are added automatically, for example ${example}.");

        text = Regex.Replace(
            text,
            @"Chỉ được nhập số nguyên, tối đa (?<digits>\d+) chữ số; không được nhập dấu chấm hoặc ký tự khác\.",
            "Enter integers only, with at most ${digits} digits; decimal points and other characters are not allowed.");

        text = Regex.Replace(
            text,
            @"Chỉ được nhập số, một dấu âm ở đầu và một dấu chấm; tối đa (?<places>\d+) chữ số sau dấu chấm\.",
            "Enter digits, one optional leading minus sign, and at most one decimal point; up to ${places} decimal places are allowed.");

        text = Regex.Replace(
            text,
            @"Từ 0 ÷ x = (?<result>.+?), phép biến đổi hình thức cho x = 0 nhưng x = 0 lại làm phép chia không xác định\.",
            "From 0 ÷ x = ${result}, a formal transformation gives x = 0, but x = 0 makes the division undefined.");

        text = Regex.Replace(
            text,
            @"Muốn tìm số chia, ta lấy số bị chia chia cho thương; đồng thời số chia phải khác 0\.",
            "To find the divisor, divide the dividend by the quotient; the divisor must also be nonzero.");

        text = Regex.Replace(
            text,
            @"Số chia (?<divisor>.+?) có (?<places>\d+) chữ số ở phần thập phân\.",
            "The divisor ${divisor} has ${places} decimal places.");

        text = Regex.Replace(
            text,
            @"Ta chuyển dấu phẩy của cả số bị chia và số chia sang phải (?<places>\d+) chữ số:",
            "Move the decimal points of both the dividend and divisor ${places} places to the right:");

        text = Regex.Replace(
            text,
            @"Giá trị ""(?<value>.+?)"" không phải là số nguyên hợp lệ trong giới hạn (?<digits>\d+) chữ số\.",
            "The value \"${value}\" is not a valid integer within the ${digits}-digit limit.");

        text = Regex.Replace(
            text,
            @"(?<field>[^.\n]+) phải là số nguyên hợp lệ, tối đa (?<digits>\d+) chữ số\.",
            match =>
                $"{TranslateFieldName(match.Groups["field"].Value)} must be a valid integer with at most {match.Groups["digits"].Value} digits.");

        text = Regex.Replace(
            text,
            @"Vế trái ≈ (?<left>.+?); vế phải = (?<right>.+?)\.",
            "Left side ≈ ${left}; right side = ${right}.");

        return text;
    }

    private static string TranslateFieldName(
        string fieldName)
    {
        string normalized =
            fieldName.Trim();

        var fieldMap =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["số thứ nhất"] = "the first number",
                ["số thứ hai"] = "the second number",
                ["tử số của phân số thứ nhất"] = "the numerator of the first fraction",
                ["mẫu số của phân số thứ nhất"] = "the denominator of the first fraction",
                ["tử số của phân số thứ hai"] = "the numerator of the second fraction",
                ["mẫu số của phân số thứ hai"] = "the denominator of the second fraction",
                ["số hạng đã biết"] = "the known addend",
                ["số bị trừ"] = "the minuend",
                ["số trừ"] = "the subtrahend",
                ["thừa số đã biết"] = "the known factor",
                ["số bị chia"] = "the dividend",
                ["số chia"] = "the divisor",
                ["giá trị đã biết"] = "the known value",
                ["hệ số a"] = "coefficient a",
                ["hệ số b"] = "coefficient b",
                ["hệ số c"] = "coefficient c",
                ["tổng"] = "the sum",
                ["hiệu"] = "the difference",
                ["tích"] = "the product",
                ["thương"] = "the quotient",
                ["kết quả"] = "the result"
            };

        return fieldMap.TryGetValue(
                normalized,
                out string? translated)
            ? translated
            : Translate(
                normalized);
    }

    private static Dictionary<string, string> BuildEnglishMap()
    {
        var map =
            new Dictionary<string, string>(
                StringComparer.Ordinal);

        (string Vietnamese, string English)[] pairs =
        [
            ("Giải toán", "Solve"),
            ("Công thức", "Formulas"),
            ("Cửu chương", "Times Tables"),
            ("Cài đặt giao diện", "Interface settings"),
            ("Mở cài đặt giao diện", "Open interface settings"),
            ("+ Cơ bản", "+ Basic"),
            ("½ Phân số", "½ Fractions"),
            ("𝑥 Tìm x", "𝑥 Find x"),
            ("△ Hình học", "△ Geometry"),
            ("x² Phương trình bậc 2", "x² Quadratic Equation"),
            ("PHƯƠNG TRÌNH BẬC HAI", "QUADRATIC EQUATION"),
            ("Tính biệt thức Δ • Xét số nghiệm • Trình bày lời giải chi tiết", "Compute the discriminant Δ • Determine the roots • Show detailed solution steps"),
            ("1. Nhập các hệ số", "1. Enter the coefficients"),
            ("Hệ số a", "Coefficient a"),
            ("Hệ số b", "Coefficient b"),
            ("Hệ số c", "Coefficient c"),
            ("Hệ số của x²", "Coefficient of x²"),
            ("Hệ số của x", "Coefficient of x"),
            ("Hệ số tự do", "Constant term"),
            ("Tính nghiệm", "Solve"),
            ("2. Kết quả", "2. Result"),
            ("Biệt thức", "Discriminant"),
            ("3. Lời giải chi tiết", "3. Detailed Solution"),
            ("Đồ thị parabol", "Parabola Graph"),
            ("Đồ thị trình bày theo dạng SGK: có trục tọa độ, trục đối xứng, đỉnh và nghiệm thực. Kéo chuột để di chuyển; cuộn con lăn lên để phóng to, xuống để thu nhỏ; hoặc dùng −, + và 100%.", "The graph follows a textbook-style layout with coordinate axes, an axis of symmetry, the vertex, and real roots. Drag with the mouse to pan; scroll the wheel up to zoom in and down to zoom out; or use −, +, and 100%."),
            ("Chưa có dữ liệu đồ thị.", "No graph data is available."),
            ("Bước 1. Xác định các hệ số", "Step 1. Identify the coefficients"),
            ("Bước 2. Tính biệt thức Δ", "Step 2. Compute the discriminant Δ"),
            ("Bước 3. Xét dấu của Δ", "Step 3. Determine the sign of Δ"),
            ("Bước 3. Tính nghiệm kép", "Step 3. Compute the repeated root"),
            ("Bước 3. Tính căn bậc hai của Δ", "Step 3. Compute √Δ"),
            ("Bước 4. Tính hai nghiệm", "Step 4. Compute the two roots"),
            ("Bước 4. Kết luận", "Step 4. Conclusion"),
            ("Δ < 0: phương trình vô nghiệm trong tập số thực.", "Δ < 0: The equation has no real roots."),
            ("Δ = 0: phương trình có nghiệm kép.", "Δ = 0: The equation has one repeated real root."),
            ("Δ > 0: phương trình có hai nghiệm phân biệt.", "Δ > 0: The equation has two distinct real roots."),
            ("Vô nghiệm", "No real roots"),
            ("Phương trình vô nghiệm trong tập số thực ℝ.", "The equation has no real roots."),
            ("Hệ số a phải khác 0. Khi a = 0, biểu thức không còn là phương trình bậc hai.", "Coefficient a must be nonzero. When a = 0, the expression is no longer a quadratic equation."),
            ("Kết quả Δ vượt quá phạm vi mà ứng dụng hỗ trợ.", "The value of Δ exceeds the numeric range supported by the app."),
            ("Nghiệm vượt quá phạm vi mà ứng dụng hỗ trợ.", "The root exceeds the numeric range supported by the app."),
            ("Chỉ được nhập số nguyên; không được nhập dấu chấm hoặc ký tự khác.", "Enter integers only; decimal points and other characters are not allowed."),
            ("Chỉ được nhập số nguyên trong phạm vi từ −79,228,162,514,264,337,593,543,950,335 đến 79,228,162,514,264,337,593,543,950,335.", "Enter integers only, from −79,228,162,514,264,337,593,543,950,335 to 79,228,162,514,264,337,593,543,950,335."),
            ("Kết quả Δ không thể biểu diễn hữu hạn bằng độ chính xác Double Double. Ứng dụng không thể tiếp tục tính toán.", "The value of Δ cannot be represented as a finite Double Double value. The app cannot continue the calculation."),
            ("Nghiệm không thể biểu diễn hữu hạn bằng độ chính xác Double Double. Ứng dụng không thể tiếp tục tính toán.", "The root cannot be represented as a finite Double Double value. The app cannot continue the calculation."),
            ("Số nguyên chỉ được chứa chữ số và một dấu âm ở đầu. Dấu phẩy phân nhóm được ứng dụng thêm tự động.", "An integer may contain only digits and one leading minus sign. Thousands separators are added automatically."),
            ("Số thập phân chỉ được chứa chữ số, một dấu âm ở đầu, tối đa một dấu chấm và tối đa 10 chữ số sau dấu chấm; dấu phẩy được thêm tự động.", "A decimal may contain only digits, one leading minus sign, at most one decimal point, and at most 10 decimal places. Thousands separators are added automatically."),
            ("Số nguyên phải nằm trong phạm vi từ −170,141,183,460,469,231,731,687,303,715,884,105,728 đến 170,141,183,460,469,231,731,687,303,715,884,105,727.", "The integer must be within the range from −170,141,183,460,469,231,731,687,303,715,884,105,728 to 170,141,183,460,469,231,731,687,303,715,884,105,727."),
            ("Số thập phân phải nằm trong phạm vi từ −79,228,162,514,264,337,593,543,950,335 đến 79,228,162,514,264,337,593,543,950,335.", "The decimal must be within the range from −79,228,162,514,264,337,593,543,950,335 to 79,228,162,514,264,337,593,543,950,335."),
            ("PHÉP TÍNH CƠ BẢN", "BASIC ARITHMETIC"),
            ("Cộng • Trừ • Nhân • Chia", "Add • Subtract • Multiply • Divide"),
            ("1. Chọn phép tính", "1. Choose an operation"),
            ("2. Nhập các số", "2. Enter the numbers"),
            ("Số nguyên", "Integer"),
            ("Số thập phân", "Decimal"),
            ("Số thứ nhất", "First number"),
            ("Số thứ hai", "Second number"),
            ("Tính kết quả", "Calculate"),
            ("Xóa", "Clear"),
            ("3. Kết quả", "3. Result"),
            ("Đáp số", "Answer"),
            ("Thương", "Quotient"),
            ("Số dư", "Remainder"),
            ("Lời giải", "Solution"),
            ("Lời giải chi tiết", "Detailed solution"),
            ("Phép chia đặt tính", "Long division"),
            ("Chưa có phép chia để hiển thị.", "No division is available to display."),
            ("Các bước thực hiện phép chia theo cách trình bày ở tiểu học.", "Long-division steps shown in the elementary-school format."),
            ("Hiển thị phép chia", "Division display"),
            ("Theo chương trình Tiểu học", "Elementary-school method"),
            ("Thương và số dư", "Quotient and remainder"),
            ("Theo số thập phân", "Decimal method"),
            ("Tiếp tục chia đến kết quả thập phân", "Continue until a decimal result is obtained"),
            ("Thông tin thêm", "Additional information"),
            ("Lưu ý khi sử dụng", "Usage note"),
            ("HÌNH HỌC", "GEOMETRY"),
            ("Tính chu vi và diện tích các hình", "Calculate shape perimeters and areas"),
            ("Chu vi và diện tích", "Perimeter and area"),
            ("Đang phát triển", "In development"),
            ("TÌM THÀNH PHẦN CHƯA BIẾT", "FIND THE UNKNOWN COMPONENT"),
            ("Số hạng • Số bị trừ • Số trừ • Thừa số • Số bị chia • Số chia", "Addend • Minuend • Subtrahend • Factor • Dividend • Divisor"),
            ("Vị trí của x", "Position of x"),
            ("2. Nhập các giá trị đã biết", "2. Enter the known values"),
            ("Số hạng đã biết", "Known addend"),
            ("Tổng", "Sum"),
            ("Phương trình đang nhập", "Current equation"),
            ("Tìm x", "Find x"),
            ("Nghiệm duy nhất", "Unique solution"),
            ("Quy tắc áp dụng", "Applied rule"),
            ("Kiểm tra lại", "Verification"),
            ("Ghi nhớ", "Remember"),
            ("𝑥  Tìm thành phần chưa biết", "𝑥  Find the unknown component"),
            ("△  Hình học", "△  Geometry"),
            ("Dạng tổng quát • Quy tắc • Ví dụ minh họa", "General form • Rule • Worked example"),
            ("Dạng:", "Form:"),
            ("Quy tắc", "Rule"),
            ("Ví dụ : ", "Example: "),
            ("CHU VI, DIỆN TÍCH VÀ THỂ TÍCH", "PERIMETER, AREA AND VOLUME"),
            ("Hình minh họa • Ký hiệu • Công thức thường dùng", "Diagram • Symbols • Common formulas"),
            ("Hình học mặt phẳng • Hình học không gian • Công thức thường dùng", "Plane geometry • Solid geometry • Common formulas"),
            ("HÌNH HỌC MẶT PHẲNG", "PLANE GEOMETRY"),
            ("Các hình hai chiều • Chu vi và diện tích", "2D shapes • Perimeter and area"),
            ("HÌNH HỌC KHÔNG GIAN", "SOLID GEOMETRY"),
            ("Các khối ba chiều • Diện tích và thể tích", "3D solids • Surface area and volume"),
            ("LƯU Ý VỀ TÍNH CHẤT BỐN PHÉP TÍNH", "IMPORTANT PROPERTIES OF THE FOUR OPERATIONS"),
            ("Khi tìm thành phần chưa biết, cần xác định đúng vai trò của x, dùng phép tính ngược phù hợp và luôn kiểm tra lại kết quả trong phép tính ban đầu.", "When finding an unknown component, identify the role of x, use the correct inverse operation, and always verify the result in the original equation."),
            ("+  PHÉP CỘNG", "+  ADDITION"),
            ("• Giao hoán: a + b = b + a. Đổi chỗ hai số hạng thì tổng không thay đổi.", "• Commutative property: a + b = b + a. Swapping the addends does not change the sum."),
            ("• Kết hợp: (a + b) + c = a + (b + c). Có thể nhóm các số hạng theo cách thuận tiện.", "• Associative property: (a + b) + c = a + (b + c). Addends may be grouped in a convenient way."),
            ("• Phần tử trung hòa: a + 0 = 0 + a = a.", "• Additive identity: a + 0 = 0 + a = a."),
            ("• Tìm số hạng chưa biết: x + b = c thì x = c − b.", "• To find an unknown addend: if x + b = c, then x = c − b."),
            ("Ví dụ: x + 18 = 45 ⇒ x = 45 − 18 = 27. Kiểm tra: 27 + 18 = 45.", "Example: x + 18 = 45 ⇒ x = 45 − 18 = 27. Check: 27 + 18 = 45."),
            ("−  PHÉP TRỪ", "−  SUBTRACTION"),
            ("• Không giao hoán: a − b thường khác b − a. Vì vậy không được tự ý đổi chỗ số bị trừ và số trừ.", "• Not commutative: a − b is generally different from b − a. Do not swap the minuend and subtrahend."),
            ("• Không kết hợp: (a − b) − c thường khác a − (b − c). Cần thực hiện đúng thứ tự.", "• Not associative: (a − b) − c is generally different from a − (b − c). Follow the correct order."),
            ("• Tính chất cơ bản: a − 0 = a và a − a = 0.", "• Basic properties: a − 0 = a and a − a = 0."),
            ("• Tìm số bị trừ: x − b = c thì x = c + b. Tìm số trừ: a − x = c thì x = a − c.", "• To find the minuend: if x − b = c, then x = c + b. To find the subtrahend: if a − x = c, then x = a − c."),
            ("Ví dụ: 52 − x = 19 ⇒ x = 52 − 19 = 33. Kiểm tra: 52 − 33 = 19.", "Example: 52 − x = 19 ⇒ x = 52 − 19 = 33. Check: 52 − 33 = 19."),
            ("×  PHÉP NHÂN", "×  MULTIPLICATION"),
            ("• Giao hoán: a × b = b × a. Đổi chỗ hai thừa số thì tích không thay đổi.", "• Commutative property: a × b = b × a. Swapping the factors does not change the product."),
            ("• Kết hợp: (a × b) × c = a × (b × c). Có thể nhóm các thừa số theo cách thuận tiện.", "• Associative property: (a × b) × c = a × (b × c). Factors may be grouped in a convenient way."),
            ("• Phần tử đơn vị và phần tử hấp thụ: a × 1 = a; a × 0 = 0.", "• Multiplicative identity and zero property: a × 1 = a; a × 0 = 0."),
            ("• Tìm thừa số chưa biết: x × b = c thì x = c ÷ b, với b ≠ 0.", "• To find an unknown factor: if x × b = c, then x = c ÷ b, where b ≠ 0."),
            ("Ví dụ: x × 7 = 56 ⇒ x = 56 ÷ 7 = 8. Kiểm tra: 8 × 7 = 56.", "Example: x × 7 = 56 ⇒ x = 56 ÷ 7 = 8. Check: 8 × 7 = 56."),
            ("÷  PHÉP CHIA", "÷  DIVISION"),
            ("• Không giao hoán: a ÷ b thường khác b ÷ a. Phép chia cũng không có tính kết hợp.", "• Not commutative: a ÷ b is generally different from b ÷ a. Division is also not associative."),
            ("• Tính chất cơ bản: a ÷ 1 = a; a ÷ a = 1 với a ≠ 0; 0 ÷ a = 0 với a ≠ 0.", "• Basic properties: a ÷ 1 = a; a ÷ a = 1 when a ≠ 0; 0 ÷ a = 0 when a ≠ 0."),
            ("• Không bao giờ được chia cho 0. Mọi giá trị làm số chia hoặc mẫu số bằng 0 đều không hợp lệ.", "• Never divide by 0. Any value that makes a divisor or denominator equal to 0 is invalid."),
            ("• Tìm số bị chia: x ÷ b = c thì x = c × b, với b ≠ 0. Tìm số chia trong dạng cơ bản a ÷ x = c thì x = a ÷ c, đồng thời x ≠ 0 và c ≠ 0.", "• To find the dividend: if x ÷ b = c, then x = c × b, where b ≠ 0. In the basic form a ÷ x = c, the divisor is x = a ÷ c, with x ≠ 0 and c ≠ 0."),
            ("Ví dụ: 72 ÷ x = 9 ⇒ x = 72 ÷ 9 = 8. Kiểm tra: 72 ÷ 8 = 9.", "Example: 72 ÷ x = 9 ⇒ x = 72 ÷ 9 = 8. Check: 72 ÷ 8 = 9."),
            ("Ghi nhớ: Phép cộng và phép trừ là hai phép tính ngược nhau; phép nhân và phép chia cũng là hai phép tính ngược nhau. Sau khi tìm được x, luôn thay x vào phép tính ban đầu để kiểm tra và loại mọi giá trị vi phạm điều kiện chia cho 0.", "Remember: Addition and subtraction are inverse operations; multiplication and division are also inverse operations. After finding x, substitute it into the original equation and reject any value that violates the division-by-zero restriction."),
            ("PHÉP TÍNH PHÂN SỐ", "FRACTION OPERATIONS"),
            ("Cộng • Trừ • Nhân • Chia • Quy đồng", "Add • Subtract • Multiply • Divide • Common denominator"),
            ("Quy đồng", "Common denominator"),
            ("2. Nhập các phân số", "2. Enter the fractions"),
            ("Phân số thứ nhất", "First fraction"),
            ("Phân số thứ hai", "Second fraction"),
            ("Tử số", "Numerator"),
            ("Mẫu số", "Denominator"),
            ("Và", "and"),
            ("Tử số và mẫu số chỉ được nhập số nguyên; không được dùng dấu chấm (.) hoặc dấu phẩy (,).", "Numerators and denominators must be integers; decimal points and commas are not allowed."),
            ("BẢNG CỬU CHƯƠNG", "TIMES TABLES"),
            ("Bảng nhân và bảng chia từ 1 đến 20", "Multiplication and division tables from 1 to 20"),
            ("1. Chọn loại bảng", "1. Choose table type"),
            ("Chọn bảng nhân hoặc bảng chia.", "Choose multiplication or division tables."),
            ("×  Bảng nhân", "×  Multiplication"),
            ("÷  Bảng chia", "÷  Division"),
            ("2. Phạm vi hiển thị", "2. Display range"),
            ("Chọn nhóm cơ bản, nâng cao hoặc hiển thị toàn bộ.", "Choose the basic group, advanced group, or show all."),
            ("Bảng 1 – 10", "Tables 1–10"),
            ("10 bảng cơ bản", "10 basic tables"),
            ("Bảng 11 – 20", "Tables 11–20"),
            ("10 bảng nâng cao", "10 advanced tables"),
            ("Tất cả 1 – 20", "All 1–20"),
            ("Hiển thị đầy đủ 20 bảng", "Show all 20 tables"),
            ("3. Danh sách bảng cửu chương", "3. Times-table list"),
            ("Mỗi ô hiển thị đầy đủ 10 phép tính.", "Each card shows all 10 calculations."),
            ("CÀI ĐẶT GIAO DIỆN", "INTERFACE SETTINGS"),
            ("Phong cách hiển thị", "Appearance"),
            ("Theo hệ thống sẽ tự chuyển sáng hoặc tối theo thiết bị.", "System mode follows the device light or dark appearance."),
            ("Hệ thống", "System"),
            ("Sáng", "Light"),
            ("Tối", "Dark"),
            ("Màu chủ đề", "Accent color"),
            ("Áp dụng", "Apply"),
            ("Xem trước màu chủ đề", "Accent color preview"),
            ("Phông chữ", "Font"),
            ("Xem trước phông chữ", "Font preview"),
            ("Aa Bb Cc — 0123456789 — Toán học", "Aa Bb Cc — 0123456789 — Mathematics"),
            ("Khôi phục mặc định", "Restore defaults"),
            ("Khôi phục", "Restore"),
            ("Ngôn ngữ", "Language"),
            ("Tiếng Việt", "Vietnamese"),
            ("Tiếng Anh", "English"),
            ("Tùy chỉnh màu…", "Customize color…"),
            ("Đo sức mạnh", "Performance"),
            ("Phần cứng và hiệu năng tính toán", "Hardware and calculation performance"),
            ("Thông tin phần cứng và hiệu năng tính toán", "Hardware information and calculation performance"),
            ("Thông tin thiết bị", "Device information"),
            ("Tên thiết bị", "Device name"),
            ("Mẫu thiết bị", "Device model"),
            ("Nhà sản xuất", "Manufacturer"),
            ("Nền tảng", "Platform"),
            ("Phiên bản hệ điều hành", "Operating system version"),
            ("Môi trường thiết bị", "Device environment"),
            ("Màn hình", "Display"),
            ("Bộ xử lý", "Processor"),
            ("Tên bộ vi xử lý", "Processor name"),
            ("Xung nhịp CPU", "CPU clock speed"),
            ("Kiến trúc CPU", "CPU architecture"),
            ("Kiến trúc hệ điều hành", "Operating system architecture"),
            ("Số lõi vật lý", "Physical cores"),
            ("Số luồng logic", "Logical processors"),
            ("Tập lệnh SIMD", "SIMD instruction set"),
            ("Độ rộng vector", "Vector width"),
            ("Bộ nhớ khả dụng cho tiến trình", "Memory available to the process"),
            ("Tăng tốc tính toán", "Calculation acceleration"),
            ("Tăng tốc phần cứng", "Hardware acceleration"),
            ("Bật để dùng SIMD; tắt để dùng chế độ Scalar thông thường.", "Enable SIMD; disable it to use the regular Scalar mode."),
            ("Chế độ xử lý hiện tại", "Current processing mode"),
            ("SIMD chỉ được bật khi CPU và .NET Runtime của thiết bị hỗ trợ.", "SIMD is enabled only when supported by the device CPU and .NET Runtime."),
            ("Đang dùng SIMD.", "SIMD is active."),
            ("Đang dùng Scalar.", "Scalar mode is active."),
            ("Thiết bị không hỗ trợ SIMD. Ứng dụng sẽ dùng Scalar.", "This device does not support SIMD. The app will use Scalar mode."),
            ("Chế độ xử lý: {0}", "Processing mode: {0}"),
            ("Không được hỗ trợ", "Not supported"),
            ("Đa luồng", "Multithreading"),
            ("Bật để BigInteger và các tác vụ độc lập sử dụng nhiều luồng CPU.", "Enable multiple CPU threads for BigInteger and other independent workloads."),
            ("Chế độ số thực", "Floating-point mode"),
            ("Chế độ BigInteger", "BigInteger mode"),
            ("Số thực luôn chạy một luồng để so sánh SIMD và Scalar công bằng. BigInteger không dùng SIMD.", "Floating-point always uses one thread for a fair SIMD versus Scalar comparison. BigInteger does not use SIMD."),
            ("SIMD • 1 luồng", "SIMD • 1 thread"),
            ("Scalar • 1 luồng", "Scalar • 1 thread"),
            ("Đa luồng ({0} luồng)", "Multithreaded ({0} threads)"),
            ("Đơn luồng", "Single-threaded"),
            ("Đang dùng {0} luồng CPU.", "Using {0} CPU threads."),
            ("Đang dùng một luồng CPU.", "Using one CPU thread."),
            ("Thiết bị chỉ có một luồng logic.", "This device has only one logical processor."),
            ("Đo SIMD hoặc Scalar trên số thực một luồng và đo BigInteger đơn luồng hoặc đa luồng.", "Measure SIMD or Scalar floating-point performance on one thread and BigInteger performance with one or multiple threads."),
            ("Số thực: {0} • 1 luồng", "Floating point: {0} • 1 thread"),
            ("BigInteger: {0}", "BigInteger: {0}"),
            ("Số thực: {0:N1} triệu phép tính/giây ({1:N0} ms)", "Floating point: {0:N1} million operations/second ({1:N0} ms)"),
            ("Số nguyên lớn: {0:N0} phép tính/giây ({1:N0} ms)", "Big integers: {0:N0} operations/second ({1:N0} ms)"),
            ("ĐO SỨC MẠNH", "PERFORMANCE"),
            ("Đo hiệu năng Int32, Int64, Float và Double", "Benchmark Int32, Int64, Float, and Double performance"),
            ("Bật để Float và Double dùng SIMD trong benchmark.", "Enable SIMD for Float and Double benchmarks."),
            ("Bật để benchmark Float, Double, Int32 và Int64 sử dụng nhiều luồng CPU.", "Enable multiple CPU threads for Float, Double, Int32, and Int64 benchmarks."),
            ("Chế độ Float / Double", "Float / Double mode"),
            ("Chế độ Int32 / Int64", "Int32 / Int64 mode"),
            ("Float và Double dùng SIMD khi bật. Int32 và Int64 luôn dùng Scalar. Đa luồng chỉ áp dụng cho benchmark.", "Float and Double use SIMD when enabled. Int32 and Int64 always use Scalar. Multithreading applies only to the benchmark."),
            ("Mỗi kiểu Int32, Int64, Float và Double được đo trong 10 giây; kết quả cao nhất của từng kiểu được dùng để tính điểm tổng thể.", "Each Int32, Int64, Float, and Double test runs for 10 seconds; the best result from each type is used for the overall score."),
            ("Bài đo kéo dài khoảng 40 giây, chưa tính thời gian khởi động ngắn giữa các kiểu dữ liệu.", "The benchmark takes about 40 seconds, excluding short warm-up time between data types."),
            ("Thiết bị không hỗ trợ SIMD. Float và Double sẽ dùng Scalar.", "This device does not support SIMD. Float and Double will use Scalar."),
            ("Float và Double đang dùng SIMD.", "Float and Double are using SIMD."),
            ("Float và Double đang dùng Scalar.", "Float and Double are using Scalar."),
            ("Benchmark đang dùng {0} luồng CPU.", "The benchmark is using {0} CPU threads."),
            ("Benchmark đang dùng một luồng CPU.", "The benchmark is using one CPU thread."),
            ("{0} + đa luồng ({1} luồng)", "{0} + multithreaded ({1} threads)"),
            ("{0} + đơn luồng", "{0} + single-threaded"),
            ("Scalar + đa luồng ({0} luồng)", "Scalar + multithreaded ({0} threads)"),
            ("Scalar + đơn luồng", "Scalar + single-threaded"),
            ("Đang đo {0} ({1}/4) • 10 giây…", "Benchmarking {0} ({1}/4) • 10 seconds…"),
            ("Đã hủy đo sức mạnh.", "Benchmark cancelled."),
            ("Float / Double: {0}", "Float / Double: {0}"),
            ("Int32 / Int64: {0}", "Int32 / Int64: {0}"),
            ("Int32: {0:N1} triệu phép tính/giây", "Int32: {0:N1} million operations/second"),
            ("Int64: {0:N1} triệu phép tính/giây", "Int64: {0:N1} million operations/second"),
            ("Float: {0:N1} triệu phép tính/giây", "Float: {0:N1} million operations/second"),
            ("Double: {0:N1} triệu phép tính/giây", "Double: {0:N1} million operations/second"),
            ("Tổng thời gian: {0:N1} giây", "Total time: {0:N1} seconds"),
            ("Tập lệnh SIMD benchmark", "Benchmark SIMD instruction set"),
            ("Chỉ các tập lệnh CPU đang hỗ trợ mới được hiển thị.", "Only instruction sets supported by the CPU are shown."),
            ("Float và Double đang dùng {0}.", "Float and Double are using {0}."),
            ("Dừng đo sức mạnh", "Stop benchmark"),
            ("Xác nhận dừng", "Confirm stop"),
            ("Bạn có muốn dừng trình đo sức mạnh không?", "Do you want to stop the benchmark?"),
            ("Đang dừng đo sức mạnh…", "Stopping benchmark…"),
            ("{0} giây", "{0} seconds"),
            ("Đang đo {0} ({1}/4)", "Benchmarking {0} ({1}/4)"),
            ("Mỗi kiểu được đo liên tiếp trong 10 giây và lấy kết quả cao nhất. Float/Double hiển thị MFLOPS/GFLOPS; Int32/Int64 hiển thị MOPS/GOPS.", "Each type is measured continuously for 10 seconds, and the best result is retained. Float/Double use MFLOPS/GFLOPS; Int32/Int64 use MOPS/GOPS."),
            ("Int32: {0:N1} MOPS • {1:N3} GOPS", "Int32: {0:N1} MOPS • {1:N3} GOPS"),
            ("Int64: {0:N1} MOPS • {1:N3} GOPS", "Int64: {0:N1} MOPS • {1:N3} GOPS"),
            ("Float: {0:N1} MFLOPS • {1:N3} GFLOPS", "Float: {0:N1} MFLOPS • {1:N3} GFLOPS"),
            ("Double: {0:N1} MFLOPS • {1:N3} GFLOPS", "Double: {0:N1} MFLOPS • {1:N3} GFLOPS"),
            ("Đo hiệu năng phép tính", "Calculation benchmark"),
            ("Đo tốc độ xử lý số thực và số nguyên lớn trên thiết bị này.", "Measure floating-point and big-integer calculation speed on this device."),
            ("Chạy đo sức mạnh", "Run benchmark"),
            ("Chưa chạy đo sức mạnh.", "The benchmark has not been run."),
            ("Đang đo sức mạnh xử lý…", "Measuring calculation performance…"),
            ("Đo sức mạnh hoàn tất.", "Benchmark completed."),
            ("Không thể chạy đo sức mạnh trên thiết bị này.", "The benchmark could not run on this device."),
            ("Điểm tham khảo", "Reference score"),
            ("Số thực", "Floating point"),
            ("Số nguyên lớn", "Big integers"),
            ("Tổng thời gian", "Total time"),
            ("Số thực: {0:N1} triệu phép tính/giây", "Floating point: {0:N1} million operations/second"),
            ("Số nguyên lớn: {0:N0} phép tính/giây", "Big integers: {0:N0} operations/second"),
            ("Tổng thời gian: {0:N0} ms", "Total time: {0:N0} ms"),
            ("Kết quả chỉ nên so sánh trong cùng phiên bản Math Solver.", "Results should only be compared within the same Math Solver version."),
            ("Máy tính để bàn", "Desktop"),
            ("Điện thoại", "Phone"),
            ("Máy tính bảng", "Tablet"),
            ("Đồng hồ", "Watch"),
            ("Thiết bị thật", "Physical device"),
            ("Thiết bị ảo", "Virtual device"),
            ("Có", "Yes"),
            ("Không", "No"),
            ("Cài đặt nâng cao", "Advanced settings"),
            ("Đặt lại", "Reset"),
            ("Đóng", "Close"),
            ("Chủ đề", "Theme"),
            ("Mặc định hệ thống", "System default"),
            ("Hình vuông", "Square"),
            ("Hình chữ nhật", "Rectangle"),
            ("Hình tam giác", "Triangle"),
            ("Hình tam giác vuông", "Right triangle"),
            ("Hình tam giác đều", "Equilateral triangle"),
            ("Hình tròn", "Circle"),
            ("Hình thang", "Trapezoid"),
            ("Hình thang cân", "Isosceles trapezoid"),
            ("Hình thang vuông", "Right trapezoid"),
            ("Hình thoi", "Rhombus"),
            ("Hình bình hành", "Parallelogram"),
            ("Hình lập phương", "Cube"),
            ("Hình hộp chữ nhật", "Rectangular prism"),
            ("Hình cầu", "Sphere"),
            ("Hình trụ", "Cylinder"),
            ("Hình nón", "Cone"),
            ("Vô số nghiệm", "Infinitely many solutions"),
            ("Không có nghiệm", "No solution"),
            ("Không thể giải", "Cannot solve"),
            ("Phép tính không xác định", "Undefined operation"),
            ("Cài đặt", "Settings"),
            ("Tùy chỉnh nhanh Math Solver", "Quickly customize Math Solver"),
            ("Khôi phục cài đặt mặc định", "Restore default settings"),
            ("Màu HEX, RGB và xem trước", "HEX, RGB, and preview"),
            ("Chọn phong cách sáng, tối và bất kỳ màu chủ đề nào cho Math Solver", "Choose light or dark appearance and any accent color for Math Solver"),
            ("Chọn màu có sẵn, nhập mã HEX hoặc điều chỉnh ba kênh RGB.", "Choose a preset, enter a HEX code, or adjust the RGB channels."),
            ("Chọn phông chữ dùng cho toàn bộ giao diện Math Solver.", "Choose the font used throughout Math Solver."),
            ("Chọn ngôn ngữ dùng cho toàn bộ giao diện và lời giải.", "Choose the language for the entire interface and solutions."),
            ("Đưa ứng dụng về màu tím, phông Open Sans và phong cách theo hệ thống.", "Restore the purple accent, Open Sans font, and system appearance."),
            ("Màu tím", "Purple"),
            ("Màu xanh dương", "Blue"),
            ("Màu xanh ngọc", "Cyan"),
            ("Màu xanh lá", "Green"),
            ("Màu cam", "Orange"),
            ("Màu hồng", "Pink"),
            ("Nhập số nguyên; dấu phẩy phân nhóm hàng nghìn được thêm tự động, ví dụ: 1,000 hoặc -25,000.", "Enter an integer; thousands separators are added automatically, for example 1,000 or -25,000."),
            ("Khu vực này sẽ hỗ trợ hình vuông, hình chữ nhật, tam giác, hình tròn, hình thang và các hình học khác.", "This area will support squares, rectangles, triangles, circles, trapezoids, and other shapes."),
            ("Tìm số hạng chưa biết", "Find an unknown addend"),
            ("Tìm số bị trừ", "Find the minuend"),
            ("Tìm số trừ", "Find the subtrahend"),
            ("Tìm thừa số chưa biết", "Find an unknown factor"),
            ("Tìm số bị chia", "Find the dividend"),
            ("Tìm số chia", "Find the divisor"),
            ("Bạn đang chọn chế độ số nguyên.", "Integer mode is selected."),
            ("Màu không hợp lệ. Hãy nhập dạng #RRGGBB, ví dụ #6D28D9.", "Invalid color. Enter #RRGGBB, for example #6D28D9."),
            ("Chọn ngôn ngữ dùng cho toàn bộ giao diện và lời giải.", "Choose the language for the entire interface and solutions."),
            ("0 ÷ a = 0, với a ≠ 0", "0 ÷ a = 0, where a ≠ 0"),
            ("a ÷ a = 1, với a ≠ 0", "a ÷ a = 1, where a ≠ 0"),
            ("Nhập các giá trị đã biết bằng số nguyên. Nếu x không phải số nguyên, ứng dụng vẫn giữ kết quả chính xác dưới dạng phân số.", "Enter the known values as integers. If x is not an integer, the app keeps the exact result as a fraction."),
            ("Khi một thừa số bằng 0, tích luôn bằng 0.", "When one factor is 0, the product is always 0."),
            ("Đẳng thức đúng với mọi giá trị của x.", "The equality is true for every value of x."),
            ("Không có số nào nhân với 0 mà cho kết quả khác 0.", "No number multiplied by 0 can produce a nonzero result."),
            ("Vế trái luôn bằng 0 nên không thể bằng vế phải.", "The left side is always 0, so it cannot equal the right side."),
            ("Phương trình có dạng x ÷ 0 nên phép chia không được xác định.", "The equation has the form x ÷ 0, so the division is undefined."),
            ("0 chia cho mọi số khác 0 đều bằng 0.", "Zero divided by any nonzero number is 0."),
            ("Phương trình 0 ÷ x = 0 đúng với mọi x khác 0.", "The equation 0 ÷ x = 0 is true for every nonzero x."),
            ("Một số khác 0 chia cho một số hữu hạn khác 0 không thể bằng 0.", "A nonzero number divided by a finite nonzero number cannot equal 0."),
            ("Số chia x phải khác 0.", "The divisor x must not be 0."),
            ("Mẫu số chung nhỏ nhất là bội chung nhỏ nhất của hai mẫu số.", "The least common denominator is the least common multiple of the two denominators."),
            ("Vì phép tính có số thập phân nên kết quả được trình bày theo dạng số thập phân.", "Because the calculation contains decimals, the result is displayed as a decimal."),
            ("Tử số và mẫu số chỉ được nhập số nguyên trong phạm vi từ −170,141,183,460,469,231,731,687,303,715,884,105,728 đến 170,141,183,460,469,231,731,687,303,715,884,105,727; không được dùng dấu chấm (.) hoặc dấu phẩy (,).", "Numerators and denominators must be integers within the range from −170,141,183,460,469,231,731,687,303,715,884,105,728 to 170,141,183,460,469,231,731,687,303,715,884,105,727; decimal points and commas are not allowed."),
            ("Kết quả vượt quá phạm vi của kiểu decimal.", "The result exceeds the range of the decimal type."),
            ("Sai khác nhỏ xuất hiện do giới hạn làm tròn của decimal.", "The small difference is caused by decimal rounding limits."),
            ("Sau đó thực hiện phép chia đặt tính như chia số tự nhiên.", "Then perform long division as with whole numbers.")
        ];

        foreach ((string vietnamese, string english)
                 in pairs)
        {
            map[vietnamese] =
                english;
        }

        return map;
    }

    private static IEnumerable<KeyValuePair<string, string>> BuildPhraseMap()
    {
        (string Vietnamese, string English)[] pairs =
        [
            ("Phương trình có dạng ax² + bx + c = 0.", "The equation has the form ax² + bx + c = 0."),
            ("Ta có:", "Given:"),
            ("Phương trình:", "Equation:"),
            ("Phép cộng có tính giao hoán và kết hợp.", "Addition is commutative and associative."),
            ("Hai số nguyên được cộng theo đúng giá trị hàng.", "The integers are added by matching place values."),
            ("Đang cộng với 0 nên giá trị không thay đổi.", "Adding 0 leaves the value unchanged."),
            ("Cộng các chữ số cùng hàng từ phải sang trái và nhớ sang hàng kế tiếp khi cần.", "Add matching digits from right to left and carry when necessary."),
            ("Phép trừ là phép toán ngược của phép cộng.", "Subtraction is the inverse of addition."),
            ("Đang trừ đi 0 nên giá trị không thay đổi.", "Subtracting 0 leaves the value unchanged."),
            ("Lấy số bị trừ bớt đi số trừ.", "Subtract the subtrahend from the minuend."),
            ("Trừ các chữ số cùng hàng từ phải sang trái và mượn ở hàng kế tiếp khi cần.", "Subtract matching digits from right to left and borrow when necessary."),
            ("Phép nhân có tính giao hoán, kết hợp và phân phối đối với phép cộng.", "Multiplication is commutative, associative, and distributive over addition."),
            ("Có một thừa số bằng 0 nên tích bằng 0.", "One factor is 0, so the product is 0."),
            ("Có một thừa số bằng 1 nên tích bằng thừa số còn lại.", "One factor is 1, so the product equals the other factor."),
            ("Tích được tạo bởi phép cộng lặp lại theo các hàng.", "The product is formed from repeated partial products by place value."),
            ("Nhân lần lượt từng chữ số rồi cộng các tích riêng đã dịch đúng vị trí.", "Multiply by each digit, shift each partial product, and then add them."),
            ("Phép chia là phép toán ngược của phép nhân.", "Division is the inverse of multiplication."),
            ("Số bị chia được tách thành thương và số dư.", "The dividend is decomposed into a quotient and a remainder."),
            ("Số dư luôn có giá trị tuyệt đối nhỏ hơn số chia.", "The absolute value of the remainder is always smaller than the divisor."),
            ("Phần mềm được tạo ra nhằm hỗ trợ học tập, kiểm tra kết quả và giúp người học hiểu cách thực hiện phép tính, lời giải.", "This software supports learning, result checking, and understanding calculation steps and solutions."),
            ("Không nên lạm dụng phần mềm để làm thay toàn bộ bài tập hoặc sao chép kết quả mà không tự suy nghĩ.", "Do not use it to replace all your own work or copy answers without thinking."),
            ("Hãy tự làm bài trước, sau đó sử dụng phần mềm để kiểm tra và tìm hiểu những bước mình chưa hiểu.", "Try the problem first, then use the software to check your work and review unfamiliar steps."),
            ("Sau khi tìm được x, ứng dụng luôn thay x trở lại phép tính ban đầu để kiểm tra.", "After finding x, the app substitutes it back into the original equation to verify the answer."),
            ("Nếu phép chia có x ở mẫu số thì x phải khác 0.", "If x is a divisor, x must not equal 0."),
            ("Tử số và mẫu số phải là số nguyên; mẫu số phải khác 0.", "The numerator and denominator must be integers, and the denominator must not be 0."),
            ("Kết quả được xử lý chính xác bằng phân số nội bộ.", "The result is processed exactly using an internal rational representation."),
            ("Nếu x không phải số nguyên, ứng dụng vẫn giữ kết quả chính xác dưới dạng phân số.", "If x is not an integer, the app keeps the exact result as a fraction."),
            ("Chọn x là số hạng thứ nhất hoặc số hạng thứ hai.", "Choose whether x is the first or second addend."),
            ("Vị trí của x quyết định x là số bị trừ hay số trừ.", "The position of x determines whether it is the minuend or subtrahend."),
            ("Chọn x là thừa số thứ nhất hoặc thừa số thứ hai.", "Choose whether x is the first or second factor."),
            ("Vị trí của x quyết định x là số bị chia hay số chia.", "The position of x determines whether it is the dividend or divisor."),
            ("Muốn tìm một số hạng chưa biết, ta lấy tổng trừ đi số hạng đã biết.", "To find an unknown addend, subtract the known addend from the sum."),
            ("Muốn tìm một số hạng, lấy tổng trừ đi số hạng đã biết.", "To find an unknown addend, subtract the known addend from the sum."),
            ("Muốn tìm số bị trừ, ta lấy hiệu cộng với số trừ.", "To find the minuend, add the difference and the subtrahend."),
            ("Muốn tìm số bị trừ, lấy hiệu cộng với số trừ.", "To find the minuend, add the difference and the subtrahend."),
            ("Muốn tìm số trừ, ta lấy số bị trừ trừ đi hiệu.", "To find the subtrahend, subtract the difference from the minuend."),
            ("Muốn tìm số trừ, lấy số bị trừ trừ đi hiệu.", "To find the subtrahend, subtract the difference from the minuend."),
            ("Muốn tìm một thừa số chưa biết, ta lấy tích chia cho thừa số đã biết.", "To find an unknown factor, divide the product by the known factor."),
            ("Muốn tìm một thừa số, lấy tích chia cho thừa số đã biết.", "To find an unknown factor, divide the product by the known factor."),
            ("Muốn tìm số bị chia, ta lấy thương nhân với số chia.", "To find the dividend, multiply the quotient by the divisor."),
            ("Muốn tìm số bị chia, lấy thương nhân với số chia.", "To find the dividend, multiply the quotient by the divisor."),
            ("Muốn tìm số chia, ta lấy số bị chia chia cho thương", "To find the divisor, divide the dividend by the quotient"),
            ("Muốn tìm số chia, lấy số bị chia chia cho thương.", "To find the divisor, divide the dividend by the quotient."),
            ("Bước 1. Xác định thành phần chưa biết và áp dụng quy tắc.", "Step 1. Identify the unknown component and apply the rule."),
            ("Bước 2. Thay các giá trị đã biết:", "Step 2. Substitute the known values:"),
            ("Thay x =", "Substitute x ="),
            ("vào phép tính ban đầu:", "into the original equation:"),
            ("Hai vế bằng nhau nên kết quả tìm được là đúng.", "Both sides are equal, so the solution is correct."),
            ("Vế trái =", "Left side ="),
            ("vế phải =", "right side ="),
            ("Vậy x =", "Therefore x ="),
            ("Không tồn tại giá trị x phù hợp", "No valid value of x exists"),
            ("Mọi x khác 0", "Every x except 0"),
            ("Mọi giá trị của x", "Every value of x"),
            ("Giá trị gần đúng:", "Approximate value:"),
            ("Mẫu số không được bằng 0.", "The denominator must not be 0."),
            ("Số chia phải khác 0.", "The divisor must not be 0."),
            ("Bạn không thể chia cho 0.", "You cannot divide by 0."),
            ("Trong toán học, phép chia cho 0 không được xác định.", "Division by 0 is undefined in mathematics."),
            ("Bạn không thể chia một số cho 0.", "A number cannot be divided by 0."),
            ("Đây là phép chia hết", "This division has no remainder"),
            ("Đây là phép chia có dư", "This division has a remainder"),
            ("Vì số dư bằng 0 nên đây là phép chia hết.", "Because the remainder is 0, the division is exact."),
            ("Ta kiểm tra:", "Check:"),
            ("Ta thực hiện phép chia", "We divide"),
            ("Ta lấy", "Take"),
            ("Vậy kết quả là", "Therefore, the result is"),
            ("Vậy thương là", "Therefore, the quotient is"),
            ("và số dư là", "and the remainder is"),
            ("Thương là", "The quotient is"),
            ("Vì phép tính có số thập phân nên kết quả được trình bày theo dạng số thập phân.", "Because the calculation contains decimals, the result is shown in decimal form."),
            ("Phép cộng có tính chất giao hoán và kết hợp.", "Addition is commutative and associative."),
            ("Số 0 là phần tử trung hòa của phép cộng.", "Zero is the additive identity."),
            ("Phép trừ không có tính chất giao hoán.", "Subtraction is not commutative."),
            ("Phép trừ cũng không có tính chất kết hợp.", "Subtraction is not associative."),
            ("Phép nhân có tính chất giao hoán, kết hợp và phân phối.", "Multiplication is commutative, associative, and distributive."),
            ("Số 1 là phần tử đơn vị; số 0 là phần tử hấp thụ.", "One is the multiplicative identity; zero is absorbing."),
            ("Phép chia không có tính chất giao hoán.", "Division is not commutative."),
            ("Phép chia cũng không có tính chất kết hợp.", "Division is not associative."),
            ("Mọi quy tắc chỉ áp dụng khi số chia khác 0.", "All division rules require a nonzero divisor."),
            ("Tính chất chung", "General properties"),
            ("Trường hợp đang áp dụng", "Current case"),
            ("Minh họa", "Example"),
            ("Quy tắc:", "Rule:"),
            ("Cả hai số hạng đều bằng 0.", "Both addends are 0."),
            ("Đây là trường hợp cộng với số 0.", "This is addition with 0."),
            ("Hai số hạng bằng nhau.", "The two addends are equal."),
            ("Áp dụng tính chất giao hoán.", "Apply the commutative property."),
            ("Đây là trường hợp trừ đi số 0.", "This is subtraction by 0."),
            ("Một số trừ chính nó luôn bằng 0.", "A number minus itself is always 0."),
            ("Lấy 0 trừ một số sẽ được số đối của số đó.", "Subtracting a number from 0 gives its opposite."),
            ("Có thể kiểm tra phép trừ bằng phép cộng.", "Subtraction can be checked using addition."),
            ("Hiệu + số trừ = số bị trừ", "Difference + subtrahend = minuend"),
            ("Đây là trường hợp nhân với số 0.", "This is multiplication by 0."),
            ("Đây là trường hợp nhân với số 1.", "This is multiplication by 1."),
            ("Nhân với −1 sẽ đổi một số thành số đối của nó.", "Multiplying by −1 gives the opposite number."),
            ("Hai thừa số bằng nhau nên đây là một bình phương.", "The factors are equal, so this is a square."),
            ("Số 0 chia cho một số khác 0 luôn bằng 0.", "Zero divided by any nonzero number is 0."),
            ("Một số chia cho 1 vẫn giữ nguyên.", "A number divided by 1 is unchanged."),
            ("Một số khác 0 chia cho chính nó luôn bằng 1.", "A nonzero number divided by itself is 1."),
            ("Chia cho −1 sẽ đổi một số thành số đối của nó.", "Dividing by −1 gives the opposite number."),
            ("Có thể kiểm tra phép chia bằng phép nhân.", "Division can be checked using multiplication."),
            ("Thương × số chia = số bị chia", "Quotient × divisor = dividend"),
            ("Phân số thứ nhất là 0/0 nên không xác định.", "The first fraction is 0/0 and is undefined."),
            ("Mẫu số của phân số thứ nhất phải khác 0.", "The first denominator must not be 0."),
            ("Phân số thứ hai là 0/0 nên không xác định.", "The second fraction is 0/0 and is undefined."),
            ("Mẫu số của phân số thứ hai phải khác 0.", "The second denominator must not be 0."),
            ("Phép toán không được hỗ trợ.", "This operation is not supported."),
            ("Phép tính ban đầu", "Original calculation"),
            ("Kiểm tra mẫu số", "Check denominators"),
            ("Tìm mẫu số chung nhỏ nhất", "Find the least common denominator"),
            ("Mẫu số chung nhỏ nhất là bội chung nhỏ nhất của hai mẫu số.", "The least common denominator is the least common multiple of the denominators."),
            ("Quy đồng phân số thứ nhất", "Convert the first fraction"),
            ("Quy đồng phân số thứ hai", "Convert the second fraction"),
            ("Kết quả sau khi quy đồng", "Result after finding a common denominator"),
            ("Hai phân số lúc này đã có cùng mẫu số.", "The two fractions now have the same denominator."),
            ("Trừ hai phân số", "Subtract the fractions"),
            ("Cộng hai phân số", "Add the fractions"),
            ("Trừ hai tử số", "Subtract the numerators"),
            ("Cộng hai tử số", "Add the numerators"),
            ("Giữ nguyên mẫu số", "Keep the denominator"),
            ("Thực hiện phép nhân", "Multiply"),
            ("Nhân tử với tử, mẫu với mẫu", "Multiply numerator by numerator and denominator by denominator"),
            ("Kết quả phép nhân", "Multiplication result"),
            ("Đảo phân số chia", "Invert the divisor fraction"),
            ("Kết quả phép chia", "Division result"),
            ("Đổi phép chia thành phép nhân với phân số nghịch đảo:", "Change division to multiplication by the reciprocal:"),
            ("Hai phân số ban đầu", "Original fractions"),
            ("Quy đồng mẫu số", "Find a common denominator"),
            ("Kết quả quy đồng", "Common-denominator result"),
            ("Hai phân số sau khi quy đồng là:", "The equivalent fractions are:"),
            ("Phân số có tử số bằng 0 nên kết quả bằng 0.", "The numerator is 0, so the result is 0."),
            ("Rút gọn phân số", "Simplify the fraction"),
            ("Chia cả tử và mẫu cho", "Divide both numerator and denominator by"),
            ("Phân số đã ở dạng tối giản.", "The fraction is already in simplest form."),
            ("BCNN", "LCM"),
            ("ƯCLN", "GCD"),
            ("Chu vi:", "Perimeter:"),
            ("Quan hệ cạnh:", "Side relation:"),
            ("Diện tích xung quanh:", "Lateral surface area:"),
            ("Diện tích toàn phần:", "Total surface area:"),
            ("Diện tích mặt cầu:", "Sphere surface area:"),
            ("Diện tích đáy:", "Base area:"),
            ("Diện tích:", "Area:"),
            ("Thể tích:", "Volume:"),
            ("Hoặc:", "Or:"),
            ("độ dài một cạnh", "side length"),
            ("chiều dài", "length"),
            ("chiều rộng", "width"),
            ("độ dài đáy", "base length"),
            ("độ dài mỗi cạnh", "length of each side"),
            ("hai cạnh còn lại", "the other two sides"),
            ("hai cạnh góc vuông", "the two perpendicular legs"),
            ("cạnh huyền", "hypotenuse"),
            ("Góc giữa a và b bằng 90°", "The angle between a and b is 90°"),
            ("Ba cạnh bằng nhau; ba góc đều bằng 60°", "All three sides are equal, and all three angles are 60°"),
            ("chiều cao tương ứng với đáy", "height corresponding to base"),
            ("bán kính hình cầu", "sphere radius"),
            ("bán kính đáy", "base radius"),
            ("bán kính", "radius"),
            ("đường kính", "diameter"),
            ("hai đáy song song", "two parallel bases"),
            ("hai cạnh bên bằng nhau", "two equal legs"),
            ("cạnh bên vuông góc với hai đáy, đồng thời là chiều cao", "the leg perpendicular to both bases, which is also the height"),
            ("cạnh bên còn lại", "the other leg"),
            ("hai cạnh bên", "two legs"),
            ("hai đường chéo", "two diagonals"),
            ("độ dài cạnh bên", "side length"),
            ("chiều cao", "height"),
            ("đường sinh", "slant height"),
            ("Có 6 mặt là các hình vuông bằng nhau", "It has 6 congruent square faces"),
            ("Sxq gồm 4 mặt bên; Stp gồm cả 6 mặt", "The lateral area includes 4 side faces; total area includes all 6 faces"),
            ("Ví dụ:", "Example:"),
            ("Nhập các giá trị đã biết bằng số nguyên.", "Enter the known values as integers."),
            ("Chỉ được nhập số nguyên", "Only integers are allowed"),
            ("Chỉ được nhập số, một dấu âm ở đầu và một dấu chấm", "Enter digits, an optional leading minus sign, and at most one decimal point"),
            ("Khi một thừa số bằng 0, tích luôn bằng 0.", "When one factor is 0, the product is always 0."),
            ("Phương trình trở thành", "The equation becomes"),
            ("Đẳng thức đúng với mọi giá trị của x.", "The equality is true for every value of x."),
            ("Không có số nào nhân với 0 mà cho kết quả khác 0.", "No number multiplied by 0 can produce a nonzero result."),
            ("Vế trái luôn bằng 0 nên không thể bằng vế phải.", "The left side is always 0, so it cannot equal the right side."),
            ("0 chia cho mọi số khác 0 đều bằng 0.", "Zero divided by any nonzero number is 0."),
            ("Phương trình 0 ÷ x = 0 đúng với mọi x khác 0.", "The equation 0 ÷ x = 0 is true for every nonzero x."),
            ("Một số khác 0 chia cho một số hữu hạn khác 0 không thể bằng 0.", "A nonzero number divided by a finite nonzero number cannot equal 0."),
            ("không có giá trị x hợp lệ.", "has no valid value of x."),
            ("Số chia x phải khác 0.", "The divisor x must not be 0."),
            ("đồng thời số chia phải khác 0.", "and the divisor must not be 0."),
            ("Không thể thực hiện phép tính", "Cannot calculate"),
            ("Vì phép tính có số thập phân nên kết quả được trình bày theo dạng số thập phân.", "Because the calculation contains decimals, the result is displayed as a decimal."),
            ("vượt quá phạm vi số mà ứng dụng đang hỗ trợ.", "exceeds the numeric range supported by the app."),
            ("cho kết quả cần nhiều hơn", "produces a result requiring more than"),
            ("chữ số sau dấu chấm", "digits after the decimal point"),
            ("không thể biểu diễn chính xác trong giới hạn hiện tại", "cannot be represented exactly within the current limits"),
            ("Ứng dụng không làm tròn kết quả để tránh sai lệch.", "The app does not round the result, to avoid loss of accuracy."),
            ("tối đa", "at most"),
            ("dấu phẩy phân nhóm hàng nghìn được thêm tự động", "thousands separators are added automatically"),
            ("Dấu phẩy phân nhóm được ứng dụng thêm tự động.", "Thousands separators are added automatically."),
            ("Khi chia đến phần thập phân của số bị chia, ta viết dấu phẩy vào thương rồi tiếp tục chia.", "When the decimal part of the dividend is reached, place the decimal point in the quotient and continue dividing."),
            ("Ta chuyển dấu phẩy của cả số bị chia và số chia sang phải", "Move the decimal points of both the dividend and divisor to the right by"),
            ("Sau đó thực hiện phép chia đặt tính như chia số tự nhiên.", "Then perform long division as with whole numbers."),
            ("Ta thực hiện", "Perform"),
            ("Hai phân số đã có cùng mẫu số", "The fractions have the same denominator"),
            ("nên không cần quy đồng.", "so no common-denominator conversion is needed."),
            ("Để đổi mẫu số", "To change the denominator"),
            ("ta nhân cả tử và mẫu", "multiply both numerator and denominator"),
            ("hai tử số", "the numerators"),
            ("Mẫu số chung nhỏ nhất", "The least common denominator"),
            ("bội chung nhỏ nhất của hai mẫu số", "the least common multiple of the two denominators"),
            ("số thứ nhất", "the first number"),
            ("số thứ hai", "the second number"),
            ("Giá trị", "The value"),
            ("không phải là số nguyên hợp lệ.", "is not a valid integer."),
            ("không phải là số thập phân hợp lệ.", "is not a valid decimal."),
            ("không phải là số hợp lệ.", "is not a valid number."),
            ("Nhập số nguyên;", "Enter an integer;"),
            ("Số nguyên chỉ được chứa chữ số, một dấu âm ở đầu", "An integer may contain only digits and one leading minus sign"),
            ("Số thập phân chỉ được chứa chữ số, một dấu âm ở đầu", "A decimal may contain only digits and one leading minus sign"),
            ("một dấu chấm", "one decimal point"),
            ("chữ số tổng cộng", "digits in total"),
            ("dấu phẩy được thêm tự động", "thousands separators are added automatically"),
            ("ví dụ:", "for example:"),
            ("phần thập phân", "decimal part"),
            ("chữ số ở phần thập phân", "decimal places"),
            ("sang phải", "to the right"),
            ("Từ 0 ÷ x", "From 0 ÷ x"),
            ("phép biến đổi hình thức", "the formal transformation"),
            ("cho x = 0 nhưng x = 0 lại làm phép chia không xác định.", "gives x = 0, but x = 0 makes the division undefined."),
            ("đồng thời", "and"),
            ("phải khác 0", "must not be 0"),
            ("hai phân số", "the two fractions"),
            ("tử và mẫu", "numerator and denominator"),
            ("của hai mẫu số", "of the two denominators"),
            ("Vui lòng nhập", "Please enter"),
            ("không phải là một số nguyên hợp lệ.", "is not a valid integer."),
            ("không phải là số thập phân hợp lệ.", "is not a valid decimal."),
            ("không phải là một số hợp lệ.", "is not a valid number."),
            ("không phải là số hợp lệ.", "is not a valid number."),
            ("Không thể chia cho", "Cannot divide by"),
            ("vì phân số này bằng 0.", "because this fraction equals 0."),
            ("không xác định", "undefined"),
            ("Không xác định", "Undefined"),
            ("Kết quả", "Result"),
            ("Phép tính", "Calculation"),
            ("Số bị trừ", "Minuend"),
            ("Số trừ", "Subtrahend"),
            ("Thừa số đã biết", "Known factor"),
            ("Số bị chia", "Dividend"),
            ("Số chia", "Divisor"),
            ("Giá trị đã biết", "Known value"),
            ("Hiệu", "Difference"),
            ("Tích", "Product"),
            ("phép cộng", "addition"),
            ("phép trừ", "subtraction"),
            ("phép nhân", "multiplication"),
            ("phép chia", "division"),
            (" và ", " and "),
            (" dư ", " remainder "),
            ("Vậy ", "Therefore, ")
        ];

        foreach ((string vietnamese, string english)
                 in pairs)
        {
            yield return
                new KeyValuePair<string, string>(
                    vietnamese,
                    english);
        }
    }
}