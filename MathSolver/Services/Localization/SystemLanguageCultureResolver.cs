using System.Globalization;

namespace MathSolver.Services.Localization;

internal static class SystemLanguageCultureResolver
{
    internal static string Resolve(
        CultureInfo systemCulture,
        IEnumerable<string> availableCultures,
        string fallbackCulture)
    {
        ArgumentNullException.ThrowIfNull(systemCulture);
        ArgumentNullException.ThrowIfNull(availableCultures);

        string[] available = availableCultures
            .Where(culture => !string.IsNullOrWhiteSpace(culture))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // A region-specific pack wins; for example, en-GB takes precedence
        // over en-US if both have been added to the app.
        string? exact = available.FirstOrDefault(culture =>
            string.Equals(culture, systemCulture.Name,
                StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        // Use an installed variant of the system language when an exact
        // region is unavailable (for example, en-GB -> en-US).
        string? sameLanguage = available.FirstOrDefault(culture =>
        {
            try
            {
                return string.Equals(
                    CultureInfo.GetCultureInfo(culture).TwoLetterISOLanguageName,
                    systemCulture.TwoLetterISOLanguageName,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (CultureNotFoundException)
            {
                return false;
            }
        });

        return sameLanguage ?? fallbackCulture;
    }
}
