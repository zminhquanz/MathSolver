using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateProbabilityStory(ArithmeticQuizMode mode, ElementaryQuizType type,
        AppLanguage language, CurriculumTier tier, Random facts) => CompleteQuestion(mode,
            CreateProbabilityDifficulty(type, language, tier, facts), [], null);

    private ElementaryQuizContract CreateProbabilityDifficulty(ElementaryQuizType type, AppLanguage language, CurriculumTier tier,
        Random? factsRandom = null)
    {
        var t = new DifficultyBuilder(QuizProblemKind.Probability, type, language, tier);
        int level = (int)tier;
        var facts = factsRandom ?? _random;
        t.RequiresSolution = false;
        if (type == ElementaryQuizType.Likelihood)
        {
            var original = CreateLikelihoodTask(language, facts.Next(3, 12), facts.Next(3, 12));
            int n = original.Scenario.TotalCount, k = original.Scenario.EventCount;
            t.Given("outcomes", n); t.Given("favorable", k);
            string setup = original.SetupText!, classificationEvent = original.Scenario.EventText;
            int total = n, favorable = k;
            if (level == 2)
            {
                classificationEvent = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.001", ("classificationEvent", $"{classificationEvent}"));
                favorable = n - k;
            }
            else if (level == 3)
            {
                setup += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.002");
                classificationEvent = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.003", ("classificationEvent", $"{classificationEvent}"));
                total = n * n; favorable = k * k;
            }
            else if (level >= 4)
            {
                // Each equally likely original outcome is represented once on a card.
                // The subsequent sample explicitly draws cards, including for die/coin contexts.
                setup += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.004", ("n", $"{n}"), ("k", $"{k}"));
                if (level == 5)
                {
                    int removed = k > 0 && n > 2 ? 1 : 0;
                    string remove = t.Given("removed-favorable", removed);
                    setup += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.005", ("remove", $"{remove}"));
                    n -= removed; k -= removed;
                }
                setup += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.006");
                classificationEvent = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.007", ("classificationEvent", $"{classificationEvent}"));
                total = n * (n - 1); favorable = k * (k - 1);
            }
            string answer = favorable == 0 ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.008")
                : favorable == total ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.009") : QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.010");
            t.TextAnswer(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.011"), answer,
                answer == QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.012") ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.013") : answer);
            t.Explanation = favorable == 0 ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.014")
                : favorable == total ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.015")
                : QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.016");
            string problem = setup + QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.017", ("classificationEvent", $"{classificationEvent}"));
            return t.Build("likelihood-relations-" + level, problem,
                probability: new(original.Scenario.ContextId, classificationEvent, favorable, total, false)) with { StoryContextId = original.Scenario.ContextId };
        }

        var source = CreateExperimentalProbabilityTask(language, _random.Next(10, 30));
        int batchCount = level <= 2 ? 1 : level <= 4 ? 2 : 3;
        int boundary = NextContextVariant(type, language, "observed-boundaries", 4);
        string[] totals = new string[batchCount], successes = new string[batchCount];
        int[] actualTotal = new int[batchCount], actualSuccess = new int[batchCount];
        string text = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.018", ("source_ActionText", $"{source.ActionText}"));
        for (int index = 0; index < batchCount; index++)
        {
            int total = facts.Next(10, 31);
            int success = boundary == 0 ? 0 : boundary == 1 ? total : facts.Next(1, total);
            actualTotal[index] = total; actualSuccess[index] = success;
            totals[index] = t.Given("total-" + index, total);
            if (level >= 4 && index == batchCount - 1)
            {
                string failures = t.Given("failures-" + index, total - success);
                successes[index] = $"({totals[index]}-{failures})";
                text += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.019", ("index_1", $"{index + 1}"), ("totals_index", $"{totals[index]}"), ("failures", $"{failures}"), ("source_ResultText", $"{source.ResultText}"));
                t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.020"), successes[index]);
            }
            else
            {
                successes[index] = t.Given("success-" + index, success);
                text += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.021", ("index_1", $"{index + 1}"), ("totals_index", $"{totals[index]}"), ("successes_index", $"{successes[index]}"), ("source_ResultText", $"{source.ResultText}"));
            }
        }
        int invalid = level == 5 ? Math.Min(2, actualTotal[0] - actualSuccess[0]) : 0;
        string denominator = "(" + string.Join("+", totals) + ")";
        if (level == 5)
        {
            string removed = t.Given("invalid-failures", invalid);
            denominator = $"({denominator}-{removed})";
            text += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.022", ("removed", $"{removed}"));
        }
        string numerator = "(" + string.Join("+", successes) + ")";
        bool complement = level == 2 || level >= 3 && _random.Next(2) == 0;
        string eventText = complement ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.023", ("source_ResultText", $"{source.ResultText}")) : source.ResultText!;
        if (batchCount > 1)
        {
            t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.024"), denominator);
            t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.025"), numerator);
        }
        if (complement) numerator = $"({denominator}-{numerator})";
        t.Answer(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.026"), $"{numerator}/{denominator}");
        int valid = actualTotal.Sum() - invalid, favorableTrials = complement ? valid - actualSuccess.Sum() : actualSuccess.Sum();
        text += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.ProbabilityDifficulty.CreateProbabilityDifficulty.027", ("eventText", $"{eventText}"));
        return t.Build("observed-relations-" + level, text,
            probability: new(source.Scenario.ContextId, eventText, favorableTrials, valid, true)) with { StoryContextId = source.Scenario.ContextId };
    }
}
