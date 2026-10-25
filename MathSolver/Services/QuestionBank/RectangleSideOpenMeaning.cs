using MathSolver.Models;
using static MathSolver.Services.QuestionBank.RectangleDirectOpenLanguage;

namespace MathSolver.Services.QuestionBank;

internal static class RectangleSideOpenMeaning
{
    private static string Role(string role,bool vi)=>vi?(role=="length"?"chiều dài":"chiều rộng"):role;
    private static bool Perimeter(string text,bool vi)=>vi
        ?Match(text,@"\{v0\} có chu vi (?:(?:là|bằng) )?\{f0\} \{v2\}")
            ||Match(text,@"chu vi (?:của )?\{v0\} (?:là|bằng|đo được) \{f0\} \{v2\}")
        :Match(text,@"\{v0\} has a perimeter of \{f0\} \{v2\}")
            ||Match(text,@"the perimeter of \{v0\} (?:is|measures|equals) \{f0\} \{v2\}")
            ||Match(text,@"\{v0\}['’]s perimeter (?:is|measures|equals) \{f0\} \{v2\}");
    private static bool Target(string text,string role,bool vi)=>vi
        ?Match(text,@"(?:hỏi )?"+Role(role,true)+@" (?:của )?\{v0\} (?:là|bằng) bao nhiêu \{v2\}")
            ||Match(text,Role(role,true)+@" (?:của )?\{v0\} tính theo \{v2\} (?:là|bằng) bao nhiêu")
        :Match(text,@"what is the "+role+@" of \{v0\} in \{v2\}")
            ||Match(text,@"what is \{v0\}['’]s "+role+@" in \{v2\}")
            ||Match(text,@"in \{v2\}, what is (?:the "+role+@" of \{v0\}|\{v0\}['’]s "+role+@")")
            ||Match(text,@"what is the "+role+@" of \{v0\}, measured in \{v2\}")
            ||Match(text,@"how "+(role=="length"?"long":"wide")+@" is \{v0\} in \{v2\}");
    private static bool Lead(string text,string role,bool vi)=>vi
        ?Match(text,Role(role,true)+@" (?:của )?\{v0\}(?: là)?")
        :Match(text,@"(?:the )?"+role+@" of \{v0\}")||Match(text,@"\{v0\}['’]s "+role);
    private static bool HalfLead(string text,bool vi)=>vi
        ?Match(text,@"nửa chu vi (?:của )?\{v0\}(?: là)?")
            ||Match(text,@"tổng chiều dài và chiều rộng (?:của )?\{v0\}(?: là)?")
        :Match(text,@"half (?:the perimeter of \{v0\}|\{v0\}['’]s perimeter)")
            ||Match(text,@"(?:the )?semiperimeter of \{v0\}")
            ||Match(text,@"(?:the )?sum of the length and width of \{v0\}");
    private static bool PerimeterTarget(string text,bool vi)=>vi
        ?Match(text,@"(?:hỏi )?chu vi (?:của )?\{v0\} (?:là|bằng) bao nhiêu \{v2\}")
            ||Match(text,@"chu vi (?:của )?\{v0\} tính theo \{v2\} (?:là|bằng) bao nhiêu")
        :Match(text,@"what is (?:the perimeter of \{v0\}|\{v0\}['’]s perimeter) in \{v2\}")
            ||Match(text,@"in \{v2\}, what is (?:the perimeter of \{v0\}|\{v0\}['’]s perimeter)")
            ||Match(text,@"what is the perimeter of \{v0\}, measured in \{v2\}");
    private static bool PerimeterLead(string text,bool vi)=>vi
        ?Match(text,@"chu vi (?:của )?\{v0\}(?: là)?")
        :Match(text,@"(?:the )?perimeter of \{v0\}")||Match(text,@"\{v0\}['’]s perimeter");
    internal static bool ProvesReference(RectangleSidePilot.Context context,AppLanguage language)
    {
        bool vi=language==AppLanguage.Vietnamese;
        return Shape(context.ShapeFact,language)&&Perimeter(context.PerimeterFact,vi)
            &&Dimension(context.KnownFact,vi?(context.KnownRole=="length"?"dài":"rộng"):context.KnownRole,@"\{f1\}",language)
            &&Target(context.Question,context.TargetRole,vi)&&HalfLead(context.HalfLead,vi)&&Lead(context.Lead,context.TargetRole,vi)
            &&new[]{context.ShapeFact,context.PerimeterFact,context.KnownFact}.All(x=>x.TrimEnd().EndsWith('.'))
            &&context.Question.TrimEnd().EndsWith('?');
    }
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft draft)
    {
        var context=RectangleSidePilot.Create(c.SceneId,c.Language,c.Tier,new(c.Story!.Seed)).Context;
        bool vi=c.Language==AppLanguage.Vietnamese;
        string shape=draft.Facts![0].Text,perimeter=draft.Facts[1].Text,known=draft.Facts[2].Text,
            question=draft.Question,half=draft.SolutionLeads![0].Text,lead=draft.SolutionLeads[1].Text;
        string all=string.Join(" ",OpenQuestionProse.Fields(draft).Select(f=>f.Text));
        if(Has(all,@"[²³^]|\b(?:squared|cubed)\b")||Has(all,vi?@"\b(?:mét vuông|mét khối)\b":@"\bsquare (?:metres?|meters?|centimeters?|centimetres?)\b"))
            return new(null,"ChangedUnits",ErrorDetails:"The perimeter and both sides use the same linear length unit.");
        // The first solution lead correctly names half the perimeter. The input
        // fact and requested target must retain the entire perimeter and side.
        if(Has(all,vi?@"\b(?:diện tích|thể tích|hình vuông|hình tròn|tam giác|hình thang|cổng|lối đi|hàng rào|khoảng hở|cắt|ghép|chi phí|giá tiền|không|chưa|gấp|hơn|kém|đổi đơn vị)\b"
                :@"\b(?:area|volume|square|circle|triangle|trapezoid|gate|path|fence|gap|cut|joined|cost|price|not|never|twice|longer|shorter|convert)\b")
            ||Has(perimeter+" "+question,vi?@"\bnửa\b":@"\b(?:half|semiperimeter)\b")
            // A generic "side length" or "how long is the other side" does
            // not prove a named length/width role. Reject role contradictions
            // only when the complete clause establishes the opposite role;
            // all unproved wording still follows the review path below.
            ||Dimension(known,vi?(context.TargetRole=="length"?"dài":"rộng"):context.TargetRole,@"\{f1\}",c.Language)
            ||Target(question,context.KnownRole,vi)||Lead(lead,context.KnownRole,vi)
            ||PerimeterTarget(question,vi)||PerimeterLead(lead,vi)||HalfLead(lead,vi)
            ||Dimension(perimeter,vi?"dài":"length",@"\{f0\}",c.Language)
            ||Dimension(perimeter,vi?"rộng":"width",@"\{f0\}",c.Language))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"Give the actual total rectangle perimeter and the specified known side, then ask only the other side without extra conditions.");
        if(OpenQuestionProse.Fields(draft).Any(f=>Has(f.Text.Trim(),@"[.!?:;].+")))
            return new(null,"InvalidText",ErrorDetails:"Use one sentence per field without an added event or technical note.");
        bool shapeProved=Shape(shape,c.Language),perimeterProved=Perimeter(perimeter,vi),
            knownProved=Dimension(known,vi?(context.KnownRole=="length"?"dài":"rộng"):context.KnownRole,@"\{f1\}",c.Language),
            targetProved=Target(question,context.TargetRole,vi),halfProved=HalfLead(half,vi),leadProved=Lead(lead,context.TargetRole,vi),
            punctuation=draft.Facts.All(x=>x.Text.TrimEnd().EndsWith('.'))&&question.TrimEnd().EndsWith('?');
        if(shapeProved&&perimeterProved&&knownProved&&targetProved&&halfProved&&leadProved&&punctuation)return new(draft,null,c);
        string[] unproved=[..new[]{(shapeProved,"rectangle shape"),(perimeterProved,"entire perimeter"),(knownProved,"known "+context.KnownRole),
            (targetProved,"unknown "+context.TargetRole),(halfProved,"half-perimeter solution lead"),(leadProved,"target solution lead"),(punctuation,"sentence punctuation")}
            .Where(x=>!x.Item1).Select(x=>x.Item2)];
        return new(draft,"SemanticReviewRequired",c,"Could not prove: "+string.Join(", ",unproved)+". Check wording before use.");
    }
}
