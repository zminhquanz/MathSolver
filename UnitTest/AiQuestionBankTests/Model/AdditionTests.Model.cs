using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using System.Text.Json;

internal static partial class AdditionTests
{
    public static async Task RunModelAsync(string path, bool englishOnly = false, bool stockOnly = false)
    {
        await using var modelSession = new ModelTestSession();
        var runtime = modelSession.Runtime;
        await modelSession.LoadAsync(path);

        Check(runtime.InferenceThreadCount == GgufQuestionRuntime.GetInferenceThreadCount(Environment.ProcessorCount)
            && runtime.PromptThreadCount == runtime.InferenceThreadCount,
            "Loaded GGUF did not use the configured 75% thread budget.");
        Console.WriteLine($"GGUF loaded: {runtime.ModelName}; logical CPUs: {Environment.ProcessorCount}; inference/prompt threads: {runtime.InferenceThreadCount}/{runtime.PromptThreadCount}");
        var cases = Enum.GetValues<AppLanguage>().SelectMany(language => new[] {
            Create("family-gifts", BasicQuestionStructure.Combine, CurriculumTier.OneStar, language),
            Create("craft", BasicQuestionStructure.Combine, CurriculumTier.TwoStars, language),
            Create("donations", BasicQuestionStructure.AddComparisonMore, CurriculumTier.ThreeStars, language),
            Create("shop-stock", BasicQuestionStructure.RecoverInitial, CurriculumTier.FourStars, language),
            Create("notebook-production", BasicQuestionStructure.Combine, CurriculumTier.FiveStars, language),
            Create("garden", BasicQuestionStructure.Combine, CurriculumTier.FiveStars, language),
            Create("recycling", BasicQuestionStructure.AddComparisonInverse, CurriculumTier.FiveStars, language)
        }).Concat(new[] {
            Create("birds-arrive", BasicQuestionStructure.Increase, CurriculumTier.OneStar),
            Create("club-arrivals", BasicQuestionStructure.Increase, CurriculumTier.TwoStars),
            Create("notebook-production", BasicQuestionStructure.Combine, CurriculumTier.ThreeStars),
            Create("notebook-production", BasicQuestionStructure.Combine, CurriculumTier.FourStars),
            Create("book-distribution", BasicQuestionStructure.Combine, CurriculumTier.FiveStars)
        });
        var failed = new List<string>();
        foreach (var c in cases.Where(c => (!englishOnly || c.Language == AppLanguage.English)
            && (!stockOnly || AdditionQuestionCatalogue.Find(c.SceneId)!.Kind == AdditionSceneKind.Stock)))
        {
            string? correction = null;
            bool passed = false;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                var streamed = new System.Text.StringBuilder();
                Console.WriteLine($"GENERATING {c.Language}/{c.Tier}/{c.SceneId}/{c.Structure} attempt {attempt}");
                string raw = await runtime.GenerateAsync(c, BasicQuestionPrompt.Build(c, correction), cancellation.Token, delta => streamed.Append(delta));
                var validation = BasicQuestionValidator.Validate(raw, c);
                Console.WriteLine($"{c.Language}/{c.Tier}/{c.SceneId}/{c.Structure} attempt {attempt}: {validation.ErrorCode ?? "PASS"}\n{raw}");
                Check(streamed.ToString() == raw && raw.Length > 0, "Real addition stream did not match final output.");
                correction = validation.ErrorCode;
                if (!validation.IsValid) continue;
                for (int seed = 0; seed < 4; seed++)
                {
                    var fresh = validation.Contract!.FreshFacts(new Random(642 + seed));
                    var word = validation.Draft!.ToWordProblem(fresh);
                    string equation = $"{fresh.Left} + {fresh.Right} = {fresh.Answer} {word.AnswerUnit}";
                    Check(new EssayAnswerValidator(new BasicArithmeticEngine()).Validate(fresh.ToPracticeQuestion(word, ArithmeticQuizMode.Essay),
                        word.SolutionLead, equation, $"{fresh.Answer} {word.AnswerUnit}").IsCorrect, "Real addition template failed fresh C# essay grading.");
                    if (seed == 0) Console.WriteLine("RENDERED: " + word.ProblemText + "\nSOLUTION: " + word.SolutionLead + "\n" + equation);
                }
                passed = true; break;
            }
            if (!passed) failed.Add($"{c.Language}/{c.Tier}/{c.SceneId}/{c.Structure}");
        }
        Check(failed.Count == 0, "Real model failed addition cases after three attempts: " + string.Join(", ", failed));

        Console.WriteLine(stockOnly ? "PASS real GGUF stock addition: personal holdings, restored wholesale stock and distribution stock, streamed JSON and fresh C# grading. No benchmark or app database writes."
            : englishOnly ? "PASS real GGUF English addition at 1–5 stars, streamed JSON and fresh C# grading. No benchmark or app database writes."
            : "PASS real GGUF addition: 1–5 stars, five relations, different scene roles, streamed JSON and fresh C# grading. No benchmark or app database writes.");
    }
}
