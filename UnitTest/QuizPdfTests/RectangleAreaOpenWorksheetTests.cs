using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;

internal static class RectangleAreaOpenWorksheetTests
{
    private sealed class AlwaysBank:Random { public override int Next(int maxValue)=>maxValue-1; }
    internal static async Task Run(string directory)
    {
        int cases=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var items=new List<QuizPrintItem>();
            foreach(string profile in RectangleAreaPilot.Profiles(language))
            foreach(var tier in Enum.GetValues<CurriculumTier>())
            {
                bool vi=language==AppLanguage.Vietnamese;
                var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Geometry,RectangleAreaPilot.Variant,tier,language,new(813),profile) with {OpenProse=true};
                var d=ReasoningStoryCatalogue.Draft(c);
                d=d with {Facts=[new("fact_0",vi?"{v0} mang hình dáng {v1}.":"{v0} is shaped like a {v1}."),
                    new("fact_1",vi?"Cạnh dài của {v0} bằng {f0} {v2}.":"{v0} measures {f0} {v2} in length."),
                    new("fact_2",vi?"Cạnh rộng của {v0} bằng {f1} {v2}.":"{v0} measures {f1} {v2} in width.")],
                    Question=vi?"Diện tích {v0} bằng bao nhiêu {v3}?":"What is {v0}'s area in {v3}?"};
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c).IsValid,"Area PDF fixture not verified.");
                var store=new QuestionBankStore(Path.Combine(directory,$"area-{language}-{profile.Split('/')[1]}-{tier}.db3"));
                var pending=d with {Question=(vi?"Theo ghi chép, ":"According to the record, ")+d.Question};
                string raw=QuestionBankStore.SerializeDraft(pending);
                Check(BasicQuestionValidator.Validate(raw,c).NeedsReview,"Unknown area must review.");
                Check(await store.QueueReviewAsync(PendingQuestionReview.Create(c,pending,raw,"synthetic-pdf","review")),"Cannot queue area review.");
                Check(await store.TakeReasoningAsync(c.Family,c.BankVariant,tier,language) is null,"Pending area entered PDF bank.");
                Check(await store.InsertAsync(new(c,d,QuestionBankStore.SerializeDraft(d),"verified-pdf",DateTime.UtcNow)),"Cannot insert area.");
                var factory=new PracticeQuestionFactory(new(store,new AlwaysBank()));
                foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var q=await factory.CreateAsync(new(QuizProblemKind.Geometry,GeometryShape:GeometryQuizShape.Rectangle,GeometryMeasurement:GeometryMeasurement.Area),
                        mode,new(tier,false),language,null,PracticeQuestionFormat.WordProblem,false);
                    var g=q.GeometryProblem!;
                    Check(g.Reasoning!.ScenarioId=="rectangle-area-direct"&&g.Reasoning.HiddenDimensions.Count==0&&g.Reasoning.Steps.Count==0,"PDF area inferred a side.");
                    Check(g.CorrectAnswer==g.Dimensions["a"]*g.Dimensions["b"]&&g.UnitPower==2,"PDF area arithmetic/unit changed.");
                    Check(g.Reasoning.Givens.All(x=>x.Unit==g.LengthUnitSymbol)&&g.AnswerUnit==g.LengthUnitSymbol+"²","PDF merged side/answer units.");
                    var printed=QuizWorksheetBuilder.Format(q,language);
                    Check(printed.Solution.Contains('×')&&!printed.Solution.Contains('+')&&printed.Solution.Contains(g.AnswerUnit),"PDF lost direct area working/squared unit.");
                    Check(printed.Prompt.Contains(g.LengthUnitSymbol)&&printed.Prompt.Contains(g.AnswerUnit),"PDF prompt lost one unit role.");
                    Check(!printed.Prompt.Contains("{v")&&!printed.Prompt.Contains("{f"),"Unbound area slots in PDF.");
                    if(mode==ArithmeticQuizMode.Essay)items.Add(printed);
                    cases++;
                }
            }
            var sheet=new QuizWorksheet("RECTANGLE-AREA",language==AppLanguage.Vietnamese?"Diện tích hình chữ nhật":"Rectangle area",
                language==AppLanguage.Vietnamese?"Biết trực tiếp chiều dài, chiều rộng; kết quả dùng đơn vị diện tích":"Direct length and width; result in area units",
                language,CurriculumTier.ThreeStars,ArithmeticQuizMode.Essay,items.AsReadOnly());
            string snapshot=JsonSerializer.Serialize(sheet);
            foreach(bool answers in new[]{false,true})
            {
                byte[] pdf=QuizPdfWriter.Write(sheet,answers);PdfAssertions.HasEmbeddedText(pdf);
                string text=PdfTestReader.ExtractText(pdf);
                Check(!text.Contains("{v")&&!text.Contains("{f"),"PDF has unbound area slots.");
                if(!answers)Check(!text.Contains(language==AppLanguage.Vietnamese?"Đáp số:":"Answer:"),"Area worksheet leaked answer.");
                File.WriteAllBytes(Path.Combine(directory,$"rectangle-area-{language}-{(answers?"answers":"worksheet")}.pdf"),pdf);
            }
            Check(snapshot==JsonSerializer.Serialize(sheet),"PDF export changed area facts.");
        }
        Console.WriteLine($"PASS {cases} rectangle-area factory/mode cases; direct sides, separate squared answer units, review isolation, frozen bilingual worksheet/answer PDFs.");
    }
    private static void Check(bool b,string message)=>TestAssert.That(b,message);
}
