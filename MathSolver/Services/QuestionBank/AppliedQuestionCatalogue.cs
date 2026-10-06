using MathSolver.Models;
using MathSolver.Services.Core;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

public sealed record AppliedQuestionScene(string Id, QuestionKnowledgeGroup Group, ArithmeticOperation Operation,
    int MinimumStars, int Capacity, int MaxFactor, string UnitId,
    string ViA, string ViB, string ViQ, string ViLead, string EnA, string EnB, string EnQ, string EnLead,
    int Conversion = 1, int RightDisplayFactor = 1)
{
    public BasicQuestionStructure Structure => Operation switch
    {
        ArithmeticOperation.Add when Id.EndsWith("inverse-larger", StringComparison.Ordinal) => BasicQuestionStructure.AddComparisonInverse,
        ArithmeticOperation.Add when Id.EndsWith("larger", StringComparison.Ordinal) => BasicQuestionStructure.AddComparisonMore,
        ArithmeticOperation.Add when Id.EndsWith("original", StringComparison.Ordinal) => BasicQuestionStructure.RecoverInitial,
        ArithmeticOperation.Add when Id.EndsWith("total", StringComparison.Ordinal) => BasicQuestionStructure.Combine,
        ArithmeticOperation.Add => BasicQuestionStructure.Increase,
        ArithmeticOperation.Subtract when Id.EndsWith("smaller", StringComparison.Ordinal) => BasicQuestionStructure.SubComparisonLess,
        ArithmeticOperation.Subtract when Id.EndsWith("inverse-comparison", StringComparison.Ordinal) => BasicQuestionStructure.SubComparisonInverse,
        ArithmeticOperation.Subtract when Id.EndsWith("difference", StringComparison.Ordinal) => BasicQuestionStructure.Difference,
        ArithmeticOperation.Subtract when Id.EndsWith("part", StringComparison.Ordinal) => BasicQuestionStructure.FindPart,
        ArithmeticOperation.Subtract => BasicQuestionStructure.Remaining,
        ArithmeticOperation.Multiply when Id.EndsWith("times", StringComparison.Ordinal) => BasicQuestionStructure.TimesAsMany,
        ArithmeticOperation.Multiply => BasicQuestionStructure.EqualGroups,
        ArithmeticOperation.Divide when Id.EndsWith("times-fewer", StringComparison.Ordinal) => BasicQuestionStructure.TimesFewer,
        ArithmeticOperation.Divide when Id.EndsWith("count-groups", StringComparison.Ordinal) => BasicQuestionStructure.CountGroups,
        _ => BasicQuestionStructure.EqualShare
    };
}

