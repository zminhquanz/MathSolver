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
    private enum DirectRateProfile
    {
        GenericCount,
        FabricMeters,
        TreesPerStudent,
        MoneyDong,
        CargoTons,
        FuelLiters,
        RiceBagKilograms,
        VegetableGrams,
        FruitGrams,
        MeatGrams,
        EggGrams,
        DistanceKilometers,
        ContainerLiters,
        PaintAreaSquareMeters
    }

    private sealed record TemplateDefinition(ProportionQuizType Type, ProportionScenarioKind Scenario, string Template, string Unit, string Subject, bool AsksForAdditionalPeople = false, DirectRateProfile RateProfile = DirectRateProfile.GenericCount, string NarrativeId = "");

    private static IReadOnlyList<TemplateDefinition> Templates(AppLanguage language) => QuizContentCatalog.LoadList<TemplateDefinition>("ProportionQuizGenerator.Templates", QuizContentCatalog.Culture(language));

    private readonly Random _random;

    public ProportionQuizGenerator(Random? random = null)
    {
        _random = random ?? Random.Shared;
    }

    public ArithmeticQuizQuestion GenerateAlgorithm(
        ArithmeticQuizMode mode,
        ProportionQuizType? type,
        AppLanguage language,
        QuizCurriculumContext? curriculumContext = null)
    {
        ProportionQuizContract contract = CreateContract(
            type,
            language,
            curriculumContext);
        return CreateQuestion(mode, contract);
    }

    public ArithmeticQuizQuestion GenerateContract(
        ArithmeticQuizMode mode,
        ProportionQuizType? type,
        AppLanguage language,
        QuizCurriculumContext? curriculumContext = null)
    {
        ProportionQuizContract contract = CreateContract(
            type,
            language,
            curriculumContext);
        return CreateQuestion(mode, contract);
    }

    internal ArithmeticQuizQuestion GenerateNarrative(ArithmeticQuizMode mode, ProportionQuizType type,
        AppLanguage language, QuizCurriculumContext context, string narrativeId)
        => CreateQuestion(mode, CreateContract(type, language, context, narrativeId));

    private ProportionQuizContract CreateContract(
        ProportionQuizType? requestedType,
        AppLanguage language,
        QuizCurriculumContext? curriculumContext, string? narrativeId = null)
    {
        IReadOnlyList<ProportionQuizType> allowedTypes =
            curriculumContext.HasValue
                ? QuizCurriculumLayer.GetAllowedProportionTypes(
                    curriculumContext.Value)
                : Enum.GetValues<ProportionQuizType>();

        if (allowedTypes.Count == 0)
        {
            throw new InvalidOperationException(
                "Proportion problems are not available at the selected curriculum tier.");
        }

        int? level = QuizDifficultyPolicy.Level(curriculumContext);
        allowedTypes = QuizDifficultyPolicy.Prefer(allowedTypes, requestedType, level,
            QuizDifficultyPolicy.ProportionTypes);
        ProportionQuizType type =
            requestedType.HasValue &&
            allowedTypes.Contains(requestedType.Value)
                ? requestedType.Value
                : allowedTypes[_random.Next(allowedTypes.Count)];

        TemplateDefinition[] candidates = Candidates(type, language, level);

        if (!string.IsNullOrEmpty(narrativeId))
            candidates = candidates.Where(t => t.NarrativeId == narrativeId).ToArray();
        if (candidates.Length == 0) throw new ArgumentException("InvalidProportionNarrativeProfile");

        TemplateDefinition template = PickWeightedTemplate(candidates);

        (int a, int b, int c, BigInteger answer) =
            template.Type == ProportionQuizType.Direct
                ? CreateDirectNumbers(template.RateProfile, language, level)
                : CreateInverseNumbers(
                    template.Scenario,
                    template.AsksForAdditionalPeople, level);

        string unit = template.Unit;
        string subject = template.Subject;
        string problemText = string.Format(CultureInfo.CurrentCulture, template.Template, a, b, c);

        // Named slots distinguish equal-valued quantities with different roles.
        // Keep legacy positional packs usable; current packs carry reviewed IDs.
        if (template.NarrativeId.Length > 0)
        {
            string[] roles = NarrativeRoles(template);
            problemText = QuizContentCatalog.Text(language, template.NarrativeId,
                (roles[0], a.ToString(CultureInfo.CurrentCulture)),
                (roles[1], b.ToString(CultureInfo.CurrentCulture)),
                (roles[2], c.ToString(CultureInfo.CurrentCulture)));
        }

        return new(template.Type, template.Scenario, a, b, c, answer, unit, subject,
            problemText, template.AsksForAdditionalPeople) { NarrativeId = template.NarrativeId };
    }

    internal static string[] NarrativeIds(ProportionQuizType type, CurriculumTier tier, AppLanguage language)
        => Candidates(type, language, (int)tier).Select(t => t.NarrativeId).Where(id => id.Length > 0).ToArray();

    private static TemplateDefinition[] Candidates(ProportionQuizType type, AppLanguage language, int? level)
        => Templates(language)
            .Where(template => template.Type == type &&
                (level is not >= 4 || type != ProportionQuizType.Direct ||
                    template.RateProfile is not (DirectRateProfile.TreesPerStudent or DirectRateProfile.GenericCount or DirectRateProfile.MoneyDong)) &&
                (level is not <= 2 || (!template.AsksForAdditionalPeople &&
                    template.Scenario is not (ProportionScenarioKind.WorkersRequired or ProportionScenarioKind.MachinesRequired))) &&
                (level is not >= 5 || type != ProportionQuizType.Inverse ||
                    template.AsksForAdditionalPeople || template.Scenario is ProportionScenarioKind.WorkersRequired or ProportionScenarioKind.MachinesRequired))
            .ToArray();

    private static string[] NarrativeRoles(TemplateDefinition template) => template.Scenario switch
    {
        ProportionScenarioKind.Clothing => ["sets", "fabric", "new_sets"],
        ProportionScenarioKind.StudentsPlanting => ["students", "trees", "new_students"],
        ProportionScenarioKind.Shopping => ["quantity", "cost", "new_quantity"],
        ProportionScenarioKind.VehiclesCargo => ["vehicles", "cargo", "new_vehicles"],
        ProportionScenarioKind.VehiclesFuel => ["vehicles", "fuel", "new_vehicles"],
        ProportionScenarioKind.DistanceTime => ["duration", "distance", "new_duration"],
        ProportionScenarioKind.ProductionItems => ["groups", "items", "new_groups"],
        ProportionScenarioKind.RiceBagsWeight or ProportionScenarioKind.FoodWeightGrams or ProportionScenarioKind.EggWeightGrams => ["quantity", "mass", "new_quantity"],
        ProportionScenarioKind.ContainersLiquid => ["containers", "volume", "new_containers"],
        ProportionScenarioKind.PaintArea => ["cans", "area", "new_cans"],
        ProportionScenarioKind.WorkersDays or ProportionScenarioKind.WorkersJob => ["workers", "duration", "new_workers"],
        ProportionScenarioKind.MachinesHours => ["machines", "duration", "new_machines"],
        ProportionScenarioKind.FoodPeopleDays => ["people", "days", "new_people"],
        ProportionScenarioKind.FoodAdditionalPeople => ["people", "days", "new_days"],
        ProportionScenarioKind.SalesStock => ["days", "daily_sales", "new_daily_sales"],
        ProportionScenarioKind.WorkersRequired => ["workers", "days", "new_days"],
        ProportionScenarioKind.MachinesRequired => ["machines", "hours", "new_hours"],
        ProportionScenarioKind.TapsTime => ["taps", "minutes", "new_taps"],
        ProportionScenarioKind.TravelSpeedTime => ["speed", "hours", "new_speed"],
        ProportionScenarioKind.TransportTrips => ["vehicles", "trips_each", "new_vehicles"],
        ProportionScenarioKind.PackagingCount => ["packages", "mass_each", "new_mass_each"],
        _ => throw new InvalidDataException("Unknown proportion roles")
    };

    private TemplateDefinition PickWeightedTemplate(
        IReadOnlyList<TemplateDefinition> candidates)
    {
        if (candidates[0].Type == ProportionQuizType.Inverse)
        {
            // A second wording must not double a scenario's probability.
            // Pick the mathematical context first, then one of its wordings.
            TemplateDefinition[][] scenarios = candidates
                .GroupBy(candidate => candidate.Scenario)
                .Select(group => group.ToArray())
                .ToArray();
            int scenarioRoll = _random.Next(
                scenarios.Sum(group => GetTemplateWeight(group[0])));
            foreach (TemplateDefinition[] group in scenarios)
            {
                scenarioRoll -= GetTemplateWeight(group[0]);
                if (scenarioRoll < 0)
                    return group[_random.Next(group.Length)];
            }

            return scenarios[^1][0];
        }

        int totalWeight = 0;
        foreach (TemplateDefinition candidate in candidates)
        {
            totalWeight += GetTemplateWeight(candidate);
        }

        int roll = _random.Next(totalWeight);
        foreach (TemplateDefinition candidate in candidates)
        {
            roll -= GetTemplateWeight(candidate);
            if (roll < 0)
            {
                return candidate;
            }
        }

        return candidates[^1];
    }

    private static int GetTemplateWeight(
        TemplateDefinition template) =>
        template.Scenario switch
        {
            // Các cặp kinh điển lớp 5: ưu tiên xuất hiện nhiều nhất.
            ProportionScenarioKind.Shopping => 7,
            ProportionScenarioKind.Clothing => 6,
            ProportionScenarioKind.StudentsPlanting => 6,
            ProportionScenarioKind.WorkersDays => 7,
            ProportionScenarioKind.FoodPeopleDays => 6,
            ProportionScenarioKind.MachinesHours => 5,
            ProportionScenarioKind.WorkersJob => 5,

            // Nhóm phổ biến tiếp theo.
            ProportionScenarioKind.VehiclesCargo => 4,
            ProportionScenarioKind.VehiclesFuel => 3,
            ProportionScenarioKind.DistanceTime => 4,
            ProportionScenarioKind.ContainersLiquid => 4,
            ProportionScenarioKind.ProductionItems => 3,
            ProportionScenarioKind.SalesStock => 4,
            ProportionScenarioKind.FoodAdditionalPeople => 3,
            ProportionScenarioKind.WorkersRequired => 5,
            ProportionScenarioKind.MachinesRequired => 4,
            ProportionScenarioKind.TapsTime => 4,
            ProportionScenarioKind.TravelSpeedTime => 4,
            ProportionScenarioKind.TransportTrips => 4,
            ProportionScenarioKind.PackagingCount => 4,

            // Khối lượng / diện tích có trong chương trình nhưng ít gặp hơn.
            ProportionScenarioKind.RiceBagsWeight => 3,
            ProportionScenarioKind.FoodWeightGrams => 1,
            ProportionScenarioKind.EggWeightGrams => 2,
            ProportionScenarioKind.PaintArea => 2,
            _ => 1
        };

    private (int A, int B, int C, BigInteger Answer)
        CreateDirectNumbers(
            DirectRateProfile rateProfile,
            AppLanguage language, int? level)
    {
        int a = level.HasValue ? _random.Next(2, 4 + 2 * level.Value) : _random.Next(2, 11);
        int c;
        do
        {
            c = level is <= 2 ? a * _random.Next(2, 4) : _random.Next(2, 6 + 3 * (level ?? 3));
        }
        while (c == a);

        int rate = rateProfile switch
        {
            DirectRateProfile.FabricMeters => PickFrom([2, 3, 4, 5]),
            DirectRateProfile.TreesPerStudent => _random.Next(2, 9),
            DirectRateProfile.MoneyDong =>
                language == AppLanguage.Vietnamese
                    ? _random.Next(4, 21) * 1000
                    : _random.Next(2, 13),
            DirectRateProfile.CargoTons => _random.Next(2, 11),
            DirectRateProfile.FuelLiters => _random.Next(1, 9) * 5,
            DirectRateProfile.RiceBagKilograms => PickFrom([10, 20, 25, 30, 40, 50]),
            DirectRateProfile.VegetableGrams => _random.Next(2, 11) * 50,
            DirectRateProfile.FruitGrams => PickFrom([100, 125, 150, 175, 200, 225, 250, 300]),
            DirectRateProfile.MeatGrams => _random.Next(2, 11) * 50,
            DirectRateProfile.EggGrams => _random.Next(45, 76),
            DirectRateProfile.DistanceKilometers => _random.Next(30, 91),
            DirectRateProfile.ContainerLiters => _random.Next(1, 7) * 5,
            DirectRateProfile.PaintAreaSquareMeters => _random.Next(2, 7) * 5,
            _ => _random.Next(2, 16)
        };

        // Higher tiers use rational rates only for continuous quantities.
        // Individual trees/items and prices in whole dong keep integer rates.
        int denominator = level is >= 4 ? (level == 5 ? 4 : 2) : 1;
        if (denominator > 1)
        {
            a *= denominator;
            c *= denominator;
        }
        int numerator = rate * denominator + (denominator > 1 ? 1 : 0);
        int b = checked(a * numerator / denominator);
        BigInteger answer = (BigInteger)c * numerator / denominator;
        return (a, b, c, answer);
    }

    private (int A, int B, int C, BigInteger Answer)
        CreateInverseNumbers(
            ProportionScenarioKind scenario,
            bool asksForAdditionalPeople, int? level)
    {
        for (int attempt = 0; attempt < 1024; attempt++)
        {
            int a;
            int b;
            int c;

            switch (scenario)
            {
                case ProportionScenarioKind.FoodPeopleDays:
                case ProportionScenarioKind.FoodAdditionalPeople:
                    a = _random.Next(2, 13) * 10;       // 20..120 người
                    b = _random.Next(3, 16);            // 3..15 ngày
                    c = asksForAdditionalPeople
                        ? _random.Next(1, b)
                        : _random.Next(2, 16) * 10;      // 20..150 người
                    break;

                case ProportionScenarioKind.SalesStock:
                    a = _random.Next(5, 21);             // số ngày dự kiến
                    b = _random.Next(2, 11) * 5;         // 10..50 hộp/ngày
                    c = _random.Next(2, 13) * 5;         // 10..60 hộp/ngày
                    break;

                case ProportionScenarioKind.MachinesHours:
                case ProportionScenarioKind.MachinesRequired:
                    a = _random.Next(2, 11);
                    b = _random.Next(2, 13);
                    c = _random.Next(2, 13);
                    break;

                case ProportionScenarioKind.WorkersDays:
                case ProportionScenarioKind.WorkersJob:
                case ProportionScenarioKind.WorkersRequired:
                    a = _random.Next(4, 21);
                    b = _random.Next(3, 16);
                    c = _random.Next(4, 25);
                    break;

                case ProportionScenarioKind.TapsTime:
                    a = _random.Next(1, 7);
                    b = _random.Next(2, 13) * 5;         // 10..60 phút
                    c = _random.Next(1, 9);
                    break;

                case ProportionScenarioKind.TravelSpeedTime:
                    a = PickFrom([20, 30, 40, 50, 60]); // km/h
                    b = _random.Next(1, 7);             // giờ
                    c = PickFrom([20, 30, 40, 50, 60]);
                    break;

                case ProportionScenarioKind.TransportTrips:
                    a = _random.Next(2, 11);
                    b = _random.Next(2, 13);
                    c = _random.Next(2, 13);
                    break;

                case ProportionScenarioKind.PackagingCount:
                    a = _random.Next(4, 21);
                    b = PickFrom([5, 10, 20, 25, 40, 50]);
                    c = PickFrom([5, 10, 20, 25, 40, 50]);
                    break;

                default:
                    a = _random.Next(2, 13);
                    b = _random.Next(2, 13);
                    c = _random.Next(2, 13);
                    break;
            }

            if (asksForAdditionalPeople)
            {
                int totalPersonDays = checked(a * b);
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

            int changedQuantity = scenario is
                ProportionScenarioKind.WorkersRequired or
                ProportionScenarioKind.MachinesRequired or
                ProportionScenarioKind.SalesStock or
                ProportionScenarioKind.PackagingCount
                    ? b : a;
            if (c == changedQuantity)
            {
                continue;
            }

            if (level is <= 2 &&
                ((changedQuantity % c != 0 && c % changedQuantity != 0) ||
                 Math.Max(changedQuantity, c) / Math.Min(changedQuantity, c) > level.Value + 1))
                continue;
            int total = checked(a * b);
            if (total % c != 0)
            {
                continue;
            }

            int answer = total / c;
            int maxReasonableAnswer = scenario switch
            {
                ProportionScenarioKind.MachinesHours => 24,
                ProportionScenarioKind.SalesStock => 30,
                ProportionScenarioKind.FoodPeopleDays => 30,
                ProportionScenarioKind.WorkersDays or
                ProportionScenarioKind.WorkersJob => 30,
                ProportionScenarioKind.WorkersRequired => 60,
                ProportionScenarioKind.MachinesRequired => 24,
                ProportionScenarioKind.TapsTime => 120,
                ProportionScenarioKind.TravelSpeedTime => 12,
                ProportionScenarioKind.TransportTrips => 24,
                ProportionScenarioKind.PackagingCount => 200,
                _ => 60
            };

            if (answer > 0 && answer <= maxReasonableAnswer)
            {
                return (a, b, c, answer);
            }
        }

        return scenario switch
        {
            ProportionScenarioKind.FoodAdditionalPeople => (40, 6, 4, 20),
            ProportionScenarioKind.FoodPeopleDays => (40, 6, 80, 3),
            ProportionScenarioKind.SalesStock => (10, 30, 50, 6),
            ProportionScenarioKind.MachinesHours => (4, 6, 8, 3),
            ProportionScenarioKind.WorkersDays or
            ProportionScenarioKind.WorkersJob => (6, 8, 12, 4),
            ProportionScenarioKind.WorkersRequired => (6, 8, 4, 12),
            ProportionScenarioKind.MachinesRequired => (4, 6, 3, 8),
            ProportionScenarioKind.TapsTime => (2, 30, 3, 20),
            ProportionScenarioKind.TravelSpeedTime => (40, 3, 60, 2),
            ProportionScenarioKind.TransportTrips => (4, 6, 8, 3),
            ProportionScenarioKind.PackagingCount => (12, 25, 50, 6),
            _ => (4, 6, 8, 3)
        };
    }

    private int PickFrom(IReadOnlyList<int> values) =>
        values[_random.Next(values.Count)];

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
            if (contract.B % contract.A != 0)
                return new((BigInteger)contract.B * contract.C, ArithmeticOperation.Divide, contract.A);
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
            return CreateMoneyDistractors(
                contract,
                correctAnswer,
                count);
        }

        return CreateStandardDistractors(correctAnswer, count);
    }

    private IReadOnlyList<BigInteger> CreateMoneyDistractors(
        ProportionQuizContract contract,
        BigInteger correctAnswer,
        int count)
    {
        var distractors = new HashSet<BigInteger>();
        BigInteger step = GetMoneyDistractorStep(
            contract,
            correctAnswer);

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
        ProportionQuizContract contract,
        BigInteger correctAnswer)
    {
        BigInteger absolute = BigInteger.Abs(correctAnswer);

        if (contract.AnswerUnit.Contains(
                "dollar",
                StringComparison.OrdinalIgnoreCase) ||
            contract.AnswerUnit.Contains(
                "USD",
                StringComparison.OrdinalIgnoreCase))
        {
            if (absolute >= 100)
            {
                return 10;
            }

            if (absolute >= 40)
            {
                return 5;
            }

            return 1;
        }

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
                   StringComparison.OrdinalIgnoreCase) ||
               unit.Contains(
                   "dollar",
                   StringComparison.OrdinalIgnoreCase) ||
               unit.Contains(
                   "USD",
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
