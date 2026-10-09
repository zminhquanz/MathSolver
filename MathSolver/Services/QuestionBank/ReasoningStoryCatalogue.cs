using MathSolver.Models;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

public sealed record ReasoningStorySeed(int Seed, BankQuestionFamily Family, int Variant, string Schema, string NarrativeId = "");
public sealed record NarrativeClause(string Role, string Text);
internal sealed record NarrativeQuantity(string Id, string Role, string Value);
internal sealed record NarrativeStep(string Id, string Lead, string Calculation);

/// <summary>Projects the actual C# curriculum generators into reusable language templates.
/// Neither stored prose nor the LLM supplies numbers, answers, or inference steps.</summary>
public static class ReasoningStoryCatalogue
{
    public const int Version = 8;
    internal static bool UsesReviewedPhrasings(BankQuestionFamily family) => family is
        BankQuestionFamily.MultiStep or BankQuestionFamily.Motion or BankQuestionFamily.Proportion
        or BankQuestionFamily.Decimal or BankQuestionFamily.Measurement or BankQuestionFamily.Remainder or BankQuestionFamily.Time or BankQuestionFamily.FractionQuantity;
    private static readonly ConditionalWeakTable<BasicQuestionContract, NarrativeLesson> Lessons = new();
    public static bool Supports(BankQuestionFamily family) => family is BankQuestionFamily.TwoNumbers
        or BankQuestionFamily.Average or BankQuestionFamily.Percentage or BankQuestionFamily.MultiStep or BankQuestionFamily.Motion or BankQuestionFamily.Proportion or BankQuestionFamily.Decimal or BankQuestionFamily.Measurement or BankQuestionFamily.Remainder or BankQuestionFamily.Time or BankQuestionFamily.FractionQuantity;
    public static int[] Variants(BankQuestionFamily family, CurriculumTier tier) => family switch
    {
        BankQuestionFamily.TwoNumbers => [(int)ElementaryQuizType.SumDifference, (int)ElementaryQuizType.SumRatio, (int)ElementaryQuizType.DifferenceRatio],
        BankQuestionFamily.Average => Enum.GetValues<AverageQuizType>().Select(value => (int)value).ToArray(),
        BankQuestionFamily.Percentage => Enum.GetValues<PercentageQuizType>().Select(value => (int)value).ToArray(),
        BankQuestionFamily.MultiStep => ElementaryQuizGenerator.Types(QuizProblemKind.MultiStep).Select(value => (int)value).ToArray(),
        BankQuestionFamily.Motion => Enum.GetValues<MotionQuizType>().Select(value => (int)value).ToArray(),
        BankQuestionFamily.Proportion => Enum.GetValues<ProportionQuizType>().Select(value => (int)value).ToArray(),
        BankQuestionFamily.Decimal => ElementaryQuizGenerator.DecimalStoryTypes.Select(value => (int)value).ToArray(),
        BankQuestionFamily.Measurement => ElementaryQuizGenerator.MeasurementStoryTypes.Select(value => (int)value).ToArray(),
        BankQuestionFamily.Remainder => ElementaryQuizGenerator.RemainderStoryTypes.Select(value => (int)value).ToArray(),
        BankQuestionFamily.Time => ElementaryQuizGenerator.TimeStoryTypes.Select(value => (int)value).ToArray(),
        BankQuestionFamily.FractionQuantity => ElementaryQuizGenerator.FractionQuantityStoryTypes.Select(value => (int)value).ToArray(),
        _ => []
    };

    public static BasicQuestionContract Create(BankQuestionFamily family, int variant, CurriculumTier tier,
        AppLanguage language, Random? random = null, string narrativeId = "")
    {
        if (!Supports(family) || !Variants(family, tier).Contains(variant) || !Enum.IsDefined(tier)
            || language is not (AppLanguage.Vietnamese or AppLanguage.English)) throw new ArgumentException("InvalidStoryProfile");
        random ??= Random.Shared;
        var provisional = new BasicQuestionContract(Version, ArithmeticOperation.Add, tier, language,
            1, 1, "", "", "") { Story = new(random.Next(), family, variant, "", narrativeId) };
        var lesson = Build(provisional);
        return provisional with { Subject = lesson.Subject, Unit = lesson.Unit, SceneId = lesson.Context,
            TopicId = family.ToString(), Story = provisional.Story! with { Schema = lesson.Schema,
                NarrativeId = family is BankQuestionFamily.Decimal or BankQuestionFamily.Measurement or BankQuestionFamily.Remainder or BankQuestionFamily.Time or BankQuestionFamily.FractionQuantity ? lesson.Context : lesson.QuestionModel.ProportionProblem?.NarrativeId ?? "" } };
    }

