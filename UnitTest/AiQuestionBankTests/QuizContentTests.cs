using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.Text.Json;
using System.Text.Json.Nodes;

internal static class QuizContentTests
{
    internal static QuizContentPack Read(string path)
    {
        using var stream = File.OpenRead(path);
        return QuizContentCatalog.ReadPack(stream);
    }

    internal static void Run()
    {
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        void Reject(Action action, string label)
        {
            try { action(); } catch (InvalidDataException) { return; }
            throw new InvalidOperationException("Invalid pack accepted: " + label);
        }
        QuizContentPack Load(string culture)
        {
            using var stream = typeof(QuizContentCatalog).Assembly.GetManifestResourceStream("QuizContent." + culture + ".json")!;
            return QuizContentCatalog.ReadPack(stream);
        }
        var vi = Load("vi-VN"); var en = Load("en-US");
        foreach (var pack in new[] { vi, en })
        {
            QuizContentValidation.CheckTranslation(pack, pack);
            foreach (var entry in pack.Texts)
            {
                var values = entry.Variables.Select(v => (v, "<{data}>:" + v)).ToArray();
                var rendered = QuizContentCatalog.Text(pack.Culture, entry.Id, values);
                Check(!entry.Variables.Any(v => rendered.Contains("{" + v + "}", StringComparison.Ordinal)), "Unrendered value: " + entry.Id);
            }
        }
        var first = en.Texts.First(t => t.Variables.Length == 0);
        Check(QuizContentCatalog.Text("fr-FR", first.Id) == first.Text, "Missing culture fallback failed.");
        Reject(() => QuizContentCatalog.Text("en-US", "unknown.text.id"), "missing key");
        var variable = en.Texts.First(t => t.Variables.Length > 0);
        Reject(() => QuizContentCatalog.Text("en-US", variable.Id), "missing values");
        Reject(() => QuizContentCatalog.Validate(new() { Version = 1, Culture = "en-US", Texts = [first, first] }), "duplicate key");
        Reject(() => QuizContentCatalog.Validate(new() { Version = 1, Culture = "en-US", Texts = [new("typo", "{wrong_slot}", [])] }), "unknown slot");
        Reject(() => QuizContentCatalog.Validate(new() { Version = 1, Culture = "vi-VN",
            Texts = [new("unit-text", "{a} {unit}", ["a", "unit"], new() { ["kg"] = "{a}" })] }),
            "unit wording lost a fact or dimension");
        Reject(() => QuizContentCatalog.Validate(new() { Version = 1, Culture = "vi-VN",
            Texts = [new("unit-text", "{a} {unit}", ["a", "unit"], new() { ["kg"] = "{unit} {a}" })] }),
            "unit wording swapped ordered roles");
        Reject(() => QuizContentCatalog.Validate(new() { Version = 8, Culture = "en-US" }), "version");
        var changed = new QuizContentPack { Version = 1, Culture = "fr-FR", Texts = en.Texts.Skip(1).ToList(), Lists = en.Lists };
        Reject(() => QuizContentValidation.CheckTranslation(changed, en), "incomplete translation");
        QuizContentPack ChangeRow(string list, string field, int value)
        {
            var rows = JsonNode.Parse(en.Lists[list].GetRawText())!;
            rows[0]![field] = value;
            return new() { Version = 1, Culture = "fr-FR", Texts = en.Texts,
                Lists = new(en.Lists) { [list] = JsonSerializer.SerializeToElement(rows) } };
        }
        Reject(() => QuizContentCatalog.Validate(ChangeRow("MotionQuizGenerator.UnitProfiles", "TimeDivisor", 0)), "invalid time conversion");
        Reject(() => QuizContentValidation.CheckTranslation(ChangeRow("AverageQuizGenerator.DirectContexts", "Capacity", 1), en), "changed mathematical metadata");
        var charts = QuizContentCatalog.LoadList<DataChartStoryContext>("DataChartContexts", "vi-VN");
        Check(ReferenceEquals(charts, QuizContentCatalog.LoadList<DataChartStoryContext>("DataChartContexts", "vi-VN")), "Lists are not cached.");
        Check(QuizContentCatalog.LoadList<AppliedQuestionScene>("AppliedScenes").Count == 241, "Lost applied scenes.");
        Check(QuizContentCatalog.LoadList<FractionQuestionCatalogue.Scene>("FractionScenes").Count == 84, "Lost fraction scenes.");
        Console.WriteLine($"Quiz JSON content passed: {vi.Texts.Count} keys per language, 325 scenes, typed/cached lists, fallback, slot rendering and malformed-pack rejection.");
    }
}
