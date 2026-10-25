using MathSolver.Models;
using static MathSolver.Services.QuestionBank.RectangleDirectOpenLanguage;

namespace MathSolver.Services.QuestionBank;

internal static class TriangleAreaOpenMeaning
{
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft draft)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        string shape=draft.Facts![0].Text,basis=draft.Facts[1].Text,height=draft.Facts[2].Text,question=draft.Question,lead=draft.SolutionLeads![0].Text;
        string all=string.Join(" ",OpenQuestionProse.Fields(draft).Select(x=>x.Text));
        if(Has(all,@"[²³^]|\b(?:squared|cubed)\b")||Has(all,vi?@"\b(?:mét vuông|mét khối)\b":@"\bsquare (?:metres?|meters?|centimeters?|centimetres?)\b"))
            return new(null,"ChangedUnits",ErrorDetails:"Use the length unit v2 and the immutable area unit v3 without adding a power.");
        // Negated, conditional and otherwise qualified assertions do not prove givens.
        // Keep the established unknown => review disposition rather than guessing intent.
        if(Has(all,vi?@"\b(?:không|chưa|nếu|chỉ khi|có thể|giả sử)\b":@"\b(?:not|never|if|unless|might|may|suppose|assuming)\b"))
            return Review(draft,c,"Qualified triangle facts or target require review.");
        if(Has(all,vi?@"\b(?:chu vi|thể tích|hình chữ nhật|hình vuông|hình tròn|hình thang|cắt|bớt|ghép|lối đi|lát|gạch|chi phí|giá tiền|nửa|gấp|hơn|kém|đổi đơn vị)\b"
            :@"\b(?:perimeter|volume|rectangle|square|circle|trapezoid|cut|cutting|remove|removed|join|joined|path|tile|tiles|tiling|cost|price|half|twice|longer|shorter|convert)\b")
            ||Has(basis,vi?@"\b(?:chiều cao|cạnh bên|chiều dài|chiều rộng)\b":@"\b(?:height|altitude|width)\b")
            ||Has(height,vi?@"\b(?:đáy khác|một đáy khác|đáy còn lại)\b":@"\b(?:another base|other base|different base)\b")
            // A side may be chosen as the base. Its anaphoric mention in an
            // unproved height description is not a known side-length swap.
            ||Match(height,vi?@"(?:độ dài )?(?:cạnh|cạnh bên|cạnh huyền) (?:của )?\{v0\} (?:là|bằng|dài|đo được) \{f1\} \{v2\}"
                :@"(?:a|one|the|another) (?:side|hypotenuse) of \{v0\} (?:is|measures|equals) \{f1\} \{v2\}(?: long)?")
            ||Match(height,vi?@"\{v0\} có (?:cạnh|cạnh bên|cạnh huyền) (?:(?:là|bằng|dài) )?\{f1\} \{v2\}"
                :@"\{v0\} has (?:a|one) (?:side|hypotenuse) (?:of|measuring) \{f1\} \{v2\}")
            ||(!vi&&Match(height,@"\{v0\}['’]s (?:side|hypotenuse) (?:is|measures|equals) \{f1\} \{v2\}(?: long)?"))
            ||Match(height,vi?@"(?:độ dài )?đáy (?:của )?\{v0\} (?:là|bằng|dài) \{f1\} \{v2\}":@"the base of \{v0\} (?:is|measures|equals) \{f1\} \{v2\}")
            ||Match(height,vi?@"\{v0\} có (?:độ dài )?đáy (?:(?:là|bằng|dài) )?\{f1\} \{v2\}":@"\{v0\} has a base (?:of|measuring) \{f1\} \{v2\}"))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"Keep f0 as the supplied base and f1 as its corresponding perpendicular height; ask only the entire triangle area.");
        if(OpenQuestionProse.Fields(draft).Any(f=>Has(f.Text.Trim(),@"[.!?:;].+")))
            return new(null,"InvalidText",ErrorDetails:"Use one sentence per field.");
        bool shapeProved=Shape(shape,c.Language);
        bool baseProved=vi
            ?Match(basis,@"(?:độ dài )?đáy (?:của )?\{v0\} (?:là|bằng|đo được|dài) \{f0\} \{v2\}")
                ||Match(basis,@"\{v0\} có (?:độ dài )?đáy (?:(?:là|bằng|dài) )?\{f0\} \{v2\}")
            :Match(basis,@"the (?:base|base length) of \{v0\} (?:is|measures|equals) \{f0\} \{v2\}")
                ||Match(basis,@"\{v0\} has a base (?:of|measuring) \{f0\} \{v2\}")
                ||Match(basis,@"\{v0\}['’]s base (?:is|measures) \{f0\} \{v2\}")
                ||Match(basis,@"the base of \{v0\} is \{f0\} \{v2\} long");
        const string englishBase=@"(?:that|this|the supplied|the given|the stated) base";
        bool heightProved=vi
            ?Match(height,@"chiều cao (?:tương ứng )?vuông góc với (?:chính )?đáy đó (?:của )?\{v0\} (?:là|bằng|đo được|dài) \{f1\} \{v2\}")
                ||Match(height,@"\{v0\} có chiều cao (?:tương ứng )?vuông góc với (?:chính )?đáy đó (?:(?:là|bằng|dài) )?\{f1\} \{v2\}")
                ||Match(height,@"chiều cao của \{v0\} vuông góc với đáy đó và (?:dài|bằng) \{f1\} \{v2\}")
                ||Match(height,@"chiều cao vuông góc của \{v0\} (?:ứng với|tương ứng với|theo) đáy đó (?:là|bằng|đo được) \{f1\} \{v2\}")
            :Match(height,@"the perpendicular (?:height|altitude) of \{v0\} (?:corresponding to|to|for) "+englishBase+@" (?:is|measures|equals) \{f1\} \{v2\}")
                ||Match(height,@"\{v0\} has a perpendicular (?:height|altitude) of \{f1\} \{v2\} (?:corresponding to|to|for) "+englishBase)
                ||Match(height,@"the (?:height|altitude) of \{v0\},? perpendicular to "+englishBase+@",? (?:is|measures) \{f1\} \{v2\}")
                ||Match(height,@"the perpendicular (?:height|altitude) (?:corresponding to|to|for) "+englishBase+@" of \{v0\} (?:is|measures) \{f1\} \{v2\}")
                ||Match(height,@"\{v0\}['’]s perpendicular (?:height|altitude) (?:corresponding to|to|for) "+englishBase+@" (?:is|measures) \{f1\} \{v2\}");
        bool targetProved=vi
            ?Match(question,@"(?:hỏi )?diện tích (?:của )?\{v0\} (?:là|bằng) bao nhiêu \{v3\}")
                ||Match(question,@"diện tích (?:của )?\{v0\} tính theo \{v3\} (?:là|bằng) bao nhiêu")
            :Match(question,@"what is (?:the area of \{v0\}|\{v0\}['’]s area) in \{v3\}")
                ||Match(question,@"in \{v3\}, what is (?:the area of \{v0\}|\{v0\}['’]s area)")
                ||Match(question,@"what is the area of \{v0\}, measured in \{v3\}");
        bool leadProved=vi?Match(lead,@"diện tích (?:của )?\{v0\}(?: là)?")
            :Match(lead,@"(?:the )?area of \{v0\}")||Match(lead,@"\{v0\}['’]s area");
        bool punctuation=draft.Facts.All(f=>f.Text.TrimEnd().EndsWith('.'))&&question.TrimEnd().EndsWith('?');
        if(shapeProved&&baseProved&&heightProved&&targetProved&&leadProved&&punctuation)return new(draft,null,c);
        string[] missing=[..new[]{(shapeProved,"triangle shape"),(baseProved,"base role/unit"),(heightProved,"corresponding perpendicular height/length unit"),
            (targetProved,"entire area question/unit"),(leadProved,"area lead"),(punctuation,"punctuation")}.Where(x=>!x.Item1).Select(x=>x.Item2)];
        return Review(draft,c,"Could not prove: "+string.Join(", ",missing)+".");
    }
    private static BasicDraftValidation Review(BasicQuestionDraft draft,BasicQuestionContract c,string detail)=>new(draft,"SemanticReviewRequired",c,detail);
}
