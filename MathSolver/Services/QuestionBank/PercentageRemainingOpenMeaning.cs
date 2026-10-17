using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class PercentageRemainingOpenMeaning
{
    private static readonly TimeSpan Timeout=TimeSpan.FromMilliseconds(100);
    private static bool Has(string text,string pattern)=>Regex.IsMatch(text,pattern,
        RegexOptions.IgnoreCase|RegexOptions.CultureInvariant|RegexOptions.NonBacktracking,Timeout);
    private static bool Match(string text,string pattern)=>Has(Regex.Replace(text.Trim(),@"\s+"," ",
        RegexOptions.CultureInvariant|RegexOptions.NonBacktracking,Timeout).TrimEnd('.', '?', '!', ':').Trim(),@"\A(?:"+pattern+@")\z");

    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft draft)
    {
        // The surface clauses have the same gift/base/target roles as fraction
        // remainder. Numeric meaning and the percent sign stay in the dedicated
        // C# contract, never in this wording verifier.
        bool vi=c.Language==AppLanguage.Vietnamese;
        var facts=draft.Facts!;
        string all=string.Join(" ",facts.Select(f=>f.Text))+" "+draft.Question+" "+draft.SolutionLeads![0].Text;
        // This count-and-gift pilot has no functions or invented letter aliases.
        // Inspect prose only, so the required C# slots are never mistaken for symbols.
        string prose=Regex.Replace(all,@"\{[fv]\d+\}","",RegexOptions.CultureInvariant|RegexOptions.NonBacktracking,Timeout);
        if(!vi&&(Has(all,@"\b[a-z]['’]?\{[fv]\d+\}|\{[fv]\d+\}[a-z]\b")||Has(prose,@"\b[a-z]['’]s\b")))
            return new(null,"InvalidText",ErrorDetails:"Use only the supplied person, count and unit placeholders; remove fused letter aliases and ownership by unbound letters.");
        if(Has(prose,@"\b[a-z]\s*\(\s*[a-z](?:\s*,\s*[a-z])*\s*\)")
            ||!vi&&Has(prose,@"\b(?:let|define|denote)\s+[a-z]\b|\bwhere\s+[a-z]\s+(?:is|equals|represents)\b"))
            return new(null,"InvalidText",ErrorDetails:"Use ordinary pupil-facing count sentences, without function notation, invented letter aliases or variable definitions.");
        if(Has(all,vi?@"\{f1\}\s+phần trăm\b":@"\{f1\}\s+(?:percent|per cent)\b"))
            return new(null,"ChangedQuantities",ErrorDetails:"The percentage placeholder already includes its number and percent unit; do not append a second percent unit.");
        string numerals=vi?"không|một|mốt|hai|ba|bốn|tư|năm|lăm|sáu|bảy|tám|chín|mười|mươi|trăm|nghìn|ngàn|triệu"
            :"zero|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen|twenty|thirty|forty|fifty|sixty|seventy|eighty|ninety|hundred|thousand|million";
        if(Has(facts[0].Text,@"\b(?:(?:"+numerals+@")[ -]+)+\{v1\}"))
            return new(null,"ChangedQuantities",ErrorDetails:"A literal count written in words was added beside the C# quantity placeholder.");
        // Placeholders are typed roles, not just a bag of tokens. Reject a known
        // object-as-person error even when every required placeholder is present.
        if(Has(all,vi?@"(?:là của|ban đầu của|lúc đầu của|từ|cho) \{v1\}|\{v1\} (?:đã )?(?:tặng|cho|nhận|sở hữu)\b"
            :@"(?:by|from|to) \{v1\}|\{v1\}['’]s|\{v1\} (?:gives|gave|receives|received|owns|owned)\b"))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"The item-unit placeholder is being used as a giver, recipient or owner.");
        if(Has(facts[1].Text,vi?@"(?:ban đầu|lúc đầu) của \{v2\}|\{f1\} (?:là )?(?:phần|số|lượng) (?:còn lại|giữ lại)"
            :@"(?:original|initial) (?:stock|count|collection) (?:of|belonging to) \{v2\}|\{f1\} (?:is|are) (?:left|remaining|retained)"))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"The rate names a recipient-owned base or the remaining portion instead of the original donated portion.");
        if(Has(facts[1].Text,vi?@"\{v0\} (?:đã |chỉ )?(?:giữ lại|còn giữ) \{f1\}"
            :@"\{v0\} (?:keeps|kept|retains|retained) \{f1\}")
            ||Match(draft.Question,vi?@"(?:hỏi )?(?:số|số lượng) \{v1\} mà \{v0\} (?:đã )?tặng là bao nhiêu"
            :@"how many \{v1\} did \{v0\} (?:give away|give|donate)"))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"The donated rate was changed to a retained rate, or the question asks for the donated count.");
        var result=FractionRemainingOpenMeaning.Validate(c,draft);
        if(result.ErrorCode=="ChangedRelationOrTarget"||result.ErrorCode=="InvalidText")return result;
        if(OpenQuestionProse.Fields(draft).Any(f=>Has(f.Text.Trim(),@"[.!?:;].+")))
            return new(null,"InvalidText",ErrorDetails:"Use one sentence per field, without an extra explanation or placeholder note.");
        if(Regex.IsMatch(all,c.Language==AppLanguage.Vietnamese?@"\bcủa họ\b":@"\b(?:their original|their initial)\b",
            RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)))
            return new(draft,"SemanticReviewRequired",c,"Original-stock owner is expressed by an unresolved pronoun.");
        if(Has(all,vi?@"\b(?:một lượng bằng|một số lượng tương ứng|lưu ý|chú thích)\b"
            :@"\b(?:amount of|amount remaining|in books|in pens|in notebooks)\b|remaining \{v1\} count"))
            return new(draft,"SemanticReviewRequired",c,"Editorial review required: use an ordinary count of the configured items, without placeholder notes or cumbersome quantity wording.");
        if(!vi&&Match(draft.Question,@"(?:the )?(?:number|count) of \{v1\} (?:remaining|left) (?:for|with) \{v0\}"))
            return new(draft,"SemanticReviewRequired",c,"Editorial review required: write a complete question with a verb, rather than a count caption.");
        if(!vi&&(facts.Any(f=>!f.Text.TrimEnd().EndsWith('.'))||!draft.Question.TrimEnd().EndsWith('?')))
            return new(draft,"SemanticReviewRequired",c,"Editorial review required: end each fact with a period and the complete question with a question mark, so the displayed problem has separate sentences.");
        if(!result.NeedsReview)return result;

        // Prove additional complete clauses by role-preserving normalization.
        // This is comparison only: keep the model's original wording in storage.
        string whole=facts[0].Text,question=draft.Question;
        if(vi)
        {
            if(Match(whole,@"\{v0\} (?:ban đầu|lúc đầu) (?:có|sở hữu) \{f0\} \{v1\}"))
                whole="Ban đầu, {v0} có {f0} {v1}.";
            if(Match(question,@"(?:hỏi )?sau (?:lần tặng đó|khi tặng),? \{v0\} còn lại bao nhiêu \{v1\}"))
                question="{v0} còn lại bao nhiêu {v1}?";
        }
        else
        {
            if(Match(whole,@"\{v0\} (?:originally|initially) (?:had|has|owned) \{f0\} \{v1\}")
                ||Match(whole,@"\{v0\} (?:starts|started) with \{f0\} \{v1\}")
                ||Match(whole,@"\{v0\} (?:had|owned|possessed|held) \{f0\} \{v1\} (?:at the start|initially|originally)"))
                whole="Initially, {v0} had {f0} {v1}.";
            if(Match(question,@"after (?:giving|donating),? how many \{v1\} does \{v0\} (?:have left|still have)")
                ||Match(question,@"how many \{v1\} are left with \{v0\}")
                ||Match(question,@"how many \{v1\} does \{v0\} (?:have|own|possess|hold) (?:now|after the (?:gift|donation))")
                ||Match(question,@"how many \{v1\} does \{v0\} still (?:own|possess|hold)"))
                question="How many {v1} does {v0} have left?";
        }
        var proof=FractionRemainingOpenMeaning.Validate(c,draft with
            {Facts=[facts[0] with {Text=whole},facts[1]],Question=question});
        if(proof.IsValid)return new(draft,null,c);
        return result;
    }
}
