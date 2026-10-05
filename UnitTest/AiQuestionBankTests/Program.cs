using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.Text.Json;
using System.Text;

static void Check(bool condition, string message)
{ if (!condition) throw new InvalidOperationException(message); }

static string Draft(BasicQuestionContract c)
{
    if (c.IsTemplate) return QuestionBankStore.SerializeDraft(BasicQuestionTemplates.Example(c));
    bool vi = c.Language == AppLanguage.Vietnamese;
    string a = c.Operation == ArithmeticOperation.Multiply
        ? vi ? $"Mỗi thùng chứa {c.Left} quyển sách." : $"Each box holds {c.Left} books."
        : vi ? $"{c.Subject} có {c.Left} quyển sách." : $"{c.Subject} has {c.Left} books.";
    string b = c.Operation switch
    {
        ArithmeticOperation.Add => vi ? $"{c.Subject} nhận thêm {c.Right} quyển sách." : $"{c.Subject} receives {c.Right} more books.",
        ArithmeticOperation.Subtract => vi ? $"{c.Subject} cho đi {c.Right} quyển sách." : $"{c.Subject} gives away {c.Right} books.",
        ArithmeticOperation.Multiply => vi ? $"{c.Subject} có {c.Right} thùng như nhau." : $"{c.Subject} has {c.Right} identical boxes.",
        _ => vi ? $"{c.Subject} xếp đều sách vào {c.Right} thùng." : $"{c.Subject} distributes the books equally into {c.Right} boxes."
    };
    string q = c.Operation switch
    {
        ArithmeticOperation.Subtract => vi ? $"Hỏi {c.Subject} còn lại bao nhiêu quyển sách?" : $"How many books does {c.Subject} have left?",
        ArithmeticOperation.Divide => vi ? $"Hỏi mỗi thùng của {c.Subject} chứa bao nhiêu quyển sách?" : $"How many books are in each box of {c.Subject}?",
        _ => vi ? $"Hỏi {c.Subject} có tất cả bao nhiêu quyển sách?" : $"How many books does {c.Subject} have in total?"
    };
    return JsonSerializer.Serialize(new { given_a = a, given_b = b, question = q });
}

