using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using System.Globalization;

internal static partial class FractionBankTests
{
    public static async Task RunModelAsync(string modelPath)
    {
        string directory = Path.GetFullPath(Path.Combine("artifacts", "verification", "fraction-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(directory);
        await using var modelSession = new ModelTestSession();
        var runtime = modelSession.Runtime;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var store = new QuestionBankStore(Path.Combine(directory, "model.db3"));

        Console.WriteLine("Loading fraction model; evidence: " + directory);
        await modelSession.LoadAsync(modelPath, timeout.Token);
        int index = 0;
        foreach (var group in QuestionLearningProfile.Groups())
        {
            var operation = (ArithmeticOperation)(index++ % 4);
            var language = index % 3 == 0 ? AppLanguage.English : AppLanguage.Vietnamese;
            var c = FractionQuestionCatalogue.Create(new(group), operation, CurriculumTier.FiveStars, language, new Random(index));
            var original = FractionQuestionCatalogue.Draft(c);
            await store.InsertAsync(new(c, original, QuestionBankStore.SerializeDraft(original), "seed", DateTime.UtcNow));
            var excluded = await store.GetProseHashesAsync(timeout.Token);
            string prompt = BasicQuestionPrompt.Build(c, excludedProse: excluded);
            string raw = await runtime.GenerateNovelAsync(c, prompt, excluded, timeout.Token);
            await File.WriteAllTextAsync(Path.Combine(directory, group + ".json"), raw, timeout.Token);
            var validation = BasicQuestionValidator.Validate(raw, c);
            Check(validation.IsValid, "Real fraction model validation failed: " + group + ": " + validation.ErrorCode);
            var question = new ValidatedBankQuestion(c, validation.Draft!, raw, runtime.ModelName, DateTime.UtcNow);
            Check(!excluded.Contains(QuestionProseIdentity.Hash(c, question.Draft)), "Native grammar repeated seeded prose.");
            Check(await store.InsertAsync(question, timeout.Token), "Real fraction prose failed to insert.");
            Check(c.FreshFacts(new Random(405)).IsValid, "Real model changed dimensions.");
            Console.WriteLine($"PASS model {group}/{operation}/{language}: {question.WordProblem.ProblemText}");
        }

    }
}
