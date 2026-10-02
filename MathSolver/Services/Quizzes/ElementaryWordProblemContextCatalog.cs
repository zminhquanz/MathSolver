namespace MathSolver.Services;

/// <summary>
/// Một học sinh có thể xuất hiện trong toán đố. NaturalReference giữ cách
/// gọi tự nhiên theo ngôn ngữ, ví dụ "bạn Lan" hoặc "Emma".
/// </summary>
public sealed record WordProblemStudent(
    string Name,
    string NaturalReference,
    WordProblemStudentGender Gender);

public enum WordProblemStudentGender
{
    Male,
    Female
}

/// <summary>
/// Dữ liệu nhân vật theo ngôn ngữ. Language profile chỉ quản lý cách gọi;
/// chương trình học và quy tắc toán không phụ thuộc vào profile này.
/// </summary>
public sealed record WordProblemPeopleProfile
{
    public WordProblemPeopleProfile(
        IReadOnlyList<WordProblemStudent> maleStudents,
        IReadOnlyList<WordProblemStudent> femaleStudents,
        IReadOnlyList<string> familyReferences,
        IReadOnlyList<string> schoolReferences)
    {
        MaleStudents = maleStudents;
        FemaleStudents = femaleStudents;
        Students = [.. maleStudents, .. femaleStudents];
        FamilyReferences = familyReferences;
        SchoolReferences = schoolReferences;
    }

    public IReadOnlyList<WordProblemStudent> MaleStudents { get; }

    public IReadOnlyList<WordProblemStudent> FemaleStudents { get; }

    public IReadOnlyList<WordProblemStudent> Students { get; }

    public IReadOnlyList<string> FamilyReferences { get; }

    public IReadOnlyList<string> SchoolReferences { get; }
}

/// <summary>
/// Nguồn dữ liệu duy nhất cho tên học sinh và cách xưng hô trong toán đố.
/// Prompt và validator cùng đọc catalog này để không bị lệch quy tắc.
/// </summary>
public static class WordProblemPeopleCatalog
{
    // Thứ tự 100 tên nam và 100 tên nữ dựa trên mẫu SG01 gồm 124.823 nam
    // và 116.189 nữ: https://hoten.org/100-ten-nam-pho-bien-vietnam/
    // và https://hoten.org/100-ten-nu-gioi-pho-bien/.
    private static readonly WordProblemPeopleProfile VietnameseProfile =
        new(
            maleStudents: CreateVietnameseStudents(
                WordProblemStudentGender.Male,
                [
                    "Huy", "Khang", "Bảo", "Minh", "Phúc", "Anh", "Khoa", "Phát", "Đạt", "Khôi",
                    "Long", "Nam", "Duy", "Quân", "Kiệt", "Thịnh", "Tuấn", "Hưng", "Hoàng", "Hiếu",
                    "Nhân", "Trí", "Tài", "Phong", "Nguyên", "An", "Phú", "Thành", "Đức", "Dũng",
                    "Lộc", "Khánh", "Vinh", "Tiến", "Nghĩa", "Thiện", "Hào", "Hải", "Đăng", "Quang",
                    "Lâm", "Nhật", "Trung", "Thắng", "Tú", "Hùng", "Tâm", "Sang", "Sơn", "Thái",
                    "Cường", "Vũ", "Toàn", "Ân", "Thuận", "Bình", "Trường", "Danh", "Kiên", "Phước",
                    "Thiên", "Tân", "Việt", "Khải", "Tín", "Dương", "Tùng", "Quý", "Hậu", "Trọng",
                    "Triết", "Luân", "Phương", "Quốc", "Thông", "Khiêm", "Hòa", "Thanh", "Tường", "Kha",
                    "Vỹ", "Bách", "Khanh", "Mạnh", "Lợi", "Đại", "Hiệp", "Đông", "Nhựt", "Giang",
                    "Kỳ", "Phi", "Tấn", "Văn", "Vương", "Công", "Hiển", "Linh", "Ngọc", "Vĩ"
                ]),
            femaleStudents: CreateVietnameseStudents(
                WordProblemStudentGender.Female,
                [
                    "Anh", "Vy", "Ngọc", "Nhi", "Hân", "Thư", "Linh", "Như", "Ngân", "Phương",
                    "Thảo", "My", "Trân", "Quỳnh", "Nghi", "Trang", "Trâm", "An", "Thy", "Châu",
                    "Trúc", "Uyên", "Yến", "Ý", "Tiên", "Mai", "Hà", "Vân", "Nguyên", "Hương",
                    "Quyên", "Duyên", "Kim", "Trinh", "Thanh", "Tuyền", "Hằng", "Dương", "Chi", "Giang",
                    "Tâm", "Lam", "Tú", "Ánh", "Hiền", "Khánh", "Minh", "Huyền", "Thùy", "Vi",
                    "Ly", "Dung", "Nhung", "Phúc", "Lan", "Phụng", "Ân", "Thi", "Khanh", "Kỳ",
                    "Nga", "Tường", "Thúy", "Mỹ", "Hoa", "Tuyết", "Lâm", "Thủy", "Đan", "Hạnh",
                    "Xuân", "Oanh", "Mẫn", "Khuê", "Diệp", "Thương", "Nhiên", "Băng", "Hồng", "Bình",
                    "Loan", "Thơ", "Phượng", "Mi", "Nhã", "Nguyệt", "Bích", "Đào", "Diễm", "Kiều",
                    "Hiếu", "Di", "Liên", "Trà", "Tuệ", "Thắm", "Diệu", "Quân", "Nhàn", "Doanh"
                ]),
            familyReferences:
            [
                "ba", "bố", "cha", "tía", "mẹ", "má", "ông", "bà",
                "anh", "chị", "em", "cô", "cậu", "mợ", "dì", "chú", "bác"
            ],
            schoolReferences:
            [
                "bạn", "cô giáo", "thầy giáo", "giáo viên",
                "học sinh", "các bạn trong lớp"
            ]);

