using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

/// <summary>Editorial checks never establish mathematical meaning or repair model text.</summary>
internal static class PracticalOpenEditorial
{
    private static bool Has(string text,string pattern)=>Regex.IsMatch(text,pattern,
        RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    internal static BasicDraftValidation Check(BasicQuestionContract c,BasicQuestionDraft d,BasicDraftValidation meaning)
    {
        var fields=OpenQuestionProse.Fields(d);
        // These are literal technical stand-ins invented by the model, not a
        // supplied actor placeholder. Exact field slots and semantic proofs
        // remain independently required by the existing validation pipeline.
        if(fields.Any(f=>Has(f.Text,@"\b(?:actor|subject|person|nhân vật|chủ thể)\s+[ab]\b")))
            return new(null,"InvalidText",ErrorDetails:"Invented technical actor outside the supplied placeholders.");
        if(!meaning.IsValid&&!meaning.NeedsReview)return meaning;
        foreach(var field in fields)
        {
            string rendered=ReasoningStoryCatalogue.Render(field.Text,c);
            if(Has(rendered,@"\b(?:hình\s+hình|rectangle\s+rectangle|triangle\s+triangle|the\s+the|the\s+a|a\s+the|a\s+a|an\s+the)\b"))
                return Review(c,d,"A complete object or shape slot was given a duplicate shape name or article.");
        }
        if(d.SolutionLeads!.Any(h=>Has(h.Text.Trim(),@"\A(?:tính|tìm|hãy|calculate|find|determine|divide|subtract|multiply|add)\b")))
            return Review(c,d,"Solution headings must name quantities rather than instruct a calculation.");
        return meaning;
    }
    private static BasicDraftValidation Review(BasicQuestionContract c,BasicQuestionDraft d,string reason)=>new(d,"SemanticReviewRequired",c,"Editorial: "+reason);
}
