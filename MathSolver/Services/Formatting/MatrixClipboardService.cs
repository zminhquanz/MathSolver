using MathSolver.Services.Core;

namespace MathSolver.Services;

internal static class MatrixClipboardService
{
    internal static Task SetAsync(MatrixClipboardContent content)
    {
#if WINDOWS
        var package = new global::Windows.ApplicationModel.DataTransfer.DataPackage
        {
            RequestedOperation = global::Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy
        };
        package.SetText(content.Text);
        if (content.Html is { } html)
            package.SetHtmlFormat(global::Windows.ApplicationModel.DataTransfer.HtmlFormatHelper.CreateHtmlFormat(html));
        global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        global::Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
        return Task.CompletedTask;
#else
        return Clipboard.Default.SetTextAsync(content.Text);
#endif
    }
}
