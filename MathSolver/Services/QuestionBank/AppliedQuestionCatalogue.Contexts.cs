using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

public static partial class AppliedQuestionCatalogue
{
    // Each selected scene is the same small contract for C# prose, prompt, grammar and validation.
    // Context capacities are physical limits, independent of the star number ceiling.
    private static void AddExpandedScenes(List<AppliedQuestionScene> scenes)
    {
        void Add(string id, QuestionKnowledgeGroup group, ArithmeticOperation op, int stars, int cap, int factor, string unit,
            string va, string vb, string vq, string vl, string ea, string eb, string eq, string el)
            => scenes.Add(new(id, group, op, stars, cap, factor, unit, va, vb, vq, vl, ea, eb, eq, el));

        // Count people, animals, pages, points and visits, rather than swapping nouns in an ownership story.
        foreach (var context in new[] {
            ("classroom", "students", 40, "có", "has"),
            ("poultry", "chickens", 500, "nuôi", "raises"),
            ("reading", "pages", 300, "đọc được", "reads"),
            ("sports", "points", 100, "ghi được", "scores"),
            ("visits", "visits", 20000, "đón", "records"),
            ("planting", "seedlings", 5000, "trồng được", "plants"),
            ("recycling", "bottles", 10000, "thu gom được", "collects"),
            ("bakery", "rolls", 10000, "làm được", "bakes"),
            ("harvest", "mangoes", 20000, "thu hoạch được", "harvests") })
        {
            string p = context.Item1, uid = context.Item2, v = context.Item4, e = context.Item5;
            Add(p + "-total", QuestionKnowledgeGroup.Objects, ArithmeticOperation.Add, 1, context.Item3, 99, uid,
                "{name} " + v + " {a} {unit},", "{other} " + v + " {b} {unit}.",
                "Hỏi cả hai có tổng cộng bao nhiêu {unit}?", "Tổng số {unit} của {name} và {other} là:",
                "{name} " + e + " {a} {unit},", "{other} " + e + " {b} {unit}.",
                "How many {unit} do they have in total?", "Their total number of {unit} is:");
            Add(p + "-difference", QuestionKnowledgeGroup.Objects, ArithmeticOperation.Subtract, 2, context.Item3, 99, uid,
                "{name} " + v + " {a} {unit},", "{other} " + v + " {b} {unit}.",
                "Hỏi số {unit} của {name} nhiều hơn {other} bao nhiêu?", "Chênh lệch số {unit} của hai bên là:",
                "{name} " + e + " {a} {unit},", "{other} " + e + " {b} {unit}.",
                "How many more {unit} does {name} have than {other}?", "The difference in their numbers of {unit} is:");
            var group = CountedActivityGroup(p);
            string va = p switch {
                "reading" => "Mỗi ngày {name} đọc được {a} {unit_a},",
                "sports" => "Mỗi trận đấu đội của {name} ghi được {a} {unit_a},",
                "visits" => "Mỗi ngày khu vui chơi đón {a} {unit_a},",
                "poultry" => "Mỗi chuồng có {a} {unit_a},",
                _ => "Mỗi " + group.Vi + " " + v + " {a} {unit_a}," };
            string ea = p switch {
                "reading" => "{name} reads {a} {unit_a} per reading day,",
                "sports" => "The team scores {a} {unit_a} per match,",
                "visits" => "The visitor centre records {a} {unit_a} per opening day,",
                "poultry" => "Each pen holds {a} {unit_a},",
                _ => "Each " + group.En + " " + e + " {a} {unit_a}," };
            Add(p + "-groups", QuestionKnowledgeGroup.Objects, ArithmeticOperation.Multiply, 1, context.Item3, 30, uid,
                va, "có {b} {unit_b} với số lượng như nhau.",
                "Hỏi tổng số {unit} là bao nhiêu?", "Tổng số {unit} là:",
                ea, "there are {b} such {unit_b} with equal numbers.",
                "How many {unit} are there altogether?", "The total number of {unit} is:");
            Add(p + "-share", QuestionKnowledgeGroup.Objects, ArithmeticOperation.Divide, 1, context.Item3, 30, uid,
                "Có tổng cộng {a} {unit_a},", "số lượng bằng nhau trong {b} {unit_b}.",
                "Hỏi mỗi " + group.Vi + " có bao nhiêu {unit}?", "Số {unit} mỗi " + group.Vi + " có là:",
                "There are a total of {a} {unit_a},", "the numbers are equal across {b} {unit_b}.",
                "How many {unit} are there per " + group.En + "?", "The number of {unit} per " + group.En + " is:");
        }

        Add("age-larger", QuestionKnowledgeGroup.Time, ArithmeticOperation.Add, 1, 90, 9, "years",
            "{other} năm nay {a} {unit},", "{name} hơn {other} {b} {unit}.", "Hỏi năm nay {name} bao nhiêu {unit}?", "Tuổi của {name} năm nay là:",
            "{other} is {a} {unit} old,", "{name} is {b} {unit} older than {other}.", "How many {unit} old is {name}?", "The age of {name} is:");
        Add("age-difference", QuestionKnowledgeGroup.Time, ArithmeticOperation.Subtract, 1, 90, 9, "years",
            "{name} năm nay {a} {unit},", "{other} năm nay {b} {unit}.", "Hỏi {name} hơn {other} bao nhiêu {unit}?", "Số tuổi {name} hơn {other} là:",
            "{name} is {a} {unit} old,", "{other} is {b} {unit} old.", "How many {unit} older is {name} than {other}?", "The difference in their ages is:");
        Add("age-inverse-larger", QuestionKnowledgeGroup.Time, ArithmeticOperation.Add, 4, 90, 9, "years",
            "{other} năm nay {a} {unit},", "{other} kém {name} {b} {unit}.", "Hỏi năm nay {name} bao nhiêu {unit}?", "Tuổi của {name} năm nay là:",
            "{other} is {a} {unit} old,", "{other} is {b} {unit} younger than {name}.", "How many {unit} old is {name}?", "The age of {name} is:");
        Add("age-smaller", QuestionKnowledgeGroup.Time, ArithmeticOperation.Subtract, 4, 90, 9, "years",
            "{other} năm nay {a} {unit},", "{other} hơn {name} {b} {unit}.", "Hỏi năm nay {name} bao nhiêu {unit}?", "Tuổi của {name} năm nay là:",
            "{other} is {a} {unit} old,", "{other} is {b} {unit} older than {name}.", "How many {unit} old is {name}?", "The age of {name} is:");
        Add("age-past-total", QuestionKnowledgeGroup.Time, ArithmeticOperation.Add, 3, 90, 9, "years",
            "Vào một sinh nhật trước đây, {name} được {a} {unit_a},", "đến sinh nhật năm nay đã qua {b} {unit_b}.", "Hỏi năm nay {name} bao nhiêu {unit}?", "Tuổi của {name} năm nay là:",
            "At an earlier birthday, {name} was {a} {unit_a} old,", "the time elapsed by this year's birthday is {b} {unit_b}.", "How many {unit} old is {name} this year?", "The current age of {name} is:");
        Add("age-future-left", QuestionKnowledgeGroup.Time, ArithmeticOperation.Subtract, 5, 90, 9, "years",
            "Vào một sinh nhật về sau, {name} sẽ được {a} {unit_a},", "từ sinh nhật năm nay đến khi đó còn {b} {unit_b}.", "Hỏi năm nay {name} bao nhiêu {unit}?", "Tuổi của {name} năm nay là:",
            "At a future birthday, {name} will be {a} {unit_a} old,", "that birthday is {b} {unit_b} after this year's birthday.", "How many {unit} old is {name} this year?", "The current age of {name} is:");
        Add("schedule-total", QuestionKnowledgeGroup.Time, ArithmeticOperation.Add, 1, 180, 9, "minutes",
            "{name} đọc sách trong {a} {unit},", "sau đó làm bài tập trong {b} {unit}.", "Hỏi hai hoạt động kéo dài tất cả bao nhiêu {unit}?", "Tổng thời gian của hai hoạt động là:",
            "{name} reads for {a} {unit},", "then does homework for {b} {unit}.", "How many {unit} do the two activities take altogether?", "The total duration of the activities is:");
        Add("schedule-left", QuestionKnowledgeGroup.Time, ArithmeticOperation.Subtract, 1, 180, 9, "minutes",
            "{name} dự định luyện đàn trong {a} {unit},", "{name} đã luyện được {b} {unit}.", "Hỏi {name} còn cần luyện bao nhiêu {unit}?", "Thời gian cần luyện thêm là:",
            "{name} plans to practise music for {a} {unit},", "{name} has practised for {b} {unit}.", "How many {unit} of practice remain?", "The remaining practice time is:");
        Add("schedule-groups", QuestionKnowledgeGroup.Time, ArithmeticOperation.Multiply, 1, 600, 7, "minutes",
            "Mỗi ngày {name} đọc sách trong {a} {unit_a},", "{name} duy trì như vậy trong {b} {unit_b}.", "Hỏi tổng thời gian đọc sách là bao nhiêu {unit}?", "Tổng thời gian đọc sách là:",
            "{name} reads for {a} {unit_a} each day,", "{name} does this for {b} {unit_b}.", "How many {unit} does {name} spend reading altogether?", "The total reading time is:");
        Add("schedule-share", QuestionKnowledgeGroup.Time, ArithmeticOperation.Divide, 1, 600, 7, "minutes",
            "{name} dành tổng cộng {a} {unit_a} để đọc sách,", "thời gian đọc mỗi ngày bằng nhau trong {b} {unit_b}.", "Hỏi mỗi ngày {name} đọc sách bao nhiêu {unit}?", "Thời gian đọc sách mỗi ngày là:",
            "{name} spends a total of {a} {unit_a} reading,", "the daily reading time is equal over {b} {unit_b}.", "How many {unit} does {name} read each day?", "The daily reading time is:");

        Add("water-total", QuestionKnowledgeGroup.Measurement, ArithmeticOperation.Add, 1, 1000, 20, "litres",
            "Bồn nước thứ nhất chứa {a} {unit},", "bồn thứ hai chứa {b} {unit}.", "Hỏi hai bồn chứa tất cả bao nhiêu {unit} nước?", "Tổng lượng nước trong hai bồn là:",
            "The first tank contains {a} {unit},", "the second tank contains {b} {unit}.", "How many {unit} of water are in the tanks altogether?", "The total volume of water is:");
        Add("water-left", QuestionKnowledgeGroup.Measurement, ArithmeticOperation.Subtract, 1, 1000, 20, "litres",
            "Bồn có {a} {unit} nước,", "{name} dùng {b} {unit} để tưới cây.", "Hỏi bồn còn lại bao nhiêu {unit} nước?", "Lượng nước còn lại là:",
            "A tank contains {a} {unit} of water,", "{name} uses {b} {unit} to water plants.", "How many {unit} of water remain?", "The remaining volume of water is:");
        Add("water-groups", QuestionKnowledgeGroup.Measurement, ArithmeticOperation.Multiply, 1, 1000, 20, "litres",
            "Mỗi can chứa {a} {unit_a} nước,", "{name} chuẩn bị {b} {unit_b} đầy như vậy.", "Hỏi các can chứa tất cả bao nhiêu {unit} nước?", "Tổng lượng nước trong các can là:",
            "Each can contains {a} {unit_a} of water,", "{name} prepares {b} full {unit_b} like this.", "How many {unit} of water are in the cans altogether?", "The total volume of water in the cans is:");
        Add("water-share", QuestionKnowledgeGroup.Measurement, ArithmeticOperation.Divide, 1, 1000, 20, "litres",
            "{name} có {a} {unit_a} nước,", "{name} rót đều vào {b} {unit_b}.", "Hỏi mỗi can chứa bao nhiêu {unit} nước?", "Lượng nước trong mỗi can là:",
            "{name} has {a} {unit_a} of water,", "{name} pours it equally into {b} {unit_b}.", "How many {unit} of water are in each can?", "The volume of water per can is:");
        scenes.AddRange(scenes.Where(s => s.Id.StartsWith("water-", StringComparison.Ordinal)).Select(s => s with {
            Id = s.Id.Replace("water-", "water-convert-"), UnitId = "ml", MinimumStars = 4, Capacity = 50000, Conversion = 1000,
            ViA = s.ViA.Replace("{unit}", "{unit_a}"), EnA = s.EnA.Replace("{unit}", "{unit_a}") }).ToArray());

        Add("garden-area-total", QuestionKnowledgeGroup.Geometry, ArithmeticOperation.Add, 1, 5000, 50, "m²",
            "Khu trồng rau có diện tích {a} {unit},", "khu trồng hoa riêng biệt có diện tích {b} {unit}.", "Hỏi hai khu có tổng diện tích bao nhiêu {unit}?", "Tổng diện tích hai khu là:",
            "The vegetable plot has an area of {a} {unit},", "the separate flower plot has an area of {b} {unit}.", "What is their total area in {unit}?", "The total area of the two plots is:");
        Add("garden-area-left", QuestionKnowledgeGroup.Geometry, ArithmeticOperation.Subtract, 1, 5000, 50, "m²",
            "Vườn có diện tích {a} {unit},", "phần đã trồng rau có diện tích {b} {unit}.", "Hỏi phần chưa trồng rau có diện tích bao nhiêu {unit}?", "Diện tích phần chưa trồng rau là:",
            "A garden has an area of {a} {unit},", "vegetables occupy {b} {unit} of it.", "What area in {unit} is not planted with vegetables?", "The unplanted area is:");
        Add("garden-area-groups", QuestionKnowledgeGroup.Geometry, ArithmeticOperation.Multiply, 1, 5000, 50, "m²",
            "Một luống rau hình chữ nhật dài {a} {unit_a},", "rộng {b} {unit_b}.", "Hỏi diện tích luống rau là bao nhiêu {unit}?", "Diện tích luống rau là:",
            "A rectangular vegetable bed is {a} {unit_a} long,", "it is {b} {unit_b} wide.", "What is its area in {unit}?", "The area of the vegetable bed is:");
        Add("garden-side-share", QuestionKnowledgeGroup.Geometry, ArithmeticOperation.Divide, 1, 5000, 50, "m",
            "Một luống rau hình chữ nhật có diện tích {a} {unit_a},", "chiều rộng là {b} {unit_b}.", "Hỏi chiều dài luống rau là bao nhiêu {unit}?", "Chiều dài luống rau là:",
            "A rectangular vegetable bed has an area of {a} {unit_a},", "its width is {b} {unit_b}.", "What is its length in {unit}?", "The length of the vegetable bed is:");
        Add("garden-perimeter", QuestionKnowledgeGroup.Geometry, ArithmeticOperation.Add, 3, 1000, 50, "m",
            "Một vườn hình chữ nhật dài {a} {unit_a},", "rộng {b} {unit_b}.", "Hỏi chu vi vườn là bao nhiêu {unit}?", "Chu vi vườn là:",
            "A rectangular garden is {a} {unit_a} long,", "it is {b} {unit_b} wide.", "What is its perimeter in {unit}?", "The perimeter of the garden is:");
        Add("tank-volume-groups", QuestionKnowledgeGroup.Geometry, ArithmeticOperation.Multiply, 4, 500, 5, "m³",
            "Một bể hình hộp chữ nhật có diện tích đáy {a} {unit_a},", "chiều cao trong lòng bể là {b} {unit_b}.", "Hỏi thể tích trong lòng bể là bao nhiêu {unit}?", "Thể tích trong lòng bể là:",
            "A rectangular tank has a base area of {a} {unit_a},", "its internal height is {b} {unit_b}.", "What is its internal volume in {unit}?", "The internal volume of the tank is:");
        Add("tank-height-share", QuestionKnowledgeGroup.Geometry, ArithmeticOperation.Divide, 4, 500, 50, "m",
            "Một bể hình hộp chữ nhật có thể tích trong lòng bể {a} {unit_a},", "diện tích đáy là {b} {unit_b}.", "Hỏi chiều cao trong lòng bể là bao nhiêu {unit}?", "Chiều cao trong lòng bể là:",
            "A rectangular tank has an internal volume of {a} {unit_a},", "its base area is {b} {unit_b}.", "What is its internal height in {unit}?", "The internal height of the tank is:");

        Add("packing-total", QuestionKnowledgeGroup.Packaging, ArithmeticOperation.Add, 1, 20000, 99, "notebooks",
            "{name} chuẩn bị {a} {unit} ở bàn thứ nhất,", "ở bàn thứ hai có thêm {b} {unit} riêng biệt.", "Hỏi có tất cả bao nhiêu {unit} để đóng gói?", "Tổng số {unit} để đóng gói là:",
            "{name} prepares {a} {unit} on the first table,", "a separate second table holds {b} {unit}.", "How many {unit} are ready for packing altogether?", "The total number of {unit} ready for packing is:");
        Add("packing-left", QuestionKnowledgeGroup.Packaging, ArithmeticOperation.Subtract, 1, 20000, 99, "notebooks",
            "{name} cần đóng gói {a} {unit},", "{name} đã đóng gói {b} {unit}.", "Hỏi còn bao nhiêu {unit} chưa đóng gói?", "Số {unit} chưa đóng gói là:",
            "{name} needs to pack {a} {unit},", "{name} has packed {b} {unit}.", "How many {unit} still need packing?", "The number of {unit} still to pack is:");
        Add("packing-groups", QuestionKnowledgeGroup.Packaging, ArithmeticOperation.Multiply, 1, 20000, 99, "notebooks",
            "Mỗi gói có {a} {unit_a},", "{name} chuẩn bị {b} {unit_b} như vậy.", "Hỏi các gói có tất cả bao nhiêu {unit}?", "Tổng số {unit} trong các gói là:",
            "Each pack holds {a} {unit_a},", "{name} prepares {b} such {unit_b}.", "How many {unit} are in the packs altogether?", "The total number of {unit} in the packs is:");
        Add("packing-share", QuestionKnowledgeGroup.Packaging, ArithmeticOperation.Divide, 1, 20000, 99, "notebooks",
            "{name} có {a} {unit_a},", "{name} chia đều vào {b} {unit_b}.", "Hỏi mỗi gói có bao nhiêu {unit}?", "Số {unit} trong mỗi gói là:",
            "{name} has {a} {unit_a},", "{name} shares them equally among {b} {unit_b}.", "How many {unit} are in each pack?", "The number of {unit} per pack is:");
        Add("packing-count-groups", QuestionKnowledgeGroup.Packaging, ArithmeticOperation.Divide, 2, 20000, 99, "notebooks",
            "{name} có {a} {unit_a},", "mỗi gói phải có {b} {unit_b}.", "Hỏi đóng được bao nhiêu gói đầy?", "Số gói đầy đóng được là:",
            "{name} has {a} {unit_a},", "each full pack holds {b} {unit_b}.", "How many full packs can be made?", "The number of full packs is:");
        Add("packing-remainder", QuestionKnowledgeGroup.Packaging, ArithmeticOperation.Divide, 3, 20000, 99, "notebooks",
            "{name} có {a} {unit_a},", "{name} đóng các gói đầy, mỗi gói có {b} {unit_b}.", "Hỏi còn thừa bao nhiêu {unit} không đủ đóng một gói?", "Số {unit} còn thừa là:",
            "{name} has {a} {unit_a},", "{name} fills packs with {b} {unit_b} in each.", "How many {unit} remain outside the full packs?", "The number of {unit} left over is:");
        Add("packing-minimum", QuestionKnowledgeGroup.Packaging, ArithmeticOperation.Divide, 4, 20000, 99, "packs",
            "{name} cần đựng hết {a} {unit_a},", "mỗi gói đựng được nhiều nhất {b} {unit_b}.", "Hỏi phải chuẩn bị ít nhất bao nhiêu {unit}?", "Số {unit} ít nhất cần chuẩn bị là:",
            "{name} needs to pack all {a} {unit_a},", "each pack can hold at most {b} {unit_b}.", "What is the minimum number of {unit} needed?", "The minimum number of {unit} needed is:");

        Add("production-total", QuestionKnowledgeGroup.Production, ArithmeticOperation.Add, 1, 50000, 30, "products",
            "Ca sáng của xưởng làm được {a} {unit},", "ca chiều làm được thêm {b} {unit} riêng biệt.", "Hỏi hai ca làm được tất cả bao nhiêu {unit}?", "Tổng số {unit} của hai ca là:",
            "A workshop makes {a} {unit} in the morning shift,", "it makes another {b} {unit} in the afternoon shift.", "How many {unit} are made across both shifts?", "The total number of {unit} made is:");
        Add("production-left", QuestionKnowledgeGroup.Production, ArithmeticOperation.Subtract, 1, 50000, 30, "products",
            "Xưởng cần hoàn thành {a} {unit},", "xưởng đã làm được {b} {unit}.", "Hỏi xưởng còn cần làm bao nhiêu {unit}?", "Số {unit} cần làm thêm là:",
            "A workshop needs to finish {a} {unit},", "it has completed {b} {unit}.", "How many {unit} still need to be made?", "The number of {unit} still needed is:");
        Add("production-groups", QuestionKnowledgeGroup.Production, ArithmeticOperation.Multiply, 1, 50000, 30, "products",
            "Mỗi ca xưởng làm được {a} {unit_a},", "xưởng làm {b} {unit_b} với sản lượng như nhau.", "Hỏi xưởng làm được tất cả bao nhiêu {unit}?", "Tổng số {unit} xưởng làm được là:",
            "A workshop makes {a} {unit_a} per shift,", "it works {b} {unit_b} at this output.", "How many {unit} does it make altogether?", "The total number of {unit} made is:");
        Add("production-share", QuestionKnowledgeGroup.Production, ArithmeticOperation.Divide, 1, 50000, 30, "products",
            "Xưởng làm được tổng cộng {a} {unit_a},", "sản lượng mỗi ca bằng nhau trong {b} {unit_b}.", "Hỏi mỗi ca xưởng làm được bao nhiêu {unit}?", "Số {unit} xưởng làm được mỗi ca là:",
            "A workshop makes a total of {a} {unit_a},", "its output is equal across {b} {unit_b}.", "How many {unit} are made per shift?", "The number of {unit} made per shift is:");

        Add("survey-total", QuestionKnowledgeGroup.Data, ArithmeticOperation.Add, 1, 5000, 30, "students",
            "Khảo sát có {a} {unit} chỉ thích bóng đá,", "{b} {unit} khác chỉ thích cầu lông.", "Hỏi hai nhóm có tổng cộng bao nhiêu {unit}?", "Tổng số {unit} trong hai nhóm là:",
            "A survey records {a} {unit} who prefer only football,", "another {b} {unit} prefer only badminton.", "How many {unit} are in the two groups altogether?", "The total number of {unit} in the groups is:");
        Add("survey-difference", QuestionKnowledgeGroup.Data, ArithmeticOperation.Subtract, 1, 5000, 30, "students",
            "Khảo sát có {a} {unit} thích bóng đá,", "{b} {unit} thích cầu lông.", "Hỏi số {unit} thích bóng đá nhiều hơn số thích cầu lông bao nhiêu?", "Chênh lệch số {unit} là:",
            "A survey records {a} {unit} who like football,", "{b} {unit} like badminton.", "How many more {unit} like football than badminton?", "The difference in the numbers of {unit} is:");
        Add("visitors-groups", QuestionKnowledgeGroup.Data, ArithmeticOperation.Multiply, 1, 50000, 30, "visits",
            "Bảng thống kê ghi mỗi ngày có {a} {unit_a},", "số lượt bằng nhau trong {b} {unit_b}.", "Hỏi tổng số lượt trong thời gian đó là bao nhiêu {unit}?", "Tổng số {unit} được ghi nhận là:",
            "A record shows {a} {unit_a} each day,", "the daily number is the same for {b} {unit_b}.", "How many {unit} are recorded altogether?", "The total number of {unit} recorded is:");
        Add("visitors-average", QuestionKnowledgeGroup.Data, ArithmeticOperation.Divide, 1, 50000, 30, "visits",
            "Bảng thống kê ghi tổng cộng {a} {unit_a},", "thời gian thống kê là {b} {unit_b}.", "Hỏi trung bình mỗi ngày có bao nhiêu {unit}?", "Số {unit} trung bình mỗi ngày là:",
            "A record shows a total of {a} {unit_a},", "the recording period is {b} {unit_b}.", "What is the average number of {unit} per day?", "The average number of {unit} per day is:");
        Add("visitors-average-groups", QuestionKnowledgeGroup.Data, ArithmeticOperation.Multiply, 4, 50000, 30, "visits",
            "Thống kê cho biết trung bình mỗi ngày có {a} {unit_a},", "đợt thống kê kéo dài {b} {unit_b}.", "Hỏi cả đợt có tổng cộng bao nhiêu {unit}?", "Tổng số {unit} trong đợt là:",
            "A record shows an average of {a} {unit_a} per day,", "the period covers {b} {unit_b}.", "How many {unit} are recorded for the entire period?", "The total number of {unit} for the period is:");
        Add("production-recover-groups", QuestionKnowledgeGroup.Production, ArithmeticOperation.Multiply, 5, 50000, 30, "products",
            "Xưởng đã giao hết sản phẩm làm trong {b} {unit_a},", "trước khi giao, mỗi ca làm được {a} {unit_b}.", "Hỏi xưởng đã giao tất cả bao nhiêu {unit}?", "Tổng số {unit} xưởng đã giao là:",
            "A workshop dispatches all the goods made in {b} {unit_a},", "each shift previously made {a} {unit_b}.", "How many {unit} are dispatched altogether?", "The total number of {unit} dispatched is:");
        foreach (var (prefix, unit, viPlace, enPlace, cap) in new[] {
            ("stationery", "notebooks", "xưởng vở", "stationery workshop", 50000),
            ("bread", "rolls", "lò bánh", "bakery", 10000),
            ("nursery", "seedlings", "vườn ươm", "plant nursery", 10000) })
            scenes.AddRange(scenes.Where(s => s.Group == QuestionKnowledgeGroup.Production && s.Id.StartsWith("production-", StringComparison.Ordinal)).Select(s => s with {
                Id = prefix + "-" + s.Id, UnitId = unit, Capacity = cap,
                ViA = s.ViA.Replace("xưởng", viPlace).Replace("Xưởng", viPlace),
                ViB = s.ViB.Replace("xưởng", viPlace), ViQ = s.ViQ.Replace("xưởng", viPlace), ViLead = s.ViLead.Replace("xưởng", viPlace),
                EnA = s.EnA.Replace("workshop", enPlace), EnQ = s.EnQ.Replace("workshop", enPlace)
            }).ToArray());
    }

