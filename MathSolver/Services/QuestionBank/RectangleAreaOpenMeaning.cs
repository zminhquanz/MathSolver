using MathSolver.Models;
using static MathSolver.Services.QuestionBank.RectangleDirectOpenLanguage;

namespace MathSolver.Services.QuestionBank;

internal static class RectangleAreaOpenMeaning
{
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft draft)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        string shape=draft.Facts![0].Text,length=draft.Facts[1].Text,width=draft.Facts[2].Text,
            question=draft.Question,lead=draft.SolutionLeads![0].Text;
        string all=string.Join(" ",OpenQuestionProse.Fields(draft).Select(f=>f.Text));
        if(Has(all,vi?@"\b(?:chu vi|thể tích|nửa|hình vuông|hình tròn|tam giác|hình thang|cắt|bớt|ghép|lối đi|lát|gạch|cổng|hàng rào|chi phí|giá tiền|không|chưa|gấp|hơn|kém|đổi đơn vị)\b"
            :@"\b(?:perimeter|volume|half|semiperimeter|square|circle|triangle|trapezoid|cut|cutting|remove|removed|joined|join|path|tile|tiles|tiling|gate|fence|cost|price|not|never|twice|longer|shorter|convert)\b")
            ||Has(length,vi?@"\b(?:(?:chiều|cạnh) rộng|cạnh ngắn)\b":@"\b(?:width|wide|breadth)\b")
            ||Has(width,vi?@"\b(?:chiều|cạnh) dài\b":@"\b(?:length|long)\b"))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"Keep directly supplied rectangle length and width in their own roles and ask its entire area; add no conditions.");
        // The immutable v3 already expands to the squared unit. Literal powers
        // or repeated unit words would change the dimensions after rendering.
        if(Has(all,@"[²³^]|\b(?:squared|cubed)\b")||Has(all,vi?@"\b(?:mét vuông|mét khối)\b":@"\bsquare (?:metres?|meters?|centimeters?|centimetres?)\b"))
            return new(null,"ChangedUnits",ErrorDetails:"Use v2 for side lengths and the immutable v3 for area, without an extra power.");
        if(OpenQuestionProse.Fields(draft).Any(f=>Has(f.Text.Trim(),@"[.!?:;].+")))
            return new(null,"InvalidText",ErrorDetails:"Use one sentence per field without an added event or technical note.");
        bool shapeProved=Shape(shape,c.Language);
        bool lengthProved=Dimension(length,vi?"dài":"length",@"\{f0\}",c.Language);
        bool widthProved=Dimension(width,vi?"rộng":"width",@"\{f1\}",c.Language);
        bool targetProved=vi
            ?Match(question,@"(?:hỏi )?diện tích (?:của )?\{v0\} (?:là|bằng|đạt giá trị) bao nhiêu \{v3\}")
                ||Match(question,@"diện tích (?:của )?\{v0\} tính theo \{v3\} (?:là|bằng) bao nhiêu")
            :Match(question,@"what is the area of \{v0\} in \{v3\}")
                ||Match(question,@"how much space does \{v0\} cover in \{v3\}")
                ||Match(question,@"what is \{v0\}['’]s area in \{v3\}")
                ||Match(question,@"in \{v3\}, what is (?:the area of \{v0\}|\{v0\}['’]s area)")
                ||Match(question,@"what is (?:the area of \{v0\}|\{v0\}['’]s area), measured in \{v3\}");
        bool leadProved=vi?Match(lead,@"diện tích (?:của )?\{v0\}(?: là)?")
            :Match(lead,@"(?:the )?area of \{v0\}")||Match(lead,@"\{v0\}['’]s area");
        bool punctuation=draft.Facts.All(f=>f.Text.TrimEnd().EndsWith('.'))&&question.TrimEnd().EndsWith('?');
        if(shapeProved&&lengthProved&&widthProved&&targetProved&&leadProved&&punctuation)return new(draft,null,c);
        string[] unproved=[..new[]{(shapeProved,"rectangle shape"),(lengthProved,"length role/unit"),
            (widthProved,"width role/unit"),(targetProved,"area question/unit"),(leadProved,"solution lead"),(punctuation,"sentence punctuation")}
            .Where(check=>!check.Item1).Select(check=>check.Item2)];
        return new(draft,"SemanticReviewRequired",c,"Could not prove: "+string.Join(", ",unproved)+". Check wording before use.");
    }
}
