using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using System.Text.Json;

internal static class AdditionTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    private static BasicDraftValidation Validate(BasicQuestionContract c, BasicQuestionDraft d)
        => BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d), c);
    private static BasicQuestionContract Create(string scene, BasicQuestionStructure relation, CurriculumTier tier = CurriculumTier.FiveStars,
        AppLanguage language = AppLanguage.Vietnamese) => AdditionQuestionCatalogue.Create(tier, language, new Random(457), scene, relation);
    public static async Task RunModelAsync(string path, bool englishOnly = false, bool stockOnly = false)
    {
        var runtime = new GgufQuestionRuntime();
        await runtime.LoadAsync(path);
        try
        {
            var cases = new[] {
                Create("family-gifts", BasicQuestionStructure.Combine, CurriculumTier.OneStar),
                Create("family-gifts", BasicQuestionStructure.Combine, CurriculumTier.FiveStars),
                Create("birds-arrive", BasicQuestionStructure.Increase, CurriculumTier.OneStar),
                Create("craft", BasicQuestionStructure.Combine, CurriculumTier.TwoStars),
                Create("donations", BasicQuestionStructure.AddComparisonMore, CurriculumTier.ThreeStars),
                Create("shop-stock", BasicQuestionStructure.RecoverInitial, CurriculumTier.FourStars),
                Create("garden", BasicQuestionStructure.Combine, CurriculumTier.FiveStars),
                Create("recycling", BasicQuestionStructure.AddComparisonInverse, CurriculumTier.FiveStars, AppLanguage.English)
            };
            foreach (var c in cases.Where(c => (!englishOnly || c.Language == AppLanguage.English)
                && (!stockOnly || c.SceneId == "family-gifts")))
            {
                string? correction = null;
                bool passed = false;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                    var streamed = new System.Text.StringBuilder();
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
                    }
                    passed = true; break;
                }
                Check(passed, "Real model failed addition context: " + c.SceneId);
            }
        }
        finally { await runtime.EjectAsync(); }
        Console.WriteLine(stockOnly ? "PASS real GGUF independent family holdings at 1 and 5 stars, streamed JSON and fresh C# grading. No benchmark or app database writes."
            : englishOnly ? "PASS real GGUF English inverse addition, streamed JSON and fresh C# grading. No benchmark or app database writes."
            : "PASS real GGUF addition: 1–5 stars, five relations, different scene roles, streamed JSON and fresh C# grading. No benchmark or app database writes.");
    }
    public static async Task RunAsync(string directory)
    {
        int cases = 0;
        var math = new ArithmeticQuizValidator(new BasicArithmeticEngine());
        var essays = new EssayAnswerValidator(new BasicArithmeticEngine());
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var pair in AdditionQuestionCatalogue.Available(tier))
        foreach (var id in pair.Scene.UnitIds)
        {
            var c = BasicQuestionTemplates.ApplyUnit(Create(pair.Scene.Id, pair.Structure, tier, language), QuestionUnits.Find(id)!);
            var d = AdditionQuestionCatalogue.Example(c);
            var v = Validate(c, d);
            Check(v.IsValid && v.Contract == c, $"Addition example {language}/{tier}/{pair.Scene.Id}/{pair.Structure}/{id}: {v.ErrorCode}");
            string prompt = BasicQuestionPrompt.Build(c), grammar = GgufQuestionRuntime.BuildGrammar(c);
            Check(!prompt.Contains("\"Left\"") && !prompt.Contains("\"Right\"") && !prompt.Contains("\"Subject\"")
                && prompt.Contains(c.TopicId) && prompt.Contains(c.SceneId), "Addition prompt leaked facts or lost scene roles.");
            Check(grammar.Contains("\"" + id + "\"") && !grammar.Contains("ACTOR_") && !grammar.Contains("{group}"), "Addition grammar lost compatible units or roles.");
            for (int i = 0; i < 4; i++)
            {
                var fresh = c.FreshFacts(new Random(83 + i));
                var word = d.ToWordProblem(fresh);
                Check(fresh.IsValid && fresh.SceneId == c.SceneId && fresh.TopicId == c.TopicId && fresh.Structure == c.Structure
                    && fresh.Unit == c.Unit && fresh.PartA == c.PartA && fresh.PartB == c.PartB
                    && !word.ProblemText.Contains('{') && fresh.Left <= pair.Scene.MaxOperand && fresh.Right <= pair.Scene.MaxOperand,
                    "Fresh addition facts lost roles/domain/slots.");
                foreach (var mode in Enum.GetValues<ArithmeticQuizMode>()) Check(math.Validate(fresh.ToPracticeQuestion(word, mode)).IsValid, "Addition C# result/mode invalid.");
                if (i == 0)
                {
                    string equation = $"{fresh.Left} + {fresh.Right} = {fresh.Answer} {word.AnswerUnit}";
                    Check(essays.Validate(fresh.ToPracticeQuestion(word, ArithmeticQuizMode.Essay), word.SolutionLead,
                        equation, $"{fresh.Answer} {word.AnswerUnit}").IsCorrect, "Addition prose broke essay grading: " + word.ProblemText);
                }
            }
            Check(!Validate(c, d with { GivenA = d.GivenA.Replace("{a}", "9") }).IsValid
                && !Validate(c, d with { GivenB = d.GivenB.Replace("{b}", "{a}") }).IsValid
                && !Validate(c, d with { Question = d.Question + " {a}" }).IsValid
                && !Validate(c, d with { UnitId = "money" }).IsValid, "Changed numeric/slot/unit roles accepted.");
            Check(!(c with { TopicId = "wrong" }).IsValid && !(c with { Left = pair.Scene.MaxOperand + 1 }).IsValid,
                "Forged scene/domain contract accepted.");
            cases++;
        }
        Console.WriteLine($"PASS {cases} bilingual addition scene/relation/unit/star combinations, fresh values and existing grading");
        NaturalAndInvalidProse();
        PromptFieldRegression();
        CheckJoinedClauses();
        CheckRotation();
        await PersistenceAsync(directory);
    }

    private static void PromptFieldRegression()
    {
        var c = Create("family-gifts", BasicQuestionStructure.Combine, CurriculumTier.OneStar);
        var d = AdditionQuestionCatalogue.Example(c);
        Check(Validate(c, d with {
            GivenA = "{name} giữ {a} {unit} để học tập.",
            GivenB = "{other} có {b} {unit} riêng.",
            Question = "Hỏi {name} và {other} có tất cả bao nhiêu {unit}?"
        }).IsValid, "Short independent facts and one target question were rejected.");
        Check(!Validate(c, d with { GivenA = d.GivenA + " Vì vậy, {name} có {unit} để học tập." }).IsValid,
            "Screenshot-style repeated explanations in given_a were accepted.");
        Check(!Validate(c, d with { GivenB = d.GivenB + " Lan cũng có {unit} riêng." }).IsValid,
            "A second sentence/extra actor in given_b was accepted.");
        Check(!Validate(c, d with { Question = d.Question + " Tổng là {unit} + {unit} = {unit}." }).IsValid,
            "A worked answer appended to the question was accepted.");
        Check(!Validate(c, d with { GivenA = d.GivenA.Replace("{unit}", "{unit} " + c.Unit) }).IsValid,
            "Duplicate noun after the complete unit slot was accepted.");
        Check(!Validate(c, d with { GivenA = "{name} có {a} {unit} và {unit} riêng." }).IsValid,
            "Repeated units silently introduced a third amount.");
        Check(!Validate(c, d with { Question = "Hỏi {name} và {other} có tổng cộng bao nhiêu {unit}, vì {name} có sẵn?" }).IsValid,
            "Repeated actor/explanation within the question was accepted.");
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var contract = Create("family-gifts", BasicQuestionStructure.Combine, CurriculumTier.OneStar, language);
            string prompt = BasicQuestionPrompt.Build(contract, "ChangedQuantities");
            Check(prompt.Contains(language == AppLanguage.Vietnamese ? "mệnh đề" : "one clause")
                && !prompt.Contains("Scene:") && !prompt.Contains("gấp, thu hoạch, quyên góp"),
                "Field instructions were lost or unrelated scene activities leaked into the prompt.");
            Check(GgufQuestionRuntime.BuildGrammar(contract).All(ch => ch < 128), "Native addition grammar contains non-ASCII interop text.");
        }
        Console.WriteLine("PASS prompt field regression: single facts, one target question, no repeated explanation/actor/unit or worked answer");
    }

    private static void CheckJoinedClauses()
    {
        var c = Create("school-supplies", BasicQuestionStructure.Combine, CurriculumTier.OneStar) with {
            Subject = "nhóm của Cường", OtherSubject = "nhóm của Hải", Left = 1, Right = 4 };
        var d = AdditionQuestionCatalogue.Example(c) with { GivenA = "{name} góp được {a} {unit}." };
        string rendered = d.ToWordProblem(c).ProblemText;
        Check(rendered.Contains(", nhóm của Hải góp được") && !rendered.Contains(". Nhóm của Hải"),
            "Saved sentence-style data did not render as comma-joined clauses.");
        Check(StreamingQuestionPreview.Render(QuestionBankStore.SerializeDraft(d), c) == rendered,
            "Streamed and completed question punctuation/capitalization differ.");
        Check(d.GivenA.EndsWith('.') && d.ProblemText.Contains(". {other}"),
            "Rendering changed raw historical JSON/hash input.");
        var stock = Create("family-gifts", BasicQuestionStructure.Combine, CurriculumTier.OneStar) with {
            Subject = "Cường", OtherSubject = "Hải" };
        var example = AdditionQuestionCatalogue.Example(stock);
        Check(example.GivenA.EndsWith(',') && example.ToWordProblem(stock).ProblemText.Contains(", Hải có"),
            "A proper name at the start of the continuing clause was lowercased.");
        Check(example.ToWordProblem(stock with { OtherSubject = "cô Lan" }).ProblemText.Contains(", cô Lan có"),
            "A Vietnamese family role was capitalized, or its proper name was lowercased.");
        Check(BasicQuestionTemplates.RenderProblem("{name} có {a} {unit}.", "Sau đó, {name} nhận thêm {b} {unit}.", example.Question, stock)
            .Contains(", sau đó, Cường nhận thêm"), "Ordinary clause opening remained uppercase.");
        Console.WriteLine("PASS joined clauses in saved/live previews, lowercase role/opening, proper names and unchanged raw hash input");
    }

    private static void NaturalAndInvalidProse()
    {
        var craft = Create("craft", BasicQuestionStructure.Combine);
        var d = AdditionQuestionCatalogue.Example(craft) with {
            GivenA = "Trong {part_a}, {name} gấp được {a} {unit} để trang trí.",
            GivenB = "Đến {part_b}, {name} hoàn thành thêm {b} {unit}.",
            Question = "Qua các buổi làm thủ công, {name} làm được tất cả bao nhiêu {unit}?",
            SolutionLead = "Tổng số {unit} làm được qua các buổi là:" };
        Check(Validate(craft, d).IsValid, "Natural folding/activity prose rejected.");
        var legacyTemplate = BasicQuestionContract.CreateTemplate(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese);
        Check(!(legacyTemplate with { TopicId = "nature", SceneId = "birds-arrive" }).IsValid,
            "A legacy contract impersonated a typed addition setting.");
        string badUnicode = "{\"given_a\":\"\\uD800\",\"given_b\":\"valid prose\",\"question\":\"valid prose\",\"solution_lead\":\"valid prose\",\"unit_id\":\"cards\"}";
        Check(!BasicQuestionValidator.Validate(badUnicode, craft).IsValid, "Malformed Unicode output was accepted.");
        Check(Validate(craft, d with { Question = "Tính tổng số {unit} mà {name} gấp được qua cả hai buổi." }).IsValid,
            "Real-model natural imperative 'Tính tổng' was rejected as wrong language.");
        Check(!Validate(craft, d with { GivenB = "Vào {part_b}, {name} nhận thêm {b} {unit}." }).IsValid,
            "Possession increase silently replaced a production result.");
        Check(!Validate(craft, d with { GivenB = d.GivenB.Replace("{part_b}", "{part_a}") }).IsValid
            && !Validate(craft, d with { Question = "Hỏi vào {part_a}, {name} làm được tất cả bao nhiêu {unit}?" }).IsValid
            && !Validate(craft, d with { GivenB = d.GivenB.Replace("{name}", "{other}") }).IsValid,
            "Swapped period/actor or partial-period target accepted.");
        var more = Create("school-supplies", BasicQuestionStructure.AddComparisonMore);
        var m = AdditionQuestionCatalogue.Example(more);
        Check(Validate(more, m with { GivenB = "{name} góp được hơn {other} là {b} {unit}." }).IsValid,
            "Natural additive 'hơn' phrasing rejected.");
        Check(!Validate(more, m with { Question = "Hỏi {name} góp được hơn bao nhiêu {unit}?" }).IsValid,
            "A bare 'hơn' difference target was mistaken for the larger amount.");
        Check(!Validate(more, m with { GivenB = "Số {unit} {other} góp được nhiều hơn {name} là {b} {unit}." }).IsValid,
            "Reversed larger actor accepted.");
        Check(!Validate(more, m with { GivenB = "{name} góp được ít hơn {other} là {b} {unit}." }).IsValid,
            "The word 'hơn' inside 'ít hơn' reversed the comparison.");
        var inverse = Create("school-supplies", BasicQuestionStructure.AddComparisonInverse);
        var inv = AdditionQuestionCatalogue.Example(inverse);
        Check(Validate(inverse, inv with { GivenB = "Số {unit} {other} góp được kém {name} là {b} {unit}." }).IsValid,
            "Natural inverse 'kém' phrasing rejected.");
        var englishInverse = Create("recycling", BasicQuestionStructure.AddComparisonInverse, CurriculumTier.FiveStars, AppLanguage.English);
        var englishDraft = AdditionQuestionCatalogue.Example(englishInverse);
        Check(Validate(englishInverse, englishDraft with { Question = "How many {unit} does {name} collect in total?",
            SolutionLead = "The total number of {unit} collected by {name} is:" }).IsValid,
            "A natural total for the ONE requested actor was mistaken for a combined-actor question.");
        Check(!Validate(englishInverse, englishDraft with { GivenB = "{other} has {b} fewer {unit} than {name}." }).IsValid,
            "Real-model comparison silently changed collected amounts into possessions.");
        Check(!Validate(more, m with { GivenB = "{name} có nhiều hơn {other} là {b} {unit}." }).IsValid,
            "Vietnamese comparison silently changed contributions into possessions.");
        Check(!Validate(englishInverse, englishDraft with { GivenA = "The team collects {a} {unit} of {other}." }).IsValid
            && !Validate(englishInverse, englishDraft with { GivenB = "The team collects {b} fewer {unit} than {name} when collecting {other}." }).IsValid,
            "Actual model output changed the actor into a supplier or collected object.");
        Check(!Validate(inverse, inv with { GivenB = "{name} góp được ít hơn {other} là {b} {unit}." }).IsValid
            && !Validate(inverse, inv with { Question = "Hỏi {other} góp được bao nhiêu {unit}?" }).IsValid,
            "Reversed inverse comparison or target accepted.");
        var arrivals = Create("birds-arrive", BasicQuestionStructure.Increase);
        var bird = AdditionQuestionCatalogue.Example(arrivals) with { GivenB = "Sau đó, {b} {unit} khác bay tới {name}." };
        Check(Validate(arrivals, bird).IsValid, "Quantity-first arrival prose rejected.");
        var englishBird = Create("birds-arrive", BasicQuestionStructure.Increase, CurriculumTier.OneStar, AppLanguage.English) with { Left = 1, Right = 1 };
        string birdText = AdditionQuestionCatalogue.Example(englishBird).ToWordProblem(englishBird).ProblemText;
        Check(birdText.Contains("There is 1 bird") && birdText.Contains("1 bird flies"), "Singular arrival units/verbs were not rendered correctly.");
        var englishMore = Create("library", BasicQuestionStructure.AddComparisonMore, CurriculumTier.TwoStars, AppLanguage.English) with { Right = 1 };
        string moreText = AdditionQuestionCatalogue.Example(englishMore).ToWordProblem(englishMore).ProblemText;
        var moreUnit = QuestionUnits.Find(englishMore)!;
        Check(moreText.Contains("1 more " + moreUnit.Singular) && !moreText.Contains("1 more " + moreUnit.Plural),
            "Singular comparison gap unit was not rendered correctly.");
        Check(!Validate(arrivals, bird with { GivenB = "Có {b} {unit} khác bay khỏi {name}." }).IsValid
            && !Validate(arrivals, bird with { UnitId = "pencils" }).IsValid, "Departure or incompatible theme unit accepted.");
        Check(!Validate(arrivals, bird with { GivenA = "Trong khu rừng có {a} {unit} đang đậu trên cành cây cao, {name}." }).IsValid
            && !Validate(arrivals, bird with { GivenB = "Sau đó, có thêm {b} {unit} nữa bay đến đậu cùng {name}." }).IsValid,
            "Real-model dangling location or treating the park as a bird was accepted.");
        var garden = Create("garden", BasicQuestionStructure.Combine);
        var plant = AdditionQuestionCatalogue.Example(garden) with {
            GivenA = "Ở {part_a} trong {name} trồng được {a} {unit}.",
            GivenB = "Ở {part_b} trong {name} trồng được {b} {unit}." };
        Check(Validate(garden, plant).IsValid, "Natural planting prose rejected.");
        Check(!Validate(garden, plant with { GivenA = "{part_a} trong luống hoa có {name} {a} {unit}." }).IsValid,
            "Real-model output placed the garden in the counted quantity instead of locating the row.");
        Check(!Validate(craft, d with { GivenA = "Trong {part_a}, làm được {name} {a} {unit}." }).IsValid,
            "The activity result treated the producing actor as a counted item.");
        Check(!Validate(garden, plant with { GivenB = "Ở {part_b} trong {name} trồng được {b} {unit} và ba {unit}." }).IsValid,
            "Third quantity accepted.");
        var recover = Create("library", BasicQuestionStructure.RecoverInitial);
        var r = AdditionQuestionCatalogue.Example(recover);
        Check(!Validate(recover, r with { GivenB = "Trước đó, {name} được tặng {b} {unit}." }).IsValid
            && !Validate(recover, r with { Question = "Hỏi {name} còn lại bao nhiêu {unit}?" }).IsValid,
            "Wrong inverse event/target accepted.");
        Console.WriteLine("PASS natural folding/planting/arrival roles, reversed comparisons, wrong period/target/event/unit and added-fact rejection");
    }

    private static void CheckRotation()
    {
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        {
            var cycle = new AdditionQuestionCycle(new Random(237));
            var values = Enumerable.Range(0, 60).Select(_ => cycle.Next(tier, AppLanguage.Vietnamese)).ToArray();
            var availableRelations = AdditionQuestionCatalogue.Available(tier).Select(p => p.Structure).Distinct().Count();
            Check(values.Take(availableRelations).Select(c => c.Structure).Distinct().Count() == availableRelations,
                "A large scene family dominated relationship choice.");
            var counts = values.GroupBy(c => c.Structure).Select(g => g.Count()).ToArray();
            Check(counts.Max() - counts.Min() <= 1 && values.Select(c => c.SceneId).Distinct().Count() >= 10,
                "Addition generator failed relation/scene diversity.");
            Check(values.Zip(values.Skip(1), (a,b) => a.SceneId == b.SceneId).Count(b => b) <= 3,
                "Addition batch repeatedly chose the same setting.");
        }
        // Small physical domains reroll instead of repeatedly becoming the domain maximum at high stars.
        var garden = Create("garden", BasicQuestionStructure.Combine);
        var valuesSet = Enumerable.Range(0, 50).Select(i => garden.FreshFacts(new Random(i)).Expression).Distinct().Count();
        Check(valuesSet > 30, "Bounded high-star scenes froze their quantities.");
        Console.WriteLine("PASS 1–5-star relationship/topic/scene rotation and bounded fresh quantity diversity");
    }

    private static async Task PersistenceAsync(string directory)
    {
        string path = Path.Combine(directory, "addition-v3.db3");
        var store = new QuestionBankStore(path);
        var entries = new List<ValidatedBankQuestion>();
        foreach (var pair in AdditionQuestionCatalogue.Available(CurriculumTier.FiveStars))
        {
            var c = Create(pair.Scene.Id, pair.Structure);
            var d = AdditionQuestionCatalogue.Example(c);
            var q = new ValidatedBankQuestion(c, d, QuestionBankStore.SerializeDraft(d), "addition-test", DateTime.UtcNow);
            Check(await store.InsertAsync(q), "Addition insert failed."); entries.Add(q);
            Check(!await store.InsertAsync(q with { Contract = c.FreshFacts(new Random(624)) }), "Preview values duplicated the addition template.");
        }
        // Unequal row counts must not drown out small relation/topic families.
        var repeat = entries.First(q => q.Contract.SceneId == "family-gifts" && q.Contract.Structure == BasicQuestionStructure.Increase);
        for (int i = 0; i < 40; i++) Check(await store.InsertAsync(repeat with { Draft = repeat.Draft with {
            GivenA = new string(' ', i + 1) + repeat.Draft.GivenA } }), "Test skewed bank insert failed.");
        var picked = new List<BasicQuestionContract>();
        for (int i = 0; i < 50; i++)
        {
            var question = await store.TakeAsync(ArithmeticOperation.Add, CurriculumTier.FiveStars, AppLanguage.Vietnamese);
            Check(question is not null && question.Contract.IsValid, "Addition SQLite selection returned no valid row.");
            picked.Add(question!.Contract);
        }
        Check(picked.Take(5).Select(c => c.Structure).Distinct().Count() == 5
            && picked.Select(c => c.SceneId).Distinct().Count() >= 10,
            "Skewed row counts defeated bank relation/scene rotation: " + string.Join(",", picked.Select(c => c.Structure + "/" + c.SceneId)));
        var relationCounts = picked.GroupBy(c => c.Structure).Select(g => g.Count()).ToArray();
        Check(relationCounts.Max() - relationCounts.Min() <= 1, "Bank relationships were not balanced.");
        var restarted = new QuestionBankStore(path);
        var next = await restarted.TakeAsync(ArithmeticOperation.Add, CurriculumTier.FiveStars, AppLanguage.Vietnamese);
        Check(next is not null && next.Contract.Structure != picked[^1].Structure, "Persistent last-use history was ignored after restart.");
        var provider = new BasicPracticeQuestionProvider(store, new AlwaysBank());
        var generator = new ArithmeticQuizGenerator(new BasicArithmeticEngine(), new Random(516));
        var freshValues = new HashSet<IntegerArithmeticExpression>();
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        for (int i = 0; i < 10; i++)
        {
            var original = generator.Generate(mode, ArithmeticOperation.Add, new(CurriculumTier.FiveStars, false));
            var practice = await provider.SelectAsync(original, CurriculumTier.FiveStars, AppLanguage.Vietnamese);
            Check(practice.WordProblem is not null && !practice.WordProblem.ProblemText.Contains('{')
                && new ArithmeticQuizValidator(new BasicArithmeticEngine()).Validate(practice).IsValid,
                "Production provider failed to instantiate an addition scene for practice.");
            freshValues.Add(practice.Expression);
        }
        Check(freshValues.Count > 15, "Addition bank provider reused preview operands.");
        using var workbook = new MemoryStream();
        QuestionBankWorkbook.Write(workbook, entries);
        workbook.Position = 0;
        var rows = QuestionBankWorkbook.Read(workbook);
        Check(rows.Count == entries.Count && rows.Select(r => r.Question?.Contract).SequenceEqual(entries.Select(q => q.Contract)),
            "Excel lost addition scene/topic/period metadata.");
        workbook.Position = 0;
        var imported = new QuestionBankStore(Path.Combine(directory, "addition-import.db3"));
        var report = await imported.ImportExcelAsync(workbook);
        Check(report.Inserted == entries.Count && report.Rejected == 0, "Addition Excel import failed.");
        var tampered = await imported.QueryAsync("UPDATE BasicQuestionBank SET TopicId='wrong' WHERE SceneId='craft'");
        Check(tampered.IsSuccess, "Metadata edit failed.");
        using var export = new MemoryStream();
        var exported = await imported.ExportExcelAsync(export);
        Check(exported.Skipped == 1 && exported.Exported == entries.Count - 1, "Forged indexed metadata reached export.");
        await imported.QueryAsync("UPDATE BasicQuestionBank SET DraftJson='{}'");
        Check(await imported.TakeAsync(ArithmeticOperation.Add, CurriculumTier.FiveStars, AppLanguage.Vietnamese) is null,
            "Corrupt addition prose reached practice.");
        await MigrationAsync(directory, repeat);
        Console.WriteLine("PASS addition SQLite semantic rotation despite skewed row counts, dedup, restart, Excel, forged metadata and old schema migration");
    }

    private static async Task MigrationAsync(string directory, ValidatedBankQuestion addition)
    {
        string path = Path.Combine(directory, "pre-addition-schema.db3");
        var legacy = BasicQuestionContract.CreateTemplate(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese);
        var draft = BasicQuestionTemplates.Example(legacy);
        using (var db = new SQLite.SQLiteConnection(path))
        {
            db.Execute("CREATE TABLE BasicQuestionBank(Hash TEXT PRIMARY KEY,Operation INTEGER,Stars INTEGER,Language INTEGER,Version INTEGER,ContractJson TEXT,DraftJson TEXT,RawJson TEXT,ModelName TEXT,CreatedUtc BIGINT,LastUsedUtc BIGINT,UseCount BIGINT)");
            db.Execute("INSERT INTO BasicQuestionBank VALUES(?,?,?,?,?,?,?,?,?,?,?,?)", "old-v2", 0, 1, 0, 2,
                JsonSerializer.Serialize(legacy), QuestionBankStore.SerializeDraft(draft), "old raw", "old", DateTime.UtcNow, DateTime.MinValue, 0);
        }
        var migrated = new QuestionBankStore(path);
        Check((await migrated.TakeAsync(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese))?.Contract == legacy,
            "Old schema/template could not be read after migration.");
        Check(await migrated.InsertAsync(addition), "New addition template could not be stored in migrated schema.");
        Check((await migrated.QueryAsync("SELECT TopicId,SceneId,Structure FROM BasicQuestionBank WHERE Version=3")).Rows.Count == 1,
            "Migration did not add scene indexes/columns.");
    }
    private sealed class AlwaysBank : Random { public override int Next(int maxValue) => 1; }
}
