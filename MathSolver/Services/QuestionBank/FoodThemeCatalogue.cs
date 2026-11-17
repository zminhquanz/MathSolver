using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>Authored subjects and units. Country examples never determine a mathematical relation.</summary>
public static class FoodThemeCatalogue
{
    public sealed record Subject(QuestionKnowledgeGroup Group, QuestionUnit Unit, string ArtworkId,
        string? FractionVietnamese = null, string? FractionEnglish = null, string? FractionArtworkId = null);
    private static QuestionUnit Unit(string id, string vi, string one, string many) => new(id, vi, one, many, "khay", "tray", "trays");
    public static IReadOnlyList<Subject> Subjects { get; } = Array.AsReadOnly<Subject>(
    [
        .. BreadQuestionCatalogue.Types.Select(t => new Subject(QuestionKnowledgeGroup.Food, t.Unit, t.ArtworkId)),
        new(QuestionKnowledgeGroup.Food, Unit("cakes","chiếc bánh kem","cream cake","cream cakes"),"cake"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-cam","chiếc bánh cam","Vietnamese sesame ball","Vietnamese sesame balls"),"banh_cam"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-com","chiếc bánh cốm","green rice cake","green rice cakes"),"banh_com"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-trung-thu","chiếc bánh trung thu","mooncake","mooncakes"),"banh_trung_thu"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-pia","chiếc bánh pía","pia pastry","pia pastries"),"banh_pia"),
        new(QuestionKnowledgeGroup.Food, Unit("croissants","chiếc bánh croissant","croissant","croissants"),"croissant"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-da-lon","miếng bánh da lợn","layered steamed cake slice","layered steamed cake slices"),"banh_da_lon"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-bo","chiếc bánh bò","Vietnamese honeycomb cake","Vietnamese honeycomb cakes"),"banh_bo"),
        new(QuestionKnowledgeGroup.Food, Unit("muffins","chiếc bánh muffin","muffin","muffins"),"muffin"),
        new(QuestionKnowledgeGroup.Food, Unit("cupcakes","chiếc bánh cupcake","cupcake","cupcakes"),"cupcake"),
        new(QuestionKnowledgeGroup.Food, Unit("cookies","chiếc bánh cookie","cookie","cookies"),"cookie"),
        new(QuestionKnowledgeGroup.Food, Unit("donuts","chiếc bánh donut","donut","donuts"),"donut"),
        new(QuestionKnowledgeGroup.Food, Unit("waffles","chiếc bánh waffle","waffle","waffles"),"waffle"),
        new(QuestionKnowledgeGroup.Food, Unit("apple-pies","chiếc bánh táo","apple pie","apple pies"),"apple_pie"),
        new(QuestionKnowledgeGroup.Food, Unit("flans","chiếc bánh flan","crème caramel","crème caramels"),"flan"),
        new(QuestionKnowledgeGroup.Food, Unit("biscuits","chiếc bánh quy","biscuit","biscuits"),"biscuit"),
        new(QuestionKnowledgeGroup.Food, Unit("eggs","quả trứng","egg","eggs"),"egg"),
        new(QuestionKnowledgeGroup.Food, Unit("candies","viên kẹo","sweet","sweets"),"candy"),
        new(QuestionKnowledgeGroup.Food, Unit("pho","tô phở","bowl of pho","bowls of pho"),"pho"),
        new(QuestionKnowledgeGroup.Food, Unit("lamingtons","chiếc bánh lamington","lamington","lamingtons"),"lamington"),
        new(QuestionKnowledgeGroup.Food, Unit("fish-chips","suất cá chiên và khoai tây","fish and chips meal","fish and chips meals"),"fish_chips"),
        new(QuestionKnowledgeGroup.Food, Unit("hamburgers","chiếc hamburger","hamburger","hamburgers"),"hamburger"),
        new(QuestionKnowledgeGroup.Food, Unit("pancakes","chiếc bánh pancake","pancake","pancakes"),"pancake"),
        new(QuestionKnowledgeGroup.Food, Unit("com-tam","suất cơm tấm","serving of broken rice","servings of broken rice"),"com_tam"),
        new(QuestionKnowledgeGroup.Food, Unit("op-la","phần trứng ốp-la","serving of fried eggs","servings of fried eggs"),"op_la"),
        new(QuestionKnowledgeGroup.Food, Unit("thit-kho-trung","phần thịt kho trứng","serving of braised pork and eggs","servings of braised pork and eggs"),"thit_kho_trung"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-xeo","chiếc bánh xèo","Vietnamese sizzling pancake","Vietnamese sizzling pancakes"),"banh_xeo"),
        // Five wrapped/steamed cakes are sold at bakeries; banh beo, banh khot,
        // banh bot loc and ha cao remain prepared dishes at eateries.
        new(QuestionKnowledgeGroup.Food, Unit("banh-chung","chiếc bánh chưng","square sticky rice cake","square sticky rice cakes"),"banh_chung"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-day","chiếc bánh dày","round sticky rice cake","round sticky rice cakes"),"banh_day"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-tet","đòn bánh tét","cylindrical sticky rice cake","cylindrical sticky rice cakes"),"banh_tet"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-beo","chén bánh bèo","small dish of steamed rice cake","small dishes of steamed rice cakes"),"banh_beo"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-khot","chiếc bánh khọt","mini savory rice pancake","mini savory rice pancakes"),"banh_khot"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-bot-loc","chiếc bánh bột lọc","translucent tapioca dumpling","translucent tapioca dumplings"),"banh_bot_loc"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-gio","chiếc bánh giò","pyramid steamed rice dumpling","pyramid steamed rice dumplings"),"banh_gio"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-bao-thit-trung-cut","chiếc bánh bao nhân thịt và trứng cút","pork and quail egg steamed bun","pork and quail egg steamed buns"),"banh_bao_thit_trung_cut"),
        new(QuestionKnowledgeGroup.Food, Unit("ha-cao","viên há cảo","steamed shrimp dumpling","steamed shrimp dumplings"),"ha_cao"),
        new(QuestionKnowledgeGroup.Food, Unit("bun-thit-nuong-cha-gio","tô bún thịt nướng chả giò","bowl of grilled pork vermicelli with spring rolls","bowls of grilled pork vermicelli with spring rolls"),"bun_thit_nuong_cha_gio"),
        new(QuestionKnowledgeGroup.Food, Unit("hu-tieu-nam-vang","tô hủ tiếu Nam Vang","bowl of Nam Vang noodle soup","bowls of Nam Vang noodle soup"),"hu_tieu_nam_vang"),
        new(QuestionKnowledgeGroup.Food, Unit("mi-quang","tô mì Quảng","bowl of Quang noodles","bowls of Quang noodles"),"mi_quang"),
        new(QuestionKnowledgeGroup.Food, Unit("bun-bo-hue","tô bún bò Huế","bowl of Hue beef noodle soup","bowls of Hue beef noodle soup"),"bun_bo_hue"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-cuon","đĩa bánh cuốn","plate of steamed rice rolls","plates of steamed rice rolls"),"banh_cuon"),
        new(QuestionKnowledgeGroup.Food, Unit("kimchi","đĩa kim chi","plate of kimchi","plates of kimchi"),"kimchi"),
        new(QuestionKnowledgeGroup.Food, Unit("sushi","phần sushi","serving of sushi","servings of sushi"),"sushi"),
        new(QuestionKnowledgeGroup.Food, Unit("bot-chien","phần bột chiên","serving of Vietnamese fried rice flour cakes","servings of Vietnamese fried rice flour cakes"),"bot_chien"),
        new(QuestionKnowledgeGroup.Food, Unit("pizzas","chiếc bánh pizza","pizza","pizzas"),"pizza"),
        new(QuestionKnowledgeGroup.Food, Unit("mixed-salad","phần salad trộn","serving of mixed salad","servings of mixed salad"),"mixed_salad"),
        new(QuestionKnowledgeGroup.Food, Unit("spaghetti","đĩa mì Ý","plate of spaghetti","plates of spaghetti"),"spaghetti"),
        new(QuestionKnowledgeGroup.Food, Unit("hot-dogs","chiếc bánh hot dog","hot dog","hot dogs"),"hot_dog"),
        new(QuestionKnowledgeGroup.Food, Unit("udon","tô mì udon","bowl of udon","bowls of udon"),"udon"),
        new(QuestionKnowledgeGroup.Food, Unit("goi-cuon","đĩa gỏi cuốn","plate of fresh spring rolls","plates of fresh spring rolls"),"goi_cuon"),
        new(QuestionKnowledgeGroup.Food, Unit("bun-cha","suất bún chả","serving of bun cha","servings of bun cha"),"bun_cha"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-trang-tron","phần bánh tráng trộn","serving of Vietnamese rice paper salad","servings of Vietnamese rice paper salad"),"banh_trang_tron"),
        new(QuestionKnowledgeGroup.Food, Unit("banh-trang-nuong","chiếc bánh tráng nướng","grilled Vietnamese rice paper","grilled Vietnamese rice papers"),"banh_trang_nuong"),
        new(QuestionKnowledgeGroup.Food, Unit("pha-lau-bo","phần phá lấu bò","serving of Vietnamese beef offal stew","servings of Vietnamese beef offal stew"),"pha_lau_bo"),
        new(QuestionKnowledgeGroup.Food, Unit("ca-ri-ga-viet","phần cà ri gà Việt Nam","serving of Vietnamese chicken curry","servings of Vietnamese chicken curry"),"ca_ri_ga_viet"),
        new(QuestionKnowledgeGroup.Food, Unit("ca-ri-bo-nhat","phần cà ri bò Nhật Bản","serving of Japanese beef curry","servings of Japanese beef curry"),"ca_ri_bo_nhat"),
        new(QuestionKnowledgeGroup.Food, Unit("tau-hu-nuoc-duong","chén tàu hũ nước đường gừng và nước cốt dừa","bowl of tofu pudding with ginger syrup and coconut milk","bowls of tofu pudding with ginger syrup and coconut milk"),"tau_hu_nuoc_duong"),
        new(QuestionKnowledgeGroup.Food, Unit("jjinppang","chiếc bánh Jjinppang nhân đậu đỏ","Korean jjinppang red bean bun","Korean jjinppang red bean buns"),"jjinppang"),
        new(QuestionKnowledgeGroup.Food, Unit("chapssal-doughnut","chiếc bánh Chapssal doughnut","Korean chapssal doughnut","Korean chapssal doughnuts"),"chapssal_doughnut"),
        new(QuestionKnowledgeGroup.Food, Unit("bibimbap","tô cơm trộn Hàn Quốc","bowl of Korean bibimbap","bowls of Korean bibimbap"),"bibimbap"),
        new(QuestionKnowledgeGroup.Food, Unit("steak-bo","suất steak bò","serving of beef steak","servings of beef steak"),"steak_bo"),
        new(QuestionKnowledgeGroup.Food, Unit("steak-ga","suất steak gà","serving of chicken steak","servings of chicken steak"),"steak_ga"),
        new(QuestionKnowledgeGroup.Food, Unit("steak-cuu","suất steak cừu","serving of lamb steak","servings of lamb steak"),"steak_cuu"),
        new(QuestionKnowledgeGroup.Food, Unit("steak-bo-sot-tieu","suất steak bò sốt tiêu","serving of beef steak with peppercorn sauce","servings of beef steak with peppercorn sauce"),"steak_bo_sot_tieu"),
        new(QuestionKnowledgeGroup.Food, Unit("steak-ga-sot-kem-nam","suất steak gà sốt kem nấm","serving of chicken steak with creamy mushroom sauce","servings of chicken steak with creamy mushroom sauce"),"steak_ga_sot_kem_nam"),
        new(QuestionKnowledgeGroup.Food, Unit("steak-bo-sot-ruou-vang-do","suất steak bò sốt rượu vang đỏ","serving of beef steak with red wine sauce","servings of beef steak with red wine sauce"),"steak_bo_sot_ruou_vang_do"),
        new(QuestionKnowledgeGroup.Food, Unit("steak-bo-sot-bearnaise","suất steak bò sốt béarnaise","serving of beef steak with béarnaise sauce","servings of beef steak with béarnaise sauce"),"steak_bo_sot_bearnaise"),
        new(QuestionKnowledgeGroup.Food, Unit("steak-cuu-sot-chimichurri","suất steak cừu sốt chimichurri","serving of lamb steak with chimichurri sauce","servings of lamb steak with chimichurri sauce"),"steak_cuu_sot_chimichurri"),
        new(QuestionKnowledgeGroup.Food, Unit("steak-ga-sot-bbq","suất steak gà sốt BBQ","serving of chicken steak with BBQ sauce","servings of chicken steak with BBQ sauce"),"steak_ga_sot_bbq"),
        new(QuestionKnowledgeGroup.Food, Unit("steak-cuu-sot-bo-toi","suất steak cừu sốt bơ tỏi thảo mộc","serving of lamb steak with garlic herb butter","servings of lamb steak with garlic herb butter"),"steak_cuu_sot_bo_toi"),
        new(QuestionKnowledgeGroup.Food, Unit("tteokbokki","đĩa bánh gạo cay Tteokbokki","plate of Korean spicy rice cakes (tteokbokki)","plates of Korean spicy rice cakes (tteokbokki)"),"tteokbokki"),
        new(QuestionKnowledgeGroup.Food, Unit("korean-corn-dog","chiếc Korean Corn Dog","Korean corn dog","Korean corn dogs"),"korean_corn_dog"),
        new(QuestionKnowledgeGroup.Food, Unit("hotteok","chiếc bánh Hotteok","Korean hotteok sweet pancake","Korean hotteok sweet pancakes"),"hotteok"),
        new(QuestionKnowledgeGroup.Food, Unit("bungeoppang","chiếc bánh Bungeoppang","Korean bungeoppang fish-shaped pastry","Korean bungeoppang fish-shaped pastries"),"bungeoppang"),
        new(QuestionKnowledgeGroup.Food, Unit("gyeranppang","chiếc bánh Gyeranppang","Korean gyeranppang egg bread","Korean gyeranppang egg breads"),"gyeranppang"),
        new(QuestionKnowledgeGroup.Food, Unit("sundae-gukbap","tô Sundae Gukbap","bowl of Korean blood sausage rice soup (sundae gukbap)","bowls of Korean blood sausage rice soup (sundae gukbap)"),"sundae_gukbap"),
        new(QuestionKnowledgeGroup.Food, Unit("andong-jjimdak","phần Andong Jjimdak","serving of Andong Jjimdak (Korean braised chicken)","servings of Andong Jjimdak (Korean braised chicken)"),"andong_jjimdak"),
        new(QuestionKnowledgeGroup.Food, Unit("kalguksu","tô Kalguksu","bowl of Korean knife-cut noodle soup (kalguksu)","bowls of Korean knife-cut noodle soup (kalguksu)"),"kalguksu"),
        new(QuestionKnowledgeGroup.Food, Unit("squid-sundae","phần Squid Sundae","serving of Squid Sundae (Korean stuffed squid)","servings of Squid Sundae (Korean stuffed squid)"),"squid_sundae"),
        new(QuestionKnowledgeGroup.Food, Unit("com-suon-cay","suất cơm sườn cây","serving of rice with grilled pork ribs","servings of rice with grilled pork ribs"),"com_suon_cay"),
        new(QuestionKnowledgeGroup.Food, Unit("com-chien-hai-san","đĩa cơm chiên hải sản","plate of seafood fried rice","plates of seafood fried rice"),"com_chien_hai_san"),
        new(QuestionKnowledgeGroup.Food, Unit("com-chien-duong-chau","đĩa cơm chiên Dương Châu","plate of Yangzhou fried rice","plates of Yangzhou fried rice"),"com_chien_duong_chau"),
        new(QuestionKnowledgeGroup.Food, Unit("com-ga-xoi-mo","suất cơm gà xối mỡ","serving of crispy fried chicken with rice","servings of crispy fried chicken with rice"),"com_ga_xoi_mo"),
        new(QuestionKnowledgeGroup.Food, Unit("com-ga-sot-me","suất cơm gà sốt me","serving of chicken with tamarind sauce and rice","servings of chicken with tamarind sauce and rice"),"com_ga_sot_me"),
        new(QuestionKnowledgeGroup.Food, Unit("com-ga-sot-tieu-xanh","suất cơm gà sốt tiêu xanh","serving of chicken with green peppercorn sauce and rice","servings of chicken with green peppercorn sauce and rice"),"com_ga_sot_tieu_xanh"),
        new(QuestionKnowledgeGroup.Food, Unit("com-ga-mam-toi","suất cơm gà mắm tỏi","serving of chicken rice with garlic fish sauce","servings of chicken rice with garlic fish sauce"),"com_ga_mam_toi"),
        new(QuestionKnowledgeGroup.Food, Unit("kem-bo-da-lat","ly kem bơ Đà Lạt","glass of Da Lat avocado ice cream","glasses of Da Lat avocado ice cream"),"kem_bo_da_lat"),
        new(QuestionKnowledgeGroup.Food, Unit("kem-trang-tien","que kem Tràng Tiền","Trang Tien ice cream bar","Trang Tien ice cream bars"),"kem_trang_tien"),
        new(QuestionKnowledgeGroup.Food, Unit("sua-chua-ha-long","cốc sữa chua Hạ Long","cup of Ha Long yogurt","cups of Ha Long yogurt"),"sua_chua_ha_long"),
        new(QuestionKnowledgeGroup.Food, Unit("canh-chua-ca-loc","tô canh chua cá lóc","bowl of sour snakehead fish soup","bowls of sour snakehead fish soup"),"canh_chua_ca_loc"),
        new(QuestionKnowledgeGroup.Food, Unit("canh-kho-qua-nhoi-thit","tô canh khổ qua nhồi thịt","bowl of stuffed bitter melon soup","bowls of stuffed bitter melon soup"),"canh_kho_qua_nhoi_thit"),
        new(QuestionKnowledgeGroup.Food, Unit("canh-bi-do-ham-xuong","tô canh bí đỏ hầm xương","bowl of pumpkin and pork bone soup","bowls of pumpkin and pork bone soup"),"canh_bi_do_ham_xuong"),
        new(QuestionKnowledgeGroup.Food, Unit("canh-cua-rau-day-mong-toi","tô canh cua rau đay, mồng tơi","bowl of crab soup with jute mallow and Malabar spinach","bowls of crab soup with jute mallow and Malabar spinach"),"canh_cua_rau_day_mong_toi"),
        new(QuestionKnowledgeGroup.Food, Unit("canh-rau-ngot-thit-bam","tô canh rau ngót thịt băm","bowl of sweet leaf soup with minced pork","bowls of sweet leaf soup with minced pork"),"canh_rau_ngot_thit_bam"),
        new(QuestionKnowledgeGroup.Food, Unit("ca-loc-kho-to","phần cá lóc kho tộ","serving of claypot braised snakehead fish","servings of claypot braised snakehead fish"),"ca_loc_kho_to"),
        new(QuestionKnowledgeGroup.Food, Unit("che-dau-do","chén chè đậu đỏ","bowl of red bean sweet soup","bowls of red bean sweet soup"),"che_dau_do"),
        new(QuestionKnowledgeGroup.Food, Unit("che-dau-den","chén chè đậu đen","bowl of black bean sweet soup","bowls of black bean sweet soup"),"che_dau_den"),
        new(QuestionKnowledgeGroup.Food, Unit("che-dau-xanh","chén chè đậu xanh","bowl of mung bean sweet soup","bowls of mung bean sweet soup"),"che_dau_xanh"),
        new(QuestionKnowledgeGroup.Food, Unit("che-troi-nuoc","chén chè trôi nước","bowl of glutinous rice ball sweet soup","bowls of glutinous rice ball sweet soup"),"che_troi_nuoc"),
        new(QuestionKnowledgeGroup.Food, Unit("che-thap-cam","chén chè thập cẩm","bowl of mixed Vietnamese sweet soup","bowls of mixed Vietnamese sweet soup"),"che_thap_cam"),
        new(QuestionKnowledgeGroup.Food, Unit("che-thai","chén chè Thái","bowl of Thai-style fruit sweet soup","bowls of Thai-style fruit sweet soup"),"che_thai"),
        new(QuestionKnowledgeGroup.Food, Unit("che-chuoi","chén chè chuối","bowl of banana and coconut sweet soup","bowls of banana and coconut sweet soup"),"che_chuoi"),
        new(QuestionKnowledgeGroup.Food, Unit("che-bap","chén chè bắp","bowl of sweet corn pudding","bowls of sweet corn pudding"),"che_bap"),
        new(QuestionKnowledgeGroup.Food, Unit("che-hat-sen-long-nhan","chén chè hạt sen long nhãn","bowl of lotus seed and longan sweet soup","bowls of lotus seed and longan sweet soup"),"che_hat_sen_long_nhan"),
        new(QuestionKnowledgeGroup.Food, Unit("che-dau-phong","chén chè đậu phộng","bowl of peanut sweet soup","bowls of peanut sweet soup"),"che_dau_phong"),
        new(QuestionKnowledgeGroup.Food, Unit("che-khuc-bach","chén chè khúc bạch","bowl of almond panna cotta sweet soup","bowls of almond panna cotta sweet soup"),"che_khuc_bach"),
        new(QuestionKnowledgeGroup.Food, Unit("che-suong-sa-hat-luu","chén chè sương sa hạt lựu","bowl of jelly and pomegranate-seed sweet soup","bowls of jelly and pomegranate-seed sweet soup"),"che_suong_sa_hat_luu"),
        new(QuestionKnowledgeGroup.Food, Unit("che-khoai-mon","chén chè khoai môn","bowl of taro sweet soup","bowls of taro sweet soup"),"che_khoai_mon"),
        new(QuestionKnowledgeGroup.Drinks, Unit("drinking-water","ly nước lọc","glass of drinking water","glasses of drinking water"),"water","lít nước lọc","litres of drinking water"),
        new(QuestionKnowledgeGroup.Drinks, Unit("milk-glasses","ly sữa","glass of milk","glasses of milk"),"milk_liquid","lít sữa","litres of milk"),
        new(QuestionKnowledgeGroup.Drinks, Unit("milk-cartons","hộp sữa","milk carton","milk cartons"),"milk_carton","lít sữa","litres of milk","milk_liquid"),
        new(QuestionKnowledgeGroup.Drinks, Unit("orange-juice-glasses","ly nước cam","glass of orange juice","glasses of orange juice"),"orange_juice","lít nước cam","litres of orange juice"),
        new(QuestionKnowledgeGroup.Drinks, Unit("tea-cups","tách trà","cup of tea","cups of tea"),"tea","lít trà","litres of tea"),
        new(QuestionKnowledgeGroup.Drinks, Unit("lemon-tea-glasses","ly trà chanh","glass of lemon tea","glasses of lemon tea"),"lemon_tea","lít trà chanh","litres of lemon tea"),
        new(QuestionKnowledgeGroup.Drinks, Unit("milkshakes","ly sữa lắc","glass of milkshake","glasses of milkshake"),"milkshake","lít sữa lắc","litres of milkshake"),
        new(QuestionKnowledgeGroup.Fruit, Unit("apples","quả táo","apple","apples"),"apple"),
        new(QuestionKnowledgeGroup.Fruit, Unit("oranges","quả cam","orange","oranges"),"orange"),
        new(QuestionKnowledgeGroup.Fruit, Unit("mangoes","quả xoài","mango","mangoes"),"mango"),
        new(QuestionKnowledgeGroup.Fruit, Unit("bananas","quả chuối","banana","bananas"),"banana"),
        new(QuestionKnowledgeGroup.Fruit, Unit("dragon-fruit","quả thanh long","dragon fruit","dragon fruits"),"dragon_fruit"),
        new(QuestionKnowledgeGroup.Fruit, Unit("strawberries","quả dâu tây","strawberry","strawberries"),"strawberry"),
        new(QuestionKnowledgeGroup.Fruit, Unit("pears","quả lê","pear","pears"),"pear")
    ]);
    // A shared C# source for question scenes, AI templates, PDF and Excel contracts.
    // Shared scenes for additional dishes; their selling venue depends on IsBakeryFood.
    private static readonly string[] RestaurantDishIds =
    [
        "banh-chung", "banh-day", "banh-tet", "banh-beo", "banh-khot",
        "banh-bot-loc", "banh-gio", "banh-bao-thit-trung-cut", "ha-cao",
        "bot-chien", "pizzas", "mixed-salad", "spaghetti", "hot-dogs", "udon",
        "goi-cuon", "bun-cha", "banh-trang-tron", "banh-trang-nuong",
        "pha-lau-bo", "ca-ri-ga-viet", "ca-ri-bo-nhat",
        "tau-hu-nuoc-duong", "jjinppang", "chapssal-doughnut", "bibimbap",
        "steak-bo", "steak-ga", "steak-cuu",
        "steak-bo-sot-tieu", "steak-ga-sot-kem-nam", "steak-bo-sot-ruou-vang-do",
        "steak-bo-sot-bearnaise", "steak-cuu-sot-chimichurri", "steak-ga-sot-bbq", "steak-cuu-sot-bo-toi",
        "tteokbokki", "korean-corn-dog", "hotteok", "bungeoppang", "gyeranppang",
        "sundae-gukbap", "andong-jjimdak", "kalguksu", "squid-sundae",
        "com-suon-cay", "com-chien-hai-san", "com-chien-duong-chau",
        "com-ga-xoi-mo", "com-ga-sot-me", "com-ga-sot-tieu-xanh", "com-ga-mam-toi",
        "kem-bo-da-lat", "kem-trang-tien", "sua-chua-ha-long",
        "canh-chua-ca-loc", "canh-kho-qua-nhoi-thit", "canh-bi-do-ham-xuong",
        "canh-cua-rau-day-mong-toi", "canh-rau-ngot-thit-bam", "ca-loc-kho-to",
        "che-dau-do", "che-dau-den", "che-dau-xanh", "che-troi-nuoc",
        "che-thap-cam", "che-thai", "che-chuoi", "che-bap", "che-hat-sen-long-nhan",
        "che-dau-phong", "che-khuc-bach", "che-suong-sa-hat-luu", "che-khoai-mon"
    ];
    public static IReadOnlyList<Subject> NewRestaurantDishes { get; } =
        Array.AsReadOnly(RestaurantDishIds.Select(id => Subjects.Single(subject => subject.Unit.Id == id)).ToArray());
    // Match the complete unit ID: "steak-bo" must not capture "steak-bo-sot-tieu".
    internal static Subject? RestaurantDishForScene(string sceneId)
    {
        const string appliedPrefix = "theme-food-";
        const string fractionPrefix = "fraction-theme-food-";
        string prefix = sceneId.StartsWith(fractionPrefix, StringComparison.Ordinal) ? fractionPrefix
            : sceneId.StartsWith(appliedPrefix, StringComparison.Ordinal) ? appliedPrefix : "";
        if (prefix.Length == 0) return null;
        int suffixStart = sceneId.LastIndexOf('-');
        if (suffixStart <= prefix.Length) return null;
        string suffix = sceneId[(suffixStart + 1)..];
        if (suffix is not ("total" or "remaining" or "groups" or "share")) return null;
        string dishId = sceneId[prefix.Length..suffixStart];
        return NewRestaurantDishes.FirstOrDefault(d => d.Unit.Id == dishId);
    }
    // Existing registries retain their original singular/plural/group units for saved contracts.
    private static readonly string[] ExistingUnits = ["sandwich-bread","round-bread","banh-mi","cakes","candies","milk-cartons","apples","oranges","mangoes"];
    internal static IEnumerable<QuestionUnit> AdditionalUnits => Subjects.Where(s => !ExistingUnits.Contains(s.Unit.Id)).Select(s => s.Unit);
    public static bool IsTheme(QuestionKnowledgeGroup group) => group is QuestionKnowledgeGroup.Food or QuestionKnowledgeGroup.Drinks or QuestionKnowledgeGroup.Fruit;
    public static bool IsCheDessert(string? unitId) => unitId is
        "che-dau-do" or "che-dau-den" or "che-dau-xanh" or "che-troi-nuoc"
        or "che-thap-cam" or "che-thai" or "che-chuoi" or "che-bap"
        or "che-hat-sen-long-nhan" or "che-dau-phong" or "che-khuc-bach"
        or "che-suong-sa-hat-luu" or "che-khoai-mon";
    // One stable venue type for each prepared dessert, shared by C#, AI, PDF and Excel.
    internal static string? DessertShopKind(string? unitId) => IsCheDessert(unitId) ? "chè"
        : unitId is "kem-bo-da-lat" or "kem-trang-tien" ? "kem"
        : unitId == "sua-chua-ha-long" ? "sữa chua" : null;
    internal static string? DessertShopKind(BasicQuestionContract c)
    {
        if (c.KnowledgeGroup != QuestionKnowledgeGroup.Food) return null;
        string? unitId = c.Version switch
        {
            FractionQuestionCatalogue.Version => RestaurantDishForScene(c.SceneId)?.Unit.Id,
            FindXQuestionCatalogue.Version => FindXQuestionCatalogue.Find(c.SceneId)?.SourceScene.UnitId,
            AppliedQuestionCatalogue.Version => AppliedQuestionCatalogue.Find(c.SceneId)?.UnitId,
            _ => null
        };
        return DessertShopKind(unitId);
    }
    internal static bool IsCheDessertContract(BasicQuestionContract c)
    {
        if (c.KnowledgeGroup != QuestionKnowledgeGroup.Food) return false;
        return c.Version switch
        {
            FractionQuestionCatalogue.Version => IsCheDessert(RestaurantDishForScene(c.SceneId)?.Unit.Id),
            FindXQuestionCatalogue.Version => IsCheDessert(FindXQuestionCatalogue.Find(c.SceneId)?.SourceScene.UnitId),
            AppliedQuestionCatalogue.Version => IsCheDessert(AppliedQuestionCatalogue.Find(c.SceneId)?.UnitId),
            _ => false
        };
    }
    // Food names are stable identifiers shared by C#, AI contracts, PDF and Excel.
    // Bánh cuốn and bánh xèo remain restaurant dishes.
    public static bool IsBakeryFood(string? unitId) => unitId is
        "sandwich-bread" or "round-bread" or "banh-mi" or "bread-rolls" or
        "cakes" or "biscuits" or "lamingtons" or "pancakes" or
        "banh-cam" or "banh-com" or "banh-trung-thu" or "banh-pia" or
        "croissants" or "banh-da-lon" or "banh-bo" or "muffins" or
        "cupcakes" or "cookies" or "donuts" or "waffles" or "apple-pies" or "flans" or
        "banh-chung" or "banh-gio" or "banh-day" or "banh-bao-thit-trung-cut" or "banh-tet" or
        "jjinppang" or "chapssal-doughnut" or "hotteok" or "bungeoppang" or "gyeranppang";

    public static bool IsBakeryFractionScene(string sceneId) =>
        Subjects.Any(s => s.Group == QuestionKnowledgeGroup.Food && IsBakeryFood(s.Unit.Id)
            && sceneId.StartsWith($"fraction-theme-food-{s.Unit.Id}-", StringComparison.Ordinal));

    public static bool IsBakeryContract(BasicQuestionContract c)
    {
        if (c.KnowledgeGroup != QuestionKnowledgeGroup.Food) return false;
        if (c.Version == FractionQuestionCatalogue.Version) return IsBakeryFractionScene(c.SceneId);
        if (c.Version == FindXQuestionCatalogue.Version)
            return IsBakeryFood(FindXQuestionCatalogue.Find(c.SceneId)?.SourceScene.UnitId);
        if (c.Version == AppliedQuestionCatalogue.Version)
            return IsBakeryFood(AppliedQuestionCatalogue.Find(c.SceneId)?.UnitId);
        return false;
    }
    internal static string EnglishFractionUnit(string sceneId, string unit, bool proper, bool one)
    {
        const string prefix = "fraction-theme-";
        int suffixStart = sceneId.LastIndexOf('-');
        if (!sceneId.StartsWith(prefix, StringComparison.Ordinal) || suffixStart <= prefix.Length) return unit;
        string groupAndUnit = sceneId[prefix.Length..suffixStart];
        var subject = Subjects.FirstOrDefault(s => groupAndUnit ==
            $"{s.Group.ToString().ToLowerInvariant()}-{s.Unit.Id}");
        if (subject is null || (!proper && !one)) return unit;
        string singular = subject.FractionEnglish?.Replace("litres", "litre", StringComparison.Ordinal) ?? subject.Unit.Singular;
        return proper ? "of " + ("aeiou".Contains(char.ToLowerInvariant(singular[0])) ? "an " : "a ") + singular : singular;
    }
    public static bool IsThemedItem(string? unit) => unit is "chiếc bánh mì" or "ổ bánh mì" or "bread rolls" or "bread roll" or "suất ăn" or "meals" or "túi gạo" or "rice bags"
        || Subjects.Any(s => unit == s.Unit.Vietnamese || unit == s.Unit.Plural || unit == s.Unit.Singular);
    public static bool IsLegacyContract(BasicQuestionContract c) => c.KnowledgeGroup == QuestionKnowledgeGroup.Objects &&
        (c.Version == FractionQuestionCatalogue.Version ? FractionQuestionCatalogue.Find(c.SceneId) is { } f && IsLegacyFraction(f)
        : c.Version == FindXQuestionCatalogue.Version ? FindXQuestionCatalogue.Find(c.SceneId) is { } x && IsLegacySubject(x.SourceScene)
        : c.Version == AppliedQuestionCatalogue.Version ? AppliedQuestionCatalogue.Find(c.SceneId) is { } a && IsLegacySubject(a)
        : IsThemedItem(c.Unit));
    public static bool IsLegacySubject(AppliedQuestionScene s) => s.Group == QuestionKnowledgeGroup.Objects
        && (s.UnitId is "rolls" or "meals" or "rice-bags" || Subjects.Any(t => t.Unit.Id == s.UnitId));
    public static bool IsLegacyFraction(FractionQuestionCatalogue.Scene s) => s.Group == QuestionKnowledgeGroup.Objects
        && (s.Id.StartsWith("fraction-bread-",StringComparison.Ordinal) || s.Id.StartsWith("fraction-cake-",StringComparison.Ordinal));
}
