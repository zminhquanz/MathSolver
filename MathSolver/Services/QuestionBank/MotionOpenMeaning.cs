using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class MotionOpenMeaning
{
    private static bool Match(string text,string pattern)=>Regex.IsMatch(
        Regex.Replace(text.Trim().ToLowerInvariant(),@"\s+"," ",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)).TrimEnd(' ','.','?',':','!'),
        @"\A(?:"+pattern+@")\z",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        bool Proof(string text,string v,string e)=>Match(text,vi?v:e);
        string bare=string.Join(" ",OpenQuestionProse.Fields(d).Select(f=>Regex.Replace(f.Text,@"\{[fv]\d+\}","")));
        if(Regex.IsMatch(bare,vi?@"\b(?:km|giờ|ô tô|xe buýt|xe đạp)\b":@"\b(?:km|hours?|cars?|buses?|bicycles?)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)))
            return new(null,"ChangedUnits",ErrorDetails:"Keep traveller and unit names in placeholders.");
        if(OpenQuestionProse.Fields(d).Any(f=>Regex.IsMatch(f.Text,@"\p{L}\{[fv]\d+\}|\{[fv]\d+\}\p{L}",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100))))
            return new(null,"InvalidText",ErrorDetails:"Keep placeholders as complete words.");
        string kind=c.SceneId.Split('/')[1];
        var novel=NovelFactPatterns(kind,vi);
        var editorial=EditorialFactPatterns(kind,vi);
        if(d.Facts!.Any(f=>Regex.IsMatch(f.Text,vi?@"\bchiếc\s+\{f\d+\}":@"\bthe\s+\{f\d+\}\s+\{v\d+\}\s+travels\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100))))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"A measurement was used as the vehicle.");
        var reference=MotionOpenPilot.Relations(c.Language).Single(r=>r.Id==kind);
        var clauses=new[]{reference.Fact0,reference.Fact1,reference.Fact2};
        for(int i=0;i<d.Facts!.Count;i++)
        {
            foreach(string clause in i==0?new[]{reference.Fact0,reference.AlternateFact0}:new[]{clauses[i]!})
            {
                string denial=(vi?"không phải ":"it is not true that ")+clause.TrimEnd('.').ToLowerInvariant();
                if(Match(d.Facts[i].Text,Regex.Escape(denial)))
                    return new(null,"ChangedRelationOrTarget",ErrorDetails:"An explicitly required motion premise is denied.");
            }
        }
        for(int i=0;i<d.Facts.Count;i++)
            if(Match(d.Facts[i].Text,Regex.Escape(vi?"không phải ":"it is not true that ")+"(?:"+novel[i]+"|"+editorial[i]+")"))
                return new(null,"ChangedRelationOrTarget",ErrorDetails:"An explicitly required motion premise is denied.");
        if(kind=="rest"&&Proof(d.Facts[2].Text,@"sau khi kết thúc mới nghỉ \{f2\} \{v4\}",@"after arrival there is one stop lasting \{f2\} \{v4\}"))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"The stop was moved outside the supplied elapsed duration.");
        if(kind!="rest")
        {
            string changed=reference.Fact1.Replace(vi?"không nghỉ":"no stops",vi?"có nghỉ":"a stop",StringComparison.Ordinal);
            if(changed!=reference.Fact1&&Match(d.Facts[1].Text,Regex.Escape(changed.TrimEnd('.').ToLowerInvariant())))
                return new(null,"ChangedRelationOrTarget",ErrorDetails:"A stop was added to the continuous no-stop relation.");
        }
        if(d.Facts.Any(f=>Regex.IsMatch(f.Text,vi?@"\.\s+Sau đó đi thêm một chặng\.\s*$":@"\.\s+Then it makes a return journey\.\s*$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100))))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"An extra journey was added.");
        static bool Complete(string text,char ending)
        {
            text=text.TrimEnd();return text.Length>1&&text[^1]==ending&&".!?:".IndexOf(text[^2])<0;
        }
        if(d.Facts!.Any(f=>!Complete(f.Text,'.'))||!Complete(d.Question,'?'))
            return new(d,"SemanticReviewRequired",c,"Facts and the question need complete sentence punctuation.");
        bool given=kind is "speed" or "time"
            ?Proof(d.Facts![0].Text,@"một \{v0\} (?:đi được|đi quãng đường dài) \{f0\} \{v3\}",@"a \{v0\} (?:travels \{f0\} \{v3\}|covers a distance of \{f0\} \{v3\})")
            :kind=="rest"
            ?Proof(d.Facts![0].Text,@"một \{v0\} giữ vận tốc \{f0\} \{v1\} khi đang di chuyển|khi di chuyển, một \{v0\} giữ vận tốc \{f0\} \{v1\}",
                @"a \{v0\} maintains a speed of \{f0\} \{v1\} while moving|while moving, a \{v0\} keeps a speed of \{f0\} \{v1\}")
            :Proof(d.Facts![0].Text,@"một \{v0\} (?:chuyển động đều với vận tốc|đi với vận tốc không đổi) \{f0\} \{v1\}",@"a \{v0\} (?:travels at a constant speed of|maintains a speed of) \{f0\} \{v1\}");
        bool duration=kind switch {
            "distance"=>Proof(d.Facts![1].Text,@"thời gian thực sự di chuyển là \{f1\} \{v2\}, liên tục và không nghỉ",@"the actual travel time is \{f1\} \{v2\}, continuously with no stops"),
            "speed"=>Proof(d.Facts![1].Text,@"thời gian thực sự di chuyển là \{f1\} \{v2\}, chuyển động đều và không nghỉ",@"the actual travel time is \{f1\} \{v2\}, at constant speed with no stops"),
            "time"=>Proof(d.Facts![1].Text,@"vận tốc luôn là \{f1\} \{v1\} trong suốt chuyến đi liên tục, không nghỉ",@"the speed remains \{f1\} \{v1\} throughout a continuous journey with no stops"),
            _=>Proof(d.Facts![1].Text,@"tổng thời gian từ lúc bắt đầu đến lúc kết thúc là \{f1\} \{v2\}",@"the total elapsed time from start to finish is \{f1\} \{v2\}")};
        bool rest=kind!="rest"||Proof(d.Facts![2].Text,@"trong khoảng đó có đúng một lần nghỉ \{f2\} \{v4\}",@"that interval includes exactly one stop lasting \{f2\} \{v4\}");
        given|=Match(d.Facts[0].Text,novel[0]);
        duration|=Match(d.Facts[1].Text,novel[1]);
        if(kind=="rest")rest|=Match(d.Facts[2].Text,novel[2]);
        given|=Match(d.Facts[0].Text,editorial[0]);
        duration|=Match(d.Facts[1].Text,editorial[1]);
        if(kind=="rest")rest|=Match(d.Facts[2].Text,editorial[2]);
        bool target=kind switch {
            "speed"=>Proof(d.Question,@"vận tốc là bao nhiêu \{v1\}",@"what is the speed in \{v1\}"),
            "time"=>Proof(d.Question,@"thời gian thực sự di chuyển là bao nhiêu \{v2\}",@"what is the actual travel time in \{v2\}"),
            _=>Proof(d.Question,@"quãng đường đi được là bao nhiêu \{v3\}",@"what distance is travelled in \{v3\}")};
        bool lead=kind switch {
            "speed"=>Proof(d.SolutionLeads![0].Text,"vận tốc","speed"),
            "time"=>Proof(d.SolutionLeads![0].Text,"thời gian thực sự di chuyển","actual travel time"),
            _=>Proof(d.SolutionLeads![0].Text,"quãng đường đi được","distance travelled")};
        target|=kind switch {
            "speed"=>Proof(d.Question,@"vận tốc chuyển động bằng bao nhiêu \{v1\}",@"what uniform speed is maintained in \{v1\}"),
            "time"=>Proof(d.Question,@"thời gian di chuyển thực tế là bao nhiêu \{v2\}",@"how long does the actual travel take in \{v2\}"),
            _=>Proof(d.Question,@"quãng đường đã đi dài bao nhiêu \{v3\}",@"how far does it travel in \{v3\}")};
        lead|=kind switch {"speed"=>Proof(d.SolutionLeads[0].Text,"vận tốc chuyển động","uniform speed"),
            "time"=>Proof(d.SolutionLeads[0].Text,"thời gian di chuyển thực tế","actual time spent travelling"),
            _=>Proof(d.SolutionLeads[0].Text,"quãng đường đã đi","distance covered")};
        target|=kind switch {
            "speed"=>Proof(d.Question,@"vận tốc của (?:chiếc xe|xe) là bao nhiêu \{v1\}",@"what is (?:its|the vehicle's) (?:constant |uniform )?speed in \{v1\}"),
            "time"=>Proof(d.Question,@"(?:khoảng )?thời gian thực sự di chuyển là bao nhiêu \{v2\}",@"how long is the actual travel duration in \{v2\}"),
            _=>Proof(d.Question,@"(?:tổng )?quãng đường (?:đã đi được|mà chiếc xe đã đi được) là bao nhiêu \{v3\}",@"what distance does (?:it|the vehicle) cover in \{v3\}")};
        lead|=kind switch {"speed"=>Proof(d.SolutionLeads[0].Text,"vận tốc","constant speed"),
            "time"=>Proof(d.SolutionLeads[0].Text,"thời gian di chuyển",@"actual (?:moving|travel) duration"),
            _=>Proof(d.SolutionLeads[0].Text,"quãng đường",@"(?:total )?distance covered")};
        return given&&duration&&rest&&target&&lead?new(d,null,c):new(d,"SemanticReviewRequired",c,
            "The single traveller, uniform moving speed, actual versus elapsed time, included stop or requested quantity could not be proved.");
    }
    // Full-clause proofs preserve roles and logical scope; no negation stripping.
    private static string[] EditorialFactPatterns(string kind,bool vi)
    {
        string vehicle=vi?@"(?:một chiếc|một|chiếc) \{v0\}":@"(?:a|the) \{v0\}";
        string first=kind is "speed" or "time"
            ?vehicle+(vi?@" (?:đã )?(?:đi hết quãng đường|di chuyển được|đi được) \{f0\} \{v3\}":@" covers a distance of \{f0\} \{v3\}")
            :vehicle+(kind=="rest"
                ?vi?@" duy trì (?:vận tốc|tốc độ) \{f0\} \{v1\} khi đang di chuyển":@" maintains a constant speed of \{f0\} \{v1\} while moving"
                :vi?@" duy trì (?:vận tốc|tốc độ) \{f0\} \{v1\} trong suốt hành trình":@" maintains a (?:steady|constant) speed of \{f0\} \{v1\}");
        string second=kind switch {
            "distance"=>vi?@"khoảng thời gian di chuyển liên tục là \{f1\} \{v2\} mà không hề dừng lại":@"the (?:uninterrupted travel duration|total time spent moving without interruption) amounts to \{f1\} \{v2\}",
            "speed"=>vi?@"thời gian di chuyển là \{f1\} \{v2\}, vận tốc không đổi và không nghỉ":@"the actual moving duration is \{f1\} \{v2\}, with constant speed and no stops",
            "time"=>vi?@"vận tốc là \{f1\} \{v1\} và không đổi trong suốt hành trình không nghỉ":@"its constant speed is maintained at \{f1\} \{v1\} during the entire trip without any (?:interruptions|pauses)",
            _=>vi?@"tổng thời gian trôi qua cho hành trình này là \{f1\} \{v2\}":@"the total elapsed duration is \{f1\} \{v2\}"};
        return kind=="rest"?[first,second,vi?@"trong suốt quá trình đó, có một lần dừng lại với thời gian là \{f2\} \{v4\}":@"this elapsed duration includes exactly one rest lasting \{f2\} \{v4\}"]:[first,second];
    }
    private static string[] NovelFactPatterns(string kind,bool vi)
    {
        string first=kind is "speed" or "time"
            ?vi?@"chiếc \{v0\} đi hết quãng đường \{f0\} \{v3\}":@"the \{v0\} covers \{f0\} \{v3\}"
            :kind=="rest"?vi?@"chiếc \{v0\} giữ vận tốc ổn định \{f0\} \{v1\} lúc di chuyển":@"the \{v0\} moves at a constant speed of \{f0\} \{v1\} whenever it is travelling"
            :vi?@"chiếc \{v0\} di chuyển với vận tốc ổn định \{f0\} \{v1\}":@"the \{v0\} moves at a steady speed of \{f0\} \{v1\}";
        string second=kind switch {
            "distance"=>vi?@"thời gian di chuyển liên tục không nghỉ là \{f1\} \{v2\}":@"it travels continuously for \{f1\} \{v2\} without stopping",
            "speed"=>vi?@"chuyến đi liên tục không nghỉ dài \{f1\} \{v2\}, vận tốc không đổi":@"the journey takes \{f1\} \{v2\} of actual moving time, with constant speed and no stops",
            "time"=>vi?@"vận tốc giữ ở mức \{f1\} \{v1\} trong cả chuyến đi, liên tục không nghỉ":@"its speed stays at \{f1\} \{v1\} during continuous travel with no stops",
            _=>vi?@"từ lúc khởi hành đến lúc đến nơi, tổng thời gian là \{f1\} \{v2\}":@"from departure until arrival, the elapsed time is \{f1\} \{v2\}"};
        return kind=="rest"?[first,second,vi?@"có đúng một lần nghỉ \{f2\} \{v4\} trong tổng thời gian đó":@"exactly one rest of \{f2\} \{v4\} occurs within that elapsed time"]:[first,second];
    }
}
