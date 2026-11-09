using System.Text;

namespace MathSolver.Services;

internal sealed record StoryArtworkActor(string Name, string? ArtworkId);

/// <summary>Decorative context only. Never infer a picture from AI prose or numeric answers.</summary>
internal static class QuizStoryArtwork
{
    private sealed record ObjectArtwork(string Id, string Asset, IReadOnlyList<string> Units);
    // Exact authored nouns, including QuestionUnits' VI/singular/plural forms. No prose parsing.
    private static readonly ObjectArtwork[] Objects =
    [
        new("erasers", "eraser", ["cục gôm", "cục tẩy", "eraser", "erasers"]),
        new("footwear-pairs", "footwear", ["đôi giày dép", "đôi giày", "đôi dép", "pair of shoes or sandals", "pairs of shoes or sandals"]),
        new("clothing-sets", "clothing", ["bộ quần áo", "set of clothes", "sets of clothes"]),
        new("toothbrushes", "toothbrush", ["bàn chải đánh răng", "toothbrush", "toothbrushes"]),
        new("combs", "comb", ["chiếc lược", "lược", "comb", "combs"]),
        new("serving-bowls", "serving_bowl", ["cái tô", "tô", "serving bowl", "serving bowls"]),
        new("rice-bowls", "rice_bowl", ["cái chén", "chén", "rice bowl", "rice bowls"]),
        new("plates", "plate", ["cái đĩa", "đĩa", "plate", "plates"]),
        new("spoons", "spoon", ["cái muỗng", "muỗng", "spoon", "spoons"]),
        new("forks", "fork", ["cái nĩa", "nĩa", "fork", "forks"]),
        new("chopstick-pairs", "chopsticks", ["đôi đũa", "pair of chopsticks", "pairs of chopsticks"]),
        new("drinking-glasses", "drinking_glass", ["cái ly", "ly", "drinking glass", "drinking glasses"]),
        new("books", "books", ["sách", "quyển sách", "cuốn sách", "book", "books"]),
        new("notebooks", "notebook", ["vở", "quyển vở", "cuốn vở", "notebook", "notebooks"]),
        new("pencils", "pencil", ["bút chì", "cây bút chì", "pencil", "pencils"]),
        new("pens", "pen", ["bút", "cây bút", "chiếc bút", "pen", "pens"]),
        new("fountain-pens", "fountain_pen", ["bút mực", "cây bút mực", "fountain pen", "fountain pens"]),
        new("ink-bottles", "ink_bottle", ["bình mực", "lọ mực", "ink bottle", "ink bottles"]),
        new("pencil-cases", "pencil_case", ["hộp bút", "pencil case", "pencil cases"]),
        new("compasses", "compass", ["compa", "chiếc compa", "drawing compass", "drawing compasses"]),
        new("rulers", "ruler", ["thước kẻ", "cây thước kẻ", "ruler", "rulers"]),
        new("satchels", "satchel", ["cặp táp", "chiếc cặp táp", "school satchel", "school satchels"]),
        new("candies", "candy", ["kẹo", "viên kẹo", "sweet", "sweets", "candy", "candies"]),
        new("apples", "apple", ["quả táo", "apple", "apples"]),
        new("oranges", "orange", ["quả cam", "orange", "oranges"]),
        new("flowers", "flower", ["bông hoa", "flower", "flowers"]),
        new("cards", "card", ["tấm thiệp", "card", "cards"]),
        new("balls", "ball", ["quả bóng", "ball", "balls"]),
        new("stickers", "sticker", ["nhãn dán", "sticker", "stickers"]),
        new("cakes", "cake", ["chiếc bánh", "cake", "cakes"]),
        new("gifts", "gift", ["gói quà", "hộp quà", "gift", "gifts", "gift package", "gift packages"]),
        new("plastic-bottles", "plastic_bottle", ["chai nhựa", "plastic bottle", "plastic bottles"]),
        new("cans", "can", ["lon", "can", "cans"]),
        new("recycled-paper", "recycled_paper", ["kg giấy", "giấy thu gom", "collected paper", "kg of paper"]),
        new("trees", "tree", ["cây", "tree", "trees"]),
        new("seedlings", "seedling", ["cây con", "cây giống", "seedling", "seedlings"]),
        new("desks", "desk", ["chiếc bàn", "desk", "desks"]),
        new("chairs", "chair", ["chiếc ghế", "cái ghế", "chair", "chairs"]),
        new("students", "student", ["bạn", "học sinh", "student", "students"]),
        new("paper-flowers", "paper_flower", ["bông hoa giấy", "paper flower", "paper flowers"]),
        new("responses", "response_sheet", ["phiếu trả lời", "survey response", "survey responses", "response", "responses"]),
        new("milk-cartons", "milk_carton", ["hộp sữa", "milk carton", "milk cartons"]),
        new("rice-bags", "rice_bag", ["túi gạo", "rice bag", "rice bags"]),
        new("rice-sacks", "paddy_sack", ["bao lúa", "rice sack", "rice sacks"]),
        new("bread-rolls", "bread", ["chiếc bánh mì", "ổ bánh mì", "bread roll", "bread rolls"]),
        new("sandwich-bread", "bread_sandwich", ["ổ bánh mì sandwich", "sandwich bread loaf", "sandwich bread loaves"]),
        new("round-bread", "bread", ["ổ bánh mì tròn giòn", "crusty round bread roll", "crusty round bread rolls"]),
        new("banh-mi", "bread_banh_mi", ["ổ bánh mì thịt việt nam", "vietnamese meat-filled banh mi sandwich", "vietnamese meat-filled banh mi sandwiches"]),
        new("mangoes", "mango", ["quả xoài", "mango", "mangoes"]),
        new("eggs", "egg", ["quả trứng", "egg", "eggs"]),
        new("meals", "meal", ["suất ăn", "meal", "meals"]),
        new("marbles", "marble", ["viên bi", "marble", "marbles"]),
        new("tickets", "ticket", ["vé", "ticket", "tickets"]),
        new("seeds", "seed", ["hạt giống", "seed", "seeds"]),
        new("shirts", "shirt", ["chiếc áo", "shirt", "shirts"]),
        new("backpacks", "backpack", ["ba lô", "backpack", "backpacks"]),
        new("birds", "bird", ["con chim", "bird", "birds"]),
        new("chickens", "chicken", ["con gà", "chicken", "chickens"]),
        new("ducks", "duck", ["con vịt", "duck", "ducks"]),
        new("fish", "fish", ["con cá", "fish"]),
        new("cows", "cow", ["con bò", "cow", "cows"]),
        new("fabric", "fabric", ["vải", "mét vải", "fabric", "meters of fabric", "metres of fabric"]),
        new("ribbon", "ribbon", ["ruy băng", "m ruy băng", "ribbon", "m of ribbon"]),
        new("water", "water", ["nước", "lít nước", "water", "liters of water", "litres of water"]),
        new("flour", "flour", ["bột mì", "flour"]),
        new("vegetables", "vegetables", ["rau", "rau thu hoạch", "kg rau", "vegetables", "harvested vegetables", "kg of vegetables"]),
        new("honey", "honey", ["mật ong", "lít mật ong", "honey", "liters of honey", "litres of honey"]),
        new("oil", "oil", ["dầu", "lít dầu", "oil", "liters of oil", "litres of oil"]),
        new("bricks", "brick", ["viên gạch", "brick", "bricks"]),
        new("tiles", "tile", ["viên gạch lát", "tile", "tiles"]),
        new("printers", "printer", ["máy in", "printer", "printers"]),
        new("workers", "worker", ["công nhân", "worker", "workers"]),
        new("craftspeople", "craftsperson", ["thợ", "builder", "builders", "craftsperson", "tradesperson"]),
        new("liquid-milk", "milk_liquid", ["sữa", "milk"]),
        new("orange-juice", "orange_juice", ["nước cam", "orange juice"]),
        new("loose-rice", "rice_grains", ["gạo", "rice"]),
        new("animal-feed", "animal_feed", ["thức ăn chăn nuôi", "animal feed"]),
        new("soft-cord", "cord", ["dây trang trí", "dây buộc quà", "dây treo bảng", "cord", "rope"]),
        new("parcels", "parcel", ["kiện hàng", "parcel", "parcels"]),
        new("theme-sandwich-bread", "bread_sandwich", ["ổ bánh mì sandwich", "sandwich bread loaf", "sandwich bread loaves"]),
        new("theme-round-bread", "bread", ["ổ bánh mì tròn giòn", "crusty round bread roll", "crusty round bread rolls"]),
        new("theme-banh-mi", "bread_banh_mi", ["ổ bánh mì thịt việt nam", "vietnamese meat-filled banh mi sandwich", "vietnamese meat-filled banh mi sandwiches"]),
        new("theme-cakes", "cake", ["chiếc bánh", "cake", "cakes"]),
        new("theme-eggs", "egg", ["quả trứng", "egg", "eggs"]),
        new("theme-candies", "candy", ["viên kẹo", "sweet", "sweets"]),
        new("theme-pho", "pho", ["tô phở", "bowl of pho", "bowls of pho"]),
        new("theme-lamingtons", "lamington", ["chiếc bánh lamington", "lamington", "lamingtons"]),
        new("theme-fish-chips", "fish_chips", ["suất cá chiên và khoai tây", "fish and chips meal", "fish and chips meals"]),
        new("theme-hamburgers", "hamburger", ["chiếc hamburger", "hamburger", "hamburgers"]),
        new("theme-pancakes", "pancake", ["chiếc bánh pancake", "pancake", "pancakes"]),
        new("theme-drinking-water", "water", ["ly nước lọc", "glass of drinking water", "glasses of drinking water"]),
        new("theme-milk-glasses", "milk_liquid", ["ly sữa", "glass of milk", "glasses of milk"]),
        new("theme-milk-cartons", "milk_carton", ["hộp sữa", "milk carton", "milk cartons"]),
        new("theme-orange-juice-glasses", "orange_juice", ["ly nước cam", "glass of orange juice", "glasses of orange juice"]),
        new("theme-tea-cups", "tea", ["tách trà", "cup of tea", "cups of tea"]),
        new("theme-lemon-tea-glasses", "lemon_tea", ["ly trà chanh", "glass of lemon tea", "glasses of lemon tea"]),
        new("theme-milkshakes", "milkshake", ["ly sữa lắc", "glass of milkshake", "glasses of milkshake"]),
        new("theme-apples", "apple", ["quả táo", "apple", "apples"]),
        new("theme-oranges", "orange", ["quả cam", "orange", "oranges"]),
        new("theme-mangoes", "mango", ["quả xoài", "mango", "mangoes"]),
        new("theme-bananas", "banana", ["quả chuối", "banana", "bananas"]),
        new("theme-dragon-fruit", "dragon_fruit", ["quả thanh long", "dragon fruit", "dragon fruits"]),
        new("theme-strawberries", "strawberry", ["quả dâu tây", "strawberry", "strawberries"]),
        new("theme-pears", "pear", ["quả lê", "pear", "pears"]),
        new("horses", "horse", ["con ngựa", "horse", "horses"]),
        new("deer", "deer", ["con nai", "deer"]),
        new("dogs", "dog", ["con chó", "dog", "dogs"]),
        new("cats", "cat", ["con mèo", "cat", "cats"]),
        new("pigs", "pig", ["con heo", "con lợn", "pig", "pigs"]),
        new("rabbits", "rabbit", ["con thỏ", "rabbit", "rabbits"]),
        new("turtles", "turtle", ["con rùa", "turtle", "turtles"]),
        new("tortoises", "tortoise", ["con rùa cạn", "tortoise", "tortoises"]),
        new("geese", "goose", ["con ngỗng", "goose", "geese"]),
        new("sugar", "sugar", ["đường", "sugar"]),
        new("carrots", "carrot", ["cà rốt", "carrot", "carrots"]),
        new("potatoes", "potato", ["khoai tây", "potato", "potatoes"]),
        new("biscuits", "biscuit", ["bánh quy", "biscuit", "biscuits", "cookie", "cookies"]),
        new("jam", "jam", ["mứt", "jam"]),
        new("fencing-wire", "fencing_wire", ["dây hàng rào", "fencing wire"]),
        new("color-markers", "colored_markers", ["bút màu", "bút lông màu", "marker", "markers", "colour markers", "color markers"]),
        new("fish-sauce", "fish_sauce", ["nước mắm", "fish sauce", "bottle of fish sauce", "bottles of fish sauce"]),
        new("televisions", "television", ["ti vi", "chiếc ti vi", "television", "televisions"]),
        new("laptops", "laptop", ["máy tính xách tay", "laptop", "laptops"]),
        new("trucks", "truck", ["xe tải", "truck", "trucks"]),
        new("taps", "tap", ["vòi nước", "tap", "taps", "faucet", "faucets"]),
        new("pumps", "pump", ["máy bơm", "water pump", "water pumps"]),
        new("paint-cans", "paint_can", ["thùng sơn", "paint can", "paint cans"]),
        new("sand", "sand", ["cát", "sand"])
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
        string[] matches = Objects.Where(item => item.Units.Any(knownUnits.Contains)).Select(item => item.Asset).Distinct().ToArray();
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
