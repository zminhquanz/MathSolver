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
        }
        finally { await runtime.EjectAsync(); }
        Console.WriteLine(stockOnly ? "PASS real GGUF stock addition: personal holdings, restored wholesale stock and distribution stock, streamed JSON and fresh C# grading. No benchmark or app database writes."
            : englishOnly ? "PASS real GGUF English addition at 1–5 stars, streamed JSON and fresh C# grading. No benchmark or app database writes."
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
        foreach (var id in pair.Scene.Scale(tier)!.UnitIds)
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
                    && !word.ProblemText.Contains('{') && fresh.Left >= QuizCurriculumLayer.GetMinimumPrimaryOperandValue(tier)
                    && fresh.Left <= pair.Scene.Scale(tier)!.MaxOperand && fresh.Right <= pair.Scene.Scale(tier)!.MaxOperand,
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
        CheckScalePolicies();
        CheckContextCoverageAndPromptBudget();
        CheckThreadBudget();
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
        var en = Create("school-supplies", BasicQuestionStructure.Combine, CurriculumTier.FourStars, AppLanguage.English);
        var enDraft = AdditionQuestionCatalogue.Example(en);
        foreach (string negation in new[] { "don't", "DON'T", "don’t", "doesn't", "doesn’t", "didn't", "can't", "won't", "cannot", "never" })
        foreach (bool first in new[] { true, false })
        {
            string fact = (first ? "{name}" : "{other}") + " " + negation + " bring " + (first ? "{a}" : "{b}") + " {unit}.";
            var invalid = first ? enDraft with { GivenA = fact } : enDraft with { GivenB = fact };
            Check(Validate(en, invalid).ErrorCode == "ExtraRelations", "Live-model negation passed: " + fact);
        }
        Check(Validate(en, enDraft with { GivenA = "{name} bring {a} {unit},", GivenB = "{other} bring {b} {unit}." }).IsValid,
            "Affirmative natural action was rejected by negation check.");
        var survey = Create("survey-responses", BasicQuestionStructure.AddComparisonMore, CurriculumTier.ThreeStars, AppLanguage.English);
        var surveyDraft = AdditionQuestionCatalogue.Example(survey);
        Check(Validate(survey, surveyDraft with {
            GivenA = "{other} gathers {a} {unit},", Question = "How many {unit} does {name} gather?"
        }).IsValid, "A natural survey response collection synonym was rejected.");
        foreach (string sceneId in new[] { "trial-results", "vehicle-count" })
        foreach (var (past, basic) in new[] { ("logged", "log"), ("observed", "observe"), ("tallied", "tally") })
        {
            var observed = Create(sceneId, BasicQuestionStructure.Combine, CurriculumTier.FiveStars, AppLanguage.English);
            var recorded = AdditionQuestionCatalogue.Example(observed);
            Check(Validate(observed, recorded with {
                GivenA = "During {part_a}, {name} " + past + " {a} {unit},",
                Question = "How many {unit} did {name} " + basic + " across these months?"
            }).IsValid, "A natural observation count synonym was rejected: " + sceneId);
        }
        var material = Create("construction-stock", BasicQuestionStructure.AddComparisonInverse);
        var stock = AdditionQuestionCatalogue.Example(material);
        Check(Validate(material, stock with { GivenA = "{other} sở hữu {a} {unit},",
            GivenB = "{other} sở hữu ít hơn {name} là {b} {unit}." }).IsValid,
            "Natural material-stock ownership was rejected.");
        var crop = Create("crop-harvest", BasicQuestionStructure.Combine, CurriculumTier.ThreeStars);
        var rice = BasicQuestionTemplates.ApplyUnit(crop, QuestionUnits.Find("rice-sacks")!);
        var reaped = AdditionQuestionCatalogue.Example(rice) with {
            GivenA = "Vào {part_a}, {name} gặt được {a} {unit},",
            GivenB = "vào {part_b}, {name} gặt được {b} {unit}."
        };
        Check(Validate(rice, reaped).IsValid
            && !Validate(BasicQuestionTemplates.ApplyUnit(crop, QuestionUnits.Find("mangoes")!), reaped with { UnitId = "mangoes" }).IsValid,
            "Reaping must be accepted for rice and rejected for mangoes.");
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
        var englishStock = Create("shop-stock", BasicQuestionStructure.RecoverInitial, CurriculumTier.FourStars, AppLanguage.English);
        var spaced = AdditionQuestionCatalogue.Example(englishStock) with {
            GivenA = "{name} has {a} {unit} remaining, ,",
            GivenB = "previously, {name} gave away {b} {unit} ." };
        string tidy = spaced.ToWordProblem(englishStock).ProblemText;
        Check(!tidy.Contains(",,") && !tidy.Contains(" .") && tidy.Contains("remaining, previously,"),
            "Actual GGUF repeated commas/spaced punctuation survived display formatting.");
        Check(StreamingQuestionPreview.Render(QuestionBankStore.SerializeDraft(spaced), englishStock) == tidy
            && spaced.GivenA.EndsWith(", ,") && spaced.GivenB.EndsWith(" ."),
            "Live punctuation diverged from saved display or modified historical JSON.");
        Console.WriteLine("PASS joined clauses in saved/live previews, lowercase role/opening, proper names and unchanged raw hash input");
    }

    private static void NaturalAndInvalidProse()
    {
        var increase = Create("family-gifts", BasicQuestionStructure.Increase, CurriculumTier.OneStar);
        var increased = AdditionQuestionCatalogue.Example(increase);
        Check(Validate(increase, increased with { GivenB = "{name} thêm {b} {unit}." }).IsValid
            && Validate(increase, increased with { GivenB = "{name} được thêm {b} {unit} vào." }).IsValid,
            "Real GGUF natural stock-increase phrasing was rejected.");
        Check(!Validate(increase, increased with { GivenB = "{name} cần thêm {b} {unit}." }).IsValid,
            "Required stock was confused with an actual increase.");
        var craft = Create("craft", BasicQuestionStructure.Combine, CurriculumTier.TwoStars);
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
        var englishCraft = Create("craft", BasicQuestionStructure.Combine, CurriculumTier.TwoStars, AppLanguage.English);
        var ec = AdditionQuestionCatalogue.Example(englishCraft) with {
            GivenA = "In {part_a}, {name} makes {a} {unit},",
            GivenB = "in {part_b}, {name} makes {b} {unit}.",
            Question = "How many {unit} does {name} make over these periods?",
            SolutionLead = "The number of {unit} made over these periods is:" };
        Check(Validate(englishCraft, ec).IsValid, "A combined-period question without the literal word 'total' was rejected.");
        Check(!Validate(englishCraft, ec with { GivenA = ec.GivenA.Replace("{name}", "{name}'s craft group") }).IsValid,
            "Actual GGUF output appended a duplicated craft-group role to the complete actor.");
        Check(!Validate(englishCraft, ec with { Question = "How many {unit} does {name} make in {part_a}?" }).IsValid,
            "Partial-period target was accepted after widening natural aggregate phrasing.");
        var englishStock = Create("shop-stock", BasicQuestionStructure.RecoverInitial, CurriculumTier.FourStars, AppLanguage.English);
        var es = AdditionQuestionCatalogue.Example(englishStock) with { Question = "How many {unit} did {name} have at the start?" };
        Check(Validate(englishStock, es).IsValid, "Actual GGUF 'at the start' original-stock target was rejected.");
        Check(Validate(englishStock, es with { GivenB = "previously, {name} removed {b} {unit}.",
            Question = "How many {unit} did {name} start with?" }).IsValid,
            "Actual GGUF 'start with' original-stock target was rejected.");
        Check(!Validate(englishStock, es with { GivenB = "previously, {name} took {b} {unit}." }).IsValid,
            "Ambiguous 'took' was accepted without indicating removal from the known stock.");
        Check(!Validate(englishStock, es with {
            GivenA = "{name} has {a} {unit} left, previously, the shop has fewer than that,",
            GivenB = "previously, {name} removed {b} {unit}." }).IsValid,
            "Actual GGUF extra comparison was accepted in a remaining-stock fact.");
        Check(!Validate(englishStock, es with { GivenA = es.GivenA.Replace("{name}", "{name}'s wholesale shop") }).IsValid,
            "Actual GGUF wholesale-shop role was appended to the complete actor.");
        Check(Validate(englishStock, es with { GivenB = "previously, {name} removed {b} {unit}." }).IsValid
            && Validate(englishStock, es with { GivenB = "{name} had {b} {unit} removed from the stock.",
                Question = "How many {unit} did {name} have in the beginning?" }).IsValid,
            "Actual GGUF past-tense/passive removed-stock paraphrases were rejected.");
        Check(!Validate(englishStock, es with { GivenA = "{name} has {a} {unit} left, while," }).IsValid,
            "Actual GGUF dangling conjunction in a given was accepted.");
        var distribution = Create("book-distribution", BasicQuestionStructure.Combine);
        var distributionDraft = AdditionQuestionCatalogue.Example(distribution);
        Check(!Validate(distribution, distributionDraft with { GivenA = "{name} có ộ {a} {unit}," }).IsValid,
            "Actual GGUF orphaned letter before a stock quantity was accepted.");
        Check(Validate(distribution, distributionDraft with { GivenA = "{name} hiện đang giữ {a} {unit}," }).IsValid,
            "A valid stock adverb/verb was rejected alongside the orphaned-letter guard.");
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
        Check(!Validate(inverse, inv with {
            GivenA = "{other} góp được ٤٥٠٠٠ {a} {unit},",
            GivenB = "{other} góp được ít hơn {name} là ٨٠٠٠ {b} {unit}."
        }).IsValid, "Actual GGUF Unicode digits bypassed the C#-only numeric facts.");
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
        var arrivals = Create("birds-arrive", BasicQuestionStructure.Increase, CurriculumTier.OneStar);
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

    private static void CheckThreadBudget()
    {
        foreach (var (cpus, threads) in new[] { (1, 1), (2, 1), (3, 2), (4, 3), (8, 6), (12, 9), (16, 12), (32, 24) })
            Check(GgufQuestionRuntime.GetInferenceThreadCount(cpus) == threads, "Incorrect 75% CPU thread budget.");
        Check(GgufQuestionRuntime.GetInferenceThreadCount(int.MaxValue) == 1_610_612_735,
            "Thread budget arithmetic overflowed.");
        Console.WriteLine("PASS 75% logical-CPU thread budgets, minimum one worker and overflow-safe rounding");
    }

    private static void CheckScalePolicies()
    {
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        {
            int lower = QuizCurriculumLayer.GetMinimumPrimaryOperandValue(tier);
            int upper = QuizCurriculumLayer.GetMaximumOperandValue(tier);
            foreach (var pair in AdditionQuestionCatalogue.Available(tier))
            {
                var scale = pair.Scene.Scale(tier)!;
                foreach (Random random in Enumerable.Range(0, 24).Select(seed => new Random(seed))
                    .Cast<Random>().Concat([new ExtremeRandom(false), new ExtremeRandom(true)]))
                {
                    var c = AdditionQuestionCatalogue.Create(tier, language, random, pair.Scene.Id, pair.Structure);
                    var d = AdditionQuestionCatalogue.Example(c);
                    Check(c.IsValid && c.Left >= lower && c.Left <= upper && c.Right > 0 && c.Right <= upper
                        && c.Left <= scale.MaxOperand && c.Right <= scale.MaxOperand
                        && c.Answer <= scale.MaxCombinedQuantity,
                        $"Scale/capacity invalid: {language}/{tier}/{pair.Scene.Id}: {c.Left}+{c.Right}");
                    Check(c.Subject != c.OtherSubject && !c.Subject.Contains("'s shop's")
                        && scale.MatchesActor(language, c.Subject) && scale.MatchesActor(language, c.OtherSubject),
                        "Distinct actors did not retain the selected scale.");
                    Check(Validate(c, d).IsValid && !d.ToWordProblem(c).ProblemText.Contains('{'),
                        "Scaled roles no longer render/validate.");
                    Check(!(c with { Left = lower - 1 }).IsValid && !(c with { Right = upper + 1 }).IsValid,
                        "A forged tier operand was accepted.");
                    foreach (string excluded in pair.Scene.UnitIds.Except(scale.UnitIds))
                        Check(!Validate(c, d with { UnitId = excluded }).IsValid,
                            "Unit incompatible with the star-specific activity was accepted.");
                }
            }
            if ((int)tier >= 3)
            {
                Check(!AdditionQuestionCatalogue.Available(tier).Any(p => p.Scene.Id is "family-gifts" or "sports" or "birds-arrive" or "club-arrivals"),
                    "A small personal/attendance/score scene still appears at high stars.");
                var numbers = Enumerable.Range(0, 100).Select(seed => AdditionQuestionCatalogue.Create(tier, language,
                    new Random(seed), "library", BasicQuestionStructure.Combine)).ToArray();
                Check(numbers.Any(c => c.Right < lower) && numbers.Any(c => c.Answer > upper),
                    "The second operand or result was incorrectly forced into the primary digit bucket.");
                Check(numbers.Count(c => AdditionQuestionCatalogue.CountCarries(c.Left, c.Right) >= (int)tier - 2) > 45,
                    "Higher stars did not include sufficient carrying practice.");
            }
        }

        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var c = Create("notebook-production", BasicQuestionStructure.Combine, CurriculumTier.FiveStars, language);
            var d = AdditionQuestionCatalogue.Example(c);
            string prompt = BasicQuestionPrompt.Build(c);
            Check(prompt.Contains(c.PartA) && prompt.Contains(c.PartB) && prompt.Contains(language == AppLanguage.Vietnamese
                ? "nhà máy sản xuất vở" : "notebook factory"), "Prompt omitted the bound scale or time periods.");
            Check(!(c with { Subject = language == AppLanguage.Vietnamese ? "Lan" : "Mary" }).IsValid
                && !(c with { PartB = c.PartA }).IsValid,
                "An individual producing thousands of objects, or overlapping periods, was accepted.");
            Check(!Validate(c, d with { GivenB = d.GivenB.Replace("{part_b}", "{part_b}, " + (language == AppLanguage.Vietnamese
                ? "trong buổi sáng" : "in the morning")) }).IsValid,
                "Monthly factory output was silently narrowed to one morning.");
            Check(!Validate(c, d with { GivenB = d.GivenB.Replace("{b} {unit}", language == AppLanguage.Vietnamese
                ? "{b} {unit} quyển sách" : "{b} {unit} books") }).IsValid,
                "A second object noun changed the selected unit.");
            Check(!Validate(c, d with { GivenB = d.GivenB.Replace("{b} {unit}", language == AppLanguage.Vietnamese
                ? "{b} {unit} lũy kế" : "{b} {unit} cumulatively so far") }).IsValid,
                "A cumulative amount containing the previous period was added again.");

            var club = Create("club-arrivals", BasicQuestionStructure.Increase, CurriculumTier.TwoStars, language) with { Left = 20, Right = 20 };
            Check(!club.IsValid, "Individually bounded operands exceeded club capacity.");
            var smallLibrary = Create("library", BasicQuestionStructure.Increase, CurriculumTier.TwoStars, language);
            var largeLibrary = Create("library", BasicQuestionStructure.Increase, CurriculumTier.FiveStars, language);
            Check(!(largeLibrary with { Subject = smallLibrary.Subject }).IsValid,
                "An old low-scale actor could be reused with a five-digit quantity.");
        }
        foreach (string person in new[] { "Trường", "Khoa", "cô Lan" })
        {
            var c = Create("family-gifts", BasicQuestionStructure.Combine, CurriculumTier.OneStar) with { Subject = person, OtherSubject = "An" };
            Check(c.IsValid, "A valid personal name/title was mistaken for an institution: " + person);
        }
        bool rejected = false;
        try { Create("club-arrivals", BasicQuestionStructure.Increase, CurriculumTier.FiveStars); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Explicitly requesting a high-star small club downgraded the primary number.");
        Console.WriteLine("PASS all five scene scales in both languages, primary/secondary/result ranges, total capacity, actor/unit/time roles, carry progression and incompatible-setting rejection");
    }

    private sealed class ExtremeRandom(bool high) : Random
    {
        public override int Next(int maxValue) => high ? maxValue - 1 : 0;
        public override int Next(int minValue, int maxValue) => high ? maxValue - 1 : minValue;
    }

    private static void CheckContextCoverageAndPromptBudget()
    {
        string[] topics = ["school", "food", "animals", "agriculture", "trade", "traffic",
            "construction", "environment", "statistics", "probability"];
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var available = AdditionQuestionCatalogue.Available(tier).ToArray();
            Check(topics.All(topic => available.Any(p => p.Scene.TopicId == topic)),
                "A requested count context is unavailable at " + tier);
            foreach (var pair in available)
            {
                var c = Create(pair.Scene.Id, pair.Structure, tier, language);
                string prompt = BasicQuestionPrompt.Build(c);
                string grammar = GgufQuestionRuntime.BuildGrammar(c);
                var chosen = QuestionUnits.Find(c)!;
                Check(prompt.Length < 1900 && !prompt.Contains("Unit catalogue:")
                    && prompt.Contains("unit_id=\"" + chosen.Id + "\""),
                    "Prompt lost its chosen unit or exceeded the single-context budget: " + pair.Scene.Id);
                foreach (string other in pair.Scene.Scale(tier)!.UnitIds.Where(id => id != chosen.Id))
                    Check(!grammar.Contains("\"" + other + "\""),
                        "Grammar still permits generation to switch the C#-selected unit.");
            }
            var cycle = new AdditionQuestionCycle(new Random(271));
            var selected = Enumerable.Range(0, 5 * AdditionQuestionCatalogue.Scenes.Count * 3)
                .Select(_ => cycle.Next(tier, language)).ToArray();
            Check(topics.All(topic => selected.Any(c => c.TopicId == topic)),
                "Random balanced selection starved one of the requested count contexts.");
            Check(selected.Select(c => c.SceneId).Distinct().Count() == available.Select(p => p.Scene.Id).Distinct().Count(),
                "Random selection failed to reach all compatible scenes.");
        }
        Console.WriteLine("PASS ten count-context groups at all stars/languages, random full-scene coverage, compact prompts and one-unit generation grammar");
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
            int scenes = AdditionQuestionCatalogue.Available(tier).Select(p => p.Scene.Id).Distinct().Count();
            Check(counts.Max() - counts.Min() <= 1 && values.Select(c => c.SceneId).Distinct().Count() >= Math.Min(10, scenes),
                $"Addition generator failed relation/scene diversity at {tier}: " + string.Join(",", values.Select(c => c.SceneId).Distinct()));
            Check(values.Zip(values.Skip(1), (a,b) => a.SceneId == b.SceneId).Count(b => b) <= 3,
                "Addition batch repeatedly chose the same setting.");
        }
        // A high-star nursery retains its digit bucket and produces fresh values.
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
        var repeat = entries.First(q => q.Contract.SceneId == "library" && q.Contract.Structure == BasicQuestionStructure.Increase);
        for (int i = 0; i < 40; i++) Check(await store.InsertAsync(repeat with { Draft = repeat.Draft with {
            GivenA = new string(' ', i + 1) + repeat.Draft.GivenA } }), "Test skewed bank insert failed.");
        var picked = new List<BasicQuestionContract>();
        // Each relation gets an equal share; cover every scene of the largest
        // relation family after the catalogue grows, despite skewed row counts.
        int selectionCount = 5 * entries.GroupBy(q => q.Contract.Structure)
            .Max(group => group.Select(q => q.Contract.SceneId).Distinct().Count()) * 2;
        for (int i = 0; i < selectionCount; i++)
        {
            var question = await store.TakeAsync(ArithmeticOperation.Add, CurriculumTier.FiveStars, AppLanguage.Vietnamese);
            Check(question is not null && question.Contract.IsValid, "Addition SQLite selection returned no valid row.");
            picked.Add(question!.Contract);
        }
        Check(picked.Take(5).Select(c => c.Structure).Distinct().Count() == 5
            && picked.Select(c => c.SceneId).Distinct().Count() == entries.Select(q => q.Contract.SceneId).Distinct().Count(),
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
        var tampered = await imported.QueryAsync("UPDATE BasicQuestionBank SET TopicId='wrong' WHERE SceneId='notebook-production'");
        Check(tampered.IsSuccess, "Metadata edit failed.");
        using var export = new MemoryStream();
        var exported = await imported.ExportExcelAsync(export);
        Check(exported.Skipped == 1 && exported.Exported == entries.Count - 1, "Forged indexed metadata reached export.");
        await imported.QueryAsync("UPDATE BasicQuestionBank SET DraftJson='{}'");
        Check(await imported.TakeAsync(ArithmeticOperation.Add, CurriculumTier.FiveStars, AppLanguage.Vietnamese) is null,
            "Corrupt addition prose reached practice.");
        await MigrationAsync(directory, repeat);
        await OutdatedScaleRowsAsync(directory, repeat);
        Console.WriteLine("PASS addition SQLite semantic rotation despite skewed row counts, dedup, restart, Excel, forged metadata and old schema migration");
    }

    private static async Task OutdatedScaleRowsAsync(string directory, ValidatedBankQuestion valid)
    {
        string path = Path.Combine(directory, "outdated-addition-scales.db3");
        var store = new QuestionBankStore(path);
        Check(await store.InsertAsync(valid), "Could not initialise scale regression bank.");
        var oldLibrary = valid.Contract with { Subject = "tủ sách của Lan", OtherSubject = "tủ sách của An" };
        var oldClub = Create("club-arrivals", BasicQuestionStructure.Increase, CurriculumTier.OneStar) with {
            Tier = CurriculumTier.FiveStars, Left = 12, Right = 6 };
        using (var db = new SQLite.SQLiteConnection(path))
        {
            foreach (var old in new[] { oldLibrary, oldClub })
                db.Insert(new QuestionBankStore.Row {
                    Hash = "outdated-" + old.SceneId, Version = old.Version, Operation = (int)old.Operation,
                    Stars = (int)old.Tier, Language = (int)old.Language, Structure = (int)old.Structure,
                    TopicId = old.TopicId, SceneId = old.SceneId, ContractJson = JsonSerializer.Serialize(old),
                    DraftJson = QuestionBankStore.SerializeDraft(valid.Draft), CreatedUtc = DateTime.UtcNow,
                    LastUsedUtc = DateTime.MinValue });
        }
        var selected = await store.TakeAsync(ArithmeticOperation.Add, CurriculumTier.FiveStars, AppLanguage.Vietnamese);
        Check(selected?.Contract == valid.Contract, "An obsolete small setting reached high-star practice.");
        using var excel = new MemoryStream();
        var export = await store.ExportExcelAsync(excel);
        Check(export.Exported == 1 && export.Skipped == 2, "Export did not reject obsolete scale contracts.");
        var count = await store.QueryAsync("SELECT COUNT(*) FROM BasicQuestionBank");
        Check(count.IsSuccess && count.Rows.Count == 1 && count.Rows[0][0] == "3",
            "Validation deleted saved data instead of skipping obsolete templates.");
        Console.WriteLine("PASS obsolete SQLite scales skipped during practice/export without deleting saved rows");
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
