using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>Finite capacity stories with distinct ceiling and floor targets.</summary>
internal static class RemainderGroupsPilot
{
    internal const string Prefix = "remainder-groups-open/";
    internal sealed record Context(string Id, string Items, string Containers, string Container,
        int MaximumCapacity, string Total, string TotalAlternate, string Capacity, string CapacityAlternate,
        string Question, string QuestionAlternate, string FullLead, string MinimumLead);
    internal sealed record Data(Context Context, string Profile, ElementaryQuizType Type, int Total, int Capacity)
    {
        internal int Full => Total / Capacity;
        internal int Remainder => Total % Capacity;
        internal int Extra => Remainder == 0 ? 0 : 1;
        internal int Answer => Full + (Type == ElementaryQuizType.MinimumGroups ? Extra : 0);
        internal string AnswerUnit => Answer == 1 ? Context.Container : Context.Containers;
        internal string FullExpression => $"({Total}-{Remainder})/{Capacity}";
        internal string DivisionCalculation(AppLanguage language) => $"{Total} ÷ {Capacity} = {Full} "
            + (language == AppLanguage.Vietnamese ? "dư " : "remainder ") + Remainder;
        internal Dictionary<string, string> Values => new()
        {
            ["f0"] = Total.ToString(CultureInfo.InvariantCulture),
            ["f1"] = Capacity.ToString(CultureInfo.InvariantCulture),
            ["v0"] = Context.Containers, ["v1"] = Context.Items, ["v2"] = Context.Container
        };
        internal string Render(string text)
        {
            foreach (var (key, value) in Values) text = text.Replace("{" + key + "}", value, StringComparison.Ordinal);
            return text;
        }
        internal string Problem => Render(Context.Total + " " + Context.Capacity + " " + Context.Question);
    }

    internal static bool Supports(int variant) => variant == (int)ElementaryQuizType.MinimumGroups || variant == (int)ElementaryQuizType.FullGroups;
    internal static IReadOnlyList<Context> Contexts(AppLanguage language, int variant)
    {
        if (!Supports(variant) || language is not (AppLanguage.Vietnamese or AppLanguage.English))
            throw new ArgumentException("InvalidRemainderGroupsRelation");
        bool minimum = variant == (int)ElementaryQuizType.MinimumGroups;
        string relation = minimum ? "minimum" : "full";
        bool vi = language == AppLanguage.Vietnamese;
        string total = vi ? "Có tất cả {f0} {v1} cần xếp." : "There are {f0} {v1} to pack in total.";
        string alternate = vi ? "Tổng số {v1} cần xếp là {f0}." : "The total number of {v1} to pack is {f0}.";
        string capacity = minimum
            ? (vi ? "Mỗi {v2} chứa được nhiều nhất {f1} {v1}." : "Each {v2} can hold at most {f1} {v1}.")
            : (vi ? "Mỗi {v2} đầy chứa đúng {f1} {v1}." : "Each full {v2} holds exactly {f1} {v1}.");
        string capacityAlternate = minimum
            ? (vi ? "Sức chứa tối đa của mỗi {v2} là {f1} {v1}." : "The maximum capacity of each {v2} is {f1} {v1}.")
            : (vi ? "Một {v2} đầy có {f1} {v1}." : "A full {v2} contains {f1} {v1}.");
        string question = minimum
            ? (vi ? "Cần ít nhất bao nhiêu {v0} để chứa hết?" : "What is the minimum number of {v0} needed to hold everything?")
            : (vi ? "Xếp được bao nhiêu {v0} đầy?" : "How many full {v0} can be filled?");
        string questionAlternate = minimum
            ? (vi ? "Để xếp hết, cần tối thiểu bao nhiêu {v0}?" : "How many {v0} are needed at a minimum to pack all the items?")
            : (vi ? "Có thể xếp đầy bao nhiêu {v0}?" : "How many {v0} can be completely filled?");
        string fullLead = vi ? "Số {v0} đầy" : "Number of full {v0}";
        string minimumLead = vi ? "Số {v0} tối thiểu cần dùng" : "Minimum number of {v0} needed";
        return vi
            ? [new(relation + "/pens", "cái bút", "hộp", "hộp", 12, total, alternate, capacity, capacityAlternate, question, questionAlternate, fullLead, minimumLead),
               new(relation + "/apples", "quả táo", "giỏ", "giỏ", 15, total, alternate, capacity, capacityAlternate, question, questionAlternate, fullLead, minimumLead)]
            : [new(relation + "/pens", "pens", "boxes", "box", 12, total, alternate, capacity, capacityAlternate, question, questionAlternate, fullLead, minimumLead),
               new(relation + "/apples", "apples", "baskets", "basket", 15, total, alternate, capacity, capacityAlternate, question, questionAlternate, fullLead, minimumLead)];
    }
    internal static IEnumerable<string> Profiles(AppLanguage language, int variant) => Contexts(language, variant).Select(c => Prefix + c.Id);
    internal static Data Create(string profile, AppLanguage language, CurriculumTier tier, int variant, Random random)
    {
        if (!Enum.IsDefined(tier)) throw new ArgumentException("InvalidRemainderGroupsTier");
        var context = Contexts(language, variant).SingleOrDefault(c => Prefix + c.Id == profile)
            ?? throw new ArgumentException("InvalidRemainderGroupsProfile");
        int capacity = random.Next(2, Math.Min(context.MaximumCapacity, 3 + 2 * (int)tier) + 1);
        // A zero floor result is meaningful; total is always strictly positive.
        int full = random.Next(0, 3 + 2 * (int)tier);
        int remainder = random.Next(full == 0 ? 1 : 0, capacity);
        return new(context, profile, (ElementaryQuizType)variant, full * capacity + remainder, capacity);
    }
    internal static bool IsValidModel(ElementaryQuizContract p)
    {
        if (p.Kind != QuizProblemKind.Remainder || !Supports((int)p.Type) || p.Reasoning is not { } r
            || p.StoryContextId is null || r.ScenarioId != p.StoryContextId || !Enum.IsDefined(r.Tier)
            || p.Language is not (AppLanguage.Vietnamese or AppLanguage.English)
            || p.Facts.Count != 2 || r.Givens.Count != 2 || p.Answers.Count != 1 || !p.RequiresSolution || !p.RequiresCalculation
            || p.Visual is not null || p.DataChart is not null || p.AreaMeasurement is not null
            || !int.TryParse(p.Facts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int total)
            || !int.TryParse(p.Facts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int capacity)
            || total <= 0 || capacity < 2) return false;
        var context = Contexts(p.Language, (int)p.Type).SingleOrDefault(c => Prefix + c.Id == p.StoryContextId);
        if (context is null || capacity > Math.Min(context.MaximumCapacity, 3 + 2 * (int)r.Tier)
            || total / capacity >= 3 + 2 * (int)r.Tier) return false;
        var d = new Data(context, p.StoryContextId, p.Type, total, capacity);
        if (!r.Givens.SequenceEqual(new[] { new ElementaryGivenValue("total-item-count", p.Facts[0], context.Items),
                new ElementaryGivenValue("items-per-container-capacity", p.Facts[1], context.Items) })) return false;
        string target = d.FullExpression + (p.Type == ElementaryQuizType.MinimumGroups ? "+" + d.Extra : "");
        var a = p.Answers[0];
        if (a.Value != new ReducedFraction(d.Answer, 1) || a.Unit != d.AnswerUnit || a.Expression != target || a.IsText
            || r.Steps.Count != (p.Type == ElementaryQuizType.MinimumGroups ? 2 : 1)) return false;
        for (int i = 0; i < r.Steps.Count; i++)
        {
            var step = r.Steps[i];
            int expected = i == 0 ? d.Full : d.Answer;
            if (step.Expression != (i == 0 ? d.FullExpression : target) || step.Value != new ReducedFraction(expected, 1)
                || step.DisplayValue != expected.ToString(CultureInfo.InvariantCulture)
                || step.Unit != (p.Type == ElementaryQuizType.MinimumGroups && i == 0 ? context.Containers : d.AnswerUnit)) return false;
        }
        return p.Constants.Contains(d.Full.ToString(CultureInfo.InvariantCulture))
            && p.Constants.Contains(d.Remainder.ToString(CultureInfo.InvariantCulture));
    }
}

