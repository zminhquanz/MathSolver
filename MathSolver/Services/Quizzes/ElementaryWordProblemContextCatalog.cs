using MathSolver.Models;

namespace MathSolver.Services;

public sealed record FractionQuantityStoryContext(
    string Unit, string PartLabel, string WholeLabel,
    string PartProblemTemplate, string WholeProblemTemplate, int Capacity = 5000, string? ContextId = null);

public static class FractionQuantityStoryContextCatalog
{
    // {0} is the given quantity; {1} is the fraction. The same context owns
    // the question, solution labels and answer unit in the C# generator and grader.
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
        language == AppLanguage.Vietnamese ? CombinedVietnamese : CombinedEnglish;

    private static FractionQuantityStoryContext Limit(FractionQuantityStoryContext c) => c with {
        Capacity = c.Unit switch { "kg" => 50, "m" => 200, "cây bút" or "pencils" => 100, "học sinh" or "students" => 600, _ => 1000 },
        PartProblemTemplate = c.PartProblemTemplate.Replace("bình", "bể").Replace("container", "tank"),
        WholeProblemTemplate = c.WholeProblemTemplate.Replace("bình", "bể").Replace("container", "tank") };
    private static readonly IReadOnlyList<FractionQuantityStoryContext> CombinedVietnamese = Array.AsReadOnly(
        VietnameseProfile.Select(Limit).Concat(Expanded(AppLanguage.Vietnamese)).ToArray());
    private static readonly IReadOnlyList<FractionQuantityStoryContext> CombinedEnglish = Array.AsReadOnly(
        EnglishProfile.Select(Limit).Concat(Expanded(AppLanguage.English)).ToArray());

    private static IEnumerable<FractionQuantityStoryContext> Expanded(AppLanguage language)
    {
        bool vi = language == AppLanguage.Vietnamese;
        foreach (var c in QuizStoryContextCatalog.All.Where(c => c.Id is "kitchen" or "craft" or "community" or "distribution" or "survey"))
        {
            string unit = c.Unit(language), setting = c.Setting(language);
            yield return vi
                ? new(unit, "Lượng đã hoàn thành", "Tổng lượng cần hoàn thành",
                    $"Trong {setting}, cần hoàn thành {{0}} {unit}. Đã hoàn thành {{1}} lượng đó. Hỏi đã hoàn thành bao nhiêu {unit}?",
                    $"Trong {setting}, đã hoàn thành {{0}} {unit}, bằng {{1}} lượng cần hoàn thành. Hỏi cần hoàn thành tất cả bao nhiêu {unit}?", c.MaximumPerPeriod, c.Id)
                : new(unit, "Completed quantity", "Total target quantity",
                    $"For {setting}, the target is {{0}} {unit}. {{1}} of this target is complete. How many {unit} are complete?",
                    $"For {setting}, {{0}} {unit} are complete, representing {{1}} of the target. What is the total target in {unit}?", c.MaximumPerPeriod, c.Id);
        }
    }
}

/// <summary>A chart theme keeps its categories, quantity and unit together.</summary>
public sealed record DataChartStoryContext(string Description, IReadOnlyList<string> Labels,
    string Unit, string QuantityName, int Capacity = 5000, string? ContextId = null);

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
        language == AppLanguage.Vietnamese ? CombinedVietnamese : CombinedEnglish;

    private static IReadOnlyList<DataChartStoryContext> Combine(AppLanguage language) => Array.AsReadOnly(
        (language == AppLanguage.Vietnamese ? VietnameseProfile : EnglishProfile)
        .Concat(QuizStoryContextCatalog.AverageContexts.Select(c => language == AppLanguage.Vietnamese
            ? new DataChartStoryContext($"{c.ViSetting} trong ba {c.ViPeriod} riêng biệt",
                [Capital(c.ViPeriod) + " đầu", Capital(c.ViPeriod) + " giữa", Capital(c.ViPeriod) + " cuối"], c.ViUnit, c.ViUnit, c.MaximumPerPeriod, c.Id)
            : new DataChartStoryContext($"{c.EnSetting} across three separate {QuizStoryContextCatalog.PluralPeriod(c.EnPeriod)}",
                ["First " + c.EnPeriod, "Middle " + c.EnPeriod, "Last " + c.EnPeriod], c.EnUnit, c.EnUnit, c.MaximumPerPeriod, c.Id)))
        .ToArray());
    private static readonly IReadOnlyList<DataChartStoryContext> CombinedVietnamese = Combine(AppLanguage.Vietnamese);
    private static readonly IReadOnlyList<DataChartStoryContext> CombinedEnglish = Combine(AppLanguage.English);
    private static string Capital(string text) => char.ToUpperInvariant(text[0]) + text[1..];
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
