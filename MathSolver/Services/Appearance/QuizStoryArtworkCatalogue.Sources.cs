using System.Collections.Concurrent;
using MathSolver.Models;
using MathSolver.Services.QuestionBank;

namespace MathSolver.Services;

/// <summary>Bind presentation identities to exact source List keys, not rendered/AI prose or bare units.</summary>
internal static partial class QuizStoryArtworkCatalogue
{
    /// <summary>Match an authored scene's group and exact subject, never guessed prose or a shared unit.</summary>
    internal static string? AppliedSceneArtwork(AppliedQuestionScene? scene)
    {
        if (scene is null) return null;
        if (scene.Group == QuestionKnowledgeGroup.SchoolSupplies)
            return scene.UnitId switch
            {
                "books" => "books", "notebooks" => "notebook", "pencils" => "pencil",
                "fountain-pens" => "fountain_pen", "ink-bottles" => "ink_bottle",
                "pencil-cases" => "pencil_case", "compasses" => "compass",
                "rulers" => "ruler", "satchels" => "satchel", "erasers" => "eraser",
                "color-markers" => "colored_markers",
                _ => null
            };
        if (scene.Group == QuestionKnowledgeGroup.Objects)
            return scene.UnitId switch
            {
                "footwear-pairs" => "footwear", "clothing-sets" => "clothing",
                "toothbrushes" => "toothbrush", "combs" => "comb",
                "serving-bowls" => "serving_bowl", "rice-bowls" => "rice_bowl",
                "plates" => "plate", "spoons" => "spoon", "forks" => "fork",
                "chopstick-pairs" => "chopsticks", "drinking-glasses" => "drinking_glass",
                "balls" => "ball", "marbles" => "marble",
                _ => null
            };
        if (scene.Group == QuestionKnowledgeGroup.Animals)
            return scene.UnitId switch
            {
                "chickens" => "chicken", "ducks" => "duck", "birds" => "bird", "fish" => "fish",
                "cows" => "cow", "horses" => "horse", "deer" => "deer", "dogs" => "dog",
                "cats" => "cat", "pigs" => "pig",
                "rabbits" => "rabbit", "turtles" => "turtle", "tortoises" => "tortoise", "geese" => "goose",
                _ => null
            };
        return FoodThemeCatalogue.IsTheme(scene.Group)
            ? FoodThemeCatalogue.Subjects.FirstOrDefault(subject =>
                subject.Group == scene.Group && subject.Unit.Id == scene.UnitId)?.ArtworkId
            : null;
    }

    private sealed record SubjectRow(string? Id = null, string? ContextId = null, string? NarrativeId = null,
        string? ArtworkId = null, string? Unit = null, string? Subject = null, string? RatioPart = null,
        string? Type = null, string? Scenario = null, string? Category = null,
        IReadOnlyList<string>? CategoryIds = null, IReadOnlyDictionary<string,string>? CategoryArtwork = null);
    private static readonly ConcurrentDictionary<(string List, AppLanguage Language), IReadOnlyList<SubjectRow>> SubjectCache = new();

    private static IEnumerable<SubjectRow> Subjects(AppLanguage language, params string[] lists) => lists.SelectMany(list =>
        SubjectCache.GetOrAdd((list, language), key => QuizContentCatalog.LoadList<SubjectRow>(key.List, QuizContentCatalog.Culture(key.Language))));

    private static string? SingleArtwork(IEnumerable<SubjectRow> rows)
    {
        // A missing identity in a duplicate source is ambiguity, not permission to select another row's picture.
        string?[] identities = rows.Select(row => row.ArtworkId).Distinct(StringComparer.Ordinal).ToArray();
        return identities.Length > 1 ? "" : identities.Length == 1 ? identities[0] : null;
    }

    private static string? ContextArtwork(string id, AppLanguage language, params string[] lists) =>
        SingleArtwork(Subjects(language, lists).Where(row => (row.Id ?? row.ContextId) == id));

    internal static string? PurchasedItemArtwork(MathWordProblem? word, AppLanguage language)
    {
        if(word is not {Quantity:WordProblemQuantity.Money,PracticeDetails.Purchases.Count:>0})return null;
        var purchases=word.PracticeDetails.Purchases;
        if(purchases.Select(p=>p.ItemId).Distinct(StringComparer.Ordinal).Count()!=1)return null;
        var rows=QuizContentCatalog.LoadList<PracticeMoneyItem>("PracticeMoneyItems",QuizContentCatalog.Culture(language));
        var row=rows.SingleOrDefault(r=>r.Id==purchases[0].ItemId);
        if(row?.ArtworkId is not {} art||art=="money"||purchases.Any(p=>p.ItemName!=row.Name))return null;
        return QuizStoryArtwork.Select(true,false,[],artworkId:art)==$"quiz_story_{art}.png"?art:null;
    }