    // Top 100 boys' and girls' names from the official U.S. Social Security
    // Administration aggregate for 2020-2025 (100% sample, March 2026):
    // https://www.ssa.gov/oact/babynames/decades/names2020s.html
    private static readonly WordProblemPeopleProfile EnglishProfile =
        new(
            maleStudents: CreateEnglishStudents(
                WordProblemStudentGender.Male,
                [
                    "Liam", "Noah", "Oliver", "James", "Elijah", "William", "Henry", "Lucas", "Theodore", "Benjamin",
                    "Mateo", "Levi", "Sebastian", "Jack", "Daniel", "Michael", "Alexander", "Ethan", "Samuel", "Owen",
                    "John", "Asher", "Ezra", "Leo", "Jackson", "Mason", "Hudson", "Joseph", "David", "Jacob",
                    "Julian", "Logan", "Luke", "Luca", "Matthew", "Wyatt", "Aiden", "Elias", "Gabriel", "Dylan",
                    "Grayson", "Isaac", "Thomas", "Carter", "Maverick", "Anthony", "Santiago", "Jayden", "Miles", "Charles",
                    "Josiah", "Caleb", "Lincoln", "Cooper", "Ezekiel", "Isaiah", "Christopher", "Joshua", "Nathan", "Andrew",
                    "Nolan", "Roman", "Cameron", "Adrian", "Angel", "Waylon", "Wesley", "Bennett", "Jaxon", "Aaron",
                    "Kai", "Brooks", "Axel", "Christian", "Eli", "Ian", "Ryan", "Weston", "Jonathan", "Beau",
                    "Rowan", "Everett", "Silas", "Leonardo", "Robert", "Colton", "Thiago", "Jeremiah", "Easton", "Landon",
                    "Jose", "Micah", "Parker", "Jordan", "Jameson", "Gael", "Adam", "Dominic", "Hunter", "Xavier"
                ]),
            femaleStudents: CreateEnglishStudents(
                WordProblemStudentGender.Female,
                [
                    "Olivia", "Emma", "Charlotte", "Amelia", "Sophia", "Mia", "Isabella", "Ava", "Evelyn", "Harper",
                    "Luna", "Camila", "Sofia", "Eleanor", "Elizabeth", "Gianna", "Scarlett", "Violet", "Ella", "Emily",
                    "Chloe", "Abigail", "Aria", "Penelope", "Aurora", "Hazel", "Avery", "Nora", "Lily", "Ellie",
                    "Mila", "Layla", "Eliana", "Madison", "Isla", "Grace", "Nova", "Zoe", "Lucy", "Riley",
                    "Willow", "Ivy", "Emilia", "Victoria", "Stella", "Naomi", "Hannah", "Zoey", "Elena", "Leah",
                    "Lillian", "Valentina", "Maya", "Paisley", "Delilah", "Addison", "Everly", "Natalie", "Genesis", "Sophie",
                    "Sadie", "Madelyn", "Ruby", "Josephine", "Leilani", "Claire", "Alice", "Kinsley", "Audrey", "Adeline",
                    "Kennedy", "Autumn", "Aaliyah", "Lainey", "Brooklyn", "Emery", "Eloise", "Caroline", "Anna", "Quinn",
                    "Iris", "Savannah", "Hailey", "Vivian", "Clara", "Aubrey", "Bella", "Gabriella", "Jade", "Madeline",
                    "Sarah", "Cora", "Maria", "Allison", "Liliana", "Lydia", "Natalia", "Athena", "Ariana", "Serenity"
                ]),
            familyReferences:
            [
                "mother", "father", "mom", "dad",
                "grandmother", "grandfather", "brother", "sister"
            ],
            schoolReferences:
            [
                "friend", "teacher", "student", "classmate",
                "classmates"
            ]);

    public static WordProblemPeopleProfile GetProfile(
        AppLanguage language) =>
        language == AppLanguage.Vietnamese
            ? VietnameseProfile
            : EnglishProfile;

    private static WordProblemStudent[] CreateVietnameseStudents(
        WordProblemStudentGender gender,
        IReadOnlyList<string> names) =>
        names
            .Select(name =>
                new WordProblemStudent(
                    name,
                    $"bạn {name}",
                    gender))
            .ToArray();

