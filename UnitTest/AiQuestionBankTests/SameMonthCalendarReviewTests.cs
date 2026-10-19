using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static class SameMonthCalendarReviewTests
{
    private sealed record Example(string Case,string Rule,string Question,string Lead,bool Clear,bool DateStatement=false,bool DateFirst=false);
    // Read separately in the previous real-model report: nine clear candidates,
    // five awkward candidates and two ambiguous targets. Preserve that distinction.
    private static readonly Example[] Previous=[
        new("CalendarSameMonth-model-Vietnamese","Không tính ngày bắt đầu, có tính ngày kết thúc.","Thời gian {v4} của {v0} là bao nhiêu ngày?","Số ngày trôi qua",true,true),
        new("CalendarSameMonth-library-English","The count begins the day following the initial date and concludes on the final date.","What is the duration of {v0}'s {v4}?","Total days",true),
        new("CalendarSameMonth-equipment-English","The count begins the day following the first date and finishes on the final date.","What is the duration of {v0}'s {v4}?","Duration of loan",true,DateFirst:true),
        new("CalendarSameMonth-garden-English","The counting convention excludes the initial date and includes the final date.","What is the length of {v0}'s {v4}?","Duration in days",true),
        new("CalendarSameMonth-model-English","The count begins the day after the first date and ends on the final date.","What is the duration of {v0}'s {v4}?","Number of days",true),
        new("CalendarInclusive-exhibition-Vietnamese","Tính mọi ngày, gồm cả ngày bắt đầu và ngày kết thúc.","Số ngày của {v4} của {v0} là bao nhiêu?","Số ngày được tính",true),
        new("CalendarInclusive-reading-Vietnamese","Tính mọi ngày, bao gồm cả ngày đầu và ngày cuối.","Thời gian {v4} của {v0} là bao nhiêu ngày?","Số ngày được tính",true,DateFirst:true),
        new("CalendarInclusive-garden-English","Every calendar day is counted, starting with the first day and ending with the last day.","What is the duration of {v0}'s {v4}?","Total days in the period",true),
        new("CalendarInclusive-practice-English","Count every calendar day, including both the start date and the end date.","What is the count of days for {v0}'s {v4}?","Total number of days",true,DateFirst:true),
        new("CalendarSameMonth-library-Vietnamese","Tính số ngày từ ngày sau khi bắt đầu đến hết ngày kết thúc.","Khoảng thời gian mượn của {v4} của {v0} là bao nhiêu ngày?","Số ngày trôi qua",false),
        new("CalendarSameMonth-equipment-Vietnamese","Không tính ngày bắt đầu, có tính ngày kết thúc.","Khoảng thời gian mượn của {v4} của {v0} là bao nhiêu ngày?","Số ngày trôi qua",false,DateFirst:true),
        new("CalendarSameMonth-garden-Vietnamese","Không tính ngày bắt đầu, có tính ngày kết thúc.","Số {v4} của {v0} là bao nhiêu ngày?","Số ngày trôi qua",false),
        new("CalendarInclusive-garden-Vietnamese","Tính mọi ngày, bao gồm cả ngày bắt đầu và ngày kết thúc.","Khoảng thời gian {v4} của {v0} kéo dài bao nhiêu ngày?","Số ngày được tính",false),
        new("CalendarInclusive-practice-Vietnamese","Tính mọi ngày, bao gồm cả ngày bắt đầu và ngày kết thúc.","Khoảng thời gian {v4} của {v0} kéo dài bao nhiêu ngày?","Số ngày được tính",false),
        new("CalendarInclusive-exhibition-English","Every calendar day must be included, counting both the start date and the end date.","What is the total number of {v0}'s {v4} days?","Count of calendar days",false),
        new("CalendarInclusive-reading-English","All days within the span are included, starting from the first date and ending with the last date.","What is the total count for {v0}'s {v4}?","Total calendar days",false)
    ];

    internal static void Run()
    {
        var cases=SameMonthCalendarTests.NativeCases().Concat(SameMonthInclusiveTests.NativeCases()).ToDictionary(x=>x.Name,x=>x.Contract with {OpenProse=true});
        int clear=0,review=0,wrong=0;
        foreach(var row in Previous)
        {
            var c=cases[row.Case];bool vi=c.Language==AppLanguage.Vietnamese,inclusive=SameMonthCalendarPilot.IsInclusive(c.SceneId);
            var basis=ReasoningStoryCatalogue.Draft(c);
            var facts=basis.Facts!.ToArray();
            if(row.DateFirst)
            {
                facts[0]=new("fact_0",vi?"Vào ngày {f0} tháng {f2} năm {f3}, {v0} {v2} {v1}.":"On day {f0} of month {f2} in year {f3}, {v0} {v2} {v1}.");
                if(row.Case=="CalendarSameMonth-equipment-English")facts[1]=new("fact_1","On day {f1} of month {f2} in year {f3}, {v0} {v3} {v1}.");
            }
            if(row.DateStatement)facts[0]=new("fact_0","Ngày {v0} {v2} {v1} là ngày {f0} tháng {f2} năm {f3}.");
            facts[2]=new("fact_2",row.Rule);
            var d=basis with {Facts=facts,Question=row.Question,SolutionLeads=[new("step_0",row.Lead)]};
            var result=Validate(c,d);
            if(!row.Clear){Check(result.NeedsReview,"Previously awkward/ambiguous candidate must remain pending: "+row.Case);review++;continue;}
            Check(result.IsValid,"Previously clear candidate still missed: "+row.Case+" / "+result);
            Check(QuestionBankStore.SerializeDraft(result.Draft!)==QuestionBankStore.SerializeDraft(d),"Proof rewrote native wording.");
            SameMonthCalendarTests.CheckPractice(c,d);clear++;
            var promptLead=d with {SolutionLeads=[new("step_0",vi?"Số ngày được tính":"Number of days")]};
            Check(Validate(c,promptLead).IsValid,"Clear day-count lead suggested by the prompt must work for both relations.");
            SameMonthCalendarTests.CheckPractice(c,promptLead);clear++;
            foreach(var fresh in Enumerable.Range(0,4).Select(i=>c.FreshFacts(new(2213+i*7919))))SameMonthCalendarTests.CheckPractice(fresh,d);
            foreach(string uncertain in vi?new[]{
                "Có thể "+Lower(row.Rule),"Không chắc "+Lower(row.Rule),row.Rule.TrimEnd('.')+", có lẽ vậy.",
                row.Rule.TrimEnd('.')+" nếu không phải ngày nghỉ.",
                "Chỉ đếm một số ngày trong khoảng này.",
                "Không tính cả ngày bắt đầu và ngày kết thúc.",
                "Không được chỉ tính ngày kết thúc."}:new[]{
                "Perhaps "+Lower(row.Rule),"It is not known whether "+Lower(row.Rule),row.Rule.TrimEnd('.')+", perhaps.",
                row.Rule.TrimEnd('.')+" unless it is a holiday.",
                "Count some days from the first date through the final date.",
                "Do not count only the end date."})
            {Check(Validate(c,Rule(d,uncertain)).NeedsReview,"Qualified/negated counting claim auto-approved or hard-rejected: "+uncertain);review++;}
            foreach(string target in vi?new[]{
                "Số ngày sử dụng thực tế của {v4} của {v0} là bao nhiêu?",
                "Khoảng thời gian mượn của {v4} của {v0} là bao nhiêu ngày?",
                "Số {v4} của {v0} là bao nhiêu ngày?",
                "Không rõ số ngày của {v4} của {v0} là bao nhiêu?"}:new[]{
                "How many days of actual use did {v0}'s {v4} involve?",
                "What is the duration of the period {v0}'s {v4}?",
                "What is the total count for {v0}'s {v4}?",
                "What is not the number of days in {v0}'s {v4}?"})
            {Check(Validate(c,d with {Question=target}).NeedsReview,"Unclear/negated/changed target auto-approved.");review++;}
            foreach(string bad in vi?new[]{
                inclusive?"Không tính ngày đầu, có tính ngày cuối.":"Tính mọi ngày, bao gồm cả ngày đầu và ngày cuối.",
                "Không tính ngày cuối, có tính ngày đầu.","Chỉ tính ngày làm việc."}:new[]{
                inclusive?"The counting rule excludes the initial day and includes the final day.":"Include both the initial date and the final date.",
                "The counting convention excludes the final date and includes the first date.",
                "The count begins the day following the last day and ends on the first day.",
                "Count only working days."})
            {var proof=Validate(c,Rule(d,bad));Check(!proof.IsValid&&!proof.NeedsReview,"Definite changed endpoint/scope not rejected: "+bad);wrong++;}
            foreach(var unproved in new[]{
                d with {Facts=[new("fact_0",vi?"{v0} {v2} {v1} bắt đầu vào ngày {f0} tháng {f2} năm {f3}.":"{v0} {v2} {v1} started on day {f0} of month {f2} in year {f3}."),d.Facts![1],d.Facts[2]]},
                d with {SolutionLeads=[new("step_0",vi?"Tính số ngày":"Calculate the number of days")]}})
            {Check(Validate(c,unproved).NeedsReview,"Awkward event or imperative lead auto-approved.");review++;}
        }
        // Independent combinations of date-role synonyms: a second supplied
        // DATE may be the endpoint, but a second DAY cannot stand for that date.
        foreach(var c in cases.Values.Where(c=>c.Language==AppLanguage.English))
        foreach(string start in new[]{"start date","initial date","first date","first day"})
        foreach(string end in new[]{"end date","final date","last day","second date"})
        {
            var d=ReasoningStoryCatalogue.Draft(c);bool inclusive=SameMonthCalendarPilot.IsInclusive(c.SceneId);
            string rule=inclusive?$"Every calendar day is counted, starting with the {start} and ending with the {end}.":$"The count begins the day following the {start} and concludes on the {end}.";
            Check(Validate(c,Rule(d,rule)).IsValid,"Valid composed date roles missed.");clear++;
            foreach(string uncertain in new[]{"Not "+Lower(rule),rule.Replace("the "+end,"the second day"),rule.TrimEnd('.')+", excluding holidays."})
            {Check(Validate(c,Rule(d,uncertain)).NeedsReview,"Negated, second-day or exception scope auto-approved.");review++;}
            string reverse=$"The counting rule excludes the {end} and includes the {start}.";
            var proof=Validate(c,Rule(d,reverse));Check(!proof.IsValid&&!proof.NeedsReview,"Reversed date roles missed.");wrong++;
        }
        Console.WriteLine($"PASS calendar role-language strengthening: {clear} clear (all 9 prior clear cases), {review} review-only (all 7 prior awkward/ambiguous), {wrong} definite rule/scope errors; preserved text, fresh C# grading and date-role synonym contrasts.");
    }
    private static string Lower(string s)=>char.ToLowerInvariant(s[0])+s[1..];
    private static BasicQuestionDraft Rule(BasicQuestionDraft d,string text)=>d with {Facts=[d.Facts![0],d.Facts[1],new("fact_2",text)]};
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static void Check(bool value,string message)=>TestAssert.That(value,message);
}