    private static readonly IReadOnlyDictionary<string, (string Vi, string En, string Singular)> ExpandedUnits =
        new Dictionary<string, (string, string, string)> {
            ["years"] = ("tuổi", "years", "year"), ["minutes"] = ("phút", "minutes", "minute"),
            ["litres"] = ("l", "litres", "litre"), ["packs"] = ("gói", "packs", "pack"),
            ["products"] = ("sản phẩm", "products", "product"), ["visits"] = ("lượt tham gia", "visits", "visit"),
            ["students"] = ("học sinh", "students", "student"), ["points"] = ("điểm", "points", "point"),
            ["chickens"] = ("con gà", "chickens", "chicken"), ["pages"] = ("trang sách", "pages", "page"),
            ["seedlings"] = ("cây con", "seedlings", "seedling"), ["bottles"] = ("chai nhựa", "plastic bottles", "plastic bottle"),
            ["rolls"] = ("ổ bánh mì", "bread rolls", "bread roll"), ["mangoes"] = ("quả xoài", "mangoes", "mango"),
            ["meals"] = ("suất ăn", "meals", "meal"), ["eggs"] = ("quả trứng", "eggs", "egg"),
            ["paper-flowers"] = ("bông hoa giấy", "paper flowers", "paper flower"),
            ["chairs"] = ("chiếc ghế", "chairs", "chair"), ["responses"] = ("phiếu trả lời", "responses", "response"),
            ["gift-packs"] = ("gói quà", "gift packs", "gift pack"),
            ["passenger-journeys"] = ("lượt khách", "passenger journeys", "passenger journey"),
            ["litres-water"] = ("lít nước", "litres of water", "litre of water") };
    private static string? ExpandedUnit(string id, AppLanguage language, bool singular) => ExpandedUnits.TryGetValue(id, out var u)
        ? language == AppLanguage.Vietnamese ? u.Vi : singular ? u.Singular : u.En : null;
    private static string? ExpandedSingular(string unit) => ExpandedUnits.Values.FirstOrDefault(u => u.En == unit).Singular;

