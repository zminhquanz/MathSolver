using MathSolver.Models.Localization;
using MathSolver.Services.Localization;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MathSolver.Services;

/// <summary>
/// Compatibility facade for the JSON localization system.
///
/// Existing XAML and calculation code can continue calling Translate(source)
/// while new code should prefer stable keys through TranslateKey or the
/// Translate markup extension.
/// </summary>
public static class LocalizationService
{
    private sealed class TrackedProperty
    {
        public required string PropertyName { get; init; }

        public required Func<string?> Getter { get; init; }

        public required Action<string> Setter { get; init; }

        public string SourceText { get; set; } =
            string.Empty;
    }

    private sealed class TrackedObject
    {
        public List<TrackedProperty> Properties { get; } =
            [];
    }

    private static readonly ConditionalWeakTable<BindableObject, TrackedObject>
        TrackedObjects =
            new();

    private static readonly List<WeakReference<BindableObject>>
        TrackedObjectReferences =
            [];

    private static readonly List<WeakReference<Element>>
        RootReferences =
            [];

    private static bool _initialized;
    private static bool _isApplying;

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        AppLanguageManager.Initialize();

        LocalizationManager.Instance
            .InitializeAsync(
                ResolveInitialCulture())
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();

        AppLanguageManager.LanguageChanged +=
            OnLegacyLanguageChanged;

        LocalizationManager.Instance.CultureChanged +=
            OnCultureChanged;

