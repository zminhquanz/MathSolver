using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;

internal static class MotionOpenWorksheetTests
{
    private sealed class AlwaysBank:Random {public override int Next(int maxValue)=>maxValue-1;}
    internal static async Task Run(string directory)
    {
        // Small text/math mixture reproduces font-state problems without
        // requiring the complete SQL/factory matrix to finish first.
        var smoke=MotionOpenPilot.Profiles(AppLanguage.Vietnamese).Select(profile=>QuizWorksheetBuilder.Format(
            new MotionQuizGenerator(new(713)).GenerateNarrative(ArithmeticQuizMode.Essay,AppLanguage.Vietnamese,MotionQuizType.Basic,new(CurriculumTier.ThreeStars,false),profile),AppLanguage.Vietnamese)).ToArray();
        var sample=new QuizWorksheet("MOTION-FONTS","Chuyển động","Quãng đường và vận tốc",AppLanguage.Vietnamese,CurriculumTier.ThreeStars,ArithmeticQuizMode.Essay,smoke);
        byte[] samplePdf=QuizPdfWriter.Write(sample,false);File.WriteAllBytes(Path.Combine(directory,"motion-font-regression.pdf"),samplePdf);
        Check(PdfTestReader.ExtractText(samplePdf).Contains("Quãng",StringComparison.Ordinal),"Motion PDF lost Vietnamese text beside math.");
        int count=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var items=new List<QuizPrintItem>();
            foreach(string profile in MotionOpenPilot.Profiles(language))
            foreach(var tier in Enum.GetValues<CurriculumTier>())
            {
                var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Motion,0,tier,language,new(713),profile) with {OpenProse=true};
                var d=ReasoningStoryCatalogue.Draft(c);var data=MotionOpenPilot.Create(profile,language,tier,new(c.Story!.Seed));
                var store=new QuestionBankStore(Path.Combine(directory,$"motion-{language}-{profile.Split('/')[1]}-{profile.Split('/')[2]}-{tier}.db3"));
                var unknown=d with {Question=d.Question+" New wording."};string raw=QuestionBankStore.SerializeDraft(unknown);
                Check(BasicQuestionValidator.Validate(raw,c).NeedsReview,"Unknown motion PDF draft not pending.");
                Check(await store.QueueReviewAsync(PendingQuestionReview.Create(c,unknown,raw,"fixture","review")),"Cannot queue motion PDF draft.");
                Check(await store.TakeReasoningAsync(c.Family,0,tier,language) is null,"Pending motion draft reached PDF bank.");
                Check(await store.InsertAsync(new(c,d,QuestionBankStore.SerializeDraft(d),"fixture",DateTime.UtcNow)),"Cannot save motion PDF reference.");
                var factory=new PracticeQuestionFactory(new(store,new AlwaysBank()));
                foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var q=await factory.CreateAsync(new(QuizProblemKind.Motion,MotionType:MotionQuizType.Basic),mode,new(tier,false),language,null,PracticeQuestionFormat.WordProblem,false);
                    var m=q.MotionProblem!;
                    Check(m.NarrativeId==profile&&m.QuestionKind==data.Relation.Kind&&MotionOpenPilot.ValidContract(m),"PDF/factory changed motion target or facts.");
                    var printed=QuizWorksheetBuilder.Format(q,language);
                    Check(printed.Solution.Contains(m.EquationText+" "+m.AnswerUnit),"PDF lost fresh C# motion equation/unit.");
                    if(mode!=ArithmeticQuizMode.TrueFalse)Check(printed.Answer.EndsWith(m.CorrectAnswer+" "+m.AnswerUnit,StringComparison.Ordinal),"Wrong motion PDF answer unit.");
                    Check(!printed.Prompt.Contains("{f")&&!printed.Prompt.Contains("{v"),"Motion PDF raw slots.");
                    if(mode==ArithmeticQuizMode.Essay&&tier==CurriculumTier.ThreeStars)items.Add(printed);count++;
                }
            }
            var sheet=new QuizWorksheet("MOTION-OPEN",language==AppLanguage.Vietnamese?"Chuyển động":"Motion",
                "Distance, speed, travel time, one included stop",language,CurriculumTier.ThreeStars,ArithmeticQuizMode.Essay,items.AsReadOnly());
            string before=JsonSerializer.Serialize(sheet);
            foreach(bool answers in new[]{false,true})
            {
                byte[] pdf=QuizPdfWriter.Write(sheet,answers);File.WriteAllBytes(Path.Combine(directory,$"motion-open-{language}-{(answers?"answers":"worksheet")}.pdf"),pdf);
                PdfAssertions.HasEmbeddedText(pdf);string text=PdfTestReader.ExtractText(pdf);
                Check(!text.Contains("{f")&&!text.Contains("{v"),"PDF contains raw motion slots.");
                File.WriteAllBytes(Path.Combine(directory,$"motion-open-{language}-{(answers?"answers":"worksheet")}.pdf"),pdf);
            }
            Check(before==JsonSerializer.Serialize(sheet),"Motion PDF changed frozen data.");
        }
        Console.WriteLine($"PASS {count} motion factory/mode/PDF cases; four targets, included-rest arithmetic, pending isolation, bilingual frozen worksheets/answers.");
    }
    private static void Check(bool value,string message)=>TestAssert.That(value,message);
}
