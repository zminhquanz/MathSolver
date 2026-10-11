using CommunityToolkit.Maui.Storage;
using MathSolver.Controls;
using MathSolver.Graphics;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Localization;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using System.Collections.ObjectModel;
using System.Globalization;

namespace MathSolver.Views;

internal sealed class QuizPdfPage : ContentPage
{
    private sealed record PreviewRow(int Number, QuizPrintItem Item);
    private readonly PracticeQuestionFactory _factory;
    private readonly QuizWorksheetSettingsView _settingsForm;
    private readonly Label _selectionSummary = new() { LineBreakMode = LineBreakMode.WordWrap };
    private QuizWorksheetSettings? _worksheetSettings;
    private const ArithmeticQuizMode Mode = ArithmeticQuizMode.Essay;
    private readonly ObservableCollection<PreviewRow> _rows = [];
    private readonly Entry _title = new();
    private readonly Entry _count = new() { Keyboard = Keyboard.Numeric, Text = "20", MaxLength = 3, IsVisible = false };
    private readonly Picker _countPicker = new();
    private readonly Label _status = new() { LineBreakMode = LineBreakMode.WordWrap };
    private readonly Button _create = new(), _stop = new(), _save = new(), _saveAnswers = new(), _share = new(), _shareAnswers = new(), _open = new();
    private readonly Button _change = new();
    private readonly Button _back = new() { AutomationId = "QuizPdfBack" };
    private readonly Label _empty = new();
    private readonly Switch _answers = new() { IsToggled = true };
    private readonly CollectionView _preview = new();
    private readonly LiveWallpaperView _wallpaper = new();
    private readonly Grid _exportActions = new() { ColumnSpacing = 8, RowSpacing = 8 };
    private readonly Grid _fields = new() { ColumnSpacing = 16, RowSpacing = 12 };
    private readonly Grid _generation = new() { ColumnSpacing = 8, RowSpacing = 8 };
    private readonly ActivityIndicator _activity = new() { WidthRequest = 24, HeightRequest = 24, IsVisible = false };
    private Border? _customCountInput;
    private QuizWorksheet? _worksheet;
    private CancellationTokenSource? _run;
    private bool _visible, _busy, _goingBack;
    private int _exportColumns, _exportVisibility;
    private int _fieldColumns, _generationColumns;
    public bool IsIdle => !_busy;

