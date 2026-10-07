using MathSolver.Services;
using System.Globalization;
using System.Text;

namespace MathSolver.Services.QuestionBank;

/// <summary>Character policy shared by native sampling and all persisted prose validators.</summary>
internal static class QuestionProseLanguage
{
    // Whole Latin blocks include Polish, Turkish and phonetic letters, not just Vietnamese.
    private const string VietnameseLower = "àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵđ";
    private static readonly string VietnameseLetters = VietnameseLower + VietnameseLower.ToUpperInvariant();

    // Keep native GBNF itself ASCII; Unicode code points are escaped explicitly.
    internal static string GrammarLetters(AppLanguage language) => "A-Za-z" + (language == AppLanguage.Vietnamese
        ? string.Concat(VietnameseLetters.Select(ch => "\\u" + ((int)ch).ToString("x4", CultureInfo.InvariantCulture))) : "");

    internal static string? ValidateAndNormalize(BasicQuestionDraft draft, AppLanguage language,
        out BasicQuestionDraft normalized)
    {
        normalized = draft;
        if (language is not (AppLanguage.Vietnamese or AppLanguage.English)) return "WrongLanguage";
        try
        {
            normalized = draft with {
                GivenA = draft.GivenA.Normalize(NormalizationForm.FormC),
                GivenB = draft.GivenB.Normalize(NormalizationForm.FormC),
                Question = draft.Question.Normalize(NormalizationForm.FormC),
                SolutionLead = draft.SolutionLead?.Normalize(NormalizationForm.FormC),
                Facts = draft.Facts?.Select(f => f with { Text = f.Text.Normalize(NormalizationForm.FormC) }).ToArray(),
                SolutionLeads = draft.SolutionLeads?.Select(f => f with { Text = f.Text.Normalize(NormalizationForm.FormC) }).ToArray()
            };
        }
        catch (ArgumentException) { return "InvalidText"; }
        foreach (string text in new[] { normalized.GivenA, normalized.GivenB, normalized.Question,
            normalized.SolutionLead ?? "" }.Concat(normalized.Facts?.Select(f => f.Text) ?? []).Concat(normalized.SolutionLeads?.Select(f => f.Text) ?? []))
        foreach (char ch in text)
        {
            // ASCII controls, quantities and placeholders are checked by the field
            // validators. Check letters before any accent-folding of relation words.
            if (ch <= 127) continue;
            if (char.IsLetter(ch))
            {
                if (language != AppLanguage.Vietnamese || !VietnameseLetters.Contains(ch)) return "WrongLanguage";
            }
            else if (!char.IsWhiteSpace(ch) && !"‘’“”–—…²³°".Contains(ch))
                return "InvalidText"; // Orphan marks, replacement glyphs, emoji, bidi/zero-width text.
        }
        return null;
    }
}
