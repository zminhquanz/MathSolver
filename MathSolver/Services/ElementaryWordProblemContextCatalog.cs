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
                new("An", "bạn An"),
                new("Anh", "bạn Anh"),
                new("Ánh", "bạn Ánh"),
                new("Ân", "bạn Ân"),

                new("Bảo", "bạn Bảo"),
                new("Bình", "bạn Bình"),
                new("Bích", "bạn Bích"),

                new("Châu", "bạn Châu"),
                new("Chinh", "bạn Chinh"),
                new("Cúc", "bạn Cúc"),
                new("Cương", "bạn Cương"),
                new("Cường", "bạn Cường"),
                new("Chi", "bạn Chi"),
                new("Chí", "bạn Chí"),
                new("Chiến", "bạn Chiến"),
                new("Chung", "bạn Chung"),

                new("Dung", "bạn Dung"),
                new("Dương", "bạn Dương"),
                new("Dũng", "bạn Dũng"),
                new("Diệp", "bạn Diệp"),
                new("Duy", "bạn Duy"),

                new("Đức", "bạn Đức"),
                new("Đạt", "bạn Đạt"),
                new("Đan", "bạn Đan"),
                new("Duyên", "bạn Duyên"),
                new("Điền", "bạn Điền"),
                new("Định", "bạn Định"),

                new("Giang", "bạn Giang"),

                new("Hà", "bạn Hà"),
                new("Hán", "bạn Hán"),
                new("Hồ", "bạn Hồ"),
                new("Hạnh", "bạn Hạnh"),
                new("Hiếu", "bạn Hiếu"),
                new("Hoa", "bạn Hoa"),
                new("Huy", "bạn Huy"),
                new("Hằng", "bạn Hằng"),
                new("Hiền", "bạn Hiền"),
                new("Hải", "bạn Hải"),
                new("Hùng", "bạn Hùng"),
                new("Hưng", "bạn Hưng"),
                new("Hương", "bạn Hương"),
                new("Hảo", "bạn Hảo"),
                new("Hồng", "bạn Hồng"),
                new("Hiệp", "bạn Hiệp"),
                new("Hoàng", "bạn Hoàng"),
                new("Huyền", "bạn Huyền"),

                new("Khoa", "bạn Khoa"),
                new("Khánh", "bạn Khánh"),
                new("Khang", "bạn Khang"),
                new("Khuê", "bạn Khuê"),
                new("Khiêm", "bạn Khiêm"),
                new("Khôi", "bạn Khôi"),
                new("Khải", "bạn Khải"),
                new("Khuyết", "bạn Khuyết"),
                new("Kiên", "bạn Kiên"),
                new("Kim", "bạn Kim"),

                new("Luật", "bạn Luật"),
                new("Lan", "bạn Lan"),
                new("Linh", "bạn Linh"),
                new("Lộc", "bạn Lộc"),
                new("Lâm", "bạn Lâm"),
                new("Long", "bạn Long"),
                new("Lý", "bạn Lý"),
                new("Loan", "bạn Loan"),
                new("Lệ", "bạn Lệ"),
                new("Lợi", "bạn Lợi"),
                new("Luân", "bạn Luân"),
                new("Liêm", "bạn Liêm"),
                new("Lương", "bạn Lương"),
                new("Lượng", "bạn Lượng"),

                new("Mai", "bạn Mai"),
                new("Vy", "bạn Vy"),
                new("Ngọc", "bạn Ngọc"),
                new("An", "bạn An"),
                new("Bình", "bạn Bình"),
                new("Minh", "bạn Minh"),
                new("My", "bạn My"),
                new("Mỹ", "bạn Mỹ"),
                new("Mạnh", "bạn Mạnh"),
                new("Mẫn", "bạn Mẫn"),
                new("Mãn", "bạn Mãn"),

                new("Nguyên", "bạn Nguyên"),
                new("Ngân", "bạn Ngân"),
                new("Nga", "bạn Nga"),
                new("Nam", "bạn Nam"),
                new("Ngọc", "bạn Ngọc"),
                new("Nguyệt", "bạn Nguyệt"),
                new("Như", "bạn Như"),
                new("Nhung", "bạn Nhung"),
                new("Nhật", "bạn Nhật"),
                new("Nhựt", "ban Nhựt"),
                new("Nhân", "bạn Nhân"),
                new("Nhi", "bạn Nhi"),
                new("Nhiên", "bạn Nhiên"),
                new("Nhàn", "bạn Nhàn"),
                new("Ninh", "bạn Ninh"),
                new("Nghi", "bạn Nghi"),
                new("Nghị", "bạn Nghị"),
                new("Nhã", "bạn Nhã"),
                new("Nghiêm", "bạn Nghiêm"),
                new("Nghĩa", "bạn Nghĩa"),

                new("Phúc", "bạn Phúc"),
                new("Phương", "bạn Phương"),
                new("Phát", "bạn Phát"),
                new("Phú", "bạn Phú"),
                new("Phước", "bạn Phước"),
                new("Phi", "bạn Phi"),
                new("Phượng", "bạn Phượng"),

                new("Quân", "bạn Quân"),
                new("Quỳnh", "bạn Quỳnh"),
                new("Quốc", "bạn Quốc"),
                new("Quang", "bạn Quang"),
                new("Quyên", "bạn Quyên"),
                new("Quyết", "bạn Quyết"),
                new("Quý", "bạn Quý"),

                new("Oanh", "bạn Oanh"),

                new("Tám", "bạn Tám"),
                new("Tài", "bạn Tài"),
                new("Tâm", "bạn Tâm"),
                new("Hoa", "bạn Hoa"),
                new("Linh", "bạn Linh"),
                new("Huy", "bạn Huy"),
                new("Hà", "bạn Hà"),
                new("Thảo", "bạn Thảo"),
                new("Thành", "bạn Thành"),
                new("Thanh", "bạn Thanh"),
                new("Thoa", "bạn Thoa"),
                new("Thủy", "bạn Thủy"),
                new("Thùy", "bạn Thùy"),
                new("Thúy", "bạn Thúy"),
                new("Thục", "bạn Thục"),
                new("Thắng", "bạn Thắng"),
                new("Thịnh", "bạn Thịnh"),
                new("Thương", "bạn Thương"),
                new("Thu", "bạn Thu"),
                new("Thư", "bạn Thư"),
                new("Thế", "bạn Thế"),
                new("Tèo", "bạn Tèo"),
                new("Trà", "bạn Trà"),
                new("Trang", "bạn Trang"),
                new("Trân", "bạn Trân"),
                new("Trí", "bạn Trí"),
                new("Trâm", "bạn Trâm"),
                new("Trung", "bạn Trung"),
                new("Trinh", "bạn Trinh"),
                new("Triết", "bạn Triết"),
                new("Trúc", "bạn Trúc"),
                new("Trường", "bạn Trường"),
                new("Phúc", "bạn Phúc"),
                new("Quân", "bạn Quân"),
                new("Khoa", "bạn Khoa"),
                new("My", "bạn My"),
                new("Duy", "bạn Duy"),
                new("Tú", "bạn Tú"),
                new("Tuấn", "bạn Tuấn"),
                new("Tiên", "bạn Tiên"),
                new("Tiến", "bạn Tiến"),
                new("Tùng", "bạn Tùng"),
                new("Tường", "bạn Tường"),
                new("Tuyến", "bạn Tuyến"),
                new("Tuyên", "bạn Tuyên"),
                new("Tuyền", "bạn Tuyền"),
                new("Tuyết", "bạn Tuyết"),

                new("Sơn", "bạn Sơn"),
                new("Sang", "bạn Sang"),
                new("Sỹ", "bạn Sỹ"),
                new("Sương", "bạn Sương"),

                new("Rô", "bạn Rô"),

                new("Uyên", "bạn Uyên"),
                new("Uy", "bạn Uy"),

                new("Văn", "bạn Văn"),
                new("Vũ", "bạn Vũ"),
                new("Vy", "bạn Vy"),
                new("Vân", "bạn Vân"),
                new("Việt", "bạn Việt"),
                new("Vinh", "bạn Vinh"),
                new("Vương", "bạn Vương"),

                new("Xuân", "bạn Xuân")

                new("Yến", "bạn Yến"),
                new("Yên", "bạn Yên"),
                new("Chi", "bạn Chi"),
                new("Diệp", "bạn Diệp"),
                new("Vũ", "bạn Vũ")
            ],
            FamilyReferences:
            [
                "ba", "bố", "cha", "tía", "mẹ", "má", "ông ngoại", "bà ngoại", "ông nội", "bà nội",
                "anh", "chị", "em", "cô", "cậu", "mợ", "dì", "chú", "bác"
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
