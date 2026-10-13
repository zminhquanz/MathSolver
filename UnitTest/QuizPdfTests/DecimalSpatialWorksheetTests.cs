using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using System.Text.Json;
using PdfSharp.Pdf.IO;

internal static class DecimalSpatialWorksheetTests
{
    internal static async Task Run(PracticeQuestionFactory factory,string directory)
    {
        ElementaryQuizType[] types=[.. ElementaryQuizGenerator.DecimalSkillTypes,.. ElementaryQuizGenerator.SpatialSkillTypes];
        int cases=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            int next=0;
            var sheet=await QuizWorksheetBuilder.CreateAsync(20,language==AppLanguage.Vietnamese?"Số thập phân và vị trí trong không gian":"Decimals and spatial positions",
                "Decimals · Front · Behind · Between",language,CurriculumTier.ThreeStars,ArithmeticQuizMode.Essay,token=>
                {
                    var type=types[next++%types.Length];
                    return factory.CreateAsync(new(ElementaryQuizGenerator.SpatialSkillTypes.Contains(type)?QuizProblemKind.VisualGeometry:QuizProblemKind.Decimal,ElementaryType:type),
                        ArithmeticQuizMode.Essay,new(CurriculumTier.ThreeStars,false),language,null,PracticeQuestionFormat.Numeric,false,token);
                });
            Check(sheet.Questions.Select(q=>q.Source.ElementaryProblem!.Type).Distinct().Count()==10,"PDF missed a new skill.");
            string frozen=JsonSerializer.Serialize(sheet);
            foreach(bool answers in new[]{false,true})
            {
                var bytes=QuizPdfWriter.Write(sheet,answers);
                File.WriteAllBytes(Path.Combine(directory,$"decimal-spatial-{language}-{(answers?"answers":"worksheet")}.pdf"),bytes);
                using var pdf=PdfReader.Open(new MemoryStream(bytes),PdfDocumentOpenMode.Import);
                Check(pdf.PageCount>1&&pdf.Pages.Cast<PdfSharp.Pdf.PdfPage>().All(p=>Math.Abs(p.Width.Point-595.28)<1&&Math.Abs(p.Height.Point-841.89)<1),"Invalid skill A4 pages.");
                PdfAssertions.HasEmbeddedText(bytes);
                string text=PdfTestReader.ExtractText(bytes);
                Check(text.Contains(sheet.Title),"PDF lost Vietnamese title.");
                if(answers) Check(!text.Contains("Đáp số:")&&!text.Contains("Answer:"),"A skill answer was duplicated as a separate final-answer line.");
            }
            Check(JsonSerializer.Serialize(sheet)==frozen,"PDF mutated skill questions.");
            var catalogue=new QuizProblemTypeCatalog();
            foreach(var type in types)
            foreach(var tier in Enum.GetValues<CurriculumTier>())
            {
                var kind=ElementaryQuizGenerator.SpatialSkillTypes.Contains(type)?QuizProblemKind.VisualGeometry:QuizProblemKind.Decimal;
                int index=catalogue.Options.ToList().FindIndex(o=>o.FixedRequest?.Kind==kind);
                Check(QuizWorksheetChoices.Subtypes(kind).Any(s=>s.Request.ElementaryType==type),"Missing PDF skill picker.");
                var q=await new QuizWorksheetSettings(index,new(kind,ElementaryType:type),tier,language).GenerateAsync(factory,catalogue);
                Check(q.ElementaryProblem!.Type==type&&q.Mode==ArithmeticQuizMode.Essay,"Wrong PDF selected skill.");cases++;
            }
        }
        Console.WriteLine($"PASS {cases} decimal/spatial PDF selections; bilingual frozen 20-question sets and A4 vector queue diagrams.");
    }
}