    internal static NarrativeLesson Lesson(BasicQuestionContract c) => Lessons.GetValue(c, Build);

    internal static object DiagnosticData(BasicQuestionContract c)
    {
        var lesson = Lesson(c);
        return new { Contract = c, Quantities = lesson.Quantities,
            Facts = lesson.Facts.Select(f => new { f.Role, Text = Render(f.Text, c) }),
            Question = Render(lesson.Question, c), Steps = lesson.Steps, Answer = lesson.Answer };
    }

    public static bool IsValid(BasicQuestionContract c)
    {
        if (c.Version != Version || c.Story is not { } seed || seed.Seed < 0 || !Supports(seed.Family)
            || !Variants(seed.Family, c.Tier).Contains(seed.Variant) || !Enum.IsDefined(c.Tier)
            || c.Language is not (AppLanguage.Vietnamese or AppLanguage.English) || c.Operation != ArithmeticOperation.Add
            || c.Left != 1 || c.Right != 1 || c.LeftDenominator != 1 || c.RightDenominator != 1
            || c.Grade != 0 || c.KnowledgeGroup != QuestionKnowledgeGroup.Objects || c.UnknownRole != FindXUnknownRole.None
            || c.Structure != BasicQuestionStructure.Increase || c.OtherSubject != "" || c.GroupUnit != ""
            || c.PartA != "" || c.PartB != ""
            || (seed.Family is not (BankQuestionFamily.Proportion or BankQuestionFamily.Decimal or BankQuestionFamily.Measurement or BankQuestionFamily.Remainder or BankQuestionFamily.Time or BankQuestionFamily.FractionQuantity) && seed.NarrativeId != "")
            || (seed.Family is BankQuestionFamily.Proportion or BankQuestionFamily.Decimal or BankQuestionFamily.Measurement or BankQuestionFamily.Remainder or BankQuestionFamily.Time or BankQuestionFamily.FractionQuantity && string.IsNullOrEmpty(seed.NarrativeId))) return false;
        NarrativeLesson lesson;
        try { lesson = Lesson(c); }
        catch (ArgumentException) when (seed.Family is BankQuestionFamily.Proportion or BankQuestionFamily.Decimal or BankQuestionFamily.Measurement or BankQuestionFamily.Remainder or BankQuestionFamily.Time or BankQuestionFamily.FractionQuantity) { return false; }
        return c.TopicId == seed.Family.ToString() && c.SceneId == lesson.Context && c.Subject == lesson.Subject
            && c.Unit == lesson.Unit && seed.Schema == lesson.Schema && lesson.Quantities.Count > 0;
    }

    public static BasicQuestionContract Fresh(BasicQuestionContract c, Random? random = null)
    {
        if (!IsValid(c)) throw new ArgumentException("InvalidContract");
        random ??= Random.Shared;
        // A schema binds roles, context, question and step order. Resample the
        // original generator, retaining precisely that schema, never just two values.
        for (int attempt = 0; attempt < 512; attempt++)
        {
            var fresh = Create(c.Family, c.Story!.Variant, c.Tier, c.Language, random, c.Story.NarrativeId);
            if (fresh.Story!.Schema == c.Story.Schema
                && (c.Family is not (BankQuestionFamily.Decimal or BankQuestionFamily.Measurement or BankQuestionFamily.Remainder or BankQuestionFamily.Time or BankQuestionFamily.FractionQuantity) || !Lesson(fresh).Quantities.Where(q => q.Id.StartsWith('f'))
                    .Select(q => q.Value).SequenceEqual(Lesson(c).Quantities.Where(q => q.Id.StartsWith('f')).Select(q => q.Value))))
                return fresh;
        }
        throw new InvalidOperationException("StorySchemaUnavailable");
    }

