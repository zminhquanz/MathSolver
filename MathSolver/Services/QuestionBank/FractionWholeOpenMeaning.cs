using MathSolver.Models;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class FractionWholeOpenMeaning
{
    // These role patterns are fixed by code, not supplied by users. Retain the
    // linear matchers instead of repeatedly evicting .NET's small static cache.
    // The shared timeout policy also permits cold matching in a busy UI process.
    private static readonly ConcurrentDictionary<string,Regex> Patterns=new(StringComparer.Ordinal);
    private static Regex Pattern(string pattern)=>Patterns.GetOrAdd(pattern,QuestionTextRegex.Create);
    private static readonly Regex Whitespace=QuestionTextRegex.Create(@"\s+");
    private static string Clean(string text)=>Whitespace.Replace(text.Trim().ToLowerInvariant()," ").TrimEnd(' ','.','?',':','!');
    private static bool Match(string text,string pattern)=>Pattern("\\A(?:"+pattern+")\\z").IsMatch(Clean(text));
    private static bool Has(string text,string pattern)=>Pattern(pattern).IsMatch(Clean(text));
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft draft)
    {
        string gift=draft.Facts![0].Text,share=draft.Facts[1].Text,question=draft.Question,lead=draft.SolutionLeads![0].Text;
        string facts=gift+" "+share,all=facts+" "+question+" "+lead;
        const string owner=@"\{v0\}",unit=@"\{v1\}",recipient=@"\{v2\}",count=@"\{f0\}",fraction=@"\{f1\}";
        bool vi=c.Language==AppLanguage.Vietnamese;
        if(Has(all,@"\b(?:đơn vị đếm|biến số|placeholder|stands for)\b|\{[fv]\d+\} (?:là đơn vị|means|is the variable)")
            ||Pattern(@"\bA\b").IsMatch(all)&&Pattern(@"\bB\b").IsMatch(all))
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
        changed|=ReversesWholeAndPart(share,vi);
        if(changed)return new(null,"ChangedRelationOrTarget",ErrorDetails:"Known donated count, gift direction, original fraction base or original-whole target changed.");
        if(Has(share,vi?@"\bcủa họ\b":@"\b(?:their original|their initial)\b"))
            return new(draft,"SemanticReviewRequired",c,"Original-stock owner is expressed by an unresolved pronoun.");
        if(!vi&&Has(all,@"\bamount of \{v1\}|\b(?:initial amount|original amount)\b"))
            return new(draft,"SemanticReviewRequired",c,"Editorial review required: use a count noun for counted objects.");
        string viGive=@"(?:tặng|tặng cho|cho|cho đi|trao tặng|trao cho)";
        string enGive=@"(?:gave|gives|donated|donates|gave away|gives away|has given|has donated)";
        bool giftProved=vi
            ? Match(gift,owner+@" (?:đã )?"+viGive+" "+recipient+" "+count+" "+unit)
                ||Match(gift,recipient+@" (?:đã )?nhận(?: được)? "+count+" "+unit+@" từ "+owner)
                ||Match(gift,recipient+@" (?:đã )?được "+owner+@" (?:tặng|cho|trao tặng) "+count+" "+unit)
                ||Match(gift,count+" "+unit+@" (?:đã )?được "+owner+@" (?:tặng|cho|trao tặng) (?:cho )?"+recipient)
            : Match(gift,owner+" "+enGive+" "+count+" "+unit+@" to "+recipient)
                ||Match(gift,owner+@" (?:gave|gives|has given) "+recipient+" "+count+" "+unit)
                ||Match(gift,recipient+@" (?:received|receives|has received) (?:exactly )?"+count+" "+unit+@" from "+owner)
                ||Match(gift,recipient+@" (?:was|is) given "+count+" "+unit+@" by "+owner)
                ||Match(gift,count+" "+unit+@" (?:were|are) (?:given|donated) to "+recipient+@" by "+owner);
        bool shareProved=vi
            ? Match(share,@"số "+unit+" "+owner+@" (?:đã )?(?:tặng|cho đi) (?:bằng|chiếm) "+fraction+@" (?:của )?tổng số ban đầu")
                ||Match(share,@"số "+unit+@" (?:đã tặng|đã cho đi) (?:bằng|chiếm) "+fraction+@" (?:của )?tổng số ban đầu của "+owner)
                ||Match(share,@"số "+unit+@" (?:mà )?"+owner+@" (?:đã )?(?:tặng|cho đi) (?:bằng|chiếm) "+fraction+@" (?:của )?tổng số lúc đầu")
                ||Match(share,@"số "+unit+@" mà "+owner+@" (?:đã )?(?:cho|tặng|cho đi) chính là "+fraction+@" (?:của )?tổng số ban đầu")
            : Match(share,@"the donated "+unit+@" (?:are|were|represent) "+fraction+@" of "+owner+@"'s original (?:total|stock)")
                ||Match(share,@"the "+unit+@" (?:that )?"+owner+@" (?:gave away|donated) (?:are|were|represent) "+fraction+@" of (?:the )?original total")
                ||Match(share,@"the number of "+unit+@" (?:given away|donated) (?:is|was|equals) "+fraction+@" of "+owner+@"'s original (?:total|stock)")
                ||Match(share,@"the donated "+unit+@" (?:are|were) "+fraction+@" of the original total that "+owner+@" had")
                ||Match(share,@"the total count of "+unit+@" given away equals "+fraction+@" of what "+owner+@" started with");
        bool targetProved=vi
            ? Match(question,@"(?:hỏi )?(?:ban đầu|lúc đầu),? "+owner+@" (?:đã )?có (?:tất cả )?bao nhiêu "+unit)
                ||Match(question,@"(?:hỏi )?"+owner+@" có (?:tất cả )?bao nhiêu "+unit+@" (?:ban đầu|lúc đầu|trước khi tặng)")
                ||Match(question,@"tổng số "+unit+@" ban đầu của "+owner+@" là bao nhiêu")
                ||Match(question,@"(?:hỏi )?trước khi tặng,? "+owner+@" có (?:tất cả )?bao nhiêu "+unit)
            : Match(question,@"how many "+unit+@" did "+owner+@" (?:have|own) (?:originally|initially|before the gift)")
                ||Match(question,@"what was "+owner+@"'s original (?:number|count) of "+unit)
                ||Match(question,@"what was the initial number of "+unit+@" that "+owner+@" possessed");
        bool leadProved=vi?Match(lead,@"(?:tổng số|số) "+unit+@" (?:ban đầu|lúc đầu)")
            : Match(lead,@"(?:the )?original (?:number|count|quantity|total number) of "+unit);
        if(giftProved&&shareProved&&targetProved&&leadProved)return new(draft,null,c);
        return new(draft,"SemanticReviewRequired",c,"The donated count, single gift, fraction of the original stock or original-whole question could not be fully proved; wording also needs review.");
    }

    // Recognize the roles on both sides of an explicitly stated equality,
    // rather than one previously observed full sentence. Unknown clauses still
    // need review. A proper fraction times the known part cannot be its whole.
    private static bool ReversesWholeAndPart(string text,bool vi)
    {
        string clause=Clean(text);
        if(!vi)
        {
            clause=Pattern(@"\b(?:is|was) (?:equivalent|equal) to\b").Replace(clause,"equals");
            clause=Pattern(@"\b(originally|initially) (had|owned|possessed|held)\b").Replace(clause,"$2 $1");
        }
        var equation=Pattern(vi?@"\A(?<left>.+?) (?:bằng|là|gấp) \{f1\} (?:lần|của) (?<right>.+)\z"
            :@"\A(?<left>.+?) (?:is|was|equals) \{f1\} (?:times|of) (?<right>.+)\z").Match(clause);
        if(!equation.Success)return false;
        string left=equation.Groups["left"].Value,right=equation.Groups["right"].Value;
        bool whole=vi?Match(left,@"(?:tổng số|số) \{v1\} (?:ban đầu|lúc đầu) của \{v0\}")
            :Match(left,@"the (?:total|original|initial) (?:number|count|quantity) of \{v1\} (?:that )?\{v0\} (?:had|owned|possessed|held) (?:at the start|before giving(?: any away)?|initially|originally)")
                ||Match(left,@"\{v0\}'s (?:original|initial) (?:number|count|quantity) of \{v1\}");
        bool part=vi?Has(right,@"\b(?:số|phần) (?:đã tặng|đã cho|đã cho đi)\b")
            :Has(right,@"\b(?:gifted|donated|given away)\b");
        return whole&&part;
    }
}
