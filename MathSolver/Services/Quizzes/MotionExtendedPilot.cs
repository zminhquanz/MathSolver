using MathSolver.Models;
using System.Globalization;
using System.Numerics;

namespace MathSolver.Services;

internal static class MotionExtendedPilot
{
    internal const string Prefix = "motion-extended/";
    internal sealed record Context(string Id, string Group, string First, string Second, int MinimumSpeed, int MaximumSpeed, int MaximumCurrent);
    internal sealed record Relation(string Id, MotionQuizType Type, MotionQuestionKind Kind, string Group,
        IReadOnlyList<string> Facts, string Question, string Lead, IReadOnlyList<string> AlternateFacts);
    internal sealed record Data(Context Context, Relation Relation, string Profile, AppLanguage Language, int[] Facts)
    {
        internal bool IsRoad => Relation.Group == "road";
        internal string TimeUnit => Language == AppLanguage.Vietnamese ? "giờ" : "hours";
        internal string AnswerUnit => IsRoad ? TimeUnit : "km/h";
        internal BigInteger RepresentativeLeft => Relation.Id switch
        {
            "boat" => (BigInteger)Facts[0] + Facts[1],
            "current" => (BigInteger)Facts[0] - Facts[1],
            _ => Facts[0]
        };
        internal BigInteger RepresentativeRight => Relation.Id switch
        {
            "meeting" => (BigInteger)Facts[1] + Facts[2],
            "chasing" => (BigInteger)Facts[2] - Facts[1],
            "boat" or "current" => 2,
            _ => Facts[1]
        };
        internal ArithmeticOperation RepresentativeOperation => Relation.Id switch
        {
            "downstream" => ArithmeticOperation.Add,
            "upstream" => ArithmeticOperation.Subtract,
            _ => ArithmeticOperation.Divide
        };
        internal int Answer => checked((int)(Relation.Id switch
        {
            "downstream" => RepresentativeLeft + RepresentativeRight,
            "upstream" => RepresentativeLeft - RepresentativeRight,
            _ => RepresentativeLeft / RepresentativeRight
        }));
        internal string Equation => Relation.Id switch
        {
            "meeting" => $"{Facts[0]} ÷ ({Facts[1]} + {Facts[2]}) = {Answer}",
            "chasing" => $"{Facts[0]} ÷ ({Facts[2]} - {Facts[1]}) = {Answer}",
            "downstream" => $"{Facts[0]} + {Facts[1]} = {Answer}",
            "upstream" => $"{Facts[0]} - {Facts[1]} = {Answer}",
            "boat" => $"({Facts[0]} + {Facts[1]}) ÷ 2 = {Answer}",
            _ => $"({Facts[0]} - {Facts[1]}) ÷ 2 = {Answer}"
        };
        internal Dictionary<string, string> Values
        {
            get
            {
                var values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["f0"] = N(Facts[0]), ["f1"] = N(Facts[1]), ["v0"] = Context.First, ["v2"] = "km/h"
                };
                if (IsRoad)
                {
                    values["f2"] = N(Facts[2]); values["v1"] = Context.Second;
                    values["v3"] = TimeUnit; values["v4"] = "km";
                }
                return values;
            }
        }
        internal string Render(string text)
        {
            foreach (var (key, value) in Values) text = text.Replace("{" + key + "}", value, StringComparison.Ordinal);
            return text;
        }
        internal string Problem => Render(string.Join(" ", Relation.Facts.Append(Relation.Question)));
    }

    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    internal static bool IsProfile(string? profile) => profile?.StartsWith(Prefix, StringComparison.Ordinal) == true;
    internal static IReadOnlyList<Context> Contexts(AppLanguage language) =>
        QuizContentCatalog.LoadList<Context>("MotionExtended.Contexts", QuizContentCatalog.Culture(language));
    internal static IReadOnlyList<Relation> Relations(AppLanguage language) =>
        QuizContentCatalog.LoadList<Relation>("MotionExtended.Relations", QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language, MotionQuizType? type = null) =>
        Relations(language).Where(r => !type.HasValue || r.Type == type.Value)
            .SelectMany(r => Contexts(language).Where(c => c.Group == r.Group).Select(c => Prefix + r.Id + "/" + c.Id));

    internal static Data Create(string profile, AppLanguage language, CurriculumTier tier, Random random)
    {
        string[] parts = profile.Split('/');
        if (parts.Length != 3 || parts[0] != "motion-extended" || !Enum.IsDefined(tier)) throw new ArgumentException("InvalidMotionProfile");
        var relation = Relations(language).SingleOrDefault(r => r.Id == parts[1]) ?? throw new ArgumentException("InvalidMotionRelation");
        var context = Contexts(language).SingleOrDefault(c => c.Id == parts[2] && c.Group == relation.Group)
            ?? throw new ArgumentException("InvalidMotionContext");
        int maximum = Math.Min(context.MaximumSpeed, context.MinimumSpeed + 4 * (int)tier);
        int first = random.Next(context.MinimumSpeed, maximum);
        if (context.Group == "road")
        {
            int second = random.Next(first + 1, maximum + 1);
            // Keep duration questions in plural hours, as in the existing pilot.
            int time = random.Next(2, Math.Min(5, (int)tier + 2) + 1);
            int relative = relation.Id == "meeting" ? checked(first + second) : second - first;
            return new(context, relation, profile, language, [checked(relative * time), first, second]);
        }
        int current = random.Next(1, Math.Min(context.MaximumCurrent, first - 1) + 1);
        return new(context, relation, profile, language, relation.Id is "boat" or "current"
            ? [checked(first + current), first - current] : [first, current]);
    }

    internal static bool ValidContract(MotionQuizContract contract)
    {
        if (!IsProfile(contract.NarrativeId)) return false;
        string[] parts = contract.NarrativeId!.Split('/');
        if (parts.Length != 3) return false;
        var relation = Relations(AppLanguage.English).SingleOrDefault(r => r.Id == parts[1]);
        var context = Contexts(AppLanguage.English).SingleOrDefault(c => c.Id == parts[2]);
        if (relation is null || context is null || relation.Group != context.Group
            || contract.Type != relation.Type || contract.QuestionKind != relation.Kind
            || contract.Facts.Count != (relation.Group == "road" ? 3 : 2) || contract.Facts.Any(f => f <= 0)) return false;
        if (contract.NarrativeActors is null) return false;
        if (relation.Group == "road")
        {
            if (contract.AnswerUnit is not ("giờ" or "hours")) return false;
            var actorContext = contract.AnswerUnit == "giờ"
                ? Contexts(AppLanguage.Vietnamese).Single(c => c.Id == context.Id) : context;
            if (!contract.NarrativeActors.SequenceEqual(new[] { actorContext.First, actorContext.Second }, StringComparer.Ordinal)) return false;
        }
        else if (contract.NarrativeActors.Count != 1
            || contract.NarrativeActors[0] != context.First
                && contract.NarrativeActors[0] != Contexts(AppLanguage.Vietnamese).Single(c => c.Id == context.Id).First) return false;
        var f = contract.Facts;
        BigInteger left = f[0], right;
        ArithmeticOperation operation;
        if (relation.Group == "road")
        {
            if (f[1] < context.MinimumSpeed || f[1] > context.MaximumSpeed
                || f[2] < context.MinimumSpeed || f[2] > context.MaximumSpeed
                || f[2] <= f[1]) return false;
            right = relation.Id == "meeting" ? (BigInteger)f[1] + f[2] : (BigInteger)f[2] - f[1];
            operation = ArithmeticOperation.Divide;
        }
        else if (relation.Id is "boat" or "current")
        {
            if (f[0] <= f[1] || ((BigInteger)f[0] + f[1]) % 2 != 0) return false;
            BigInteger boat = ((BigInteger)f[0] + f[1]) / 2, current = ((BigInteger)f[0] - f[1]) / 2;
            if (boat < context.MinimumSpeed || boat > context.MaximumSpeed || current > context.MaximumCurrent) return false;
            left = relation.Id == "boat" ? (BigInteger)f[0] + f[1] : (BigInteger)f[0] - f[1];
            right = 2; operation = ArithmeticOperation.Divide;
        }
        else
        {
            if (f[0] < context.MinimumSpeed || f[0] > context.MaximumSpeed || f[1] > context.MaximumCurrent || f[1] >= f[0]) return false;
            right = f[1]; operation = relation.Id == "downstream" ? ArithmeticOperation.Add : ArithmeticOperation.Subtract;
        }
        if (left <= 0 || left > int.MaxValue || right <= 0 || right > int.MaxValue
            || operation == ArithmeticOperation.Divide && left % right != 0) return false;
        BigInteger answer = operation == ArithmeticOperation.Add ? left + right
            : operation == ArithmeticOperation.Subtract ? left - right : left / right;
        string[] units = relation.Group == "road" ? ["km/h", contract.AnswerUnit, "km"] : ["km/h"];
        return answer > 0 && answer <= int.MaxValue && contract.CorrectAnswer == answer
            && (relation.Group == "road" ? contract.AnswerUnit is "giờ" or "hours" : contract.AnswerUnit == "km/h")
            && contract.RequiredProblemUnits.Count == units.Length
            && contract.RequiredProblemUnits.ToHashSet(StringComparer.Ordinal).SetEquals(units)
            && contract.RepresentativeLeft == left && contract.RepresentativeRight == right
            && contract.RepresentativeOperation == operation;
    }
}

