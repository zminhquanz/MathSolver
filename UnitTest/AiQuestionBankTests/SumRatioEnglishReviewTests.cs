using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static class SumRatioEnglishReviewTests
{
    internal static void Run()
    {
        int clear = 0, rejected = 0, pending = 0;
        foreach (string profile in SumRatioPilot.Profiles(AppLanguage.English))
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        {
            var c = ReasoningStoryCatalogue.Create(BankQuestionFamily.TwoNumbers, (int)ElementaryQuizType.SumRatio,
                tier, AppLanguage.English, new(713), profile) with { OpenProse = true };
            var d = ReasoningStoryCatalogue.Draft(c);
            var previousClear = d with
            {
                Facts = [new("fact_0", "The total count of {v2} held by {v0} and {v1} is {f0}."),
                    new("fact_1", "The number of {v2} that {v0} possesses equals {f1} times the number of {v2} that {v1} holds.")],
                Question = "How many {v2} does {v0} have and how many does {v1} have?",
                SolutionLeads = [new("step_0", "The count of {v2} in one equal share."),
                    new("step_1", "The number of {v2} for {v0}."), new("step_2", "The number of {v2} for {v1}.")]
            };
            Require(Validate(c, previousClear).IsValid, "Previously clear native English output must be proved, including relative owners, times, no comma and one equal share.");
            clear++;
            SumRatioOpenTests.CheckPractice(c, previousClear);
            string[] ratios =
            [
                previousClear.Facts![1].Text,
                "The count of {v2} which {v0} owns is {f1} of the count of {v2} that {v1} has.",
                "The number of {v2} held by {v0} is equal to {f1} times {v1}'s number of {v2}.",
                "{v0}'s number of {v2} equals {f1} of the number of {v2} that {v1} possesses."
            ];
            foreach (string ratio in ratios)
            {
                var candidate = d with { Facts = [d.Facts![0], new("fact_1", ratio)] };
                Require(Validate(c, candidate).IsValid, "Clear oriented count ratio missed: " + ratio); clear++;
                Require(Validate(c, candidate with { Facts = [d.Facts[0], new("fact_1", Swap(ratio))] }).ErrorCode == "ChangedRelationOrTarget",
                    "Reversed ownership must be a hard failure, including relative clauses."); rejected++;
                foreach (string prefix in new[] { "It is not true that ", "If ", "Only if ", "It may be that " })
                {
                    Require(Validate(c, candidate with { Facts = [d.Facts[0], new("fact_1", prefix + ratio)] }).NeedsReview,
                        "Negated, conditional or uncertain ratio must never become verified."); pending++;
                }
                foreach (string changed in new[] { ratio.Replace(" is ", " is not ").Replace(" equals ", " does not equal "),
                    ratio.Replace(" possesses ", " does not possess ").Replace(" owns ", " does not own ").Replace(" held by ", " not held by ").Replace("{v0}'s", "not {v0}'s") })
                    if (changed != ratio)
                    { Require(!Validate(c, candidate with { Facts = [d.Facts[0], new("fact_1", changed)] }).IsValid, "Internal negation was ignored."); rejected++; }
            }
            foreach (string question in new[] { previousClear.Question,
                "How many {v2} does {v1} possess, and how many does {v0} hold?",
                "How many {v2} does each of {v0} and {v1} have?" })
            { Require(Validate(c, d with { Question = question }).IsValid, "Separate-owner target missed: " + question); clear++; }
            foreach (string lead in new[] { previousClear.SolutionLeads![0].Text, "Number of {v2} per each equal share", "The count of {v2} in one equal part" })
            {
                var candidate = d with { SolutionLeads = [new("step_0", lead), d.SolutionLeads![1], d.SolutionLeads[2]] };
                Require(Validate(c, candidate).IsValid, "Equal ratio-part heading missed: " + lead); clear++;
                Require(!Validate(c, candidate with { SolutionLeads = [new("step_0", "Not " + lead), d.SolutionLeads[1], d.SolutionLeads[2]] }).IsValid,
                    "A negated solution heading must not be normalized away."); rejected++;
            }
            foreach (var bad in new[]
            {
                previousClear with { Question = "What is the count of {v2} for both {v0} and {v1}?" },
                previousClear with { Question = "How many {v2} do {v0} and {v1} have?" },
                previousClear with { Facts = [previousClear.Facts![0], new("fact_1", previousClear.Facts[1].Text.Replace(" equals ", " does not equal "))] },
                previousClear with { Facts = [new("fact_0", "{v0} and {v1} each have {f0} {v2}."), previousClear.Facts![1]] },
                previousClear with { Question = "What is the total number of {v2} for {v0} and {v1}?" },
                previousClear with { SolutionLeads = [previousClear.SolutionLeads![0], new("step_1", "The number of {v2} for {v1}."), new("step_2", "The number of {v2} for {v0}.")] }
            })
            { Require(!Validate(c, bad).IsValid, "Ambiguous target, changed total/ratio or swapped answer heading passed."); rejected++; }
            Require(Validate(c, previousClear with { Question = "What is the count of {v2} for both {v0} and {v1}?" }).NeedsReview,
                "Clear facts and owner headings cannot repair an ambiguous question; retain review-once routing."); pending++;
        }
        Console.WriteLine($"PASS English sum-ratio consolidation: {clear} clear proofs, {rejected} wrong/ambiguous mutations not verified, {pending} qualified cases pending.");
        var factExamples = new HashSet<string>();
        foreach (string profile in SumRatioPilot.Profiles(AppLanguage.English))
        {
            var c = ReasoningStoryCatalogue.Create(BankQuestionFamily.TwoNumbers, (int)ElementaryQuizType.SumRatio,
                CurriculumTier.ThreeStars, AppLanguage.English, new(713), profile) with { OpenProse = true };
            string prompt = BasicQuestionPrompt.Build(c);
            const string marker = "\nReference JSON:\n";
            string raw = prompt[(prompt.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..];
            var reference = BasicQuestionValidator.Validate(raw, c);
            Require(reference.IsValid, "Each context's prompt reference must retain syntax, owner roles and separate targets.");
            factExamples.Add(string.Join(" / ", reference.Draft!.Facts!.Select(f => f.Text)));
            var own = SumRatioPilot.Contexts(AppLanguage.English).Single(context => SumRatioPilot.Prefix + context.Id == profile);
            foreach (var other in SumRatioPilot.Contexts(AppLanguage.English).Where(context => context.Id != own.Id))
                Require(!System.Text.RegularExpressions.Regex.IsMatch(SumRatioOpenCatalogue.PromptRelation(c),
                    @"\b" + System.Text.RegularExpressions.Regex.Escape(other.Unit) + @"\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                    "A single-context writing guide must not introduce another context's concrete object name.");
        }
        Require(factExamples.Count == 3, "English contexts must not all prime the model with the identical pair of fact sentences.");
    }

    private static string Swap(string text) => text.Replace("{v0}", "{swap}").Replace("{v1}", "{v0}").Replace("{swap}", "{v1}");
    private static BasicDraftValidation Validate(BasicQuestionContract c, BasicQuestionDraft d) => BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d), c);
    private static void Require(bool condition, string message) => TestAssert.That(condition, message);
}
