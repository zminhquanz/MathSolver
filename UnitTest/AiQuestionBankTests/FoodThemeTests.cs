using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;

internal static class FoodThemeTests
{
    private static readonly string[] TakeawayBakeryCakeIds =
        ["banh-chung", "banh-gio", "banh-day", "banh-bao-thit-trung-cut", "banh-tet"];
    private static readonly (string UnitId, string ArtworkId, string Vietnamese, string Singular, string Plural, bool Bakery)[] AdditionalPreparedDishes =
    [
        ("che-dau-do", "che_dau_do", "chén chè đậu đỏ", "bowl of red bean sweet soup", "bowls of red bean sweet soup", false),
        ("che-dau-den", "che_dau_den", "chén chè đậu đen", "bowl of black bean sweet soup", "bowls of black bean sweet soup", false),
        ("che-dau-xanh", "che_dau_xanh", "chén chè đậu xanh", "bowl of mung bean sweet soup", "bowls of mung bean sweet soup", false),
        ("che-troi-nuoc", "che_troi_nuoc", "chén chè trôi nước", "bowl of glutinous rice ball sweet soup", "bowls of glutinous rice ball sweet soup", false),
        ("che-thap-cam", "che_thap_cam", "chén chè thập cẩm", "bowl of mixed Vietnamese sweet soup", "bowls of mixed Vietnamese sweet soup", false),
        ("che-thai", "che_thai", "chén chè Thái", "bowl of Thai-style fruit sweet soup", "bowls of Thai-style fruit sweet soup", false),
        ("che-chuoi", "che_chuoi", "chén chè chuối", "bowl of banana and coconut sweet soup", "bowls of banana and coconut sweet soup", false),
        ("che-bap", "che_bap", "chén chè bắp", "bowl of sweet corn pudding", "bowls of sweet corn pudding", false),
        ("che-hat-sen-long-nhan", "che_hat_sen_long_nhan", "chén chè hạt sen long nhãn", "bowl of lotus seed and longan sweet soup", "bowls of lotus seed and longan sweet soup", false),
        ("che-dau-phong", "che_dau_phong", "chén chè đậu phộng", "bowl of peanut sweet soup", "bowls of peanut sweet soup", false),
        ("che-khuc-bach", "che_khuc_bach", "chén chè khúc bạch", "bowl of almond panna cotta sweet soup", "bowls of almond panna cotta sweet soup", false),
        ("che-suong-sa-hat-luu", "che_suong_sa_hat_luu", "chén chè sương sa hạt lựu", "bowl of jelly and pomegranate-seed sweet soup", "bowls of jelly and pomegranate-seed sweet soup", false),
        ("che-khoai-mon", "che_khoai_mon", "chén chè khoai môn", "bowl of taro sweet soup", "bowls of taro sweet soup", false),
        ("bot-chien", "bot_chien", "phần bột chiên", "serving of Vietnamese fried rice flour cakes", "servings of Vietnamese fried rice flour cakes", false),
        ("pizzas", "pizza", "chiếc bánh pizza", "pizza", "pizzas", false),
        ("mixed-salad", "mixed_salad", "phần salad trộn", "serving of mixed salad", "servings of mixed salad", false),
        ("spaghetti", "spaghetti", "đĩa mì Ý", "plate of spaghetti", "plates of spaghetti", false),
        ("hot-dogs", "hot_dog", "chiếc bánh hot dog", "hot dog", "hot dogs", false),
        ("udon", "udon", "tô mì udon", "bowl of udon", "bowls of udon", false),
        ("goi-cuon", "goi_cuon", "đĩa gỏi cuốn", "plate of fresh spring rolls", "plates of fresh spring rolls", false),
        ("bun-cha", "bun_cha", "suất bún chả", "serving of bun cha", "servings of bun cha", false),
        ("banh-trang-tron", "banh_trang_tron", "phần bánh tráng trộn", "serving of Vietnamese rice paper salad", "servings of Vietnamese rice paper salad", false),
        ("banh-trang-nuong", "banh_trang_nuong", "chiếc bánh tráng nướng", "grilled Vietnamese rice paper", "grilled Vietnamese rice papers", false),
        ("pha-lau-bo", "pha_lau_bo", "phần phá lấu bò", "serving of Vietnamese beef offal stew", "servings of Vietnamese beef offal stew", false),
        ("ca-ri-ga-viet", "ca_ri_ga_viet", "phần cà ri gà Việt Nam", "serving of Vietnamese chicken curry", "servings of Vietnamese chicken curry", false),
        ("ca-ri-bo-nhat", "ca_ri_bo_nhat", "phần cà ri bò Nhật Bản", "serving of Japanese beef curry", "servings of Japanese beef curry", false),
        ("tau-hu-nuoc-duong", "tau_hu_nuoc_duong", "chén tàu hũ nước đường gừng và nước cốt dừa", "bowl of tofu pudding with ginger syrup and coconut milk", "bowls of tofu pudding with ginger syrup and coconut milk", false),
        ("jjinppang", "jjinppang", "chiếc bánh Jjinppang nhân đậu đỏ", "Korean jjinppang red bean bun", "Korean jjinppang red bean buns", true),
        ("chapssal-doughnut", "chapssal_doughnut", "chiếc bánh Chapssal doughnut", "Korean chapssal doughnut", "Korean chapssal doughnuts", true),
        ("bibimbap", "bibimbap", "tô cơm trộn Hàn Quốc", "bowl of Korean bibimbap", "bowls of Korean bibimbap", false),
        ("steak-bo", "steak_bo", "suất steak bò", "serving of beef steak", "servings of beef steak", false),
        ("steak-ga", "steak_ga", "suất steak gà", "serving of chicken steak", "servings of chicken steak", false),
        ("steak-cuu", "steak_cuu", "suất steak cừu", "serving of lamb steak", "servings of lamb steak", false),
        ("steak-bo-sot-tieu", "steak_bo_sot_tieu", "suất steak bò sốt tiêu", "serving of beef steak with peppercorn sauce", "servings of beef steak with peppercorn sauce", false),
        ("steak-ga-sot-kem-nam", "steak_ga_sot_kem_nam", "suất steak gà sốt kem nấm", "serving of chicken steak with creamy mushroom sauce", "servings of chicken steak with creamy mushroom sauce", false),
        ("steak-bo-sot-ruou-vang-do", "steak_bo_sot_ruou_vang_do", "suất steak bò sốt rượu vang đỏ", "serving of beef steak with red wine sauce", "servings of beef steak with red wine sauce", false),
        ("steak-bo-sot-bearnaise", "steak_bo_sot_bearnaise", "suất steak bò sốt béarnaise", "serving of beef steak with béarnaise sauce", "servings of beef steak with béarnaise sauce", false),
        ("steak-cuu-sot-chimichurri", "steak_cuu_sot_chimichurri", "suất steak cừu sốt chimichurri", "serving of lamb steak with chimichurri sauce", "servings of lamb steak with chimichurri sauce", false),
        ("steak-ga-sot-bbq", "steak_ga_sot_bbq", "suất steak gà sốt BBQ", "serving of chicken steak with BBQ sauce", "servings of chicken steak with BBQ sauce", false),
        ("steak-cuu-sot-bo-toi", "steak_cuu_sot_bo_toi", "suất steak cừu sốt bơ tỏi thảo mộc", "serving of lamb steak with garlic herb butter", "servings of lamb steak with garlic herb butter", false),
        ("tteokbokki", "tteokbokki", "đĩa bánh gạo cay Tteokbokki", "plate of Korean spicy rice cakes (tteokbokki)", "plates of Korean spicy rice cakes (tteokbokki)", false),
        ("korean-corn-dog", "korean_corn_dog", "chiếc Korean Corn Dog", "Korean corn dog", "Korean corn dogs", false),
        ("hotteok", "hotteok", "chiếc bánh Hotteok", "Korean hotteok sweet pancake", "Korean hotteok sweet pancakes", true),
        ("bungeoppang", "bungeoppang", "chiếc bánh Bungeoppang", "Korean bungeoppang fish-shaped pastry", "Korean bungeoppang fish-shaped pastries", true),
        ("gyeranppang", "gyeranppang", "chiếc bánh Gyeranppang", "Korean gyeranppang egg bread", "Korean gyeranppang egg breads", true),
        ("sundae-gukbap", "sundae_gukbap", "tô Sundae Gukbap", "bowl of Korean blood sausage rice soup (sundae gukbap)", "bowls of Korean blood sausage rice soup (sundae gukbap)", false),
        ("andong-jjimdak", "andong_jjimdak", "phần Andong Jjimdak", "serving of Andong Jjimdak (Korean braised chicken)", "servings of Andong Jjimdak (Korean braised chicken)", false),
        ("kalguksu", "kalguksu", "tô Kalguksu", "bowl of Korean knife-cut noodle soup (kalguksu)", "bowls of Korean knife-cut noodle soup (kalguksu)", false),
        ("squid-sundae", "squid_sundae", "phần Squid Sundae", "serving of Squid Sundae (Korean stuffed squid)", "servings of Squid Sundae (Korean stuffed squid)", false),
        ("com-suon-cay", "com_suon_cay", "suất cơm sườn cây", "serving of rice with grilled pork ribs", "servings of rice with grilled pork ribs", false),
        ("com-chien-hai-san", "com_chien_hai_san", "đĩa cơm chiên hải sản", "plate of seafood fried rice", "plates of seafood fried rice", false),
        ("com-chien-duong-chau", "com_chien_duong_chau", "đĩa cơm chiên Dương Châu", "plate of Yangzhou fried rice", "plates of Yangzhou fried rice", false),
        ("com-ga-xoi-mo", "com_ga_xoi_mo", "suất cơm gà xối mỡ", "serving of crispy fried chicken with rice", "servings of crispy fried chicken with rice", false),
        ("com-ga-sot-me", "com_ga_sot_me", "suất cơm gà sốt me", "serving of chicken with tamarind sauce and rice", "servings of chicken with tamarind sauce and rice", false),
        ("com-ga-sot-tieu-xanh", "com_ga_sot_tieu_xanh", "suất cơm gà sốt tiêu xanh", "serving of chicken with green peppercorn sauce and rice", "servings of chicken with green peppercorn sauce and rice", false),
        ("com-ga-mam-toi", "com_ga_mam_toi", "suất cơm gà mắm tỏi", "serving of chicken rice with garlic fish sauce", "servings of chicken rice with garlic fish sauce", false),
        ("kem-bo-da-lat", "kem_bo_da_lat", "ly kem bơ Đà Lạt", "glass of Da Lat avocado ice cream", "glasses of Da Lat avocado ice cream", false),
        ("kem-trang-tien", "kem_trang_tien", "que kem Tràng Tiền", "Trang Tien ice cream bar", "Trang Tien ice cream bars", false),
        ("sua-chua-ha-long", "sua_chua_ha_long", "cốc sữa chua Hạ Long", "cup of Ha Long yogurt", "cups of Ha Long yogurt", false),
        ("canh-chua-ca-loc", "canh_chua_ca_loc", "tô canh chua cá lóc", "bowl of sour snakehead fish soup", "bowls of sour snakehead fish soup", false),
        ("canh-kho-qua-nhoi-thit", "canh_kho_qua_nhoi_thit", "tô canh khổ qua nhồi thịt", "bowl of stuffed bitter melon soup", "bowls of stuffed bitter melon soup", false),
        ("canh-bi-do-ham-xuong", "canh_bi_do_ham_xuong", "tô canh bí đỏ hầm xương", "bowl of pumpkin and pork bone soup", "bowls of pumpkin and pork bone soup", false),
        ("canh-cua-rau-day-mong-toi", "canh_cua_rau_day_mong_toi", "tô canh cua rau đay, mồng tơi", "bowl of crab soup with jute mallow and Malabar spinach", "bowls of crab soup with jute mallow and Malabar spinach", false),
        ("canh-rau-ngot-thit-bam", "canh_rau_ngot_thit_bam", "tô canh rau ngót thịt băm", "bowl of sweet leaf soup with minced pork", "bowls of sweet leaf soup with minced pork", false),
        ("ca-loc-kho-to", "ca_loc_kho_to", "phần cá lóc kho tộ", "serving of claypot braised snakehead fish", "servings of claypot braised snakehead fish", false)
    ];

