using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;

internal static class UnitaryTotalWorksheetTests
{
    private sealed class AlwaysBank:Random {public override int Next(int maxValue)=>maxValue-1;}
    internal static async Task Run(string directory)
    {
        int count=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var items=new List<QuizPrintItem>();
            foreach(string profile in UnitaryTotalPilot.Profiles(language))
            foreach(var tier in Enum.GetValues<CurriculumTier>())
            {
                var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Proportion,(int)ProportionQuizType.Direct,tier,language,new(713),profile) with {OpenProse=true};
                var d=ReasoningStoryCatalogue.Draft(c);var data=UnitaryTotalPilot.Create(profile,language,tier,new(c.Story!.Seed));
                var store=new QuestionBankStore(Path.Combine(directory,$"unitary-{language}-{profile.Split('/')[1]}-{tier}.db3"));
                var unknown=d with {Question=d.Question+(language==AppLanguage.Vietnamese?" Theo cách mới.":" In a new way.")};
                string raw=QuestionBankStore.SerializeDraft(unknown);
                Check(BasicQuestionValidator.Validate(raw,c).NeedsReview,"Unknown PDF prose not pending.");
                Check(await store.QueueReviewAsync(PendingQuestionReview.Create(c,unknown,raw,"pdf-fixture","review")),"Cannot queue PDF prose.");
                Check(await store.TakeReasoningAsync(c.Family,c.BankVariant,tier,language) is null,"Pending prose leaked into PDF.");
                Check(await store.InsertAsync(new(c,d,QuestionBankStore.SerializeDraft(d),"pdf-fixture",DateTime.UtcNow)),"Cannot store verified PDF fixture.");
                var factory=new PracticeQuestionFactory(new(store,new AlwaysBank()));
                foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var q=await factory.CreateAsync(new(QuizProblemKind.Proportion,ProportionType:ProportionQuizType.Direct),mode,new(tier,false),language,null,PracticeQuestionFormat.WordProblem,false);
                    var p=q.ProportionProblem!;
                    Check(p.NarrativeId==profile&&p.Type==ProportionQuizType.Direct&&p.B%p.A==0&&p.CorrectAnswer==p.B/p.A*p.C,"Factory changed unitary roles.");
                    var printed=QuizWorksheetBuilder.Format(q,language);
                    Check(printed.Prompt.Contains(data.Context.Groups)&&printed.Prompt.Contains(data.Context.Unit),"Missing group/item units.");
                    Check(printed.Solution.Contains($"{p.B} ÷ {p.A} = {p.B/p.A}")&&printed.Solution.Contains($"{p.B/p.A} × {p.C} = {p.CorrectAnswer}"),"PDF lost two C# steps.");
                    Check(!printed.Prompt.Contains(language==AppLanguage.Vietnamese?"Đáp số:":"Answer:"),"PDF prompt leaked answer.");
                    if(mode==ArithmeticQuizMode.Essay)items.Add(printed);
                    count++;
                }
            }
            var sheet=new QuizWorksheet("UNITARY-TOTAL",language==AppLanguage.Vietnamese?"Rút về đơn vị":"Unitary method",
                language==AppLanguage.Vietnamese?"Tìm tổng lượng của số nhóm mới":"Find the total for a new group count",language,CurriculumTier.ThreeStars,ArithmeticQuizMode.Essay,items.AsReadOnly());
            string snapshot=JsonSerializer.Serialize(sheet);
            foreach(bool answers in new[]{false,true})
            {
                byte[] pdf=QuizPdfWriter.Write(sheet,answers);PdfAssertions.HasEmbeddedText(pdf);
                string text=PdfTestReader.ExtractText(pdf);Check(!text.Contains("{f")&&!text.Contains("{v"),"Unrendered PDF slots.");
                File.WriteAllBytes(Path.Combine(directory,$"unitary-total-{language}-{(answers?"answers":"worksheet")}.pdf"),pdf);
            }
            Check(snapshot==JsonSerializer.Serialize(sheet),"PDF mutated frozen worksheet.");
        }
        Console.WriteLine($"PASS {count} unitary-total factory/mode/PDF cases, two C# steps, unchanged data, bilingual PDFs, pending isolation.");
    }
    private static void Check(bool value,string message)=>TestAssert.That(value,message);
}
