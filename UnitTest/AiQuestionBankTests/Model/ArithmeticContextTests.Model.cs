using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using System.Text;
using System.Text.Json;

internal static partial class ArithmeticContextTests
{
    public static async Task RunModelAsync(string path)
    {
        await using var modelSession = new ModelTestSession();
        var runtime = modelSession.Runtime;
        var failures = new List<string>();
        await modelSession.LoadAsync(path);

        Console.WriteLine($"MODEL {runtime.ModelName}; threads {runtime.InferenceThreadCount}/{Environment.ProcessorCount}");
        var scenes = new[] { "food-supplies", "poultry", "school-supplies", "construction-stock", "library" };
        foreach (var operation in Operations)
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        {
            var language = tier == CurriculumTier.FourStars ? AppLanguage.English : AppLanguage.Vietnamese;
            var roles = BasicQuestionTemplates.Allowed(operation, tier);
            var structure = roles[((int)tier - 1) % roles.Length];
            if (operation == ArithmeticOperation.Multiply && tier == CurriculumTier.FiveStars) structure = BasicQuestionStructure.TimesAsMany;
            var c = ArithmeticQuestionCatalogue.Create(operation, tier, language, new Random(836), scenes[(int)tier - 1], structure);
            string? correction = null;
            bool passed = false;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                string tag = $"{operation}/{tier}/{language}/{c.SceneId}/{structure}";
                string prompt = BasicQuestionPrompt.Build(c, correction);
                Console.WriteLine($"GENERATING {tag} attempt {attempt}, prompt {prompt.Length} chars");
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                var streamed = new StringBuilder();
                AiGenerationMetrics? metrics = null;
                string raw = await runtime.GenerateAsync(c, prompt, timeout.Token, text => streamed.Append(text), value => metrics = value);
                var valid = BasicQuestionValidator.Validate(raw, c);
                Console.WriteLine(raw);
                Console.WriteLine($"RESULT {valid.ErrorCode ?? "PASS"}, {metrics?.TokensPerSecond:F2} token/s, context {runtime.LastContextTokens}");
                Check(streamed.ToString() == raw && metrics?.TokensPerSecond > 0, "Broken stream/token metrics.");
                correction = valid.ErrorCode;
                if (!valid.IsValid) continue;
                for (int seed = 0; seed < 4; seed++)
                {
                    var fresh = c.FreshFacts(new Random(seed));
                    var word = valid.Draft!.ToWordProblem(fresh);
                    var essay = fresh.ToPracticeQuestion(word, ArithmeticQuizMode.Essay);
                    string equation = $"{fresh.Left} {BasicArithmeticEngine.GetSymbol(operation)} {fresh.Right} = {fresh.Answer} {word.AnswerUnit}";
                    Check(fresh.IsValid && !word.ProblemText.Contains('{') && new EssayAnswerValidator(new BasicArithmeticEngine())
                        .Validate(essay, word.SolutionLead, equation, $"{fresh.Answer} {word.AnswerUnit}").IsCorrect, "Generated context failed fresh practice grading.");
                }
                Console.WriteLine("RENDERED " + valid.Draft!.ToWordProblem(c).ProblemText);
                passed = true; break;
            }
            if (!passed) failures.Add($"{operation}/{tier}/{structure}");
        }

        Check(failures.Count == 0, "Failed three model attempts: " + string.Join(", ", failures));
        Console.WriteLine("PASS real GGUF subtraction/multiplication/division at all five tiers; no app database writes.");
    }
}
