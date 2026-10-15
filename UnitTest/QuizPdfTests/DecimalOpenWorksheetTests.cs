using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;

internal static class DecimalOpenWorksheetTests
{
    private sealed class AlwaysBank:Random {public override int Next(int maxValue)=>maxValue-1;}
    internal static async Task Run(string directory)
    {
        int cases=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var items=new List<QuizPrintItem>();
            foreach(string relation in new[]{"combine","increase","remaining","groups","price","share","count"})
            {
                bool money=relation=="price"||language==AppLanguage.English&&relation!="groups";
                string id=relation+(money&&relation!="price"?"-money":"");
                var r=DecimalStoryPilot.Relations(language).Single(x=>x.Id==id);
                string context=money?"notebooks":"water";
                string profile=DecimalStoryPilot.Prefix+id+"/"+context;
                var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Decimal,(int)r.Type,CurriculumTier.ThreeStars,language,new(713),profile) with {OpenProse=true};
                var draft=ReasoningStoryCatalogue.Draft(c);
                var store=new QuestionBankStore(Path.Combine(directory,$"decimal-open-{language}-{relation}.db3"));
                var factory=new PracticeQuestionFactory(new(store,new AlwaysBank()));
                if(relation=="combine")
                {
                    string opening=language==AppLanguage.Vietnamese?"Trong buổi chuẩn bị mới, ":"In the new preparation session, ";
                    draft=draft with {Facts=draft.Facts!.Select((f,i)=>i==0?f with {Text=opening+f.Text}:f).ToArray()};
                    string raw=QuestionBankStore.SerializeDraft(draft);
                    Check(BasicQuestionValidator.Validate(raw,c).NeedsReview,"Unknown PDF prose did not await review.");
                    var pending=PendingQuestionReview.Create(c,draft,raw,"pdf-review-fixture","review");
                    Check(await store.QueueReviewAsync(pending),"Could not queue a PDF fixture.");
                    var before=await factory.CreateAsync(new(QuizProblemKind.Decimal,ElementaryType:r.Type),ArithmeticQuizMode.Essay,
                        new(c.Tier,false),language,null,PracticeQuestionFormat.WordProblem,false);
                    Check(before.ElementaryProblem!.StoryContextId?.StartsWith(DecimalStoryPilot.Prefix)!=true,"Pending decimal leaked into PDF selection.");
                    Check(await store.ApproveReviewAsync(pending.Id),"Explicit PDF fixture approval failed.");
                }
                else Check(await store.InsertAsync(new(c,draft,QuestionBankStore.SerializeDraft(draft),"verified-pdf-fixture",DateTime.UtcNow)),"PDF bank write failed.");
                foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var q=await factory.CreateAsync(new(QuizProblemKind.Decimal,ElementaryType:r.Type),mode,new(c.Tier,false),language,null,PracticeQuestionFormat.WordProblem,false);
                    Check(q.ElementaryProblem!.StoryContextId==profile&&q.ElementaryProblem.Reasoning!.Steps.Count==1,"PDF factory lost the pinned decimal relation.");
                    if(mode==ArithmeticQuizMode.Essay)items.Add(QuizWorksheetBuilder.Format(q,language));
                    cases++;
                }
                var numeric=await factory.CreateAsync(new(QuizProblemKind.Decimal,ElementaryType:r.Type),ArithmeticQuizMode.Essay,
                    new(c.Tier,false),language,null,PracticeQuestionFormat.Numeric,false);
                Check(numeric.ElementaryProblem!.IsNumericDecimalCalculation,"Decimal pilot replaced numeric practice.");
            }
            var sheet=new QuizWorksheet("DECIMAL-OPEN","Decimal word problems",language==AppLanguage.Vietnamese?"Bảy quan hệ toán một bước":"Seven one-step relationships",
                language,CurriculumTier.ThreeStars,ArithmeticQuizMode.Essay,items.AsReadOnly());
            string frozen=JsonSerializer.Serialize(sheet);
            foreach(bool answers in new[]{false,true})
            {
                byte[] pdf=QuizPdfWriter.Write(sheet,answers);PdfAssertions.HasEmbeddedText(pdf);
                string text=PdfTestReader.ExtractText(pdf);
                Check(text.Contains("Decimal word problems"),"Decimal PDF text missing.");
                Check(!System.Text.RegularExpressions.Regex.IsMatch(text,@"\{[fv]\d+\}"),"Decimal PDF contains unrendered variables.");
                File.WriteAllBytes(Path.Combine(directory,$"decimal-open-{language}-{(answers?"answers":"worksheet")}.pdf"),pdf);
            }
            Check(frozen==JsonSerializer.Serialize(sheet),"Decimal printing mutated facts.");
        }
        Console.WriteLine($"PASS {cases} decimal pilot factory/mode cases: seven relations, pending isolation, explicit approval, numeric isolation and frozen bilingual worksheet/answer PDFs.");
    }
    private static void Check(bool condition,string message)=>TestAssert.That(condition,message);
}