if (args is ["--arithmetic-context-model", var arithmeticModelPath])
{
    try { await ArithmeticContextTests.RunModelAsync(arithmeticModelPath); }
    catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
    return;
}
if (args is ["--arithmetic-contexts"])
{
    string contextDirectory = Path.Combine(Path.GetTempPath(), "MathSolver-arithmetic-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(contextDirectory);
    try { await ArithmeticContextTests.RunAsync(contextDirectory); }
    catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
    finally { Directory.Delete(contextDirectory, true); }
    return;
}
if (args is ["--one-step-practice"])
{
    try { OneStepPracticeTests.Run(); }
    catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
    return;
}
if (args is ["--findx-bank"])
{
    string folder = Path.GetFullPath(Path.Combine("artifacts", "verification", "findx-tests-" + Guid.NewGuid().ToString("N")));
    Directory.CreateDirectory(folder);
    try { await FindXBankTests.RunAsync(folder); }
    catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
    return;
}
if (args is ["--learning-profiles"])
{
    string folder = Path.Combine(Path.GetTempPath(), "MathSolver-learning-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    try { await LearningProfileTests.RunAsync(folder); }
    catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
    finally { Directory.Delete(folder, true); }
    return;
}
if (args is ["--addition-throughput", var throughputModelPath])
{
    try { await AdditionThroughputChecks.RunAsync(throughputModelPath); }
    catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
    return;
}

if (args is ["--addition-context-model", var contextModelPath])
{
    try { await AdditionThroughputChecks.RunAsync(contextModelPath, newContexts: true); }
    catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
    return;
}

if (args is ["--memory-model", var memoryModelPath])
{
    await MemoryLifecycleTests.RunModelAsync(memoryModelPath);
    return;
}

if (args is ["--model", var modelPath])
{
    var runtime = new GgufQuestionRuntime();
    await runtime.LoadAsync(modelPath);
    try
    {
        foreach (var operation in Enum.GetValues<ArithmeticOperation>())
        {
            var contract = BasicQuestionContract.CreateTemplate(operation, CurriculumTier.OneStar, AppLanguage.Vietnamese);
            string? correction = null;
            bool passed = false;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                var chunks = new StringBuilder();
                string output = await runtime.GenerateAsync(contract, BasicQuestionPrompt.Build(contract, correction), cancellation.Token,
                    text => chunks.Append(text));
                Check(chunks.Length > 0 && chunks.ToString() == output, "Live model stream did not match the final JSON.");
                var validation = BasicQuestionValidator.Validate(output, contract);
                Console.WriteLine($"{operation} attempt {attempt}: {validation.ErrorCode ?? "PASS"}\n{output}");
                correction = validation.ErrorCode;
                if (validation.IsValid)
                {
                    var resolved = validation.Contract ?? contract;
                    for (int seed = 0; seed < 4; seed++)
                    {
                        var facts = resolved.FreshFacts(new Random(712 + seed));
                        var word = validation.Draft!.ToWordProblem(facts);
                        var quiz = facts.ToPracticeQuestion(word, ArithmeticQuizMode.Essay);
                        string equation = $"{facts.Left} {MathSolver.Services.Core.BasicArithmeticEngine.GetSymbol(facts.Operation)} {facts.Right} = {facts.Answer} {word.AnswerUnit}";
                        Check(!word.ProblemText.Contains('{') && new EssayAnswerValidator(new MathSolver.Services.Core.BasicArithmeticEngine())
                            .Validate(quiz, word.SolutionLead, equation, $"{facts.Answer} {word.AnswerUnit}").IsCorrect,
                            "Live template failed fresh C# instantiation/grading.");
                    }
                    passed = true; break;
                }
            }
            Check(passed, "Live model did not produce a valid " + operation + " question.");
        }
    }
    finally { await runtime.EjectAsync(); }
    Console.WriteLine("PASS live GGUF templates for all four operations, streamed JSON, fresh C# values and essay grading. No benchmark or app database writes.");
    return;
}

if (args is ["--addition-tests"])
{
    string testDirectory = Path.Combine(Path.GetTempPath(), "MathSolver-Addition-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(testDirectory);
    try { await AdditionTests.RunAsync(testDirectory); }
    finally { Directory.Delete(testDirectory, true); }
    return;
}

if (args is ["--addition-model", var additionModelPath])
{
    try { await AdditionTests.RunModelAsync(additionModelPath); }
    catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
    return;
}

if (args is ["--addition-model-stock", var stockAdditionModelPath])
{
    try { await AdditionTests.RunModelAsync(stockAdditionModelPath, stockOnly: true); }
    catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
    return;
}

if (args is ["--addition-model-english", var englishAdditionModelPath])
{
    try { await AdditionTests.RunModelAsync(englishAdditionModelPath, englishOnly: true); }
    catch (Exception error) { Console.Error.WriteLine(error); Environment.ExitCode = 1; }
    return;
}

int checkedDrafts = 0;
foreach (var language in Enum.GetValues<AppLanguage>())
foreach (var operation in Enum.GetValues<ArithmeticOperation>())
foreach (var tier in Enum.GetValues<CurriculumTier>())
{
    var contract = BasicQuestionContract.Create(operation, tier, language, new Random(100 + (int)tier));
    Check(contract.Operation == operation && contract.Tier == tier && contract.IsValid, "Fixed selection changed.");
    string valid = Draft(contract);
    var validation = BasicQuestionValidator.Validate(valid, contract);
    Check(validation.IsValid, $"Valid {language}/{operation}/{tier} rejected: {validation.ErrorCode}");
    // These mutations model actual generated prose, not just echoed numeric fields.
    var draft = validation.Draft!;
    string changedNumber = QuestionBankStore.SerializeDraft(draft with { GivenA = draft.GivenA.Replace(contract.Left.ToString(), (contract.Left + 1).ToString()) });
    Check(!BasicQuestionValidator.Validate(changedNumber, contract).IsValid, "Changed prose quantity accepted.");
    string changedUnit = QuestionBankStore.SerializeDraft(draft with { Question = draft.Question.Replace(contract.Unit, language == AppLanguage.Vietnamese ? "quả táo" : "apples") });
    Check(!BasicQuestionValidator.Validate(changedUnit, contract).IsValid, "Changed target unit accepted.");
    string changedSubject = QuestionBankStore.SerializeDraft(draft with { Question = draft.Question.Replace(contract.Subject, "Zelda") });
    Check(!BasicQuestionValidator.Validate(changedSubject, contract).IsValid, "Changed target subject accepted.");
    string extra = QuestionBankStore.SerializeDraft(draft with { GivenB = draft.GivenB + " 99" });
    Check(!BasicQuestionValidator.Validate(extra, contract).IsValid, "Extra prose quantity accepted.");
    foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
    {
        var question = contract.ToPracticeQuestion(draft.ToWordProblem(contract), mode);
        Check(new ArithmeticQuizValidator(new MathSolver.Services.Core.BasicArithmeticEngine()).Validate(question).IsValid, "Practice math invalid.");
    }
    checkedDrafts++;
}
Console.WriteLine($"PASS {checkedDrafts} bilingual operation/tier contracts and prose mutation checks");

var addition = new BasicQuestionContract(1, ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese, 7, 2, "Lan", "quyển sách", "thùng");
var addDraft = BasicQuestionValidator.Validate(Draft(addition), addition).Draft!;
Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(addDraft with { GivenB = "Lan cho đi 2 quyển sách." }), addition).IsValid, "Reversed operation accepted.");
Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(addDraft with { Question = "Hỏi Lan còn lại bao nhiêu quyển sách?" }), addition).IsValid, "Reversed target accepted.");
Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(addDraft with { Question = "Hỏi Lan nhận thêm bao nhiêu quyển sách?" }), addition).IsValid, "Given quantity used as the target accepted.");
Check(!BasicQuestionValidator.Validate("{\"given_a\":\"x\",\"given_a\":\"y\",\"question\":\"z\"}", addition).IsValid, "Duplicate JSON fields accepted.");
Check(!BasicQuestionValidator.Validate(Draft(addition)[..^3], addition).IsValid, "Truncated JSON accepted.");
var naturalContext = BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(addDraft with { GivenA = "Ở thư viện nhỏ, Lan có 7 cuốn sách." }), addition);
Check(naturalContext.IsValid, "Natural context / equivalent unit rejected: " + naturalContext.ErrorCode);
Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(addDraft with { GivenB = "Bạn Lan vừa được tặng 2 quyển sách." }), addition).IsValid, "Natural name prefix / passive receipt rejected.");
Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(addDraft with { GivenB = "Bình nhận thêm 2 quyển sách từ Lan." }), addition).IsValid, "Wrong receiving actor accepted.");
Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(addDraft with { GivenA = "Bình có 7 quyển sách của Lan." }), addition).IsValid, "Wrong owning actor accepted.");
Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(addDraft with { GivenB = "Lan nhận thêm 2 quyển sách và hai quyển sách." }), addition).IsValid, "Additional written quantity accepted.");
Check(!(addition with { Unit = "quả táo" }).IsValid, "Unsupported unit contract accepted.");
var division = addition with { Operation = ArithmeticOperation.Divide, Left = 8 };
var divisionDraft = BasicQuestionValidator.Validate(Draft(division), division).Draft!;
Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(divisionDraft with { Question = "Mỗi thùng có bao nhiêu quyển sách?" }), division).IsValid, "Natural implicit subject rejected.");
Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(divisionDraft with { Question = "Mỗi thùng của Bình có bao nhiêu quyển sách?" }), division).IsValid, "Changed box owner accepted.");
Console.WriteLine("PASS changed relationships, targets, malformed JSON and natural aliases");

