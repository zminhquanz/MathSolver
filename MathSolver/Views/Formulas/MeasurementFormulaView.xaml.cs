using MathSolver.Services;

namespace MathSolver.Views;

public partial class MeasurementFormulaView : ContentView
{
    private bool _isUpdatingPickers;

    private MeasurementEngine.MeasurementCategory? _selectedCategory;

    public MeasurementFormulaView()
    {
        InitializeComponent();

        LocalizationService.ExcludeSubtreeFromLegacyTracking(
            this);

        LocalizationService.CultureChanged +=
            OnCultureChanged;

#if ANDROID
        AndroidPickerVisualHelper.Attach(CategoryPicker);
        AndroidPickerVisualHelper.Attach(FromUnitPicker);
        AndroidPickerVisualHelper.Attach(ToUnitPicker);
#endif

        RefreshLocalization();

        CategoryPicker.SelectedIndex = 0;
        ValueEntry.Text = "1";
    }

    public void RefreshLocalization()
    {
        PageTitleLabel.Text = T("Formula.Measurement.Title");
        PageSubtitleLabel.Text = T("Formula.Measurement.Subtitle");
        ConverterTitleLabel.Text = T("Formula.Measurement.ConverterTitle");
        CategoryLabel.Text = T("Formula.Measurement.CategoryLabel");
        ValueLabel.Text = T("Formula.Measurement.ValueLabel");
        FromLabel.Text = T("Formula.Measurement.FromLabel");
        ToLabel.Text = T("Formula.Measurement.ToLabel");
        ResultCaptionLabel.Text = T("Formula.Measurement.ResultLabel");
        ReferenceTitleLabel.Text = T("Formula.Measurement.ReferenceTitle");
        ReferenceHintLabel.Text = T("Formula.Measurement.ReferenceHint");
        TipsTitleLabel.Text = T("Formula.Measurement.TipsTitle");
        TipsTextLabel.Text = T("Formula.Measurement.TipsText");
        ValueEntry.Placeholder = T("Formula.Measurement.ValuePlaceholder");

        int previousCategoryIndex =
            Math.Max(
                0,
                CategoryPicker.SelectedIndex);

        _isUpdatingPickers = true;

        CategoryPicker.ItemsSource =
            MeasurementEngine.Categories
                .Select(category => category.DisplayName)
                .ToList();

        CategoryPicker.SelectedIndex =
            Math.Min(
                previousCategoryIndex,
                MeasurementEngine.Categories.Count - 1);

        _isUpdatingPickers = false;

        LoadSelectedCategory(
            preserveUnitSelection: true);
    }

    private void OnCultureChanged(
        object? sender,
        EventArgs e)
    {
        Dispatcher.Dispatch(
            RefreshLocalization);
    }

    private static string T(
        string key) =>
        LocalizationService.TranslateKey(key);

    private void OnCategoryChanged(
        object? sender,
        EventArgs e)
    {
        if (_isUpdatingPickers)
        {
            return;
        }

        LoadSelectedCategory(
            preserveUnitSelection: false);
    }

