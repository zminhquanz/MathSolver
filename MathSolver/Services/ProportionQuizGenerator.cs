using MathSolver.Models;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Services;

/// <summary>
/// Sinh bài toán tỉ lệ thuận/nghịch từ catalog template cố định. Câu chữ được
/// chọn bằng Random và dữ kiện số cũng được sinh bằng Random nhưng luôn đảm
/// bảo đáp án nguyên để phù hợp chương trình tiểu học/THCS cơ bản.
/// </summary>
public sealed class ProportionQuizGenerator
{
    private enum DirectRateProfile
    {
        GenericCount,
        FabricMeters,
        TreesPerStudent,
        MoneyDong,
        CargoTons,
        FuelLiters,
        RiceBagKilograms,
        VegetableGrams,
        FruitGrams,
        MeatGrams,
        EggGrams,
        DistanceKilometers,
        ContainerLiters,
        PaintAreaSquareMeters
    }

    private sealed record TemplateDefinition(
        ProportionQuizType Type,
        ProportionScenarioKind Scenario,
        string VietnameseTemplate,
        string EnglishTemplate,
        string VietnameseUnit,
        string EnglishUnit,
        string VietnameseSubject,
        string EnglishSubject,
        bool AsksForAdditionalPeople = false,
        DirectRateProfile RateProfile = DirectRateProfile.GenericCount);

