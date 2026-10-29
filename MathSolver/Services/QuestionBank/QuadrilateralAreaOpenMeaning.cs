using MathSolver.Models;
using static MathSolver.Services.QuestionBank.RectangleDirectOpenLanguage;

namespace MathSolver.Services.QuestionBank;

internal static class QuadrilateralAreaOpenMeaning
{
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft draft)
    {
        bool vi=c.Language==AppLanguage.Vietnamese,trap=c.BankVariant==QuadrilateralAreaPilot.TrapezoidVariant;
        string shape=draft.Facts![0].Text,basis=draft.Facts[1].Text,height=draft.Facts[2].Text,question=draft.Question,lead=draft.SolutionLeads![0].Text;
        string all=string.Join(" ",OpenQuestionProse.Fields(draft).Select(x=>x.Text));
        if(Has(all,@"[²³^]|\b(?:squared|cubed)\b")||Has(all,vi?@"\b(?:mét vuông|mét khối)\b":@"\bsquare (?:metres?|meters?|centimeters?|centimetres?)\b"))
            return new(null,"ChangedUnits",ErrorDetails:"Keep v2 as length and v3 as the immutable area unit.");
        // An explicit change to the parallel-base relation is a known wrong
        // relation. Other denials and hypothetical assertions remain unproved.
        if(trap&&(Has(basis,vi?@"\b(?:không song song|không có hai đáy song song)\b":@"\b(?:nonparallel|non-parallel|not parallel)\b")
            ||Has(basis,vi?@"đáy nhỏ (?:dài|bằng) \{f0\}|đáy lớn (?:dài|bằng) \{f1\}":@"(?:shorter (?:base )?(?:measuring|of|is|measures) \{f0\}|longer (?:base )?(?:measuring|of|is|measures) \{f1\})")))
            return Reject();
        if(Has(all,vi?@"\b(?:không|chưa|nếu|chỉ khi|có thể|giả sử)\b":@"\b(?:not|never|if|unless|might|may|suppose|assuming)\b"))
            return Review(draft,c,"Qualified dimensions or target require review.");
        string wrongShape=trap?vi?@"hình bình hành":@"parallelogram":vi?@"hình thang":@"trapezoid";
        if(Has(all,vi?@"\b(?:chu vi|thể tích|hình chữ nhật|hình vuông|hình tròn|hình tam giác|cắt|bớt|ghép|lối đi|lát|gạch|chi phí|giá tiền|nửa|gấp|hơn|kém|đổi đơn vị)\b"
                :@"\b(?:perimeter|volume|rectangle|square|circle|triangle|cut|cutting|remove|removed|joined|path|tile|tiles|tiling|cost|price|half|twice|longer than|shorter than|convert)\b")
            ||Has(all,@"\b(?:"+wrongShape+@")\b")
            ||Has(all,vi?@"\bghép thêm\b":@"\bjoining (?:another|an? extra|an? additional|a second)\b")
            ||Has(basis,vi?@"\b(?:chiều cao|cạnh bên|đường chéo)\b":@"\b(?:height|altitude|sloping|diagonal)\b")
            ||Has(height,vi?@"\b(?:đáy khác|cạnh bên|cạnh xiên|đường chéo)\b":@"\b(?:another base|other base|different base|sloping side|diagonal)\b")
            ||Match(height,vi?@"(?:một )?cạnh (?:bên )?(?:của )?\{v0\} (?:là|bằng|dài) \{f[12]\} \{v2\}":@"(?:a|one|the) side of \{v0\} (?:is|measures|equals) \{f[12]\} \{v2\}")
            ||Has(height,vi?@"\A(?:độ dài )?đáy\b":@"\Athe (?:base|base length) of\b"))return Reject();
        if(OpenQuestionProse.Fields(draft).Any(f=>Has(f.Text.Trim(),@"[.!?:;].+")))
            return new(null,"InvalidText",ErrorDetails:"Use one sentence per field.");
        bool shapeProved=Shape(shape,c.Language)
            ||(!vi&&Match(shape,@"\{v0\} possesses the form of a \{v1\}"));
        bool baseProved=trap?ParallelBases(basis,c.Language):Base(basis,c.Language);
        string amount=trap?@"\{f2\}":@"\{f1\}";
        string supplied=trap?@"(?:those|these|the supplied|the given|the two parallel) bases":@"(?:that(?: specific)?|this|the supplied|the given|the stated) base";
        string viBase=trap?@"giữa (?:chính )?hai đáy đó":@"với (?:chính )?(?:cạnh )?đáy đó";
        bool heightProved=vi
            ?Match(height,@"chiều cao (?:tương ứng )?(?:đo )?vuông góc "+viBase+@" (?:của )?\{v0\} (?:là|bằng|đo được|dài) "+amount+@" \{v2\}")
                ||Match(height,@"\{v0\} có chiều cao (?:tương ứng )?(?:đo )?vuông góc "+viBase+@" (?:(?:là|bằng|dài) )?"+amount+@" \{v2\}")
                ||(!trap&&Match(height,@"chiều cao vuông góc của \{v0\} (?:ứng với|tương ứng với|theo) đáy đó (?:là|bằng|đo được) "+amount+@" \{v2\}"))
                ||(!trap&&Match(height,@"chiều cao vuông góc tương ứng với (?:chính )?(?:cạnh )?đáy đó của \{v0\} (?:là|bằng|đo được|dài) "+amount+@" \{v2\}"))
                ||(trap&&Match(height,@"khoảng cách vuông góc giữa (?:chính )?(?:hai đáy đó|hai cạnh song song đó) của \{v0\} (?:là|bằng|đo được|dài) "+amount+@" \{v2\}"))
            :Match(height,@"the perpendicular (?:height|altitude) of \{v0\} "+(trap?"between ":@"(?:corresponding to|to|for) ")+supplied+@" (?:is|measures|equals) "+amount+@" \{v2\}")
                ||Match(height,@"\{v0\} has a perpendicular (?:height|altitude) of "+amount+@" \{v2\} "+(trap?"between ":@"(?:corresponding to|to|for) ")+supplied)
                ||Match(height,@"the (?:height|altitude) of \{v0\},? "+(trap?"measured perpendicularly between ":"perpendicular to ")+supplied+@",? (?:is|measures|equals) "+amount+@" \{v2\}")
                ||Match(height,@"the perpendicular (?:height|altitude) "+(trap?"between ":@"(?:corresponding to|to|for) ")+supplied+@" of \{v0\} (?:is|measures|equals) "+amount+@" \{v2\}")
                ||(!trap&&Match(height,@"the (?:height|altitude) perpendicular to "+supplied+@" for \{v0\} (?:is|measures|equals|amounts to) "+amount+@" \{v2\}"))
                ||(!trap&&Match(height,@"the perpendicular (?:height|altitude) corresponding to "+supplied+@" (?:is|measures|equals) "+amount+@" \{v2\} for \{v0\}"));
        bool targetProved=vi
            ?Match(question,@"(?:hỏi )?(?:tổng )?diện tích (?:toàn bộ )?(?:của )?\{v0\} (?:là|bằng) bao nhiêu \{v3\}")
                ||Match(question,@"diện tích (?:toàn bộ )?(?:của )?\{v0\} tính theo \{v3\} (?:là|bằng) bao nhiêu")
            :Match(question,@"what is (?:the (?:(?:total|entire) )?area of \{v0\}|\{v0\}['’]s area) (?:expressed )?in \{v3\}")
                ||Match(question,@"in \{v3\}, what is (?:the (?:(?:total|entire) )?area of \{v0\}|\{v0\}['’]s area)")
                ||Match(question,@"what is the area of \{v0\}, measured in \{v3\}");
        bool leadProved=vi?Match(lead,@"diện tích (?:toàn bộ )?(?:của )?\{v0\}(?: là)?")
            :Match(lead,@"(?:the )?(?:(?:total|entire) )?area of \{v0\}")||Match(lead,@"\{v0\}['’]s area");
        bool punctuation=draft.Facts.All(f=>f.Text.TrimEnd().EndsWith('.'))&&question.TrimEnd().EndsWith('?');
        if(shapeProved&&baseProved&&heightProved&&targetProved&&leadProved&&punctuation)return new(draft,null,c);
        return Review(draft,c,"Could not prove complete shape, base roles, corresponding perpendicular height or whole-area target.");
    }
    private static bool Base(string text,AppLanguage language)=>language==AppLanguage.Vietnamese
        ?Match(text,@"(?:độ dài )?(?:(?:một )?cạnh )?đáy (?:của )?\{v0\} (?:có độ dài )?(?:là|bằng|đo được|dài) \{f0\} \{v2\}")
            ||Match(text,@"\{v0\} có (?:độ dài )?đáy (?:(?:là|bằng|dài) )?\{f0\} \{v2\}")
        :Match(text,@"the (?:supplied )?(?:base|base length) of \{v0\} (?:is|measures|equals) \{f0\} \{v2\}(?: long)?")
            ||Match(text,@"the measurement for the base of \{v0\} (?:is|measures|equals) \{f0\} \{v2\}")
            ||Match(text,@"\{v0\} has a base (?:of|measuring) \{f0\} \{v2\}")
            ||Match(text,@"\{v0\}['’]s base (?:is|measures|equals) \{f0\} \{v2\}");
    private static bool ParallelBases(string text,AppLanguage language)=>language==AppLanguage.Vietnamese
        ?Match(text,@"\{v0\} có hai đáy song song, đáy lớn (?:dài|bằng) \{f0\} \{v2\} và đáy nhỏ (?:dài|bằng) \{f1\} \{v2\}")
            ||Match(text,@"hai đáy song song của \{v0\} có độ dài lần lượt là \{f0\} \{v2\} và \{f1\} \{v2\}")
            // A trapezoid's two parallel sides are its bases; keep the ordered supplied slots.
            ||Match(text,@"hai cạnh song song của \{v0\} lần lượt dài \{f0\} \{v2\} và \{f1\} \{v2\}")
            ||Match(text,@"hai đáy song song của \{v0\} (?:có độ dài|lần lượt dài) \{f0\} \{v2\} và \{f1\} \{v2\}, (?:theo thứ tự )?từ lớn đến nhỏ")
            ||Match(text,@"đáy lớn và đáy nhỏ song song của \{v0\} lần lượt (?:dài|bằng) \{f0\} \{v2\} và \{f1\} \{v2\}")
        :Match(text,@"\{v0\} has two parallel bases, the longer (?:base )?(?:measuring|of) \{f0\} \{v2\} and the shorter (?:base )?(?:measuring|of) \{f1\} \{v2\}")
            ||Match(text,@"the two parallel bases of \{v0\} measure \{f0\} \{v2\} and \{f1\} \{v2\}, (?:respectively )?from longer to shorter")
            ||Match(text,@"the longer and shorter parallel bases of \{v0\} (?:are|measure|equal) \{f0\} \{v2\} and \{f1\} \{v2\}(?: respectively)?");
    private static BasicDraftValidation Reject()=>new(null,"ChangedRelationOrTarget",ErrorDetails:"Keep supplied base roles, perpendicular height and entire area target.");
    private static BasicDraftValidation Review(BasicQuestionDraft draft,BasicQuestionContract c,string detail)=>new(draft,"SemanticReviewRequired",c,detail);
}
