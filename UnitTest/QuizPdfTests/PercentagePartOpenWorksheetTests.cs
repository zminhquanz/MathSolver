using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;

internal static class PercentagePartOpenWorksheetTests
{
    private sealed class AlwaysBank:Random {public override int Next(int maxValue)=>maxValue-1;}
    internal static async Task Run(string directory)
    {
        int cases=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var items=new List<QuizPrintItem>();
            foreach(string profile in PercentagePartPilot.Profiles(language))
            foreach(var tier in Enum.GetValues<CurriculumTier>())
            {
                var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Percentage,(int)PercentageQuizType.FindPercentageValue,tier,language,new(713),profile) with {OpenProse=true};
                var d=ReasoningStoryCatalogue.Draft(c);
                d=d with {Facts=[new("fact_0",language==AppLanguage.Vietnamese?"Trong kho của {v0} có {f0} {v1}.":"{f0} {v1} belong to {v0}."),d.Facts![1]]};
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c).IsValid,"Percentage PDF fixture not verified.");
                var store=new QuestionBankStore(Path.Combine(directory,$"percentage-part-{language}-{profile.Split('/')[1]}-{tier}.db3"));
                var pending=d with {Question=d.Question+(language==AppLanguage.Vietnamese?" Theo cách mới.":" In a new way.")};
                string raw=QuestionBankStore.SerializeDraft(pending);
                Check(BasicQuestionValidator.Validate(raw,c).NeedsReview,"Unknown percentage PDF must need review.");
                Check(await store.QueueReviewAsync(PendingQuestionReview.Create(c,pending,raw,"synthetic-pdf-fixture","review")),"Cannot queue percentage.");
                Check(await store.TakeReasoningAsync(c.Family,c.BankVariant,tier,language) is null,"Pending percentage entered PDF bank.");
                Check(await store.InsertAsync(new(c,d,QuestionBankStore.SerializeDraft(d),"verified-pdf-fixture",DateTime.UtcNow)),"Cannot insert verified percentage.");
                var factory=new PracticeQuestionFactory(new(store,new AlwaysBank()));
                foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var q=await factory.CreateAsync(new(QuizProblemKind.Percentage,PercentageType:PercentageQuizType.FindPercentageValue),mode,
                        new(tier,false),language,null,PracticeQuestionFormat.WordProblem,false);
                    var p=q.PercentageProblem!;
                    Check(p.StoryContextId==profile&&p.Facts.Count==2&&p.CombinedQuantities is null,"PDF lost the single percentage relation.");
                    Check(p.Facts[0]*p.Facts[1]%100==0&&p.CorrectAnswer==p.Facts[0]*p.Facts[1]/100,"PDF arithmetic differs from the known whole.");
                    var printed=QuizWorksheetBuilder.Format(q,language);
                    Check(printed.Prompt.Contains(p.Facts[1]+"%")&&printed.Solution.Contains(p.AnswerUnit),"PDF lost rate or subset unit.");
                    if(mode==ArithmeticQuizMode.Essay)items.Add(printed);
                    cases++;
                }
            }
            var sheet=new QuizWorksheet("PERCENTAGE-PART",language==AppLanguage.Vietnamese?"Tìm giá trị phần trăm":"Percentage of a quantity",
                language==AppLanguage.Vietnamese?"Biết lượng gốc và tỉ lệ, tìm một phần":"Known whole and percentage, find the subset count",
                language,CurriculumTier.ThreeStars,ArithmeticQuizMode.Essay,items.AsReadOnly());
            string snapshot=JsonSerializer.Serialize(sheet);
            foreach(bool answers in new[]{false,true})
            {
                byte[] pdf=QuizPdfWriter.Write(sheet,answers);PdfAssertions.HasEmbeddedText(pdf);
                string text=PdfTestReader.ExtractText(pdf);
                Check(!text.Contains("{f")&&!text.Contains("{v"),"Unbound percentage PDF variables.");
                if(!answers)Check(text.Contains('%'),"Worksheet lost its percentage notation: "+language);
                File.WriteAllBytes(Path.Combine(directory,$"percentage-part-{language}-{(answers?"answers":"worksheet")}.pdf"),pdf);
            }
            Check(snapshot==JsonSerializer.Serialize(sheet),"PDF printing changed percentage facts.");
        }
        Console.WriteLine($"PASS {cases} percentage-part factory/mode cases; exact integer subset, review isolation, frozen bilingual worksheet/answer PDFs.");
    }
    private static void Check(bool b,string message)=>TestAssert.That(b,message);
}
