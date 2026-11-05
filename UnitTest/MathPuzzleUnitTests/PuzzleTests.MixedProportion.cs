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
                 { null, ProportionQuizType.Direct, ProportionQuizType.Inverse, ProportionQuizType.UnitaryGroupCount })
        {
            QuizProblemRequest request = catalog.Resolve(proportionIndex, null, null, selection,
                null, null, null, null, null, CurriculumTier.OneStar);
            Require(request.Kind == QuizProblemKind.Proportion && request.ProportionType == selection,
                "The catalog lost a mixed or fixed proportion selection.");
        }

        var essayValidator = new EssayAnswerValidator(new BasicArithmeticEngine());
        int count = 0;
        foreach (CurriculumTier tier in Enum.GetValues<CurriculumTier>())
        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        foreach (ArithmeticQuizMode mode in Enum.GetValues<ArithmeticQuizMode>())
        foreach (bool contextual in new[] { false, true })
        {
            AppLanguageManager.CurrentLanguage = language;
            var generator = new ProportionQuizGenerator(new Random(
                92002 + (int)tier * 100 + (int)language * 10 + (int)mode * 2 + (contextual ? 1 : 0)));
            QuizProblemRequest request = catalog.Resolve(proportionIndex, null, null, null,
                null, null, null, null, null, tier);
            var curriculum = new QuizCurriculumContext(tier, false);
            var seen = new HashSet<ProportionQuizType>();
            for (int sample = 0; sample < 64; sample++)
            {
                ArithmeticQuizQuestion question = contextual
                    ? generator.GenerateContract(mode, request.ProportionType, language, curriculum)
                    : generator.GenerateAlgorithm(mode, request.ProportionType, language, curriculum);
                ProportionQuizContract contract = question.ProportionProblem!;
                string label = $"Mixed proportion/{tier}/{language}/{mode}/{contextual}/{sample}";
                Require(contract.Type is ProportionQuizType.Direct or ProportionQuizType.Inverse or ProportionQuizType.UnitaryGroupCount,
                    $"{label}: the generated contract must contain a concrete relationship.");
                seen.Add(contract.Type);
                BigInteger expected = contract.Type switch
                {
                    ProportionQuizType.Direct => (BigInteger)contract.B * contract.C / contract.A,
                    ProportionQuizType.Inverse => (BigInteger)contract.A * contract.B / contract.C
                        - (contract.AsksForAdditionalPeople ? contract.A : 0),
                    ProportionQuizType.UnitaryGroupCount => (BigInteger)contract.A * contract.C / contract.B,
                    _ => throw new ArgumentOutOfRangeException()
                };
                Require(question.CorrectAnswer == expected && contract.CorrectAnswer == expected,
                    $"{label}: the answer used the wrong relationship.");
                CheckIntegerAnswerMode(question, label);
                string worked = ProportionQuizSolutionFormatter.Format(contract, language, CultureInfo.InvariantCulture);
                string[] solution = worked.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
                Require(solution.Length == (contract.Type == ProportionQuizType.UnitaryGroupCount ? 5 : 3),
                    $"{label}: worked solution must include the required calculation steps and answer.");
                if (mode == ArithmeticQuizMode.Essay)
                {
                    var submission = EssayCombinedInputParser.Parse(worked, true, preserveAllCalculations: true);
                    Require(essayValidator.Validate(question, submission.Solution, submission.Equation, submission.Answer).IsCorrect,
                        $"{label}: correct essay rejected for the sampled relationship.");
                }
                count++;
            }
            Require(seen.SetEquals(tier == CurriculumTier.OneStar ? new[] { ProportionQuizType.Direct } : tier == CurriculumTier.FiveStars ? new[] { ProportionQuizType.Inverse } : Enum.GetValues<ProportionQuizType>()),
                $"{tier}/{language}/{mode}/{contextual}: mixed mode did not match the tier-appropriate relationships.");

            // Choosing a fixed type again must stop the random relationship selection.
            foreach (ProportionQuizType type in Enum.GetValues<ProportionQuizType>())
            {
                request = catalog.Resolve(proportionIndex, null, null, type,
                    null, null, null, null, null, tier);
                ArithmeticQuizQuestion question = contextual
                    ? generator.GenerateContract(mode, request.ProportionType, language, curriculum)
                    : generator.GenerateAlgorithm(mode, request.ProportionType, language, curriculum);
                Require(question.ProportionProblem!.Type == type,
                    "A fixed proportion selection remained mixed.");
            }
        }
        Console.WriteLine($"  Checked {count} mixed proportion questions, both sources and fixed selections.");
    }
}