    private static WordProblemStudent[] CreateEnglishStudents(
        WordProblemStudentGender gender,
        IReadOnlyList<string> names) =>
        names
            .Select(name =>
                new WordProblemStudent(
                    name,
                    name,
                    gender))
            .ToArray();

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

public enum WordProblemContextCategory
{
    SchoolSupply,
    Comic,
    Book,
    Toy,
    Fruit,
    Sweet,
    Pet,
    OrnamentalPlant
}

/// <summary>
/// Một ngữ cảnh đếm được cho toán đố. NaturalReference là cụm danh từ dùng
/// trong đề; AnswerUnit là đơn vị chuẩn để LLM trả về và bài tự luận chấm.
/// </summary>
public sealed record WordProblemStoryContext(
    WordProblemContextCategory Category,
    string NaturalReference,
    string AnswerUnit);

public sealed record WordProblemStoryContextProfile(
    IReadOnlyList<WordProblemStoryContext> Items);

/// <summary>
/// Catalog ngữ cảnh được chuẩn hóa theo ngôn ngữ. Mỗi lượt sinh chỉ chọn một
/// mục từ catalog; toàn bộ danh sách không bao giờ bị nối vào prompt.
/// </summary>
public static class WordProblemStoryContextCatalog
{
    private static readonly WordProblemStoryContextProfile VietnameseProfile =
        new(
            [
                // Đồ dùng học tập
                Vi(WordProblemContextCategory.SchoolSupply, "cây bút"),
                Vi(WordProblemContextCategory.SchoolSupply, "cây bút chì"),
                Vi(WordProblemContextCategory.SchoolSupply, "cây bút bi"),
                Vi(WordProblemContextCategory.SchoolSupply, "cây bút mực"),
                Vi(WordProblemContextCategory.SchoolSupply, "cây bút màu"),
                Vi(WordProblemContextCategory.SchoolSupply, "cây bút sáp"),
                Vi(WordProblemContextCategory.SchoolSupply, "cây bút dạ quang"),
                Vi(WordProblemContextCategory.SchoolSupply, "cục tẩy"),
                Vi(WordProblemContextCategory.SchoolSupply, "cây thước kẻ"),
                Vi(WordProblemContextCategory.SchoolSupply, "chiếc ê ke"),
                Vi(WordProblemContextCategory.SchoolSupply, "chiếc compa"),
                Vi(WordProblemContextCategory.SchoolSupply, "quyển vở ô ly"),
                Vi(WordProblemContextCategory.SchoolSupply, "quyển vở bài tập"),
                Vi(WordProblemContextCategory.SchoolSupply, "tờ giấy màu"),
                Vi(WordProblemContextCategory.SchoolSupply, "tờ nhãn vở"),
                Vi(WordProblemContextCategory.SchoolSupply, "viên phấn"),
                Vi(WordProblemContextCategory.SchoolSupply, "chiếc bảng con"),
                Vi(WordProblemContextCategory.SchoolSupply, "hộp bút"),
                Vi(WordProblemContextCategory.SchoolSupply, "chiếc cặp sách"),
                Vi(WordProblemContextCategory.SchoolSupply, "chiếc ba lô"),
                Vi(WordProblemContextCategory.SchoolSupply, "lọ hồ dán"),

                // Truyện tranh
                Vi(WordProblemContextCategory.Comic, "quyển truyện tranh thiếu nhi"),
                Vi(WordProblemContextCategory.Comic, "quyển truyện tranh khoa học"),
                Vi(WordProblemContextCategory.Comic, "quyển truyện tranh lịch sử"),
                Vi(WordProblemContextCategory.Comic, "quyển truyện tranh hài"),
                Vi(WordProblemContextCategory.Comic, "quyển truyện tranh phiêu lưu"),
                Vi(WordProblemContextCategory.Comic, "quyển truyện tranh động vật"),
                Vi(WordProblemContextCategory.Comic, "quyển truyện cổ tích minh họa"),
                Vi(WordProblemContextCategory.Comic, "tập truyện tranh"),
                Vi(WordProblemContextCategory.Comic, "bộ truyện tranh"),
                Vi(WordProblemContextCategory.Comic, "cuốn truyện tranh"),

                // Sách
                Vi(WordProblemContextCategory.Book, "quyển sách giáo khoa"),
                Vi(WordProblemContextCategory.Book, "quyển sách bài tập"),
                Vi(WordProblemContextCategory.Book, "quyển sách tham khảo"),
                Vi(WordProblemContextCategory.Book, "quyển sách khoa học"),
                Vi(WordProblemContextCategory.Book, "quyển sách lịch sử"),
                Vi(WordProblemContextCategory.Book, "quyển sách thiếu nhi"),
                Vi(WordProblemContextCategory.Book, "quyển sách truyện"),
                Vi(WordProblemContextCategory.Book, "quyển sách tô màu"),
                Vi(WordProblemContextCategory.Book, "quyển sách ảnh"),
                Vi(WordProblemContextCategory.Book, "quyển từ điển"),
                Vi(WordProblemContextCategory.Book, "cuốn sổ tay"),
                Vi(WordProblemContextCategory.Book, "cuốn sách đố vui"),

                // Đồ chơi
                Vi(WordProblemContextCategory.Toy, "quả bóng"),
                Vi(WordProblemContextCategory.Toy, "chiếc ô tô đồ chơi"),
                Vi(WordProblemContextCategory.Toy, "con búp bê"),
                Vi(WordProblemContextCategory.Toy, "khối gỗ xếp hình"),
                Vi(WordProblemContextCategory.Toy, "viên bi"),
                Vi(WordProblemContextCategory.Toy, "con thú nhồi bông"),
                Vi(WordProblemContextCategory.Toy, "bộ xếp hình"),
                Vi(WordProblemContextCategory.Toy, "con quay"),
                Vi(WordProblemContextCategory.Toy, "đoàn tàu đồ chơi"),
                Vi(WordProblemContextCategory.Toy, "chiếc máy bay đồ chơi"),
                Vi(WordProblemContextCategory.Toy, "con diều"),
                Vi(WordProblemContextCategory.Toy, "sợi dây nhảy"),
                Vi(WordProblemContextCategory.Toy, "quả cầu lông"),
                Vi(WordProblemContextCategory.Toy, "chú robot đồ chơi"),
                Vi(WordProblemContextCategory.Toy, "tấm thẻ hình"),
                Vi(WordProblemContextCategory.Toy, "miếng ghép hình"),

                // Trái cây
                Vi(WordProblemContextCategory.Fruit, "quả táo"),
                Vi(WordProblemContextCategory.Fruit, "quả cam"),
                Vi(WordProblemContextCategory.Fruit, "quả quýt"),
                Vi(WordProblemContextCategory.Fruit, "quả xoài"),
                Vi(WordProblemContextCategory.Fruit, "quả ổi"),
                Vi(WordProblemContextCategory.Fruit, "quả lê"),
                Vi(WordProblemContextCategory.Fruit, "quả chuối"),
                Vi(WordProblemContextCategory.Fruit, "quả dưa hấu"),
                Vi(WordProblemContextCategory.Fruit, "quả dâu tây"),
                Vi(WordProblemContextCategory.Fruit, "quả thanh long"),
                Vi(WordProblemContextCategory.Fruit, "quả lựu"),
                Vi(WordProblemContextCategory.Fruit, "quả bơ"),
                Vi(WordProblemContextCategory.Fruit, "quả dừa"),
                Vi(WordProblemContextCategory.Fruit, "quả khế"),
                Vi(WordProblemContextCategory.Fruit, "quả chôm chôm"),
                Vi(WordProblemContextCategory.Fruit, "quả nhãn"),
                Vi(WordProblemContextCategory.Fruit, "quả vải"),
                Vi(WordProblemContextCategory.Fruit, "quả măng cụt"),
                Vi(WordProblemContextCategory.Fruit, "quả mận"),
                Vi(WordProblemContextCategory.Fruit, "quả đào"),
                Vi(WordProblemContextCategory.Fruit, "quả đu đủ"),
                Vi(WordProblemContextCategory.Fruit, "quả dứa"),

                // Bánh kẹo
                Vi(WordProblemContextCategory.Sweet, "viên kẹo sữa"),
                Vi(WordProblemContextCategory.Sweet, "viên kẹo dẻo"),
                Vi(WordProblemContextCategory.Sweet, "chiếc kẹo mút"),
                Vi(WordProblemContextCategory.Sweet, "thanh sô-cô-la"),
                Vi(WordProblemContextCategory.Sweet, "chiếc bánh quy"),
                Vi(WordProblemContextCategory.Sweet, "chiếc bánh ngọt"),
                Vi(WordProblemContextCategory.Sweet, "chiếc bánh cupcake"),
                Vi(WordProblemContextCategory.Sweet, "chiếc bánh rán"),
                Vi(WordProblemContextCategory.Sweet, "chiếc bánh bao"),
                Vi(WordProblemContextCategory.Sweet, "chiếc bánh trung thu"),
                Vi(WordProblemContextCategory.Sweet, "miếng bánh bông lan"),
                Vi(WordProblemContextCategory.Sweet, "gói bánh quy"),
                Vi(WordProblemContextCategory.Sweet, "hộp kẹo"),
                Vi(WordProblemContextCategory.Sweet, "hũ kẹo"),

                // Thú cưng
                Vi(WordProblemContextCategory.Pet, "chú chó"),
                Vi(WordProblemContextCategory.Pet, "chú mèo"),
                Vi(WordProblemContextCategory.Pet, "chú thỏ"),
                Vi(WordProblemContextCategory.Pet, "chú chuột hamster"),
                Vi(WordProblemContextCategory.Pet, "chú chuột lang"),
                Vi(WordProblemContextCategory.Pet, "chú nhím cảnh"),
                Vi(WordProblemContextCategory.Pet, "chú sóc cảnh"),
                Vi(WordProblemContextCategory.Pet, "chú rùa cảnh"),
                Vi(WordProblemContextCategory.Pet, "chú cá vàng"),
                Vi(WordProblemContextCategory.Pet, "chú cá bảy màu"),
                Vi(WordProblemContextCategory.Pet, "chú chim yến phụng"),
                Vi(WordProblemContextCategory.Pet, "chú chim cảnh"),

                // Cây cảnh
                Vi(WordProblemContextCategory.OrnamentalPlant, "chậu xương rồng"),
                Vi(WordProblemContextCategory.OrnamentalPlant, "chậu sen đá"),
                Vi(WordProblemContextCategory.OrnamentalPlant, "chậu hoa hồng"),
                Vi(WordProblemContextCategory.OrnamentalPlant, "chậu hoa cúc"),
                Vi(WordProblemContextCategory.OrnamentalPlant, "chậu hoa lan"),
                Vi(WordProblemContextCategory.OrnamentalPlant, "chậu trầu bà"),
                Vi(WordProblemContextCategory.OrnamentalPlant, "chậu lưỡi hổ"),
                Vi(WordProblemContextCategory.OrnamentalPlant, "chậu kim tiền"),
                Vi(WordProblemContextCategory.OrnamentalPlant, "chậu phát tài"),
                Vi(WordProblemContextCategory.OrnamentalPlant, "chậu bonsai"),
                Vi(WordProblemContextCategory.OrnamentalPlant, "chậu dương xỉ"),
                Vi(WordProblemContextCategory.OrnamentalPlant, "chậu vạn niên thanh"),
                Vi(WordProblemContextCategory.OrnamentalPlant, "chậu nha đam"),
                Vi(WordProblemContextCategory.OrnamentalPlant, "chậu ngọc ngân")
            ]);

