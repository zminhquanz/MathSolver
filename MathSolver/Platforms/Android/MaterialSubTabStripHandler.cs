using Android.Content.Res;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Widget;
using Google.Android.Material.Tabs;
using MathSolver.Controls;
using MathSolver.Services;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace MathSolver;

/// <summary>TabLayout owns measurement, scrolling, ripple and selected indicator.</summary>
public sealed class MaterialSubTabStripHandler : ViewHandler<MaterialSubTabStrip, TabLayout>
{
    private bool _updating;

    public static readonly IPropertyMapper<MaterialSubTabStrip, MaterialSubTabStripHandler> Mapper =
        new PropertyMapper<MaterialSubTabStrip, MaterialSubTabStripHandler>(ViewMapper)
        {
            [nameof(MaterialSubTabStrip.TabKeys)] = (handler, _) => handler.UpdateTabs(),
            [nameof(MaterialSubTabStrip.SelectedIndex)] = (handler, _) => handler.UpdateSelection(),
            [nameof(MaterialSubTabStrip.AccentColor)] = (handler, _) => handler.UpdateColors(),
            [nameof(MaterialSubTabStrip.LabelColor)] = (handler, _) => handler.UpdateColors(),
            [nameof(MaterialSubTabStrip.SelectedLabelColor)] = (handler, _) => handler.UpdateColors(),
            [nameof(MaterialSubTabStrip.SurfaceColor)] = (handler, _) => handler.UpdateColors(),
            [nameof(MaterialSubTabStrip.IsEnabled)] = (handler, _) => handler.UpdateEnabled()
        };

    public static readonly CommandMapper<MaterialSubTabStrip, MaterialSubTabStripHandler> Commands =
        new(ViewCommandMapper)
        {
            [nameof(MaterialSubTabStrip.RevealSelected)] = (handler, _, _) => handler.RevealSelected()
        };

    public MaterialSubTabStripHandler() : base(Mapper, Commands) { }

    protected override TabLayout CreatePlatformView()
    {
        var tabs = new TabLayout(new AndroidX.AppCompat.View.ContextThemeWrapper(
            Context, Resource.Style.MathSolver_ThemeOverlay_Material3_SubTabs))
        {
            TabMode = TabLayout.ModeScrollable,
            TabGravity = TabLayout.GravityStart,
            InlineLabel = true,
            HorizontalScrollBarEnabled = false
        };
        tabs.SetClipChildren(true);
        tabs.SetClipToPadding(true);
        return tabs;
    }

    protected override void ConnectHandler(TabLayout platformView)
    {
        base.ConnectHandler(platformView);
        platformView.TabSelected += OnTabSelected;
    }

    protected override void DisconnectHandler(TabLayout platformView)
    {
        platformView.TabSelected -= OnTabSelected;
        base.DisconnectHandler(platformView);
    }

    private void UpdateTabs()
    {
        _updating = true;
        try
        {
            PlatformView.RemoveAllTabs();
            using var iconFont = Android.Graphics.Typeface.CreateFromAsset(Context.Assets, "MathNavigationIcons.ttf");
            foreach (string key in VirtualView.TabKeys)
            {
                var tab = PlatformView.NewTab();
                string label = MathNavigationCatalog.TranslateLabel(key);
                tab.SetText(label);
                tab.SetContentDescription(label);
                if (MathNavigationCatalog.GlyphFor(key) is { } glyph)
                    tab.SetIcon(CreateIcon(glyph, iconFont));
                PlatformView.AddTab(tab, false);
            }
            FormatLabels(PlatformView);
            UpdateColors();
            UpdateEnabled();
        }
        finally { _updating = false; }
        UpdateSelection();
    }

    private BitmapDrawable CreateIcon(string glyph, Android.Graphics.Typeface? font)
    {
        // TabLayout owns the native ImageView and the icon/label spacing.
        // Rasterize our existing outline font at the device density so its
        // intrinsic size is always 24dp, independent of text/font scaling.
        var metrics = Context.Resources!.DisplayMetrics!;
        int size = (int)Math.Ceiling(MathNavigationCatalog.IconSize * metrics.Density);
        using var bitmap = Android.Graphics.Bitmap.CreateBitmap(size, size, Android.Graphics.Bitmap.Config.Argb8888!)!;
        bitmap.Density = (int)metrics.DensityDpi;
        using var canvas = new Android.Graphics.Canvas(bitmap);
        using var paint = new Android.Graphics.Paint(Android.Graphics.PaintFlags.AntiAlias)
        {
            Color = Android.Graphics.Color.White,
            TextSize = size,
            TextAlign = Android.Graphics.Paint.Align.Center
        };
        paint.SetTypeface(font);
        var fontMetrics = paint.GetFontMetrics()!;
        canvas.DrawText(glyph, size / 2f, (size - fontMetrics.Ascent - fontMetrics.Descent) / 2f, paint);
        return new BitmapDrawable(Context.Resources, bitmap);
    }

    private static void FormatLabels(Android.Views.View view)
    {
        if (view is TextView label)
        {
            label.SetAllCaps(false);
            label.SetSingleLine(true);
            label.Ellipsize = TextUtils.TruncateAt.End;
        }
        if (view is Android.Views.ViewGroup group)
            for (int i = 0; i < group.ChildCount; i++)
                if (group.GetChildAt(i) is { } child) FormatLabels(child);
    }

    private void UpdateColors()
    {
        var accent = VirtualView.AccentColor.ToPlatform();
        var selectedLabel = VirtualView.SelectedLabelColor.ToPlatform();
        PlatformView.SetTabTextColors(VirtualView.LabelColor.ToPlatform(), selectedLabel);
        PlatformView.TabIconTint = new ColorStateList(
            [ [-Android.Resource.Attribute.StateEnabled], [Android.Resource.Attribute.StateSelected], [] ],
            [ VirtualView.LabelColor.WithAlpha(.38f).ToPlatform(), selectedLabel, VirtualView.LabelColor.ToPlatform() ]);
        PlatformView.SetSelectedTabIndicatorColor(accent);
        PlatformView.TabRippleColor = ColorStateList.ValueOf(VirtualView.SelectedLabelColor.WithAlpha(.12f).ToPlatform());
        PlatformView.SetBackgroundColor(VirtualView.SurfaceColor.ToPlatform());
    }

    private void UpdateSelection()
    {
        int index = VirtualView.SelectedIndex;
        if (index < 0 || index >= PlatformView.TabCount || PlatformView.SelectedTabPosition == index) return;
        _updating = true;
        try { PlatformView.SelectTab(PlatformView.GetTabAt(index)); }
        finally { _updating = false; }
    }

    private void RevealSelected()
    {
        int index = VirtualView.SelectedIndex;
        if (index >= 0 && index < PlatformView.TabCount)
            PlatformView.SetScrollPosition(index, 0, true);
    }

    private void UpdateEnabled() => SetEnabled(PlatformView, VirtualView.IsEnabled);

    private static void SetEnabled(Android.Views.View view, bool enabled)
    {
        view.Enabled = enabled;
        if (view is Android.Views.ViewGroup group)
            for (int i = 0; i < group.ChildCount; i++)
                if (group.GetChildAt(i) is { } child) SetEnabled(child, enabled);
    }

    private void OnTabSelected(object? sender, TabLayout.TabSelectedEventArgs e)
    {
        if (!_updating && VirtualView.IsEnabled && e.Tab is { } tab)
            VirtualView.RequestSelection(tab.Position);
    }
}
