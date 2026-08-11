namespace MathSolver.Services;

/// <summary>
/// Một học sinh có thể xuất hiện trong toán đố. NaturalReference giữ cách
/// gọi tự nhiên theo ngôn ngữ, ví dụ "bạn Lan" hoặc "Emma".
/// </summary>
public sealed record WordProblemStudent(
    string Name,
    string NaturalReference);

/// <summary>
/// Dữ liệu nhân vật theo ngôn ngữ. Language profile chỉ quản lý cách gọi;
/// chương trình học và quy tắc toán không phụ thuộc vào profile này.
/// </summary>
public sealed record WordProblemPeopleProfile(
    IReadOnlyList<WordProblemStudent> Students,
    IReadOnlyList<string> FamilyReferences,
    IReadOnlyList<string> SchoolReferences);

/// <summary>
/// Nguồn dữ liệu duy nhất cho tên học sinh và cách xưng hô trong toán đố.
/// Prompt và validator cùng đọc catalog này để không bị lệch quy tắc.
/// </summary>
public static class WordProblemPeopleCatalog
{
    private static readonly WordProblemPeopleProfile VietnameseProfile =
        new(
            Students:
            [
                new("Lan", "bạn Lan"),
                new("Mai", "bạn Mai"),
                new("Vy", "bạn Vy"),
                new("Ngọc", "bạn Ngọc"),
                new("An", "bạn An"),
                new("Bình", "bạn Bình"),
                new("Minh", "bạn Minh"),
                new("Nam", "bạn Nam"),
                new("Hoa", "bạn Hoa"),
                new("Linh", "bạn Linh"),
                new("Huy", "bạn Huy"),
                new("Hà", "bạn Hà"),
                new("Thảo", "bạn Thảo"),
                new("Trang", "bạn Trang"),
                new("Phúc", "bạn Phúc"),
                new("Quân", "bạn Quân"),
                new("Khoa", "bạn Khoa"),
                new("My", "bạn My"),
                new("Duy", "bạn Duy"),
                new("Tú", "bạn Tú")
            ],
            FamilyReferences:
            [
                "ba", "bố", "mẹ", "ông", "bà",
                "anh", "chị", "em"
            ],
            SchoolReferences:
            [
                "bạn", "cô giáo", "thầy giáo", "giáo viên",
                "học sinh", "các bạn trong lớp"
            ]);

    private static readonly WordProblemPeopleProfile EnglishProfile =
        new(
            Students:
            [
                new("Emma", "Emma"),
                new("Lily", "Lily"),
                new("Mia", "Mia"),
                new("Jack", "Jack"),
                new("Noah", "Noah"),
                new("Olivia", "Olivia"),
                new("Ava", "Ava"),
                new("Liam", "Liam"),
                new("Ethan", "Ethan"),
                new("Grace", "Grace"),
                new("Chloe", "Chloe"),
                new("Sophie", "Sophie"),
                new("Lucas", "Lucas"),
                new("Leo", "Leo"),
                new("Ben", "Ben"),
                new("Ella", "Ella"),
                new("Ruby", "Ruby"),
                new("Henry", "Henry"),
                new("Alice", "Alice"),
                new("Daniel", "Daniel")
            ],
            FamilyReferences:
            [
                "mother", "father", "mom", "dad",
                "grandmother", "grandfather", "brother", "sister"
            ],
            SchoolReferences:
            [
                "friend", "teacher", "student", "classmate",
                "classmates"
            ]);

    public static WordProblemPeopleProfile GetProfile(
        AppLanguage language) =>
        language == AppLanguage.Vietnamese
            ? VietnameseProfile
            : EnglishProfile;