    private void LoadSelectedCategory(
        bool preserveUnitSelection)
    {
        int categoryIndex =
            CategoryPicker.SelectedIndex;

        if (categoryIndex < 0 ||
            categoryIndex >= MeasurementEngine.Categories.Count)
        {
            return;
        }

        string? previousFromId =
            preserveUnitSelection
                ? GetSelectedUnit(FromUnitPicker)?.Id
                : null;

        string? previousToId =
            preserveUnitSelection
                ? GetSelectedUnit(ToUnitPicker)?.Id
                : null;

        _selectedCategory =
            MeasurementEngine.Categories[categoryIndex];

        List<string> unitNames =
            _selectedCategory.Units
                .Select(unit => unit.DisplayName)
                .ToList();

        _isUpdatingPickers = true;

        FromUnitPicker.ItemsSource = unitNames;
        ToUnitPicker.ItemsSource = unitNames;

        int fromIndex =
            FindUnitIndex(
                _selectedCategory,
                previousFromId);

        if (fromIndex < 0)
        {
            fromIndex =
                FindUnitIndex(
                    _selectedCategory,
                    _selectedCategory.DefaultFromUnitId);
        }

        if (fromIndex < 0)
        {
            fromIndex = 0;
        }

        int toIndex =
            FindUnitIndex(
                _selectedCategory,
                previousToId);

        if (toIndex < 0)
        {
            toIndex =
                FindUnitIndex(
                    _selectedCategory,
                    _selectedCategory.DefaultToUnitId);
        }

        if (toIndex < 0)
        {
            toIndex =
                fromIndex + 1 < _selectedCategory.Units.Count
                    ? fromIndex + 1
                    : Math.Max(0, fromIndex - 1);
        }

        FromUnitPicker.SelectedIndex = fromIndex;
        ToUnitPicker.SelectedIndex = toIndex;

        _isUpdatingPickers = false;

        ReferenceTextLabel.Text =
            T(_selectedCategory.ReferenceKey);

        RefreshResult();
    }

    private static int FindUnitIndex(
        MeasurementEngine.MeasurementCategory category,
        string? unitId)
    {
        if (string.IsNullOrWhiteSpace(unitId))
        {
            return -1;
        }

        for (int index = 0;
             index < category.Units.Count;
             index++)
        {
            if (string.Equals(
                    category.Units[index].Id,
                    unitId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private MeasurementEngine.MeasurementUnit? GetSelectedUnit(
        Picker picker)
    {
        if (_selectedCategory is null ||
            picker.SelectedIndex < 0 ||
            picker.SelectedIndex >= _selectedCategory.Units.Count)
        {
            return null;
        }

        return _selectedCategory.Units[picker.SelectedIndex];
    }

    private void OnValueTextChanged(
        object? sender,
        TextChangedEventArgs e) =>
        RefreshResult();

    private void OnUnitChanged(
        object? sender,
        EventArgs e)
    {
        if (_isUpdatingPickers)
        {
            return;
        }

        RefreshResult();
    }

    private void OnSwapClicked(
        object? sender,
        EventArgs e)
    {
        if (FromUnitPicker.SelectedIndex < 0 ||
            ToUnitPicker.SelectedIndex < 0)
        {
            return;
        }

        _isUpdatingPickers = true;

        int fromIndex = FromUnitPicker.SelectedIndex;
        FromUnitPicker.SelectedIndex = ToUnitPicker.SelectedIndex;
        ToUnitPicker.SelectedIndex = fromIndex;

        _isUpdatingPickers = false;

        RefreshResult();
    }

    private void RefreshResult()
    {
        ErrorLabel.IsVisible = false;

        MeasurementEngine.MeasurementUnit? from =
            GetSelectedUnit(FromUnitPicker);

        MeasurementEngine.MeasurementUnit? to =
            GetSelectedUnit(ToUnitPicker);

        if (from is null || to is null)
        {
            ResultLabel.Text = "—";
            EquationLabel.Text = string.Empty;
            return;
        }

        if (!MeasurementEngine.TryParseInput(
                ValueEntry.Text,
                out decimal input))
        {
            ResultLabel.Text = "—";
            EquationLabel.Text = string.Empty;

            if (!string.IsNullOrWhiteSpace(ValueEntry.Text))
            {
                ShowError(
                    T("Formula.Measurement.InvalidNumber"));
            }

            return;
        }

        if (!MeasurementEngine.TryConvert(
                input,
                from,
                to,
                out decimal result))
        {
            ResultLabel.Text = "—";
            EquationLabel.Text = string.Empty;
            ShowError(
                T("Formula.Measurement.NumberTooLarge"));
            return;
        }

        string inputText =
            MeasurementEngine.FormatValue(input);

        string resultText =
            MeasurementEngine.FormatValue(result);

        ResultLabel.Text =
            $"{resultText} {to.Symbol}";

        EquationLabel.Text =
            $"{inputText} {from.Symbol} = {resultText} {to.Symbol}";
    }

    private void ShowError(
        string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
