using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class AverageMissingOpenMeaning
{
    private static bool Match(string text,string rule)=>Regex.IsMatch(
        Regex.Replace(text.Trim().ToLowerInvariant(),@"\s+"," ",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)).TrimEnd(' ','.','?',':','!'),
        @"\A(?:"+rule+@")\z",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        bool Proof(string text,string vietnamese,string english)=>Match(text,vi?vietnamese:english);
        // Only complete, recognized contradictory claims trigger a retry. Unrecognized
        // clauses (including negated or qualified claims) remain available for review.
        bool wrongMean=Proof(d.Facts![2].Text,
            @"(?:tổng số|tổng cộng) \{v3\} (?:mà |do )?mỗi \{v4\} \{v5\} là \{f2\}|mỗi \{v4\} \{v5\} đúng \{f2\} \{v3\}",
            @"the total (?:number|amount) of \{v3\} \{v5\} by each \{v4\} is \{f2\}|each \{v4\} \{v5\} exactly \{f2\} \{v3\}");
        bool wrongScope=Proof(d.Facts[3].Text,@"có \{f3\} \{v7\} (?:không độc lập|trùng nhau)",
            @"there are \{f3\} (?:overlapping|dependent) \{v7\}");
        bool wrongQuestion=Proof(d.Question,
            @"(?:hỏi )?trung bình \{v2\} \{v6\} bao nhiêu \{v3\}|(?:hỏi )?\{v2\} \{v6\} bao nhiêu \{v3\} mỗi người",
            @"how many \{v3\} did \{v2\} \{v6\},? (?:on average|per person)");
        if(wrongMean||wrongScope||wrongQuestion)
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"The mean, independent observations or missing-group target changed.");
        for(int i=0;i<2;i++)
            if(Proof(d.Facts[i].Text,@"trung bình \{v"+i+@"\} \{v5\} \{f"+i+@"\} \{v3\}",
                @"on average,? \{v"+i+@"\} \{v5\} \{f"+i+@"\} \{v3\}"))
                return new(null,"ChangedRelationOrTarget",ErrorDetails:"An actual observation was changed to a group mean.");
        bool facts=true;
        for(int i=0;i<2;i++)
        {
            string owner=@"\{v"+i+@"\}",amount=@"\{f"+i+@"\}";
            facts&=Proof(d.Facts[i].Text,
                owner+@" (?:đã )?\{v5\} "+amount+@" \{v3\}|(?:số|số lượng) \{v3\} (?:mà |do )?"+owner+@" (?:đã )?\{v5\} là "+amount+@"|có "+amount+@" \{v3\} do "+owner+@" (?:đã )?\{v5\}",
                owner+@" (?:has )?\{v5\} "+amount+@" \{v3\}|"+amount+@" \{v3\} (?:was|were) \{v5\} by "+owner+@"|the (?:number|amount) of \{v3\} \{v5\} by "+owner+@" (?:was|is) "+amount);
        }
        bool mean=Proof(d.Facts[2].Text,
            @"trung bình mỗi \{v4\} (?:đã )?\{v5\} \{f2\} \{v3\}|mỗi \{v4\} (?:đã )?\{v5\} trung bình \{f2\} \{v3\}|(?:số|số lượng) \{v3\} trung bình (?:mà )?mỗi \{v4\} \{v5\} là \{f2\}",
            @"on average,? each \{v4\} \{v5\} \{f2\} \{v3\}|the (?:average|mean) (?:number|amount) of \{v3\} \{v5\} by each \{v4\} (?:was|is) \{f2\}");
        bool scope=Proof(d.Facts[3].Text,
            @"(?:có|tổng cộng có|số liệu thuộc|các số liệu thuộc) \{f3\} \{v7\} (?:độc lập|riêng biệt)",
            @"(?:there are|the observations cover) \{f3\} (?:independent|separate) \{v7\}");
        bool question=Proof(d.Question,
            @"(?:hỏi |vậy |vậy hỏi |tổng cộng )?\{v2\} (?:đã )?\{v6\} bao nhiêu \{v3\}|(?:số|số lượng) \{v3\} (?:mà |do )?\{v2\} \{v6\} là bao nhiêu",
            @"how many \{v3\} did \{v2\} \{v6\}(?: in total)?|what (?:number|amount) of \{v3\} did \{v2\} \{v6\}");
        bool lead=Proof(d.SolutionLeads![0].Text,
            @"(?:số|số lượng) \{v3\} (?:của|do) \{v2\}",
            @"(?:the )?(?:number|amount) of \{v3\} for \{v2\}");
        return facts&&mean&&scope&&question&&lead?new(d,null,c):new(d,"SemanticReviewRequired",c,
            "Two known observations, the mean of all three independent groups, or the missing-group target could not be proved.");
    }
}
