using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class SumRatioOpenMeaning
{
    private static bool Match(string text,string pattern)=>Regex.IsMatch(
        Regex.Replace(text.Trim().ToLowerInvariant(),@"\s+"," ",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)).TrimEnd(' ','.','?',':','!'),
        @"\A(?:"+pattern+@")\z",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        var fields=OpenQuestionProse.Fields(d);
        if(fields.Any(f=>Regex.IsMatch(f.Text,@"\p{L}\{[fv]\d+\}|\{[fv]\d+\}\p{L}",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100))))
            return new(null,"InvalidText",ErrorDetails:"Keep placeholders as separate words; do not invent attached letter aliases or append letters to them. English possessives need an apostrophe.");
        string Bare(string text)=>Regex.Replace(text,@"\{[fv]\d+\}","",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
        string prose=string.Join(" ",fields.Select(f=>Bare(f.Text)));
        if(Regex.IsMatch(prose,vi?@"\b(?:sách|bút|vở|nhãn dán|táo)\b":@"\b(?:books?|pens?|notebooks?|stickers?|apples?|pencils?)\b",
            RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)))
            return new(null,"ChangedUnits",ErrorDetails:"Use the item-unit placeholder only; remove literal or foreign item names added beside it.");
        string numerals=vi?@"\b(?:ba|bốn|sáu|bảy|tám|chín|mười|mươi|trăm|nghìn|ngàn|triệu)\b"
            :@"\b(?:zero|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty|thirty|forty|fifty|sixty|seventy|eighty|ninety|hundred|thousand|million|thirds|halves|quarters)\b";
        if(d.Facts!.Any(f=>Regex.IsMatch(Bare(f.Text),numerals,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100))))
            return new(null,"ChangedQuantities",ErrorDetails:"Remove counts or fractions written in words beside the immutable C# quantity placeholders.");
        bool Proof(string text,string vietnamese,string english)=>Match(text,vi?vietnamese:english);
        string Owner(int i)=>@"\{v"+i+@"\}";
        string owners=@"(?:\{v0\} và \{v1\}|\{v1\} và \{v0\})",enOwners=@"(?:\{v0\} and \{v1\}|\{v1\} and \{v0\})";
        string Count(int i)=>@"(?:the )?(?:number|count) of \{v2\} (?:belonging to|owned by|held by|for) "+Owner(i);
        string ViCount(int i)=>@"(?:số|số lượng) \{v2\} (?:của|thuộc về) "+Owner(i);
        bool Ratio(int numeratorOwner,int denominatorOwner)=>Proof(d.Facts![1].Text,
            ViCount(numeratorOwner)+@" (?:bằng|là) \{f1\} "+ViCount(denominatorOwner)
                +"|"+Owner(numeratorOwner)+@" có số \{v2\} bằng \{f1\} số \{v2\} của "+Owner(denominatorOwner),
            Count(numeratorOwner)+@" (?:is|equals) \{f1\} of "+Count(denominatorOwner)
                +"|"+Owner(numeratorOwner)+@"['’]s (?:number|count) of \{v2\} (?:is|equals) \{f1\} of "+Owner(denominatorOwner)+@"['’]s (?:number|count) of \{v2\}");
        bool wrongTotal=Proof(d.Facts![0].Text,owners+@" mỗi người có \{f0\} \{v2\}",enOwners+@" each have \{f0\} \{v2\}");
        bool increment=Proof(d.Facts[1].Text,ViCount(0)+@" (?:nhiều hơn|tăng thêm) \{f1\} "+ViCount(1),
            Count(0)+@" is \{f1\} more than "+Count(1));
        bool transfer=Proof(d.Facts[1].Text,
            @"\{v0\} (?:tặng|cho) \{v1\} một phần số \{v2\}, bằng \{f1\} số \{v2\} ban đầu",
            @"\{v0\} (?:gives|gave) \{v1\} some \{v2\}, equal to \{f1\} of the original \{v2\}");
        bool wrongTarget=Proof(d.Question,
            @"(?:hỏi )?(?:tổng số|hiệu số) \{v2\} của "+owners+@" là bao nhiêu",
            @"what is the (?:total number|difference in the number) of \{v2\} (?:for|between) "+enOwners);
        if(wrongTotal||Ratio(1,0)||increment||transfer||wrongTarget)
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"Combined total, ratio ownership, static equality or both-owner target changed.");
        bool total=Proof(d.Facts[0].Text,owners+@" (?:có|sở hữu) (?:tất cả|tổng cộng|chung) \{f0\} \{v2\}|(?:tổng số|tổng cộng số) \{v2\} của "+owners+@" là \{f0\}",
            @"(?:together,? )?"+enOwners+@" have \{f0\} \{v2\}(?: (?:in total|altogether))?|the (?:(?:total|combined) (?:number|count)|combined total) of \{v2\} (?:belonging to|owned by|held by|for) "+enOwners+@" (?:is|equals|amounts to) \{f0\}");
        string Pair(int first,int second)=>@"how many \{v2\} does "+Owner(first)+@" (?:have|possess|hold), and how many does "+Owner(second)+@" (?:have|possess|hold)";
        bool question=Proof(d.Question,@"(?:hỏi )?số \{v2\} của "+owners+@" lần lượt là bao nhiêu|(?:hỏi )?\{v0\} có bao nhiêu \{v2\}, \{v1\} có bao nhiêu",
            @"how many \{v2\} do "+enOwners+@" have (?:each|respectively)|"+Pair(0,1)+"|"+Pair(1,0));
        bool PartLead()=>Proof(d.SolutionLeads![0].Text,@"số \{v2\} (?:ứng với|trong) mỗi phần",
            @"(?:the )?(?:number|count) of \{v2\} (?:in|per) each equal part");
        bool Lead(int index,int owner)=>Proof(d.SolutionLeads![index].Text,@"(?:số|số lượng) \{v2\} của "+Owner(owner),
            @"(?:the )?(?:number|count) of \{v2\} (?:for|belonging to|owned by|held by) "+Owner(owner));
        return total&&Ratio(0,1)&&question&&PartLead()&&Lead(1,0)&&Lead(2,1)?new(d,null,c):new(d,"SemanticReviewRequired",c,
            "The direct combined total, smaller/larger ratio, two targets or equal-part/owner headings could not be proved.");
    }
}