    /// <summary>
    /// Chỉ chấp nhận tên đứng như một từ độc lập, tránh nhận nhầm tên ngắn
    /// như An hoặc My khi chúng nằm bên trong một từ khác.
    /// </summary>
    public static bool ContainsStudentName(
        string text,
        AppLanguage language)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return GetProfile(language)
            .Students
            .Any(student =>
                ContainsWholeName(
                    text,
                    student.Name));
    }

    private static bool ContainsWholeName(
        string text,
        string name)
    {
        int searchStart = 0;

        while (searchStart < text.Length)
        {
            int index = text.IndexOf(
                name,
                searchStart,
                StringComparison.OrdinalIgnoreCase);

            if (index < 0)
            {
                return false;
            }

            int end = index + name.Length;
            bool startsAtBoundary =
                index == 0 ||
                !IsNameCharacter(text[index - 1]);
            bool endsAtBoundary =
                end == text.Length ||
                !IsNameCharacter(text[end]);

            if (startsAtBoundary && endsAtBoundary)
            {
                return true;
            }

            searchStart = index + 1;
        }

        return false;
    }

    private static bool IsNameCharacter(
        char value) =>
        char.IsLetter(value) ||
        char.GetUnicodeCategory(value) is
            System.Globalization.UnicodeCategory.NonSpacingMark or
            System.Globalization.UnicodeCategory.SpacingCombiningMark;
}

/// <summary>
/// Quy tắc tên lớp tiểu học dùng chung cho mọi ngôn ngữ: khối 1–5, lớp con
/// 1–9 hoặc A–I. Dạng số viết đầy đủ bằng dấu gạch chéo, ví dụ 3/1.
/// </summary>
public static class PrimarySchoolClassCatalog
{
    public const int MinimumGrade = 1;
    public const int MaximumGrade = 5;
    public const int MaximumSectionNumber = 9;

    public static IReadOnlyList<int> Grades { get; } =
        Enumerable.Range(
            MinimumGrade,
            MaximumGrade - MinimumGrade + 1)
        .ToArray();

    public static IReadOnlyList<int> NumericSections { get; } =
        Enumerable.Range(
            1,
            MaximumSectionNumber)
        .ToArray();

    public static IReadOnlyList<char> AlphabeticSections { get; } =
        Enumerable.Range(
            'A',
            MaximumSectionNumber)
        .Select(value => (char)value)
        .ToArray();

    public static IReadOnlyList<string> GradeLabels { get; } =
        Grades
            .Select(grade => grade.ToString())
            .ToArray();

    public static IReadOnlyList<string> NumericClassLabels { get; } =
        Grades
            .SelectMany(grade =>
                NumericSections.Select(section =>
                    $"{grade}/{section}"))
            .ToArray();

    public static IReadOnlyList<string> AlphabeticClassLabels { get; } =
        Grades
            .SelectMany(grade =>
                AlphabeticSections.Select(section =>
                    $"{grade}{section}"))
            .ToArray();

    public static bool TryNormalizeLabel(
        string? rawLabel,
        out string normalizedLabel)
    {
        normalizedLabel = string.Concat(
            (rawLabel ?? string.Empty)
                .Where(value => !char.IsWhiteSpace(value)));

        if (normalizedLabel.Length == 1 &&
            TryParseGrade(
                normalizedLabel[0],
                out int gradeOnly))
        {
            normalizedLabel = gradeOnly.ToString();
            return true;
        }

        if (normalizedLabel.Length == 2 &&
            TryParseGrade(
                normalizedLabel[0],
                out int compactGrade))
        {
            char section =
                normalizedLabel[1];

            if (section is >= '1' and <= '9')
            {
                normalizedLabel =
                    $"{compactGrade}/{section}";
                return true;
            }

            char upperSection =
                char.ToUpperInvariant(section);

            if (AlphabeticSections.Contains(upperSection))
            {
                normalizedLabel =
                    $"{compactGrade}{upperSection}";
                return true;
            }
        }

        if (normalizedLabel.Length == 3 &&
            TryParseGrade(
                normalizedLabel[0],
                out int slashGrade) &&
            normalizedLabel[1] == '/' &&
            normalizedLabel[2] is >= '1' and <= '9')
        {
            normalizedLabel =
                $"{slashGrade}/{normalizedLabel[2]}";
            return true;
        }

        return false;
    }

    private static bool TryParseGrade(
        char value,
        out int grade)
    {
        grade = value - '0';

        return grade is >= MinimumGrade and <= MaximumGrade;
    }
}
