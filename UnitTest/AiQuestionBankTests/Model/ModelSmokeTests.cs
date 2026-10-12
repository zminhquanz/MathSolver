using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;

internal static class ModelSmokeTests
{
    internal static async Task RunAsync(string modelPath)
    {
        await using var session = new ModelTestSession();
        await session.LoadAsync(modelPath);
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var operation in Enum.GetValues<ArithmeticOperation>())
        {
            var contract = BasicQuestionContract.CreateTemplate(operation, CurriculumTier.OneStar, language);
            string? correction = null;
            bool passed = false;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                var native = await session.GenerateAsync(contract, BasicQuestionPrompt.Build(contract, correction), cancellation.Token);
                var validation = native.Validation;
                Console.WriteLine($"{language}/{operation} attempt {attempt}: {validation.ErrorCode ?? "PASS"}\n{native.Raw}");
                correction = validation.ErrorCode;
                if (!validation.IsValid) continue;
                var resolved = validation.Contract ?? contract;
                for (int seed = 0; seed < 4; seed++)
                {
                    var facts = resolved.FreshFacts(new Random(712 + seed));
                    var word = validation.Draft!.ToWordProblem(facts);
                    var quiz = facts.ToPracticeQuestion(word, ArithmeticQuizMode.Essay);
                    string equation = $"{facts.Left} {BasicArithmeticEngine.GetSymbol(facts.Operation)} {facts.Right} = {facts.Answer} {word.AnswerUnit}";
                    Check(!word.ProblemText.Contains('{') && new EssayAnswerValidator(new BasicArithmeticEngine())
                        .Validate(quiz, word.SolutionLead, equation, $"{facts.Answer} {word.AnswerUnit}").IsCorrect,
                        "Live template failed fresh C# instantiation/grading.");
                }
                passed = true;
                break;
            }
            Check(passed, "Live model did not produce a valid " + operation + " question.");
        }
        Console.WriteLine("PASS live GGUF templates in both languages for all four operations, streamed JSON, token metrics, fresh C# values and essay grading.");
    }
}
