using MathSolver.Services;

namespace MathSolver.Views;

public partial class DataManagementPage
{
    private static string DT(string key) => LocalizationService.TranslateKey("DataManagement." + key);

    private Task<bool> ConfirmDataChangeAsync(string title, string message, string accept, CancellationToken cancellation, bool transfer = false)
        => MainThread.InvokeOnMainThreadAsync(async () =>
        {
            cancellation.ThrowIfCancellationRequested();
            if (!_appeared || _closing || _confirmingDataChange) return false;
            _confirmingDataChange = true;
            UpdateDataActions();
            try
            {
                bool confirmed = await DisplayAlertAsync(title, message, accept, T("Cancel"));
                bool accepted = confirmed && _appeared && !_closing && !cancellation.IsCancellationRequested;
                if (!accepted && _appeared)
                    (transfer ? DataTransferStatusLabel : InquiryStatusLabel).Text = T("DataCancelled");
                return accepted;
            }
            finally
            {
                _confirmingDataChange = false;
                UpdateDataActions();
            }
        });
}
