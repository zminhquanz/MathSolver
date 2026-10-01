using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckExpressionAi()
    {
        var generator = new ExpressionQuizGenerator(new Random(20261001));
        var modelValidator = new LlmWordProblemValidator();
        var engine = new BasicArithmeticEngine();
        var grader = new EssayAnswerValidator(engine);
        int count = 0;
        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        {
            AppLanguageManager.CurrentLanguage = language;
            var contexts = new HashSet<string>();
            var mixedTypes = new HashSet<ExpressionQuizType>();
            foreach (CurriculumTier tier in Enum.GetValues<CurriculumTier>())
            foreach (ExpressionQuizType type in Enum.GetValues<ExpressionQuizType>())
            foreach (ArithmeticQuizMode mode in Enum.GetValues<ArithmeticQuizMode>())
            for (int sample = 0; sample < 8; sample++)
            {
                ArithmeticQuizQuestion original = generator.GenerateContract(mode, type, language, tier);
                ExpressionQuizContract contract = original.ExpressionProblem!;
                ExpressionStoryContract story = contract.Story!;
                contexts.Add(story.ContextName);
                Require(contract.Type == type && story.Language == language && original.WordProblem is null,
                    "The AI contract lost the selected subtype/language or bypassed model validation.");

                // Evaluate a verbal plan independently: only operation phrases are
                // replaced, leaving its C#-generated nesting and ordered operands.
                string spokenExpression = DecodeExpressionPlan(story.CalculationPlan, language);
                var checkedResult = engine.EvaluateIntegerExpression(spokenExpression);
                Require(new ReducedFraction(checkedResult.ResultNumerator, checkedResult.ResultDenominator) ==
                        contract.CorrectAnswer, $"Verbal plan disagrees with the exact answer: {story.CalculationPlan}");
                Require(Regex.Matches(story.CalculationPlan, @"\d+(?:/\d+)?").Count == (int)tier + 2,
                    "The verbal plan lost operands.");

                var reference = story.ReferenceProblem;
                var draft = new LlmWordProblemDraft
                {
                    ProblemText = reference.ProblemText, SubjectName = reference.SubjectName,
                    AnswerUnit = reference.AnswerUnit, SolutionLead = reference.SolutionLead
                };
                string json = JsonSerializer.Serialize(draft);
                Require(LlmWordProblemParser.TryParse(json, out var parsed, out _, out _,
                        allowEmptyAnswerUnit: true) && parsed is not null,
                    "Expression JSON did not pass the production parser.");
                Require(!LlmWordProblemParser.TryParse(json, out _, out _, out _),
                    "Ordinary word-problem parsing must still require an answer unit.");
                Require(!LlmWordProblemParser.TryParse(json.Replace("\"answer_unit\":\"\"", "\"answer_unit\":null"),
                    out _, out _, out _, allowEmptyAnswerUnit: true), "Expression parsing accepted a null unit field.");
                var validation = modelValidator.ValidateExpression(parsed!, contract, language);
                Require(validation.IsValid && validation.WordProblem is not null,
                    $"Reference expression was rejected: {validation.ErrorCode}: {reference.ProblemText}");
                var question = original with { WordProblem = validation.WordProblem };
                Require(EssayAnswerValidator.RequiresSolution(question) &&
                        EssayAnswerValidator.GetExpectedUnit(question) == string.Empty,
                    "AI expressions need a solution sentence but no physical unit.");
                string answer = contract.CorrectAnswer.ToString();
                string equation = contract.ExpressionText + " = " + answer;
                string solution = ElementaryWordProblemSolutionFormatter.Format(question, language,
                    CultureInfo.InvariantCulture);
                string[] solutionLines = solution.Split(Environment.NewLine);
                Require(solutionLines.Length == 3 && solutionLines[1] == equation &&
                        solutionLines[2] == (language == AppLanguage.Vietnamese ? "Đáp số: " : "Answer: ") + answer,
                    "The displayed AI solution must include one lead, the full expression and the exact answer.");
                if (mode == ArithmeticQuizMode.Essay)
                {
                    Require(grader.Validate(question, reference.SolutionLead, equation, answer).IsCorrect,
                        $"Correct AI expression work was rejected: {equation}");
                    Require(grader.Validate(question, reference.SolutionLead, spokenExpression + " = " + answer,
                        answer).IsCorrect, $"Correct grouping from the AI verbal plan was rejected: {spokenExpression} vs {contract.ExpressionText}.");
                    foreach (string work in new[] { reference.SolutionLead + Environment.NewLine + equation,
                                 reference.SolutionLead + " " + equation })
                    {
                        var input = EssayCombinedInputParser.Split(work, requiresSolution: true);
                        Require(grader.Validate(question, input.Solution, input.Equation, answer).IsCorrect,
                            "The combined AI expression editor failed to separate valid work.");
                    }
                    Require(grader.Validate(question, "", equation, answer).SolutionError == EssayAnswerError.MissingSolution,
                        "An empty AI solution was accepted.");
                    Require(grader.Validate(question, language == AppLanguage.Vietnamese ? "Diện tích là:" : "The area is:",
                        equation, answer).SolutionError == EssayAnswerError.WrongSolutionContent,
                        "An unrelated AI solution was accepted.");
                    Require(!grader.Validate(question, reference.SolutionLead, "0 + " + answer + " = " + answer, answer)
                        .EquationIsCorrect, "An unrelated equation with the right answer was accepted.");
                    if (contract.UsesFractions)
                    {
                        string equivalent = $"{contract.CorrectAnswer.Numerator * 2}/{contract.CorrectAnswer.Denominator * 2}";
                        Require(grader.Validate(question, reference.SolutionLead,
                            contract.ExpressionText + " = " + equivalent, equivalent).IsCorrect,
                            "An equivalent fraction answer was rejected.");
                    }
                }
                else if (mode == ArithmeticQuizMode.MultipleChoice)
                    Require(contract.Choices.Count == 4 && contract.Choices.Distinct().Count() == 4 &&
                            contract.Choices.Contains(contract.CorrectAnswer), "AI choices lost their exact answers.");
                else
                    Require(question.PresentedEquationIsCorrect == (contract.PresentedAnswer == contract.CorrectAnswer),
                        "AI true/false grading lost its rational answer.");

                string prompt = LlmQuizPromptBuilder.BuildExpressionUserPrompt(contract, language, null);
                Require(prompt.Contains(story.CalculationPlan) && prompt.Contains(story.ContextName) &&
                        prompt.Contains("\"answer_unit\": \"\""), "The expression prompt lost its authoritative plan or empty unit.");
                CheckRejected(draft.ProblemText!.Replace(story.CalculationPlan, "999"), "ExpressionPlanMismatch");
                CheckRejected(draft.ProblemText + " 123", "ExpressionExtraFacts");
                CheckRejected(draft.ProblemText + " " + story.CalculationPlan, "ExpressionPlanMismatch");
                CheckRejected(draft.ProblemText, "AnswerUnitMismatch", unit: "kg");
                CheckRejected(draft.ProblemText, "SolutionLeadContainsCalculation", lead: answer);
                // Changing only an operation leaves every numeric fact intact.
                string wrongPlan = story.CalculationPlan.Replace(language == AppLanguage.Vietnamese ? "tổng" : "sum",
                    language == AppLanguage.Vietnamese ? "tích" : "product", StringComparison.Ordinal);
                if (wrongPlan != story.CalculationPlan)
                    CheckRejected(draft.ProblemText.Replace(story.CalculationPlan, wrongPlan), "ExpressionPlanMismatch");

                mixedTypes.Add(generator.GenerateContract(mode, null, language, tier).ExpressionProblem!.Type);
                count++;

                void CheckRejected(string problem, string expected, string? unit = null, string? lead = null)
                {
                    var bad = new LlmWordProblemDraft
                    {
                        ProblemText = problem, SubjectName = draft.SubjectName,
                        AnswerUnit = unit ?? draft.AnswerUnit, SolutionLead = lead ?? draft.SolutionLead
                    };
                    var result = modelValidator.ValidateExpression(bad, contract, language);
                    Require(!result.IsValid && result.ErrorCode == expected,
                        $"Expected {expected}, got {result.ErrorCode}.");
                }
            }
            Require(contexts.Count == 8 && mixedTypes.Count == 4,
                "AI expression Mixed did not cover all themes/subtypes.");
        }
        // Same answer and numbers do not make a changed operation tree valid.
        var repeated = generator.GenerateContract(ArithmeticQuizMode.Essay, ExpressionQuizType.Integer,
            AppLanguage.English, CurriculumTier.OneStar);
        var changedContract = repeated.ExpressionProblem! with
        {
            ExpressionText = "1 × (2 + 3)", CorrectAnswer = new ReducedFraction(5, 1)
        };
        var changedQuestion = repeated with
        {
            ExpressionProblem = changedContract, CorrectAnswer = 5,
            WordProblem = changedContract.Story!.ReferenceProblem
        };
        Require(!grader.Validate(changedQuestion, "The result is:", "1 × 2 + 3 = 5", "5").EquationIsCorrect,
            "A different operation tree with a coincidentally equal answer was accepted.");
        Console.WriteLine($"  Checked {count} bilingual AI expression contracts, verbal plans, model-output validation and grading.");
    }

    private static string DecodeExpressionPlan(string plan, AppLanguage language)
    {
        // Prefix phrases are distinct for each operation. Strip the phrase,
        // find the connector at this node's depth, then decode each child.
        plan = plan.Trim();
        if (Regex.IsMatch(plan, @"^\d+(?:/\d+)?$")) return $"({plan})";
        if (plan.StartsWith('(') && plan.EndsWith(')'))
            plan = plan[1..^1];
        (string Prefix, string Connector, string Operator)[] rules = language == AppLanguage.Vietnamese
            ? [("tổng của ", " và ", "+"), ("hiệu của ", " và ", "−"),
               ("tích của ", " và ", "×"), ("thương của ", " chia cho ", "÷")]
            : [("the sum of ", " and ", "+"), ("the difference between ", " and ", "−"),
               ("the product of ", " and ", "×"), ("the quotient of ", " divided by ", "÷")];
        foreach (var rule in rules)
        {
            if (!plan.StartsWith(rule.Prefix, StringComparison.Ordinal)) continue;
            string children = plan[rule.Prefix.Length..];
            int depth = 0;
            for (int index = 0; index < children.Length; index++)
            {
                if (children[index] == '(') depth++;
                if (children[index] == ')') depth--;
                if (depth == 0 && children.AsSpan(index).StartsWith(rule.Connector, StringComparison.Ordinal))
                    return $"({DecodeExpressionPlan(children[..index], language)} {rule.Operator} " +
                        $"{DecodeExpressionPlan(children[(index + rule.Connector.Length)..], language)})";
            }
        }
        throw new InvalidOperationException("Cannot decode verbal plan: " + plan);
    }
}
