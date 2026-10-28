using System.Globalization;
using MathSolver.Models;
using V = MathSolver.Services.EssayCalculationEvaluator.Value;

namespace MathSolver.Services;

/// <summary>Only quantities derived from this direct profile's actual givens may be used as work.</summary>
internal static class PracticalWorkedRules
{
    internal static bool Supports(ArithmeticQuizQuestion q) =>
        q.ElementaryProblem?.StoryContextId is { } id && (id.StartsWith(MoneyChangePilot.Prefix, StringComparison.Ordinal)
            || id.StartsWith(RectangleSidePilot.Prefix, StringComparison.Ordinal)
            || id.StartsWith(MapScaleOpenPilot.Prefix,StringComparison.Ordinal)||id.StartsWith(DiscountOpenPilot.Prefix,StringComparison.Ordinal))
        || q.PercentageProblem?.StoryContextId?.StartsWith(PercentageRatioPilot.Prefix, StringComparison.Ordinal) == true
        || q.GeometryProblem?.Reasoning?.ScenarioId is "triangle-area-direct" or "parallelogram-area-direct" or "trapezoid-area-direct";

    internal static bool Validate(ArithmeticQuizQuestion q, string? equations)
    {
        if (!Supports(q)) return true;
        string[] facts, constants, expressions;
        string[]? stepUnits=null;
        if (q.ElementaryProblem is { } e)
        {
            facts = [.. e.Facts]; constants = [.. e.Constants];
            expressions = e.Reasoning!.Steps.Select(s => s.Expression).ToArray();
            stepUnits=e.Reasoning.Steps.Select(s=>s.Unit).ToArray();
        }
        else if (q.PercentageProblem is { } p)
        {
            facts = p.Facts.Select(n => n.ToString(CultureInfo.InvariantCulture)).ToArray(); constants = ["100"];
            expressions = [$"{facts[1]}/{facts[0]}", $"{facts[1]}/{facts[0]}*100"];
        }
        else
        {
            var g = q.GeometryProblem!;
            facts = [g.Dimensions["a"].ToString(CultureInfo.InvariantCulture), g.Dimensions["h"].ToString(CultureInfo.InvariantCulture)];
            if(g.ShapeId=="parallelogram")
            {constants=[];expressions=[$"{facts[0]}*{facts[1]}"];}
            else if(g.ShapeId=="trapezoid")
            {
                facts=[g.Dimensions["a"].ToString(CultureInfo.InvariantCulture),g.Dimensions["b"].ToString(CultureInfo.InvariantCulture),g.Dimensions["h"].ToString(CultureInfo.InvariantCulture)];
                constants=["2"];expressions=[$"{facts[0]}+{facts[1]}",$"({facts[0]}+{facts[1]})*{facts[2]}",$"({facts[0]}+{facts[1]})*{facts[2]}/2"];
            }
            else {constants = ["2"]; expressions = [$"{facts[0]}*{facts[1]}", $"{facts[0]}*{facts[1]}/2"];}
            stepUnits=Enumerable.Repeat(g.AnswerUnit,expressions.Length).ToArray();
            if(g.ShapeId=="trapezoid")stepUnits[0]=g.LengthUnitSymbol;
        }
        var original = new StepDerivationTracker(facts, constants);
        var tracker = new StepDerivationTracker(facts, constants);
        var allowed = expressions.Select(original.Read).ToArray();
        if (allowed.Any(x => x.Count == 0)) return false;
        string[] lines = (equations ?? "").Replace("\r", "", StringComparison.Ordinal)
            .Split(['\n', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length is 0 or >64 || (equations?.Length ?? 0) > 32768) return false;
        bool reached = false;
        foreach (string line in lines)
        {
            var (calculation, unit) = EssayAnswerValidator.SplitEquationResult(q, line, allowFractionResult: true);
            var members = calculation.Split('=', StringSplitOptions.TrimEntries);
            if (members.Length < 2 || !EssayCalculationEvaluator.TryEvaluate(members[0], out V value, out bool operation) || !operation) return false;
            var origins = tracker.Read(members[0]);
            for (int i = 1; i < members.Length; i++)
            {
                if (!EssayCalculationEvaluator.TryEvaluate(members[i], out V other, out _) || other != value) return false;
                if (i < members.Length - 1)
                {
                    var next = tracker.Read(members[i]);
                    origins = origins.Where(o => next.Any(o.Equivalent)).ToList();
                }
            }
            var matches = allowed.Select(a => origins.Any(o => a.Any(o.Equivalent))).ToArray();
            if (!matches.Any(x => x)) return false;
            // The only dimensionless intermediate is the subset/whole fraction, before scaling by 100.
            bool dimensionless = q.PercentageProblem is not null && matches[0] && !matches[^1];
            bool unitOkay = unit.Length == 0 || !dimensionless && (stepUnits is not null
                ? Enumerable.Range(0,matches.Length).Any(index=>matches[index]&&(q.ElementaryProblem is not null
                    ?ElementaryEssayValidator.PracticalUnitMatches(q,unit,stepUnits[index])
                    :EssayAnswerValidator.IsExpectedUnitForFeedback(q with {WordProblem=q.WordProblem! with {AnswerUnit=stepUnits[index]}},unit)))
                : EssayAnswerValidator.IsExpectedUnitForFeedback(q,unit));
            if (!unitOkay) return false;
            var proven = origins.Where(o => allowed.Where((_, i) => matches[i]).Any(a => a.Any(o.Equivalent))).ToArray();
            tracker.Remember(value, proven);
            reached |= matches[^1];
        }
        return reached;
    }
}
