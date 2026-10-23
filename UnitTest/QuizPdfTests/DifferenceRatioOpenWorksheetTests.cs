using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;

internal static class DifferenceRatioOpenWorksheetTests
{
    private sealed class AlwaysBank:Random {public override int Next(int maxValue)=>maxValue-1;}
    internal static async Task Run(string directory)
    {
        int cases=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var items=new List<QuizPrintItem>();
            foreach(string profile in DifferenceRatioPilot.Profiles(language))
            foreach(var tier in Enum.GetValues<CurriculumTier>())
            {
                var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.TwoNumbers,(int)ElementaryQuizType.DifferenceRatio,tier,language,new(713),profile) with {OpenProse=true};
                var d=ReasoningStoryCatalogue.Draft(c);
                d=d with {Facts=[new("fact_0",language==AppLanguage.Vietnamese?"{v1} hơn {v0} {f0} {v2}.":"{v1} holds {f0} more {v2} than {v0}."),
                    new("fact_1",language==AppLanguage.Vietnamese?"{v0} có số {v2} bằng {f1} số {v2} của {v1}.":"{v0}'s number of {v2} is {f1} of {v1}'s number of {v2}.")]};
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c).IsValid,"PDF fixture not verified.");
                var store=new QuestionBankStore(Path.Combine(directory,$"difference-ratio-{language}-{profile.Split('/')[1]}-{tier}.db3"));
                var unknown=d with {Question=d.Question+(language==AppLanguage.Vietnamese?" Theo cách mới.":" In a new way.")};
                string raw=QuestionBankStore.SerializeDraft(unknown);
                Check(BasicQuestionValidator.Validate(raw,c).NeedsReview,"Unknown PDF fixture did not await review.");
                Check(await store.QueueReviewAsync(PendingQuestionReview.Create(c,unknown,raw,"pdf-fixture","review")),"Cannot queue draft.");
                Check(await store.TakeReasoningAsync(c.Family,c.BankVariant,tier,language) is null,"Pending draft leaked into PDF.");
                Check(await store.InsertAsync(new(c,d,QuestionBankStore.SerializeDraft(d),"verified-pdf-fixture",DateTime.UtcNow)),"Cannot insert verified draft.");
                var factory=new PracticeQuestionFactory(new(store,new AlwaysBank()));
                foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var q=await factory.CreateAsync(new(QuizProblemKind.TwoNumbers,ElementaryType:ElementaryQuizType.DifferenceRatio),mode,new(tier,false),language,null,PracticeQuestionFormat.WordProblem,false);
                    var p=q.ElementaryProblem!;
                    Check(p.StoryContextId==profile&&p.Reasoning!.Steps.Count==3,"PDF factory lost direct profile.");
                    var data=DifferenceRatioPilot.Contexts(language).Single(x=>DifferenceRatioPilot.Prefix+x.Id==profile);
                    int small=(int)p.Answers[0].Value.Numerator,large=(int)p.Answers[1].Value.Numerator;
                    var given=p.Reasoning!.Givens.ToDictionary(g=>g.Role,g=>int.Parse(g.Value));
                    Check(p.Answers[0].Label==data.SmallerOwner&&p.Answers[1].Label==data.LargerOwner
                        &&large-small==given["larger-minus-smaller-difference"]&&small*given["larger-ratio-parts"]==large*given["smaller-ratio-parts"],"PDF difference/ratio or owner roles changed.");
                    var printed=QuizWorksheetBuilder.Format(q,language);
                    Check(printed.Prompt.Contains(data.LargerOwner)&&printed.Prompt.Contains(data.SmallerOwner)&&printed.Solution.Contains(data.Unit),"PDF lost owners/units.");
                    Check(printed.Diagram!.Rows[0].Label==data.SmallerOwner&&printed.Diagram.Rows[1].Label==data.LargerOwner
                        &&printed.Diagram.Rows[0].Segments.Count==given["smaller-ratio-parts"]
                        &&printed.Diagram.Rows[1].Segments.Count==given["larger-ratio-parts"]&&printed.Diagram.Rows.SelectMany(r=>r.Segments).All(s=>s.Text=="?"),"Diagram lost smaller/larger roles or exposed answers.");
                    Check(!printed.Prompt.Contains(language==AppLanguage.Vietnamese?"Đáp số:":"Answer:"),"Prompt leaked answer label.");
                    if(mode==ArithmeticQuizMode.Essay)items.Add(printed);
                    cases++;
                }
            }
            var sheet=new QuizWorksheet("DIFFERENCE-RATIO-OPEN",language==AppLanguage.Vietnamese?"Hai số hiệu–tỉ":"Difference and ratio",
                language==AppLanguage.Vietnamese?"Biết hiệu và tỉ số trực tiếp, tìm lượng của từng bên":"Direct difference and oriented ratio: find both amounts",
                language,CurriculumTier.ThreeStars,ArithmeticQuizMode.Essay,items.AsReadOnly());
            string snapshot=JsonSerializer.Serialize(sheet);
            foreach(bool answers in new[]{false,true})
            {
                byte[] pdf=QuizPdfWriter.Write(sheet,answers);PdfAssertions.HasEmbeddedText(pdf);
                string text=PdfTestReader.ExtractText(pdf);
                Check(!text.Contains("{v")&&!text.Contains("{f"),"PDF has unbound variables.");
                File.WriteAllBytes(Path.Combine(directory,$"difference-ratio-open-{language}-{(answers?"answers":"worksheet")}.pdf"),pdf);
            }
            Check(snapshot==JsonSerializer.Serialize(sheet),"Printing changed frozen data.");
        }
        Console.WriteLine($"PASS {cases} difference/ratio factory/mode cases; owner-bound sums/ratios, diagrams, pending isolation, bilingual frozen PDFs.");
    }
    private static void Check(bool b,string message)=>TestAssert.That(b,message);
}
