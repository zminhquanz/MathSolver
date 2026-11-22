using System.ComponentModel;
using Google.Android.Material.Dialog;
using MathSolver.Controls;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using AlertDialog = AndroidX.AppCompat.App.AlertDialog;
using NativeView = Android.Views.View;

namespace MathSolver;

internal sealed class WallpaperModePickerHandler : PickerHandler2
{
    private AlertDialog? _dialog;

    protected override void ConnectHandler(MauiMaterialPicker platformView)
    {
        base.ConnectHandler(platformView);
        platformView.SetOnClickListener(new OpenWallpaperDialogListener(this));
        ((WallpaperModePicker)VirtualView).PropertyChanged += OnPickerPropertyChanged;
    }

    protected override void DisconnectHandler(MauiMaterialPicker platformView)
    {
        ((WallpaperModePicker)VirtualView).PropertyChanged -= OnPickerPropertyChanged;
        _dialog?.Dismiss();
        platformView.SetOnClickListener(null);
        base.DisconnectHandler(platformView);
    }

    private void OpenDialog()
    {
        if (_dialog is not null || VirtualView is not WallpaperModePicker { IsEnabled: true } picker)
        {
            return;
        }

        using var builder = new MaterialAlertDialogBuilder(MauiMaterialContextThemeWrapper.Create(Context));
        builder.SetTitle(picker.Title);

        // A checked index passed to the builder also scrolls the list to that
        // item. Check it after showing the dialog so wrapped rows start at the top.
        builder.SetSingleChoiceItems(picker.Items.ToArray(), -1, (_, args) =>
        {
            picker.SelectedIndex = args.Which;
            _dialog?.Dismiss();
        });
        builder.SetNegativeButton(Android.Resource.String.Cancel, (_, _) => { });

        var dialog = builder.Create();
        _dialog = dialog;
        dialog.DismissEvent += OnDialogDismissed;
        dialog.SetCanceledOnTouchOutside(true);
        dialog.Show();
        if (picker.SelectedIndex >= 0)
        {
            dialog.ListView?.SetItemChecked(picker.SelectedIndex, true);
        }
        picker.IsOpen = true;
    }

    private void OnPickerPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(Picker.IsOpen) && sender is WallpaperModePicker { IsOpen: false })
        {
            _dialog?.Dismiss();
        }
    }

    private void OnDialogDismissed(object? sender, EventArgs args)
    {
        var dialog = _dialog;
        _dialog = null;
        if (dialog is not null)
        {
            dialog.DismissEvent -= OnDialogDismissed;
            dialog.Dispose();
        }
        if (VirtualView is WallpaperModePicker picker)
        {
            picker.IsOpen = false;
        }
    }

    private sealed class OpenWallpaperDialogListener(WallpaperModePickerHandler handler)
        : Java.Lang.Object, NativeView.IOnClickListener
    {
        public void OnClick(NativeView? view) => handler.OpenDialog();
    }
}
