using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text;
using System.Text.Json;
using static BankDraftFixture;

internal static class DefaultBankSuite
{
    internal static async Task RunAsync()
    {
        await HarnessInfrastructureTests.RunAsync();
        await OpenProseTests.RunAsync();
        await DecimalOpenTests.RunAsync();
        await SameDayTimeTests.RunAsync();
        await FractionPartOpenTests.RunAsync();
        await FractionRemainingOpenTests.RunAsync();
        await FractionWholeOpenTests.RunAsync();
        await AverageOpenTests.RunAsync();
        await PercentagePartOpenTests.RunAsync();
        await PercentageRemainingOpenTests.RunAsync();
        await PercentageWholeOpenTests.RunAsync();
        await RectanglePerimeterOpenTests.RunAsync();
        await AppliedNarrativeBankTests.RunAsync();
        await RegexTimeoutRegressionTests.RunAsync();
        await ExpandedArithmeticTests.RunAsync();
        await ModelHarnessTests.RunAsync();
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


        using var workspace = TestWorkspace.Temporary("AiBank");
        string directory = workspace.DirectoryPath;

        QuizContentTests.Run();
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
        await ProseDeduplicationTests.RunAsync(directory);
        await DeleteAllTests.RunAsync(directory);
        await PracticeProviderTests.RunAsync(directory, validated, Draft);
        await BackgroundPracticeTests.RunAsync(directory);
        await BankDataTests.RunAsync(directory, validated, Draft);
        await SqlMutationTests.RunAsync(directory, validated);
        await SqlGridTests.RunAsync(directory, validated);
        await StreamingTests.RunAsync(Draft);

        await WorkerLifecycleTests.RunAsync();
        await MemoryLifecycleTests.RunAsync();
        await LanguageTests.RunAsync(directory);
        await TemplateTests.RunAsync(directory);
        await AdditionTests.RunAsync(directory);
        await ArithmeticContextTests.RunAsync(directory);
        OneStepPracticeTests.Run();
        await LearningProfileTests.RunAsync(directory);
        await FindXBankTests.RunAsync(directory);
        await PracticeFormatTests.RunAsync(directory);
        await FractionBankTests.RunAsync(directory);
        await ReasoningStoryTests.RunAsync();
        await NextAiFormsTests.RunAsync();
        await MultiStepBankTests.RunAsync();
        await MotionBankTests.RunAsync();
        await ProportionBankTests.RunAsync();
        await DecimalBankTests.RunAsync();
        await MeasurementBankTests.RunAsync();
        await PackingTimeBankTests.RunAsync();
        await ActivityDepthTests.RunAsync();
        await FractionQuantityBankTests.RunAsync();
        await ProseExpansionTests.RunAsync();
        await NarrativeDepthTests.RunAsync();
        await QuantityDepthTests.RunAsync();



        GenerationStructurePolicyTests.Run();
        await TimeFractionBankTests.RunAsync();
        await DecimalSpatialBankTests.RunAsync();
        Console.WriteLine("All AI question bank checks passed. No model inference or benchmark ran.");
    }
}
