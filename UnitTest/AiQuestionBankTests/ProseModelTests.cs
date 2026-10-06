using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using System.Text;

internal static class ProseModelTests
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public static async Task RunAsync(string modelPath)
    {
        string directory = Path.GetFullPath(Path.Combine("artifacts", "verification", "prose-model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(directory);
        var runtime = new GgufQuestionRuntime();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var log = new StringBuilder();
        try
        {
            Console.WriteLine($"Loading {Path.GetFileName(modelPath)}; evidence: {directory}");
            await runtime.LoadAsync(modelPath, timeout.Token);
            foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
            {
                var c = AppliedQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Money), ArithmeticOperation.Add,
                    CurriculumTier.ThreeStars, language, new Random(17), "saving-total");
                var store = new QuestionBankStore(Path.Combine(directory, language + ".db3"));
                var original = AppliedQuestionCatalogue.Draft(c);
                var choices = ReviewedQuestionProse.For(c)!;
                // All old combinations and even new questions over the same old
                // givens are already in SQLite: changing only the question cannot pass.
                foreach (string q in choices.Questions)
                {
                    var draft = original with { Question = q };
                    await store.InsertAsync(new(c, draft, QuestionBankStore.SerializeDraft(draft), "existing-wording", DateTime.UtcNow));
                }
                var excluded = new HashSet<string>(await store.GetProseHashesAsync(timeout.Token), StringComparer.Ordinal);
                var factualPhrasings = new HashSet<string>(StringComparer.Ordinal);
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    string prompt = BasicQuestionPrompt.Build(c, excludedProse: excluded);
                    var streamed = new StringBuilder();
                    string raw = await runtime.GenerateNovelAsync(c, prompt, excluded, timeout.Token, text => streamed.Append(text));
                    var validation = BasicQuestionValidator.Validate(raw, c);
                    log.AppendLine($"{language} attempt {attempt}: {validation.ErrorCode ?? "Valid"}\nPROMPT\n{prompt}\nRAW\n{raw}\n");
                    Check(validation.IsValid, $"Model output rejected: {validation.ErrorCode}: {raw}");
                    Check(streamed.ToString() == raw, "Streaming lost model output.");
                    var question = new ValidatedBankQuestion(c, validation.Draft!, raw, runtime.ModelName, DateTime.UtcNow);
                    Check(!await store.ContainsProseAsync(question, timeout.Token), "Model repeated SQLite wording.");
                    Check(question.Draft.GivenA != original.GivenA || question.Draft.GivenB != original.GivenB,
                        "Only the question changed while the factual clauses remained identical.");
                    Check(await store.InsertAsync(question, timeout.Token), "A novel model question failed to save.");
                    foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                    {
                        var quiz = c.ToPracticeQuestion(question.WordProblem, mode, new Random(17));
                        Check(new ArithmeticQuizValidator(new BasicArithmeticEngine()).Validate(quiz).IsValid
                            && quiz.CorrectAnswer == c.Answer, "Model prose changed the C# answer.");
                    }
                    Check(excluded.Add(QuestionProseIdentity.Hash(c, question.Draft)), "Repeated wording within this batch.");
                    factualPhrasings.Add(question.Draft.GivenA + "\n" + question.Draft.GivenB);
                    Console.WriteLine($"PASS {language} {attempt}: {question.WordProblem.ProblemText}");
                }
                Check(factualPhrasings.Count >= 2, "Model varied only questions over the same factual clauses.");
                var findX = FindXQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Money), ArithmeticOperation.Subtract,
                    CurriculumTier.ThreeStars, language, new Random(17), FindXUnknownRole.Minuend, "findx-Minuend-saving-total");
                string findPrompt = BasicQuestionPrompt.Build(findX, excludedProse: excluded);
                string findRaw = await runtime.GenerateNovelAsync(findX, findPrompt, excluded, timeout.Token);
                var findValidation = BasicQuestionValidator.Validate(findRaw, findX);
                log.AppendLine($"{language} Find X: {findValidation.ErrorCode ?? "Valid"}\nPROMPT\n{findPrompt}\nRAW\n{findRaw}\n");
                Check(findValidation.IsValid, "Real Find X paraphrase changed its roles or units.");
                var findQuestion = new ValidatedBankQuestion(findX, findValidation.Draft!, findRaw, runtime.ModelName, DateTime.UtcNow);
                Check(!await store.ContainsProseAsync(findQuestion, timeout.Token) && await store.InsertAsync(findQuestion, timeout.Token),
                    "Find X repeated existing applied-story wording or failed to save.");
                Console.WriteLine($"PASS {language} Find X: {findQuestion.WordProblem.ProblemText}");
            }
            // Exercise the actual app worker with old prose pre-seeded for every
            // eligible scene, automatic insertion, and one model per job.
            var workerStore = new QuestionBankStore(Path.Combine(directory, "worker.db3"));
            var profile = new QuestionLearningProfile(QuestionKnowledgeGroup.Money);
            foreach (var scene in AppliedQuestionCatalogue.Available(profile, ArithmeticOperation.Add, CurriculumTier.ThreeStars))
            {
                var c = AppliedQuestionCatalogue.Create(profile, ArithmeticOperation.Add, CurriculumTier.ThreeStars,
                    AppLanguage.Vietnamese, new Random(19), scene.Id);
                var d = AppliedQuestionCatalogue.Draft(c); var e = AppliedQuestionCatalogue.Draft(c, 1);
                foreach (var q in new[] { d.Question, e.Question }.Distinct())
                {
                    var draft = d with { Question = q };
                    await workerStore.InsertAsync(new(c, draft, QuestionBankStore.SerializeDraft(draft), "existing-wording", DateTime.UtcNow));
                }
            }
            var worker = new AiQuestionGenerationService(runtime, workerStore);
            worker.Start(new(ArithmeticOperation.Add, CurriculumTier.ThreeStars, AppLanguage.Vietnamese, 3, true, profile));
            await worker.Completion.WaitAsync(timeout.Token);
            foreach (var item in worker.Snapshot.Items)
            {
                log.AppendLine($"WORKER {item.Number}: {item.State} {item.Error}");
                foreach (var attempt in item.Attempts) log.AppendLine($"{attempt.Prompt}\n{attempt.RawJson}\n{attempt.ErrorCode}");
                Console.WriteLine($"Worker {item.Number}: {item.State}; attempts={item.Attempts.Count}; {item.Question?.WordProblem.ProblemText}");
            }
            Check(worker.Snapshot.State == AiJobState.Completed && worker.Snapshot.Items.Count == 3
                && worker.Snapshot.Items.All(i => i.State == AiItemState.Saved), "Real model worker failed: " + worker.Snapshot.Error);
            Check(!runtime.IsLoaded, "Worker retained model weights after completion.");
            Console.WriteLine("PASS real GGUF: six distinct savings stories, two Find X stories, C# grading, SQLite insertion, real three-item worker and model cleanup. App database untouched.");
        }
        finally
        {
            await runtime.EjectAsync();
            await File.WriteAllTextAsync(Path.Combine(directory, "generation.txt"), log.ToString(), Encoding.UTF8);
        }
    }
}
