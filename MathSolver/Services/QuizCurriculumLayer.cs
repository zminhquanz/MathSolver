using MathSolver.Models;

namespace MathSolver.Services;

/// <summary>
/// Curriculum Layer chỉ điều phối nội dung do tab Toán đố sinh.
///
/// Hai luồng được tách rõ:
/// 1) Hỗn hợp toàn bộ: số sao quyết định pool dạng toán được phép xuất hiện.
/// 2) Đã chọn một dạng toán: 1..5 sao chỉ điều khiển độ lớn dữ kiện; không
///    khóa skill/subtype và không làm UI phải thêm/xóa item theo số sao.
///
/// Các rule này dùng chung cho nguồn Thuật toán và AI/LLM. Tab Giải toán
/// không được phụ thuộc Curriculum Layer.
/// </summary>
public static class QuizCurriculumLayer
{
    private sealed record WeightedMixedRequest(
        QuizProblemRequest Request,
        int Weight);

    public sealed record ArithmeticRules(
        CurriculumTier Tier,
        int MaximumValue,
        int MaximumMultiplicationFactor,
        int MaximumDivisionFactor,
        int MaximumDivisionQuotient,
        IReadOnlyList<ArithmeticOperation> AllowedOperations);

    public sealed record FractionRules(
        CurriculumTier Tier,
        bool IsAvailable,
        int MaximumNumerator,
        int MaximumDenominator,
        bool RequireSameDenominatorForAddSubtract,
        IReadOnlyList<FractionOperation> AllowedOperations);

    public sealed record FindXRules(
        CurriculumTier Tier,
        int MaximumAddSubtractValue,
        int MaximumFactor,
        IReadOnlyList<ArithmeticOperation> AllowedOperations);

    public sealed record GeometryRules(
        int MaximumDimension,
        IReadOnlyList<GeometryQuizShape> AllowedShapes,
        bool AllowArea,
        bool AllowVolume);

    private static readonly ArithmeticOperation[] AddSubtract =
    [
        ArithmeticOperation.Add,
        ArithmeticOperation.Subtract
    ];

    private static readonly ArithmeticOperation[] FourOperations =
    [
        ArithmeticOperation.Add,
        ArithmeticOperation.Subtract,
        ArithmeticOperation.Multiply,
        ArithmeticOperation.Divide
    ];

    private static readonly FractionOperation[] FractionAddSubtract =
    [
        FractionOperation.Add,
        FractionOperation.Subtract
    ];

    private static readonly FractionOperation[] FourFractionOperations =
    [
        FractionOperation.Add,
        FractionOperation.Subtract,
        FractionOperation.Multiply,
        FractionOperation.Divide
    ];

    /// <summary>
    /// Giới hạn dữ kiện chính theo số sao.
    /// ★ đơn vị, ★★ chục, ★★★ trăm, ★★★★ nghìn, ★★★★★ chục nghìn.
    /// Toán hạng thứ hai được random theo bậc chữ số từ ★ đến tier hiện tại,
    /// thay vì bị ép cùng bậc chữ số với toán hạng chính. Kết quả không bị ép
    /// nằm trong giới hạn chữ số này (riêng phép nhân vẫn phải nằm trong Int32).
    /// </summary>
    public static int GetMaximumOperandValue(
        CurriculumTier tier) =>
        tier switch
        {
            CurriculumTier.OneStar => 9,
            CurriculumTier.TwoStars => 99,
            CurriculumTier.ThreeStars => 999,
            CurriculumTier.FourStars => 9_999,
            CurriculumTier.FiveStars => 99_999,
            _ => throw new ArgumentOutOfRangeException(nameof(tier))
        };


    /// <summary>
    /// Cận dưới của toán hạng chính ở từng tier để độ khó có ý nghĩa rõ ràng.
    /// Ví dụ ★★★★★ sinh toán hạng chính trong 10.000..99.999, trong khi
    /// toán hạng thứ hai có thể là 2, 37, 418, 6.205...
    /// </summary>
    public static int GetMinimumPrimaryOperandValue(
        CurriculumTier tier) =>
        tier switch
        {
            CurriculumTier.OneStar => 1,
            CurriculumTier.TwoStars => 10,
            CurriculumTier.ThreeStars => 100,
            CurriculumTier.FourStars => 1_000,
            CurriculumTier.FiveStars => 10_000,
            _ => throw new ArgumentOutOfRangeException(nameof(tier))
        };

