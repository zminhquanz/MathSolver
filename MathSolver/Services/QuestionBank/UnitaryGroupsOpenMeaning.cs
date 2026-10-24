using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class UnitaryGroupsOpenMeaning
{
    private static bool Match(string text,string pattern)=>Regex.IsMatch(
        Regex.Replace(text.Trim().ToLowerInvariant(),@"\s+"," ",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)).TrimEnd(' ','.','?',':','!'),
        @"\A(?:"+pattern+@")\z",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        bool Proof(string text,string v,string e)=>Match(text,vi?v:e);
        string bare=string.Join(" ",OpenQuestionProse.Fields(d).Select(f=>Regex.Replace(f.Text,@"\{[fv]\d+\}","")));
        if(Regex.IsMatch(bare,vi?@"\b(?:bút|quà|vở|sách|hộp|túi|chồng|táo)\b":@"\b(?:pens?|gifts?|notebooks?|books?|box(?:es)?|bags?|stacks?|apples?)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)))
            return new(null,"ChangedUnits",ErrorDetails:"Keep group and item names in their placeholders.");
        if(OpenQuestionProse.Fields(d).Any(f=>Regex.IsMatch(f.Text,@"\p{L}\{[fv]\d+\}|\{[fv]\d+\}\p{L}",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100))))
            return new(null,"InvalidText",ErrorDetails:"Keep placeholders as whole words.");
        bool wrong=Proof(d.Facts![0].Text,@"trong \{f0\} \{v0\}, mỗi nhóm có \{f1\} \{v1\}",@"each of the \{f0\} \{v0\} contains \{f1\} \{v1\}")
            ||Proof(d.Question,@"\{f2\} \{v0\} như vậy có bao nhiêu \{v1\}",@"how many \{v1\} do \{f2\} \{v0\} like these contain in total")
            ||Proof(d.Facts[1].Text,@"số \{v1\} trong mỗi \{v2\} khác nhau|số \{v1\} trong mỗi \{v2\} thay đổi",@"the number of \{v1\} in each \{v2\} (?:differs|changes)");
        if(wrong)return new(null,"ChangedRelationOrTarget",ErrorDetails:"Source total, target item count or equal unchanged group relation changed.");
        bool given=Proof(d.Facts[0].Text,@"\{f0\} \{v0\} (?:chứa tổng cộng|có tất cả|có tổng cộng) \{f1\} \{v1\}|có tổng cộng \{f1\} \{v1\} trong \{f0\} \{v0\}|tổng số \{v1\} trong \{f0\} \{v0\} là \{f1\}",
            @"\{f0\} \{v0\} (?:contain|hold) \{f1\} \{v1\} (?:altogether|in total)|altogether, \{f0\} \{v0\} hold \{f1\} \{v1\}|there are \{f1\} \{v1\} in total in \{f0\} \{v0\}|the total number of \{v1\} in \{f0\} \{v0\} is \{f1\}|the combined (?:amount|number) of \{v1\} across \{f0\} \{v0\} is \{f1\}");
        bool condition=Proof(d.Facts[1].Text,@"số \{v1\} trong mỗi \{v2\} bằng nhau và giữ nguyên ở các nhóm mới|mỗi \{v2\} có cùng số \{v1\}, các nhóm mới cũng giữ nguyên số đó|số \{v1\} trong mỗi \{v2\} bằng nhau và không đổi khi xếp các nhóm mới|số \{v1\} trong mỗi \{v2\} là như nhau và không thay đổi ở các nhóm mới",
            @"each \{v2\} has the same number of \{v1\}, and the new groups keep that count|the number of \{v1\} in each \{v2\} is equal, and it remains unchanged for the new groups|each \{v2\} contains the same number of \{v1\}, and that count stays the same in the new groups");
        // Each branch proves equality across the original containers AND
        // invariance for new ones. Never infer equality from 'as before' alone.
        condition|=Proof(d.Facts[1].Text,
            @"mỗi \{v2\} có cùng số \{v1\}, và số đó giữ nguyên khi đóng các nhóm mới",
            @"every (?:single|original) \{v2\} holds an identical count of \{v1\} which remains constant for the new containers|every original \{v2\} has the same number of \{v1\}, and all new groups keep that count");
        bool question=Proof(d.Question,@"cần bao nhiêu \{v0\} như vậy để chứa tổng cộng \{f2\} \{v1\}|để chứa \{f2\} \{v1\} theo cách đó, cần bao nhiêu \{v0\}|muốn xếp tổng cộng \{f2\} \{v1\} như vậy thì cần bao nhiêu \{v0\}|hỏi cần bao nhiêu \{v0\} như vậy để chứa \{f2\} \{v1\}",
            @"how many \{v0\} like these are needed to hold \{f2\} \{v1\} in total|to hold \{f2\} \{v1\} in the same way, how many \{v0\} are needed|how many \{v0\} are needed for a total of \{f2\} \{v1\} like these|how many \{v0\} like these will hold \{f2\} \{v1\} altogether|how many \{v0\} are required to hold a total of \{f2\} \{v1\} using these same arrangements");
        bool rate=Proof(d.SolutionLeads![0].Text,@"số \{v1\} (?:có )?trong (?:mỗi|một) \{v2\}",@"(?:the )?number of \{v1\} (?:in|within|inside) (?:each|one) \{v2\}");
        bool count=Proof(d.SolutionLeads[1].Text,@"số \{v0\} cần (?:dùng|có)",@"(?:the )?(?:number|(?:total )?count) of \{v0\} (?:needed|required)");
        return given&&condition&&question&&rate&&count?new(d,null,c):new(d,"SemanticReviewRequired",c,
            "The combined item total, equal unchanged per-group count, new item total or target group count could not be proved.");
    }
}
