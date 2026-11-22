using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;

/// <summary>
/// Real generator-backed bilingual fixtures for subject-specific story illustrations.
/// The expected artwork identity is specified independently from the generated question.
/// </summary>
internal static class RemainingSubjectFixtures
{
    internal sealed record Sample(string Name, string Artwork, ArithmeticQuizQuestion Question);

    internal static IEnumerable<Sample> Cases(AppLanguage language)
    {
        // Measurement contexts share units, but each named subject owns its own illustration.
        var measurements = new (string Context, ElementaryQuizType Type, string Artwork)[]
        {
            ("water", ElementaryQuizType.CapacityConversion, "water"),
            ("bakery", ElementaryQuizType.MassConversion, "flour"),
            ("agriculture", ElementaryQuizType.MassConversion, "vegetables"),
            ("environment", ElementaryQuizType.MassConversion, "recycled_paper"),
            ("decoration", ElementaryQuizType.LengthConversion, "ribbon"),
            ("craft", ElementaryQuizType.LengthConversion, "fabric"),
            ("dairy", ElementaryQuizType.CapacityConversion, "milk_liquid"),
            ("kitchen", ElementaryQuizType.MassConversion, "rice_grains"),
            ("animal-feed", ElementaryQuizType.MassConversion, "animal_feed"),
            ("fruit-harvest", ElementaryQuizType.MassConversion, "mango"),
            ("juice", ElementaryQuizType.CapacityConversion, "orange_juice"),
            ("gift-cord", ElementaryQuizType.LengthConversion, "cord"),
            ("fencing-wire", ElementaryQuizType.LengthConversion, "fencing_wire")
        };
        foreach (var (context, type, artwork) in measurements)
            yield return new("measurement/" + context, artwork,
                new ElementaryQuizGenerator(new Random(4)).GenerateMeasurementStory(
                    ArithmeticQuizMode.Essay, type, language, CurriculumTier.FourStars, context));

        foreach (var (context, artwork) in new[]
        {
            ("reading-pages", "books"), ("delivered-parcels", "parcel")
        })
            yield return new("fraction/" + context, artwork,
                new ElementaryQuizGenerator(new Random(8)).GenerateFractionQuantityStory(
                    ArithmeticQuizMode.Essay, ElementaryQuizType.FractionOfNumber,
                    language, CurriculumTier.FourStars, context));

        // A chart picture belongs only to the single category actually asked about.
        foreach (var (context, artwork) in new[]
        {
            ("farm-animals", "goose"), ("stationery-types", "colored_markers")
        })
        {
            var question = Enumerable.Range(0, 128)
                .Select(seed => new ElementaryQuizGenerator(new Random(seed)).GenerateDataChart(
                    ArithmeticQuizMode.Essay, ElementaryQuizType.ReadTable,
                    language, CurriculumTier.TwoStars, context, DataChartQuestionKind.CategoryValue))
                .FirstOrDefault(q => QuizStoryArtworkCatalogue.SourceArtwork(q, language) == artwork)
                ?? throw new InvalidOperationException("Could not generate chart category: " + context);
            yield return new("chart/" + context, artwork, question);
        }

        const string printerNarrative = "ProportionQuizGenerator.Narrative.039";
        var proportionTier = Enum.GetValues<CurriculumTier>().First(t =>
            ProportionQuizGenerator.NarrativeIds(ProportionQuizType.Inverse, t, language)
                .Contains(printerNarrative));
        yield return new("proportion/printers", "printer",
            new ProportionQuizGenerator(new Random(17)).GenerateNarrative(
                ArithmeticQuizMode.Essay, ProportionQuizType.Inverse, language,
                new QuizCurriculumContext(proportionTier, false), printerNarrative));

        var money = ExpandedArithmeticCatalogue.Create(new(QuestionKnowledgeGroup.Money),
            ArithmeticOperation.Add, CurriculumTier.FiveStars, language,
            new Random(65), "school-laptop");
        var draft = ExpandedArithmeticCatalogue.Draft(money);
        yield return new("purchase/school-laptop", "laptop",
            money.ToPracticeQuestion(draft.ToWordProblem(money), ArithmeticQuizMode.Essay, new Random(3)));

        foreach (var (context, artwork) in new[]
        {
            ("car", "car"), ("bus", "bus"), ("bicycle", "bicycle")
        })
            yield return new("motion/" + context, artwork,
                new MotionQuizGenerator(new Random(31)).GenerateNarrative(
                    ArithmeticQuizMode.Essay, language, MotionQuizType.Basic,
                    new QuizCurriculumContext(CurriculumTier.ThreeStars, false),
                    "motion-open/distance/" + context));
    }
}