    public static int NextPrimaryOperand(
        Random random,
        CurriculumTier tier,
        int minimumAllowed = 1,
        int? maximumOverride = null)
    {
        ArgumentNullException.ThrowIfNull(random);

        int maximum = Math.Min(
            GetMaximumOperandValue(tier),
            maximumOverride ?? int.MaxValue);
        int minimum = Math.Max(
            GetMinimumPrimaryOperandValue(tier),
            minimumAllowed);

        if (maximum < minimum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumOverride),
                "No primary operand exists inside the requested range.");
        }

        return random.Next(minimum, maximum + 1);
    }

    /// <summary>
    /// Sinh toán hạng thứ hai theo bậc chữ số đồng xác suất. Với ★★★★★,
    /// b có thể rơi vào 1 chữ số, 2 chữ số, ... hoặc 5 chữ số. Nhờ vậy các
    /// phép kiểu 18.258 ÷ 2 xuất hiện tự nhiên thay vì cả a và b đều rất lớn.
    /// </summary>
    public static int NextSecondaryOperand(
        Random random,
        CurriculumTier tier,
        int minimumAllowed = 1,
        int? maximumOverride = null)
    {
        ArgumentNullException.ThrowIfNull(random);

        int cap = Math.Min(
            GetMaximumOperandValue(tier),
            maximumOverride ?? int.MaxValue);

        var availableTiers = new List<CurriculumTier>(5);
        for (int value = (int)CurriculumTier.OneStar;
             value <= (int)tier;
             value++)
        {
            CurriculumTier candidate = (CurriculumTier)value;
            int bucketMinimum = Math.Max(
                GetMinimumPrimaryOperandValue(candidate),
                minimumAllowed);

            if (bucketMinimum <= cap)
            {
                availableTiers.Add(candidate);
            }
        }

        if (availableTiers.Count == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumOverride),
                "No secondary operand exists inside the requested range.");
        }

        CurriculumTier selected =
            availableTiers[random.Next(availableTiers.Count)];

        int minimum = Math.Max(
            GetMinimumPrimaryOperandValue(selected),
            minimumAllowed);
        int maximum = Math.Min(
            GetMaximumOperandValue(selected),
            cap);

        return random.Next(minimum, maximum + 1);
    }

    public static int NextPrimaryMultiple(
        Random random,
        CurriculumTier tier,
        int divisor,
        int? maximumOverride = null)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (divisor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(divisor));
        }

        int minimum = GetMinimumPrimaryOperandValue(tier);
        int maximum = Math.Min(
            GetMaximumOperandValue(tier),
            maximumOverride ?? int.MaxValue);

        int minimumQuotient =
            Math.Max(1, (minimum + divisor - 1) / divisor);
        int maximumQuotient = maximum / divisor;

        if (maximumQuotient < minimumQuotient)
        {
            throw new ArgumentOutOfRangeException(
                nameof(divisor),
                "No exact multiple exists inside the primary operand range.");
        }

        return divisor * random.Next(
            minimumQuotient,
            maximumQuotient + 1);
    }

    /// <summary>
    /// Skill Mode luôn cho phép chọn cả 1..5 sao. Việc skill nào xuất hiện ở
    /// mốc nào chỉ thuộc Mixed Mode và được mã hóa trong ResolveMixedRequest.
    /// Giữ API này để code cũ không phải biết chi tiết đó.
    /// </summary>
    public static CurriculumTier GetMinimumTier(
        QuizProblemKind kind) =>
        CurriculumTier.OneStar;

    public static bool IsSkillAvailable(
        QuizProblemKind kind,
        CurriculumTier tier) =>
        true;

    public static ArithmeticRules GetArithmeticRules(
        QuizCurriculumContext context)
    {
        int maximum = GetMaximumOperandValue(context.Tier);

        // Chỉ Mixed toàn bộ mới mô phỏng lộ trình phép toán của mốc học.
        // Khi người dùng đã chọn "Phép tính", mọi phép + - × ÷ luôn khả dụng.
        IReadOnlyList<ArithmeticOperation> operations =
            context.IsMixedMode && context.Tier == CurriculumTier.OneStar
                ? AddSubtract
                : FourOperations;

        return new(
            context.Tier,
            maximum,
            maximum,
            maximum,
            maximum,
            operations);
    }

    public static FractionRules GetFractionRules(
        QuizCurriculumContext context)
    {
        int maximum = GetMaximumOperandValue(context.Tier);

        if (!context.IsMixedMode)
        {
            // Skill Mode: phân số luôn chọn được từ ★ đến ★★★★★. Sao chỉ
            // điều khiển độ lớn tử/mẫu, không khóa phép tính hay subtype.
            return new(
                context.Tier,
                true,
                maximum,
                maximum,
                false,
                FourFractionOperations);
        }

        // Mixed toàn bộ: curriculum quyết định từ mốc nào phân số được đưa
        // vào pool. ResolveMixedRequest hiện chỉ chọn Fraction từ ★★★★.
        return context.Tier switch
        {
            CurriculumTier.OneStar or
            CurriculumTier.TwoStars or
            CurriculumTier.ThreeStars => new(
                context.Tier,
                false,
                maximum,
                maximum,
                true,
                Array.Empty<FractionOperation>()),

            CurriculumTier.FourStars => new(
                context.Tier,
                true,
                maximum,
                maximum,
                true,
                FractionAddSubtract),

            CurriculumTier.FiveStars => new(
                context.Tier,
                true,
                maximum,
                maximum,
                false,
                FourFractionOperations),

            _ => throw new ArgumentOutOfRangeException(nameof(context))
        };
    }

    public static FindXRules GetFindXRules(
        QuizCurriculumContext context)
    {
        int maximum = GetMaximumOperandValue(context.Tier);

        IReadOnlyList<ArithmeticOperation> operations =
            context.IsMixedMode &&
            context.Tier is CurriculumTier.OneStar or CurriculumTier.TwoStars
                ? AddSubtract
                : FourOperations;

        return new(
            context.Tier,
            maximum,
            maximum,
            operations);
    }

    public static GeometryRules GetGeometryRules(
        QuizCurriculumContext context)
    {
        // Geometry không dùng dải 9/99/... cứng vì từng công thức cần các
        // kích thước "đẹp" để đáp án tiểu học luôn chính xác. Số sao vẫn làm
        // dữ kiện tăng dần, nhưng Skill Mode không bao giờ khóa hình/subtype.
        int maximumDimension = context.Tier switch
        {
            CurriculumTier.OneStar => 20,
            CurriculumTier.TwoStars => 30,
            CurriculumTier.ThreeStars => 50,
            CurriculumTier.FourStars => 80,
            CurriculumTier.FiveStars => 150,
            _ => throw new ArgumentOutOfRangeException(nameof(context))
        };

        if (!context.IsMixedMode)
        {
            return new(
                maximumDimension,
                Enum.GetValues<GeometryQuizShape>(),
                AllowArea: true,
                AllowVolume: true);
        }

        return context.Tier switch
        {
            CurriculumTier.OneStar => new(
                12,
                [GeometryQuizShape.Square, GeometryQuizShape.Rectangle],
                AllowArea: false,
                AllowVolume: false),

            CurriculumTier.TwoStars => new(
                20,
                [GeometryQuizShape.Square, GeometryQuizShape.Rectangle],
                AllowArea: false,
                AllowVolume: false),

            CurriculumTier.ThreeStars => new(
                30,
                [
                    GeometryQuizShape.Square,
                    GeometryQuizShape.Rectangle,
                    GeometryQuizShape.Triangle
                ],
                AllowArea: true,
                AllowVolume: false),

            CurriculumTier.FourStars => new(
                40,
                [
                    GeometryQuizShape.Square,
                    GeometryQuizShape.Rectangle,
                    GeometryQuizShape.Triangle,
                    GeometryQuizShape.Trapezoid,
                    GeometryQuizShape.Rhombus,
                    GeometryQuizShape.Parallelogram
                ],
                AllowArea: true,
                AllowVolume: false),

            CurriculumTier.FiveStars => new(
                80,
                Enum.GetValues<GeometryQuizShape>(),
                AllowArea: true,
                AllowVolume: true),

            _ => throw new ArgumentOutOfRangeException(nameof(context))
        };
    }

    public static IReadOnlyList<ProportionQuizType> GetAllowedProportionTypes(
        QuizCurriculumContext context)
    {
        if (!context.IsMixedMode)
        {
            return Enum.GetValues<ProportionQuizType>();
        }

        return context.Tier switch
        {
            CurriculumTier.FourStars => [ProportionQuizType.Direct],
            CurriculumTier.FiveStars =>
                [ProportionQuizType.Direct, ProportionQuizType.Inverse],
            _ => Array.Empty<ProportionQuizType>()
        };
    }

    public static IReadOnlyList<MotionQuizType> GetAllowedMotionTypes(
        QuizCurriculumContext context)
    {
        if (!context.IsMixedMode)
        {
            return Enum.GetValues<MotionQuizType>();
        }

        return context.Tier switch
        {
            CurriculumTier.FourStars => [MotionQuizType.Basic],
            CurriculumTier.FiveStars => Enum.GetValues<MotionQuizType>(),
            _ => Array.Empty<MotionQuizType>()
        };
    }

    public static IReadOnlyList<AverageQuizType> GetAllowedAverageTypes(
        QuizCurriculumContext context)
    {
        if (!context.IsMixedMode)
        {
            return Enum.GetValues<AverageQuizType>();
        }

        return context.Tier switch
        {
            CurriculumTier.FourStars =>
                [AverageQuizType.Direct, AverageQuizType.TotalToAverage],
            CurriculumTier.FiveStars => Enum.GetValues<AverageQuizType>(),
            _ => Array.Empty<AverageQuizType>()
        };
    }

    public static IReadOnlyList<PercentageQuizType> GetAllowedPercentageTypes(
        QuizCurriculumContext context)
    {
        if (!context.IsMixedMode)
        {
            return Enum.GetValues<PercentageQuizType>();
        }

        return context.Tier == CurriculumTier.FiveStars
            ? Enum.GetValues<PercentageQuizType>()
            : Array.Empty<PercentageQuizType>();
    }

    /// <summary>
    /// Curriculum của "Hỗn hợp các dạng". Đây là nơi duy nhất quyết định
    /// skill nào được phép xuất hiện theo mốc sao. Skill Mode không dùng pool
    /// này nên không bị khóa UI theo Curriculum.
    /// </summary>
    public static QuizProblemRequest ResolveMixedRequest(
        CurriculumTier tier,
        Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        WeightedMixedRequest[] pool = tier switch
        {
            CurriculumTier.OneStar =>
            [
                new(new(QuizProblemKind.Arithmetic), 7),
                new(new(QuizProblemKind.FindX), 2),
                new(new(QuizProblemKind.Geometry), 1)
            ],

            CurriculumTier.TwoStars =>
            [
                new(new(QuizProblemKind.Arithmetic), 6),
                new(new(QuizProblemKind.FindX), 2),
                new(new(QuizProblemKind.Geometry), 2)
            ],

            CurriculumTier.ThreeStars =>
            [
                new(new(QuizProblemKind.Arithmetic), 5),
                new(new(QuizProblemKind.FindX), 2),
                new(new(QuizProblemKind.Geometry), 3)
            ],

            CurriculumTier.FourStars =>
            [
                new(new(QuizProblemKind.Arithmetic), 5),
                new(new(QuizProblemKind.Fraction), 3),
                new(new(QuizProblemKind.FindX), 3),
                new(new(QuizProblemKind.Geometry), 3),
                new(new(QuizProblemKind.Proportion), 2),
                new(new(QuizProblemKind.Motion), 2),
                new(new(QuizProblemKind.Average), 2)
            ],

            CurriculumTier.FiveStars =>
            [
                new(new(QuizProblemKind.Arithmetic), 3),
                new(new(QuizProblemKind.Fraction), 3),
                new(new(QuizProblemKind.FindX), 2),
                new(new(QuizProblemKind.Geometry), 3),
                new(new(QuizProblemKind.Proportion), 2),
                new(new(QuizProblemKind.Motion), 2),
                new(new(QuizProblemKind.Average), 2),
                new(new(QuizProblemKind.Percentage), 3)
            ],

            _ => throw new ArgumentOutOfRangeException(nameof(tier))
        };

        int totalWeight = pool.Sum(item => item.Weight);
        int roll = random.Next(totalWeight);

        foreach (WeightedMixedRequest item in pool)
        {
            if (roll < item.Weight)
            {
                return item.Request;
            }

            roll -= item.Weight;
        }

        return pool[^1].Request;
    }

    public static bool IsArithmeticOperationAllowed(
        QuizProblemKind kind,
        ArithmeticOperation operation,
        QuizCurriculumContext context)
    {
        if (kind == QuizProblemKind.Arithmetic)
        {
            return GetArithmeticRules(context)
                .AllowedOperations.Contains(operation);
        }

        if (kind == QuizProblemKind.Fraction)
        {
            FractionOperation mapped = operation switch
            {
                ArithmeticOperation.Add => FractionOperation.Add,
                ArithmeticOperation.Subtract => FractionOperation.Subtract,
                ArithmeticOperation.Multiply => FractionOperation.Multiply,
                ArithmeticOperation.Divide => FractionOperation.Divide,
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };

            return GetFractionRules(context)
                .AllowedOperations.Contains(mapped);
        }

        return true;
    }

    public static string ToStars(CurriculumTier tier) =>
        new string('★', (int)tier) +
        new string('☆', 5 - (int)tier);
}
