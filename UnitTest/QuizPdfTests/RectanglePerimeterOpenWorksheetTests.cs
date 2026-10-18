using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;

internal static class RectanglePerimeterOpenWorksheetTests
{
    private sealed class AlwaysBank:Random { public override int Next(int maxValue)=>maxValue-1; }
    internal static async Task Run(string directory)
    {
        int cases=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var items=new List<QuizPrintItem>();
            foreach(string profile in RectanglePerimeterPilot.Profiles(language))
            foreach(var tier in Enum.GetValues<CurriculumTier>())
            {
                var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Geometry,RectanglePerimeterPilot.Variant,tier,language,new(713),profile) with {OpenProse=true};
                var d=ReasoningStoryCatalogue.Draft(c);
                bool vi=language==AppLanguage.Vietnamese;
                d=d with {Facts=[new("fact_0",vi?"{v0} mang hình dáng {v1}.":"{v0} is shaped like a {v1}."),
                    new("fact_1",vi?"Chiều dài của {v0} bằng {f0} {v2}.":"{v0} measures {f0} {v2} in length."),
                    new("fact_2",vi?"Chiều rộng của {v0} bằng {f1} {v2}.":"{v0} measures {f1} {v2} in width.")],
                    Question=vi?"Chu vi {v0} bằng bao nhiêu {v2}?":"What is {v0}'s perimeter, measured in {v2}?"};
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c).IsValid,"Rectangle PDF fixture not verified.");
                var store=new QuestionBankStore(Path.Combine(directory,$"rectangle-{language}-{profile.Split('/')[1]}-{tier}.db3"));
                var pending=d with {Question=(language==AppLanguage.Vietnamese?"Theo ghi chép, ":"According to the record, ")+d.Question};
                string raw=QuestionBankStore.SerializeDraft(pending);
                Check(BasicQuestionValidator.Validate(raw,c).NeedsReview,"Unknown geometry must review.");
                Check(await store.QueueReviewAsync(PendingQuestionReview.Create(c,pending,raw,"synthetic-pdf","review")),"Cannot queue rectangle.");
                Check(await store.TakeReasoningAsync(c.Family,c.BankVariant,tier,language) is null,"Pending geometry entered PDF bank.");
                Check(await store.InsertAsync(new(c,d,QuestionBankStore.SerializeDraft(d),"verified-pdf",DateTime.UtcNow)),"Cannot insert geometry.");
                var factory=new PracticeQuestionFactory(new(store,new AlwaysBank()));
                foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var q=await factory.CreateAsync(new(QuizProblemKind.Geometry,GeometryShape:GeometryQuizShape.Rectangle,GeometryMeasurement:GeometryMeasurement.Perimeter),
                        mode,new(tier,false),language,null,PracticeQuestionFormat.WordProblem,false);
                    var g=q.GeometryProblem!;
                    Check(g.Reasoning!.ScenarioId=="rectangle-perimeter-direct"&&g.Reasoning.HiddenDimensions.Count==0,"PDF lost direct-side profile.");
                    Check(g.CorrectAnswer==2*(g.Dimensions["a"]+g.Dimensions["b"])&&g.UnitPower==1,"PDF perimeter arithmetic/unit changed.");
                    var printed=QuizWorksheetBuilder.Format(q,language);
                    Check(printed.Solution.Contains('×')&&printed.Solution.Contains('+')&&printed.Solution.Contains(g.AnswerUnit),"PDF lost perimeter working/length unit: "+mode+" "+printed.Solution);
                    Check(!printed.Prompt.Contains("{v")&&!printed.Prompt.Contains("{f"),"Unbound geometry in PDF.");
                    if(mode==ArithmeticQuizMode.Essay)items.Add(printed);
                    cases++;
                }
            }
            var sheet=new QuizWorksheet("RECTANGLE-PERIMETER",language==AppLanguage.Vietnamese?"Chu vi hình chữ nhật":"Rectangle perimeter",
                language==AppLanguage.Vietnamese?"Biết trực tiếp chiều dài, chiều rộng; cùng đơn vị":"Directly known length and width in the same unit",
                language,CurriculumTier.ThreeStars,ArithmeticQuizMode.Essay,items.AsReadOnly());
            string snapshot=JsonSerializer.Serialize(sheet);
            foreach(bool answers in new[]{false,true})
            {
                byte[] pdf=QuizPdfWriter.Write(sheet,answers);PdfAssertions.HasEmbeddedText(pdf);
                string text=PdfTestReader.ExtractText(pdf);
                Check(!text.Contains("{v")&&!text.Contains("{f"),"PDF has unbound geometry slots.");
                if(!answers)Check(!text.Contains(language==AppLanguage.Vietnamese?"Đáp số:":"Answer:"),"Worksheet leaked answer.");
                File.WriteAllBytes(Path.Combine(directory,$"rectangle-perimeter-{language}-{(answers?"answers":"worksheet")}.pdf"),pdf);
            }
            Check(snapshot==JsonSerializer.Serialize(sheet),"PDF export changed frozen facts.");
        }
        Console.WriteLine($"PASS {cases} rectangle-perimeter factory/mode cases; direct dimensions, length units, review isolation, frozen bilingual worksheet/answer PDFs.");
    }
    private static void Check(bool b,string message)=>TestAssert.That(b,message);
}