string directory = Path.Combine(Path.GetTempPath(), "MathSolver-AiBank-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    var store = new QuestionBankStore(Path.Combine(directory, "bank.db3"));
    var validated = new ValidatedBankQuestion(addition, addDraft, Draft(addition), "test-model", DateTime.UtcNow);
    Check(await store.InsertAsync(validated), "Initial insert failed.");
    Check(!await store.InsertAsync(validated), "Duplicate inserted.");
    var reopened = new QuestionBankStore(Path.Combine(directory, "bank.db3"));
    Check((await reopened.TakeAsync(addition.Operation, addition.Tier, addition.Language))?.Draft == addDraft, "Persisted question did not round-trip.");
    Check(await store.TakeAsync(ArithmeticOperation.Subtract, addition.Tier, addition.Language) is null, "Wrong subtype returned.");
    Check(await store.TakeAsync(addition.Operation, CurriculumTier.TwoStars, addition.Language) is null, "Wrong stars returned.");
    Check(await store.TakeAsync(addition.Operation, addition.Tier, AppLanguage.English) is null, "Wrong language returned.");
    bool rejected = false;
    try { await store.InsertAsync(validated with { Draft = addDraft with { GivenB = "Lan cho đi 2 quyển sách." } }); }
    catch (InvalidOperationException) { rejected = true; }
    Check(rejected, "Storage boundary accepted invalid prose.");
    string blockedDirectory = Path.Combine(directory, "not-a-directory");
    await File.WriteAllTextAsync(blockedDirectory, "test");
    Check(await new QuestionBankStore(Path.Combine(blockedDirectory, "bank.db3"))
        .TakeAsync(addition.Operation, addition.Tier, addition.Language) is null, "Unavailable database prevented C# fallback.");
    var modelLibrary = new AiModelLibrary(Path.Combine(directory, "models"));
    await using (var invalidModel = new MemoryStream("invalid model"u8.ToArray()))
    {
        bool invalidHeader = false;
        try { await modelLibrary.ImportAsync(invalidModel, "wrong.gguf", CancellationToken.None); }
        catch (InvalidDataException) { invalidHeader = true; }
        Check(invalidHeader && !Directory.EnumerateFiles(modelLibrary.DirectoryPath).Any(), "Invalid import published a model or left a partial file.");
    }
    Console.WriteLine("PASS SQLite persistence, deduplication, selection and invalid-insert protection");
    await PracticeProviderTests.RunAsync(directory, validated, Draft);
    await BackgroundPracticeTests.RunAsync(directory);
    await BankDataTests.RunAsync(directory, validated, Draft);
    await SqlMutationTests.RunAsync(directory, validated);
    await SqlGridTests.RunAsync(directory, validated);
    await StreamingTests.RunAsync(Draft);

    var memoryStore = new MemoryStore();
    var fake = new FakeRuntime(prompt =>
    {
        string marker = prompt.Contains("\nJSON: ") ? "\nJSON: " : "Correct role example: ";
        return prompt.Split(marker)[1].Split('\n')[0];
    });
    var service = new AiQuestionGenerationService(fake, memoryStore);
    service.Start(new(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese, 3, false));
    await service.Completion;
    Check(service.Snapshot.State == AiJobState.Completed && service.Snapshot.Items.Count == 3 && memoryStore.Saved == 0, "Manual batch auto-saved or failed.");
    await service.InsertAsync(2);
    Check(memoryStore.Saved == 1 && service.Snapshot.Items[1].State == AiItemState.Saved, "Manual insert failed.");
    service.Start(new(ArithmeticOperation.Multiply, CurriculumTier.TwoStars, AppLanguage.Vietnamese, 2, true));
    await service.Completion;
    Check(memoryStore.Saved == 3 && service.Snapshot.Items.All(i => i.State == AiItemState.Saved), "Auto batch failed.");
    var invalid = new FakeRuntime(_ => "{}");
    service = new(invalid, memoryStore);
    service.Start(new(ArithmeticOperation.Subtract, CurriculumTier.OneStar, AppLanguage.Vietnamese, 10, true));
    await service.Completion;
    Check(invalid.Calls == 3 && service.Snapshot.State == AiJobState.Failed && memoryStore.Saved == 3, "Retry bound/save isolation failed.");
    var blocking = new BlockingRuntime();
    service = new(blocking, memoryStore);
    service.Start(new(ArithmeticOperation.Divide, CurriculumTier.OneStar, AppLanguage.Vietnamese, 10, true));
    await blocking.Started.Task;
    service.Stop();
    await service.Completion;
    Check(service.Snapshot.State == AiJobState.Stopped && memoryStore.Saved == 3, "Cancelled output saved.");
    var retryStore = new FailingStore();
    service = new(fake, retryStore);
    service.Start(new(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese, 3, true));
    await service.Completion;
    Check(service.Snapshot.State == AiJobState.Failed && service.Snapshot.Items.Count == 1
        && service.Snapshot.Items[0].State == AiItemState.SaveFailed && service.Snapshot.Items[0].Question is not null,
        "Failed insertion discarded the valid preview or continued the batch.");
    await service.InsertAsync(1);
    Check(service.Snapshot.Items[0].State == AiItemState.Saved && retryStore.Attempts == 2, "Manual save recovery failed.");
    Console.WriteLine("PASS batch, manual/auto insertion, three-attempt limit and cancellation");
    await MemoryLifecycleTests.RunAsync();
    await LanguageTests.RunAsync(directory);
    await TemplateTests.RunAsync(directory);
    await AdditionTests.RunAsync(directory);
    await ArithmeticContextTests.RunAsync(directory);
    OneStepPracticeTests.Run();
    await LearningProfileTests.RunAsync(directory);
    await FindXBankTests.RunAsync(directory);
}
finally { Directory.Delete(directory, true); }

