namespace MathSolver.Models;

/// <summary>
/// Nhóm engine chịu trách nhiệm tạo và kiểm tra hợp đồng của một câu hỏi.
/// Thêm dạng đề mới tại đây rồi đăng ký nó trong QuizProblemTypeCatalog.
/// </summary>
public enum QuizProblemKind
{
    Arithmetic,
    Geometry,
    FindX
}

/// <summary>
/// Yêu cầu đã được phân giải cho đúng một câu hỏi. Nguồn Thuật toán và AI
/// cùng nhận đối tượng này để không tự diễn giải lựa chọn Hỗn hợp khác nhau.
/// </summary>
public readonly record struct QuizProblemRequest(
    QuizProblemKind Kind,
    ArithmeticOperation? ArithmeticOperation = null);

/// <summary>
/// Một mục hiển thị trong danh sách dạng đề. FixedRequest bằng null dành cho
/// mục Hỗn hợp; IncludeInMixed cho biết mục cụ thể có tham gia bộ trộn hay không.
/// </summary>
public sealed record QuizProblemOption(
    string LocalizationKey,
    QuizProblemRequest? FixedRequest,
    bool IncludeInMixed = false)
{
    public bool IsMixed =>
        FixedRequest is null;
}
