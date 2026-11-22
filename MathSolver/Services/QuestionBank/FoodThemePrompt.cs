using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

internal sealed record FoodDishDescription(string Id, string Vi, string En);
internal sealed record FoodVenueKind(string Vi, string En);
internal sealed record FoodDessertShopTranslation(string Dessert, string EnglishShop);
internal sealed record FoodDrinkShopActorPhrases(string Id, string ViFirst, string ViSecond, string ViBoth,
    string EnFirst, string EnSecond, string EnBoth);
internal sealed record FoodVenuePlural(string Singular, string Plural);

internal static class FoodThemePrompt
{
    private static readonly IReadOnlyList<string> PairedDishIds = QuizContentCatalog.LoadList<string>("FoodPairedDishIds");
    private static readonly IReadOnlyDictionary<string, FoodDishDescription> KoreanDishDescriptions =
        QuizContentCatalog.LoadList<FoodDishDescription>("FoodKoreanDishDescriptions").ToDictionary(x => x.Id, StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, FoodDishDescription> VietnameseDishDescriptions =
        QuizContentCatalog.LoadList<FoodDishDescription>("FoodVietnameseDishDescriptions").ToDictionary(x => x.Id, StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, FoodDishDescription> JapaneseDishDescriptions =
        QuizContentCatalog.LoadList<FoodDishDescription>("FoodJapaneseDishDescriptions").ToDictionary(x => x.Id, StringComparer.Ordinal);

    internal static bool IsPairedFractionScene(string sceneId)
        => PairedDishIds.Any(id => sceneId == $"fraction-theme-food-{id}-total")
            || FoodThemeCatalogue.DessertShopKind(FoodThemeCatalogue.RestaurantDishForScene(sceneId)?.Unit.Id) is not null
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
    private static readonly IReadOnlyList<FoodVenueKind> Bakeries = QuizContentCatalog.LoadList<FoodVenueKind>("FoodBakeryVenues");
    private static readonly IReadOnlyList<FoodVenueKind> Eateries = QuizContentCatalog.LoadList<FoodVenueKind>("FoodEateryVenues");

    internal static int VenueCount(bool bakery) => bakery ? Bakeries.Count : Eateries.Count;

    internal static string Venue(bool bakery, int choice, AppLanguage language)
    {
        var kinds = bakery ? Bakeries : Eateries;
        var kind = kinds[choice % kinds.Count];
        return language == AppLanguage.Vietnamese ? kind.Vi : kind.En;
    }

    internal static string OwnerVenue(string owner, AppLanguage language, int choice, bool bakery)
        => language == AppLanguage.Vietnamese
            ? Venue(bakery, choice, language) + " của " + owner
            : owner + "'s " + Venue(bakery, choice, language);

    // Preserve the venue in saved prose when refreshing numeric facts and owners.
    // Switching it would also change the historical prose deduplication hash.
    internal static int? VenueChoice(BasicQuestionContract contract)
    {
        if (contract.KnowledgeGroup != QuestionKnowledgeGroup.Food
            || FoodThemeCatalogue.DessertShopKind(contract) is not null)
            return null;

        bool bakery = FoodThemeCatalogue.IsBakeryContract(contract);
        for (int choice = 0; choice < VenueCount(bakery); choice++)
        {
            string venue = Venue(bakery, choice, contract.Language);
            bool matches = contract.Language == AppLanguage.Vietnamese
                ? contract.Subject.StartsWith(venue + " của ", StringComparison.Ordinal)
                : contract.Subject.EndsWith("'s " + venue, StringComparison.Ordinal);
            if (matches) return choice;
        }
        return null;
    }

    private static readonly IReadOnlyDictionary<string, string> DessertEnglishShops =
        QuizContentCatalog.LoadList<FoodDessertShopTranslation>("FoodDessertShopTranslations")
            .ToDictionary(entry => entry.Dessert, entry => entry.EnglishShop, StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, FoodDrinkShopActorPhrases> DrinkShopPhrases =
        QuizContentCatalog.LoadList<FoodDrinkShopActorPhrases>("FoodDrinkShopActorPhrases")
            .ToDictionary(entry => entry.Id, StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string, string> VenuePlurals =
        QuizContentCatalog.LoadList<FoodVenuePlural>("FoodVenuePlurals")
            .ToDictionary(entry => entry.Singular, entry => entry.Plural, StringComparer.Ordinal);

    internal static (string First, string Second, string Both) DessertShopActors(string dessert, AppLanguage language)
    {
        if (language == AppLanguage.Vietnamese)
            return ($"quán {dessert} thứ nhất", $"quán {dessert} thứ hai", $"cả hai quán {dessert}");

        if (dessert is null || !DessertEnglishShops.TryGetValue(dessert, out string? shop))
            throw new ArgumentOutOfRangeException(nameof(dessert), dessert, "Unsupported dessert venue");
        return ($"the first {shop}", $"the second {shop}", $"both {shop}s");
    }

    internal static (string First, string Second, string Both) DrinkShopActors(bool coffee, AppLanguage language)
        => DrinkShopActors(coffee, false, language);

    internal static (string First, string Second, string Both) DrinkShopActors(bool coffee, bool milkTea, AppLanguage language)
    {
        string kind = coffee ? "coffee" : milkTea ? "milk-tea" : "drinks";
        var phrases = DrinkShopPhrases[kind];
        return language == AppLanguage.Vietnamese
            ? (phrases.ViFirst, phrases.ViSecond, phrases.ViBoth)
            : (phrases.EnFirst, phrases.EnSecond, phrases.EnBoth);
    }

    private static string PluralVenue(string venue)
        => VenuePlurals.TryGetValue(venue, out string? plural) ? plural : "diners";

    // AI drafts must keep the same venue in the question as in both C# owner facts.
    internal static bool CombinedQuestionNamesVenue(string questionTemplate, BasicQuestionContract c)
    {
        if (c.Version == FindXQuestionCatalogue.Version) c = FindXQuestionCatalogue.AsApplied(c);
        if (FruitGardenStoryTemplates.IsGardenScene(c.SceneId))
            return FruitGardenStoryTemplates.MatchesVenue(questionTemplate, c);
        if (FoodThemeCatalogue.DessertShopKind(c) is { } dessert)
        {
            if (c.Structure != BasicQuestionStructure.Combine) return true;
            var actors = DessertShopActors(dessert, c.Language);
            return c.Subject.Equals(actors.First, StringComparison.OrdinalIgnoreCase)
                && c.OtherSubject.Equals(actors.Second, StringComparison.OrdinalIgnoreCase)
                && questionTemplate.Contains(actors.Both, StringComparison.OrdinalIgnoreCase)
                && !questionTemplate.Contains(c.Language == AppLanguage.Vietnamese ? "cửa hàng" : "both shops", StringComparison.OrdinalIgnoreCase);
        }
        if (c.KnowledgeGroup == QuestionKnowledgeGroup.Drinks)
        {
            if (c.Structure != BasicQuestionStructure.Combine) return true;
            var shops = DrinkShopActors(FoodThemeCatalogue.IsCoffeeDrinkContract(c),
                FoodThemeCatalogue.IsMilkTeaDrinkContract(c), c.Language);
            return c.Subject.Equals(shops.First, StringComparison.OrdinalIgnoreCase)
                && c.OtherSubject.Equals(shops.Second, StringComparison.OrdinalIgnoreCase)
                && questionTemplate.Contains(shops.Both, StringComparison.OrdinalIgnoreCase)
                && !questionTemplate.Contains(c.Language == AppLanguage.Vietnamese ? "cửa hàng" : "both shops", StringComparison.OrdinalIgnoreCase);
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
        if (FoodThemeCatalogue.DessertShopKind(c) is not null) return text;
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
        if (FruitGardenStoryTemplates.IsGardenScene(c.SceneId))
            return FruitGardenStoryTemplates.Guidance(c);
        bool vi=c.Language==AppLanguage.Vietnamese;
        if (FoodThemeCatalogue.DessertShopKind(c) is { } dessert)
        {
            var shops = DessertShopActors(dessert, c.Language);
            string dessertGuidance = vi
                ? $"\nĐề món {dessert}: '{shops.First}' và '{shops.Second}' là hai chủ thể cố định; không thay bằng cửa hàng, tiệm bánh, nhà hàng hoặc người sở hữu. {{unit}} là đơn vị món đúng tên do C# chọn; không lặp tên món sau đơn vị. Phép cộng số nguyên: hai quán bán riêng, dùng 'bán được' ở cả hai dữ kiện và hỏi '{shops.Both} bán được tổng cộng bao nhiêu'. Phép trừ số nguyên: một quán chuẩn bị rồi bán bớt. Phân số cộng/trừ: hai quán có lượng riêng, hỏi tổng hoặc chênh lệch. Phép nhân/chia số nguyên: dùng khay; phân số nhân/chia giữ quan hệ phần-toàn phần. Giữ nguyên {{unit}}, {{unit_a}}, {{unit_b}}."
                : $"\nVietnamese {dessert} stories: '{shops.First}' and '{shops.Second}' are fixed actors; do not replace them with generic stores, bakeries, restaurants or owners. The full C# {{unit}} specifies the exact item and serving; do not repeat the item after {{unit}}, {{unit_a}} or {{unit_b}}. For integer addition, both shops sell separate amounts: use 'sold' in both facts and ask how many {shops.Both} sold altogether. For integer subtraction, one shop prepares then sells some. For fraction addition/subtraction, compare two shop amounts and ask for their sum or difference. For integer multiplication/division use trays; for fractions preserve the part-to-whole relationship.";
            return dessertGuidance + (vi ? "\nĐơn vị món C# đã chọn: " : "\nC# counted unit: ") + c.Unit + ".";
        }
        if (c.KnowledgeGroup == QuestionKnowledgeGroup.Drinks)
        {
            var shops = DrinkShopActors(FoodThemeCatalogue.IsCoffeeDrinkContract(c),
                FoodThemeCatalogue.IsMilkTeaDrinkContract(c), c.Language);
            string drinkGuidance = vi
                ? $"\nĐề nước uống: {{name}} là '{shops.First}', {{other}} là '{shops.Second}'. Không dùng cửa hàng, quán sai loại hay cá nhân để thay thế. Giữ nguyên {{unit}}, {{unit_a}}, {{unit_b}} theo C# và không lặp tên thức uống sau đơn vị. Với phép cộng số nguyên, hai quán bán lượng riêng, dùng 'bán được' trong hai dữ kiện và hỏi '{shops.Both} bán được tổng cộng bao nhiêu'. Với phân số cộng/trừ, so sánh hai lượng riêng và hỏi tổng hoặc chênh lệch. Với phép nhân/chia giữ quan hệ nhóm hoặc phần-toàn phần."
                : $"\nDrink problems: {{name}} is '{shops.First}' and {{other}} is '{shops.Second}'. Do not replace them with generic shops, a different kind of shop, or people. Preserve the complete C# {{unit}}, {{unit_a}} and {{unit_b}} without repeating the drink name. For integer addition both shops sell separate amounts: use 'sold' in both facts and ask how many {shops.Both} sold altogether. For fraction addition/subtraction compare two separate amounts and ask for the sum or difference. For multiplication/division preserve the group or part-to-whole relationship.";
            return drinkGuidance + (vi ? "\nĐơn vị thức uống C# đã chọn: " : "\nC# counted drink unit: ") + c.Unit + ".";
        }
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
                ? " Bánh được làm hoặc bán tại tiệm bánh/cửa hàng bánh có chủ được nêu tên: bánh chưng, bánh giò, bánh dày, bánh bao thịt và trứng cút, bánh tét. Viết về chuẩn bị/bán bánh tại tiệm; bánh cuốn và bánh xèo phục vụ ở quán ăn. {name} và {other} đã gồm tên tiệm và chủ, không thêm chủ sở hữu. Nhân/chia số nguyên dùng khay bánh; phân số giữ đúng phần và toàn phần."
                : " Cakes, pastries and bakery items are made or sold at a named owner's bakery or cake shop: banh chung, banh gio, banh day, pork-and-quail-egg buns and banh tet. Describe bakery preparation/sales; rice rolls and sizzling pancakes are restaurant dishes. {name} and {other} already include the venue and owner. For integer multiplication/division use trays; for fractions keep the part-to-whole relation."
            : c.KnowledgeGroup == QuestionKnowledgeGroup.Food
            ? vi
                ? " Tất cả món ăn đã chế biến được phục vụ hoặc bán tại quán ăn/nhà hàng/tiệm ăn có chủ: bánh bèo, bánh khọt, bánh bột lọc, há cảo, bánh cuốn, bánh xèo, bột chiên, mì Ý, pizza, salad, hot dog và udon. {name} và {other} đã gồm quán và chủ, không lặp chủ sở hữu. Nhân/chia số nguyên dùng bàn ăn chia suất; phân số giữ đúng phần và toàn phần."
                : " All prepared dishes are served or sold at the named owner's eatery, restaurant or diner: banh beo, banh khot, banh bot loc, steamed dumplings, steamed rice rolls, Vietnamese sizzling pancakes, bot chien, spaghetti, pizza, salad, hot dogs and udon. {name} and {other} already include the venue and owner; do not repeat either. For integer multiplication/division use tables for servings; for fractions preserve part-to-whole quantities."
            : "";
        string pairGuidance = c.KnowledgeGroup == QuestionKnowledgeGroup.Food
            && c.Structure == BasicQuestionStructure.Combine && !string.IsNullOrWhiteSpace(c.OtherSubject)
            ? vi
                ? " Hai cửa hàng trong cùng một đề phải cùng loại địa điểm. Chủ sở hữu do C# chọn: " + c.Subject + " và " + c.OtherSubject
                    + ". Hỏi 'cả hai' kèm đúng loại tiệm bánh/cửa hàng bánh/quán ăn/nhà hàng/tiệm ăn, không dùng 'cơ sở'."
                : " Both owners run the same kind of venue, fixed by C#: " + c.Subject + " and " + c.OtherSubject
                    + ". Ask about both matching bakeries, cake shops, eateries, restaurants or diners; avoid generic places."
            : "";
        string steakGuidance = c.KnowledgeGroup == QuestionKnowledgeGroup.Food
            && c.Unit.Contains("steak", StringComparison.OrdinalIgnoreCase)
            ? vi
                ? " Mỗi đơn vị là một suất steak đã chế biến, không phải miếng thịt sống. Giữ đúng thịt bò/gà/cừu và đúng sốt theo đơn vị C# (tiêu, kem nấm, rượu vang đỏ, béarnaise, chimichurri, BBQ hoặc bơ tỏi thảo mộc); món không ghi sốt thì không tự thêm sốt."
                : " Each unit is one prepared steak serving, not raw meat. Preserve the C# protein (beef, chicken or lamb) and its exact sauce (peppercorn, creamy mushroom, red wine, béarnaise, chimichurri, BBQ or garlic herb butter); do not invent sauce for a plain steak."
            : "";
        var koreanDish = c.KnowledgeGroup == QuestionKnowledgeGroup.Food
            ? FoodThemeCatalogue.Subjects.FirstOrDefault(s => KoreanDishDescriptions.ContainsKey(s.Unit.Id)
                && (c.Unit == s.Unit.Vietnamese || c.Unit == s.Unit.Singular || c.Unit == s.Unit.Plural))
            : null;
        string koreanGuidance = koreanDish is null ? "" : vi
            ? " Giữ đúng món Hàn Quốc do C# chọn: " + KoreanDishDescriptions[koreanDish.Unit.Id].Vi
                + ". Mỗi {unit} là một phần ăn đã chế biến. Không đổi món, biến thành nguyên liệu sống hoặc nhầm dồi Hàn Quốc trong Sundae Gukbap với mực nhồi Squid Sundae."
            : " Preserve the Korean dish selected by C#: " + KoreanDishDescriptions[koreanDish.Unit.Id].En
                + ". Each {unit} is a complete prepared serving. Never rename the dish, turn it into raw ingredients or confuse the blood sausage in Sundae Gukbap with the stuffed squid in Squid Sundae.";
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
        var japaneseDish = c.KnowledgeGroup == QuestionKnowledgeGroup.Food
            ? FoodThemeCatalogue.Subjects.FirstOrDefault(s => JapaneseDishDescriptions.ContainsKey(s.Unit.Id)
                && (c.Unit == s.Unit.Vietnamese || c.Unit == s.Unit.Singular || c.Unit == s.Unit.Plural))
            : null;
        string japaneseGuidance = japaneseDish is null ? "" : vi
            ? " Giữ đúng món Nhật Bản do C# chọn: " + JapaneseDishDescriptions[japaneseDish.Unit.Id].Vi
                + ". Đây là món/khẩu phần đã chế biến để bán tại quán ăn, nhà hàng hoặc tiệm ăn; không đổi thành nguyên liệu, tên món khác hoặc loại hình kinh doanh khác. Không tự chia nhỏ một phần thành từng miếng khi đếm."
            : " Preserve the exact Japanese dish selected by C#: " + JapaneseDishDescriptions[japaneseDish.Unit.Id].En
                + ". This is a complete prepared item/serving sold at an eatery, restaurant or diner; do not replace it with an ingredient, a different dish or a different venue type. Do not count pieces inside a serving as separate units.";
        return "\n"+(vi
            ?"Tên đầy đủ của đơn vị đếm do C# điền: "+c.Unit+". {unit}, {unit_a}, {unit_b} đã gồm cả tên vật và từ chỉ loại; không viết lặp tên món/quả/nước sau các biến đó. {name} và {other} là chủ thể sở hữu, không phải số lượng hay đồ vật. Không đổi món đã hoàn thành thành nguyên liệu. Mỗi dữ kiện chỉ là một câu mô tả, không chèn câu hỏi hoặc lời chỉ dẫn vào dữ kiện. "
            :"The complete counted unit supplied by C# is: "+c.Unit+". {unit}, {unit_a}, {unit_b} already include the object and its counter; never append the food, fruit, drink or group noun again after these slots. {name} and {other} identify owners, never amounts or items. Keep prepared food distinct from ingredients. Each fact is one declarative sentence; do not insert questions or author instructions inside facts. ")
            +relation+venueGuidance+pairGuidance+steakGuidance+koreanGuidance+vietnameseDishesGuidance+japaneseGuidance;
    }
}
