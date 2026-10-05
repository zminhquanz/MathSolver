using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;

internal static class LearningProfileTests
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public static async Task RunAsync(string directory)
    {
        int checkedQuestions = 0;
        var math = new BasicArithmeticEngine();
        var quizValidator = new ArithmeticQuizValidator(math);
        var essay = new EssayAnswerValidator(math);
        foreach (var group in QuestionLearningProfile.Groups())
        foreach (var operation in Enum.GetValues<ArithmeticOperation>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var profile = new QuestionLearningProfile(group);
            if (!profile.Allows(operation)) continue;
            var scenes = AppliedQuestionCatalogue.Available(profile, operation, tier).ToArray();
            Check(scenes.Length > 0, $"No scene for {profile}/{operation}/{tier}");
            foreach (var scene in scenes)
            for (int seed = 0; seed < 12; seed++)
            {
                var c = AppliedQuestionCatalogue.Create(profile, operation, tier, language, new Random(seed * 31 + 17), scene.Id);
                Check(c.IsValid, $"Invalid generated facts: {profile}/{operation}/{tier}/{language}/{scene.Id} {c.Left}, {c.Right}");
                Check(c.Grade == 0 && c.KnowledgeGroup == group && c.Operation == operation, "Lost group or reintroduced grade metadata.");
                if (group == QuestionKnowledgeGroup.Objects)
                    Check(c.Left <= profile.ArithmeticCeiling(tier) && c.Right <= profile.ArithmeticCeiling(tier)
                        && c.Answer <= profile.ArithmeticCeiling(tier), "Count scale does not follow stars.");
                if (operation == ArithmeticOperation.Divide && AppliedQuestionCatalogue.Reasoning(c) is null)
                    Check(c.Left % c.Right == 0, "Fractional quotient in integer profile.");
                string prompt = BasicQuestionPrompt.Build(c);
                Check(prompt.Length < 2300 && prompt.Contains("input units={\"GivenA\":") && !prompt.Contains("\"Left\":")
                    && !prompt.Contains("grade="),
                    $"Prompt became large or exposed facts: {scene.Id}/{language} length {prompt.Length}");
                string grammar = GgufQuestionRuntime.BuildGrammar(c);
                Check(grammar.StartsWith("root ::=") && grammar.All(ch => ch <= 127), "Grammar is not escaped GBNF.");
                for (int variant = 0; variant < 2; variant++)
                {
                    var draft = AppliedQuestionCatalogue.Draft(c, variant);
                    string raw = QuestionBankStore.SerializeDraft(draft);
                    var validated = BasicQuestionValidator.Validate(raw, c);
                    Check(validated.IsValid, $"Reviewed draft rejected: {scene.Id}/{language}/{variant}: {validated.ErrorCode}");
                    var word = validated.Draft!.ToWordProblem(c);
                    Check(!word.ProblemText.Contains('{') && !word.SolutionLead.Contains('{'), "Unrendered slot in prose.");
                    Check(StreamingQuestionPreview.Render(raw, c) == word.ProblemText, "Live preview changed units or roles.");
                    Check(!BasicQuestionValidator.Validate(raw.Replace("{a}", "{b}"), c).IsValid, "Changed quantity role passed.");
                    Check(!BasicQuestionValidator.Validate(raw.Replace(draft.GivenB,
                        draft.GivenB + (language == AppLanguage.Vietnamese ? " Một nhóm khác tham gia." : " Another group joins.")), c).IsValid,
                        "Extra fact passed.");
                    Check(!BasicQuestionValidator.Validate(raw.Replace(draft.GivenA, draft.GivenA + " łącznie"), c).IsValid, "Foreign text passed.");
                    foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                    {
                        var question = c.ToPracticeQuestion(word, mode, new Random(seed));
                        Check(quizValidator.Validate(question).IsValid && question.CorrectAnswer == c.Answer, "Invalid answer mode.");
                        if (mode != ArithmeticQuizMode.Essay) continue;
                        string equation = $"{AppliedQuestionCatalogue.Reasoning(c)?.Equation ?? $"{c.Left} {BasicArithmeticEngine.GetSymbol(operation)} {c.Right}"} = {c.Answer} {c.AnswerUnit}";
                        var graded = essay.Validate(question, word.SolutionLead, equation, $"{c.Answer} {c.AnswerUnit}");
                        Check(graded.IsCorrect, $"Correct solution failed grading: {scene.Id}/{language}: {graded}");
                        Check(!essay.Validate(question, word.SolutionLead, equation, $"{c.Answer + 1} {c.AnswerUnit}").IsCorrect, "Wrong answer passed.");
                        string badUnit = c.AnswerUnit == "m³" ? "m²" : c.AnswerUnit == "m²" ? "m³" : c.AnswerUnit == "kg" ? "km" : "kg";
                        Check(!essay.Validate(question, word.SolutionLead, equation, $"{c.Answer} {badUnit}").IsCorrect,
                            $"Cross-dimensional answer unit passed: {scene.Id}/{language}");
                        if (word.FactTable is { } table) Check(table.Rows.Count == 2 && table.Rows[0].Value == c.Left.ToString()
                            && table.Rows[1].Value == c.Right.ToString(), "Displayed table changed C# facts.");
                        if (word.ArithmeticReasoning is { } reasoning) {
                            Check(!quizValidator.Validate(question with { CorrectAnswer = c.Answer + 1 }).IsValid, "Wrong derived answer key passed.");
                            Check(!quizValidator.Validate(question with { WordProblem = word with { ArithmeticReasoning = reasoning with { Left = c.Left + 1 } } }).IsValid,
                                "Derived rule accepted facts unrelated to the question.");
                        }
                    }
                    checkedQuestions++;
                }
                var refreshed = c.FreshFacts(new Random(seed + 901));
                Check(refreshed.IsValid && refreshed.Grade == 0 && refreshed.KnowledgeGroup == group
                    && refreshed.SceneId == c.SceneId, "Fresh facts changed knowledge/dimensions.");
            }
        }
        Check(QuestionLearningProfile.Groups().Length == 9
            && !new QuestionLearningProfile((QuestionKnowledgeGroup)99).IsValid, "Invalid or grade-filtered groups.");
        var advancedSmall = AppliedQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects), ArithmeticOperation.Add,
            CurriculumTier.FiveStars, AppLanguage.Vietnamese, new Random(18), "school-pencils-object-original");
        Check(advancedSmall.Left < 100 && advancedSmall.Right < 100 && advancedSmall.Structure == BasicQuestionStructure.RecoverInitial,
            "High stars forced a large collection or lost inverse reasoning.");
        var money = AppliedQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Money), ArithmeticOperation.Multiply,
            CurriculumTier.FiveStars, AppLanguage.Vietnamese, new Random(18));
        Check(AppliedQuestionCatalogue.InputUnits(money) == ("đồng", "quyển vở") && money.AnswerUnit == "đồng",
            "Price and item count were treated as the same dimension.");
        var conversion = AppliedQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Mass), ArithmeticOperation.Add,
            CurriculumTier.FiveStars, AppLanguage.Vietnamese, new Random(31), "mass-convert-total");
        Check(conversion.Left % 100 == 0 && AppliedQuestionCatalogue.ConversionStep(conversion) is not null,
            "Exact conversion metadata was lost.");
        var conversionQuestion = conversion.ToPracticeQuestion(AppliedQuestionCatalogue.Draft(conversion).ToWordProblem(conversion), ArithmeticQuizMode.Essay);
        Check(ElementaryWordProblemSolutionFormatter.Format(conversionQuestion, AppLanguage.Vietnamese,
            System.Globalization.CultureInfo.GetCultureInfo("vi-VN")).StartsWith(AppliedQuestionCatalogue.ConversionStep(conversion)!),
            "Worked solution omitted the intermediate unit conversion.");
        var motion = AppliedQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Motion), ArithmeticOperation.Multiply,
            CurriculumTier.FiveStars, AppLanguage.Vietnamese, new Random(7), "motion-distance-minutes");
        Check(AppliedQuestionCatalogue.InputUnits(motion) == ("km/h", "phút")
            && AppliedQuestionCatalogue.ConversionStep(motion)!.Contains("phút ="), "High-star motion lost time conversion.");
        foreach (var c in new[] { conversion, motion })
        {
            var question = c.ToPracticeQuestion(AppliedQuestionCatalogue.Draft(c).ToWordProblem(c), ArithmeticQuizMode.Essay);
            string worked = ElementaryWordProblemSolutionFormatter.Format(question, c.Language, System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));
            var parts = EssayCombinedInputParser.Parse(worked, true, preserveAllCalculations: true);
            Check(essay.Validate(question, parts.Solution, parts.Equation, parts.Answer).IsCorrect,
                "Displayed conversion solution cannot be submitted for grading.");
            string wrong = parts.Equation.Replace(question.WordProblem!.ConversionStep!, "1 kg = 1 g");
            Check(!essay.Validate(question, parts.Solution, wrong, parts.Answer).IsCorrect, "Wrong intermediate conversion was ignored.");
            Check(!essay.Validate(question, parts.Solution, parts.Equation.Replace(question.WordProblem.ConversionStep!, "1 km = 1000 g"), parts.Answer).IsCorrect,
                "Conversion between unrelated dimensions passed.");
        }
        await PersistenceAndPracticeAsync(directory);
        await ExpandedPersistenceAsync(directory);
        Console.WriteLine($"PASS {checkedQuestions} bilingual group/stars scene instantiations, semantic rejection, streaming and grading; grade-independent SQLite/Excel and C#/bank mix");
    }

    private static async Task ExpandedPersistenceAsync(string directory)
    {
        var store = new QuestionBankStore(Path.Combine(directory, "expanded-groups.db3"));
        foreach (var group in QuestionLearningProfile.Groups())
        foreach (var op in Enum.GetValues<ArithmeticOperation>())
        {
            var c = AppliedQuestionCatalogue.Create(new(group), op, CurriculumTier.FiveStars, AppLanguage.Vietnamese, new Random(47));
            var d = AppliedQuestionCatalogue.Draft(c);
            Check(await store.InsertAsync(new(c, d, QuestionBankStore.SerializeDraft(d), "expanded-test", DateTime.UtcNow)), "New group insert failed.");
            var saved = await store.TakeForProfileAsync(op, c.Tier, c.Language, new(group));
            var word = d.ToWordProblem(c);
            Check(saved?.Contract == c && saved.WordProblem.ProblemText == word.ProblemText && saved.WordProblem.AnswerUnit == word.AnswerUnit
                && saved.WordProblem.ArithmeticReasoning == word.ArithmeticReasoning
                && (word.FactTable is null || saved.WordProblem.FactTable?.Rows.SequenceEqual(word.FactTable.Rows) == true), "New group lost data in SQLite.");
        }
        // Existing dimensional templates remain usable through the merged Measurement picker.
        foreach (var old in new[] { QuestionKnowledgeGroup.Mass, QuestionKnowledgeGroup.Length, QuestionKnowledgeGroup.Transport })
        {
            var c = AppliedQuestionCatalogue.Create(new(old), ArithmeticOperation.Subtract, CurriculumTier.ThreeStars, AppLanguage.English, new Random(9));
            var d = AppliedQuestionCatalogue.Draft(c);
            await store.InsertAsync(new(c, d, QuestionBankStore.SerializeDraft(d), "legacy-dimension", DateTime.UtcNow));
        }
        var read = new HashSet<QuestionKnowledgeGroup>();
        for (int i = 0; i < 3; i++) {
            var saved = await store.TakeForProfileAsync(ArithmeticOperation.Subtract, CurriculumTier.ThreeStars, AppLanguage.English, new(QuestionKnowledgeGroup.Measurement));
            Check(saved is not null && saved.Contract.FreshFacts().IsValid, "Merged measurement stranded historical dimensional data.");
            read.Add(saved!.Contract.KnowledgeGroup);
        }
        Check(read.SetEquals([QuestionKnowledgeGroup.Mass, QuestionKnowledgeGroup.Length, QuestionKnowledgeGroup.Transport]), "Legacy subgroups did not share measurement selection.");
        using var workbook = new MemoryStream();
        var exported = await store.ExportExcelAsync(workbook);
        Check(exported.Exported == 39 && exported.Skipped == 0, "New group Excel export failed.");
        workbook.Position = 0;
        Check(QuestionBankWorkbook.Read(workbook).All(row => row.Question is not null && row.Question.Contract.IsValid), "New group Excel import failed.");
        var runtime = new TemplateRuntime();
        var service = new AiQuestionGenerationService(runtime, store);
        service.Start(new(ArithmeticOperation.Divide, CurriculumTier.FiveStars, AppLanguage.English, 4, true, new(QuestionKnowledgeGroup.Packaging)));
        await service.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Check(service.Snapshot.State == AiJobState.Completed && service.Snapshot.Items.All(i => i.State == AiItemState.Saved)
            && service.Snapshot.Items.Any(i => i.Question?.WordProblem.ArithmeticReasoning?.Rule == AppliedArithmeticRule.MinimumGroups)
            && service.Snapshot.Items.Any(i => i.Question?.WordProblem.ArithmeticReasoning?.Rule == AppliedArithmeticRule.Remainder), "Background AI skipped non-exact division semantics.");
    }

    private static async Task PersistenceAndPracticeAsync(string directory)
    {
        var store = new QuestionBankStore(Path.Combine(directory, "learning.db3"));
        var profile = new QuestionLearningProfile(QuestionKnowledgeGroup.Money);
        var c = AppliedQuestionCatalogue.Create(profile, ArithmeticOperation.Multiply, CurriculumTier.FiveStars, AppLanguage.Vietnamese, new Random(84));
        var d = AppliedQuestionCatalogue.Draft(c);
        var q = new ValidatedBankQuestion(c, d, QuestionBankStore.SerializeDraft(d), "profile-test", DateTime.UtcNow);
        Check(await store.InsertAsync(q), "Cannot insert new profile.");
        Check(await store.TakeForProfileAsync(c.Operation, c.Tier, c.Language, profile) is not null, "Cannot read matching profile.");
        Check(await store.TakeForProfileAsync(c.Operation, c.Tier, c.Language, profile with { Group = QuestionKnowledgeGroup.Objects }) is null,
            "Cross-knowledge selection.");
        Check(await store.TakeAsync(c.Operation, c.Tier, c.Language) is null, "Legacy selection consumed profile row.");
        var grid = await store.QueryAsync("SELECT * FROM BasicQuestionBank");
        Check(grid.IsSuccess && grid.EditableColumns?.Contains("Grade") == true
            && grid.EditableColumns.Contains("KnowledgeGroup"), "New profile columns disabled the editable SQL grid.");
        using var workbook = new MemoryStream();
        var export = await store.ExportExcelAsync(workbook);
        Check(export.Exported == 1 && export.Skipped == 0, "Profile export failed.");
        workbook.Position = 0;
        var imported = QuestionBankWorkbook.Read(workbook).Single();
        Check(imported.Question?.Contract == c, "Excel lost learning metadata.");
        var reimport = new QuestionBankStore(Path.Combine(directory, "learning-import.db3"));
        Check(await reimport.InsertAsync(imported.Question!) && !await reimport.InsertAsync(q), "Profile deduplication lost on Excel roundtrip.");

        // Saved grade metadata no longer partitions the bank. Its template must
        // remain readable, but new practice operands use the group/star policy.
        var historical = new QuestionBankStore(Path.Combine(directory, "historical-grades.db3"));
        foreach (int oldGrade in new[] { 4, 5 })
            Check(await historical.InsertAsync(q with { Contract = c with { Grade = oldGrade } }), "Historical grade insert failed.");
        var readGrades = new HashSet<int>();
        for (int i = 0; i < 2; i++)
        {
            var saved = await historical.TakeForProfileAsync(c.Operation, c.Tier, c.Language, profile);
            Check(saved is not null, "A historical grade still filtered a matching bank template.");
            readGrades.Add(saved!.Contract.Grade);
            var fresh = saved.Contract.FreshFacts(new Random(i + 90));
            Check(fresh.Grade == 0 && fresh.IsValid && fresh.KnowledgeGroup == profile.Group,
                "Historical grade constraints survived fresh fact generation.");
        }
        Check(readGrades.SetEquals([4, 5]), "Different historical grades did not share the same question pool.");

        var legacyStore = new QuestionBankStore(Path.Combine(directory, "legacy-group.db3"));
        var legacyContract = BasicQuestionContract.CreateTemplate(ArithmeticOperation.Add, CurriculumTier.ThreeStars,
            AppLanguage.Vietnamese, new Random(71));
        var legacyDraft = BasicQuestionTemplates.Example(legacyContract);
        Check(await legacyStore.InsertAsync(new(legacyContract, legacyDraft, QuestionBankStore.SerializeDraft(legacyDraft),
            "legacy", DateTime.UtcNow)), "Cannot insert legacy counted template.");
        Check(await legacyStore.TakeForProfileAsync(legacyContract.Operation, legacyContract.Tier, legacyContract.Language,
            new(QuestionKnowledgeGroup.Objects)) is not null, "Removing the general picker stranded old counted templates.");
        Check(await legacyStore.TakeForProfileAsync(legacyContract.Operation, legacyContract.Tier, legacyContract.Language,
            new(QuestionKnowledgeGroup.Money)) is null, "Legacy counts leaked into money group.");

        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var numeric = new ArithmeticQuizGenerator(new BasicArithmeticEngine(), new Random(43)).Generate(mode, c.Operation, new(c.Tier, false));
            var source = new FixedRandom(0);
            var format = new AlternatingRandom();
            var provider = new BasicPracticeQuestionProvider(store, source, format);
            var first = await provider.SelectAsync(numeric, c.Tier, c.Language, profile: profile);
            var second = await provider.SelectAsync(numeric, c.Tier, c.Language, profile: profile);
            Check(first.WordProblem is null && second.WordProblem is not null && first.Mode == mode && second.Mode == mode,
                "C# lost numeric/story variety under a profile.");
            var bankProvider = new BasicPracticeQuestionProvider(store, new FixedRandom(1));
            Check((await bankProvider.SelectAsync(numeric, c.Tier, c.Language, profile: profile)).WordProblem is not null,
                "Matching bank source failed.");
        }
        var runtime = new TemplateRuntime();
        var service = new AiQuestionGenerationService(runtime, store);
        service.Start(new(ArithmeticOperation.Add, CurriculumTier.FiveStars, AppLanguage.English, 3, true, profile));
        await service.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Check(service.Snapshot.State == AiJobState.Completed && runtime.Released && service.Snapshot.Items.All(i =>
            i.State == AiItemState.Saved && i.Question!.Contract.Grade == 0 && i.Question.Contract.KnowledgeGroup == profile.Group),
            "AI worker lost profile validation/storage/release.");
        Check(service.Snapshot.Items.Select(i => i.Contract.SceneId).Distinct().Count() == 3, "Batch repeated a scene before cycling available ones.");
    }
    private sealed class FixedRandom(int value) : Random { public override int Next(int maxValue) => value; }
    private sealed class AlternatingRandom : Random { private int _next; public override int Next(int maxValue) => _next++ % maxValue; }
    private sealed class TemplateRuntime : IQuestionTextRuntime
    {
        public bool IsLoaded => true;
        public string ModelName => "fake-profile-runtime";
        public bool Released { get; private set; }
        public Task ReleaseAsync() { Released = true; return Task.CompletedTask; }
        public Task<string> GenerateAsync(BasicQuestionContract contract, string prompt, CancellationToken token,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
        {
            string raw = QuestionBankStore.SerializeDraft(AppliedQuestionCatalogue.Draft(contract, 1));
            foreach (char ch in raw) onText?.Invoke(ch.ToString());
            return Task.FromResult(raw);
        }
    }
}
