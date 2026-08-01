using MathSolver.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using System.ComponentModel;

namespace MathSolver.MarkupExtensions;

/// <summary>
/// Binds XAML text to a stable localization key.
///
/// The binding does not use an indexer path such as:
/// [Tabs.TimesTables]
///
/// .NET MAUI splits binding paths on periods, including periods inside
/// indexer text, which can throw:
/// "Indexer did not contain closing bracket".
///
/// Instead, each binding uses the normal property path "Value".
/// </summary>
[ContentProperty(nameof(Key))]
public sealed class TranslateExtension :
    IMarkupExtension
{
    public string Key { get; set; } =
        string.Empty;

    public object ProvideValue(
        IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(
                Key))
        {
            return string.Empty;
        }

        return new Binding(
            nameof(
                TranslationBindingSource.Value))
        {
            Source =
                new TranslationBindingSource(
                    Key),

            Mode =
                BindingMode.OneWay
        };
    }

    /// <summary>
    /// Exposes one ordinary property for the MAUI Binding engine.
    /// Stable keys may safely contain periods because they are no longer
    /// placed inside the Binding.Path string.
    /// </summary>
    private sealed class TranslationBindingSource :
        INotifyPropertyChanged
    {
        private readonly string _key;

        // Keep the delegate so it remains stable while this source exists.
        private readonly EventHandler
            _cultureChangedHandler;

        public TranslationBindingSource(
            string key)
        {
            _key =
                key;

            var weakSource =
                new WeakReference<
                    TranslationBindingSource>(
                    this);

            EventHandler? handler =
                null;

            handler =
                (sender, args) =>
                {
                    if (weakSource.TryGetTarget(
                            out TranslationBindingSource?
                                source))
                    {
                        source.PropertyChanged?.Invoke(
                            source,
                            new PropertyChangedEventArgs(
                                nameof(Value)));

                        return;
                    }

                    // The target control and its binding source were released.
                    // Remove the dead handler the next time the active
                    // language pack finishes changing.
                    if (handler is not null)
                    {
                        LocalizationService.CultureChanged -=
                            handler;
                    }
                };

            LocalizationService.Initialize();

            _cultureChangedHandler =
                handler;

            LocalizationService.CultureChanged +=
                _cultureChangedHandler;
        }

        public string Value =>
            LocalizationService.TranslateKey(
                _key);

        public event PropertyChangedEventHandler?
            PropertyChanged;
    }
}
