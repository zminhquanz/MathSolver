using System.Globalization;
using MathSolver.Models;

namespace MathSolver.Services;

public sealed record QuizNumberName(int Value, string Words);

public sealed partial class ElementaryQuizGenerator
{
    public static bool IsFoundationSkill(ElementaryQuizType type) => type >= ElementaryQuizType.Counting;

    private ElementaryQuizContract CreateFoundation(QuizProblemKind kind, ElementaryQuizType type,
        AppLanguage language, CurriculumTier tier)
    {
        var t = new DifficultyBuilder(kind, type, language, tier) { RequiresSolution = false };
        int level = (int)tier;
        string N(int value) => value.ToString(CultureInfo.InvariantCulture);
        string L(string id, params (string Name, string Value)[] slots) => QuizContentCatalog.Text(language, "Foundation." + id, slots);
        string label = L("Answer"), problem;
        QuizVisualData? visual = null;
        string[]? choices = null;
        void Direct(int value, string unit = "") => t.Answer(label, t.Given("reading", value, unit), unit);
        void TextChoice(string text, IEnumerable<string> alternatives)
        {
            t.TextAnswer(label, text);
            choices = alternatives.Prepend(text).Distinct().Take(4).ToArray();
        }
        int maximum = level switch { 1 => 100, 2 => 1000, 3 => 100000, _ => 1000000 };
        int number = _random.Next(1, maximum);
        switch (type)
        {
            case ElementaryQuizType.Counting:
            {
                int count = _random.Next(1, level <= 2 ? 11 : 21);
                Direct(count);
                problem = L("Counting");
                visual = new("foundation-count", [], [count], "", AccessibleDescription: L("CountDescription", ("count", N(count))));
                break;
            }
            case ElementaryQuizType.ReadNumber:
            case ElementaryQuizType.WriteNumber:
            {
                var names = QuizContentCatalog.LoadList<QuizNumberName>("Foundation.NumberNames", QuizContentCatalog.Culture(language))
                    .Where(item => item.Value <= maximum).ToArray();
                var selected = names[_random.Next(names.Length)];
                if (type == ElementaryQuizType.ReadNumber)
                {
                    TextChoice(selected.Words, names.Where(item => item.Value != selected.Value).Select(item => item.Words).OrderBy(_ => _random.Next()));
                    problem = L("ReadNumber", ("number", N(selected.Value)));
                }
                else { Direct(selected.Value); problem = L("WriteNumber", ("words", selected.Words)); }
                break;
            }
            case ElementaryQuizType.PlaceValue:
            {
                int power = _random.Next(0, N(number).Length), place = (int)Math.Pow(10, power);
                int digit = number / place % 10;
                Direct(digit * place);
                problem = L("PlaceValue", ("number", N(number)), ("place", L("Place" + power)), ("digit", N(digit)));
                break;
            }
            case ElementaryQuizType.AdjacentNumbers:
                t.Answer(L("Before"), t.Given("before", number - 1));
                t.Answer(L("After"), t.Given("after", number + 1));
                problem = L("AdjacentNumbers", ("number", N(number)));
                break;
            case ElementaryQuizType.NumberLine:
            {
                int step = level <= 2 ? 1 : level == 3 ? 10 : 100;
                int start = _random.Next(0, 8) * step, hidden = _random.Next(1, 5);
                Direct(start + hidden * step);
                problem = L("NumberLine");
                visual = new("foundation-number-line", [], Enumerable.Range(0, 6).Select(i => (decimal)(start + i * step)).ToArray(), "",
                    HiddenValueIndices: new HashSet<int> { hidden }, AccessibleDescription: L("LineDescription", ("numbers", string.Join(", ", Enumerable.Range(0, 6).Select(i => i == hidden ? "?" : N(start + i * step))))));
                break;
            }
            case ElementaryQuizType.Parity:
                TextChoice(L(number % 2 == 0 ? "Even" : "Odd"), [L("Even"), L("Odd"), L("BothParity"), L("NeitherParity")]);
                problem = L("Parity", ("number", N(number)));
                break;
            case ElementaryQuizType.RomanNumerals:
            {
                string[] roman = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX"];
                int value = _random.Next(1, 21);
                Direct(value); problem = L("RomanNumerals", ("roman", roman[value - 1]));
                break;
            }
            case ElementaryQuizType.OrderNumbers:
            case ElementaryQuizType.OrderFractions:
            {
                int denominator = type == ElementaryQuizType.OrderFractions ? _random.Next(5, 13) : 1;
                var values = Enumerable.Range(1, denominator == 1 ? 20 : denominator - 1).OrderBy(_ => _random.Next()).Take(level <= 2 ? 3 : 4).ToArray();
                if (denominator == 1) values = values.Select(i => i * (level < 3 ? 1 : 100)).ToArray();
                string Item(int value) => denominator == 1 ? N(value) : $"{value}/{denominator}";
                bool descending = _random.Next(2) == 0;
                int[] sorted = descending ? values.OrderDescending().ToArray() : values.Order().ToArray();
                string Format(IEnumerable<int> items) => string.Join(" → ", items.Select(Item));
                var wrong = new List<string>();
                for (int i = 0; i < 3; i++) { var copy = sorted.ToArray(); int right = 1 + i % (copy.Length - 1); (copy[0], copy[right]) = (copy[right], copy[0]); wrong.Add(Format(copy)); }
                // Three-element lists only have two first-element swaps; reversing supplies the third distractor.
                wrong.Add(Format(sorted.Reverse()));
                TextChoice(Format(sorted), wrong);
                t.Answers[0] = t.Answers[0] with { Aliases = [string.Join(", ", sorted.Select(Item)),
                    string.Join(descending ? " > " : " < ", sorted.Select(Item))] };
                problem = L("Order", ("direction", L(descending ? "Descending" : "Ascending")), ("numbers", string.Join(", ", values.Select(Item))));
                break;
            }
            case ElementaryQuizType.RoundWholeNumber:
            case ElementaryQuizType.EstimateSum:
            {
                int place = (int)Math.Pow(10, Math.Min(level, 5));
                int first = _random.Next(place, place * 9), second = _random.Next(place, place * 9);
                int Round(int v) => (int)(Math.Round((decimal)v / place, 0, MidpointRounding.AwayFromZero) * place);
                Direct(type == ElementaryQuizType.RoundWholeNumber ? Round(first) : Round(first) + Round(second));
                problem = type == ElementaryQuizType.RoundWholeNumber
                    ? L("RoundWholeNumber", ("number", N(first)), ("place", N(place)))
                    : L("EstimateSum", ("a", N(first)), ("b", N(second)), ("place", N(place)));
                break;
            }
            case ElementaryQuizType.LetterExpression:
            {
                int a = _random.Next(1, 10), b = _random.Next(1, 10), c = _random.Next(1, 10);
                string first = t.Given("a", a), second = t.Given("b", b), third = t.Given("c", c);
                string expression = level <= 2 ? $"{first}+{second}" : $"({first}+{second})*{third}";
                t.Answer(label, expression);
                problem = L("LetterExpression", ("expression", level <= 2 ? "a + b" : "(a + b) × c"), ("a", first), ("b", second), ("c", third));
                break;
            }
            case ElementaryQuizType.FractionPicture:
            case ElementaryQuizType.FractionTerms:
            case ElementaryQuizType.EquivalentFraction:
            {
                int denominator = _random.Next(3, 10), numerator = _random.Next(1, denominator);
                if (type == ElementaryQuizType.FractionTerms)
                {
                    t.Answer(L("Numerator"), t.Given("numerator", numerator));
                    t.Answer(L("Denominator"), t.Given("denominator", denominator));
                    problem = L("FractionTerms", ("fraction", $"{numerator}/{denominator}"));
                }
                else if (type == ElementaryQuizType.EquivalentFraction)
                {
                    int factor = _random.Next(2, 6); Direct(numerator * factor);
                    problem = L("EquivalentFraction", ("fraction", $"{numerator}/{denominator}"), ("denominator", N(denominator * factor)));
                }
                else
                {
                    t.Answer(label, $"{t.Given("numerator", numerator)}/{t.Given("denominator", denominator)}");
                    problem = L("FractionPicture");
                    visual = new("foundation-fraction", [], [numerator, denominator], "", AccessibleDescription: L("FractionDescription", ("numerator", N(numerator)), ("denominator", N(denominator))));
                }
                break;
            }
            case ElementaryQuizType.SpatialPosition:
            {
                int position = _random.Next(4);
                string[] names = [L("Left"), L("Right"), L("Above"), L("Below")];
                TextChoice(names[position], names);
                problem = L("SpatialPosition");
                visual = new("foundation-position", ["A", "B"], [position], "", AccessibleDescription: L("PositionDescription", ("position", names[position])));
                break;
            }
            case ElementaryQuizType.IdentifyLine:
            {
                string[] names = [L("Segment"), L("Line"), L("Curve"), L("Polyline")];
                int index = _random.Next(4); TextChoice(names[index], names); problem = L("IdentifyLine");
                visual = new("foundation-line", [], [index], "", AccessibleDescription: L("LineShapeDescription" + index));
                break;
            }
            case ElementaryQuizType.Midpoint:
            {
                bool centered = _random.Next(2) == 0;
                int left = _random.Next(2, 11), right = centered ? left : left + _random.Next(1, 5);
                TextChoice(L(centered ? "MidpointYes" : "MidpointNo"), [L("MidpointYes"), L("MidpointNo"), L("Endpoint"), L("Outside")]);
                problem = L("Midpoint");
                visual = new("foundation-midpoint", ["A", "M", "B"], [left, right], "cm", AccessibleDescription: L("MidpointDescription", ("left", N(left)), ("right", N(right))));
                break;
            }
            case ElementaryQuizType.CircleParts:
            {
                string[] names = [L("Center"), L("Radius"), L("Diameter"), L("Circle")];
                int index = _random.Next(3); TextChoice(names[index], names);
                problem = L("CircleParts", ("target", index == 0 ? "O" : index == 1 ? "OC" : "AB"));
                visual = new("foundation-circle", ["O", "A", "B", "C"], [], "", AccessibleDescription: L("CircleDescription"));
                break;
            }
            case ElementaryQuizType.ShapeNet:
            {
                int index = _random.Next(3);
                string[] names = [L("Cube"), L("Cuboid"), L("Cylinder"), L("Sphere")];
                TextChoice(names[index], names); problem = L("ShapeNet");
                visual = new("foundation-net", [], [index], "", AccessibleDescription: L("NetDescription" + index));
                break;
            }
            case ElementaryQuizType.TriangleKind:
            {
                int index = _random.Next(4);
                string[] names = [L("AcuteTriangle"), L("RightTriangle"), L("ObtuseTriangle"), L("EquilateralTriangle")];
                TextChoice(names[index], names); problem = L("TriangleKind");
                visual = new("foundation-triangle", [], [index], "", AccessibleDescription: L("TriangleDescription" + index));
                break;
            }
            case ElementaryQuizType.ReadRuler:
            {
                int start = _random.Next(0, 4), end = start + _random.Next(2, 7); Direct(end - start, "cm");
                problem = L("ReadRuler"); visual = new("foundation-ruler", [], [start, end], "cm",
                    AccessibleDescription: L("RulerDescription", ("start", N(start)), ("end", N(end)))); break;
            }
            case ElementaryQuizType.ReadProtractor:
            {
                int degrees = _random.Next(2, 17) * 10; Direct(degrees, "°");
                problem = L("ReadProtractor"); visual = new("foundation-protractor", [], [degrees], "°",
                    AccessibleDescription: L("ProtractorDescription", ("degrees", N(degrees)))); break;
            }
            case ElementaryQuizType.ReadThermometer:
            {
                int degrees = _random.Next(0, 21) * 2; Direct(degrees, "°C");
                problem = L("ReadThermometer"); visual = new("foundation-thermometer", [], [degrees], "°C",
                    AccessibleDescription: L("ThermometerDescription", ("degrees", N(degrees)))); break;
            }
            case ElementaryQuizType.SortData:
            case ElementaryQuizType.CompleteBarChart:
            {
                string[] categories = [L("Red"), L("Blue"), L("Yellow")];
                int[] values = [_random.Next(1, 6), _random.Next(1, 6), _random.Next(1, 6)];
                var items = Enumerable.Range(0, 3).SelectMany(i => Enumerable.Repeat(categories[i], values[i])).OrderBy(_ => _random.Next()).ToArray();
                if (type == ElementaryQuizType.SortData)
                {
                    for (int i = 0; i < 3; i++) t.Answer(categories[i], t.Given("category-" + i, values[i]));
                    problem = L("SortData", ("items", string.Join(", ", items)));
                }
                else
                {
                    int hidden = _random.Next(3); Direct(values[hidden]);
                    problem = L("CompleteBarChart", ("items", string.Join(", ", items)), ("category", categories[hidden]));
                    visual = new("bar", categories, values.Select(v => (decimal)v).ToArray(), "",
                        HiddenValueIndices: new HashSet<int> { hidden });
                }
                break;
            }
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
        return t.Build("foundation-" + type, problem, visual) with
        {
            ChoiceTexts = choices,
            RequiresCalculation = type == ElementaryQuizType.LetterExpression
        };
    }
}
