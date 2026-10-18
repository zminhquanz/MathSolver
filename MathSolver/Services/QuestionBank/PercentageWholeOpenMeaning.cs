using MathSolver.Models;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class PercentageWholeOpenMeaning
{
    private static readonly ConcurrentDictionary<string,Regex> Patterns=new(StringComparer.Ordinal);
    private static readonly Regex Slots=QuestionTextRegex.Create(@"\{[fv]\d+\}");
    private static Regex Pattern(string pattern)=>Patterns.GetOrAdd(pattern,
        static p=>QuestionTextRegex.Create("(?i)"+p));
    private static bool Has(string text,string pattern)=>Pattern(pattern).IsMatch(text);
    private static readonly Regex Whitespace=QuestionTextRegex.Create(@"\s+");
    private static bool Match(string text,string pattern)=>Has(Whitespace.Replace(text.Trim()," ").TrimEnd('.', '?', ':', '!').Trim(),@"\A(?:"+pattern+@")\z");

    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft draft)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        string all=string.Join(" ",OpenQuestionProse.Fields(draft).Select(f=>f.Text));
        string prose=Slots.Replace(all,"");
        if(!vi&&(Has(all,@"\b[a-z]['’]?\{[fv]\d+\}|\{[fv]\d+\}[a-z]\b")||Has(prose,@"\b[a-z]['’]s\b"))
            ||Has(prose,@"\b[a-z]\s*\(\s*[a-z](?:\s*,\s*[a-z])*\s*\)")
            ||!vi&&Has(prose,@"\b(?:let|define|denote)\s+[a-z]\b|\bwhere\s+[a-z]\s+(?:is|equals|represents)\b"))
            return new(null,"InvalidText",ErrorDetails:"Use ordinary pupil-facing prose with the supplied roles; remove functions, letter aliases and variable definitions.");
        if(Has(all,vi?@"\{f1\}\s+phần trăm\b":@"\{f1\}\s+(?:percent|per cent)\b"))
            return new(null,"ChangedQuantities",ErrorDetails:"The rate placeholder already includes its number and percent sign.");
        string numerals=vi?"không|một|mốt|hai|ba|bốn|tư|năm|lăm|sáu|bảy|tám|chín|mười|mươi|trăm|nghìn|ngàn|triệu"
            :"zero|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty|thirty|forty|fifty|sixty|seventy|eighty|ninety|hundred|thousand|million";
        if(Has(draft.Facts![0].Text,@"\b(?:(?:"+numerals+@")[ -]+)+\{v1\}"))
            return new(null,"ChangedQuantities",ErrorDetails:"Do not add counts written in words beside the given C# quantity.");
        if(Has(all,vi?@"(?:ban đầu của|lúc đầu của|từ|cho) \{v1\}"
            :@"(?:by|from|to) \{v1\}|\{v1\}['’]s|\{v1\} (?:gives|gave|receives|received|owns|owned)\b")
            ||Has(draft.Facts[0].Text,vi?@"\A\s*\{v1\} (?:đã )?(?:tặng|cho|nhận|sở hữu)\b"
            :@"\A\s*\{v1\} (?:gives|gave|receives|received|owns|owned)\b")
            ||Has(draft.Facts[1].Text,vi?@"(?:ban đầu|lúc đầu) của \{v2\}"
            :@"\{v2\}['’]s (?:original|initial)|(?:original|initial) (?:stock|total|count|collection) (?:of|belonging to) \{v2\}"))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"The item unit became a person, or the original percentage base belongs to the recipient.");
        if(ReversesPercentageRoles(draft.Facts[1].Text,vi))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"The original whole was stated as a percentage of the known gift, reversing the required equation.");

        // The same surface roles apply to a proper fraction and to a percentage:
        // known gift = rate × unknown original stock. Numeric interpretation is
        // deliberately kept in the dedicated percentage contract and generator.
        var result=FractionWholeOpenMeaning.Validate(c,draft);
        if(result.ErrorCode is "ChangedRelationOrTarget" or "InvalidText")return result;
        if(OpenQuestionProse.Fields(draft).Any(f=>Has(f.Text.Trim(),@"[.!?:;].+")))
            return new(null,"InvalidText",ErrorDetails:"Use one short sentence per field, without an added event or technical note.");
        if(draft.Facts.Any(f=>!f.Text.TrimEnd().EndsWith('.'))||!draft.Question.TrimEnd().EndsWith('?'))
            return new(draft,"SemanticReviewRequired",c,"Editorial review required: separate the facts with periods and end the full question with a question mark.");
        if(!result.NeedsReview)return result;

        // Additional complete clauses, compared without altering stored prose.
        // A donor-relative clause still binds the donated items to that donor's
        // initial stock. Do not normalize pronouns or unspecified recipient stock.
        string gift=draft.Facts[0].Text,share=draft.Facts[1].Text,question=draft.Question;
        if(vi)
        {
            if(Match(gift,@"\{v0\} (?:đã )?(?:trao cho|tặng cho|tặng) \{v2\} đúng \{f0\} \{v1\}"))
                gift="{v0} đã tặng {v2} {f0} {v1}.";
            if(Match(share,@"số \{v1\} (?:mà )?\{v0\} (?:đã )?(?:trao đi|tặng|cho đi|trao tặng) (?:bằng|chiếm|chính là) \{f1\} (?:của )?tổng số (?:ban đầu|lúc đầu)"))
                share="Số {v1} {v0} đã tặng bằng {f1} tổng số ban đầu.";
            // A discourse opener changes neither the bound owner nor the
            // original-stock target. Match the complete clause, not a substring.
            if(Match(question,@"vậy,? (?:hỏi )?(?:ban đầu|lúc đầu),? \{v0\} (?:đã )?có (?:tất cả )?bao nhiêu \{v1\}"))
                question="Hỏi ban đầu {v0} có bao nhiêu {v1}?";
            if(Match(question,@"hỏi tổng số \{v1\} (?:ban đầu|lúc đầu) của \{v0\} là bao nhiêu"))
                question="Tổng số {v1} ban đầu của {v0} là bao nhiêu?";
        }
        else
        {
            if(Match(gift,@"\{v0\} (?:gave|donated) exactly \{f0\} \{v1\} to \{v2\}"))
                gift="{v0} gave {f0} {v1} to {v2}.";
            if(Match(share,@"the (?:number|count) of (?:donated|given-away) \{v1\} (?:is|was|equals|represents) \{f1\} of \{v0\}'s original (?:total|stock)"))
                share="The number of {v1} donated equals {f1} of {v0}'s original total.";
            if(Match(question,@"what was the (?:original|initial) (?:number|count) of \{v1\} that \{v0\} (?:had|owned|possessed|held)"))
                question="How many {v1} did {v0} have originally?";
            if(Match(question,@"how many \{v1\} did \{v0\} (?:have|own|possess) at the start"))
                question="How many {v1} did {v0} have originally?";
        }
        var proof=FractionWholeOpenMeaning.Validate(c,draft with
            {Facts=[draft.Facts[0] with {Text=gift},draft.Facts[1] with {Text=share}],Question=question});
        if(proof.IsValid)return new(draft,null,c);
        return result;
    }

    private static bool ReversesPercentageRoles(string text,bool vi)
    {
        // Analyze the roles on each side, including owner-relative clauses and
        // omitted Vietnamese prepositions. Do not enumerate complete sentences.
        // Whole = proper percentage × gift contradicts this pilot's positive
        // gift and 0 < rate < 100. An unknown clause still awaits review.
        string connector=vi?@"bằng|là|chiếm|gấp|tương ứng(?: với)?|tương đương(?: với)?"
            :@"is|was|equals|represents|amounts to|is equal to|was equal to|is equivalent to|was equivalent to";
        string preposition=vi?@"lần|của":@"times|of";
        var equation=Pattern(@"\A(?<left>.+?) (?:"+connector+@") \{f1\} (?:(?:"+preposition+@") )?(?<right>.+)\z")
            .Match(Whitespace.Replace(text.Trim()," ").TrimEnd('.', '?', '!', ':'));
        if(!equation.Success)return false;
        string left=equation.Groups["left"].Value,right=equation.Groups["right"].Value;
        bool original=Has(left,vi?@"\b(?:ban đầu|lúc đầu|trước khi tặng)\b"
            :@"\b(?:original|originally|initial|initially|starting|at the start|before the gift|before giving)\b");
        bool quantity=Has(left,vi?@"\b(?:tổng số|số lượng|số)\b"
            :@"\b(?:total|number|count|quantity|stock|collection)\b");
        bool boundOwner=left.Contains("{v0}",StringComparison.Ordinal)&&left.Contains("{v1}",StringComparison.Ordinal);
        string nonTemporal=Pattern(vi?@"\btrước khi tặng\b":@"\bbefore (?:the (?:gift|donation|transfer)|giving(?: any away)?)\b")
            .Replace(left," original-time ");
        bool giftedLeft=Has(nonTemporal,vi?@"\b(?:tặng|cho đi|trao đi|đã cho|đã nhận)\b"
            :@"\b(?:donated|gifted|given|gave|giving)\b");
        bool giftedRight=Has(right,vi?@"\b(?:số|phần|lượng) (?:đã )?(?:tặng|cho đi|trao đi)\b"
            :@"\b(?:donated|gifted|given(?: away)?|gave away)\b");
        return original&&quantity&&boundOwner&&!giftedLeft&&giftedRight;
    }
}
