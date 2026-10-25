using MathSolver.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckMotionExtendedLists(QuizContentPack pack)
    {
        bool hasContexts = pack.Lists.TryGetValue("MotionExtended.Contexts", out var contexts);
        bool hasRelations = pack.Lists.TryGetValue("MotionExtended.Relations", out var relations);
        if (!hasContexts && !hasRelations) return;
        if (!hasContexts || !hasRelations || contexts.ValueKind != JsonValueKind.Array || relations.ValueKind != JsonValueKind.Array
            || contexts.GetArrayLength() != 4 || relations.GetArrayLength() != 6) Fail("Invalid extended motion catalogue size.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var context in contexts.EnumerateArray())
        {
            string id = RequiredString(context, "Id"), group = RequiredString(context, "Group");
            if (!ids.Add(id)) Fail("Duplicate extended motion context.");
            var expected = id switch
            {
                "cyclists" => (Group: "road", Min: 8, Max: 24, Current: 0),
                "cars" => (Group: "road", Min: 30, Max: 80, Current: 0),
                "canoe" => (Group: "river", Min: 8, Max: 16, Current: 3),
                "motorboat" => (Group: "river", Min: 18, Max: 45, Current: 6),
                _ => (Group: "", Min: 0, Max: 0, Current: 0)
            };
            if (group != expected.Group || context.GetProperty("MinimumSpeed").GetInt32() != expected.Min
                || context.GetProperty("MaximumSpeed").GetInt32() != expected.Max
                || context.GetProperty("MaximumCurrent").GetInt32() != expected.Current) Fail("Changed extended motion context bounds.");
            CheckText(RequiredString(context, "First"), []);
            if (!context.TryGetProperty("Second", out var second) || second.ValueKind != JsonValueKind.String)
                Fail("Missing second extended motion actor.");
            if (group == "road")
            {
                CheckText(RequiredString(context, "Second"), []);
                if (second.GetString() == context.GetProperty("First").GetString()) Fail("Extended motion actors must be distinct.");
            }
            else if (second.GetString() != "") Fail("River relation requires one craft.");
        }
        if (!ids.SetEquals(["cyclists", "cars", "canoe", "motorboat"])) Fail("Unknown extended motion contexts.");
        ids.Clear();
        foreach (var relation in relations.EnumerateArray())
        {
            string id = RequiredString(relation, "Id"), group = RequiredString(relation, "Group");
            if (!ids.Add(id)) Fail("Duplicate extended motion relation.");
            var expected = id switch
            {
                "meeting" => (MotionQuizType.Meeting, MotionQuestionKind.MeetingTime),
                "chasing" => (MotionQuizType.Chasing, MotionQuestionKind.CatchUpTime),
                "downstream" => (MotionQuizType.River, MotionQuestionKind.RiverDownstreamSpeed),
                "upstream" => (MotionQuizType.River, MotionQuestionKind.RiverUpstreamSpeed),
                "boat" => (MotionQuizType.River, MotionQuestionKind.RiverBoatSpeed),
                "current" => (MotionQuizType.River, MotionQuestionKind.RiverCurrentSpeed),
                _ => ((MotionQuizType)(-1), (MotionQuestionKind)(-1))
            };
            if ((int)expected.Item1 < 0 || ReadEnum<MotionQuizType>(relation, "Type") != (int)expected.Item1
                || ReadEnum<MotionQuestionKind>(relation, "Kind") != (int)expected.Item2
                || group != (id is "meeting" or "chasing" ? "road" : "river")) Fail("Changed extended motion relation identity.");
            bool road = group == "road";
            foreach (string key in new[] { "Facts", "AlternateFacts" })
            {
                var facts = RequiredArray(relation, key);
                if (facts.GetArrayLength() != (road ? 4 : 3)) Fail("Changed extended motion fact count.");
                string[][] slots = road
                    ? [["v0", "v1", "f0", "v4"], ["v0", "f1", "v2"], ["v1", "f2", "v2"], []]
                    : [["v0", "f0", "v2"], id is "boat" or "current" ? ["v0", "f1", "v2"] : ["f1", "v2"], []];
                for (int i = 0; i < slots.Length; i++)
                {
                    if (facts[i].ValueKind != JsonValueKind.String) Fail("Invalid extended motion fact.");
                    CheckText(facts[i].GetString()!, slots[i]);
                }
            }
            CheckText(RequiredString(relation, "Question"), [road ? "v3" : "v2"]);
            CheckText(RequiredString(relation, "Lead"), []);
        }
        if (!ids.SetEquals(["meeting", "chasing", "downstream", "upstream", "boat", "current"])) Fail("Unknown extended motion relation.");

        static int ReadEnum<T>(JsonElement row, string field) where T : struct, Enum
        {
            if (!row.TryGetProperty(field, out var value)) return -1;
            return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) ? number
                : value.ValueKind == JsonValueKind.String && Enum.TryParse<T>(value.GetString(), out var parsed) ? Convert.ToInt32(parsed) : -1;
        }
        static void CheckText(string text, string[] slots)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Any(char.IsControl) || text.Length > 700
                || !Regex.Matches(text, @"\{([^{}]+)\}").Select(m => m.Groups[1].Value).Order().SequenceEqual(slots.Order())
                || Regex.Replace(text, @"\{[^{}]+\}", "").Any(char.IsDigit)) Fail("Changed extended motion placeholders or prose.");
        }
    }
}
