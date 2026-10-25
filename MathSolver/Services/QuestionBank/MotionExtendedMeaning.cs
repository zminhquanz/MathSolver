using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class MotionExtendedMeaning
{
    private static string Normal(string text)=>Regex.Replace(text.Trim().ToLowerInvariant(),@"\s+"," ",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)).TrimEnd(' ','.','?',':','!');
    private static bool Matches(string text,string pattern)=>Regex.IsMatch(Normal(text),@"\A(?:"+pattern+@")\z",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    private static bool Equal(string a,string b)=>Normal(a)==Normal(b);
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        var relation=MotionExtendedPilot.Relations(c.Language).Single(r=>r.Id==c.SceneId.Split('/')[1]);
        var facts=d.Facts!;
        var novel=NovelFacts(relation.Id,vi);
        if(KnownWrongFact(relation.Id,facts,vi))return Wrong("A definite direction, starting order or river-speed frame contradicts this profile.");
        string? targetKind=KnownTarget(d.Question,vi);
        if(targetKind is not null&&targetKind!=relation.Id)return Wrong("A different motion quantity was requested.");
        bool FactProof(int index,string text)=>Equal(text,relation.Facts[index])||Equal(text,relation.AlternateFacts[index])
            ||novel[index].Length>0&&Matches(text,novel[index]);
        string denial=vi?"không phải ":"it is not true that ";
        for(int i=0;i<facts.Count;i++)
        {
            string text=Normal(facts[i].Text);
            if(text.StartsWith(denial,StringComparison.Ordinal)&&FactProof(i,text[denial.Length..]))
                return Wrong("A required extended-motion premise was explicitly denied.");
            foreach(string source in new[]{relation.Facts[i],relation.AlternateFacts[i]})
            foreach(var (before,after) in WrongSubstitutions(vi))
            {
                string changed=source.Replace(before,after,StringComparison.OrdinalIgnoreCase);
                if(changed!=source&&Equal(changed,facts[i].Text))return Wrong("An owner, direction, departure time or speed frame changed.");
            }
        }
        // Definite target replacements within this family are retryable, not an
        // unproved synonym. Inverse river relations intentionally share facts.
        foreach(var other in MotionExtendedPilot.Relations(c.Language).Where(r=>r.Id!=relation.Id))
            if(Equal(d.Question,other.Question)||Equal(d.SolutionLeads![0].Text,other.Lead))
            {
                // Downstream/upstream share no target; meeting/catch-up differ.
                if(other.Question!=relation.Question&&Equal(d.Question,other.Question)
                    ||other.Lead!=relation.Lead&&Equal(d.SolutionLeads![0].Text,other.Lead))return Wrong("The requested motion quantity changed.");
            }
        static bool Complete(string value,char ending)=>value.TrimEnd().EndsWith(ending)&&value.TrimEnd().Length>1
            &&".!?:".IndexOf(value.TrimEnd()[^2])<0;
        bool proved=facts.Select((f,i)=>Complete(f.Text,'.')&&FactProof(i,f.Text)).All(x=>x)
            &&Complete(d.Question,'?')&&Equal(d.Question,relation.Question)&&Equal(d.SolutionLeads![0].Text,relation.Lead);
        return proved?new(d,null,c):new(d,"SemanticReviewRequired",c,
            "Could not prove actor-speed ownership, initial order, simultaneous direction, constant speeds/no stops, riverbank/still-water frame and the requested quantity.");
    }
    private static BasicDraftValidation Wrong(string message)=>new(null,"ChangedRelationOrTarget",ErrorDetails:message);
    private static bool KnownWrongFact(string kind,IReadOnlyList<NarrativeClause> f,bool vi)
    {
        if(kind is "meeting" or "chasing")
        {
            string direction=kind=="meeting"
                ?vi?"hai người khởi hành cùng lúc và đi cùng chiều với vận tốc không đổi, không dừng lại":"both travellers start simultaneously and move in the same direction at constant speeds without stopping"
                :vi?"hai người khởi hành cùng lúc và đi về phía nhau với vận tốc không đổi, không dừng lại":"both travellers start simultaneously and move toward each other at constant speeds without stopping";
            if(Equal(f[3].Text,direction))return true;
            return kind=="chasing"&&Matches(f[0].Text,vi?@"ban đầu, \{v0\} ở sau \{v1\} một quãng đường \{f0\} \{v4\}":@"initially, \{v0\} is behind \{v1\} by \{f0\} \{v4\}");
        }
        if(kind is "boat" or "current")return Matches(f[0].Text,vi?
            @"so với nước, \{v0\} xuôi dòng với vận tốc \{f0\} \{v2\}|so với bờ, \{v0\} ngược dòng với vận tốc \{f0\} \{v2\}":
            @"relative to the water, \{v0\} moves downstream at \{f0\} \{v2\}|relative to the bank, \{v0\} moves upstream at \{f0\} \{v2\}");
        return Matches(f[0].Text,vi?@"so với bờ, \{v0\} di chuyển với vận tốc \{f0\} \{v2\}":@"relative to the bank, \{v0\} travels at \{f0\} \{v2\}");
    }
    private static string? KnownTarget(string question,bool vi)
    {
        (string Kind,string Pattern)[] targets=vi?
            [("downstream",@"vận tốc xuôi dòng(?: của thuyền)? so với bờ là bao nhiêu \{v2\}"),
             ("upstream",@"vận tốc ngược dòng(?: của thuyền)? so với bờ là bao nhiêu \{v2\}"),
             ("boat",@"vận tốc của thuyền trong nước yên là bao nhiêu \{v2\}"),
             ("current",@"vận tốc dòng nước(?: so với bờ)? là bao nhiêu \{v2\}")]
            :[("downstream",@"what is the downstream speed(?: of the craft)? relative to the (?:bank|riverbank) in \{v2\}"),
              ("upstream",@"what is the upstream speed(?: of the craft)? relative to the (?:bank|riverbank) in \{v2\}"),
              ("boat",@"what is the still-water speed of the craft in \{v2\}"),
              ("current",@"what is the current speed(?: relative to the (?:bank|riverbank))? in \{v2\}")];
        return targets.FirstOrDefault(t=>Matches(question,t.Pattern)).Kind;
    }
    private static IEnumerable<(string,string)> WrongSubstitutions(bool vi)=>vi?
        [("xuất phát cùng lúc","xuất phát khác lúc"),("bắt đầu đồng thời","bắt đầu khác lúc"),
         ("cùng chiều","ngược chiều"),("ngược chiều về phía nhau","cùng chiều ra xa nhau"),("tiến về phía nhau","đi ra xa nhau"),
         ("không dừng nghỉ","có dừng nghỉ"),("không nghỉ","có nghỉ"),("không đổi","thay đổi"),("giữ nguyên","thay đổi"),
         ("ở phía trước","ở phía sau"),("dẫn trước","ở phía sau"),("xuôi dòng","ngược dòng"),("ngược dòng","xuôi dòng"),
         ("so với bờ","so với nước"),("nước yên lặng","nước đang chảy")]
        :[("simultaneously","at different times"),("at the same time","at different times"),("towards each other","away from each other"),
          ("towards one another","away from one another"),("in the same direction","in opposite directions"),
          ("with no stops","with a stop"),("without stopping","with a stop"),("constant","variable"),("unchanged","changing"),
          ("ahead of","behind"),("with a lead of","with a delay of"),("downstream","upstream"),("upstream","downstream"),
          ("relative to the riverbank","relative to the water"),("still water","flowing water")];
    private static string[] NovelFacts(string kind,bool vi)
    {
        if(kind is "meeting" or "chasing")return ["",
            vi?@"vận tốc của \{v0\} luôn bằng \{f1\} \{v2\}":@"the speed maintained by \{v0\} is \{f1\} \{v2\}",
            vi?@"vận tốc của \{v1\} luôn bằng \{f2\} \{v2\}":@"the speed maintained by \{v1\} is \{f2\} \{v2\}",""];
        if(kind is "downstream" or "upstream")return [
            vi?@"trong nước yên, \{v0\} di chuyển với vận tốc \{f0\} \{v2\}":@"in still water, \{v0\} travels at \{f0\} \{v2\}",
            vi?@"dòng nước chảy với vận tốc \{f1\} \{v2\}":@"the current flows at \{f1\} \{v2\}",""];
        return [vi?@"so với bờ, \{v0\} xuôi dòng với vận tốc \{f0\} \{v2\}":@"relative to the bank, \{v0\} moves downstream at \{f0\} \{v2\}",
            vi?@"so với bờ, \{v0\} ngược dòng với vận tốc \{f1\} \{v2\}":@"relative to the bank, \{v0\} moves upstream at \{f1\} \{v2\}",""];
    }
}
