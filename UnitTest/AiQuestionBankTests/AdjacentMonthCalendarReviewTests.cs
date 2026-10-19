using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static class AdjacentMonthCalendarReviewTests
{
    private sealed record Prior(string Case,string Schema,string Raw,bool Clear);
    // Exact outputs from the previous native run, classified separately by reading.
    // Keep them in source so regression tests do not depend on local artifacts.
    private static readonly Prior[] Previous=[
        new("CalendarAdjacent-library-Vietnamese", "2CE082C8CA23F67DE55B31F0E65604E23A9B282903467B1D20AB8A11A416DF54", """{"facts": [{"role": "fact_0", "text": "{v0} {v2} {v1} vào ngày {f0} tháng {f2} năm {f3}."}, {"role": "fact_1", "text": "{v0} {v3} {v1} vào ngày {f1} tháng {f4} năm {f3}."}, {"role": "fact_2", "text": "Tháng {f2} có {f5} ngày."}, {"role": "fact_3", "text": "Việc tính toán này không bao gồm ngày bắt đầu mà sẽ tính cả ngày kết thúc, đồng thời đếm tất cả các ngày ở giữa."}], "question": "Khoảng thời gian từ khi {v4} của {v0} là bao nhiêu ngày?", "solution_leads": [{"step": "step_0", "text": "Số ngày trôi qua"}]}""", false),
        new("CalendarAdjacent-equipment-Vietnamese", "1B0D161EA8998DD6653DD908BB44CF33D03B39C4F9B3D5FBFE51AF2D737DE47A", """{"facts": [{"role": "fact_0", "text": "{v0} {v2} {v1} vào ngày {f0} tháng {f2} năm {f3}."}, {"role": "fact_1", "text": "{v0} {v3} {v1} vào ngày {f1} tháng {f4} năm {f3}."}, {"role": "fact_2", "text": "Tháng {f2} có {f5} ngày."}, {"role": "fact_3", "text": "Việc tính toán này không bao gồm ngày bắt đầu mà sẽ tính cả ngày kết thúc."}], "question": "Khoảng thời gian sử dụng {v4} của {v0} là bao nhiêu ngày?", "solution_leads": [{"step": "step_0", "text": "Số ngày trôi qua"}]}""", false),
        new("CalendarAdjacent-garden-Vietnamese", "62B232565D20E8EEC75E2FF9ECF2B555CF63B6C18733BF8EEB42D09AF1B4149B", """{"facts": [{"role":"fact_0","text":"{v0} {v2} {v1} vào ngày {f0} tháng {f2} năm {f3}."},{"role":"fact_1","text":"{v0} {v3} {v1} vào ngày {f1} tháng {f4} năm {f3}."},{"role":"fact_2","text":"Tháng {f2} có {f5} ngày."},{"role":"fact_3","text":"Không tính ngày bắt đầu, có tính ngày kết thúc."}], "question": "Số ngày của {v4} của {v0} là bao nhiêu?", "solution_leads": [{"step":"step_0","text":"Số ngày trôi qua"}]}""", true),
        new("CalendarAdjacent-model-Vietnamese", "1755C16D04F729E968866E23AC3DE3602D59F8CD942FBCCD673E837997A32FC7", """{"facts":[{"role":"fact_0","text":"Vật dụng {v0} {v2} {v1} vào ngày {f0} tháng {f2} năm {f3}."},{"role":"fact_1","text":"{v0} {v3} {v1} vào ngày {f1} tháng {f4} năm {f3}."},{"role":"fact_2","text":"Tháng {f2} có {f5} ngày."},{"role":"fact_3","text":"Việc đếm số ngày không tính ngày bắt đầu, nhưng sẽ tính ngày kết thúc và tất cả các ngày ở giữa."}],"question":"Số ngày của khoảng thời gian từ {v4} của {v0} là bao nhiêu?","solution_leads":[{"step":"step_0","text":"Số ngày trôi qua"}]}""", false),
        new("CalendarAdjacent-library-English", "3C2EA691344E7AE842C16DD9E6A0D22A0720CDF2AEBE1B3A042F59AC551141AA", """{ "facts": [ { "role": "fact_0", "text": "{v0} {v2} {v1} on day {f0} of month {f2} in year {f3}." }, { "role": "fact_1", "text": "{v0} {v3} {v1} on day {f1} of month {f4} in year {f3}." }, { "role": "fact_2", "text": "Month {f2} has {f5} days." }, { "role": "fact_3", "text": "The period begins after the initial date and ends on the final date, counting all intervening days." } ], "question": "What is the span of time for {v0}'s {v4} in days?", "solution_leads": [ { "step": "step_0", "text": "Number of days" } ] }""", false),
        new("CalendarAdjacent-equipment-English", "145FDB1F431FB5F549ECE721D2C25E62B6D6CB4D56F8C5FB81CAEA8385A7B882", """{ "facts": [ { "role": "fact_0", "text": "{v0} {v2} {v1} on day {f0} of month {f2} in year {f3}." }, { "role": "fact_1", "text": "{v0} {v3} {v1} on day {f1} of month {f4} in year {f3}." }, { "role": "fact_2", "text": "Month {f2} has {f5} days." }, { "role": "fact_3", "text": "The period begins after the start date and concludes on the end date, counting all intervening days." } ], "question": "What is the length of {v0}'s {v4} in days?", "solution_leads": [ { "step": "step_0", "text": "Number of days" } ] }""", false),
        new("CalendarAdjacent-garden-English", "C794F8F283B8885F7DE41DC39A22664E85EB9ED0150E9E047C07087DA0BB12FA", """{ "facts": [ { "role": "fact_0", "text": "{v0} {v2} {v1} on day {f0} of month {f2} in year {f3}." }, { "role": "fact_1", "text": "{v0} {v3} {v1} on day {f1} of month {f4} in year {f3}." }, { "role": "fact_2", "text": "Month {f2} has {f5} days." }, { "role": "fact_3", "text": "The start date is excluded and the end date is included." } ], "question": "What is the duration of {v0}'s {v4} in days?", "solution_leads": [ { "step": "step_0", "text": "Number of days" } ] }""", true),
        new("CalendarAdjacent-model-English", "4E4FB4F78FF431D89D1B7A9E3172325392A0E16211C10F9864C0A38347C59F76", """{ "facts": [ { "role": "fact_0", "text": "{v0} {v2} {v1} on day {f0} of month {f2} in year {f3}." }, { "role": "fact_1", "text": "{v0} {v3} {v1} on day {f1} of month {f4} in year {f3}." }, { "role": "fact_2", "text": "Month {f2} has {f5} days." }, { "role": "fact_3", "text": "The start date is excluded and the end date is included." } ], "question": "What is the total duration of {v0}'s {v4} in days?", "solution_leads": [ { "step": "step_0", "text": "Number of days" } ] }""", true),
    ];
    internal static void Run()
    {
        var cases=AdjacentMonthCalendarTests.NativeCases().ToDictionary(x=>x.Name,x=>x.Contract with {OpenProse=true});
        int clear=0,review=0,wrong=0;
        foreach(var row in Previous)
        {
            var c=cases[row.Case];Check(c.Story!.Schema==row.Schema,"Existing calendar schema changed.");
            var result=BasicQuestionValidator.Validate(row.Raw,c);
            Check(row.Clear?result.IsValid:result.NeedsReview,"Previous native quality classification changed: "+row.Case+" "+result);
            var original=OpenQuestionProse.ValidateSyntax(row.Raw,c).Draft!;
            Check(QuestionBankStore.SerializeDraft(result.Draft!)==QuestionBankStore.SerializeDraft(original),"Verifier rewrote prior prose.");
            if(row.Clear){clear++;AdjacentMonthCalendarTests.CheckPractice(c.FreshFacts(new(413)),result.Draft!);}else review++;
        }
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var eventPairs=new HashSet<string>();
            foreach(var c in cases.Values.Where(c=>c.Language==language))
            {
                bool vi=language==AppLanguage.Vietnamese;var basis=OpenQuestionProse.Example(c);
                Check(Validate(c,basis).IsValid,"Context-specific open reference not proved.");
                eventPairs.Add(basis.Facts![0].Text+"\n"+basis.Facts[1].Text);
                Check(OpenQuestionProse.Grammar(c).All(ch=>ch<=127),"Calendar grammar is not ASCII escaped.");
                string prompt=BasicQuestionPrompt.Build(c);
                Check(prompt.Contains(vi?"chỉ việc đếm ngày":"Only counting begins later")&&prompt.Contains(vi?"không phải mẫu câu bắt buộc":"not mandatory sentences"),"Counting/event distinction or creative freedom missing from prompt.");
                string[] rules=vi?[
                    "Khi đếm ngày, không tính ngày đầu và có tính ngày cuối.",
                    "Việc đếm ngày không bao gồm ngày bắt đầu mà sẽ tính cả ngày kết thúc, đồng thời đếm tất cả các ngày ở giữa.",
                    "Không tính ngày bắt đầu, có tính ngày kết thúc."]:[
                    "For counting, exclude the start date and include the end date.",
                    "The day count begins on the day immediately after the start date and ends on the end date.",
                    "The day count starts on the day following the start date and finishes on the end date, counting all intervening days."];
                foreach(string rule in rules)
                {
                    var d=Rule(basis,rule);Check(Validate(c,d).IsValid,"Clear counting rule missed: "+rule);clear++;
                    Check(QuestionBankStore.SerializeDraft(Validate(c,d).Draft!)==QuestionBankStore.SerializeDraft(d),"Open reference rewritten.");
                    AdjacentMonthCalendarTests.CheckPractice(c.FreshFacts(new(315)),d);
                    foreach(string uncertain in vi?new[]{
                        "Nếu "+Lower(rule),"Không chắc "+Lower(rule),"Có lẽ "+Lower(rule),rule.TrimEnd('.')+", nếu không có ngày nghỉ.",
                        "Khoảng thời gian bắt đầu sau ngày bắt đầu và kết thúc vào ngày kết thúc.",
                        "Hoạt động bắt đầu từ ngày liền sau ngày bắt đầu và kết thúc vào ngày cuối.",
                        "Chỉ khi đếm ngày, không tính ngày đầu và có tính ngày cuối."}:new[]{
                        "If "+Lower(rule),"It is not true that "+Lower(rule),"Perhaps "+Lower(rule),rule.TrimEnd('.')+", unless it is a holiday.",
                        "The period begins after the initial date and ends on the final date, counting all intervening days.",
                        "The period begins after the start date and concludes on the end date, counting all intervening days.",
                        "The activity begins on the day immediately after the start date and ends on the end date.",
                        "The day count begins on the second day and ends on the end date.",
                        "The day count does not begin on the day immediately after the start date and ends on the end date."})
                    {Check(Validate(c,Rule(d,uncertain)).NeedsReview,"Qualified rule or changed event start auto-approved/hard-rejected: "+uncertain);review++;}
                    foreach(string target in vi?new[]{
                        "Khoảng thời gian từ khi {v4} của {v0} là bao nhiêu ngày?",
                        "Khoảng thời gian sử dụng {v4} của {v0} là bao nhiêu ngày?",
                        "Số ngày của khoảng thời gian từ {v4} của {v0} là bao nhiêu?",
                        "Không rõ {v4} của {v0} kéo dài bao nhiêu ngày?"}:new[]{
                        "What is not the total duration of {v0}'s {v4} in days?",
                        "If known, what is the total duration of {v0}'s {v4} in days?",
                        "What is the total duration of the period {v0}'s {v4} in days?",
                        "What is the total duration of {v0}'s {v4} in hours?",
                        "What is the total duration of actual use during {v0}'s {v4} in days?",
                        "What is the total duration of {v0}'s {v4} excluding holidays in days?"})
                    {Check(Validate(c,d with {Question=target}).NeedsReview,"Negated, redundant or wrong-unit/actual-use target auto-approved.");review++;}
                    foreach(string bad in vi?new[]{"Tính cả ngày bắt đầu và ngày kết thúc.","Chỉ tính ngày làm việc.","Không tính ngày cuối, có tính ngày đầu."}
                        :new[]{"Include both the start date and the end date.","Count only working days.","The counting rule excludes the end date and includes the start date."})
                    {var result=Validate(c,Rule(d,bad));Check(!result.IsValid&&!result.NeedsReview,"Definite rule change not rejected.");wrong++;}
                    var wrongTarget=d with {Question=vi?"Ngày kết thúc {v4} của {v0} là ngày nào?":"What is the end date of {v0}'s {v4}?"};
                    Check(!Validate(c,wrongTarget).IsValid&&!Validate(c,wrongTarget).NeedsReview,"Changed date target not rejected.");wrong++;
                    if(!vi)foreach(string target in new[]{"What is the total duration of {v0}'s {v4} in days?","What was the total length of {v0}’s {v4} in days?","What is the total duration of the {v4} for {v0} in days?"})
                    {Check(Validate(c,d with {Question=target}).IsValid,"Clear total-duration target missed.");clear++;}
                }
            }
            Check(eventPairs.Count==4,"Context references still share event-pair structures.");
        }
        Console.WriteLine($"PASS adjacent-calendar strengthening: {clear} clear (all 3 prior clear outputs), {review} review-only (all 5 prior awkward/ambiguous outputs), {wrong} definite contradictions; exact text/schema preserved, four reference event pairs per language, fresh C# grading and counting-vs-event contrasts.");
    }
    private static string Lower(string s)=>char.ToLowerInvariant(s[0])+s[1..];
    private static BasicQuestionDraft Rule(BasicQuestionDraft d,string text)=>d with {Facts=d.Facts!.Select((f,i)=>i==3?new NarrativeClause(f.Role,text):f).ToArray()};
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static void Check(bool value,string message)=>TestAssert.That(value,message);
}
