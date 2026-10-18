using MathSolver.Controls;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Localization;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;

namespace MathSolver.Views;

/// <summary>Local worksheet selection. Reuses practice catalogues and the illustrated CollectionView chooser.</summary>
internal sealed class QuizWorksheetSettingsView : ContentView
{
    private readonly Picker _problem = new(), _format = new(), _group = new(), _subtype = new(), _measurement = new(), _stars = new();
    private readonly VerticalStackLayout _formatField, _groupField, _subtypeField, _measurementField;
    private readonly Label _formatTitle = new(), _formatHint = new();
    private IReadOnlyList<QuizWorksheetChoices.Subtype> _subtypes = [];
    private GeometryMeasurement?[] _measurements = [];
    private readonly QuestionKnowledgeGroup[] _groups = QuestionLearningProfile.Groups(includeMixed: true);
    private readonly QuizProblemTypeCatalog _catalog = new();
    private bool _updating;
    public QuizWorksheetSettings Settings { get; private set; }
    public event EventHandler? SettingsChanged;

    private static string T(string key)
    {
        string culture = AppLanguageManager.CurrentLanguage == AppLanguage.Vietnamese ? "vi-VN" : "en-US";
        return QuizLocalizationOverrides.TryGetValue(key, culture, out var text) ? text : LocalizationService.TranslateKey(key);
    }

    private static Label Text(string text)
    {
        var label = new Label { Text = text, FontSize = 14, LineBreakMode = LineBreakMode.WordWrap };
        label.SetDynamicResource(Label.TextColorProperty, "WallpaperTextPrimaryColor");
        return label;
    }

    private static VerticalStackLayout Field(Picker picker, string title, string caption, Label? titleLabel = null, bool illustrated = true)
    {
        var heading = titleLabel ?? Text(title);
        heading.Text = title; heading.FontAttributes = FontAttributes.Bold;
        heading.SetDynamicResource(Label.TextColorProperty, "WallpaperTextPrimaryColor");
        var frame = new Border { Content = picker };
        frame.SetDynamicResource(StyleProperty, "SolverInputBorderStyle");
        var field = new VerticalStackLayout { Spacing = 6 };
        field.Add(heading); field.Add(frame);
        if (illustrated) IllustratedQuizPicker.Attach(picker, caption);
        else
        {
            frame.Padding = 0;
            picker.FontSize = 15; picker.FontAttributes = FontAttributes.Bold;
            picker.MinimumHeightRequest = OperatingSystem.IsAndroid() ? 56 : 50;
            picker.HorizontalOptions = picker.VerticalOptions = LayoutOptions.Fill;
            picker.SetDynamicResource(Picker.TextColorProperty, "WallpaperTextPrimaryColor");
#if ANDROID
            var host = new Grid();
            frame.Content = host; host.Add(picker);
            var arrow = new Microsoft.Maui.Controls.Shapes.Polyline
            {
                Points = new PointCollection { new(1, 1), new(6, 6), new(11, 1) },
                StrokeThickness = 1.5, WidthRequest = 12, HeightRequest = 7, Margin = new Thickness(0, 0, 18, 0),
                HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center, InputTransparent = true
            };
            arrow.SetDynamicResource(Microsoft.Maui.Controls.Shapes.Shape.StrokeProperty, "WallpaperTextSecondaryBrush");
            AutomationProperties.SetIsInAccessibleTree(arrow, false);
            host.Add(arrow);
            AndroidPickerVisualHelper.Attach(picker);
#endif
        }
        return field;
    }

    public QuizWorksheetSettingsView(QuizWorksheetSettings settings)
    {
        Settings = settings;
        var content = new VerticalStackLayout { Spacing = 12 };
        content.Add(Field(_stars, T("AiBank.Difficulty"), "Choice.SelectDifficulty"));
        content.Add(Field(_problem, IllustratedQuizPicker.Text("Choice.SelectProblem"), "Choice.SelectProblem"));
        _formatField = Field(_format, "", "Choice.SelectSubtype", _formatTitle, illustrated: false);
        _formatHint.LineBreakMode = LineBreakMode.WordWrap; _formatHint.FontSize = 13;
        _formatHint.SetDynamicResource(Label.TextColorProperty, "WallpaperTextPrimaryColor");
        _formatField.Add(_formatHint); content.Add(_formatField);
        _groupField = Field(_group, T("Learning.Group"), "Choice.SelectGroup");
        _groupField.Add(Text(T("Learning.Hint"))); content.Add(_groupField);
        _subtypeField = Field(_subtype, IllustratedQuizPicker.Text("Choice.SelectSubtype"), "Choice.SelectSubtype");
        content.Add(_subtypeField);
        _measurementField = Field(_measurement, T("Quiz.GeometryMeasurementTitle"), "Choice.SelectSubtype");
        content.Add(_measurementField);
        Content = content;
        _problem.AutomationId = "QuizPdfProblem"; _format.AutomationId = "QuizPdfFormat";
        _group.AutomationId = "QuizPdfGroup"; _subtype.AutomationId = "QuizPdfSubtype";
        _measurement.AutomationId = "QuizPdfMeasurement"; _stars.AutomationId = "QuizPdfDifficulty";
        _problem.SelectedIndexChanged += (_, _) => Change(() => Settings.SelectProblem(_problem.SelectedIndex));
        _stars.SelectedIndexChanged += (_, _) => Change(() => Settings with { Tier = (CurriculumTier)(_stars.SelectedIndex + 1) });
        _format.SelectedIndexChanged += (_, _) =>
        {
            if (_format.SelectedIndex >= 0) Change(() => Settings.SelectWords(_format.SelectedIndex == 1));
        };
        _group.SelectedIndexChanged += (_, _) => Change(() => Settings with { Group = _groups[_group.SelectedIndex] });
        _subtype.SelectedIndexChanged += (_, _) => Change(() => Settings.SelectSubtype(_subtypes[_subtype.SelectedIndex].Request));
        _measurement.SelectedIndexChanged += (_, _) => Change(() => Settings with
            { Request = Settings.Request!.Value with { GeometryMeasurement = _measurements[_measurement.SelectedIndex] } });
        Refresh();
    }

