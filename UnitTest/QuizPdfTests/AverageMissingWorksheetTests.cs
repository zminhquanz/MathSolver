using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;

internal static class AverageMissingWorksheetTests
{
    private sealed class AlwaysBank:Random {public override int Next(int maxValue)=>maxValue-1;}
    internal static async Task Run(string directory)
    {
        int cases=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var items=new List<QuizPrintItem>();
            foreach(string profile in AverageMissingPilot.Profiles(language))
            foreach(var tier in Enum.GetValues<CurriculumTier>())
            {
                var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Average,(int)AverageQuizType.MissingValue,tier,language,new(713),profile) with {OpenProse=true};
                var d=ReasoningStoryCatalogue.Draft(c);
                d=d with {Facts=d.Facts!.Select((f,i)=>i>=2?f:f with {Text=language==AppLanguage.Vietnamese
                    ?"Số {v3} do {v"+i+"} {v5} là {f"+i+"}."
                    :"{f"+i+"} {v3} were {v5} by {v"+i+"}."}).ToArray()};
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c).IsValid,"Average PDF fixture not verified.");
                var store=new QuestionBankStore(Path.Combine(directory,$"average-missing-open-{language}-{profile.Split('/')[1]}-{tier}.db3"));
                var unknown=d with {Question=d.Question+(language==AppLanguage.Vietnamese?" Theo cách mới.":" In a new way.")};
                string raw=QuestionBankStore.SerializeDraft(unknown);
                Check(BasicQuestionValidator.Validate(raw,c).NeedsReview,"Unknown PDF fixture did not need review.");
                Check(await store.QueueReviewAsync(PendingQuestionReview.Create(c,unknown,raw,"synthetic-pdf-fixture","review")),"Cannot queue average.");
                Check(await store.TakeReasoningAsync(c.Family,c.BankVariant,tier,language) is null,"Pending average entered active bank.");
                Check(await store.InsertAsync(new(c,d,QuestionBankStore.SerializeDraft(d),"verified-pdf-fixture",DateTime.UtcNow)),"Cannot insert average.");
                var factory=new PracticeQuestionFactory(new(store,new AlwaysBank()));
                foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var q=await factory.CreateAsync(new(QuizProblemKind.Average,AverageType:AverageQuizType.MissingValue),mode,new(tier,false),language,null,PracticeQuestionFormat.WordProblem,false);
                    var p=q.AverageProblem!;
                    Check(p.StoryContextId==profile&&p.Facts.Count==4&&p.Facts[0]==3,"PDF factory lost mean and two known observations.");
                    Check(p.CorrectAnswer==p.Facts[1]*3-p.Facts[2]-p.Facts[3],"PDF answer differs from independent missing-observation arithmetic.");
                    var printed=QuizWorksheetBuilder.Format(q,language);
                    Check(printed.Solution.Contains(p.AnswerUnit),"PDF solution lost units.");
                    Check(!printed.Prompt.Contains(p.EquationText)&&!printed.Prompt.Contains(language==AppLanguage.Vietnamese?"Đáp số:":"Answer:"),"Worksheet leaked the worked answer.");
                    if(mode==ArithmeticQuizMode.Essay)items.Add(printed);
                    cases++;
                }
            }
            var sheet=new QuizWorksheet("AVERAGE-MISSING-OPEN",language==AppLanguage.Vietnamese?"Trung bình cộng":"Arithmetic mean",
                language==AppLanguage.Vietnamese?"Ba nhóm độc lập: biết trung bình và hai lượng, tìm lượng còn lại":"Three independent groups: mean and two known observations",
                language,CurriculumTier.ThreeStars,ArithmeticQuizMode.Essay,items.AsReadOnly());
            string snapshot=JsonSerializer.Serialize(sheet);
            foreach(bool answers in new[]{false,true})
            {
                byte[] pdf=QuizPdfWriter.Write(sheet,answers);PdfAssertions.HasEmbeddedText(pdf);
                string text=PdfTestReader.ExtractText(pdf);
                Check(!text.Contains("{f")&&!text.Contains("{v"),"Unbound average PDF variables.");
                Check(text.Contains(language==AppLanguage.Vietnamese?"Trung bình":"Arithmetic mean"),"Average PDF title missing.");
                File.WriteAllBytes(Path.Combine(directory,$"average-missing-open-{language}-{(answers?"answers":"worksheet")}.pdf"),pdf);
            }
            Check(snapshot==JsonSerializer.Serialize(sheet),"Printing changed mean facts.");
        }
        Console.WriteLine($"PASS {cases} average-missing-open factory/mode cases; missing observations, review isolation, bilingual frozen worksheet/answer PDFs.");
    }
    private static void Check(bool b,string message)=>TestAssert.That(b,message);
}
