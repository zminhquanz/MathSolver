using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>
/// A countable-object setting at a particular curriculum tier. Limits include the
/// combined/initial/larger quantity, not only the two operands. Money and measures
/// deliberately have no profiles in this catalogue.
/// </summary>
public sealed record AdditionQuestionScale(
    int MaxOperand, int MaxCombinedQuantity, string[] UnitIds,
    string VietnameseActor, string EnglishActor,
    string VietnameseScope, string EnglishScope,
    string VietnamesePartA = "", string VietnamesePartB = "",
    string EnglishPartA = "", string EnglishPartB = "",
    string VietnameseSpan = "", string EnglishSpan = "")
{
    public string ActorPattern(AppLanguage language) => language == AppLanguage.Vietnamese ? VietnameseActor : EnglishActor;
    public string Scope(AppLanguage language) => language == AppLanguage.Vietnamese ? VietnameseScope : EnglishScope;
    public (string A, string B) Parts(AppLanguage language) => language == AppLanguage.Vietnamese
        ? (VietnamesePartA, VietnamesePartB) : (EnglishPartA, EnglishPartB);
    public string Span(AppLanguage language) => language == AppLanguage.Vietnamese ? VietnameseSpan : EnglishSpan;

    public string Actor(AppLanguage language, string person) => ActorPattern(language).Replace("{person}", person);

    public bool MatchesActor(AppLanguage language, string actor)
    {
        string[] pattern = ActorPattern(language).Split("{person}");
        if (actor.Length <= pattern[0].Length + pattern[1].Length
            || !actor.StartsWith(pattern[0], StringComparison.OrdinalIgnoreCase)
            || !actor.EndsWith(pattern[1], StringComparison.OrdinalIgnoreCase)) return false;
        string person = actor.Substring(pattern[0].Length, actor.Length - pattern[0].Length - pattern[1].Length);
        foreach (string title in new[] { "ông ", "bà ", "cha ", "mẹ ", "anh ", "chị ", "cô ", "dì ", "chú ", "bác ", "cậu ", "mợ ",
            "Grandma ", "Grandpa ", "Uncle ", "Aunt ", "Mr. ", "Ms. " })
            if (person.StartsWith(title, StringComparison.OrdinalIgnoreCase)) { person = person[title.Length..]; break; }
        // Check the bound owner/name, not substrings such as 'kho' in Khoa or
        // 'trường' in Trường. Imported proper names are not restricted to a pool.
        return person.Length > 0 && person.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(p => char.IsUpper(p[0]));
    }
}

