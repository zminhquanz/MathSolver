using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static class FebruaryCommonCalendarReviewTests
{
    private sealed record Prior(string Case,string Schema,string Raw);
    // Exact completed native outputs; do not replace their wording with references.
    private static readonly Prior[] Previous=[
        new("CalendarFebruaryCommon-library-Vietnamese", "20289FAC58E6F1987EF6B4F7222DE1E6D93B0394006364879A6EC83F27D167AE", """{"facts": [{"role": "fact_0", "text": "{v0} {v2} {v1} vào ngày {f0} tháng {f2} năm {f3}."}, {"role": "fact_1", "text": "Vào ngày {f1} tháng {f4} năm {f3}, {v0} {v3} {v1}."}, {"role": "fact_2", "text": "Tháng {f2} có {f5} ngày."}, {"role": "fact_3", "text": "Cần tính tất cả các ngày, bao gồm cả ngày khởi đầu và ngày cuối cùng."}], "question": "Hỏi {v4} của {v0} kéo dài bao nhiêu ngày?", "solution_leads": [{"step": "step_0", "text": "Số ngày được tính"}]}"""),
        new("CalendarFebruaryCommon-equipment-Vietnamese", "930CDC31624DBBB342BD8A1549D0B28AEB4BA09D239BD2E81BB64D4F52F53246", """{"facts": [{"role": "fact_0", "text": "Vào ngày {f0} tháng {f2} năm {f3}, {v0} {v2} {v1}."}, {"role": "fact_1", "text": "{v0} {v3} {v1} vào ngày {f1} tháng {f4} năm {f3}."}, {"role": "fact_2", "text": "Tháng {f2} có {f5} ngày."}, {"role": "fact_3", "text": "Cần tính tất cả các ngày, bao gồm cả ngày bắt đầu và ngày kết thúc."}], "question": "Hỏi {v4} của {v0} kéo dài bao nhiêu ngày?", "solution_leads": [{"step": "step_0", "text": "Số ngày được tính"}]}"""),
        new("CalendarFebruaryCommon-garden-Vietnamese", "D814F9808FB59DA1CEDEB7EB3994473645B6EADDD7ABC006EB4462D7991AAF7B", """{"facts": [{"role": "fact_0", "text": "Vào ngày {f0} tháng {f2} năm {f3}, {v0} {v2} {v1}."}, {"role": "fact_1", "text": "Vào ngày {f1} tháng {f4} năm {f3}, {v0} {v3} {v1}."}, {"role": "fact_2", "text": "Tháng {f2} có {f5} ngày."}, {"role": "fact_3", "text": "Cần đếm tất cả các ngày, bao gồm cả ngày bắt đầu và ngày kết thúc."}], "question": "Hỏi {v4} của {v0} kéo dài bao nhiêu ngày?", "solution_leads": [{"step": "step_0", "text": "Số ngày được tính"}]}"""),
        new("CalendarFebruaryCommon-model-Vietnamese", "A8878D6019B597A7B0FD6755652B422AA634649B2090752CE9BF1CAB5E4FB32B", """{"facts": [{"role": "fact_0", "text": "{v0} {v2} {v1} vào ngày {f0} tháng {f2} năm {f3}."}, {"role": "fact_1", "text": "{v0} {v3} {v1} vào ngày {f1} tháng {f4} năm {f3}."}, {"role": "fact_2", "text": "Tháng {f2} có {f5} ngày."}, {"role": "fact_3", "text": "Tính tất cả các ngày, bao gồm cả ngày đầu và ngày cuối."}], "question": "Hỏi {v4} của {v0} kéo dài bao nhiêu ngày?", "solution_leads": [{"step": "step_0", "text": "Số ngày được tính"}]}"""),
        new("CalendarFebruaryCommon-library-English", "D9EF9A97E6AC1EA35195C8078F3F89656DEE9C02E9A3CD02FC7A206FA469659C", """{ "facts": [ { "role": "fact_0", "text": "{v0} {v2} {v1} on day {f0} of month {f2} in year {f3}." }, { "role": "fact_1", "text": "On day {f1} of month {f4} in year {f3}, {v0} {v3} {v1}." }, { "role": "fact_2", "text": "Month {f2} has {f5} days." }, { "role": "fact_3", "text": "Count every calendar day, including both the start date and the end date." } ], "question": "What is the total number of days in {v0}'s {v4}?", "solution_leads": [ { "step": "step_0", "text": "Number of calendar days" } ] }"""),
        new("CalendarFebruaryCommon-equipment-English", "38C9B1CC6BB1F6365B0A822CF639B68115E76E626760870485F44F7B6F40AED4", """{ "facts": [ { "role": "fact_0", "text": "On day {f0} of month {f2} in year {f3}, {v0} {v2} {v1}." }, { "role": "fact_1", "text": "{v0} {v3} {v1} on day {f1} of month {f4} in year {f3}." }, { "role": "fact_2", "text": "Month {f2} has {f5} days." }, { "role": "fact_3", "text": "Count every calendar day, including both the start date and the end date." } ], "question": "What is the total number of calendar days in {v0}'s {v4}?", "solution_leads": [ { "step": "step_0", "text": "Number of calendar days" } ] }"""),
        new("CalendarFebruaryCommon-garden-English", "7D8E345C8615B818E68FC59BF3CF8D46F4F49B5982AB1F81BB4CD69538500801", """{ "facts": [ { "role": "fact_0", "text": "On day {f0} of month {f2} in year {f3}, {v0} {v2} {v1}." }, { "role": "fact_1", "text": "On day {f1} of month {f4} in year {f3}, {v0} {v3} {v1}." }, { "role": "fact_2", "text": "Month {f2} has {f5} days." }, { "role": "fact_3", "text": "Count every calendar day, including both the start date and the end date." } ], "question": "What is the total number of calendar days in {v0}'s {v4}?", "solution_leads": [ { "step": "step_0", "text": "Number of calendar days" } ] }"""),
        new("CalendarFebruaryCommon-model-English", "7C124D4232C97E16EF8A2DDEB51F36436E820E69086071BEEDD5E7E1406C445D", """{ "facts": [ { "role": "fact_0", "text": "On day {f0} of month {f2} in year {f3}, {v0} {v2} {v1}." }, { "role": "fact_1", "text": "{v0} {v3} {v1} on day {f1} of month {f4} in year {f3}." }, { "role": "fact_2", "text": "Month {f2} has {f5} days." }, { "role": "fact_3", "text": "Count every calendar day, including both the start date and the end date." } ], "question": "What is the total number of calendar days in {v0}'s {v4}?", "solution_leads": [ { "step": "step_0", "text": "Number of calendar days" } ] }"""),
    ];
    internal static void Run()
    {
        var native=FebruaryCommonCalendarTests.NativeCases().ToDictionary(x=>x.Name,x=>x.Contract with {OpenProse=true});
        var modelReference=OpenQuestionProse.Example(native["CalendarFebruaryCommon-model-English"]);
        Check(modelReference.Facts![0].Text.IndexOf("{v0}",StringComparison.Ordinal)<modelReference.Facts[0].Text.IndexOf("{f0}",StringComparison.Ordinal)
            &&modelReference.Facts[1].Text.IndexOf("{f1}",StringComparison.Ordinal)<modelReference.Facts[1].Text.IndexOf("{v0}",StringComparison.Ordinal),
            "Model reference must demonstrate the actor-first start/date-first end requested by its variation guidance.");
        int clear=0,unproved=0,contrary=0;
        foreach(var prior in Previous)
        {
            var c=native[prior.Case];
            Check(c.Story!.Schema==prior.Schema,"February schema changed.");
            var result=BasicQuestionValidator.Validate(prior.Raw,c);
            Check(result.IsValid,"Prior clear February draft missed: "+prior.Case+" "+result.ErrorDetails);
            var syntax=OpenQuestionProse.ValidateSyntax(prior.Raw,c);
            Check(QuestionBankStore.SerializeDraft(result.Draft!)==QuestionBankStore.SerializeDraft(syntax.Draft!),"Proof rewrote native wording.");
            FebruaryCommonCalendarTests.CheckPractice(c.FreshFacts(new(719)),result.Draft!);clear++;
        }
        foreach(var type in new[]{SameMonthCalendarPilot.Type,SameMonthCalendarPilot.InclusiveType,SameMonthCalendarPilot.AdjacentType,SameMonthCalendarPilot.AdjacentInclusiveType,SameMonthCalendarPilot.FebruaryCommonType})
        foreach(var profile in SameMonthCalendarPilot.ProfilesForType(AppLanguage.Vietnamese,(int)type))
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)type,tier,AppLanguage.Vietnamese,new(19731),profile) with {OpenProse=true};
            var basis=OpenQuestionProse.Example(c);bool inclusive=SameMonthCalendarPilot.IsInclusive(profile);
            string[] included=[
                "Cần tính tất cả các ngày, bao gồm cả ngày khởi đầu và ngày cuối cùng.",
                "Tính cả ngày khởi đầu và ngày cuối cùng.",
                "Ngày khởi đầu và ngày cuối cùng đều được tính.",
                "Đếm mọi ngày từ ngày khởi đầu đến hết ngày cuối cùng.",
                "Tính cả ngày bắt đầu và ngày cuối cùng.",
                "Tính cả ngày khởi đầu và ngày kết thúc."];
            string excluded="Không tính ngày khởi đầu, có tính ngày cuối cùng.";
            foreach(string rule in included.Append(excluded))
            {
                bool matches=inclusive==(rule!=excluded);
                var d=Rule(basis,rule,profile);var result=Validate(c,d);
                if(matches)
                {
                    Check(result.IsValid,"Clear synonym rule missed: "+profile+" "+rule+" "+result.ErrorDetails);clear++;
                    Check(QuestionBankStore.SerializeDraft(result.Draft!)==QuestionBankStore.SerializeDraft(d),"Synonym proof rewrote wording.");
                }
                else {Check(!result.IsValid&&!result.NeedsReview,"Opposite counting rule was not rejected: "+profile+" "+rule);contrary++;}
                foreach(string prefix in new[]{"Không phải ","Có thể ","Nếu ","Không chắc "})
                {
                    var uncertain=Validate(c,Rule(basis,prefix+char.ToLowerInvariant(rule[0])+rule[1..],profile));
                    Check(uncertain.NeedsReview&&!uncertain.IsValid,"Qualified claim was approved or hard-rejected: "+prefix+rule);unproved++;
                }
                foreach(string text in new[]{rule.TrimEnd('.')+" nhưng không tính ngày cuối cùng.",rule.TrimEnd('.')+" nhưng bỏ qua cuối tuần.",rule.TrimEnd('.')+" nếu không có ngày nghỉ."})
                {Check(!Validate(c,Rule(basis,text,profile)).IsValid,"Trailing qualification was auto-approved: "+text);unproved++;}
                if(!matches)continue;
                foreach(string target in new[]{"{v4} của {v0} kéo dài bao nhiêu giờ?","{v0} thực sự sử dụng {v1} bao nhiêu ngày trong {v4}?","Ngày kết thúc {v4} của {v0} là ngày nào?"})
                {Check(!Validate(c,d with {Question=target}).IsValid,"Changed target auto-approved.");unproved++;}
                foreach(var fact in basis.Facts!.Take(2))
                {
                    var negated=d with {Facts=d.Facts!.Select(f=>f.Role==fact.Role?new NarrativeClause(f.Role,"Không phải "+f.Text):f).ToArray()};
                    Check(!Validate(c,negated).IsValid,"Negated date/action proof was auto-approved.");unproved++;
                }
            }
        }
        // A style hint is not a new grammar restriction: alternative openings remain accepted.
        foreach(var row in native.Values.Where(c=>c.Language==AppLanguage.English))
        {
            var basis=OpenQuestionProse.Example(row);
            Check(Validate(row,basis).IsValid,"Reference wording no longer valid.");
            var changed=basis with {Question="How many calendar days are counted for {v0}'s {v4}?"};
            Check(Validate(row,changed).IsValid,"A different clear question became unavailable.");
            FebruaryCommonCalendarTests.CheckPractice(row.FreshFacts(new(315)),changed);clear++;
        }
        Console.WriteLine($"PASS February follow-up: {clear} clear (all 8 prior native drafts), {unproved} unproved/negated/changed-target contrasts, {contrary} opposite conventions; all five calendar profiles, preserved schemas/text, fresh C# grading.");
    }
    private static BasicQuestionDraft Rule(BasicQuestionDraft d,string text,string profile)=>d with {Facts=d.Facts!.Select((f,i)=>i==(SameMonthCalendarPilot.IsAdjacent(profile)?3:2)?new NarrativeClause(f.Role,text):f).ToArray()};
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static void Check(bool value,string message)=>TestAssert.That(value,message);
}