        _initialized =
            true;
    }

    public static string Translate(
        string? source)
    {
        Initialize();

        return LocalizationManager.Instance
            .TranslateSource(
                source);
    }

    public static string TranslateKey(
        string key)
    {
        Initialize();

        return LocalizationManager.Instance
            .TranslateKey(
                key);
    }

    public static string FormatKey(
        string key,
        IReadOnlyDictionary<string, object?> values)
    {
        Initialize();

        return LocalizationManager.Instance
            .FormatKey(
                key,
                values);
    }

    public static Task SetCultureAsync(
        string culture,
        CancellationToken cancellationToken = default)
    {
        Initialize();

        return LocalizationManager.Instance
            .SetCultureAsync(
                culture,
                cancellationToken);
    }

    public static Task<IReadOnlyList<LanguageOption>>
        GetAvailableLanguagesAsync(
            CancellationToken cancellationToken = default)
    {
        Initialize();

        return LocalizationManager.Instance
            .GetAvailableLanguagesAsync(
                cancellationToken);
    }

    public static Task<LanguagePackValidationResult>
        ImportLanguagePackAsync(
            Stream stream,
            CancellationToken cancellationToken = default)
    {
        Initialize();

        return LocalizationManager.Instance
            .ImportLanguagePackAsync(
                stream,
                cancellationToken);
    }

    public static void Attach(
        Element root)
    {
        ArgumentNullException.ThrowIfNull(
            root);

        Initialize();

        if (!RootReferences.Any(
                reference =>
                    reference.TryGetTarget(
                        out Element? existing) &&
                    ReferenceEquals(
                        existing,
                        root)))
        {
            RootReferences.Add(
                new WeakReference<Element>(
                    root));
        }

        AttachRecursive(
            root,
            new HashSet<Element>(
                ReferenceEqualityComparer.Instance));
    }

    public static void RefreshAll()
    {
        void Refresh()
        {
            for (int index =
                     RootReferences.Count - 1;
                 index >= 0;
                 index--)
            {
                if (!RootReferences[index]
                        .TryGetTarget(
                            out Element? root))
                {
                    RootReferences.RemoveAt(
                        index);

                    continue;
                }

                AttachRecursive(
                    root,
                    new HashSet<Element>(
                        ReferenceEqualityComparer.Instance));
            }

            for (int index =
                     TrackedObjectReferences.Count - 1;
                 index >= 0;
                 index--)
            {
                if (!TrackedObjectReferences[index]
                        .TryGetTarget(
                            out BindableObject? bindableObject))
                {
                    TrackedObjectReferences.RemoveAt(
                        index);

                    continue;
                }

                if (TrackedObjects.TryGetValue(
                        bindableObject,
                        out TrackedObject? trackedObject))
                {
                    ApplyTrackedObject(
                        trackedObject);
                }
            }
        }

        if (MainThread.IsMainThread)
        {
            Refresh();
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(
                Refresh);
        }
    }

    private static async void OnLegacyLanguageChanged(
        object? sender,
        EventArgs e)
    {
        try
        {
            await LocalizationManager.Instance
                .SetCultureAsync(
                    ResolveLegacyCulture());
        }
        catch
        {
            // Keep the current/fallback language if a pack is unavailable.
        }
    }

    private static void OnCultureChanged(
        object? sender,
        EventArgs e)
    {
        RefreshAll();
    }


    private static string ResolveInitialCulture()
    {
        const string preferenceKey =
            "Localization.SelectedCulture";

        if (Preferences.Default.ContainsKey(
                preferenceKey))
        {
            return Preferences.Default.Get(
                preferenceKey,
                ResolveLegacyCulture());
        }

        return ResolveLegacyCulture();
    }

    private static string ResolveLegacyCulture()
    {
        return AppLanguageManager
            .CurrentLanguage
            .ToString() switch
        {
            "English" =>
                "en-US",

            "Vietnamese" =>
                "vi-VN",

            _ =>
                "vi-VN"
        };
    }

    private static void AttachRecursive(
        Element element,
        HashSet<Element> visited)
    {
        if (!visited.Add(
                element))
        {
            return;
        }

        TrackBindableObject(
            element);

        if (element is Microsoft.Maui.IVisualTreeElement visualElement)
        {
            foreach (Microsoft.Maui.IVisualTreeElement child
                     in visualElement.GetVisualChildren())
            {
                if (child is Element childElement)
                {
                    AttachRecursive(
                        childElement,
                        visited);
                }
            }
        }

        if (element is Shell shell)
        {
            foreach (ShellItem item
                     in shell.Items)
            {
                AttachRecursive(
                    item,
                    visited);
            }
        }

        if (element is ShellItem shellItem)
        {
            foreach (ShellSection section
                     in shellItem.Items)
            {
                AttachRecursive(
                    section,
                    visited);
            }
        }

        if (element is ShellSection shellSection)
        {
            foreach (ShellContent content
                     in shellSection.Items)
            {
                AttachRecursive(
                    content,
                    visited);
            }
        }
    }

    private static void TrackBindableObject(
        BindableObject bindableObject)
    {
        if (TrackedObjects.TryGetValue(
                bindableObject,
                out TrackedObject? existing))
        {
            ApplyTrackedObject(
                existing);

            return;
        }

        var trackedObject =
            new TrackedObject();

        AddTrackedProperties(
            bindableObject,
            trackedObject);

        if (trackedObject.Properties.Count ==
            0)
        {
            return;
        }

        TrackedObjects.Add(
            bindableObject,
            trackedObject);

        TrackedObjectReferences.Add(
            new WeakReference<BindableObject>(
                bindableObject));

        bindableObject.PropertyChanged +=
            OnTrackedObjectPropertyChanged;

        ApplyTrackedObject(
            trackedObject);
    }

    private static void AddTrackedProperties(
        BindableObject bindableObject,
        TrackedObject trackedObject)
    {
        switch (bindableObject)
        {
            case Label label:
                AddProperty(
                    trackedObject,
                    nameof(Label.Text),
                    () => label.Text,
                    value => label.Text = value);
                break;

            case Button button:
                AddProperty(
                    trackedObject,
                    nameof(Button.Text),
                    () => button.Text,
                    value => button.Text = value);
                break;

            case Entry entry:
                AddProperty(
                    trackedObject,
                    nameof(Entry.Placeholder),
                    () => entry.Placeholder,
                    value => entry.Placeholder = value);
                break;

            case Editor editor:
                AddProperty(
                    trackedObject,
                    nameof(Editor.Placeholder),
                    () => editor.Placeholder,
                    value => editor.Placeholder = value);
                break;

            case SearchBar searchBar:
                AddProperty(
                    trackedObject,
                    nameof(SearchBar.Placeholder),
                    () => searchBar.Placeholder,
                    value => searchBar.Placeholder = value);
                break;

            case Picker picker:
                AddProperty(
                    trackedObject,
                    nameof(Picker.Title),
                    () => picker.Title,
                    value => picker.Title = value);
                break;

            case RadioButton radioButton
                when radioButton.Content is string:
                AddProperty(
                    trackedObject,
                    nameof(RadioButton.Content),
                    () => radioButton.Content as string,
                    value => radioButton.Content = value);
                break;

            case MenuItem menuItem:
                AddProperty(
                    trackedObject,
                    nameof(MenuItem.Text),
                    () => menuItem.Text,
                    value => menuItem.Text = value);
                break;
        }

        if (bindableObject is Page page)
        {
            AddProperty(
                trackedObject,
                nameof(Page.Title),
                () => page.Title,
                value => page.Title = value);
        }
        else if (bindableObject is BaseShellItem shellItem)
        {
            AddProperty(
                trackedObject,
                nameof(BaseShellItem.Title),
                () => shellItem.Title,
                value => shellItem.Title = value);
        }
    }

    private static void AddProperty(
        TrackedObject trackedObject,
        string propertyName,
        Func<string?> getter,
        Action<string> setter)
    {
        trackedObject.Properties.Add(
            new TrackedProperty
            {
                PropertyName =
                    propertyName,

                Getter =
                    getter,

                Setter =
                    setter,

                SourceText =
                    getter() ??
                    string.Empty
            });
    }

    private static void OnTrackedObjectPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (_isApplying ||
            sender is not BindableObject bindableObject ||
            !TrackedObjects.TryGetValue(
                bindableObject,
                out TrackedObject? trackedObject))
        {
            return;
        }

        IEnumerable<TrackedProperty> changedProperties =
            string.IsNullOrEmpty(
                e.PropertyName)
                ? trackedObject.Properties
                : trackedObject.Properties.Where(
                    property =>
                        property.PropertyName ==
                        e.PropertyName);

        foreach (TrackedProperty property
                 in changedProperties)
        {
            string? currentText =
                property.Getter();

            if (string.IsNullOrEmpty(
                    currentText))
            {
                continue;
            }

            string expectedText =
                Translate(
                    property.SourceText);

            if (string.Equals(
                    currentText,
                    expectedText,
                    StringComparison.Ordinal))
            {
                continue;
            }

            property.SourceText =
                currentText;

            ApplyTrackedProperty(
                property);
        }
    }

    private static void ApplyTrackedObject(
        TrackedObject trackedObject)
    {
        foreach (TrackedProperty property
                 in trackedObject.Properties)
        {
            ApplyTrackedProperty(
                property);
        }
    }

    private static void ApplyTrackedProperty(
        TrackedProperty property)
    {
        string translatedText =
            Translate(
                property.SourceText);

        if (string.Equals(
                property.Getter(),
                translatedText,
                StringComparison.Ordinal))
        {
            return;
        }

        _isApplying =
            true;

        try
        {
            property.Setter(
                translatedText);
        }
        finally
        {
            _isApplying =
                false;
        }
    }
}