    public static BasicQuestionDraft Draft(BasicQuestionContract c)
    {
        var lesson = Lesson(c);
        return new("", "", lesson.Question, lesson.Steps[0].Lead, "story")
        { Facts = lesson.Facts, SolutionLeads = lesson.Steps.Select(s => new NarrativeClause(s.Id, s.Lead)).ToArray() };
    }

    public static string Render(string text, BasicQuestionContract c)
    {
        var quantities = Lesson(c).Quantities.ToDictionary(q => q.Id, q => q.Value);
        return Regex.Replace(text, @"\{([fv]\d+)\}", m => quantities.GetValueOrDefault(m.Groups[1].Value, m.Value));
    }

    public static string Solution(BasicQuestionContract c, BasicQuestionDraft? draft = null)
    {
        var lesson = Lesson(c);
        var leads = draft?.SolutionLeads?.ToDictionary(l => l.Role, l => l.Text);
        string text = string.Join("\n", lesson.Steps.Select(s => Render(leads?.GetValueOrDefault(s.Id) ?? s.Lead, c)
            .TrimEnd(':', ' ') + ":\n" + s.Calculation));
        return text + "\n" + (c.Language == AppLanguage.Vietnamese ? "Đáp số: " : "Answer: ") + lesson.Answer;
    }

    public static ArithmeticQuizQuestion ToPractice(BasicQuestionContract c, BasicQuestionDraft draft,
        ArithmeticQuizMode mode)
    {
        var lesson = Lesson(c);
        string problem = Render(draft.ProblemText, c), solution = Solution(c, draft);
        var q = Generate(c, mode);
        if (q.ElementaryProblem is { } elementary)
        {
            var leads = draft.SolutionLeads!.ToDictionary(l => l.Role, l => Render(l.Text, c).TrimEnd(':', ' '));
            var reasoning = elementary.Reasoning! with { Steps = elementary.Reasoning!.Steps.Select((s, i) =>
                s with { Label = leads["step_" + i] }).ToArray() };
            return q with { ElementaryProblem = elementary with { ProblemText = problem, SolutionText = solution, Reasoning = reasoning } };
        }
        if (q.AverageProblem is { } average)
            return q with { AverageProblem = average with { ProblemText = problem, SolutionText = solution }, WordProblem = null };
        if (q.MotionProblem is { } motion)
            return q with { MotionProblem = motion with { ProblemText = problem, SolutionText = solution }, WordProblem = null };
        if (q.ProportionProblem is { } proportion)
            return q with { ProportionProblem = proportion with { ProblemText = problem, SolutionText = solution }, WordProblem = null };
        return q with { PercentageProblem = q.PercentageProblem! with { ProblemText = problem, SolutionText = solution }, WordProblem = null };
    }

