using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class FractionRemainingOpenMeaning
{
    private static bool Match(string text,string pattern) => Regex.IsMatch(Clean(text),"\\A(?:"+pattern+")\\z",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    private static bool Has(string text,string pattern) => Regex.IsMatch(Clean(text),pattern,RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    private static string Clean(string text) => Regex.Replace(text.Trim().ToLowerInvariant(),@"\s+"," ",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)).TrimEnd(' ','.','?',':','!');
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft draft)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        string whole=draft.Facts![0].Text,share=draft.Facts[1].Text,question=draft.Question,lead=draft.SolutionLeads![0].Text;
        const string owner=@"\{v0\}",unit=@"\{v1\}",recipient=@"\{v2\}",count=@"\{f0\}",fraction=@"\{f1\}";
        // Recognized contradictions reject. Unknown complete clauses remain reviewable.
        string all=whole+" "+share+" "+question+" "+lead;
        string facts=whole+" "+share;
        bool definesSlot=Has(all,@"(?:lưu ý|note)\s*:\s*\{[fv]\d+\}\s+(?:là|is|means)\b");
        bool addsLetterActors=Regex.IsMatch(all,@"\bA\b",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100))
            &&Regex.IsMatch(all,@"\bB\b",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
        if(definesSlot||addsLetterActors)return new(null,"InvalidText",ErrorDetails:"The draft defines a placeholder or introduces unbound A/B actors instead of the configured people.");
        bool changed=vi
            ? Has(facts,@"\b(?:không|chưa)\b")
                ||Has(all,@"\b(?:nhận thêm|mua thêm|tặng thêm|lần nữa|lần thứ|tăng thêm|giảm giá|của người khác)\b")
                ||Has(share,owner+@" (?:chỉ )?(?:còn|còn lại) "+fraction)
                ||Has(share,fraction+@" (?:của )?(?:số|tổng số) "+unit+@" còn lại")
                ||Has(share,owner+@" (?:đã )?(?:được tặng|được nhận|nhận)\b|"+recipient+@" (?:đã )?(?:tặng|cho|trao)\b")
                ||Has(whole,owner+@" (?:đã )?(?:tặng|cho đi|nhận) "+count)
                ||Has(question,owner+@" (?:đã )?(?:tặng|cho đi|trao tặng|trao đi) (?:tất cả )?bao nhiêu")
                ||Has(question,@"(?:số|bao nhiêu) "+unit+@" (?:đã |được )?(?:tặng|cho đi)")
                ||Has(question,@"(?:ban đầu|lúc đầu) "+owner+@" (?:đã )?có (?:tất cả )?bao nhiêu|\b(?:phân số|tỉ lệ|tỷ lệ)\b")
                ||Has(question,@"\btrước khi (?:tặng|cho)\b")
                ||Has(lead,@"\b(?:đã tặng|được tặng|đã cho đi|ban đầu)\b")
            : Has(facts,@"\b(?:not|never)\b")
                ||Has(all,@"\b(?:another|again|additional|more|discount|somebody else)\b")
                ||Has(share,owner+@" has "+fraction+@" of (?:those|these|the original) "+unit+@" (?:remaining|left)")
                ||Has(share,fraction+@" of (?:the )?(?:remaining "+unit+"|"+unit+@" (?:remaining|left))")
                ||Has(share,owner+@" (?:receives?|received|is given|was given|has received)\b|"+recipient+@" (?:donates|donated|gives|gave)\b")
                ||Has(share,@"(?:given|donated) to "+owner+@" by "+recipient)
                ||Has(whole,owner+@" (?:donates|donated|gives away|gave away|gave|receives|received) "+count)
                ||Has(question,@"how many "+unit+@" (?:does "+owner+@" (?:donate|give away)|did "+owner+@" have originally)")
                ||Has(question,@"(?:count|number) of "+unit+@" (?:donated|given away|received)|\b(?:what fraction|what percentage)\b")
                ||Has(question,@"\bbefore the (?:gift|donation)\b")
                ||Has(lead,@"(?:number|count) of "+unit+@" (?:donated|given away|received)|\boriginal\b");
        if(changed)return new(null,"ChangedRelationOrTarget",ErrorDetails:"Original whole, donor/recipient, donated fraction or remaining-count target changed.");
        // A complete draft can have the right relation but still be unsuitable
        // for pupils. Preserve it for editorial review, without a retry or rewrite.
        string? editorial=EditorialConcern(c.Language,whole,share,question,lead);
        if(editorial is not null)return new(draft,"SemanticReviewRequired",c,"Editorial review required: "+editorial);
        string viBase=@"(?:của )?(?:(?:tổng số|số) "+unit+@"(?: (?:đó|này|ban đầu))?|toàn bộ số "+unit+@" ban đầu)";
        string viGift=@"(?:tặng|tặng cho|đem tặng|trao tặng|trao cho|cho)";
        string enBase=@"of (?:those|these|the original|the initial|the total|the) "+unit;
        string enGift=@"(?:donates|donated|gives|gave|gives away|gave away|has given|has donated)";
        bool proved=vi
            ? (Match(whole,@"(?:(?:ban đầu|lúc đầu),? )?"+owner+@" (?:có|đang có|có tất cả|có tổng cộng|sở hữu(?: tổng cộng)?) "+count+" "+unit)
                ||Match(whole,@"tổng cộng,? "+owner+@" có "+count+" "+unit+@"(?: ban đầu)?")
                ||Match(whole,@"trong kho của "+owner+@" có "+count+" "+unit)
                ||Match(whole,count+" "+unit+@" (?:thuộc|thuộc về) "+owner)
                ||Match(whole,@"(?:tổng số|số) "+unit+@" ban đầu của "+owner+@" là "+count)
                ||Match(whole,@"(?:tổng số|số) "+unit+@" của "+owner+@" ban đầu là "+count))
                &&(Match(share,@"(?:trong số đó, )?"+owner+@" (?:đã )?"+viGift+" "+recipient+" "+fraction+" "+viBase)
                    ||Match(share,recipient+@" (?:đã )?được "+owner+@" (?:tặng|cho|trao tặng) "+fraction+" "+viBase)
                    ||Match(share,fraction+" "+viBase+@" (?:đã )?được "+owner+@" (?:tặng|cho|trao tặng) (?:cho )?"+recipient)
                    ||Match(share,recipient+@" (?:đã )?nhận(?: được)? "+fraction+" "+viBase+@" từ "+owner))
                &&(Match(question,@"(?:hỏi |sau khi tặng, )?"+owner+@" (?:còn lại|còn|giữ lại|còn giữ lại) bao nhiêu "+unit+@"(?: sau khi (?:cho|tặng))?")
                    ||Match(question,@"số "+unit+@" còn lại của "+owner+@" là bao nhiêu")
                    ||Match(question,@"số "+unit+@" mà "+owner+@" (?:còn|giữ lại) là bao nhiêu"))
                &&Match(lead,@"(?:số|số lượng) "+unit+@" (?:còn lại|giữ lại)")
            : (Match(whole,@"(?:initially,? )?"+owner+@" (?:has|had|owns|owned|holds|possesses|possessed|has a total of) "+count+" "+unit)
                ||Match(whole,count+" "+unit+@" belong to "+owner)
                ||Match(whole,@"the original (?:number|count) of "+unit+@" (?:that "+owner+@" had|belonging to "+owner+@"|owned by "+owner+@") (?:was|is) "+count))
                &&(Match(share,owner+" "+enGift+" "+fraction+" "+enBase+@" to "+recipient)
                    ||Match(share,owner+@" (?:gives|gave|has given) "+recipient+" "+fraction+" "+enBase)
                    ||Match(share,recipient+@" (?:is|was) given "+fraction+" "+enBase+@" by "+owner)
                    ||Match(share,fraction+" "+enBase+@" (?:are|were) (?:given|donated) to "+recipient+@" by "+owner)
                    ||Match(share,recipient+@" (?:receives|received|has received) "+fraction+" "+enBase+@" from "+owner))
                &&(Match(question,@"(?:after the (?:gift|donation), )?how many "+unit+@" does "+owner+@" (?:have left|still have|still hold|still keep)(?: after the (?:gift|donation))?")
                    ||Match(question,@"how many "+unit+@" remain with "+owner)
                    ||Match(question,@"what is the (?:count|number) of "+unit+@" left with "+owner)
                    ||Match(question,@"what is the (?:count|number) of "+unit+@" that "+owner+@" still (?:has|holds)"))
                &&Match(lead,@"(?:the )?(?:number|count) of "+unit+@" (?:remaining|left|kept)");
        return proved?new(draft,null,c):new(draft,"SemanticReviewRequired",c,
            "Original ownership, the single donation to the recipient, fraction base or donor's remaining-count question could not be proved.");
    }

    private static string? EditorialConcern(AppLanguage language,params string[] fields)
    {
        string text=string.Join(" ",fields);
        if(language==AppLanguage.Vietnamese)
        {
            if(Has(text,@"\b(?:còn lại với|sở hữu ban đầu)\b"))return "Unnatural word order or remaining-ownership wording.";
            if(Has(text,@"(?:phân số (?:là )?|một phần là )\{f1\}|toàn bộ \{v1\}"))return "Name the counted objects directly, without a metamathematical label or a missing count noun.";
        }
        else if(Has(text,@"\b(?:after the giving|portion)\b"))return "Use an ordinary gift clause and a grammatical fraction-of-items phrase.";
        return null;
    }
}
