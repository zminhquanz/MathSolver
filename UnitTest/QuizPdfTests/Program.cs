using Microsoft.Maui.Graphics;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Services.Printing;
using MathSolver.Services.Core;
using PdfSharp.Pdf.IO;
using System.Text.Json;
if (args is ["--help"] or ["--list"])
{
    Console.WriteLine("Extended motion PDF suite: --motion-extended-open output-root");
    Console.WriteLine("Usage: QuizPdfTests [--artifacts [output-root] | legacy-output-root | --unitary-groups-open output-root | --unitary-total-open output-root | --difference-ratio-open output-root | --sum-ratio-open output-root | --sum-difference-open output-root | --average-missing-open output-root | --average-open output-root | --percentage-part-open output-root | --fraction-remaining-open output-root | --fraction-whole-open output-root | --percentage-remaining-open output-root | --percentage-whole-open output-root | --rectangle-perimeter-open output-root | --rectangle-area-open output-root | --time-break-open output-root | --overnight-open output-root | --calendar-open output-root | --calendar-inclusive-open output-root | --calendar-adjacent-open output-root | --calendar-adjacent-inclusive-open output-root | --calendar-february-common-open output-root | --calendar-february-leap-open output-root]. Default: isolated temporary files, cleaned after tests.");
    return 0;
}
bool februaryLeapOnly=args is ["--calendar-february-leap-open",_];
bool februaryCommonOnly=args is ["--calendar-february-common-open",_];
bool adjacentInclusiveOnly=args is ["--calendar-adjacent-inclusive-open",_];
bool adjacentCalendarOnly=args is ["--calendar-adjacent-open",_];
bool calendarOnly=args is ["--calendar-open",_];
bool inclusiveCalendarOnly=args is ["--calendar-inclusive-open",_];
bool overnightOnly=args is ["--overnight-open",_];
bool timeBreakOnly=args is ["--time-break-open",_];
bool rectangleAreaOnly=args is ["--rectangle-area-open",_];
bool rectanglePerimeterOnly=args is ["--rectangle-perimeter-open",_];
bool percentageWholeOnly=args is ["--percentage-whole-open",_];
bool percentageRemainingOnly=args is ["--percentage-remaining-open",_];
bool wholeOnly=args is ["--fraction-whole-open",_];
bool remainingOnly=args is ["--fraction-remaining-open",_];
bool unitaryOnly=args is ["--unitary-total-open",_];
bool motionOpenOnly=args is ["--motion-open",_];
bool motionExtendedOnly=args is ["--motion-extended-open",_];
bool unitaryGroupsOnly=args is ["--unitary-groups-open",_];
bool differenceRatioOnly=args is ["--difference-ratio-open",_];
bool sumRatioOnly=args is ["--sum-ratio-open",_];
bool sumDifferenceOnly=args is ["--sum-difference-open",_];
bool averageMissingOnly=args is ["--average-missing-open",_];
bool averageOnly=args is ["--average-open",_];
bool percentageOnly=args is ["--percentage-part-open",_];
if(februaryLeapOnly||februaryCommonOnly||adjacentInclusiveOnly||adjacentCalendarOnly||inclusiveCalendarOnly||calendarOnly||overnightOnly||timeBreakOnly||rectangleAreaOnly||rectanglePerimeterOnly||percentageWholeOnly||unitaryOnly||motionOpenOnly||motionExtendedOnly||unitaryGroupsOnly||differenceRatioOnly||sumRatioOnly||sumDifferenceOnly||averageMissingOnly||averageOnly||percentageOnly||remainingOnly||wholeOnly||percentageRemainingOnly)args=["--artifacts",args[1]];
if (!(args.Length == 0 || args is ["--artifacts"] or ["--artifacts", _] || (args.Length == 1 && !args[0].StartsWith("--"))))
{
    Console.Error.WriteLine("Invalid PDF test arguments. Use --help.");
    return 2;
}
try
{
    using var workspace = args.Length == 0 ? TestWorkspace.Temporary("Pdf")
        : TestWorkspace.Artifacts("quiz-pdf", args[0] == "--artifacts" ? (args.Length == 2 ? args[1] : "artifacts/verification") : args[0]);
    string directory = workspace.DirectoryPath;
    await HarnessInfrastructureTests.RunAsync();
    if(februaryLeapOnly||februaryCommonOnly||adjacentInclusiveOnly||adjacentCalendarOnly||calendarOnly||inclusiveCalendarOnly)
    {
        await SameMonthCalendarWorksheetTests.Run(directory,inclusiveCalendarOnly||adjacentInclusiveOnly||februaryCommonOnly||februaryLeapOnly,adjacentCalendarOnly||adjacentInclusiveOnly||februaryCommonOnly||februaryLeapOnly,februaryCommonOnly,februaryLeapOnly);
        Console.WriteLine("Calendar PDF output: "+directory);
        return 0;
    }
    if(overnightOnly)
    {
        await OvernightTimeWorksheetTests.Run(directory);
        Console.WriteLine("Overnight PDF output: "+directory);
        return 0;
    }
    if(timeBreakOnly)
    {
        await SameDayBreakWorksheetTests.Run(directory);
        Console.WriteLine("One-break PDF output: "+directory);
        return 0;
    }
    if(rectangleAreaOnly)
    {
        await RectangleAreaOpenWorksheetTests.Run(directory);
        Console.WriteLine("Rectangle area PDF output: "+directory);
        return 0;
    }
    if(rectanglePerimeterOnly)
    {
        await RectanglePerimeterOpenWorksheetTests.Run(directory);
        Console.WriteLine("Rectangle perimeter PDF output: "+directory);
        return 0;
    }
    if(percentageWholeOnly)
    {
        await PercentageWholeOpenWorksheetTests.Run(directory);
        Console.WriteLine("Percentage whole PDF output: "+directory);
        return 0;
    }
    if(percentageRemainingOnly)
    {
        await PercentageRemainingOpenWorksheetTests.Run(directory);
        Console.WriteLine("Percentage remaining PDF output: "+directory);
        return 0;
    }
    if(wholeOnly)
    {
        await FractionWholeOpenWorksheetTests.Run(directory);
        Console.WriteLine("Fraction whole PDF output: "+directory);
        return 0;
    }
    if(remainingOnly)
    {
        await FractionRemainingOpenWorksheetTests.Run(directory);
        Console.WriteLine("Fraction remainder PDF output: "+directory);
        return 0;
    }
    if(percentageOnly)
    {
        await PercentagePartOpenWorksheetTests.Run(directory);
    await PercentageRemainingOpenWorksheetTests.Run(directory);
        Console.WriteLine("Percentage PDF output: "+directory);
        return 0;
    }
    if(unitaryOnly)
    {
        await UnitaryTotalWorksheetTests.Run(directory);
        Console.WriteLine("Unitary-total PDF output: "+directory);
        return 0;
    }
    if(motionExtendedOnly)
    {
        await MotionExtendedWorksheetTests.Run(directory);
        Console.WriteLine("Extended motion PDF output: "+directory);
        return 0;
    }
    if(motionOpenOnly)
    {
        await MotionOpenWorksheetTests.Run(directory);
        Console.WriteLine("Motion PDF output: "+directory);
        return 0;
    }
    if(unitaryGroupsOnly)
    {
        await UnitaryGroupsWorksheetTests.Run(directory);
        Console.WriteLine("Unitary-groups PDF output: "+directory);
        return 0;
    }
    if(differenceRatioOnly)
    {
        await DifferenceRatioOpenWorksheetTests.Run(directory);
        Console.WriteLine("Difference-ratio PDF output: "+directory);
        return 0;
    }
    if(sumRatioOnly)
    {
        await SumRatioOpenWorksheetTests.Run(directory);
        Console.WriteLine("Sum-ratio PDF output: "+directory);
        return 0;
    }
    if(sumDifferenceOnly)
    {
        await SumDifferenceOpenWorksheetTests.Run(directory);
        Console.WriteLine("Sum/difference PDF output: "+directory);
        return 0;
    }
    if(averageMissingOnly)
    {
        await AverageMissingWorksheetTests.Run(directory);
        Console.WriteLine("Average missing-value PDF output: "+directory);
        return 0;
    }
    if(averageOnly)
    {
        await AverageOpenWorksheetTests.Run(directory);
        Console.WriteLine("Average PDF output: "+directory);
        return 0;
    }

    var provider = new BasicPracticeQuestionProvider(new QuestionBankStore(Path.Combine(directory, "isolated-bank.db3")));
    var factory = new PracticeQuestionFactory(provider);
    var catalog = new QuizProblemTypeCatalog(new Random(135));
    int cases = 0;
    var traces = new List<object>();
    foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var items = new List<QuizPrintItem>();
            for (int option = 0; option < catalog.Options.Count; option++)
                for (int stars = 1; stars <= 5; stars++)
                {
                    var tier = (CurriculumTier)stars;
                    var request = catalog.Resolve(option, null, null, null, null, null, null, null, null, tier);
                    var q = await factory.CreateAsync(request, mode, new(tier, option == 0), language, new(QuestionKnowledgeGroup.Objects), PracticeQuestionFormat.WordProblem, true);
                    var printed = QuizWorksheetBuilder.Format(q, language);
                    Check(!string.IsNullOrWhiteSpace(printed.Prompt) && !string.IsNullOrWhiteSpace(printed.Answer), "Missing print content.");
                    Check(printed.Diagram?.Explanation is null, "Solution leaked into a question diagram.");
                    if (q.ElementaryProblem?.Visual is { Kind: "bar", HiddenValueIndices.Count: > 0 } original)
                        foreach (var index in original.HiddenValueIndices) Check(printed.Visual!.Values[index] == 0, "Hidden bar leaked.");
                    if (mode == ArithmeticQuizMode.MultipleChoice) Check(printed.Answer.Contains(". "), "Answer key missing choice letter.");
                    if (mode == ArithmeticQuizMode.TrueFalse) Check(printed.Answer == (q.PresentedEquationIsCorrect == true ? printed.Options[0] : printed.Options[1]), "Incorrect assertion key.");
                    items.Add(printed); cases++;
                    traces.Add(new { language, mode, stars, request.Kind, Type = q.ElementaryProblem?.Type.ToString(), printed.Prompt, printed.Answer, printed.Solution });
                }
            var worksheet = new QuizWorksheet("TEST" + (int)language + (int)mode, language == AppLanguage.Vietnamese ? "Luyện tập toán — phân số và hình minh họa" : "Math practice — fractions and diagrams", "Coverage: all available families and 1–5 stars", language, CurriculumTier.ThreeStars, mode, items.AsReadOnly());
            // Split the coverage fixture into valid-sized sets.
            foreach (bool answers in new[] { false, true })
            {
                var sample = worksheet with { Questions = Array.AsReadOnly(items.Take(100).ToArray()) };
                byte[] bytes = QuizPdfWriter.Write(sample, answers);
                string name = $"{language}-{mode}-{(answers ? "answers" : "worksheet")}.pdf";
                File.WriteAllBytes(Path.Combine(directory, name), bytes);
                using var pdf = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
                Check(pdf.PageCount >= 2 && pdf.Pages.Cast<PdfSharp.Pdf.PdfPage>().All(p => Math.Abs(p.Width.Point - 595.28) < 1 && Math.Abs(p.Height.Point - 841.89) < 1), "PDF page size/pagination.");
                PdfAssertions.HasEmbeddedText(bytes);
            }
        }

    int generated = 0;
    async Task<ArithmeticQuizQuestion> Generate(CancellationToken cancellation)
    {
        generated++;
        return await factory.CreateAsync(new(QuizProblemKind.Arithmetic, ArithmeticOperation.Add), ArithmeticQuizMode.Essay,
            new(CurriculumTier.ThreeStars, false), AppLanguage.Vietnamese, new(QuestionKnowledgeGroup.Objects), PracticeQuestionFormat.WordProblem, false, cancellation);
    }
    var batch = await QuizWorksheetBuilder.CreateAsync(20, "Bộ đề toán có lời văn", "Cộng · 3 sao · Số lượng và so sánh", AppLanguage.Vietnamese,
        CurriculumTier.ThreeStars, ArithmeticQuizMode.Essay, Generate);
    Check(batch.Questions.Select(q => q.Identity).Distinct().Count() == 20, "Batch duplicate.");
    int beforeExport = generated; var snapshot = JsonSerializer.Serialize(batch);
    File.WriteAllBytes(Path.Combine(directory, "sample-20-questions.pdf"), QuizPdfWriter.Write(batch, false));
    File.WriteAllBytes(Path.Combine(directory, "sample-20-answers.pdf"), QuizPdfWriter.Write(batch, true));
    Check(generated == beforeExport && JsonSerializer.Serialize(batch) == snapshot, "PDF generation changed the set.");
    var expectedCounts = new[] { 1, 101, -1 };
    foreach (int invalid in expectedCounts)
    {
        try { QuizWorksheetBuilder.ValidateCount(invalid); throw new Exception("Accepted invalid count."); }
        catch (ArgumentOutOfRangeException) { }
    }
    using (var stopped = new CancellationTokenSource())
    {
        stopped.Cancel();
        try { await QuizWorksheetBuilder.CreateAsync(2, "", "", AppLanguage.Vietnamese, CurriculumTier.OneStar, ArithmeticQuizMode.Essay, Generate, cancellation: stopped.Token); throw new Exception("Ignored cancellation."); }
        catch (OperationCanceledException) { }
    }
    var repeat = batch.Questions[0].Source;
    try
    {
        await QuizWorksheetBuilder.CreateAsync(2, "", "", AppLanguage.Vietnamese, CurriculumTier.OneStar, ArithmeticQuizMode.Essay, _ => Task.FromResult(repeat));
        throw new Exception("Repeated questions silently filled the set.");
    }
    catch (InvalidOperationException error) when (error.Message == "WorksheetNotEnoughDistinctQuestions") { }
    var visualItems = new List<QuizPrintItem>();
    int subtypeCases = 0;
    foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var kind in Enum.GetValues<QuizProblemKind>().Where(ElementaryQuizGenerator.Supports))
            foreach (var type in ElementaryQuizGenerator.Types(kind))
                foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var q = await factory.CreateAsync(new(kind, ElementaryType: type), mode, new(CurriculumTier.FiveStars, false), language,
                        new(QuestionKnowledgeGroup.Objects), PracticeQuestionFormat.WordProblem, false);
                    var item = QuizWorksheetBuilder.Format(q, language); subtypeCases++;
                    if (mode == ArithmeticQuizMode.Essay && item.Visual is not null) visualItems.Add(item);
                    if (mode == ArithmeticQuizMode.TrueFalse && q.ElementaryProblem!.IsComparison)
                        Check(item.Prompt.Contains(q.ElementaryProblem.FormatComparison(q.ElementaryProblem.PresentedText!)), "Comparison assertion lost its operands.");
                }
    for (int start = 0; start < visualItems.Count; start += 40)
    {
        var chunk = visualItems.Skip(start).Take(40).ToArray();
        if (chunk.Length == 1) chunk = [chunk[0], chunk[0]];
        var visuals = batch with { Title = "Hình minh họa — dữ kiện cần dùng", Questions = Array.AsReadOnly(chunk) };
        File.WriteAllBytes(Path.Combine(directory, $"visuals-{start / 40 + 1}.pdf"), QuizPdfWriter.Write(visuals, false));
    }
    File.WriteAllText(Path.Combine(directory, "visuals-index.json"), JsonSerializer.Serialize(visualItems.Select((item, i) => new
    { File = $"visuals-{i / 40 + 1}.pdf", Question = i % 40 + 1, Kind = item.Visual!.Kind, Type = item.Source.ElementaryProblem!.Type.ToString() })));
    // Stacked fraction, deep division and line-wrapping fixture.
    var fractionQuestion = batch.Questions[0] with { Prompt = "Đổi thời gian: 17/4 giờ; tính (2/3 + 5/6) ÷ 7/9.\nCó 3 1/2 lít nước trong bình.", Solution = "(2/3 + 5/6) ÷ 7/9 = 27/14" };
    var fractionSet = batch with { Questions = Array.AsReadOnly(new[] { fractionQuestion, fractionQuestion with { Prompt = "(1000 + 2000) / 2 = ?" } }) };
    File.WriteAllBytes(Path.Combine(directory, "textbook-fractions.pdf"), QuizPdfWriter.Write(fractionSet, false));
    File.WriteAllText(Path.Combine(directory, "cases.json"), JsonSerializer.Serialize(traces, new JsonSerializerOptions { WriteIndented = true }));
    await CalculationSolutionTests.Run(factory, directory);
    await PracticeNumberRangesTests.Run(directory);
    await ExpandedArithmeticBankTests.Run(directory);
    await DecimalOpenWorksheetTests.Run(directory);
    await SameDayTimeWorksheetTests.Run(directory);
    await OvernightTimeWorksheetTests.Run(directory);
    await SameMonthCalendarWorksheetTests.Run(directory);
    await SameMonthCalendarWorksheetTests.Run(directory,true);
    await SameMonthCalendarWorksheetTests.Run(directory,adjacent:true);
    await SameMonthCalendarWorksheetTests.Run(directory,inclusive:true,adjacent:true);
    await SameMonthCalendarWorksheetTests.Run(directory,inclusive:true,adjacent:true,february:true);
    await SameMonthCalendarWorksheetTests.Run(directory,inclusive:true,adjacent:true,leap:true);
    await SameDayBreakWorksheetTests.Run(directory);
    await FractionPartOpenWorksheetTests.Run(directory);
    await FractionRemainingOpenWorksheetTests.Run(directory);
    await FractionWholeOpenWorksheetTests.Run(directory);
    await AverageOpenWorksheetTests.Run(directory);
    await AverageMissingWorksheetTests.Run(directory);
    await UnitaryTotalWorksheetTests.Run(directory);
    await UnitaryGroupsWorksheetTests.Run(directory);
    await MotionOpenWorksheetTests.Run(directory);
    await MotionExtendedWorksheetTests.Run(directory);
    await DifferenceRatioOpenWorksheetTests.Run(directory);
    await SumRatioOpenWorksheetTests.Run(directory);
    await SumDifferenceOpenWorksheetTests.Run(directory);
    await PercentagePartOpenWorksheetTests.Run(directory);
    await PercentageRemainingOpenWorksheetTests.Run(directory);
    await PercentageWholeOpenWorksheetTests.Run(directory);
    await FactSummaryTests.Run(factory, directory);
    await WorksheetSettingsTests.Run(factory);
    await MeasurementPracticeTests.Run(directory);
    await MixedKnowledgeGroupsTests.Run(directory);
    FractionOperandLayoutTests.Run(directory);
    await TimeFractionWorksheetTests.Run(factory, directory);
    await MixedTimeWorksheetTests.Run(factory, directory);
    await DecimalSpatialWorksheetTests.Run(factory, directory);
    await AppliedNarrativeWorksheetTests.Run(directory);
    Console.WriteLine($"PASS {cases} family/star/language/mode cases + {subtypeCases} elementary subtype/language/mode cases; {visualItems.Count} essential drawings. A4 PDF parsing, choices, masked data, vector diagrams, embedded fonts, frozen 20-question batch, count limits, bounded deduplication and cancellation. Output: {directory}");
    return 0;

}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    return 1;
}
