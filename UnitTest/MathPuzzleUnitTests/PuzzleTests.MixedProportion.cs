using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckMixedProportionSelection()
    {
        var catalog = new QuizProblemTypeCatalog(new Random(92001));
        int proportionIndex = catalog.Options.ToList().FindIndex(option =>
            option.FixedRequest?.Kind == QuizProblemKind.Proportion);
        foreach (ProportionQuizType? selection in new ProportionQuizType?[]
                 { null, ProportionQuizType.Direct, ProportionQuizType.Inverse })
        {
            QuizProblemRequest request = catalog.Resolve(proportionIndex, null, null, selection,
                null, null, null, null, null, CurriculumTier.OneStar);
            Require(request.Kind == QuizProblemKind.Proportion && request.ProportionType == selection,
                "The catalog lost a mixed or fixed proportion selection.");
        }

        var aiValidator = new LlmWordProblemValidator();
        var essayValidator = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;
        foreach (CurriculumTier tier in Enum.GetValues<CurriculumTier>())
        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        foreach (ArithmeticQuizMode mode in Enum.GetValues<ArithmeticQuizMode>())
        foreach (bool ai in new[] { false, true })
        {
            AppLanguageManager.CurrentLanguage = language;
            var generator = new ProportionQuizGenerator(new Random(
                92002 + (int)tier * 100 + (int)language * 10 + (int)mode * 2 + (ai ? 1 : 0)));
            QuizProblemRequest request = catalog.Resolve(proportionIndex, null, null, null,
                null, null, null, null, null, tier);
            var curriculum = new QuizCurriculumContext(tier, false);
            var seen = new HashSet<ProportionQuizType>();
            for (int sample = 0; sample < 64; sample++)
            {
                ArithmeticQuizQuestion question = ai
                    ? generator.GenerateContract(mode, request.ProportionType, language, curriculum)
                    : generator.GenerateAlgorithm(mode, request.ProportionType, language, curriculum);
                ProportionQuizContract contract = question.ProportionProblem!;
                string label = $"Mixed proportion/{tier}/{language}/{mode}/{ai}/{sample}";
                Require(contract.Type is ProportionQuizType.Direct or ProportionQuizType.Inverse,
                    $"{label}: the generated contract must contain a concrete relationship.");
                seen.Add(contract.Type);
                BigInteger expected = contract.IsDirect ? (BigInteger)contract.B * contract.C / contract.A :
                    (BigInteger)contract.A * contract.B / contract.C;
                if (contract.AsksForAdditionalPeople) expected -= contract.A;
                Require(question.CorrectAnswer == expected && contract.CorrectAnswer == expected,
                    $"{label}: the answer used the wrong relationship.");
                CheckIntegerAnswerMode(question, label);
                string[] solution = ProportionQuizSolutionFormatter.Format(contract, language,
                    CultureInfo.InvariantCulture).Split(Environment.NewLine);
                Require(solution.Length == 3, $"{label}: expected one solution, calculation and answer.");
                if (mode == ArithmeticQuizMode.Essay)
                    Require(essayValidator.Validate(question, solution[0], solution[1],
                            $"{contract.CorrectAnswer} {contract.AnswerUnit}").IsCorrect,
                        $"{label}: correct essay rejected for the sampled relationship.");
                if (ai)
                {
                    Require(LlmQuizPromptBuilder.BuildProportionUserPrompt(contract, language, null).Length > 100,
                        $"{label}: no AI prompt for the sampled relationship.");
                    var validation = aiValidator.ValidateProportion(
                        DraftFromAlgorithm(QuizProblemKind.Proportion, question), contract, language);
                    Require(validation.IsValid,
                        $"{label}/{contract.Scenario}: AI rejected a matching proportion story: " +
                        $"{validation.ErrorCode}; {contract.ProblemText}");
                }
                count++;
            }
            Require(seen.SetEquals(Enum.GetValues<ProportionQuizType>()),
                $"{tier}/{language}/{mode}/{ai}: mixed mode never selected both relationships.");

            // Choosing a fixed type again must stop the random relationship selection.
            foreach (ProportionQuizType type in Enum.GetValues<ProportionQuizType>())
            {
                request = catalog.Resolve(proportionIndex, null, null, type,
                    null, null, null, null, null, tier);
                ArithmeticQuizQuestion question = ai
                    ? generator.GenerateContract(mode, request.ProportionType, language, curriculum)
                    : generator.GenerateAlgorithm(mode, request.ProportionType, language, curriculum);
                Require(question.ProportionProblem!.Type == type,
                    "A fixed proportion selection remained mixed.");
            }
        }
        Console.WriteLine($"  Checked {count} mixed proportion questions, both sources and fixed selections.");
    }
}
