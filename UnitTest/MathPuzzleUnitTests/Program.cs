using MathSolver.Tests;

var tests = new (string Name, Action Run)[]
{
    ("Function graph SIMD linear endpoints and parabola regression", PuzzleTests.CheckFunctionGraphSimd),
    ("NTT kernel execution and AVX-512 exact results", PuzzleTests.CheckNttKernelExecution),
    ("Power diagnostics phase boundaries and stage percentages", PuzzleTests.CheckPowerDiagnostics),
    ("Textbook mental calculations and contextual quantities", PuzzleTests.CheckTextbookPractice),
    ("Practice number ranges and realistic money", PuzzleTests.CheckPracticeNumberRanges),
    ("Nine-digit number sense and exact product boundaries", PuzzleTests.CheckLargeNumberSense),
    ("Recoverable result storage", PuzzleTests.CheckRecoverableResultStorage),
    ("Shutdown exception diagnostics", PuzzleTests.CheckShutdownDiagnostics),
    ("Catalog", PuzzleTests.CheckCatalog),
    ("Illustrated choices and search routing", PuzzleTests.CheckIllustratedChoices),
    ("Primary curriculum mapping", PuzzleTests.CheckPrimaryCurriculum),
    ("Time and different-denominator fraction skills", PuzzleTests.CheckTimeFractionExtensions),
    ("Mixed time arithmetic and keyboard duration input", PuzzleTests.CheckMixedTime),
    ("Decimal and spatial extension skills", PuzzleTests.CheckDecimalSpatialSkills),
    ("Foundation skills and independent visual checks", PuzzleTests.CheckFoundations),
    ("Short spatial position answer input", PuzzleTests.CheckSpatialPositionInput),
    ("Natural number ordering answer separators", PuzzleTests.CheckNaturalNumberOrderSeparators),
    ("Comma separators for multiple essay answers", PuzzleTests.CheckMultipleAnswerCommas),
    ("Quiz localization", PuzzleTests.CheckQuizLocalization),
    ("Question prompt comma punctuation", PuzzleTests.CheckPromptPunctuation),
    ("Responsive quiz layouts and accessible diagrams", PuzzleTests.CheckResponsivePresentation),
    ("Integer and fraction comparisons", PuzzleTests.CheckComparisons),
    ("Quiz diagrams and hidden answers", PuzzleTests.CheckQuizDiagrams),
    ("Elementary contracts and grading matrix", PuzzleTests.CheckElementaryContracts),
    ("Elementary reasoning across five stars", PuzzleTests.CheckElementaryDifficulty),
    ("Shared story contexts and realistic quantities", PuzzleTests.CheckStoryContexts),
    ("Time context rotation when changing stars", PuzzleTests.CheckTimeContextRotation),
    ("Decimal presentation and grading consistency", PuzzleTests.CheckDecimalPresentationAndGrading),
    ("Decimal word problem litre unit aliases", PuzzleTests.CheckDecimalLitreUnits),
    ("Curriculum expansion and Mixed subtype policy", PuzzleTests.CheckCurriculumExpansion),
    ("Textbook fraction presentation", PuzzleTests.CheckFractionPresentation),
    ("Fraction quantity context IDs and exact answers", PuzzleTests.CheckFractionQuantityContexts),
    ("Area measurement foundation and quantity boundaries", PuzzleTests.CheckAreaMeasurementFoundation),
    ("Data chart contracts and hidden data", PuzzleTests.CheckDataChartContracts),
    ("Optional answer units when reading bar charts", PuzzleTests.CheckBarChartAnswerUnits),
    ("Direct pictograph answer input", PuzzleTests.CheckPictographInput),
    ("Direct table and pie chart answer input", PuzzleTests.CheckTablePieInput),
    ("Visual geometry variety", PuzzleTests.CheckVisualGeometryVariety),
    ("Side counting, missing sides and composite areas", PuzzleTests.CheckShapeProblemVariety),
    ("Direct missing side answer input", PuzzleTests.CheckMissingSideInput),
    ("Probability variety and contracts", PuzzleTests.CheckProbabilityVariety),
    ("Flexible multi-step work and two answers", PuzzleTests.CheckElementaryFlexibleWork),
    ("Algorithm question matrix", PuzzleTests.CheckAlgorithmMatrix),
    ("Numeric expressions", PuzzleTests.CheckExpressions),
    ("Geometry measurement selection", PuzzleTests.CheckGeometryMeasurements),
    ("Geometry reasoning across five stars", PuzzleTests.CheckGeometryDifficulty),
    ("Inverse proportion variety", PuzzleTests.CheckInverseProportionVariety),
    ("Mixed proportion selection", PuzzleTests.CheckMixedProportionSelection),
    ("Essay solution requirements", PuzzleTests.CheckEssaySolutionRequirements),
    ("Essay calculation units", PuzzleTests.CheckEssayCalculationUnits),
    ("Essay contextual object units", PuzzleTests.CheckEssayObjectUnits),
    ("Detailed essay feedback", PuzzleTests.CheckEssayFeedback),
    ("Combined essay input", PuzzleTests.CheckEssayCombinedInput),
    ("Colon division across essay grading", PuzzleTests.CheckEssayColonDivision),
    ("Result-only numeric practice submissions", PuzzleTests.CheckResultOnlyPractice),
    ("Keyboard mixed number answer input", PuzzleTests.CheckMixedNumberInput),
    ("Keyboard thermometer Celsius input", PuzzleTests.CheckThermometerInput),
    ("Keyboard protractor degree input", PuzzleTests.CheckProtractorInput),
    ("Direct mixed measurement answer input", PuzzleTests.CheckMixedMeasurementInput),
    ("Direct measurement conversion and map scale input", PuzzleTests.CheckMeasurementResultInput),
    ("Numeric measurement practice", PuzzleTests.CheckMeasurementPractice),
    ("Direct clock hour and minute input", PuzzleTests.CheckDirectClockInput),
    ("Direct elapsed time conversion and calendar input", PuzzleTests.CheckTimeResultInput),
    ("Indirect average solution steps", PuzzleTests.CheckAverageIndirectSteps),
    ("Word problem solutions and formatting", PuzzleTests.CheckWordProblemFormatting),
    ("Flexible essay calculations", PuzzleTests.CheckFlexibleEssayCalculations),
    ("Motion realism", PuzzleTests.CheckMotionRealism),
    ("Structural difficulty across five stars", PuzzleTests.CheckStructuralDifficulty)
};

if (args is ["--help"] or ["--list"])
{
    Console.WriteLine("Usage: MathPuzzleUnitTests [test group name filters]. No arguments: all groups.");
    foreach (var test in tests) Console.WriteLine(test.Name);
    return 0;
}
if (args.Any(a => a.StartsWith("--")))
{
    Console.Error.WriteLine("Unknown option. Use --list or supply test group name filters.");
    return 2;
}
await MathSolver.Testing.HarnessInfrastructureTests.RunAsync();
if (args.Length > 0)
{
    tests = tests.Where(test => args.Any(filter =>
        test.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))).ToArray();
    if (tests.Length == 0)
    {
        Console.Error.WriteLine("No test groups match the supplied name filters.");
        return 1;
    }
}

int failed = 0;
foreach ((string name, Action run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception error)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {name}: {error}");
    }
}

Console.WriteLine($"{tests.Length - failed}/{tests.Length} test groups passed.");
return failed == 0 ? 0 : 1;
