using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private sealed record ProbabilityTask(string ProblemText, string Reason,
        string[] Facts, ProbabilityQuizScenario Scenario, string? Expression = null, string? SetupText = null, string? ActionText = null, string? ResultText = null);

    private static string ProbabilityNumber(int value) => value.ToString(CultureInfo.InvariantCulture);

    private ProbabilityTask CreateLikelihoodTask(AppLanguage language, int a, int b)
    {
        bool vi = language == AppLanguage.Vietnamese;
        int family = NextContextVariant(ElementaryQuizType.Likelihood, language, "context", 10);
        int category = NextContextVariant(ElementaryQuizType.Likelihood, language, "category", 3);
        string setup, eventText, contextId;
        int total, favorable;
        string[] facts;
        if (family <= 6)
        {
            string[] colors = new[] { QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.001"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.002"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.003"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.004"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.005") };
            _random.Shuffle(colors);
            (contextId, string container, string item) = family switch
            {
                0 => ("marbles", QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.006"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.007")),
                1 => ("pencils", QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.008"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.009")),
                2 => ("blocks", QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.010"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.011")),
                3 => ("color-cards", QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.012"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.013")),
                4 => ("buttons", QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.014"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.015")),
                5 => ("stickers", QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.016"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.017")),
                _ => ("spinner", "", "")
            };
            bool mixed = category == 2;
            total = mixed ? a + b : a;
            favorable = category == 0 ? total : category == 1 ? 0 : a;
            string target = category == 1 ? colors[2] : colors[0];
            if (family == 6)
            {
                setup = (mixed ? QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.069", ("total", $"{total}"), ("a", $"{a}"), ("colors_0", $"{colors[0]}"), ("b", $"{b}"), ("colors_1", $"{colors[1]}")) : QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.070", ("total", $"{total}"), ("a", $"{a}"), ("colors_0", $"{colors[0]}")));
                eventText = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.018", ("target", $"{target}"));
            }
            else
            {
                setup = (mixed ? QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.071", ("container", $"{container}"), ("a", $"{a}"), ("item", $"{item}"), ("colors_0", $"{colors[0]}"), ("b", $"{b}"), ("colors_1", $"{colors[1]}")) : QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.072", ("container", $"{container}"), ("a", $"{a}"), ("item", $"{item}"), ("colors_0", $"{colors[0]}")));
                eventText = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.068", ("item", $"{item}"), ("target", $"{target}"));
            }
            facts = mixed ? [ProbabilityNumber(a), ProbabilityNumber(b), ProbabilityNumber(total)] : [ProbabilityNumber(a)];
        }
        else if (family == 7)
        {
            contextId = "die";
            total = 6;
            bool alternate = _random.Next(2) == 0;
            favorable = category == 0 ? 6 : category == 1 ? 0 : 3;
            setup = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.019");
            eventText = category switch
            {
                0 => alternate ? QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.020")
                    : QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.021"),
                1 => alternate ? QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.022") : QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.023"),
                _ => alternate ? QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.024") : QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.025")
            };
            facts = ["1", "6", alternate ? "7" : "3", "0"];
        }
        else if (family == 8)
        {
            contextId = "number-cards";
            total = a + b;
            bool even = _random.Next(2) == 0;
            favorable = category == 0 ? total : category == 1 ? 0 : even ? total / 2 : (total + 1) / 2;
            setup = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.026", ("total", $"{total}"));
            eventText = category switch
            {
                0 => QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.027", ("total", $"{total}")),
                1 => QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.028", ("total_1", $"{total + 1}")),
                _ => even ? QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.029")
                    : QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.030")
            };
            facts = ["1", ProbabilityNumber(total), ProbabilityNumber(total + 1)];
        }
        else
        {
            contextId = "coin";
            total = 2;
            favorable = category == 0 ? 2 : category == 1 ? 0 : 1;
            setup = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.031");
            eventText = category switch
            {
                0 => QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.032"),
                1 => QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.033"),
                _ => _random.Next(2) == 0 ? QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.034") : QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.035")
            };
            facts = [];
        }
        string[] prompts = new[] { QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.036", ("eventText", $"{eventText}")), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.037", ("eventText", $"{eventText}")), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.038", ("eventText", $"{eventText}")) };
        string reason = favorable == total ? QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.039")
            : favorable == 0 ? QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.040")
            : QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateLikelihoodTask.041");
        return new(setup + " " + prompts[_random.Next(prompts.Length)], reason, facts,
            new(contextId, eventText, favorable, total, false), SetupText: setup);
    }

    private ProbabilityTask CreateExperimentalProbabilityTask(AppLanguage language, int total)
    {
        bool vi = language == AppLanguage.Vietnamese;
        int family = NextContextVariant(ElementaryQuizType.ExperimentalProbability, language, "context", 10);
        (string contextId, string action, string result) = family switch
        {
            0 => ("coin", QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.042"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.043")),
            1 => ("die-six", QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.044"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.045")),
            2 => ("die-even", QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.046"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.047")),
            3 => ("spinner", QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.048"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.049")),
            4 => ("marbles", QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.050"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.051")),
            5 => ("number-cards", QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.052"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.053")),
            6 => ("tokens", QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.054"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.055")),
            7 => ("basketball", QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.056"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.057")),
            8 => ("ring-toss", QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.058"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.059")),
            _ => ("target", QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.060"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.061"))
        };
        // Include none/all observed successes without implying the next trial is impossible/certain.
        int observed = _random.Next(total + 1);
        bool complement = _random.Next(2) == 0;
        int successes = complement ? total - observed : observed;
        string eventText = complement ? QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.062", ("result", $"{result}")) : result;
        string setup = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.063", ("total", $"{total}"), ("action", $"{action}"), ("observed", $"{observed}"), ("result", $"{result}"));
        string[] prompts = new[] { QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.064", ("eventText", $"{eventText}")), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.065", ("eventText", $"{eventText}")), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.066", ("eventText", $"{eventText}")) };
        string numerator = complement ? $"({total}-{observed})" : ProbabilityNumber(observed);
        string reason = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Probability.CreateExperimentalProbabilityTask.067", ("successes", $"{successes}"), ("total", $"{total}"));
        return new(setup + " " + prompts[_random.Next(prompts.Length)], reason,
            [ProbabilityNumber(observed), ProbabilityNumber(total)],
            new(contextId, eventText, successes, total, true), $"{numerator}/{total}", ActionText: action, ResultText: result);
    }
}
