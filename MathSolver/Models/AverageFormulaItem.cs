namespace MathSolver.Models;

/// <summary>
/// Nội dung học/tra cứu cho một dạng toán trung bình cộng trong tab Công thức.
/// Đây chỉ là dữ liệu trình bày; phần luyện tập vẫn dùng AverageQuizContract.
/// </summary>
public sealed record AverageFormulaItem(
    AverageQuizType Type,
    string Title,
    string Formula,
    string Rule,
    string Example,
    string Solution);
