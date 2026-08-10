using MathSolver.Services;
using MathSolver.Services.Localization;
using Microsoft.Maui.Controls.Xaml;
using System.ComponentModel;

namespace MathSolver.MarkupExtensions;

/// <summary>
/// Translation binding for the quiz surface. New AI strings have a built-in
/// Vietnamese/English fallback so older external language packs remain usable.
/// </summary>
[ContentProperty(nameof(Key))]
public sealed class QuizTranslateExtension :
    IMarkupExtension
{
    public string Key { get; set; } =
        string.Empty;

    public object ProvideValue(
        IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            return string.Empty;
        }

        return new Binding(
            nameof(QuizTranslationBindingSource.Value))
        {
            Source =
                new QuizTranslationBindingSource(Key),
            Mode = BindingMode.OneWay
        };
    }

    private sealed class QuizTranslationBindingSource :
        INotifyPropertyChanged
    {
        private readonly string _key;
        private readonly EventHandler _cultureChangedHandler;

        public QuizTranslationBindingSource(
            string key)
        {
            _key = key;

            var weakSource =
                new WeakReference<QuizTranslationBindingSource>(
                    this);

            EventHandler? handler = null;

            handler =
                (sender, args) =>
                {
                    if (weakSource.TryGetTarget(
                            out QuizTranslationBindingSource? source))
                    {
                        source.PropertyChanged?.Invoke(
                            source,
                            new PropertyChangedEventArgs(
                                nameof(Value)));
                        return;
                    }

                    if (handler is not null)
                    {
                        LocalizationService.CultureChanged -=
                            handler;
                    }
                };

            LocalizationService.Initialize();
            _cultureChangedHandler = handler;
            LocalizationService.CultureChanged +=
                _cultureChangedHandler;
        }

        public string Value
        {
            get
            {
                if (QuizLocalizationOverrides.TryGetValue(
                        _key,
                        LocalizationManager.Instance.CurrentCulture,
                        out string overrideValue))
                {
                    return overrideValue;
                }

                return LocalizationService.TranslateKey(
                    _key);
            }
        }

        public event PropertyChangedEventHandler?
            PropertyChanged;
    }
}
