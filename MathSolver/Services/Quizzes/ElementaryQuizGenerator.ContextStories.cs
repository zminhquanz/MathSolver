using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    internal static string[] TimeStoryContextIds(CurriculumTier tier, bool expanded = true) => expanded
        ? TimeActivities(AppLanguage.Vietnamese).Where(c => c.MinimumStar <= (int)tier).Select(c => c.Id).ToArray()
        : tier >= CurriculumTier.ThreeStars
        ? ["tourism", "events", "schedule"] : ["library", "sports", "craft", "schedule"];

    private ElementaryQuizContract AddTimeStory(ElementaryQuizContract contract, string contextId = "")
    {
        string[] ids = TimeStoryContextIds(contract.Reasoning!.Tier, false);
        string id = contextId.Length == 0 ? ids[NextContextVariant(contract.Type, contract.Language, "time-story", ids.Length)]
            : ids.Contains(contextId) ? contextId : throw new ArgumentException("InvalidTimeStoryProfile");
        string introduction = id switch
        {
            "library" => QuizContentCatalog.Text(contract.Language, "ElementaryQuizGenerator.ContextStories.AddTimeStory.012"),"sports" => QuizContentCatalog.Text(contract.Language, "ElementaryQuizGenerator.ContextStories.AddTimeStory.013"),            "craft" => QuizContentCatalog.Text(contract.Language, "ElementaryQuizGenerator.ContextStories.AddTimeStory.014"),"events" => QuizContentCatalog.Text(contract.Language, "ElementaryQuizGenerator.ContextStories.AddTimeStory.015"),            "tourism" => QuizContentCatalog.Text(contract.Language, "ElementaryQuizGenerator.ContextStories.AddTimeStory.016"),_ => QuizContentCatalog.Text(contract.Language, "ElementaryQuizGenerator.ContextStories.AddTimeStory.017")        };
        return contract with { ProblemText = introduction + contract.ProblemText, StoryContextId = id };
    }

    internal sealed record PackingStory(string Id, string Item, string Container, int Capacity, int MaximumSize, string Activity = "");
    internal static IReadOnlyList<PackingStory> PackingStories(AppLanguage language, bool expanded = true) => NarrativeContextExpansion.Load<PackingStory>("ElementaryQuizGenerator.PackingStories", language, expanded);
    private ElementaryQuizContract CreateRemainderStory(ElementaryQuizType type, AppLanguage language, CurriculumTier tier, string contextId = "")
    {
        var t = new DifficultyBuilder(QuizProblemKind.Remainder, type, language, tier);
        var contexts = PackingStories(language, _expandActivityStories);
        var s = contextId.Length == 0 ? contexts[NextContextVariant(type, language, "packing-story", contexts.Count)]
            : contexts.FirstOrDefault(context => context.Id == contextId) ?? throw new ArgumentException("InvalidRemainderStoryProfile");
        int level = (int)tier, size = _random.Next(2, Math.Min(s.MaximumSize - (level == 5 ? 3 : 0), 4 + level * 4) + 1);
        int full = _expandActivityStories ? _random.Next(1, Math.Min(2 + level * 3, (s.Capacity - size + 1) / size) + 1)
            : _random.Next(1, Math.Min(2 + level * 3, s.Capacity / size));
        int remainder = level == 1 ? 0 : _random.Next(_expandActivityStories && level == 2 ? 1 : 0, size), total = full * size + remainder;
        string Text(string suffix, params (string Key, string Value)[] values) => QuizContentCatalog.Text(language,
            "ElementaryQuizGenerator.ContextStories.CreateRemainderStory."
            + (_expandActivityStories && s.Activity.Length > 0 ? "Activity." + s.Activity + "." : "") + suffix, values);
        string item = s.Item, container = s.Container;
        string amount, problem;
        if (level <= 2)
        {
            amount = t.Given("total", total, item);
            problem = Text("001", ("amount", $"{amount}"), ("item", $"{item}"), ("container", $"{container}"));
        }
        else
        {
            int first = Math.Max(1, total / 2);
            string a = t.Given("first-batch", first, item), b = t.Given("second-batch", total - first, item);
            amount = $"({a}+{b})";
            problem = Text("002", ("a", $"{a}"), ("b", $"{b}"), ("item", $"{item}"));
            if (level >= 4)
            {
                int removed = _random.Next(1, Math.Max(2, total / 3));
                total -= removed; full = Math.DivRem(total, size, out remainder);
                string used = t.Given("already-accommodated", removed, item);
                problem += Text("003", ("used", $"{used}"), ("item", $"{item}"));
                amount = $"({amount}-{used})";
            }
            t.Step(Text("004"), amount, item);
        }
        string per;
        if (level == 5) {
            int reserved = _random.Next(1, 4);
            string nominal = t.Given("nominal-capacity", size + reserved, item), excluded = t.Given("reserved-capacity", reserved, item);
            per = $"({nominal}-{excluded})";
            problem += Text("005", ("container", $"{container}"), ("nominal", $"{nominal}"), ("item", $"{item}"), ("excluded", $"{excluded}"));
            // Keep nominal physical capacities within the selected context.
            // The working capacity is derived rather than stated as another given.
        }
        else {
            per = t.Given("capacity", size, item);
            problem += Text("006", ("container", $"{container}"), ("per", $"{per}"), ("item", $"{item}"));
        }
        // Record the quotient/remainder as derived constants, never as supplied facts.
        t.Constant(full, remainder);
        string q = $"({amount}-{remainder})/{per}", r = $"{amount}-{full}*{per}";
        if (type == ElementaryQuizType.MinimumGroups)
        {
            problem += Text("007", ("container", $"{container}"));
            t.Answer(Text("008"), q + (remainder > 0 ? "+1" : ""), container);
        }
        else if (type == ElementaryQuizType.FullGroups)
        {
            problem += Text("019", ("container", container));
            t.Answer(Text("010"), q, container);
        }
        else if (type == ElementaryQuizType.Leftovers)
        {
            problem += Text("018",
                ("container", container), ("item", item));
            t.Step(Text("010"), q, container);
            t.Answer(Text("011"), r, item);
        }
        else
        {
            problem += Text("009", ("container", $"{container}"), ("item", $"{item}"));
            t.Answer(Text("010"), q, container);
            t.Answer(Text("011"), r, item);
        }
        return t.Build("packing-" + level, problem) with { StoryContextId = s.Id };
    }
}
