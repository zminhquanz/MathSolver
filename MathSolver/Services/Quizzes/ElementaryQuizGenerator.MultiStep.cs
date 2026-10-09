using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private ElementaryQuizContract CreateMultiStep(ElementaryQuizType type, AppLanguage language, CurriculumTier tier)
    {
        var t = new DifficultyBuilder(QuizProblemKind.MultiStep, type, language, tier);
        int level = (int)tier;
        string[] ids = ["library", "craft", "community", "distribution"];
        if (_expandNarratives) ids = [.. ids, "kitchen", "livestock", "water", "environment", "agriculture"];
        var context = QuizStoryContextCatalog.Find(ids[NextContextVariant(type, language, "multi-step", ids.Length)]);
        string unit = context.Unit(language);
        int cap = Math.Min(context.MaximumPerPeriod, level == 1 ? 24 : level == 2 ? 96 : 200);
        int per = _random.Next(2, Math.Max(3, cap / 12));
        int groups = _random.Next(2, 4), secondGroups = _random.Next(2, 4);
        int baseTotal = per * groups, extra = per * secondGroups;
        string label = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.001"), expression, problem;
        string intro = context.Id switch
        {
            "library" => QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.002"),
            "craft" => QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.003"),
            "community" => QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.004"),
            "distribution" => QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.005"),
            _ => QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.Intro." + context.Id)
        };
        if (type == ElementaryQuizType.MultiStepAddSubtract)
        {
            if (level <= 3)
            {
                string first = t.Given("first", baseTotal, unit), second = t.Given("second", extra, unit);
                string combined = $"{first}+{second}";
                t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.006"), combined, unit);
                problem = intro + QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.007", ("first", $"{first}"), ("second", $"{second}"), ("unit", $"{unit}"));
                if (level == 3)
                {
                    string third = t.Given("third", per, unit);
                    combined = $"({combined})+{third}";
                    t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.008"), combined, unit);
                    problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.009", ("third", $"{third}"), ("unit", $"{unit}"));
                }
                string used = t.Given("used", per, unit);
                expression = $"({combined})-{used}";
                problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.010", ("used", $"{used}"), ("unit", $"{unit}"));
            }
            else
            {
                string remaining = t.Given("remaining", baseTotal + extra - per, unit), removed = t.Given("removed", per, unit);
                problem = intro + QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.011", ("removed", $"{removed}"), ("unit", $"{unit}"), ("remaining", $"{remaining}"));
                string original = $"{remaining}+{removed}";
                if (level == 5)
                {
                    string added = t.Given("added", per, unit);
                    // Remaining above is after adding then removing; the second initial portion is smaller by per.
                    original = $"({original})-{added}";
                    problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.012", ("added", $"{added}"), ("unit", $"{unit}"));
                    t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.013"), $"{remaining}+{removed}", unit);
                }
                t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.014"), original, unit);
                {
                    string each = t.Given("each", per, unit), count = t.Given("groups", groups, QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.015"));
                    string first = $"{each}*{count}";
                    t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.016"), first, unit);
                    expression = $"({original})-({first})";
                    problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.017", ("count", $"{count}"), ("each", $"{each}"), ("unit", $"{unit}"));
                }
                problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.018", ("unit", $"{unit}"));
            }
        }
        else
        {
            string each = t.Given("each", per, unit), count = t.Given("groups", level == 5 ? groups + 1 : groups, QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.019"));
            string active = count;
            if (level == 5)
            {
                string unused = t.Given("unused-groups", 1, QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.020"));
                active = $"({count}-{unused})";
                t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.021"), active, QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.022"));
            }
            string combined = $"{each}*{active}";
            t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.023"), combined, unit);
            problem = intro + QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.024", ("count", $"{count}"), ("each", $"{each}"), ("unit", $"{unit}"));
            if (level == 5) problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.025");
            if (level >= 3)
            {
                string second;
                if (level == 3)
                {
                    second = t.Given("second", extra, unit);
                    problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.026", ("second", $"{second}"), ("unit", $"{unit}"));
                }
                else
                {
                    string eachSecond = t.Given("each-second", per, unit), countSecond = t.Given("groups-second", secondGroups, QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.027"));
                    second = $"{eachSecond}*{countSecond}";
                    t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.028"), second, unit);
                    problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.029", ("countSecond", $"{countSecond}"), ("eachSecond", $"{eachSecond}"), ("unit", $"{unit}"));
                }
                combined = $"({combined})+({second})";
                t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.030"), combined, unit);
            }
            bool subtract = type == ElementaryQuizType.MultiStepRemaining || level >= 4;
            int adjustment = per;
            if (type != ElementaryQuizType.MultiStepShare || level > 1)
            {
                string delta = t.Given(subtract ? "used" : "extra", adjustment, unit);
                combined = $"({combined}){(subtract ? "-" : "+")}{delta}";
                problem += subtract ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.031", ("delta", $"{delta}"), ("unit", $"{unit}"))
                    : QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.032", ("delta", $"{delta}"), ("unit", $"{unit}"));
            }
            if (type == ElementaryQuizType.MultiStepShare)
            {
                if (t.Steps[^1].Expression != combined) t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.033"), combined, unit);
                string shares = t.Given("shares", per, QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.034"));
                expression = $"({combined})/{shares}";
                problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.035", ("shares", $"{shares}"), ("unit", $"{unit}"));
            }
            else
            {
                expression = combined;
                problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.MultiStep.CreateMultiStep.036", ("unit", $"{unit}"));
            }
        }
        t.Answer(label, expression, unit);
        return t.Build("multi-step-" + type + "-" + level, problem) with { StoryContextId = context.Id };
    }
}
