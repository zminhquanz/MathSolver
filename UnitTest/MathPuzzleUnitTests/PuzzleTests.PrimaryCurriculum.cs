using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using System.Globalization;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckPrimaryCurriculum()
    {
        var mappings = PrimaryCurriculumCatalog.All;
        Require(mappings.Count >= 130 && mappings.Count(row => row.Coverage == CurriculumCoverage.Planned) == 3,
            "The curriculum must describe implemented subtypes and explicitly retain practical gaps.");
        foreach (var row in mappings)
        foreach (var language in Enum.GetValues<AppLanguage>())
            Require(!string.IsNullOrWhiteSpace(QuizContentCatalog.Text(language, row.RequirementId)), "Missing translated objective: " + row.Id);
        foreach (var type in Enum.GetValues<ElementaryQuizType>())
            Require(mappings.Any(row => row.Subtype == type.ToString()), "An elementary subtype has no grade mapping: " + type);
        foreach (var (kind, subtype) in AllSubtypes())
        foreach (var language in Enum.GetValues<AppLanguage>())
        for (int seed = 0; seed < 10; seed++)
        {
            var q = Generate(kind, subtype, ArithmeticQuizMode.Essay, language, seed);
            Require(PrimaryCurriculumCatalog.ForQuestion(q) is not null, "Generated question has no curriculum route: " + kind + "/" + subtype);
        }
        Require(mappings.Single(row => row.Id == "TwoNumbers.SumRatio").Grades.SequenceEqual([5])
            && mappings.Single(row => row.Id == "Measurement.MapScale").Grades.SequenceEqual([5]),
            "2018 sum-ratio and map-scale references must start in grade 5.");
        Require(mappings.Where(row => row.Kind == QuizProblemKind.Motion && !row.Subtype.StartsWith("Basic"))
            .All(row => row.Scope == CurriculumScope.Extension), "Advanced motion must not count as core coverage.");
        var invalid = mappings.ToArray(); invalid[0] = invalid[0] with { Grades = [0, 6] };
        bool rejected = false;
        try { PrimaryCurriculumCatalog.Validate(invalid); } catch (InvalidDataException) { rejected = true; }
        Require(rejected, "Invalid grade metadata must be rejected.");
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var names = QuizContentCatalog.LoadList<QuizNumberName>("Foundation.NumberNames", QuizContentCatalog.Culture(language));
            Require(names.Count >= 40 && names.Select(n => n.Value).Distinct().Count() == names.Count
                && names.All(n => n.Value >= 0 && !string.IsNullOrWhiteSpace(n.Words)), "Invalid reviewed number vocabulary.");
        }
        Console.WriteLine($"  Checked {mappings.Count} grade/objective references, algorithm routes and practical gaps.");
    }

    internal static void CheckFoundations()
    {
        var grader = new EssayAnswerValidator(new BasicArithmeticEngine());
        int tested = 0;
        foreach (var kind in Enum.GetValues<QuizProblemKind>().Where(ElementaryQuizGenerator.Supports))
        foreach (var type in ElementaryQuizGenerator.Types(kind).Where(ElementaryQuizGenerator.IsFoundationSkill))
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        for (int seed = 0; seed < 16; seed++)
        {
            var q = new ElementaryQuizGenerator(new Random(seed)).Generate(mode, kind, type, language, tier);
            var c = q.ElementaryProblem!;
            Require((!c.RequiresSolution || kind == QuizProblemKind.Data && tier >= CurriculumTier.ThreeStars) && PrimaryCurriculumCatalog.ForQuestion(q) is not null,
                "Foundation tasks need a curriculum reference without invented solution sentences.");
            Require(c.ChoiceTexts is { Count: 4 } && c.ChoiceTexts.Distinct().Count() == 4
                && c.ChoiceTexts.Count(choice => ElementaryEssayValidator.CheckAnswers(q, choice)) == 1,
                "Ambiguous foundation choices: " + type);
            var parts = EssayCombinedInputParser.Parse(c.SolutionText, false, true, allowTextAnswer: c.Answers.Any(a => a.IsText));
            var result = grader.Validate(q, parts.Solution, parts.Equation, parts.Answer);
            Require(result.IsCorrect, $"Foundation example rejected: {type}/{language}/{tier}/{seed}: {string.Join(" | ", result.Details)}\n{c.SolutionText}");
            if (!c.RequiresCalculation)
                Require(grader.Validate(q, "", "", c.AnswerText).IsCorrect, "Direct readings must accept answer-only submissions: " + type);
            Require(!ElementaryEssayValidator.CheckAnswers(q, "999999999 invalidunit"), "An unrelated result must be rejected.");
            if (c.Visual is { } visual)
            {
                string description = QuizDiagramDescriptionFormatter.Format(null, visual, false, language);
                Require(!string.IsNullOrWhiteSpace(description), "The diagram needs an accessible data description.");
                if (type == ElementaryQuizType.Counting)
                    Require(c.Answers[0].Value == new ReducedFraction((int)visual.Values[0], 1), "Dot count and answer disagree.");
                if (type == ElementaryQuizType.FractionPicture)
                    Require(c.Answers[0].Value == new ReducedFraction((int)visual.Values[0], (int)visual.Values[1]), "Shaded fraction and answer disagree.");
                if (type == ElementaryQuizType.ReadRuler)
                    Require(c.Answers[0].Value == new ReducedFraction((int)(visual.Values[1] - visual.Values[0]), 1)
                        && c.Answers[0].Unit == "cm", "Ruler readings must subtract a nonzero starting mark.");
                if (type == ElementaryQuizType.NumberLine)
                {
                    int hidden = visual.HiddenValueIndices!.Single();
                    Require(c.Answers[0].Value == new ReducedFraction((int)visual.Values[hidden], 1)
                        && description.Contains(language == AppLanguage.Vietnamese ? "chưa biết" : "unknown"),
                        "Number-line accessibility must preserve the missing tick.");
                }
                if (type == ElementaryQuizType.CompleteBarChart)
                {
                    int hidden = visual.HiddenValueIndices!.Single();
                    Require(c.Answers[0].Value == new ReducedFraction((int)visual.Values[hidden], 1)
                        && !description.Contains(visual.Labels[hidden] + ": " + visual.Values[hidden].ToString(CultureInfo.CurrentCulture)),
                        "A hidden bar must not reveal its answer in the accessible description.");
                }
            }
            if (type == ElementaryQuizType.SortData)
            {
                int sum = c.Answers.Sum(answer => (int)answer.Value.Numerator);
                var batches = c.DataChart!.Observations!;
                Require(batches.Sum(batch => (batch.Excluded ? -1 : 1) * batch.CategoryIds.Count) == sum,
                    "Tallies disagree with the observed and excluded lists.");
            }
            tested++;
        }
        Console.WriteLine($"  Checked {tested} foundation questions, examples, distractors, visual data and answer-only grading.");
    }
}