    private static (string Vi, string En) CountedActivityGroup(string prefix) => prefix switch {
        "classroom" => ("nhóm học sinh", "student group"), "poultry" => ("chuồng", "pen"),
        "reading" => ("ngày đọc sách", "reading day"), "sports" => ("trận đấu", "match"),
        "visits" => ("ngày mở cửa", "opening day"), "planting" => ("đội trồng cây", "planting team"),
        "recycling" => ("đội thu gom", "recycling team"), "bakery" => ("lò bánh", "bakery"),
        "harvest" => ("nông trại", "farm"), _ => ("nhóm", "group") };
    private static readonly string[] CountedPrefixes = ["classroom", "poultry", "reading", "sports",
        "visits", "planting", "recycling", "bakery", "harvest"];
    private static string? CountedPrefix(AppliedQuestionScene s) => CountedPrefixes.FirstOrDefault(p => s.Id.StartsWith(p + "-", StringComparison.Ordinal));
    private static string? SceneGroupUnit(AppliedQuestionScene s, AppLanguage language)
    {
        if (s.StoryContextId is { } id) return QuizStoryContextCatalog.Find(id).Period(language);
        if (s.Group == QuestionKnowledgeGroup.Packaging) return language == AppLanguage.Vietnamese ? "gói" : "pack";
        if (s.Group == QuestionKnowledgeGroup.Objects && CountedPrefix(s) is { } prefix) {
            var group = CountedActivityGroup(prefix);
            return language == AppLanguage.Vietnamese ? group.Vi : group.En;
        }
        return null;
    }
    private static (string A, string B)? ExpandedInputUnits(AppliedQuestionScene s, AppLanguage lang)
    {
        bool vi = lang == AppLanguage.Vietnamese;
        string unit = AnswerUnit(s, lang);
        if (s.StoryContextId is { } id) {
            var context = QuizStoryContextCatalog.Find(id);
            return s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide
                ? (unit, vi ? context.ViPeriod : PluralGroup(context.EnPeriod)) : (unit, unit);
        }
        if (s.Group == QuestionKnowledgeGroup.Production && s.Id.EndsWith("recover-groups", StringComparison.Ordinal)) return (vi ? "ca" : "shifts", unit);
        if (s.Id is "age-past-total" or "age-future-left") return (unit, vi ? "năm" : "years");
        if (s.Group == QuestionKnowledgeGroup.Objects && CountedPrefix(s) is { } prefix
            && s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide) {
            var group = CountedActivityGroup(prefix);
            return (unit, vi ? group.Vi : PluralGroup(group.En));
        }
        if (s.Group == QuestionKnowledgeGroup.Time && s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
            return (unit, vi ? "ngày" : "days");
        if (s.Group == QuestionKnowledgeGroup.Measurement)
            return (s.Conversion == 1 ? unit : "l", s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide ? vi ? "can" : "cans" : unit);
        if (s.Group == QuestionKnowledgeGroup.Geometry)
            return s.Id switch {
                "garden-area-groups" or "garden-perimeter" => ("m", "m"),
                "garden-side-share" => ("m²", "m"), "tank-volume-groups" => ("m²", "m"),
                "tank-height-share" => ("m³", "m²"), _ => (unit, unit) };
        if (s.Group == QuestionKnowledgeGroup.Packaging && s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
            return (AnswerUnit(s with { UnitId = "notebooks" }, lang), s.Id.EndsWith("count-groups", StringComparison.Ordinal)
                || HasDerivedAnswer(s) ? AnswerUnit(s with { UnitId = "notebooks" }, lang) : vi ? "gói" : "packs");
        if (s.Group == QuestionKnowledgeGroup.Production && s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
            return (unit, vi ? "ca" : "shifts");
        if (s.Group == QuestionKnowledgeGroup.Data && s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
            return (unit, vi ? "ngày" : "days");
        return null;
    }
    private static string PluralGroup(string singular) => singular.EndsWith("bakery", StringComparison.Ordinal)
        ? singular[..^1] + "ies" : singular.EndsWith("match", StringComparison.Ordinal) ? singular + "es" : singular + "s";
    public static AppliedArithmeticReasoning? Reasoning(BasicQuestionContract c)
    {
        var rule = c.SceneId switch { "garden-perimeter" => AppliedArithmeticRule.RectanglePerimeter,
            "packing-remainder" => AppliedArithmeticRule.Remainder, "packing-minimum" => AppliedArithmeticRule.MinimumGroups,
            _ => AppliedArithmeticRule.Ordinary };
        return rule == AppliedArithmeticRule.Ordinary ? null : new(c.Left, c.Right, rule);
    }
    private static bool HasDerivedAnswer(AppliedQuestionScene s) => s.Id is "garden-perimeter" or "packing-remainder" or "packing-minimum";
    private static (int, int)? ExpandedNumbers(AppliedQuestionScene s, CurriculumTier tier, int cap, int factor, Random random)
    {
        if (s.StoryContextId is not null && s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide) {
            int count = random.Next(2, Math.Min(factor, cap / 2) + 1);
            int per = random.Next(1, Math.Max(2, cap / count + 1));
            return s.Operation == ArithmeticOperation.Divide ? (per * count, count) : (per, count);
        }
        if (s.Group == QuestionKnowledgeGroup.Measurement && s.Conversion != 1
            && s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide) {
            int step = tier == CurriculumTier.FiveStars ? 100 : 1000;
            int count = random.Next(2, Math.Min(factor, cap / step) + 1);
            int per = random.Next(1, Math.Min(20000, cap / count) / step + 1) * step;
            return s.Operation == ArithmeticOperation.Divide ? (per * count, count) : (per, count);
        }
        if (s.Id == "garden-perimeter") {
            int width = random.Next(1, Math.Max(2, cap / 8));
            return (random.Next(width, Math.Max(width + 1, cap / 2 - width + 1)), width);
        }
        if (s.Id is "packing-remainder" or "packing-minimum") {
            int size = random.Next(2, Math.Min(factor, cap / 3) + 1);
            int full = random.Next(1, Math.Max(2, (cap - size + 1) / size));
            return (full * size + random.Next(1, size), size);
        }
        if (s.Id is "garden-area-groups" or "garden-side-share" or "tank-volume-groups" or "tank-height-share") {
            if (s.Id == "tank-height-share") {
                int area = random.Next(2, Math.Min(factor, 30) + 1);
                return (area * random.Next(1, 6), area);
            }
            int width = random.Next(2, Math.Min((int)Math.Sqrt(cap), Math.Min(factor, s.Id.StartsWith("tank-", StringComparison.Ordinal) ? 5 : 20)) + 1);
            int length = random.Next(width, Math.Max(width + 1, Math.Min(cap / width, s.Id.StartsWith("tank-", StringComparison.Ordinal) ? 30 : 100) + 1));
            return s.Operation == ArithmeticOperation.Divide ? (width * length, width) : (length, width);
        }
        if (s.Conversion == 1 && s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide
            && s.Group is QuestionKnowledgeGroup.Time or QuestionKnowledgeGroup.Measurement or QuestionKnowledgeGroup.Production or QuestionKnowledgeGroup.Data or QuestionKnowledgeGroup.Packaging) {
            int count = random.Next(2, Math.Min(factor, cap / 2) + 1);
            int perMax = s.Group switch { QuestionKnowledgeGroup.Time => 60, QuestionKnowledgeGroup.Measurement => 20,
                QuestionKnowledgeGroup.Packaging => 100, _ => 5000 };
            int per = random.Next(1, Math.Max(2, Math.Min(perMax, cap / count) + 1));
            return s.Operation == ArithmeticOperation.Divide ? (per * count, count) : (per, count);
        }
        return null;
    }
    private static bool ExpandedFactsValid(BasicQuestionContract c, AppliedQuestionScene s)
    {
        if (s.StoryContextId is { } id) return c.Left <= QuizStoryContextCatalog.Find(id).MaximumPerPeriod;
        if (Reasoning(c) is { } r) return r.Matches(c.Expression) && (s.Id != "garden-perimeter" || c.Left >= c.Right);
        if (s.Id is "garden-area-groups" or "garden-side-share")
            return (c.Operation == ArithmeticOperation.Divide ? c.Answer : c.Left) >= c.Right && c.Right <= 20;
        if (s.Id == "tank-volume-groups") return c.Right <= 5 && c.Left <= 30;
        if (s.Id == "tank-height-share") return c.Answer <= 5 && c.Right <= 30;
        if (s.Group == QuestionKnowledgeGroup.Time && c.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
            return (c.Operation == ArithmeticOperation.Divide ? c.Answer : c.Left) <= 60;
        if (s.Group == QuestionKnowledgeGroup.Measurement && s.Conversion == 1 && c.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
            return (c.Operation == ArithmeticOperation.Divide ? c.Answer : c.Left) <= 20;
        if (s.Group == QuestionKnowledgeGroup.Measurement && s.Conversion != 1 && c.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
            return (c.Operation == ArithmeticOperation.Divide ? c.Answer : c.Left) <= 20000;
        return true;
    }
    private static void SetSceneActors(AppliedQuestionScene s, AppLanguage lang, int a, int b, ref string name, ref string other)
    {
        bool vi = lang == AppLanguage.Vietnamese;
        // Age problems do not randomly imply that a child is older than a parent.
        if (s.Id.StartsWith("age-", StringComparison.Ordinal)) { name = vi ? "An" : "Alex"; other = vi ? "Bình" : "Sam"; return; }
        if (s.Group == QuestionKnowledgeGroup.Packaging && Math.Max(a, b) > 100) {
            name = vi ? "đội đóng gói" : "the packing team"; other = vi ? "đội phát quà" : "the distribution team"; return;
        }
        string? actor = s.UnitId switch {
            "students" => vi ? "lớp" : "class", "chickens" => vi ? "trang trại" : "farm",
            "visits" => vi ? "khu vui chơi" : "visitor centre", "seedlings" => vi ? "đội trồng cây" : "planting team",
            "bottles" => vi ? "đội thu gom" : "recycling team", "rolls" => vi ? "lò bánh" : "bakery",
            "mangoes" => vi ? "nông trại" : "farm", "points" => vi ? "đội thể thao" : "sports team", _ => null };
        if (s.UnitId == "pages" && Math.Max(a, b) > 100) actor = vi ? "nhóm đọc sách" : "reading group";
        if (actor is not null) {
            name = vi ? actor + " thứ nhất" : "the first " + actor;
            other = vi ? actor + " thứ hai" : "the second " + actor;
        }
    }

    public static QuestionFactTable? FactTable(BasicQuestionContract c)
    {
        if (c.KnowledgeGroup != QuestionKnowledgeGroup.Data) return null;
        bool vi = c.Language == AppLanguage.Vietnamese;
        if (Find(c.SceneId)!.StoryContextId is { } id) {
            var context = QuizStoryContextCatalog.Find(id);
            string firstLabel = c.Operation switch {
                ArithmeticOperation.Add => vi ? "Phần thứ nhất" : "First period",
                ArithmeticOperation.Subtract => vi ? "Mục tiêu" : "Target",
                ArithmeticOperation.Multiply => vi ? "Lượng mỗi " + context.ViPeriod : "Quantity per " + context.EnPeriod,
                _ => vi ? "Tổng lượng" : "Total quantity" };
            string secondLabel = c.Operation switch {
                ArithmeticOperation.Add => vi ? "Phần thứ hai" : "Second period",
                ArithmeticOperation.Subtract => vi ? "Đã hoàn thành" : "Completed",
                _ => vi ? "Số " + context.ViPeriod : "Number of " + QuizStoryContextCatalog.PluralPeriod(context.EnPeriod) };
            return new(vi ? "Dữ kiện" : "Fact", vi ? "Giá trị" : "Value", [
                new(firstLabel, c.Left.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new(secondLabel, c.Right.ToString(System.Globalization.CultureInfo.InvariantCulture)) ]);
        }
        if (c.SceneId.StartsWith("survey-", StringComparison.Ordinal))
            return new(vi ? "Sở thích" : "Preference", vi ? "Số học sinh" : "Students", [
                new(vi ? "Bóng đá" : "Football", c.Left.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new(vi ? "Cầu lông" : "Badminton", c.Right.ToString(System.Globalization.CultureInfo.InvariantCulture)) ]);
        string first = c.SceneId == "visitors-average-groups" ? vi ? "Trung bình lượt/ngày" : "Average visits/day"
            : vi ? c.Operation == ArithmeticOperation.Multiply ? "Lượt mỗi ngày" : "Tổng lượt" : c.Operation == ArithmeticOperation.Multiply ? "Daily visits" : "Total visits";
        return new(vi ? "Dữ kiện" : "Fact", vi ? "Giá trị" : "Value", [
            new(first, c.Left.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(vi ? "Số ngày" : "Days", c.Right.ToString(System.Globalization.CultureInfo.InvariantCulture)) ]);
    }
}
