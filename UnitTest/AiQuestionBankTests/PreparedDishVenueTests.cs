using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static class PreparedDishVenueTests
{
    private static readonly string[] Dishes = ["bot-chien", "spaghetti", "pizzas", "mixed-salad", "hot-dogs", "udon"];
    // Fail with a useful assertion instead of hanging if owner selection rerolls forever.
    private sealed class ConstantNameRandom(bool lastChoice) : Random
    {
        public int NameChoiceCalls { get; private set; }
        public override int Next(int maxValue)
        {
            if (++NameChoiceCalls > 128) throw new InvalidOperationException("Owner selection exceeded 128 bounded random choices");
            return lastChoice ? maxValue - 1 : 0;
        }
        public override int Next(int minValue, int maxValue) => lastChoice ? maxValue - 1 : minValue;
    }
    // Independent expectations: do not derive these from FoodThemePrompt.
    private static readonly (string Vi, string En, string PairVi, string PairEn)[] Venues =
    [
        ("quán ăn", "eatery", "cả hai quán ăn", "the two eateries"),
        ("nhà hàng", "restaurant", "cả hai nhà hàng", "the two restaurants"),
        ("tiệm ăn", "diner", "cả hai tiệm ăn", "the two diners")
    ];

    private static BasicQuestionContract Create(string dish, AppLanguage language, BankQuestionFamily family,
        ArithmeticOperation sourceOperation, int seed)
    {
        var profile = new QuestionLearningProfile(QuestionKnowledgeGroup.Food);
        var tier = CurriculumTier.ThreeStars;
        string suffix = sourceOperation switch { ArithmeticOperation.Add => "total", ArithmeticOperation.Subtract => "remaining",
            ArithmeticOperation.Multiply => "groups", _ => "share" };
        string id = $"theme-food-{dish}-{suffix}";
        if (family == BankQuestionFamily.Fraction)
            return FractionQuestionCatalogue.Create(profile, sourceOperation, tier, language, new(seed), "fraction-" + id);
        if (family == BankQuestionFamily.FindX)
        {
            var scene = FindXQuestionCatalogue.All.First(s => s.SourceScene.Id == id);
            return FindXQuestionCatalogue.Create(profile, scene.EquationOperation, tier, language, new(seed), scene.Role, scene.Id);
        }
        return AppliedQuestionCatalogue.Create(profile, sourceOperation, tier, language, new(seed), id);
    }

    public static void Run(string? output = null)
    {
        var failures = new List<string>();
        var samples = new List<object>();
        int cases = 0, negativeCases = 0, workbooks = 0;
        void Check(bool ok, string name) { if (!ok) failures.Add(name); }
        foreach (string dish in Dishes)
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (bool lastChoice in new[] { false, true })
        {
            string label = $"{dish}/{language}/{(lastChoice ? "last" : "first")}";
            var random = new ConstantNameRandom(lastChoice);
            try
            {
                var c = FractionQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Food), ArithmeticOperation.Add,
                    CurriculumTier.ThreeStars, language, random, $"fraction-theme-food-{dish}-total");
                string venue = language == AppLanguage.Vietnamese ? lastChoice ? "tiệm ăn của " : "quán ăn của "
                    : lastChoice ? "'s diner" : "'s eatery";
                Check(c.Subject != c.OtherSubject && c.Subject.Contains(venue) && c.OtherSubject.Contains(venue),
                    "Constant random yields two distinct matching venue owners: " + label);
                Check(c.IsValid && BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(FractionQuestionCatalogue.Draft(c)), c).IsValid,
                    "Constant random yields valid paired fraction total: " + label);
            }
            catch (InvalidOperationException e)
            {
                Check(false, "Constant random owner selection must complete: " + label + ": " + e.Message);
            }
        }
        bool OpenAccepts(string raw, BasicQuestionContract c)
        {
            try { return OpenQuestionProse.Validate(raw, c with { OpenProse = true }).IsValid; }
            catch (Exception e) { failures.Add($"Open AI validation must not throw: {c.SceneId}/{c.Language}: {e.GetType().Name}"); return false; }
        }
        foreach (var dish in Dishes)
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var family in new[] { BankQuestionFamily.Arithmetic, BankQuestionFamily.Fraction, BankQuestionFamily.FindX })
        foreach (var venue in Venues)
        {
            bool vi = language == AppLanguage.Vietnamese;
            string kind = vi ? venue.Vi : venue.En;
            string ownerPhrase = vi ? kind + " của " : "'s " + kind;
            string pair = vi ? venue.PairVi : venue.PairEn;
            string label = $"{dish}/{language}/{family}/{kind}";
            var c = Enumerable.Range(0, 100).Select(seed => Create(dish, language, family, ArithmeticOperation.Add, seed))
                .First(c => c.Subject.Contains(ownerPhrase, StringComparison.OrdinalIgnoreCase));
            var d = BasicQuestionTemplates.Example(c);
            string story = d.ToWordProblem(c).ProblemText;
            string expectedQuestion = vi
                ? "Hỏi " + pair + " phục vụ " + (family == BankQuestionFamily.Fraction ? "tổng cộng" : "tất cả") + " bao nhiêu {unit}?"
                : "How many {unit} do " + pair + " serve altogether?";
            Check(d.Question == expectedQuestion, "Independent question wording: " + label + ": " + d.Question);
            Check(d.GivenA == (vi ? "{name} phục vụ {a} " : "{name} serves {a} ")
                + (family == BankQuestionFamily.Fraction ? "{unit_a}," : "{unit},"), "Independent first fact wording: " + label);
            Check(d.GivenB == (vi ? "{other} phục vụ {b} " : "{other} serves {b} ")
                + (family == BankQuestionFamily.Fraction ? "{unit_b}." : "{unit}."), "Independent second fact wording: " + label);
            samples.Add(new { dish, language, family, venue = kind, c.Subject, c.OtherSubject,
                c.Structure, c.Operation, Question = story, Template = d.Question, c.AnswerText });
            Check(c.OtherSubject.Contains(ownerPhrase, StringComparison.OrdinalIgnoreCase) && c.Subject != c.OtherSubject,
                "Two distinct matching venue owners: " + label + ": " + c.Subject + " / " + c.OtherSubject);
            Check(d.GivenA.Contains("{name}") && d.GivenB.Contains("{other}"), "Separate owner fact slots: " + label);
            Check(story.Contains(pair, StringComparison.OrdinalIgnoreCase), "Exact venue pair in rendered question: " + label + ": " + story);
            Check(!story.Contains(vi ? "cơ sở" : "businesses", StringComparison.OrdinalIgnoreCase), "No generic venue: " + label);
            Check(Regex.Matches(story, Regex.Escape(vi ? " của " : "'s "), RegexOptions.IgnoreCase).Count == 2,
                "No duplicate owner prefix: " + label + ": " + story);
            Check(c.IsValid && BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d), c).IsValid,
                "Typed AI accepts correct pair: " + label);
            Check(BasicQuestionPrompt.Build(c).Contains(vi ? "bột chiên, mì Ý, pizza, salad, hot dog và udon"
                : "bot chien, spaghetti, pizza, salad, hot dogs and udon", StringComparison.OrdinalIgnoreCase),
                "AI guidance explicitly names six prepared dishes: " + label);
            if (OpenQuestionProse.Supports(c))
                Check(OpenAccepts(QuestionBankStore.SerializeDraft(d), c),
                    "Open AI accepts correct pair: " + label);

            // Render first: mutating the generic template would be undone by alignment.
            string renderedQuestion = family == BankQuestionFamily.Fraction ? FractionQuestionCatalogue.Render(d.Question, c)
                : family == BankQuestionFamily.FindX ? FindXQuestionCatalogue.Render(d.Question, c)
                : AppliedQuestionCatalogue.Render(d.Question, c);
            string wrongPair = vi ? Venues.First(v => v.Vi != kind).PairVi : Venues.First(v => v.En != kind).PairEn;
            string[] invalidPairs = [vi ? "cả hai cơ sở" : "the two businesses", wrongPair,
                pair + (vi ? " và cả hai cơ sở" : " and the two businesses"),
                pair + (vi ? " và " : " and ") + wrongPair];
            foreach (string invalidPair in invalidPairs)
            {
                string bad = d.Question.Replace(pair, invalidPair, StringComparison.OrdinalIgnoreCase);
                // Existing generic source templates are also tested unchanged for strict rejection.
                if (!d.Question.Contains(pair, StringComparison.OrdinalIgnoreCase))
                    bad = d.Question.Replace(vi ? "cả hai cơ sở" : "the two businesses", invalidPair, StringComparison.OrdinalIgnoreCase);
                string raw = QuestionBankStore.SerializeDraft(d with { Question = bad });
                Check(!BasicQuestionValidator.Validate(raw, c).IsValid,
                    "Typed AI rejects generic/conflicting pair: " + label + ": " + invalidPair);
                if (OpenQuestionProse.Supports(c))
                    Check(!OpenAccepts(raw, c),
                        "Open AI rejects generic/conflicting pair: " + label + ": " + invalidPair);
                negativeCases++;
            }
            Check(renderedQuestion.Contains(pair, StringComparison.OrdinalIgnoreCase), "Rendered mutation has exact target: " + label);
            var duplicate = d with { GivenA = vi ? "Nhà hàng của {name} phục vụ {a} {unit}," : "{name}'s restaurant serves {a} {unit}," };
            Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(duplicate), c).IsValid,
                "Typed AI rejects duplicated venue ownership: " + label);
            if (OpenQuestionProse.Supports(c))
                Check(!OpenAccepts(QuestionBankStore.SerializeDraft(duplicate), c), "Open AI rejects duplicated venue ownership: " + label);
            var mismatched = c with { OtherSubject = c.OtherSubject.Replace(kind, vi ? "tiệm bánh" : "bakery", StringComparison.OrdinalIgnoreCase) };
            Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d), mismatched).IsValid,
                "Typed AI rejects mismatched owner venues: " + label);

            using var backup = new MemoryStream();
            QuestionBankWorkbook.WriteBackup(backup, [new(c, d, QuestionBankStore.SerializeDraft(d), "Venue regression", DateTime.UtcNow)]);
            backup.Position = 0;
            var row = QuestionBankWorkbook.Read(backup).Single();
            Check(row.Question?.Contract == c && row.Question?.Draft.ToWordProblem(c).ProblemText == story,
                "Excel backup preserves venue facts and pair: " + label);
            using var authoring = new MemoryStream();
            QuestionAuthoringWorkbook.Write(authoring, c, dish, language);
            using var active = new MemoryStream(FoodThemeInterchangeTests.ActivateExample(authoring.ToArray(), language));
            var authored = QuestionBankWorkbook.Read(active).Single();
            Check(authored.Question?.Contract == c && authored.Question?.Draft.ToWordProblem(c).ProblemText == story,
                "Excel authoring preserves venue facts and pair: " + label);
            workbooks += 2;
            var fresh = Enumerable.Range(0, 100).Select(seed => c.FreshFacts(new(seed)))
                .First(f => !f.Subject.Contains(ownerPhrase, StringComparison.OrdinalIgnoreCase));
            var freshVenue = Venues.Single(v => fresh.Subject.Contains(vi ? v.Vi + " của " : "'s " + v.En, StringComparison.OrdinalIgnoreCase));
            string freshPair = vi ? freshVenue.PairVi : freshVenue.PairEn;
            string replay = d.ToWordProblem(fresh).ProblemText;
            Check(replay.Contains(freshPair, StringComparison.OrdinalIgnoreCase) && !replay.Contains(pair, StringComparison.OrdinalIgnoreCase),
                "Saved draft follows refreshed owners' venue: " + label + ": " + replay);
            cases++;
        }

        // Existing saved single-owner fractions, including all bakery contracts, remain readable.
        foreach (string dish in Dishes.Concat(new[] { "banh-chung", "cakes" }))
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        {
            var old = Create(dish, language, BankQuestionFamily.Fraction, ArithmeticOperation.Add, 15)
                with { Structure = BasicQuestionStructure.Increase, OtherSubject = "" };
            var oldDraft = FractionQuestionCatalogue.Draft(old);
            Check(old.IsValid && FractionQuestionCatalogue.Validate(QuestionBankStore.SerializeDraft(oldDraft), old).IsValid,
                "Saved single-owner fraction remains valid: " + dish + "/" + language);
            Check(!oldDraft.GivenB.Contains("{other}"), "Saved single-owner fraction keeps original fact roles: " + dish);
            if (Dishes.Contains(dish))
            {
                bool vi = language == AppLanguage.Vietnamese;
                old = old with { Left = 1, Right = 1, LeftDenominator = 2, RightDenominator = 3,
                    Subject = vi ? "nhà hàng của An" : "Ann's restaurant" };
                var storedDraft = new BasicQuestionDraft(
                    vi ? "{name} phục vụ {a} {unit_a}," : "{name} serves {a} {unit_a},",
                    vi ? "một lượt phục vụ khác có thêm {b} {unit_b}." : "another serving adds {b} {unit_b}.",
                    vi ? "Hỏi hai lượt phục vụ có tất cả bao nhiêu {unit}?" : "How many {unit} are served across the two servings?",
                    vi ? "Tổng lượng món phục vụ là ({unit}):" : "The total amount served is ({unit}):", old.SceneId);
                string raw = QuestionBankStore.SerializeDraft(storedDraft);
                Check(old.AnswerText == "5/6" && BasicQuestionValidator.Validate(raw, old).IsValid,
                    "Frozen numerical contract and stored one-owner text remain valid: " + dish + "/" + language);
                var refreshedOld = old.FreshFacts(new(7));
                Check(refreshedOld.Structure == BasicQuestionStructure.Increase && refreshedOld.OtherSubject == ""
                    && BasicQuestionValidator.Validate(raw, refreshedOld).IsValid,
                    "Saved one-owner fraction retains ownership on refresh: " + dish + "/" + language);
                using var saved = new MemoryStream();
                QuestionBankWorkbook.WriteBackup(saved, [new(old, storedDraft, raw, "Saved legacy fraction", DateTime.UnixEpoch)]);
                saved.Position = 0;
                var restored = QuestionBankWorkbook.Read(saved).Single().Question;
                Check(restored?.Contract == old && restored.Draft == storedDraft
                    && restored.Contract.AnswerText == "5/6" && !restored.Draft.ToWordProblem(old).ProblemText.Contains('{'),
                    "Stored legacy fraction survives bank workbook validation: " + dish + "/" + language);
            }
        }

        // Subtraction, multiplication and division must retain their original story relation.
        foreach (string dish in Dishes)
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        foreach (var family in new[] { BankQuestionFamily.Arithmetic, BankQuestionFamily.Fraction, BankQuestionFamily.FindX })
        foreach (var op in new[] { ArithmeticOperation.Subtract, ArithmeticOperation.Multiply, ArithmeticOperation.Divide })
        {
            var c = Create(dish, language, family, op, 9);
            var d = BasicQuestionTemplates.Example(c);
            string label = $"{dish}/{language}/{family}/{op}";
            string story = d.ToWordProblem(c).ProblemText;
            Check(!story.Contains(language == AppLanguage.Vietnamese ? "cả hai" : "the two", StringComparison.OrdinalIgnoreCase),
                "Other operations retain single venue relation: " + label);
            Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d), c).IsValid,
                "Other operations remain valid: " + label);
            if (family == BankQuestionFamily.Fraction)
                Check(c.Structure == BasicQuestionStructure.Increase && c.OtherSubject == "", "Fraction non-add ownership unchanged: " + label);
            else
                Check(c.Structure == (op switch { ArithmeticOperation.Subtract => BasicQuestionStructure.Remaining,
                    ArithmeticOperation.Multiply => BasicQuestionStructure.EqualGroups, _ => BasicQuestionStructure.EqualShare }),
                    "Original operation structure retained: " + label);
        }
        if (output is not null)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "venue-cases.json"), JsonSerializer.Serialize(samples,
                new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }));
            File.WriteAllLines(Path.Combine(output, "failures.txt"), failures);
        }
        if (failures.Count > 0)
            throw new Exception($"Prepared dish venues: {failures.Count} failures\n" + string.Join('\n', failures.Take(24)));
        Console.WriteLine($"PASS prepared dish venues: {cases} six-dish/VI-EN/three-venue/three-family cases, {negativeCases} invalid pairs, {workbooks} Excel roundtrips, 108 operation-preservation cases.");
    }
}
