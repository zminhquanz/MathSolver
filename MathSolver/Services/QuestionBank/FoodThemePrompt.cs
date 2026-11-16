using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

internal static class FoodThemePrompt
{
    private static readonly string[] PairedDishIds = ["bot-chien", "spaghetti", "pizzas", "mixed-salad", "hot-dogs", "udon"];
    private static readonly HashSet<string> KoreanDishIds = new(StringComparer.Ordinal)
    {
        "tteokbokki", "korean-corn-dog", "hotteok", "bungeoppang", "gyeranppang",
        "sundae-gukbap", "andong-jjimdak", "kalguksu", "squid-sundae"
    };
    // Specificity guard: keep 12 Vietnamese prepared dishes distinct in AI prose.
    // The full counted unit still comes from the deterministic C# contract.
    private static readonly IReadOnlyDictionary<string, (string Vi, string En)> VietnameseDishDescriptions =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["com-suon-cay"] = ("cơm trắng với sườn heo nướng nguyên cây", "rice with whole grilled pork ribs"),
            ["com-chien-hai-san"] = ("cơm chiên với hải sản như tôm, mực", "fried rice with seafood such as shrimp and squid"),
            ["com-chien-duong-chau"] = ("cơm chiên Dương Châu với trứng, rau củ và thịt", "Yangzhou fried rice with egg, vegetables and meat"),
            ["com-ga-xoi-mo"] = ("cơm với gà chiên da giòn bằng dầu nóng", "rice with crispy fried chicken"),
            ["com-ga-sot-me"] = ("cơm với gà phủ sốt me chua ngọt", "rice with chicken in sweet-and-sour tamarind sauce"),
            ["com-ga-sot-tieu-xanh"] = ("cơm với gà phủ sốt tiêu xanh", "rice with chicken in green peppercorn sauce"),
            ["canh-chua-ca-loc"] = ("canh chua có cá lóc và rau nấu canh chua", "sour soup with snakehead fish and vegetables"),
            ["canh-kho-qua-nhoi-thit"] = ("canh khổ qua cắt khúc nhồi thịt", "soup of bitter melon pieces stuffed with pork"),
            ["canh-bi-do-ham-xuong"] = ("canh bí đỏ hầm với xương heo", "pumpkin soup simmered with pork bones"),
            ["canh-cua-rau-day-mong-toi"] = ("canh cua đồng với cả rau đay và mồng tơi", "field crab soup with both jute mallow and Malabar spinach"),
            ["canh-rau-ngot-thit-bam"] = ("canh rau ngót nấu thịt heo băm", "sweet leaf soup with minced pork"),
            ["ca-loc-kho-to"] = ("cá lóc kho trong tộ đất với nước kho sánh", "snakehead fish braised in a clay pot with a reduced sauce")
        };

    internal static bool IsPairedFractionScene(string sceneId)
        => PairedDishIds.Any(id => sceneId == $"fraction-theme-food-{id}-total")
            || FoodThemeCatalogue.IsCheDessert(FoodThemeCatalogue.RestaurantDishForScene(sceneId)?.Unit.Id)
                && (sceneId.EndsWith("-total", StringComparison.Ordinal)
                    || sceneId.EndsWith("-remaining", StringComparison.Ordinal));

    internal static bool IsPairedDish(BasicQuestionContract c)
    {
        string sceneId = c.Version == FindXQuestionCatalogue.Version
            ? FindXQuestionCatalogue.Find(c.SceneId)?.SourceScene.Id ?? "" : c.SceneId;
        return c.KnowledgeGroup == QuestionKnowledgeGroup.Food && PairedDishIds.Any(id =>
            sceneId.StartsWith($"theme-food-{id}-", StringComparison.Ordinal)
            || sceneId.StartsWith($"fraction-theme-food-{id}-", StringComparison.Ordinal));
    }

    // A single venue kind is chosen per question and shared by both owners.
    private static readonly (string Vi, string En)[] Bakeries =
        [("tiệm bánh", "bakery"), ("cửa hàng bánh", "cake shop")];
    private static readonly (string Vi, string En)[] Eateries =
        [("quán ăn", "eatery"), ("nhà hàng", "restaurant"), ("tiệm ăn", "diner")];

    internal static int VenueCount(bool bakery) => bakery ? Bakeries.Length : Eateries.Length;

    internal static string Venue(bool bakery, int choice, AppLanguage language)
    {
        var kinds = bakery ? Bakeries : Eateries;
        var kind = kinds[choice % kinds.Length];
        return language == AppLanguage.Vietnamese ? kind.Vi : kind.En;
    }

    internal static string OwnerVenue(string owner, AppLanguage language, int choice, bool bakery)
        => language == AppLanguage.Vietnamese
            ? Venue(bakery, choice, language) + " của " + owner
            : owner + "'s " + Venue(bakery, choice, language);

    private static string PluralVenue(string venue) => venue switch
    {
        "bakery" => "bakeries", "cake shop" => "cake shops", "eatery" => "eateries",
        "restaurant" => "restaurants", _ => "diners"
    };

    // AI drafts must keep the same venue in the question as in both C# owner facts.
    internal static bool CombinedQuestionNamesVenue(string questionTemplate, BasicQuestionContract c)
    {
        if (c.Version == FindXQuestionCatalogue.Version) c = FindXQuestionCatalogue.AsApplied(c);
        if (FoodThemeCatalogue.IsCheDessertContract(c))
        {
            if (c.Structure != BasicQuestionStructure.Combine) return true;
            bool viChe = c.Language == AppLanguage.Vietnamese;
            return c.Subject.Equals(viChe ? "quán chè thứ nhất" : "the first dessert shop", StringComparison.OrdinalIgnoreCase)
                && c.OtherSubject.Equals(viChe ? "quán chè thứ hai" : "the second dessert shop", StringComparison.OrdinalIgnoreCase)
                && questionTemplate.Contains(viChe ? "cả hai quán chè" : "both dessert shops", StringComparison.OrdinalIgnoreCase)
                && !questionTemplate.Contains(viChe ? "cửa hàng" : "both shops", StringComparison.OrdinalIgnoreCase);
        }
        if (c.KnowledgeGroup == QuestionKnowledgeGroup.Drinks)
        {
            if (c.Structure != BasicQuestionStructure.Combine) return true;
            bool viDrink = c.Language == AppLanguage.Vietnamese;
            return c.Subject.Equals(viDrink ? "quán nước thứ nhất" : "the first drink shop", StringComparison.OrdinalIgnoreCase)
                && c.OtherSubject.Equals(viDrink ? "quán nước thứ hai" : "the second drink shop", StringComparison.OrdinalIgnoreCase)
                && questionTemplate.Contains(viDrink ? "cả hai quán nước" : "both drink shops", StringComparison.OrdinalIgnoreCase)
                && !questionTemplate.Contains(viDrink ? "cửa hàng" : "both shops", StringComparison.OrdinalIgnoreCase);
        }
        if (c.KnowledgeGroup != QuestionKnowledgeGroup.Food || c.Structure != BasicQuestionStructure.Combine)
            return true;

        bool vi = c.Language == AppLanguage.Vietnamese;
        bool bakery = FoodThemeCatalogue.IsBakeryContract(c);
        foreach (int choice in Enumerable.Range(0, VenueCount(bakery)))
        {
            string venue = Venue(bakery, choice, c.Language);
            string ownerPrefix = vi ? venue + " của " : "'s " + venue;
            if (!c.Subject.Contains(ownerPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (!c.OtherSubject.Contains(ownerPrefix, StringComparison.OrdinalIgnoreCase)) return false;

            // The six prepared dishes supply an explicit pair in their reviewed templates.
            // Validate AI wording before rendering can repair a generic venue phrase.
            string question = IsPairedDish(c) ? questionTemplate : AppliedQuestionCatalogue.Render(questionTemplate, c);
            string pair = vi ? "cả hai " + venue : "the two " + PluralVenue(venue);
            if (IsPairedDish(c) && (question.Contains(vi ? "cơ sở" : "business", StringComparison.OrdinalIgnoreCase)
                || !vi && question.Contains("places", StringComparison.OrdinalIgnoreCase)
                || Enumerable.Range(0, VenueCount(bakery)).Select(i => Venue(bakery, i, c.Language))
                    .Where(v => v != venue).Any(v => question.Contains(vi ? v : PluralVenue(v), StringComparison.OrdinalIgnoreCase))))
                return false;
            return question.Contains(pair, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    internal static string AlignCombinedQuestion(string text, BasicQuestionContract c)
    {
        if (FoodThemeCatalogue.IsCheDessertContract(c)) return text;
        if (c.KnowledgeGroup != QuestionKnowledgeGroup.Food || c.Structure != BasicQuestionStructure.Combine
            || !text.Contains('?')) return text;

        bool bakery = FoodThemeCatalogue.IsBakeryContract(c);
        var kinds = bakery ? Bakeries : Eateries;
        string? venue = null;
        foreach (var kind in kinds)
        {
            if (c.Language == AppLanguage.Vietnamese
                ? c.Subject.StartsWith(kind.Vi + " của ", StringComparison.OrdinalIgnoreCase)
                : c.Subject.EndsWith("'s " + kind.En, StringComparison.OrdinalIgnoreCase))
            {
                venue = c.Language == AppLanguage.Vietnamese ? kind.Vi : kind.En;
                break;
            }
        }
        if (venue is null) return text;
        // Saved drafts are reused with FreshFacts(), which can choose another venue.
        // Rebind their explicit pair at display time; AI validation still checks raw prose.
        if (IsPairedDish(c))
            foreach (var kind in kinds)
                text = c.Language == AppLanguage.Vietnamese
                    ? text.Replace("cả hai " + kind.Vi, "cả hai " + venue, StringComparison.OrdinalIgnoreCase)
                    : text.Replace("the two " + PluralVenue(kind.En), "the two " + PluralVenue(venue), StringComparison.OrdinalIgnoreCase);
        if (c.Language == AppLanguage.Vietnamese)
            return text.Replace("cả hai cơ sở", "cả hai " + venue, StringComparison.OrdinalIgnoreCase)
                .Replace("Hai cơ sở", "Hai " + venue, StringComparison.OrdinalIgnoreCase);

        string plural = PluralVenue(venue);
        return text.Replace("the two businesses", "the two " + plural, StringComparison.OrdinalIgnoreCase)
            .Replace("both places", "both " + plural, StringComparison.OrdinalIgnoreCase);
    }

    internal static string Guidance(BasicQuestionContract c)
    {
        if(!FoodThemeCatalogue.IsTheme(c.KnowledgeGroup))return "";
        bool vi=c.Language==AppLanguage.Vietnamese;
        if (FoodThemeCatalogue.IsCheDessertContract(c))
            return vi
                ? "\nĐề món chè: quán chè thứ nhất và quán chè thứ hai là hai chủ thể cố định, không thay bằng cửa hàng, tiệm bánh, nhà hàng hoặc người sở hữu. {unit} là chén chè đúng tên món do C# chọn; không lặp tên chè sau {unit}, {unit_a}, {unit_b}. Phép cộng số nguyên: hai quán bán riêng, dùng 'bán được' ở cả hai dữ kiện và hỏi 'cả hai quán chè bán được tổng cộng bao nhiêu'. Phép trừ số nguyên: một quán chuẩn bị rồi bán bớt. Phân số cộng/trừ: hai quán có lượng riêng, hỏi tổng hoặc chênh lệch, giữ nguyên các phân số. Phép nhân/chia số nguyên: dùng khay chè; phép nhân/chia phân số: giữ đúng quan hệ phần-toàn phần."
                : "\nVietnamese sweet-soup stories: the first dessert shop and the second dessert shop are fixed actors; do not replace them with generic stores, bakeries, restaurants, or owners. {unit} already specifies the bowl and exact dessert name, so never repeat the food after {unit}, {unit_a}, or {unit_b}. For integer addition, both shops sell separate amounts: 'sold' in both facts, and ask how many both dessert shops sold altogether. For integer subtraction one shop prepares then sells some. For fraction addition/subtraction compare two separate shop amounts and ask for their total or difference. For integer multiplication/division use trays; for fraction multiplication/division preserve the part-to-whole relationship.";
        if (c.KnowledgeGroup == QuestionKnowledgeGroup.Drinks)
            return vi
                ? "\nĐề nước uống: {name} là 'quán nước thứ nhất', {other} là 'quán nước thứ hai'. Không dùng 'cửa hàng' hay chủ thể cá nhân để thay thế. Giữ nguyên {unit}, {unit_a}, {unit_b} theo C# và không lặp tên nước sau đơn vị. Với phép cộng số nguyên, cả hai quán nước bán hai lượng riêng, dùng 'bán được' trong hai dữ kiện và hỏi 'cả hai quán nước bán được tổng cộng bao nhiêu'. Với phân số cộng/trừ, so sánh hai lượng riêng của hai quán nước; hỏi tổng hoặc chênh lệch, giữ đúng đơn vị và phân số. Với phép nhân/chia giữ quan hệ nhóm hoặc phần-toàn phần của đề."
                : "\nDrink problems: {name} is 'the first drink shop' and {other} is 'the second drink shop'. Never replace them with generic shops or individual people. Preserve the complete C# {unit}, {unit_a} and {unit_b} without repeating the drink name. For integer addition both drink shops sell separate amounts: use 'sold' in both facts and ask how many both drink shops sold altogether. For fraction addition/subtraction compare two separate amounts held at the shops and ask for the sum or difference. For multiplication/division preserve the group or part-to-whole relationship.";
        var operation=c.Version==FindXQuestionCatalogue.Version?FindXQuestionCatalogue.Find(c.SceneId)!.SourceScene.Operation:c.Operation;
        string relation=operation switch
        {
            ArithmeticOperation.Add=>vi?"Hai chủ thể có hai lượng riêng; hỏi tổng của cả hai.":"Two owners hold separate quantities; ask their combined total.",
            ArithmeticOperation.Subtract=>vi?"Cùng một chủ thể có lượng ban đầu và dùng bớt một phần; hỏi lượng còn lại.":"One owner has an initial stock and uses part of it; ask the amount left.",
            ArithmeticOperation.Multiply=>vi?"Mỗi nhóm có cùng lượng; số nhóm đã cho; hỏi tổng tất cả các nhóm.":"Every group has the same amount; the group count is given; ask the total across all groups.",
            _=>vi?"Tổng lượng được chia đều cho số nhóm đã cho; hỏi lượng của mỗi nhóm.":"The total is shared equally across the given group count; ask the amount per group."
        };
        if(c.Version==FractionQuestionCatalogue.Version)
            relation=operation switch {
                ArithmeticOperation.Subtract=>vi?"So sánh hai lượng riêng của hai chủ thể; hỏi chênh lệch, không phải lấy bớt hay phần còn lại.":"Compare two separate amounts held by two owners; ask their difference, not removal or remaining stock.",
                ArithmeticOperation.Multiply=>vi?"Biết lượng ban đầu và phân số được chọn; hỏi giá trị phần được chọn.":"The original amount and selected fraction are given; ask the selected amount.",
                ArithmeticOperation.Divide=>vi?"Biết giá trị một phần và phân số tương ứng; hỏi lượng ban đầu.":"The value of one part and its fraction are given; ask the original whole amount.",
                _=>relation };
        string venueGuidance = FoodThemeCatalogue.IsBakeryContract(c)
            ? vi
                ? " Bánh được làm hoặc bán tại tiệm bánh/cửa hàng bánh có chủ được nêu tên; bao gồm bánh chưng, bánh giò, bánh dày, bánh bao nhân thịt và trứng cút, bánh tét. Viết dữ kiện theo hoạt động chuẩn bị/bán bánh tại cửa hàng, không viết như món phục vụ trên bàn ăn; riêng bánh cuốn và bánh xèo là món phục vụ tại quán ăn/nhà hàng. {name} và {other} đã bao gồm tên tiệm và chủ tiệm; không thêm tiền tố chủ sở hữu. Với phép nhân/chia số nguyên, dùng khay bánh để chia số bánh, không dùng bàn ăn hoặc nhóm chung chung. Với phân số, giữ nguyên quan hệ lượng phần và lượng toàn phần của bánh."
                : " Cakes, pastries and bakery items are made or sold at a named owner's bakery or cake shop, including banh chung, banh gio, banh day, pork-and-quail-egg steamed buns and banh tet. Describe preparing or selling the cakes in the shop, rather than serving restaurant meals at tables. Steamed rice rolls and Vietnamese sizzling pancakes remain restaurant dishes. {name} and {other} already include the bakery and owner; never add a duplicate owner. For whole-number multiplication/division, use trays rather than tables or generic groups. For fractions, retain the given part-to-whole relation."
            : c.KnowledgeGroup == QuestionKnowledgeGroup.Food
            ? vi
                ? " Tất cả món ăn đã chế biến được phục vụ hoặc bán tại quán ăn/nhà hàng/tiệm ăn có chủ được nêu tên; bao gồm bánh bèo, bánh khọt, bánh bột lọc, há cảo, bánh cuốn, bánh xèo, bột chiên, mì Ý, pizza, salad, hot dog và udon. {name} và {other} đã bao gồm tên quán và chủ quán; không thêm tiền tố chủ sở hữu. Với phép nhân/chia số nguyên, dùng bàn ăn để chia suất; không dùng 'nhóm' chung chung. Với phân số, giữ nguyên quan hệ lượng phần và lượng toàn phần của món đã làm."
                : " All prepared dishes are served or sold at a named owner's eatery, restaurant or diner; including banh beo, banh khot, banh bot loc, steamed dumplings, steamed rice rolls, Vietnamese sizzling pancakes, bot chien, spaghetti, pizza, salad, hot dogs and udon. {name} and {other} already include the business and owner; do not add another owner prefix. For whole-number multiplication/division, distribute servings across tables, not generic groups. For fractions, retain the given part-to-whole relation of prepared dishes."
            : "";
        string pairGuidance = c.KnowledgeGroup == QuestionKnowledgeGroup.Food
            && c.Structure == BasicQuestionStructure.Combine && !string.IsNullOrWhiteSpace(c.OtherSubject)
            ? vi
                ? " Hai cửa hàng trong cùng một đề phải cùng loại địa điểm: hai tiệm bánh, hai cửa hàng bánh, hai quán ăn, hai nhà hàng hoặc hai tiệm ăn. "
                    + "Hai chủ sở hữu đã được C# gán cùng loại: " + c.Subject + " và " + c.OtherSubject
                    + ". Câu hỏi phải nói 'cả hai' kèm đúng tên loại địa điểm này, không dùng 'cơ sở' chung chung."
                : " Both owners run the same kind of venue, already fixed by C#: " + c.Subject + " and " + c.OtherSubject
                    + ". Ask about both bakeries, both cake shops, both eateries, both restaurants or both diners as appropriate; avoid generic businesses or places."
            : "";
        string steakGuidance = c.KnowledgeGroup == QuestionKnowledgeGroup.Food
            && c.Unit.Contains("steak", StringComparison.OrdinalIgnoreCase)
            ? vi
                ? " Mỗi đơn vị là một suất steak đã chế biến, không phải miếng thịt sống. Giữ đúng thịt bò/gà/cừu và đúng sốt theo đơn vị C# (tiêu, kem nấm, rượu vang đỏ, béarnaise, chimichurri, BBQ hoặc bơ tỏi thảo mộc); món không ghi sốt thì không tự thêm sốt."
                : " Each unit is one prepared steak serving, not raw meat. Preserve the C# protein (beef, chicken or lamb) and its exact sauce (peppercorn, creamy mushroom, red wine, béarnaise, chimichurri, BBQ or garlic herb butter); do not invent sauce for a plain steak."
            : "";
        string koreanGuidance = c.KnowledgeGroup == QuestionKnowledgeGroup.Food
            && FoodThemeCatalogue.Subjects.Any(s => KoreanDishIds.Contains(s.Unit.Id)
                && (c.Unit == s.Unit.Vietnamese || c.Unit == s.Unit.Plural || c.Unit == s.Unit.Singular))
            ? vi
                ? " Giữ đúng danh tính món Hàn Quốc do đơn vị C# xác định: Tteokbokki là bánh gạo cay; Korean Corn Dog là xúc xích hoặc phô mai bọc bột chiên; Hotteok là bánh rán dẹt nhân ngọt; Bungeoppang là bánh hình cá; Gyeranppang là bánh mì trứng; Sundae Gukbap là súp cơm với dồi Hàn Quốc; Andong Jjimdak là gà om; Kalguksu là mì sợi cắt bằng dao; Squid Sundae là mực nhồi. Tuyệt đối không nhầm dồi trong Sundae Gukbap với mực nhồi Squid Sundae, không tự đổi món hoặc biến phần ăn chín thành nguyên liệu sống."
                : " Preserve the exact Korean dish selected by C#: Tteokbokki is spicy rice cakes; Korean Corn Dog is a battered fried sausage or cheese snack; Hotteok is a sweet filled griddled pancake; Bungeoppang is a fish-shaped pastry; Gyeranppang is egg bread; Sundae Gukbap is Korean blood sausage rice soup; Andong Jjimdak is braised chicken; Kalguksu is knife-cut noodle soup; Squid Sundae is stuffed squid. Never confuse blood sausage in Sundae Gukbap with stuffed squid in Squid Sundae, rename the selected dish, or turn a prepared serving into raw ingredients."
            : "";
        var vietnameseDish = c.KnowledgeGroup == QuestionKnowledgeGroup.Food
            ? FoodThemeCatalogue.Subjects.FirstOrDefault(s => VietnameseDishDescriptions.ContainsKey(s.Unit.Id)
                && (c.Unit == s.Unit.Vietnamese || c.Unit == s.Unit.Singular || c.Unit == s.Unit.Plural))
            : null;
        string vietnameseDishesGuidance = vietnameseDish is null ? "" : vi
            ? " Giữ đúng món Việt Nam đã chọn, không đổi sang món cùng loại hoặc nguyên liệu sống: "
                + VietnameseDishDescriptions[vietnameseDish.Unit.Id].Vi
                + ". Mỗi đơn vị {unit} là một suất/đĩa/tô/phần món đã hoàn chỉnh, không tự đổi sốt, nguyên liệu chính hay cách nấu."
            : " Preserve the exact Vietnamese prepared dish selected by C#: "
                + VietnameseDishDescriptions[vietnameseDish.Unit.Id].En
                + ". Each {unit} is one complete prepared serving/plate/bowl, never a raw ingredient; do not change its sauce, main ingredients or cooking method.";
        return "\n"+(vi
            ?"Tên đầy đủ của đơn vị đếm do C# điền: "+c.Unit+". {unit}, {unit_a}, {unit_b} đã gồm cả tên vật và từ chỉ loại; không viết lặp tên món/quả/nước sau các biến đó. {name} và {other} là chủ thể sở hữu, không phải số lượng hay đồ vật. Không đổi món đã hoàn thành thành nguyên liệu. Mỗi dữ kiện chỉ là một câu mô tả, không chèn câu hỏi hoặc lời chỉ dẫn vào dữ kiện. "
            :"The complete counted unit supplied by C# is: "+c.Unit+". {unit}, {unit_a}, {unit_b} already include the object and its counter; never append the food, fruit, drink or group noun again after these slots. {name} and {other} identify owners, never amounts or items. Keep prepared food distinct from ingredients. Each fact is one declarative sentence; do not insert questions or author instructions inside facts. ")
            +relation+venueGuidance+pairGuidance+steakGuidance+koreanGuidance+vietnameseDishesGuidance;
    }
}
