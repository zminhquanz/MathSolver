using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;

internal static class OvernightTimeWorksheetTests
{
    private sealed class AlwaysBank:Random {public override int Next(int maxValue)=>maxValue-1;}
    internal static async Task Run(string directory)
    {
        int cases=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            bool vi=language==AppLanguage.Vietnamese;var items=new List<QuizPrintItem>();
            foreach(var context in OvernightTimePilot.Contexts(language))
            foreach(var tier in Enum.GetValues<CurriculumTier>())
            {
                var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)OvernightTimePilot.Type,tier,language,new(1813),OvernightTimePilot.Prefix+context.Id) with {OpenProse=true};
                var d=ReasoningStoryCatalogue.Draft(c) with {Facts=context.AlternativeFacts.Select((s,i)=>new NarrativeClause("fact_"+i,s)).ToArray(),Question=context.AlternativeQuestion};
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c).IsValid,"PDF overnight fixture not verified.");
                var store=new QuestionBankStore(Path.Combine(directory,$"overnight-{language}-{context.Id}-{tier}.db3"));
                var pending=d with {Question=vi?"{v1} của {v0} kéo dài bao lâu?":"How long does {v0}'s {v1} last?"};
                string raw=QuestionBankStore.SerializeDraft(pending);
                Check(BasicQuestionValidator.Validate(raw,c).NeedsReview,"Unspecified duration unit should review.");
                Check(await store.QueueReviewAsync(PendingQuestionReview.Create(c,pending,raw,"synthetic-pdf","review")),"Cannot queue pending break.");
                Check(await store.TakeReasoningAsync(c.Family,c.BankVariant,tier,language) is null,"Pending break entered PDF bank.");
                Check(await store.InsertAsync(new(c,d,QuestionBankStore.SerializeDraft(d),"verified-fixture",DateTime.UtcNow)),"Cannot save verified break.");
                var factory=new PracticeQuestionFactory(new(store,new AlwaysBank()));
                foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var q=await factory.CreateAsync(new(QuizProblemKind.Time,ElementaryType:OvernightTimePilot.Type),mode,new(tier,false),language,null,PracticeQuestionFormat.WordProblem,false);
                    var p=q.ElementaryProblem!;
                    Check(p.StoryContextId==c.SceneId&&p.Reasoning!.ScenarioId==OvernightTimePilot.Relation,"PDF fell back to another time relation.");
                    var givens=p.Reasoning!.Givens.ToDictionary(g=>g.Role,g=>int.Parse(g.Value));
                    int elapsed=24*60+(givens["end-hour"]-givens["start-hour"])*60+givens["end-minute"]-givens["start-minute"];
                    int day=givens["end-day-offset"];
                    Check(day==1&&elapsed>0&&elapsed<24*60&&p.Answers.Single().Value==new ReducedFraction(elapsed,1),"PDF lost the next-day offset.");
                    Check(p.Reasoning.Givens.Count==5&&p.Answers[0].Unit==(vi?"phút":"minutes"),"PDF lost roles or minutes.");
                    var printed=QuizWorksheetBuilder.Format(q,language);
                    Check(!printed.Prompt.Contains("{v")&&!printed.Prompt.Contains("{f"),"Unrendered break slots.");
                    Check(printed.Prompt.Contains(vi?"ngày hôm sau":"the next day",StringComparison.OrdinalIgnoreCase),"PDF lost the following-day condition.");
                    Check(printed.Solution.Contains(p.Answers[0].Unit),"PDF lost answer unit.");
                    if(mode==ArithmeticQuizMode.Essay)items.Add(printed);
                    cases++;
                }
            }
            var sheet=new QuizWorksheet("TIME-OVERNIGHT",vi?"Thời gian qua nửa đêm":"Elapsed time across midnight",
                vi?"Liên tục, không nghỉ, kết thúc ngày hôm sau; tính số phút trôi qua":"Continuous, no breaks, ends the following day; find elapsed minutes",
                language,CurriculumTier.ThreeStars,ArithmeticQuizMode.Essay,items.AsReadOnly());
            string snapshot=JsonSerializer.Serialize(sheet);
            foreach(bool answers in new[]{false,true})
            {
                byte[] pdf=QuizPdfWriter.Write(sheet,answers);PdfAssertions.HasEmbeddedText(pdf);
                string text=PdfTestReader.ExtractText(pdf);
                Check(!text.Contains("{v")&&!text.Contains("{f"),"PDF contains unrendered slots.");
                if(!answers)Check(!text.Contains(vi?"Đáp số:":"Answer:"),"Worksheet leaked answers.");
                File.WriteAllBytes(Path.Combine(directory,$"time-overnight-{language}-{(answers?"answers":"worksheet")}.pdf"),pdf);
            }
            Check(snapshot==JsonSerializer.Serialize(sheet),"PDF export resampled overnight clocks.");
        }
        Console.WriteLine($"PASS {cases} overnight factory/mode cases; exactly the following day, elapsed-minute answers, review isolation, frozen bilingual worksheet/answer PDFs.");
    }
    private static void Check(bool b,string message)=>TestAssert.That(b,message);
}
