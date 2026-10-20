using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static class AdjacentMonthInclusiveReviewTests
{
    private sealed record Prior(string Case,string Schema,string Raw,bool Clear);
    // Exact prior native outputs. Their raw prose and schemas must remain unchanged.
    private static readonly Prior[] Previous=[
        new("CalendarAdjacentInclusive-library-Vietnamese", "9664C2FC4330FBB0DA9C464B6D3F653105606758F842A6230B7D2A1EAED507F2", """{"facts":[{"role":"fact_0","text":"{v0} {v2} {v1} ngày {f0} tháng {f2} năm {f3}."},{"role":"fact_1","text":"Vào ngày {f1} tháng {f4} năm {f3}, {v0} {v3} {v1}."},{"role":"fact_2","text":"Tháng {f2} có {f5} ngày."},{"role":"fact_3","text":"Cần đếm tất cả các ngày, bao gồm cả ngày bắt đầu và ngày kết thúc."}],"question":"Hỏi {v4} của {v0} kéo dài bao nhiêu ngày?","solution_leads":[{"step":"step_0","text":"Số ngày được tính"}]}""", true),
        new("CalendarAdjacentInclusive-equipment-Vietnamese", "9179E6BBB103F654E567DDDDF51CAEEFF7578A1446429ABA42FB5C4F45A95CD1", """{"facts": [{"role": "fact_0", "text": "Vào ngày {f0} tháng {f2} năm {f3}, {v0} {v2} {v1}."}, {"role": "fact_1", "text": "{v0} {v3} {v1} vào ngày {f1} tháng {f4} năm {f3}."}, {"role": "fact_2", "text": "Tháng {f2} có {f5} ngày."}, {"role": "fact_3", "text": "Cần đếm tất cả các ngày, bao gồm cả ngày bắt đầu và ngày kết thúc."}], "question": "Hỏi {v4} của {v0} kéo dài bao nhiêu ngày?", "solution_leads": [{"step": "step_0", "text": "Số ngày được tính"}]}""", true),
        new("CalendarAdjacentInclusive-garden-Vietnamese", "A91C1FDEB15E0049EA2E8D02C9B6716D769F2D36D72F8070621B4C502FAFF5B7", """{"facts":[{"role":"fact_0","text":"{v0} {v2} {v1} vào ngày {f0} tháng {f2} năm {f3}."},{"role":"fact_1","text":"{v0} {v3} {v1} vào ngày {f1} tháng {f4} năm {f3}."},{"role":"fact_2","text":"Tháng {f2} có {f5} ngày."},{"role":"fact_3","text":"Toàn bộ khoảng thời gian, bao gồm cả ngày đầu và ngày cuối, đều được tính."}],"question":"{v4} của {v0} kéo dài bao nhiêu ngày?","solution_leads":[{"step":"step_0","text":"Số ngày được tính"}]}""", true),
        new("CalendarAdjacentInclusive-model-Vietnamese", "B8633309BF2D38C97DF9359D59EDF24471AB00ECAB88BDEDBD290BB8F7F9842F", """{"facts": [{"role": "fact_0", "text": "{v0} {v2} {v1} vào ngày {f0} tháng {f2} năm {f3}."}, {"role": "fact_1", "text": "{v0} {v3} {v1} vào ngày {f1} tháng {f4} năm {f3}."}, {"role": "fact_2", "text": "Tháng {f2} có {f5} ngày."}, {"role": "fact_3", "text": "Cần tính tất cả các ngày, bao gồm cả ngày đầu và ngày cuối."}], "question": "Hỏi {v4} của {v0} kéo dài bao nhiêu ngày?", "solution_leads": [{"step": "step_0", "text": "Số ngày được tính"}]}""", true),
        new("CalendarAdjacentInclusive-library-English", "9ECCD2D9B62308768EC77CED90B0E6246BF3D8C9E97B8D650CEC6C6E874D617F", """{ "facts": [ { "role": "fact_0", "text": "{v0} {v2} {v1} on day {f0} of month {f2} in year {f3}." }, { "role": "fact_1", "text": "On day {f1} of month {f4} in year {f3}, {v0} {v3} {v1}." }, { "role": "fact_2", "text": "Month {f2} has {f5} days." }, { "role": "fact_3", "text": "Count every calendar day, including both the start date and the end date." } ], "question": "How many calendar days are in {v0}'s {v4}?", "solution_leads": [ { "step": "step_0", "text": "Number of calendar days" } ] }""", true),
        new("CalendarAdjacentInclusive-equipment-English", "E8B7A175A175C4EF7F69D568FEE2DE519A6B80D348D7096403844EE17046CB6D", """{ "facts": [ { "role": "fact_0", "text": "On day {f0} of month {f2} in year {f3}, {v0} {v2} {v1}." }, { "role": "fact_1", "text": "{v0} {v3} {v1} on day {f1} of month {f4} in year {f3}." }, { "role": "fact_2", "text": "Month {f2} possesses {f5} days." }, { "role": "fact_3", "text": "All intervening calendar days must be counted, starting from the initial day and ending on the final day." } ], "question": "What is the total number of the {v0}'s {v4}?", "solution_leads": [ { "step": "step_0", "text": "Number of calendar days" } ] }""", false),
        new("CalendarAdjacentInclusive-garden-English", "F7CB4CB28F5B9CCFE49EA82AA602CEED74A9EA066C32CC893F754F208096D58A", """{ "facts": [ { "role": "fact_0", "text": "On day {f0} of month {f2} in year {f3}, {v0} {v2} {v1}." }, { "role": "fact_1", "text": "On day {f1} of month {f4} in year {f3}, {v0} {v3} {v1}." }, { "role": "fact_2", "text": "Month {f2} has {f5} days." }, { "role": "fact_3", "text": "Count every calendar day, including both the start date and the end date." } ], "question": "How many calendar days are in {v0}'s {v4}?", "solution_leads": [ { "step": "step_0", "text": "Number of calendar days" } ] }""", true),
        new("CalendarAdjacentInclusive-model-English", "52E6DE09E9FBAAA54EA80F52C51AE00B1D22B943C0EFB800CF36A7085F85A867", """{ "facts": [ { "role": "fact_0", "text": "On day {f0} of month {f2} in year {f3}, {v0} {v2} {v1}." }, { "role": "fact_1", "text": "{v0} {v3} {v1} on day {f1} of month {f4} in year {f3}." }, { "role": "fact_2", "text": "Month {f2} contains {f5} days." }, { "role": "fact_3", "text": "Count every intervening calendar day, encompassing both the initial and final dates." } ], "question": "What is the number of calendar {v0}'s {v4}?", "solution_leads": [ { "step": "step_0", "text": "Number of calendar days" } ] }""", false),
        new("CalendarAdjacentInclusive-library-Vietnamese", "9664C2FC4330FBB0DA9C464B6D3F653105606758F842A6230B7D2A1EAED507F2", """{ "facts": [ { "role": "fact_0", "text": "{v0} {v2} {v1} vào ngày {f0} tháng {f2} năm {f3}." }, { "role": "fact_1", "text": "Vào ngày {f1} tháng {f4} năm {f3}, {v0} {v3} {v1}." }, { "role": "fact_2", "text": "Tháng {f2} có {f5} ngày." }, { "role": "fact_3", "text": "Toàn bộ khoảng thời gian, bao gồm cả ngày bắt đầu và ngày kết thúc, được tính." } ], "question": "Hỏi {v4} của {v0} kéo dài bao nhiêu ngày?", "solution_leads": [ { "step": "step_0", "text": "Số ngày được tính" } ] }""", true),
        new("CalendarAdjacentInclusive-equipment-Vietnamese", "9179E6BBB103F654E567DDDDF51CAEEFF7578A1446429ABA42FB5C4F45A95CD1", """{"facts":[{"role":"fact_0","text":"Theo ghi chép, {v0} {v2} {v1} vào ngày {f0} tháng {f2} năm {f3}."},{"role":"fact_1","text":"Vào ngày {f1} tháng {f4} năm {f3}, {v0} {v3} {v1}."},{"role":"fact_2","text":"Tháng {f2} có {f5} ngày."},{"role":"fact_3","text":"Toàn bộ khoảng thời gian, bao gồm cả ngày đầu và ngày cuối, được tính."}],"question":"Hỏi {v4} của {v0} kéo dài bao nhiêu ngày?","solution_leads":[{"step":"step_0","text":"Số ngày được tính"}]}""", true),
        new("CalendarAdjacentInclusive-garden-Vietnamese", "A91C1FDEB15E0049EA2E8D02C9B6716D769F2D36D72F8070621B4C502FAFF5B7", """{"facts": [{"role": "fact_0", "text": "{v0} {v2} {v1} vào ngày {f0} tháng {f2} năm {f3}."}, {"role": "fact_1", "text": "Vào ngày {f1} tháng {f4} năm {f3}, {v0} {v3} {v1}."}, {"role": "fact_2", "text": "Tháng {f2} có {f5} ngày."}, {"role": "fact_3", "text": "Toàn bộ khoảng thời gian, bao gồm cả ngày bắt đầu và ngày kết thúc, được tính."}], "question": "Hỏi {v4} của {v0} kéo dài bao nhiêu ngày?", "solution_leads": [{"step": "step_0", "text": "Số ngày được tính"}]}""", true),
        new("CalendarAdjacentInclusive-model-Vietnamese", "B8633309BF2D38C97DF9359D59EDF24471AB00ECAB88BDEDBD290BB8F7F9842F", """{"facts": [{"role": "fact_0", "text": "Vào ngày {f0} tháng {f2} năm {f3}, {v0} {v2} {v1}."}, {"role": "fact_1", "text": "{v0} {v3} {v1} vào ngày {f1} tháng {f4} năm {f3}."}, {"role": "fact_2", "text": "Tháng {f2} có {f5} ngày."}, {"role": "fact_3", "text": "Mọi ngày trong khoảng thời gian từ ngày bắt đầu đến ngày kết thúc đều được tính."}], "question": "Hỏi {v4} của {v0} kéo dài bao nhiêu ngày?", "solution_leads": [{"step": "step_0", "text": "Số ngày được tính"}]}""", true),
    ];
    internal static void Run()
    {
        var cases=AdjacentMonthInclusiveTests.NativeCases().ToDictionary(x=>x.Name,x=>x.Contract with {OpenProse=true});
        int clear=0,review=0,wrong=0;
        foreach(var prior in Previous)
        {
            var c=cases[prior.Case];Check(c.Story!.Schema==prior.Schema,"Existing inclusive profile schema changed.");
            var result=BasicQuestionValidator.Validate(prior.Raw,c);
            if(prior.Clear)
            {
                Check(result.IsValid,"Prior clear inclusive output missed: "+prior.Case+" "+result.ErrorDetails);
                var original=OpenQuestionProse.ValidateSyntax(prior.Raw,c).Draft!;
                Check(QuestionBankStore.SerializeDraft(result.Draft!)==QuestionBankStore.SerializeDraft(original),"Verifier rewrote a native draft.");
                AdjacentMonthInclusiveTests.CheckPractice(c.FreshFacts(new(713)),result.Draft!);clear++;
            }
            else {Check(result.NeedsReview&&!result.IsValid,"Ambiguous English native question was auto-approved or hard-rejected.");review++;}
        }
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(var context in SameMonthCalendarPilot.Contexts(language,true,true))
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            bool vi=language==AppLanguage.Vietnamese;
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)SameMonthCalendarPilot.AdjacentInclusiveType,tier,language,new(19731),SameMonthCalendarPilot.AdjacentInclusivePrefix+context.Id) with {OpenProse=true};
            var basis=OpenQuestionProse.Example(c);
            string[] rules=vi?[
                "Cần đếm tất cả các ngày, bao gồm cả ngày bắt đầu và ngày kết thúc.",
                "Cần tính tất cả các ngày, bao gồm cả ngày đầu và ngày cuối.",
                "Toàn bộ khoảng thời gian, bao gồm cả ngày đầu và ngày cuối, đều được tính.",
                "Toàn bộ khoảng thời gian, bao gồm cả ngày bắt đầu và ngày kết thúc, được tính.",
                "Mọi ngày trong khoảng thời gian từ ngày bắt đầu đến ngày kết thúc đều được tính."]:[
                "Count every calendar day, including both the start date and the end date.",
                "Include both the start date and the end date."];
            foreach(string rule in rules)
            {
                var d=Rule(basis,rule);
                if(!vi)d=d with {Question="How many calendar days are in {v0}'s {v4}?"};
                var proof=Validate(c,d);Check(proof.IsValid,"Clear inclusive rule/target not proved: "+rule+" "+proof.ErrorDetails);clear++;
                Check(QuestionBankStore.SerializeDraft(proof.Draft!)==QuestionBankStore.SerializeDraft(d),"Proof rewrote wording.");
                AdjacentMonthInclusiveTests.CheckPractice(c.FreshFacts(new(315)),d);
                // The same positive include-both rule contradicts the elapsed-day profile.
                var elapsed=ReasoningStoryCatalogue.Create(c.Family,(int)SameMonthCalendarPilot.AdjacentType,tier,language,new(19731),SameMonthCalendarPilot.AdjacentPrefix+context.Id) with {OpenProse=true};
                var contrary=Validate(elapsed,d);Check(!contrary.IsValid&&!contrary.NeedsReview,"Inclusive rule admitted under elapsed-day profile.");wrong++;
                foreach(string text in vi?new[]{"Nếu "+Lower(rule),"Có lẽ "+Lower(rule),"Không chắc "+Lower(rule),"Không phải "+Lower(rule),rule.TrimEnd('.')+", nếu không có ngày nghỉ.",rule.Replace("bao gồm","không bao gồm"),rule.Replace("tất cả các ngày","một số ngày"),rule.Replace("Mọi ngày","Một số ngày"),rule.Replace("đều được tính","không được tính"),rule.Replace("từ ngày bắt đầu","từ ngày liền sau ngày bắt đầu")}:new[]{"If "+Lower(rule),"Perhaps "+Lower(rule),"It is not true that "+Lower(rule),rule.TrimEnd('.')+", unless it is a holiday.",rule.Replace("including both","excluding both"),rule.Replace("Include both","Do not include both"),rule.Replace("every calendar day","every other calendar day")})
                {
                    if(text==rule)continue;
                    var r=Validate(c,Rule(d,text));Check(r.NeedsReview&&!r.IsValid,"Qualified/negated/partial count was approved or hard-rejected: "+text);review++;
                }
                foreach(string target in vi?new[]{"Không rõ {v4} của {v0} kéo dài bao nhiêu ngày?","{v4} của {v0} kéo dài bao nhiêu giờ?","{v0} thực sự sử dụng {v1} bao nhiêu ngày trong {v4}?"}:new[]{"How many calendar days are not in {v0}'s {v4}?","If known, how many calendar days are in {v0}'s {v4}?","How many calendar hours are in {v0}'s {v4}?","What is the total number of the {v0}'s {v4}?","What is the number of calendar {v0}'s {v4}?","How many working days are in {v0}'s {v4}?","How many calendar days are in actual use during {v0}'s {v4}?","How many calendar days are in {v0}'s {v4}, excluding holidays?"})
                {var r=Validate(c,d with {Question=target});Check(!r.IsValid,"Changed/unclear day-count target accepted: "+target+" "+r.ErrorCode);if(r.NeedsReview)review++;else wrong++;}
                foreach(string bad in vi?new[]{"Không tính ngày bắt đầu, có tính ngày kết thúc.","Tính ngày bắt đầu, không tính ngày kết thúc.","Không tính ngày bắt đầu, không tính ngày kết thúc.","Chỉ tính ngày làm việc."}:new[]{"Exclude the start date and include the end date.","Include the start date and exclude the end date.","Exclude both the start date and the end date.","Count only working days."})
                {var r=Validate(c,Rule(d,bad));Check(!r.IsValid&&!r.NeedsReview,"Definite changed counting rule not rejected.");wrong++;}
                var endTarget=d with {Question=vi?"Ngày kết thúc {v4} của {v0} là ngày nào?":"What is the end date of {v0}'s {v4}?"};
                Check(!Validate(c,endTarget).IsValid&&!Validate(c,endTarget).NeedsReview,"End-date target not rejected.");wrong++;
            }
            if(!vi)
            {
                string grammar=OpenQuestionProse.Grammar(c);
                string questionRule=grammar.Split('\n').Single(line=>line.StartsWith("field-4 ::="));
                Check(questionRule.Contains("calendar-days")&&grammar.Contains("calendar-days ::="),"English question grammar does not bind the days unit.");
                Check(questionRule.Contains("word-gap"),"English question no longer has open connective prose.");
                Check(!questionRule.Contains("How many")&&!questionRule.Contains("What is"),"Grammar enumerates complete question openings.");
            }
        }
        Console.WriteLine($"PASS inclusive-calendar strengthening: {clear} clear (all 6 prior clear and diagnostic clear outputs), {review} review-only (both prior ambiguous), {wrong} definite contradictions; exact text/schema, fresh C# grading, negation, counting convention and English days-unit grammar.");
    }
    private static string Lower(string s)=>char.ToLowerInvariant(s[0])+s[1..];
    private static BasicQuestionDraft Rule(BasicQuestionDraft d,string text)=>d with {Facts=d.Facts!.Select((f,i)=>i==3?new NarrativeClause(f.Role,text):f).ToArray()};
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static void Check(bool value,string message)=>TestAssert.That(value,message);
}
