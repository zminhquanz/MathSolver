using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.Text;
using System.Text.RegularExpressions;
using SQLite;

internal static class LanguageTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    public static async Task RunAsync(string directory)
    {
        var c = AdditionQuestionCatalogue.Create(CurriculumTier.ThreeStars, AppLanguage.Vietnamese,
            new Random(74), "library", BasicQuestionStructure.AddComparisonInverse);
        c = BasicQuestionTemplates.ApplyUnit(c, QuestionUnits.Find("books")!);
        var screenshot = new BasicQuestionDraft("{other} có łącznie {a} {unit},",
            "{other} ít hơn {name} là {b} {unit}.", "Hỏi {name} có bao nhiêu {unit}?",
            "Số {unit} mà {name} có là:", "books");
        var result = BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(screenshot), c);
        Check(!result.IsValid && result.ErrorCode == "WrongLanguage",
            "Screenshot regression: Polish 'łącznie' passed Vietnamese validation: " + result.ErrorCode);
        Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(screenshot)
            .Replace("łącznie", "\\u0142\\u0105cznie"), c).IsValid,
            "Escaped foreign letters bypassed JSON decoding validation.");

        int cases = 0;
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (int version in new[] { 1, 2, 3 })
        {
            var contract = version == 3 ? AdditionQuestionCatalogue.Create(CurriculumTier.OneStar,
                language, new Random(75), "family-gifts", BasicQuestionStructure.Increase)
                : version == 2 ? BasicQuestionContract.CreateTemplate(ArithmeticOperation.Add,
                    CurriculumTier.OneStar, language, new Random(75)) with { Structure = BasicQuestionStructure.Increase }
                : new BasicQuestionContract(1, ArithmeticOperation.Add, CurriculumTier.OneStar, language,
                    7, 2, "Lan", language == AppLanguage.Vietnamese ? "quyển sách" : "books", language == AppLanguage.Vietnamese ? "thùng" : "box");
            var example = version == 3 ? AdditionQuestionCatalogue.Example(contract)
                : version == 2 ? BasicQuestionTemplates.Example(contract)
                : language == AppLanguage.Vietnamese
                    ? new BasicQuestionDraft("Lan có 7 quyển sách.", "Lan nhận thêm 2 quyển sách.", "Hỏi Lan có tất cả bao nhiêu quyển sách?")
                    : new BasicQuestionDraft("Lan has 7 books.", "Lan receives 2 more books.", "How many books does Lan have in total?");
            BasicDraftValidation Validate(BasicQuestionDraft draft) => BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft), contract);
            Check(Validate(example).IsValid, $"Language test fixture v{version}/{language} is invalid: {Validate(example).ErrorCode}");
            foreach (string foreign in new[] { "łącznie", "ŁĄCZNIE", "şimdi", "Москва", "全部" })
            for (int field = 0; field < (version == 1 ? 3 : 4); field++)
            {
                var changed = AddText(example, field, foreign);
                Check(Validate(changed).ErrorCode == "WrongLanguage",
                    $"Foreign text accepted at v{version}/{language}/field {field}: {foreign}");
                cases++;
            }
            foreach (string garbage in new[] { "\ufffd", "\u200b", "\u202e", "😀", "\u0301" })
            for (int field = 0; field < (version == 1 ? 3 : 4); field++)
                Check(Validate(AddText(example, field, garbage)).ErrorCode == "InvalidText",
                    $"Invalid Unicode text accepted at v{version}/{language}/field {field}.");
            var decomposed = example with { GivenA = example.GivenA.Normalize(NormalizationForm.FormD),
                GivenB = example.GivenB.Normalize(NormalizationForm.FormD), Question = example.Question.Normalize(NormalizationForm.FormD),
                SolutionLead = example.SolutionLead?.Normalize(NormalizationForm.FormD) };
            var nfc = Validate(decomposed);
            Check(nfc.IsValid && nfc.Draft == example, "Valid decomposed Vietnamese accents were not normalized before relation checks.");
            if (language == AppLanguage.English)
                Check(Validate(AddText(example, 0, "tiếng Việt")).ErrorCode == "WrongLanguage", "Vietnamese leaked into English prose.");
            else
            {
                Check(Validate(AddText(example, 0, "– ‘quà’")).IsValid, "Legitimate typographic punctuation was rejected.");
                const string alphabet = "aàáảãạăằắẳẵặâầấẩẫậeèéẻẽẹêềếểễệiìíỉĩịoòóỏõọôồốổỗộơờớởỡợuùúủũụưừứửữựyỳýỷỹỵđ";
                var allLetters = AddText(example, 0, alphabet + " " + alphabet.ToUpperInvariant());
                Check(QuestionProseLanguage.ValidateAndNormalize(allLetters, language, out _) is null,
                    "A valid Vietnamese alphabet letter was rejected.");
            }
            if (version != 1)
            {
                string grammar = GgufQuestionRuntime.BuildGrammar(contract);
                Check(grammar.All(ch => ch < 128) && !grammar.Contains("{PROSE_LETTERS}"), "Native grammar was not fully expanded as ASCII.");
                string proseClass = Regex.Match(grammar, @"(?:prose|char) ::= (\[[^\r\n]+?\])").Groups[1].Value;
                Check(proseClass.Length > 0, "Grammar lost its prose character class.");
                var accepts = new Regex("^" + proseClass + "$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
                foreach (string foreign in new[] { "ł", "ą", "ş", "Ж", "全", "\u200b", "\ufffd", "٢" })
                    Check(!accepts.IsMatch(foreign), "Native grammar alphabet permits: " + foreign);
                Check(accepts.IsMatch("a") && accepts.IsMatch("Đ") == (language == AppLanguage.Vietnamese),
                    "Grammar lost legitimate prose letters or ignores selected language.");
                Check(BasicQuestionPrompt.Build(contract, "WrongLanguage").Contains("foreign-language"),
                    "Retry prompt did not explain the language error.");
            }
        }

        var valid = new ValidatedBankQuestion(c, AdditionQuestionCatalogue.Example(c), "{}", "language-test", DateTime.UtcNow);
        var store = new QuestionBankStore(Path.Combine(directory, "language.db3"));
        try
        {
            await store.InsertAsync(valid with { Draft = screenshot });
            throw new InvalidOperationException("Manual insert accepted foreign prose.");
        }
        catch (InvalidOperationException error) when (error.Message == "WrongLanguage") { }
        Check(await store.InsertAsync(valid), "Valid language fixture did not insert.");
        using (var db = new SQLiteConnection(Path.Combine(directory, "language.db3")))
            db.Execute("UPDATE BasicQuestionBank SET DraftJson=?", QuestionBankStore.SerializeDraft(screenshot));
        Check(await store.TakeAsync(c.Operation, c.Tier, c.Language) is null, "Old SQLite foreign prose reached practice.");
        using var export = new MemoryStream();
        var report = await store.ExportExcelAsync(export);
        Check(report.Exported == 0 && report.Skipped == 1, "Foreign prose reached Excel export.");
        using var workbook = new MemoryStream();
        QuestionBankWorkbook.Write(workbook, [valid with { Draft = screenshot }]);
        workbook.Position = 0;
        var import = await store.ImportExcelAsync(workbook);
        Check(import.Rejected == 1 && import.Inserted == 0 && import.Issues.Single().ErrorCode == "WrongLanguage",
            "Foreign prose from Excel import bypassed validation.");

        var runtime = new ForeignProseRuntime();
        var retryStore = new QuestionBankStore(Path.Combine(directory, "foreign-retry.db3"));
        var service = new AiQuestionGenerationService(runtime, retryStore);
        service.Start(new(ArithmeticOperation.Add, CurriculumTier.ThreeStars, AppLanguage.Vietnamese, 1, true));
        await service.Completion;
        Check(runtime.Calls == 3 && service.Snapshot.State == AiJobState.Failed
            && service.Snapshot.Items.Single().Attempts.All(a => a.ErrorCode == "WrongLanguage")
            && await retryStore.TakeAsync(ArithmeticOperation.Add, CurriculumTier.ThreeStars, AppLanguage.Vietnamese) is null,
            "Foreign generated prose did not fail after three retries without saving.");
        Console.WriteLine($"PASS screenshot/escaped-JSON language regression, {cases} foreign-prose mutations, accents, grammar, retries and SQLite/Excel boundaries");
    }

    private static BasicQuestionDraft AddText(BasicQuestionDraft draft, int field, string text) => field switch {
        0 => draft with { GivenA = draft.GivenA + " " + text },
        1 => draft with { GivenB = draft.GivenB + " " + text },
        2 => draft with { Question = draft.Question + " " + text },
        _ => draft with { SolutionLead = draft.SolutionLead + " " + text }
    };

    private sealed class ForeignProseRuntime : IQuestionTextRuntime
    {
        public bool IsLoaded => true;
        public string ModelName => "foreign-prose-test";
        public int Calls { get; private set; }
        public Task<string> GenerateAsync(BasicQuestionContract contract, string prompt, CancellationToken cancellationToken,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
        {
            Calls++;
            return Task.FromResult(QuestionBankStore.SerializeDraft(AddText(AdditionQuestionCatalogue.Example(contract), 0, "łącznie")));
        }
    }
}
