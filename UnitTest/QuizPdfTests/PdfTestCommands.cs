internal sealed record PdfTestCommand(string OutputLabel, Func<string, Task> Run);

internal static class PdfTestCommands
{
    internal static IReadOnlyDictionary<string, PdfTestCommand> All { get; } =
        new Dictionary<string, PdfTestCommand>(StringComparer.Ordinal)
        {
            ["--food-themes"] = new("Food/drink/fruit themes", FoodThemeWorksheetTests.Run),
            ["--applied-open"] = new("Applied open-prose", AppliedOpenWorksheetTests.Run),
            ["--practical-open"] = new("Practical open-prose", PracticalOpenWorksheetTests.Run),
            ["--motion-open"] = new("Motion", MotionOpenWorksheetTests.Run),
            ["--motion-extended-open"] = new("Extended motion", MotionExtendedWorksheetTests.Run),
            ["--unitary-total-open"] = new("Unitary-total", UnitaryTotalWorksheetTests.Run),
            ["--unitary-groups-open"] = new("Unitary-groups", UnitaryGroupsWorksheetTests.Run),
            ["--difference-ratio-open"] = new("Difference-ratio", DifferenceRatioOpenWorksheetTests.Run),
            ["--sum-ratio-open"] = new("Sum-ratio", SumRatioOpenWorksheetTests.Run),
            ["--sum-difference-open"] = new("Sum/difference", SumDifferenceOpenWorksheetTests.Run),
            ["--average-missing-open"] = new("Average missing-value", AverageMissingWorksheetTests.Run),
            ["--average-open"] = new("Average", AverageOpenWorksheetTests.Run),
            // Preserve the existing focused command's coverage of both targets.
            ["--percentage-part-open"] = new("Percentage", async directory =>
            {
                await PercentagePartOpenWorksheetTests.Run(directory);
                await PercentageRemainingOpenWorksheetTests.Run(directory);
            }),
            ["--percentage-remaining-open"] = new("Percentage remaining", PercentageRemainingOpenWorksheetTests.Run),
            ["--percentage-whole-open"] = new("Percentage whole", PercentageWholeOpenWorksheetTests.Run),
            ["--fraction-remaining-open"] = new("Fraction remainder", FractionRemainingOpenWorksheetTests.Run),
            ["--fraction-whole-open"] = new("Fraction whole", FractionWholeOpenWorksheetTests.Run),
            ["--rectangle-area-open"] = new("Rectangle area", RectangleAreaOpenWorksheetTests.Run),
            ["--rectangle-perimeter-open"] = new("Rectangle perimeter", RectanglePerimeterOpenWorksheetTests.Run),
            ["--time-break-open"] = new("One-break", SameDayBreakWorksheetTests.Run),
            ["--overnight-open"] = new("Overnight", OvernightTimeWorksheetTests.Run),
            ["--calendar-open"] = new("Calendar", directory => SameMonthCalendarWorksheetTests.Run(directory)),
            ["--calendar-inclusive-open"] = new("Calendar", directory =>
                SameMonthCalendarWorksheetTests.Run(directory, inclusive: true)),
            ["--calendar-adjacent-open"] = new("Calendar", directory =>
                SameMonthCalendarWorksheetTests.Run(directory, adjacent: true)),
            ["--calendar-adjacent-inclusive-open"] = new("Calendar", directory =>
                SameMonthCalendarWorksheetTests.Run(directory, inclusive: true, adjacent: true)),
            ["--calendar-february-common-open"] = new("Calendar", directory =>
                SameMonthCalendarWorksheetTests.Run(directory, inclusive: true, adjacent: true, february: true)),
            ["--calendar-february-leap-open"] = new("Calendar", directory =>
                SameMonthCalendarWorksheetTests.Run(directory, inclusive: true, adjacent: true, leap: true))
        };
}
