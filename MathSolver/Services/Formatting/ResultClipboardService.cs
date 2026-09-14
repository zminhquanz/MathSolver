using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;

namespace MathSolver.Services;

/// <summary>
/// Sao chép đáp số và hiển thị phản hồi tạm thời thống nhất giữa các tab.
/// </summary>
public static class ResultClipboardService
{
    private const int CopiedFeedbackMilliseconds =
        1200;

    public static async Task CopyAsync(
        Button button,
        string? resultText)
    {
        if (string.IsNullOrWhiteSpace(
                resultText) ||
            !button.IsEnabled)
        {
            return;
        }

        button.IsEnabled =
            false;

        try
        {
            await Clipboard.Default.SetTextAsync(
                resultText);

            button.Text =
                LocalizationService.TranslateKey(
                    "PowerRoot.Copied");

            await Task.Delay(
                CopiedFeedbackMilliseconds);
        }
        finally
        {
            button.Text =
                LocalizationService.TranslateKey(
                    "PowerRoot.CopyResult");

            button.IsEnabled =
                true;
        }
    }
}
