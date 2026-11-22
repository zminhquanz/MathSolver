using System.Text;
using MathSolver.Services.QuestionBank;

namespace MathSolver.Services;

internal sealed record StoryArtworkActor(string Name, string? ArtworkId);

/// <summary>Decorative context only. Never infer a picture from AI prose or numeric answers.</summary>
internal static class QuizStoryArtwork
{
    private sealed record ObjectArtwork(string Id, string Asset, IReadOnlyList<string> Units);
    // Exact authored nouns, including QuestionUnits' VI/singular/plural forms. No prose parsing.
    private static readonly ObjectArtwork[] Objects =
    [
        new("paracetamol-boxes", "medical_paracetamol_box", (string[])["hộp thuốc paracetamol", "box of paracetamol", "boxes of paracetamol"]),
        new("vitamin-c-boxes", "medical_vitamin_c_box", (string[])["hộp vitamin C", "box of vitamin C", "boxes of vitamin C"]),
        new("cough-syrup-bottles", "medical_cough_syrup_bottle", (string[])["chai siro ho", "bottle of cough syrup", "bottles of cough syrup"]),
        new("medical-bandage-rolls", "medical_bandage_roll", (string[])["cuộn băng y tế", "roll of medical bandage", "rolls of medical bandage"]),
        new("medical-gauze-packs", "medical_gauze_pack", (string[])["gói gạc y tế", "pack of medical gauze", "packs of medical gauze"]),
        new("erasers", "eraser", (string[])["cục gôm", "cục tẩy", "eraser", "erasers"]),
        new("footwear-pairs", "footwear", (string[])["đôi giày dép", "đôi giày", "đôi dép", "pair of shoes or sandals", "pairs of shoes or sandals"]),
        new("clothing-sets", "clothing", (string[])["bộ quần áo", "set of clothes", "sets of clothes"]),
        new("toothbrushes", "toothbrush", (string[])["bàn chải đánh răng", "toothbrush", "toothbrushes"]),
        new("combs", "comb", (string[])["chiếc lược", "lược", "comb", "combs"]),
        new("serving-bowls", "serving_bowl", (string[])["cái tô", "tô", "serving bowl", "serving bowls"]),
        new("rice-bowls", "rice_bowl", (string[])["cái chén", "chén", "rice bowl", "rice bowls"]),
        new("plates", "plate", (string[])["cái đĩa", "đĩa", "plate", "plates"]),
        new("spoons", "spoon", (string[])["cái muỗng", "muỗng", "spoon", "spoons"]),
        new("forks", "fork", (string[])["cái nĩa", "nĩa", "fork", "forks"]),
        new("chopstick-pairs", "chopsticks", (string[])["đôi đũa", "pair of chopsticks", "pairs of chopsticks"]),
        new("drinking-glasses", "drinking_glass", (string[])["cái ly", "ly", "drinking glass", "drinking glasses"]),
        new("books", "books", (string[])["sách", "quyển sách", "cuốn sách", "book", "books"]),
        new("notebooks", "notebook", (string[])["vở", "quyển vở", "cuốn vở", "notebook", "notebooks"]),
        new("pencils", "pencil", (string[])["bút chì", "cây bút chì", "pencil", "pencils"]),
        new("pens", "pen", (string[])["bút", "cây bút", "chiếc bút", "pen", "pens"]),
        new("fountain-pens", "fountain_pen", (string[])["bút mực", "cây bút mực", "fountain pen", "fountain pens"]),
        new("ink-bottles", "ink_bottle", (string[])["bình mực", "lọ mực", "ink bottle", "ink bottles"]),
        new("pencil-cases", "pencil_case", (string[])["hộp bút", "pencil case", "pencil cases"]),
        new("compasses", "compass", (string[])["compa", "chiếc compa", "drawing compass", "drawing compasses"]),
        new("rulers", "ruler", (string[])["thước kẻ", "cây thước kẻ", "ruler", "rulers"]),
        new("satchels", "satchel", (string[])["cặp táp", "chiếc cặp táp", "school satchel", "school satchels"]),
        new("pages", "reading_page", (string[])["trang sách", "trang", "page", "pages"]),
        new("chalk-sticks", "chalk_stick", (string[])["viên phấn", "phấn viết bảng", "piece of chalk", "pieces of chalk"]),
        new("drawing-sheets", "drawing_sheet", (string[])["tờ giấy vẽ", "giấy vẽ", "drawing sheet", "drawing sheets"]),
        new("crayons", "crayon", (string[])["bút sáp màu", "cây bút sáp màu", "crayon", "crayons"]),
        new("glue-sticks", "glue_stick", (string[])["thỏi keo khô", "keo khô", "glue stick", "glue sticks"]),
        new("scissors", "scissors", (string[])["chiếc kéo", "kéo thủ công", "pair of scissors", "pairs of scissors"]),
        new("calculators", "calculator", (string[])["máy tính cầm tay", "calculator", "calculators"]),
        new("bookmarks", "bookmark", (string[])["thẻ đánh dấu trang", "dấu trang", "bookmark", "bookmarks"]),
        new("felt-tip-pens", "colored_markers", (string[])["bút dạ màu", "bút lông màu", "felt-tip pen", "felt-tip pens"]),
        new("school-bags", "satchel", (string[])["cặp sách", "cặp đi học", "school bag", "school bags"]),
        new("school-desks", "desk", (string[])["bàn học", "bàn học sinh", "school desk", "school desks"]),
        new("candies", "candy", (string[])["kẹo", "viên kẹo", "sweet", "sweets", "candy", "candies"]),
        new("apples", "apple", (string[])["quả táo", "apple", "apples"]),
        new("oranges", "orange", (string[])["quả cam", "orange", "oranges"]),
        new("flowers", "flower", (string[])["bông hoa", "flower", "flowers"]),
        new("cards", "card", (string[])["tấm thiệp", "card", "cards"]),
        .. ToyThemeCatalogue.Subjects.Select(subject => new ObjectArtwork("theme-" + subject.Unit.Id,
            subject.ArtworkId, (string[])[subject.Unit.Vietnamese, subject.Unit.Singular, subject.Unit.Plural])),
        new("balls", "ball", (string[])["quả bóng", "ball", "balls"]),
        new("stickers", "sticker", (string[])["nhãn dán", "sticker", "stickers"]),
        new("cakes", "cake", (string[])["chiếc bánh kem", "cream cake", "cream cakes", "chiếc bánh", "cake", "cakes"]),
        new("gifts", "gift", (string[])["gói quà", "hộp quà", "gift", "gifts", "gift package", "gift packages"]),
        new("plastic-bottles", "plastic_bottle", (string[])["chai nhựa", "plastic bottle", "plastic bottles"]),
        new("cans", "can", (string[])["lon", "can", "cans"]),
        new("recycled-paper", "recycled_paper", (string[])["kg giấy", "giấy thu gom", "collected paper", "kg of paper"]),
        new("trees", "tree", (string[])["cây", "tree", "trees"]),
        new("seedlings", "seedling", (string[])["cây con", "cây giống", "seedling", "seedlings"]),
        new("desks", "desk", (string[])["chiếc bàn", "desk", "desks"]),
        new("chairs", "chair", (string[])["chiếc ghế", "cái ghế", "chair", "chairs"]),
        new("students", "student", (string[])["bạn", "học sinh", "student", "students"]),
        new("paper-flowers", "paper_flower", (string[])["bông hoa giấy", "paper flower", "paper flowers"]),
        new("responses", "response_sheet", (string[])["phiếu trả lời", "survey response", "survey responses", "response", "responses"]),
        new("milk-cartons", "milk_carton", (string[])["hộp sữa", "milk carton", "milk cartons"]),
        new("rice-bags", "rice_bag", (string[])["túi gạo", "rice bag", "rice bags"]),
        new("rice-sacks", "paddy_sack", (string[])["bao lúa", "rice sack", "rice sacks"]),
        new("bread-rolls", "bread", (string[])["chiếc bánh mì", "ổ bánh mì", "bread roll", "bread rolls"]),
        new("sandwich-bread", "bread_sandwich", (string[])["ổ bánh mì sandwich", "sandwich bread loaf", "sandwich bread loaves"]),
        new("round-bread", "bread", (string[])["ổ bánh mì tròn giòn", "crusty round bread roll", "crusty round bread rolls"]),
        new("banh-mi", "bread_banh_mi", (string[])["ổ bánh mì thịt việt nam", "vietnamese meat-filled banh mi sandwich", "vietnamese meat-filled banh mi sandwiches"]),
        new("mangoes", "mango", (string[])["quả xoài", "mango", "mangoes"]),
        new("eggs", "egg", (string[])["quả trứng", "egg", "eggs"]),
        new("meals", "meal", (string[])["suất ăn", "meal", "meals"]),
        new("marbles", "marble", (string[])["viên bi", "marble", "marbles"]),
        new("tickets", "ticket", (string[])["vé", "ticket", "tickets"]),
        new("seeds", "seed", (string[])["hạt giống", "seed", "seeds"]),
        new("shirts", "shirt", (string[])["chiếc áo", "shirt", "shirts"]),
        new("backpacks", "backpack", (string[])["ba lô", "backpack", "backpacks"]),
        new("birds", "bird", (string[])["con chim", "bird", "birds"]),
        .. PetAnimalThemeCatalogue.Subjects.Select(subject => new ObjectArtwork(
            subject.Unit.Id, QuizStoryArtworkCatalogue.PetArtwork(subject.Unit.Id)!,
            (string[])[subject.Unit.Vietnamese, subject.Unit.Singular, subject.Unit.Plural])),
        new("chickens", "chicken", (string[])["con gà", "chicken", "chickens"]),
        new("ducks", "duck", (string[])["con vịt", "duck", "ducks"]),
        new("fish", "fish", (string[])["con cá", "fish"]),
        new("cows", "cow", (string[])["con bò", "cow", "cows"]),
        new("fabric", "fabric", (string[])["vải", "mét vải", "fabric", "meters of fabric", "metres of fabric"]),
        new("ribbon", "ribbon", (string[])["ruy băng", "m ruy băng", "ribbon", "m of ribbon"]),
        new("water", "water", (string[])["nước", "lít nước", "lít nước lọc", "water", "liters of water", "litres of water", "litres of drinking water"]),
        new("water-bottles", "water_bottle", (string[])["chai nước", "chai nước lọc", "chai nước suối", "bottle of water", "bottles of water", "bottle of drinking water", "bottles of drinking water"]),
        new("flour", "flour", (string[])["bột mì", "flour"]),
        new("vegetables", "vegetables", (string[])["rau", "rau thu hoạch", "kg rau", "vegetables", "harvested vegetables", "kg of vegetables"]),
        new("honey", "honey", (string[])["mật ong", "lít mật ong", "honey", "liters of honey", "litres of honey"]),
        new("oil", "oil", (string[])["dầu", "lít dầu", "oil", "liters of oil", "litres of oil"]),
        new("bricks", "brick", (string[])["viên gạch", "brick", "bricks"]),
        new("tiles", "tile", (string[])["viên gạch lát", "tile", "tiles"]),
        new("printers", "printer", (string[])["máy in", "printer", "printers"]),
        new("workers", "worker", (string[])["công nhân", "worker", "workers"]),
        new("craftspeople", "craftsperson", (string[])["thợ", "builder", "builders", "craftsperson", "tradesperson"]),
        new("liquid-milk", "milk_liquid", (string[])["sữa", "milk"]),
        new("orange-juice", "orange_juice", (string[])["nước cam", "orange juice"]),
        new("loose-rice", "rice_grains", (string[])["gạo", "rice"]),
        new("animal-feed", "animal_feed", (string[])["thức ăn chăn nuôi", "animal feed"]),
        new("soft-cord", "cord", (string[])["dây trang trí", "dây buộc quà", "dây treo bảng", "cord", "rope"]),
        new("parcels", "parcel", (string[])["kiện hàng", "parcel", "parcels"]),
        new("theme-sandwich-bread", "bread_sandwich", (string[])["ổ bánh mì sandwich", "sandwich bread loaf", "sandwich bread loaves"]),
        new("theme-round-bread", "bread", (string[])["ổ bánh mì tròn giòn", "crusty round bread roll", "crusty round bread rolls"]),
        new("theme-banh-mi", "bread_banh_mi", (string[])["ổ bánh mì thịt việt nam", "vietnamese meat-filled banh mi sandwich", "vietnamese meat-filled banh mi sandwiches"]),
        new("theme-cakes", "cake", (string[])["chiếc bánh kem", "cream cake", "cream cakes", "chiếc bánh", "cake", "cakes"]),
        new("theme-banh-cam", "banh_cam", (string[])["chiếc bánh cam", "Vietnamese sesame ball", "Vietnamese sesame balls"]),
        new("theme-banh-com", "banh_com", (string[])["chiếc bánh cốm", "green rice cake", "green rice cakes"]),
        new("theme-banh-trung-thu", "banh_trung_thu", (string[])["chiếc bánh trung thu", "mooncake", "mooncakes"]),
        new("theme-banh-pia", "banh_pia", (string[])["chiếc bánh pía", "pia pastry", "pia pastries"]),
        new("theme-croissants", "croissant", (string[])["chiếc bánh croissant", "croissant", "croissants"]),
        new("theme-banh-da-lon", "banh_da_lon", (string[])["miếng bánh da lợn", "layered steamed cake slice", "layered steamed cake slices"]),
        new("theme-banh-bo", "banh_bo", (string[])["chiếc bánh bò", "Vietnamese honeycomb cake", "Vietnamese honeycomb cakes"]),
        new("theme-muffins", "muffin", (string[])["chiếc bánh muffin", "muffin", "muffins"]),
        new("theme-cupcakes", "cupcake", (string[])["chiếc bánh cupcake", "cupcake", "cupcakes"]),
        new("theme-cookies", "cookie", (string[])["chiếc bánh cookie", "cookie", "cookies"]),
        new("theme-donuts", "donut", (string[])["chiếc bánh donut", "donut", "donuts"]),
        new("theme-waffles", "waffle", (string[])["chiếc bánh waffle", "waffle", "waffles"]),
        new("theme-apple-pies", "apple_pie", (string[])["chiếc bánh táo", "apple pie", "apple pies"]),
        new("theme-flans", "flan", (string[])["chiếc bánh flan", "crème caramel", "crème caramels"]),
        new("theme-eggs", "egg", (string[])["quả trứng", "egg", "eggs"]),
        new("theme-candies", "candy", (string[])["viên kẹo", "sweet", "sweets"]),
        new("theme-pho", "pho", (string[])["tô phở", "bowl of pho", "bowls of pho"]),
        new("theme-lamingtons", "lamington", (string[])["chiếc bánh lamington", "lamington", "lamingtons"]),
        new("theme-fish-chips", "fish_chips", (string[])["suất cá chiên và khoai tây", "fish and chips meal", "fish and chips meals"]),
        new("theme-hamburgers", "hamburger", (string[])["chiếc hamburger", "hamburger", "hamburgers"]),
        new("theme-pancakes", "pancake", (string[])["chiếc bánh pancake", "pancake", "pancakes"]),
        new("theme-com-tam", "com_tam", (string[])["suất cơm tấm", "serving of broken rice", "servings of broken rice"]),
        new("theme-op-la", "op_la", (string[])["phần trứng ốp-la", "serving of fried eggs", "servings of fried eggs"]),
        new("theme-thit-kho-trung", "thit_kho_trung", (string[])["phần thịt kho trứng", "serving of braised pork and eggs", "servings of braised pork and eggs"]),
        new("theme-banh-xeo", "banh_xeo", (string[])["chiếc bánh xèo", "Vietnamese sizzling pancake", "Vietnamese sizzling pancakes"]),
        .. MathSolver.Services.QuestionBank.FoodThemeCatalogue.NewRestaurantDishes.Select(d =>
            new ObjectArtwork("theme-" + d.Unit.Id, d.ArtworkId,
                (string[])[d.Unit.Vietnamese, d.Unit.Singular, d.Unit.Plural])),
        new("theme-bun-thit-nuong-cha-gio", "bun_thit_nuong_cha_gio", (string[])["tô bún thịt nướng chả giò", "bowl of grilled pork vermicelli with spring rolls", "bowls of grilled pork vermicelli with spring rolls"]),
        new("theme-hu-tieu-nam-vang", "hu_tieu_nam_vang", (string[])["tô hủ tiếu Nam Vang", "bowl of Nam Vang noodle soup", "bowls of Nam Vang noodle soup"]),
        new("theme-mi-quang", "mi_quang", (string[])["tô mì Quảng", "bowl of Quang noodles", "bowls of Quang noodles"]),
        new("theme-bun-bo-hue", "bun_bo_hue", (string[])["tô bún bò Huế", "bowl of Hue beef noodle soup", "bowls of Hue beef noodle soup"]),
        new("theme-banh-cuon", "banh_cuon", (string[])["đĩa bánh cuốn", "plate of steamed rice rolls", "plates of steamed rice rolls"]),
        new("theme-kimchi", "kimchi", (string[])["đĩa kim chi", "plate of kimchi", "plates of kimchi"]),
        new("theme-sushi", "sushi", (string[])["phần sushi", "serving of sushi", "servings of sushi"]),
        new("theme-drinking-water", "water", (string[])["ly nước lọc", "glass of drinking water", "glasses of drinking water"]),
        new("theme-milk-glasses", "milk_liquid", (string[])["ly sữa", "glass of milk", "glasses of milk"]),
        new("theme-milk-cartons", "milk_carton", (string[])["hộp sữa", "milk carton", "milk cartons"]),
        new("theme-orange-juice-glasses", "orange_juice", (string[])["ly nước cam", "glass of orange juice", "glasses of orange juice"]),
        new("theme-tea-cups", "tea", (string[])["tách trà", "cup of tea", "cups of tea"]),
        new("theme-lemon-tea-glasses", "lemon_tea", (string[])["ly trà chanh", "glass of lemon tea", "glasses of lemon tea"]),
        new("theme-milkshakes", "milkshake", (string[])["ly sữa lắc", "glass of milkshake", "glasses of milkshake"]),
        .. MathSolver.Services.QuestionBank.FoodThemeCatalogue.Subjects
            .Where(d => d.Group == MathSolver.Models.QuestionKnowledgeGroup.Drinks
                && d.Unit.Id is not ("drinking-water" or "milk-glasses" or "milk-cartons"
                    or "orange-juice-glasses" or "tea-cups" or "lemon-tea-glasses" or "milkshakes"))
            .Select(d => new ObjectArtwork("theme-" + d.Unit.Id, d.ArtworkId,
                (string[])[d.Unit.Vietnamese, d.Unit.Singular, d.Unit.Plural])),
        new("theme-apples", "apple", (string[])["quả táo", "apple", "apples"]),
        new("theme-oranges", "orange", (string[])["quả cam", "orange", "oranges"]),
        new("theme-mangoes", "mango", (string[])["quả xoài", "mango", "mangoes"]),
        new("theme-bananas", "banana", (string[])["quả chuối", "banana", "bananas"]),
        new("theme-dragon-fruit", "dragon_fruit", (string[])["quả thanh long", "dragon fruit", "dragon fruits"]),
        new("theme-strawberries", "strawberry", (string[])["quả dâu tây", "strawberry", "strawberries"]),
        new("theme-pears", "pear", (string[])["quả lê", "pear", "pears"]),
        .. MathSolver.Services.QuestionBank.FoodThemeCatalogue.Subjects
            .Where(d => d.Group == MathSolver.Models.QuestionKnowledgeGroup.Fruit
                && d.Unit.Id is not ("apples" or "oranges" or "mangoes" or "bananas"
                    or "dragon-fruit" or "strawberries" or "pears"))
            .Select(d => new ObjectArtwork("theme-" + d.Unit.Id, d.ArtworkId,
                (string[])[d.Unit.Vietnamese, d.Unit.Singular, d.Unit.Plural])),
        new("horses", "horse", (string[])["con ngựa", "horse", "horses"]),
        new("deer", "deer", (string[])["con nai", "deer"]),
        new("dogs", "dog", (string[])["con chó", "dog", "dogs"]),
        new("cats", "cat", (string[])["con mèo", "cat", "cats"]),
        new("pigs", "pig", (string[])["con heo", "con lợn", "pig", "pigs"]),
        new("rabbits", "rabbit", (string[])["con thỏ", "rabbit", "rabbits"]),
        new("turtles", "turtle", (string[])["con rùa", "turtle", "turtles"]),
        new("tortoises", "tortoise", (string[])["con rùa cạn", "tortoise", "tortoises"]),
        new("geese", "goose", (string[])["con ngỗng", "goose", "geese"]),
        new("sugar", "sugar", (string[])["đường", "sugar"]),
        new("carrots", "carrot", (string[])["cà rốt", "carrot", "carrots"]),
        new("potatoes", "potato", (string[])["khoai tây", "potato", "potatoes"]),
        new("biscuits", "biscuit", (string[])["bánh quy", "chiếc bánh quy", "biscuit", "biscuits"]),
        new("jam", "jam", (string[])["mứt", "jam"]),
        new("fencing-wire", "fencing_wire", (string[])["dây hàng rào", "fencing wire"]),
        new("color-markers", "colored_markers", (string[])["bút màu", "bút lông màu", "cây bút dạ quang", "colored marker", "colored markers", "marker", "markers", "colour markers", "color markers"]),
        new("fish-sauce", "fish_sauce", (string[])["nước mắm", "fish sauce", "bottle of fish sauce", "bottles of fish sauce"]),
        new("televisions", "television", (string[])["ti vi", "chiếc ti vi", "television", "televisions"]),
        new("laptops", "laptop", (string[])["máy tính xách tay", "laptop", "laptops"]),
        new("trucks", "truck", (string[])["xe tải", "truck", "trucks"]),
        new("taps", "tap", (string[])["vòi nước", "tap", "taps", "faucet", "faucets"]),
        new("pumps", "pump", (string[])["máy bơm", "water pump", "water pumps"]),
        new("paint-cans", "paint_can", (string[])["thùng sơn", "paint can", "paint cans"]),
        new("ballpoint-pens", "pen", (string[])["bút bi", "cây bút bi", "ballpoint pen", "ballpoint pens"]),
        new("cooking-oil-bottles", "oil", (string[])["chai dầu ăn", "bottle of cooking oil", "bottles of cooking oil"]),
        new("school-backpacks", "backpack", (string[])["ba lô học sinh", "chiếc ba lô học sinh", "school backpack", "school backpacks"]),
        new("child-bicycles", "bicycle", (string[])["xe đạp trẻ em", "chiếc xe đạp trẻ em", "children's bicycle", "children's bicycles"]),
        new("refrigerators", "refrigerator", (string[])["tủ lạnh", "chiếc tủ lạnh", "refrigerator", "refrigerators"]),
        new("mobile-phones", "mobile_phone", (string[])["điện thoại di động", "chiếc điện thoại di động", "mobile phone", "mobile phones"]),
        new("sand", "sand", (string[])["cát", "sand"])
    ];
    private static string Normalize(string text) => text.Normalize(NormalizationForm.FormC).Trim().ToLowerInvariant();
    private static string Asset(string id) => $"quiz_story_{id}.png";
    private static readonly string[] MotionAssets = ["car", "motorcycle", "bus", "train", "bicycle", "walking", "runner", "boat",
        "horse", "deer", "dog", "rabbit", "turtle", "tortoise", "truck"];

