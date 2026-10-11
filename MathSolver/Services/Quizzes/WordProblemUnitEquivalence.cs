using System.Text;
using System.Text.RegularExpressions;

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
        "sợi", "thanh", "miếng", "tấm", "đoàn", "ổ"
    ];

    public static bool AreVietnameseUnitsEquivalent(
        string? first,
        string? second,
        string? problemText = null)
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

        if (string.Equals(
                   normalizedFirst,
                   normalizedSecond,
                   StringComparison.Ordinal) ||
               string.Equals(
                   RemoveVietnameseClassifier(normalizedFirst),
                   RemoveVietnameseClassifier(normalizedSecond),
                   StringComparison.Ordinal))
        {
            return true;
        }

        // A bare count classifier may stand for the requested object: "ổ"
        // accepts "bánh mì" only when that is what the question asks for.
        // Compare the entire object name; prose containment is too permissive
        // for a calculation/answer unit (e.g. "kg bánh mì" is not a loaf).
        string firstObject = GetRequestedObject(normalizedFirst, problemText);
        string secondObject = GetRequestedObject(normalizedSecond, problemText);
        return firstObject.Length > 0 && string.Equals(firstObject,
                   RemoveVietnameseClassifier(normalizedSecond), StringComparison.Ordinal) ||
               secondObject.Length > 0 && string.Equals(secondObject,
                   RemoveVietnameseClassifier(normalizedFirst), StringComparison.Ordinal);
    }

    public static bool ContainsVietnameseUnit(
        string? text,
        string? expectedUnit,
        string? problemText = null)
    {
        string normalizedText = NormalizeComparisonText(text);
        string normalizedUnit =
            RemoveVietnameseClassifier(
                NormalizeComparisonText(expectedUnit));

        if (normalizedText.Length > 0 &&
               normalizedUnit.Length > 0 &&
               $" {normalizedText} ".Contains(
                   $" {normalizedUnit} ",
                   StringComparison.Ordinal))
        {
            return true;
        }

        // Some fraction contracts store only a classifier ("chiếc"), while
        // the question names the object ("bao nhiêu chiếc bánh?"). Use the
        // requested object, not an arbitrary noun from the given quantities.
        string targetObject = GetRequestedObject(normalizedUnit, problemText);
        return targetObject.Length > 0 &&
            $" {normalizedText} ".Contains($" {targetObject} ", StringComparison.Ordinal);
    }

    private static string GetRequestedObject(string normalizedUnit, string? problemText)
    {
        if (!VietnameseClassifiers.Contains(normalizedUnit) ||
            string.IsNullOrWhiteSpace(problemText))
        {
            return string.Empty;
        }

        string pattern = @"(?:\bbao\s+nhiêu|\bmấy)\s+" +
            Regex.Escape(normalizedUnit) +
            @"\s+(?<object>[^.!?;\r\n]+?)(?=\s+(?:của|trong|còn\s+lại|mỗi)\b|[.!?;\r\n]|$)";
        Match target = Regex.Match(problemText, pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
        return NormalizeComparisonText(target.Groups["object"].Value);
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
