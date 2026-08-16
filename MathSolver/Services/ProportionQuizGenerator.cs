using MathSolver.Models;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Services;

/// <summary>
/// Sinh bài toán tỉ lệ thuận/nghịch từ catalog template cố định. Câu chữ được
/// chọn bằng Random và dữ kiện số cũng được sinh bằng Random nhưng luôn đảm
/// bảo đáp án nguyên để phù hợp chương trình tiểu học/THCS cơ bản.
/// </summary>
public sealed class ProportionQuizGenerator
{
    private sealed record TemplateDefinition(
        ProportionQuizType Type,
        ProportionScenarioKind Scenario,
        string VietnameseTemplate,
        string EnglishTemplate,
        string VietnameseUnit,
        string EnglishUnit,
        string VietnameseSubject,
        string EnglishSubject,
        bool AsksForAdditionalPeople = false);

    private static readonly TemplateDefinition[] Templates =
    [
        // Tỉ lệ thuận -------------------------------------------------------
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.Clothing,
            "May {0} bộ quần áo hết {1} mét vải. Hỏi may {2} bộ quần áo như thế hết bao nhiêu mét vải?",
            "Making {0} sets of clothes uses {1} meters of fabric. How many meters of fabric are needed for {2} sets at the same rate?",
            "mét vải",
            "meters of fabric",
            "vải",
            "fabric"),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.Clothing,
            "Cần {1} mét vải để may {0} bộ quần áo. Hỏi may {2} bộ thì cần bao nhiêu mét vải?",
            "It takes {1} meters of fabric to make {0} sets of clothes. How many meters are needed for {2} sets?",
            "mét vải",
            "meters of fabric",
            "vải",
            "fabric"),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.StudentsPlanting,
            "{0} học sinh trồng được {1} cây. Hỏi {2} học sinh trồng được bao nhiêu cây, biết mỗi em trồng như nhau?",
            "{0} students plant {1} trees. How many trees can {2} students plant if every student plants the same number?",
            "cây",
            "trees",
            "cây",
            "trees"),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.StudentsPlanting,
            "Một tổ có {0} em trồng được {1} cây. Hỏi cả lớp {2} em trồng được bao nhiêu cây nếu năng suất mỗi em như nhau?",
            "A group of {0} students plants {1} trees. How many trees can a class of {2} students plant at the same rate?",
            "cây",
            "trees",
            "cây",
            "trees"),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.Shopping,
            "{0} quyển vở giá {1} đồng. Hỏi {2} quyển vở giá bao nhiêu tiền, biết giá mỗi quyển như nhau?",
            "{0} notebooks cost {1} đồng. How much do {2} notebooks cost at the same unit price?",
            "đồng",
            "đồng",
            "tiền",
            "money"),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.Shopping,
            "Mẹ mua {0} quyển tập hết {1} đồng. Hỏi mua {2} quyển thì hết bao nhiêu tiền?",
            "A parent buys {0} notebooks for {1} đồng. How much would {2} notebooks cost at the same unit price?",
            "đồng",
            "đồng",
            "tiền",
            "money"),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.VehiclesCargo,
            "{0} xe chở được {1} tấn hàng. Hỏi {2} xe như thế chở được bao nhiêu tấn hàng?",
            "{0} trucks carry {1} tons of cargo. How many tons can {2} identical trucks carry?",
            "tấn hàng",
            "tons of cargo",
            "hàng",
            "cargo"),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.VehiclesFuel,
            "{0} xe hết {1} lít xăng. Hỏi {2} xe như thế hết bao nhiêu lít xăng?",
            "{0} vehicles use {1} liters of fuel. How many liters do {2} identical vehicles use?",
            "lít xăng",
            "liters of fuel",
            "xăng",
            "fuel"),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.DistanceTime,
            "Một ô tô trong {0} giờ đi được {1} km. Hỏi với cùng vận tốc, trong {2} giờ ô tô đi được bao nhiêu km?",
            "A car travels {1} km in {0} hours. At the same speed, how many kilometers does it travel in {2} hours?",
            "km",
            "km",
            "quãng đường",
            "distance"),

        // Tỉ lệ nghịch -----------------------------------------------------
        new(
            ProportionQuizType.Inverse,
            ProportionScenarioKind.WorkersDays,
            "{0} người làm xong một công việc trong {1} ngày. Hỏi {2} người làm xong công việc đó trong bao nhiêu ngày, biết năng suất mỗi người như nhau?",
            "{0} people finish a job in {1} days. How many days do {2} people need if everyone works at the same rate?",
            "ngày",
            "days",
            "thời gian",
            "time"),
        new(
            ProportionQuizType.Inverse,
            ProportionScenarioKind.WorkersDays,
            "{0} công nhân đắp xong một đoạn đường trong {1} ngày. Hỏi {2} công nhân đắp xong đoạn đường đó trong bao nhiêu ngày?",
            "{0} workers finish a road section in {1} days. How many days do {2} workers need at the same productivity?",
            "ngày",
            "days",
            "thời gian",
            "time"),
        new(
            ProportionQuizType.Inverse,
            ProportionScenarioKind.MachinesHours,
            "{0} máy hoàn thành một công việc trong {1} giờ. Hỏi {2} máy cùng năng suất hoàn thành công việc đó trong bao nhiêu giờ?",
            "{0} machines finish a job in {1} hours. How many hours do {2} equally productive machines need?",
            "giờ",
            "hours",
            "thời gian",
            "time"),
        new(
            ProportionQuizType.Inverse,
            ProportionScenarioKind.WorkersJob,
            "{0} thợ làm xong một công việc trong {1} ngày. Hỏi {2} thợ cùng năng suất làm xong trong bao nhiêu ngày?",
            "{0} workers finish a job in {1} days. How many days do {2} workers need at the same productivity?",
            "ngày",
            "days",
            "thời gian",
            "time"),
        new(
            ProportionQuizType.Inverse,
            ProportionScenarioKind.FoodPeopleDays,
            "Một bếp ăn chuẩn bị gạo đủ cho {0} người ăn trong {1} ngày. Thực tế có {2} người. Hỏi số gạo đó đủ ăn trong bao nhiêu ngày?",
            "A kitchen has enough rice for {0} people for {1} days. If there are actually {2} people, for how many days will the rice last?",
            "ngày",
            "days",
            "thời gian",
            "time"),
        new(
            ProportionQuizType.Inverse,
            ProportionScenarioKind.FoodAdditionalPeople,
            "Chuẩn bị đủ thực phẩm cho {0} người ăn trong {1} ngày. Vì có thêm người nên số thực phẩm đó chỉ đủ ăn trong {2} ngày. Hỏi có thêm bao nhiêu người?",
            "Food is prepared for {0} people for {1} days. Because more people arrive, it lasts only {2} days. How many additional people arrived?",
            "người",
            "people",
            "số người đến thêm",
            "additional people",
            AsksForAdditionalPeople: true),
        new(
            ProportionQuizType.Inverse,
            ProportionScenarioKind.SalesStock,
            "Một cửa hàng chuẩn bị số hộp mứt đủ bán trong {0} ngày nếu mỗi ngày bán {1} hộp. Thực tế mỗi ngày bán {2} hộp. Hỏi số hàng đó đủ bán trong bao nhiêu ngày?",
            "A store has enough jam boxes for {0} days when selling {1} boxes per day. If it actually sells {2} boxes per day, for how many days will the stock last?",
            "ngày",
            "days",
            "thời gian",
            "time")
    ];

    private readonly Random _random;

    public ProportionQuizGenerator(Random? random = null)
    {
        _random = random ?? Random.Shared;
    }

    public ArithmeticQuizQuestion GenerateAlgorithm(
        ArithmeticQuizMode mode,
        ProportionQuizType type,
        AppLanguage language)
    {
        ProportionQuizContract contract = CreateContract(type, language);
        return CreateQuestion(mode, contract);
    }

    public ArithmeticQuizQuestion GenerateContract(
        ArithmeticQuizMode mode,
        ProportionQuizType type,
        AppLanguage language)
    {
        ProportionQuizContract contract = CreateContract(type, language);
        return CreateQuestion(mode, contract);
    }

    private ProportionQuizContract CreateContract(
        ProportionQuizType type,
        AppLanguage language)
    {
        TemplateDefinition[] candidates = Templates
            .Where(template => template.Type == type)
            .ToArray();

        TemplateDefinition template =
            candidates[_random.Next(candidates.Length)];

        (int a, int b, int c, BigInteger answer) =
            template.Type == ProportionQuizType.Direct
                ? CreateDirectNumbers(template.Scenario)
                : CreateInverseNumbers(template.AsksForAdditionalPeople);

        string problemTemplate = language == AppLanguage.Vietnamese
            ? template.VietnameseTemplate
            : template.EnglishTemplate;

        string unit = language == AppLanguage.Vietnamese
            ? template.VietnameseUnit
            : template.EnglishUnit;

        string subject = language == AppLanguage.Vietnamese
            ? template.VietnameseSubject
            : template.EnglishSubject;

        string problemText = string.Format(
            CultureInfo.CurrentCulture,
            problemTemplate,
            a,
            b,
            c);

        return new(
            template.Type,
            template.Scenario,
            a,
            b,
            c,
            answer,
            unit,
            subject,
            problemText,
            template.AsksForAdditionalPeople);
    }

    private (int A, int B, int C, BigInteger Answer)
        CreateDirectNumbers(ProportionScenarioKind scenario)
    {
        int a = _random.Next(2, 11);
        int c;
        do
        {
            c = _random.Next(2, 16);
        }
        while (c == a);

        int rate = scenario switch
        {
            ProportionScenarioKind.Shopping => _random.Next(2, 16) * 1000,
            ProportionScenarioKind.DistanceTime => _random.Next(25, 81),
            _ => _random.Next(2, 16)
        };

        int b = checked(a * rate);
        BigInteger answer = (BigInteger)c * rate;
        return (a, b, c, answer);
    }

    private (int A, int B, int C, BigInteger Answer)
        CreateInverseNumbers(bool asksForAdditionalPeople)
    {
        for (int attempt = 0; attempt < 128; attempt++)
        {
            int a = _random.Next(2, 13);
            int b = _random.Next(2, 13);

            if (asksForAdditionalPeople)
            {
                // c là số ngày mới, phải nhỏ hơn b để thực sự có thêm người.
                int c = _random.Next(1, b);
                int totalPersonDays = a * b;
                if (totalPersonDays % c != 0)
                {
                    continue;
                }

                int newPeople = totalPersonDays / c;
                int added = newPeople - a;
                if (added > 0)
                {
                    return (a, b, c, added);
                }

                continue;
            }

            int cPeopleOrRate = _random.Next(2, 13);
            if (cPeopleOrRate == a)
            {
                continue;
            }

            int total = a * b;
            if (total % cPeopleOrRate != 0)
            {
                continue;
            }

            int answer = total / cPeopleOrRate;
            if (answer > 0)
            {
                return (a, b, cPeopleOrRate, answer);
            }
        }

        // Fallback luôn chia hết.
        return asksForAdditionalPeople
            ? (4, 6, 3, 4)
            : (4, 6, 8, 3);
    }

    private ArithmeticQuizQuestion CreateQuestion(
        ArithmeticQuizMode mode,
        ProportionQuizContract contract)
    {
        IntegerArithmeticExpression expression =
            CreateRepresentativeExpression(contract);

        BigInteger answer = contract.CorrectAnswer;

        return mode switch
        {
            ArithmeticQuizMode.TrueFalse =>
                CreateTrueFalseQuestion(expression, contract, answer),
            ArithmeticQuizMode.MultipleChoice =>
                CreateMultipleChoiceQuestion(expression, contract, answer),
            ArithmeticQuizMode.Essay =>
                new(
                    expression,
                    mode,
                    answer,
                    null,
                    null,
                    [],
                    ProportionProblem: contract),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    private ArithmeticQuizQuestion CreateTrueFalseQuestion(
        IntegerArithmeticExpression expression,
        ProportionQuizContract contract,
        BigInteger answer)
    {
        bool showCorrect = _random.Next(2) == 0;
        BigInteger presented = showCorrect
            ? answer
            : CreateDistractors(contract, answer, 1)[0];

        return new(
            expression,
            ArithmeticQuizMode.TrueFalse,
            answer,
            presented,
            presented == answer,
            [],
            ProportionProblem: contract);
    }

    private ArithmeticQuizQuestion CreateMultipleChoiceQuestion(
        IntegerArithmeticExpression expression,
        ProportionQuizContract contract,
        BigInteger answer)
    {
        var choices = new List<BigInteger> { answer };
        choices.AddRange(CreateDistractors(contract, answer, 3));
        Shuffle(choices);

        return new(
            expression,
            ArithmeticQuizMode.MultipleChoice,
            answer,
            null,
            null,
            choices,
            ProportionProblem: contract);
    }

    private static IntegerArithmeticExpression CreateRepresentativeExpression(
        ProportionQuizContract contract)
    {
        if (contract.IsDirect)
        {
            int unitRate = contract.B / contract.A;
            return new(unitRate, ArithmeticOperation.Multiply, contract.C);
        }

        if (contract.AsksForAdditionalPeople)
        {
            int newPeople = contract.A * contract.B / contract.C;
            return new(newPeople, ArithmeticOperation.Subtract, contract.A);
        }

        int total = contract.A * contract.B;
        return new(total, ArithmeticOperation.Divide, contract.C);
    }

    private IReadOnlyList<BigInteger> CreateDistractors(
        ProportionQuizContract contract,
        BigInteger correctAnswer,
        int count)
    {
        // Câu hỏi tiền tệ không dùng kiểu +/- 1, 2, 3 đồng vì nhìn rất giả
        // và người học có thể loại ngay bằng hình thức đáp án. Với tiền, các
        // phương án nhiễu luôn đi theo bước tiền hợp lý (1.000, 10.000,
        // 100.000...) và có cả đáp án gần lẫn đáp án lệch xa hơn.
        if (IsMoneyProblem(contract))
        {
            return CreateMoneyDistractors(correctAnswer, count);
        }

        return CreateStandardDistractors(correctAnswer, count);
    }

    private IReadOnlyList<BigInteger> CreateMoneyDistractors(
        BigInteger correctAnswer,
        int count)
    {
        var distractors = new HashSet<BigInteger>();
        BigInteger step = GetMoneyDistractorStep(correctAnswer);

        // Ví dụ 84.000 đồng với step 1.000 có thể sinh 81.000, 82.000,
        // 83.000, 85.000, 86.000, 94.000... thay vì 83.998/83.999.
        int[] multipliers = [-10, -5, -3, -2, -1, 1, 2, 3, 5, 10];
        Shuffle(multipliers);

        foreach (int multiplier in multipliers)
        {
            if (distractors.Count >= count)
            {
                break;
            }

            BigInteger candidate = correctAnswer + step * multiplier;
            if (candidate > 0 && candidate != correctAnswer)
            {
                distractors.Add(candidate);
            }
        }

        // Fallback vẫn giữ đúng bội số của step, tuyệt đối không quay về +/-1.
        for (int multiplier = 11;
             distractors.Count < count;
             multiplier++)
        {
            int signedMultiplier = multiplier % 2 == 0
                ? multiplier
                : -multiplier;

            BigInteger candidate =
                correctAnswer + step * signedMultiplier;

            if (candidate > 0 && candidate != correctAnswer)
            {
                distractors.Add(candidate);
            }
        }

        return distractors.ToArray();
    }

    private static BigInteger GetMoneyDistractorStep(
        BigInteger correctAnswer)
    {
        BigInteger absolute = BigInteger.Abs(correctAnswer);

        if (absolute >= 10_000_000)
        {
            return 1_000_000;
        }

        if (absolute >= 1_000_000)
        {
            return 100_000;
        }

        if (absolute >= 100_000)
        {
            return 10_000;
        }

        if (absolute >= 10_000)
        {
            return 1_000;
        }

        if (absolute >= 1_000)
        {
            return 500;
        }

        return 100;
    }

    private static bool IsMoneyProblem(
        ProportionQuizContract contract)
    {
        if (contract.Scenario == ProportionScenarioKind.Shopping)
        {
            return true;
        }

        string unit = contract.AnswerUnit.Trim();
        return unit.Contains(
                   "đồng",
                   StringComparison.OrdinalIgnoreCase) ||
               unit.Contains(
                   "money",
                   StringComparison.OrdinalIgnoreCase) ||
               unit.Contains(
                   "currency",
                   StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<BigInteger> CreateStandardDistractors(
        BigInteger correctAnswer,
        int count)
    {
        var set = new HashSet<BigInteger>();
        int[] offsets = [-10, -5, -3, -2, -1, 1, 2, 3, 5, 10];
        int start = _random.Next(offsets.Length);

        for (int index = 0; index < offsets.Length && set.Count < count; index++)
        {
            BigInteger candidate =
                correctAnswer + offsets[(start + index) % offsets.Length];

            if (candidate > 0 && candidate != correctAnswer)
            {
                set.Add(candidate);
            }
        }

        while (set.Count < count)
        {
            BigInteger candidate = correctAnswer + set.Count + 1;
            if (candidate > 0 && candidate != correctAnswer)
            {
                set.Add(candidate);
            }
        }

        return set.ToArray();
    }

    private void Shuffle<T>(IList<T> values)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int swapIndex = _random.Next(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }
}
