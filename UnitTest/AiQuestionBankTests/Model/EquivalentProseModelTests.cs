using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using System.Text;
using System.Text.Json;

internal static class EquivalentProseModelTests
{
    internal static void CheckEvidence(string directory)
    {
        using var database = new SQLite.SQLiteConnection(Path.Combine(directory, "model.db3"), SQLite.SQLiteOpenFlags.ReadOnly);
        var rows = database.Query<EvidenceRow>("SELECT ContractJson, RawJson FROM BasicQuestionBank");
        if (rows.Count != 10) throw new Exception("Native evidence is incomplete");
        foreach (var row in rows)
        {
            var c = JsonSerializer.Deserialize<BasicQuestionContract>(row.ContractJson)!;
            for (int seed = 0; seed < 3; seed++)
            {
                var fresh = c.FreshFacts(new(23 + seed));
                var validation = BasicQuestionValidator.Validate(row.RawJson, fresh);
                if (!validation.IsValid) throw new Exception("Native evidence rejected: " + validation.ErrorCode);
                if (c.Version == ReasoningStoryCatalogue.Version) ProseExpansionTests.VerifyPractice(fresh, validation.Draft!);
                else foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                    if (!new ArithmeticQuizValidator(new BasicArithmeticEngine()).Validate(
                        fresh.ToPracticeQuestion(validation.Draft!.ToWordProblem(fresh), mode, new(13))).IsValid)
                        throw new Exception("Native evidence failed fresh grading");
            }
        }
        Console.WriteLine("PASS replay of all 10 native GGUF outputs with final validator/rendering, fresh C# facts and grading.");
    }

    private sealed class EvidenceRow
    {
        public string ContractJson { get; set; } = "";
        public string RawJson { get; set; } = "";
    }

    internal static async Task RunAsync(string path)
    {
        string directory = Path.GetFullPath("artifacts/verification/equivalent-prose-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(directory);
        await using var modelSession = new ModelTestSession();
        var runtime = modelSession.Runtime;
        var store = new QuestionBankStore(Path.Combine(directory, "model.db3"));
        var results = new List<object>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        int passed = 0;
        Console.WriteLine("Native GGUF, isolated SQLite; evidence: " + directory);
        try
        {
            await modelSession.LoadAsync(path, timeout.Token);
            var cases = new List<(string Name, BasicQuestionContract Contract)> {
                ("OneStep-Add-VI", AdditionQuestionCatalogue.Create(CurriculumTier.OneStar, AppLanguage.Vietnamese, new(17), "family-gifts", BasicQuestionStructure.Combine)),
                ("OneStep-Divide-EN", ArithmeticQuestionCatalogue.Create(ArithmeticOperation.Divide, CurriculumTier.ThreeStars, AppLanguage.English, new(17)))
            };
            foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
            {
                cases.Add(($"FindX-{language}", FindXQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Packaging), ArithmeticOperation.Divide,
                    CurriculumTier.ThreeStars, language, new(19), FindXUnknownRole.Divisor)));
                foreach (var family in new[] { BankQuestionFamily.Average, BankQuestionFamily.Percentage, BankQuestionFamily.MultiStep })
                    cases.Add(($"{family}-{language}", ReasoningStoryCatalogue.Create(family,
                        ReasoningStoryCatalogue.Variants(family, CurriculumTier.FiveStars).First(), CurriculumTier.FiveStars, language, new(19))));
            }
            foreach (var (name, c) in cases)
            {
                bool reasoning = c.Version == ReasoningStoryCatalogue.Version;
                var original = reasoning ? ReasoningStoryCatalogue.Draft(c) : c.Version == FindXQuestionCatalogue.Version
                    ? FindXQuestionCatalogue.Draft(c) : OneStepQuestionCatalogue.Draft(c);
                var excluded = new HashSet<string> { QuestionProseIdentity.Hash(c, original) };
                string? correction = null, details = null;
                bool valid = false;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    string prompt = BasicQuestionPrompt.Build(c, correction, excludedProse: excluded, errorDetails: details);
                    var streamed = new StringBuilder();
                    AiGenerationMetrics? metrics = null;
                    string raw = await runtime.GenerateNovelAsync(c, prompt, excluded, timeout.Token, text => streamed.Append(text), value => metrics = value);
                    var validation = BasicQuestionValidator.Validate(raw, c);
                    correction = validation.ErrorCode;
                    details = validation.ErrorDetails;
                    if (validation.IsValid && excluded.Contains(QuestionProseIdentity.Hash(c, validation.Draft!))) correction = "DuplicateProse";
                    results.Add(new { Case = name, Attempt = attempt, ErrorCode = correction, ErrorDetails = details, Metrics = metrics, ContextTokens = runtime.LastContextTokens });
                    await File.WriteAllTextAsync(Path.Combine(directory, name + "-" + attempt + ".txt"),
                        prompt + "\n\n" + raw + "\n\n" + correction + " " + details, timeout.Token);
                    Console.WriteLine($"{name}: attempt {attempt}, {correction ?? "Valid"}, {metrics?.TokensPerSecond:F2} token/s");
                    if (streamed.ToString() != raw) throw new Exception("Streaming lost output");
                    if (correction is not null) continue;
                    var record = new ValidatedBankQuestion(c, validation.Draft!, raw, runtime.ModelName, DateTime.UtcNow);
                    if (!await store.InsertAsync(record, timeout.Token) || !await store.ContainsProseAsync(record, timeout.Token))
                        throw new Exception("SQLite template roundtrip failed: " + name);
                    for (int seed = 0; seed < 3; seed++)
                    {
                        var fresh = c.FreshFacts(new(23 + seed));
                        if (!BasicQuestionValidator.Validate(raw, fresh).IsValid) throw new Exception("Fresh facts broke prose: " + name);
                        if (reasoning) ProseExpansionTests.VerifyPractice(fresh, validation.Draft!);
                        else foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                        {
                            var word = validation.Draft!.ToWordProblem(fresh);
                            if (word.ProblemText.Contains('{') || !new ArithmeticQuizValidator(new BasicArithmeticEngine())
                                .Validate(fresh.ToPracticeQuestion(word, mode, new(13))).IsValid)
                                throw new Exception("Fresh facts or grading failed: " + name);
                        }
                    }
                    valid = true; passed++; break;
                }
                if (!valid) throw new Exception("Native GGUF failed three attempts: " + name);
            }
        }
        finally
        {
            await modelSession.DisposeAsync();
            await File.WriteAllTextAsync(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (runtime.IsLoaded) throw new Exception("Weights not released");
        Console.WriteLine($"PASS {passed} actual GGUF cases, novel prose, SQLite roundtrip, fresh C# facts and grading; app database untouched.");
    }
}