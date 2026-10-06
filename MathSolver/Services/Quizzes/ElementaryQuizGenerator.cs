using MathSolver.Models;
using MathSolver.Services.Core;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>C# math puzzle data and rules.</summary>
public sealed partial class ElementaryQuizGenerator(Random? random = null)
{
    private readonly Random _random = random ?? Random.Shared;
    public static bool Supports(QuizProblemKind kind) => Types(kind).Count > 0;
    public static IReadOnlyList<ElementaryQuizType> Types(QuizProblemKind kind) => kind switch
    {
        QuizProblemKind.TwoNumbers => [ElementaryQuizType.SumDifference, ElementaryQuizType.SumRatio, ElementaryQuizType.DifferenceRatio],
        QuizProblemKind.Measurement => [ElementaryQuizType.LengthConversion, ElementaryQuizType.MassConversion, ElementaryQuizType.CapacityConversion, ElementaryQuizType.AreaConversion, ElementaryQuizType.VolumeConversion, ElementaryQuizType.MixedLength],
        QuizProblemKind.Time => [ElementaryQuizType.ElapsedTime, ElementaryQuizType.TimeAddition, ElementaryQuizType.ReadClock, ElementaryQuizType.Calendar],
        QuizProblemKind.Remainder => [ElementaryQuizType.QuotientRemainder, ElementaryQuizType.MinimumGroups, ElementaryQuizType.Leftovers],
        QuizProblemKind.Decimal => [ElementaryQuizType.DecimalAdd, ElementaryQuizType.DecimalSubtract, ElementaryQuizType.DecimalMultiply, ElementaryQuizType.DecimalDivide, ElementaryQuizType.DecimalRound, ElementaryQuizType.DecimalCompare],
        QuizProblemKind.FractionSkills => [ElementaryQuizType.ReduceFraction, ElementaryQuizType.MixedNumber, ElementaryQuizType.CommonDenominator, ElementaryQuizType.FractionOfNumber, ElementaryQuizType.WholeFromFraction],
        QuizProblemKind.Data => [ElementaryQuizType.ReadTable, ElementaryQuizType.ReadBarChart, ElementaryQuizType.ReadPieChart, ElementaryQuizType.ChartTotal, ElementaryQuizType.ChartDifference],
        QuizProblemKind.Probability => [ElementaryQuizType.Likelihood, ElementaryQuizType.ExperimentalProbability],
        QuizProblemKind.VisualGeometry => [ElementaryQuizType.ClassifyAngle, ElementaryQuizType.ParallelLines, ElementaryQuizType.PerpendicularLines, ElementaryQuizType.CountSides, ElementaryQuizType.RectangleSide, ElementaryQuizType.CompositeArea],
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
        IReadOnlyList<ElementaryQuizType> types = (kind, selected) switch
        {
            (QuizProblemKind.Arithmetic, ElementaryQuizType.IntegerCompare) => [ElementaryQuizType.IntegerCompare],
            (QuizProblemKind.Fraction, ElementaryQuizType.CompareFractions) => [ElementaryQuizType.CompareFractions],
            _ => Types(kind)
        };
        if (types.Count == 0) throw new ArgumentOutOfRangeException(nameof(kind));
        var type = selected.HasValue && types.Contains(selected.Value) ? selected.Value : types[_random.Next(types.Count)];
        if (CreateDifficultyContract(kind, type, language, tier) is { } difficultyContract)
            return CompleteQuestion(mode, difficultyContract, [], null);
        bool vi = language == AppLanguage.Vietnamese;
        string L(string vietnamese, string english) => vi ? vietnamese : english;
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
        void Constant(params decimal[] values) => constants.AddRange(values.Select(N));
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
            if (calculation) work.Add((displayExpression ?? expression) + " = " + ElementaryQuizContract.FormatAnswer(answer));
        }
        void TextAnswer(string label, string text, params string[] aliases)
        {
            answers.Add(new(label, new(0, 1), "", "", text, aliases));
            work.Add(label + ":");
            requiresSolution = false;
        }
        switch (type)
        {
            case ElementaryQuizType.IntegerCompare:
            {
                a = QuizCurriculumLayer.NextPrimaryOperand(_random, tier);
                b = QuizCurriculumLayer.NextSecondaryOperand(_random, tier);
                int relation = _random.Next(3);
                if (relation == 0) b = a;
                else
                {
                    if (a == b) b = a == 1 ? 2 : a - 1;
                    if ((relation == 1 && a > b) || (relation == 2 && a < b)) (a, b) = (b, a);
                }
                Fact(a, b);
                problem = L($"So sánh {a} và {b}.", $"Compare {a} and {b}.");
                TextAnswer(L("Dấu so sánh", "Comparison"), a < b ? "<" : a > b ? ">" : "=");
                break;
            }
            case ElementaryQuizType.QuotientRemainder:
            case ElementaryQuizType.MinimumGroups:
            case ElementaryQuizType.Leftovers:
            {
                int divisor = b, dividend = a * divisor + _random.Next(divisor);
                int quotient = Math.DivRem(dividend, divisor, out int remainder);
                a = quotient;
                Fact(dividend, divisor); Constant(a, remainder);
                if (type == ElementaryQuizType.MinimumGroups)
                {
                    problem = L($"Có {dividend} học sinh. Mỗi xe chở tối đa {divisor} học sinh. Cần ít nhất bao nhiêu xe?",
                        $"There are {dividend} students. Each vehicle holds at most {divisor} students. How many vehicles are needed?");
                    work.Add($"{dividend}-{a}*{divisor}={remainder}");
                    Answer(L("Số xe cần", "Vehicles needed"), $"({dividend}-{remainder})/{divisor}" + (remainder > 0 ? "+1" : ""), L("xe", "vehicles"));
                }
                else
                {
                    problem = type == ElementaryQuizType.QuotientRemainder
                        ? L($"Chia {dividend} cho {divisor}. Tìm thương và số dư.", $"Divide {dividend} by {divisor}. Find quotient and remainder.")
                        : L($"Có {dividend} quả cam, mỗi hộp chứa {divisor} quả. Đóng được bao nhiêu hộp đầy và còn dư bao nhiêu quả?",
                            $"There are {dividend} oranges, with {divisor} in each box. How many full boxes and how many oranges left over?");
                    Answer(type == ElementaryQuizType.Leftovers ? L("Số hộp đầy", "Full boxes") : L("Thương", "Quotient"), $"({dividend}-{remainder})/{divisor}", type == ElementaryQuizType.Leftovers ? L("hộp", "boxes") : "");
                    Answer(type == ElementaryQuizType.Leftovers ? L("Số cam còn lại", "Oranges left over") : L("Số dư", "Remainder"), $"{dividend}-{a}*{divisor}", type == ElementaryQuizType.Leftovers ? L("quả", "oranges") : "");
                }
                break;
            }
            case ElementaryQuizType.DecimalAdd:
            case ElementaryQuizType.DecimalSubtract:
            case ElementaryQuizType.DecimalMultiply:
            case ElementaryQuizType.DecimalDivide:
            case ElementaryQuizType.DecimalRound:
            case ElementaryQuizType.DecimalCompare:
            {
                decimal x = a + _random.Next(1, 10) / 10m, y = b + _random.Next(1, 10) / 10m;
                if (type == ElementaryQuizType.DecimalCompare)
                {
                    int comparison = _random.Next(3);
                    y = comparison == 0 ? x : comparison == 1 ? x - _random.Next(1, 10) / 10m : x + _random.Next(1, 10) / 10m;
                    Fact(x, y); problem = L($"Điền dấu so sánh giữa {N(x)} và {N(y)}.", $"Compare {N(x)} and {N(y)}.");
                    TextAnswer(L("Dấu so sánh", "Comparison"), x > y ? ">" : x < y ? "<" : "="); break;
                }
                if (type == ElementaryQuizType.DecimalRound)
                {
                    Fact(x); Constant(decimal.Round(x, 0, MidpointRounding.AwayFromZero));
                    problem = L($"Làm tròn {N(x)} đến số nguyên gần nhất.", $"Round {N(x)} to the nearest integer (halves round up).");
                    Answer(L("Số sau khi làm tròn", "Rounded number"), N(decimal.Round(x, 0, MidpointRounding.AwayFromZero)));
                    requiresSolution = false; break;
                }
                string op = type switch { ElementaryQuizType.DecimalAdd => "+", ElementaryQuizType.DecimalSubtract => "-", ElementaryQuizType.DecimalMultiply => "*", _ => "/" };
                var operation = type switch
                {
                    ElementaryQuizType.DecimalAdd => ArithmeticOperation.Add,
                    ElementaryQuizType.DecimalSubtract => ArithmeticOperation.Subtract,
                    ElementaryQuizType.DecimalMultiply => ArithmeticOperation.Multiply,
                    _ => ArithmeticOperation.Divide
                };
                if (op == "/") x = a * y;
                if (op == "-" && x < y) (x, y) = (y, x);
                string displayExpression = $"{N(x)} {BasicArithmeticEngine.GetSymbol(operation)} {N(y)}";
                Fact(x, y); problem = L($"Tính {displayExpression}.", $"Calculate {displayExpression}.");
                Answer(L("Kết quả", "Result"), $"{N(x)}{op}{N(y)}", displayExpression: displayExpression); requiresSolution = false; break;
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
                problem = L($"So sánh {numerator}/{denominator} và {otherNumerator}/{otherDenominator}.",
                    $"Compare {numerator}/{denominator} and {otherNumerator}/{otherDenominator}.");
                int comparison = ((long)numerator * otherDenominator).CompareTo((long)otherNumerator * denominator);
                TextAnswer(L("Dấu so sánh", "Comparison"), comparison < 0 ? "<" : comparison > 0 ? ">" : "=");
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
                string[] prompts = vi
                    ? [$"Quan sát hình: góc {name} là góc nhọn, góc vuông, góc tù hay góc bẹt?",
                       $"Góc {name} trong hình thuộc loại góc nào?",
                       $"Hai tia {labels[1]}{labels[0]} và {labels[1]}{labels[2]} tạo thành loại góc nào?",
                       $"Hãy gọi tên loại góc có đỉnh {labels[1]} trong hình."]
                    : [$"Is angle {name} in the figure acute, right, obtuse or straight?",
                       $"What type of angle is {name} in the figure?",
                       $"What type of angle is formed by rays {labels[1]}{labels[0]} and {labels[1]}{labels[2]}?",
                       $"Identify the type of angle with vertex {labels[1]} in the figure."];
                problem = prompts[_random.Next(prompts.Length)];
                string[] angleNames = vi ? ["Góc Nhọn", "Góc Vuông", "Góc Tù", "Góc Bẹt"] : ["Acute", "Right", "Obtuse", "Straight"];
                TextAnswer(L("Loại góc", "Angle type"), angleNames[index], vi ? new[] { "nhọn", "vuông", "tù", "bẹt" }[index] : angleNames[index]);
                break;
            }
            case ElementaryQuizType.ParallelLines:
            case ElementaryQuizType.PerpendicularLines:
            {
                bool parallel = type == ElementaryQuizType.ParallelLines;
                bool choosePair = scale >= 4 || scale == 3 && _random.Next(2) == 0;
                decimal rotation = scale == 1 ? _random.Next(2) * 90 : scale == 2 ? _random.Next(4) * 45 : _random.Next(12) * 15;
                string[][] nameSets = [["a", "b", "c", "d", "e"], ["d", "e", "f", "g", "h"], ["m", "n", "p", "q", "r"]];
                string[] names = nameSets[_random.Next(nameSets.Length)];
                if (choosePair)
                {
                    bool hasPair = _random.Next(4) != 0;
                    int lineCount = scale >= 4 ? scale : 3;
                    names = names[..lineCount];
                    decimal[] directions = hasPair
                        ? parallel ? [0, 0, 25, 55, 120] : [0, 90, 25, 55, 120]
                        : [0, 25, 55, 120, 155];
                    directions = directions[..lineCount].Select(direction => direction + rotation).ToArray();
                    _random.Shuffle(directions);
                    var lines = names.Select((label, lineIndex) => new QuizVisualLine(label, directions[lineIndex],
                        parallel && hasPair ? (lineIndex - (lineCount - 1) / 2f) * .22f : 0)).ToArray();
                    visual = new("line-pairs", names, [], "", Lines: lines);
                    string Pair(int left, int right) => L($"Đường {names[left]} Và Đường {names[right]}", $"Lines {names[left]} and {names[right]}");
                    string none = parallel ? L("Không Có Cặp Song Song", "No parallel pair") : L("Không Có Cặp Vuông Góc", "No perpendicular pair");
                    int pairIndex = -1;
                    var pairs = (from left in Enumerable.Range(0, lineCount)
                                 from right in Enumerable.Range(left + 1, lineCount - left - 1)
                                 select (Left: left, Right: right)).ToArray();
                    for (int candidate = 0; candidate < pairs.Length; candidate++)
                    {
                        var pair = pairs[candidate];
                        decimal difference = Math.Abs(directions[pair.Left] - directions[pair.Right]) % 180;
                        if (difference == (parallel ? 0 : 90)) pairIndex = candidate;
                    }
                    string[] distractors = pairs.Where((_, candidate) => candidate != pairIndex)
                        .Select(pair => Pair(pair.Left, pair.Right)).ToArray();
                    _random.Shuffle(distractors);
                    string correctPair = pairIndex < 0 ? none : Pair(pairs[pairIndex].Left, pairs[pairIndex].Right);
                    textChoices = pairIndex < 0 ? [.. distractors.Take(3), none]
                        : [correctPair, .. distractors.Take(2), none];
                    string relationName = parallel ? L("song song", "parallel") : L("vuông góc", "perpendicular");
                    string[] prompts = vi
                        ? [$"Trong các đường thẳng {string.Join(", ", names)}, cặp đường nào {relationName}? Nếu không có cặp phù hợp, ghi không có.",
                           $"Quan sát hình và tìm cặp đường thẳng {relationName}. Nếu không có, hãy cho biết không có cặp phù hợp.",
                           $"Cặp nào trong hình có quan hệ {relationName}? Chọn cặp đúng hoặc cho biết không có."]
                        : [$"Which pair among lines {string.Join(", ", names)} is {relationName}? If there is none, say there is no such pair.",
                           $"Find the {relationName} pair of lines in the figure, or state that there is no such pair.",
                           $"Which pair in the figure is {relationName}? Choose the pair or state that none exists."];
                    problem = prompts[_random.Next(prompts.Length)];
                    if (pairIndex < 0) TextAnswer(L("Cặp đường thẳng", "Pair of lines"), none, L("không có", "none"));
                    else
                    {
                        var pair = pairs[pairIndex];
                        TextAnswer(L("Cặp đường thẳng", "Pair of lines"), correctPair,
                            L($"{names[pair.Left]} và {names[pair.Right]}", $"{names[pair.Left]} and {names[pair.Right]}"),
                            L($"{names[pair.Right]} và {names[pair.Left]}", $"{names[pair.Right]} and {names[pair.Left]}"),
                            L($"Đường {names[pair.Right]} Và Đường {names[pair.Left]}", $"Lines {names[pair.Right]} and {names[pair.Left]}"));
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
                    string[] prompts = vi
                        ? [$"Hai đường thẳng {names[0]} và {names[1]} trong hình có quan hệ gì?",
                           $"Quan sát hình: {names[0]} và {names[1]} là hai đường song song, vuông góc hay cắt nhau nhưng không vuông góc?",
                           $"Hãy xác định quan hệ giữa hai đường kẻ {names[0]} và {names[1]}." ]
                        : [$"What is the relationship between lines {names[0]} and {names[1]} in the figure?",
                           $"Are lines {names[0]} and {names[1]} parallel, perpendicular, or intersecting without a right angle?",
                           $"Identify the relationship between the two drawn lines {names[0]} and {names[1]}." ];
                    problem = prompts[_random.Next(prompts.Length)];
                    TextAnswer(L("Quan hệ hai đường", "Line relationship"), relation switch
                    {
                        "parallel" => L("Song Song", "Parallel"),
                        "perpendicular" => L("Vuông Góc", "Perpendicular"),
                        _ => L("Cắt Nhau Nhưng Không Vuông Góc", "Intersecting but not perpendicular")
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
        List<string> work, string[]? textChoices)
    {
        bool vi = contract.Language == AppLanguage.Vietnamese;
        string L(string a, string b) => vi ? a : b;
        string N(decimal value) => value.ToString("0.################", CultureInfo.InvariantCulture);
        var answers = contract.Answers.ToList();
        if (contract.Reasoning?.Explanation is { } explanation) work.Add(explanation);
        if (contract.IsComparison)
            work.Add(contract.FormatComparison(answers[0].Text!));
        // Display task-required forms, not only their normalized rational values.
        answers = answers.Select(answer => answer.RequireMixedNumber ? answer with { Text = Mixed(answer.Value) }
            : answer.RequiredDenominator is int denominator ? answer with { Text = $"{answer.Value.Numerator * denominator / answer.Value.Denominator}/{denominator}" } : answer).ToList();
        contract = contract with { Answers = answers };
        string answerLabel = L("Đáp số", "Answer");
        if (contract.Reasoning is not null)
            work.AddRange(contract.Reasoning.Steps.Select(step => step.Label + ":" + Environment.NewLine +
                step.Expression + " = " + step.DisplayValue + (step.Unit.Length == 0 ? "" : " " + step.Unit)));
        if (contract.Reasoning is not null && work.Count == 0) work.Add(answers[0].Label + ":");
        contract = contract with { SolutionText = string.Join(Environment.NewLine, work) + Environment.NewLine + answerLabel + ": " + contract.AnswerText };
        var choices = new List<string> { contract.AnswerText };
        if (textChoices is not null)
            choices.AddRange(textChoices.Where(text => !string.Equals(text, contract.AnswerText, StringComparison.OrdinalIgnoreCase)));
        else if (contract.Type == ElementaryQuizType.ExperimentalProbability)
        {
            int total = contract.ProbabilityScenario!.TotalCount;
            int favorable = contract.ProbabilityScenario.EventCount;
            choices.AddRange(Enumerable.Range(1, 3).Select(delta =>
                new ReducedFraction((favorable + delta) % (total + 1), total).ToString()));
        }
        else for (int delta = 1; delta <= 3; delta++)
        {
            var alternative = answers.Select((answer, index) => index != 0 ? answer : answer.Text is not null
                ? answer with { Text = WrongText(answer, delta, vi) }
                : answer with { Value = new(answer.Value.Numerator + delta * answer.Value.Denominator, answer.Value.Denominator),
                    DisplayValue = answer.DisplayValue is not null
                        ? N((decimal)answer.Value.Numerator / (decimal)answer.Value.Denominator + delta) : null }).ToArray();
            choices.Add((contract with { Answers = alternative }).AnswerText);
        }
        string[] options = choices.Distinct().ToArray();
        // Text tasks may naturally have only three alternatives; add a localized 'unsure'.
        if (options.Length < 4) options = [.. options, L("Không xác định", "Undetermined")];
        options = options.Distinct().Take(4).ToArray();
        _random.Shuffle(options);
        bool correct = _random.Next(2) == 0;
        contract = contract with { PresentedText = correct ? contract.AnswerText : choices[1], ChoiceTexts = options };
        return new(new(0, ArithmeticOperation.Add, 0), mode, 0, null, correct, [], ElementaryProblem: contract);
    }

    private static string Mixed(ReducedFraction value) => $"{value.Numerator / value.Denominator} {value.Numerator % value.Denominator}/{value.Denominator}";
    private static string WrongText(ElementaryAnswer answer, int delta, bool vi)
    {
        if (answer.Text is ">" or "<" or "=") return new[] { ">", "<", "=" }.Where(text => text != answer.Text).ElementAt((delta - 1) % 2);
        if (answer.RequireMixedNumber) return Mixed(new(answer.Value.Numerator + delta * answer.Value.Denominator, answer.Value.Denominator));
        if (answer.RequiredDenominator is int denominator) return (answer.Value.Numerator * denominator / answer.Value.Denominator + delta).ToString() + "/" + denominator;
        string? normalizedText = answer.Text?.ToLowerInvariant();
        if (normalizedText is "góc nhọn" or "góc vuông" or "góc tù" or "góc bẹt" or "acute" or "right" or "obtuse" or "straight")
            return (vi ? new[] { "Góc Nhọn", "Góc Vuông", "Góc Tù", "Góc Bẹt" } : new[] { "Acute", "Right", "Obtuse", "Straight" })
                .Where(text => !string.Equals(text, answer.Text, StringComparison.OrdinalIgnoreCase)).ElementAt(delta - 1);
        if (normalizedText is "song song" or "vuông góc" or "cắt nhau nhưng không vuông góc" or "parallel" or "perpendicular" or "intersecting but not perpendicular")
            return (vi ? new[] { "Song Song", "Vuông Góc", "Cắt Nhau Nhưng Không Vuông Góc", "Trùng Nhau" }
                : new[] { "Parallel", "Perpendicular", "Intersecting but not perpendicular", "Coincident" })
                .Where(text => !string.Equals(text, answer.Text, StringComparison.OrdinalIgnoreCase)).ElementAt(delta - 1);
        string[] likelihoodChoices = (vi ? new[] { "Chắc Chắn", "Có Thể", "Không Thể" }
            : new[] { "certain", "possible", "impossible" })
            .Where(text => !string.Equals(text, answer.Text, StringComparison.OrdinalIgnoreCase)).ToArray();
        return likelihoodChoices[(delta - 1) % likelihoodChoices.Length];
    }
}