public sealed partial class ElementaryQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateRemainderGroupsOpen(ArithmeticQuizMode mode, AppLanguage language,
        CurriculumTier tier, int variant, string profile)
    {
        var d = RemainderGroupsPilot.Create(profile, language, tier, variant, _random);
        var b = new DifficultyBuilder(QuizProblemKind.Remainder, d.Type, language, tier);
        b.Given("total-item-count", d.Total, d.Context.Items);
        b.Given("items-per-container-capacity", d.Capacity, d.Context.Items);
        b.Constant(d.Full, d.Remainder);
        if (d.Type == ElementaryQuizType.MinimumGroups)
        {
            b.Step(d.Render(d.Context.FullLead), d.FullExpression, d.Context.Containers);
            b.Answer(d.Render(d.Context.MinimumLead), d.FullExpression + "+" + d.Extra, d.AnswerUnit);
            b.Explanation = language == AppLanguage.Vietnamese
                ? (d.Remainder > 0 ? "Số đồ vật còn dư cần thêm một đồ đựng." : "Phép chia hết nên không cần thêm đồ đựng.")
                : (d.Remainder > 0 ? "The remaining items need one additional container." : "The division is exact, so no additional container is needed.");
        }
        else
        {
            b.Answer(d.Render(d.Context.FullLead), d.FullExpression, d.AnswerUnit);
            b.Explanation = language == AppLanguage.Vietnamese
                ? "Chỉ tính các đồ đựng đầy; số đồ vật dư không tạo thành một đồ đựng đầy."
                : "Only full containers count; any remaining items do not fill another container.";
        }
        var q = CompleteQuestion(mode, b.Build(profile, d.Problem) with { StoryContextId = profile }, [], null);
        string solution = b.Explanation + "\n" + d.Render(d.Context.FullLead) + ":\n" + d.DivisionCalculation(language);
        if (d.Type == ElementaryQuizType.MinimumGroups)
            solution += "\n" + d.Render(d.Context.MinimumLead) + $":\n{d.Full} + {d.Extra} = {d.Answer} {d.AnswerUnit}";
        solution += "\n" + (language == AppLanguage.Vietnamese ? "Đáp số: " : "Answer: ") + q.ElementaryProblem!.AnswerText;
        return q with { ElementaryProblem = q.ElementaryProblem with { SolutionText = solution } };
    }
}