    private static readonly TemplateDefinition[] Templates =
    [
        // Tỉ lệ thuận -------------------------------------------------------
        // Nhóm kinh điển được ưu tiên bằng GetTemplateWeight() bên dưới.
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.Clothing,
            "May {0} bộ quần áo hết {1} mét vải. Hỏi may {2} bộ quần áo như thế hết bao nhiêu mét vải?",
            "Making {0} sets of clothes uses {1} meters of fabric. How many meters of fabric are needed for {2} sets at the same rate?",
            "mét vải",
            "meters of fabric",
            "vải",
            "fabric",
            RateProfile: DirectRateProfile.FabricMeters),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.Clothing,
            "Cần {1} mét vải để may {0} bộ quần áo. Hỏi may {2} bộ thì cần bao nhiêu mét vải?",
            "It takes {1} meters of fabric to make {0} sets of clothes. How many meters are needed for {2} sets?",
            "mét vải",
            "meters of fabric",
            "vải",
            "fabric",
            RateProfile: DirectRateProfile.FabricMeters),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.StudentsPlanting,
            "{0} học sinh trồng được {1} cây. Hỏi {2} học sinh trồng được bao nhiêu cây, biết mỗi em trồng như nhau?",
            "{0} students plant {1} trees. How many trees can {2} students plant if every student plants the same number?",
            "cây",
            "trees",
            "cây",
            "trees",
            RateProfile: DirectRateProfile.TreesPerStudent),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.StudentsPlanting,
            "Một tổ có {0} em trồng được {1} cây. Hỏi cả lớp {2} em trồng được bao nhiêu cây nếu năng suất mỗi em như nhau?",
            "A group of {0} students plants {1} trees. How many trees can a class of {2} students plant at the same rate?",
            "cây",
            "trees",
            "cây",
            "trees",
            RateProfile: DirectRateProfile.TreesPerStudent),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.Shopping,
            "{0} quyển vở giá {1} đồng. Hỏi {2} quyển vở giá bao nhiêu tiền, biết giá mỗi quyển như nhau?",
            "{0} notebooks cost ${1}. How much do {2} notebooks cost at the same unit price?",
            "đồng",
            "dollars",
            "tiền",
            "money",
            RateProfile: DirectRateProfile.MoneyDong),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.Shopping,
            "Mẹ mua {0} quyển tập hết {1} đồng. Hỏi mua {2} quyển thì hết bao nhiêu tiền?",
            "A parent buys {0} notebooks for ${1}. How much would {2} notebooks cost at the same unit price?",
            "đồng",
            "dollars",
            "tiền",
            "money",
            RateProfile: DirectRateProfile.MoneyDong),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.VehiclesCargo,
            "{0} xe tải chở được {1} tấn gạo đóng bao. Hỏi {2} xe tải như thế chở được bao nhiêu tấn gạo?",
            "{0} trucks carry {1} tons of bagged rice. How many tons of rice can {2} identical trucks carry?",
            "tấn gạo",
            "tons of rice",
            "gạo",
            "rice",
            RateProfile: DirectRateProfile.CargoTons),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.VehiclesFuel,
            "{0} xe cùng loại hết {1} lít xăng cho cùng một quãng đường. Hỏi {2} xe như thế hết bao nhiêu lít xăng?",
            "{0} identical vehicles use {1} liters of fuel over the same route. How many liters do {2} such vehicles use?",
            "lít xăng",
            "liters of fuel",
            "xăng",
            "fuel",
            RateProfile: DirectRateProfile.FuelLiters),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.DistanceTime,
            "Một ô tô trong {0} giờ đi được {1} km. Hỏi với cùng vận tốc, trong {2} giờ ô tô đi được bao nhiêu km?",
            "A car travels {1} km in {0} hours. At the same speed, how many kilometers does it travel in {2} hours?",
            "km",
            "km",
            "quãng đường",
            "distance",
            RateProfile: DirectRateProfile.DistanceKilometers),

        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.ProductionItems,
            "Trong cùng một khoảng thời gian, {0} thợ làm được {1} cái ghế. Hỏi {2} thợ cùng năng suất làm được bao nhiêu cái ghế?",
            "In the same amount of time, {0} workers make {1} chairs. How many chairs can {2} equally productive workers make?",
            "cái ghế",
            "chairs",
            "ghế",
            "chairs",
            RateProfile: DirectRateProfile.GenericCount),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.ProductionItems,
            "Trong cùng một khoảng thời gian, {0} máy làm được {1} sản phẩm. Hỏi {2} máy cùng năng suất làm được bao nhiêu sản phẩm?",
            "In the same amount of time, {0} machines make {1} products. How many products can {2} equally productive machines make?",
            "sản phẩm",
            "products",
            "sản phẩm",
            "products",
            RateProfile: DirectRateProfile.GenericCount),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.ProductionItems,
            "{0} thùng như nhau có tổng cộng {1} hộp hàng. Hỏi {2} thùng như thế có bao nhiêu hộp hàng?",
            "{0} identical crates contain {1} boxes in total. How many boxes are in {2} such crates?",
            "hộp",
            "boxes",
            "hộp hàng",
            "boxes",
            RateProfile: DirectRateProfile.GenericCount),

        // Khối lượng / thực phẩm thực tế -----------------------------------
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.RiceBagsWeight,
            "{0} bao gạo cùng loại nặng {1} kg. Hỏi {2} bao gạo như thế nặng bao nhiêu kg?",
            "{0} equal bags of rice weigh {1} kg. How many kilograms do {2} such bags weigh?",
            "kg",
            "kg",
            "gạo",
            "rice",
            RateProfile: DirectRateProfile.RiceBagKilograms),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.FoodWeightGrams,
            "{0} bó rau cùng loại nặng {1} gam. Hỏi {2} bó rau như thế nặng bao nhiêu gam?",
            "{0} equal bunches of vegetables weigh {1} grams. How many grams do {2} such bunches weigh?",
            "gam",
            "grams",
            "rau",
            "vegetables",
            RateProfile: DirectRateProfile.VegetableGrams),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.FoodWeightGrams,
            "{0} củ cà rốt có khối lượng như nhau nặng tổng cộng {1} gam. Hỏi {2} củ như thế nặng bao nhiêu gam?",
            "{0} equal carrots weigh {1} grams in total. How many grams do {2} such carrots weigh?",
            "gam",
            "grams",
            "cà rốt",
            "carrots",
            RateProfile: DirectRateProfile.VegetableGrams),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.FoodWeightGrams,
            "{0} củ khoai tây cùng cỡ nặng tổng cộng {1} gam. Hỏi {2} củ khoai tây như thế nặng bao nhiêu gam?",
            "{0} equal potatoes weigh {1} grams in total. How many grams do {2} such potatoes weigh?",
            "gam",
            "grams",
            "khoai tây",
            "potatoes",
            RateProfile: DirectRateProfile.VegetableGrams),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.FoodWeightGrams,
            "{0} quả táo cùng cỡ nặng tổng cộng {1} gam. Hỏi {2} quả táo như thế nặng bao nhiêu gam?",
            "{0} equal apples weigh {1} grams in total. How many grams do {2} such apples weigh?",
            "gam",
            "grams",
            "táo",
            "apples",
            RateProfile: DirectRateProfile.FruitGrams),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.FoodWeightGrams,
            "{0} quả cam cùng cỡ nặng tổng cộng {1} gam. Hỏi {2} quả cam như thế nặng bao nhiêu gam?",
            "{0} equal oranges weigh {1} grams in total. How many grams do {2} such oranges weigh?",
            "gam",
            "grams",
            "cam",
            "oranges",
            RateProfile: DirectRateProfile.FruitGrams),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.FoodWeightGrams,
            "{0} quả xoài cùng cỡ nặng tổng cộng {1} gam. Hỏi {2} quả xoài như thế nặng bao nhiêu gam?",
            "{0} equal mangoes weigh {1} grams in total. How many grams do {2} such mangoes weigh?",
            "gam",
            "grams",
            "xoài",
            "mangoes",
            RateProfile: DirectRateProfile.FruitGrams),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.FoodWeightGrams,
            "{0} phần thịt heo cùng khối lượng nặng tổng cộng {1} gam. Hỏi {2} phần như thế nặng bao nhiêu gam?",
            "{0} equal portions of pork weigh {1} grams in total. How many grams do {2} such portions weigh?",
            "gam",
            "grams",
            "thịt heo",
            "pork",
            RateProfile: DirectRateProfile.MeatGrams),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.FoodWeightGrams,
            "{0} phần cá cùng khối lượng nặng tổng cộng {1} gam. Hỏi {2} phần cá như thế nặng bao nhiêu gam?",
            "{0} equal portions of fish weigh {1} grams in total. How many grams do {2} such portions weigh?",
            "gam",
            "grams",
            "cá",
            "fish",
            RateProfile: DirectRateProfile.MeatGrams),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.FoodWeightGrams,
            "{0} phần thịt gà cùng khối lượng nặng tổng cộng {1} gam. Hỏi {2} phần như thế nặng bao nhiêu gam?",
            "{0} equal portions of chicken weigh {1} grams in total. How many grams do {2} such portions weigh?",
            "gam",
            "grams",
            "thịt gà",
            "chicken",
            RateProfile: DirectRateProfile.MeatGrams),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.EggWeightGrams,
            "{0} quả trứng cùng cỡ nặng tổng cộng {1} gam. Hỏi {2} quả trứng như thế nặng bao nhiêu gam?",
            "{0} eggs of the same size weigh {1} grams in total. How many grams do {2} such eggs weigh?",
            "gam",
            "grams",
            "trứng",
            "eggs",
            RateProfile: DirectRateProfile.EggGrams),

        // Thùng / lít và diện tích -----------------------------------------
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.ContainersLiquid,
            "{0} thùng cùng loại chứa được {1} lít mật ong. Hỏi {2} thùng như thế chứa được bao nhiêu lít mật ong?",
            "{0} identical containers hold {1} liters of honey. How many liters can {2} such containers hold?",
            "lít mật ong",
            "liters of honey",
            "mật ong",
            "honey",
            RateProfile: DirectRateProfile.ContainerLiters),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.ContainersLiquid,
            "{0} can cùng loại chứa được {1} lít dầu. Hỏi {2} can như thế chứa được bao nhiêu lít dầu?",
            "{0} identical cans hold {1} liters of oil. How many liters can {2} such cans hold?",
            "lít dầu",
            "liters of oil",
            "dầu",
            "oil",
            RateProfile: DirectRateProfile.ContainerLiters),
        new(
            ProportionQuizType.Direct,
            ProportionScenarioKind.PaintArea,
            "{0} thùng sơn cùng loại sơn được {1} m² tường. Hỏi {2} thùng sơn như thế sơn được bao nhiêu m² tường?",
            "{0} identical cans of paint cover {1} m² of wall. How many square meters can {2} cans cover?",
            "m²",
            "m²",
            "diện tích tường",
            "wall area",
            RateProfile: DirectRateProfile.PaintAreaSquareMeters),

        // Tỉ lệ nghịch -----------------------------------------------------
        new(
            ProportionQuizType.Inverse,
            ProportionScenarioKind.WorkersDays,
            "{0} người làm xong một công việc trong {1} ngày. Hỏi {2} người làm xong công việc đó trong bao nhiêu ngày, biết năng suất mỗi người như nhau?",
            "{0} people finish a job in {1} days. How many days do {2} people need if everyone works at the same rate?",
            "ngày",
            "days",
            "thời gian",
            "time"),
        new(
            ProportionQuizType.Inverse,
            ProportionScenarioKind.WorkersDays,
            "{0} công nhân đắp xong một đoạn đường trong {1} ngày. Hỏi {2} công nhân đắp xong đoạn đường đó trong bao nhiêu ngày?",
            "{0} workers finish a road section in {1} days. How many days do {2} workers need at the same productivity?",
            "ngày",
            "days",
            "thời gian",
            "time"),
        new(
            ProportionQuizType.Inverse,
            ProportionScenarioKind.WorkersDays,
            "{0} công nhân cùng năng suất hoàn thành một công việc trong {1} giờ. Hỏi {2} công nhân hoàn thành công việc đó trong bao nhiêu giờ?",
            "{0} equally productive workers finish a job in {1} hours. How many hours do {2} workers need to finish the same job?",
            "giờ",
            "hours",
            "thời gian",
            "time"),
        new(
            ProportionQuizType.Inverse,
            ProportionScenarioKind.MachinesHours,
            "{0} máy hoàn thành một công việc trong {1} giờ. Hỏi {2} máy cùng năng suất hoàn thành công việc đó trong bao nhiêu giờ?",
            "{0} machines finish a job in {1} hours. How many hours do {2} equally productive machines need?",
            "giờ",
            "hours",
            "thời gian",
            "time"),
        new(
            ProportionQuizType.Inverse,
            ProportionScenarioKind.WorkersJob,
            "{0} thợ làm xong một công việc trong {1} ngày. Hỏi {2} thợ cùng năng suất làm xong trong bao nhiêu ngày?",
            "{0} workers finish a job in {1} days. How many days do {2} workers need at the same productivity?",
            "ngày",
            "days",
            "thời gian",
            "time"),
        new(
            ProportionQuizType.Inverse,
            ProportionScenarioKind.FoodPeopleDays,
            "Một bếp ăn chuẩn bị gạo đủ cho {0} người ăn trong {1} ngày. Thực tế có {2} người. Hỏi số gạo đó đủ ăn trong bao nhiêu ngày?",
            "A kitchen has enough rice for {0} people for {1} days. If there are actually {2} people, for how many days will the rice last?",
            "ngày",
            "days",
            "thời gian",
            "time"),
        new(
            ProportionQuizType.Inverse,
            ProportionScenarioKind.FoodPeopleDays,
            "Một bếp ăn có đủ thực phẩm cho {0} học sinh dùng trong {1} ngày. Nếu số học sinh thực tế là {2} em thì số thực phẩm đó đủ dùng trong bao nhiêu ngày?",
            "A school kitchen has enough food for {0} students for {1} days. If there are actually {2} students, for how many days will the food last?",
            "ngày",
            "days",
            "thời gian",
            "time"),
        new(
            ProportionQuizType.Inverse,
            ProportionScenarioKind.FoodAdditionalPeople,
            "Chuẩn bị đủ thực phẩm cho {0} người ăn trong {1} ngày. Vì có thêm người nên số thực phẩm đó chỉ đủ ăn trong {2} ngày. Hỏi có thêm bao nhiêu người?",
            "Food is prepared for {0} people for {1} days. Because more people arrive, it lasts only {2} days. How many additional people arrived?",
            "người",
            "people",
            "số người đến thêm",
            "additional people",
            AsksForAdditionalPeople: true),
        new(
            ProportionQuizType.Inverse,
            ProportionScenarioKind.SalesStock,
            "Một cửa hàng chuẩn bị số hộp mứt đủ bán trong {0} ngày nếu mỗi ngày bán {1} hộp. Thực tế mỗi ngày bán {2} hộp. Hỏi số hàng đó đủ bán trong bao nhiêu ngày?",
            "A store has enough jam boxes for {0} days when selling {1} boxes per day. If it actually sells {2} boxes per day, for how many days will the stock last?",
            "ngày",
            "days",
            "thời gian",
            "time")
    ];

    private readonly Random _random;

    public ProportionQuizGenerator(Random? random = null)
    {
        _random = random ?? Random.Shared;
    }

    public ArithmeticQuizQuestion GenerateAlgorithm(
        ArithmeticQuizMode mode,
        ProportionQuizType type,
        AppLanguage language)
    {
        ProportionQuizContract contract = CreateContract(type, language);
        return CreateQuestion(mode, contract);
    }

    public ArithmeticQuizQuestion GenerateContract(
        ArithmeticQuizMode mode,
        ProportionQuizType type,
        AppLanguage language)
    {
        ProportionQuizContract contract = CreateContract(type, language);
        return CreateQuestion(mode, contract);
    }

    private ProportionQuizContract CreateContract(
        ProportionQuizType type,
        AppLanguage language)
    {
        TemplateDefinition[] candidates = Templates
            .Where(template => template.Type == type)
            .ToArray();

        TemplateDefinition template = PickWeightedTemplate(candidates);

        (int a, int b, int c, BigInteger answer) =
            template.Type == ProportionQuizType.Direct
                ? CreateDirectNumbers(template.RateProfile, language)
                : CreateInverseNumbers(
                    template.Scenario,
                    template.AsksForAdditionalPeople);

        string problemTemplate = language == AppLanguage.Vietnamese
            ? template.VietnameseTemplate
            : template.EnglishTemplate;

        string unit = language == AppLanguage.Vietnamese
            ? template.VietnameseUnit
            : template.EnglishUnit;

        string subject = language == AppLanguage.Vietnamese
            ? template.VietnameseSubject
            : template.EnglishSubject;

        string problemText = string.Format(
            CultureInfo.CurrentCulture,
            problemTemplate,
            a,
            b,
            c);

        return new(
            template.Type,
            template.Scenario,
            a,
            b,
            c,
            answer,
            unit,
            subject,
            problemText,
            template.AsksForAdditionalPeople);
    }

    private TemplateDefinition PickWeightedTemplate(
        IReadOnlyList<TemplateDefinition> candidates)
    {
        int totalWeight = 0;
        foreach (TemplateDefinition candidate in candidates)
        {
            totalWeight += GetTemplateWeight(candidate);
        }

        int roll = _random.Next(totalWeight);
        foreach (TemplateDefinition candidate in candidates)
        {
            roll -= GetTemplateWeight(candidate);
            if (roll < 0)
            {
                return candidate;
            }
        }

        return candidates[^1];
    }

    private static int GetTemplateWeight(
        TemplateDefinition template) =>
        template.Scenario switch
        {
            // Các cặp kinh điển lớp 5: ưu tiên xuất hiện nhiều nhất.
            ProportionScenarioKind.Shopping => 7,
            ProportionScenarioKind.Clothing => 6,
            ProportionScenarioKind.StudentsPlanting => 6,
            ProportionScenarioKind.WorkersDays => 7,
            ProportionScenarioKind.FoodPeopleDays => 6,
            ProportionScenarioKind.MachinesHours => 5,
            ProportionScenarioKind.WorkersJob => 5,

            // Nhóm phổ biến tiếp theo.
            ProportionScenarioKind.VehiclesCargo => 4,
            ProportionScenarioKind.VehiclesFuel => 3,
            ProportionScenarioKind.DistanceTime => 4,
            ProportionScenarioKind.ContainersLiquid => 4,
            ProportionScenarioKind.ProductionItems => 3,
            ProportionScenarioKind.SalesStock => 4,
            ProportionScenarioKind.FoodAdditionalPeople => 3,

            // Khối lượng / diện tích có trong chương trình nhưng ít gặp hơn.
            ProportionScenarioKind.RiceBagsWeight => 3,
            ProportionScenarioKind.FoodWeightGrams => 1,
            ProportionScenarioKind.EggWeightGrams => 2,
            ProportionScenarioKind.PaintArea => 2,
            _ => 1
        };

    private (int A, int B, int C, BigInteger Answer)
        CreateDirectNumbers(
            DirectRateProfile rateProfile,
            AppLanguage language)
    {
        int a = _random.Next(2, 11);
        int c;
        do
        {
            c = _random.Next(2, 16);
        }
        while (c == a);

        int rate = rateProfile switch
        {
            DirectRateProfile.FabricMeters => PickFrom([2, 3, 4, 5]),
            DirectRateProfile.TreesPerStudent => _random.Next(2, 9),
            DirectRateProfile.MoneyDong =>
                language == AppLanguage.Vietnamese
                    ? _random.Next(4, 21) * 1000
                    : _random.Next(2, 13),
            DirectRateProfile.CargoTons => _random.Next(2, 11),
            DirectRateProfile.FuelLiters => _random.Next(1, 9) * 5,
            DirectRateProfile.RiceBagKilograms => PickFrom([10, 20, 25, 30, 40, 50]),
            DirectRateProfile.VegetableGrams => _random.Next(2, 11) * 50,
            DirectRateProfile.FruitGrams => PickFrom([100, 125, 150, 175, 200, 225, 250, 300]),
            DirectRateProfile.MeatGrams => _random.Next(2, 11) * 50,
            DirectRateProfile.EggGrams => _random.Next(45, 76),
            DirectRateProfile.DistanceKilometers => _random.Next(30, 91),
            DirectRateProfile.ContainerLiters => _random.Next(1, 7) * 5,
            DirectRateProfile.PaintAreaSquareMeters => _random.Next(2, 7) * 5,
            _ => _random.Next(2, 16)
        };

        int b = checked(a * rate);
        BigInteger answer = (BigInteger)c * rate;
        return (a, b, c, answer);
    }

    private (int A, int B, int C, BigInteger Answer)
        CreateInverseNumbers(
            ProportionScenarioKind scenario,
            bool asksForAdditionalPeople)
    {
        for (int attempt = 0; attempt < 192; attempt++)
        {
            int a;
            int b;
            int c;

            switch (scenario)
            {
                case ProportionScenarioKind.FoodPeopleDays:
                case ProportionScenarioKind.FoodAdditionalPeople:
                    a = _random.Next(2, 13) * 10;       // 20..120 người
                    b = _random.Next(3, 16);            // 3..15 ngày
                    c = asksForAdditionalPeople
                        ? _random.Next(1, b)
                        : _random.Next(2, 16) * 10;      // 20..150 người
                    break;

                case ProportionScenarioKind.SalesStock:
                    a = _random.Next(5, 21);             // số ngày dự kiến
                    b = _random.Next(2, 11) * 5;         // 10..50 hộp/ngày
                    c = _random.Next(2, 13) * 5;         // 10..60 hộp/ngày
                    break;

                case ProportionScenarioKind.MachinesHours:
                    a = _random.Next(2, 11);
                    b = _random.Next(2, 13);
                    c = _random.Next(2, 13);
                    break;

                case ProportionScenarioKind.WorkersDays:
                case ProportionScenarioKind.WorkersJob:
                    a = _random.Next(4, 21);
                    b = _random.Next(3, 16);
                    c = _random.Next(4, 25);
                    break;

                default:
                    a = _random.Next(2, 13);
                    b = _random.Next(2, 13);
                    c = _random.Next(2, 13);
                    break;
            }

            if (asksForAdditionalPeople)
            {
                int totalPersonDays = checked(a * b);
                if (totalPersonDays % c != 0)
                {
                    continue;
                }

                int newPeople = totalPersonDays / c;
                int added = newPeople - a;
                if (added > 0)
                {
                    return (a, b, c, added);
                }

                continue;
            }

            if (c == a &&
                scenario is not ProportionScenarioKind.SalesStock)
            {
                continue;
            }

            int total = checked(a * b);
            if (total % c != 0)
            {
                continue;
            }

            int answer = total / c;
            int maxReasonableAnswer = scenario switch
            {
                ProportionScenarioKind.MachinesHours => 24,
                ProportionScenarioKind.SalesStock => 30,
                ProportionScenarioKind.FoodPeopleDays => 30,
                ProportionScenarioKind.WorkersDays or
                ProportionScenarioKind.WorkersJob => 30,
                _ => 60
            };

            if (answer > 0 && answer <= maxReasonableAnswer)
            {
                return (a, b, c, answer);
            }
        }

        return scenario switch
        {
            ProportionScenarioKind.FoodAdditionalPeople => (40, 6, 4, 20),
            ProportionScenarioKind.FoodPeopleDays => (40, 6, 80, 3),
            ProportionScenarioKind.SalesStock => (10, 30, 50, 6),
            ProportionScenarioKind.MachinesHours => (4, 6, 8, 3),
            ProportionScenarioKind.WorkersDays or
            ProportionScenarioKind.WorkersJob => (6, 8, 12, 4),
            _ => (4, 6, 8, 3)
        };
    }

    private int PickFrom(IReadOnlyList<int> values) =>
        values[_random.Next(values.Count)];

    private ArithmeticQuizQuestion CreateQuestion(
        ArithmeticQuizMode mode,
        ProportionQuizContract contract)
    {
        IntegerArithmeticExpression expression =
            CreateRepresentativeExpression(contract);

        BigInteger answer = contract.CorrectAnswer;

        return mode switch
        {
            ArithmeticQuizMode.TrueFalse =>
                CreateTrueFalseQuestion(expression, contract, answer),
            ArithmeticQuizMode.MultipleChoice =>
                CreateMultipleChoiceQuestion(expression, contract, answer),
            ArithmeticQuizMode.Essay =>
                new(
                    expression,
                    mode,
                    answer,
                    null,
                    null,
                    [],
                    ProportionProblem: contract),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }

    private ArithmeticQuizQuestion CreateTrueFalseQuestion(
        IntegerArithmeticExpression expression,
        ProportionQuizContract contract,
        BigInteger answer)
    {
        bool showCorrect = _random.Next(2) == 0;
        BigInteger presented = showCorrect
            ? answer
            : CreateDistractors(contract, answer, 1)[0];

        return new(
            expression,
            ArithmeticQuizMode.TrueFalse,
            answer,
            presented,
            presented == answer,
            [],
            ProportionProblem: contract);
    }

    private ArithmeticQuizQuestion CreateMultipleChoiceQuestion(
        IntegerArithmeticExpression expression,
        ProportionQuizContract contract,
        BigInteger answer)
    {
        var choices = new List<BigInteger> { answer };
        choices.AddRange(CreateDistractors(contract, answer, 3));
        Shuffle(choices);

        return new(
            expression,
            ArithmeticQuizMode.MultipleChoice,
            answer,
            null,
            null,
            choices,
            ProportionProblem: contract);
    }

    private static IntegerArithmeticExpression CreateRepresentativeExpression(
        ProportionQuizContract contract)
    {
        if (contract.IsDirect)
        {
            int unitRate = contract.B / contract.A;
            return new(unitRate, ArithmeticOperation.Multiply, contract.C);
        }

        if (contract.AsksForAdditionalPeople)
        {
            int newPeople = contract.A * contract.B / contract.C;
            return new(newPeople, ArithmeticOperation.Subtract, contract.A);
        }

        int total = contract.A * contract.B;
        return new(total, ArithmeticOperation.Divide, contract.C);
    }

    private IReadOnlyList<BigInteger> CreateDistractors(
        ProportionQuizContract contract,
        BigInteger correctAnswer,
        int count)
    {
        // Câu hỏi tiền tệ không dùng kiểu +/- 1, 2, 3 đồng vì nhìn rất giả
        // và người học có thể loại ngay bằng hình thức đáp án. Với tiền, các
        // phương án nhiễu luôn đi theo bước tiền hợp lý (1.000, 10.000,
        // 100.000...) và có cả đáp án gần lẫn đáp án lệch xa hơn.
        if (IsMoneyProblem(contract))
        {
            return CreateMoneyDistractors(
                contract,
                correctAnswer,
                count);
        }

        return CreateStandardDistractors(correctAnswer, count);
    }

    private IReadOnlyList<BigInteger> CreateMoneyDistractors(
        ProportionQuizContract contract,
        BigInteger correctAnswer,
        int count)
    {
        var distractors = new HashSet<BigInteger>();
        BigInteger step = GetMoneyDistractorStep(
            contract,
            correctAnswer);

        // Ví dụ 84.000 đồng với step 1.000 có thể sinh 81.000, 82.000,
        // 83.000, 85.000, 86.000, 94.000... thay vì 83.998/83.999.
        int[] multipliers = [-10, -5, -3, -2, -1, 1, 2, 3, 5, 10];
        Shuffle(multipliers);

        foreach (int multiplier in multipliers)
        {
            if (distractors.Count >= count)
            {
                break;
            }

            BigInteger candidate = correctAnswer + step * multiplier;
            if (candidate > 0 && candidate != correctAnswer)
            {
                distractors.Add(candidate);
            }
        }

        // Fallback vẫn giữ đúng bội số của step, tuyệt đối không quay về +/-1.
        for (int multiplier = 11;
             distractors.Count < count;
             multiplier++)
        {
            int signedMultiplier = multiplier % 2 == 0
                ? multiplier
                : -multiplier;

            BigInteger candidate =
                correctAnswer + step * signedMultiplier;

            if (candidate > 0 && candidate != correctAnswer)
            {
                distractors.Add(candidate);
            }
        }

        return distractors.ToArray();
    }

    private static BigInteger GetMoneyDistractorStep(
        ProportionQuizContract contract,
        BigInteger correctAnswer)
    {
        BigInteger absolute = BigInteger.Abs(correctAnswer);

        if (contract.AnswerUnit.Contains(
                "dollar",
                StringComparison.OrdinalIgnoreCase) ||
            contract.AnswerUnit.Contains(
                "USD",
                StringComparison.OrdinalIgnoreCase))
        {
            if (absolute >= 100)
            {
                return 10;
            }

            if (absolute >= 40)
            {
                return 5;
            }

            return 1;
        }

        if (absolute >= 10_000_000)
        {
            return 1_000_000;
        }

        if (absolute >= 1_000_000)
        {
            return 100_000;
        }

        if (absolute >= 100_000)
        {
            return 10_000;
        }

        if (absolute >= 10_000)
        {
            return 1_000;
        }

        if (absolute >= 1_000)
        {
            return 500;
        }

        return 100;
    }

    private static bool IsMoneyProblem(
        ProportionQuizContract contract)
    {
        if (contract.Scenario == ProportionScenarioKind.Shopping)
        {
            return true;
        }

        string unit = contract.AnswerUnit.Trim();
        return unit.Contains(
                   "đồng",
                   StringComparison.OrdinalIgnoreCase) ||
               unit.Contains(
                   "money",
                   StringComparison.OrdinalIgnoreCase) ||
               unit.Contains(
                   "currency",
                   StringComparison.OrdinalIgnoreCase) ||
               unit.Contains(
                   "dollar",
                   StringComparison.OrdinalIgnoreCase) ||
               unit.Contains(
                   "USD",
                   StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<BigInteger> CreateStandardDistractors(
        BigInteger correctAnswer,
        int count)
    {
        var set = new HashSet<BigInteger>();
        int[] offsets = [-10, -5, -3, -2, -1, 1, 2, 3, 5, 10];
        int start = _random.Next(offsets.Length);

        for (int index = 0; index < offsets.Length && set.Count < count; index++)
        {
            BigInteger candidate =
                correctAnswer + offsets[(start + index) % offsets.Length];

            if (candidate > 0 && candidate != correctAnswer)
            {
                set.Add(candidate);
            }
        }

        while (set.Count < count)
        {
            BigInteger candidate = correctAnswer + set.Count + 1;
            if (candidate > 0 && candidate != correctAnswer)
            {
                set.Add(candidate);
            }
        }

        return set.ToArray();
    }

    private void Shuffle<T>(IList<T> values)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int swapIndex = _random.Next(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }
}
