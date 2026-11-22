using System.Text.Json;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static readonly string[] CalendarRequiredFields = ["Actor", "Item", "StartAction", "EndAction", "Interval"];
    private static readonly string[] CalendarFactFields = ["Facts", "AlternativeFacts"];
    private static readonly string[] CalendarQuestionFields = ["Question", "AlternativeQuestion"];
    private static readonly string[] CalendarKnownRoles = ["start-day", "end-day", "month", "year", "include-start", "include-end"];
    private static readonly string[] AdjacentCalendarKnownRoles =
        ["start-day", "end-day", "start-month", "end-month", "year", "start-month-length", "include-start", "include-end"];
    private static readonly string[][] CalendarFactSlots =
    [
        ["{f0}", "{f2}", "{f3}", "{v0}", "{v1}", "{v2}"],
        ["{f1}", "{f2}", "{f3}", "{v0}", "{v1}", "{v3}"],
        []
    ];
    private static readonly string[][] AdjacentCalendarFactSlots =
    [
        ["{f0}", "{f2}", "{f3}", "{v0}", "{v1}", "{v2}"],
        ["{f1}", "{f4}", "{f3}", "{v0}", "{v1}", "{v3}"],
        ["{f2}", "{f5}"],
        []
    ];
    private static readonly string[] CalendarQuestionSlots = ["{v0}", "{v4}"];

    private static void CheckSameMonthCalendarLists(QuizContentPack pack)
    {
        CheckSameMonthCalendarList(pack, inclusive: false);
        CheckSameMonthCalendarList(pack, inclusive: true);
        CheckSameMonthCalendarList(pack, inclusive: false, adjacent: true);
        CheckSameMonthCalendarList(pack, inclusive: true, adjacent: true);
        CheckSameMonthCalendarList(pack, inclusive: true, adjacent: true, february: true);
        CheckSameMonthCalendarList(pack, inclusive: true, adjacent: true, leap: true);
    }

    private static void CheckSameMonthCalendarList(QuizContentPack pack, bool inclusive, bool adjacent = false,
        bool february = false, bool leap = false)
    {
        string listName = leap ? SameMonthCalendarPilot.FebruaryLeapListName
            : february ? SameMonthCalendarPilot.FebruaryCommonListName
            : adjacent ? (inclusive ? SameMonthCalendarPilot.AdjacentInclusiveListName : SameMonthCalendarPilot.AdjacentListName)
            : inclusive ? SameMonthCalendarPilot.InclusiveListName : SameMonthCalendarPilot.ListName;
        if (!pack.Lists.TryGetValue(listName, out var rows)) return;
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() == 0)
            Fail("Missing same-month contexts.");

        string relation = leap ? SameMonthCalendarPilot.FebruaryLeapRelation
            : february ? SameMonthCalendarPilot.FebruaryCommonRelation
            : adjacent ? (inclusive ? SameMonthCalendarPilot.AdjacentInclusiveRelation : SameMonthCalendarPilot.AdjacentRelation)
            : inclusive ? SameMonthCalendarPilot.InclusiveRelation : SameMonthCalendarPilot.Relation;
        string countRule = inclusive ? "include-start-include-end" : "exclude-start-include-end";
        string targetRole = inclusive ? "counted-calendar-days" : "elapsed-days";
        string[] knownRoles = adjacent ? AdjacentCalendarKnownRoles : CalendarKnownRoles;
        string[][] factSlots = adjacent ? AdjacentCalendarFactSlots : CalendarFactSlots;
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows.EnumerateArray())
        {
            if (!ids.Add(RequiredString(row, "Id"))) Fail("Duplicate same-month context.");
            foreach (string field in CalendarRequiredFields) _ = RequiredString(row, field);
            if (RequiredString(row, "StartAction") == RequiredString(row, "EndAction"))
                Fail("Same-month events must differ.");
            if (RequiredString(row, "RelationId") != relation || RequiredString(row, "CountRule") != countRule
                || RequiredString(row, "TargetRole") != targetRole)
                Fail("Changed same-month relation/counting rule.");

            var roles = row.GetProperty("KnownRoles");
            if (roles.GetArrayLength() != knownRoles.Length) Fail("Changed calendar roles.");
            for (int i = 0; i < knownRoles.Length; i++)
                if (roles[i].GetString() != knownRoles[i]) Fail("Changed calendar roles.");

            foreach (string field in CalendarFactFields)
            {
                var facts = row.GetProperty(field);
                if (facts.GetArrayLength() != factSlots.Length)
                    Fail("Calendar requires dates, month length where needed, and an explicit count rule.");
                for (int i = 0; i < factSlots.Length; i++)
                    if (!HasExactPlaceholderSlots(facts[i].GetString()!, factSlots[i]))
                        Fail("Changed calendar fact slots.");
            }

            foreach (string field in CalendarQuestionFields)
                if (!HasExactPlaceholderSlots(RequiredString(row, field), CalendarQuestionSlots))
                    Fail("Changed same-month target slots.");
            if (!HasExactPlaceholderSlots(RequiredString(row, "Lead"), []))
                Fail("Unexpected same-month lead slots.");
        }
    }

    // Preserve Regex.Matches(text, @"\{[^{}]+\}") multiset semantics without Match strings or LINQ.
    private static bool HasExactPlaceholderSlots(ReadOnlySpan<char> text, ReadOnlySpan<string> expected)
    {
        Span<bool> seen = stackalloc bool[8];
        seen.Clear();
        int count = 0;
        foreach (var match in PlaceholderSlotPattern().EnumerateMatches(text))
        {
            int index = 0;
            for (; index < expected.Length; index++)
                if (text.Slice(match.Index, match.Length).SequenceEqual(expected[index])) break;
            if (index == expected.Length || seen[index]) return false;
            seen[index] = true;
            count++;
        }
        return count == expected.Length;
    }

    [GeneratedRegex(@"\{[^{}]+\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderSlotPattern();
}
