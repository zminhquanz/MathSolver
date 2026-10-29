using MathSolver.Models;
using static MathSolver.Services.QuestionBank.RectangleDirectOpenLanguage;

namespace MathSolver.Services.QuestionBank;

internal static class MapScaleOpenMeaning
{
    internal static bool ProvesReference(MapScaleOpenPilot.Context context,AppLanguage language)
    {
        if(new[]{context.ScaleFact,context.LengthFact,context.Question,context.CentimetreLead,context.ActualLead}.Any(string.IsNullOrWhiteSpace))return false;
        var d=new BasicQuestionDraft("","",context.Question,context.ActualLead,"story")
        {Facts=[new("fact_0",context.ScaleFact),new("fact_1",context.LengthFact)],
            SolutionLeads=[new("step_0",context.CentimetreLead),new("step_1",context.ActualLead)]};
        return Validate(language,d,null).IsValid;
    }
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>Validate(c.Language,d,c);
    private static BasicDraftValidation Validate(AppLanguage language,BasicQuestionDraft d,BasicQuestionContract? c)
    {
        bool vi=language==AppLanguage.Vietnamese;
        if(d.Facts is not {Count:2}||d.SolutionLeads is not {Count:2})return new(null,"InvalidFields");
        string scale=d.Facts[0].Text,length=d.Facts[1].Text,all=string.Join(" ",OpenQuestionProse.Fields(d).Select(x=>x.Text));
        if(Has(all,@"[²³^]|\b(?:km|mm|cm|metres?|meters?|centimetres?|centimeters?|kilometres?|kilometers?)\b")
            ||Has(all,vi?@"\b(?:mét|xăng-ti-mét|ki-lô-mét)\b":@"\b(?:square|cubic|squared|cubed)\b"))
            return new(null,"ChangedUnits",ErrorDetails:"Use only v2 for map centimetres and v3 for actual metres.");
        if(Has(all,vi?@"\b(?:không|chưa|nếu|chỉ khi|giả sử|có thể|trừ khi)\b":@"\b(?:not|never|no|if|unless|only if|might|may|suppose|assuming)\b"))
            return Review(d,c,"Qualified map facts do not prove a direct distance relation.");
        bool reversedScale=vi?Has(scale,@"\{f0\} (?:trên|đến|so với) một"):Has(scale,@"\{f0\} (?:to|for) one");
        bool reversedLength=Has(length,vi?@"\b(?:thực tế|ngoài thực địa|ngoài đời)\b":@"\b(?:in reality|actual|real-world|on the ground)\b");
        bool changedTarget=Has(d.Question+" "+string.Join(" ",d.SolutionLeads.Select(x=>x.Text)),vi?@"\b(?:trên bản đồ|diện tích|chu vi|thể tích|tỉ lệ|tỷ lệ)\b":@"\b(?:map length|on the map|area|perimeter|volume|scale)\b");
        if(reversedScale||reversedLength||changedTarget||Has(all,vi?@"\b(?:hai đoạn|nhiều đoạn|đoạn khác|đường gấp khúc|rồi|sau đó|thêm|tổng độ dài)\b":@"\b(?:two segments|another segment|second segment|combined|additional|then|afterwards|total route)\b"))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"Preserve scale one to n, one map segment in cm and its actual length in m.");
        if(OpenQuestionProse.Fields(d).Any(x=>Has(x.Text.Trim(),@"[.!?:;].+")))return new(null,"InvalidText");
        bool scaleProved=vi
            ?Match(scale,@"(?:tỉ|tỷ) lệ (?:của )?\{v0\} (?:là|bằng) một trên \{f0\}")
                ||Match(scale,@"\{v0\} (?:có|được vẽ theo) (?:tỉ|tỷ) lệ một trên \{f0\}")
                ||Match(scale,@"trên \{v0\},? (?:tỉ|tỷ) lệ được ghi là một trên \{f0\}")
            :Match(scale,@"\{v0\} (?:has|uses|shows|is drawn at) (?:a )?scale of one to \{f0\}")
                ||Match(scale,@"the scale (?:used for|of|shown on) \{v0\} is one to \{f0\}");
        bool lengthProved=vi
            ?Match(length,@"(?:trên \{v0\},? \{v1\}|\{v1\} trên \{v0\}) (?:dài|đo được|có độ dài(?: là)?) \{f1\} \{v2\}")
                ||Match(length,@"độ dài của \{v1\} được đo trên \{v0\} là \{f1\} \{v2\}")
                ||Match(length,@"\{v1\} đo trên \{v0\} có độ dài \{f1\} \{v2\}")
            :Match(length,@"(?:on \{v0\},? \{v1\}|\{v1\} on \{v0\}) (?:is|measures|has a length of) \{f1\} \{v2\}(?: long)?")
                ||Match(length,@"\{v1\} measures \{f1\} \{v2\} on \{v0\}")
                ||Match(length,@"the (?:length|measurement) of \{v1\} (?:drawn )?on \{v0\} is \{f1\} \{v2\}");
        bool targetProved=vi
            ?Match(d.Question,@"(?:hỏi )?độ dài thực tế (?:của )?\{v1\} (?:là|bằng) bao nhiêu \{v3\}")
                ||Match(d.Question,@"độ dài thực tế (?:của )?\{v1\} tính theo \{v3\} (?:là|bằng) bao nhiêu")
                ||Match(d.Question,@"(?:hỏi )?\{v1\} có độ dài thực tế (?:là )?bao nhiêu \{v3\}")
            :Match(d.Question,@"what is the (?:actual|real-world) length of \{v1\} (?:expressed )?in \{v3\}")
                ||Match(d.Question,@"in \{v3\}, what is the (?:actual|real-world) length of \{v1\}")
                ||Match(d.Question,@"how long is \{v1\} in reality,? (?:measured )?in \{v3\}")
                ||Match(d.Question,@"how long is the actual distance represented by \{v1\} in \{v3\}")
                ||Match(d.Question,@"what distance does \{v1\} represent in reality,? measured in \{v3\}");
        bool first=Lead(d.SolutionLeads[0].Text,vi),last=Lead(d.SolutionLeads[1].Text,vi);
        bool punctuation=d.Facts.All(f=>f.Text.TrimEnd().EndsWith('.'))&&d.Question.TrimEnd().EndsWith('?');
        if(scaleProved&&lengthProved&&targetProved&&first&&last&&punctuation)return new(d,null,c);
        return Review(d,c,"Could not prove the one-to-denominator scale, map centimetres, actual metres or distance leads.");
    }
    private static bool Lead(string text,bool vi)=>vi?Match(text,@"(?:độ|chiều) dài (?:ngoài )?thực tế (?:của )?\{v1\}(?: là)?")
        :Match(text,@"(?:the )?(?:actual|real-world) length of \{v1\}")
            ||Match(text,@"(?:the )?actual distance represented by \{v1\}");
    private static BasicDraftValidation Review(BasicQuestionDraft d,BasicQuestionContract? c,string detail)=>new(d,"SemanticReviewRequired",c,detail);
}
