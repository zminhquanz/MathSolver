using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Services.Printing;
using PdfSharp.Pdf.IO;
using System.Text.Json;

internal static class AppliedNarrativeWorksheetTests
{
    internal static async Task Run(string directory)
    {
        int cases=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var store=new QuestionBankStore(Path.Combine(directory,"applied-"+language+".db3"));
            foreach(var type in ElementaryQuizGenerator.AppliedNarrativeTypes)
            {
                var family=Family(type);
                var c=ReasoningStoryCatalogue.Create(family,(int)type,CurriculumTier.ThreeStars,language,new Random(713));
                var draft=ReviewedReasoningProse.NovelDrafts(c,new HashSet<string>()).Last();
                Check(await store.InsertAsync(new(c,draft,QuestionBankStore.SerializeDraft(draft),"reviewed-pdf",DateTime.UtcNow)),"Failed PDF bank insertion.");
            }
            var factory=new PracticeQuestionFactory(new BasicPracticeQuestionProvider(store,new AlwaysBank()));
            int next=0;
            var sheet=await QuizWorksheetBuilder.CreateAsync(18,language==AppLanguage.Vietnamese?"Đo lường, hình học và thời gian":"Measurement, geometry and time",
                language==AppLanguage.Vietnamese?"Đề luyện tập có lời văn":"Contextual practice",language,CurriculumTier.ThreeStars,ArithmeticQuizMode.Essay,token=>
                {
                    var type=ElementaryQuizGenerator.AppliedNarrativeTypes[next++%9];
                    return factory.CreateAsync(new(Kind(type),ElementaryType:type),ArithmeticQuizMode.Essay,new(CurriculumTier.ThreeStars,false),language,null,PracticeQuestionFormat.WordProblem,false,token);
                });
            Check(sheet.Questions.Select(q=>q.Source.ElementaryProblem!.Type).Distinct().Count()==9,"PDF missed an applied type.");
            Check(sheet.Questions.All(q=>q.Source.ElementaryProblem!.StoryContextId?.StartsWith("applied-")==true),"PDF bypassed contextual prose.");
            // Batch deduplication may retry one subtype, so assess each retained
            // question instead of assuming exactly two of every source type.
            Check(sheet.Questions.Where(q=>q.Source.ElementaryProblem!.Type==ElementaryQuizType.ReadClock)
                .All(q=>q.Visual?.Kind=="clock"),"PDF lost clock diagrams.");
            Check(sheet.Questions.Where(q=>q.Source.ElementaryProblem!.Type is ElementaryQuizType.CompositeArea or ElementaryQuizType.RectangleSide)
                .All(q=>q.Visual?.Polygons?.Count>0),"PDF lost composite/missing-side diagrams.");
            string frozen=JsonSerializer.Serialize(sheet);
            foreach(bool answers in new[]{false,true})
            {
                byte[] bytes=QuizPdfWriter.Write(sheet,answers);
                File.WriteAllBytes(Path.Combine(directory,$"applied-{language}-{(answers?"answers":"worksheet")}.pdf"),bytes);
                using var pdf=PdfReader.Open(new MemoryStream(bytes),PdfDocumentOpenMode.Import);
                Check(pdf.PageCount>1&&pdf.Pages.Cast<PdfSharp.Pdf.PdfPage>().All(p=>Math.Abs(p.Width.Point-595.28)<1&&Math.Abs(p.Height.Point-841.89)<1),"Wrong applied PDF pages.");
                PdfAssertions.HasEmbeddedText(bytes);
                string text=PdfTestReader.ExtractText(bytes);
                Check(text.Contains(sheet.Title),"Lost applied title/Unicode.");
                Check(!text.Contains("{f")&&!text.Contains("{v"),"Unrendered PDF slots.");
            }
            Check(JsonSerializer.Serialize(sheet)==frozen,"PDF export mutated questions or answers.");
            foreach(var type in ElementaryQuizGenerator.AppliedNarrativeTypes)
            foreach(var tier in Enum.GetValues<CurriculumTier>())
            {
                Check(QuizWorksheetChoices.Subtypes(Kind(type)).Any(s=>s.Request.ElementaryType==type),"Missing PDF subtype.");
                var q=await factory.CreateAsync(new(Kind(type),ElementaryType:type),ArithmeticQuizMode.Essay,new(tier,false),language,null,PracticeQuestionFormat.WordProblem,false);
                Check(q.ElementaryProblem!.Type==type,"Wrong applied worksheet selection.");cases++;
            }
        }
        Console.WriteLine($"PASS {cases} applied PDF selections; bilingual frozen 18-question AI-bank sets, A4, clock and polygon diagrams.");
    }
    private static BankQuestionFamily Family(ElementaryQuizType type)=>Kind(type) switch
    {QuizProblemKind.Measurement=>BankQuestionFamily.Measurement,QuizProblemKind.VisualGeometry=>BankQuestionFamily.Spatial,QuizProblemKind.Time=>BankQuestionFamily.Time,_=>BankQuestionFamily.Decimal};
    private static QuizProblemKind Kind(ElementaryQuizType type)=>type switch
    {ElementaryQuizType.AreaConversion or ElementaryQuizType.VolumeConversion or ElementaryQuizType.MixedLength=>QuizProblemKind.Measurement,
     ElementaryQuizType.CompositeArea or ElementaryQuizType.RectangleSide=>QuizProblemKind.VisualGeometry,
     ElementaryQuizType.Calendar or ElementaryQuizType.ReadClock=>QuizProblemKind.Time,_=>QuizProblemKind.Decimal};
    private sealed class AlwaysBank:Random {public override int Next(int maxValue)=>maxValue-1;}
}
