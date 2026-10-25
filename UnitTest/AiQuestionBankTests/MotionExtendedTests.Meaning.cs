using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;

internal static partial class MotionExtendedTests
{
    private static BasicQuestionDraft Novel(BasicQuestionDraft d,string relation,bool vi)
    {
        var facts=d.Facts!.ToArray();
        if(relation is "meeting" or "chasing")
        {
            facts[1]=facts[1] with {Text=vi?"Vận tốc của {v0} luôn bằng {f1} {v2}.":"The speed maintained by {v0} is {f1} {v2}."};
            facts[2]=facts[2] with {Text=vi?"Vận tốc của {v1} luôn bằng {f2} {v2}.":"The speed maintained by {v1} is {f2} {v2}."};
        }
        else if(relation is "downstream" or "upstream")
        {
            facts[0]=facts[0] with {Text=vi?"Trong nước yên, {v0} di chuyển với vận tốc {f0} {v2}.":"In still water, {v0} travels at {f0} {v2}."};
            facts[1]=facts[1] with {Text=vi?"Dòng nước chảy với vận tốc {f1} {v2}.":"The current flows at {f1} {v2}."};
        }
        else
        {
            facts[0]=facts[0] with {Text=vi?"So với bờ, {v0} xuôi dòng với vận tốc {f0} {v2}.":"Relative to the bank, {v0} moves downstream at {f0} {v2}."};
            facts[1]=facts[1] with {Text=vi?"So với bờ, {v0} ngược dòng với vận tốc {f1} {v2}.":"Relative to the bank, {v0} moves upstream at {f1} {v2}."};
        }
        return d with {Facts=facts};
    }