    private static readonly WordProblemStoryContextProfile EnglishProfile =
        new(
            [
                // School supplies
                En(WordProblemContextCategory.SchoolSupply, "pencil", "pencils"),
                En(WordProblemContextCategory.SchoolSupply, "ballpoint pen", "ballpoint pens"),
                En(WordProblemContextCategory.SchoolSupply, "fountain pen", "fountain pens"),
                En(WordProblemContextCategory.SchoolSupply, "colored pencil", "colored pencils"),
                En(WordProblemContextCategory.SchoolSupply, "crayon", "crayons"),
                En(WordProblemContextCategory.SchoolSupply, "marker", "markers"),
                En(WordProblemContextCategory.SchoolSupply, "highlighter", "highlighters"),
                En(WordProblemContextCategory.SchoolSupply, "eraser", "erasers"),
                En(WordProblemContextCategory.SchoolSupply, "ruler", "rulers"),
                En(WordProblemContextCategory.SchoolSupply, "protractor", "protractors"),
                En(WordProblemContextCategory.SchoolSupply, "compass", "compasses"),
                En(WordProblemContextCategory.SchoolSupply, "notebook", "notebooks"),
                En(WordProblemContextCategory.SchoolSupply, "workbook", "workbooks"),
                En(WordProblemContextCategory.SchoolSupply, "sheet of colored paper", "sheets of colored paper"),
                En(WordProblemContextCategory.SchoolSupply, "index card", "index cards"),
                En(WordProblemContextCategory.SchoolSupply, "stick of chalk", "sticks of chalk"),
                En(WordProblemContextCategory.SchoolSupply, "mini whiteboard", "mini whiteboards"),
                En(WordProblemContextCategory.SchoolSupply, "pencil case", "pencil cases"),
                En(WordProblemContextCategory.SchoolSupply, "school bag", "school bags"),
                En(WordProblemContextCategory.SchoolSupply, "glue stick", "glue sticks"),

                // Comics and graphic stories
                En(WordProblemContextCategory.Comic, "children's comic book", "children's comic books"),
                En(WordProblemContextCategory.Comic, "science comic book", "science comic books"),
                En(WordProblemContextCategory.Comic, "history comic book", "history comic books"),
                En(WordProblemContextCategory.Comic, "funny comic book", "funny comic books"),
                En(WordProblemContextCategory.Comic, "adventure comic book", "adventure comic books"),
                En(WordProblemContextCategory.Comic, "animal comic book", "animal comic books"),
                En(WordProblemContextCategory.Comic, "superhero comic book", "superhero comic books"),
                En(WordProblemContextCategory.Comic, "graphic novel", "graphic novels"),
                En(WordProblemContextCategory.Comic, "comic issue", "comic issues"),
                En(WordProblemContextCategory.Comic, "comic collection", "comic collections"),

                // Books
                En(WordProblemContextCategory.Book, "textbook", "textbooks"),
                En(WordProblemContextCategory.Book, "storybook", "storybooks"),
                En(WordProblemContextCategory.Book, "reference book", "reference books"),
                En(WordProblemContextCategory.Book, "science book", "science books"),
                En(WordProblemContextCategory.Book, "history book", "history books"),
                En(WordProblemContextCategory.Book, "children's book", "children's books"),
                En(WordProblemContextCategory.Book, "picture book", "picture books"),
                En(WordProblemContextCategory.Book, "coloring book", "coloring books"),
                En(WordProblemContextCategory.Book, "dictionary", "dictionaries"),
                En(WordProblemContextCategory.Book, "puzzle book", "puzzle books"),
                En(WordProblemContextCategory.Book, "activity book", "activity books"),
                En(WordProblemContextCategory.Book, "fact book", "fact books"),

                // Toys
                En(WordProblemContextCategory.Toy, "ball", "balls"),
                En(WordProblemContextCategory.Toy, "toy car", "toy cars"),
                En(WordProblemContextCategory.Toy, "doll", "dolls"),
                En(WordProblemContextCategory.Toy, "building block", "building blocks"),
                En(WordProblemContextCategory.Toy, "marble", "marbles"),
                En(WordProblemContextCategory.Toy, "stuffed animal", "stuffed animals"),
                En(WordProblemContextCategory.Toy, "building set", "building sets"),
                En(WordProblemContextCategory.Toy, "spinning top", "spinning tops"),
                En(WordProblemContextCategory.Toy, "toy train", "toy trains"),
                En(WordProblemContextCategory.Toy, "toy airplane", "toy airplanes"),
                En(WordProblemContextCategory.Toy, "kite", "kites"),
                En(WordProblemContextCategory.Toy, "jump rope", "jump ropes"),
                En(WordProblemContextCategory.Toy, "shuttlecock", "shuttlecocks"),
                En(WordProblemContextCategory.Toy, "toy robot", "toy robots"),
                En(WordProblemContextCategory.Toy, "trading card", "trading cards"),
                En(WordProblemContextCategory.Toy, "puzzle piece", "puzzle pieces"),

                // Fruits
                En(WordProblemContextCategory.Fruit, "apple", "apples"),
                En(WordProblemContextCategory.Fruit, "orange", "oranges"),
                En(WordProblemContextCategory.Fruit, "tangerine", "tangerines"),
                En(WordProblemContextCategory.Fruit, "mango", "mangoes"),
                En(WordProblemContextCategory.Fruit, "guava", "guavas"),
                En(WordProblemContextCategory.Fruit, "banana", "bananas"),
                En(WordProblemContextCategory.Fruit, "pear", "pears"),
                En(WordProblemContextCategory.Fruit, "watermelon", "watermelons"),
                En(WordProblemContextCategory.Fruit, "strawberry", "strawberries"),
                En(WordProblemContextCategory.Fruit, "dragon fruit", "dragon fruits"),
                En(WordProblemContextCategory.Fruit, "pomegranate", "pomegranates"),
                En(WordProblemContextCategory.Fruit, "avocado", "avocados"),
                En(WordProblemContextCategory.Fruit, "coconut", "coconuts"),
                En(WordProblemContextCategory.Fruit, "star fruit", "star fruits"),
                En(WordProblemContextCategory.Fruit, "rambutan", "rambutans"),
                En(WordProblemContextCategory.Fruit, "longan", "longans"),
                En(WordProblemContextCategory.Fruit, "lychee", "lychees"),
                En(WordProblemContextCategory.Fruit, "mangosteen", "mangosteens"),
                En(WordProblemContextCategory.Fruit, "plum", "plums"),
                En(WordProblemContextCategory.Fruit, "peach", "peaches"),
                En(WordProblemContextCategory.Fruit, "papaya", "papayas"),
                En(WordProblemContextCategory.Fruit, "pineapple", "pineapples"),

                // Candy and baked treats
                En(WordProblemContextCategory.Sweet, "piece of candy", "pieces of candy"),
                En(WordProblemContextCategory.Sweet, "gummy bear", "gummy bears"),
                En(WordProblemContextCategory.Sweet, "lollipop", "lollipops"),
                En(WordProblemContextCategory.Sweet, "chocolate bar", "chocolate bars"),
                En(WordProblemContextCategory.Sweet, "cookie", "cookies"),
                En(WordProblemContextCategory.Sweet, "brownie", "brownies"),
                En(WordProblemContextCategory.Sweet, "cupcake", "cupcakes"),
                En(WordProblemContextCategory.Sweet, "doughnut", "doughnuts"),
                En(WordProblemContextCategory.Sweet, "muffin", "muffins"),
                En(WordProblemContextCategory.Sweet, "slice of sponge cake", "slices of sponge cake"),
                En(WordProblemContextCategory.Sweet, "packet of cookies", "packets of cookies"),
                En(WordProblemContextCategory.Sweet, "box of candy", "boxes of candy"),
                En(WordProblemContextCategory.Sweet, "jar of candy", "jars of candy"),
                En(WordProblemContextCategory.Sweet, "piece of chocolate", "pieces of chocolate"),

                // Pets
                En(WordProblemContextCategory.Pet, "pet dog", "pet dogs"),
                En(WordProblemContextCategory.Pet, "pet cat", "pet cats"),
                En(WordProblemContextCategory.Pet, "rabbit", "rabbits"),
                En(WordProblemContextCategory.Pet, "hamster", "hamsters"),
                En(WordProblemContextCategory.Pet, "guinea pig", "guinea pigs"),
                En(WordProblemContextCategory.Pet, "pet mouse", "pet mice"),
                En(WordProblemContextCategory.Pet, "pet turtle", "pet turtles"),
                En(WordProblemContextCategory.Pet, "goldfish", "goldfish"),
                En(WordProblemContextCategory.Pet, "guppy", "guppies"),
                En(WordProblemContextCategory.Pet, "budgie", "budgies"),
                En(WordProblemContextCategory.Pet, "canary", "canaries"),
                En(WordProblemContextCategory.Pet, "pet finch", "pet finches"),

                // Ornamental plants
                En(WordProblemContextCategory.OrnamentalPlant, "potted cactus", "potted cacti"),
                En(WordProblemContextCategory.OrnamentalPlant, "potted succulent", "potted succulents"),
                En(WordProblemContextCategory.OrnamentalPlant, "potted rose", "potted roses"),
                En(WordProblemContextCategory.OrnamentalPlant, "potted chrysanthemum", "potted chrysanthemums"),
                En(WordProblemContextCategory.OrnamentalPlant, "potted orchid", "potted orchids"),
                En(WordProblemContextCategory.OrnamentalPlant, "potted pothos plant", "potted pothos plants"),
                En(WordProblemContextCategory.OrnamentalPlant, "potted snake plant", "potted snake plants"),
                En(WordProblemContextCategory.OrnamentalPlant, "potted jade plant", "potted jade plants"),
                En(WordProblemContextCategory.OrnamentalPlant, "potted lucky bamboo plant", "potted lucky bamboo plants"),
                En(WordProblemContextCategory.OrnamentalPlant, "potted bonsai tree", "potted bonsai trees"),
                En(WordProblemContextCategory.OrnamentalPlant, "potted fern", "potted ferns"),
                En(WordProblemContextCategory.OrnamentalPlant, "potted peace lily", "potted peace lilies"),
                En(WordProblemContextCategory.OrnamentalPlant, "potted aloe plant", "potted aloe plants"),
                En(WordProblemContextCategory.OrnamentalPlant, "potted spider plant", "potted spider plants")
            ]);