    internal static string? SourceArtwork(ArithmeticQuizQuestion question, AppLanguage language)
    {
        if(question.WordProblem?.PracticeDetails is not null)
            return PurchasedItemArtwork(question.WordProblem,language);
        if (question.ProportionProblem is { NarrativeId.Length: > 0 } proportion)
            return SingleArtwork(Subjects(language, "ProportionQuizGenerator.Templates", "ProportionQuizGenerator.Templates.Extensions")
                .Where(row => row.NarrativeId == proportion.NarrativeId && row.Unit == proportion.AnswerUnit && row.Subject == proportion.SubjectName
                    && row.Type == proportion.Type.ToString() && row.Scenario == proportion.Scenario.ToString()));

        if (question.AverageProblem?.StoryContextId is { } average)
            return ContextArtwork(average, language, "AverageQuizGenerator.DirectContexts", "AverageQuizGenerator.DirectContexts.Extensions");

        if (question.PercentageProblem is { } percentage)
        {
            if (percentage.StoryContextId is { } story)
                return ContextArtwork(story, language, "PercentageQuizGenerator.PercentageStories", "PercentageQuizGenerator.PercentageStories.Extensions");
            return SingleArtwork(Subjects(language, "PercentageQuizGenerator.Contexts", "PercentageQuizGenerator.Contexts.Extensions")
                .Where(row => percentage.Type switch
                {
                    PercentageQuizType.FindPercentageRatio => row.RatioPart == percentage.SubjectName,
                    PercentageQuizType.FindPercentageValue => row.RatioPart == percentage.AnswerUnit,
                    PercentageQuizType.FindWholeFromPercentageValue => row.Unit == percentage.AnswerUnit,
                    _ => false
                }));
        }

        if (question.ElementaryProblem is not { StoryContextId: { } context } elementary) return null;
        if(elementary.Kind==QuizProblemKind.Data&&elementary.DataChart?.Profile is {} profile
            && elementary.Type is (ElementaryQuizType.ReadTable or ElementaryQuizType.ReadBarChart or ElementaryQuizType.ReadPieChart or ElementaryQuizType.ReadPictograph)
            && profile.Type==elementary.Type&&profile.Language==language&&profile.ContextId==context
            && profile.QuestionKind==DataChartQuestionKind.CategoryValue&&profile.TargetCategoryIds.Count==1)
        {
            string target=profile.TargetCategoryIds[0];
            var rows=Subjects(language,"DataChartContexts").Where(r=>r.ContextId==context
                &&r.CategoryIds is not null&&r.CategoryIds.ToHashSet(StringComparer.Ordinal).SetEquals(profile.CategoryIds)
                &&r.CategoryIds.Contains(target)).ToArray();
            return SingleArtwork(rows.Select(r=>r with {ArtworkId=r.CategoryArtwork?.GetValueOrDefault(target)}));
        }
        if (elementary.Kind == QuizProblemKind.Measurement && ElementaryQuizGenerator.MeasurementStoryTypes.Contains(elementary.Type))
            return SingleArtwork(Subjects(language, "MeasurementStoryContexts", "MeasurementStoryContexts.Extensions")
                .Where(row => row.Id == context && row.Category == ElementaryQuizGenerator.MeasurementCategory(elementary.Type)));
        if (elementary.Kind == QuizProblemKind.Remainder && elementary.Type is (ElementaryQuizType.MinimumGroups
            or ElementaryQuizType.FullGroups or ElementaryQuizType.Leftovers or ElementaryQuizType.QuotientRemainder))
            return ContextArtwork(context, language, "ElementaryQuizGenerator.PackingStories", "ElementaryQuizGenerator.PackingStories.Extensions");
        if (context.StartsWith(DiscountOpenPilot.Prefix, StringComparison.Ordinal)
            && elementary.Kind == QuizProblemKind.MultiStep && (int)elementary.Type == DiscountOpenPilot.Variant)
            return ContextArtwork(context[DiscountOpenPilot.Prefix.Length..], language, DiscountOpenPilot.ListName);
        if (elementary.Kind == QuizProblemKind.Decimal && elementary.Type is (ElementaryQuizType.DecimalAdd or ElementaryQuizType.DecimalSubtract
            or ElementaryQuizType.DecimalMultiply or ElementaryQuizType.DecimalDivide))
        {
            if (context.StartsWith(DecimalStoryPilot.Prefix, StringComparison.Ordinal))
            {
                if (!DecimalStoryPilot.Profiles(elementary.Type, language).Contains(context, StringComparer.Ordinal)) return null;
                return ContextArtwork(context[(context.LastIndexOf('/') + 1)..], language, DecimalStoryPilot.ContextList);
            }
            return ContextArtwork(context, language, "DecimalStoryContexts", "DecimalStoryContexts.Extensions");
        }
        if (elementary.Kind == QuizProblemKind.FractionSkills && elementary.Type is (ElementaryQuizType.FractionOfNumber
            or ElementaryQuizType.WholeFromFraction or ElementaryQuizType.FractionRemaining))
            return ContextArtwork(context, language, "FractionQuantityContexts", "FractionQuantityContexts.Extensions");
        return null;
    }
}