    private string T(string key) => LocalizationService.TranslateKey("QuizPdf." + key);
    private string F(string key, params object[] values) => string.Format(CultureInfo.CurrentCulture, T(key), values);
    private Label Label(string text, bool bold = false)
    {
        var label = new Label { Text = text, FontSize = 14, FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None, LineBreakMode = LineBreakMode.WordWrap };
        label.SetDynamicResource(Microsoft.Maui.Controls.Label.TextColorProperty, "WallpaperTextPrimaryColor"); return label;
    }
    private Button Action(Button button, string key, bool primary = false)
    {
        string text = T(key);
        string icon = key switch
        {
            "ChangeSelection" => "⚙️",
            "Generate" => "📝",
            "Cancel" => "⏹️",
            "SaveQuestions" or "SaveAnswers" => "💾",
            "ShareQuestions" or "ShareAnswers" => "📤",
            "OpenQuestions" => "🖨️",
            "Replace" => "🔄",
            _ => ""
        };
        // Native button text renders emoji without font-image or SVG resources.
        button.Text = string.IsNullOrEmpty(icon) ? text : $"{icon} {text}";
        SemanticProperties.SetDescription(button, text);
        button.SetDynamicResource(StyleProperty, primary ? "SolverPrimaryActionStyle" : "SolverOutlinedActionStyle");
        button.MinimumHeightRequest = 48; button.LineBreakMode = LineBreakMode.WordWrap; return button;
    }
    private static Border Card(View content)
    {
        var border = new Border { Content = content };
        border.SetDynamicResource(StyleProperty, "SolverCardStyle"); return border;
    }
    private static Border Input(View content)
    {
        var border = new Border { Content = content, MinimumHeightRequest = OperatingSystem.IsAndroid() ? 56 : 48 };
        border.SetDynamicResource(StyleProperty, "SolverInputBorderStyle");
        border.SetDynamicResource(BackgroundColorProperty, "SurfaceAltColor");
        border.SetDynamicResource(Border.StrokeProperty, "PrimaryBorderBrush");
        content.HorizontalOptions = LayoutOptions.Fill; content.VerticalOptions = LayoutOptions.Fill;
        return border;
    }
    public QuizPdfPage(QuizWorksheetSettings initialSettings, PracticeQuestionFactory factory)
    {
        _factory = factory;
        _settingsForm = new(initialSettings) { IsVisible = false };
        _selectionSummary.Text = _settingsForm.Summary();
        _selectionSummary.SetDynamicResource(Microsoft.Maui.Controls.Label.TextColorProperty, "WallpaperTextPrimaryColor");
        _settingsForm.SettingsChanged += (_, _) => _selectionSummary.Text = _settingsForm.Summary();
        Title = T("Title"); SetDynamicResource(BackgroundColorProperty, "PageBackgroundColor");
        _title.Text = _title.Placeholder = T("DefaultTitle"); _title.MaxLength = 120;
        _count.Placeholder = T("CountRange");
        _countPicker.ItemsSource = new[] { F("Questions", 10), F("Questions", 20), F("Questions", 30), T("CustomCount") };
        _countPicker.SelectedIndex = 1;
        _countPicker.SelectedIndexChanged += (_, _) =>
        {
            _count.IsVisible = _countPicker.SelectedIndex == 3;
            if (_customCountInput is not null) _customCountInput.IsVisible = _count.IsVisible;
        };
        _create.Clicked += OnCreate; _stop.Clicked += (_, _) => _run?.Cancel();
        _save.Clicked += async (_, _) => await ExportAsync(false, "save");
        _saveAnswers.Clicked += async (_, _) => await ExportAsync(true, "save");
        _share.Clicked += async (_, _) => await ExportAsync(false, "share");
        _shareAnswers.Clicked += async (_, _) => await ExportAsync(true, "share");
        _open.Clicked += async (_, _) => await ExportAsync(false, "open");
        _change.Clicked += (_, _) => _settingsForm.IsVisible = !_settingsForm.IsVisible;
        _back.Clicked += OnBackClicked;
        _answers.Toggled += (_, _) => UpdateActions();
        _title.TextChanged += (_, _) => { if (_worksheet is { } w) _worksheet = w with { Title = CleanTitle() }; };
        var top = new VerticalStackLayout { Spacing = 14, Padding = new Thickness(16, 0, 16, 24), MaximumWidthRequest = 1320, HorizontalOptions = LayoutOptions.Center };
        top.SetBinding(WidthRequestProperty, new Binding(nameof(Width), source: _preview));
        top.Add(Label(T("Description")));
        var settings = new VerticalStackLayout { Spacing = 12 };
        settings.Add(Label(T("SettingsSection"), true)); settings.Add(Label(T("EssayOnly"), true));
        settings.Add(_selectionSummary); settings.Add(Action(_change, "ChangeSelection")); settings.Add(_settingsForm);
        var titleField = new VerticalStackLayout { Spacing = 6 };
        titleField.Add(Label(T("TitleLabel"), true)); titleField.Add(Input(_title));
        var countField = new VerticalStackLayout { Spacing = 6 };
        countField.Add(Label(T("Count"), true));
        var countPickerInput = Input(_countPicker); countPickerInput.Padding = 0; countField.Add(countPickerInput);
        _customCountInput = Input(_count); _customCountInput.IsVisible = false; countField.Add(_customCountInput);
        _fields.Add(titleField); _fields.Add(countField); _fields.SizeChanged += (_, _) => UpdateFormLayout(); settings.Add(_fields);
        _generation.Add(Action(_create, "Generate", true)); _generation.Add(Action(_stop, "Cancel"));
        _generation.SizeChanged += (_, _) => UpdateFormLayout(); settings.Add(_generation);
        _activity.SetDynamicResource(ActivityIndicator.ColorProperty, "PrimaryColor");
        _status.SetDynamicResource(Microsoft.Maui.Controls.Label.TextColorProperty, "WallpaperTextPrimaryColor");
        var progress = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)], ColumnSpacing = 8 };
        progress.Add(_activity, 0); progress.Add(_status, 1); settings.Add(progress); settings.Add(Label(T("FrozenHint")));
        top.Add(Card(settings));
        var export = new VerticalStackLayout { Spacing = 12 };
        export.Add(Label(T("ExportSection"), true));
        var answerChoice = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)] };
        answerChoice.Add(Label(T("IncludeAnswers"), true), 0); answerChoice.Add(_answers, 1); export.Add(answerChoice);
        export.Add(Label(T("AnswersHint"))); export.Add(_exportActions); top.Add(Card(export)); top.Add(Label(T("Preview"), true));
        _empty.Text = T("Empty"); _empty.LineBreakMode = LineBreakMode.WordWrap;
        _empty.SetDynamicResource(Microsoft.Maui.Controls.Label.TextColorProperty, "WallpaperTextPrimaryColor");
        var emptyCard = Card(_empty); emptyCard.SetBinding(IsVisibleProperty, new Binding(nameof(IsVisible), source: _empty)); top.Add(emptyCard);
        var actionButtons = new[] { Action(_save, "SaveQuestions", true), Action(_share, "ShareQuestions"), Action(_saveAnswers, "SaveAnswers"), Action(_shareAnswers, "ShareAnswers"), Action(_open, "OpenQuestions") };
        foreach (var button in actionButtons) _exportActions.Children.Add(button);
        _exportActions.SizeChanged += (_, _) => UpdateExportLayout();
        _preview.ItemsSource = _rows; _preview.SelectionMode = SelectionMode.None; _preview.ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems;
        _preview.BackgroundColor = Colors.Transparent;
        // WinUI lays out EmptyView over the whole CollectionView, including its
        // interactive header. Keep the empty message in the header instead so
        // the initial form remains clickable before any questions exist.
        _preview.Header = top; _preview.ItemTemplate = new DataTemplate(CreatePreview);
        _create.AutomationId = "QuizPdfGenerate"; _status.AutomationId = "QuizPdfStatus";
        _title.AutomationId = "QuizPdfTitle"; _countPicker.AutomationId = "QuizPdfCount";
        _save.AutomationId = "QuizPdfSave";
        var heading = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)], ColumnSpacing = 12,
            Padding = new Thickness(16, 10), MaximumWidthRequest = 1320, HorizontalOptions = LayoutOptions.Center };
        _back.SetDynamicResource(StyleProperty, "RoundPlatformBackButtonStyle");
        InteractiveButtonAnimation.SetIsScopeEnabled(_back, true);
        InteractiveButtonAnimation.SetIsPlatformBackButton(_back, true);
        InteractiveButtonAnimation.SetIsPressAnimationEnabled(_back, false);
        SemanticProperties.SetDescription(_back, LocalizationService.TranslateKey("About.Back.Accessibility"));
        ToolTipProperties.SetText(_back, LocalizationService.TranslateKey("About.Back.Accessibility"));
        var backIcon = new ThemedSvgPathIcon
        {
            Data = "M313-440h487v-80H313l223-223-56-57-320 320 320 320 56-57-223-223Z",
            WidthRequest = 20, HeightRequest = 20, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
            InputTransparent = true
        };
        backIcon.SetBinding(OpacityProperty, new Binding(nameof(Opacity), source: _back));
        AutomationProperties.SetIsInAccessibleTree(backIcon, false);
        var backHost = new Grid { WidthRequest = 48, HeightRequest = 48 };
        backHost.Add(_back); backHost.Add(backIcon); heading.Add(backHost, 0);
        var pageTitle = Label(T("Title"), true); pageTitle.SetDynamicResource(StyleProperty, "SolverSectionTitleStyle");
        pageTitle.VerticalOptions = LayoutOptions.Center; heading.Add(pageTitle, 1);
        var root = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)] };
        heading.SetBinding(WidthRequestProperty, new Binding(nameof(Width), source: root));
        Grid.SetRowSpan(_wallpaper, 2); root.Add(_wallpaper);
        root.Add(heading, 0, 0); root.Add(_preview, 0, 1); Content = root;
        UpdateActions();
    }
    private void UpdateFormLayout()
    {
        int fields = _fields.Width >= 800 ? 2 : 1;
        if (_fieldColumns != fields)
        {
            _fieldColumns = fields;
            _fields.ColumnDefinitions.Clear(); _fields.RowDefinitions.Clear();
            for (int i = 0; i < fields; i++) _fields.ColumnDefinitions.Add(new(GridLength.Star));
            for (int i = 0; i < 2 / fields; i++) _fields.RowDefinitions.Add(new(GridLength.Auto));
            for (int i = 0; i < _fields.Children.Count; i++)
            { Grid.SetRow((BindableObject)_fields.Children[i], i / fields); Grid.SetColumn((BindableObject)_fields.Children[i], i % fields); }
        }
        int actions = _generation.Width >= 480 ? 2 : 1;
        if (_generationColumns == actions) return;
        _generationColumns = actions;
        _generation.ColumnDefinitions.Clear(); _generation.RowDefinitions.Clear();
        _generation.ColumnDefinitions.Add(new(GridLength.Star));
        if (actions == 2) _generation.ColumnDefinitions.Add(new(GridLength.Auto));
        for (int i = 0; i < 2 / actions; i++) _generation.RowDefinitions.Add(new(GridLength.Auto));
        Grid.SetRow(_stop, actions == 1 ? 1 : 0); Grid.SetColumn(_stop, actions == 1 ? 0 : 1);
    }
    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (_goingBack) return;
        _goingBack = true; _run?.Cancel(); _back.IsEnabled = false;
        try { await Navigation.PopAsync(); }
        catch (Exception error)
        { System.Diagnostics.Debug.WriteLine(error); if (_visible) _status.Text = T("Failed"); }
        finally { _goingBack = false; _back.IsEnabled = true; }
    }
    private void UpdateExportLayout()
    {
        var buttons = new[] { _save, _share, _saveAnswers, _shareAnswers, _open };
        int columns = _exportActions.Width >= 620 ? 2 : 1;
        int visibility = 0;
        for (int i = 0; i < buttons.Length; i++) if (buttons[i].IsVisible) visibility |= 1 << i;
        // Changing grid definitions causes another layout pass. Only rebuild
        // when the breakpoint or answer-file choice actually changes.
        if (_exportColumns == columns && _exportVisibility == visibility) return;
        _exportColumns = columns; _exportVisibility = visibility;
        _exportActions.ColumnDefinitions.Clear(); _exportActions.RowDefinitions.Clear();
        for (int i = 0; i < columns; i++) _exportActions.ColumnDefinitions.Add(new(GridLength.Star));
        var visible = buttons.Where(button => button.IsVisible).ToArray();
        for (int i = 0; i < (visible.Length + columns - 1) / columns; i++) _exportActions.RowDefinitions.Add(new(GridLength.Auto));
        for (int i = 0; i < visible.Length; i++)
        {
            Grid.SetRow(visible[i], i / columns); Grid.SetColumn(visible[i], i % columns);
            Grid.SetColumnSpan(visible[i], i == visible.Length - 1 && i % columns == 0 ? columns : 1);
        }
    }
    private string CleanTitle() => string.IsNullOrWhiteSpace(_title.Text) ? T("DefaultTitle") : _title.Text.Trim();
    private sealed class PreviewWidthConverter(double inset) : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // A native CollectionView can initially measure a cell with unlimited
            // width. Constrain it from the viewport, including the card's margins,
            // before measuring prose or a wrapping math expression.
            double viewport = value is double width && double.IsFinite(width) && width > 0 ? width : 320;
            return Math.Max(1, Math.Min(1320, viewport) - 32 - inset);
        }
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
    private void ConstrainPreviewWidth(View view, double inset = 0)
    {
        var converter = new PreviewWidthConverter(inset);
        view.SetBinding(WidthRequestProperty, new Binding(nameof(Width), source: _preview, converter: converter));
        view.SetBinding(MaximumWidthRequestProperty, new Binding(nameof(Width), source: _preview, converter: converter));
    }
    private View CreatePreview()
    {
        var heading = Label("", true);
        var question = new FractionExpressionView { ParseArithmeticExpressions = true, WrapContent = true, MathFontSize = 16 };
        question.SetDynamicResource(FractionExpressionView.MathColorProperty, "WallpaperTextPrimaryColor");
        var facts = Label("");
        var drawing = new GraphicsView { HeightRequest = 250, HorizontalOptions = LayoutOptions.Fill };
        var replace = Action(new Button(), "Replace");
        replace.SetBinding(IsEnabledProperty, new Binding(nameof(IsIdle), source: this));
        var content = new Grid { ColumnDefinitions = [new(GridLength.Star)], RowSpacing = 10 };
        var children = new View[] { heading, question, facts, drawing, replace };
        for (int i = 0; i < children.Length; i++)
        {
            content.RowDefinitions.Add(new(GridLength.Auto)); content.Add(children[i], 0, i);
            ConstrainPreviewWidth(children[i], 34); // 16px padding each side + 1px stroke each side.
        }
        ConstrainPreviewWidth(content, 34);
        var border = new Border { Content = content, Padding = 16, StrokeThickness = 1,
            Margin = new Thickness(16, 0, 16, 12), HorizontalOptions = LayoutOptions.Center };
        ConstrainPreviewWidth(border);
        border.SetDynamicResource(StyleProperty, "SolverCardStyle");
        border.BindingContextChanged += (_, _) =>
        {
            if (border.BindingContext is not PreviewRow row) return;
            var item = row.Item; heading.Text = F("Question", row.Number); question.Expression = item.Prompt;
            facts.Text = item.Table is { } table ? string.Join("\n", table.Rows.Select(r => r.Label + ": " + r.Value)) : "";
            facts.IsVisible = item.Table is not null;
            drawing.IsVisible = item.Visual is not null || item.Diagram is not null;
            drawing.HeightRequest = item.Visual is { Kind: "table" } v ? v.Labels.Count * 64 + 30 : item.Visual is { Kind: "pie" } pie ? 250 + pie.Labels.Count * 22 : 280;
            drawing.Drawable = item.Visual is { } visual ? new ElementaryQuizDrawable(visual) : item.Diagram is { } diagram ? new QuizDiagramDrawable(diagram) : null;
            drawing.Invalidate();
        };
        replace.Clicked += async (_, _) => { if (border.BindingContext is PreviewRow row) await ReplaceAsync(row.Number - 1); };
        return border;
    }
    private void UpdateActions()
    {
        OnPropertyChanged(nameof(IsIdle));
        _create.IsEnabled = _change.IsEnabled = _title.IsEnabled = _count.IsEnabled = _countPicker.IsEnabled = !_busy;
        _settingsForm.IsEnabled = !_busy;
        _stop.IsEnabled = _busy;
        foreach (var button in new[] { _save, _share, _saveAnswers, _shareAnswers, _open }) button.IsEnabled = !_busy && _worksheet is not null;
        _saveAnswers.IsVisible = _shareAnswers.IsVisible = _answers.IsToggled;
        _empty.IsVisible = _worksheet is null;
        _activity.IsRunning = _activity.IsVisible = _busy;
        UpdateFormLayout();
        UpdateExportLayout();
    }
    private int SelectedCount() => _countPicker.SelectedIndex switch { 0 => 10, 1 => 20, 2 => 30, _ => int.TryParse(_count.Text, out var count) ? count : 0 };
    private async void OnCreate(object? sender, EventArgs e)
    {
        if (_busy) return;
        int count = SelectedCount();
        if (count is < QuizWorksheetBuilder.MinimumCount or > QuizWorksheetBuilder.MaximumCount) { _status.Text = T("CountRange"); return; }
        var settings = _settingsForm.Settings;
        string selection = _settingsForm.Summary();
        var catalog = new QuizProblemTypeCatalog();
        await RunAsync(async cancellation =>
        {
            string title = CleanTitle();
            _status.Text = F("Progress", 0, count);
            var progress = new Progress<int>(value => { if (_visible) _status.Text = F("Progress", value, count); });
            var worksheet = await Task.Run(() => QuizWorksheetBuilder.CreateAsync(count, title, selection, settings.Language,
                settings.Tier, Mode, token => settings.GenerateAsync(_factory, catalog, token), progress, cancellation), cancellation);
            cancellation.ThrowIfCancellationRequested();
            _worksheet = worksheet; _worksheetSettings = settings; _rows.Clear();
            for (int i = 0; i < worksheet.Questions.Count; i++) _rows.Add(new(i + 1, worksheet.Questions[i]));
            _status.Text = F("Ready", worksheet.Id, worksheet.Questions.Count);
        });
    }
    private async Task ReplaceAsync(int index)
    {
        if (_busy || _worksheet is null || _worksheetSettings is not { } settings) return;
        var catalog = new QuizProblemTypeCatalog();
        await RunAsync(async cancellation =>
        {
            var existing = _worksheet.Questions.Select(q => q.Identity).ToHashSet();
            for (int attempt = 0; attempt < 200; attempt++)
            {
                // Replacements belong to the displayed set, even after editing the next set's settings.
                var item = await Task.Run(async () => QuizWorksheetBuilder.Format(
                    await settings.GenerateAsync(_factory, catalog, cancellation), settings.Language), cancellation);
                cancellation.ThrowIfCancellationRequested();
                if (existing.Contains(item.Identity)) continue;
                var items = _worksheet.Questions.ToArray(); items[index] = item;
                _worksheet = _worksheet with { Id = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), Questions = Array.AsReadOnly(items) };
                _rows[index] = new(index + 1, item); _status.Text = F("Ready", _worksheet.Id, items.Length); return;
            }
            throw new InvalidOperationException("WorksheetNotEnoughDistinctQuestions");
        });
    }
    private async Task ExportAsync(bool answers, string action)
    {
        if (_busy || _worksheet is null) return;
        var sheet = _worksheet;
        await RunAsync(async cancellation =>
        {
            var bytes = await Task.Run(() => QuizPdfWriter.Write(sheet, answers, cancellation), cancellation);
            cancellation.ThrowIfCancellationRequested();
            string name = $"MathSolver-{sheet.Id}-{(answers ? "answers" : "worksheet")}.pdf";
            if (action == "save")
            {
                using var stream = new MemoryStream(bytes);
                var result = await FileSaver.Default.SaveAsync(name, stream, cancellation);
                if (!result.IsSuccessful) throw result.Exception ?? new IOException("PDF save failed.");
                if (_visible) _status.Text = F("Saved", result.FilePath);
            }
            else
            {
                string directory = Path.Combine(FileSystem.CacheDirectory, "QuizPdf", sheet.Id); Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, name); await File.WriteAllBytesAsync(path, bytes, cancellation);
                if (action == "share") await Share.Default.RequestAsync(new ShareFileRequest { Title = sheet.Title, File = new ShareFile(path, "application/pdf") });
                else if (!await Launcher.Default.OpenAsync(new OpenFileRequest(sheet.Title, new ReadOnlyFile(path, "application/pdf"))))
                    throw new IOException("No PDF viewer available.");
            }
        });
    }
    private async Task RunAsync(Func<CancellationToken, Task> work)
    {
        _busy = true; _run = new CancellationTokenSource(); UpdateActions();
        try { await work(_run.Token); }
        catch (OperationCanceledException) { if (_visible) _status.Text = T("Stopped"); }
        catch (Exception error)
        { System.Diagnostics.Debug.WriteLine(error); if (_visible) _status.Text = T(error.Message == "WorksheetNotEnoughDistinctQuestions" ? "DistinctError" : "Failed"); }
        finally { _run.Dispose(); _run = null; _busy = false; if (_visible) UpdateActions(); }
    }
    protected override void OnAppearing() { base.OnAppearing(); _visible = true; _wallpaper.Resume(); UpdateActions(); }
    protected override void OnDisappearing() { _visible = false; _run?.Cancel(); _wallpaper.Pause(); base.OnDisappearing(); }
}
