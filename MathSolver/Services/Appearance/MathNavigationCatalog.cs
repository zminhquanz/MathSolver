namespace MathSolver.Services;

/// <summary>Shared navigation identity; translated labels never contain icon glyphs.</summary>
public static class MathNavigationCatalog
{
    public const string FontAlias = "MathNavigationIcons";
    public const double IconSize = 24;

    // Order matches generate-navigation-icons.mjs. SVG originals remain the design source.
    public static string? GlyphFor(string? key) => key switch
    {
        "Calculation.Tabs.Basic" => "\ue900",
        "Calculation.Tabs.NumberTheory" => "\ue901",
        "Calculation.Tabs.Average" or "Formula.Tabs.Average" => "\ue902",
        "Calculation.Tabs.PowerRoot" => "\ue903",
        "Calculation.Tabs.Fraction" => "\ue904",
        "Calculation.Tabs.FindX" or "Formula.Tabs.UnknownComponent" => "\ue905",
        "Calculation.Tabs.Quadratic" => "\ue906",
        "Calculation.Tabs.Geometry" or "Formula.Tabs.Geometry" => "\ue907",
        "Calculation.Tabs.Matrix" => "\ue908",
        "Formula.Tabs.Proportion" => "\ue909",
        "Formula.Tabs.Motion" => "\ue90a",
        "Formula.Tabs.Measurement" => "\ue90b",
        _ => null
    };

    /// <summary>Accept older user language packs without showing their old icon twice.</summary>
    public static string LabelFor(string key, string translated)
    {
        string? legacyPrefix = key switch
        {
            "Calculation.Tabs.Basic" => "🧮",
            "Calculation.Tabs.NumberTheory" => "🔢",
            "Calculation.Tabs.Average" => "📊",
            "Calculation.Tabs.PowerRoot" => "⚡",
            "Calculation.Tabs.Fraction" => "½",
            "Calculation.Tabs.FindX" => "🔎",
            "Calculation.Tabs.Quadratic" => "📈",
            "Calculation.Tabs.Geometry" => "📐",
            "Calculation.Tabs.Matrix" => "▦",
            "Formula.Tabs.UnknownComponent" => "𝑥",
            "Formula.Tabs.Proportion" => "↗↘⤢",
            "Formula.Tabs.Motion" => "➜",
            "Formula.Tabs.Average" => "x̄",
            "Formula.Tabs.Measurement" => "↔",
            "Formula.Tabs.Geometry" => "△",
            _ => null
        };
        // Only strip the known decorative prefix when separated from the real label.
        return legacyPrefix is not null && translated.StartsWith(legacyPrefix, StringComparison.Ordinal)
            && translated.Length > legacyPrefix.Length && char.IsWhiteSpace(translated[legacyPrefix.Length])
            ? translated[legacyPrefix.Length..].TrimStart() : translated;
    }

    public static string TranslateLabel(string key) => LabelFor(key, LocalizationService.TranslateKey(key));
}
