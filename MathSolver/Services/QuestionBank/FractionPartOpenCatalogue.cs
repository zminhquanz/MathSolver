using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class FractionPartOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c) => c.Family==BankQuestionFamily.FractionQuantity
        && c.Story?.NarrativeId.StartsWith(FractionPartPilot.Prefix,StringComparison.Ordinal)==true;
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        if(c.BankVariant!=(int)ElementaryQuizType.FractionOfNumber) throw new ArgumentException("InvalidFractionPartRelation");
        var d=FractionPartPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        var q=new ElementaryQuizGenerator(new(c.Story.Seed)).GenerateFractionPartPilot(ArithmeticQuizMode.Essay,c.Language,c.Tier,d.Profile);
        var p=q.ElementaryProblem!;
        string[] roles=["whole","donated-fraction-of-whole","owner","counted-unit"];
        var quantities=d.Values.Select((v,i)=>new NarrativeQuantity(v.Key,roles[i],v.Value)).ToArray();
        NarrativeClause[] facts=[new("fact_0",d.Context.Whole),new("fact_1",d.Context.Share)];
        var step=p.Reasoning!.Steps.Single();
        NarrativeStep[] steps=[new("step_0",d.Context.Lead,QuizMathExpressionFormatter.Format(step.Expression)+" = "+step.DisplayValue+" "+step.Unit)];
        string signature=string.Join("\n",d.Profile,c.Language,c.Tier,string.Join("/",roles),d.Context);
        string schema=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
        return new(q,d.Profile,d.Render(d.Context.Lead),d.Context.Unit,p.AnswerText,schema,quantities,facts,d.Context.Question,steps,
            facts.ToDictionary(f=>f.Role,f=>new[]{f.Text}),[d.Context.Question],new Dictionary<string,string[]>{{"step_0",[d.Context.Lead]}});
    }
    internal const string Relation = "relation=known-whole-find-donated-part; {f0}=whole item count; {f1}=proper fraction of that SAME whole, donated by {v0}; {v1}=counted item unit. Ask donated item count, NOT remaining count or original whole. No additional transfer, new stock, overlap, receipt or second fraction. C# computes whole * numerator / denominator as an integer. Preserve the donation action and its owner in facts and question.";

    private static string Clean(string s) => Regex.Replace(s.Trim().ToLowerInvariant(),@"\s+"," ",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)).TrimEnd(' ','.','?',':','!');
    private static bool Match(string s,string pattern) => Regex.IsMatch(Clean(s),"\\A(?:"+pattern+")\\z",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft draft)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        string all=draft.ProblemText+" "+string.Join(" ",draft.SolutionLeads!.Select(s=>s.Text));
        string contradiction=vi?@"\b(?:còn lại|còn bao nhiêu|nhận thêm|mua thêm|sau khi|lần nữa|lần thứ|không tặng|chưa tặng|tặng thêm)\b"
            :@"\b(?:remaining|remain|left|buys?|after|again|another|not donate|does not|did not|more)\b";
        if(Regex.IsMatch(all,contradiction,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"Donation direction, original base or target changed.");
        // A donated-object label is passive, but does not make the donor a
        // recipient. Direction reversal must bind to the owner in the share fact.
        string receipt=vi?@"\{v0\}\s+(?:đã\s+)?(?:được tặng|được nhận|nhận)\b"
            :@"\{v0\}\s+(?:receives?|is given|was given|has received)\b";
        if(Regex.IsMatch(draft.Facts![1].Text,receipt,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"The donor became the recipient.");
        string owner=@"\{v0\}",unit=@"\{v1\}",whole=@"\{f0\}",share=@"\{f1\}";
        // Complete clauses only: a keyword hit cannot prove who owns the whole,
        // which quantity the fraction describes, or whether the question asks the part.
        bool proved=vi
            ? (Match(draft.Facts![0].Text,@"(?:ban đầu, )?"+owner+@" (?:có|đang có|sở hữu(?: tổng cộng)?|có tất cả|có tổng cộng) "+whole+" "+unit)
                || Match(draft.Facts[0].Text,@"trong kho của "+owner+@" có "+whole+" "+unit))
                && Match(draft.Facts[1].Text,@"(?:trong số đó, )?"+owner+@" (?:đã )?(?:tặng|đem tặng|cho đi|trao tặng|trao đi) (?:một phần là )?"+share+@" (?:của )?(?:(?:tổng số|số) "+unit+@" (?:đó|này)|toàn bộ "+unit+")")
                && (Match(draft.Question,owner+@" (?:đã )?(?:tặng|cho đi|trao tặng|trao đi) (?:bao nhiêu|tất cả bao nhiêu) "+unit)
                    || Match(draft.Question,@"số "+unit+@" mà "+owner+@" (?:đã )?(?:tặng|cho đi) là bao nhiêu"))
                && Match(draft.SolutionLeads![0].Text,@"(?:số|số lượng) "+unit+@" (?:đã )?(?:tặng|đem tặng|cho đi|được tặng|được cho đi)")
            : (Match(draft.Facts![0].Text,owner+@" (?:has|owns|holds|possesses|has a total of) "+whole+" "+unit)
                || Match(draft.Facts[0].Text,whole+" "+unit+@" belong to "+owner))
                && Match(draft.Facts[1].Text,owner+@" (?:donates|gives away) "+share+@" (?:portion )?of (?:those|these|the) "+unit)
                && (Match(draft.Question,@"how many "+unit+@" does "+owner+@" (?:donate|give away)")
                    || Match(draft.Question,@"what is the (?:count|number) of "+unit+@" (?:given|donated) by "+owner))
                && Match(draft.SolutionLeads![0].Text,@"(?:the )?(?:number|count) of "+unit+@" (?:donated|given away)");
        return proved?new(draft,null,c):new(draft,"SemanticReviewRequired",c,"Whole ownership, fraction base, donation or requested part could not be proved.");
    }
}
