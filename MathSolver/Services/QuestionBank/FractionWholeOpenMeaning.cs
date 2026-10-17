using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class FractionWholeOpenMeaning
{
    private static string Clean(string text)=>Regex.Replace(text.Trim().ToLowerInvariant(),@"\s+"," ",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)).TrimEnd(' ','.','?',':','!');
    private static bool Match(string text,string pattern)=>Regex.IsMatch(Clean(text),"\\A(?:"+pattern+")\\z",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    private static bool Has(string text,string pattern)=>Regex.IsMatch(Clean(text),pattern,RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft draft)
    {
        string gift=draft.Facts![0].Text,share=draft.Facts[1].Text,question=draft.Question,lead=draft.SolutionLeads![0].Text;
        string facts=gift+" "+share,all=facts+" "+question+" "+lead;
        const string owner=@"\{v0\}",unit=@"\{v1\}",recipient=@"\{v2\}",count=@"\{f0\}",fraction=@"\{f1\}";
        bool vi=c.Language==AppLanguage.Vietnamese;
        if(Has(all,@"\b(?:đơn vị đếm|biến số|placeholder|stands for)\b|\{[fv]\d+\} (?:là đơn vị|means|is the variable)")
            ||Regex.IsMatch(all,@"\bA\b",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100))
                &&Regex.IsMatch(all,@"\bB\b",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)))
            return new(null,"InvalidText",ErrorDetails:"Unbound actors or technical placeholder explanations entered the prose.");
        bool changed=vi
            ? Has(facts,@"\b(?:không|chưa|còn lại|còn giữ|nhận thêm|mua thêm|tặng thêm|lần nữa|lần thứ|tăng thêm|giảm giá)\b")
                ||Has(gift,owner+@" (?:đã )?(?:có|sở hữu|giữ lại|còn) (?:tất cả |tổng cộng )?"+count)
                ||Has(gift,owner+@" (?:đã )?(?:nhận|được tặng|được cho)\b|"+recipient+@" (?:đã )?(?:tặng|cho|trao)\b")
                ||Has(question,owner+@" (?:đã )?(?:tặng|cho đi) (?:tất cả )?bao nhiêu|(?:số|bao nhiêu) "+unit+@" (?:đã |được )?tặng|\bcòn lại\b|\b(?:phân số|tỉ lệ|tỷ lệ)\b")
                ||Has(lead,@"\b(?:còn lại|đã tặng|được tặng)\b")
            : Has(facts,@"\b(?:not|never|remaining|left|another|again|additional|more|discount)\b")
                ||Has(gift,owner+@" (?:has|had|owns|owned|keeps|kept|possesses|possessed) (?:a total of )?"+count)
                ||Has(gift,owner+@" (?:receives?|received|is given|was given|has received)\b|"+recipient+@" (?:gives|gave|donates|donated)\b")
                ||Has(gift,@"(?:given|donated) to "+owner+@" by "+recipient)
                ||Has(question,@"\b(?:left|remaining|what fraction|what percentage)\b|how many "+unit+@" (?:does "+owner+@" (?:donate|give)|did "+owner+@" (?:donate|give))")
                ||Has(lead,@"\b(?:left|remaining|donated|given away)\b");
        if(changed)return new(null,"ChangedRelationOrTarget",ErrorDetails:"Known donated count, gift direction, original fraction base or original-whole target changed.");
        string viGive=@"(?:tặng|tặng cho|cho|cho đi|trao tặng|trao cho)";
        string enGive=@"(?:gave|gives|donated|donates|gave away|gives away|has given|has donated)";
        bool giftProved=vi
            ? Match(gift,owner+@" (?:đã )?"+viGive+" "+recipient+" "+count+" "+unit)
                ||Match(gift,recipient+@" (?:đã )?nhận(?: được)? "+count+" "+unit+@" từ "+owner)
                ||Match(gift,recipient+@" (?:đã )?được "+owner+@" (?:tặng|cho|trao tặng) "+count+" "+unit)
                ||Match(gift,count+" "+unit+@" (?:đã )?được "+owner+@" (?:tặng|cho|trao tặng) (?:cho )?"+recipient)
            : Match(gift,owner+" "+enGive+" "+count+" "+unit+@" to "+recipient)
                ||Match(gift,owner+@" (?:gave|gives|has given) "+recipient+" "+count+" "+unit)
                ||Match(gift,recipient+@" (?:received|receives|has received) "+count+" "+unit+@" from "+owner)
                ||Match(gift,recipient+@" (?:was|is) given "+count+" "+unit+@" by "+owner)
                ||Match(gift,count+" "+unit+@" (?:were|are) (?:given|donated) to "+recipient+@" by "+owner);
        bool shareProved=vi
            ? Match(share,@"số "+unit+" "+owner+@" (?:đã )?(?:tặng|cho đi) (?:bằng|chiếm) "+fraction+@" (?:của )?tổng số ban đầu")
                ||Match(share,@"số "+unit+@" (?:đã tặng|đã cho đi) (?:bằng|chiếm) "+fraction+@" (?:của )?tổng số ban đầu của "+owner)
                ||Match(share,@"số "+unit+@" (?:mà )?"+owner+@" (?:đã )?(?:tặng|cho đi) (?:bằng|chiếm) "+fraction+@" (?:của )?tổng số lúc đầu")
            : Match(share,@"the donated "+unit+@" (?:are|were|represent) "+fraction+@" of "+owner+@"'s original (?:total|stock)")
                ||Match(share,@"the "+unit+@" (?:that )?"+owner+@" (?:gave away|donated) (?:are|were|represent) "+fraction+@" of (?:the )?original total")
                ||Match(share,@"the number of "+unit+@" (?:given away|donated) (?:is|was|equals) "+fraction+@" of "+owner+@"'s original (?:total|stock)")
                ||Match(share,@"the donated "+unit+@" (?:are|were) "+fraction+@" of the original total that "+owner+@" had");
        bool targetProved=vi
            ? Match(question,@"(?:hỏi )?(?:ban đầu|lúc đầu),? "+owner+@" (?:đã )?có (?:tất cả )?bao nhiêu "+unit)
                ||Match(question,@"(?:hỏi )?"+owner+@" có (?:tất cả )?bao nhiêu "+unit+@" (?:ban đầu|lúc đầu|trước khi tặng)")
                ||Match(question,@"tổng số "+unit+@" ban đầu của "+owner+@" là bao nhiêu")
            : Match(question,@"how many "+unit+@" did "+owner+@" (?:have|own) (?:originally|initially|before the gift)")
                ||Match(question,@"what was "+owner+@"'s original (?:number|count) of "+unit);
        bool leadProved=vi?Match(lead,@"(?:tổng số|số) "+unit+@" (?:ban đầu|lúc đầu)")
            : Match(lead,@"(?:the )?original (?:number|count|total number) of "+unit);
        if(giftProved&&shareProved&&targetProved&&leadProved)return new(draft,null,c);
        return new(draft,"SemanticReviewRequired",c,"The donated count, single gift, fraction of the original stock or original-whole question could not be fully proved; wording also needs review.");
    }
}
