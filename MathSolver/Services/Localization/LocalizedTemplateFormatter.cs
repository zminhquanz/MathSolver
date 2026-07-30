using System.Globalization;
using System.Text.RegularExpressions;

namespace MathSolver.Services.Localization;

public static partial class LocalizedTemplateFormatter
{
    public static string Format(
        string template,
        IReadOnlyDictionary<string, object?> values,
        Func<string, string>? translateCapturedValue = null)
    {
        ArgumentNullException.ThrowIfNull(
            template);

        ArgumentNullException.ThrowIfNull(
            values);

        return PlaceholderRegex().Replace(
            template,
            match =>
            {
                string name =
                    match.Groups["name"].Value;

                if (!values.TryGetValue(
                        name,
                        out object? rawValue))
                {
                    return match.Value;
                }

                string? format =
                    match.Groups["format"].Success
                        ? match.Groups["format"].Value
                        : null;

                string value =
                    rawValue is IFormattable formattable
                        ? formattable.ToString(
                              format,
                              CultureInfo.CurrentCulture) ??
                          string.Empty
                        : Convert.ToString(
                              rawValue,
                              CultureInfo.CurrentCulture) ??
                          string.Empty;

                if (match.Groups["transform"].Success &&
                    string.Equals(
                        match.Groups["transform"].Value,
                        "translate",
                        StringComparison.OrdinalIgnoreCase) &&
                    translateCapturedValue is not null)
                {
                    value =
                        translateCapturedValue(
                            value);
                }

                return value;
            });
    }

    [GeneratedRegex(
        @"(?<!\{)\{(?<name>[A-Za-z0-9_.-]+)(?:\|(?<transform>[A-Za-z0-9_.-]+))?(?::(?<format>[^{}]+))?\}(?!\})",
        RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();
}
