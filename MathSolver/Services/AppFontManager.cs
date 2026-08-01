namespace MathSolver.Services;

public static class AppFontManager
{
    private const string FontPreferenceKey =
        "app_font_key";

    private static bool _initialized;
    private static Application? _application;

    public static event EventHandler? FontChanged;

    public static string CurrentFontKey { get; private set; } =
        AppFontCatalog.DefaultFontKey;

    public static AppFontOption CurrentFont =>
        AppFontCatalog.GetByKey(
            CurrentFontKey);

    public static void Initialize(
        Application application)
    {
        ArgumentNullException.ThrowIfNull(
            application);

        if (_initialized)
        {
            return;
        }

        _initialized =
            true;

        _application =
            application;

        string storedKey =
            Preferences.Default.Get(
                FontPreferenceKey,
                AppFontCatalog.DefaultFontKey);

        CurrentFontKey =
            AppFontCatalog
                .GetByKey(storedKey)
                .Key;

        ApplyCurrentFont(
            savePreference: false);
    }

    public static bool SetFont(
        string? fontKey)
    {
        AppFontOption option =
            AppFontCatalog.GetByKey(
                fontKey);

        if (option.Key ==
            CurrentFontKey)
        {
            // Re-apply anyway. This is useful after a published Windows
            // build recreates handlers or pages.
            ApplyCurrentFont(
                savePreference: false);

            return false;
        }

        CurrentFontKey =
            option.Key;

        ApplyCurrentFont(
            savePreference: true);

        return true;
    }

    public static void ResetToDefault()
    {
        CurrentFontKey =
            AppFontCatalog.DefaultFontKey;

        ApplyCurrentFont(
            savePreference: true);
    }

    /// <summary>
    /// Reapplies the selected font to every currently realized MAUI text
    /// control. The app still uses DynamicResource AppFontFamily, but this
    /// explicit pass makes font switching reliable in published Windows
    /// Release builds as well.
    /// </summary>
    public static void RefreshVisibleFont()
    {
        ApplyCurrentFont(
            savePreference: false);
    }

    private static void ApplyCurrentFont(
        bool savePreference)
    {
        void Apply()
        {
            Application? application =
                _application ??
                Application.Current;

            if (application is null)
            {
                return;
            }

            AppFontOption font =
                CurrentFont;

            application.Resources["AppFontFamily"] =
                font.FontFamily;

            application.Resources["AppFontDisplayName"] =
                font.DisplayName;

            // DynamicResource normally updates these controls itself.
            // A direct visual-tree refresh avoids the Release-publish case
            // where existing Windows handlers keep the old native font.
            foreach (Window window
                     in application.Windows)
            {
                if (window.Page is Element root)
                {
                    ApplyFontToTree(
                        root,
                        font.FontFamily,
                        new HashSet<Element>());
                }
            }

            if (savePreference)
            {
                Preferences.Default.Set(
                    FontPreferenceKey,
                    CurrentFontKey);
            }

            FontChanged?.Invoke(
                null,
                EventArgs.Empty);
        }

        if (MainThread.IsMainThread)
        {
            Apply();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(
                Apply);
        }
    }

    private static void ApplyFontToTree(
        Element element,
        string fontFamily,
        HashSet<Element> visited)
    {
        if (!visited.Add(
                element))
        {
            return;
        }

        ApplyFontToElement(
            element,
            fontFamily);

        if (element is not
            IVisualTreeElement visualTreeElement)
        {
            return;
        }

        foreach (IVisualTreeElement child
                 in visualTreeElement.GetVisualChildren())
        {
            if (child is Element childElement)
            {
                ApplyFontToTree(
                    childElement,
                    fontFamily,
                    visited);
            }
        }
    }

    private static void ApplyFontToElement(
        Element element,
        string fontFamily)
    {
        switch (element)
        {
            case Label label:
                label.FontFamily =
                    fontFamily;
                break;

            case Button button:
                button.FontFamily =
                    fontFamily;
                break;

            case Entry entry:
                entry.FontFamily =
                    fontFamily;
                break;

            case Editor editor:
                editor.FontFamily =
                    fontFamily;
                break;

            case Picker picker:
                picker.FontFamily =
                    fontFamily;
                break;

            case SearchBar searchBar:
                searchBar.FontFamily =
                    fontFamily;
                break;

            case RadioButton radioButton:
                radioButton.FontFamily =
                    fontFamily;
                break;

            case DatePicker datePicker:
                datePicker.FontFamily =
                    fontFamily;
                break;

            case TimePicker timePicker:
                timePicker.FontFamily =
                    fontFamily;
                break;

            case Span span:
                span.FontFamily =
                    fontFamily;
                break;
        }
    }
}
