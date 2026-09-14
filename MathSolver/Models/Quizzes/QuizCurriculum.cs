namespace MathSolver.Models;

/// <summary>
/// Trục độ khó chuẩn hóa của Math Solver. Đây không phải lớp học của một
/// quốc gia cụ thể; mỗi skill tự ánh xạ 1..5 sao sang phạm vi kiến thức và số.
/// </summary>
public enum CurriculumTier
{
    OneStar = 1,
    TwoStars = 2,
    ThreeStars = 3,
    FourStars = 4,
    FiveStars = 5
}

/// <summary>
/// Ngữ cảnh Curriculum dùng duy nhất cho nội dung do ứng dụng sinh trong tab
/// Toán đố. Không được dùng để giới hạn dữ kiện người dùng tự nhập ở Giải toán.
/// </summary>
public readonly record struct QuizCurriculumContext(
    CurriculumTier Tier,
    bool IsMixedMode)
{
    public int StarCount => (int)Tier;
}
