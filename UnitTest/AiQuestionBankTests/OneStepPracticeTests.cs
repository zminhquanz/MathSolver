using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;

internal static class OneStepPracticeTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    public static void Run()
    {
        int count = 0;
        var math = new BasicArithmeticEngine();
        var grading = new EssayAnswerValidator(math);
        foreach (var template in OneStepQuestionCatalogue.All)
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var facts = template.CreateFacts(language, new Random(831 + count));
            for (int variant = 0; variant < 3; variant++)
            {
                var d = OneStepQuestionCatalogue.Draft(facts, variant);
                string tag = $"{template.Operation}/{template.Tier}/{template.SceneId}/{template.Structure}/{language}/{variant}";
                var validated = BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d), facts);
                Check(facts.IsValid && validated.IsValid && validated.Contract == facts, $"Built-in pattern failed shared AI validation: {tag}: {validated.ErrorCode}");
                var word = d.ToWordProblem(facts);
                Check(!word.ProblemText.Contains('{') && !word.SolutionLead.Contains('{'), "Unfilled slots: " + tag);
                foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                    Check(new ArithmeticQuizValidator(math).Validate(facts.ToPracticeQuestion(word, mode)).IsValid, "Invalid answer mode: " + tag);
                var essay = facts.ToPracticeQuestion(word, ArithmeticQuizMode.Essay);
                string equation = $"{facts.Left} {BasicArithmeticEngine.GetSymbol(facts.Operation)} {facts.Right} = {facts.Answer} {word.AnswerUnit}";
                Check(grading.Validate(essay, word.SolutionLead, equation, $"{facts.Answer} {word.AnswerUnit}").IsCorrect, "Correct one-step essay rejected: " + tag);
                Check(!grading.Validate(essay, word.SolutionLead, equation, $"{facts.Answer + 1} {word.AnswerUnit}").IsCorrect, "Incorrect answer accepted: " + tag);
                count++;
            }
        }

        // Assert mathematical meaning and units independently of the answer generator.
        foreach (var structure in new[] { BasicQuestionStructure.SubComparisonLess, BasicQuestionStructure.SubComparisonInverse,
            BasicQuestionStructure.FindPart, BasicQuestionStructure.CompareFactor })
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var operation = BasicQuestionTemplates.Operation(structure);
            var c = ArithmeticQuestionCatalogue.Create(operation, CurriculumTier.ThreeStars, language, new Random(715), "library", structure, "books")
                with { Left = 120, Right = 30 };
            var d = OneStepQuestionCatalogue.Draft(c);
            Check(c.IsValid && c.Answer == (structure == BasicQuestionStructure.CompareFactor ? 4 : 90), "Wrong extended relation arithmetic.");
            Check(c.AnswerUnit == (structure == BasicQuestionStructure.CompareFactor ? language == AppLanguage.Vietnamese ? "lần" : "times"
                : language == AppLanguage.Vietnamese ? "quyển sách" : "books"), "Wrong extended answer dimension.");
            string Swap(string text) => text.Replace("{name}", "{swap}").Replace("{other}", "{name}").Replace("{swap}", "{other}");
            foreach (var wrong in new[] { d with { GivenB = Swap(d.GivenB) }, d with { Question = Swap(d.Question) },
                d with { SolutionLead = Swap(d.SolutionLead!) }, d with { GivenA = d.GivenA + (language == AppLanguage.Vietnamese ? " rồi bán đi." : " then sells them.") } })
                Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(wrong), c).IsValid, "Reversed role or extra step accepted.");
            Check(BasicQuestionPrompt.Build(c).Contains(QuestionBankStore.SerializeDraft(d)) && GgufQuestionRuntime.BuildGrammar(c).Length > 0,
                "AI prompt/grammar lost the shared role example.");
        }

        foreach (var operation in Enum.GetValues<ArithmeticOperation>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var cycle = new OneStepQuestionCycle(new Random(945));
            var questions = Enumerable.Range(0, 64).Select(_ => cycle.Next(operation, tier, language, ArithmeticQuizMode.Essay)).ToArray();
            Check(questions.All(q => q.WordProblem is not null && q.Expression.Operation == operation && new ArithmeticQuizValidator(math).Validate(q).IsValid),
                "Built-in practice escaped the requested operation or lost prose.");
            Check(questions.Select(q => q.WordProblem!.ProblemText).Distinct().Count() > 20
                && questions.Select(q => q.Expression).Distinct().Count() > 1, "Built-in practice repeated fixed text or operands.");
        }
        Console.WriteLine($"PASS {count} reviewed bilingual one-step patterns/variants, shared AI validation, all answer modes, correct/incorrect essay grading and bounded variety");
    }
}
