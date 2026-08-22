using MathSolver.Graphics;
using MathSolver.Models;
using MathSolver.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace MathSolver.Views;

public partial class AverageFormulaView : ContentView
{
    private const int MinimumValueCount = 2;
    private const int MaximumValueCount = 5;
    private const double InteractiveMaximumValue = 12d;

    private readonly AverageDistributionDrawable _drawable = new();
    private readonly List<double> _values = [2d, 4d, 7d];
    private bool _isSliderSyncInProgress;

    public ObservableCollection<AverageFormulaItem> FormulaItems { get; } = [];

    public AverageFormulaView()
    {
        InitializeComponent();
        BindingContext = this;

        AverageGraphicsView.Drawable = _drawable;
        RefreshLocalization();
        RebuildSliderRows();
        RefreshInteractiveVisual();
    }

    public void RefreshLocalization()
    {
        bool vi = AppLanguageManager.CurrentLanguage == AppLanguage.Vietnamese;

        PageTitleLabel.Text = vi
            ? "TRUNG BÌNH CỘNG"
            : "ARITHMETIC MEAN";
        PageSubtitleLabel.Text = vi
            ? "Chia đều • Công thức • 6 dạng toán thường gặp • Ví dụ minh họa"
            : "Equal sharing • Formulas • 6 common problem types • Worked examples";
        InteractiveTitleLabel.Text = vi
            ? "Minh họa tương tác: chia đều"
            : "Interactive visualization: equal sharing";
        InteractiveSubtitleLabel.Text = vi
            ? "Kéo các thanh trượt để thay đổi lượng ban đầu. Các cột bên phải luôn chia đều về cùng một mức trung bình."
            : "Move the sliders to change the original amounts. The bars on the right redistribute the same total into equal shares.";
        InteractiveMeaningLabel.Text = vi
            ? "x̄ là trung bình cộng: tổng lượng không đổi, chỉ được phân lại thành các phần bằng nhau."
            : "x̄ is the arithmetic mean: the total stays the same and is redistributed into equal shares.";
        RemoveValueButton.Text = vi ? "Gỡ bỏ giá trị" : "Remove value";
        AddValueButton.Text = vi ? "Thêm giá trị" : "Add value";
        SixTypesTitleLabel.Text = vi
            ? "6 dạng toán trung bình cộng"
            : "6 arithmetic-mean problem types";
        SixTypesSubtitleLabel.Text = vi
            ? "Các công thức và ví dụ dùng cùng logic với phần Toán đố → Thuật toán / AI-LLM."
            : "These formulas and examples use the same logic as Math Puzzle → Algorithm / AI-LLM.";

        FormulaItems.Clear();
        foreach (AverageFormulaItem item in BuildFormulaItems(vi))
        {
            FormulaItems.Add(item);
        }

        _drawable.Vietnamese = vi;
        RefreshInteractiveVisual();
    }

