using MathSolver.Models;
using System.Numerics;

namespace MathSolver.Services;

/// <summary>Generates 3–7 operands and, optionally, 1–5 grouped subexpressions.</summary>
public sealed class ExpressionQuizGenerator(Random? random = null)
{
    private readonly Random _random = random ?? Random.Shared;

    private sealed record Node(string Text, ReducedFraction Value, int Height,
        ArithmeticOperation? Operation = null, Node? Left = null, Node? Right = null);

    public ArithmeticQuizQuestion Generate(
        ArithmeticQuizMode mode, ExpressionQuizType? type, CurriculumTier tier) =>
        GenerateCore(mode, type, tier, null);

    public ArithmeticQuizQuestion GenerateContract(
        ArithmeticQuizMode mode, ExpressionQuizType? type, AppLanguage language, CurriculumTier tier) =>
        GenerateCore(mode, type, tier, language);

    private ArithmeticQuizQuestion GenerateCore(
        ArithmeticQuizMode mode, ExpressionQuizType? requestedType, CurriculumTier tier, AppLanguage? language)
    {
        if (requestedType.HasValue && !Enum.IsDefined(requestedType.Value))
            throw new ArgumentOutOfRangeException(nameof(requestedType));
        if (!Enum.IsDefined(tier))
            throw new ArgumentOutOfRangeException(nameof(tier));
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (language.HasValue && !Enum.IsDefined(language.Value))
            throw new ArgumentOutOfRangeException(nameof(language));

        ExpressionQuizType type = requestedType ?? (ExpressionQuizType)_random.Next(4);

        int operands = (int)tier + 2;
        bool fractions = type is ExpressionQuizType.Fraction or ExpressionQuizType.FractionWithBrackets;
        bool brackets = type is ExpressionQuizType.IntegerWithBrackets or ExpressionQuizType.FractionWithBrackets;
        string text = string.Empty;
        ReducedFraction answer = default;
        Node? tree = null;
        for (int attempt = 0; attempt < 512; attempt++)
        {
            tree = brackets
                ? CreateGroup(operands, fractions, tier, root: true)
                : CreatePlain(operands, fractions, tier);
            text = tree.Text;
            if (!EssayCalculationEvaluator.TryEvaluate(text, out var value, out bool operation) ||
                !operation || value.Numerator.Sign <= 0 ||
                !fractions && !value.Denominator.IsOne ||
                value.Numerator.ToString().Length > 40 || value.Denominator.ToString().Length > 40)
                continue;

            if (brackets && tier != CurriculumTier.OneStar)
            {
                // At least one group must change the calculation. Do not use
                // brackets merely to decorate an associative addition chain
                // at higher tiers. One star also practices grouping sums.
                string ungrouped = string.Concat(text.Where(character => character is not
                    ('(' or ')' or '[' or ']' or '{' or '}')));
                if (EssayCalculationEvaluator.TryEvaluate(ungrouped, out var plain, out _) && plain == value)
                    continue;
            }

            answer = new(value.Numerator, value.Denominator);
            break;
        }
        if (answer.Denominator.IsZero)
            throw new InvalidOperationException("Unable to generate a valid numeric expression.");

        ReducedFraction? presented = null;
        IReadOnlyList<ReducedFraction> choices = [];
        ReducedFraction[] distractors =
        [
            Offset(answer, -1), Offset(answer, 1), Offset(answer, -2), Offset(answer, 2),
            new(answer.Numerator * 2, answer.Denominator),
            fractions ? new(answer.Numerator, answer.Denominator * 2)
                : new(answer.Numerator / 2, 1)
        ];
        distractors = distractors.Where(value => value.Numerator.Sign >= 0 && value != answer)
            .Distinct().ToArray();
        _random.Shuffle(distractors.AsSpan());
        if (mode == ArithmeticQuizMode.TrueFalse)
            presented = _random.Next(2) == 0 ? answer : distractors[0];
        else if (mode == ArithmeticQuizMode.MultipleChoice)
        {
            ReducedFraction[] options = [answer, .. distractors.Take(3)];
            _random.Shuffle(options.AsSpan());
            choices = options;
        }

        ExpressionStoryContract? story = language.HasValue
            ? CreateStory(tree!, language.Value) : null;
        var contract = new ExpressionQuizContract(type, tier, text, operands,
            brackets ? (int)tier : 0, answer, presented, choices, story);
        BigInteger integerAnswer = fractions ? BigInteger.Zero : answer.Numerator;
        return new(new(integerAnswer, ArithmeticOperation.Add, BigInteger.Zero), mode,
            integerAnswer, fractions ? null : presented?.Numerator,
            presented.HasValue ? presented.Value == answer : null,
            fractions ? [] : choices.Select(choice => choice.Numerator).ToArray(),
            ExpressionProblem: contract);
    }