    private static int CheckSemantics(BasicQuestionContract c,BasicQuestionDraft d,BasicQuestionDraft novel)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;string relation=c.SceneId.Split('/')[1];int count=0;
        void Reject(BasicQuestionDraft bad,string label,bool? review=null)
        {
            var result=Validate(c,bad);Require(!result.IsValid,$"Unsafe extended motion {label} proved: {c.SceneId}/{c.Language}");
            if(review.HasValue)Require(result.NeedsReview==review.Value,$"Wrong extended motion {label} disposition: {result.ErrorCode}/{result.ErrorDetails}");
            count++;
        }
        foreach(var source in new[]{d,novel})
        foreach(string prefix in vi?new[]{"Không phải ","Nếu ","Chỉ khi "}:new[]{"It is not true that ","If ","Only if "})
        for(int field=0;field<source.Facts!.Count;field++)
        {
            var facts=source.Facts.ToArray();facts[field]=facts[field] with {Text=prefix+facts[field].Text};
            Reject(source with {Facts=facts},"negated/conditional fact",prefix!=(vi?"Không phải ":"It is not true that "));
        }
        foreach(string prefix in vi?new[]{"Không phải ","Nếu "}:new[]{"It is not true that ","If "})
            Reject(novel with {Question=prefix+novel.Question},"negated/conditional target");
        Reject(d with {Question=d.Question+(vi?" Theo cách mới.":" Under a new interpretation.")},"unknown target tail",true);
        Reject(d with {Facts=d.Facts!.Select(f=>f with {Text=f.Text.TrimEnd('.')}).ToArray(),Question=d.Question.TrimEnd('?')},"unpunctuated prose",true);
        Reject(d with {SolutionLeads=[new("step_0",vi?"Một kết quả được suy đoán":"A conjectured result")]},"unknown solution heading",true);
        var allSlots=d.Facts!.Select(f=>f with {Text=f.Text.Replace("{f0}","{temp}").Replace("{f1}","{f0}").Replace("{temp}","{f1}")}).ToArray();
        Reject(d with {Facts=allSlots},"swapped numeric roles",false);
        Reject(d with {Question=d.Question.Replace("{v3}","{v2}").Replace("{v2}","{v4}")},"wrong target measurement unit",false);
        if(relation is "meeting" or "chasing")
        {
            int invariant=3;var facts=d.Facts!.ToArray();
            string wrongDirection=relation=="meeting"
                ?vi?"Hai người khởi hành cùng lúc và đi cùng chiều với vận tốc không đổi, không dừng lại.":"Both travellers start simultaneously and move in the same direction at constant speeds without stopping."
                :vi?"Hai người khởi hành cùng lúc và đi về phía nhau với vận tốc không đổi, không dừng lại.":"Both travellers start simultaneously and move toward each other at constant speeds without stopping.";
            facts[invariant]=facts[invariant] with {Text=wrongDirection};Reject(d with {Facts=facts},"opposite road direction",false);
            foreach(string invariantText in vi?new[]{
                "Hai người khởi hành khác lúc, đi với vận tốc không đổi và không dừng lại.",
                "Hai người khởi hành cùng lúc nhưng dừng lại giữa đường.",
                "Hai người khởi hành cùng lúc, vận tốc thay đổi và không dừng lại.",
                "Hai người khởi hành cùng lúc, đi đến nơi rồi quay trở lại."}:new[]{
                "The second traveller starts later, with constant speeds and no stops.",
                "Both travellers start simultaneously but stop along the way.",
                "Both travellers start simultaneously with variable speeds and no stops.",
                "Both travellers start simultaneously, reach the destination and then return."})
            {facts=d.Facts!.ToArray();facts[invariant]=facts[invariant] with {Text=invariantText};Reject(d with {Facts=facts},"changed timing/stops/constant speed/extra leg");}
            facts=d.Facts!.ToArray();
            facts[1]=facts[1] with {Text=facts[1].Text.Replace("{v0}","{v1}")};
            facts[2]=facts[2] with {Text=facts[2].Text.Replace("{v1}","{v0}")};
            Reject(d with {Facts=facts},"speed owners swapped with unchanged meeting sum",false);
            if(relation=="chasing")
            {
                facts=d.Facts!.ToArray();facts[0]=facts[0] with {Text=vi?"Ban đầu, {v0} ở sau {v1} một quãng đường {f0} {v4}.":"Initially, {v0} is behind {v1} by {f0} {v4}."};
                Reject(d with {Facts=facts},"slower actor behind the faster actor",false);
            }
            Reject(d with {Question=vi?"Quãng đường đi được là bao nhiêu {v3}?":"What distance is covered in {v3}?"},"distance target using time unit");
        }
        else
        {
            var facts=d.Facts!.ToArray();
            if(relation is "boat" or "current")
            {
                facts[0]=facts[0] with {Text=vi?"So với nước, {v0} xuôi dòng với vận tốc {f0} {v2}.":"Relative to the water, {v0} moves downstream at {f0} {v2}."};
                Reject(d with {Facts=facts},"water frame replacing bank frame",false);
                facts=d.Facts!.ToArray();facts[0]=facts[0] with {Text=novel.Facts![0].Text.Replace(vi?"xuôi dòng":"downstream",vi?"ngược dòng":"upstream")};
                Reject(d with {Facts=facts},"downstream observation changed to upstream",false);
            }
            else
            {
                facts[0]=facts[0] with {Text=vi?"So với bờ, {v0} di chuyển với vận tốc {f0} {v2}.":"Relative to the bank, {v0} travels at {f0} {v2}."};
                Reject(d with {Facts=facts},"bank speed replacing still-water speed",false);
                facts=d.Facts!.ToArray();facts[1]=facts[1] with {Text=vi?"Vận tốc của {v0} là {f1} {v2}.":"The speed of {v0} is {f1} {v2}."};
                Reject(d with {Facts=facts},"craft owns current speed",false);
            }
            string target=relation switch {
                "downstream"=>vi?"Vận tốc ngược dòng của thuyền so với bờ là bao nhiêu {v2}?":"What is the upstream speed of the craft relative to the bank in {v2}?",
                "upstream"=>vi?"Vận tốc xuôi dòng của thuyền so với bờ là bao nhiêu {v2}?":"What is the downstream speed of the craft relative to the bank in {v2}?",
                "boat"=>vi?"Vận tốc dòng nước là bao nhiêu {v2}?":"What is the current speed in {v2}?",
                _=>vi?"Vận tốc của thuyền trong nước yên là bao nhiêu {v2}?":"What is the still-water speed of the craft in {v2}?"};
            Reject(d with {Question=target},"other river target",false);
            facts=d.Facts!.ToArray();facts[2]=facts[2] with {Text=facts[2].Text+(vi?" Sau đó vận tốc dòng nước thay đổi.":" Then the current speed changes.")};
            Reject(d with {Facts=facts},"variable river current");
        }
        return count;
    }

    private static void CheckContent()
    {
        foreach(var language in Enum.GetValues<AppLanguage>())
        for(int mutation=0;mutation<8;mutation++)
        {
            var contexts=System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(MotionExtendedPilot.Contexts(language)))!.AsArray();
            var relations=System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(MotionExtendedPilot.Relations(language)))!.AsArray();
            switch(mutation)
            {
                case 0:contexts[0]!["First"]="";break;
                case 1:contexts[0]!["MinimumSpeed"]=-1;break;
                case 2:contexts[1]!["Id"]=contexts[0]!["Id"]!.GetValue<string>();break;
                case 3:contexts[0]!["Group"]="river";break;
                case 4:relations[0]!["Kind"]=(int)MotionQuestionKind.BasicSpeed;break;
                case 5:relations[0]!["Question"]="{f0}";break;
                case 6:relations[0]!["Facts"]![0]="{f0}";break;
                case 7:relations[0]!["Lead"]="{f0}";break;
            }
            bool rejected=false;
            try {QuizContentCatalog.Validate(new(){Version=1,Culture=QuizContentCatalog.Culture(language),Lists=new(){
                ["MotionExtended.Contexts"]=System.Text.Json.JsonSerializer.SerializeToElement(contexts),
                ["MotionExtended.Relations"]=System.Text.Json.JsonSerializer.SerializeToElement(relations)}});}
            catch(InvalidDataException){rejected=true;}
            Require(rejected,"Malformed motion extended content passed: "+mutation);
        }
    }
}