    private static IReadOnlyList<AverageFormulaItem> BuildFormulaItems(bool vi)
    {
        if (vi)
        {
            return
            [
                new(
                    AverageQuizType.Direct,
                    "1. Tìm TBC trực tiếp",
                    "x̄ = (x₁ + x₂ + … + xₙ) ÷ n",
                    "Cộng tất cả các giá trị rồi chia cho số lượng giá trị.",
                    "Ví dụ: Ba ngày cửa hàng bán 120, 150, 180 quyển vở. Trung bình mỗi ngày bán bao nhiêu?",
                    "(120 + 150 + 180) ÷ 3 = 450 ÷ 3 = 150 quyển vở."),
                new(
                    AverageQuizType.TotalToAverage,
                    "2. Biết tổng → tìm TBC",
                    "x̄ = S ÷ n",
                    "Biết tổng S và số nhóm/phần tử n thì lấy tổng chia cho số nhóm.",
                    "Ví dụ: 5 lớp trồng tổng cộng 175 cây. Trung bình mỗi lớp trồng bao nhiêu cây?",
                    "175 ÷ 5 = 35 cây mỗi lớp."),
                new(
                    AverageQuizType.AverageToTotal,
                    "3. Biết TBC → tìm tổng",
                    "S = x̄ × n",
                    "Biết trung bình và số lượng phần tử thì lấy trung bình nhân số phần tử để tìm tổng.",
                    "Ví dụ: 4 bạn trung bình có 18 viên bi. Cả 4 bạn có bao nhiêu viên?",
                    "18 × 4 = 72 viên bi."),
                new(
                    AverageQuizType.MissingValue,
                    "4. Biết TBC + các giá trị → tìm số còn thiếu",
                    "x thiếu = x̄ × n − tổng các giá trị đã biết",
                    "Tìm tổng cần có từ TBC trước, rồi trừ tổng những giá trị đã biết.",
                    "Ví dụ: An được 8, 9, 7 điểm. Bài thứ tư cần bao nhiêu điểm để TBC 4 bài là 8?",
                    "Tổng cần có: 8 × 4 = 32. Tổng đã biết: 8 + 9 + 7 = 24. Điểm còn thiếu: 32 − 24 = 8."),
                new(
                    AverageQuizType.IndirectData,
                    "5. Dữ kiện gián tiếp",
                    "Suy ra từng giá trị → x̄ = tổng các giá trị ÷ số giá trị",
                    "Khi đề cho quan hệ hơn/kém thay vì cho trực tiếp tất cả số liệu, cần tìm từng giá trị trước rồi mới tính TBC.",
                    "Ví dụ: Lan có 20 quyển, Mai nhiều hơn Lan 4 quyển, Hoa ít hơn Mai 3 quyển. Trung bình mỗi bạn có bao nhiêu?",
                    "Mai = 24, Hoa = 21. TBC = (20 + 24 + 21) ÷ 3 = 65/3 = 21 2/3 quyển."),
                new(
                    AverageQuizType.TwoGroups,
                    "6. TBC hai nhóm",
                    "x̄ chung = (x̄₁ × n₁ + x̄₂ × n₂) ÷ (n₁ + n₂)",
                    "Không lấy trung bình của hai TBC nếu hai nhóm có số người khác nhau. Phải đổi mỗi nhóm về tổng trước.",
                    "Ví dụ: Nhóm A có 4 bạn, TBC 8 điểm; nhóm B có 6 bạn, TBC 7 điểm. Tìm TBC chung.",
                    "Tổng A = 8 × 4 = 32; tổng B = 7 × 6 = 42. TBC chung = (32 + 42) ÷ 10 = 7.4 điểm.")
            ];
        }

        return
        [
            new(
                AverageQuizType.Direct,
                "1. Find the mean directly",
                "x̄ = (x₁ + x₂ + … + xₙ) ÷ n",
                "Add all values, then divide by the number of values.",
                "Example: A store sells 120, 150 and 180 notebooks over three days. What is the average per day?",
                "(120 + 150 + 180) ÷ 3 = 450 ÷ 3 = 150 notebooks."),
            new(
                AverageQuizType.TotalToAverage,
                "2. Total → mean",
                "x̄ = S ÷ n",
                "When the total S and number of groups n are known, divide the total by the number of groups.",
                "Example: 5 classes plant 175 trees altogether. How many trees per class on average?",
                "175 ÷ 5 = 35 trees per class."),
            new(
                AverageQuizType.AverageToTotal,
                "3. Mean → total",
                "S = x̄ × n",
                "Multiply the mean by the number of values to recover the total.",
                "Example: 4 students have an average of 18 marbles each. How many marbles altogether?",
                "18 × 4 = 72 marbles."),
            new(
                AverageQuizType.MissingValue,
                "4. Mean + known values → missing value",
                "missing = x̄ × n − sum of known values",
                "First find the required total, then subtract the values already known.",
                "Example: Alex scores 8, 9 and 7. What score is needed on the fourth test for an average of 8?",
                "Required total: 8 × 4 = 32. Known total: 8 + 9 + 7 = 24. Missing score: 32 − 24 = 8."),
            new(
                AverageQuizType.IndirectData,
                "5. Indirect data",
                "derive each value → x̄ = total ÷ number of values",
                "When values are described through more/less relationships, derive them first and then compute the mean.",
                "Example: Lan has 20 books, Mai has 4 more than Lan, and Hoa has 3 fewer than Mai. What is the average?",
                "Mai = 24 and Hoa = 21. Mean = (20 + 24 + 21) ÷ 3 = 65/3 = 21 2/3 books."),
            new(
                AverageQuizType.TwoGroups,
                "6. Combined mean of two groups",
                "combined x̄ = (x̄₁ × n₁ + x̄₂ × n₂) ÷ (n₁ + n₂)",
                "Do not simply average the two means when the group sizes differ. Convert each group mean back to a total first.",
                "Example: Group A has 4 students averaging 8 points; group B has 6 students averaging 7 points. Find the combined mean.",
                "A total = 8 × 4 = 32; B total = 7 × 6 = 42. Combined mean = (32 + 42) ÷ 10 = 7.4 points.")
        ];
    }

