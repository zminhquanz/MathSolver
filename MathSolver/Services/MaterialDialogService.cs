#if ANDROID
using Android.Content;
using Google.Android.Material.Dialog;
using Microsoft.Maui.ApplicationModel;
#endif

namespace MathSolver.Services;

/// <summary>
/// Keeps the existing MAUI alert presentation on non-Android platforms while
/// using MaterialAlertDialogBuilder on Android. This lets Windows keep the
/// stable WinUI UX and gives Android dialogs Material shape/color/state theming.
/// </summary>
public static class MaterialDialogService
{
    public static Task ShowAlertAsync(
        Page page,
        string title,
        string message,
        string buttonText)
    {
#if ANDROID
        if (Platform.CurrentActivity is Android.App.Activity activity)
        {
            return ShowAndroidAlertAsync(
                activity,
                title,
                message,
                buttonText);
        }
#endif

        return page.DisplayAlertAsync(
            title,
            message,
            buttonText);
    }

    public static Task<bool> ConfirmAsync(
        Page page,
        string title,
        string message,
        string accept,
        string cancel)
    {
#if ANDROID
        if (Platform.CurrentActivity is Android.App.Activity activity)
        {
            return ShowAndroidConfirmationAsync(
                activity,
                title,
                message,
                accept,
                cancel);
        }
#endif

        return page.DisplayAlertAsync(
            title,
            message,
            accept,
            cancel);
    }

#if ANDROID
    private static Task ShowAndroidAlertAsync(
        Android.App.Activity activity,
        string title,
        string message,
        string buttonText)
    {
        var completion =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var cancelListener =
            new DialogCancelListener(
                () => completion.TrySetResult(true));

        var builder =
            new MaterialAlertDialogBuilder(activity)
                .SetTitle(title)
                .SetMessage(message)
                .SetPositiveButton(
                    buttonText,
                    (_, _) => completion.TrySetResult(true))
                .SetOnCancelListener(cancelListener);

        var dialog =
            builder.Create();

        dialog.SetCanceledOnTouchOutside(true);
        dialog.Show();

        return completion.Task;
    }

    private static Task<bool> ShowAndroidConfirmationAsync(
        Android.App.Activity activity,
        string title,
        string message,
        string accept,
        string cancel)
    {
        var completion =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var cancelListener =
            new DialogCancelListener(
                () => completion.TrySetResult(false));

        var builder =
            new MaterialAlertDialogBuilder(activity)
                .SetTitle(title)
                .SetMessage(message)
                .SetPositiveButton(
                    accept,
                    (_, _) => completion.TrySetResult(true))
                .SetNegativeButton(
                    cancel,
                    (_, _) => completion.TrySetResult(false))
                .SetOnCancelListener(cancelListener);

        var dialog =
            builder.Create();

        dialog.SetCanceledOnTouchOutside(true);
        dialog.Show();

        return completion.Task;
    }

    private sealed class DialogCancelListener(
        Action onCancel)
        : Java.Lang.Object,
          IDialogInterfaceOnCancelListener
    {
        public void OnCancel(
            IDialogInterface? dialog)
        {
            onCancel();
        }
    }
#endif
}
