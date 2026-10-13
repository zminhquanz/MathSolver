using System.Text.Json;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static partial class ExpandedArithmeticTests
{
    public static async Task RunModelAsync(string model)
    {
        string directory = Path.GetFullPath(Path.Combine("artifacts", "verification", "expanded-arithmetic-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(directory);
        Console.WriteLine("Actual GGUF inference; isolated SQLite. Evidence: " + directory);
        await using var session = new ModelTestSession();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(40));
        await session.LoadAsync(model, timeout.Token);
        var results = new List<object>();
        try
        {
            foreach (var language in Enum.GetValues<AppLanguage>())
            foreach (var tier in new[] { CurriculumTier.FourStars, CurriculumTier.FiveStars })
            foreach (var operation in Enum.GetValues<ArithmeticOperation>())
            {
                var group = operation switch { ArithmeticOperation.Add when tier == CurriculumTier.FourStars => QuestionKnowledgeGroup.Data,
                    ArithmeticOperation.Subtract when tier == CurriculumTier.FourStars => QuestionKnowledgeGroup.Production,
                    ArithmeticOperation.Multiply when tier == CurriculumTier.FourStars => QuestionKnowledgeGroup.Objects,
                    ArithmeticOperation.Multiply => QuestionKnowledgeGroup.Production,
                    ArithmeticOperation.Divide when tier == CurriculumTier.FourStars => QuestionKnowledgeGroup.Packaging,
                    _ => QuestionKnowledgeGroup.Money };
                string context = group switch { QuestionKnowledgeGroup.Data => "population-statistics", QuestionKnowledgeGroup.Production => "annual-paper-production",
                    QuestionKnowledgeGroup.Objects => "classroom", QuestionKnowledgeGroup.Packaging => "packing-network", _ => "school-laptop" };
                PracticeArithmeticStyle? style = tier == CurriculumTier.FourStars && operation is ArithmeticOperation.Add or ArithmeticOperation.Subtract
                    ? PracticeArithmeticStyle.Mental : null;
                var c = ExpandedArithmeticCatalogue.Create(new(group), operation, tier, language, new(61), context, style);
                string name = $"{language}-{(int)tier}-{operation}";
                var store = new QuestionBankStore(Path.Combine(directory, name + ".db3"));
                var excluded = new HashSet<string>();
                for (int pass = 0; pass < (operation == ArithmeticOperation.Add && tier == CurriculumTier.FiveStars ? 2 : 1); pass++)
                {
                    var prompt = BasicQuestionPrompt.Build(c, excludedProse: excluded);
                    var output = await session.GenerateAsync(c, prompt, timeout.Token, excluded);
                    await File.WriteAllTextAsync(Path.Combine(directory, name + "-" + pass + ".txt"), prompt + "\n\n" + output.Raw + "\n" + output.Validation.ErrorCode, timeout.Token);
                    TestAssert.That(output.Validation.IsValid, "Real model output rejected: " + name + ": " + output.Validation.ErrorCode);
                    var d = output.Validation.Draft!;
                    var saved = new ValidatedBankQuestion(c, d, output.Raw, session.Runtime.ModelName, DateTime.UtcNow);
                    TestAssert.That(await store.InsertAsync(saved, timeout.Token), "Native expanded SQLite insert failed.");
                    TestAssert.That(!await store.InsertAsync(saved with { Contract = c.FreshFacts(new(19)) }, timeout.Token), "Fresh-number duplicate inserted.");
                    var read = await store.TakeForProfileAsync(operation, tier, language, new(group), timeout.Token);
                    TestAssert.That(read is not null, "Native expanded SQLite read failed.");
                    Verify(read!.Contract.FreshFacts(new(94)), read.Draft);
                    Verify(c.FreshFacts(new(63)), d);
                    string hash = QuestionProseIdentity.Hash(c, d);
                    TestAssert.That(excluded.Add(hash), "Native model repeated excluded wording.");
                    results.Add(new { Case = name, Pass = pass, Contract = c, Metrics = output.Metrics, Validation = "Valid", Hash = hash });
                    Console.WriteLine($"PASS {name}/{pass}: tokens={output.Metrics.GeneratedTokens}, tok/s={output.Metrics.TokensPerSecond:F2}, SQLite/fresh facts/grading/duplicates.");
                }
            }
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine($"PASS {results.Count} real expanded arithmetic generations in Vietnamese/English at four/five stars.");
    }
}