    // Ngữ cảnh quy mô lớn dùng riêng cho AI/LLM khi dữ kiện đạt hàng nghìn
    // trở lên. Các danh từ này có thể xuất hiện tự nhiên trong kho, nhà máy,
    // trung tâm phân phối, thư viện lớn hoặc vườn ươm; tránh tình huống vô lý
    // như hàng chục nghìn chậu cây trong một khu vườn gia đình/lớp học.
    private static readonly WordProblemStoryContextProfile VietnameseLargeQuantityProfile =
        new(
            [
                Vi(WordProblemContextCategory.Book, "quyển sách"),
                Vi(WordProblemContextCategory.SchoolSupply, "tờ giấy in"),
                Vi(WordProblemContextCategory.Fruit, "quả cam"),
                Vi(WordProblemContextCategory.Fruit, "quả táo"),
                Vi(WordProblemContextCategory.OrnamentalPlant, "cây giống"),
                Vi(WordProblemContextCategory.Toy, "linh kiện điện tử"),
                Vi(WordProblemContextCategory.Toy, "viên gạch"),
                Vi(WordProblemContextCategory.Sweet, "chai nước"),
                Vi(WordProblemContextCategory.Sweet, "gói hàng"),
                Vi(WordProblemContextCategory.SchoolSupply, "tem nhãn")
            ]);

