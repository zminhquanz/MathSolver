using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

/// <summary>Contrastive prose cases: clear inclusion/exclusion versus changed or unproved roles.</summary>
internal static class SameDayBreakMeaningTests
{
    internal static void Run()
    {
        int clearCount=0,unsafeCount=0;
        foreach(var (_,contract) in SameDayBreakTests.NativeCases())
        {
            var c=contract with {OpenProse=true};
            bool vi=c.Language==AppLanguage.Vietnamese;
            var reference=ReasoningStoryCatalogue.Draft(c);
            string[] starts=vi
                ?["{v1} của {v0} bắt đầu vào lúc {f0} giờ {f1} phút.","Vào lúc {f0} giờ {f1} phút, {v1} của {v0} khởi đầu.","{v1} của {v0} bắt đầu lúc {f0} giờ {f1} phút."]
                :["{v0} began the {v1} at {f0} hours {f1} minutes.","At {f0} hours and {f1} minutes, {v0}'s {v1} started.","The {v1} for {v0} begins at {f0} hours {f1} minutes."];
            string[] ends=vi
                ?["{v1} của {v0} kết thúc vào lúc {f2} giờ {f3} phút.","Lúc {f2} giờ {f3} phút, {v1} của {v0} hoàn tất.","{v1} của {v0} kết thúc lúc {f2} giờ {f3} phút."]
                :["{v0} concluded the {v1} at {f2} hours {f3} minutes.","{v0}'s {v1} concludes at {f2} hours and {f3} minutes.","The {v1} for {v0} finishes at {f2} hours {f3} minutes."];
            string[] pauses=vi
                ?["Trong khoảng thời gian {v1} của {v0} có một lần nghỉ dài {f4} phút.",
                  "Trong khoảng thời gian diễn ra {v1} của {v0}, chỉ có một lần nghỉ kéo dài {f4} phút.",
                  "Trong quá trình thực hiện {v1} của {v0} có một lần nghỉ dài {f4} phút."]
                :["There is only a single break of {f4} minutes within {v0}'s {v1}.",
                  "One break of {f4} minutes occurred while {v0} was engaged in the {v1}.",
                  "Exactly one pause of {f4} minutes occurs within the span of the {v1} for {v0}."];
            string[] days=vi
                ?["Cả hai mốc thời gian này đều xảy ra trong cùng một ngày.","Cả hai mốc thời gian đều nằm trong cùng một ngày.","Cả hai thời điểm diễn ra trong cùng một ngày."]
                :["The start and end times both fall on a single day.","The session remained within a single day.","The beginning and end times for this session occur on the same calendar day."];
            string[] questions=vi
                ?["Thời gian thực hiện {v1} của {v0} (không tính thời gian nghỉ) là bao nhiêu phút?",
                  "Thời gian thực hiện {v1} của {v0} không tính phần nghỉ là bao nhiêu phút?",
                  "Thời gian hoạt động thực tế của {v1} của {v0}, không kể lần nghỉ, là bao nhiêu phút?",
                  "Không tính thời gian nghỉ, {v1} của {v0} diễn ra bao nhiêu phút?"]
                :["Excluding the break, what is {v0}'s {v1} duration in minutes?",
                  "What is the duration of {v0}'s {v1} in minutes (excluding the break)?",
                  "Excluding the single break, what is the active duration of {v0}'s {v1} in minutes?",
                  "What is the active duration of the {v1} for {v0} in minutes, excluding the break?"];
            // Cross the constructions so proof cannot rely on one fixed example.
            for(int i=0;i<starts.Length;i++)
            for(int q=0;q<questions.Length;q++)
            {
                var d=reference with
                {
                    Facts=[new("fact_0",starts[i]),new("fact_1",ends[i]),new("fact_2",pauses[i]),new("fact_3",days[i])],
                    Question=questions[q],
                    SolutionLeads=[new("step_0",vi?(i==2?"Thời lượng hoạt động thực tế không kể thời gian giải lao":"Thời lượng hoạt động không nghỉ"):"Active minutes excluding the break")]
                };
                var proof=Validate(c,d);
                TestAssert.That(proof.IsValid,"Clear active-duration construction missed: "+proof);
                TestAssert.That(QuestionBankStore.SerializeDraft(proof.Draft!)==QuestionBankStore.SerializeDraft(d),"Meaning proof rewrote the candidate.");
                SameDayBreakTests.CheckPractice(c,d);clearCount++;

                var unsafeDrafts=new[]
                {
                    // Removing or reversing the exclusion must not auto-save.
                    d with {Question=d.Question.Replace(vi?"không tính":"excluding",vi?"bao gồm":"including",StringComparison.OrdinalIgnoreCase).Replace("không kể","bao gồm")},
                    d with {Question=d.Question.Replace(vi?"phút":"minutes",vi?"giờ":"hours")},
                    d with {Question=d.Question.Replace(vi?"phút":" in minutes","")},
                    // An article is required in English active voice; do not
                    // turn grammar repair into an unsupported relation proof.
                    d with {Facts=[new("fact_0",vi?"{v1} của {v0} không bắt đầu vào lúc {f0} giờ {f1} phút.":"{v0} began {v1} at {f0} hours {f1} minutes."),..d.Facts!.Skip(1)]},
                    d with {Facts=[new("fact_0",vi?"{v1} của {v0} có thể bắt đầu vào lúc {f0} giờ {f1} phút.":"{v0} might begin the {v1} at {f0} hours {f1} minutes."),..d.Facts!.Skip(1)]},
                    // The same complete clock clause with the opposite role.
                    d with {Facts=[new("fact_0",ends[i].Replace("{f2}","{f0}").Replace("{f3}","{f1}")),..d.Facts!.Skip(1)]},
                    d with {Facts=[d.Facts![0],new("fact_1",starts[i].Replace("{f0}","{f2}").Replace("{f1}","{f3}")),d.Facts[2],d.Facts[3]]},
                    d with {Facts=[d.Facts![0],d.Facts[1],new("fact_2",vi?"Sau khi {v1} của {v0} kết thúc, có một lần nghỉ dài {f4} phút.":"After {v0}'s {v1} ends, there is one break of {f4} minutes."),d.Facts[3]]},
                    d with {Facts=[d.Facts![0],d.Facts[1],new("fact_2",vi?"Trong khoảng thời gian {v1} của {v0} có hai lần nghỉ dài {f4} phút.":"There are two breaks of {f4} minutes within {v0}'s {v1}."),d.Facts[3]]},
                    d with {Facts=[d.Facts![0],d.Facts[1],new("fact_2",vi?"Trong khoảng thời gian {v1} của {v0} không có một lần nghỉ dài {f4} phút.":"There is not a single break of {f4} minutes within {v0}'s {v1}."),d.Facts[3]]},
                    d with {Facts=[d.Facts![0],d.Facts[1],d.Facts[2],new("fact_3",vi?"Cả hai mốc thời gian này không diễn ra trong cùng một ngày.":"The start and end times do not fall on a single day.")]},
                    d with {Facts=[d.Facts![0],d.Facts[1],d.Facts[2],new("fact_3",vi?"Cả hai mốc thời gian này không xảy ra trong cùng một ngày.":"The start and end times might fall on the same day.")]},
                    d with {Facts=[d.Facts![0],d.Facts[1],d.Facts[2],new("fact_3",vi?"Buổi hoạt động kết thúc ngày hôm sau.":"The session ends the next day.")]},
                    // Clear facts with a typo, ambiguous filler or a calculation
                    // instruction still need review rather than auto-use.
                    d with {Question=vi?"Thời gian thực n {v1} của {v0} không tính nghỉ là bao nhiêu phút?":"What is the recorded value of {v0}'s {v1} in minutes, excluding the break?"},
                    d with {SolutionLeads=[new("step_0",vi?"Tổng thời gian hoạt động trừ thời gian nghỉ":"Subtract the break from total time")]},
                    d with {Facts=[d.Facts![0],d.Facts[1],new("fact_2",vi?"Trong khoảng thời gian {v1} của {v0} có một lần nghỉ dài {f4} phút vậy đâu nhé.":"There is only a single break of {f4} minutes within {v0}'s {v1}, perhaps."),d.Facts[3]]}
                };
                foreach(var bad in unsafeDrafts)
                {
                    TestAssert.That(!Validate(c,bad).IsValid,"Changed/unproved active-duration meaning auto-saved: "+QuestionBankStore.SerializeDraft(bad));unsafeCount++;
                }
            }
            var unknown=reference with {Question=vi?"Thời gian thực hiện {v1} của {v0} là bao lâu?":"What is the duration of {v0}'s {v1} when the break is taken out?"};
            TestAssert.That(Validate(c,unknown).NeedsReview,"Missing explicit minutes must review once, not retry.");
            string prompt=BasicQuestionPrompt.Build(c);
            TestAssert.That(prompt.Contains(vi?"phải có chữ phút":"MUST explicitly say minutes")&&prompt.Contains("fact_3"),"Prompt lost minute target or same-day field responsibility.");
        }
        Console.WriteLine($"PASS one-break prose strengthening: {clearCount} clear complete drafts, {unsafeCount} contrastive unsafe drafts; explicit minutes, parenthetical exclusion, clock ownership, no rewrite.");
    }
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)
        =>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
}
