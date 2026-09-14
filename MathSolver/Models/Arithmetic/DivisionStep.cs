namespace MathSolver.Models;

public sealed class DivisionStep
{
    // Chuỗi phần số đang được xét.
    // Dùng string để giữ được số 0 ở đầu nếu cần.
    public string PartialDividendText { get; init; } =
        string.Empty;

    public int QuotientDigit { get; init; }

    public string ProductText { get; init; } =
        string.Empty;

    public string RemainderText { get; init; } =
        string.Empty;

    // Cột cuối của phần số đang xử lý.
    public int EndColumn { get; init; }

    // Cho biết bước này nằm sau dấu thập phân của thương.
    public bool IsAfterDecimalPoint { get; init; }
}