    private void Change(Func<QuizWorksheetSettings> change)
    {
        if (_updating) return;
        Settings = change(); Refresh(); SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void Choices(Picker picker, IEnumerable<string> keys, int index)
    {
        var values = keys.ToArray();
        var labels = values.Select(T).ToArray();
        // Refresh can run inside this picker's own selection event. Replacing
        // identical items queues another reset/selection after _updating exits.
        if (!picker.Items.SequenceEqual(labels)) picker.ItemsSource = labels;
        IllustratedQuizPicker.SetKeys(picker, values);
        if (picker.SelectedIndex != index) picker.SelectedIndex = index;
    }

    private void Refresh()
    {
        _updating = true;
        try
        {
            Choices(_problem, _catalog.Options.Select(o => o.LocalizationKey), Settings.ProblemIndex);
            var difficultyLabels = IllustratedQuizPicker.DifficultyLabels();
            if (!_stars.Items.SequenceEqual(difficultyLabels)) _stars.ItemsSource = difficultyLabels;
            IllustratedQuizPicker.SetTextOptions(_stars);
            if (_stars.SelectedIndex != (int)Settings.Tier - 1) _stars.SelectedIndex = (int)Settings.Tier - 1;
            Choices(_group, _groups.Select(g => "Learning.Group." + g), Math.Max(0, Array.IndexOf(_groups, Settings.Group)));
            _formatField.IsVisible = Settings.CanChooseFormat; _groupField.IsVisible = Settings.CanChooseGroup;
            if (Settings.FormatPrefix.Length > 0)
            {
                _formatTitle.Text = T(Settings.FormatPrefix + ".Mode"); _formatHint.Text = T(Settings.FormatPrefix + ".Hint");
                SemanticProperties.SetDescription(_format, _formatTitle.Text);
                var formats = new[] { T(Settings.FormatPrefix + ".Numeric"), T("FractionBank.WordProblems") };
                if (!_format.Items.SequenceEqual(formats)) _format.ItemsSource = formats;
                _format.SelectedIndex = Settings.UsesWords ? 1 : 0;
            }
            _subtypeField.IsVisible = Settings.Kind.HasValue;
            _measurementField.IsVisible = Settings.Kind == QuizProblemKind.Geometry;
            if (Settings.Kind is { } kind)
            {
                _subtypes = QuizWorksheetChoices.Subtypes(kind);
                var match = Settings.Request!.Value with { GeometryMeasurement = null };
                int index = _subtypes.ToList().FindIndex(o => o.Request == match);
                Choices(_subtype, _subtypes.Select(o => o.Key), Math.Max(0, index));
                if (kind == QuizProblemKind.Geometry)
                {
                    _measurements = new GeometryMeasurement?[] { null }.Concat(
                        GeometryQuizGenerator.GetAvailableMeasurements(match.GeometryShape).Select(m => (GeometryMeasurement?)m)).ToArray();
                    Choices(_measurement, _measurements.Select(m => m.HasValue ? "Quiz.Geometry" + m : "Quiz.SubtypeMixed"),
                        Math.Max(0, Array.IndexOf(_measurements, Settings.Request.Value.GeometryMeasurement)));
                }
            }
        }
        finally { _updating = false; }
    }

    public string Summary()
    {
        var parts = new List<string> { IllustratedQuizPicker.DifficultyLabels()[(int)Settings.Tier - 1],
            T(_catalog.Options[Settings.ProblemIndex].LocalizationKey) };
        if (Settings.CanChooseFormat) parts.Add(T(Settings.UsesWords ? "FractionBank.WordProblems" : Settings.FormatPrefix + ".Numeric"));
        if (Settings.CanChooseGroup) parts.Add(T("Learning.Group." + Settings.Group));
        if (_subtypeField.IsVisible && _subtype.SelectedIndex >= 0) parts.Add(_subtype.Items[_subtype.SelectedIndex]);
        if (_measurementField.IsVisible && _measurement.SelectedIndex > 0) parts.Add(_measurement.Items[_measurement.SelectedIndex]);
        return string.Join(" · ", parts);
    }
}
