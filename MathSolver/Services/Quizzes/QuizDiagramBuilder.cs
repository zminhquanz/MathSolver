using MathSolver.Models;
using System.Collections.ObjectModel;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>C# math puzzle data and rules.</summary>
public static class QuizDiagramBuilder
{
    public static QuizDiagram? Build(ArithmeticQuizQuestion question, AppLanguage language, bool revealSolution = false)
    {
        bool vi = language == AppLanguage.Vietnamese;
        string L(string vietnamese, string english) => vi ? vietnamese : english;
        string? solution = null;
        QuizDiagram? diagram = null;
        if (question.GeometryProblem is GeometryQuizContract geometry)
        {
            string[] keys = (geometry.ShapeId, geometry.Measurement) switch
            {
                ("square" or "cube", _) => ["a"],
                ("rectangle", _) => ["a", "b"],
                ("triangle", GeometryMeasurement.Perimeter) => ["a", "b", "c"],
                ("triangle", _) => ["a", "h"],
                ("trapezoid", GeometryMeasurement.Perimeter) => ["a", "b", "c", "d"],
                ("trapezoid", _) => ["a", "b", "h"],
                ("rhombus", GeometryMeasurement.Perimeter) => ["a"],
                ("rhombus", _) => ["d1", "d2"],
                ("parallelogram", GeometryMeasurement.Perimeter) => ["a", "b"],
                ("parallelogram", _) => ["a", "h"],
                ("circle", _) => ["r"],
                ("rectangular_prism", _) => ["a", "b", "h"],
                _ => []
            };
            var labels = keys.Where(geometry.Dimensions.ContainsKey).ToDictionary(key => key,
                key => !revealSolution && geometry.Reasoning?.HiddenDimensions.Contains(key) == true ? "? " + geometry.LengthUnitSymbol
                    : geometry.Dimensions[key].ToString(CultureInfo.InvariantCulture) + " " + geometry.LengthUnitSymbol);
            string target = geometry.Measurement switch
            {
                GeometryMeasurement.Perimeter => L("Chu vi", "Perimeter"),
                GeometryMeasurement.Volume => L("Thể tích", "Volume"),
                GeometryMeasurement.LateralArea => L("Diện tích xung quanh", "Lateral surface area"),
                GeometryMeasurement.TotalArea => L("Diện tích toàn phần", "Total surface area"),
                _ => L("Diện tích", "Area")
            };
            diagram = new("geometry", target + " = " + (revealSolution ? geometry.CorrectAnswer.ToString(CultureInfo.InvariantCulture) : "?") + " " + geometry.AnswerUnit, [], geometry.ShapeId,
                new ReadOnlyDictionary<string, string>(labels));
            if (revealSolution) solution = geometry.Reasoning is not null ? GeometryReasoningText.FormatSolution(geometry)
                : geometry.Formula + Environment.NewLine + geometry.EquationText + " " + geometry.AnswerUnit;
        }
        else if (question.ElementaryProblem is ElementaryQuizContract elementary)
        {
            IReadOnlyList<string> facts = elementary.Facts;
            if (elementary.Reasoning?.SupportingDiagram is { } supporting)
            {
                diagram = revealSolution ? supporting with
                {
                    Rows = supporting.Rows.Select((row, index) => row with
                    {
                        Label = row.Label + " = " + ElementaryQuizContract.FormatAnswer(elementary.Answers[index])
                    }).ToArray()
                } : supporting;
            }
            else if (elementary.Reasoning is not null && elementary.Kind == QuizProblemKind.FractionSkills)
            {
                // Quantities can be inferred from several givens. No solved quantity is supplied as a diagram label.
                var givens = elementary.Reasoning.Givens;
                bool quantity = elementary.Type is ElementaryQuizType.FractionOfNumber or ElementaryQuizType.WholeFromFraction;
                string unit = elementary.Answers[0].Unit;
                var rows = new List<QuizDiagramRow>();
                foreach (var numerator in givens.Where(given => given.Role.StartsWith("numerator", StringComparison.Ordinal)))
                {
                    string denominatorRole = numerator.Role.Replace("numerator", "denominator", StringComparison.Ordinal);
                    var denominator = givens.FirstOrDefault(given => given.Role == denominatorRole);
                    if (denominator is null) continue;
                    int n = int.Parse(numerator.Value, CultureInfo.InvariantCulture);
                    int d = int.Parse(denominator.Value, CultureInfo.InvariantCulture);
                    rows.Add(new(quantity ? L("Phần đang xét", "Considered portion") + " = ? " + unit : $"{n}/{d}",
                        [], FractionNumerator: n, FractionDenominator: d));
                }
                if (rows.Count == 0) rows.Add(new(L("Phân số cần suy ra", "Fraction to infer") + " = ?", []));
                diagram = new("fractions", quantity ? (elementary.Reasoning.Tier == CurriculumTier.FiveStars
                    ? L("Lượng còn lại sau phần đầu", "Quantity after the first portion") : L("Toàn bộ", "Whole")) + " = ? " + unit
                    : L("Các phân số đã cho; suy ra phần còn thiếu", "Given fractions; infer missing components"), rows);
            }
            else if (elementary.Kind == QuizProblemKind.TwoNumbers)
            {
                bool ratio = elementary.Type != ElementaryQuizType.SumDifference;
                int small = ratio ? int.Parse(facts[1], CultureInfo.InvariantCulture) : 2;
                int large = ratio ? int.Parse(facts[2], CultureInfo.InvariantCulture) : 3;
                // Sum/difference bars are schematic: no lengths are calculated from the answers.
                QuizDiagramSegment[] Parts(int count) => Enumerable.Range(0, count)
                    .Select(index => new QuizDiagramSegment("?", Highlight: !ratio && index == count - 1 && count == large)).ToArray();
                var rows = new[] { new QuizDiagramRow(L("Số bé", "Smaller number"), Parts(small)),
                    new QuizDiagramRow(L("Số lớn", "Larger number"), Parts(large)) };
                string caption = elementary.Type switch
                {
                    ElementaryQuizType.SumDifference => L("Tổng", "Sum") + " = " + facts[0] + "; " + L("Hiệu", "Difference") + " = " + facts[1],
                    ElementaryQuizType.SumRatio => L("Tổng", "Sum") + " = " + facts[0] + "; " + L("Tỉ số", "Ratio") + " = " + facts[1] + "/" + facts[2],
                    _ => L("Hiệu", "Difference") + " = " + facts[0] + "; " + L("Tỉ số", "Ratio") + " = " + facts[1] + "/" + facts[2]
                };
                if (!ratio)
                {
                    rows[0] = rows[0] with { Segments = [new("?", 2)] };
                    rows[1] = rows[1] with { Segments = [new("?", 2), new(facts[1], Highlight: true)] };
                }
                if (revealSolution)
                {
                    var smallAnswer = elementary.Answers[0].Value;
                    string partValue = new ReducedFraction(smallAnswer.Numerator,
                        smallAnswer.Denominator * small).ToString();
                    for (int index = 0; index < rows.Length; index++)
                        rows[index] = rows[index] with
                        {
                            Label = rows[index].Label + " = " + ElementaryQuizContract.FormatAnswer(elementary.Answers[index]),
                            Segments = rows[index].Segments.Select(segment => segment with
                            { Text = segment.Text == "?" ? ratio ? partValue : smallAnswer.ToString() : segment.Text }).ToArray()
                        };
                }
                diagram = new("bars", caption, Array.AsReadOnly(rows));
            }
            else if (elementary.Type is ElementaryQuizType.FractionOfNumber or ElementaryQuizType.WholeFromFraction)
            {
                int numerator = int.Parse(facts[1], CultureInfo.InvariantCulture);
                int denominator = int.Parse(facts[2], CultureInfo.InvariantCulture);
                bool wholeGiven = elementary.Type == ElementaryQuizType.FractionOfNumber;
                string unknown = revealSolution ? elementary.Answers[0].Value.ToString() : "?";
                string caption = L("Toàn bộ", "Whole") + " = " + (wholeGiven ? facts[0] : unknown) + " " + elementary.Answers[0].Unit;
                string part = L("Phần đã chọn", "Selected part") + " = " + (wholeGiven ? unknown : facts[0]) + " " + elementary.Answers[0].Unit;
                diagram = new("fractions", caption,
                    [new(part, [], FractionNumerator: numerator, FractionDenominator: denominator)]);
            }
            else if (elementary.Kind == QuizProblemKind.FractionSkills && elementary.Visual is null)
            {
                var rows = new List<QuizDiagramRow>();
                int pairs = elementary.Type == ElementaryQuizType.CommonDenominator ? 2 : 1;
                for (int index = 0; index < pairs; index++)
                {
                    // Common-denominator facts are stored as n1, n2, d1, d2.
                    int n = int.Parse(facts[elementary.Type == ElementaryQuizType.CommonDenominator ? index : index * 2], CultureInfo.InvariantCulture);
                    int d = int.Parse(facts[elementary.Type == ElementaryQuizType.CommonDenominator ? index + 2 : index * 2 + 1], CultureInfo.InvariantCulture);
                    rows.Add(new($"{n}/{d}", [], FractionNumerator: n, FractionDenominator: d));
                }
                diagram = new("fractions", L("Mỗi thanh biểu diễn một đơn vị", "Each bar represents one whole"), rows.AsReadOnly());
            }
            if (revealSolution && diagram is not null) solution = elementary.SolutionText;
        }
        else if (question.MotionProblem is MotionQuizContract motion)
        {
            var facts = motion.Facts;
            string speedUnit = motion.RequiredProblemUnits.FirstOrDefault(unit => unit.Contains('/') || unit == "mph") ?? motion.AnswerUnit;
            string distanceUnit = motion.RequiredProblemUnits.FirstOrDefault(unit => unit is "km" or "m" or "cm" or "mm" or "miles" or "mi" or "dặm") ?? "";
            string timeUnit = motion.RequiredProblemUnits.FirstOrDefault(unit => unit != speedUnit && unit != distanceUnit) ?? "";
            string V(int index, string unit) => facts[index].ToString(CultureInfo.InvariantCulture) + " " + unit;
            var rows = new List<QuizDiagramRow>();
            string caption = motion.SubjectName + " = " + (revealSolution ? motion.CorrectAnswer.ToString(CultureInfo.InvariantCulture) : "?") + " " + motion.AnswerUnit;
            void Arrow(string label, int index, int direction) => rows.Add(new(label + ": " + V(index, speedUnit), [], direction));
            switch (motion.QuestionKind)
            {
                case MotionQuestionKind.CatchUpTime:
                    caption = L("Khoảng cách ban đầu", "Initial gap") + ": " + V(0, distanceUnit) + "; " + caption;
                    Arrow(L("Phía sau", "Behind"), 2, 1); Arrow(L("Phía trước", "Ahead"), 1, 1); break;
                case MotionQuestionKind.MeetingTime:
                    caption = L("Khoảng cách ban đầu", "Initial distance") + ": " + V(0, distanceUnit) + "; " + caption;
                    Arrow("A", 1, 1); Arrow("B", 2, -1); break;
                case MotionQuestionKind.RiverDownstreamSpeed:
                case MotionQuestionKind.RiverUpstreamSpeed:
                    Arrow(L("Thuyền khi nước yên", "Boat in still water"), 0,
                        motion.QuestionKind == MotionQuestionKind.RiverDownstreamSpeed ? 1 : -1);
                    Arrow(L("Dòng nước", "Current"), 1, 1); break;
                case MotionQuestionKind.RiverBoatSpeed:
                case MotionQuestionKind.RiverCurrentSpeed:
                    Arrow(L("Xuôi dòng", "Downstream"), 0, 1); Arrow(L("Ngược dòng", "Upstream"), 1, -1); break;
                case MotionQuestionKind.BasicRestDistance:
                    Arrow(L("Vận tốc", "Speed"), 0, 1);
                    rows.Add(new(L("Tổng thời gian / thời gian nghỉ", "Elapsed time / rest"), [new(V(1, timeUnit)), new(V(2, timeUnit), Highlight: true)])); break;
                case MotionQuestionKind.BasicDistance:
                    Arrow(L("Vận tốc", "Speed"), 0, 1); rows.Add(new(L("Thời gian", "Time"), [new(V(1, timeUnit))])); break;
                case MotionQuestionKind.BasicSpeed:
                    rows.Add(new(L("Quãng đường", "Distance"), [new(V(0, distanceUnit))], 1));
                    rows.Add(new(L("Thời gian", "Time"), [new(V(1, timeUnit))])); break;
                case MotionQuestionKind.BasicTime:
                    Arrow(L("Vận tốc", "Speed"), 0, 1); rows.Add(new(L("Quãng đường", "Distance"), [new(V(1, distanceUnit))])); break;
            }
            diagram = new("motion", caption, rows.AsReadOnly());
            if (revealSolution) solution = motion.SolutionText;
        }
        else if (question.FractionProblem is FractionQuizContract fraction)
        {
            QuizDiagramRow Row(ReducedFraction value) => new(value.ToString(), [],
                FractionNumerator: value.Numerator >= 0 && value.Numerator <= 128 ? (int)value.Numerator : null,
                FractionDenominator: value.Denominator <= 32 ? (int)value.Denominator : null);
            diagram = new("fractions", fraction.ExpressionText + " = ?", [Row(fraction.LeftOperand), Row(fraction.RightOperand)]);
            if (revealSolution) solution = fraction.ExpressionText + " = " + fraction.CorrectAnswer;
        }
        else if (question.AverageProblem is { IndirectData: AverageIndirectData data } average)
        {
            string unit = average.AnswerUnit;
            diagram = new("bars", L("Trung bình mỗi bạn", "Average per person") + " = " + (revealSolution ? average.CorrectAnswer.ToString(CultureInfo.InvariantCulture) : "?") + " " + unit,
            [
                new("Lan", [new(data.FirstQuantity + " " + unit, 3)]),
                new("Mai = ?", [new("?", 3), new("+" + data.Increase + " " + unit, Highlight: true)]),
                new("Hoa = ?", [new(L("Ít hơn Mai", "Less than Mai") + ": " + data.Decrease + " " + unit, 3)])
            ]);
            if (revealSolution)
            {
                int mai = data.FirstQuantity + data.Increase, hoa = mai - data.Decrease;
                diagram = diagram with { Rows = [
                    new("Lan", [new(data.FirstQuantity + " " + unit, data.FirstQuantity)]),
                    new("Mai", [new(mai + " " + unit, mai)]),
                    new("Hoa", [new(hoa + " " + unit, hoa)])
                ] };
                solution = average.SolutionText;
            }
        }
        // A single gate owns solution exposure for every supported family.
        return diagram is null ? null : diagram with { Explanation = revealSolution ? solution : null };
    }
}
