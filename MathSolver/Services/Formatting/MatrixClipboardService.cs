using MathSolver.Services.Core;
#if WINDOWS
using System.Runtime.InteropServices;
#endif

namespace MathSolver.Services;

internal static class MatrixClipboardService
{
    internal static Task<bool> TrySetAsync(MatrixClipboardContent content, Func<bool> canPublish) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
    {
        if (!canPublish()) return false;
#if WINDOWS
        var package = new global::Windows.ApplicationModel.DataTransfer.DataPackage
        {
            RequestedOperation = global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy
        };
        package.SetText(content.Text);
        if (content.Html is { } html)
            package.SetHtmlFormat(global::Windows.ApplicationModel.DataTransfer.HtmlFormatHelper.CreateHtmlFormat(html));
        var result = await MatrixClipboardWriter.TryWriteAsync(
            // Unlike SetContent, this API returns false for normal clipboard contention.
            () => global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContentWithOptions(package, null),
            TryFlush, canPublish, () => Task.Delay(60));
        return result.Copied;
#else
        await Clipboard.Default.SetTextAsync(content.Text);
        return true;
#endif
    });

#if WINDOWS
    private static bool TryFlush()
    {
        // Preserve the native HRESULT instead of asking the WinRT projection to
        // throw COMException when clipboard persistence is temporarily unavailable.
        // The text/HTML package has already been published successfully at this point.
        int status = OleFlushClipboard();
        if (status < 0) System.Diagnostics.Debug.WriteLine($"Matrix clipboard persistence unavailable: 0x{status:X8}");
        return status >= 0;
    }

    [DllImport("ole32.dll", ExactSpelling = true, PreserveSig = true)]
    private static extern int OleFlushClipboard();
#endif
}
