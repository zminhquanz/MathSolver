using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private ElementaryQuizContract AddTimeStory(ElementaryQuizContract contract)
    {
        bool late = contract.Reasoning?.Tier >= CurriculumTier.ThreeStars;
        string[] ids = late ? ["tourism", "events", "schedule"] : ["library", "sports", "craft", "schedule"];
        string id = ids[NextContextVariant(contract.Type, contract.Language, "time-story", ids.Length)];
        string introduction = id switch
        {
            "library" => QuizContentCatalog.Text(contract.Language, "ElementaryQuizGenerator.ContextStories.AddTimeStory.012"),"sports" => QuizContentCatalog.Text(contract.Language, "ElementaryQuizGenerator.ContextStories.AddTimeStory.013"),            "craft" => QuizContentCatalog.Text(contract.Language, "ElementaryQuizGenerator.ContextStories.AddTimeStory.014"),"events" => QuizContentCatalog.Text(contract.Language, "ElementaryQuizGenerator.ContextStories.AddTimeStory.015"),            "tourism" => QuizContentCatalog.Text(contract.Language, "ElementaryQuizGenerator.ContextStories.AddTimeStory.016"),_ => QuizContentCatalog.Text(contract.Language, "ElementaryQuizGenerator.ContextStories.AddTimeStory.017")        };
        return contract with { ProblemText = introduction + contract.ProblemText, StoryContextId = id };
    }

    private sealed record PackingStory(string Id, string Item, string Container, int Capacity, int MaximumSize);
    private static IReadOnlyList<PackingStory> PackingStories(AppLanguage language) => QuizContentCatalog.LoadList<PackingStory>("ElementaryQuizGenerator.PackingStories", QuizContentCatalog.Culture(language));
    private ElementaryQuizContract CreateRemainderStory(ElementaryQuizType type, AppLanguage language, CurriculumTier tier)
    {
        var t = new DifficultyBuilder(QuizProblemKind.Remainder, type, language, tier);
        var s = PackingStories(language)[NextContextVariant(type, language, "packing-story", PackingStories(language).Count)];
        int level = (int)tier, size = _random.Next(2, Math.Min(s.MaximumSize - (level == 5 ? 3 : 0), 4 + level * 4) + 1);
        int full = _random.Next(1, Math.Min(2 + level * 3, s.Capacity / size));
        int remainder = level == 1 ? 0 : _random.Next(1, size), total = full * size + remainder;
        string item = s.Item, container = s.Container;
        string amount, problem;
        if (level <= 2)
        {
            amount = t.Given("total", total, item);
            problem = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ContextStories.CreateRemainderStory.001", ("amount", $"{amount}"), ("item", $"{item}"), ("container", $"{container}"));
        }
        else
        {
            int first = Math.Max(1, total / 2);
            string a = t.Given("first-batch", first, item), b = t.Given("second-batch", total - first, item);
            amount = $"({a}+{b})";
            problem = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ContextStories.CreateRemainderStory.002", ("a", $"{a}"), ("b", $"{b}"), ("item", $"{item}"));
            if (level >= 4)
            {
                int removed = _random.Next(1, Math.Max(2, total / 3));
                total -= removed; full = Math.DivRem(total, size, out remainder);
                string used = t.Given("already-accommodated", removed, item);
                problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ContextStories.CreateRemainderStory.003", ("used", $"{used}"), ("item", $"{item}"));
                amount = $"({amount}-{used})";
            }
            t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ContextStories.CreateRemainderStory.004"), amount, item);
        }
        string per;
        if (level == 5) {
            int reserved = _random.Next(1, 4);
            string nominal = t.Given("nominal-capacity", size + reserved, item), excluded = t.Given("reserved-capacity", reserved, item);
            per = $"({nominal}-{excluded})";
            problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ContextStories.CreateRemainderStory.005", ("container", $"{container}"), ("nominal", $"{nominal}"), ("item", $"{item}"), ("excluded", $"{excluded}"));
            // Keep nominal physical capacities within the selected context.
            // The working capacity is derived rather than stated as another given.
        }
        else {
            per = t.Given("capacity", size, item);
            problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ContextStories.CreateRemainderStory.006", ("container", $"{container}"), ("per", $"{per}"), ("item", $"{item}"));
        }
        // Record the quotient/remainder as derived constants, never as supplied facts.
        t.Constant(full, remainder);
        string q = $"({amount}-{remainder})/{per}", r = $"{amount}-{full}*{per}";
        if (type == ElementaryQuizType.MinimumGroups)
        {
            problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ContextStories.CreateRemainderStory.007", ("container", $"{container}"));
            t.Answer(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ContextStories.CreateRemainderStory.008"), q + (remainder > 0 ? "+1" : ""), container);
        }
        else
        {
            problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ContextStories.CreateRemainderStory.009", ("container", $"{container}"), ("item", $"{item}"));
            t.Answer(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ContextStories.CreateRemainderStory.010"), q, container);
            t.Answer(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ContextStories.CreateRemainderStory.011"), r, item);
        }
        return t.Build("packing-" + level, problem) with { StoryContextId = s.Id };
    }
}
