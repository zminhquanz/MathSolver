using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;

internal static class UnitaryGroupsWorksheetTests
{
    private sealed class AlwaysBank:Random {public override int Next(int maxValue)=>maxValue-1;}
    internal static async Task Run(string directory)
    {
        var choice=QuizWorksheetChoices.Subtypes(QuizProblemKind.Proportion).Single(x=>x.Request.ProportionType==ProportionQuizType.UnitaryGroupCount);
        Check(choice.Key=="Quiz.ProportionUnitaryGroupCount","PDF choice not registered separately.");
        foreach(var language in Enum.GetValues<AppLanguage>())
            Check(!string.IsNullOrWhiteSpace(QuizChoiceCatalog.Create(0,choice.Key,"groups",language,false).Description),
                "New group-count choice must open in the illustrated picker.");
        int count=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var items=new List<QuizPrintItem>();
            foreach(string profile in UnitaryGroupsPilot.Profiles(language))
            foreach(var tier in Enum.GetValues<CurriculumTier>())
            {
                var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Proportion,(int)ProportionQuizType.UnitaryGroupCount,tier,language,new(713),profile) with {OpenProse=true};
                var d=ReasoningStoryCatalogue.Draft(c);var data=UnitaryGroupsPilot.Create(profile,language,tier,new(c.Story!.Seed));
                var store=new QuestionBankStore(Path.Combine(directory,$"groups-{language}-{profile.Split('/')[1]}-{tier}.db3"));
                var unknown=d with {Question=d.Question+(language==AppLanguage.Vietnamese?" Theo cách mới.":" In a new way.")};string raw=QuestionBankStore.SerializeDraft(unknown);
                Check(BasicQuestionValidator.Validate(raw,c).NeedsReview,"Unknown printable prose not pending.");
                Check(await store.QueueReviewAsync(PendingQuestionReview.Create(c,unknown,raw,"pdf-fixture","review")),"Cannot queue pending prose.");
                Check(await store.TakeReasoningAsync(c.Family,c.BankVariant,tier,language) is null,"Pending group prose reached PDF bank.");
                Check(await store.InsertAsync(new(c,d,QuestionBankStore.SerializeDraft(d),"pdf-fixture",DateTime.UtcNow)),"Cannot save valid PDF fixture.");
                var factory=new PracticeQuestionFactory(new(store,new AlwaysBank()));
                foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var q=await factory.CreateAsync(new(QuizProblemKind.Proportion,ProportionType:ProportionQuizType.UnitaryGroupCount),mode,new(tier,false),language,null,PracticeQuestionFormat.WordProblem,false);
                    var p=q.ProportionProblem!;Check(p.Type==ProportionQuizType.UnitaryGroupCount&&p.NarrativeId==profile,"Factory changed group-count subtype/context.");
                    Check(p.B%p.A==0&&p.C%(p.B/p.A)==0&&p.CorrectAnswer==p.C/(p.B/p.A),"PDF group arithmetic changed.");
                    var printed=QuizWorksheetBuilder.Format(q,language);
                    if(mode==ArithmeticQuizMode.TrueFalse)
                        Check(printed.Prompt.Contains($"{q.PresentedAnswer} {data.Units.Groups}"),"PDF assertion must count containers.");
                    else Check(printed.Answer.EndsWith($"{p.CorrectAnswer} {data.Units.Groups}",StringComparison.Ordinal),"PDF answer must count containers.");
                    Check(printed.Solution.Contains($"{p.B} ÷ {p.A} = {p.B/p.A} {data.Units.Rate}")&&printed.Solution.Contains($"{p.C} ÷ {p.B/p.A} = {p.CorrectAnswer} {data.Units.Groups}"),"PDF lost mixed-unit steps.");
                    Check(!printed.Prompt.Contains(language==AppLanguage.Vietnamese?"Đáp số:":"Answer:")&&!printed.Prompt.Contains("{f"),"PDF leaked answer or slots.");
                    if(mode==ArithmeticQuizMode.Essay)items.Add(printed);count++;
                }
            }
            var sheet=new QuizWorksheet("UNITARY-GROUPS",language==AppLanguage.Vietnamese?"Rút về đơn vị":"Unitary method",
                language==AppLanguage.Vietnamese?"Tìm số nhóm":"Find the group count",language,CurriculumTier.ThreeStars,ArithmeticQuizMode.Essay,items.AsReadOnly());
            string before=JsonSerializer.Serialize(sheet);
            foreach(bool answers in new[]{false,true})
            {
                byte[] pdf=QuizPdfWriter.Write(sheet,answers);PdfAssertions.HasEmbeddedText(pdf);
                string text=PdfTestReader.ExtractText(pdf);Check(!text.Contains("{f")&&!text.Contains("{v"),"PDF contains raw slots.");
                File.WriteAllBytes(Path.Combine(directory,$"unitary-groups-{language}-{(answers?"answers":"worksheet")}.pdf"),pdf);
            }
            Check(before==JsonSerializer.Serialize(sheet),"PDF mutated worksheet snapshot.");
        }
        Console.WriteLine($"PASS {count} group-count factory/mode/PDF cases; item/group rate then group answer, pending isolation and bilingual PDFs.");
    }
    private static void Check(bool value,string message)=>TestAssert.That(value,message);
}
