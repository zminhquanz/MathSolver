using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class SumDifferenceOpenMeaning
{
    private static bool Match(string text,string rule)=>Regex.IsMatch(
        Regex.Replace(text.Trim().ToLowerInvariant(),@"\s+"," ",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)).TrimEnd(' ','.','?',':','!'),
        @"\A(?:"+rule+@")\z",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        bool Proof(string text,string vietnamese,string english)=>Match(text,vi?vietnamese:english);
        string owners=@"(?:\{v0\} và \{v1\}|\{v1\} và \{v0\})",enOwners=@"(?:\{v0\} and \{v1\}|\{v1\} and \{v0\})";
        bool wrongTotal=Proof(d.Facts![0].Text,owners+@" mỗi bên có \{f0\} \{v2\}",enOwners+@" each have \{f0\} \{v2\}");
        string Owner(int i)=>@"\{v"+i+@"\}";
        bool More(string text,int more,int less)=>Proof(text,
            Owner(more)+@" (?:có|sở hữu|giữ) nhiều hơn "+Owner(less)+@" (?:(?:là|đúng) )?\{f1\} \{v2\}"
                +@"|so với "+Owner(less)+", "+Owner(more)+@" có nhiều hơn \{f1\} \{v2\}",
            Owner(more)+@" (?:has|possesses) (?:exactly )?\{f1\} more \{v2\} than "+Owner(less)
                +"|"+Owner(more)+@" (?:has|possesses) \{f1\} \{v2\} more than "+Owner(less)
                +"|"+Owner(more)+@" (?:has|possesses) \{f1\} additional \{v2\} compared to "+Owner(less));
        bool Fewer(string text,int less,int more)=>Proof(text,
            Owner(less)+@" (?:có|sở hữu|giữ) ít hơn "+Owner(more)+@" (?:(?:là|đúng) )?\{f1\} \{v2\}",
            Owner(less)+@" (?:has|possesses) (?:exactly )?\{f1\} fewer \{v2\} than "+Owner(more));
        bool reversed=More(d.Facts[1].Text,1,0)||Fewer(d.Facts[1].Text,0,1);
        bool changed=Proof(d.Facts[1].Text,
            @"\{v0\} tặng \{v1\} \{f1\} \{v2\}|\{v0\} có gấp \{f1\} lần số \{v2\} của \{v1\}",
            @"\{v0\} (?:gave|gives) \{v1\} \{f1\} \{v2\}|\{v0\} has \{f1\} times as many \{v2\} as \{v1\}");
        bool changedTarget=Proof(d.Question,
            @"(?:hỏi )?\{v0\} và \{v1\} có tất cả bao nhiêu \{v2\}|(?:hỏi )?hiệu số \{v2\} của \{v0\} và \{v1\} là bao nhiêu",
            @"how many \{v2\} do \{v0\} and \{v1\} have (?:in total|altogether)|what is the difference in \{v2\} between \{v0\} and \{v1\}");
        if(wrongTotal||reversed||changed||changedTarget)
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"Combined total, larger/smaller ownership, comparison direction or both-amount target changed.");
        bool total=Proof(d.Facts[0].Text,
            owners+@" (?:có|sở hữu) (?:tất cả|tổng cộng|chung) \{f0\} \{v2\}|(?:tổng số|tổng cộng số) \{v2\} của "+owners+@" là \{f0\}",
            @"(?:together,? )?"+enOwners+@" have \{f0\} \{v2\}(?: (?:in total|altogether))?|the (?:(?:total|combined) (?:number|amount|count)|combined total) of \{v2\} (?:belonging to|owned by|held by|for) "+enOwners+@" (?:is|equals|reaches|amounts to) \{f0\}");
        bool difference=More(d.Facts[1].Text,0,1)||Fewer(d.Facts[1].Text,1,0);
        string EnglishPair(int first,int second)=>
            @"(?:how many \{v2\}|what (?:quantity|number|amount|count) of \{v2\}) does "+Owner(first)
            +@" (?:have|possess|hold), and (?:how many|what (?:quantity|number|amount|count)) does "+Owner(second)+@" (?:have|possess|hold)"
            +@"|what is the (?:amount|quantity|number|count) of \{v2\} (?:held by|owned by|belonging to) "+Owner(first)
            +@", and what is the (?:amount|quantity|number|count) (?:held by|owned by|belonging to) "+Owner(second);
        bool question=Proof(d.Question,
            @"(?:hỏi )?\{v0\} có bao nhiêu \{v2\}, \{v1\} có bao nhiêu|(?:hỏi )?số \{v2\} của "+owners+@" lần lượt là bao nhiêu",
            EnglishPair(0,1)+"|"+EnglishPair(1,0)+@"|how many \{v2\} do "+enOwners+@" have (?:respectively|each)");
        bool Lead(string text,int owner)=>Proof(text,@"(?:số|số lượng) \{v2\} của \{v"+owner+@"\}",
            @"(?:the )?(?:number|quantity|count|amount) of \{v2\} (?:for|belonging to) \{v"+owner+@"\}");
        bool leads=Lead(d.SolutionLeads![0].Text,1)&&Lead(d.SolutionLeads[1].Text,0);
        return total&&difference&&question&&leads?new(d,null,c):new(d,"SemanticReviewRequired",c,
            "The direct combined total, oriented difference, both owner-specific targets or solution headings could not be proved.");
    }
}