    internal static IEnumerable<(string Name,BasicQuestionContract Contract)> NativeCases()
    {
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(var (id,op) in new[]{("pho",ArithmeticOperation.Subtract),("lemon-tea-glasses",ArithmeticOperation.Multiply),("strawberries",ArithmeticOperation.Add)}
            .Concat(TakeawayBakeryCakeIds.Select(id => (id,ArithmeticOperation.Add)))
            .Concat(AdditionalPreparedDishes.Select(d => (d.UnitId,ArithmeticOperation.Add))))
        {
            var s=FoodThemeCatalogue.Subjects.Single(s=>s.Unit.Id==id);
            yield return ($"{s.Group}/{id}/{op}/{language}",FoodThemeInterchangeTests.Contract(s,op,language,BankQuestionFamily.Arithmetic));
        }
    }
    public static void WriteManifest() => File.WriteAllText("artifacts/food-theme-subjects.json", System.Text.Json.JsonSerializer.Serialize(
        FoodThemeCatalogue.Subjects.Select(s => new { Group=s.Group.ToString(), s.Unit, s.ArtworkId, s.FractionVietnamese, s.FractionEnglish, s.FractionArtworkId }),
        new System.Text.Json.JsonSerializerOptions { WriteIndented=true, Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
    private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    private static void CheckAllFoodVenueKinds()
    {
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var (unit, bakery) in BakeryVenueCases())
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (int seed in Enumerable.Range(0, 80))
            {
                var contract = AppliedQuestionCatalogue.Create(new QuestionLearningProfile(QuestionKnowledgeGroup.Food),
                    ArithmeticOperation.Add, CurriculumTier.ThreeStars, language, new Random(seed),
                    $"theme-food-{unit}-total");
                CheckFoodVenue(contract, AppliedQuestionCatalogue.Draft(contract));
                foreach (int i in Enumerable.Range(0, FoodThemePrompt.VenueCount(bakery)))
                {
                    string venue = FoodThemePrompt.Venue(bakery, i, language);
                    string ownerPhrase = language == AppLanguage.Vietnamese ? venue + " của " : "'s " + venue;
                    if (contract.Subject.Contains(ownerPhrase, StringComparison.OrdinalIgnoreCase))
                        seen.Add(venue);
                }
            }
            Check(seen.Count == FoodThemePrompt.VenueCount(bakery),
                $"Seeded C# scenes did not exercise every food venue: {unit}/{language}: {string.Join(", ", seen)}");
        }
    }
    private static void CheckAiCombinedVenueValidation()
    {
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var (unit, bakery) in BakeryVenueCases())
        {
            var contract = AppliedQuestionCatalogue.Create(new QuestionLearningProfile(QuestionKnowledgeGroup.Food),
                ArithmeticOperation.Add, CurriculumTier.ThreeStars, language, new Random(23),
                $"theme-food-{unit}-total");
            var draft = AppliedQuestionCatalogue.Draft(contract);
            Check(FoodThemePrompt.CombinedQuestionNamesVenue(draft.Question, contract),
                "C# reference question has mismatched food venues");

            var venues = Enumerable.Range(0, FoodThemePrompt.VenueCount(bakery))
                .Select(i => FoodThemePrompt.Venue(bakery, i, language)).ToArray();
            string current = venues.Single(v => contract.Subject.Contains(
                language == AppLanguage.Vietnamese ? v + " của " : "'s " + v,
                StringComparison.OrdinalIgnoreCase));
            string different = venues.First(v => v != current);
            string replacement = language == AppLanguage.Vietnamese ? "cả hai " + different
                : "the two " + (different switch {
                    "bakery" => "bakeries", "cake shop" => "cake shops", "eatery" => "eateries",
                    "restaurant" => "restaurants", _ => "diners"
                });
            string invalid = draft.Question.Replace(
                language == AppLanguage.Vietnamese ? "cả hai cơ sở" : "the two businesses",
                replacement, StringComparison.OrdinalIgnoreCase).Replace(
                language == AppLanguage.Vietnamese ? "cả hai " + current
                    : "the two " + (current switch {
                        "bakery" => "bakeries", "cake shop" => "cake shops", "eatery" => "eateries",
                        "restaurant" => "restaurants", _ => "diners"
                    }), replacement, StringComparison.OrdinalIgnoreCase);
            Check(invalid != draft.Question, "Reference question no longer has a replaceable venue phrase");
            Check(!FoodThemePrompt.CombinedQuestionNamesVenue(invalid, contract),
                "Mismatched AI venue must not pass the semantic venue check");
            Check(!AppliedQuestionCatalogue.Validate(QuestionBankStore.SerializeDraft(draft with { Question = invalid }), contract).IsValid,
                "Mismatched AI venue must be rejected before saving to the question bank");
            var creative = contract with { OpenProse = true };
            if (OpenQuestionProse.Supports(creative))
                Check(!OpenQuestionProse.Validate(QuestionBankStore.SerializeDraft(draft with { Question = invalid }), creative).IsValid,
                    "Creative AI prose must reject a mismatched food venue");
        }
    }
    internal static void CheckDrinkShopVenue(BasicQuestionContract contract, BasicQuestionDraft draft)
    {
        if (contract.KnowledgeGroup != QuestionKnowledgeGroup.Drinks) return;
        var storyOperation = contract.Version == FindXQuestionCatalogue.Version
            ? FindXQuestionCatalogue.Find(contract.SceneId)!.SourceScene.Operation
            : contract.Operation;
        bool vi = contract.Language == AppLanguage.Vietnamese;
        string first = vi ? "quán nước thứ nhất" : "the first drink shop";
        string second = vi ? "quán nước thứ hai" : "the second drink shop";
        string pair = vi ? "cả hai quán nước" : "both drink shops";
        Check(contract.Subject.Equals(first, StringComparison.OrdinalIgnoreCase),
            "Drink venue must be the first drink shop: " + contract.SceneId + "/" + contract.Subject);
        if (contract.Version != FractionQuestionCatalogue.Version)
            Check(contract.OtherSubject.Equals(second, StringComparison.OrdinalIgnoreCase),
                "Drink venue must be the second drink shop: " + contract.SceneId + "/" + contract.OtherSubject);
        string problem = draft.ToWordProblem(contract).ProblemText;
        Check(problem.Contains(first, StringComparison.OrdinalIgnoreCase),
            "Drink story must mention the first drink shop: " + contract.SceneId + ": " + problem);
        if (storyOperation == ArithmeticOperation.Add ||
            contract.Version == FractionQuestionCatalogue.Version && storyOperation == ArithmeticOperation.Subtract)
        {
            Check(problem.Contains(second, StringComparison.OrdinalIgnoreCase)
                && problem.Contains(pair, StringComparison.OrdinalIgnoreCase),
                "Drink addition/comparison must name both drink shops: " + contract.SceneId + ": " + problem);
        }
        if (contract.Version != FractionQuestionCatalogue.Version && storyOperation == ArithmeticOperation.Add)
        {
            Check(problem.Contains(vi ? "bán được" : "sold", StringComparison.OrdinalIgnoreCase),
                "Drink addition must describe sales: " + contract.SceneId);
            Check(FoodThemePrompt.CombinedQuestionNamesVenue(draft.Question, contract),
                "Drink AI reference question must name both beverage shops: " + contract.SceneId);
        }
        string prompt = BasicQuestionPrompt.Build(contract);
        Check(prompt.Contains(vi ? "quán nước" : "drink shop", StringComparison.OrdinalIgnoreCase),
            "Drink AI prompt lost shop vocabulary: " + contract.SceneId);
    }