    internal static (double Width, double Height) Size(double panelWidth, bool compact)
    {
        double desired = compact ? 180 : 240;
        double width = double.IsFinite(panelWidth) && panelWidth > 0 ? Math.Min(desired, panelWidth) : desired;
        return (width, width * 2 / 3);
    }

    internal static string? Select(bool hasStory, bool hasFactTable, IEnumerable<string>? units, bool isMoney = false,
        string? itemUnit = null, string? artworkId = null)
    {
        if (!hasStory || hasFactTable) return null;
        if (isMoney) return "quiz_story_money.png";
        // Authored presentation metadata identifies subjects whose numeric unit is only kg/loaves/hours.
        if (artworkId is not null)
        {
            // Some authored Lists store a subject ID (animal-feed), while others store the PNG stem (animal_feed).
            // Resolve exact catalogue IDs before checking the allowed assets; never infer from prose.
            string asset = Objects.FirstOrDefault(item => item.Id == artworkId)?.Asset ?? artworkId;
            return asset == "money" || Objects.Any(item => item.Asset == asset) || MotionAssets.Contains(asset)
                ? Asset(asset) : null;
        }
        string[] knownUnits = units?.Select(Normalize).ToArray() ?? [];
        // Currency is the fallback when there is no explicitly authored subject (e.g. a backpack discount).
        if (knownUnits.Any(unit => unit is "đồng" or "dong" or "vnd" or "₫"))
            return "quiz_story_money.png";
        // An authoritative original item (not an answer in boxes/times) must never be replaced by a guess.
        if (itemUnit is not null) knownUnits = [Normalize(itemUnit)];
        string[] matches = Objects.Where(item => item.Units.Any(value => knownUnits.Contains(Normalize(value)))).Select(item => item.Asset).Distinct().ToArray();
        return matches.Length == 1 ? Asset(matches[0]) : null;
    }

    internal static string? SelectMotion(IReadOnlyList<string> actors, IReadOnlyList<StoryArtworkActor> catalogue, bool diagramVisible)
    {
        // Supporting diagrams and decorative context coexist; neither changes the other's data.
        if (actors.Count == 0) return null;
        var selected = new HashSet<string>(StringComparer.Ordinal);
        foreach (string actor in actors)
        {
            string name = Normalize(actor);
            string?[] ids = catalogue.Where(row => Normalize(row.Name) == name).Select(row => row.ArtworkId).Distinct().ToArray();
            if (ids.Length != 1 || ids[0] is null || !MotionAssets.Contains(ids[0]))
                return null;
            selected.Add(ids[0]!);
        }
        return selected.Count == 1 ? Asset(selected.Single()) : null;
    }
}
