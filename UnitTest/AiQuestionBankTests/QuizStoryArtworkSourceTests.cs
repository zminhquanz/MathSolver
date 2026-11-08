using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;

internal static class QuizStoryArtworkSourceTests
{
    private sealed record Row(string? Id=null,string? NarrativeId=null,string? ArtworkId=null,string? Type=null);
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    internal static void Run()
    {
        int count=ConnectedSubjects();
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            string culture=QuizContentCatalog.Culture(language);
            foreach(string list in new[]{"ProportionQuizGenerator.Templates","ProportionQuizGenerator.Templates.Extensions"})
                foreach(var row in QuizContentCatalog.LoadList<Row>(list,culture))
                {
                    string suffix=row.NarrativeId![^3..];
                    if(suffix=="049")Check(row.ArtworkId=="cake","Ovens bake cakes, not trees");
                    if(suffix=="050")Check(row.ArtworkId=="animal_feed","Explicit animal feed is not rice");
                    if(suffix is "030" or "038" or "020" or "021" or "022")Check(row.ArtworkId is null,"No machine/meat inference "+suffix);
                    if(suffix is "039" or "054")Check(row.ArtworkId=="printer","Only explicit printers");
                    var type=Enum.Parse<ProportionQuizType>(row.Type!);var tier=Enum.GetValues<CurriculumTier>().First(t=>ProportionQuizGenerator.NarrativeIds(type,t,language).Contains(row.NarrativeId));
                    var q=new ProportionQuizGenerator(new(17)).GenerateNarrative(ArithmeticQuizMode.Essay,type,language,new(tier,false),row.NarrativeId!);
                    Check(QuizStoryArtworkCatalogue.SourceArtwork(q,language)==row.ArtworkId,"Exact proportion source "+row.NarrativeId);count++;
                }
            foreach(string list in new[]{"DecimalStoryContexts","DecimalStoryContexts.Extensions"})
                foreach(var row in QuizContentCatalog.LoadList<Row>(list,culture).Where(r=>r.ArtworkId is not null))
                {
                    var q=new ElementaryQuizGenerator(new(29)).GenerateDecimalStory(ArithmeticQuizMode.Essay,ElementaryQuizType.DecimalAdd,language,CurriculumTier.FourStars,row.Id!);
                    Check(QuizStoryArtworkCatalogue.SourceArtwork(q,language)==row.ArtworkId,"Exact decimal source "+row.Id);count++;
                }
            var seen=new HashSet<string>();
            for(int i=0;i<160;i++)
            {
                var q=new PercentageQuizGenerator(new(i)).GenerateAlgorithm(ArithmeticQuizMode.Essay,PercentageQuizType.FindPercentageValue,language);
                string? id=QuizStoryArtworkCatalogue.SourceArtwork(q,language);if(id is not null)seen.Add(id);count++;
            }
            Check(new[]{"marble","ticket","seed","shirt"}.All(seen.Contains),"Percentage subjects "+language+": "+string.Join(",",seen));
            seen.Clear();for(int i=0;i<160;i++)
            {
                var q=new AverageQuizGenerator(new(i)).GenerateAlgorithm(ArithmeticQuizMode.Essay,AverageQuizType.Direct,language);
                string? id=QuizStoryArtworkCatalogue.SourceArtwork(q,language);if(id is not null)seen.Add(id);count++;
            }
            Check(seen.Contains("water")&&seen.Contains("vegetables"),"Average measured subjects");
            var discount=new ElementaryQuizGenerator(new(2)).GenerateOpenDiscount(ArithmeticQuizMode.Essay,language,CurriculumTier.FourStars,"discount-open/backpack");
            Check(QuizStoryArtworkCatalogue.SourceArtwork(discount,language)=="backpack","Named discount subject");
            foreach(var scene in FractionQuestionCatalogue.All.Where(s=>new[]{"fraction-water-","fraction-flour-","fraction-ribbon-"}.Any(s.Id.StartsWith)))
            {
                var c=FractionQuestionCatalogue.Create(new(scene.Group),scene.Operation,CurriculumTier.FourStars,language,new(73),scene.Id);
                var word=FractionQuestionCatalogue.Draft(c).ToWordProblem(c);
                Check(c.IsValid&&word.StoryArtworkId==QuizStoryArtworkCatalogue.FractionArtwork(scene.Id),"Fraction source metadata");count++;
            }
        }
        Console.WriteLine($"PASS story artwork sources: {count} actual C# fixtures, VI/EN, exact List identity, printer/machine/feed/meat safeguards, decimal/percentage/average/discount/fraction.");
    }
    private static int ConnectedSubjects()
    {
        int count=0;
        var measurement=new[]{("water",ElementaryQuizType.CapacityConversion,"water"),("bakery",ElementaryQuizType.MassConversion,"flour"),
            ("agriculture",ElementaryQuizType.MassConversion,"vegetables"),("environment",ElementaryQuizType.MassConversion,"recycled_paper"),
            ("decoration",ElementaryQuizType.LengthConversion,"ribbon"),("craft",ElementaryQuizType.LengthConversion,"fabric"),
            ("dairy",ElementaryQuizType.CapacityConversion,"milk_liquid"),("kitchen",ElementaryQuizType.MassConversion,"rice_grains"),
            ("animal-feed",ElementaryQuizType.MassConversion,"animal_feed"),("fruit-harvest",ElementaryQuizType.MassConversion,"mango"),
            ("juice",ElementaryQuizType.CapacityConversion,"orange_juice"),("watering",ElementaryQuizType.CapacityConversion,"water"),
            ("gift-cord",ElementaryQuizType.LengthConversion,"cord")};
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            foreach(var (context,type,id) in measurement)
            {
                var q=new ElementaryQuizGenerator(new(4)).GenerateMeasurementStory(ArithmeticQuizMode.Essay,type,language,tier,context);
                Check(QuizStoryArtworkCatalogue.SourceArtwork(q,language)==id,"Connected measurement "+language+"/"+context+"/"+tier);count++;
                Check(QuizStoryArtworkCatalogue.SourceArtwork(q with {ElementaryProblem=q.ElementaryProblem! with {Kind=QuizProblemKind.Time}},language) is null,"Wrong kind must not borrow measurement context");
                if(context=="water")Check(QuizStoryArtworkCatalogue.SourceArtwork(q with {ElementaryProblem=q.ElementaryProblem! with {Type=ElementaryQuizType.MassConversion}},language) is null,"Wrong measurement category cannot borrow water");
            }
            var wire=new ElementaryQuizGenerator(new(4)).GenerateMeasurementStory(ArithmeticQuizMode.Essay,ElementaryQuizType.LengthConversion,language,tier,"fencing-wire");
            Check(QuizStoryArtworkCatalogue.SourceArtwork(wire,language)=="fencing_wire","Fencing wire has its own metal-wire picture, not soft cord");count++;
            foreach(var (context,id) in new[]{("kitchen","rice_grains"),("dairy","milk_liquid"),("juice-bottling","orange_juice"),("feed-preparation","animal_feed"),("rope-cutting","cord"),("rice-packing","rice_grains")})
            foreach(var type in new[]{ElementaryQuizType.DecimalAdd,ElementaryQuizType.DecimalSubtract,ElementaryQuizType.DecimalMultiply,ElementaryQuizType.DecimalDivide})
            {
                var q=new ElementaryQuizGenerator(new(9)).GenerateDecimalStory(ArithmeticQuizMode.Essay,type,language,tier,context);
                Check(QuizStoryArtworkCatalogue.SourceArtwork(q,language)==id,"Connected decimal "+context);count++;
            }
            foreach(var (context,id) in new[]{("reading-pages","books"),("shop-rice","rice_grains"),("juice-capacity","orange_juice"),("decoration-cord","cord"),("delivered-parcels","parcel")})
            foreach(var type in new[]{ElementaryQuizType.FractionOfNumber,ElementaryQuizType.WholeFromFraction})
            {
                var q=new ElementaryQuizGenerator(new(8)).GenerateFractionQuantityStory(ArithmeticQuizMode.Essay,type,language,tier,context);
                Check(QuizStoryArtworkCatalogue.SourceArtwork(q,language)==id,"Connected fraction "+context);count++;
                Check(QuizStoryArtworkCatalogue.SourceArtwork(q with {ElementaryProblem=q.ElementaryProblem! with {Kind=QuizProblemKind.Time}},language) is null,"Wrong kind cannot borrow fraction source");
            }
            var packing=Enumerable.Range(0,180).Select(seed=>new ElementaryQuizGenerator(new(seed)).Generate(ArithmeticQuizMode.Essay,QuizProblemKind.Remainder,ElementaryQuizType.MinimumGroups,language,tier))
                .First(q=>q.ElementaryProblem!.StoryContextId=="community");
            Check(QuizStoryArtworkCatalogue.SourceArtwork(packing,language)=="gift","Packing source metadata in both languages");count++;
            Check(QuizStoryArtworkCatalogue.SourceArtwork(packing with {ElementaryProblem=packing.ElementaryProblem! with {Kind=QuizProblemKind.Decimal}},language) is null,"Wrong kind cannot borrow packing source");
            Check(QuizStoryArtworkCatalogue.SourceArtwork(packing with {ElementaryProblem=packing.ElementaryProblem! with {Type=ElementaryQuizType.MassConversion}},language) is null,"Wrong type cannot borrow packing source");
            var kitchen=Enumerable.Range(0,180).Select(seed=>new ElementaryQuizGenerator(new(seed)).Generate(ArithmeticQuizMode.Essay,QuizProblemKind.Remainder,ElementaryQuizType.FullGroups,language,tier))
                .First(q=>q.ElementaryProblem!.StoryContextId=="kitchen");
            Check(QuizStoryArtworkCatalogue.SourceArtwork(kitchen,language)=="cake","Packing kitchen is cake, measurement kitchen is rice");count++;
        }
        Console.WriteLine($"PASS connected/new subject sources: {count} VI/EN actual C# fixtures across five tiers; kind and fencing-wire guards");return count;
    }
}
