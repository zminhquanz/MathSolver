using MathSolver.Models;

namespace MathSolver.Services;

/// <summary>Prose, roles, units, expressions and answers are reconstructed from one bound area contract.</summary>
public static class AreaMeasurementQuestionValidator
{
    public static void Validate(ElementaryQuizContract c)
    {
        var source = c.AreaMeasurement ?? throw new InvalidDataException("Missing area measurement contract.");
        var expected = ElementaryQuizGenerator.BuildAreaMeasurement(source);
        if (source.Context is null || source.Draft is null || c.Kind != expected.Kind || c.Type != expected.Type || c.Language != expected.Language
            || c.StoryContextId != expected.StoryContextId || c.ProblemText != expected.ProblemText
            || c.RequiresSolution != expected.RequiresSolution || c.Visual is not null
            || !c.Facts.SequenceEqual(expected.Facts) || !c.Constants.ToHashSet().SetEquals(expected.Constants)
            || !c.Answers.SequenceEqual(expected.Answers) || c.Reasoning is not { } r
            || r.Tier != expected.Reasoning!.Tier || r.ScenarioId != expected.Reasoning.ScenarioId
            || !r.Givens.SequenceEqual(expected.Reasoning.Givens) || !r.Steps.SequenceEqual(expected.Reasoning.Steps)
            || !r.IntermediateUnits.ToHashSet().SetEquals(expected.Reasoning.IntermediateUnits)
            || r.SupportingDiagram is not null || r.Explanation != expected.Reasoning.Explanation)
            throw new InvalidDataException("Area measurement prose, data, units or target changed.");
    }
}
