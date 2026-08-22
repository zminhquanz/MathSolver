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
    Percentage
}

/// <summary>
/// Yêu cầu đã được phân giải cho đúng một câu hỏi. Nguồn Thuật toán và AI
/// cùng nhận đối tượng này để không tự diễn giải lựa chọn Hỗn hợp khác nhau.
/// </summary>
public readonly record struct QuizProblemRequest(
    QuizProblemKind Kind,
    ArithmeticOperation? ArithmeticOperation = null,
    FractionOperation? FractionOperation = null,
    ProportionQuizType? ProportionType = null,
    AverageQuizType? AverageType = null,
    PercentageQuizType? PercentageType = null);

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
