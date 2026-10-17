using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class AverageOpenMeaning
{
    private static readonly TimeSpan Timeout=TimeSpan.FromMilliseconds(100);
    private static string Clean(string s)=>Regex.Replace(s.Trim().ToLowerInvariant(),@"\s+"," ",RegexOptions.CultureInvariant,Timeout).TrimEnd(' ','.','?',':','!');
    private static bool Match(string text,string rule)=>Regex.IsMatch(Clean(text),@"\A(?:"+rule+@")\z",RegexOptions.CultureInvariant,Timeout);
    private static bool Has(string text,string rule)=>Regex.IsMatch(text,rule,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,Timeout);
    private static BasicDraftValidation Review(BasicQuestionContract c,BasicQuestionDraft d,string reason)
        =>new(d,"SemanticReviewRequired",c,reason);
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        string all=d.ProblemText+" "+d.SolutionLeads![0].Text;
        bool asksMean=Has(d.Question,vi?@"\btrung bình\b":@"\b(?:average|mean)\b");
        if(!asksMean&&Match(d.Question,vi?@"(?:hỏi )?mỗi \{v4\} (?:đã )?\{v6\} bao nhiêu \{v3\}"
            :@"(?:how many \{v3\}|what (?:amount|number) of \{v3\}) did each \{v4\} \{v6\}"))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"The question asks individual group observations, not their arithmetic mean.");
        if(Has(all,vi?@"\b(?:không độc lập|cùng một nhóm|trùng nhau|trùng lặp|chuyển sang|nhiều hơn|ít hơn|mỗi người|mỗi học sinh|có trọng số)\b"
            :@"\b(?:not independent|same group|overlap|transferred|more than|less than|per person|each person|per pupil|weighted)\b")
            ||d.Facts!.Take(3).Any(f=>Has(f.Text,vi?@"\btrung bình\b":@"\b(?:average|mean)\b"))
            ||!Has(d.Question,vi?@"\btrung bình\b":@"\b(?:average|mean)\b")&&Has(d.Question,vi?@"\b(?:tổng cộng|tất cả|nhiều nhất|ít nhất)\b":@"\b(?:total|altogether|maximum|minimum)\b"))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"Group independence, observation roles or requested mean changed.");
        // Syntax/relationship checks cannot certify fluent pupil-facing prose.
        // Preserve awkward drafts for review instead of repairing or retrying them.
        string? wording=EditorialConcern(c,d);
        if(wording is not null)return Review(c,d,"Editorial review required: "+wording);
        bool facts=true;
        for(int i=0;i<3;i++)
        {
            string owner=@"\{v"+i+@"\}",amount=@"\{f"+i+@"\}",unit=@"\{v3\}",verb=@"\{v5\}";
            facts &= vi
                ? Match(d.Facts![i].Text,owner+@" (?:đã )?"+verb+" "+amount+" "+unit)
                    ||Match(d.Facts[i].Text,@"(?:số|số lượng) "+unit+@" (?:mà |do )?"+owner+@" (?:đã )?"+verb+@" là "+amount)
                    ||Match(d.Facts[i].Text,@"có "+amount+" "+unit+@" do "+owner+@" (?:đã )?"+verb)
                : Match(d.Facts![i].Text,owner+@" (?:has )?"+verb+" "+amount+" "+unit)
                    ||Match(d.Facts[i].Text,amount+" "+unit+@" (?:was|were) "+verb+@" by "+owner)
                    ||Match(d.Facts[i].Text,@"the (?:number|amount) of "+unit+" "+verb+@" by "+owner+@" (?:was|is) "+amount);
        }
        bool scope=vi?Match(d.Facts![3].Text,@"(?:có|tổng cộng có|số liệu thuộc|các số liệu thuộc) \{f3\} \{v7\} (?:độc lập|riêng biệt)")
            :Match(d.Facts![3].Text,@"(?:there are|the observations cover) \{f3\} (?:independent|separate) \{v7\}");
        bool question=vi?Match(d.Question,@"(?:hỏi )?trung bình (?:mỗi|một) \{v4\} (?:đã )?\{v6\} bao nhiêu \{v3\}")
            ||Match(d.Question,@"(?:hỏi )?mỗi \{v4\} (?:đã )?\{v6\} trung bình bao nhiêu \{v3\}")
            ||Match(d.Question,@"(?:số|số lượng) \{v3\} trung bình (?:mỗi|một) \{v4\} \{v6\} là bao nhiêu")
            :Match(d.Question,@"(?:on average,? )?how many \{v3\} did each \{v4\} \{v6\}(?:,? on average)?");
        bool lead=vi?Match(d.SolutionLeads![0].Text,@"(?:số|số lượng) \{v3\} trung bình mỗi \{v4\}")
            :Match(d.SolutionLeads![0].Text,@"(?:the )?average (?:number|amount) of \{v3\} per \{v4\}");
        return facts&&scope&&question&&lead&&asksMean?new(d,null,c):Review(c,d,
            "Complete independent observations, clear per-group mean question or solution target could not be proved.");
    }

    private static string? EditorialConcern(BasicQuestionContract c,BasicQuestionDraft d)
    {
        if(d.Facts!.Take(3).Any(f=>Has(f.Text,@"(?:^|[.!?])\s*\{v5\}")))
            return "An action placeholder is detached in a separate sentence; keep it with its group observation.";
        if(c.Language==AppLanguage.Vietnamese)
        {
            if(Has(d.Question,@"\{v3\}\s+bao nhiêu\b"))return "The unit precedes 'bao nhiêu'; check Vietnamese question word order.";
            var context=AverageOpenPilot.Create(c.SceneId,c.Language,c.Tier,new(c.Story!.Seed)).Context;
            string expanded=d.Question.Replace("{v6}",context.BaseVerb,StringComparison.Ordinal);
            if(Regex.Matches(expanded,Regex.Escape(context.BaseVerb),RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,Timeout).Count>1
                ||Has(d.Question,@"\{v6\}\s+(?:trung bình\s+)?được\b"))
                return "The question repeats the action or 'được' already contained in the action binding.";
        }
        else
        {
            if(Regex.Matches(d.Question,@"\b(?:average|mean)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,Timeout).Count>1)
                return "The question repeats the average/mean phrase.";
            if(Has(d.Question,@"\beach \{v4\} \{v6\}")&&!Has(d.Question,@"\bdid each \{v4\} \{v6\}"))
                return "The base action after 'each group' lacks 'did'; check tense and subject-verb agreement.";
        }
        return null;
    }
}