public sealed partial class MotionQuizGenerator
{
    public ArithmeticQuizQuestion GenerateExtendedNarrative(ArithmeticQuizMode mode, AppLanguage language, MotionQuizType type,
        QuizCurriculumContext context, string profile)
    {
        var data = MotionExtendedPilot.Create(profile, language, context.Tier, _random);
        if (type != data.Relation.Type) throw new ArgumentException("InvalidMotionRelation");
        string lead = data.Render(data.Relation.Lead);
        string solution = lead + ":\n" + data.Equation + " " + data.AnswerUnit + "\n"
            + (language == AppLanguage.Vietnamese ? "Đáp số: " : "Answer: ") + data.Answer + " " + data.AnswerUnit;
        var contract = new MotionQuizContract(type, data.Relation.Kind, data.Facts, data.Answer, data.AnswerUnit,
            lead, data.Problem, data.Equation, solution, data.RepresentativeLeft, data.RepresentativeOperation, data.RepresentativeRight,
            data.IsRoad ? ["km/h", data.TimeUnit, "km"] : ["km/h"])
        {
            NarrativeId = data.Profile,
            NarrativeActors = data.IsRoad ? [data.Context.First, data.Context.Second] : [data.Context.First]
        };
        return CreateQuestion(mode, contract);
    }
}
