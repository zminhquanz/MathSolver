using System.Text;

namespace MathSolver.Services;

/// <summary>
/// Chuẩn hóa đơn vị ngôn ngữ tự nhiên mà không làm mất cách diễn đạt của
/// model. Đặc biệt, cây/cái/chiếc bút được xem là cùng một đơn vị khi chấm.
/// </summary>
public static class WordProblemUnitEquivalence
{
    private static readonly string[] VietnameseClassifiers =
    [
        "cái", "chiếc", "cây", "quyển", "cuốn", "quả", "trái",
        "con", "chú", "tờ", "viên", "bông", "hộp", "chai",
        "cục", "lọ", "hũ", "chậu", "tập", "bộ", "khối",
        "sợi", "thanh", "miếng", "tấm", "đoàn"
    ];

    public static bool AreVietnameseUnitsEquivalent(
        string? first,
        string? second)
    {
        // Hai phía phải đi qua cùng một canonical form. Nhờ vậy các cách
        // viết sô-cô-la, sô‑cô‑la hoặc sô cô la không tạo false negative.
        string normalizedFirst = NormalizeComparisonText(first);
        string normalizedSecond = NormalizeComparisonText(second);

        if (normalizedFirst.Length == 0 ||
            normalizedSecond.Length == 0)
        {
            return false;
        }

        return string.Equals(
                   normalizedFirst,
                   normalizedSecond,
                   StringComparison.Ordinal) ||
               string.Equals(
                   RemoveVietnameseClassifier(normalizedFirst),
                   RemoveVietnameseClassifier(normalizedSecond),
                   StringComparison.Ordinal);
    }

    public static bool ContainsVietnameseUnit(
        string? text,
        string? expectedUnit)
    {
        string normalizedText = NormalizeComparisonText(text);
        string normalizedUnit =
            RemoveVietnameseClassifier(
                NormalizeComparisonText(expectedUnit));

        return normalizedText.Length > 0 &&
               normalizedUnit.Length > 0 &&
               $" {normalizedText} ".Contains(
                   $" {normalizedUnit} ",
                   StringComparison.Ordinal);
    }

    public static bool IsVietnamesePenUnit(
        string? unit)
    {
        string core =
            RemoveVietnameseClassifier(
                Normalize(unit));

        return string.Equals(
                   core,
                   "bút",
                   StringComparison.Ordinal) ||
               core.StartsWith(
                   "bút ",
                   StringComparison.Ordinal);
    }

    public static IReadOnlyList<string> GetVietnamesePenVariants(
        string unit)
    {
        string core =
            RemoveVietnameseClassifier(
                Normalize(unit));

        return IsVietnamesePenUnit(unit)
            ? [
                $"cây {core}",
                $"cái {core}",
                $"chiếc {core}"
            ]
            : [Normalize(unit)];
    }

    public static string RemoveVietnameseClassifier(
        string value)
    {
        foreach (string classifier in VietnameseClassifiers)
        {
            string prefix = $"{classifier} ";

            if (value.StartsWith(
                    prefix,
                    StringComparison.Ordinal))
            {
                return value[prefix.Length..];
            }
        }

        return value;
    }

    public static string Normalize(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(
            ' ',
            value.Trim()
                .TrimEnd('.', '!', '?', ':', ';')
                .ToLowerInvariant()
                .Split(
                    [' ', '\t', '\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizeComparisonText(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = new StringBuilder(value.Length);

        foreach (char character in value.ToLowerInvariant())
        {
            normalized.Append(
                char.IsLetterOrDigit(character) ||
                char.IsNumber(character) ||
                char.IsWhiteSpace(character)
                    ? character
                    : ' ');
        }

        return string.Join(
            ' ',
            normalized.ToString().Split(
                [' ', '\t', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries));
    }
}
