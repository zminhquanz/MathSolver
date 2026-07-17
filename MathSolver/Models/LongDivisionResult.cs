using MathSolver.Models;

namespace MathSolver.Models;

public sealed class LongDivisionResult
{
    // Giá trị người dùng nhập ban đầu.
    public decimal OriginalDividend { get; init; }

    public decimal OriginalDivisor { get; init; }

    // Chuỗi đã chuẩn hóa để đưa vào phép chia đặt tính.
    // Ví dụ 12,6 ÷ 0,3 sẽ thành 126 ÷ 3.
    public string NormalizedDividendText { get; init; } =
        string.Empty;

    public string NormalizedDivisorText { get; init; } =
        string.Empty;

    // Chuỗi thương để drawable có thể vẽ cả dấu thập phân.
    public string QuotientText { get; init; } =
        string.Empty;

    public decimal Quotient { get; init; }

    public decimal Remainder { get; init; }

    public bool IsDecimalDivision { get; init; }

    // Số chữ số đã dịch dấu phẩy để số chia trở thành số nguyên.
    public int DecimalShiftCount { get; init; }

    // Vị trí dấu thập phân trong QuotientText.
    // -1 nghĩa là thương không có dấu thập phân.
    public int QuotientDecimalIndex { get; init; } = -1;

    public IReadOnlyList<DivisionStep> Steps { get; init; } =
        Array.Empty<DivisionStep>();

    public bool IsLongDivisionSupported { get; init; } = true;
}