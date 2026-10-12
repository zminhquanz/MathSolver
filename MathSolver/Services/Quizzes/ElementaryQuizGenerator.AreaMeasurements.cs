using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    public ArithmeticQuizQuestion GenerateAreaMeasurement(ArithmeticQuizMode mode, AreaMeasurementProfile profile,
        AreaMeasurementDraft? draft = null) => RecreateAreaMeasurement(mode, new(profile, _random.Next(), Draft: draft));

    public ArithmeticQuizQuestion RecreateAreaMeasurement(ArithmeticQuizMode mode, AreaMeasurementQuestionContract contract)
    {
        if (contract.Version != 1 || contract.DataSeed < 0) throw new ArgumentException("Unsupported area measurement contract.");
        return CompleteQuestion(mode, BuildAreaMeasurement(contract), [], null);
    }

    public ArithmeticQuizQuestion FreshAreaMeasurement(ArithmeticQuizMode mode, AreaMeasurementQuestionContract contract)
    {
        var original = BuildAreaMeasurement(contract);
        for (int i = 0; i < 128; i++)
        {
            var fresh = contract with { DataSeed = _random.Next() };
            var question = BuildAreaMeasurement(fresh);
            if (!question.Facts.SequenceEqual(original.Facts)) return CompleteQuestion(mode, question, [], null);
        }
        throw new InvalidOperationException("Area measurement facts unavailable.");
    }

    internal static ElementaryQuizContract BuildAreaMeasurement(AreaMeasurementQuestionContract contract)
    {
        if (contract.Version != 1 || contract.DataSeed < 0) throw new ArgumentException("Unsupported area measurement contract.");
        var p = contract.Profile;
        var context = AreaMeasurementTemplates.Context(p);
        var snapshot = new AreaMeasurementContextSnapshot("area", context.Subject, context.LargeUnitId,
            context.SmallUnitId, context.MaximumQuantity);
        if (contract.Context is not null && contract.Context != snapshot)
            throw new InvalidDataException("The saved area context or its quantity/units has changed.");
        var draft = contract.Draft ?? AreaMeasurementTemplates.Draft(p);
        if (!AreaMeasurementTemplates.IsValidDraft(p, draft)) throw new InvalidDataException("Changed area measurement relation or target.");
        var random = new Random(contract.DataSeed);
        var units = MeasurementEngine.GetCategory("area").Units;
        string large = units.Single(u => u.Id == context.LargeUnitId).Symbol;
        string small = units.Single(u => u.Id == context.SmallUnitId).Symbol;
        decimal factor = MeasurementQuantityRules.Convert(1, "area", context.LargeUnitId, "area", context.SmallUnitId);
        string f = factor.ToString(CultureInfo.InvariantCulture);
        int level = (int)p.Tier;
        var t = new DifficultyBuilder(QuizProblemKind.Measurement, ElementaryQuizType.AreaConversion, p.Language, p.Tier);
        t.RequiresSolution = false;
        t.Constants.Add(f);
        int initial = random.Next(1, Math.Min(9, context.MaximumQuantity - 2) + 1);
        string expression;
        if (level == 2)
        {
            string given = t.Given("initial-small", initial * factor + factor / 2, small);
            expression = $"{given}/{f}";
        }
        else
        {
            string a = t.Given("initial-large", initial, large);
            expression = $"{a}*{f}";
            if (level >= 3)
            {
                string b = t.Given("initial-small", random.Next(2, (int)Math.Min(40, factor)), small);
                t.Step("", expression, small);
                expression = $"({expression})+{b}";
                string added = t.Given("added-large", random.Next(1, Math.Min(7, context.MaximumQuantity - initial - 1) + 1), large);
                t.Step("", $"{added}*{f}", small);
                expression = $"({expression})+{added}*{f}";
                if (level >= 4)
                {
                    string removed = t.Given("removed-small", random.Next(1, int.Parse(b, CultureInfo.InvariantCulture) + 1), small);
                    expression = $"({expression})-{removed}";
                }
                if (level == 5)
                {
                    t.Step("", expression, small);
                    expression = $"({expression})/{f}";
                }
            }
        }
        string answerUnit = level is 2 or 5 ? large : small;
        t.Answer(QuizContentCatalog.Text(p.Language, AreaMeasurementTemplates.Prefix + "AnswerLabel"), expression, answerUnit);
        var bindings = AreaMeasurementTemplates.Bindings(p, t.Givens);
        string Render(string text) => AreaMeasurementTemplates.Render(text, bindings);
        for (int i = 0; i < t.Steps.Count; i++) t.Steps[i] = t.Steps[i] with { Label = Render(draft.SolutionLeads[i]) };
        string problem = string.Join(" ", new[] { draft.Initial, draft.Added, draft.Removed, draft.Question }.Where(s => s is not null).Select(s => Render(s!)));
        return t.Build("area-measurement-" + level, problem) with {
            StoryContextId = context.Id, AreaMeasurement = contract with { Draft = draft, Context = snapshot }
        };
    }
}
