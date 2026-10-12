using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

public sealed record AreaMeasurementContext(string Id, string Subject, string LargeUnitId,
    string SmallUnitId, int MaximumQuantity);

/// <summary>Preparation for AI: finite reviewed prose, explicit roles and serializable contexts.</summary>
public static class AreaMeasurementTemplates
{
    public const string ContextList = "AreaMeasurementContexts";
    public const string PhrasingList = "Narrative.AreaMeasurement.Phrasings";
    public const string VocabularyList = "AreaMeasurementVocabulary";
    public const string Prefix = "AreaMeasurementStories.";

    public static IReadOnlyList<AreaMeasurementContext> Contexts(AppLanguage language) =>
        QuizContentCatalog.LoadList<AreaMeasurementContext>(ContextList, QuizContentCatalog.Culture(language));

    public static AreaMeasurementContext Context(AreaMeasurementProfile p)
    {
        if (!Enum.IsDefined(p.Tier) || !Enum.IsDefined(p.Language))
            throw new ArgumentException("Invalid area measurement profile.");
        var context = Contexts(p.Language).SingleOrDefault(c => c.Id == p.ContextId)
            ?? throw new ArgumentException("Unknown area measurement context.");
        decimal factor = MeasurementQuantityRules.Convert(1, "area", context.LargeUnitId, "area", context.SmallUnitId);
        if (factor <= 1 || context.MaximumQuantity < 4) throw new InvalidDataException("Invalid area measurement units or bounds.");
        return context;
    }

    internal static string InitialId(CurriculumTier tier) => tier switch {
        CurriculumTier.OneStar => "InitialLarge", CurriculumTier.TwoStars => "InitialSmall", _ => "InitialMixed"
    };
    internal static string QuestionId(CurriculumTier tier) => tier is CurriculumTier.TwoStars or CurriculumTier.FiveStars ? "QuestionLarge" : "QuestionSmall";
    internal static string[] LeadIds(CurriculumTier tier) => (int)tier <= 2 ? ["ResultLead"]
        : tier == CurriculumTier.FiveStars ? ["InitialConversionLead", "AddedConversionLead", "CombinedLead", "ResultLead"]
        : ["InitialConversionLead", "AddedConversionLead", "ResultLead"];

    public static IReadOnlyList<string> Choices(AppLanguage language, string suffix)
    {
        var culture = QuizContentCatalog.Culture(language);
        var row = QuizContentCatalog.LoadList<ReviewedNarrativePhrasings>(PhrasingList, culture).Single(r => r.Id == Prefix + suffix);
        return new[] { QuizContentCatalog.Entry(culture, row.Id).Text }.Concat(row.Alternatives).Distinct().ToArray();
    }

    public static AreaMeasurementDraft Draft(AreaMeasurementProfile p)
    {
        _ = Context(p);
        string Text(string id) => Choices(p.Language, id)[0];
        return new(Text(InitialId(p.Tier)), (int)p.Tier >= 3 ? Text("Added") : null,
            (int)p.Tier >= 4 ? Text("Removed") : null, Text(QuestionId(p.Tier)), LeadIds(p.Tier).Select(Text).ToArray());
    }

    public static bool IsValidDraft(AreaMeasurementProfile p, AreaMeasurementDraft draft)
    {
        _ = Context(p);
        static string Normalize(string s) => Regex.Replace(s.Normalize().Trim(), @"\s+", " ").ToLowerInvariant();
        bool Match(string? value, string id) => value is not null && Choices(p.Language, id).Any(v => Normalize(v) == Normalize(value));
        return Match(draft.Initial, InitialId(p.Tier)) && Match(draft.Question, QuestionId(p.Tier))
            && ((int)p.Tier >= 3 ? Match(draft.Added, "Added") : draft.Added is null)
            && ((int)p.Tier >= 4 ? Match(draft.Removed, "Removed") : draft.Removed is null)
            && draft.SolutionLeads is not null && draft.SolutionLeads.Count == LeadIds(p.Tier).Length
            && draft.SolutionLeads.Zip(LeadIds(p.Tier)).All(pair => Match(pair.First, pair.Second));
    }

    internal static IReadOnlyDictionary<string, string> Bindings(AreaMeasurementProfile p, IReadOnlyList<ElementaryGivenValue> givens)
    {
        var context = Context(p);
        var units = MeasurementEngine.GetCategory("area").Units;
        var result = new Dictionary<string, string> {
            ["subject"] = context.Subject,
            ["large_unit"] = units.Single(u => u.Id == context.LargeUnitId).Symbol,
            ["small_unit"] = units.Single(u => u.Id == context.SmallUnitId).Symbol
        };
        foreach (var given in givens) result.Add(given.Role.Replace('-', '_'), given.Value);
        return result;
    }

    internal static string Render(string text, IReadOnlyDictionary<string, string> bindings) =>
        Regex.Replace(text, @"\{([a-z_]+)\}", m => bindings.TryGetValue(m.Groups[1].Value, out var value)
            ? value : throw new InvalidDataException("Unbound area measurement role: " + m.Value));

    public static string ProseHash(AreaMeasurementProfile p, AreaMeasurementDraft draft)
    {
        if (!IsValidDraft(p, draft)) throw new InvalidDataException("Changed area measurement relation or target.");
        var bindings = new Dictionary<string, string>(Bindings(p, []));
        foreach (var role in new[] { "initial_large", "initial_small", "added_large", "removed_small" }) bindings.Add(role, "quantity");
        string text = Render(string.Join(" ", new[] { draft.Initial, draft.Added, draft.Removed, draft.Question }.Where(t => t is not null)), bindings);
        string canonical = string.Join(" ", Regex.Matches(text.Normalize().ToLowerInvariant(), @"[\p{L}\p{M}\p{N}]+")
            .Select(m => m.Value));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(p.Language + "\n" + canonical)));
    }
}
