using MathSolver.Services;
using MathSolver.Services.QuestionBank;

namespace MathSolver.Views;

/// <summary>Settings destination for stored app data, independent of the AI editor.</summary>
public partial class DataManagementPage : ContentPage
{
    // Use the app-owned store and deletion coordinator; opening this page never loads a model.
    private readonly AiQuestionBank _bank = AiQuestionBank.Current;
    private TaskCompletionSource? _dataCompletion;
    private bool _appeared;
    private bool _loadedTable;
    private bool _closing;

    public DataManagementPage()
    {
        InitializeComponent();
        LocalizationService.ExcludeSubtreeFromLegacyTracking(this);
        QueryEditor.Text = QuestionBankStore.DefaultInquiry;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _appeared = true;
        LocalizationService.CultureChanged += OnCultureChanged;
        _bank.Generation.Changed += OnBankStateChanged;
        RefreshSqlHelp();
        UpdateDataActions();
        if (!_loadedTable)
        {
            _loadedTable = true;
            await LoadEditableSqlAsync();
        }
    }

    protected override void OnDisappearing()
    {
        _appeared = false;
        LocalizationService.CultureChanged -= OnCultureChanged;
        _bank.Generation.Changed -= OnBankStateChanged;
        _dataCancellation?.Cancel();
        base.OnDisappearing();
    }

    private void OnCultureChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(() =>
    {
        if (!_appeared) return;
        RefreshSqlCellLabels();
        RefreshSqlHelp();
        UpdateDataActions();
    });

    private void OnBankStateChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(() =>
    {
        if (_appeared) UpdateDataActions();
    });

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync();
        return true;
    }

    private async void OnCloseClicked(object? sender, EventArgs e) => await CloseAsync();

    private async Task CloseAsync()
    {
        if (_closing) return;
        _closing = true;
        try
        {
            if (_confirmingDeleteAll) return;
            if ((QueryResultsTable.IsEditing || QueryResultsTable.IsInserting)
                && !await DisplayAlertAsync(LocalizationService.TranslateKey("DataManagement.Title"),
                    LocalizationService.TranslateKey("DataManagement.DiscardHint"),
                    LocalizationService.TranslateKey("DataManagement.Discard"), T("Cancel"))) return;
            DataBackButton.IsEnabled = false;
            if (_dataCancellation is { } cancellation && _dataCompletion is { } completion)
            {
                cancellation.Cancel();
                await completion.Task;
            }
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            _closing = false;
            if (_appeared)
            {
                DataBackButton.IsEnabled = true;
                UpdateDataActions();
            }
        }
    }

    private static string T(string key) => LocalizationService.TranslateKey("AiBank." + key);
    private static string ErrorText(string error)
    {
        string key = "AiBank.Error." + error;
        string text = LocalizationService.TranslateKey(key);
        return text == key ? error : text;
    }

    private Task ShowErrorAsync(Exception error) => DisplayAlertAsync(
        LocalizationService.TranslateKey("DataManagement.Title"), ErrorText(error.Message), T("Ok"));
}
