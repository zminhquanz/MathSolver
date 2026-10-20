using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;

internal static class SameMonthCalendarWorksheetTests
{
    private sealed class AlwaysBank:Random {public override int Next(int maxValue)=>maxValue-1;}
    internal static async Task Run(string directory,bool inclusive=false,bool adjacent=false,bool february=false)
    {
        int cases=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            bool vi=language==AppLanguage.Vietnamese;var items=new List<QuizPrintItem>();
            foreach(var context in SameMonthCalendarPilot.Contexts(language,inclusive,adjacent,february))
            foreach(var tier in Enum.GetValues<CurriculumTier>())
            {
                string prefix=SameMonthCalendarPilot.ProfilePrefix(inclusive,adjacent,february);
                var type=SameMonthCalendarPilot.ProfileType(prefix);
                string relation=SameMonthCalendarPilot.ProfileRelation(prefix);
                var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)type,tier,language,new(2813),prefix+context.Id) with {OpenProse=true};
                var d=ReasoningStoryCatalogue.Draft(c) with {Facts=context.AlternativeFacts.Select((s,i)=>new NarrativeClause("fact_"+i,s)).ToArray(),Question=context.AlternativeQuestion};
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c).IsValid,"PDF calendar fixture not verified.");
                var store=new QuestionBankStore(Path.Combine(directory,$"calendar-{(february?"february-common":adjacent?(inclusive?"adjacent-inclusive":"adjacent"):inclusive.ToString())}-{language}-{context.Id}-{tier}.db3"));
                var pending=d with {Facts=d.Facts!.Select((f,i)=>i==(adjacent?3:2)?new NarrativeClause(f.Role,vi?"Đếm ngày theo hai mốc.":"Count days using the two dates."):f).ToArray()};
                string raw=QuestionBankStore.SerializeDraft(pending);
                Check(BasicQuestionValidator.Validate(raw,c).NeedsReview,"Missing calendar convention must review.");
                Check(await store.QueueReviewAsync(PendingQuestionReview.Create(c,pending,raw,"synthetic-pdf","review")),"Cannot queue unproved calendar.");
                Check(await store.TakeReasoningAsync(c.Family,c.BankVariant,tier,language) is null,"Pending calendar entered PDF bank.");
                Check(await store.InsertAsync(new(c,d,QuestionBankStore.SerializeDraft(d),"verified-fixture",DateTime.UtcNow)),"Cannot save verified calendar.");
                var factory=new PracticeQuestionFactory(new(store,new AlwaysBank()));
                foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var q=await factory.CreateAsync(new(QuizProblemKind.Time,ElementaryType:type),mode,new(tier,false),language,null,PracticeQuestionFormat.WordProblem,false);
                    var p=q.ElementaryProblem!;
                    Check(p.StoryContextId==c.SceneId&&p.Reasoning!.ScenarioId==relation,"PDF chose another calendar relation.");
                    var givens=p.Reasoning!.Givens.ToDictionary(g=>g.Role,g=>int.Parse(g.Value));
                    var start=new DateTime(givens["year"],givens[adjacent?"start-month":"month"],givens["start-day"]);
                    var end=new DateTime(givens["year"],givens[adjacent?"end-month":"month"],givens["end-day"]);
                    if(february)Check(start.Month==2&&end.Month==3&&!DateTime.IsLeapYear(start.Year)&&givens["start-month-length"]==28,"PDF February common-year scope changed.");
                    else if(adjacent)Check(end.Month==start.Month+1&&start.Month>=3&&givens["start-month-length"]==DateTime.DaysInMonth(start.Year,start.Month),"PDF adjacent month roles changed.");
                    int elapsed=(end-start).Days+(inclusive?1:0);
                    Check(elapsed>0&&p.Answers.Single().Value==new ReducedFraction(elapsed,1)&&givens["include-start"]==(inclusive?1:0)&&givens["include-end"]==1,"PDF calendar convention/oracle failed.");
                    var printed=QuizWorksheetBuilder.Format(q,language);
                    Check(!printed.Prompt.Contains('{')&&printed.Prompt.Contains(inclusive?(vi?"Tính cả ngày bắt đầu":"Include both the start date"):(vi?"Ngày bắt đầu không được tính":"Exclude the start date"),StringComparison.OrdinalIgnoreCase),"PDF lost convention or slots.");
                    Check(printed.Solution.Contains(p.Answers[0].Unit),"PDF lost day unit.");
                    if(adjacent)Check(printed.Prompt.Contains(givens["start-month-length"].ToString()),"PDF lost start-month length.");
                    if(mode==ArithmeticQuizMode.Essay)items.Add(printed);cases++;
                }
            }
            if(inclusive||adjacent)
            {
                // Force the equal-date or adjacent-boundary case into each worksheet;
                // random factory coverage alone must not stand in for this edge.
                var boundary=ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)(february?SameMonthCalendarPilot.FebruaryCommonType:adjacent?(inclusive?SameMonthCalendarPilot.AdjacentInclusiveType:SameMonthCalendarPilot.AdjacentType):SameMonthCalendarPilot.InclusiveType),
                    CurriculumTier.OneStar,language,new(341),SameMonthCalendarPilot.Profiles(language,inclusive,adjacent,february).First()) with {OpenProse=true};
                for(int seed=0;;seed++)
                {
                    var data=SameMonthCalendarPilot.Create(boundary.SceneId,language,boundary.Tier,new(seed));
                    if(adjacent?data.StartDay!=data.MonthLength||data.EndDay!=1:data.StartDay!=data.EndDay)continue;
                    boundary=boundary with {Story=boundary.Story! with {Seed=seed}};
                    var q=ReasoningStoryCatalogue.ToPractice(boundary,ReasoningStoryCatalogue.Draft(boundary),ArithmeticQuizMode.Essay);
                    Check(q.ElementaryProblem!.Answers.Single().Value==new ReducedFraction(adjacent&&inclusive?2:1,1),"Boundary PDF counting convention failed.");
                    items.Add(QuizWorksheetBuilder.Format(q,language));break;
                }
            }
            if(february)
            {
                var example=ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)SameMonthCalendarPilot.FebruaryCommonType,
                    CurriculumTier.OneStar,language,new(341),SameMonthCalendarPilot.Profiles(language,true,true,true).First()) with {OpenProse=true};
                for(int seed=0;;seed++)
                {
                    var data=SameMonthCalendarPilot.Create(example.SceneId,language,example.Tier,new(seed));
                    if(data.Year!=2027||data.StartDay!=26||data.EndDay!=3)continue;
                    example=example with {Story=example.Story! with {Seed=seed}};
                    var q=ReasoningStoryCatalogue.ToPractice(example,ReasoningStoryCatalogue.Draft(example),ArithmeticQuizMode.Essay);
                    Check(q.ElementaryProblem!.Answers.Single().Value==new ReducedFraction(6,1),"26 February 2027 to 3 March 2027 must count six days.");
                    items.Add(QuizWorksheetBuilder.Format(q,language));break;
                }
            }
            var sheet=new QuizWorksheet(february?"CALENDAR-FEBRUARY-COMMON":adjacent?(inclusive?"CALENDAR-ADJACENT-INCLUSIVE":"CALENDAR-ADJACENT"):inclusive?"CALENDAR-INCLUSIVE":"CALENDAR-SAME-MONTH",february?(vi?"Tháng Hai sang tháng Ba (năm thường, tính cả hai đầu)":"February to March (common year, both endpoints)"):adjacent?(inclusive?(vi?"Số ngày qua tháng kế tiếp (tính cả hai đầu)":"Calendar days across adjacent months (both endpoints)"):(vi?"Số ngày qua tháng kế tiếp":"Elapsed days across adjacent months")):inclusive?(vi?"Số ngày tính cả hai đầu mốc":"Calendar days including both endpoints"):(vi?"Số ngày trong cùng tháng":"Elapsed days within one month"),
                inclusive?(vi?"Tính cả ngày đầu và ngày cuối":"Include both start and end dates"):(vi?"Không tính ngày đầu, có tính ngày cuối":"Exclude the start date, include the end date"),
                language,CurriculumTier.ThreeStars,ArithmeticQuizMode.Essay,items.AsReadOnly());
            string snapshot=JsonSerializer.Serialize(sheet);
            foreach(bool answers in new[]{false,true})
            {
                byte[] pdf=QuizPdfWriter.Write(sheet,answers);PdfAssertions.HasEmbeddedText(pdf);
                string text=PdfTestReader.ExtractText(pdf);
                Check(!text.Contains("{v")&&!text.Contains("{f"),"PDF contains calendar slots.");
                if(!answers)Check(!text.Contains(vi?"Đáp số:":"Answer:"),"Calendar worksheet leaked answers.");
                File.WriteAllBytes(Path.Combine(directory,$"calendar-{(february?"february-common":adjacent?(inclusive?"adjacent-inclusive":"adjacent"):inclusive?"inclusive":"same-month")}-{language}-{(answers?"answers":"worksheet")}.pdf"),pdf);
            }
            Check(snapshot==JsonSerializer.Serialize(sheet),"PDF resampled calendar dates.");
        }
        Console.WriteLine($"PASS {cases} calendar {(february?"February common-year":adjacent?"adjacent-month":inclusive?"inclusive":"same-month elapsed")} factory/mode cases; count rule, DateTime oracle, pending isolation, frozen bilingual worksheet/answer PDFs.");
    }
    private static void Check(bool value,string message)=>TestAssert.That(value,message);
}