    private static readonly WordProblemStoryContextProfile EnglishLargeQuantityProfile =
        new(
            [
                En(WordProblemContextCategory.Book, "book", "books"),
                En(WordProblemContextCategory.SchoolSupply, "sheet of printing paper", "sheets of printing paper"),
                En(WordProblemContextCategory.Fruit, "orange", "oranges"),
                En(WordProblemContextCategory.Fruit, "apple", "apples"),
                En(WordProblemContextCategory.OrnamentalPlant, "seedling", "seedlings"),
                En(WordProblemContextCategory.Toy, "electronic component", "electronic components"),
                En(WordProblemContextCategory.Toy, "brick", "bricks"),
                En(WordProblemContextCategory.Sweet, "bottle of water", "bottles of water"),
                En(WordProblemContextCategory.Sweet, "parcel", "parcels"),
                En(WordProblemContextCategory.SchoolSupply, "printed label", "printed labels")
            ]);

    // Phép nhân có hai vai trò số nhóm × số vật mỗi nhóm. Ở quy mô lớn chỉ
    // dùng những vật có thể được đóng lô/thùng/pallet hoặc sản xuất hàng loạt.
    private static readonly WordProblemStoryContextProfile VietnameseLargeMultiplicationProfile =
        new(
            [
                Vi(WordProblemContextCategory.Toy, "linh kiện điện tử"),
                Vi(WordProblemContextCategory.Toy, "viên gạch"),
                Vi(WordProblemContextCategory.Sweet, "chai nước"),
                Vi(WordProblemContextCategory.Sweet, "gói hàng"),
                Vi(WordProblemContextCategory.SchoolSupply, "tờ giấy in"),
                Vi(WordProblemContextCategory.SchoolSupply, "tem nhãn"),
                Vi(WordProblemContextCategory.OrnamentalPlant, "cây giống")
            ]);

    private static readonly WordProblemStoryContextProfile EnglishLargeMultiplicationProfile =
        new(
            [
                En(WordProblemContextCategory.Toy, "electronic component", "electronic components"),
                En(WordProblemContextCategory.Toy, "brick", "bricks"),
                En(WordProblemContextCategory.Sweet, "bottle of water", "bottles of water"),
                En(WordProblemContextCategory.Sweet, "parcel", "parcels"),
                En(WordProblemContextCategory.SchoolSupply, "sheet of printing paper", "sheets of printing paper"),
                En(WordProblemContextCategory.SchoolSupply, "printed label", "printed labels"),
                En(WordProblemContextCategory.OrnamentalPlant, "seedling", "seedlings")
            ]);

    public static WordProblemStoryContextProfile GetProfile(
        AppLanguage language) =>
        language == AppLanguage.Vietnamese
            ? VietnameseProfile
            : EnglishProfile;

