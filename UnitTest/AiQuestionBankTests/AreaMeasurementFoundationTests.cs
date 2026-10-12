using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using SQLite;
using System.Text.Json;

internal static class AreaMeasurementFoundationTests
{
    private sealed class PreparedTemplate
    {
        [PrimaryKey] public string Hash { get; set; } = "";
        public string ContractJson { get; set; } = "";
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public static void Run()
    {
        var directory = Path.GetFullPath("artifacts/verification/area-measurement-foundation");
        Directory.CreateDirectory(directory);
        // Preparation storage only: exercise real SQLite serialization and uniqueness
        // without enabling an untested AI family in the production BasicQuestionBank.
        string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".db3");
        using var db = new SQLiteConnection(path);
        db.CreateTable<PreparedTemplate>();
        int count = 0;
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var context in AreaMeasurementTemplates.Contexts(language))
        {
            Check(!ReasoningStoryCatalogue.Variants(BankQuestionFamily.Measurement, tier).Contains((int)ElementaryQuizType.AreaConversion),
                "Area was prematurely enabled in AI generation.");
            var profile = new AreaMeasurementProfile(context.Id, tier, language);
            var generator = new ElementaryQuizGenerator(new(47));
            var original = AreaMeasurementTemplates.Draft(profile);
            var alternatives = AreaMeasurementTemplates.Choices(language, AreaMeasurementTemplates.InitialId(tier));
            foreach (string initial in alternatives)
            {
                var draft = original with { Initial = initial };
                var c = generator.GenerateAreaMeasurement(ArithmeticQuizMode.Essay, profile, draft).ElementaryProblem!;
                string hash = AreaMeasurementTemplates.ProseHash(profile, draft);
                var row = new PreparedTemplate { Hash = hash, ContractJson = JsonSerializer.Serialize(c.AreaMeasurement) };
                Check(db.Insert(row) == 1, "Prepared template was not stored.");
                var fresh = generator.FreshAreaMeasurement(ArithmeticQuizMode.Essay, c.AreaMeasurement!).ElementaryProblem!;
                Check(AreaMeasurementTemplates.ProseHash(fresh.AreaMeasurement!.Profile, draft) == hash, "Fresh values changed wording identity.");
                Check(db.Execute("INSERT OR IGNORE INTO PreparedTemplate (Hash,ContractJson) VALUES (?,?)", hash,
                    JsonSerializer.Serialize(fresh.AreaMeasurement)) == 0, "SQLite admitted duplicate words with different numbers.");
                count++;
            }
        }
        foreach (var row in db.Table<PreparedTemplate>().ToArray())
        {
            var source = JsonSerializer.Deserialize<AreaMeasurementQuestionContract>(row.ContractJson)!;
            foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
            {
                var generator = new ElementaryQuizGenerator(new(105));
                var original = generator.RecreateAreaMeasurement(mode, source).ElementaryProblem!;
                var fresh = generator.FreshAreaMeasurement(mode, source).ElementaryProblem!;
                AreaMeasurementQuestionValidator.Validate(original);
                AreaMeasurementQuestionValidator.Validate(fresh);
                Check(!fresh.Facts.SequenceEqual(original.Facts) && fresh.StoryContextId == source.Profile.ContextId,
                    "SQLite replay changed context or kept old facts.");
                Check(AreaMeasurementTemplates.ProseHash(source.Profile, source.Draft!) == row.Hash, "Replay changed template identity.");
            }
        }
        Console.WriteLine($"PASS {count} prepared area templates in real SQLite; uniqueness, bilingual replay and fresh facts in all grading modes; AI remains disabled.");
    }
}
