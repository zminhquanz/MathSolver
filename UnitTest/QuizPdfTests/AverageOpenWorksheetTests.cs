using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;

internal static class AverageOpenWorksheetTests
{
    private sealed class AlwaysBank:Random {public override int Next(int maxValue)=>maxValue-1;}
    internal static async Task Run(string directory)
    {
        int cases=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var items=new List<QuizPrintItem>();
            foreach(string profile in AverageOpenPilot.Profiles(language))
            foreach(var tier in Enum.GetValues<CurriculumTier>())
            {
                var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Average,(int)AverageQuizType.Direct,tier,language,new(713),profile) with {OpenProse=true};
                var d=ReasoningStoryCatalogue.Draft(c);
                d=d with {Facts=d.Facts!.Select((f,i)=>i>=3?f:f with {Text=language==AppLanguage.Vietnamese
                    ?"Số {v3} do {v"+i+"} {v5} là {f"+i+"}."
                    :"{f"+i+"} {v3} were {v5} by {v"+i+"}."}).ToArray()};
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c).IsValid,"Average PDF fixture not verified.");
                var store=new QuestionBankStore(Path.Combine(directory,$"average-open-{language}-{profile.Split('/')[1]}-{tier}.db3"));
                var unknown=d with {Question=d.Question+(language==AppLanguage.Vietnamese?" Theo cách mới.":" In a new way.")};
                string raw=QuestionBankStore.SerializeDraft(unknown);
                Check(BasicQuestionValidator.Validate(raw,c).NeedsReview,"Unknown PDF fixture did not need review.");
                Check(await store.QueueReviewAsync(PendingQuestionReview.Create(c,unknown,raw,"synthetic-pdf-fixture","review")),"Cannot queue average.");
                Check(await store.TakeReasoningAsync(c.Family,c.BankVariant,tier,language) is null,"Pending average entered active bank.");
                Check(await store.InsertAsync(new(c,d,QuestionBankStore.SerializeDraft(d),"verified-pdf-fixture",DateTime.UtcNow)),"Cannot insert average.");
                var factory=new PracticeQuestionFactory(new(store,new AlwaysBank()));
                foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var q=await factory.CreateAsync(new(QuizProblemKind.Average,AverageType:AverageQuizType.Direct),mode,new(tier,false),language,null,PracticeQuestionFormat.WordProblem,false);
                    var p=q.AverageProblem!;
                    Check(p.StoryContextId==profile&&p.Facts.Count==4&&p.Facts[0]==3,"PDF factory lost complete independent observations.");
                    Check(p.CorrectAnswer==p.Facts.Skip(1).Sum()/3,"PDF answer differs from independent average.");
                    var printed=QuizWorksheetBuilder.Format(q,language);
                    Check(printed.Solution.Contains(p.AnswerUnit),"PDF solution lost units.");
                    if(mode==ArithmeticQuizMode.Essay)items.Add(printed);
                    cases++;
                }
            }
            var sheet=new QuizWorksheet("AVERAGE-OPEN",language==AppLanguage.Vietnamese?"Trung bình cộng":"Arithmetic mean",
                language==AppLanguage.Vietnamese?"Các nhóm độc lập, biết đủ số liệu":"Independent groups with complete observations",
                language,CurriculumTier.ThreeStars,ArithmeticQuizMode.Essay,items.AsReadOnly());
            string snapshot=JsonSerializer.Serialize(sheet);
            foreach(bool answers in new[]{false,true})
            {
                byte[] pdf=QuizPdfWriter.Write(sheet,answers);PdfAssertions.HasEmbeddedText(pdf);
                string text=PdfTestReader.ExtractText(pdf);
                Check(!text.Contains("{f")&&!text.Contains("{v"),"Unbound average PDF variables.");
                Check(text.Contains(language==AppLanguage.Vietnamese?"Trung bình":"Arithmetic mean"),"Average PDF title missing.");
                File.WriteAllBytes(Path.Combine(directory,$"average-open-{language}-{(answers?"answers":"worksheet")}.pdf"),pdf);
            }
            Check(snapshot==JsonSerializer.Serialize(sheet),"Printing changed mean facts.");
        }
        Console.WriteLine($"PASS {cases} average-open factory/mode cases; independent means, review isolation, bilingual frozen worksheet/answer PDFs.");
    }
    private static void Check(bool b,string message)=>TestAssert.That(b,message);
}
