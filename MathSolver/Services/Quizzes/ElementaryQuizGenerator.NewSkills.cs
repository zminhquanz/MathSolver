using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private ElementaryQuizContract CreateMapScale(AppLanguage language, CurriculumTier tier)
    {
        var t = new DifficultyBuilder(QuizProblemKind.Measurement, ElementaryQuizType.MapScale, language, tier);
        int level = (int)tier;
        int denominator = level == 1 ? 100 : level == 2 ? _random.Next(5, 21) * 100
            : _random.Next(1, 6) * 100_000;
        if (level == 5) denominator = new[] { 100_000, 200_000, 500_000 }[_random.Next(3)];
        string scale = t.Given("scale-denominator", denominator);
        string first = t.Given(level == 5 ? "actual-km" : "map-cm", _random.Next(2, 7), level == 5 ? "km" : "cm");
        string expression, problem, unit;
        if (level == 5)
        {
            string next = t.Given("actual-m", _random.Next(2, 8) * 100, "m");
            t.Constant(1000, 100_000);
            string km = $"{first}+{next}/1000";
            t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreateMapScale.001"), $"{next}/1000", "km");
            t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreateMapScale.002"), km, "km");
            expression = $"({km})*100000/{scale}"; unit = "cm";
            problem = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreateMapScale.003", ("scale", $"{scale}"), ("first", $"{first}"), ("next", $"{next}"));
        }
        else
        {
            string length = first;
            problem = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreateMapScale.004", ("scale", $"{scale}"));
            if (level == 4)
            {
                string second = t.Given("second-map-cm", _random.Next(1, 5), "cm");
                length = $"({first}+{second})";
                t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreateMapScale.005"), length, "cm");
                problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreateMapScale.006", ("first", $"{first}"), ("second", $"{second}"));
            }
            else problem += level <= 2
                ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreateMapScale.007", ("first", $"{first}"))
                : QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreateMapScale.008", ("first", $"{first}"));
            unit = level <= 2 ? "m" : "km";
            int factor = level <= 2 ? 100 : 100_000;
            t.Constant(factor);
            t.Step(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreateMapScale.009"), $"{length}*{scale}", "cm");
            expression = $"{length}*{scale}/{factor}";
            problem += QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreateMapScale.010", ("unit", $"{unit}"));
        }
        t.Answer(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreateMapScale.011"), expression, unit);
        return t.Build("map-scale-" + level, problem) with { StoryContextId = "tourism" };
    }

    private ElementaryQuizContract CreatePictograph(AppLanguage language, CurriculumTier tier)
    {
        var t = new DifficultyBuilder(QuizProblemKind.Data, ElementaryQuizType.ReadPictograph, language, tier);
        int level = (int)tier;
        string[] contexts = ["library", "craft", "community"];
        var context = QuizStoryContextCatalog.Find(contexts[NextContextVariant(t.Type, language, "pictograph", contexts.Length)]);
        string unit = context.Unit(language);
        int key = new[] { 1, 2, 5, 10, 20 }[level - 1];
        int[] counts = [_random.Next(3, 8), _random.Next(3, 8), _random.Next(2, 5)];
        string[] labels = new[] { QuizContentCatalog.Text(language, "ElementaryQuizGenerator.NewSkills.CreatePictograph.022"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.NewSkills.CreatePictograph.023"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.NewSkills.CreatePictograph.024") };
        string[] symbols = counts.Select((count, i) => t.Given("icons-" + i, count)).ToArray();
        string multiplier = t.Given("key", key, unit);
        string expression, question;
        if (level <= 2)
        {
            expression = $"{symbols[0]}*{multiplier}";
            question = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreatePictograph.012", ("unit", $"{unit}"));
        }
        else
        {
            string total = level == 3 ? $"{symbols[0]}+{symbols[1]}" : $"{symbols[0]}+{symbols[1]}+{symbols[2]}";
            if (level == 5) total = $"({symbols[0]}+{symbols[1]})-{symbols[2]}";
            t.Step((level == 5 ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreatePictograph.036") : QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreatePictograph.037")), total);
            expression = $"({total})*{multiplier}";
            question = level == 3 ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreatePictograph.013", ("unit", $"{unit}"))
                : level == 4 ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreatePictograph.014", ("unit", $"{unit}"))
                : QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreatePictograph.015", ("unit", $"{unit}"));
        }
        t.Answer(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreatePictograph.016"), expression, unit);
        string problem = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreatePictograph.017", ("context_Setting_language", $"{context.Setting(language)}"), ("multiplier", $"{multiplier}"), ("unit", $"{unit}"), ("question", $"{question}"));
        return t.Build("pictograph-" + level, problem, new("pictograph", labels,
            counts.Select(count => (decimal)(count * key)).ToArray(), unit, PictographKey: key)) with { StoryContextId = context.Id };
    }

    private ElementaryQuizContract CreateShapeRecognition(AppLanguage language, CurriculumTier tier)
    {
        var t = new DifficultyBuilder(QuizProblemKind.VisualGeometry, ElementaryQuizType.RecognizeShape, language, tier);
        int level = (int)tier;
        string[] ids = ["circle", "triangle", "square", "rectangle", "trapezoid", "parallelogram", "rhombus", "cube", "cuboid", "cylinder", "sphere"];
        string[] names = new[] { QuizContentCatalog.Text(language, "ElementaryQuizGenerator.NewSkills.CreateShapeRecognition.025"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.NewSkills.CreateShapeRecognition.026"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.NewSkills.CreateShapeRecognition.027"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.NewSkills.CreateShapeRecognition.028"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.NewSkills.CreateShapeRecognition.029"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.NewSkills.CreateShapeRecognition.030"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.NewSkills.CreateShapeRecognition.031"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.NewSkills.CreateShapeRecognition.032"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.NewSkills.CreateShapeRecognition.033"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.NewSkills.CreateShapeRecognition.034"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.NewSkills.CreateShapeRecognition.035") };
        int[] pool = level <= 2 ? [0, 1, 2, 3] : level == 3 ? [1, 2, 3, 4, 5, 6] : level == 4 ? [7, 8, 9, 10] : Enumerable.Range(0, ids.Length).ToArray();
        int index = pool[_random.Next(pool.Length)];
        t.TextAnswer(QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreateShapeRecognition.018"), names[index], names[index].Replace("Hình ", ""), index == 8 ? QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreateShapeRecognition.019") : names[index]);
        // Orientation varies; the answer is never placed in the figure's labels.
        decimal rotation = level == 1 || index >= 7 ? 0 : _random.Next(1, 7) * 15;
        t.Explanation = QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreateShapeRecognition.020", ("names_index_ToLowerInvariant", $"{names[index].ToLowerInvariant()}"));
        string[] distractors = names.Where((_, i) => i != index).ToArray();
        _random.Shuffle(distractors);
        return t.Build("recognize-" + ids[index], QuizContentCatalog.Text(t.Language, "ElementaryQuizGenerator.NewSkills.CreateShapeRecognition.021"),
            new("shape", [], [], "", RotationDegrees: rotation, ScenarioId: ids[index]))
            with { ChoiceTexts = [names[index], .. distractors.Take(3)] };
    }
}
