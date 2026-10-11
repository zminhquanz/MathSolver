using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using System.Text.Json;

internal static class MixedKnowledgeGroupsTests
{
    internal static async Task Run(string directory)
    {
        var mixed = new QuestionLearningProfile(QuestionKnowledgeGroup.Mixed);
        Check(!mixed.IsValid && mixed.IsMixed, "Mixed selection must not be a stored language profile.");
        Check(!QuestionLearningProfile.Groups().Contains(mixed.Group), "AI/Excel authoring exposed a mixed contract.");
        Check(QuestionLearningProfile.Groups(includeMixed: true)[0] == mixed.Group, "Mixed is not the first practice option.");
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var choice = QuizChoiceCatalog.Create(0, "Learning.Group.Mixed", language == AppLanguage.Vietnamese ? "Hỗn hợp" : "Mixed", language, true);
            Check(choice.IllustrationId == "mixed" && !choice.Description.StartsWith("Choice."), "Missing mixed illustration or translated description.");
        }
        try
        {
            new AiGenerationOptions(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese, 1, false, mixed).Validate();
            throw new Exception("AI accepted a mixed language contract.");
        }
        catch (ArgumentOutOfRangeException) { }

        int cases = 0;
        var validator = new ArithmeticQuizValidator(new BasicArithmeticEngine());
        foreach (bool saved in new[] { false, true })
        foreach (var kind in new[] { QuizProblemKind.Arithmetic, QuizProblemKind.FindX, QuizProblemKind.Fraction })
        foreach (var operation in Enum.GetValues<ArithmeticOperation>())
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var store = new CapturingStore(saved);
            var factory = new PracticeQuestionFactory(new(store, new SavedSourceRandom()), new CyclingRandom());
            var eligible = QuestionLearningProfile.Groups().Where(group => Eligible(kind, operation, tier, new(group))).ToArray();
            Check(eligible.Length > 0, "No supported knowledge groups.");
            var request = Request(kind, operation);
            var seen = new HashSet<QuestionKnowledgeGroup>();
            foreach (var expected in eligible)
            {
                var question = await factory.CreateAsync(request, mode, new(tier, false), language, mixed,
                    PracticeQuestionFormat.WordProblem, kind == QuizProblemKind.Fraction);
                Check(store.Group == expected && seen.Add(store.Group), "Mixed did not select each eligible group or sent the sentinel to SQLite.");
                // Fraction stories retain rational answers (and sometimes a
                // multi-step expression), rather than the legacy integer key.
                var validation = kind == QuizProblemKind.Fraction
                    ? ValidateFraction(question)
                    : validator.Validate(question);
                Check(question.WordProblem is not null && validation.IsValid,
                    $"Invalid mixed story: {kind}/{operation}/{language}/{tier}/{mode}/{expected}/saved={saved}: {validation.ErrorCode}");
                Check(QuizWorksheetBuilder.Format(question, language).Table is null,
                    $"Repeated facts in a built-in/stored PDF preview: {kind}/{operation}/{language}/{tier}/{mode}/{expected}/saved={saved}");
                Check(kind switch
                {
                    QuizProblemKind.FindX => question.FindXProblem?.Operation == operation,
                    QuizProblemKind.Fraction => question.Expression.Operation == operation,
                    _ => question.Expression.Operation == operation
                }, "Mixing knowledge groups changed the fixed operation.");
                cases++;
            }
            Check(seen.SetEquals(eligible), "Missing eligible knowledge groups.");
            int reads = store.Reads;
            var numeric = await factory.CreateAsync(request, mode, new(tier, false), language, mixed, PracticeQuestionFormat.Numeric, false);
            Check(store.Reads == reads && numeric.WordProblem is null, "Mixed groups changed numeric-only practice or read the bank.");
        }

        var practice = new QuizWorksheetSettings(1, Request(QuizProblemKind.Arithmetic, ArithmeticOperation.Add),
            CurriculumTier.ThreeStars, AppLanguage.Vietnamese, QuestionKnowledgeGroup.Objects, ArithmeticWords: true);
        string original = JsonSerializer.Serialize(practice);
        var settings = practice with { Group = QuestionKnowledgeGroup.Mixed };
        var batchStore = new CapturingStore(false);
        var batchFactory = new PracticeQuestionFactory(new(batchStore, new SavedSourceRandom()), new CyclingRandom());
        var selectedGroups = new HashSet<QuestionKnowledgeGroup>();
        var catalog = new QuizProblemTypeCatalog(new Random(127));
        var batch = await QuizWorksheetBuilder.CreateAsync(20, "Bộ đề hỗn hợp nhóm kiến thức", "Cộng · 3 sao · Hỗn hợp",
            settings.Language, settings.Tier, ArithmeticQuizMode.Essay, async token =>
            {
                var question = await settings.GenerateAsync(batchFactory, catalog, token);
                selectedGroups.Add(batchStore.Group);
                return question;
            });
        Check(selectedGroups.Count > 1 && !selectedGroups.Contains(QuestionKnowledgeGroup.Mixed), "PDF set used only one group or retained the sentinel.");
        int beforeExport = batchStore.Reads;
        string frozen = JsonSerializer.Serialize(batch);
        File.WriteAllBytes(Path.Combine(directory, "mixed-knowledge-worksheet.pdf"), QuizPdfWriter.Write(batch, false));
        File.WriteAllBytes(Path.Combine(directory, "mixed-knowledge-answers.pdf"), QuizPdfWriter.Write(batch, true));
        Check(batchStore.Reads == beforeExport && JsonSerializer.Serialize(batch) == frozen && JsonSerializer.Serialize(practice) == original,
            "PDF export regenerated the questions or changed practice selections.");
        Console.WriteLine($"PASS mixed knowledge groups: {cases} built-in/stored story cases across three families, four operations, five stars, two languages and all modes; frozen mixed PDF export and numeric isolation.");
    }

    private static QuizProblemRequest Request(QuizProblemKind kind, ArithmeticOperation operation) => kind switch
    {
        QuizProblemKind.FindX => new(kind, FindXOperation: operation),
        QuizProblemKind.Fraction => new(kind, FractionOperation: (FractionOperation)operation),
        _ => new(kind, ArithmeticOperation: operation)
    };
    private static ArithmeticQuizValidationResult ValidateFraction(ArithmeticQuizQuestion question)
    {
        string? expression = question.FractionProblem?.ExpressionText ?? question.ExpressionProblem?.ExpressionText;
        var answer = question.ExactAnswer;
        var word = question.WordProblem;
        if (expression is null || word is null || !new EssayAnswerValidator(new BasicArithmeticEngine())
            .Validate(question, word.SolutionLead, $"{expression} = {answer} {word.AnswerUnit}", $"{answer} {word.AnswerUnit}").IsCorrect)
            return new(false, "IncorrectFractionAnswer");
        var presented = question.FractionProblem?.PresentedAnswer ?? question.ExpressionProblem?.PresentedAnswer;
        var choices = question.FractionProblem?.Choices ?? question.ExpressionProblem!.Choices;
        bool valid = question.Mode switch
        {
            ArithmeticQuizMode.TrueFalse => presented is not null && question.PresentedEquationIsCorrect == (presented == answer),
            ArithmeticQuizMode.MultipleChoice => choices.Count == 4 && choices.Distinct().Count() == 4 && choices.Contains(answer),
            _ => true
        };
        return new(valid, valid ? null : "IncorrectFractionAnswerMode");
    }
    private static bool Eligible(QuizProblemKind kind, ArithmeticOperation operation, CurriculumTier tier, QuestionLearningProfile profile) => kind switch
    {
        QuizProblemKind.FindX => FindXQuestionCatalogue.Available(profile, operation, tier).Any(),
        QuizProblemKind.Fraction => FractionQuestionCatalogue.Available(profile, operation, tier).Any(),
        _ => AppliedQuestionCatalogue.Available(profile, operation, tier).Any()
    };
    private sealed class CyclingRandom : Random
    {
        private int _next;
        public override int Next(int maxValue) => _next++ % maxValue;
    }
    private sealed class SavedSourceRandom : Random
    {
        public override int Next(int maxValue) => maxValue == 2 ? 1 : base.Next(maxValue);
    }
    private sealed class CapturingStore(bool saved) : IQuestionBankStore
    {
        public int Reads { get; private set; }
        public QuestionKnowledgeGroup Group { get; private set; }
        public Task<bool> InsertAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ValidatedBankQuestion?> TakeAsync(ArithmeticOperation operation, CurriculumTier tier,
            AppLanguage language, CancellationToken cancellationToken = default) => throw new Exception("Mixed lost its concrete profile.");
        public Task<ValidatedBankQuestion?> TakeForProfileAsync(ArithmeticOperation operation, CurriculumTier tier,
            AppLanguage language, QuestionLearningProfile profile, CancellationToken cancellationToken = default) => Read(QuizProblemKind.Arithmetic, operation, tier, language, profile);
        public Task<ValidatedBankQuestion?> TakeFindXAsync(ArithmeticOperation operation, CurriculumTier tier,
            AppLanguage language, QuestionLearningProfile profile, CancellationToken cancellationToken = default) => Read(QuizProblemKind.FindX, operation, tier, language, profile);
        public Task<ValidatedBankQuestion?> TakeFractionAsync(ArithmeticOperation operation, CurriculumTier tier,
            AppLanguage language, QuestionLearningProfile profile, CancellationToken cancellationToken = default) => Read(QuizProblemKind.Fraction, operation, tier, language, profile);
        private Task<ValidatedBankQuestion?> Read(QuizProblemKind kind, ArithmeticOperation operation, CurriculumTier tier,
            AppLanguage language, QuestionLearningProfile profile)
        {
            Check(profile.IsValid && !profile.IsMixed && Eligible(kind, operation, tier, profile), "Invalid profile reached the bank.");
            Group = profile.Group; Reads++;
            if (!saved) return Task.FromResult<ValidatedBankQuestion?>(null);
            var contract = kind switch
            {
                QuizProblemKind.FindX => FindXQuestionCatalogue.Create(profile, operation, tier, language, new Random(91)),
                QuizProblemKind.Fraction => FractionQuestionCatalogue.Create(profile, operation, tier, language, new Random(92)),
                _ => AppliedQuestionCatalogue.Create(profile, operation, tier, language, new Random(93))
            };
            var draft = kind switch
            {
                QuizProblemKind.FindX => FindXQuestionCatalogue.Draft(contract),
                QuizProblemKind.Fraction => FractionQuestionCatalogue.Draft(contract),
                _ => AppliedQuestionCatalogue.Draft(contract)
            };
            return Task.FromResult<ValidatedBankQuestion?>(new(contract, draft, QuestionBankStore.SerializeDraft(draft), "mixed-test", DateTime.UtcNow));
        }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
