using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

internal static class AdditionThroughputChecks
{
    public static async Task RunAsync(string modelPath, bool newContexts = false)
    {
        var runtime = new GgufQuestionRuntime();
        await runtime.LoadAsync(modelPath);
        var results = new List<object>();
        var failures = new List<string>();
        Console.WriteLine($"MODEL {runtime.ModelName}; logical CPUs {Environment.ProcessorCount}; prompt/decode threads {runtime.PromptThreadCount}/{runtime.InferenceThreadCount}");
        try
        {
            var cases = newContexts ? new[] {
                ("bakery", BasicQuestionStructure.Combine, CurriculumTier.OneStar, AppLanguage.Vietnamese),
                ("poultry", BasicQuestionStructure.Increase, CurriculumTier.TwoStars, AppLanguage.Vietnamese),
                ("crop-harvest", BasicQuestionStructure.Combine, CurriculumTier.ThreeStars, AppLanguage.Vietnamese),
                ("passenger-count", BasicQuestionStructure.Combine, CurriculumTier.FourStars, AppLanguage.Vietnamese),
                ("construction-stock", BasicQuestionStructure.AddComparisonInverse, CurriculumTier.FiveStars, AppLanguage.Vietnamese),
                ("survey-responses", BasicQuestionStructure.AddComparisonMore, CurriculumTier.ThreeStars, AppLanguage.English),
                ("trial-results", BasicQuestionStructure.Combine, CurriculumTier.FiveStars, AppLanguage.English) } : new[] {
                ("family-gifts", BasicQuestionStructure.Combine, CurriculumTier.OneStar, AppLanguage.Vietnamese),
                ("library", BasicQuestionStructure.AddComparisonInverse, CurriculumTier.ThreeStars, AppLanguage.Vietnamese),
                ("notebook-production", BasicQuestionStructure.Combine, CurriculumTier.FiveStars, AppLanguage.Vietnamese),
                ("school-supplies", BasicQuestionStructure.Combine, CurriculumTier.FourStars, AppLanguage.English) };
            foreach (var (scene, structure, tier, language) in cases)
            {
                var contract = AdditionQuestionCatalogue.Create(tier, language, new Random(836), scene, structure);
                string? correction = null;
                bool valid = false;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    string prompt = BasicQuestionPrompt.Build(contract, correction);
                    using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                    var streamed = new StringBuilder();
                    var elapsed = Stopwatch.StartNew();
                    AiGenerationMetrics? metrics = null;
                    TimeSpan? firstToken = null;
                    Console.WriteLine($"GENERATING {language}/{tier}/{scene}/{structure}, attempt {attempt}, prompt chars {prompt.Length}");
                    string raw = await runtime.GenerateAsync(contract, prompt, cancellation.Token, text => streamed.Append(text),
                        value => { metrics = value; firstToken ??= elapsed.Elapsed; });
                    var validation = BasicQuestionValidator.Validate(raw, contract);
                    correction = validation.ErrorCode;
                    if (streamed.ToString() != raw || metrics?.TokensPerSecond is not > 0)
                        throw new InvalidOperationException("Missing streamed output or token measurements.");
                    var result = new { language, tier, scene, structure, attempt, validation.ErrorCode,
                        PromptCharacters = prompt.Length, ContextTokens = runtime.LastContextTokens,
                        metrics.GeneratedTokens, metrics.TokensPerSecond,
                        FirstTokenSeconds = firstToken?.TotalSeconds, TotalSeconds = elapsed.Elapsed.TotalSeconds };
                    results.Add(result);
                    Console.WriteLine(JsonSerializer.Serialize(result));
                    Console.WriteLine(raw);
                    if (!validation.IsValid) continue;
                    for (int seed = 0; seed < 4; seed++)
                    {
                        var fresh = validation.Contract!.FreshFacts(new Random(724 + seed));
                        var word = validation.Draft!.ToWordProblem(fresh);
                        var quiz = fresh.ToPracticeQuestion(word, ArithmeticQuizMode.Essay);
                        var equation = $"{fresh.Left} + {fresh.Right} = {fresh.Answer} {word.AnswerUnit}";
                        if (word.ProblemText.Contains('{') || !new EssayAnswerValidator(new BasicArithmeticEngine())
                            .Validate(quiz, word.SolutionLead, equation, $"{fresh.Answer} {word.AnswerUnit}").IsCorrect)
                            throw new InvalidOperationException("Generated template failed fresh C# rendering/grading: " + scene);
                    }
                    Console.WriteLine("RENDERED: " + validation.Draft!.ToWordProblem(validation.Contract!).ProblemText);
                    valid = true; break;
                }
                if (!valid) failures.Add(scene);
            }
        }
        finally { await runtime.EjectAsync(); }
        Console.WriteLine("MEASUREMENTS: " + JsonSerializer.Serialize(results));
        if (failures.Count > 0) throw new InvalidOperationException("Questions failed three attempts: " + string.Join(", ", failures));
        Console.WriteLine("PASS real GGUF addition throughput and validation; no app database writes.");
    }
}
