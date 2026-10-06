using CommunityToolkit.Maui.Storage;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace MathSolver.Views;

public partial class AiQuestionBankPage : ContentPage
{
    private readonly AiQuestionBank _bank = AiQuestionBank.Current;
    private bool _updating;
    private bool _appeared;
    private bool _saving;
    private bool _goingBack;
    private bool _followLatestItem = true;
    private int _renderQueued;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly FilePickerFileType GgufType = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        [DevicePlatform.WinUI] = [".gguf"], [DevicePlatform.Android] = ["application/octet-stream", "*/*"],
        [DevicePlatform.iOS] = ["public.data"], [DevicePlatform.MacCatalyst] = ["public.data"]
    });

    public AiQuestionBankPage()
    {
        InitializeComponent();
        QueryEditor.Text = QuestionBankStore.DefaultInquiry;
        LocalizationService.ExcludeSubtreeFromLegacyTracking(this);
        RefreshPickerLabels();
        StarsPicker.SelectedIndex = 2;
        LanguagePicker.SelectedIndex = AppLanguageManager.CurrentLanguage == AppLanguage.Vietnamese ? 0 : 1;
        Render();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _appeared = true;
        Shell.SetTabBarIsVisible(this, true);
        LiveWallpaper.Resume();
        _bank.Generation.Changed += OnStateChanged;
        _bank.Changed += OnStateChanged;
        LocalizationService.CultureChanged += OnCultureChanged;
        DeveloperModeManager.DeveloperModeChanged += OnDeveloperModeChanged;
        var options = _bank.Generation.Snapshot.Options;
        if (options is not null)
        {
            _learningGroup = options.Profile?.Group ?? QuestionKnowledgeGroup.Objects;
            _family = options.Family;
            _unknownRole = options.UnknownRole;
            _updating = true;
            ProblemPicker.SelectedIndex = (int)_family;
            _updating = false;
            RefreshLearningPickers(options.Operation);
            StarsPicker.SelectedIndex = (int)options.Tier - 1;
            LanguagePicker.SelectedIndex = options.Language == AppLanguage.Vietnamese ? 0 : 1;
            BatchModePicker.SelectedIndex = options.Count > 1 ? 1 : 0;
            CountEntry.Text = options.Count.ToString(CultureInfo.InvariantCulture);
            AutoInsertSwitch.IsToggled = options.AutoInsert;
            RefreshFindXRoles();
        }
        Render();
    }

    protected override void OnDisappearing()
    {
        _appeared = false;
        _bank.Generation.Changed -= OnStateChanged;
        _bank.Changed -= OnStateChanged;
        LocalizationService.CultureChanged -= OnCultureChanged;
        DeveloperModeManager.DeveloperModeChanged -= OnDeveloperModeChanged;
        ModelBusyIndicator.IsRunning = false;
        LiveWallpaper.Pause();
        // Navigating away never stops generation or model management.
        base.OnDisappearing();
    }

    private static string T(string key) => LocalizationService.TranslateKey("AiBank." + key);
    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (!_appeared) return;
        // Coalesce worker notifications instead of queuing a UI render for every token/state change.
        if (Interlocked.Exchange(ref _renderQueued, 1) != 0) return;
        if (!Dispatcher.Dispatch(() =>
        {
            Interlocked.Exchange(ref _renderQueued, 0);
            if (_appeared) Render();
        })) Interlocked.Exchange(ref _renderQueued, 0);
    }
    private void OnCultureChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(() =>
    { RefreshPickerLabels(); RefreshSqlCellLabels(); Render(); });
    private void OnDeveloperModeChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(() =>
    { if (_appeared) RenderSelected(); });

    private void RefreshPickerLabels()
    {
        _updating = true;
        var operation = SelectedLearningOperation;
        int stars = Math.Max(0, StarsPicker.SelectedIndex);
        int language = Math.Max(0, LanguagePicker.SelectedIndex), mode = Math.Max(0, BatchModePicker.SelectedIndex);
        ProblemPicker.ItemsSource = new[] { T("BasicArithmetic"), LocalizationService.TranslateKey("FindXBank.Title"),
            LocalizationService.TranslateKey("FractionBank.Title") }; ProblemPicker.SelectedIndex = (int)_family;
        StarsPicker.ItemsSource = Enumerable.Range(1, 5).Select(n => new string('★', n)).ToArray();
        LanguagePicker.ItemsSource = new[] { LocalizationService.TranslateKey("Language.Vietnamese"), LocalizationService.TranslateKey("Language.English") };
        BatchModePicker.ItemsSource = new[] { T("Single"), T("Batch") };
        DownloadModelPicker.ItemsSource = AiModelLibrary.Downloads.Select(m => m.Name).ToArray();
        if (DownloadModelPicker.SelectedIndex < 0) DownloadModelPicker.SelectedIndex = 0;
        RefreshLearningPickers(operation); StarsPicker.SelectedIndex = stars;
        LanguagePicker.SelectedIndex = language; BatchModePicker.SelectedIndex = mode;
        _updating = false;
        CountEntry.IsEnabled = mode == 1;
        RefreshFindXRoles();
    }

    private void Render()
    {
        var snapshot = _bank.Generation.Snapshot;
        bool running = snapshot.IsRunning, busy = running || _bank.IsManaging || _confirmingDeleteAll || _bank.Generation.IsDeletingAll;
        ConfigurationPanel.IsEnabled = !busy;
        ChooseModelButton.IsEnabled = DownloadButton.IsEnabled = !busy;
        DownloadModelPicker.IsEnabled = !busy;
        EjectModelButton.IsEnabled = !busy && _bank.Runtime.CanGenerate;
        CancelModelButton.IsEnabled = _bank.IsManaging;
        GenerateButton.IsEnabled = !busy && !_saving && _bank.Runtime.CanGenerate;
        StopButton.IsEnabled = running;
        ModelStatusLabel.Text = _bank.Runtime.IsLoaded ? T("Loaded") + ": " + _bank.Runtime.ModelName
            : _bank.Runtime.CanGenerate ? T("SelectedModel") + ": " + _bank.Runtime.ModelName : T("NoModel");
        DownloadProgressBar.IsVisible = _bank.IsManaging && _bank.DownloadProgress is not null;
        DownloadProgressBar.Progress = _bank.DownloadProgress is { Total: > 0 } p ? (double)p.Received / p.Total.Value : 0;
        DownloadStatusLabel.Text = _bank.ManagementError is not null ? T("ModelActionFailed") + ": " + _bank.ManagementError
            : _bank.IsManaging ? T(_bank.ManagementStatus) + (_bank.DownloadProgress is { } progress
                ? $" · {progress.Received / 1048576d:N0} / {(progress.Total.HasValue ? (progress.Total.Value / 1048576d).ToString("N0") : "?")} MiB" : "") : "";
        int valid = snapshot.Items.Count(i => i.Question is not null), saved = snapshot.Items.Count(i => i.State == AiItemState.Saved);
        JobStatusLabel.Text = string.Format(CultureInfo.CurrentCulture, T("Progress"), T("Job." + snapshot.State), valid,
            snapshot.Options?.Count ?? 0, saved) + (snapshot.Error is null ? "" : "\n" + ErrorText(snapshot.Error));
        RenderGenerationStatus(snapshot, busy);
        int previous = QuestionPicker.SelectedIndex;
        _updating = true;
        string[] labels = snapshot.Items.Select(i => $"{T("Question")} {i.Number} · {T("Item." + i.State)}").ToArray();
        if (QuestionPicker.ItemsSource is not IEnumerable<string> existing || !existing.SequenceEqual(labels))
        {
            QuestionPicker.ItemsSource = labels;
            QuestionPicker.SelectedIndex = !_followLatestItem && previous >= 0 && previous < snapshot.Items.Count
                ? previous : snapshot.Items.Count - 1;
        }
        QuestionPicker.IsEnabled = snapshot.Items.Count > 0;
        _updating = false;
        RenderSelected();
        UpdateDataActions();
    }

    private AiQuestionItem? SelectedItem => _bank.Generation.Snapshot.Items.ElementAtOrDefault(QuestionPicker.SelectedIndex);
    private void RenderSelected()
    {
        var item = SelectedItem;
        PreviewStatusLabel.Text = item is null ? T("NoPreview") : T("Item." + item.State);
        string? raw = item?.Attempts.LastOrDefault()?.RawJson;
        string preview = item is null ? "" : StreamingQuestionPreview.Render(raw, item.Contract);
        bool streaming = item?.Question is null && preview.Length > 0;
        PreviewStreamingTextLabel.IsVisible = streaming;
        PreviewStreamingTextLabel.Text = streaming ? preview : "";
        PreviewText.IsVisible = !streaming;
        PreviewText.Expression = item?.Question?.WordProblem.ProblemText ?? T("NoPreview");
        PreviewFactTable.Table = item?.Question?.WordProblem.FactTable;
        bool fractionStory = item?.Question?.Contract.Family == BankQuestionFamily.Fraction;
        PreviewFractionSolution.IsVisible = fractionStory;
        PreviewFractionSolution.Expression = fractionStory ? item!.Question!.Contract.Solution : "";
        PreviewSolutionLabel.IsVisible = item?.Question is not null && !fractionStory;
        PreviewSolutionLabel.Text = item?.Question is { } question && !fractionStory
            ? (question.WordProblem.ConversionStep is { } step ? step + "\n" : "")
                + question.WordProblem.SolutionLead + "\n" + (question.WordProblem.ArithmeticReasoning?.Equation ?? question.Contract.Left + " "
                + MathSolver.Services.Core.BasicArithmeticEngine.GetSymbol(question.Contract.Expression.Operation) + " "
                + question.Contract.Right) + " = " + question.Contract.Answer + " " + question.Contract.AnswerUnit : "";
        // Only complete, validated drafts can be exported or inserted.
        InsertButton.IsEnabled = !_saving && !_confirmingDeleteAll && !_bank.Generation.IsDeletingAll && item?.Question is not null
            && (item.State == AiItemState.SaveFailed || item.State == AiItemState.Ready
                && (!_bank.Generation.IsRunning || _bank.Generation.Snapshot.Options?.AutoInsert != true));
        ShareButton.IsEnabled = item?.Question is not null;
        RenderDiagnostics(item);
    }

    private void RenderDiagnostics(AiQuestionItem? item)
    {
        DiagnosticsCard.IsVisible = DeveloperModeManager.IsEnabled;
        if (!DiagnosticsCard.IsVisible) DiagnosticsPanel.IsVisible = false;
        DiagnosticsToggleButton.Text = T(DiagnosticsPanel.IsVisible ? "HideDiagnostics" : "ShowDiagnostics");
        if (!DiagnosticsCard.IsVisible || !DiagnosticsPanel.IsVisible)
        {
            // Keep the diagnostics in the generation service; only materialize text when opened.
            ContractEditor.Text = RawJsonEditor.Text = PromptEditor.Text = ValidationLogEditor.Text = "";
            return;
        }
        ContractEditor.Text = item is null ? "" : JsonSerializer.Serialize(item.Contract, JsonOptions);
        RawJsonEditor.Text = item is null ? "" : string.Join("\n\n", item.Attempts.Select(a => $"{T("Attempt")} {a.Number}/3\n{a.RawJson}"));
        PromptEditor.Text = item?.Attempts.LastOrDefault()?.Prompt ?? "";
        ValidationLogEditor.Text = item is null ? "" : string.Join("\n", item.Attempts.Select(a =>
            $"[{a.Number}/3] {(a.ErrorCode is not null ? ErrorText(a.ErrorCode) : a.IsComplete ? T("ValidationPassed")
                : T("Job." + (item.State == AiItemState.Stopped ? AiJobState.Stopped
                    : item.State == AiItemState.Rejected ? AiJobState.Failed
                    : item.State == AiItemState.Validating ? AiJobState.Validating : AiJobState.Generating)))}"))
            + "\n" + T("Item." + item.State) + (item.Error is null ? "" : "\n" + ErrorText(item.Error));
    }

    private static string ErrorText(string error)
    {
        string key = "AiBank.Error." + error;
        string text = LocalizationService.TranslateKey(key);
        return text == key ? error : text;
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (_goingBack) return;
        _goingBack = true;
        try { await Shell.Current.GoToAsync(".."); }
        finally { _goingBack = false; }
    }
    private void OnBatchModeChanged(object? sender, EventArgs e)
    { if (!_updating) CountEntry.IsEnabled = BatchModePicker.SelectedIndex == 1; }
    private void OnQuestionSelected(object? sender, EventArgs e)
    {
        if (_updating) return;
        _followLatestItem = QuestionPicker.SelectedIndex == _bank.Generation.Snapshot.Items.Count - 1;
        RenderSelected();
    }
    private void OnToggleDiagnosticsClicked(object? sender, EventArgs e)
    {
        if (!DeveloperModeManager.IsEnabled) return;
        DiagnosticsPanel.IsVisible = !DiagnosticsPanel.IsVisible;
        UpdateDiagnosticsLayout();
        RenderSelected();
    }

    private void OnDiagnosticsSizeChanged(object? sender, EventArgs e) => UpdateDiagnosticsLayout();

    private void UpdateDiagnosticsLayout()
    {
        if (DiagnosticsPanel.Width <= 0) return;
        bool wide = DiagnosticsPanel.Width >= 800;
        if (wide == (DiagnosticsPanel.ColumnDefinitions.Count == 2)) return;

        DiagnosticsPanel.ColumnDefinitions.Clear();
        DiagnosticsPanel.ColumnDefinitions.Add(new() { Width = GridLength.Star });
        if (wide) DiagnosticsPanel.ColumnDefinitions.Add(new() { Width = GridLength.Star });
        DiagnosticsPanel.RowDefinitions.Clear();
        for (int row = 0; row < (wide ? 2 : 4); row++)
            DiagnosticsPanel.RowDefinitions.Add(new() { Height = GridLength.Auto });

        // Row order: C# facts / model JSON, then validation log / prompt.
        Border[] cards = [ContractDiagnosticCard, RawJsonDiagnosticCard, ValidationDiagnosticCard, PromptDiagnosticCard];
        for (int index = 0; index < cards.Length; index++)
        {
            Grid.SetRow(cards[index], wide ? index / 2 : index);
            Grid.SetColumn(cards[index], wide ? index % 2 : 0);
        }
    }

    private async void OnChooseModelClicked(object? sender, EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new() { PickerTitle = T("ChooseModel"), FileTypes = GgufType });
            if (file is null) return;
            await _bank.ManageAsync("Loading", async cancellation =>
            {
                string path;
                if (OperatingSystem.IsWindows() && File.Exists(file.FullPath)) path = file.FullPath;
                else
                {
                    await using var source = await file.OpenReadAsync();
                    path = await _bank.Models.ImportAsync(source, file.FileName, cancellation);
                }
                await _bank.Runtime.SelectAsync(path, cancellation);
            });
        }
        catch (Exception error) { await ShowErrorAsync(error); }
    }

    private async void OnEjectClicked(object? sender, EventArgs e)
    {
        try { await _bank.ManageAsync("Ejecting", _ => _bank.Runtime.EjectAsync()); }
        catch (Exception error) { await ShowErrorAsync(error); }
    }

    private async void OnDownloadClicked(object? sender, EventArgs e)
    {
        var model = AiModelLibrary.Downloads[Math.Max(0, DownloadModelPicker.SelectedIndex)];
        try
        {
            await _bank.ManageAsync("Downloading", async cancellation =>
            {
                string path = await _bank.Models.DownloadAsync(model, _bank.CreateDownloadProgress(), cancellation);
                await _bank.Runtime.SelectAsync(path, cancellation);
            });
        }
        catch (Exception error) { await ShowErrorAsync(error); }
    }

    private void OnCancelModelClicked(object? sender, EventArgs e) => _bank.CancelManagement();
    private async void OnOpenFolderClicked(object? sender, EventArgs e)
    {
        string directory = _bank.Models.DirectoryPath;
#if WINDOWS
        if (_bank.Runtime.CanGenerate && File.Exists(_bank.Runtime.ModelPath))
            directory = Path.GetDirectoryName(_bank.Runtime.ModelPath)!;
#endif
        Directory.CreateDirectory(directory);
        try
        {
#if WINDOWS
            Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
#elif ANDROID
            // Android's app-private model directory cannot be opened by another
            // app. Export a chosen GGUF through the system document picker instead.
            string[] files = _bank.Models.Files().ToArray();
            if (files.Length == 0) { await DisplayAlertAsync(T("OpenFolder"), T("AndroidFolderHint"), T("Ok")); return; }
            string? selected = await DisplayActionSheetAsync(T("AndroidModelFiles"), T("Cancel"), null, files.Select(Path.GetFileName).ToArray()!);
            string? path = files.FirstOrDefault(f => Path.GetFileName(f) == selected);
            if (path is null) return;
            await using var source = File.OpenRead(path);
            await FileSaver.Default.SaveAsync(Path.GetFileName(path), source, CancellationToken.None);
#else
            await Clipboard.Default.SetTextAsync(directory);
#endif
        }
        catch (Exception error) { await ShowErrorAsync(error); }
    }

    private async void OnGenerateClicked(object? sender, EventArgs e)
    {
        if (_bank.IsManaging || _bank.Generation.IsRunning || _saving || _confirmingDeleteAll || _bank.Generation.IsDeletingAll) return;
        int count = 1;
        if (BatchModePicker.SelectedIndex == 1 && (!int.TryParse(CountEntry.Text, out count) || count is < 1 or > 100))
        { await DisplayAlertAsync(T("Title"), T("CountHint"), T("Ok")); return; }
        try
        {
            _followLatestItem = true;
            _bank.Generation.Start(new(SelectedLearningOperation, (CurriculumTier)(StarsPicker.SelectedIndex + 1),
                LanguagePicker.SelectedIndex == 0 ? AppLanguage.Vietnamese : AppLanguage.English, count, AutoInsertSwitch.IsToggled, CurrentLearningProfile, _family, _unknownRole));
            _updating = true;
            try { QuestionPicker.SelectedIndex = -1; }
            finally { _updating = false; }
            Render();
        }
        catch (Exception error) { await ShowErrorAsync(error); }
    }

    private void OnStopClicked(object? sender, EventArgs e) => _bank.Generation.Stop();
    private async void OnInsertClicked(object? sender, EventArgs e)
    {
        var item = SelectedItem;
        if (item?.Question is null || _saving || _confirmingDeleteAll || _bank.Generation.IsDeletingAll) return;
        _saving = true; Render();
        try { await _bank.Generation.InsertAsync(item.Number); }
        finally { _saving = false; Render(); }
    }

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        if (SelectedItem?.Question is not { } question) return;
        try { await Share.Default.RequestAsync(new ShareTextRequest { Title = T("Share"), Text = question.WordProblem.ProblemText }); }
        catch (Exception error) { await ShowErrorAsync(error); }
    }

    private Task ShowErrorAsync(Exception error) => DisplayAlertAsync(T("Title"), ErrorText(error.Message), T("Ok"));
}