    private static ArithmeticQuizQuestion Generate(BasicQuestionContract c, ArithmeticQuizMode mode)
    {
        var random = new Random(c.Story!.Seed);
        return c.Family switch
        {
            BankQuestionFamily.TwoNumbers => new ElementaryQuizGenerator(random).Generate(mode, QuizProblemKind.TwoNumbers,
                (ElementaryQuizType)c.Story.Variant, c.Language, c.Tier),
            BankQuestionFamily.MultiStep => new ElementaryQuizGenerator(random).Generate(mode, QuizProblemKind.MultiStep,
                (ElementaryQuizType)c.Story.Variant, c.Language, c.Tier),
            BankQuestionFamily.Average => new AverageQuizGenerator(random).GenerateAlgorithm(mode,
                (AverageQuizType)c.Story.Variant, c.Language, new(c.Tier, false)),
            BankQuestionFamily.Percentage => new PercentageQuizGenerator(random).GenerateAlgorithm(mode,
                (PercentageQuizType)c.Story.Variant, c.Language, new(c.Tier, false)),
            BankQuestionFamily.Motion => new MotionQuizGenerator(random).GenerateAlgorithm(mode,
                c.Language, (MotionQuizType)c.Story.Variant, new(c.Tier, false)),
            BankQuestionFamily.Proportion => new ProportionQuizGenerator(random).GenerateNarrative(mode,
                (ProportionQuizType)c.Story.Variant, c.Language, new(c.Tier, false), c.Story.NarrativeId),
            BankQuestionFamily.Decimal => new ElementaryQuizGenerator(random).GenerateDecimalStory(mode,
                (ElementaryQuizType)c.Story.Variant, c.Language, c.Tier, c.Story.NarrativeId),
            BankQuestionFamily.Measurement => new ElementaryQuizGenerator(random).GenerateMeasurementStory(mode,
                (ElementaryQuizType)c.Story.Variant, c.Language, c.Tier, c.Story.NarrativeId),
            BankQuestionFamily.Remainder => new ElementaryQuizGenerator(random).GenerateRemainderStory(mode,
                (ElementaryQuizType)c.Story.Variant, c.Language, c.Tier, c.Story.NarrativeId),
            BankQuestionFamily.Time => new ElementaryQuizGenerator(random).GenerateTimeStory(mode,
                (ElementaryQuizType)c.Story.Variant, c.Language, c.Tier, c.Story.NarrativeId),
            BankQuestionFamily.FractionQuantity => new ElementaryQuizGenerator(random).GenerateFractionQuantityStory(mode,
                (ElementaryQuizType)c.Story.Variant, c.Language, c.Tier,
                c.Story.NarrativeId.Length > 0 ? c.Story.NarrativeId
                    : FractionQuantityStoryContextCatalog.GetProfile(c.Language)[random.Next(FractionQuantityStoryContextCatalog.GetProfile(c.Language).Count)].ContextId),
            _ => throw new ArgumentException("InvalidStoryProfile")
        };
    }