    private void RebuildSliderRows()
    {
        ValueSliderContainer.Children.Clear();

        for (int index = 0; index < _values.Count; index++)
        {
            int capturedIndex = index;

            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(48) },
                    new ColumnDefinition { Width = GridLength.Star }
                },
                ColumnSpacing = 10
            };

            var name = new Label
            {
                Text = $"x{index + 1}",
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                VerticalTextAlignment = TextAlignment.Center,
                MinimumWidthRequest = 28
            };
            name.SetDynamicResource(Label.TextColorProperty, "WallpaperTextPrimaryColor");

            var value = new Label
            {
                Text = _values[index].ToString("0", CultureInfo.CurrentCulture),
                FontSize = 16,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            };
            value.SetDynamicResource(Label.TextColorProperty, "WallpaperTextSecondaryColor");

            var slider = new Slider
            {
                Minimum = 1d,
                Maximum = InteractiveMaximumValue,
                Value = _values[index],
                HorizontalOptions = LayoutOptions.Fill
            };
            slider.SetDynamicResource(Slider.MinimumTrackColorProperty, "PrimaryColor");
            slider.SetDynamicResource(Slider.ThumbColorProperty, "PrimaryColor");
            slider.SetDynamicResource(Slider.MaximumTrackColorProperty, "WallpaperDividerColor");

            slider.ValueChanged += (_, args) =>
            {
                if (_isSliderSyncInProgress)
                {
                    return;
                }

                double snapped = Math.Clamp(
                    Math.Round(args.NewValue),
                    slider.Minimum,
                    slider.Maximum);

                if (Math.Abs(slider.Value - snapped) > 0.001d)
                {
                    _isSliderSyncInProgress = true;
                    slider.Value = snapped;
                    _isSliderSyncInProgress = false;
                }

                _values[capturedIndex] = snapped;
                value.Text = snapped.ToString("0", CultureInfo.CurrentCulture);
                RefreshInteractiveVisual();
            };

            Grid.SetColumn(name, 0);
            Grid.SetColumn(value, 1);
            Grid.SetColumn(slider, 2);
            row.Children.Add(name);
            row.Children.Add(value);
            row.Children.Add(slider);
            ValueSliderContainer.Children.Add(row);
        }

        RemoveValueButton.IsEnabled = _values.Count > MinimumValueCount;
        AddValueButton.IsEnabled = _values.Count < MaximumValueCount;
    }

    private void OnAddValueClicked(object? sender, EventArgs e)
    {
        if (_values.Count >= MaximumValueCount)
        {
            return;
        }

        double seed = Math.Clamp(
            Math.Round(_values.Average()),
            1d,
            InteractiveMaximumValue);
        _values.Add(seed);
        RebuildSliderRows();
        RefreshInteractiveVisual();
    }

    private void OnRemoveValueClicked(object? sender, EventArgs e)
    {
        if (_values.Count <= MinimumValueCount)
        {
            return;
        }

        _values.RemoveAt(_values.Count - 1);
        RebuildSliderRows();
        RefreshInteractiveVisual();
    }

    private void RefreshInteractiveVisual()
    {
        if (_values.Count == 0 || AverageGraphicsView is null)
        {
            return;
        }

        double total = _values.Sum();
        double average = total / _values.Count;
        bool vi = AppLanguageManager.CurrentLanguage == AppLanguage.Vietnamese;

        _drawable.Values = _values.ToArray();
        _drawable.Average = average;
        _drawable.Vietnamese = vi;
        _drawable.AccentColor = GetResourceColor("PrimaryColor", Color.FromArgb("#A78BFA"));
        _drawable.PrimaryTextColor = GetResourceColor("WallpaperTextPrimaryColor", Colors.White);
        _drawable.SecondaryTextColor = GetResourceColor("WallpaperTextSecondaryColor", Color.FromArgb("#CBD5E1"));

        string sumText = string.Join(
            " + ",
            _values.Select(value => value.ToString("0", CultureInfo.CurrentCulture)));
        InteractiveSumLabel.Text = $"{sumText} = {total:0}";

        int totalInt = (int)Math.Round(total);
        int count = _values.Count;
        string result = BuildFractionResult(totalInt, count);
        InteractiveFormulaView.Expression = $"x̄ = {totalInt}/{count} = {result}";

        AverageGraphicsView.Invalidate();
    }

    private static string BuildFractionResult(int numerator, int denominator)
    {
        if (denominator == 0)
        {
            return "—";
        }

        int gcd = Gcd(Math.Abs(numerator), Math.Abs(denominator));
        int reducedNumerator = numerator / gcd;
        int reducedDenominator = denominator / gcd;

        if (reducedDenominator == 1)
        {
            return reducedNumerator.ToString(CultureInfo.CurrentCulture);
        }

        int whole = reducedNumerator / reducedDenominator;
        int remainder = Math.Abs(reducedNumerator % reducedDenominator);

        return whole == 0
            ? $"{reducedNumerator}/{reducedDenominator}"
            : $"{whole} {remainder}/{reducedDenominator}";
    }

    private static int Gcd(int a, int b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return Math.Max(1, Math.Abs(a));
    }

    private static Color GetResourceColor(string key, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(key, out object? value) == true)
        {
            if (value is Color color)
            {
                return color;
            }

            if (value is SolidColorBrush brush)
            {
                return brush.Color;
            }
        }

        return fallback;
    }
}
