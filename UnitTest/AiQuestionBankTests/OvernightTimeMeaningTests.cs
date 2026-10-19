using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static class OvernightTimeMeaningTests
{
    internal static void Run()
    {
        int clearCount=0,reviewCount=0,wrongCount=0;
        foreach(var (_,contract) in OvernightTimeTests.NativeCases())
        {
            var c=contract with {OpenProse=true};
            bool vi=c.Language==AppLanguage.Vietnamese;
            var draft=ReasoningStoryCatalogue.Draft(c);
            string[] conditions=vi?["Toàn bộ quá trình diễn ra không hề gián đoạn.",
                "Hoạt động này được thực hiện liên tục không hề dừng lại.","Hoạt động này được thực hiện không gián đoạn."]:
                ["The activity proceeds without interruption.","The activity proceeds without any interruptions.","This activity proceeds without interruption."];
            foreach(string condition in conditions)
            {
                var clear=draft with {Facts=[draft.Facts![0],draft.Facts[1],new("fact_2",condition)]};
                var proof=Validate(c,clear);
                Check(proof.IsValid,"Clear uninterrupted activity missed: "+proof);
                Check(QuestionBankStore.SerializeDraft(proof.Draft!)==QuestionBankStore.SerializeDraft(clear),"Meaning proof rewrote prose.");
                OvernightTimeTests.CheckPractice(c,clear);clearCount++;
                if(!vi)
                {
                    var measured=clear with {Facts=[new("fact_0","{v0}'s {v1} commences at {f0} hours {f1} minutes."),clear.Facts![1],clear.Facts[2]],
                        Question="What is the duration of {v0}'s {v1}, measured in minutes?"};
                    Check(Validate(c,measured).IsValid,"Clear commences/measured-in-minutes draft missed.");clearCount++;
                    foreach(var unclear in new[]{
                        measured with {Facts=[new("fact_0","{v0}'s {v1} does not commence at {f0} hours {f1} minutes."),measured.Facts![1],measured.Facts[2]]},
                        measured with {Question="What is the duration of {v0}'s {v1}, not measured in minutes?"},
                        measured with {Facts=[measured.Facts![0],measured.Facts[1],new("fact_2","This activity does not proceed without interruption.")]}})
                    {Check(Validate(c,unclear).NeedsReview,"Negated new time wording auto-approved.");reviewCount++;}
                }

                string[] uncertain=vi?[
                    "Toàn bộ quá trình không diễn ra không hề gián đoạn.",
                    "Toàn bộ quá trình có thể diễn ra không hề gián đoạn.",
                    "Nếu toàn bộ quá trình diễn ra không hề gián đoạn.",
                    "Không thể khẳng định toàn bộ quá trình diễn ra không hề gián đoạn.",
                    "Toàn bộ quá trình diễn ra không hề gián đoạn, có lẽ vậy.",
                    "Một phần quá trình diễn ra không hề gián đoạn.",
                    "Quá trình của người khác diễn ra không hề gián đoạn.",
                    "Toàn bộ quá trình diễn ra không hề gián đoạn sau khi nghỉ."]:[
                    "The activity does not proceed without interruption.",
                    "The activity never proceeds without interruption.",
                    "The activity might proceed without interruption.",
                    "If the activity proceeds without interruption.",
                    "It is not known whether the activity proceeds without interruption.",
                    "The activity proceeds without interruption, perhaps.",
                    "Part of the activity proceeds without interruption.",
                    "Another activity proceeds without interruption.",
                    "The activity proceeds without interruption after a break."];
                foreach(string text in uncertain)
                {
                    var result=Validate(c,clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",text)]});
                    Check(result.NeedsReview,"Negation, hypothesis, scope or qualification auto-approved: "+text);reviewCount++;
                }
                foreach(var wrong in new[]{
                    clear with {Facts=[clear.Facts![0],new("fact_1",vi?"{v1} của {v0} kết thúc lúc {f2} giờ {f3} phút cùng ngày.":"{v0}'s {v1} ends at {f2} hours {f3} minutes on the same day."),clear.Facts[2]]},
                    clear with {Facts=[new("fact_0",vi?"{v1} của {v0} kết thúc lúc {f0} giờ {f1} phút.":"{v0}'s {v1} ends at {f0} hours {f1} minutes."),clear.Facts![1],clear.Facts[2]]},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?"Hoạt động này có một lần nghỉ.":"The activity has a break.")]},
                    clear with {Question=vi?"{v1} của {v0} bắt đầu lúc mấy giờ?":"What time does {v0}'s {v1} start?"},
                    clear with {Question=vi?"{v1} của {v0} kéo dài bao nhiêu giờ?":"How many hours does {v0}'s {v1} last?"}})
                {
                    var result=Validate(c,wrong);
                    Check(!result.IsValid&&!result.NeedsReview,"New continuity phrase hid a definite wrong relation.");wrongCount++;
                }
                foreach(var unknown in new[]{
                    clear with {SolutionLeads=[new("step_0",vi?"Tính tổng thời gian hoạt động":"Calculate the elapsed duration")]},
                    clear with {Question=vi?"{v1} của {v0} kéo dài bao lâu?":"What is the total duration in {v0}'s {v1} minutes?"}})
                {Check(Validate(c,unknown).NeedsReview,"Imperative lead or unclear minute target auto-approved.");reviewCount++;}
            }
            if(vi)
            {
                foreach(string condition in new[]{"Hoạt động này không được thực hiện không gián đoạn.",
                    "Hoạt động này có thể được thực hiện liên tục không hề dừng lại.",
                    "Hoạt động này được thực hiện không gián đoạn sau khi nghỉ."})
                {Check(Validate(c,draft with {Facts=[draft.Facts![0],draft.Facts[1],new("fact_2",condition)]}).NeedsReview,"Negated/hypothetical scoped continuity auto-approved.");reviewCount++;}
            }
            if(!vi&&c.SceneId.EndsWith("/watch",StringComparison.Ordinal))
            {
                Check(Validate(c,draft with {Facts=[draft.Facts![0],draft.Facts[1],new("fact_2","The journey proceeds without interruption.")]}).NeedsReview,
                    "Continuity of a journey proved continuity of a night watch.");reviewCount++;
            }
        }
        Console.WriteLine($"PASS overnight continuity strengthening: {clearCount} clear drafts, {reviewCount} review-only contrasts, {wrongCount} definite wrong-role/day/target mutations.");
    }
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static void Check(bool value,string message)=>TestAssert.That(value,message);
}
