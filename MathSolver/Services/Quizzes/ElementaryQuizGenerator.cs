using MathSolver.Models;
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

    public ArithmeticQuizQuestion Generate(ArithmeticQuizMode mode, QuizProblemKind kind,
        ElementaryQuizType? selected, AppLanguage language, CurriculumTier tier)
    {
        var types = Types(kind);
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
        void Fact(params decimal[] values) => facts.AddRange(values.Select(N));
        void Constant(params decimal[] values) => constants.AddRange(values.Select(N));
        void Answer(string label, string expression, string unit = "", bool reduced = false,
            int? denominator = null, bool mixed = false)
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
            if (calculation) work.Add(expression + " = " + ElementaryQuizContract.FormatAnswer(answer));
        }
        void TextAnswer(string label, string text, params string[] aliases)
        {
            answers.Add(new(label, new(0, 1), "", "", text, aliases));
            work.Add(label + ":");
            requiresSolution = false;
        }
        switch (type)
        {
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
                if (op == "/") x = a * y;
                if (op == "-" && x < y) (x, y) = (y, x);
                Fact(x, y); problem = L($"Tính {N(x)} {op} {N(y)}.", $"Calculate {N(x)} {op} {N(y)}.");
                Answer(L("Kết quả", "Result"), $"{N(x)}{op}{N(y)}"); requiresSolution = false; break;
            }
            case ElementaryQuizType.ReduceFraction:
            case ElementaryQuizType.CompareFractions:
            case ElementaryQuizType.MixedNumber:
            case ElementaryQuizType.CommonDenominator:
            case ElementaryQuizType.FractionOfNumber:
            case ElementaryQuizType.WholeFromFraction:
            {
                int denominator = _random.Next(3, 9), numerator = _random.Next(1, denominator);
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
                    int otherDenominator = scale < 3 ? denominator : _random.Next(3, 9);
                    int otherNumerator = _random.Next(1, otherDenominator);
                    Fact(numerator, denominator, otherNumerator, otherDenominator);
                    problem = L($"So sánh {numerator}/{denominator} và {otherNumerator}/{otherDenominator}.",
                        $"Compare {numerator}/{denominator} and {otherNumerator}/{otherDenominator}.");
                    int comparison = (numerator * otherDenominator).CompareTo(otherNumerator * denominator);
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
                    string unit = L("quyển sách", "books");
                    if (type == ElementaryQuizType.FractionOfNumber)
                    {
                        Fact(whole, numerator, denominator);
                        problem = L($"Có {whole} quyển sách, {numerator}/{denominator} là sách truyện. Có bao nhiêu quyển sách truyện?",
                            $"There are {whole} books; {numerator}/{denominator} are storybooks. How many storybooks?");
                        Answer(L("Số sách truyện", "Storybooks"), $"{whole}*{numerator}/{denominator}", unit);
                    }
                    else
                    {
                        Fact(part, numerator, denominator);
                        problem = L($"Có {part} quyển sách truyện, bằng {numerator}/{denominator} toàn bộ sách. Có tất cả bao nhiêu quyển sách?",
                            $"There are {part} storybooks, representing {numerator}/{denominator} of all books. How many books in total?");
                        Answer(L("Tổng số sách", "Total books"), $"{part}/{numerator}*{denominator}", unit);
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
                decimal[] values = type == ElementaryQuizType.ReadPieChart ? [25, 35, 40] : [a * 2, b * 2, a + b];
                string[] labels = L("Cam,Táo,Xoài", "Oranges,Apples,Mangoes").Split(',');
                string unit = type == ElementaryQuizType.ReadPieChart ? "%" : L("quả", "fruit");
                visual = new(type == ElementaryQuizType.ReadTable ? "table" : type == ElementaryQuizType.ReadPieChart ? "pie" : "bar", labels, values, unit);
                Fact(values); int selectedRow = _random.Next(3);
                string expression = type == ElementaryQuizType.ChartTotal ? string.Join("+", values.Select(N))
                    : type == ElementaryQuizType.ChartDifference ? $"{N(Math.Max(values[0], values[1]))}-{N(Math.Min(values[0], values[1]))}" : N(values[selectedRow]);
                problem = type == ElementaryQuizType.ChartTotal ? L("Có tất cả bao nhiêu quả trong biểu đồ?", "How many fruit in total in the chart?")
                    : type == ElementaryQuizType.ChartDifference ? L($"Số {labels[0]} và {labels[1]} chênh lệch bao nhiêu quả?", $"What is the difference between {labels[0]} and {labels[1]}?")
                    : L($"Theo bảng/biểu đồ, {labels[selectedRow]} có giá trị bao nhiêu {unit}?", $"According to the data, what is the value for {labels[selectedRow]} ({unit})?");
                Answer(L("Giá trị cần tìm", "Requested value"), expression, unit); requiresSolution = type is ElementaryQuizType.ChartTotal or ElementaryQuizType.ChartDifference; break;
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
                TextAnswer(L("Khả năng xảy ra", "Likelihood"), category == 0 ? L("chắc chắn", "certain") : category == 1 ? L("không thể", "impossible") : L("có thể", "possible")); break;
            }
            case ElementaryQuizType.ExperimentalProbability:
                Fact(a, a + b);
                problem = L($"Tung đồng xu {a + b} lần, mặt ngửa xuất hiện {a} lần. Viết phân số chỉ số lần mặt ngửa so với tổng số lần tung.",
                    $"A coin is tossed {a + b} times and lands heads {a} times. Express the heads count as a fraction of the total trials.");
                Answer(L("Phân số số lần mặt ngửa", "Heads fraction"), $"{a}/{a + b}"); requiresSolution = false; break;
            case ElementaryQuizType.ClassifyAngle:
            {
                int[] degrees = [45, 90, 120]; int index = _random.Next(3); Fact(degrees[index]);
                visual = new("angle", [], [degrees[index]], "°");
                problem = L("Góc trong hình là góc nhọn, vuông hay tù?", "Is the shown angle acute, right or obtuse?");
                TextAnswer(L("Loại góc", "Angle type"), index == 0 ? L("góc nhọn", "acute") : index == 1 ? L("góc vuông", "right") : L("góc tù", "obtuse"), index == 0 ? "nhọn" : index == 1 ? "vuông" : "tù"); break;
            }
            case ElementaryQuizType.ParallelLines:
            case ElementaryQuizType.PerpendicularLines:
                visual = new(type == ElementaryQuizType.ParallelLines ? "parallel" : "perpendicular", [], [], "");
                problem = L("Hai đường thẳng trong hình có quan hệ gì?", "What is the relationship between the two lines?");
                TextAnswer(L("Quan hệ hai đường", "Line relationship"), type == ElementaryQuizType.ParallelLines ? L("song song", "parallel") : L("vuông góc", "perpendicular")); break;
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
        // Display task-required forms, not only their normalized rational values.
        answers = answers.Select(answer => answer.RequireMixedNumber ? answer with { Text = Mixed(answer.Value) }
            : answer.RequiredDenominator is int denominator ? answer with { Text = $"{answer.Value.Numerator * denominator / answer.Value.Denominator}/{denominator}" } : answer).ToList();
        contract = contract with { Answers = answers };
        string answerLabel = L("Đáp số", "Answer");
        contract = contract with { SolutionText = string.Join(Environment.NewLine, work) + Environment.NewLine + answerLabel + ": " + contract.AnswerText };
        var choices = new List<string> { contract.AnswerText };
        for (int delta = 1; delta <= 3; delta++)
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
        if (answer.Text is "góc nhọn" or "góc vuông" or "góc tù" or "acute" or "right" or "obtuse")
            return (vi ? new[] { "góc nhọn", "góc vuông", "góc tù", "góc bẹt" } : new[] { "acute", "right", "obtuse", "straight" })
                .Where(text => text != answer.Text).ElementAt(delta - 1);
        if (answer.Text is "song song" or "vuông góc" or "parallel" or "perpendicular")
            return (vi ? new[] { "song song", "vuông góc", "cắt nhau nhưng không vuông góc", "trùng nhau" }
                : new[] { "parallel", "perpendicular", "intersecting but not perpendicular", "coincident" })
                .Where(text => text != answer.Text).ElementAt(delta - 1);
        return (vi ? new[] { "chắc chắn", "có thể", "không thể" } : new[] { "certain", "possible", "impossible" }).Where(text => text != answer.Text).ElementAt((delta - 1) % (answer.Text is "certain" or "possible" or "impossible" or "chắc chắn" or "có thể" or "không thể" ? 2 : 3));
    }
}