    private Node CreatePlain(int count, bool fractions, CurriculumTier tier)
    {
        Node first = CreateLeaf(fractions, tier);
        string text = first.Text;
        BigInteger integerTerm = first.Value.Numerator;
        var nodes = new Stack<Node>();
        var operations = new Stack<ArithmeticOperation>();
        nodes.Push(first);
        for (int index = 1; index < count; index++)
        {
            ArithmeticOperation operation = NextOperation();
            Node next = CreateLeaf(fractions, tier);
            if (!fractions && operation == ArithmeticOperation.Divide)
            {
                int[] divisors = Enumerable.Range(2, 9).Where(divisor => integerTerm % divisor == 0).ToArray();
                if (divisors.Length == 0)
                    operation = ArithmeticOperation.Add;
                else
                {
                    int divisor = divisors[_random.Next(divisors.Length)];
                    next = new(divisor.ToString(), new(divisor, 1), 0);
                }
            }
            text += $" {Symbol(operation)} {next.Text}";
            while (operations.TryPeek(out var previous) && Priority(previous) >= Priority(operation))
                Combine(nodes, operations.Pop());
            operations.Push(operation);
            nodes.Push(next);
            integerTerm = operation switch
            {
                ArithmeticOperation.Multiply => integerTerm * next.Value.Numerator,
                ArithmeticOperation.Divide when !fractions => integerTerm / next.Value.Numerator,
                _ => next.Value.Numerator
            };
        }
        while (operations.TryPop(out var operation))
            Combine(nodes, operation);
        return nodes.Pop() with { Text = text };
    }

    private Node CreateGroup(int count, bool fractions, CurriculumTier tier, bool root = false)
    {
        if (count == 1)
            return CreateLeaf(fractions, tier);

        int split = _random.Next(1, count);
        Node left = CreateGroup(split, fractions, tier);
        Node right = CreateGroup(count - split, fractions, tier);
        ArithmeticOperation operation = NextOperation();

        if (operation == ArithmeticOperation.Subtract && Compare(left.Value, right.Value) < 0)
            (left, right) = (right, left);
        if (operation == ArithmeticOperation.Divide &&
            (right.Value.Numerator.IsZero || !fractions &&
                left.Value.Numerator % right.Value.Numerator != 0))
            operation = ArithmeticOperation.Add;

        ReducedFraction value = Calculate(left.Value, right.Value, operation);
        int height = Math.Max(left.Height, right.Height) + 1;
        string text = $"{left.Text} {Symbol(operation)} {right.Text}";
        if (!root)
        {
            (char opening, char closing) = height switch
            {
                1 => ('(', ')'),
                2 => ('[', ']'),
                _ => ('{', '}')
            };
            text = $"{opening}{text}{closing}";
        }
        return new(text, value, height, operation, left, right);
    }

    private static int Priority(ArithmeticOperation operation) =>
        operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide ? 2 : 1;

    private static void Combine(Stack<Node> nodes, ArithmeticOperation operation)
    {
        Node right = nodes.Pop();
        Node left = nodes.Pop();
        nodes.Push(new(string.Empty, Calculate(left.Value, right.Value, operation),
            Math.Max(left.Height, right.Height) + 1, operation, left, right));
    }

