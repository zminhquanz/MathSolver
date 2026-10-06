using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

/// <summary>Identity of the question wording, without random numbers, actors or solution prose.</summary>
public static class QuestionProseIdentity
{
    public static string Hash(BasicQuestionContract contract, BasicQuestionDraft draft)
    {
        string prose = draft.ProblemText.Normalize(NormalizationForm.FormC).ToLowerInvariant();
        if (contract.IsTemplate)
        {
            // Substitute numeric/actor slots before rendering fixed context and units, so
            // random values cannot change singular/plural wording in the identity.
            prose = prose.Replace("{a}", " quantity ").Replace("{b}", " quantity ")
                .Replace("{name}", " actor_a ").Replace("{other}", " actor_b ");
            prose = BasicQuestionTemplates.Render(prose, contract);
        }
        else
        {
            prose = Regex.Replace(prose, @"(?<!\p{L})" + Regex.Escape(contract.Subject) + @"(?!\p{L})",
                " actor_a ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        // Legacy numeric drafts can still be compared independently of their C# facts.
        // Preserve digits embedded in unit names such as m2.
        prose = Regex.Replace(prose, @"(?<![\p{L}\p{N}])\p{Nd}+(?:[.,]\p{Nd}+)*(?![\p{L}\p{N}])", " quantity ");
        string normalized = string.Join(' ', Regex.Matches(prose.ToLowerInvariant(), @"[\p{L}\p{M}\p{N}]+")
            .Select(match => match.Value));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{contract.Language}\n{normalized}")));
    }
}
