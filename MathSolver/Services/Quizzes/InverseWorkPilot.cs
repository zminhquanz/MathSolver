using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

internal static class InverseWorkPilot
{
    internal const string Prefix = "inverse-work-open/";
    internal const string Relation = "fixed-work-equal-unchanged-productivity-find-new-completion-time";
    internal sealed record Context(string Id, string Workers, string Worker, string TimeUnit, string Task,
        string Given, string Condition, string Question, string SingleLead, string TeamLead);
    // Context and unit choices are finite typed data; the writing model never chooses them.
    private static readonly IReadOnlyList<Context> Vietnamese =
    [
        new("painting", "công nhân", "công nhân", "ngày", "việc sơn một khu trường học",
            "{f0} {v0} hoàn thành {v2} trong {f1} {v1}.",
            "Khối lượng công việc giữ nguyên; năng suất của mỗi {v3} bằng nhau và không thay đổi.",
            "{f2} {v0} cần bao nhiêu {v1} để hoàn thành cùng công việc đó?",
            "Thời gian một {v3} hoàn thành công việc đó", "Thời gian {f2} {v0} hoàn thành công việc đó"),
        new("packing", "công nhân", "công nhân", "ngày", "việc đóng gói một lô hàng",
            "{f0} {v0} hoàn thành {v2} trong {f1} {v1}.",
            "Khối lượng công việc giữ nguyên; năng suất của mỗi {v3} bằng nhau và không thay đổi.",
            "{f2} {v0} cần bao nhiêu {v1} để hoàn thành cùng công việc đó?",
            "Thời gian một {v3} hoàn thành công việc đó", "Thời gian {f2} {v0} hoàn thành công việc đó")
    ];
    private static readonly IReadOnlyList<Context> English =
    [
        new("painting", "workers", "worker", "days", "painting a school complex",
            "{f0} {v0} complete {v2} in {f1} {v1}.",
            "The amount of work stays fixed; the productivity of each {v3} is equal and unchanged.",
            "How many {v1} do {f2} {v0} need to complete that same work?",
            "The time for one {v3} to complete that work", "The time for {f2} {v0} to complete that work"),
        new("packing", "workers", "worker", "days", "packing a shipment",
            "{f0} {v0} complete {v2} in {f1} {v1}.",
            "The amount of work stays fixed; the productivity of each {v3} is equal and unchanged.",
            "How many {v1} do {f2} {v0} need to complete that same work?",
            "The time for one {v3} to complete that work", "The time for {f2} {v0} to complete that work")
    ];
    internal sealed record Data(Context Context, string Profile, int OriginalWorkers, int OriginalTime, int NewWorkers)
    {
        internal int SingleWorkerTime => checked(OriginalWorkers * OriginalTime);
        internal int Answer => SingleWorkerTime / NewWorkers;
        internal string TimeUnitFor(int value) => Context.TimeUnit == "days" && value == 1 ? "day" : Context.TimeUnit;
        internal Dictionary<string, string> Values => new()
        {
            ["f0"] = N(OriginalWorkers), ["f1"] = N(OriginalTime), ["f2"] = N(NewWorkers),
            ["v0"] = Context.Workers, ["v1"] = Context.TimeUnit, ["v2"] = Context.Task, ["v3"] = Context.Worker
        };
        internal string Render(string text)
        {
            text = text.Replace("{f1} {v1}", N(OriginalTime) + " " + TimeUnitFor(OriginalTime), StringComparison.Ordinal);
            foreach (var (key, value) in Values) text = text.Replace("{" + key + "}", value, StringComparison.Ordinal);
            return text;
        }
        internal string Problem => Render(string.Join(" ", Context.Given, Context.Condition, Context.Question));
    }
    internal static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    internal static bool Supports(int variant) => variant == (int)ProportionQuizType.Inverse;
    internal static IReadOnlyList<Context> Contexts(AppLanguage language) => language switch
    {
        AppLanguage.Vietnamese => Vietnamese,
        AppLanguage.English => English,
        _ => throw new ArgumentOutOfRangeException(nameof(language))
    };
    internal static IEnumerable<string> Profiles(AppLanguage language, int variant)
        => Supports(variant) ? Contexts(language).Select(c => Prefix + c.Id) : [];
    internal static Data Create(string profile, AppLanguage language, CurriculumTier tier, Random random)
    {
        if (!Enum.IsDefined(tier)) throw new ArgumentException("InvalidInverseWorkTier");
        var context = Contexts(language).SingleOrDefault(c => Prefix + c.Id == profile)
            ?? throw new ArgumentException("InvalidInverseWorkProfile");
        int maximum = 4 + 2 * (int)tier;
        int original = random.Next(2, maximum + 1), replacement = random.Next(2, maximum + 1);
        if (replacement == original) replacement = replacement == maximum ? 2 : replacement + 1;
        int gcd = (int)System.Numerics.BigInteger.GreatestCommonDivisor(original, replacement);
        int factor = random.Next(1, 2 + (int)tier);
        int originalTime = checked(replacement / gcd * factor);
        return new(context, profile, original, originalTime, replacement);
    }
}

public sealed partial class ProportionQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateOpenInverseWork(ArithmeticQuizMode mode, AppLanguage language,
        CurriculumTier tier, string profile)
    {
        var d = InverseWorkPilot.Create(profile, language, tier, _random);
        string solution = d.Render(d.Context.SingleLead) + ":\n"
            + $"{d.OriginalWorkers} × {d.OriginalTime} = {d.SingleWorkerTime} {d.Context.TimeUnit}\n"
            + d.Render(d.Context.TeamLead) + ":\n"
            + $"{d.SingleWorkerTime} ÷ {d.NewWorkers} = {d.Answer} {d.TimeUnitFor(d.Answer)}\n"
            + (language == AppLanguage.Vietnamese ? "Đáp số: " : "Answer: ") + $"{d.Answer} {d.TimeUnitFor(d.Answer)}";
        var contract = new ProportionQuizContract(ProportionQuizType.Inverse, ProportionScenarioKind.WorkersDays,
            d.OriginalWorkers, d.OriginalTime, d.NewWorkers, d.Answer, d.Context.TimeUnit, d.Context.Workers, d.Problem)
        { NarrativeId = profile, SolutionText = solution };
        return CreateQuestion(mode, contract);
    }
}
