using MathSolver.Models;

namespace MathSolver.Services;

internal static class PracticalModelValidator
{
    internal static ArithmeticQuizValidationResult? Validate(ArithmeticQuizQuestion q)
    {
        if(q.ElementaryProblem is { } p&&(p.StoryContextId?.StartsWith(MoneyChangePilot.Prefix,StringComparison.Ordinal)==true
            ||p.StoryContextId?.StartsWith(RectangleSidePilot.Prefix,StringComparison.Ordinal)==true
            ||p.StoryContextId?.StartsWith(MapScaleOpenPilot.Prefix,StringComparison.Ordinal)==true
            ||p.StoryContextId?.StartsWith(DiscountOpenPilot.Prefix,StringComparison.Ordinal)==true))
        {
            bool model=p.StoryContextId.StartsWith(MoneyChangePilot.Prefix,StringComparison.Ordinal)?MoneyChangePilot.IsValidModel(p)
                :p.StoryContextId.StartsWith(MapScaleOpenPilot.Prefix,StringComparison.Ordinal)?MapScaleOpenPilot.IsValidModel(p)
                :p.StoryContextId.StartsWith(DiscountOpenPilot.Prefix,StringComparison.Ordinal)?DiscountOpenPilot.IsValidModel(p):RectangleSidePilot.IsValidModel(p);
            if(!model||!Enum.IsDefined(q.Mode)||q.Expression.LeftOperand!=0||q.Expression.RightOperand!=0||q.Expression.Operation!=ArithmeticOperation.Add
                ||q.CorrectAnswer!=0||q.PresentedAnswer is not null||q.Choices.Count!=0||q.PresentedEquationIsCorrect is null||q.WordProblem is not null
                ||q.GeometryProblem is not null||q.PercentageProblem is not null||q.FindXProblem is not null||q.FractionProblem is not null
                ||q.MotionProblem is not null||q.ProportionProblem is not null||q.AverageProblem is not null||q.ExpressionProblem is not null
                ||p.ChoiceTexts is not {Count:4}||p.ChoiceTexts.Distinct().Count()!=4||p.ChoiceTexts.Count(s=>s==p.AnswerText)!=1
                ||p.PresentedText is null||q.PresentedEquationIsCorrect.Value!=(p.PresentedText==p.AnswerText))return new(false,"InvalidPracticalElementaryModel");
            return ArithmeticQuizValidationResult.Valid;
        }
        if(q.GeometryProblem is {Reasoning.ScenarioId:"triangle-area-direct" or "parallelogram-area-direct" or "trapezoid-area-direct"} triangle
            &&(!(triangle.ShapeId=="triangle"?TriangleAreaPilot.ValidContract(triangle):QuadrilateralAreaPilot.ValidContract(triangle))||!Enum.IsDefined(q.Mode)||q.CorrectAnswer!=triangle.CorrectAnswer
                ||q.Expression.LeftOperand!=triangle.CorrectAnswer||q.Expression.RightOperand!=0||q.Expression.Operation!=ArithmeticOperation.Add
                ||q.ElementaryProblem is not null||q.PercentageProblem is not null||q.FindXProblem is not null||q.FractionProblem is not null
                ||q.MotionProblem is not null||q.ProportionProblem is not null||q.AverageProblem is not null||q.ExpressionProblem is not null
                ||q.WordProblem is not { } word||word.AnswerUnit!=triangle.AnswerUnit||word.ConversionStep is not null
                ||word.ArithmeticReasoning is not null||word.PracticeDetails is not null||word.FactTable is not null))return new(false,"InvalidTriangleAreaModel");
        if(q.PercentageProblem is { } ratio&&ratio.StoryContextId?.StartsWith(PercentageRatioPilot.Prefix,StringComparison.Ordinal)==true
            &&(!PercentageRatioPilot.ValidContract(ratio)||!Enum.IsDefined(q.Mode)||q.WordProblem is not null
                ||q.ElementaryProblem is not null||q.GeometryProblem is not null||q.FindXProblem is not null||q.FractionProblem is not null
                ||q.MotionProblem is not null||q.ProportionProblem is not null||q.AverageProblem is not null||q.ExpressionProblem is not null
                ||q.CorrectAnswer!=ratio.CorrectAnswer||q.Expression.LeftOperand!=ratio.RepresentativeLeft
                ||q.Expression.RightOperand!=ratio.RepresentativeRight||q.Expression.Operation!=ratio.RepresentativeOperation))return new(false,"InvalidPercentageRatioModel");
        return null;
    }
}
