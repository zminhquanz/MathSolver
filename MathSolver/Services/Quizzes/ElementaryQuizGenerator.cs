using MathSolver.Models;
using MathSolver.Services.Core;
using System.Collections.Concurrent;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>C# math puzzle data and rules.</summary>
public sealed partial class ElementaryQuizGenerator(Random? random = null, bool expandNarratives = true, bool expandActivityStories = true,
    bool expandedNumberPractice = false)
{
    private readonly Random _random = random ?? Random.Shared;
    private readonly bool _expandNarratives = expandNarratives;
    private readonly bool _expandActivityStories = expandActivityStories;
    private readonly bool _expandedNumberPractice = expandedNumberPractice;
    private static readonly string[][] LineNameSets =
        [["a", "b", "c", "d", "e"], ["d", "e", "f", "g", "h"], ["m", "n", "p", "q", "r"]];
    private static readonly ConcurrentDictionary<QuizProblemKind, IReadOnlyList<ElementaryQuizType>> TypeCache = new();

    private sealed class ChoiceScratch
    {
        internal readonly List<string> Choices = new(4);
        internal readonly List<string> Unique = new(4);
        internal ElementaryAnswer[]? SingleAnswer;
        internal ElementaryAnswer[]? PairAnswers;
    }

    [ThreadStatic]
    private static ChoiceScratch? s_choiceScratch;

    public static bool Supports(QuizProblemKind kind) => Types(kind).Count > 0;
    public static IReadOnlyList<ElementaryQuizType> Types(QuizProblemKind kind)
        => TypeCache.GetOrAdd(kind, static key => Array.AsReadOnly(CreateTypes(key)));

    private static ElementaryQuizType[] CreateTypes(QuizProblemKind kind) => kind switch
    {
        QuizProblemKind.NumberSense => [ElementaryQuizType.Counting, ElementaryQuizType.ReadNumber,
            ElementaryQuizType.WriteNumber, ElementaryQuizType.PlaceValue, ElementaryQuizType.AdjacentNumbers,
            ElementaryQuizType.NumberLine, ElementaryQuizType.Parity, ElementaryQuizType.RomanNumerals,
            ElementaryQuizType.OrderNumbers, ElementaryQuizType.RoundWholeNumber, ElementaryQuizType.EstimateSum,
            ElementaryQuizType.LetterExpression],
        QuizProblemKind.TwoNumbers => [ElementaryQuizType.SumDifference, ElementaryQuizType.SumRatio, ElementaryQuizType.DifferenceRatio],
        QuizProblemKind.Measurement => [ElementaryQuizType.LengthConversion, ElementaryQuizType.MassConversion, ElementaryQuizType.CapacityConversion, ElementaryQuizType.AreaConversion, ElementaryQuizType.VolumeConversion, ElementaryQuizType.MixedLength, ElementaryQuizType.MapScale, ElementaryQuizType.ReadRuler, ElementaryQuizType.ReadProtractor, ElementaryQuizType.ReadThermometer],
        QuizProblemKind.Time => [ElementaryQuizType.ElapsedTime, ElementaryQuizType.TimeAddition, ElementaryQuizType.ReadClock, ElementaryQuizType.Calendar,
            ElementaryQuizType.SecondsConversion, ElementaryQuizType.YearCentury, ElementaryQuizType.CalendarWeekday,
            .. MixedTimeTypes, ElementaryQuizType.SameDayElapsed, ElementaryQuizType.SameDayStart, ElementaryQuizType.SameDayEnd, ElementaryQuizType.SameDayActiveDuration, ElementaryQuizType.OvernightElapsed, ElementaryQuizType.SameMonthElapsed, ElementaryQuizType.SameMonthInclusive, ElementaryQuizType.AdjacentMonthElapsed, ElementaryQuizType.AdjacentMonthInclusive, ElementaryQuizType.FebruaryCommonInclusive, ElementaryQuizType.FebruaryLeapInclusive],
        QuizProblemKind.Remainder => [ElementaryQuizType.QuotientRemainder, ElementaryQuizType.MinimumGroups, ElementaryQuizType.Leftovers, ElementaryQuizType.FullGroups],
        QuizProblemKind.Decimal => [ElementaryQuizType.DecimalAdd, ElementaryQuizType.DecimalSubtract, ElementaryQuizType.DecimalMultiply, ElementaryQuizType.DecimalDivide, ElementaryQuizType.DecimalRound, ElementaryQuizType.DecimalCompare, .. DecimalSkillTypes],
        QuizProblemKind.FractionSkills => [ElementaryQuizType.ReduceFraction, ElementaryQuizType.MixedNumber, ElementaryQuizType.CommonDenominator, ElementaryQuizType.FractionOfNumber, ElementaryQuizType.WholeFromFraction, ElementaryQuizType.FractionPicture, ElementaryQuizType.FractionTerms, ElementaryQuizType.EquivalentFraction, ElementaryQuizType.OrderFractions,
            ElementaryQuizType.OrderRelatedFractions, ElementaryQuizType.FractionRemaining],
        QuizProblemKind.Data => [ElementaryQuizType.ReadTable, ElementaryQuizType.ReadBarChart, ElementaryQuizType.ReadPieChart, ElementaryQuizType.ChartTotal, ElementaryQuizType.ChartDifference, ElementaryQuizType.ReadPictograph, ElementaryQuizType.SortData, ElementaryQuizType.CompleteBarChart],
        QuizProblemKind.Probability => [ElementaryQuizType.Likelihood, ElementaryQuizType.ExperimentalProbability],
        QuizProblemKind.VisualGeometry => [ElementaryQuizType.ClassifyAngle, ElementaryQuizType.ParallelLines, ElementaryQuizType.PerpendicularLines, ElementaryQuizType.CountSides, ElementaryQuizType.RectangleSide, ElementaryQuizType.CompositeArea, ElementaryQuizType.RecognizeShape, ElementaryQuizType.SpatialPosition, ElementaryQuizType.IdentifyLine, ElementaryQuizType.Midpoint, ElementaryQuizType.CircleParts, ElementaryQuizType.ShapeNet, ElementaryQuizType.TriangleKind, .. SpatialSkillTypes],
        QuizProblemKind.MultiStep => [ElementaryQuizType.MultiStepAddSubtract, ElementaryQuizType.MultiStepEqualGroups, ElementaryQuizType.MultiStepRemaining, ElementaryQuizType.MultiStepShare],
        _ => []
    };

    public ArithmeticQuizQuestion GenerateComparison(ArithmeticQuizMode mode, QuizProblemKind kind,
        AppLanguage language, CurriculumTier tier) => Generate(mode, kind, kind switch
        {
            QuizProblemKind.Arithmetic => ElementaryQuizType.IntegerCompare,
            QuizProblemKind.Fraction => ElementaryQuizType.CompareFractions,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        }, language, tier);

    public ArithmeticQuizQuestion Generate(ArithmeticQuizMode mode, QuizProblemKind kind,
        ElementaryQuizType? selected, AppLanguage language, CurriculumTier tier)
    {
        ElementaryQuizType type;
        if (kind is QuizProblemKind.Arithmetic && selected == ElementaryQuizType.IntegerCompare
            || kind is QuizProblemKind.Fraction && selected == ElementaryQuizType.CompareFractions)
            type = selected.Value;
        else
        {
            IReadOnlyList<ElementaryQuizType> types = Types(kind);
            if (types.Count == 0) throw new ArgumentOutOfRangeException(nameof(kind));
            type = selected.HasValue && types.Contains(selected.Value) ? selected.Value : types[_random.Next(types.Count)];
        }
        if (CreateDifficultyContract(kind, type, language, tier) is { } difficultyContract)
            return CompleteQuestion(mode, difficultyContract, [], difficultyContract.ChoiceTexts);
        bool vi = language == AppLanguage.Vietnamese;
        string N(decimal value) => value.ToString("0.################", CultureInfo.InvariantCulture);
        int scale = (int)tier;
        int a = _random.Next(2, 5 + scale * 4), b = _random.Next(2, 5 + scale * 3);
        string problem = "";
        var facts = new List<string>();
        var constants = new List<string> { "0", "1" };
        var answers = new List<ElementaryAnswer>();
        var work = new List<string>();
        bool requiresSolution = true;
        QuizVisualData? visual = null;
        string[]? textChoices = null;
        void Fact(params decimal[] values) => facts.AddRange(values.Select(N));
        void Answer(string label, string expression, string unit = "", bool reduced = false,
            int? denominator = null, bool mixed = false, string? displayExpression = null)
        {
            if (!EssayCalculationEvaluator.TryEvaluate(expression, out var value, out _, kind is QuizProblemKind.Decimal or QuizProblemKind.Measurement))
                throw new InvalidOperationException("Invalid generated elementary expression.");
            var answer = new ElementaryAnswer(label, new(value.Numerator, value.Denominator), unit,
                expression, RequireReduced: reduced, RequiredDenominator: denominator, RequireMixedNumber: mixed,
                Text: mixed ? Mixed(new(value.Numerator, value.Denominator)) : denominator.HasValue
                    ? $"{value.Numerator * denominator.Value / value.Denominator}/{denominator.Value}" : null,
                DisplayValue: kind is QuizProblemKind.Decimal or QuizProblemKind.Measurement
                    ? N((decimal)value.Numerator / (decimal)value.Denominator) : null);
            answers.Add(answer);
            bool calculation = expression.Any(character => "+-*/".Contains(character));
            work.Add(label + ":" + (calculation ? "" : " " + ElementaryQuizContract.FormatAnswer(answer)));
            if (calculation) work.Add(QuizMathExpressionFormatter.Format(displayExpression ?? expression,
                preserveFractions: kind == QuizProblemKind.FractionSkills || type == ElementaryQuizType.ExperimentalProbability)
                + " = " + ElementaryQuizContract.FormatAnswer(answer));
        }
        void TextAnswer(string label, string text, params string[] aliases)
        {
            answers.Add(new(label, new(0, 1), "", "", text, aliases));
            // Comparisons already receive their complete relation in CompleteQuestion.
            if (type is not (ElementaryQuizType.IntegerCompare or ElementaryQuizType.DecimalCompare or ElementaryQuizType.CompareFractions))
                work.Add(label + ": " + text);
            requiresSolution = false;
        }
        switch (type)
        {
            case ElementaryQuizType.IntegerCompare:
            {
                a = _expandedNumberPractice && scale >= 4 ? PracticeNumberNames.Next(_random, tier) : QuizCurriculumLayer.NextPrimaryOperand(_random, tier);
                b = _expandedNumberPractice && scale >= 4 ? PracticeNumberNames.Next(_random, tier) : QuizCurriculumLayer.NextSecondaryOperand(_random, tier);
                int relation = _random.Next(3);
                if (relation == 0) b = a;
                else
                {
                    if (a == b) b = a == 1 ? 2 : a - 1;
                    if ((relation == 1 && a > b) || (relation == 2 && a < b)) (a, b) = (b, a);
                }
                Fact(a, b);
                problem = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.037", ("a", $"{a}"), ("b", $"{b}"));
                TextAnswer(QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.038"), a < b ? "<" : a > b ? ">" : "=");
                break;
            }
            case ElementaryQuizType.CompareFractions:
            {
                int denominator = QuizCurriculumLayer.NextPrimaryOperand(_random, tier, minimumAllowed: 2);
                int numerator = QuizCurriculumLayer.NextSecondaryOperand(_random, tier, maximumOverride: denominator - 1);
                int otherDenominator = scale < 3 ? denominator
                    : QuizCurriculumLayer.NextPrimaryOperand(_random, tier, minimumAllowed: 2);
                int otherNumerator = _random.Next(1, otherDenominator);
                if (_random.Next(3) == 0)
                {
                    int factor = scale < 3 ? 1
                        : _random.Next(1, Math.Min(scale, QuizCurriculumLayer.GetMaximumOperandValue(tier) / denominator) + 1);
                    otherNumerator = numerator * factor;
                    otherDenominator = denominator * factor;
                }
                Fact(numerator, denominator, otherNumerator, otherDenominator);
                problem = QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.039", ("numerator", $"{numerator}"), ("denominator", $"{denominator}"), ("otherNumerator", $"{otherNumerator}"), ("otherDenominator", $"{otherDenominator}"));
                int comparison = ((long)numerator * otherDenominator).CompareTo((long)otherNumerator * denominator);
                TextAnswer(QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.040"), comparison < 0 ? "<" : comparison > 0 ? ">" : "=");
                break;
            }
            case ElementaryQuizType.ClassifyAngle:
            {
                int index = _random.Next(scale == 1 ? 2 : 4);
                decimal degrees = index switch
                {
                    1 => 90, 3 => 180,
                    0 when scale == 1 => _random.Next(2, 5) * 15,
                    0 when scale == 4 => 90 - _random.Next(1, 10) / 10m,
                    0 when scale == 5 => 90 - _random.Next(1, 10) / 100m,
                    2 when scale == 4 => 90 + _random.Next(1, 10) / 10m,
                    2 when scale == 5 => 90 + _random.Next(1, 10) / 100m,
                    0 => _random.Next(3, 18) * 5,
                    _ => _random.Next(19, 34) * 5
                };
                Fact(degrees);
                string[][] names = [["A", "O", "B"], ["M", "O", "N"], ["X", "P", "Y"], ["C", "E", "D"]];
                string[] labels = names[_random.Next(names.Length)];
                string name = string.Concat(labels);
                visual = new("angle", labels, [degrees], "°", RotationDegrees:
                    scale == 1 ? _random.Next(2) * 180 : scale == 2 ? _random.Next(4) * 90 : _random.Next(12) * 30);
                string[] prompts = new[] { QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.041", ("name", $"{name}")), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.042", ("name", $"{name}")), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.043", ("labels_1", $"{labels[1]}"), ("labels_0", $"{labels[0]}"), ("labels_2", $"{labels[2]}")), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.044", ("labels_1", $"{labels[1]}")) };
                problem = prompts[_random.Next(prompts.Length)];
                string[] angleNames = new[] { QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.045"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.046"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.047"), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.048") };
                TextAnswer(QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.049"), angleNames[index], vi ? new[] { "nhọn", "vuông", "tù", "bẹt" }[index] : angleNames[index]);
                break;
            }
            case ElementaryQuizType.ParallelLines:
            case ElementaryQuizType.PerpendicularLines:
            {
                bool parallel = type == ElementaryQuizType.ParallelLines;
                bool choosePair = scale >= 4 || scale == 3 && _random.Next(2) == 0;
                decimal rotation = scale == 1 ? _random.Next(2) * 90 : scale == 2 ? _random.Next(4) * 45 : _random.Next(12) * 15;
                string[] names = LineNameSets[_random.Next(LineNameSets.Length)];
                if (choosePair)
                {
                    bool hasPair = _random.Next(4) != 0;
                    int lineCount = scale >= 4 ? scale : 3;
                    names = names[..lineCount];
                    decimal[] directions = hasPair
                        ? parallel ? [0, 0, 25, 55, 120] : [0, 90, 25, 55, 120]
                        : [0, 25, 55, 120, 155];
                    Span<decimal> activeDirections = directions.AsSpan(0, lineCount);
                    for (int i = 0; i < activeDirections.Length; i++) activeDirections[i] += rotation;
                    _random.Shuffle(activeDirections);
                    var lines = new QuizVisualLine[lineCount];
                    for (int i = 0; i < lineCount; i++)
                        lines[i] = new(names[i], directions[i],
                            parallel && hasPair ? (i - (lineCount - 1) / 2f) * .22f : 0);
                    visual = new("line-pairs", names, [], "", Lines: lines);
                    string Pair(int left, int right) => QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.050", ("names_left", $"{names[left]}"), ("names_right", $"{names[right]}"));
                    string none = parallel ? QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.051") : QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.052");
                    int pairIndex = -1;
                    Span<(int Left, int Right)> pairs = stackalloc (int, int)[10];
                    int pairCount = 0;
                    for (int left = 0; left < lineCount; left++)
                        for (int right = left + 1; right < lineCount; right++)
                            pairs[pairCount++] = (left, right);
                    for (int candidate = 0; candidate < pairCount; candidate++)
                    {
                        var pair = pairs[candidate];
                        decimal difference = Math.Abs(directions[pair.Left] - directions[pair.Right]) % 180;
                        if (difference == (parallel ? 0 : 90)) pairIndex = candidate;
                    }
                    string[] distractors = new string[pairCount - (pairIndex < 0 ? 0 : 1)];
                    int distractorCount = 0;
                    for (int i = 0; i < pairCount; i++)
                        if (i != pairIndex) distractors[distractorCount++] = Pair(pairs[i].Left, pairs[i].Right);
                    _random.Shuffle(distractors);
                    string correctPair = pairIndex < 0 ? none : Pair(pairs[pairIndex].Left, pairs[pairIndex].Right);
                    int take = Math.Min(pairIndex < 0 ? 3 : 2, distractors.Length);
                    textChoices = new string[take + (pairIndex < 0 ? 1 : 2)];
                    if (pairIndex >= 0) textChoices[0] = correctPair;
                    Array.Copy(distractors, 0, textChoices, pairIndex < 0 ? 0 : 1, take);
                    textChoices[^1] = none;
                    string relationName = parallel ? QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.053") : QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.054");
                    string[] prompts = new[] { QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.055", ("string_Join_names", $"{string.Join(", ", names)}"), ("relationName", $"{relationName}")), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.056", ("relationName", $"{relationName}")), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.057", ("relationName", $"{relationName}")) };
                    problem = prompts[_random.Next(prompts.Length)];
                    if (pairIndex < 0) TextAnswer(QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.058"), none, QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.059"));
                    else
                    {
                        var pair = pairs[pairIndex];
                        TextAnswer(QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.060"), correctPair,
                            QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.061", ("names_pair_Left", $"{names[pair.Left]}"), ("names_pair_Right", $"{names[pair.Right]}")),
                            QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.062", ("names_pair_Right", $"{names[pair.Right]}"), ("names_pair_Left", $"{names[pair.Left]}")),
                            QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.063", ("names_pair_Right", $"{names[pair.Right]}"), ("names_pair_Left", $"{names[pair.Left]}")));
                    }
                }
                else
                {
                    string target = parallel ? "parallel" : "perpendicular";
                    string relation = _random.Next(4) switch { 0 or 1 => target, 2 => parallel ? "perpendicular" : "parallel", _ => "intersecting" };
                    decimal difference = relation switch { "parallel" => 0, "perpendicular" => 90, _ => _random.Next(2, 6) * 15 };
                    visual = new(relation, names[..2], [], "", Lines:
                        [new(names[0], rotation, relation == "parallel" ? -.28f : 0),
                         new(names[1], rotation + difference, relation == "parallel" ? .28f : 0)]);
                    string[] prompts = new[] { QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.064", ("names_0", $"{names[0]}"), ("names_1", $"{names[1]}")), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.065", ("names_0", $"{names[0]}"), ("names_1", $"{names[1]}")), QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.066", ("names_0", $"{names[0]}"), ("names_1", $"{names[1]}")) };
                    problem = prompts[_random.Next(prompts.Length)];
                    TextAnswer(QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.067"), relation switch
                    {
                        "parallel" => QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.068"),
                        "perpendicular" => QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.069"),
                        _ => QuizContentCatalog.Text(language, "ElementaryQuizGenerator.Generate.070")
                    });
                }
                break;
            }
            case ElementaryQuizType.CountSides:
            case ElementaryQuizType.RectangleSide:
            case ElementaryQuizType.CompositeArea:
            {
                var task = type switch
                {
                    ElementaryQuizType.CountSides => CreateCountSidesTask(language, scale),
                    ElementaryQuizType.RectangleSide => CreateMissingSideTask(language, a, b, scale),
                    _ => CreateCompositeAreaTask(language, a, b, _random.Next(2, 5 + scale * 3), scale)
                };
                problem = task.ProblemText;
                visual = task.Visual;
                facts.AddRange(task.Facts);
                constants.AddRange(task.Constants);
                if (task.Hint is not null) work.Add(task.Hint);
                Answer(task.AnswerLabel, task.Expression, task.Unit);
                requiresSolution = task.RequiresSolution;
                break;
            }
        }
        var contract = new ElementaryQuizContract(kind, type, language, problem, "", facts, constants, answers, requiresSolution, visual);
        return CompleteQuestion(mode, contract, work, textChoices);
    }

    private ArithmeticQuizQuestion CompleteQuestion(ArithmeticQuizMode mode, ElementaryQuizContract contract,
        List<string> work, IReadOnlyList<string>? textChoices)
    {
        if (contract.DataChart is not null) DataChartQuestionValidator.Validate(contract);
        if (contract.AreaMeasurement is not null) AreaMeasurementQuestionValidator.Validate(contract);
        bool vi = contract.Language == AppLanguage.Vietnamese;
        string N(decimal value) => value.ToString("0.################", CultureInfo.InvariantCulture);
        IReadOnlyList<ElementaryAnswer> answers = contract.Answers;
        if (contract.Reasoning?.Explanation is { } explanation) work.Add(explanation);
        if (contract.IsComparison)
            work.Add(contract.FormatComparison(answers[0].Text!));
        // Display task-required forms, not only their normalized rational values.
        ElementaryAnswer[]? formattedAnswers = null;
        for (int i = 0; i < answers.Count; i++)
        {
            var answer = answers[i];
            if (!answer.RequireMixedNumber && answer.RequiredDenominator is null) continue;
            formattedAnswers ??= [.. answers];
            formattedAnswers[i] = answer.RequireMixedNumber ? answer with { Text = Mixed(answer.Value) }
                : answer with { Text = $"{answer.Value.Numerator * answer.RequiredDenominator!.Value / answer.Value.Denominator}/{answer.RequiredDenominator.Value}" };
        }
        if (formattedAnswers is not null)
        {
            answers = formattedAnswers;
            contract = contract with { Answers = answers };
        }
        string answerLabel = QuizContentCatalog.Text(contract.Language, "ElementaryQuizGenerator.CompleteQuestion.071");
        // Keep evaluation expressions intact; use school division notation only in the displayed work.
        string DisplayStepExpression(string expression) => QuizMathExpressionFormatter.Format(expression,
            preserveFractions: contract.Kind == QuizProblemKind.FractionSkills && contract.Reasoning?.ScenarioId != FractionWholePilot.Relation
                || contract.Type == ElementaryQuizType.ExperimentalProbability);
        if (contract.Reasoning is not null)
            foreach (var step in contract.Reasoning.Steps)
                work.Add(step.Label + ":" + Environment.NewLine + DisplayStepExpression(step.Expression)
                    + " = " + step.DisplayValue + (step.Unit.Length == 0 ? "" : " " + step.Unit));
        if (contract.Reasoning is not null && work.Count == 0 && contract.Type != ElementaryQuizType.ReadClock
            && !IsFoundationSkill(contract.Type))
            work.Add(answers[0].Label + ": " + ElementaryQuizContract.FormatAnswer(answers[0]));
        work.Add(answerLabel + ": " + contract.AnswerText);
        contract = contract with { SolutionText = string.Join(Environment.NewLine, work) };
        bool sumRatioTuple = contract.Type == ElementaryQuizType.SumRatio && answers.Count == 2
            && contract.Reasoning?.ScenarioId == SumRatioPilot.Relation;
        bool differenceRatioTuple = contract.Type == ElementaryQuizType.DifferenceRatio && answers.Count == 2
            && contract.Reasoning?.ScenarioId == DifferenceRatioPilot.Relation;
        bool pairedProposal = contract.Type == ElementaryQuizType.QuotientRemainder && answers.Count == 2 || sumRatioTuple || differenceRatioTuple;
        // Each list is scratch storage. ChoiceTexts receives its own array below.
        // Remove the slot while in use so nested generation cannot share buffers.
        var scratch = s_choiceScratch ?? new ChoiceScratch();
        s_choiceScratch = null;
        var choices = scratch.Choices;
        try
        {
            choices.Add(contract.AnswerText);
            if (textChoices is not null)
            {
                foreach (string text in textChoices)
                    if (!string.Equals(text, contract.AnswerText, StringComparison.OrdinalIgnoreCase)) choices.Add(text);
            }
            else if (contract.Type == ElementaryQuizType.ExperimentalProbability)
            {
                int total = contract.ProbabilityScenario!.TotalCount;
                int favorable = contract.ProbabilityScenario.EventCount;
                for (int delta = 1; delta <= 3; delta++)
                    choices.Add(new ReducedFraction((favorable + delta) % (total + 1), total).ToString());
            }
            else
            {
                // Reuse one contiguous proposal array across all three distractors.
                var alternative = answers.Count switch
                {
                    1 => scratch.SingleAnswer ??= new ElementaryAnswer[1],
                    2 => scratch.PairAnswers ??= new ElementaryAnswer[2],
                    _ => new ElementaryAnswer[answers.Count]
                };
                for (int delta = 1; delta <= 3; delta++)
                {
                    // Paired proposals exercise either result and both. A direct ratio
                    // sum-ratio also swaps owners; difference-ratio shifts both counts
                    // equally, preserving the difference while changing the ratio.
                    for (int index = 0; index < answers.Count; index++)
                    {
                        var answer = answers[index];
                        alternative[index] = (pairedProposal ? delta < 3 && index != delta - 1 : index != 0) ? answer
                            : sumRatioTuple && delta == 3 ? answer with { Value = answers[1 - index].Value }
                            : answer.Text is not null
                            ? answer with { Text = WrongText(answer, delta, vi) }
                            : answer with { Value = new(answer.Value.Numerator + delta * answer.Value.Denominator, answer.Value.Denominator),
                                DisplayValue = answer.DisplayValue is not null
                                    ? N((decimal)answer.Value.Numerator / (decimal)answer.Value.Denominator + delta) : null };
                    }
                    choices.Add((contract with { Answers = alternative }).AnswerText);
                }
            }
            var uniqueOptions = scratch.Unique;
            foreach (string choice in choices)
                if (uniqueOptions.Count < 4 && !uniqueOptions.Contains(choice)) uniqueOptions.Add(choice);
            // Text tasks may naturally have only three alternatives; add a localized 'unsure'.
            if (uniqueOptions.Count < 4)
            {
                string unsure = QuizContentCatalog.Text(contract.Language, "ElementaryQuizGenerator.CompleteQuestion.072");
                if (!uniqueOptions.Contains(unsure)) uniqueOptions.Add(unsure);
            }
            string[] options = uniqueOptions.ToArray();
            _random.Shuffle(options);
            bool correct = _random.Next(2) == 0;
            contract = contract with { PresentedText = correct ? contract.AnswerText
                : choices[pairedProposal ? _random.Next(1, choices.Count) : 1], ChoiceTexts = options };
            return new(new(0, ArithmeticOperation.Add, 0), mode, 0, null, correct, [], ElementaryProblem: contract);
        }
        finally
        {
            scratch.Choices.Clear();
            scratch.Unique.Clear();
            if (scratch.SingleAnswer is not null) Array.Clear(scratch.SingleAnswer);
            if (scratch.PairAnswers is not null) Array.Clear(scratch.PairAnswers);
            if (scratch.Choices.Capacity <= 16 && scratch.Unique.Capacity <= 16)
                s_choiceScratch = scratch;
        }
    }

    private static string Mixed(ReducedFraction value) => $"{value.Numerator / value.Denominator} {value.Numerator % value.Denominator}/{value.Denominator}";
    private static string WrongText(ElementaryAnswer answer, int delta, bool vi)
    {
        if (answer.Text is ">" or "<" or "=")
            return SelectWrongOption(answer.Text, delta, wrap: true, ">", "<", "=");
        if (answer.RequireMixedNumber) return Mixed(new(answer.Value.Numerator + delta * answer.Value.Denominator, answer.Value.Denominator));
        if (answer.RequiredDenominator is int denominator) return (answer.Value.Numerator * denominator / answer.Value.Denominator + delta).ToString() + "/" + denominator;
        var language = QuizContentCatalog.CompatibilityLanguage(vi);
        if (EqualsWrongText(answer.Text, "góc nhọn") || EqualsWrongText(answer.Text, "góc vuông")
            || EqualsWrongText(answer.Text, "góc tù") || EqualsWrongText(answer.Text, "góc bẹt")
            || EqualsWrongText(answer.Text, "acute") || EqualsWrongText(answer.Text, "right")
            || EqualsWrongText(answer.Text, "obtuse") || EqualsWrongText(answer.Text, "straight"))
            return SelectWrongOption(answer.Text, delta, wrap: false,
                QuizContentCatalog.Text(language, "ElementaryQuizGenerator.WrongText.073"),
                QuizContentCatalog.Text(language, "ElementaryQuizGenerator.WrongText.074"),
                QuizContentCatalog.Text(language, "ElementaryQuizGenerator.WrongText.075"),
                QuizContentCatalog.Text(language, "ElementaryQuizGenerator.WrongText.076"));
        if (EqualsWrongText(answer.Text, "song song") || EqualsWrongText(answer.Text, "vuông góc")
            || EqualsWrongText(answer.Text, "cắt nhau nhưng không vuông góc")
            || EqualsWrongText(answer.Text, "parallel") || EqualsWrongText(answer.Text, "perpendicular")
            || EqualsWrongText(answer.Text, "intersecting but not perpendicular"))
            return SelectWrongOption(answer.Text, delta, wrap: false,
                QuizContentCatalog.Text(language, "ElementaryQuizGenerator.WrongText.077"),
                QuizContentCatalog.Text(language, "ElementaryQuizGenerator.WrongText.078"),
                QuizContentCatalog.Text(language, "ElementaryQuizGenerator.WrongText.079"),
                QuizContentCatalog.Text(language, "ElementaryQuizGenerator.WrongText.080"));
        return SelectWrongOption(answer.Text, delta, wrap: true,
            QuizContentCatalog.Text(language, "ElementaryQuizGenerator.WrongText.081"),
            QuizContentCatalog.Text(language, "ElementaryQuizGenerator.WrongText.082"),
            QuizContentCatalog.Text(language, "ElementaryQuizGenerator.WrongText.083"));
    }

    private static bool EqualsWrongText(string? actual, string expected)
        => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static string SelectWrongOption(string? current, int delta, bool wrap,
        string first, string second, string third, string? fourth = null)
    {
        int count = (EqualsWrongText(current, first) ? 0 : 1)
            + (EqualsWrongText(current, second) ? 0 : 1)
            + (EqualsWrongText(current, third) ? 0 : 1)
            + (fourth is null || EqualsWrongText(current, fourth) ? 0 : 1);
        int index = wrap ? (delta - 1) % count : delta - 1;
        if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(delta));
        if (!EqualsWrongText(current, first) && index-- == 0) return first;
        if (!EqualsWrongText(current, second) && index-- == 0) return second;
        if (!EqualsWrongText(current, third) && index-- == 0) return third;
        if (fourth is not null && !EqualsWrongText(current, fourth) && index == 0) return fourth;
        throw new InvalidOperationException("No matching wrong-answer option.");
    }
}