    private static NarrativeLesson Build(BasicQuestionContract c)
    {
        using var capture = new QuizNarrativeCapture(c.Family switch {
            BankQuestionFamily.MultiStep => ReviewedNarrativePhrasings.For(c.Language),
            BankQuestionFamily.Motion => ReviewedNarrativePhrasings.For(c.Language, ReviewedNarrativePhrasings.MotionListName),
            BankQuestionFamily.Proportion => ReviewedNarrativePhrasings.For(c.Language, ReviewedNarrativePhrasings.ProportionListName),
            BankQuestionFamily.Decimal => ReviewedNarrativePhrasings.For(c.Language, ReviewedNarrativePhrasings.DecimalListName, c.Unit),
            BankQuestionFamily.Measurement => ReviewedNarrativePhrasings.For(c.Language, ReviewedNarrativePhrasings.MeasurementListName,
                ElementaryQuizGenerator.MeasurementCategory((ElementaryQuizType)c.BankVariant)),
            BankQuestionFamily.Remainder => ReviewedNarrativePhrasings.For(c.Language, ReviewedNarrativePhrasings.RemainderListName),
            BankQuestionFamily.Time => ReviewedNarrativePhrasings.For(c.Language, ReviewedNarrativePhrasings.TimeListName),
            BankQuestionFamily.FractionQuantity => ReviewedNarrativePhrasings.ForFractionQuantity(c.Language),
            _ => null });
        var q = Generate(c, ArithmeticQuizMode.Essay);
        string problem = q.ElementaryProblem?.ProblemText ?? q.AverageProblem?.ProblemText ?? q.MotionProblem?.ProblemText ?? q.ProportionProblem?.ProblemText ?? q.PercentageProblem!.ProblemText;
        string template = capture.Finish(problem);
        string projectedTemplate = template;
        var quantities = new List<NarrativeQuantity>();
        var ids = new Dictionary<string, string>();
        template = Regex.Replace(template, @"\{(f\d+)\}", m =>
        {
            string original = m.Groups[1].Value;
            if (!ids.TryGetValue(original, out var id))
            {
                id = "f" + quantities.Count;
                ids.Add(original, id);
                var slot = capture.Slots[original];
                quantities.Add(new(id, slot.Role, slot.Value));
            }
            return "{" + id + "}";
        });
        string context = q.ElementaryProblem?.StoryContextId ?? q.AverageProblem?.StoryContextId
            ?? q.PercentageProblem?.StoryContextId ?? (q.ProportionProblem is { } proportionContext
                ? "proportion-" + proportionContext.NarrativeId + "-" + proportionContext.AsksForAdditionalPeople
                : q.MotionProblem is { } motion ? "motion-" + motion.QuestionKind : "distribution");
        string subject = q.AverageProblem?.SubjectName ?? q.PercentageProblem?.SubjectName
            ?? q.MotionProblem?.SubjectName ?? q.ProportionProblem?.SubjectName ?? string.Join("; ", q.ElementaryProblem!.Answers.Select(a => a.Label));
        string unit = q.ElementaryProblem?.Answers[0].Unit ?? q.AverageProblem?.AnswerUnit ?? q.MotionProblem?.AnswerUnit ?? q.ProportionProblem?.AnswerUnit ?? q.PercentageProblem!.AnswerUnit;
        string answer = q.ElementaryProblem?.AnswerText ?? q.CorrectAnswer.ToString(CultureInfo.InvariantCulture) + " " + unit;
        var steps = q.ElementaryProblem?.Reasoning!.Steps.Select((s, i) => new NarrativeStep("step_" + i,
            s.Label, QuizMathExpressionFormatter.Format(s.Expression) + " = " + s.DisplayValue + " " + s.Unit)).ToArray();
        if (steps is null)
        {
            string equation = q.AverageProblem?.EquationText ?? q.MotionProblem?.EquationText ?? q.PercentageProblem?.EquationText ?? "";
            string solution = q.AverageProblem?.SolutionText ?? q.MotionProblem?.SolutionText ?? q.PercentageProblem?.SolutionText
                ?? ProportionQuizSolutionFormatter.Format(q.ProportionProblem!, c.Language, CultureInfo.InvariantCulture);
            var calculations = new List<NarrativeStep>();
            string lead = c.Language == AppLanguage.Vietnamese ? "Số " + unit + " cần tìm là" : "The requested quantity in " + unit + " is";
            foreach (string line in solution.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))
            {
                if (line.Contains('='))
                    calculations.Add(new("step_" + calculations.Count, lead, line));
                else if (!line.StartsWith(c.Language == AppLanguage.Vietnamese ? "\u0110\u00e1p s\u1ed1:" : "Answer:", StringComparison.OrdinalIgnoreCase))
                {
                    string next = line.TrimEnd(':', ' ');
                    if (!Regex.IsMatch(next, @"\d")) lead = next;
                }
            }
            steps = calculations.Count > 0 ? calculations.ToArray() : [new("step_0", lead, equation + " " + unit)];
        }
        // Owners and dimensions are explicit bindings, just like the quantities.
        // This stops the model abstracting pupils/groups into generic objects/units.
        var bindings = new List<(string Role, string Value)> { ("answer_unit", unit) };
        var replacements = new List<(string Spelling, string Id)>();
        var originalLeads = steps.Select(s => s.Lead).ToArray();
        if (q.ElementaryProblem is { } e)
            bindings.AddRange(e.Answers.Select((a, i) => ("owner_" + i, a.Label)));
        if (q.MotionProblem is { } motionBindings)
        {
            bindings.AddRange(motionBindings.RequiredProblemUnits.Select((value, i) => ("motion_unit_" + i, value)));
            bindings.AddRange(motionBindings.NarrativeActors.Select((value, i) => ("motion_actor_" + i, value)));
        }
        if (c.Family is BankQuestionFamily.Measurement or BankQuestionFamily.Remainder or BankQuestionFamily.Time or BankQuestionFamily.FractionQuantity && q.ElementaryProblem?.Reasoning is { } measurement)
            bindings.AddRange(measurement.Givens.Select(given => given.Unit).Distinct(StringComparer.Ordinal)
                .Select((unit, i) => ("measurement_unit_" + i, unit)));
        bindings.AddRange(new[] { "Lan", "Mai", "Hoa", "An", "Bình", "Nam" }
            .Where(name => Regex.IsMatch(template, @"(?<!\p{L})" + name + @"(?!\p{L})"))
            .Select((name, i) => ("actor_" + i, name)));
        foreach (var (role, value) in bindings.Where(b => b.Value.Length > 0).OrderByDescending(b => b.Value.Length))
        {
            string pattern = @"(?<![\p{L}\p{N}])" + Regex.Escape(value) + @"(?![\p{L}\p{N}])";
            var matches = Regex.Matches(template + "\n" + string.Join("\n", steps.Select(s => s.Lead)), pattern, RegexOptions.IgnoreCase);
            foreach (string spelling in matches.Select(m => m.Value).Distinct(StringComparer.Ordinal))
            {
                string id = "v" + quantities.Count(v => v.Id.StartsWith('v'));
                quantities.Add(new(id, role, spelling));
                replacements.Add((spelling, id));
                string Replace(string text) => Regex.Replace(text, @"(?<![\p{L}\p{N}])" + Regex.Escape(spelling)
                    + @"(?![\p{L}\p{N}])", "{" + id + "}");
                template = Replace(template);
                steps = steps.Select(s => s with { Lead = Replace(s.Lead) }).ToArray();
            }
        }
        string[] sentences = Regex.Split(template.Trim(), @"(?<=[.!?])\s+");
        int questionIndex = Array.FindLastIndex(sentences, sentence => sentence.EndsWith('?'));
        if (questionIndex < 0) throw new InvalidOperationException("MissingStoryQuestion");
        var facts = sentences.Take(questionIndex).Select((text, i) => new NarrativeClause("fact_" + i, text)).ToArray();
        string question = string.Join(" ", sentences.Skip(questionIndex));
        string NormalizePhrasing(string text)
        {
            text = Regex.Replace(text, @"\{(f\d+)\}", m => "{" + ids[m.Groups[1].Value] + "}");
            foreach (var (spelling, id) in replacements)
                text = Regex.Replace(text, @"(?<![\p{L}\p{N}])" + Regex.Escape(spelling)
                    + @"(?![\p{L}\p{N}])", "{" + id + "}");
            return text;
        }
        string[] originalSentences = Regex.Split(projectedTemplate.Trim(), @"(?<=[.!?])\s+");
        var factPhrasings = facts.Select((f, i) => (f.Role, Choices: capture.Phrasings(originalSentences[i])
            .Select(NormalizePhrasing).Distinct(StringComparer.Ordinal).ToArray())).ToDictionary(f => f.Role, f => f.Choices);
        var questionPhrasings = capture.Phrasings(string.Join(" ", originalSentences.Skip(questionIndex)))
            .Select(NormalizePhrasing).Distinct(StringComparer.Ordinal).ToArray();
        var leadPhrasings = steps.Select((s, i) => (s.Id, Choices: capture.Phrasings(originalLeads[i])
            .Select(NormalizePhrasing).Distinct(StringComparer.Ordinal).ToArray())).ToDictionary(s => s.Id, s => s.Choices);
        // The signature excludes values and includes context/roles/units/target.
        string schema = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            c.Family + "/" + c.Story!.Variant + "/" + c.Tier + "/" + c.Language + "\n" + context + "\n" + template
            + "\n" + string.Join("/", quantities.Select(v => v.Role + (v.Id.StartsWith('v') && !v.Role.StartsWith("motion_actor_", StringComparison.Ordinal) ? "=" + v.Value : "")))
            + "\n" + string.Join("/", steps.Select(s => s.Lead))
            + (c.Family is BankQuestionFamily.Remainder or BankQuestionFamily.Time or BankQuestionFamily.FractionQuantity
                ? "\nanswers:" + string.Join("/", q.ElementaryProblem!.Answers.Select(a => a.Label + "=" + a.Unit)) : ""))));
        return new(q, context, subject, unit, answer, schema, quantities, facts, question, steps,
            factPhrasings, questionPhrasings, leadPhrasings);
    }
}

internal sealed record NarrativeLesson(ArithmeticQuizQuestion QuestionModel, string Context, string Subject, string Unit,
    string Answer, string Schema, IReadOnlyList<NarrativeQuantity> Quantities, IReadOnlyList<NarrativeClause> Facts,
    string Question, IReadOnlyList<NarrativeStep> Steps, IReadOnlyDictionary<string, string[]> FactPhrasings,
    string[] QuestionPhrasings, IReadOnlyDictionary<string, string[]> LeadPhrasings);
