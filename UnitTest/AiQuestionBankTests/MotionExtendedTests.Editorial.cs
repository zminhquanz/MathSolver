using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;

internal static partial class MotionExtendedTests
{
    private static int CheckEditorial()
    {
        int checks=0;
        void Check(bool value,string message){Require(value,message);checks++;}
        foreach(string relation in new[]{"meeting","chasing"})
        foreach(string context in new[]{"cyclists","cars"})
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            var c=Create(relation,context,AppLanguage.English,tier,913);
            var d=ReasoningStoryCatalogue.Draft(c) with {SolutionLeads=[new("step_0",relation=="meeting"?"Meeting duration":"Duration until catch-up")]};
            Check(Validate(c,d).IsValid,"A proved road duration heading should remain compatible with practice grading.");
            CheckPractice(c,d);
        }
        BasicQuestionDraft Inverse(BasicQuestionContract c)
        {
            var d=ReasoningStoryCatalogue.Draft(c);bool vi=c.Language==AppLanguage.Vietnamese;
            bool boat=c.SceneId.Contains("/boat/",StringComparison.Ordinal);
            return d with {
                Facts=vi?[
                    new("fact_0","Vận tốc của {v0} khi đi xuôi dòng so với bờ là {f0} {v2}."),
                    new("fact_1","Vận tốc của {v0} khi đi ngược dòng so với bờ là {f1} {v2}."),
                    new("fact_2","Cả hai vận tốc này đều được đo trong điều kiện vận tốc của phương tiện so với nước là không đổi và vận tốc dòng nước là hằng số.")]:[
                    new("fact_0","The downstream speed of {v0} relative to "+(boat?"the ":"")+"riverbank is {f0} {v2}."),
                    new("fact_1","The upstream speed of {v0} relative to "+(boat?"the ":"")+"riverbank is {f1} {v2}."),
                    new("fact_2",boat?"This same craft maintains a constant speed in still water, and the current speed remains uniform throughout both journeys.":"The same craft maintains a constant speed in still water, and the river current speed is consistent for both measurements.")],
                Question=vi?"Vận tốc dòng nước so với bờ là bao nhiêu {v2}?":boat?"Determine the craft's speed in still water measured in {v2}?":"Determine the river current speed relative to riverbank in {v2}?",
                SolutionLeads=[new("step_0",vi?"Vận tốc dòng nước so với bờ":boat?"Craft speed in still water":"River current speed relative to riverbank")]
            };
        }
        // Three manually clear native drafts, reconstructed from raw evidence,
        // tested across fresh facts, contexts and tiers; not read from an artifact.
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(string relation in language==AppLanguage.Vietnamese?new[]{"current"}:new[]{"boat","current"})
        foreach(string context in new[]{"canoe","motorboat"})
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            var c=Create(relation,context,language,tier,913);var d=Inverse(c);
            var result=Validate(c,d);
            Check(result.IsValid,"Previously clear river draft still unproved: "+c.SceneId+"/"+language+": "+result.ErrorCode);
            CheckPractice(c,d);
            bool vi=language==AppLanguage.Vietnamese;
            void Unsafe(BasicQuestionDraft bad,string label,bool? review=null)
            {
                var r=Validate(c,bad);Check(!r.IsValid,"Editorial contrast promoted: "+label);
                if(review.HasValue)Check(r.NeedsReview==review.Value,"Wrong editorial disposition for "+label+": "+r.ErrorCode);
            }
            for(int index=0;index<3;index++)
            foreach(string prefix in vi?new[]{"Không phải ","Nếu ","Chỉ khi "}:new[]{"It is not true that ","If ","Only if "})
            {
                var facts=d.Facts!.ToArray();facts[index]=facts[index] with {Text=prefix+facts[index].Text};
                Unsafe(d with {Facts=facts},"negation/conditional",prefix!=(vi?"Không phải ":"It is not true that "));
            }
            foreach(var (from,to) in vi?new[]{("xuôi dòng","ngược dòng"),("ngược dòng","xuôi dòng"),("so với bờ","so với nước"),("không đổi","thay đổi"),("hằng số","thay đổi")}
                :new[]{("downstream","upstream"),("upstream","downstream"),("riverbank","water"),("constant","variable"),("uniform","variable"),("consistent","variable")})
            {
                for(int i=0;i<3;i++)
                {
                    var facts=d.Facts!.ToArray();string changed=facts[i].Text.Replace(from,to,StringComparison.Ordinal);
                    if(changed==facts[i].Text)continue;facts[i]=facts[i] with {Text=changed};
                    Unsafe(d with {Facts=facts},"wrong frame/direction/invariant",false);
                }
            }
            var missing=d.Facts!.ToArray();missing[2]=missing[2] with {Text=vi?"Cả hai lần sử dụng cùng một phương tiện và vận tốc dòng nước không đổi.":"The same craft is used and the current remains constant."};
            Unsafe(d with {Facts=missing},"still-water invariant absent",true);
            var other=Inverse(Create(relation=="boat"?"current":"boat",context,language,tier,913));
            // Vietnamese fixture intentionally only covers the current target.
            if(!vi){Unsafe(d with {Question=other.Question},"wrong target",false);Unsafe(d with {SolutionLeads=other.SolutionLeads},"wrong heading",false);}
            Unsafe(d with {Question=(vi?"Nếu ":"If ")+d.Question},"conditional question",true);
            Unsafe(d with {SolutionLeads=[new("step_0",(vi?"Không phải ":"Not ")+d.SolutionLeads![0].Text)]},"negated heading");
            Unsafe(d with {Facts=d.Facts!.Select(f=>f with {Text=f.Text+(vi?" Sau đó đổi vận tốc.":" Then the speed changes.")}).ToArray()},"added event");
        }
        // Cover the forward-river forms as complete prose too, independently
        // from inverse-river replay and the original canonical fixtures.
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(string relation in new[]{"downstream","upstream"})
        foreach(string context in new[]{"canoe","motorboat"})
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            bool vi=language==AppLanguage.Vietnamese;var c=Create(relation,context,language,tier,415);
            string direction=relation=="downstream"?vi?"xuôi dòng":"downstream":vi?"ngược dòng":"upstream";
            var d=ReasoningStoryCatalogue.Draft(c) with {
                Facts=vi?[
                    new("fact_0","Tốc độ của {v0} khi không có dòng nước là {f0} {v2}."),
                    new("fact_1","Tốc độ của dòng nước so với bờ được xác định là {f1} {v2}."),
                    new("fact_2","Phương tiện đi "+direction+", vận tốc khi nước yên lặng và vận tốc dòng nước đều không đổi.")]:[
                    new("fact_0","The speed of {v0} when the water is still amounts to {f0} {v2}."),
                    new("fact_1","The river's current moves at a rate of {f1} {v2} relative to the bank."),
                    new("fact_2","The craft travels "+direction+", maintaining steady speeds for both its movement through still water and the river's flow.")],
                Question=vi?"Vận tốc "+direction+" so với bờ bằng bao nhiêu {v2}?":"What is the "+direction+" speed of the craft relative to the bank measured in {v2}?",
                SolutionLeads=[new("step_0",vi?"Vận tốc "+direction+" so với bờ":(relation=="downstream"?"Downstream":"Upstream")+" speed relative to bank")]
            };
            Check(Validate(c,d).IsValid,"Clear direct river clauses not proved: "+c.SceneId+"/"+language);
            CheckPractice(c,d);
            for(int i=0;i<3;i++)
            foreach(string prefix in vi?new[]{"Không phải ","Nếu ","Chỉ khi "}:new[]{"It is not true that ","If ","Only if "})
            {
                var facts=d.Facts!.ToArray();facts[i]=facts[i] with {Text=prefix+facts[i].Text};var bad=Validate(c,d with {Facts=facts});
                Check(!bad.IsValid,"Direct river conditional/negation promoted.");
                Check(bad.NeedsReview==(prefix!=(vi?"Không phải ":"It is not true that ")),"Direct river conditional/negation disposition changed.");
            }
            foreach(var (from,to) in vi?new[]{("không có dòng nước","nước đang chảy"),("so với bờ","so với nước"),("không đổi","thay đổi"),(direction,relation=="downstream"?"ngược dòng":"xuôi dòng")}
                :new[]{("when the water is still","relative to the riverbank"),("relative to the bank","relative to the water"),("steady","variable"),(direction,relation=="downstream"?"upstream":"downstream")})
            for(int i=0;i<3;i++)
            {
                var facts=d.Facts!.ToArray();string changed=facts[i].Text.Replace(from,to,StringComparison.Ordinal);
                if(changed==facts[i].Text)continue;facts[i]=facts[i] with {Text=changed};
                Check(!Validate(c,d with {Facts=facts}).IsValid,"Direct river frame/invariant/direction substitution promoted.");
            }
            Check(!Validate(c,d with {Question=(vi?"Nếu ":"If ")+d.Question}).IsValid,"Direct river conditional target promoted.");
            Check(!Validate(c,d with {SolutionLeads=[new("step_0",(vi?"Không phải ":"Not ")+d.SolutionLeads![0].Text)]}).IsValid,"Direct river negated heading promoted.");
        }
        foreach(var (name,c) in NativeCases())
        {
            bool vi=c.Language==AppLanguage.Vietnamese;bool road=c.SceneId.Contains("/meeting/")||c.SceneId.Contains("/chasing/");
            var d=ReasoningStoryCatalogue.Draft(c);string unit=road?"{v3}":"{v2}";
            var bad=d with {Question=vi?$"Vận tốc của {unit} là bao nhiêu?":$"What is the speed of {unit} relative to the riverbank?"};
            Check(!Validate(c,bad).NeedsReview&&!Validate(c,bad).IsValid,"A unit used as the speed subject must be a definite error: "+name);
            string prompt=BasicQuestionPrompt.Build(c),grammar=OpenQuestionProse.Grammar(c);
            Check(prompt.Contains(vi?"không phải tên phương tiện":"not a craft or a person",StringComparison.Ordinal),"Prompt does not distinguish units from actors.");
            Check(prompt.Contains(vi?"không viết hướng dẫn":"do not write calculation instructions",StringComparison.Ordinal),"Prompt does not rule out technical guidance inside facts.");
            if(!road)Check(!MotionExtendedCatalogue.ContextHint(c).Contains("{v1}",StringComparison.Ordinal),"River hint contains an absent actor slot.");
            string questionRule=grammar.Split('\n').Single(x=>x.StartsWith("field-"+(road?4:3)+" ::=",StringComparison.Ordinal));
            Check(!questionRule.Contains(System.Text.Json.JsonSerializer.Serialize(unit),StringComparison.Ordinal),"Grammar emits a bare unit block at arbitrary question position.");
            Check(questionRule.Contains(System.Text.Json.JsonSerializer.Serialize(vi?"bao nhiêu "+unit:"in "+unit),StringComparison.Ordinal),"Grammar lost the atomic measure phrase.");
            var tech=d.Facts!.ToArray();tech[^1]=tech[^1] with {Text=vi?"Kết quả cần được đo so với bờ và được tính dựa trên các dữ kiện đã cho.":"The result must be calculated in terms of the given speeds and units."};
            var technical=Validate(c,d with {Facts=tech});
            Check(!technical.IsValid&&!technical.NeedsReview&&technical.ErrorCode=="InvalidText","Technical instructions in facts must be recognized as invalid text.");
        }
        Console.WriteLine($"PASS {checks} motion-extended editorial assertions; three old clear drafts, 70 river context/tier fixtures, unit/technical regressions and hostile variants.");
        return checks;
    }
}
