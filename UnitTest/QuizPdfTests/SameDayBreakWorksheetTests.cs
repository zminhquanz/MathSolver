using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;

internal static class SameDayBreakWorksheetTests
{
    private sealed class AlwaysBank:Random {public override int Next(int maxValue)=>maxValue-1;}
    internal static async Task Run(string directory)
    {
        int cases=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            bool vi=language==AppLanguage.Vietnamese;var items=new List<QuizPrintItem>();
            foreach(var context in SameDayBreakPilot.Contexts(language))
            foreach(var tier in Enum.GetValues<CurriculumTier>())
            {
                var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)SameDayBreakPilot.Type,tier,language,new(1813),SameDayBreakPilot.Prefix+context.Id) with {OpenProse=true};
                var d=ReasoningStoryCatalogue.Draft(c) with {Facts=context.AlternativeFacts.Select((s,i)=>new NarrativeClause("fact_"+i,s)).ToArray(),Question=context.AlternativeQuestion};
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c).IsValid,"PDF break fixture not verified.");
                var store=new QuestionBankStore(Path.Combine(directory,$"break-{language}-{context.Id}-{tier}.db3"));
                var pending=d with {Question=vi?"{v1} của {v0} kéo dài bao nhiêu phút?":"How long does {v0}'s {v1} last in minutes?"};
                string raw=QuestionBankStore.SerializeDraft(pending);
                Check(BasicQuestionValidator.Validate(raw,c).NeedsReview,"Unknown active target should review.");
                Check(await store.QueueReviewAsync(PendingQuestionReview.Create(c,pending,raw,"synthetic-pdf","review")),"Cannot queue pending break.");
                Check(await store.TakeReasoningAsync(c.Family,c.BankVariant,tier,language) is null,"Pending break entered PDF bank.");
                Check(await store.InsertAsync(new(c,d,QuestionBankStore.SerializeDraft(d),"verified-fixture",DateTime.UtcNow)),"Cannot save verified break.");
                var factory=new PracticeQuestionFactory(new(store,new AlwaysBank()));
                foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var q=await factory.CreateAsync(new(QuizProblemKind.Time,ElementaryType:SameDayBreakPilot.Type),mode,new(tier,false),language,null,PracticeQuestionFormat.WordProblem,false);
                    var p=q.ElementaryProblem!;
                    Check(p.StoryContextId==c.SceneId&&p.Reasoning!.ScenarioId==SameDayBreakPilot.Relation,"PDF fell back to another time relation.");
                    var givens=p.Reasoning!.Givens.ToDictionary(g=>g.Role,g=>int.Parse(g.Value));
                    int elapsed=(givens["end-hour"]-givens["start-hour"])*60+givens["end-minute"]-givens["start-minute"];
                    int rest=givens["included-break-minutes"];
                    Check(elapsed>rest&&rest>0&&p.Answers.Single().Value==new ReducedFraction(elapsed-rest,1),"PDF did not subtract included break.");
                    Check(p.Reasoning.Givens.Count==5&&p.Answers[0].Unit==(vi?"phút":"minutes"),"PDF lost roles or minutes.");
                    var printed=QuizWorksheetBuilder.Format(q,language);
                    Check(!printed.Prompt.Contains("{v")&&!printed.Prompt.Contains("{f"),"Unrendered break slots.");
                    Check(printed.Prompt.Contains(vi?"không tính nghỉ":"excluding the break",StringComparison.OrdinalIgnoreCase),"PDF lost exclusion condition.");
                    Check(printed.Solution.Contains(p.Answers[0].Unit),"PDF lost answer unit.");
                    if(mode==ArithmeticQuizMode.Essay)items.Add(printed);
                    cases++;
                }
            }
            var sheet=new QuizWorksheet("TIME-BREAK",vi?"Thời gian có nghỉ trong ngày":"Active time with one break",
                vi?"Cùng ngày, một lần nghỉ trong buổi; tính số phút không tính nghỉ":"Same day, one included break; find active minutes",
                language,CurriculumTier.ThreeStars,ArithmeticQuizMode.Essay,items.AsReadOnly());
            string snapshot=JsonSerializer.Serialize(sheet);
            foreach(bool answers in new[]{false,true})
            {
                byte[] pdf=QuizPdfWriter.Write(sheet,answers);PdfAssertions.HasEmbeddedText(pdf);
                string text=PdfTestReader.ExtractText(pdf);
                Check(!text.Contains("{v")&&!text.Contains("{f"),"PDF contains unrendered slots.");
                if(!answers)Check(!text.Contains(vi?"Đáp số:":"Answer:"),"Worksheet leaked answers.");
                File.WriteAllBytes(Path.Combine(directory,$"time-break-{language}-{(answers?"answers":"worksheet")}.pdf"),pdf);
            }
            Check(snapshot==JsonSerializer.Serialize(sheet),"PDF export resampled clocks or break.");
        }
        Console.WriteLine($"PASS {cases} one-break factory/mode cases; positive included break, same-day active-time answers, review isolation, frozen bilingual worksheet/answer PDFs.");
    }
    private static void Check(bool b,string message)=>TestAssert.That(b,message);
}
