using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class RemainderGroupsOpenMeaning
{
    private static string Clean(string text) => Regex.Replace(text.Trim().ToLowerInvariant(), @"\s+", " ",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)).TrimEnd(' ', '.', '?', ':', '!');
    private static bool Match(string text, string pattern) => Regex.IsMatch(Clean(text), @"\A(?:" + pattern + @")\z",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    internal static BasicDraftValidation Validate(BasicQuestionContract c, BasicQuestionDraft d)
    {
        if (!RemainderGroupsPilot.Supports(c.BankVariant) || d.Facts?.Count != 2 || d.SolutionLeads is null)
            return new(null, "ChangedRelationOrTarget");
        bool vi = c.Language == AppLanguage.Vietnamese;
        bool minimum = c.BankVariant == (int)ElementaryQuizType.MinimumGroups;
        bool Proof(string text, string vietnamese, string english) => Match(text, vi ? vietnamese : english);
        BasicDraftValidation Pending() => new(d, "SemanticReviewRequired", c,
            "The positive total, container capacity and exact minimum/full-container target could not all be proved.");
        string[] all = OpenQuestionProse.Fields(d).Select(f => f.Text).ToArray();
        // Qualifiers and negation must never be certified by finding a positive clause inside them.
        if (all.Any(t => Regex.IsMatch(Clean(t), vi
            ? @"\b(?:không|chẳng|chưa|nếu|chỉ khi|có thể không|giả sử)\b"
            : @"\b(?:not|never|no|if|unless|might|may|perhaps|suppose|assuming|isn't|doesn't|can't)\b",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))) return Pending();

        bool minimumQuestion = Proof(d.Question,
            @"(?:hỏi )?cần (?:ít nhất|tối thiểu) bao nhiêu \{v0\} để (?:chứa|xếp) hết|để (?:xếp|chứa) hết, cần (?:ít nhất|tối thiểu) bao nhiêu \{v0\}",
            @"what is the minimum number of \{v0\} (?:needed|required) to (?:hold|pack) (?:everything|all the items)|how many \{v0\} are (?:needed|required) at a minimum to (?:pack|hold) all the items|what is the fewest number of \{v0\} (?:needed|required) to hold all the items");
        bool fullQuestion = Proof(d.Question,
            @"(?:hỏi )?xếp được bao nhiêu \{v0\} đầy|có thể xếp đầy bao nhiêu \{v0\}|(?:có thể )?xếp được bao nhiêu \{v0\} đầy",
            @"how many full \{v0\} can be filled|how many \{v0\} can be completely filled|what is the number of completely full \{v0\}");
        bool wrong = minimum ? fullQuestion : minimumQuestion;
        if (!minimum)
        {
            wrong |= Proof(d.Question,
                @"cần dùng ít nhất bao nhiêu \{v0\} để chứa hết tất cả các vật phẩm",
                @"what is the minimum number of \{v0\} required to hold all items");
            wrong |= Proof(d.Question, @"(?!)",
                @"determine the minimum number of \{v0\} that must be used to contain all the items, and then state the count of completely full containers among them");
            wrong |= d.SolutionLeads.Any(s => Proof(s.Text,
                @"số \{v0\} tối thiểu cần dùng|số \{v0\} ít nhất cần dùng",
                @"(?:the )?minimum number of \{v0\}(?: (?:needed|required))?"));
        }
        wrong |= Proof(d.Question,
            @"còn (?:dư|lại) bao nhiêu \{v0\}|cần thêm bao nhiêu \{v0\}",
            @"how many \{v0\} are left over|how many additional \{v0\} are needed");
        wrong |= Proof(d.Facts[0].Text,
            @"mỗi \{v1\} (?:có|chứa) \{f0\}|\{f0\} là sức chứa của mỗi \{v1\}|mỗi đồ đựng chứa \{f0\} \{v1\}|sức chứa mỗi đồ đựng là \{f0\} \{v1\}",
            @"each \{v1\} (?:holds|contains) \{f0\}|the capacity of each \{v1\} is \{f0\}|each container holds \{f0\} \{v1\}|the capacity of each container is \{f0\} \{v1\}");
        wrong |= Proof(d.Facts[1].Text,
            @"có tất cả \{f1\} \{v1\} trong \{v2\}|\{v2\} có tất cả \{f1\} \{v1\}",
            @"there are \{f1\} \{v1\} in total in the \{v2\}|the \{v2\} hold \{f1\} \{v1\} altogether");
        wrong |= Proof(d.Facts[1].Text,
            @"mỗi \{v2\} chứa (?:ít nhất|tối thiểu) \{f1\} \{v1\}",
            @"each \{v2\} (?:holds|contains) at least \{f1\} \{v1\}");
        // These complete added events affirmatively change the available contents or capacity.
        wrong |= Proof(d.Facts[1].Text,
            @"mỗi \{v2\} (?:chứa được nhiều nhất|đầy chứa đúng) \{f1\} \{v1\}\. (?:có thêm chỗ trống|một số đồ vật bị mất)",
            @"each (?:full )?\{v2\} (?:can hold at most|holds exactly) \{f1\} \{v1\}\. (?:extra capacity is available|some items are lost)");
        if (wrong) return new(null, "ChangedRelationOrTarget", ErrorDetails: "The given total/capacity role or minimum/full-container target changed.");

        bool total = Proof(d.Facts[0].Text,
            @"có (?:tất cả|tổng cộng) \{f0\} \{v1\} cần xếp|tổng số \{v1\} cần xếp là \{f0\}|cần xếp (?:tất cả|tổng cộng) \{f0\} \{v1\}",
            @"there are \{f0\} \{v1\} to pack in total|the total number of \{v1\} to pack is \{f0\}|a total of \{f0\} \{v1\} (?:need|needs) to be packed|there are \{f0\} \{v1\} altogether to pack");
        bool capacity = minimum
            ? Proof(d.Facts[1].Text,
                @"mỗi \{v2\} chứa được (?:nhiều nhất|tối đa) \{f1\} \{v1\}|sức chứa tối đa của mỗi \{v2\} là \{f1\} \{v1\}|mỗi \{v2\} có thể chứa tối đa \{f1\} \{v1\}",
                @"each \{v2\} can hold (?:at most|up to) \{f1\} \{v1\}|the maximum capacity of each \{v2\} is \{f1\} \{v1\}|each \{v2\} has a maximum capacity of \{f1\} \{v1\}")
            : Proof(d.Facts[1].Text,
                @"mỗi \{v2\} đầy chứa đúng \{f1\} \{v1\}|một \{v2\} đầy có \{f1\} \{v1\}|mỗi \{v2\} có đúng \{f1\} \{v1\} khi đầy",
                @"each full \{v2\} holds exactly \{f1\} \{v1\}|a full \{v2\} contains \{f1\} \{v1\}|each \{v2\} contains exactly \{f1\} \{v1\} when full");
        bool fullLead = d.SolutionLeads.Count > 0 && Proof(d.SolutionLeads[0].Text,
            @"số \{v0\} đầy|số \{v0\} xếp đầy được",
            @"(?:the )?(?:number|count) of (?:completely )?full \{v0\}|(?:the )?number of \{v0\} completely filled");
        bool leads = fullLead && (minimum
            ? d.SolutionLeads.Count == 2 && Proof(d.SolutionLeads[1].Text,
                @"số \{v0\} tối thiểu cần dùng|số \{v0\} ít nhất cần dùng",
                @"(?:the )?minimum number of \{v0\} (?:needed|required)|(?:the )?fewest \{v0\} needed")
            : d.SolutionLeads.Count == 1);
        return total && capacity && (minimum ? minimumQuestion : fullQuestion) && leads ? new(d, null, c) : Pending();
    }
}