    public static WordProblemStoryContextProfile GetLargeQuantityProfile(
        AppLanguage language) =>
        language == AppLanguage.Vietnamese
            ? VietnameseLargeQuantityProfile
            : EnglishLargeQuantityProfile;

    public static WordProblemStoryContextProfile GetLargeMultiplicationProfile(
        AppLanguage language) =>
        language == AppLanguage.Vietnamese
            ? VietnameseLargeMultiplicationProfile
            : EnglishLargeMultiplicationProfile;

    private static WordProblemStoryContext Vi(
        WordProblemContextCategory category,
        string reference) =>
        new(category, reference, reference);

    private static WordProblemStoryContext En(
        WordProblemContextCategory category,
        string reference,
        string answerUnit) =>
        new(category, reference, answerUnit);
}

/// <summary>Localized stories for finding a fractional part or its whole.</summary>
public sealed record FractionQuantityStoryContext(
    string Unit, string PartLabel, string WholeLabel,
    string PartProblemTemplate, string WholeProblemTemplate);

public static class FractionQuantityStoryContextCatalog
{
    // {0} is the given quantity; {1} is the fraction. The same context owns
    // the question, solution labels and answer unit in Algorithm and AI/LLM.
    private static readonly IReadOnlyList<FractionQuantityStoryContext> VietnameseProfile = Array.AsReadOnly<FractionQuantityStoryContext>(
    [
        new("quyển sách", "Số sách truyện", "Tổng số sách",
            "Thư viện có {0} quyển sách, trong đó {1} là sách truyện. Thư viện có bao nhiêu quyển sách truyện?",
            "Thư viện có {0} quyển sách truyện, chiếm {1} tổng số sách. Thư viện có tất cả bao nhiêu quyển sách?"),
        new("quả cam", "Số cam đã bán", "Số cam ban đầu",
            "Cửa hàng có {0} quả cam và đã bán {1} số cam đó. Cửa hàng đã bán bao nhiêu quả cam?",
            "Cửa hàng đã bán {0} quả cam, bằng {1} số cam ban đầu. Lúc đầu cửa hàng có bao nhiêu quả cam?"),
        new("cây bút", "Số bút đã tặng", "Số bút ban đầu",
            "Một hộp có {0} cây bút. Cô giáo lấy {1} số bút trong hộp để tặng học sinh. Cô giáo đã tặng bao nhiêu cây bút?",
            "Cô giáo lấy {0} cây bút để tặng học sinh, bằng {1} số bút trong hộp lúc đầu. Lúc đầu hộp có bao nhiêu cây bút?"),
        new("học sinh", "Số học sinh tham gia câu lạc bộ", "Tổng số học sinh đăng ký hoạt động ngoại khóa",
            "Một trường có {0} học sinh đăng ký hoạt động ngoại khóa, trong đó {1} số học sinh tham gia câu lạc bộ đọc sách. Có bao nhiêu học sinh tham gia câu lạc bộ?",
            "Có {0} học sinh tham gia câu lạc bộ đọc sách, chiếm {1} số học sinh đăng ký hoạt động ngoại khóa của trường. Có tất cả bao nhiêu học sinh đăng ký hoạt động ngoại khóa?"),
        new("cây", "Số cây đã trồng", "Tổng số cây giống",
            "Vườn ươm có {0} cây giống và đã trồng {1} số cây đó. Vườn ươm đã trồng bao nhiêu cây?",
            "Vườn ươm đã trồng {0} cây, bằng {1} tổng số cây giống. Ban đầu vườn ươm có tất cả bao nhiêu cây giống?"),
        new("m", "Số mét vải đã dùng", "Độ dài tấm vải ban đầu",
            "Một tấm vải dài {0} m. Thợ may dùng {1} tấm vải để may áo. Thợ may đã dùng bao nhiêu mét vải?",
            "Thợ may đã dùng {0} m vải để may áo, bằng {1} chiều dài tấm vải ban đầu. Tấm vải ban đầu dài bao nhiêu mét?"),
        new("kg", "Khối lượng gạo đã bán", "Khối lượng gạo ban đầu",
            "Một bao có {0} kg gạo. Cửa hàng bán {1} lượng gạo trong bao. Cửa hàng đã bán bao nhiêu ki-lô-gam gạo?",
            "Cửa hàng bán {0} kg gạo, bằng {1} lượng gạo trong bao lúc đầu. Lúc đầu bao có bao nhiêu ki-lô-gam gạo?"),
        new("lít", "Số lít nước đã dùng", "Số lít nước ban đầu",
            "Một bình chứa {0} lít nước. Người ta dùng {1} lượng nước trong bình để tưới cây. Đã dùng bao nhiêu lít nước?",
            "Người ta dùng {0} lít nước để tưới cây, bằng {1} lượng nước trong bình lúc đầu. Lúc đầu bình chứa bao nhiêu lít nước?"),
        new("viên bi", "Số bi đã cho", "Số bi ban đầu",
            "Một bạn có {0} viên bi và cho em {1} số bi của mình. Bạn đã cho em bao nhiêu viên bi?",
            "Một bạn cho em {0} viên bi, bằng {1} số bi của mình lúc đầu. Lúc đầu bạn có bao nhiêu viên bi?"),
        new("quả trứng", "Số trứng đã dùng", "Số trứng ban đầu",
            "Một khay có {0} quả trứng. Đầu bếp dùng {1} số trứng trong khay để làm bánh. Đầu bếp đã dùng bao nhiêu quả trứng?",
            "Đầu bếp dùng {0} quả trứng để làm bánh, bằng {1} số trứng trong khay lúc đầu. Lúc đầu khay có bao nhiêu quả trứng?")
    ]);

