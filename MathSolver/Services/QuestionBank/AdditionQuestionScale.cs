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
            "school-reading" => ProductionPeriods(Scale(["pages"], stars switch {
                1 => "{person}", 2 => "nhóm đọc sách của {person}", 3 => "lớp của {person}",
                4 => "trường do {person} phụ trách", _ => "cụm trường do {person} phụ trách" },
                stars switch { 1 => "{person}", 2 => "{person}'s reading group", 3 => "{person}'s class",
                    4 => "{person}'s school", _ => "{person}'s school cluster" },
                "Tổng số trang đã đọc của người, nhóm hoặc đơn vị trường học qua các thời kỳ riêng.",
                "Pages read by a person, group or school organisation over separate periods.")),
            "school-furniture" => Scale(["desks", "chairs"], stars switch {
                1 => "phòng học do {person} phụ trách", 2 => "dãy phòng học do {person} phụ trách", 3 => "trường do {person} phụ trách",
                4 => "kho thiết bị trường học của {person}", _ => "kho phân phối thiết bị trường học của {person}" },
                stars switch { 1 => "{person}'s classroom", 2 => "{person}'s classroom block", 3 => "{person}'s school",
                    4 => "{person}'s school furniture warehouse", _ => "{person}'s school furniture distribution centre" },
                "Đếm bàn hoặc ghế cùng loại; lượng lớn thuộc kho thiết bị, không phải một lớp học.",
                "Count desks or chairs of the same kind; large quantities belong to furniture warehouses, not one class."),
            "food-supplies" => Scale(stars <= 2 ? ["apples", "oranges", "cakes", "milk-cartons"]
                : ["apples", "oranges", "cakes", "milk-cartons", "rice-bags"], stars switch {
                1 => "giỏ thực phẩm của {person}", 2 => "quầy thực phẩm của {person}", 3 => "cửa hàng thực phẩm của {person}",
                4 => "kho thực phẩm của {person}", _ => "trung tâm phân phối thực phẩm của {person}" },
                stars switch { 1 => "{person}'s food basket", 2 => "{person}'s food stall", 3 => "{person}'s food shop",
                    4 => "{person}'s food warehouse", _ => "{person}'s food distribution centre" },
                "Đếm thực phẩm đóng gói hoặc quả cùng loại; không dùng khối lượng, giá hay tiền.",
                "Count packaged food or fruit of one kind; no mass, prices or money."),
            "bakery" => ProductionPeriods(Scale(["cakes", "bread-rolls"], stars switch {
                1 => "{person}", 2 => "gia đình {person}", 3 => "tiệm bánh của {person}",
                4 => "xưởng bánh của {person}", _ => "nhà máy bánh của {person}" },
                stars switch { 1 => "{person}", 2 => "{person}'s family", 3 => "{person}'s bakery",
                    4 => "{person}'s baking workshop", _ => "{person}'s baking factory" },
                "Bánh làm trong các buổi, ngày, tuần hoặc tháng riêng; lượng lớn thuộc xưởng hoặc nhà máy.",
                "Baking over separate selected periods; large quantities belong to a workshop or factory.")),
            "poultry" => Scale(["chickens", "ducks"], stars switch {
                1 => "chuồng gia cầm của {person}", 2 => "trại gia cầm của {person}", 3 => "trang trại gia cầm của {person}",
                4 => "cụm trang trại gia cầm của {person}", _ => "hợp tác xã chăn nuôi gia cầm của {person}" },
                stars switch { 1 => "{person}'s poultry pen", 2 => "{person}'s poultry farm", 3 => "{person}'s poultry farm",
                    4 => "{person}'s poultry farm network", _ => "{person}'s poultry cooperative" },
                "Đếm gà hoặc vịt cùng loại; quy mô từ chuồng nhỏ tới mạng lưới trang trại.",
                "Count chickens or ducks of one kind, from a small pen to a farm network."),
            "fish-farm" => Scale(["fish"], stars switch {
                1 => "bể cá của {person}", 2 => "ao cá của {person}", 3 => "trại cá của {person}",
                4 => "cơ sở nuôi cá của {person}", _ => "cơ sở ươm cá giống của {person}" },
                stars switch { 1 => "{person}'s aquarium", 2 => "{person}'s fish pond", 3 => "{person}'s fish farm",
                    4 => "{person}'s aquaculture farm", _ => "{person}'s fish hatchery" },
                "Đếm cá cùng loại; lượng lớn là cá giống tại cơ sở nuôi hoặc ươm, không phải bể cảnh.",
                "Count fish of one kind; large quantities are fish at an aquaculture farm or hatchery, not a home tank."),
            "cattle" => Scale(["cows"], stars switch {
                1 => "trại bò nhỏ của {person}", 2 => "trại bò của {person}", 3 => "trang trại bò lớn của {person}",
                4 => "mạng lưới trại bò của {person}", _ => "mạng lưới chăn nuôi toàn quốc do {person} phụ trách" },
                stars switch { 1 => "{person}'s small cattle farm", 2 => "{person}'s cattle farm", 3 => "{person}'s large cattle farm",
                    4 => "{person}'s cattle farm network", _ => "{person}'s national cattle farm network" },
                "Đếm bò; số hàng nghìn hoặc chục nghìn thuộc nhiều trại trong mạng lưới, không phải hộ gia đình.",
                "Count cattle; thousands or tens of thousands belong to a farm network, not one household."),
            "crop-harvest" => ProductionPeriods(Scale(stars == 1 ? ["mangoes"] : ["mangoes", "rice-sacks"], stars switch {
                1 => "{person}", 2 => "gia đình {person}", 3 => "nông trại của {person}",
                4 => "hợp tác xã do {person} phụ trách", _ => "liên hiệp nông nghiệp do {person} phụ trách" },
                stars switch { 1 => "{person}", 2 => "{person}'s family", 3 => "{person}'s farm",
                    4 => "{person}'s farming cooperative", _ => "{person}'s agricultural cooperative network" },
                "Đếm quả xoài hoặc bao lúa qua thời kỳ riêng; không đổi sang cân nặng hay diện tích.",
                "Count mangoes or rice sacks over separate periods; no mass or area conversion.")),
            "product-production" => ProductionPeriods(Scale(["products"], stars switch {
                1 => "{person}", 2 => "nhóm làm hàng của {person}", 3 => "xưởng của {person}",
                4 => "cơ sở sản xuất của {person}", _ => "nhà máy của {person}" },
                stars switch { 1 => "{person}", 2 => "{person}'s production group", 3 => "{person}'s workshop",
                    4 => "{person}'s manufacturer", _ => "{person}'s factory" },
                "Đếm sản phẩm cùng loại hoàn thành qua các thời kỳ riêng, không tính doanh thu.",
                "Count completed products of the same kind over separate periods, not revenue.")),
            "passenger-count" => ProductionPeriods(Scale(["passengers"], stars switch {
                1 => "bến xe nhỏ do {person} phụ trách", 2 => "bến xe do {person} phụ trách", 3 => "nhà ga do {person} phụ trách",
                4 => "đầu mối vận tải do {person} phụ trách", _ => "mạng lưới vận tải do {person} phụ trách" },
                stars switch { 1 => "{person}'s small bus stop", 2 => "{person}'s bus station", 3 => "{person}'s railway station",
                    4 => "{person}'s transport hub", _ => "{person}'s transport network" },
                "Đếm lượt khách được phục vụ qua các thời kỳ riêng; không phải số người có mặt cùng lúc.",
                "Count passenger visits over separate periods, not simultaneous occupancy.")),
            "vehicle-count" => ProductionPeriods(Scale(["vehicles"], stars switch {
                1 => "{person}", 2 => "nhóm đếm xe của {person}", 3 => "trạm đếm xe do {person} phụ trách",
                4 => "trạm thu phí do {person} phụ trách", _ => "mạng lưới trạm đếm xe do {person} phụ trách" },
                stars switch { 1 => "{person}", 2 => "{person}'s traffic-counting group", 3 => "{person}'s traffic counter",
                    4 => "{person}'s toll station", _ => "{person}'s traffic-counting network" },
                "Đếm lượt xe qua điểm quan sát trong các thời kỳ riêng, không tính quãng đường hay vận tốc.",
                "Count vehicle passages over separate periods, not distance or speed.")),
            "construction-stock" => Scale(["bricks", "tiles"], stars switch {
                1 => "góc vật tư của {person}", 2 => "kho vật tư sửa nhà của {person}", 3 => "kho vật tư công trình của {person}",
                4 => "bãi vật liệu của {person}", _ => "kho phân phối vật liệu của {person}" },
                stars switch { 1 => "{person}'s materials corner", 2 => "{person}'s home repair stock", 3 => "{person}'s construction site store",
                    4 => "{person}'s builders yard", _ => "{person}'s building materials distribution warehouse" },
                "Đếm gạch hoặc gạch lát cùng loại theo quy mô kho; không tính chiều dài, diện tích hay thể tích.",
                "Count bricks or tiles of one kind at an appropriately sized store; no length, area or volume."),
            "green-planting" => Scale(stars <= 2 ? ["trees", "seedlings"] : ["seedlings"], stars switch {
                1 => "nhóm của {person}", 2 => "đội trồng cây của {person}", 3 => "dự án trồng cây do {person} phụ trách",
                4 => "đơn vị trồng rừng của {person}", _ => "chương trình trồng rừng do {person} phụ trách" },
                stars switch { 1 => "{person}'s group", 2 => "{person}'s planting team", 3 => "{person}'s planting project",
                    4 => "{person}'s forestry crew", _ => "{person}'s reforestation programme" },
                "Kết quả trồng cây riêng của các nhóm trong đợt trồng; lượng lớn là cây con tại dự án trồng rừng.",
                "Separate groups' planting results in a campaign; large quantities are seedlings in forestry projects."),
            "survey-responses" => Scale(["responses"], stars switch {
                1 or 2 => "nhóm khảo sát của {person}", 3 => "đội khảo sát của {person}",
                4 => "đơn vị khảo sát của {person}", _ => "mạng lưới khảo sát do {person} phụ trách" },
                stars switch { 1 or 2 => "{person}'s survey group", 3 => "{person}'s survey team",
                    4 => "{person}'s survey organisation", _ => "{person}'s survey network" },
                "Đếm phiếu trả lời từ mẫu khảo sát riêng không trùng nhau; không tính trung bình hay phần trăm.",
                "Count responses from disjoint survey samples; no averages or percentages."),
            "trial-results" => ProductionPeriods(Scale(["coin-heads", "die-sixes"], stars switch {
                1 => "{person}", 2 => "nhóm thí nghiệm của {person}", 3 => "lớp của {person}",
                4 => "chương trình mô phỏng của {person}", _ => "hệ thống mô phỏng của {person}" },
                stars switch { 1 => "{person}", 2 => "{person}'s experiment group", 3 => "{person}'s class",
                    4 => "{person}'s simulation programme", _ => "{person}'s simulation system" },
                "Cộng số lần xuất hiện cùng một mặt qua các đợt thử riêng; lượng lớn là mô phỏng, không hỏi xác suất.",
                "Add occurrences of the same outcome in separate trial sessions; large counts are simulations, not a probability calculation.")),
            _ => null
        };
    }
}
