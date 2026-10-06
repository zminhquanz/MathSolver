using MathSolver.Models;

namespace MathSolver.Services;

/// <summary>A situation owns its activity, dimension and physical capacity, independently of stars.</summary>
public sealed record QuizStoryContext(string Id, QuestionKnowledgeGroup Group,
    string ViSetting, string EnSetting, string ViAction, string EnAction,
    string ViUnit, string EnUnit, string UnitId, int MaximumPerPeriod,
    string ViPeriod = "ngày", string EnPeriod = "day")
{
    public string Setting(AppLanguage language) => language == AppLanguage.Vietnamese ? ViSetting : EnSetting;
    public string Unit(AppLanguage language) => language == AppLanguage.Vietnamese ? ViUnit : EnUnit;
    public string Period(AppLanguage language) => language == AppLanguage.Vietnamese ? ViPeriod : EnPeriod;
}

/// <summary>One selected context is shared by C# generators and the AI's reviewed templates.</summary>
public static class QuizStoryContextCatalog
{
    public static IReadOnlyList<QuizStoryContext> All { get; } = Array.AsReadOnly<QuizStoryContext>([
        new("classroom", QuestionKnowledgeGroup.Objects, "hoạt động của lớp học", "classroom activities", "một lớp có", "a class has", "học sinh", "students", "students", 40),
        new("library", QuestionKnowledgeGroup.Objects, "thư viện nhận sách quyên góp", "library book donations", "một thư viện nhận được", "a library receives", "quyển sách", "books", "books", 300),
        new("family-age", QuestionKnowledgeGroup.Time, "tuổi của hai người", "the ages of two people", "", "", "tuổi", "years", "years", 90),
        new("kitchen", QuestionKnowledgeGroup.Production, "chuẩn bị suất ăn", "meal preparation", "một bếp ăn chuẩn bị", "a kitchen prepares", "suất ăn", "meals", "meals", 1000),
        new("livestock", QuestionKnowledgeGroup.Production, "thu trứng ở trang trại", "farm egg collection", "một trang trại thu được", "a farm collects", "quả trứng", "eggs", "eggs", 500),
        new("agriculture", QuestionKnowledgeGroup.Production, "thu hoạch rau", "vegetable harvests", "một nông trại thu hoạch", "a farm harvests", "kg rau", "kg of vegetables", "kg", 2000),
        new("craft", QuestionKnowledgeGroup.Production, "gấp hoa giấy", "paper flower making", "một tổ thủ công gấp được", "a craft team makes", "bông hoa giấy", "paper flowers", "paper-flowers", 200),
        new("distribution", QuestionKnowledgeGroup.Packaging, "đóng gói quà", "gift packing", "một đội đóng gói được", "a packing team prepares", "gói quà", "gift packs", "packs", 500),
        new("traffic", QuestionKnowledgeGroup.Objects, "khách đi xe", "passenger travel", "một tuyến xe đón", "a bus route carries", "lượt khách", "passenger journeys", "passengers", 500),
        new("sports", QuestionKnowledgeGroup.Objects, "thi đấu thể thao", "sports matches", "một đội ghi được", "a team scores", "điểm", "points", "points", 100, "trận", "match"),
        new("events", QuestionKnowledgeGroup.Objects, "chuẩn bị chỗ ngồi cho văn nghệ", "event seating", "một đội bố trí", "a team sets out", "chiếc ghế", "chairs", "chairs", 500, "khu", "section"),
        new("tourism", QuestionKnowledgeGroup.Data, "khách tham quan", "visitor admissions", "một khu tham quan đón", "a visitor centre admits", "lượt khách", "visits", "visits", 1000),
        new("community", QuestionKnowledgeGroup.Objects, "quyên góp sách", "book donations", "một nhóm quyên góp được", "a group donates", "quyển sách", "books", "books", 500),
        new("environment", QuestionKnowledgeGroup.Measurement, "thu gom giấy tái chế", "paper recycling", "một đội thu gom", "a team collects", "kg giấy", "kg of paper", "kg", 200),
        new("decoration", QuestionKnowledgeGroup.Measurement, "cắt ruy băng trang trí", "ribbon cutting", "một tổ sử dụng", "a team uses", "m ruy băng", "m of ribbon", "m", 50),
        new("construction", QuestionKnowledgeGroup.Geometry, "lát nền công trình", "floor tiling", "một tổ lát được", "a team tiles", "m² nền", "m² of flooring", "m²", 200),
        new("water", QuestionKnowledgeGroup.Measurement, "tiết kiệm nước tưới", "irrigation water savings", "một khu vườn tiết kiệm", "a garden saves", "lít nước", "litres of water", "litres", 1000),
        new("schedule", QuestionKnowledgeGroup.Time, "lịch sinh hoạt", "daily schedules", "một nhóm luyện tập trong", "a group practises for", "phút", "minutes", "minutes", 120),
        new("survey", QuestionKnowledgeGroup.Data, "khảo sát sở thích", "preference surveys", "một nhóm thu được", "a group collects", "phiếu trả lời", "responses", "responses", 500)
    ]);

    public static QuizStoryContext Find(string id) => All.Single(context => context.Id == id);
    public static IEnumerable<QuizStoryContext> AverageContexts => All.Where(c => c.Id is not ("family-age" or "classroom"));

    public static string PluralPeriod(string period) => period == "match" ? "matches" : period + "s";
}