    private ExpressionStoryContract CreateStory(Node tree, AppLanguage language)
    {
        (string Vietnamese, string English)[] contexts =
        [
            ("câu lạc bộ toán", "a maths club"),
            ("trò chơi thẻ số", "a number-card game"),
            ("thử thách lập trình robot", "a robot-programming challenge"),
            ("hoạt động nhóm trong lớp", "a classroom team activity"),
            ("cuộc thi tính nhẩm", "a mental-maths contest"),
            ("dự án tìm quy luật số", "a number-pattern project"),
            ("bài thực hành bảng tính", "a spreadsheet activity"),
            ("trò chơi tìm mã bí mật", "a secret-code puzzle")
        ];
        var context = contexts[_random.Next(contexts.Length)];
        bool vietnamese = language == AppLanguage.Vietnamese;
        string name = vietnamese ? context.Vietnamese : context.English;
        string plan = Describe(tree, vietnamese);
        var problem = new MathWordProblem(
            vietnamese
                ? $"Trong {name}, em cần tìm giá trị của {plan}. Kết quả cần ghi là bao nhiêu?"
                : $"In {name}, find the value of {plan}. What is the result?",
            SolutionLead: vietnamese ? "Giá trị cần tìm là:" : "The required value is:",
            AnswerUnit: string.Empty, SubjectName: vietnamese ? "kết quả" : "result");
        return new(language, name, plan, problem);
    }

    private static string Describe(Node node, bool vietnamese)
    {
        if (!node.Operation.HasValue)
            return node.Text;
        string left = Describe(node.Left!, vietnamese);
        string right = Describe(node.Right!, vietnamese);
        if (node.Left!.Operation.HasValue) left = $"({left})";
        if (node.Right!.Operation.HasValue) right = $"({right})";
        return (node.Operation.Value, vietnamese) switch
        {
            (ArithmeticOperation.Add, true) => $"tổng của {left} và {right}",
            (ArithmeticOperation.Subtract, true) => $"hiệu của {left} và {right}",
            (ArithmeticOperation.Multiply, true) => $"tích của {left} và {right}",
            (ArithmeticOperation.Divide, true) => $"thương của {left} chia cho {right}",
            (ArithmeticOperation.Add, false) => $"the sum of {left} and {right}",
            (ArithmeticOperation.Subtract, false) => $"the difference between {left} and {right}",
            (ArithmeticOperation.Multiply, false) => $"the product of {left} and {right}",
            _ => $"the quotient of {left} divided by {right}"
        };
    }

    private Node CreateLeaf(bool fractions, CurriculumTier tier)
    {
        if (fractions)
        {
            int denominator = _random.Next(2, 5 + (int)tier * 2);
            var value = new ReducedFraction(_random.Next(1, denominator), denominator);
            return new(value.ToString(), value, 0);
        }
        int integer = _random.Next(1, tier == CurriculumTier.OneStar ? 10 : 10 * (int)tier + 1);
        return new(integer.ToString(), new(integer, 1), 0);
    }

    // Expression stars control operand size, count and grouping, not which
    // operations are available. Integer division remains exact in both paths.
    private ArithmeticOperation NextOperation() =>
        (ArithmeticOperation)_random.Next(4);

    private static string Symbol(ArithmeticOperation operation) => operation switch
    {
        ArithmeticOperation.Add => "+", ArithmeticOperation.Subtract => "−",
        ArithmeticOperation.Multiply => "×", _ => "÷"
    };

    private static int Compare(ReducedFraction left, ReducedFraction right) =>
        (left.Numerator * right.Denominator).CompareTo(right.Numerator * left.Denominator);

    private static ReducedFraction Offset(ReducedFraction value, int offset) =>
        new(value.Numerator + value.Denominator * offset, value.Denominator);

    private static ReducedFraction Calculate(ReducedFraction left, ReducedFraction right, ArithmeticOperation operation) =>
        operation switch
        {
            ArithmeticOperation.Add => new(left.Numerator * right.Denominator + right.Numerator * left.Denominator,
                left.Denominator * right.Denominator),
            ArithmeticOperation.Subtract => new(left.Numerator * right.Denominator - right.Numerator * left.Denominator,
                left.Denominator * right.Denominator),
            ArithmeticOperation.Multiply => new(left.Numerator * right.Numerator, left.Denominator * right.Denominator),
            _ => new(left.Numerator * right.Denominator, left.Denominator * right.Numerator)
        };
}