/// <summary>Version 5: knowledge group, star difficulty, context capacity and dimensions.</summary>
public static partial class AppliedQuestionCatalogue
{
    public const int Version = 5;
    public static IReadOnlyList<AppliedQuestionScene> All { get; } = Build().AsReadOnly();
    private static List<AppliedQuestionScene> Build()
    {
        var scenes = new List<AppliedQuestionScene>();
        void Add(string id, QuestionKnowledgeGroup group, ArithmeticOperation op, int stars, int cap, int factor,
            string unit, string va, string vb, string vq, string vl, string ea, string eb, string eq, string el, int conversion = 1)
            => scenes.Add(new(id, group, op, stars, cap, factor, unit, va, vb, vq, vl, ea, eb, eq, el, conversion));
        Add("object-total", QuestionKnowledgeGroup.Objects, ArithmeticOperation.Add, 1, 1000000, 99, "books",
            "{name} có {a} {unit},", "{other} có {b} {unit}.", "Hỏi cả hai có tổng cộng bao nhiêu {unit}?", "Tổng số {unit} của {name} và {other} là:",
            "{name} has {a} {unit},", "{other} has {b} {unit}.", "How many {unit} do they have altogether?", "Their total number of {unit} is:");
        Add("object-original", QuestionKnowledgeGroup.Objects, ArithmeticOperation.Add, 4, 1000000, 99, "books",
            "{name} còn lại {a} {unit},", "trước đó {name} đã chuyển đi {b} {unit}.", "Hỏi lúc đầu {name} có bao nhiêu {unit}?", "Số {unit} lúc đầu của {name} là:",
            "{name} has {a} {unit} left,", "{name} previously sent away {b} {unit}.", "How many {unit} did {name} originally have?", "The original number of {unit} is:");
        Add("object-left", QuestionKnowledgeGroup.Objects, ArithmeticOperation.Subtract, 1, 1000000, 99, "books",
            "{name} có {a} {unit},", "{name} chuyển đi {b} {unit}.", "Hỏi {name} còn lại bao nhiêu {unit}?", "Số {unit} còn lại của {name} là:",
            "{name} has {a} {unit},", "{name} sends away {b} {unit}.", "How many {unit} does {name} have left?", "The number of {unit} remaining is:");
        Add("object-smaller", QuestionKnowledgeGroup.Objects, ArithmeticOperation.Subtract, 4, 1000000, 99, "books",
            "{other} có {a} {unit},", "{other} có nhiều hơn {name} là {b} {unit}.", "Hỏi {name} có bao nhiêu {unit}?", "Số {unit} của {name} là:",
            "{other} has {a} {unit},", "{other} has {b} more {unit} than {name}.", "How many {unit} does {name} have?", "The number of {unit} that {name} has is:");
        Add("object-groups", QuestionKnowledgeGroup.Objects, ArithmeticOperation.Multiply, 1, 1000000, 99, "books",
            "Mỗi {group} có {a} {unit},", "{name} có {b} {group} như vậy.", "Hỏi {name} có tất cả bao nhiêu {unit}?", "Số {unit} mà {name} có là:",
            "Each {group} contains {a} {unit},", "{name} has {b} such {groups}.", "How many {unit} does {name} have in total?", "The total number of {unit} is:");
        Add("object-share", QuestionKnowledgeGroup.Objects, ArithmeticOperation.Divide, 1, 1000000, 99, "books",
            "{name} có {a} {unit},", "{name} chia đều vào {b} {group}.", "Hỏi mỗi {group} có bao nhiêu {unit}?", "Số {unit} trong mỗi {group} là:",
            "{name} has {a} {unit},", "{name} shares them equally among {b} {groups}.", "How many {unit} are in each {group}?", "The number of {unit} per {group} is:");
        Add("object-larger", QuestionKnowledgeGroup.Objects, ArithmeticOperation.Add, 2, 1000000, 99, "books",
            "{other} có {a} {unit},", "{name} có nhiều hơn {other} là {b} {unit}.", "Hỏi {name} có bao nhiêu {unit}?", "Số {unit} của {name} là:",
            "{other} has {a} {unit},", "{name} has {b} more {unit} than {other}.", "How many {unit} does {name} have?", "The number of {unit} that {name} has is:");
        Add("object-difference", QuestionKnowledgeGroup.Objects, ArithmeticOperation.Subtract, 2, 1000000, 99, "books",
            "{name} có {a} {unit},", "{other} có {b} {unit}.", "Hỏi {name} có nhiều hơn {other} bao nhiêu {unit}?", "Số {unit} mà {name} có nhiều hơn {other} là:",
            "{name} has {a} {unit},", "{other} has {b} {unit}.", "How many more {unit} does {name} have than {other}?", "The difference in their numbers of {unit} is:");
        Add("object-part", QuestionKnowledgeGroup.Objects, ArithmeticOperation.Subtract, 3, 1000000, 99, "books",
            "{name} và {other} có tổng cộng {a} {unit},", "riêng {other} có {b} {unit}.", "Hỏi {name} có bao nhiêu {unit}?", "Số {unit} của {name} là:",
            "{name} and {other} have a total of {a} {unit},", "{other} alone has {b} {unit}.", "How many {unit} does {name} have?", "The number of {unit} that {name} has is:");
        Add("object-times", QuestionKnowledgeGroup.Objects, ArithmeticOperation.Multiply, 3, 1000000, 99, "books",
            "{other} có {a} {unit},", "{name} có số {unit} gấp {b} lần {other}.", "Hỏi {name} có bao nhiêu {unit}?", "Số {unit} của {name} là:",
            "{other} has {a} {unit},", "{name} has {b} times as many {unit} as {other}.", "How many {unit} does {name} have?", "The number of {unit} that {name} has is:");
        Add("object-times-fewer", QuestionKnowledgeGroup.Objects, ArithmeticOperation.Divide, 3, 1000000, 99, "books",
            "{other} có {a} {unit},", "số {unit} của {other} gấp {b} lần số {unit} của {name}.", "Hỏi {name} có bao nhiêu {unit}?", "Số {unit} của {name} là:",
            "{other} has {a} {unit},", "{other} has {b} times as many {unit} as {name}.", "How many {unit} does {name} have?", "The number of {unit} that {name} has is:");
        Add("object-count-groups", QuestionKnowledgeGroup.Objects, ArithmeticOperation.Divide, 4, 1000000, 99, "books",
            "{name} có {a} {unit},", "mỗi {group} chứa {b} {unit}.", "Hỏi {name} xếp đủ được bao nhiêu {group}?", "Số {group} xếp đủ được là:",
            "{name} has {a} {unit},", "each {group} holds {b} {unit}.", "How many {groups} can {name} fill?", "The number of {groups} filled is:");
        Add("object-inverse-larger", QuestionKnowledgeGroup.Objects, ArithmeticOperation.Add, 5, 1000000, 99, "books",
            "{other} có {a} {unit},", "số {unit} của {other} ít hơn {name} là {b} {unit}.", "Hỏi {name} có bao nhiêu {unit}?", "Số {unit} của {name} là:",
            "{other} has {a} {unit},", "{other} has {b} fewer {unit} than {name}.", "How many {unit} does {name} have?", "The number of {unit} that {name} has is:");
        Add("object-inverse-comparison", QuestionKnowledgeGroup.Objects, ArithmeticOperation.Subtract, 5, 1000000, 99, "books",
            "{other} có {a} {unit},", "số {unit} của {name} ít hơn {other} là {b} {unit}.", "Hỏi {name} có bao nhiêu {unit}?", "Số {unit} của {name} là:",
            "{other} has {a} {unit},", "{name} has {b} fewer {unit} than {other}.", "How many {unit} does {name} have?", "The number of {unit} that {name} has is:");
        Add("object-recover-groups", QuestionKnowledgeGroup.Objects, ArithmeticOperation.Multiply, 5, 1000000, 99, "books",
            "{name} đã phát hết số {unit} trong {b} {groups},", "mỗi {group} trước khi phát có {a} {unit}.", "Hỏi lúc đầu {name} có tất cả bao nhiêu {unit}?", "Số {unit} lúc đầu của {name} là:",
            "{name} has given away all the {unit} in {b} {groups},", "each {group} originally held {a} {unit}.", "How many {unit} did {name} originally have altogether?", "The original total number of {unit} is:");
        Add("object-recover-share", QuestionKnowledgeGroup.Objects, ArithmeticOperation.Divide, 5, 1000000, 99, "books",
            "{name} thu lại tổng cộng {a} {unit},", "thu hết từ {b} {groups} có số {unit} bằng nhau lúc đầu.", "Hỏi mỗi {group} lúc đầu có bao nhiêu {unit}?", "Số {unit} lúc đầu trong mỗi {group} là:",
            "{name} collects a total of {a} {unit},", "collecting all the contents of {b} {groups} that originally held equal numbers of {unit}.", "How many {unit} were originally in each {group}?", "The original number of {unit} per {group} is:");
        // Local-scale counted contexts remain eligible at high stars. Stars are not stock volumes.
        foreach (var (prefix, unit, capacity) in new[] { ("school-pencils", "pencils", 100), ("game-cards", "cards", 100) })
            scenes.AddRange(scenes.Where(s => s.Group == QuestionKnowledgeGroup.Objects && s.Id.StartsWith("object-", StringComparison.Ordinal)).Select(s => s with {
                Id = prefix + "-" + s.Id, UnitId = unit, Capacity = capacity }).ToArray());
        Add("shopping-total", QuestionKnowledgeGroup.Money, ArithmeticOperation.Add, 1, 300000, 99, "currency",
            "{name} mua vở hết {a} {unit},", "{name} mua bút hết {b} {unit}.", "Hỏi {name} trả tất cả bao nhiêu {unit}?", "Số tiền {name} phải trả là:",
            "{name} spends {a} {unit} on notebooks,", "{name} spends {b} {unit} on pens.", "How many {unit} does {name} spend altogether?", "The total amount spent is:");
        Add("saving-total", QuestionKnowledgeGroup.Money, ArithmeticOperation.Add, 2, 500000, 99, "currency",
            "{name} đã tiết kiệm {a} {unit},", "{name} tiết kiệm thêm {b} {unit}.", "Hỏi {name} đã tiết kiệm tất cả bao nhiêu {unit}?", "Tổng số tiền tiết kiệm là:",
            "{name} has saved {a} {unit},", "{name} saves another {b} {unit}.", "How many {unit} has {name} saved altogether?", "The total savings are:");
        Add("saving-original", QuestionKnowledgeGroup.Money, ArithmeticOperation.Add, 4, 500000, 99, "currency",
            "{name} còn lại {a} {unit},", "trước đó {name} đã dùng {b} {unit} mua đồ dùng học tập.", "Hỏi lúc đầu {name} có bao nhiêu {unit}?", "Số tiền lúc đầu của {name} là:",
            "{name} has {a} {unit} left,", "{name} previously spent {b} {unit} on school supplies.", "How many {unit} did {name} originally have?", "The original amount of money is:");
        Add("shopping-change", QuestionKnowledgeGroup.Money, ArithmeticOperation.Subtract, 1, 500000, 99, "currency",
            "{name} có {a} {unit},", "{name} dùng {b} {unit} mua đồ dùng học tập.", "Hỏi {name} còn lại bao nhiêu {unit}?", "Số tiền còn lại của {name} là:",
            "{name} has {a} {unit},", "{name} spends {b} {unit} on school supplies.", "How many {unit} does {name} have left?", "The amount of money remaining is:");
        Add("shopping-price", QuestionKnowledgeGroup.Money, ArithmeticOperation.Multiply, 1, 300000, 30, "currency",
            "Mỗi quyển vở có giá {a} {unit_a},", "{name} mua {b} {unit_b}.", "Hỏi {name} phải trả bao nhiêu {unit}?", "Số tiền mua vở là:",
            "Each notebook costs {a} {unit_a},", "{name} buys {b} {unit_b}.", "How many {unit} does {name} pay in total?", "The total cost of the notebooks is:");
        Add("shopping-unit-price", QuestionKnowledgeGroup.Money, ArithmeticOperation.Divide, 1, 300000, 30, "currency",
            "{name} trả {a} {unit_a} để mua vở cùng loại,", "số vở đã mua là {b} {unit_b}.", "Hỏi mỗi quyển vở có giá bao nhiêu {unit}?", "Giá tiền mỗi quyển vở là:",
            "{name} pays {a} {unit_a} for identical notebooks,", "the number bought is {b} {unit_b}.", "How many {unit} does each notebook cost?", "The price of each notebook is:");
        foreach (var group in new[] { QuestionKnowledgeGroup.Mass, QuestionKnowledgeGroup.Length, QuestionKnowledgeGroup.Transport })
        {
            bool length = group == QuestionKnowledgeGroup.Length, transport = group == QuestionKnowledgeGroup.Transport;
            string id = group.ToString().ToLowerInvariant(), uid = length ? "km" : "kg";
            Add(id + "-total", group, ArithmeticOperation.Add, 1, length ? 300 : 5000, transport ? 12 : 99, uid,
                length ? "{name} đi chặng đầu dài {a} {unit}," : "{name} thu hoạch đợt đầu được {a} {unit} thóc,",
                length ? "chặng tiếp theo dài {b} {unit}." : "đợt tiếp theo thu hoạch được {b} {unit} thóc.",
                length ? "Hỏi hai chặng dài tất cả bao nhiêu {unit}?" : "Hỏi hai đợt thu hoạch được tất cả bao nhiêu {unit} thóc?",
                length ? "Tổng quãng đường là:" : "Khối lượng thóc của hai đợt là:",
                length ? "{name} travels a first leg of {a} {unit}," : "{name} harvests {a} {unit} of rice in the first harvest,",
                length ? "the next leg is {b} {unit}." : "the next harvest yields {b} {unit} of rice.",
                length ? "How many {unit} long are the legs altogether?" : "How many {unit} of rice are harvested altogether?",
                length ? "The total distance is:" : "The total mass of rice is:");
            Add(id + "-left", group, ArithmeticOperation.Subtract, 1, length ? 300 : 5000, transport ? 12 : 99, uid,
                length ? "{name} cần đi quãng đường {a} {unit}," : "Kho của {name} có {a} {unit} gạo,",
                length ? "{name} đã đi được {b} {unit}." : "kho đã xuất đi {b} {unit} gạo.",
                length ? "Hỏi {name} còn phải đi bao nhiêu {unit}?" : "Hỏi kho còn bao nhiêu {unit} gạo?",
                length ? "Quãng đường còn phải đi là:" : "Khối lượng gạo còn lại là:",
                length ? "{name} must travel {a} {unit}," : "{name}'s store holds {a} {unit} of rice,",
                length ? "{name} has already travelled {b} {unit}." : "it dispatches {b} {unit} of rice.",
                length ? "How many {unit} does {name} still need to travel?" : "How many {unit} of rice remain in the store?",
                length ? "The remaining distance is:" : "The remaining mass of rice is:");
            Add(id + "-groups", group, ArithmeticOperation.Multiply, 1, length ? 300 : 5000, transport ? 12 : 30, uid,
                length ? "Mỗi chuyến của {name} dài {a} {unit_a}," : "Mỗi xe của {name} chở {a} {unit_a} gạo,",
                length ? "{name} đi {b} {unit_b} cùng quãng đường." : "{name} có {b} {unit_b} cùng tải như vậy.",
                length ? "Hỏi tổng quãng đường của các chuyến là bao nhiêu {unit}?" : "Hỏi các xe chở tất cả bao nhiêu {unit} gạo?",
                length ? "Tổng quãng đường các chuyến là:" : "Tổng khối lượng gạo được chở là:",
                length ? "Each journey of {name} is {a} {unit_a} long," : "Each truck of {name} carries {a} {unit_a} of rice,",
                length ? "{name} makes {b} {unit_b} of equal length." : "{name} uses {b} {unit_b} with the same load.",
                length ? "How many {unit} does {name} travel in total?" : "How many {unit} of rice do the trucks carry altogether?",
                length ? "The total distance travelled is:" : "The total mass transported is:");
            Add(id + "-share", group, ArithmeticOperation.Divide, 1, length ? 300 : 5000, transport ? 12 : 30, uid,
                length ? "{name} đi tổng cộng {a} {unit_a}," : "{name} cần chở {a} {unit_a} gạo,",
                length ? "quãng đường được chia đều cho {b} {unit_b}." : "gạo được chia đều lên {b} {unit_b}.",
                length ? "Hỏi mỗi chuyến dài bao nhiêu {unit}?" : "Hỏi mỗi xe chở bao nhiêu {unit} gạo?",
                length ? "Quãng đường mỗi chuyến là:" : "Khối lượng gạo mỗi xe chở là:",
                length ? "{name} travels a total of {a} {unit_a}," : "{name} needs to transport {a} {unit_a} of rice,",
                length ? "the distance is shared equally among {b} {unit_b}." : "the rice is shared equally among {b} {unit_b}.",
                length ? "How many {unit} long is each journey?" : "How many {unit} of rice does each truck carry?",
                length ? "The distance per journey is:" : "The mass transported per truck is:");
        }
        foreach (var (group, unit, viItem, enItem) in new[] {
            (QuestionKnowledgeGroup.Mass, "g", "gạo", "rice"),
            (QuestionKnowledgeGroup.Length, "m", "quãng đường", "distance") })
        {
            bool mass = group == QuestionKnowledgeGroup.Mass;
            Add(group.ToString().ToLowerInvariant() + "-convert-total", group, ArithmeticOperation.Add, 4, 100000, 99, unit,
                mass ? "{name} có {a} {unit_a} gạo," : "Chặng đầu của {name} dài {a} {unit_a},",
                mass ? "{name} có thêm {b} {unit_b} gạo." : "chặng sau dài {b} {unit_b}.",
                mass ? "Hỏi {name} có tất cả bao nhiêu {unit} gạo?" : "Hỏi cả hai chặng dài bao nhiêu {unit}?",
                mass ? "Tổng khối lượng gạo là:" : "Tổng độ dài hai chặng là:",
                mass ? "{name} has {a} {unit_a} of rice," : "The first leg of {name}'s journey is {a} {unit_a} long,",
                mass ? "{name} gets another {b} {unit_b} of rice." : "the next leg is {b} {unit_b} long.",
                mass ? "How many {unit} of rice does {name} have in total?" : "How many {unit} long are both legs together?",
                mass ? "The total mass of rice is:" : "The total distance is:", 1000);
        }
        // Mixed input units require one intermediate conversion, then the selected operation.
        foreach (var group in new[] { QuestionKnowledgeGroup.Mass, QuestionKnowledgeGroup.Length })
        {
            bool mass = group == QuestionKnowledgeGroup.Mass;
            string prefix = group.ToString().ToLowerInvariant(), uid = mass ? "g" : "m";
            Add(prefix + "-convert-left", group, ArithmeticOperation.Subtract, 4, 100000, 99, uid,
                mass ? "{name} có {a} {unit_a} gạo," : "{name} cần đi {a} {unit_a},",
                mass ? "{name} đã dùng {b} {unit_b} gạo." : "{name} đã đi {b} {unit_b}.",
                mass ? "Hỏi còn lại bao nhiêu {unit} gạo?" : "Hỏi còn phải đi bao nhiêu {unit}?",
                mass ? "Khối lượng gạo còn lại là:" : "Quãng đường còn lại là:",
                mass ? "{name} has {a} {unit_a} of rice," : "{name} needs to travel {a} {unit_a},",
                mass ? "{name} uses {b} {unit_b} of rice." : "{name} has travelled {b} {unit_b}.",
                mass ? "How many {unit} of rice remain?" : "How many {unit} remain to be travelled?",
                mass ? "The mass of rice remaining is:" : "The distance remaining is:", 1000);
            foreach (var op in new[] { ArithmeticOperation.Multiply, ArithmeticOperation.Divide })
            {
                bool multiply = op == ArithmeticOperation.Multiply;
                Add(prefix + "-convert-" + (multiply ? "groups" : "share"), group, op, 4, 100000, 30, uid,
                    multiply ? mass ? "Mỗi túi gạo nặng {a} {unit_a}," : "Mỗi chặng dài {a} {unit_a},"
                        : mass ? "{name} có {a} {unit_a} gạo," : "{name} cần đi {a} {unit_a},",
                    multiply ? mass ? "{name} có {b} {unit_b} như vậy." : "{name} đi {b} {unit_b} như vậy."
                        : mass ? "gạo được chia đều vào {b} {unit_b}." : "quãng đường được chia đều thành {b} {unit_b}.",
                    multiply ? mass ? "Hỏi tất cả nặng bao nhiêu {unit}?" : "Hỏi tổng quãng đường là bao nhiêu {unit}?"
                        : mass ? "Hỏi mỗi túi nặng bao nhiêu {unit}?" : "Hỏi mỗi chặng dài bao nhiêu {unit}?",
                    multiply ? mass ? "Tổng khối lượng gạo là:" : "Tổng quãng đường là:"
                        : mass ? "Khối lượng gạo mỗi túi là:" : "Quãng đường mỗi chặng là:",
                    multiply ? mass ? "Each bag of rice weighs {a} {unit_a}," : "Each leg is {a} {unit_a} long,"
                        : mass ? "{name} has {a} {unit_a} of rice," : "{name} needs to travel {a} {unit_a},",
                    multiply ? mass ? "{name} has {b} {unit_b}." : "{name} travels {b} {unit_b} of this length."
                        : mass ? "the rice is shared equally among {b} {unit_b}." : "the distance is divided into {b} equal {unit_b}.",
                    multiply ? mass ? "How many {unit} do the bags weigh altogether?" : "How many {unit} long is the total distance?"
                        : mass ? "How many {unit} does each bag weigh?" : "How many {unit} long is each leg?",
                    multiply ? mass ? "The total mass of rice is:" : "The total distance is:"
                        : mass ? "The mass per bag is:" : "The distance per leg is:", 1000);
            }
        }
        // A transport group counts loads, whereas harvesting belongs to the mass group.
        for (int i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].Id is "mass-groups" or "mass-share") scenes[i] = scenes[i] with {
                ViA = scenes[i].ViA.Replace("xe", "túi"), ViQ = scenes[i].ViQ.Replace("xe", "túi"),
                ViLead = scenes[i].ViLead.Replace("xe", "túi"),
                EnA = scenes[i].EnA.Replace("truck", "bag"), EnQ = scenes[i].EnQ.Replace("truck", "bag"),
                EnLead = scenes[i].EnLead.Replace("truck", "bag") };
            if (scenes[i].Id == "transport-total") scenes[i] = scenes[i] with {
                ViA = "Xe thứ nhất chở {a} {unit} gạo,", ViB = "xe thứ hai chở {b} {unit} gạo.",
                ViQ = "Hỏi hai xe chở tất cả bao nhiêu {unit} gạo?", ViLead = "Tổng khối lượng gạo hai xe chở là:",
                EnA = "The first truck carries {a} {unit} of rice,", EnB = "the second truck carries {b} {unit} of rice.",
                EnQ = "How many {unit} of rice do the two trucks carry altogether?", EnLead = "The total load of rice is:" };
        }
        Add("motion-distance", QuestionKnowledgeGroup.Motion, ArithmeticOperation.Multiply, 1, 1000, 8, "km",
            "Xe của {name} đi đều với vận tốc {a} {unit_a},", "xe đi trong {b} {unit_b}.", "Hỏi xe đi được bao nhiêu {unit}?", "Quãng đường xe đi được là:",
            "{name}'s vehicle travels at a constant speed of {a} {unit_a},", "it travels for {b} {unit_b}.", "How many {unit} does the vehicle travel?", "The distance travelled is:");
        Add("motion-speed", QuestionKnowledgeGroup.Motion, ArithmeticOperation.Divide, 1, 600, 8, "km/h",
            "Xe của {name} đi đều được {a} {unit_a},", "thời gian đi là {b} {unit_b}.", "Hỏi vận tốc của xe là bao nhiêu {unit}?", "Vận tốc của xe là:",
            "{name}'s vehicle travels {a} {unit_a} at a constant speed,", "the travel time is {b} {unit_b}.", "What is the vehicle's speed in {unit}?", "The vehicle's speed is:");
        scenes.AddRange(scenes.Where(s => s.Id is "motion-distance" or "motion-speed").Select(s => s with {
            Id = s.Id + "-minutes", MinimumStars = 4, RightDisplayFactor = 60 }).ToArray());
        // Add/subtract in this group are distances, not sums/differences of unrelated speeds.
        scenes.AddRange(scenes.Where(s => s.Group == QuestionKnowledgeGroup.Length && s.Operation is ArithmeticOperation.Add or ArithmeticOperation.Subtract
            && s.Conversion == 1).Select(s => s with { Id = "motion-" + s.Id, Group = QuestionKnowledgeGroup.Motion }).ToArray());
        AddExpandedScenes(scenes);
        return scenes;
    }

    public static IEnumerable<AppliedQuestionScene> Available(QuestionLearningProfile p, ArithmeticOperation operation, CurriculumTier tier)
        => All.Where(s => p.Allows(operation) && Enum.IsDefined(tier) && p.Includes(s.Group) && s.Operation == operation
            && (int)tier >= s.MinimumStars);
    public static AppliedQuestionScene? Find(string id) => All.FirstOrDefault(s => s.Id == id);
    public static WordProblemQuantity Quantity(BasicQuestionContract c) => Find(c.SceneId)!.Group switch
    {
        QuestionKnowledgeGroup.Objects => WordProblemQuantity.Count,
        QuestionKnowledgeGroup.Money => WordProblemQuantity.Money,
        QuestionKnowledgeGroup.Mass or QuestionKnowledgeGroup.Transport => WordProblemQuantity.Mass,
        QuestionKnowledgeGroup.Motion when c.SceneId.StartsWith("motion-speed", StringComparison.Ordinal) => WordProblemQuantity.Speed,
        QuestionKnowledgeGroup.Time => WordProblemQuantity.Time,
        QuestionKnowledgeGroup.Measurement => WordProblemQuantity.Capacity,
        QuestionKnowledgeGroup.Geometry when Find(c.SceneId)!.UnitId.Contains('³') => WordProblemQuantity.Volume,
        QuestionKnowledgeGroup.Geometry when Find(c.SceneId)!.UnitId.Contains('²') => WordProblemQuantity.Area,
        QuestionKnowledgeGroup.Geometry => WordProblemQuantity.Distance,
        QuestionKnowledgeGroup.Packaging or QuestionKnowledgeGroup.Production or QuestionKnowledgeGroup.Data => WordProblemQuantity.Count,
        _ => WordProblemQuantity.Distance
    };
    public static string? ConversionStep(BasicQuestionContract c)
    {
        var scene = Find(c.SceneId)!;
        if (scene.RightDisplayFactor != 1)
            return (c.Right * scene.RightDisplayFactor).ToString(CultureInfo.InvariantCulture)
                + (c.Language == AppLanguage.Vietnamese ? " phút = " : " minutes = ") + c.Right.ToString(CultureInfo.InvariantCulture)
                + (c.Language == AppLanguage.Vietnamese ? " giờ" : c.Right == 1 ? " hour" : " hours");
        if (scene.Conversion == 1) return null;
        var units = InputUnits(c);
        return Render("{a} {unit_a}", c) + " = " + c.Left.ToString(CultureInfo.InvariantCulture) + " " + scene.UnitId;
    }
    public static BasicQuestionContract Create(QuestionLearningProfile profile, ArithmeticOperation operation, CurriculumTier tier,
        AppLanguage language, Random? random = null, string? sceneId = null)
    {
        random ??= Random.Shared;
        var choices = Available(profile, operation, tier).Where(s => sceneId is null || s.Id == sceneId).ToArray();
        if (choices.Length == 0) throw new ArgumentException("InvalidLearningProfile");
        var scene = choices[random.Next(choices.Length)];
        int cap = Ceiling(profile, scene, tier, language);
        int factorMax = Math.Min(scene.MaxFactor, profile.MaximumFactor(tier));
        (int a, int b) = Numbers(scene, tier, cap, factorMax, random);
        string subject = QuestionNames.Create(language, random), other = QuestionNames.Create(language, random);
        if (other == subject) other = language == AppLanguage.Vietnamese ? "Bình" : "Emma";
        if (other == subject) other = language == AppLanguage.Vietnamese ? "An" : "James";
        if (profile.Group == QuestionKnowledgeGroup.Objects && Math.Max(a, b) > 100)
        { subject = language == AppLanguage.Vietnamese ? "kho sách phía Đông" : "the eastern book warehouse";
          other = language == AppLanguage.Vietnamese ? "kho sách phía Tây" : "the western book warehouse"; }
        if (scene.Group == QuestionKnowledgeGroup.Mass && scene.Conversion == 1 && Math.Max(a, b) > 50)
        { subject = language == AppLanguage.Vietnamese ? "nông trại phía Đông" : "the eastern farm";
          other = language == AppLanguage.Vietnamese ? "nông trại phía Tây" : "the western farm"; }
        if (scene.Group == QuestionKnowledgeGroup.Transport)
        { subject = language == AppLanguage.Vietnamese ? "đội vận tải phía Đông" : "the eastern transport team";
          other = language == AppLanguage.Vietnamese ? "đội vận tải phía Tây" : "the western transport team"; }
        if (profile.Group == QuestionKnowledgeGroup.Motion)
        { subject = language == AppLanguage.Vietnamese ? "bác tài An" : "the driver Alex";
          other = language == AppLanguage.Vietnamese ? "bác tài Bình" : "the driver Emma"; }
        SetSceneActors(scene, language, a, b, ref subject, ref other);
        string unit = AnswerUnit(scene, language);
        int perGroup = scene.Structure == BasicQuestionStructure.CountGroups ? b : operation == ArithmeticOperation.Divide ? a / b : a;
        string groupUnit = profile.Group == QuestionKnowledgeGroup.Objects && perGroup > 100
            ? language == AppLanguage.Vietnamese ? "kho" : "warehouse"
            : language == AppLanguage.Vietnamese ? "thùng" : "container";
        groupUnit = SceneGroupUnit(scene, language) ?? groupUnit;
        return new(Version, operation, tier, language, a, b, subject, unit,
            groupUnit,
            scene.Structure,
            other, profile.Group.ToString(), scene.Id, KnowledgeGroup: profile.Group);
    }
    private static int Capacity(AppliedQuestionScene s, AppLanguage language) => s.Group == QuestionKnowledgeGroup.Money
        && language == AppLanguage.English ? 300 : s.Capacity;
    private static int Ceiling(QuestionLearningProfile p, AppliedQuestionScene s, CurriculumTier tier, AppLanguage language)
        => s.Group == QuestionKnowledgeGroup.Motion ? Capacity(s, language)
            : s.Group == QuestionKnowledgeGroup.Money
            ? Math.Min(language == AppLanguage.Vietnamese
                ? new[] { 20000, 50000, 100000, 300000, 500000 }[(int)tier - 1]
                : new[] { 10, 30, 100, 200, 300 }[(int)tier - 1], Capacity(s, language))
            : Math.Min(p.ArithmeticCeiling(tier), Capacity(s, language));
    public static string AnswerUnit(AppliedQuestionScene s, AppLanguage language) => s.UnitId == "currency"
        ? language == AppLanguage.Vietnamese ? "đồng" : "dollars"
        : ExpandedUnit(s.UnitId, language, false) ?? QuestionUnits.Find(s.UnitId)?.Item(language) ?? s.UnitId;
    public static string ResultUnit(BasicQuestionContract c)
    {
        if (c.Structure == BasicQuestionStructure.CountGroups)
            return c.Language == AppLanguage.Vietnamese || c.Answer.IsOne ? c.GroupUnit : c.GroupUnit + "s";
        if (c.Language == AppLanguage.English && c.Answer.IsOne)
            return ExpandedUnit(Find(c.SceneId)!.UnitId, c.Language, true) ?? QuestionUnits.Find(Find(c.SceneId)!.UnitId)?.Item(c.Language, true) ?? (c.Unit == "dollars" ? "dollar" : c.Unit);
        return c.Unit;
    }
    public static (string A, string B) InputUnits(BasicQuestionContract c)
    {
        var s = Find(c.SceneId)!;
        bool vi = c.Language == AppLanguage.Vietnamese;
        if (ExpandedInputUnits(s, c.Language) is { } expanded) return expanded;
        if (s.Id.EndsWith("recover-groups", StringComparison.Ordinal)) return (c.GroupUnit, c.Unit);
        if (s.Structure == BasicQuestionStructure.CountGroups) return (c.Unit, c.Unit);
        if (s.Structure is BasicQuestionStructure.TimesAsMany or BasicQuestionStructure.TimesFewer)
            return (c.Unit, vi ? "lần" : "times");
        if (s.Conversion != 1) return (s.UnitId == "g" ? "kg" : s.UnitId == "ml" ? "l" : "km",
            c.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide
                ? s.Group == QuestionKnowledgeGroup.Mass ? vi ? "túi" : "bags" : vi ? "chặng" : "legs"
                : s.UnitId);
        string timeUnit = s.RightDisplayFactor == 60 ? vi ? "phút" : "minutes" : vi ? "giờ" : "hours";
        if (s.Id.StartsWith("motion-distance", StringComparison.Ordinal)) return ("km/h", timeUnit);
        if (s.Id.StartsWith("motion-speed", StringComparison.Ordinal)) return ("km", timeUnit);
        if (c.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
            return (c.Unit, s.Group == QuestionKnowledgeGroup.Money ? vi ? "quyển vở" : "notebooks"
                : s.Group == QuestionKnowledgeGroup.Objects ? c.GroupUnit
                : s.Group == QuestionKnowledgeGroup.Length ? vi ? "chuyến" : "journeys"
                : s.Group == QuestionKnowledgeGroup.Mass ? vi ? "túi" : "bags" : vi ? "xe" : "trucks");
        return (c.Unit, c.Unit);
    }
    private static (int, int) Numbers(AppliedQuestionScene s, CurriculumTier tier, int cap, int factorMax, Random random)
    {
        if (ExpandedNumbers(s, tier, cap, factorMax, random) is { } numbers) return numbers;
        if (s.Conversion != 1)
        {
            // Five stars can have fractional kg/km, with an exact integer g/m answer.
            int step = tier == CurriculumTier.FiveStars ? 100 : 1000;
            if (s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
            {
                int factor = random.Next(2, Math.Min(factorMax, cap / step) + 1);
                int per = random.Next(1, Math.Max(2, cap / (factor * step) + 1)) * step;
                return s.Operation == ArithmeticOperation.Multiply ? (per, factor) : (per * factor, factor);
            }
            int a = random.Next(1, Math.Max(2, cap / (2 * step))) * step;
            return (a, random.Next(1, Math.Max(2, s.Operation == ArithmeticOperation.Subtract ? a : cap - a)));
        }
        if (s.Id.StartsWith("motion-distance", StringComparison.Ordinal)) return (random.Next(20, 91), random.Next(1, Math.Min(8, factorMax) + 1));
        if (s.Id.StartsWith("motion-speed", StringComparison.Ordinal)) { int hours = random.Next(1, Math.Min(6, factorMax) + 1); return (random.Next(20, 91) * hours, hours); }
        int moneyStep = s.Group == QuestionKnowledgeGroup.Money && cap >= 1000 ? 1000 : 1;
        int bound = Math.Max(3, cap / moneyStep);
        if (s.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
        {
            int factor = random.Next(2, Math.Min(factorMax, bound / 2) + 1);
            int perCeiling = bound / factor;
            if (s.Group == QuestionKnowledgeGroup.Money) perCeiling = Math.Min(perCeiling, moneyStep == 1000 ? 30 : 15);
            if (s.Group == QuestionKnowledgeGroup.Transport) perCeiling = Math.Min(perCeiling, 2000);
            if (s.Group == QuestionKnowledgeGroup.Mass) perCeiling = Math.Min(perCeiling, 50);
            int minimum = s.Group == QuestionKnowledgeGroup.Money && moneyStep == 1000 ? 3 : 1;
            int per = random.Next(minimum, Math.Max(minimum + 1, perCeiling + 1)) * moneyStep;
            return s.Operation == ArithmeticOperation.Multiply ? (per, factor) : (per * factor, factor);
        }
        int desired = (int)tier - 1;
        int maxCarries = desired;
        (int A, int B) best = (moneyStep, moneyStep);
        int bestCount = -1;
        for (int attempt = 0; attempt < 128; attempt++)
        {
            int total = random.Next(2, bound + 1), part = random.Next(1, total);
            int a = (s.Operation == ArithmeticOperation.Add ? total - part : total) * moneyStep, b = part * moneyStep;
            int count = s.Operation == ArithmeticOperation.Add ? AdditionQuestionCatalogue.CountCarries(a, b) : ArithmeticQuestionCatalogue.CountBorrows(a, b);
            if (count > maxCarries) continue;
            if (count > bestCount) { best = (a, b); bestCount = count; }
            if (count >= desired) return (a, b);
        }
        // Guaranteed no-carry/no-borrow fallback, within every supported capacity.
        return bestCount >= 0 ? best : s.Operation == ArithmeticOperation.Add ? (moneyStep, moneyStep) : (2 * moneyStep, moneyStep);
    }
    public static bool IsValid(BasicQuestionContract c)
    {
        var p = new QuestionLearningProfile(c.KnowledgeGroup);
        var s = Find(c.SceneId);
        if (s is null || !Available(p, c.Operation, c.Tier).Contains(s) || c.TopicId != p.Group.ToString()
            || c.Unit != AnswerUnit(s, c.Language) || !ValidGroup(c)
            || c.PartA != "" || c.PartB != "" || c.Structure != s.Structure) return false;
        // Grade is historical metadata only. Old saved templates retain their valid
        // preview values; FreshFacts regenerates them under the star/group policy.
        if (c.Grade is < 0 or > 5) return false;
        long max = c.Grade == 0 ? Ceiling(p, s, c.Tier, c.Language) : Capacity(s, c.Language);
        if (s.Group == QuestionKnowledgeGroup.Motion) max = s.Capacity;
        if (c.Left <= 0 || c.Right <= 0 || c.Left > max || c.Right > max || c.Answer <= 0 || c.Answer > max
            || !ExpandedFactsValid(c, s)) return false;
        if (s.Conversion != 1 && (c.Left % (c.Tier == CurriculumTier.FiveStars ? 100 : 1000) != 0)) return false;
        if (c.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide)
        {
            if (c.Right > (c.Grade == 0 ? Math.Min(s.MaxFactor, p.MaximumFactor(c.Tier)) : s.MaxFactor)) return false;
            int per = c.Operation == ArithmeticOperation.Divide ? (int)c.Answer : c.Left;
            if (s.Group == QuestionKnowledgeGroup.Transport && per > 2000
                || s.Group == QuestionKnowledgeGroup.Mass && s.Conversion == 1 && per > 50) return false;
        }
        if (s.Id.StartsWith("motion-distance", StringComparison.Ordinal) && c.Left is < 20 or > 90
            || s.Id.StartsWith("motion-speed", StringComparison.Ordinal) && (c.Answer < 20 || c.Answer > 90)) return false;
        if (c.Grade == 0 && s.Conversion == 1 && !HasDerivedAnswer(s) && c.Operation is ArithmeticOperation.Add or ArithmeticOperation.Subtract)
        {
            int count = c.Operation == ArithmeticOperation.Add ? AdditionQuestionCatalogue.CountCarries(c.Left, c.Right)
                : ArithmeticQuestionCatalogue.CountBorrows(c.Left, c.Right);
            if (count > (int)c.Tier - 1) return false;
        }
        if (c.Operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide && s.Group == QuestionKnowledgeGroup.Money)
        {
            int price = c.Operation == ArithmeticOperation.Divide ? (int)c.Answer : c.Left;
            if (price > (c.Language == AppLanguage.Vietnamese ? 30000 : 15)
                || c.Language == AppLanguage.Vietnamese && (price < 3000 || price % 1000 != 0)) return false;
        }
        return true;
    }
    private static bool ValidGroup(BasicQuestionContract c)
    {
        if (SceneGroupUnit(Find(c.SceneId)!, c.Language) is { } group) return c.GroupUnit == group;
        int per = c.Structure == BasicQuestionStructure.CountGroups ? c.Right : c.Operation == ArithmeticOperation.Divide ? c.Left / c.Right : c.Left;
        string expected = c.KnowledgeGroup == QuestionKnowledgeGroup.Objects && per > 100
            ? c.Language == AppLanguage.Vietnamese ? "kho" : "warehouse"
            : c.Language == AppLanguage.Vietnamese ? "thùng" : "container";
        return c.GroupUnit == expected;
    }
    public static BasicQuestionDraft Draft(BasicQuestionContract c, int variant = 0)
    {
        var s = Find(c.SceneId)!;
        bool vi = c.Language == AppLanguage.Vietnamese;
        var d = vi ? new BasicQuestionDraft(s.ViA, s.ViB, s.ViQ, s.ViLead, s.UnitId)
            : new BasicQuestionDraft(s.EnA, s.EnB, s.EnQ, s.EnLead, s.UnitId);
        if (variant == 0) return d;
        // Interchangeable surface phrases, never interchangeable mathematical relations.
        string V(string text) => vi ? text.Replace("tất cả", "tổng cộng").Replace("{name} có ", "{name} giữ ").Replace("{other} có ", "{other} giữ ")
            .Replace("còn lại bao nhiêu", "còn bao nhiêu").Replace("Hỏi ", "")
            : text.Replace("altogether", "in total").Replace("{name} has {a}", "{name} holds {a}")
                .Replace("{other} has {a}", "{other} holds {a}").Replace("{other} has {b}", "{other} holds {b}");
        return d with { GivenA = V(d.GivenA), GivenB = V(d.GivenB), Question = V(d.Question), SolutionLead = V(d.SolutionLead!) };
    }
    public static BasicDraftValidation Validate(string raw, BasicQuestionContract c)
    {
        if (!c.IsValid) return new(null, "InvalidContract");
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 12000) return new(null, "InvalidJson");
        try
        {
            using var json = JsonDocument.Parse(raw.Trim(), new() { MaxDepth = 8 });
            string[] keys = ["given_a", "given_b", "question", "solution_lead", "unit_id"];
            if (json.RootElement.ValueKind != JsonValueKind.Object) return new(null, "InvalidJson");
            var props = json.RootElement.EnumerateObject().ToArray();
            if (props.Length != 5 || props.Any(p => !keys.Contains(p.Name) || p.Value.ValueKind != JsonValueKind.String)
                || props.Select(p => p.Name).Distinct().Count() != 5) return new(null, "InvalidFields");
            string F(string key) => json.RootElement.GetProperty(key).GetString()!;
            var d = new BasicQuestionDraft(F(keys[0]), F(keys[1]), F(keys[2]), F(keys[3]), F(keys[4]));
            var languageError = QuestionProseLanguage.ValidateAndNormalize(d, c.Language, out d);
            if (languageError is not null) return new(null, languageError);
            if (d.UnitId != Find(c.SceneId)!.UnitId) return new(null, "ChangedUnits");
            // Compare complete clauses against reviewed alternatives, not a bag of keywords.
            // Numeric/unit/actor slots and their order bind the roles for mixed dimensions.
            var prose = ReviewedQuestionProse.For(c)!;
            bool Same(string a, string b) => string.Equals(Regex.Replace(a.Trim(), @"\s+", " "),
                Regex.Replace(b.Trim(), @"\s+", " "), StringComparison.OrdinalIgnoreCase);
            if (!prose.GivenA.Any(e => Same(d.GivenA, e)) || !prose.GivenB.Any(e => Same(d.GivenB, e))
                || !prose.Questions.Any(e => Same(d.Question, e)) || !prose.Leads.Any(e => Same(d.SolutionLead!, e)))
                return new(null, "ChangedRelationOrTarget");
            return new(d, null, c);
        }
        catch (JsonException) { return new(null, "InvalidJson"); }
    }
    public static string Render(string template, BasicQuestionContract c)
    {
        var s = Find(c.SceneId)!;
        var units = InputUnits(c);
        string left = ((decimal)c.Left / s.Conversion).ToString("0.###", c.Language == AppLanguage.Vietnamese
            ? CultureInfo.GetCultureInfo("vi-VN") : CultureInfo.InvariantCulture);
        string Singular(string unit, decimal number) => c.Language != AppLanguage.English || number != 1 ? unit
            : ExpandedSingular(unit) ?? unit switch { "books" => "book", "dollars" => "dollar", "notebooks" => "notebook", "hours" => "hour", "minutes" => "minute",
                "trucks" => "truck", "journeys" => "journey", "bags" => "bag", "legs" => "leg",
                "pencils" => "pencil", "cards" => "card", _ => unit };
        // Quantity-specific units; question/solution units remain plural.
        template = template.Replace("{a} {unit}", "{a} " + Singular(c.Unit, (decimal)c.Left / s.Conversion))
            .Replace("{b} {unit}", "{b} " + Singular(c.Unit, c.Right));
        template = template.Replace("{a} {unit_a}", "{a} " + Singular(units.A, (decimal)c.Left / s.Conversion))
            .Replace("{b} {unit_a}", "{b} " + Singular(units.A, c.Right * s.RightDisplayFactor))
            .Replace("{a} {unit_b}", "{a} " + Singular(units.B, (decimal)c.Left / s.Conversion))
            .Replace("{b} {unit_b}", "{b} " + Singular(units.B, c.Right * s.RightDisplayFactor));
        string text = template.Replace("{unit_a}", units.A).Replace("{unit_b}", units.B)
            .Replace("{unit}", c.Unit).Replace("{group}", c.GroupUnit)
            .Replace("{groups}", c.Language == AppLanguage.Vietnamese ? c.GroupUnit : c.GroupUnit + "s")
            .Replace("{name}", c.Subject).Replace("{other}", c.OtherSubject)
            .Replace("{a}", left).Replace("{b}", (c.Right * s.RightDisplayFactor).ToString(CultureInfo.InvariantCulture));
        if (c.Language == AppLanguage.English)
            text = Regex.Replace(text, @"\b1 (more |fewer |additional )?" + Regex.Escape(c.Unit) + @"\b",
                m => "1 " + m.Groups[1].Value + Singular(c.Unit, 1));
        return Regex.Replace(text, @"(^|[.!?]\s+)(\p{Ll})", m => m.Groups[1].Value + m.Groups[2].Value.ToUpperInvariant());
    }
    public static string Prompt(BasicQuestionContract c, string? correction, BasicQuestionDraft? example = null)
        => (c.Language == AppLanguage.Vietnamese
            ? "Viết mẫu đề và câu dẫn lời giải bằng tiếng Việt cho bối cảnh đã chọn. Giữ biến, đơn vị và vai trò; không giải bài, không sinh số. given_a kết thúc dấu phẩy, given_b là vế tiếp nối."
            : "Write an English question template and solution lead for this selected scene. Preserve placeholders, dimensions and roles; no numbers or answers. Join the two givens as clauses.")
            + "\nScene: " + c.SceneId + "; group=" + c.KnowledgeGroup + "; stars=" + (int)c.Tier
            + "; input units=" + JsonSerializer.Serialize(new { GivenA = InputUnits(c).A, GivenB = InputUnits(c).B },
                new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "; answer unit=" + c.AnswerUnit
            + "\nPreserve the facts and target of this example. Equivalent wording is allowed; do not copy wording rejected as duplicate:\n"
            + QuestionBankStore.SerializeDraft(example ?? Draft(c))
            + "\nReturn only JSON with given_a, given_b, question, solution_lead, unit_id."
            + (correction is null ? "" : "\nCorrect the rejected output: " + correction);
    public static string Grammar(BasicQuestionContract c)
        => ReviewedQuestionProse.For(c)!.Grammar(c, new HashSet<string>(StringComparer.Ordinal));
}