    private static readonly IReadOnlyList<FractionQuantityStoryContext> EnglishProfile = Array.AsReadOnly<FractionQuantityStoryContext>(
    [
        new("books", "Number of storybooks", "Total number of books",
            "A library has {0} books; {1} of them are storybooks. How many storybooks are there?",
            "A library has {0} storybooks, representing {1} of all its books. How many books does the library have in total?"),
        new("oranges", "Number of oranges sold", "Initial number of oranges",
            "A shop has {0} oranges and sells {1} of them. How many oranges does it sell?",
            "A shop sells {0} oranges, representing {1} of its initial stock. How many oranges did it have at first?"),
        new("pens", "Number of pens given away", "Initial number of pens",
            "A box contains {0} pens. A teacher gives {1} of them to students. How many pens does the teacher give away?",
            "A teacher gives students {0} pens, representing {1} of the pens initially in a box. How many pens were in the box at first?"),
        new("students", "Number of students joining the club", "Total number of students registering for activities",
            "At a school, {0} students register for extracurricular activities; {1} of them join the reading club. How many students join the club?",
            "At a school, {0} students join the reading club, representing {1} of the students registering for extracurricular activities. How many students register for activities in total?"),
        new("seedlings", "Number of seedlings planted", "Total number of seedlings",
            "A nursery has {0} seedlings and plants {1} of them. How many seedlings does it plant?",
            "A nursery plants {0} seedlings, representing {1} of its initial stock. How many seedlings did it have in total?"),
        new("m", "Length of fabric used", "Initial length of fabric",
            "A piece of fabric is {0} m long. A tailor uses {1} of it to make shirts. How many metres of fabric does the tailor use?",
            "A tailor uses {0} m of fabric, representing {1} of the original piece. How many metres long was the original piece?"),
        new("kg", "Mass of rice sold", "Initial mass of rice",
            "A sack contains {0} kg of rice. A shop sells {1} of the rice. How many kilograms of rice does it sell?",
            "A shop sells {0} kg of rice, representing {1} of the rice initially in a sack. How many kilograms of rice were in the sack at first?"),
        new("litres", "Amount of water used", "Initial amount of water",
            "A container holds {0} litres of water. A gardener uses {1} of it to water plants. How many litres of water does the gardener use?",
            "A gardener uses {0} litres of water, representing {1} of the water initially in a container. How many litres did the container hold at first?"),
        new("marbles", "Number of marbles given away", "Initial number of marbles",
            "A child has {0} marbles and gives {1} of them to a younger sibling. How many marbles does the child give away?",
            "A child gives a younger sibling {0} marbles, representing {1} of the initial collection. How many marbles did the child have at first?"),
        new("eggs", "Number of eggs used", "Initial number of eggs",
            "A tray contains {0} eggs. A cook uses {1} of them to make cakes. How many eggs does the cook use?",
            "A cook uses {0} eggs to make cakes, representing {1} of the eggs initially in a tray. How many eggs were on the tray at first?")
    ]);

    public static IReadOnlyList<FractionQuantityStoryContext> GetProfile(AppLanguage language) =>
        language == AppLanguage.Vietnamese ? VietnameseProfile : EnglishProfile;
}

/// <summary>A chart theme keeps its categories, quantity and unit together.</summary>
public sealed record DataChartStoryContext(string Description, IReadOnlyList<string> Labels,
    string Unit, string QuantityName);

public static class DataChartStoryContextCatalog
{
    private static readonly IReadOnlyList<DataChartStoryContext> VietnameseProfile = Array.AsReadOnly<DataChartStoryContext>(
    [
        new("số trái cây ở một quầy hàng", ["Cam", "Táo", "Xoài"], "quả", "số trái cây"),
        new("số vật nuôi ở một trang trại", ["Gà", "Vịt", "Ngỗng"], "con", "số vật nuôi"),
        new("số sách trong thư viện theo thể loại", ["Truyện", "Khoa học", "Lịch sử"], "quyển sách", "số sách"),
        new("số bút trong một cửa hàng", ["Bút chì", "Bút mực", "Bút màu"], "cây bút", "số bút"),
        new("số học sinh chọn một môn thể thao yêu thích", ["Bóng đá", "Bơi", "Cầu lông"], "học sinh", "số học sinh"),
        new("số chậu hoa ở một vườn ươm", ["Hoa hồng", "Hoa cúc", "Hoa lan"], "chậu", "số chậu hoa"),
        new("số phương tiện trong một bãi đỗ xe", ["Xe đạp", "Xe máy", "Ô tô"], "xe", "số phương tiện"),
        new("khối lượng gạo một cửa hàng bán trong ba ngày", ["Thứ hai", "Thứ ba", "Thứ tư"], "kg", "khối lượng gạo"),
        new("lượng nước tưới cho ba nhóm cây trong vườn", ["Rau", "Hoa", "Cây ăn quả"], "lít", "lượng nước"),
        new("số gói hàng theo loại trong một cửa hàng", ["Bánh quy", "Kẹo", "Mứt"], "gói", "số gói hàng")
    ]);

    private static readonly IReadOnlyList<DataChartStoryContext> EnglishProfile = Array.AsReadOnly<DataChartStoryContext>(
    [
        new("the numbers of fruit at a stall", ["Oranges", "Apples", "Mangoes"], "fruit", "number of fruit"),
        new("the numbers of animals on a farm", ["Chickens", "Ducks", "Geese"], "animals", "number of animals"),
        new("the numbers of library books by genre", ["Stories", "Science", "History"], "books", "number of books"),
        new("the numbers of pens and pencils in a shop", ["Pencils", "Ink pens", "Markers"], "items", "number of writing tools"),
        new("the numbers of students choosing one favourite sport", ["Football", "Swimming", "Badminton"], "students", "number of students"),
        new("the numbers of flower pots at a nursery", ["Roses", "Daisies", "Orchids"], "pots", "number of flower pots"),
        new("the numbers of vehicles in a parking area", ["Bicycles", "Motorbikes", "Cars"], "vehicles", "number of vehicles"),
        new("the mass of rice sold by a shop over three days", ["Monday", "Tuesday", "Wednesday"], "kg", "mass of rice"),
        new("the water used for three groups of plants in a garden", ["Vegetables", "Flowers", "Fruit trees"], "litres", "amount of water"),
        new("the numbers of packets by type in a shop", ["Biscuits", "Sweets", "Jam"], "packets", "number of packets")
    ]);

    public static IReadOnlyList<DataChartStoryContext> GetProfile(AppLanguage language) =>
        language == AppLanguage.Vietnamese ? VietnameseProfile : EnglishProfile;
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
