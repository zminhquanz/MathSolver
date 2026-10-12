using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Services.Core;
using System.Text;
using System.Text.Json;

internal static partial class GenerationStructureModelTests
{


    public static async Task RunAsync(string path, bool reproductionOnly = false)
    {
        string directory = Path.GetFullPath(Path.Combine("artifacts", "verification", "structure-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(directory);
        await using var modelSession = new ModelTestSession();
        var runtime = modelSession.Runtime;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(30));
        var failures = new List<string>();
        var results = new List<object>();
        Console.WriteLine("Native structure verification; evidence: " + directory);
        try
        {
            await modelSession.LoadAsync(path, timeout.Token);
            var cases = Cases().ToArray();
            if (reproductionOnly) cases = cases.Take(1).ToArray();
            foreach (var (name, c) in cases)
            {
                string? correction = null;
                bool passed = false;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    string prompt = BasicQuestionPrompt.Build(c, correction);
                    var streamed = new StringBuilder();
                    AiGenerationMetrics? metrics = null;
                    string raw = await runtime.GenerateAsync(c, prompt, timeout.Token, t => streamed.Append(t), m => metrics = m);
                    var validation = BasicQuestionValidator.Validate(raw, c);
                    if (streamed.ToString() != raw) throw new Exception("Streaming differs from the final output");
                    string details = validation.ErrorDetails ?? DescribeMismatch(raw, c);
                    await File.WriteAllTextAsync(Path.Combine(directory, $"{name}-{attempt}.txt"),
                        prompt + "\n\n" + raw + "\n\n" + (validation.ErrorCode ?? "Valid") + "\n" + details, timeout.Token);
                    results.Add(new { Case = name, Attempt = attempt, Error = validation.ErrorCode, ErrorDetails = details, Metrics = metrics });
                    Console.WriteLine($"{name} attempt {attempt}: {validation.ErrorCode ?? "Valid"}; {details}");
                    if (validation.IsValid) { passed = true; break; }
                    correction = validation.ErrorCode;
                }
                if (!passed) failures.Add(name);
            }
        }
        finally
        {
            await modelSession.DisposeAsync();
            await File.WriteAllTextAsync(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        if (failures.Count > 0) throw new Exception("Native structure failures: " + string.Join(", ", failures) + "; evidence: " + directory);
        Console.WriteLine("All native structure cases passed; evidence: " + directory);
    }

    private static IEnumerable<(string Name, BasicQuestionContract Contract)> Cases()
    {
        yield return ("fraction-bread-add-1-vi", FractionQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects),
            ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese, new(19), "fraction-bread-Add"));
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var (operation, tier, group) in new[] {
            (ArithmeticOperation.Add, CurriculumTier.OneStar, QuestionKnowledgeGroup.Objects),
            (ArithmeticOperation.Subtract, CurriculumTier.ThreeStars, QuestionKnowledgeGroup.Money),
            (ArithmeticOperation.Multiply, CurriculumTier.FourStars, QuestionKnowledgeGroup.Measurement),
            (ArithmeticOperation.Divide, CurriculumTier.FiveStars, QuestionKnowledgeGroup.Geometry) })
        {
            if (language == AppLanguage.Vietnamese && operation == ArithmeticOperation.Add) continue;
            yield return ($"fraction-{operation}-{(int)tier}-{language}", FractionQuestionCatalogue.Create(new(group), operation, tier, language, new(19)));
        }
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        {
            yield return ($"applied-saving-3-{language}", AppliedQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Money),
                ArithmeticOperation.Add, CurriculumTier.ThreeStars, language, new(19), "saving-total"));
            yield return ($"findx-total-4-{language}", FindXQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects),
                ArithmeticOperation.Subtract, CurriculumTier.FourStars, language, new(19), FindXUnknownRole.Minuend));
        }
        foreach (var family in new[] { BankQuestionFamily.TwoNumbers, BankQuestionFamily.Average, BankQuestionFamily.Percentage })
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
            yield return ($"{family}-5-{language}", ReasoningStoryCatalogue.Create(family,
                family == BankQuestionFamily.TwoNumbers ? 2 : family == BankQuestionFamily.Average ? 4 : 2,
                CurriculumTier.FiveStars, language, new(19)));
    }

    private static string DescribeMismatch(string raw, BasicQuestionContract c)
    {
        var pool = ReviewedQuestionProse.For(c);
        if (pool is null) return "";
        try
        {
            using var document = JsonDocument.Parse(raw);
            return string.Join(", ", new[] { ("given_a", pool.GivenA), ("given_b", pool.GivenB), ("question", pool.Questions), ("solution_lead", pool.Leads) }
                .Where(pair => !SemanticProseRules.Matches(document.RootElement.GetProperty(pair.Item1).GetString(), pair.Item2, c.Language))
                .Select(pair => pair.Item1 + " differs from semantic anchors"));
        }
        catch (JsonException) { return "Malformed JSON"; }
    }
}