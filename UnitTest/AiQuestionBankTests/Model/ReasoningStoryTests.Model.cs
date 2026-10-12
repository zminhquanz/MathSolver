using MathSolver.Services;
using MathSolver.Models;
using MathSolver.Services.QuestionBank;

internal static partial class ReasoningStoryTests
{
    public static async Task RunModelAsync(string path)
    {
        string directory = Path.Combine("artifacts", "verification", "reasoning-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(directory);
        await using var modelSession = new ModelTestSession();
        var runtime = modelSession.Runtime;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        await modelSession.LoadAsync(path, timeout.Token);

        foreach (var (family, variant, tier) in new[] {
            (BankQuestionFamily.TwoNumbers, 0, CurriculumTier.ThreeStars),
            (BankQuestionFamily.Average, 4, CurriculumTier.FourStars),
            (BankQuestionFamily.Percentage, 2, CurriculumTier.FiveStars) })
        {
            var c = ReasoningStoryCatalogue.Create(family, variant, tier, AppLanguage.Vietnamese, new(19));
            string? correction = null;
            bool passed = false;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                string prompt = BasicQuestionPrompt.Build(c, correction);
                string raw = await runtime.GenerateAsync(c, prompt, timeout.Token);
                var validated = BasicQuestionValidator.Validate(raw, c);
                await File.WriteAllTextAsync(Path.Combine(directory, $"{family}-{attempt}.txt"), prompt + "\n\n" + raw + "\n" + validated.ErrorCode);
                Console.WriteLine($"{family} {attempt}: {validated.ErrorCode ?? "Valid"}");
                if (validated.IsValid) { passed = true; break; }
                correction = validated.ErrorCode;
            }
            if (!passed) throw new Exception("Real model failed: " + family + "; evidence: " + directory);
        }

        Console.WriteLine("Real GGUF reasoning prose passed; evidence: " + directory);
    }
}
