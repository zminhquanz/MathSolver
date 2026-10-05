using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

/// <summary>Role checks for the additional one-operation relationships, not keyword inference.</summary>
public static class OneStepRelationRules
{
    public static bool IsExtended(BasicQuestionStructure s) => s is BasicQuestionStructure.SubComparisonLess
        or BasicQuestionStructure.SubComparisonInverse or BasicQuestionStructure.FindPart or BasicQuestionStructure.CompareFactor;

    public static BasicDraftValidation Validate(BasicQuestionDraft d, BasicQuestionContract c)
    {
        bool vi = c.Language == AppLanguage.Vietnamese;
        const string n = @"\{name\}", o = @"\{other\}", u = @"\{unit\}";
        string owns = vi ? @"(?:có|giữ|sở hữu)" : @"(?:has|owns|holds)";
        string Fact(string actor, string quantity) => actor + @"\s+" + owns + @"\s+" + quantity + @"\s+" + u;
        bool Match(string text, string pattern) => Regex.IsMatch(text.Trim(), @"\A" + pattern + @"\s*[,.:?]?\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        string a = c.Structure == BasicQuestionStructure.FindPart
            ? vi ? n + @"\s+và\s+" + o + @"\s+có\s+(?:tổng cộng|tất cả)\s+\{a\}\s+" + u
                : n + @"\s+and\s+" + o + @"\s+have\s+(?:a total of|altogether)\s+\{a\}\s+" + u
            : Fact(o, @"\{a\}");
        string b = c.Structure switch
        {
            BasicQuestionStructure.SubComparisonLess => vi
                ? n + @"\s+" + owns + @"\s+ít hơn\s+" + o + @"\s+(?:là\s+)?\{b\}\s+" + u
                : n + @"\s+" + owns + @"\s+\{b\}\s+fewer\s+" + u + @"\s+than\s+" + o,
            BasicQuestionStructure.SubComparisonInverse => vi
                ? o + @"\s+" + owns + @"\s+nhiều hơn\s+" + n + @"\s+(?:là\s+)?\{b\}\s+" + u
                : o + @"\s+" + owns + @"\s+\{b\}\s+more\s+" + u + @"\s+than\s+" + n,
            BasicQuestionStructure.FindPart => Fact(o, @"\{b\}"),
            _ => Fact(n, @"\{b\}")
        };
        bool ratio = c.Structure == BasicQuestionStructure.CompareFactor;
        string question = ratio
            ? vi ? @"(?:Hỏi\s+)?số\s+" + u + @"\s+của\s+" + o + @"\s+gấp\s+(?:mấy|bao nhiêu)\s+lần\s+số\s+" + u + @"\s+của\s+" + n
                : @"How many times as many\s+" + u + @"\s+does\s+" + o + @"\s+(?:have|own)\s+as\s+" + n
            : vi ? @"(?:Hỏi\s+)?" + n + @"\s+(?:có|sở hữu)\s+bao nhiêu\s+" + u
                : @"How many\s+" + u + @"\s+does\s+" + n + @"\s+(?:have|own)";
        string lead = ratio
            ? vi ? @"Số lần lượng của\s+" + o + @"\s+gấp lượng của\s+" + n + @"\s+là"
                : @"The number of times the amount of\s+" + o + @"\s+is that of\s+" + n + @"\s+is"
            : vi ? @"Số\s+" + u + @"\s+(?:mà\s+)?" + n + @"\s+(?:có|sở hữu)\s+là"
                : @"The number of\s+" + u + @"\s+that\s+" + n + @"\s+(?:has|owns)\s+is";
        // Whole-clause matching prevents an otherwise correct substring from hiding
        // reversed actors, transfers, extra conditions or another question.
        return Match(d.GivenA, a) && Match(d.GivenB, b) && Match(d.Question, question) && Match(d.SolutionLead!, lead)
            ? new(d, null, c) : new(null, "ChangedRelationOrTarget");
    }
}
