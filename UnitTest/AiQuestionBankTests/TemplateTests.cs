using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using System.Text.Json;

internal static class TemplateTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    public static async Task RunAsync(string directory)
    {
        Check(QuestionNames.VietnameseMale.Count == 100 && QuestionNames.VietnameseFemale.Count == 100
            && QuestionNames.VietnameseMale.Distinct().Count() == 100 && QuestionNames.VietnameseFemale.Distinct().Count() == 100,
            "Vietnamese name pools must contain exactly 100 distinct entries each.");
        Check(QuestionNames.VietnameseMale[0] == "Huy" && QuestionNames.VietnameseMale[^1] == "Cảnh"
            && QuestionNames.VietnameseFemale[0] == "Anh" && QuestionNames.VietnameseFemale[^1] == "Chinh",
            "Name pools no longer follow the supplied frequency lists.");
        int cases = 0;
        var maths = new ArithmeticQuizValidator(new BasicArithmeticEngine());
        var essays = new EssayAnswerValidator(new BasicArithmeticEngine());
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var structure in Enum.GetValues<BasicQuestionStructure>().Where(s => s is not
            (BasicQuestionStructure.AddComparisonMore or BasicQuestionStructure.AddComparisonInverse)))
        foreach (var tier in Enum.GetValues<CurriculumTier>().Where(t => (int)t >= BasicQuestionTemplates.MinimumStars(structure)))
        foreach (var unit in QuestionUnits.All)
        {
            var c = BasicQuestionTemplates.ApplyUnit(BasicQuestionContract.CreateTemplate(BasicQuestionTemplates.Operation(structure), tier, language,
                new Random(330 + cases)) with { Structure = structure }, unit);
            var example = BasicQuestionTemplates.Example(c);
            string json = QuestionBankStore.SerializeDraft(example);
            var validation = BasicQuestionValidator.Validate(json, c);
            Check(validation.IsValid && validation.Contract == c, $"Valid {language}/{structure}/{tier}/{unit.Id} rejected: {validation.ErrorCode}");
            string prompt = BasicQuestionPrompt.Build(c);
            Check(!prompt.Contains("\"Left\"") && !prompt.Contains("\"Right\"") && !prompt.Contains(c.Subject), "Numeric/actor preview facts leaked into the template prompt.");
            for (int seed = 0; seed < 3; seed++)
            {
                var fresh = c.FreshFacts(new Random(11 + seed));
                var word = example.ToWordProblem(fresh);
                Check(fresh.IsValid && fresh.Structure == structure && !word.ProblemText.Contains('{')
                    && QuestionNames.GivenName(fresh.Subject) != QuestionNames.GivenName(fresh.OtherSubject)
                    && word.AnswerUnit == (structure == BasicQuestionStructure.CountGroups
                        ? unit.GroupFor(fresh, fresh.Answer.IsOne) : unit.Item(language, fresh.Answer.IsOne))
                    && word.SolutionLead.Length > 0, "Template instantiation lost roles, units, or solution prose.");
                foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                    Check(maths.Validate(fresh.ToPracticeQuestion(word, mode)).IsValid, "Template-derived question failed C# math grading.");
                if (seed == 0)
                {
                    var essay = fresh.ToPracticeQuestion(word, ArithmeticQuizMode.Essay);
                    string equation = $"{fresh.Left} {BasicArithmeticEngine.GetSymbol(fresh.Operation)} {fresh.Right} = {fresh.Answer} {word.AnswerUnit}";
                    Check(essays.Validate(essay, word.SolutionLead, equation, $"{fresh.Answer} {word.AnswerUnit}").IsCorrect,
                        "Rendered prose solution or unit failed the existing essay grader.");
                    Check(!essays.Validate(essay, word.SolutionLead, equation, $"{fresh.Answer} wrong-unit").IsCorrect,
                        "The existing essay unit requirement was lost.");
                }
            }
            Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(example with { GivenA = example.GivenA.Replace("{a}", "{b}") }), c).IsValid,
                "Swapped fact slots accepted.");
            Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(example with { GivenB = example.GivenB + " 99" }), c).IsValid,
                "Literal extra facts accepted.");
            Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(example with { Question = example.Question + " {answer}" }), c).IsValid,
                "AI answer slot accepted.");
            if (structure is not (BasicQuestionStructure.Combine or BasicQuestionStructure.Difference))
                Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(example with {
                    SolutionLead = example.SolutionLead + " {other}" }), c).IsValid,
                    "A solution lead referring to another actor was accepted.");
            cases++;
        }
        Console.WriteLine($"PASS {cases} bilingual structure/star/unit templates, fresh rendering, all grading modes and invalid-slot mutations");

        var addition = BasicQuestionContract.CreateTemplate(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese, new Random(90));
        var draft = BasicQuestionTemplates.Example(addition);
        void Reject(BasicQuestionDraft changed, string why) => Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(changed), addition).IsValid, why);
        var natural = draft with { GivenA = "Trong buổi chuẩn bị quà, {name} hiện có {a} {unit}.",
            GivenB = "Sau đó, {name} vừa được tặng {b} {unit}.", Question = "Hỏi {name} có tổng cộng bao nhiêu {unit} sau khi nhận quà?" };
        Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(natural), addition).IsValid, "Natural context/passive phrasing rejected.");
        Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft with { GivenB = "{name} nhập thêm {b} {unit} vào kho." }), addition).IsValid,
            "A valid stock increase was rejected because of its verb.");
        Reject(draft with { GivenB = "{name} bán {b} {unit}." }, "Reversed addition accepted.");
        Reject(draft with { GivenB = "{other} nhận thêm {b} {unit} từ {name}." }, "Wrong receiving owner accepted.");
        Reject(draft with { Question = "Hỏi {name} còn lại bao nhiêu {unit}?" }, "Wrong target accepted.");
        Reject(draft with { SolutionLead = "Số {unit} mà {name} còn lại là:" }, "Wrong prose solution accepted.");
        Reject(draft with { SolutionLead = "Tổng số {unit} mà {other} có là:" }, "Wrong solution owner accepted.");
        Reject(draft with { UnitId = "kilometres" }, "Unbounded units accepted.");
        Reject(draft with { GivenB = draft.GivenB + " và hai {unit}." }, "Extra written facts accepted.");
        Reject(draft with { Question = draft.Question.Replace("{unit}", "quả táo") }, "Hardcoded target unit accepted.");
        Reject(draft with { GivenA = draft.GivenA.Replace("{a}", "{a") }, "Incomplete placeholder accepted.");
        Reject(draft with { SolutionLead = draft.SolutionLead + " {a} + {b}" }, "AI formula accepted.");
        Reject(draft with { GivenA = "Trong {group_one} đựng {name}, ban đầu có {a} {unit}." }, "Actor treated as a packed object accepted.");
        Reject(draft with { GivenA = "Các {name} có {a} {unit}." }, "A singular actor was silently changed into a group.");
        Reject(draft with { GivenA = "Cô giáo có {name} có {a} {unit}." }, "Forced placeholder recovery produced an actor owned as an object.");
        var sharing = BasicQuestionContract.CreateTemplate(ArithmeticOperation.Divide, CurriculumTier.OneStar, AppLanguage.Vietnamese);
        var shareDraft = BasicQuestionTemplates.Example(sharing) with { GivenB = "{name} chia đều số {unit} đó vào {b} {group}." };
        Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(shareDraft), sharing).IsValid,
            "Real-model natural division prose with a repeated unit was falsely rejected.");
        shareDraft = shareDraft with { GivenA = "{name} có {a} {unit}. {name} chuẩn bị để chia đều số {unit} này vào {group}.",
            GivenB = "{name} chia số {unit} thành {b} {group}." };
        Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(shareDraft), sharing).IsValid,
            "A stated equal-sharing relation was lost between clauses.");
        var bulk = BasicQuestionTemplates.ApplyUnit(addition with { Operation = ArithmeticOperation.Multiply,
            Tier = CurriculumTier.FiveStars, Structure = BasicQuestionStructure.EqualGroups, Left = 20000, Right = 2 }, QuestionUnits.Find("books")!);
        Check(bulk.IsValid && bulk.GroupUnit == "kho", "A bulk operand was still assigned to a small container.");
        var apples = BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft with { UnitId = "apples" }), addition);
        Check(apples.IsValid && apples.Contract!.Unit == "quả táo" && apples.Contract.GroupUnit == "giỏ", "AI unit selection did not resolve to a compatible C# unit family.");
        string serialized = QuestionBankStore.SerializeDraft(draft);
        Check(!BasicQuestionValidator.Validate(serialized.Replace("\"unit_id\":", "\"unexpected\":"), addition).IsValid
            && !BasicQuestionValidator.Validate(serialized[..^4], addition).IsValid, "Invalid JSON/schema accepted.");

        // Retry and preview facts do not change the reusable template identity.
        var store = new QuestionBankStore(Path.Combine(directory, "templates.db3"));
        var saved = new ValidatedBankQuestion(addition, draft, serialized, "template-test", DateTime.UtcNow);
        Check(await store.InsertAsync(saved), "Template insert failed.");
        Check(!await store.InsertAsync(saved with { Contract = addition.FreshFacts(new Random(123)) }), "Random preview facts duplicated a template.");
        var loaded = await new QuestionBankStore(Path.Combine(directory, "templates.db3")).TakeAsync(addition.Operation, addition.Tier, addition.Language);
        Check(loaded?.Draft == draft && loaded.Contract == addition, "Stored template failed to round-trip.");
        var provider = new BasicPracticeQuestionProvider(store, new AlwaysBank());
        var generator = new ArithmeticQuizGenerator(new BasicArithmeticEngine(), new Random(444));
        var operands = new HashSet<IntegerArithmeticExpression>();
        var actors = new HashSet<string>();
        for (int i = 0; i < 80; i++)
        {
            var original = generator.Generate(ArithmeticQuizMode.Essay, addition.Operation, new(addition.Tier, false));
            var chosen = await provider.SelectAsync(original, addition.Tier, addition.Language);
            Check(chosen.WordProblem is not null && maths.Validate(chosen).IsValid, "Stored template was not rendered for practice.");
            operands.Add(chosen.Expression); actors.Add(chosen.WordProblem!.SubjectName);
        }
        Check(operands.Count > 10 && actors.Count > 10, "Stored template reused frozen numbers/names.");
        using var workbook = new MemoryStream();
        QuestionBankWorkbook.Write(workbook, [saved, new(apples.Contract!, apples.Draft!, serialized, "apples", DateTime.UtcNow)]);
        workbook.Position = 0;
        var rows = QuestionBankWorkbook.Read(workbook);
        Check(rows.Count == 2 && rows.All(r => r.ErrorCode is null) && rows[0].Question!.Draft == saved.Draft
            && rows[0].Question!.Contract == saved.Contract && rows[1].Question!.Contract == apples.Contract,
            "Excel lost structure, secondary actor, unit ID or prose solution.");
        var tampered = await store.QueryAsync("UPDATE BasicQuestionBank SET DraftJson='{}'");
        Check(tampered.IsSuccess && await store.TakeAsync(addition.Operation, addition.Tier, addition.Language) is null,
            "SQL-mutated invalid template reached practice.");
        Console.WriteLine("PASS names, natural prose, fact/role/unit/solution rejection, template dedup, SQLite/Excel compatibility and fresh bank practice");
        await CheckLegacyHashAsync(directory);
    }
    private static async Task CheckLegacyHashAsync(string directory)
    {
        const string originalContract = """{"Version":1,"Operation":0,"Tier":1,"Language":0,"Left":7,"Right":2,"Subject":"Lan","Unit":"quyển sách","GroupUnit":"thùng"}""";
        const string originalDraft = """{"given_a":"Lan có 7 quyển sách.","given_b":"Lan nhận thêm 2 quyển sách.","question":"Hỏi Lan có tất cả bao nhiêu quyển sách?"}""";
        var contract = JsonSerializer.Deserialize<BasicQuestionContract>(originalContract)!;
        var validation = BasicQuestionValidator.Validate(originalDraft, contract);
        Check(validation.IsValid, "Historical v1 JSON no longer validates.");
        var historical = new ValidatedBankQuestion(contract, validation.Draft!, originalDraft, "historical", DateTime.UtcNow);
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            originalContract + "\n" + BasicQuestionValidator.Normalize(historical.Draft.ProblemText))));
        string path = Path.Combine(directory, "historical-v1.db3");
        var store = new QuestionBankStore(path);
        await store.QueryAsync("SELECT count(*) FROM BasicQuestionBank");
        using (var db = new SQLite.SQLiteConnection(path))
            db.Execute("INSERT INTO BasicQuestionBank (Hash,Operation,Stars,Language,Version,ContractJson,DraftJson,RawJson,ModelName,CreatedUtc,LastUsedUtc,UseCount) VALUES (?,?,?,?,?,?,?,?,?,?,?,?)",
                hash, 0, 1, 0, 1, originalContract, originalDraft, originalDraft, "historical", DateTime.UtcNow, DateTime.MinValue, 0);
        Check(!await store.InsertAsync(historical), "An old v1 hash was not recognized during import/insert.");
        Check((await store.TakeAsync(contract.Operation, contract.Tier, contract.Language))?.WordProblem == historical.WordProblem,
            "Existing v1 facts/prose were altered during backward-compatible selection.");
        Console.WriteLine("PASS historical v1 SQLite records, original hash deduplication and fixed facts");
    }
    private sealed class AlwaysBank : Random { public override int Next(int maxValue) => 1; }
}
