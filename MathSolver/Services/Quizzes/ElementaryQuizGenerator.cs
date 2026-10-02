using MathSolver.Models;
using MathSolver.Services.Core;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>Elementary practice contracts, shared by Algorithm and local AI generation.</summary>
public sealed class ElementaryQuizGenerator(Random? random = null)
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
        QuizProblemKind.FractionSkills => [ElementaryQuizType.ReduceFraction, ElementaryQuizType.CompareFractions, ElementaryQuizType.MixedNumber, ElementaryQuizType.CommonDenominator, ElementaryQuizType.FractionOfNumber, ElementaryQuizType.WholeFromFraction],
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
            case ElementaryQuizType.SumDifference:
            case ElementaryQuizType.SumRatio:
            case ElementaryQuizType.DifferenceRatio:
            {
                int smallParts = _random.Next(1, 4), largeParts = smallParts + _random.Next(1, 4);
                int small = smallParts * a, large = largeParts * a, sum = small + large, difference = large - small;
                string smallLabel = L("Số bé", "Smaller number"), largeLabel = L("Số lớn", "Larger number");
                if (type == ElementaryQuizType.SumDifference)
                {
                    Fact(sum, difference); Constant(2);
                    problem = L($"Tổng hai số là {sum}, hiệu số lớn và số bé là {difference}. Tìm hai số đó.",
                        $"Two numbers have a sum of {sum} and the larger minus the smaller is {difference}. Find both numbers.");
                    Answer(smallLabel, $"({sum}-{difference})/2");
                    Answer(largeLabel, $"{sum}-({sum}-{difference})/2");
                }
                else
                {
                    int given = type == ElementaryQuizType.SumRatio ? sum : difference;
                    Fact(given, smallParts, largeParts);
                    string op = type == ElementaryQuizType.SumRatio ? "+" : "-";
                    string partExpression = type == ElementaryQuizType.SumRatio ? $"{smallParts}+{largeParts}" : $"{largeParts}-{smallParts}";
                    problem = L($"{(op == "+" ? "Tổng" : "Hiệu số lớn và số bé")} hai số là {given}. Tỉ số của số bé và số lớn là {smallParts}/{largeParts}. Tìm hai số đó.",
                        $"The {(op == "+" ? "sum" : "difference (larger minus smaller)")} is {given}. The ratio of smaller to larger is {smallParts}/{largeParts}. Find both numbers.");
                    work.Add(L("Giá trị một phần là:", "The value of one part is:"));
                    work.Add($"{given}/({partExpression})={a}");
                    Answer(smallLabel, $"{given}/({partExpression})*{smallParts}");
                    Answer(largeLabel, $"{given}/({partExpression})*{largeParts}");
                }
                break;
            }
            case ElementaryQuizType.LengthConversion:
            case ElementaryQuizType.MassConversion:
            case ElementaryQuizType.CapacityConversion:
            case ElementaryQuizType.AreaConversion:
            case ElementaryQuizType.VolumeConversion:
            case ElementaryQuizType.MixedLength:
            {
                (string categoryId, string fromId, string toId) = type switch
                {
                    ElementaryQuizType.MassConversion => ("mass", "kg", "g"),
                    ElementaryQuizType.CapacityConversion => ("capacity", "l", "ml"),
                    ElementaryQuizType.AreaConversion => ("area", "m2", "cm2"),
                    ElementaryQuizType.VolumeConversion => ("volume", "dm3", "cm3"),
                    _ => ("length", "m", "cm")
                };
                var units = MeasurementEngine.GetCategory(categoryId).Units;
                var fromUnit = units.First(unit => unit.Id == fromId);
                var toUnit = units.First(unit => unit.Id == toId);
                decimal factor = MeasurementEngine.Convert(1, fromUnit, toUnit);
                string from = fromUnit.Symbol, to = toUnit.Symbol;
                Fact(a); Constant(factor);
                bool mixed = type == ElementaryQuizType.MixedLength;
                bool reverse = !mixed && _random.Next(2) == 0;
                if (reverse) (from, to) = (to, from);
                if (mixed) Fact(b);
                problem = L($"Đổi {a} {from}{(mixed ? $" {b} {to}" : "")} sang {to}.",
                    $"Convert {a} {from}{(mixed ? $" {b} {to}" : "")} to {to}.");
                Answer(L("Số đo sau khi đổi", "Converted measurement"), $"{a}{(reverse ? "/" : "*")}{N(factor)}" + (mixed ? $"+{b}" : ""), to);
                break;
            }
            case ElementaryQuizType.ElapsedTime:
            case ElementaryQuizType.ReadClock:
            {
                int hour = _random.Next(1, 11), minute = _random.Next(0, 12) * 5, duration = a * 5 + b;
                Fact(hour, minute);
                visual = new("clock", [], [hour, minute], "");
                if (type == ElementaryQuizType.ReadClock)
                {
                    problem = L("Đồng hồ chỉ mấy giờ, bao nhiêu phút?", "What hour and minute does the clock show?");
                    Answer(L("Giờ", "Hour"), hour.ToString());
                    Answer(L("Phút", "Minute"), minute.ToString());
                    requiresSolution = false;
                }
                else
                {
                    int end = hour * 60 + minute + duration;
                    Fact(end / 60, end % 60); Constant(60);
                    problem = L($"Bắt đầu lúc {hour}:{minute:00}, kết thúc lúc {end / 60}:{end % 60:00} trong cùng ngày. Thời gian kéo dài bao nhiêu phút?",
                        $"Start at {hour}:{minute:00} and finish at {end / 60}:{end % 60:00} on the same day. How many minutes elapse?");
                    Answer(L("Thời gian kéo dài", "Elapsed time"), $"({end / 60}*60+{end % 60})-({hour}*60+{minute})", L("phút", "minutes"));
                }
                break;
            }
            case ElementaryQuizType.TimeAddition:
                Fact(a, b); Constant(60);
                problem = L($"Tính {a} giờ + {b} phút, trả lời theo phút.", $"Add {a} hours and {b} minutes. Answer in minutes.");
                Answer(L("Thời gian", "Time"), $"{a}*60+{b}", L("phút", "minutes")); break;
            case ElementaryQuizType.Calendar:
                a = _random.Next(1, 15); b = _random.Next(2, 15);
                Fact(a, b);
                problem = L($"Trong tháng 10, từ đầu ngày {a} đến đầu ngày {a + b} cách nhau bao nhiêu ngày?",
                    $"In October, how many days pass from the start of day {a} to the start of day {a + b}?");
                facts.Clear(); Fact(a, a + b);
                Answer(L("Số ngày", "Days elapsed"), $"{a + b}-{a}", L("ngày", "days")); break;
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
            case ElementaryQuizType.ReduceFraction:
            case ElementaryQuizType.CompareFractions:
            case ElementaryQuizType.MixedNumber:
            case ElementaryQuizType.CommonDenominator:
            case ElementaryQuizType.FractionOfNumber:
            case ElementaryQuizType.WholeFromFraction:
            {
                int denominator = kind == QuizProblemKind.Fraction
                    ? QuizCurriculumLayer.NextPrimaryOperand(_random, tier, minimumAllowed: 2) : _random.Next(3, 9);
                int numerator = kind == QuizProblemKind.Fraction
                    ? QuizCurriculumLayer.NextSecondaryOperand(_random, tier, maximumOverride: denominator - 1)
                    : _random.Next(1, denominator);
                if (type == ElementaryQuizType.ReduceFraction)
                {
                    Fact(numerator * 2, denominator * 2);
                    problem = L($"Rút gọn {numerator * 2}/{denominator * 2} đến tối giản.", $"Reduce {numerator * 2}/{denominator * 2} to lowest terms.");
                    Answer(L("Phân số tối giản", "Reduced fraction"), $"{numerator * 2}/{denominator * 2}", reduced: true);
                }
                else if (type == ElementaryQuizType.MixedNumber)
                {
                    int n = a * denominator + numerator;
                    Fact(n, denominator); problem = L($"Viết {n}/{denominator} dưới dạng hỗn số.", $"Write {n}/{denominator} as a mixed number.");
                    Answer(L("Hỗn số", "Mixed number"), $"{n}/{denominator}", mixed: true);
                }
                else if (type == ElementaryQuizType.CompareFractions)
                {
                    int otherDenominator = scale < 3 ? denominator : kind == QuizProblemKind.Fraction
                        ? QuizCurriculumLayer.NextPrimaryOperand(_random, tier, minimumAllowed: 2) : _random.Next(3, 9);
                    int otherNumerator = _random.Next(1, otherDenominator);
                    if (_random.Next(3) == 0)
                    {
                        int factor = scale < 3 ? 1 : kind == QuizProblemKind.Fraction
                            ? _random.Next(1, Math.Min(scale, QuizCurriculumLayer.GetMaximumOperandValue(tier) / denominator) + 1)
                            : _random.Next(2, scale + 1);
                        otherNumerator = numerator * factor;
                        otherDenominator = denominator * factor;
                    }
                    Fact(numerator, denominator, otherNumerator, otherDenominator);
                    problem = L($"So sánh {numerator}/{denominator} và {otherNumerator}/{otherDenominator}.",
                        $"Compare {numerator}/{denominator} and {otherNumerator}/{otherDenominator}.");
                    int comparison = ((long)numerator * otherDenominator).CompareTo((long)otherNumerator * denominator);
                    TextAnswer(L("Dấu so sánh", "Comparison"), comparison < 0 ? "<" : comparison > 0 ? ">" : "=");
                }
                else if (type == ElementaryQuizType.CommonDenominator)
                {
                    int otherNumerator = _random.Next(1, denominator * 2);
                    Fact(numerator, otherNumerator, denominator, denominator * 2); Constant(2);
                    problem = L($"Quy đồng {numerator}/{denominator} và {otherNumerator}/{denominator * 2} về mẫu số {denominator * 2}.",
                        $"Express {numerator}/{denominator} and {otherNumerator}/{denominator * 2} with denominator {denominator * 2}.");
                    Answer(L("Phân số thứ nhất", "First fraction"), $"{numerator}/{denominator}", denominator: denominator * 2);
                    Answer(L("Phân số thứ hai", "Second fraction"), $"{otherNumerator}/{denominator * 2}", denominator: denominator * 2);
                }
                else
                {
                    int whole = a * denominator, part = a * numerator;
                    IReadOnlyList<FractionQuantityStoryContext> contexts = FractionQuantityStoryContextCatalog.GetProfile(language);
                    FractionQuantityStoryContext context = contexts[_random.Next(contexts.Count)];
                    string fraction = $"{numerator}/{denominator}";
                    if (type == ElementaryQuizType.FractionOfNumber)
                    {
                        Fact(whole, numerator, denominator);
                        problem = string.Format(CultureInfo.InvariantCulture, context.PartProblemTemplate, whole, fraction);
                        Answer(context.PartLabel, $"{whole}*{numerator}/{denominator}", context.Unit,
                            displayExpression: $"{whole} × {numerator}/{denominator}");
                    }
                    else
                    {
                        Fact(part, numerator, denominator);
                        problem = string.Format(CultureInfo.InvariantCulture, context.WholeProblemTemplate, part, fraction);
                        Answer(context.WholeLabel, $"{part}/{numerator}*{denominator}", context.Unit,
                            displayExpression: $"{part} ÷ ({numerator}/{denominator})");
                    }
                }
                if (type is not (ElementaryQuizType.FractionOfNumber or ElementaryQuizType.WholeFromFraction)) requiresSolution = false;
                break;
            }
            case ElementaryQuizType.ReadTable:
            case ElementaryQuizType.ReadBarChart:
            case ElementaryQuizType.ReadPieChart:
            case ElementaryQuizType.ChartTotal:
            case ElementaryQuizType.ChartDifference:
            {
                IReadOnlyList<DataChartStoryContext> contexts = DataChartStoryContextCatalog.GetProfile(language);
                DataChartStoryContext context = contexts[_random.Next(contexts.Count)];
                string[] labels = context.Labels.ToArray();
                _random.Shuffle(labels);
                bool pie = type == ElementaryQuizType.ReadPieChart;
                decimal[] values = [a * 2, b * 2, a + b];
                if (pie)
                {
                    int first = _random.Next(2, 11) * 5;
                    int second = _random.Next(2, (100 - first) / 5 - 1) * 5;
                    values = [first, second, 100 - first - second];
                }
                string unit = pie ? "%" : context.Unit;
                string visualKind = type == ElementaryQuizType.ReadTable ? "table" : pie ? "pie" : "bar";
                string visualName = visualKind switch
                {
                    "table" => L("bảng", "table"),
                    "pie" => L("biểu đồ tròn", "pie chart"),
                    _ => L("biểu đồ cột", "bar chart")
                };
                visual = new(visualKind, labels, values, unit);
                Fact(values);
                string introduction = L($"{char.ToUpperInvariant(visualName[0])}{visualName[1..]} thống kê {context.Description}{(pie ? " theo tỉ lệ phần trăm" : "")}.",
                    $"The {visualName} shows {context.Description}{(pie ? " as percentages" : "")}.");
                string questionText, chartAnswerLabel, expression;
                if (type == ElementaryQuizType.ChartTotal)
                {
                    questionText = L($"Tổng {context.QuantityName} trong {visualName} là bao nhiêu {unit}?",
                        $"What is the total {context.QuantityName} shown (in {unit})?");
                    chartAnswerLabel = L($"Tổng {context.QuantityName}", $"Total {context.QuantityName}");
                    expression = string.Join("+", values.Select(N));
                }
                else if (type == ElementaryQuizType.ChartDifference)
                {
                    int firstRow = _random.Next(labels.Length);
                    int secondRow = (firstRow + _random.Next(1, labels.Length)) % labels.Length;
                    questionText = L($"{char.ToUpperInvariant(context.QuantityName[0])}{context.QuantityName[1..]} ở hai mục “{labels[firstRow]}” và “{labels[secondRow]}” chênh lệch bao nhiêu {unit}?",
                        $"What is the difference in {context.QuantityName} between “{labels[firstRow]}” and “{labels[secondRow]}” (in {unit})?");
                    chartAnswerLabel = L($"Chênh lệch {context.QuantityName}", $"Difference in {context.QuantityName}");
                    expression = $"{N(Math.Max(values[firstRow], values[secondRow]))}-{N(Math.Min(values[firstRow], values[secondRow]))}";
                }
                else
                {
                    int selectedRow = _random.Next(labels.Length);
                    questionText = pie
                        ? L($"Mục “{labels[selectedRow]}” chiếm bao nhiêu phần trăm?", $"What percentage belongs to “{labels[selectedRow]}”?")
                        : L($"Theo {visualName}, {context.QuantityName} ở mục “{labels[selectedRow]}” là bao nhiêu {unit}?",
                            $"According to the {visualName}, what is the {context.QuantityName} for “{labels[selectedRow]}” (in {unit})?");
                    chartAnswerLabel = pie ? L($"Tỉ lệ “{labels[selectedRow]}”", $"Percentage for “{labels[selectedRow]}”")
                        : L($"{char.ToUpperInvariant(context.QuantityName[0])}{context.QuantityName[1..]} ở mục “{labels[selectedRow]}”",
                            $"{char.ToUpperInvariant(context.QuantityName[0])}{context.QuantityName[1..]} for “{labels[selectedRow]}”");
                    expression = N(values[selectedRow]);
                }
                problem = introduction + " " + questionText;
                Answer(chartAnswerLabel, expression, unit);
                requiresSolution = type is ElementaryQuizType.ChartTotal or ElementaryQuizType.ChartDifference;
                break;
            }
            case ElementaryQuizType.Likelihood:
            {
                int category = _random.Next(3);
                problem = L("Một túi chỉ có bi đỏ. ", "A bag contains only red marbles. ") + (category switch
                {
                    0 => L("Lấy một viên bi: việc lấy được bi đỏ là chắc chắn, có thể hay không thể?", "When drawing a marble, is getting red certain, possible or impossible?"),
                    1 => L("Lấy một viên bi: việc lấy được bi xanh là chắc chắn, có thể hay không thể?", "When drawing a marble, is getting blue certain, possible or impossible?"),
                    _ => L("Thêm một viên bi xanh rồi lấy ngẫu nhiên một viên: lấy được bi xanh là chắc chắn, có thể hay không thể?", "Add one blue marble and draw randomly: is getting blue certain, possible or impossible?")
                });
                TextAnswer(L("Khả năng xảy ra", "Likelihood"), category == 0 ? L("Chắc Chắn", "certain") : category == 1 ? L("Không Thể", "impossible") : L("Có Thể", "possible")); break;
            }
            case ElementaryQuizType.ExperimentalProbability:
                Fact(a, a + b);
                problem = L($"Tung đồng xu {a + b} lần, mặt ngửa xuất hiện {a} lần. Viết phân số chỉ số lần mặt ngửa so với tổng số lần tung.",
                    $"A coin is tossed {a + b} times and lands heads {a} times. Express the heads count as a fraction of the total trials.");
                Answer(L("Phân số số lần mặt ngửa", "Heads fraction"), $"{a}/{a + b}"); requiresSolution = false; break;
            case ElementaryQuizType.ClassifyAngle:
            {
                int index = _random.Next(4);
                int degrees = index switch { 0 => _random.Next(3, 18) * 5, 1 => 90, 2 => _random.Next(19, 34) * 5, _ => 180 };
                Fact(degrees);
                string[][] names = [["A", "O", "B"], ["M", "O", "N"], ["X", "P", "Y"], ["C", "E", "D"]];
                string[] labels = names[_random.Next(names.Length)];
                string name = string.Concat(labels);
                visual = new("angle", labels, [degrees], "°", RotationDegrees: _random.Next(12) * 30);
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
                bool choosePair = scale >= 3 && _random.Next(2) == 0;
                decimal rotation = _random.Next(12) * 15;
                string[][] nameSets = [["a", "b", "c"], ["d", "e", "f"], ["m", "n", "p"]];
                string[] names = nameSets[_random.Next(nameSets.Length)];
                if (choosePair)
                {
                    bool hasPair = _random.Next(4) != 0;
                    decimal[] directions = hasPair
                        ? parallel ? [rotation, rotation, rotation + 45] : [rotation, rotation + 90, rotation + 35]
                        : [rotation, rotation + 30, rotation + 65];
                    _random.Shuffle(directions);
                    var lines = names.Select((label, lineIndex) => new QuizVisualLine(label, directions[lineIndex],
                        parallel && hasPair ? (lineIndex - 1) * .35f : 0)).ToArray();
                    visual = new("line-pairs", names, [], "", Lines: lines);
                    string Pair(int left, int right) => L($"Đường {names[left]} Và Đường {names[right]}", $"Lines {names[left]} and {names[right]}");
                    string none = parallel ? L("Không Có Cặp Song Song", "No parallel pair") : L("Không Có Cặp Vuông Góc", "No perpendicular pair");
                    textChoices = [Pair(0, 1), Pair(0, 2), Pair(1, 2), none];
                    int pairIndex = 3;
                    (int Left, int Right)[] pairs = [(0, 1), (0, 2), (1, 2)];
                    for (int candidate = 0; candidate < pairs.Length; candidate++)
                    {
                        var pair = pairs[candidate];
                        decimal difference = Math.Abs(directions[pair.Left] - directions[pair.Right]) % 180;
                        if (difference == (parallel ? 0 : 90)) pairIndex = candidate;
                    }
                    string relationName = parallel ? L("song song", "parallel") : L("vuông góc", "perpendicular");
                    string[] prompts = vi
                        ? [$"Trong ba đường thẳng {string.Join(", ", names)}, cặp đường nào {relationName}? Nếu không có cặp phù hợp, ghi không có.",
                           $"Quan sát hình và tìm cặp đường thẳng {relationName}. Nếu không có, hãy cho biết không có cặp phù hợp.",
                           $"Cặp nào trong hình có quan hệ {relationName}? Chọn cặp đúng hoặc cho biết không có."]
                        : [$"Which pair among lines {string.Join(", ", names)} is {relationName}? If there is none, say there is no such pair.",
                           $"Find the {relationName} pair of lines in the figure, or state that there is no such pair.",
                           $"Which pair in the figure is {relationName}? Choose the pair or state that none exists."];
                    problem = prompts[_random.Next(prompts.Length)];
                    if (pairIndex == 3) TextAnswer(L("Cặp đường thẳng", "Pair of lines"), none, L("không có", "none"));
                    else
                    {
                        var pair = pairs[pairIndex];
                        TextAnswer(L("Cặp đường thẳng", "Pair of lines"), textChoices[pairIndex],
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
                visual = new("rectangle", [], [], ""); Constant(4);
                problem = L("Hình chữ nhật trong hình có bao nhiêu cạnh?", "How many sides does the shown rectangle have?");
                Answer(L("Số cạnh", "Sides"), "4"); requiresSolution = false; break;
            case ElementaryQuizType.RectangleSide:
                if (a < b) (a, b) = (b, a);
                Fact(a * b, a); visual = new("rectangle", [L("Chiều dài", "Length"), "?"], [a], "cm");
                problem = L($"Hình chữ nhật có diện tích {a * b} cm², chiều dài {a} cm. Tìm chiều rộng.",
                    $"A rectangle has area {a * b} cm² and length {a} cm. Find its width.");
                Answer(L("Chiều rộng", "Width"), $"{a * b}/{a}", "cm"); break;
            case ElementaryQuizType.CompositeArea:
                Fact(a, b); visual = new("composite", [], [a, b], "cm");
                problem = L($"Hình gồm hai hình vuông không chồng lấn, cạnh lần lượt {a} cm và {b} cm. Tính tổng diện tích.",
                    $"Two non-overlapping squares have side lengths {a} cm and {b} cm. Find their combined area.");
                Answer(L("Tổng diện tích", "Combined area"), $"{a}*{a}+{b}*{b}", "cm²"); break;
        }
        var contract = new ElementaryQuizContract(kind, type, language, problem, "", facts, constants, answers, requiresSolution, visual);
        if (contract.IsComparison)
            work.Add(contract.FormatComparison(answers[0].Text!));
        // Display task-required forms, not only their normalized rational values.
        answers = answers.Select(answer => answer.RequireMixedNumber ? answer with { Text = Mixed(answer.Value) }
            : answer.RequiredDenominator is int denominator ? answer with { Text = $"{answer.Value.Numerator * denominator / answer.Value.Denominator}/{denominator}" } : answer).ToList();
        contract = contract with { Answers = answers };
        string answerLabel = L("Đáp số", "Answer");
        contract = contract with { SolutionText = string.Join(Environment.NewLine, work) + Environment.NewLine + answerLabel + ": " + contract.AnswerText };
        var choices = new List<string> { contract.AnswerText };
        if (textChoices is not null)
            choices.AddRange(textChoices.Where(text => !string.Equals(text, contract.AnswerText, StringComparison.OrdinalIgnoreCase)));
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