Console.WriteLine("All AI question bank checks passed. No model inference or benchmark ran.");

sealed class FakeRuntime(Func<string, string> produce) : IQuestionTextRuntime
{
    public bool IsLoaded => true;
    public string ModelName => "fake";
    public int Calls { get; private set; }
    public Task<string> GenerateAsync(BasicQuestionContract contract, string prompt, CancellationToken token,
        Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null) { Calls++; return Task.FromResult(produce(prompt)); }
}
sealed class BlockingRuntime : IQuestionTextRuntime
{
    public bool IsLoaded => true;
    public string ModelName => "blocking";
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<string> GenerateAsync(BasicQuestionContract contract, string prompt, CancellationToken token,
        Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
    { Started.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return "{}"; }
}
sealed class MemoryStore : IQuestionBankStore
{
    public int Saved { get; private set; }
    public Task<bool> InsertAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default)
    { Saved++; return Task.FromResult(true); }
    public Task<ValidatedBankQuestion?> TakeAsync(ArithmeticOperation operation, CurriculumTier tier, AppLanguage language, CancellationToken cancellationToken = default)
        => Task.FromResult<ValidatedBankQuestion?>(null);
}
sealed class FailingStore : IQuestionBankStore
{
    public int Attempts { get; private set; }
    public Task<bool> InsertAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default)
    {
        Attempts++;
        return Attempts == 1 ? Task.FromException<bool>(new IOException("Test storage failure")) : Task.FromResult(true);
    }
    public Task<ValidatedBankQuestion?> TakeAsync(ArithmeticOperation operation, CurriculumTier tier, AppLanguage language, CancellationToken cancellationToken = default)
        => Task.FromResult<ValidatedBankQuestion?>(null);
}
