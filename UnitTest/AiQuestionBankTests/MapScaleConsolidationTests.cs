using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static class MapScaleConsolidationTests
{
    internal static Task RunAsync()
    {
        int count=0,contrasts=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(string profile in MapScaleOpenPilot.Profiles(language))
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            bool vi=language==AppLanguage.Vietnamese;
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Measurement,MapScaleOpenPilot.Variant,tier,language,new(731),profile) with {OpenProse=true};
            var reference=ReasoningStoryCatalogue.Draft(c);
            var clear=reference with
            {
                Facts=[new("fact_0",vi?"Trên {v0}, tỉ lệ được ghi là một trên {f0}.":"The scale shown on {v0} is one to {f0}."),
                    new("fact_1",vi?"Độ dài của {v1} được đo trên {v0} là {f1} {v2}.":"{v1} measures {f1} {v2} on {v0}.")],
                Question=vi?"Hỏi {v1} có độ dài thực tế là bao nhiêu {v3}?":"What distance does {v1} represent in reality, measured in {v3}?",
                SolutionLeads=[new("step_0",vi?"Chiều dài thực tế của {v1}":"The actual distance represented by {v1}"),
                    new("step_1",vi?"Độ dài ngoài thực tế của {v1}":"The real-world length of {v1}")]
            };
            var proof=Validate(c,clear);
            Check(proof.IsValid,"Explicit map scale/segment/actual-distance clauses and unit-free headings need proof: "+proof.ErrorCode+"; "+proof.ErrorDetails);
            var fresh=clear with
            {
                Facts=[new("fact_0",vi?"Tỉ lệ của {v0} là một trên {f0}.":"{v0} shows a scale of one to {f0}."),
                    new("fact_1",vi?"{v1} đo trên {v0} có độ dài {f1} {v2}.":"The length of {v1} drawn on {v0} is {f1} {v2}.")],
                Question=vi?"Độ dài thực tế của {v1} là bao nhiêu {v3}?":"How long is the actual distance represented by {v1} in {v3}?"
            };
            Check(Validate(c,fresh).IsValid,"Fresh pilot's explicit map-measurement and actual-distance question must verify: "+Validate(c,fresh));
            MapScaleOpenTests.CheckPractice(c,fresh);count++;
            foreach(string prefix in vi?new[]{"Không phải ","Nếu "}:new[]{"It is not true that ","If "})
            {Check(Validate(c,fresh with {Facts=[fresh.Facts[0],fresh.Facts[1] with {Text=prefix+fresh.Facts[1].Text}]}).NeedsReview,"Fresh map clause with qualification must remain reviewed.");contrasts++;}
            Check(!Validate(c,fresh with {Facts=[fresh.Facts[0],fresh.Facts[1] with {Text=fresh.Facts[1].Text.Replace("{v2}","{v3}",StringComparison.Ordinal)}]}).IsValid,"Fresh map clause cannot swap unit roles.");
            Check(!Validate(c,fresh with {Question=vi?"Độ dài của {v1} trên bản đồ là bao nhiêu {v3}?":"How long is {v1} on the map in {v3}?"}).IsValid,"Fresh question cannot reverse the target.");
            contrasts+=2;
            Check(QuestionBankStore.SerializeDraft(proof.Draft!)==QuestionBankStore.SerializeDraft(clear),"Map validation must preserve the authored draft.");
            MapScaleOpenTests.CheckPractice(c,clear);count++;
            foreach(var mutation in new[]
            {
                clear with {Facts=[clear.Facts[0] with {Text=vi?"Trên {v0}, tỉ lệ được ghi là {f0} trên một.":"The scale shown on {v0} is {f0} to one."},clear.Facts[1]]},
                clear with {Facts=[clear.Facts[0],clear.Facts[1] with {Text=clear.Facts[1].Text.Replace("{v2}","{v3}",StringComparison.Ordinal)}]},
                clear with {Question=vi?"Hỏi độ dài của {v1} trên bản đồ là bao nhiêu {v3}?":"What is the map length of {v1} in {v3}?"},
                clear with {Question=clear.Question.Replace("{v3}","{v2}",StringComparison.Ordinal)},
                clear with {Question=vi?"Diện tích thực tế của {v1} là bao nhiêu {v3}?":"What is the actual area of {v1} in {v3}?"},
                clear with {SolutionLeads=[clear.SolutionLeads[0] with {Text=clear.SolutionLeads[0].Text+" cm"},clear.SolutionLeads[1]]},
                clear with {SolutionLeads=[clear.SolutionLeads[0],clear.SolutionLeads[1] with {Text=clear.SolutionLeads[1].Text+" metres"}]}
            })
            {var result=Validate(c,mutation);Check(!result.IsValid&&!result.NeedsReview,"Wrong map direction/target/template units must still reject: "+result);contrasts++;}
            foreach(string prefix in vi?new[]{"Không phải ","Nếu ","Theo ghi chép, "}:new[]{"It is not true that ","If ","According to the record, "})
            {
                var result=Validate(c,clear with {Facts=[clear.Facts[0],clear.Facts[1] with {Text=prefix+clear.Facts[1].Text}]});
                Check(!result.IsValid&&result.NeedsReview,"Qualified/unknown map meaning must wait for review: "+result);contrasts++;
            }
            foreach(string instruction in vi?new[]{"Hãy nhân số đo của {v1}","{v1} là bao nhiêu"}:new[]{"Multiply the measurement of {v1}","What is the length of {v1}"})
            {Check(!Validate(c,clear with {SolutionLeads=[new("step_0",instruction),clear.SolutionLeads[1]]}).IsValid,"Instruction/question is not a verified noun heading.");contrasts++;}
        }
        Console.WriteLine($"PASS map consolidation: {count} new complete clauses, {contrasts} direction/target/unit/negation/editorial contrasts; bilingual/five tiers/fresh grading.");
        return Task.CompletedTask;
    }
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static void Check(bool condition,string message)=>TestAssert.That(condition,message);
}
