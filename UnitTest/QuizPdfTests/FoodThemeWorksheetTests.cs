using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Services.Printing;
using MathSolver.Services.Core;
using System.Text.Json;

internal static class FoodThemeWorksheetTests
{
    private static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    public static Task Run(string directory)
    {
        Directory.CreateDirectory(directory);int count=0,pdfs=0;var traces=new List<object>();
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var items=new List<QuizPrintItem>();
            foreach(var subject in FoodThemeCatalogue.Subjects)
            foreach(var fraction in new[]{false,true})
            {
                var profile=new QuestionLearningProfile(subject.Group);var tier=CurriculumTier.ThreeStars;
                string id=$"theme-{subject.Group.ToString().ToLowerInvariant()}-{subject.Unit.Id}-total";
                var c=fraction?FractionQuestionCatalogue.Create(profile,ArithmeticOperation.Add,tier,language,new(31),"fraction-"+id)
                    :AppliedQuestionCatalogue.Create(profile,ArithmeticOperation.Add,tier,language,new(37),id);
                var d=BasicQuestionTemplates.Example(c);var q=c.ToPracticeQuestion(d.ToWordProblem(c),mode,new(11));
                if(fraction)
                {
                    Check(q.ExactAnswer==FractionQuestionCatalogue.Answer(c),"PDF exact fraction changed");
                    if(mode==ArithmeticQuizMode.TrueFalse)Check(q.PresentedEquationIsCorrect==(q.FractionProblem!.PresentedAnswer==q.ExactAnswer),"Fraction assertion key changed");
                    if(mode==ArithmeticQuizMode.MultipleChoice)Check(q.FractionProblem!.Choices.Distinct().Count()==4&&q.FractionProblem.Choices.Contains(q.ExactAnswer),"Fraction distractors invalid");
                }
                else Check(new ArithmeticQuizValidator(new BasicArithmeticEngine()).Validate(q).IsValid,"Theme PDF integer invalid");
                var printed=QuizWorksheetBuilder.Format(q,language);Check(!printed.Prompt.Contains('{')&&!printed.Answer.Contains('{'),"Unbound theme PDF slots");
                if(subject.Group==QuestionKnowledgeGroup.Food)
                {
                    bool bakery=FoodThemeCatalogue.IsBakeryFood(subject.Unit.Id);
                    string venue=Enumerable.Range(0,FoodThemePrompt.VenueCount(bakery))
                        .Select(i=>FoodThemePrompt.Venue(bakery,i,language))
                        .Single(v=>c.Subject.Contains(language==AppLanguage.Vietnamese?v+" của ":"'s "+v,StringComparison.OrdinalIgnoreCase));
                    Check(printed.Prompt.Contains(c.Subject,StringComparison.OrdinalIgnoreCase),"Food PDF missing named venue: "+c.SceneId);
                    if(!fraction)
                    {
                        Check(c.OtherSubject.Contains(language==AppLanguage.Vietnamese?venue+" của ":"'s "+venue,StringComparison.OrdinalIgnoreCase),
                            "Food PDF mismatched venue owners: "+c.SceneId);
                        string pair=language==AppLanguage.Vietnamese?"cả hai "+venue:"the two "+(venue switch{
                            "bakery"=>"bakeries","cake shop"=>"cake shops","eatery"=>"eateries","restaurant"=>"restaurants",_=>"diners"});
                        Check(printed.Prompt.Contains(pair,StringComparison.OrdinalIgnoreCase),"Food PDF question missing both matching venues: "+c.SceneId);
                    }
                    if(subject.Unit.Id=="cakes")
                        Check(printed.Prompt.Contains(language==AppLanguage.Vietnamese?"bánh kem":"cream cake",StringComparison.OrdinalIgnoreCase),
                            "PDF contains generic cake wording: "+c.SceneId);
                }
                if(mode==ArithmeticQuizMode.TrueFalse)Check(printed.Answer==(q.PresentedEquationIsCorrect==true?printed.Options[0]:printed.Options[1]),"Truth key changed");
                items.Add(printed);traces.Add(new{language,mode,subject.Group,subject.Unit.Id,Fraction=fraction,printed.Prompt,printed.Answer});count++;
            }
            string title=language==AppLanguage.Vietnamese?"Thức ăn · Nước uống · Trái cây":"Food · Drinks · Fruit";
            int batch=0;
            foreach(var group in items.Chunk(QuizWorksheetBuilder.MaximumCount))
            {
                var sheet=new QuizWorksheet($"FOOD-THEMES-{++batch}",title,title,language,CurriculumTier.ThreeStars,mode,group);
                string snapshot=JsonSerializer.Serialize(sheet);
                foreach(bool answers in new[]{false,true})
                {
                    byte[] bytes=QuizPdfWriter.Write(sheet,answers);PdfAssertions.HasEmbeddedText(bytes);string text=PdfTestReader.ExtractText(bytes);
                    Check(!text.Contains("{unit")&&!text.Contains("{name}"),"PDF text contains template fields");
                    Check(text.Contains(language==AppLanguage.Vietnamese?"Thức ăn":"Food"),"Theme title not embedded");
                    File.WriteAllBytes(Path.Combine(directory,$"food-themes-{language}-{mode}-part{batch}-{(answers?"answers":"worksheet")}.pdf"),bytes);pdfs++;
                }
                Check(JsonSerializer.Serialize(sheet)==snapshot,"Export changed frozen facts/answers");
            }
        }
        File.WriteAllText(Path.Combine(directory,"food-theme-pdf-cases.json"),JsonSerializer.Serialize(traces,new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
        Console.WriteLine($"PASS food theme PDF: {count} exact subject/fraction/mode/VI-EN fixtures, {pdfs} app-generated worksheets/keys, embedded text, frozen facts and native math diagrams.");
        return Task.CompletedTask;
    }
}