public static class AdditionQuestionScales
{
    /// <summary>Null means the scene cannot supply a primary operand at this tier.</summary>
    public static AdditionQuestionScale? Find(string sceneId, CurriculumTier tier)
    {
        if (!Enum.IsDefined(tier)) return null;
        int stars = (int)tier;
        int cap = QuizCurriculumLayer.GetMaximumOperandValue(tier);
        AdditionQuestionScale Scale(string[] units, string viActor, string enActor, string viScope, string enScope,
            int? maximum = null, int? combined = null) => new(maximum ?? cap, combined ?? 2 * (maximum ?? cap),
                units, viActor, enActor, viScope, enScope);
        AdditionQuestionScale Periods(AdditionQuestionScale scale, string viA, string viB, string enA, string enB,
            string viSpan, string enSpan) => scale with {
                VietnamesePartA = viA, VietnamesePartB = viB, EnglishPartA = enA, EnglishPartB = enB,
                VietnameseSpan = viSpan, EnglishSpan = enSpan };
        AdditionQuestionScale ProductionPeriods(AdditionQuestionScale scale) => stars switch {
            <= 2 => Periods(scale, "buổi sáng", "buổi chiều", "the morning", "the afternoon", "các buổi", "these periods"),
            3 => Periods(scale, "ngày trước", "ngày sau", "the previous day", "the following day", "các ngày", "these days"),
            4 => Periods(scale, "tuần trước", "tuần này", "the previous week", "the current week", "các tuần", "these weeks"),
            _ => Periods(scale, "tháng trước", "tháng này", "the previous month", "the current month", "các tháng", "these months") };

        return sceneId switch
        {
            "family-gifts" when stars <= 2 => Scale(stars == 1
                ? ["books", "notebooks", "pencils", "candies", "cards", "stickers"]
                : ["pencils", "candies", "cards", "stickers"], "{person}", "{person}",
                "Đồ dùng hoặc bộ sưu tập nhỏ của cá nhân trong gia đình.", "A person's small belongings or collection at home."),
            "library" => Scale(["books"], stars switch {
                1 => "góc sách của {person}", 2 => "tủ sách của {person}", 3 => "thư viện trường do {person} phụ trách",
                4 => "thư viện huyện do {person} phụ trách", _ => "thư viện thành phố do {person} phụ trách" },
                stars switch { 1 => "{person}'s reading corner", 2 => "{person}'s bookcase", 3 => "{person}'s school library",
                    4 => "{person}'s district library", _ => "{person}'s city library" },
                "Sách được lưu giữ trong góc sách, tủ sách hoặc thư viện theo quy mô đã chọn.",
                "Books held in the selected reading corner, bookcase or library."),
            "school-supplies" => Scale(stars <= 2 ? ["books", "notebooks", "pencils", "cards"] : ["books", "notebooks", "pencils"],
                stars switch { 1 => "nhóm của {person}", 2 => "lớp của {person}", 3 => "trường do {person} phụ trách",
                    4 => "cụm trường do {person} phụ trách", _ => "mạng lưới trường do {person} phụ trách" },
                stars switch { 1 => "{person}'s group", 2 => "{person}'s class", 3 => "{person}'s school",
                    4 => "{person}'s school cluster", _ => "{person}'s school network" },
                "Các nhóm hoặc đơn vị trường học góp đồ dùng trong một đợt; phần góp riêng không chứa nhau.",
                "Distinct groups or educational organisations contribute supplies during a campaign; their contributions do not overlap."),
            "donations" => Scale(stars <= 3 ? ["books", "notebooks", "pencils", "cakes"] : ["books", "notebooks", "pencils"],
                stars switch { 1 or 2 => "nhóm của {person}", 3 => "trường do {person} phụ trách",
                    4 => "đoàn thiện nguyện do {person} phụ trách", _ => "tổ chức cứu trợ do {person} phụ trách" },
                stars switch { 1 or 2 => "{person}'s group", 3 => "{person}'s school",
                    4 => "{person}'s volunteer organisation", _ => "{person}'s aid organisation" },
                "Phần quyên góp riêng của các chủ thể trong đợt vận động phù hợp quy mô.",
                "Separate contributions by the selected actors in a campaign of the appropriate scale."),
            "recycling" => Scale(["plastic-bottles", "cans"], stars switch { 1 or 2 => "nhóm của {person}",
                3 => "trường do {person} phụ trách", 4 => "đội thu gom của {person}", _ => "đơn vị tái chế của {person}" },
                stars switch { 1 or 2 => "{person}'s group", 3 => "{person}'s school", 4 => "{person}'s collection crew",
                    _ => "{person}'s recycling organisation" },
                "Thu gom trong một đợt tại các nhóm hoặc khu vực riêng; lượng lớn thuộc đội chuyên trách hoặc đơn vị tái chế.",
                "A collection campaign by distinct groups or areas; large quantities belong to a collection crew or recycling organisation."),
            "craft" when stars <= 3 => ProductionPeriods(Scale(["cards", "paper-flowers"],
                stars switch { 1 => "{person}", 2 => "nhóm thủ công của {person}", _ => "xưởng thủ công của {person}" },
                stars switch { 1 => "{person}", 2 => "{person}'s craft group", _ => "{person}'s craft workshop" },
                "Hoạt động thủ công cá nhân, nhóm hoặc xưởng nhỏ trong các thời kỳ riêng đã chọn.",
                "Craft work by a person, group or small workshop over the selected separate periods.")),
            "notebook-production" when stars >= 3 => ProductionPeriods(Scale(["notebooks"],
                stars switch { 3 => "xưởng làm vở của {person}", 4 => "cơ sở sản xuất vở của {person}", _ => "nhà máy sản xuất vở của {person}" },
                stars switch { 3 => "{person}'s notebook workshop", 4 => "{person}'s notebook manufacturer", _ => "{person}'s notebook factory" },
                "Sản xuất vở trong các ngày, tuần hoặc tháng riêng; không phải một học sinh tự làm.",
                "Notebook production over separate days, weeks or months, not a pupil making them individually.")),
            "book-distribution" when stars >= 3 => Scale(["books", "notebooks"],
                stars switch { 3 => "cửa hàng sách của {person}", 4 => "kho sách của {person}", _ => "kho phân phối sách của {person}" },
                stars switch { 3 => "{person}'s bookshop", 4 => "{person}'s book warehouse", _ => "{person}'s book distribution centre" },
                "Hàng tồn hoặc hàng xuất của cửa hàng, kho hoặc trung tâm phân phối sách.",
                "Inventory or dispatches at a bookshop, warehouse or book distribution centre."),
            "harvest" => ProductionPeriods(Scale(stars <= 3 ? ["apples", "oranges", "flowers"] : ["apples", "oranges"],
                stars switch { 1 => "{person}", 2 => "gia đình {person}", 3 => "nông trại của {person}",
                    4 => "nông trại của {person}", _ => "hợp tác xã do {person} phụ trách" },
                stars switch { 1 => "{person}", 2 => "{person}'s family", 3 or 4 => "{person}'s farm", _ => "{person}'s farming cooperative" },
                "Thu hoạch của cá nhân, gia đình, nông trại hoặc hợp tác xã trong các thời kỳ riêng.",
                "Harvests by a person, family, farm or cooperative over separate periods.")),
            "garden" => Periods(Scale(stars <= 2 ? ["trees", "seedlings", "flowers"] : ["seedlings"],
                stars <= 2 ? "vườn của {person}" : stars == 3 ? "vườn ươm của {person}" : "cơ sở ươm cây của {person}",
                stars <= 2 ? "{person}'s garden" : stars == 3 ? "{person}'s nursery" : "{person}'s seedling nursery",
                "Đếm cây ở các luống hoặc khu riêng không chứa nhau; lượng lớn là cây con tại cơ sở ươm cây.",
                "Count plants in disjoint rows or sections; large quantities are seedlings at a nursery.",
                maximum: stars == 2 ? 50 : cap),
                stars <= 3 ? "luống bên trái" : "khu bên trái", stars <= 3 ? "luống bên phải" : "khu bên phải",
                stars <= 3 ? "the left row" : "the left section", stars <= 3 ? "the right row" : "the right section",
                stars <= 3 ? "các luống" : "các khu", stars <= 3 ? "these rows" : "these sections"),
            "sports" when stars <= 2 => Periods(Scale(["points"], "đội của {person}", "{person}'s team",
                "Điểm của một đội qua các hiệp trong một trận đấu.", "A team's points over separate halves of a game.",
                maximum: Math.Min(cap, 45)), "hiệp đầu", "hiệp sau", "the first half", "the second half", "các hiệp", "these halves"),
            "birds-arrive" when stars <= 2 => Scale(["birds"], "công viên {person}", "{person} Park",
                "Chim có mặt rồi bay đến cùng địa điểm; không đếm lại những con đã có.",
                "Birds initially present and other birds arriving at the same place, without counting existing birds again."),
            "club-arrivals" when stars <= 2 => Scale(["students"], "câu lạc bộ của {person}", "{person}'s club",
                "Câu lạc bộ nhỏ có người đang tham gia rồi người khác đến; tổng không vượt sức chứa.",
                "A small club's existing participants and new arrivals; the total stays within its capacity.",
                maximum: Math.Min(cap, 20), combined: 30),
            "shop-stock" => Scale(stars <= 3 ? ["apples", "oranges", "cakes", "balls", "pencils"] : ["apples", "oranges", "pencils"],
                stars switch { 1 or 2 => "quầy hàng của {person}", 3 => "cửa hàng của {person}",
                    4 => "cửa hàng bán sỉ của {person}", _ => "kho phân phối của {person}" },
                stars switch { 1 or 2 => "{person}'s stall", 3 => "{person}'s shop", 4 => "{person}'s wholesale shop", _ => "{person}'s distribution warehouse" },
                "Đếm hàng hóa theo quy mô quầy hàng, cửa hàng hoặc kho; không tính tiền hay đổi đơn vị.",
                "Count stock at a stall, shop or warehouse of the selected size; no money or unit conversion."),
            _ => null
        };
    }
}
