using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static class SameMonthCalendarMeaningTests
{
    internal static void Run()
    {
        SameMonthCalendarReviewTests.Run();
        CheckPriorClearEnglish();
        int clearCount=0,reviewCount=0,wrongCount=0;
        foreach(var (_,seed) in SameMonthCalendarTests.NativeCases())
        {
            var c=seed with {OpenProse=true};bool vi=c.Language==AppLanguage.Vietnamese;
            var reference=ReasoningStoryCatalogue.Draft(c);
            string[] rules=vi?[
                "Thời gian này không tính ngày bắt đầu mà tính cả ngày kết thúc.",
                "Thời gian tính không bao gồm ngày bắt đầu, nhưng có tính ngày kết thúc.",
                "Trong khoảng thời gian này, ta không tính ngày bắt đầu mà tính cả ngày kết thúc.",
                "Đếm tất cả các ngày từ ngày liền sau ngày bắt đầu đến hết ngày kết thúc."]:[
                "The start date is not counted, but the end date is counted.",
                "Count all calendar days from the day after the start date through the end date, inclusive.",
                "Count every calendar day from the day immediately after the start date up to and including the end date.",
                "Do not count the start date, but count the end date."];
            string[] questions=vi?[
                "Số ngày của {v4} của {v0} là bao nhiêu?",
                "Hỏi số ngày của {v4} của {v0} là bao nhiêu?",
                "{v4} của {v0} kéo dài bao nhiêu ngày?"]:[
                "What is the length of {v0}'s {v4} in days?",
                "How many days did {v0}'s {v4} cover?",
                "What is the duration of {v0}'s {v4} in days?"];
            foreach(string rule in rules)
            foreach(string question in questions)
            {
                var d=reference with {Facts=[reference.Facts![0],reference.Facts[1],new("fact_2",rule)],Question=question,
                    SolutionLeads=[new("step_0",vi?"Khoảng thời gian đã trôi qua":"Elapsed days")]};
                var proof=Validate(c,d);Check(proof.IsValid,"Clear calendar scope/target missed: "+proof);
                Check(QuestionBankStore.SerializeDraft(proof.Draft!)==QuestionBankStore.SerializeDraft(d),"Calendar proof rewrote the candidate.");
                SameMonthCalendarTests.CheckPractice(c,d);clearCount++;
                string rendered=ReasoningStoryCatalogue.ToPractice(c,d,ArithmeticQuizMode.Essay).ElementaryProblem!.ProblemText;
                Check(rendered.Split(". ").All(s=>s.Length==0||!char.IsLower(s[0])),"Rendered calendar sentence opening not capitalized.");
                foreach(string uncertain in vi?new[]{
                    "Thời gian này có thể không tính ngày bắt đầu mà tính cả ngày kết thúc.",
                    "Không chắc thời gian này không tính ngày bắt đầu mà tính cả ngày kết thúc.",
                    "Thời gian này không tính ngày bắt đầu mà tính cả ngày kết thúc, có lẽ vậy.",
                    "Chỉ đếm một phần các ngày từ ngày liền sau ngày bắt đầu đến hết ngày kết thúc.",
                    "Đừng chỉ tính ngày kết thúc.",
                    "Thời gian tính không bao gồm ngày bắt đầu, nhưng có tính ngày kết thúc nếu không phải cuối tuần.",
                    "Đợt hoạt động bắt đầu sau ngày đầu và kết thúc vào ngày cuối."}:new[]{
                    "The start date might not be counted, but the end date is counted.",
                    "It is not known whether the start date is not counted, but the end date is counted.",
                    "The start date is not counted, but the end date is counted, perhaps.",
                    "Count some calendar days from the day after the start date through the end date, inclusive.",
                    "Do not count all days from the second date up to and including it.",
                    "Count all calendar days from the day after the start date through the end date unless it is a holiday.",
                    "The period starts after the first day and ends on the last day."})
                {Check(Validate(c,d with {Facts=[d.Facts![0],d.Facts[1],new("fact_2",uncertain)]}).NeedsReview,"Qualified/negated/ambiguous rule auto-approved or hard-rejected: "+uncertain);reviewCount++;}
                foreach(string wrong in vi?new[]{
                    "Chỉ tính ngày kết thúc.",
                    "Không tính ngày bắt đầu và các ngày ở giữa, chỉ tính ngày kết thúc.",
                    "Tính cả ngày bắt đầu và ngày kết thúc.",
                    "Chỉ tính ngày làm việc."}:new[]{
                    "The duration counts all days from the second date up to and including it, but not the first date.",
                    "Count all days from the end date through that same date.",
                    "Include both the start date and the end date.",
                    "Count only working days."})
                {var result=Validate(c,d with {Facts=[d.Facts![0],d.Facts[1],new("fact_2",wrong)]});Check(!result.IsValid&&!result.NeedsReview,"Definite date-count contradiction not rejected: "+wrong);wrongCount++;}
                foreach(var unclear in new[]{
                    d with {Question=vi?"Số ngày sử dụng của {v4} của {v0} là bao nhiêu?":"How many days of actual use did {v0}'s {v4} involve?"},
                    d with {Question=vi?"Khoảng thời gian {v4} của {v0} là bao nhiêu ngày?":"How many days did the period {v0}'s {v4} cover?"},
                    d with {Question=vi?"Số ngày của đợt chăm sóc cây là {v4} của {v0}?":"Is the day count the {v4} for {v0}?"},
                    d with {SolutionLeads=[new("step_0",vi?"Tính số ngày trôi qua":"Calculate the elapsed days")]},
                    d with {Facts=[new("fact_0",vi?"{v0} {v2} {v1} bắt đầu từ ngày {f0} tháng {f2} năm {f3}.":"{v0} {v2} {v1} started on day {f0} of month {f2} in year {f3}."),d.Facts![1],d.Facts[2]]}})
                {Check(Validate(c,unclear).NeedsReview,"Awkward event, duplicate interval, changed/unclear target or imperative lead auto-approved.");reviewCount++;}
            }
        }
        Console.WriteLine($"PASS calendar prose strengthening: {clearCount} clear drafts, {reviewCount} review-only contrasts, {wrongCount} definite counting-rule contradictions; preserved text, rendering and fresh C# grading.");
    }
    private static void CheckPriorClearEnglish()
    {
        int clear=0,review=0,wrong=0;
        foreach(var (_,seed) in SameMonthCalendarTests.NativeCases().Where(x=>x.Contract.Language==AppLanguage.English&&
            (x.Contract.SceneId.EndsWith("/library")||x.Contract.SceneId.EndsWith("/equipment"))))
        {
            var c=seed with {OpenProse=true};var d=ReasoningStoryCatalogue.Draft(c);
            foreach(string question in new[]{"What is the duration of {v0}'s {v4}?","What is the duration in days of {v0}'s {v4}?","What is the duration of {v0}'s {v4} in days?"})
            {
                const string rule="The count begins on the day after the first date and ends on the second date, counting every calendar day in between.";
                var candidate=d with {Facts=[d.Facts![0],d.Facts[1],new("fact_2",rule)],Question=question,SolutionLeads=[new("step_0","Loan duration")]};
                Check(Validate(c,candidate).IsValid,"Prior clear English loan interval still missed.");
                SameMonthCalendarTests.CheckPractice(c,candidate);clear++;
                foreach(string uncertain in new[]{"Perhaps "+rule.ToLowerInvariant(),"It is not known whether "+rule.ToLowerInvariant(),
                    "Do not count all days from the second date up to and including it.",
                    "The count begins on the day after the first date and ends on the second date, counting some calendar days in between.",
                    "The count begins on the day after the first date and ends on the second date, counting every calendar day in between except holidays."})
                {Check(Validate(c,candidate with {Facts=[candidate.Facts![0],candidate.Facts[1],new("fact_2",uncertain)]}).NeedsReview,"Negation/partial scope was approved or hard-rejected.");review++;}
                foreach(string target in new[]{"What is not the duration of {v0}'s {v4}?","What is the duration of actual use during {v0}'s {v4}?","What is the duration of the period {v0}'s {v4}?"})
                {Check(Validate(c,candidate with {Question=target}).NeedsReview,"Changed/qualified target auto-approved.");review++;}
                foreach(string bad in new[]{"The duration counts all days from the second date up to and including it, but not the first date.","Include both the start date and the end date."})
                {var result=Validate(c,candidate with {Facts=[candidate.Facts![0],candidate.Facts[1],new("fact_2",bad)]});Check(!result.IsValid&&!result.NeedsReview,"Changed endpoint rule accepted.");wrong++;}
            }
        }
        Console.WriteLine($"PASS prior clear English calendar: {clear} clear, {review} negated/qualified review, {wrong} changed-rule rejects.");
    }
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static void Check(bool condition,string message)=>TestAssert.That(condition,message);
}