    private static void CheckDrinkShopAiValidation()
    {
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var subject = FoodThemeCatalogue.Subjects.Single(s => s.Unit.Id == "orange-juice-glasses");
            var original = FoodThemeInterchangeTests.Contract(subject, ArithmeticOperation.Add, language, BankQuestionFamily.Arithmetic);
            var contract = original with { Left = 5, Right = 10 };
            Check(contract.IsValid && contract.Left + contract.Right == 15,
                "5 + 10 orange juice example must remain valid");
            var draft = AppliedQuestionCatalogue.Draft(contract);
            CheckDrinkShopVenue(contract, draft);
            bool vi = language == AppLanguage.Vietnamese;
            string rendered = draft.ToWordProblem(contract).ProblemText;
            Check(rendered.Contains($"{contract.Subject} {(vi ? "bán được" : "sold")} 5 {contract.Unit}",
                    StringComparison.OrdinalIgnoreCase),
                "First drink shop must sell exactly five glasses of orange juice: " + rendered);
            Check(rendered.Contains($"{contract.OtherSubject} {(vi ? "bán được" : "sold")} 10 {contract.Unit}",
                    StringComparison.OrdinalIgnoreCase),
                "Second drink shop must sell exactly ten glasses of orange juice: " + rendered);
            Check(rendered.Contains(vi
                    ? "Hỏi cả hai quán nước bán được tổng cộng bao nhiêu ly nước cam?"
                    : "How many glasses of orange juice did both drink shops sell altogether?",
                    StringComparison.OrdinalIgnoreCase),
                "Orange juice example must ask for the total sold by both drink shops: " + rendered);
            Check(contract.Answer == 15 && AppliedQuestionCatalogue.Validate(
                    QuestionBankStore.SerializeDraft(draft), contract).IsValid,
                "The rendered 5 + 10 drink shop story must validate and answer 15");
            string invalid = draft.Question.Replace(language == AppLanguage.Vietnamese ? "cả hai quán nước" : "both drink shops",
                language == AppLanguage.Vietnamese ? "cả hai cửa hàng" : "both shops", StringComparison.OrdinalIgnoreCase);
            Check(invalid != draft.Question && !FoodThemePrompt.CombinedQuestionNamesVenue(invalid, contract),
                "AI must reject generic stores in drink questions");
            Check(!AppliedQuestionCatalogue.Validate(QuestionBankStore.SerializeDraft(draft with { Question = invalid }), contract).IsValid,
                "Drink AI validation accepted a generic store");
        }
    }
    private static void CheckDessertShopAiValidation()
    {
        foreach (var unitId in new[] { "kem-bo-da-lat", "kem-trang-tien", "sua-chua-ha-long" })
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var subject = FoodThemeCatalogue.Subjects.Single(s => s.Unit.Id == unitId);
            var original = FoodThemeInterchangeTests.Contract(subject, ArithmeticOperation.Add, language, BankQuestionFamily.Arithmetic);
            var contract = original with { Left = 10, Right = 20 };
            Check(contract.IsValid && contract.Answer == 30, "Dessert shop 10 + 20 contract invalid: " + unitId);
            var draft = AppliedQuestionCatalogue.Draft(contract);
            CheckFoodVenue(contract, draft);
            var (first, second, both) = FoodThemePrompt.DessertShopActors(
                FoodThemeCatalogue.DessertShopKind(unitId)!, language);
            string guidance = FoodThemePrompt.Guidance(contract);
            Check(guidance.Contains("{unit}", StringComparison.Ordinal)
                && guidance.Contains("{unit_a}", StringComparison.Ordinal)
                && guidance.Contains("{unit_b}", StringComparison.Ordinal)
                && !guidance.Contains("{{unit}}", StringComparison.Ordinal),
                "Dessert AI guidance has escaped or missing unit placeholders: " + unitId);
            bool vi = language == AppLanguage.Vietnamese;
            string rendered = draft.ToWordProblem(contract).ProblemText;
            Check(rendered.Contains($"{first} {(vi ? "bán được" : "sold")} 10 {contract.Unit}", StringComparison.OrdinalIgnoreCase)
                && rendered.Contains($"{second} {(vi ? "bán được" : "sold")} 20 {contract.Unit}", StringComparison.OrdinalIgnoreCase),
                "Dessert shop must sell the two independent amounts: " + rendered);
            Check(rendered.Contains(both, StringComparison.OrdinalIgnoreCase)
                && rendered.Contains(vi ? "bán được tổng cộng bao nhiêu" : "sell altogether", StringComparison.OrdinalIgnoreCase),
                "Dessert shop question must ask for the total sold: " + rendered);
            Check(AppliedQuestionCatalogue.Validate(QuestionBankStore.SerializeDraft(draft), contract).IsValid,
                "Dessert shop AI reference must validate: " + unitId);
            string generic = draft.Question.Replace(both, vi ? "cả hai cửa hàng" : "both shops", StringComparison.OrdinalIgnoreCase);
            Check(generic != draft.Question && !FoodThemePrompt.CombinedQuestionNamesVenue(generic, contract)
                && !AppliedQuestionCatalogue.Validate(QuestionBankStore.SerializeDraft(draft with { Question = generic }), contract).IsValid,
                "AI must reject generic stores in dessert questions: " + unitId);
        }
    }
    private static IEnumerable<(string Unit, bool Bakery)> BakeryVenueCases()
    {
        yield return ("cakes", true);
        yield return ("pho", false);
        foreach (var id in TakeawayBakeryCakeIds)
            yield return (id, true);
        foreach (var dish in AdditionalPreparedDishes.Where(d => FoodThemeCatalogue.DessertShopKind(d.UnitId) is null))
            yield return (dish.UnitId, dish.Bakery);
    }
    private static void CheckAdditionalPreparedDishes()
    {
        foreach (var dish in AdditionalPreparedDishes)
        {
            Check(FoodThemeCatalogue.Subjects.Count(s => s.Unit.Id == dish.UnitId) == 1,
                "Prepared dish missing or duplicated: " + dish.UnitId);
            Check(FoodThemeCatalogue.IsBakeryFood(dish.UnitId) == dish.Bakery,
                "Prepared dish has the wrong selling venue: " + dish.UnitId);
            // Regression: a base steak ID must not capture a longer sauce variant.
            foreach (string scenePrefix in new[] { "theme-food-", "fraction-theme-food-" })
                Check(FoodThemeCatalogue.RestaurantDishForScene(scenePrefix + dish.UnitId + "-total")?.Unit.Id == dish.UnitId,
                    "Scene resolved to another prepared dish: " + scenePrefix + dish.UnitId);
            var fractionScene = FractionQuestionCatalogue.Find("fraction-theme-food-" + dish.UnitId + "-total");
            Check(fractionScene is not null && fractionScene.UnitA == dish.Vietnamese && fractionScene.EnUnitA == dish.Plural,
                "Fraction unit resolved to another prepared dish: " + dish.UnitId);
            if (dish.UnitId.StartsWith("steak-", StringComparison.Ordinal))
                Check(FoodThemeCatalogue.EnglishFractionUnit(fractionScene!.Id, dish.Plural, true, false).Contains(
                    dish.Singular, StringComparison.Ordinal), "Fraction English singular lost the steak sauce: " + dish.UnitId);
            foreach (string unit in new[] { dish.Vietnamese, dish.Singular, dish.Plural })
            {
                Check(FoodThemeCatalogue.IsThemedItem(unit), "Prepared dish not recognized by practice routing: " + unit);
                Check(QuizStoryArtwork.Select(true, false, [unit]) == $"quiz_story_{dish.ArtworkId}.png",
                    "Prepared dish unit selects wrong artwork: " + unit);
            }
            Check(QuizStoryArtwork.Select(true, false, [], artworkId: "theme-" + dish.UnitId) == $"quiz_story_{dish.ArtworkId}.png",
                "Prepared dish authored identity not registered: " + dish.UnitId);
            var registered = QuestionUnits.Find(dish.UnitId);
            Check(registered?.Vietnamese == dish.Vietnamese && registered.Singular == dish.Singular && registered.Plural == dish.Plural,
                "Prepared dish units unavailable to contracts: " + dish.UnitId);
            foreach (var tier in Enum.GetValues<CurriculumTier>())
            foreach (var operation in Enum.GetValues<ArithmeticOperation>())
            foreach (var family in new[] { BankQuestionFamily.Arithmetic, BankQuestionFamily.Fraction, BankQuestionFamily.FindX })
            {
                var scenes = QuestionAuthoringChoices.Scenes(family, operation, tier, QuestionKnowledgeGroup.Food);
                Check(scenes.Any(id => id.Contains("theme-food-" + dish.UnitId + "-", StringComparison.Ordinal)),
                    $"Prepared dish absent from UI/AI/Excel choices: {dish.UnitId}/{family}/{operation}/{tier}");
            }
        }
    }
    internal static void CheckFoodVenue(BasicQuestionContract contract, BasicQuestionDraft draft)
    {
        if (contract.KnowledgeGroup != QuestionKnowledgeGroup.Food) return;
        bool vi = contract.Language == AppLanguage.Vietnamese;
        bool bakery = FoodThemeCatalogue.IsBakeryContract(contract);
        string? resolvedDishId = contract.Version == FindXQuestionCatalogue.Version
            ? FindXQuestionCatalogue.Find(contract.SceneId)?.SourceScene.UnitId
            : FoodThemeCatalogue.RestaurantDishForScene(contract.SceneId)?.Unit.Id;
        foreach (var dish in AdditionalPreparedDishes.Where(d => d.UnitId == resolvedDishId))
        {
            Check(bakery == dish.Bakery, "Food contract has the wrong selling venue: " + contract.SceneId);
            Check(contract.Unit == (vi ? dish.Vietnamese : dish.Plural),
                "Food contract changed the counted serving: " + contract.SceneId + "/" + contract.Language);
        }
        if (FoodThemeCatalogue.DessertShopKind(contract) is { } dessert)
        {
            var (first, second, both) = FoodThemePrompt.DessertShopActors(dessert, contract.Language);
            Check(contract.Subject.Equals(first, StringComparison.OrdinalIgnoreCase),
                "Dessert contract must name first dessert shop: " + contract.SceneId);
            if (contract.Structure == BasicQuestionStructure.Combine)
                Check(contract.OtherSubject.Equals(second, StringComparison.OrdinalIgnoreCase),
                    "Combined dessert contract must name second dessert shop: " + contract.SceneId);
            string cheStory = draft.ToWordProblem(contract).ProblemText;
            Check(cheStory.Contains(first, StringComparison.OrdinalIgnoreCase),
                "Dessert story must name first dessert shop: " + contract.SceneId);
            if (contract.Structure == BasicQuestionStructure.Combine)
                Check(cheStory.Contains(second, StringComparison.OrdinalIgnoreCase)
                    && cheStory.Contains(both, StringComparison.OrdinalIgnoreCase),
                    "Combined dessert story must name both dessert shops: " + contract.SceneId);
            Check(!cheStory.Contains(vi ? "cửa hàng" : "the two businesses", StringComparison.OrdinalIgnoreCase),
                "Dessert story replaced a dessert shop with a generic store");
            string chePrompt = BasicQuestionPrompt.Build(contract);
            Check(chePrompt.Contains(vi ? "Đề món " + dessert : "Vietnamese " + dessert + " stories", StringComparison.Ordinal),
                "Dessert AI prompt lost its approved shop scenario: " + contract.SceneId);
            return;
        }
        var venues = Enumerable.Range(0, FoodThemePrompt.VenueCount(bakery))
            .Select(i => FoodThemePrompt.Venue(bakery, i, contract.Language)).ToArray();
        string ActorVenue(string v) => vi ? v + " của " : "'s " + v;
        var matching = venues.Where(v => contract.Subject.Contains(ActorVenue(v), StringComparison.OrdinalIgnoreCase)).ToArray();
        Check(matching.Length == 1,
            $"Prepared food requires one valid venue: {contract.SceneId}/{contract.Language}: {contract.Subject}");
        string venue = matching[0];
        if (!string.IsNullOrWhiteSpace(contract.OtherSubject))
            Check(contract.OtherSubject.Contains(ActorVenue(venue), StringComparison.OrdinalIgnoreCase),
                $"Food owners must have identical venue kinds: {contract.SceneId}/{contract.Language}: {contract.Subject}, {contract.OtherSubject}");
        if (bakery)
        {
            Check(!contract.Subject.Contains(vi ? "quán ăn" : "'s eatery", StringComparison.OrdinalIgnoreCase),
                "Bakery question received restaurant wording: " + contract.SceneId);
            Check(contract.Unit.Contains(vi ? "bánh kem" : "cream cake", StringComparison.OrdinalIgnoreCase)
                || !contract.SceneId.Contains("food-cakes-", StringComparison.Ordinal),
                "Cake question must count cream cakes: " + contract.SceneId);
        }
        string story = draft.ToWordProblem(contract).ProblemText;
        Check(story.Contains(contract.Subject, StringComparison.OrdinalIgnoreCase),
            $"Rendered prepared-food story omits owner venue: {contract.SceneId}/{contract.Language}: {story}");
        if (TakeawayBakeryCakeIds.Any(id => contract.SceneId.Contains("food-" + id + "-", StringComparison.Ordinal)))
        {
            foreach (string restaurantTerm in vi ? new[] { "quán ăn", "nhà hàng", "phục vụ", "bàn ăn" }
                : new[] { "eatery", "restaurant", "diner", "serve at tables", "dining table" })
                Check(!story.Contains(restaurantTerm, StringComparison.OrdinalIgnoreCase),
                    $"Takeaway bakery cake uses restaurant wording: {contract.SceneId}/{contract.Language}: {story}");
            Check(story.Contains(vi ? "bán" : "sell", StringComparison.OrdinalIgnoreCase)
                || story.Contains(vi ? "chuẩn bị" : "prepare", StringComparison.OrdinalIgnoreCase),
                $"Takeaway bakery cake lacks selling/preparing context: {contract.SceneId}/{contract.Language}: {story}");
        }
        if (contract.Structure == BasicQuestionStructure.Combine)
        {
            Check(story.Contains(contract.OtherSubject, StringComparison.OrdinalIgnoreCase),
                "Combined food story omits second matching venue: " + story);
            string pair = vi ? "cả hai " + venue : "the two " + (venue switch
            {
                "bakery" => "bakeries", "cake shop" => "cake shops", "eatery" => "eateries",
                "restaurant" => "restaurants", _ => "diners"
            });
            Check(story.Contains(pair, StringComparison.OrdinalIgnoreCase),
                "Combined food question must name both matching businesses: " + story);
        }
        string prompt = BasicQuestionPrompt.Build(contract);
        string instruction = bakery
            ? (vi ? "Bánh được làm hoặc bán tại" : "Cakes, pastries and bakery items are made or sold")
            : (vi ? "Tất cả món ăn đã chế biến được phục vụ" : "All prepared dishes are served");
        Check(prompt.Split(instruction, StringSplitOptions.None).Length == 2,
            $"Prepared-food AI guidance missing or duplicated: {contract.SceneId}/{contract.Language}");
        if (contract.Structure == BasicQuestionStructure.Combine)
            Check(prompt.Contains(contract.Subject) && prompt.Contains(contract.OtherSubject)
                && prompt.Contains(vi ? "Hai cửa hàng trong cùng một đề" : "Both owners run the same kind of venue"),
                "Combined food AI prompt must keep the venue relation");
    }
    private static void CheckArtwork(BasicQuestionContract contract, BasicQuestionDraft draft, string expected)
    {
        Check(contract.IsValid, "Invalid source: " + contract.SceneId);
        var word = draft.ToWordProblem(contract);
        Check(word.StoryArtworkId == expected, $"Wrong subject image for {contract.SceneId}: {word.StoryArtworkId} != {expected}");
        Check(QuizStoryArtwork.Select(true, word.FactTable is not null, [word.AnswerUnit],
            itemUnit: word.ItemUnit, artworkId: word.StoryArtworkId) == $"quiz_story_{expected}.png",
            "Image hidden or incorrect: " + contract.SceneId);
    }
    private static int CheckAnimalArtwork()
    {
        var animals = new (string UnitId, string ArtworkId)[]
        {
            ("chickens", "chicken"), ("ducks", "duck"), ("birds", "bird"), ("fish", "fish"),
            ("cows", "cow"), ("horses", "horse"), ("deer", "deer"), ("dogs", "dog"),
            ("cats", "cat"), ("pigs", "pig"),
            ("rabbits", "rabbit"), ("turtles", "turtle"), ("tortoises", "tortoise"), ("geese", "goose")
        };
        int checkedCount = 0;
        foreach (var (id, art) in animals)
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var op in Enum.GetValues<ArithmeticOperation>())
        {
            var profile = new QuestionLearningProfile(QuestionKnowledgeGroup.Animals);
            var scene = AppliedQuestionCatalogue.All.Single(s => s.Group == QuestionKnowledgeGroup.Animals
                && s.UnitId == id && s.Operation == op);
            Check(QuestionUnits.Find(id) is not null, "Unknown animal unit: " + id);
            Check(QuizStoryArtworkCatalogue.AppliedSceneArtwork(scene) == art, "Wrong animal identity: " + id);
            Check(QuizStoryArtworkCatalogue.AppliedSceneArtwork(scene with { Group = QuestionKnowledgeGroup.Objects }) is null,
                "Animal art escaped its knowledge group: " + id);
            var c = AppliedQuestionCatalogue.Create(profile, op, CurriculumTier.ThreeStars, language, new(29), scene.Id);
            CheckArtwork(c, AppliedQuestionCatalogue.Draft(c), art);
            foreach (var findX in FindXQuestionCatalogue.All.Where(s => s.SourceScene.Id == scene.Id))
            {
                var x = FindXQuestionCatalogue.Create(profile, findX.EquationOperation,
                    CurriculumTier.ThreeStars, language, new(31), findX.Role, findX.Id);
                CheckArtwork(x, FindXQuestionCatalogue.Draft(x), art);
            }
            checkedCount++;
        }
        return checkedCount;
    }
    private static int CheckNewDishArtwork()
    {
        var dishes = new (string UnitId, string ArtworkId)[]
        {
            ("com-tam", "com_tam"), ("op-la", "op_la"),
            ("thit-kho-trung", "thit_kho_trung"), ("banh-xeo", "banh_xeo"),
            ("bun-thit-nuong-cha-gio", "bun_thit_nuong_cha_gio"),
            ("hu-tieu-nam-vang", "hu_tieu_nam_vang"), ("mi-quang", "mi_quang"),
            ("bun-bo-hue", "bun_bo_hue"), ("banh-cuon", "banh_cuon"),
            ("banh-chung", "banh_chung"), ("banh-day", "banh_day"),
            ("banh-tet", "banh_tet"), ("banh-beo", "banh_beo"),
            ("banh-khot", "banh_khot"), ("banh-bot-loc", "banh_bot_loc"),
            ("banh-gio", "banh_gio"),
            ("banh-bao-thit-trung-cut", "banh_bao_thit_trung_cut"),
            ("ha-cao", "ha_cao"),
            ("kimchi", "kimchi"), ("sushi", "sushi"),
            ("banh-cam", "banh_cam"), ("banh-com", "banh_com"),
            ("banh-trung-thu", "banh_trung_thu"), ("banh-pia", "banh_pia"),
            ("croissants", "croissant"), ("banh-da-lon", "banh_da_lon"),
            ("banh-bo", "banh_bo"), ("muffins", "muffin"),
            ("cupcakes", "cupcake"), ("cookies", "cookie"),
            ("donuts", "donut"), ("waffles", "waffle"),
            ("apple-pies", "apple_pie"), ("flans", "flan")
        };
        int verified = 0;
        foreach (var (id, artwork) in dishes.Concat(AdditionalPreparedDishes.Select(d => (d.UnitId, d.ArtworkId))))
        {
            var subject = FoodThemeCatalogue.Subjects.Single(s => s.Unit.Id == id);
            Check(subject.Group == QuestionKnowledgeGroup.Food && subject.ArtworkId == artwork,
                "Incorrect food group or image mapping: " + id);
            if (id is "banh-chung" or "banh-gio" or "banh-day" or "banh-bao-thit-trung-cut" or "banh-tet")
                Check(FoodThemeCatalogue.IsBakeryFood(id),
                    "Traditional takeaway cake must belong to a bakery/cake shop: " + id);
            else if (id is "banh-beo" or "banh-khot" or "banh-bot-loc" or "ha-cao")
                Check(!FoodThemeCatalogue.IsBakeryFood(id),
                    "Traditional prepared dish must belong to a restaurant/eatery/diner: " + id);
            foreach (var language in Enum.GetValues<AppLanguage>())
            foreach (var operation in Enum.GetValues<ArithmeticOperation>())
            foreach (var family in new[] { BankQuestionFamily.Arithmetic, BankQuestionFamily.Fraction, BankQuestionFamily.FindX })
            {
                var contract = FoodThemeInterchangeTests.Contract(subject, operation, language, family);
                var draft = BasicQuestionTemplates.Example(contract);
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft), contract).IsValid,
                    $"Invalid authored/AI example: {id}/{family}/{operation}/{language}");
                CheckFoodVenue(contract, draft);
                CheckArtwork(contract, draft, artwork);
                verified++;
            }
        }
        return verified;
    }
    public static void Run(string? wordingOutput = null)
    {
        PreparedDishVenueTests.Run(wordingOutput);
        CheckAdditionalPreparedDishes();
        CheckAllFoodVenueKinds();
        CheckAiCombinedVenueValidation();
        CheckDrinkShopAiValidation();
        CheckDessertShopAiValidation();
        Check(!FoodThemeCatalogue.IsBakeryFood("hamburgers"),
            "Hamburgers must use an eatery or restaurant, not a bakery");
        Check(!FoodThemeCatalogue.IsBakeryFood("banh-xeo") && !FoodThemeCatalogue.IsBakeryFood("banh-cuon"),
            "Banh xeo and banh cuon must use an eatery/restaurant rather than a bakery");
        Check(FoodThemeCatalogue.IsBakeryFood("cakes") && FoodThemeCatalogue.IsBakeryFood("flans"),
            "Desserts must use a named bakery or cake shop");
        int count = 0;
        int animalCount = CheckAnimalArtwork();
        int dishCount = CheckNewDishArtwork();
        foreach (var subject in FoodThemeCatalogue.Subjects)
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var matched = QuizStoryArtwork.Select(true,false,[subject.Unit.Item(language)]);
            Check(matched == $"quiz_story_{subject.ArtworkId}.png", $"Exact counted subject picture: {subject.Unit.Id}/{language}: expected {subject.ArtworkId}, got {matched ?? "<none>"} for '{subject.Unit.Item(language)}'");
        }
        foreach (var (name, value) in new[] { ("Food",12),("Drinks",13),("Fruit",14) })
        {
            Check(Enum.TryParse<QuestionKnowledgeGroup>(name, out var group) && (int)group == value, "Missing stable knowledge group: " + name);
            Check(QuestionLearningProfile.Groups().Contains(group), "Group missing from shared practice/AI/PDF/Excel choices");
            foreach (var language in Enum.GetValues<AppLanguage>())
            foreach (var tier in Enum.GetValues<CurriculumTier>())
            foreach (var op in Enum.GetValues<ArithmeticOperation>())
            {
                var profile = new QuestionLearningProfile(group);
                var c = AppliedQuestionCatalogue.Create(profile, op, tier, language, new(3));
                Check(c.IsValid && c.KnowledgeGroup == group, "Concrete theme contract " + name);
                var draft = AppliedQuestionCatalogue.Draft(c);
                var source = AppliedQuestionCatalogue.Find(c.SceneId)!;
                var subject = FoodThemeCatalogue.Subjects.Single(s => s.Group == group && s.Unit.Id == source.UnitId);
                CheckArtwork(c, draft, subject.ArtworkId);
                CheckFoodVenue(c, draft);
                Check(draft.ToWordProblem(c).Quantity==WordProblemQuantity.Count,"A counted food/drink/fruit story must not be graded as distance");
                Check(AppliedQuestionCatalogue.Validate(QuestionBankStore.SerializeDraft(draft), c).IsValid, "Standard AI template syntax/meaning");
                Check(OpenQuestionProse.Supports(c), "New theme inherits conservative open prose support");
                Check(OpenQuestionProse.Validate(QuestionBankStore.SerializeDraft(draft), c).IsValid, "Known correct theme expression must verify");
                Check(!OpenQuestionProse.Validate(QuestionBankStore.SerializeDraft(draft with { Question = draft.Question + (language == AppLanguage.English ? " Do not find this amount." : " Không tìm lượng này.") }), c).IsValid, "Changed/negated target must not auto-verify");
                var f = FractionQuestionCatalogue.Create(profile, op, tier, language, new(5));
                Check(f.IsValid && f.KnowledgeGroup == group, "Exact fraction theme");
                if(op==ArithmeticOperation.Subtract)
                    Check(BasicQuestionPrompt.Build(f).Contains(language==AppLanguage.Vietnamese?"hai lượng riêng":"two separate amounts"),"Fraction comparison must not receive removal/remaining guidance");
                if(language==AppLanguage.English)
                    Check(FractionQuestionCatalogue.Render("{a} {unit_a}",f).Contains(" of a"),"English proper fractions need a singular object/volume phrase");
                CheckArtwork(f, FractionQuestionCatalogue.Draft(f),
                    QuizStoryArtworkCatalogue.FractionArtwork(f.SceneId)!);
                CheckFoodVenue(f, FractionQuestionCatalogue.Draft(f));
                var roles = FindXQuestionCatalogue.Available(profile, op, tier).ToArray();
                Check(roles.Length > 0, "Find X theme roles available");
                var x = FindXQuestionCatalogue.Create(profile, op, tier, language, new(7));
                Check(x.IsValid && x.KnowledgeGroup == group, "Find X theme contract");
                var xScene = FindXQuestionCatalogue.Find(x.SceneId)!.SourceScene;
                var xSubject = FoodThemeCatalogue.Subjects.Single(s => s.Group == group && s.Unit.Id == xScene.UnitId);
                CheckArtwork(x, FindXQuestionCatalogue.Draft(x), xSubject.ArtworkId);
                CheckFoodVenue(x, FindXQuestionCatalogue.Draft(x));
                count++;
            }
        }
        Console.WriteLine($"PASS animal images: {animalCount} bilingual species/operation fixtures with Find X; new dish images: {dishCount} bilingual operation/family/AI fixtures; food/drink/fruit images: {count} bilingual group/operation/tier fixtures across arithmetic, fractions, Find X and AI relation checks.");
    }
}
