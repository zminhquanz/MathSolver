using MathSolver.Models;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class RectanglePerimeterOpenMeaning
{
    private static readonly ConcurrentDictionary<string,Regex> Patterns=new(StringComparer.Ordinal);
    private static readonly Regex Whitespace=QuestionTextRegex.Create(@"\s+");
    private static Regex Pattern(string pattern)=>Patterns.GetOrAdd(pattern,static p=>QuestionTextRegex.Create("(?i)"+p));
    private static bool Has(string text,string pattern)=>Pattern(pattern).IsMatch(text);
    private static bool Match(string text,string pattern)=>Has(Whitespace.Replace(text.Trim()," ").TrimEnd('.', '?', ':', '!').Trim(),@"\A(?:"+pattern+@")\z");
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft draft)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        string shape=draft.Facts![0].Text,length=draft.Facts[1].Text,width=draft.Facts[2].Text,
            question=draft.Question,lead=draft.SolutionLeads![0].Text;
        string all=string.Join(" ",OpenQuestionProse.Fields(draft).Select(f=>f.Text));
        if(Has(all,vi?@"\b(?:diện tích|thể tích|nửa chu vi|hình vuông|hình tròn|tam giác|hình thang|cổng|lối đi|hàng rào|khoảng hở|bỏ trống|chi phí|giá tiền|không|chưa|gấp|hơn|kém|đổi đơn vị)\b"
            :@"\b(?:area|volume|half|semiperimeter|square|circle|triangle|trapezoid|gate|path|fence|fencing|gap|opening|cost|price|not|never|twice|longer|shorter|convert)\b")
            ||Has(length,vi?@"\b(?:(?:chiều|cạnh) rộng|cạnh ngắn)\b":@"\b(?:width|wide|breadth)\b")
            ||Has(width,vi?@"\b(?:chiều|cạnh) dài\b":@"\b(?:length|long)\b"))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"Keep a rectangle, directly given length and width in their own roles, and its full perimeter; add no conditions.");
        if(Has(all,@"[²³]|\{v2\}\s*\^|\b(?:squared|cubed)\b")||Has(all,vi?@"\b(?:mét vuông|mét khối)\b":@"\bsquare (?:metres?|meters?|centimeters?|centimetres?)\b"))
            return new(null,"ChangedUnits",ErrorDetails:"Rectangle perimeter uses the common length unit, not an area/volume unit.");
        if(OpenQuestionProse.Fields(draft).Any(f=>Has(f.Text.Trim(),@"[.!?:;].+")))
            return new(null,"InvalidText",ErrorDetails:"Use one sentence per field, without an added event or technical note.");
        bool shapeProved=RectangleDirectOpenLanguage.Shape(shape,c.Language);
        bool lengthProved=RectangleDirectOpenLanguage.Dimension(length,vi?"dài":"length",@"\{f0\}",c.Language);
        bool widthProved=RectangleDirectOpenLanguage.Dimension(width,vi?"rộng":"width",@"\{f1\}",c.Language);
        bool targetProved=vi
            ?Match(question,@"(?:hỏi )?chu vi (?:của )?\{v0\} (?:là|bằng|đạt giá trị) bao nhiêu \{v2\}")
                ||Match(question,@"chu vi (?:của )?\{v0\} tính theo \{v2\} (?:là|bằng) bao nhiêu")
            :Match(question,@"what is the perimeter of \{v0\} in \{v2\}")
                ||Match(question,@"what is \{v0\}['’]s perimeter in \{v2\}")
                ||Match(question,@"in \{v2\}, what is (?:the perimeter of \{v0\}|\{v0\}['’]s perimeter)")
                ||Match(question,@"what is (?:the perimeter of \{v0\}|\{v0\}['’]s perimeter), measured in \{v2\}")
                ||Match(question,@"how long is the perimeter of \{v0\} in \{v2\}");
        bool leadProved=vi?Match(lead,@"chu vi (?:của )?\{v0\}(?: là)?")
            :Match(lead,@"(?:the )?perimeter of \{v0\}")||Match(lead,@"\{v0\}['’]s perimeter");
        bool punctuation=draft.Facts.All(f=>f.Text.TrimEnd().EndsWith('.'))&&question.TrimEnd().EndsWith('?');
        if(shapeProved&&lengthProved&&widthProved&&targetProved&&leadProved&&punctuation)return new(draft,null,c);
        string[] unproved=[..new[]{(shapeProved,"rectangle shape"),(lengthProved,"length role/unit"),
            (widthProved,"width role/unit"),(targetProved,"full-perimeter question"),(leadProved,"solution lead"),(punctuation,"sentence punctuation")}
            .Where(check=>!check.Item1).Select(check=>check.Item2)];
        return new(draft,"SemanticReviewRequired",c,"Could not prove: "+string.Join(", ",unproved)+". Check wording before use.");
    }
}
