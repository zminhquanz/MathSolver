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
                if(subject.Unit.Id=="water-bottles")
                {
                    var word=d.ToWordProblem(c);
                    Check(word.StoryArtworkId=="water_bottle"
                        && QuizStoryArtwork.Select(true,false,[word.AnswerUnit],
                            itemUnit:word.ItemUnit,artworkId:word.StoryArtworkId)=="quiz_story_water_bottle.png",
                        "PDF water-bottle worksheet lost its distinct countable illustration");
                }
                if(fraction)
                {
                    Check(q.ExactAnswer==FractionQuestionCatalogue.Answer(c),"PDF exact fraction changed");
                    if(mode==ArithmeticQuizMode.TrueFalse)Check(q.PresentedEquationIsCorrect==(q.FractionProblem!.PresentedAnswer==q.ExactAnswer),"Fraction assertion key changed");
                    if(mode==ArithmeticQuizMode.MultipleChoice)Check(q.FractionProblem!.Choices.Distinct().Count()==4&&q.FractionProblem.Choices.Contains(q.ExactAnswer),"Fraction distractors invalid");
                }
                else Check(new ArithmeticQuizValidator(new BasicArithmeticEngine()).Validate(q).IsValid,"Theme PDF integer invalid");
                var printed=QuizWorksheetBuilder.Format(q,language);Check(!printed.Prompt.Contains('{')&&!printed.Answer.Contains('{'),"Unbound theme PDF slots");
                if(language==AppLanguage.Vietnamese && subject.Unit.Id=="op-la")
                {
                    Check(printed.Prompt.Contains("phần trứng ốp-la",StringComparison.Ordinal),
                        "PDF question contains corrupted Vietnamese food unit");
                    if(mode!=ArithmeticQuizMode.TrueFalse)
                        Check(printed.Answer.Contains("phần trứng ốp-la",StringComparison.Ordinal),
                            "PDF answer contains corrupted Vietnamese food unit");
                }
                if(subject.Group==QuestionKnowledgeGroup.Food)
                {
                    if(FoodThemeCatalogue.DessertShopKind(subject.Unit.Id) is { } dessert)
                    {
                        bool vi=language==AppLanguage.Vietnamese;
                        var (first,second,both)=FoodThemePrompt.DessertShopActors(dessert,language);
                        Check(c.Subject.Equals(first,StringComparison.OrdinalIgnoreCase),"PDF dessert subject lost first shop: "+c.SceneId);
                        Check(printed.Prompt.Contains(first,StringComparison.OrdinalIgnoreCase)
                            && printed.Prompt.Contains(second,StringComparison.OrdinalIgnoreCase)
                            && printed.Prompt.Contains(both,StringComparison.OrdinalIgnoreCase),
                            "PDF dessert total must name both shops: "+c.SceneId);
                        if(!fraction)
                            Check(c.OtherSubject.Equals(second,StringComparison.OrdinalIgnoreCase)
                                && printed.Prompt.Contains($"{first} {(vi?"bán được":"sold")} {c.Left} {c.Unit}",StringComparison.OrdinalIgnoreCase)
                                && printed.Prompt.Contains($"{second} {(vi?"bán được":"sold")} {c.Right} {c.Unit}",StringComparison.OrdinalIgnoreCase),
                                "PDF dessert integer total must describe two independent sales: "+c.SceneId);
                    }
                    else
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
                }
                if(subject.Group==QuestionKnowledgeGroup.Drinks)
                {
                    bool vi=language==AppLanguage.Vietnamese;
                    var (first,second,both)=FoodThemePrompt.DrinkShopActors(
                        FoodThemeCatalogue.IsCoffeeDrinkContract(c),
                        FoodThemeCatalogue.IsMilkTeaDrinkContract(c),language);
                    Check(c.Subject.Equals(first,StringComparison.OrdinalIgnoreCase),"PDF drink contract lost drink shop: "+c.SceneId);
                    Check(printed.Prompt.Contains(first,StringComparison.OrdinalIgnoreCase),"PDF drink story missing first shop: "+c.SceneId);
                    if(!fraction)
                    {
                        Check(c.OtherSubject.Equals(second,StringComparison.OrdinalIgnoreCase),"PDF drink contract mismatched second shop: "+c.SceneId);
                    }
                    Check(printed.Prompt.Contains(second,StringComparison.OrdinalIgnoreCase)
                        && printed.Prompt.Contains(both,StringComparison.OrdinalIgnoreCase),
                        "PDF drink total must name both shops: "+c.SceneId);
                    if(!fraction)
                        Check(printed.Prompt.Contains(vi?"bán được":"sold",StringComparison.OrdinalIgnoreCase),
                            "PDF drink total must describe sales: "+c.SceneId);
                }
                if(mode==ArithmeticQuizMode.TrueFalse)Check(printed.Answer==(q.PresentedEquationIsCorrect==true?printed.Options[0]:printed.Options[1]),"Truth key changed");
                items.Add(printed);traces.Add(new{language,mode,subject.Group,subject.Unit.Id,Fraction=fraction,printed.Prompt,printed.Answer});count++;
            }
            foreach (var fruit in FoodThemeCatalogue.Subjects.Where(s => s.Group == QuestionKnowledgeGroup.Fruit))
            foreach (var action in new[] { "plant", "harvest" })
            foreach (var (left, right) in new[] { (100, 100), (100, 101), (101, 100), (101, 101) })
            {
                var contract = AppliedQuestionCatalogue.Create(new QuestionLearningProfile(QuestionKnowledgeGroup.Fruit),
                    ArithmeticOperation.Add, CurriculumTier.ThreeStars, language, new Random(31),
                    $"theme-fruit-{fruit.Unit.Id}-{action}-total");
                string firstVenue = left <= 100 ? language == AppLanguage.Vietnamese ? "khu vườn" : "garden"
                    : language == AppLanguage.Vietnamese ? "nông trại" : "farm";
                string secondVenue = right <= 100 ? language == AppLanguage.Vietnamese ? "khu vườn" : "garden"
                    : language == AppLanguage.Vietnamese ? "nông trại" : "farm";
                contract = contract with
                {
                    Left = left,
                    Right = right,
                    Subject = language == AppLanguage.Vietnamese ? $"{firstVenue} của An" : $"Alex's {firstVenue}",
                    OtherSubject = language == AppLanguage.Vietnamese ? $"{secondVenue} của Bình" : $"Sam's {secondVenue}"
                };
                Check(contract.IsValid && contract.Answer == left + right, "Invalid fruit garden facts in PDF");
                var draft = BasicQuestionTemplates.Example(contract);
                var question = contract.ToPracticeQuestion(draft.ToWordProblem(contract), mode, new Random(11));
                Check(new ArithmeticQuizValidator(new BasicArithmeticEngine()).Validate(question).IsValid,
                    "Fruit garden PDF question failed arithmetic validation");
                var printed = QuizWorksheetBuilder.Format(question, language);
                string both = language == AppLanguage.Vietnamese
                    ? left <= 100 && right <= 100 ? "cả hai khu vườn"
                        : left > 100 && right > 100 ? "cả hai nông trại"
                        : left <= 100 ? "cả khu vườn và nông trại" : "cả nông trại và khu vườn"
                    : left <= 100 && right <= 100 ? "both gardens"
                        : left > 100 && right > 100 ? "both farms"
                        : left <= 100 ? "the garden and the farm" : "the farm and the garden";
                string verb = action == "plant" ? language == AppLanguage.Vietnamese ? "trồng được" : "planted"
                    : language == AppLanguage.Vietnamese ? "thu hoạch được" : "harvested";
                Check(printed.Prompt.Contains(both, StringComparison.OrdinalIgnoreCase)
                    && printed.Prompt.Contains(contract.Subject, StringComparison.OrdinalIgnoreCase)
                    && printed.Prompt.Contains(contract.OtherSubject, StringComparison.OrdinalIgnoreCase)
                    && printed.Prompt.Contains(verb, StringComparison.OrdinalIgnoreCase)
                    && printed.Prompt.Contains($"{left} {contract.Unit}", StringComparison.OrdinalIgnoreCase)
                    && printed.Prompt.Contains($"{right} {contract.Unit}", StringComparison.OrdinalIgnoreCase),
                    "Fruit garden PDF changed the venue, action or numbers: " + contract.SceneId);
                Check(!printed.Prompt.Contains('{') && !printed.Answer.Contains('{'),
                    "Fruit garden PDF contains unresolved placeholders");
                items.Add(printed);
                traces.Add(new { language, mode, Group = QuestionKnowledgeGroup.Fruit,
                    fruit.Unit.Id, Action = action, Left = left, Right = right, printed.Prompt, printed.Answer });
                count++;
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
        PreparedDishVenueWorksheetTests.Run(directory);
        JapaneseDishWorksheetTests.Run(directory);
        KoreanDishWorksheetTests.Run(directory);
        VietnameseDishWorksheetTests.Run(directory);
        return Task.CompletedTask;
    }
}
