using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

/// <summary>Whole-clause proofs only: qualifications and negations remain pending.</summary>
internal static class AverageRelationsOpenMeaning
{
    private static bool Match(string text,string rule)=>Regex.IsMatch(
        Regex.Replace(text.Trim().ToLowerInvariant(),@"\s+"," ",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)).TrimEnd(' ','.','?',':','!'),
        @"\A(?:"+rule+@")\z",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    private static string Slot(string id)=>@"\{"+id+@"\}";
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)
    {
        bool vi=c.Language==AppLanguage.Vietnamese,pooled=c.BankVariant==(int)AverageQuizType.TwoGroups,findTotal=c.BankVariant==(int)AverageQuizType.AverageToTotal;
        bool Proof(string text,string vietnamese,string english)=>Match(text,vi?vietnamese:english);
        bool Count(string text,string amount,string members,string owner)=>Proof(text,
            @"(?:có|tổng cộng có) "+Slot(amount)+" "+Slot(members)+" trong "+Slot(owner)+@"|số "+Slot(members)+" trong "+Slot(owner)+" là "+Slot(amount),
            @"there are "+Slot(amount)+" "+Slot(members)+" in "+Slot(owner)+@"|the number of "+Slot(members)+" in "+Slot(owner)+" is "+Slot(amount));
        bool Mean(string text,string amount,string member,string owner)=>Proof(text,
            @"trung bình mỗi "+Slot(member)+" trong "+Slot(owner)+@" (?:đã )?\{v4\} "+Slot(amount)+@" \{v3\}|mỗi "+Slot(member)+" trong "+Slot(owner)+@" (?:đã )?\{v4\} trung bình "+Slot(amount)+@" \{v3\}|số \{v3\} trung bình mỗi "+Slot(member)+" trong "+Slot(owner)+@" \{v4\} là "+Slot(amount),
            @"on average,? each "+Slot(member)+" in "+Slot(owner)+@" \{v4\} "+Slot(amount)+@" \{v3\}|each "+Slot(member)+" in "+Slot(owner)+@" \{v4\} an average of "+Slot(amount)+@" \{v3\}|the (?:average|mean) number of \{v3\} \{v4\} by each "+Slot(member)+" in "+Slot(owner)+@" (?:was|is) "+Slot(amount));
        bool WrongMean(string text,string amount,string member,string owner)=>Proof(text,
            @"mỗi "+Slot(member)+" trong "+Slot(owner)+@" \{v4\} đúng "+Slot(amount)+@" \{v3\}|tổng số \{v3\} do mỗi "+Slot(member)+" trong "+Slot(owner)+@" \{v4\} là "+Slot(amount),
            @"each "+Slot(member)+" in "+Slot(owner)+@" \{v4\} exactly "+Slot(amount)+@" \{v3\}|the total number of \{v3\} \{v4\} by each "+Slot(member)+" in "+Slot(owner)+" is "+Slot(amount));
        bool Total(string text)=>Proof(text,
            @"tổng số \{v3\} do tất cả các \{v1\} trong \{v0\} \{v4\} là \{f0\}|tất cả các \{v1\} trong \{v0\} \{v4\} tổng cộng \{f0\} \{v3\}",
            @"across every \{v1\} in \{v0\},? the total number of \{v3\} \{v4\} was \{f0\}|the combined total number of \{v3\} \{v4\} by every \{v1\} in \{v0\} (?:was|is) \{f0\}");
        bool changed;
        bool facts;
        if(pooled)
        {
            changed=WrongMean(d.Facts![1].Text,"f1","v2","v0")||WrongMean(d.Facts[3].Text,"f3","v2","v1")
                ||Proof(d.Facts[4].Text,@"các \{v6\} trong \{v0\} và \{v1\} trùng nhau",@"the \{v6\} in \{v0\} and \{v1\} overlap");
            facts=Count(d.Facts[0].Text,"f0","v6","v0")&&Mean(d.Facts[1].Text,"f1","v2","v0")
                &&Count(d.Facts[2].Text,"f2","v6","v1")&&Mean(d.Facts[3].Text,"f3","v2","v1")
                &&Proof(d.Facts[4].Text,@"các \{v6\} trong \{v0\} và \{v1\} không trùng nhau|\{v0\} và \{v1\} có các \{v6\} riêng biệt",
                    @"the \{v6\} in \{v0\} and \{v1\} are distinct|\{v0\} and \{v1\} have separate \{v6\}");
        }
        else
        {
            changed=findTotal?(WrongMean(d.Facts![1].Text,"f0","v1","v0")||Total(d.Facts[1].Text)):
                (Mean(d.Facts![1].Text,"f0","v1","v0")||WrongMean(d.Facts[1].Text,"f0","v1","v0"));
            facts=Count(d.Facts![0].Text,"f1","v2","v0")&&(findTotal?Mean(d.Facts[1].Text,"f0","v1","v0"):Total(d.Facts[1].Text));
        }
        string meanQuestionVi=pooled?@"trung bình mỗi \{v2\} của cả \{v0\} và \{v1\} \{v5\} bao nhiêu \{v3\}":@"trung bình mỗi \{v1\} trong \{v0\} \{v5\} bao nhiêu \{v3\}";
        string meanQuestionEn=pooled?@"how many \{v3\} did each \{v2\} across \{v0\} and \{v1\} \{v5\} on average|what average number of \{v3\} did each \{v2\} across \{v0\} and \{v1\} \{v5\}":@"how many \{v3\} did each \{v1\} in \{v0\} \{v5\} on average|what average number of \{v3\} did each \{v1\} in \{v0\} \{v5\}";
        bool question=pooled||!findTotal?Proof(d.Question,@"(?:hỏi |vậy )?"+meanQuestionVi,meanQuestionEn):Proof(d.Question,
            @"(?:hỏi |vậy )?tất cả các \{v2\} trong \{v0\} \{v5\} tổng cộng bao nhiêu \{v3\}",
            @"how many \{v3\} did all the \{v2\} in \{v0\} \{v5\} in total|what total number of \{v3\} did all the \{v2\} in \{v0\} \{v5\}");
        changed|=pooled?Proof(d.Question,@"(?:hỏi )?mỗi \{v2\} của cả \{v0\} và \{v1\} \{v5\} tổng cộng bao nhiêu \{v3\}",
            @"how many \{v3\} did each \{v2\} across \{v0\} and \{v1\} \{v5\} in total"):
            findTotal?Proof(d.Question,@"trung bình các \{v2\} trong \{v0\} \{v5\} bao nhiêu \{v3\}",
                @"how many \{v3\} did the \{v2\} in \{v0\} \{v5\} on average"):
                Proof(d.Question,@"mỗi \{v1\} trong \{v0\} \{v5\} tổng cộng bao nhiêu \{v3\}",
                @"how many \{v3\} did each \{v1\} in \{v0\} \{v5\} in total");
        bool lead=pooled?Proof(d.SolutionLeads![0].Text,
            @"số \{v3\} trung bình mỗi \{v2\} của cả \{v0\} và \{v1\}",@"(?:the )?(?:average|mean) number of \{v3\} per \{v2\} across \{v0\} and \{v1\}"):
            findTotal?Proof(d.SolutionLeads![0].Text,@"tổng số \{v3\} của tất cả các \{v2\} trong \{v0\}",@"(?:the )?total number of \{v3\} for all the \{v2\} in \{v0\}"):
                Proof(d.SolutionLeads![0].Text,@"số \{v3\} trung bình mỗi \{v1\} trong \{v0\}",@"(?:the )?(?:average|mean) number of \{v3\} per \{v1\} in \{v0\}");
        changed|=pooled?Proof(d.SolutionLeads![0].Text,@"tổng số \{v3\} mỗi \{v2\} của cả \{v0\} và \{v1\}",@"(?:the )?total number of \{v3\} per \{v2\} across \{v0\} and \{v1\}"):
            findTotal?Proof(d.SolutionLeads![0].Text,@"số \{v3\} trung bình của tất cả các \{v2\} trong \{v0\}",@"(?:the )?(?:average|mean) number of \{v3\} for all the \{v2\} in \{v0\}"):
                Proof(d.SolutionLeads![0].Text,@"tổng số \{v3\} mỗi \{v1\} trong \{v0\}",@"(?:the )?total number of \{v3\} per \{v1\} in \{v0\}");
        if(changed)return new(null,"ChangedRelationOrTarget",ErrorDetails:"The given total, per-member mean, membership scope or requested quantity changed.");
        return facts&&question&&lead?new(d,null,c):new(d,"SemanticReviewRequired",c,"Given count/mean/total roles, member scope or requested average relation could not be proved.");
    }
}
