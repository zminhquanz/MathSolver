using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckAiEssayFormatting()
    {
        CheckAiSolutionCues();
        CheckPaintAreaEssayValidation();
        CheckProportionFormatting();
        CheckMotionFormatting();
        CheckAverageFormatting();
        CheckPercentageFormatting();
    }

    private static void CheckPaintAreaEssayValidation()
    {
        var validator = new EssayAnswerValidator(new BasicArithmeticEngine());
        var contract = new ProportionQuizContract(
            ProportionQuizType.Direct, ProportionScenarioKind.PaintArea,
            2, 60, 8, 240, "m²", "diện tích tường",
            "2 thùng sơn được 60 m² tường. Hỏi 8 thùng sơn được bao nhiêu m² tường?");
        var algorithm = new ArithmeticQuizQuestion(
            new IntegerArithmeticExpression(60, ArithmeticOperation.Multiply, 8),
            ArithmeticQuizMode.Essay, 240, null, null, [],
            ProportionProblem: contract);
        ArithmeticQuizQuestion ai = algorithm with
        {
            WordProblem = new MathWordProblem(
                contract.ProblemText, "Diện tích tường sơn được là:",
                "m²", "thùng sơn")
        };

        foreach (ArithmeticQuizQuestion question in new[] { algorithm, ai })
        {
            EssayAnswerValidationResult accepted = validator.Validate(
                question, "Số m2 tường mà 8 thùng sơn được là:",
                "60 x 8 / 2 = 240 m2", "240m2");
            Require(accepted.IsCorrect,
                $"Paint-area proportion rejected m2 notation: " +
                $"{accepted.SolutionError}/{accepted.EquationError}/{accepted.AnswerError}.");

            Require(validator.Validate(question, "Số tường sơn được là:",
                    "60 x 8 / 2 = 240 m²", "240m²").SolutionIsCorrect,
                "Paint-area proportion rejected the requested wall object.");
            Require(!validator.Validate(question, "Số m là:",
                    "60 x 8 / 2 = 240 m²", "240m²").SolutionIsCorrect,
                "Paint-area proportion confused length with area in the solution.");
            Require(validator.Validate(question, "Số m² tường là:",
                    "60 x 8 / 2 = 240 m", "240m²").EquationError ==
                    EssayAnswerError.WrongEquationUnit,
                "Paint-area proportion accepted a length unit in the equation.");
            Require(validator.Validate(question, "Số m² tường là:",
                    "60 x 8 / 2 = 240 m²", "240m").AnswerError ==
                    EssayAnswerError.WrongAnswerUnit,
                "Paint-area proportion accepted a length unit in the answer.");
        }
    }

    private static void CheckAiSolutionCues()
    {
        var validator = new EssayAnswerValidator(new BasicArithmeticEngine());
        ArithmeticQuizQuestion arithmetic = Generate(
            QuizProblemKind.Arithmetic, ArithmeticOperation.Add,
            ArithmeticQuizMode.Essay, AppLanguage.Vietnamese, 15001)
            with
            {
                WordProblem = new MathWordProblem(
                    "Lan có trứng. Hỏi có bao nhiêu quả trứng?",
                    "Số trứng là:", "quả trứng", "Lan")
            };

        Require(validator.Validate(arithmetic, "Số trứng là:", null, null)
                .SolutionIsCorrect,
            "AI word problem rejected the requested object in the solution.");
        Require(!validator.Validate(arithmetic, "Lan tính là:", null, null)
                .SolutionIsCorrect,
            "AI word problem accepted a person's name without the requested object.");

        ArithmeticQuizQuestion mass = arithmetic with
        {
            WordProblem = arithmetic.WordProblem! with { AnswerUnit = "gam" }
        };
        Require(validator.Validate(mass, "Khối lượng (g) là:", null, null)
                .SolutionIsCorrect,
            "AI word problem rejected the matching unit abbreviation in the solution.");
    }

    private static void CheckProportionFormatting()
    {
        ProportionQuizContract[] contracts =
        [
            new(ProportionQuizType.Direct, ProportionScenarioKind.Clothing,
                10, 30, 3, 9, "mét vải", "vải", "May 3 bộ cần bao nhiêu mét vải?"),
            new(ProportionQuizType.Inverse, ProportionScenarioKind.WorkersDays,
                6, 12, 3, 24, "ngày", "ngày", "Cần bao nhiêu ngày?"),
            new(ProportionQuizType.Inverse, ProportionScenarioKind.FoodAdditionalPeople,
                6, 12, 3, 18, "người", "người", "Có thêm bao nhiêu người?",
                AsksForAdditionalPeople: true)
        ];
        string[] expectedEquations =
        [
            "30 ÷ 10 × 3 = 9 mét vải",
            "6 × 12 ÷ 3 = 24 ngày",
            "6 × 12 ÷ 3 − 6 = 18 người"
        ];

        for (int index = 0; index < contracts.Length; index++)
        {
            ProportionQuizContract contract = contracts[index];
            var wordProblem = new MathWordProblem(
                contract.ProblemText,
                "Giá trị ứng với 1 đơn vị là:",
                contract.AnswerUnit,
                contract.SubjectName);
            var question = new ArithmeticQuizQuestion(
                new IntegerArithmeticExpression(1, ArithmeticOperation.Add, 1),
                ArithmeticQuizMode.Essay, contract.CorrectAnswer,
                null, null, [], WordProblem: wordProblem,
                ProportionProblem: contract);

            string algorithm = ProportionQuizSolutionFormatter.Format(
                contract, AppLanguage.Vietnamese, CultureInfo.InvariantCulture);
            string ai = ElementaryWordProblemSolutionFormatter.Format(
                question, AppLanguage.Vietnamese, CultureInfo.InvariantCulture);

            Require(algorithm == ai,
                "Algorithm and AI proportion solutions should use the same format.");
            Require(ai.Split(Environment.NewLine).Length == 3 &&
                    ai.Contains(expectedEquations[index], StringComparison.Ordinal) &&
                    !ai.Contains("Giá trị ứng với 1 đơn vị", StringComparison.Ordinal),
                $"Proportion solution should have one specific lead and one equation: {ai}");
        }
    }

    private static void CheckMotionFormatting()
    {
        bool foundSameTimeUnit = false;
        bool foundConvertedTimeUnit = false;

        for (int seed = 0; seed < 1000 &&
             (!foundSameTimeUnit || !foundConvertedTimeUnit); seed++)
        {
            MotionQuizContract motion = new MotionQuizGenerator(new Random(seed))
                .GenerateContract(ArithmeticQuizMode.Essay,
                    AppLanguage.Vietnamese, MotionQuizType.Basic)
                .MotionProblem!;
            if (motion.QuestionKind != MotionQuestionKind.BasicDistance)
            {
                continue;
            }

            bool needsTimeConversion = motion.ProblemText.Contains(
                "phút", StringComparison.Ordinal);
            Require(motion.EquationText.Contains(" ÷ 60 ", StringComparison.Ordinal) ==
                    needsTimeConversion,
                $"Motion equation has the wrong time conversion: {motion.EquationText}");
            Require(!motion.EquationText.Contains(" × 1 ", StringComparison.Ordinal),
                $"Motion equation contains a redundant × 1: {motion.EquationText}");

            var question = new ArithmeticQuizQuestion(
                new IntegerArithmeticExpression(1, ArithmeticOperation.Add, 1),
                ArithmeticQuizMode.Essay, motion.CorrectAnswer,
                null, null, [],
                WordProblem: new MathWordProblem(
                    motion.ProblemText, "Quãng đường đi được là:",
                    motion.AnswerUnit, motion.SubjectName),
                MotionProblem: motion);
            string ai = ElementaryWordProblemSolutionFormatter.Format(
                question, AppLanguage.Vietnamese, CultureInfo.InvariantCulture);
            Require(ai.Split(Environment.NewLine).Length == 3 &&
                    ai.Contains(motion.EquationText, StringComparison.Ordinal) &&
                    !ai.Contains(" × 1 ", StringComparison.Ordinal),
                $"AI motion solution should use one equation without × 1: {ai}");

            foundConvertedTimeUnit |= needsTimeConversion;
            foundSameTimeUnit |= !needsTimeConversion;
        }

        Require(foundSameTimeUnit && foundConvertedTimeUnit,
            "Motion test did not cover both matching and converted time units.");
    }

    private static void CheckAverageFormatting()
    {
        var validator = new EssayAnswerValidator(new BasicArithmeticEngine());

        foreach (AppLanguage language in new[]
                 { AppLanguage.Vietnamese, AppLanguage.English })
        {
            foreach (AverageQuizType type in Enum.GetValues<AverageQuizType>())
            {
                ArithmeticQuizQuestion algorithm = new AverageQuizGenerator(new Random(71))
                    .GenerateAlgorithm(ArithmeticQuizMode.Essay, type, language);
                AverageQuizContract average = algorithm.AverageProblem!;
                string[] algorithmLines = average.SolutionText.Split(Environment.NewLine);

                string expectedAnswerLine = language == AppLanguage.Vietnamese
                    ? $"Đáp số: {average.CorrectAnswer} {average.AnswerUnit}"
                    : $"Answer: {average.CorrectAnswer} {average.AnswerUnit}";

                Require(algorithmLines.Length == 3 &&
                        !algorithmLines[0].Contains('=') &&
                        algorithmLines[1] == $"{average.EquationText} {average.AnswerUnit}" &&
                        algorithmLines[2] == expectedAnswerLine,
                    $"{language}/{type} should show one lead, equation and answer: {average.SolutionText}");

                if (type is AverageQuizType.Direct or AverageQuizType.IndirectData or
                    AverageQuizType.TwoGroups)
                {
                    Require(average.EquationText.Contains('(') &&
                            average.EquationText.Contains(" ÷ ", StringComparison.Ordinal),
                        $"{language}/{type} did not combine the average into one equation.");
                }

                ArithmeticQuizQuestion ai = algorithm with
                {
                    WordProblem = new MathWordProblem(
                        average.ProblemText,
                        language == AppLanguage.Vietnamese
                            ? $"Số {average.AnswerUnit} cần tìm là:"
                            : $"The requested {average.AnswerUnit} is:",
                        average.AnswerUnit,
                        average.SubjectName)
                };
                string formatted = ElementaryWordProblemSolutionFormatter.Format(
                    ai, language, CultureInfo.InvariantCulture);
                string[] aiLines = formatted.Split(Environment.NewLine);
                Require(aiLines.Length == 3 &&
                        aiLines[1] == $"{average.EquationText} {average.AnswerUnit}" &&
                        aiLines[2] == expectedAnswerLine,
                    $"AI {language}/{type} should show one lead, equation and answer: {formatted}");

                foreach (ArithmeticQuizQuestion question in new[] { algorithm, ai })
                {
                    EssayAnswerValidationResult result = validator.Validate(
                        question,
                        language == AppLanguage.Vietnamese
                            ? $"Số {average.AnswerUnit} cần tìm là:"
                            : $"The requested {average.AnswerUnit} is:",
                        $"{average.EquationText} {average.AnswerUnit}",
                        $"{average.CorrectAnswer} {average.AnswerUnit}");
                    Require(result.IsCorrect,
                        $"{language}/{type} rejected its combined equation: " +
                        $"{result.SolutionError}/{result.EquationError}/{result.AnswerError}");
                }
            }
        }
    }

    private static void CheckPercentageFormatting()
    {
        var validator = new EssayAnswerValidator(new BasicArithmeticEngine());

        foreach (AppLanguage language in new[]
                 { AppLanguage.Vietnamese, AppLanguage.English })
        {
            foreach (PercentageQuizType type in Enum.GetValues<PercentageQuizType>())
            {
                ArithmeticQuizQuestion algorithm = new PercentageQuizGenerator(new Random(83))
                    .GenerateAlgorithm(ArithmeticQuizMode.Essay, type, language);
                PercentageQuizContract percentage = algorithm.PercentageProblem!;
                string suffix = percentage.AnswerUnit == "%"
                    ? "%"
                    : $" {percentage.AnswerUnit}";
                string answerLabel = language == AppLanguage.Vietnamese
                    ? "Đáp số"
                    : "Answer";
                string[] lines = percentage.SolutionText.Split(Environment.NewLine);

                Require(lines.Length == 3 &&
                        !lines[0].Contains('=') &&
                        lines[0].Contains(
                            type == PercentageQuizType.FindPercentageRatio
                                ? percentage.SubjectName
                                : percentage.AnswerUnit,
                            StringComparison.OrdinalIgnoreCase) &&
                        lines[1] == $"{percentage.EquationText}{suffix}" &&
                        lines[2] == $"{answerLabel}: {percentage.CorrectAnswer}{suffix}",
                    $"{language}/{type} should show a contextual lead, equation and answer: " +
                    percentage.SolutionText);
                Require(!lines[0].StartsWith("Tỉ số phần trăm:", StringComparison.Ordinal) &&
                        !lines[0].StartsWith("Percentage ratio:", StringComparison.Ordinal),
                    $"{language}/{type} used a generic percentage lead.");

                ArithmeticQuizQuestion ai = algorithm with
                {
                    WordProblem = new MathWordProblem(
                        percentage.ProblemText,
                        language == AppLanguage.Vietnamese
                            ? "Tỉ số phần trăm là:"
                            : "The percentage ratio is:",
                        percentage.AnswerUnit,
                        percentage.SubjectName)
                };
                string aiSolution = ElementaryWordProblemSolutionFormatter.Format(
                    ai, language, CultureInfo.InvariantCulture);
                Require(aiSolution == percentage.SolutionText,
                    $"AI {language}/{type} should use the contextual contract solution once.");

                string studentLead = language == AppLanguage.Vietnamese
                    ? $"Số {(type == PercentageQuizType.FindPercentageRatio ? percentage.SubjectName : percentage.AnswerUnit)} cần tìm là:"
                    : $"The {(type == PercentageQuizType.FindPercentageRatio ? percentage.SubjectName : percentage.AnswerUnit)} requested is:";
                foreach (ArithmeticQuizQuestion question in new[] { algorithm, ai })
                {
                    EssayAnswerValidationResult result = validator.Validate(
                        question,
                        studentLead,
                        $"{percentage.EquationText}{suffix}",
                        $"{percentage.CorrectAnswer}{suffix}");
                    Require(result.IsCorrect,
                        $"{language}/{type} rejected a solution naming the target: " +
                        $"{result.SolutionError}/{result.EquationError}/{result.AnswerError}");
                }

                if (type == PercentageQuizType.FindPercentageRatio)
                {
                    Require(validator.Validate(algorithm,
                            language == AppLanguage.Vietnamese
                                ? "Tỉ lệ cần tìm là:"
                                : "The share is:",
                            $"{percentage.EquationText}%",
                            $"{percentage.CorrectAnswer}%").SolutionIsCorrect,
                        $"{language} rejected a natural percentage quantity cue.");
                    Require(!validator.Validate(algorithm, "Số con chó là:",
                            $"{percentage.EquationText}%",
                            $"{percentage.CorrectAnswer}%").SolutionIsCorrect,
                        $"{language} accepted an unrelated object as the solution.");
                }
            }
        }
    }
}
