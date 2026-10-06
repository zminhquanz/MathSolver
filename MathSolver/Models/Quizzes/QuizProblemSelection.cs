namespace MathSolver.Models;

/// <summary>
/// Nhóm engine chịu trách nhiệm tạo và kiểm tra hợp đồng của một câu hỏi.
/// Thêm dạng đề mới tại đây rồi đăng ký nó trong QuizProblemTypeCatalog.
/// </summary>
public enum QuizProblemKind
{
    Arithmetic,
    Fraction,
    Geometry,
    FindX,
    Proportion,
    Motion,
    Average,
    Percentage,
    Expression,
    TwoNumbers,
    Measurement,
    Time,
    Remainder,
    Decimal,
    FractionSkills,
    Data,
    Probability,
    VisualGeometry,
    MultiStep
}

/// <summary>
/// Các hình hiện được hỗ trợ trong Toán đố. null ở QuizProblemRequest nghĩa
/// là Hỗn hợp và generator sẽ chọn ngẫu nhiên một hình khả dụng.
/// </summary>
public enum GeometryQuizShape
{
    Square,
    Rectangle,
    Triangle,
    Trapezoid,
    Rhombus,
    Parallelogram,
    Circle,
    Cube,
    RectangularPrism
}

/// <summary>C# math puzzle data and rules.</summary>
public readonly record struct QuizProblemRequest(
    QuizProblemKind Kind,
    ArithmeticOperation? ArithmeticOperation = null,
    FractionOperation? FractionOperation = null,
    ProportionQuizType? ProportionType = null,
    AverageQuizType? AverageType = null,
    PercentageQuizType? PercentageType = null,
    ArithmeticOperation? FindXOperation = null,
    GeometryQuizShape? GeometryShape = null,
    MotionQuizType? MotionType = null,
    ExpressionQuizType? ExpressionType = null,
    GeometryMeasurement? GeometryMeasurement = null,
    ElementaryQuizType? ElementaryType = null,
    bool IsComparison = false);

/// <summary>
/// Một mục nhóm hiển thị trong danh sách dạng đề. FixedRequest bằng null dành
/// cho mục Hỗn hợp; phép tính con của Cơ bản/Phân số được chọn ở tầng kế tiếp.
/// </summary>
public sealed record QuizProblemOption(
    string LocalizationKey,
    QuizProblemRequest? FixedRequest)
{
    public bool IsMixed =>
        FixedRequest is null;
}
