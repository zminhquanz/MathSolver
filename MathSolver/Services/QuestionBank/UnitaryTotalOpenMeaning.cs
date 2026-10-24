using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class UnitaryTotalOpenMeaning
{
    private static bool Match(string text,string pattern)=>Regex.IsMatch(
        Regex.Replace(text.Trim().ToLowerInvariant(),@"\s+"," ",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)).TrimEnd(' ','.','?',':','!'),
        @"\A(?:"+pattern+@")\z",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        bool Proof(string text,string vietnamese,string english)=>Match(text,vi?vietnamese:english);
        if(OpenQuestionProse.Fields(d).Any(f=>Regex.IsMatch(f.Text,@"\p{L}\{[fv]\d+\}|\{[fv]\d+\}\p{L}",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100))))
            return new(null,"InvalidText",ErrorDetails:"Keep placeholders as separate words.");
        string bare=string.Join(" ",OpenQuestionProse.Fields(d).Select(f=>Regex.Replace(f.Text,@"\{[fv]\d+\}","",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100))));
        if(Regex.IsMatch(bare,vi?@"\b(?:bút|quà|vở|sách|hộp|túi|chồng|táo)\b":@"\b(?:pens?|gifts?|notebooks?|books?|box(?:es)?|bags?|stacks?|apples?)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)))
            return new(null,"ChangedUnits",ErrorDetails:"Use group and item placeholders; remove filled or invented concrete units.");
        bool perGroup=Proof(d.Facts![0].Text,@"trong \{f0\} \{v0\}, mỗi nhóm có \{f1\} \{v1\}",
            @"each of the \{f0\} \{v0\} contains \{f1\} \{v1\}");
        bool unequal=Proof(d.Facts[1].Text,@"số \{v1\} trong mỗi \{v2\} khác nhau|số \{v1\} trong mỗi \{v2\} thay đổi",
            @"the number of \{v1\} in each \{v2\} (?:is different|changes)");
        bool wrongTarget=Proof(d.Question,@"hỏi mỗi nhóm trong \{f2\} \{v0\} có bao nhiêu \{v1\}",
            @"how many \{v1\} are in each of the \{f2\} \{v0\}");
        if(perGroup||unequal||wrongTarget)
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"The source combined total, equal unchanged group size or target total changed.");
        // Equality may be stated in the source fact, but never inferred from
        // constancy alone. Every proof remains anchored to the whole field.
        bool sharedEqually=Proof(d.Facts[0].Text,@"có tất cả \{f1\} \{v1\} được chia đều vào \{f0\} \{v0\}",
            @"a total of \{f1\} \{v1\} is shared equally among \{f0\} \{v0\}");
        bool given=sharedEqually||Proof(d.Facts[0].Text,@"\{f0\} \{v0\} (?:chứa tổng cộng|có tất cả|có tổng cộng) \{f1\} \{v1\}|có tổng cộng \{f1\} \{v1\} trong \{f0\} \{v0\}|tổng số \{v1\} trong \{f0\} \{v0\} là \{f1\}",
            @"\{f0\} \{v0\} (?:contain|hold) \{f1\} \{v1\} (?:altogether|in total)|\{f0\} \{v0\} hold a combined total of \{f1\} \{v1\}|altogether, \{f0\} \{v0\} hold \{f1\} \{v1\}|there are \{f1\} \{v1\} in total in \{f0\} \{v0\}|the total number of \{v1\} in \{f0\} \{v0\} is \{f1\}");
        bool condition=Proof(d.Facts[1].Text,@"số \{v1\} trong mỗi \{v2\} bằng nhau và không thay đổi|mỗi \{v2\} có cùng số \{v1\} và số đó không thay đổi",
            @"the number of \{v1\} in each \{v2\} is the same and does not change|each \{v2\} contains the same number of \{v1\}, and that number does not change");
        condition|=Proof(d.Facts[1].Text,
            @"số \{v1\} trong mỗi \{v2\} bằng nhau và giữ nguyên khi xét các nhóm mới|mỗi \{v2\} có cùng số \{v1\}, các nhóm mới cũng giữ nguyên số đó|trong mỗi \{v2\}, số \{v1\} đều bằng nhau và không thay đổi ở các nhóm mới|mỗi \{v2\} có số \{v1\} bằng nhau và giữ nguyên số đó ở các nhóm mới|số \{v1\} trong mỗi \{v2\} đều bằng nhau và không đổi khi xét các nhóm mới",
            @"the number of \{v1\} in each \{v2\} is the same, and the new groups keep that count|each \{v2\} (?:contains|has) the same number of \{v1\}, and the new groups (?:have that same count|keep that count)|(?:every|each) \{v2\} holds an equal number of \{v1\}, and this count remains unchanged for the new groups");
        condition|=sharedEqually&&Proof(d.Facts[1].Text,@"ở mỗi \{v2\} mới, số \{v1\} vẫn bằng số trong một nhóm ban đầu",
            @"each new \{v2\} has the same number of \{v1\} as an original group");
        bool target=Proof(d.Question,@"(?:(?:hỏi|vậy,?) )?\{f2\} \{v0\} như vậy (?:chứa tổng cộng|có tất cả) bao nhiêu \{v1\}|(?:hỏi )?tổng số \{v1\} trong \{f2\} \{v0\} (?:mới )?như vậy là bao nhiêu",
            @"how many \{v1\} do \{f2\} \{v0\} like these contain in total|what is the total number of \{v1\} in \{f2\} \{v0\} like these|how many \{v1\} are there altogether in \{f2\} \{v0\} like these|how many \{v1\} would \{f2\} \{v0\} like these hold altogether|altogether, how many \{v1\} are in \{f2\} \{v0\} like these");
        bool unit=Proof(d.SolutionLeads![0].Text,@"số \{v1\} trong (?:mỗi|một) \{v2\}|số \{v1\} của mỗi \{v2\}",
            @"(?:the )?(?:number|count) of \{v1\} in (?:each|one|a single) \{v2\}|(?:the )?number of \{v1\} per \{v2\}");
        bool total=Proof(d.SolutionLeads[1].Text,@"tổng số \{v1\} (?:trong|của) (?:tất cả )?các \{v0\} mới",
            @"(?:the )?total (?:number|count) of \{v1\} in (?:all the|the) new \{v0\}|(?:the )?number of \{v1\} in all the new \{v0\}");
        if(!vi&&Match(d.Facts[0].Text,
            @"(?:a collection of )?\{f0\} groups of \{v0\} (?:hold|holds) (?:a combined total of \{f1\} \{v1\}|\{f1\} \{v1\} in total)|the combined total of all the \{v1\} in \{f0\} groups of \{v0\} amounts to \{f1\}"))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"An extra group level changed the source count's role.");
        given|=Proof(d.Facts[0].Text,@"tổng số \{v1\} được phân bổ trong \{f0\} \{v0\} là \{f1\}|tổng cộng có \{f1\} \{v1\} trong \{f0\} \{v0\}",
            @"a collection of \{f0\} \{v0\} holds a combined total of \{f1\} \{v1\}|the combined total of all the \{v1\} in \{f0\} \{v0\} amounts to \{f1\}");
        condition|=Proof(d.Facts[1].Text,
            @"số \{v1\} trong mỗi \{v2\} là như nhau và không thay đổi khi chuyển sang các nhóm mới|số lượng \{v1\} trong mỗi \{v2\} là như nhau, và các nhóm mới vẫn giữ nguyên số lượng này|số \{v1\} trong mỗi \{v2\} bằng nhau và không đổi trong các nhóm mới",
            @"each \{v2\} has the same number of \{v1\}, and this count remains unchanged in the new groups|the number of \{v1\} in each \{v2\} is equal across the original groups and stays unchanged in the new groups|each \{v2\} contains an identical number of \{v1\}, and that count stays the same in the new groups");
        target|=Proof(d.Question,@"tổng số \{v1\} (?:có trong|của) \{f2\} \{v0\} sẽ là bao nhiêu",
            @"how many \{v1\} are there in total in \{f2\} \{v0\} like these|how many \{v1\} will \{f2\} \{v0\} like these contain in total");
        unit|=Proof(d.SolutionLeads[0].Text,@"số \{v1\} có trong (?:mỗi|một) \{v2\}",
            @"(?:the )?(?:number|count) of \{v1\} within (?:each|one|a single) \{v2\}");
        total|=Proof(d.SolutionLeads[1].Text,@"tổng số \{v1\} (?:trong|của) (?:tất cả )?các \{v0\} mới",
            @"(?:the )?total (?:number|count) of \{v1\} across (?:all the|the) new \{v0\}");
        return given&&condition&&target&&unit&&total?new(d,null,c):new(d,"SemanticReviewRequired",c,
            "The source total, equal unchanged group size, new-group total or headings could not be proved.");
    }
}
