using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;

internal static class SameDayTimeWorksheetTests
{
    private sealed class AlwaysBank:Random{public override int Next(int maxValue)=>maxValue-1;}
    internal static async Task Run(string directory)
    {
        int cases=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var items=new List<QuizPrintItem>();
            foreach(var relation in SameDayTimePilot.Relations(language))
            foreach(var context in SameDayTimePilot.Contexts(language))
            {
                string profile=SameDayTimePilot.Prefix+relation.Id+"/"+context.Id;
                var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Time,(int)relation.Type,CurriculumTier.ThreeStars,language,new(713),profile) with {OpenProse=true};
                var draft=ReasoningStoryCatalogue.Draft(c);
                string first=language==AppLanguage.Vietnamese
                    ? $"Vào lúc {{f0}} giờ {{f1}} phút, {{v1}} của {{v0}} {(relation.Id=="start"?"kết thúc":"bắt đầu")}."
                    : $"At {{f0}} hours {{f1}} minutes, {{v0}}'s {{v1}} {(relation.Id=="start"?"finishes":"begins")}.";
                draft=draft with {Facts=draft.Facts!.Select((f,i)=>i==0?f with {Text=first}:f).ToArray()};
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft),c).IsValid,"New time phrasing failed meaning verification before PDF.");
                var store=new QuestionBankStore(Path.Combine(directory,$"time-open-{language}-{relation.Id}-{context.Id}.db3"));
                if(relation.Id=="elapsed")
                {
                    var unknown=draft with {Facts=draft.Facts!.Select((f,i)=>i==0?f with {Text=(language==AppLanguage.Vietnamese?"Theo một ghi chép mới, ":"From a new record, ")+f.Text}:f).ToArray()};
                    string raw=QuestionBankStore.SerializeDraft(unknown);
                    Check(BasicQuestionValidator.Validate(raw,c).NeedsReview,"Unknown time prose not queued.");
                    var pending=PendingQuestionReview.Create(c,unknown,raw,"synthetic-pdf-fixture","review");
                    Check(await store.QueueReviewAsync(pending),"Cannot queue time PDF fixture.");
                    Check(await store.TakeReasoningAsync(BankQuestionFamily.Time,c.BankVariant,c.Tier,language) is null,"Pending time leaked to PDF.");
                    Check(await store.ApproveReviewAsync(pending.Id),"Explicit time PDF approval failed.");
                }
                else Check(await store.InsertAsync(new(c,draft,QuestionBankStore.SerializeDraft(draft),"verified-fixture",DateTime.UtcNow)),"Cannot insert time PDF fixture.");
                var factory=new PracticeQuestionFactory(new(store,new AlwaysBank()));
                foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var q=await factory.CreateAsync(new(QuizProblemKind.Time,ElementaryType:relation.Type),mode,new(c.Tier,false),language,null,PracticeQuestionFormat.WordProblem,false);
                    Check(q.ElementaryProblem!.StoryContextId==profile,"Time PDF lost stored relation.");
                    var printed=QuizWorksheetBuilder.Format(q,language);
                    Check(printed.Solution.Contains(language==AppLanguage.Vietnamese?"phút":"minutes"),"Time solution missing units.");
                    if(mode==ArithmeticQuizMode.Essay)items.Add(printed);
                    cases++;
                }
            }
            var sheet=new QuizWorksheet("TIME-OPEN",language==AppLanguage.Vietnamese?"Thời gian trong cùng ngày":"Time within one day",
                language==AppLanguage.Vietnamese?"Tìm khoảng thời gian, giờ bắt đầu và giờ kết thúc":"Duration, start time and end time",
                language,CurriculumTier.ThreeStars,ArithmeticQuizMode.Essay,items.AsReadOnly());
            string snapshot=JsonSerializer.Serialize(sheet);
            foreach(bool answers in new[]{false,true})
            {
                byte[] pdf=QuizPdfWriter.Write(sheet,answers);PdfAssertions.HasEmbeddedText(pdf);
                string text=PdfTestReader.ExtractText(pdf);
                Check(!text.Contains("{f")&&!text.Contains("{v"),"Unbound time variables in PDF.");
                Check(text.Contains(language==AppLanguage.Vietnamese?"Thời gian":"Time"),"Time PDF title missing.");
                File.WriteAllBytes(Path.Combine(directory,$"time-open-{language}-{(answers?"answers":"worksheet")}.pdf"),pdf);
            }
            Check(snapshot==JsonSerializer.Serialize(sheet),"Printing resampled time facts.");
        }
        Console.WriteLine($"PASS {cases} same-day time factory/mode cases; three targets, review isolation, fresh facts and frozen bilingual PDF worksheets/answers.");
    }
    private static void Check(bool condition,string message)=>TestAssert.That(condition,message);
}
