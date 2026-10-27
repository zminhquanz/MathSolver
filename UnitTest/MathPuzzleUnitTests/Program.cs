using MathSolver.Tests;

var tests = new (string Name, Action Run)[]
{
    ("Readable content contrast and user accents", PuzzleTests.CheckReadableColors),
    ("Display density capabilities preferences and matrix sizes", PuzzleTests.CheckDisplayDensity),
    ("Matrix file-backed streaming import and cleanup", PuzzleTests.CheckMatrixFileStreamingImport),
    ("Matrix parallel storage error classification", PuzzleTests.CheckMatrixParallelStorageErrors),
    ("Matrix input store round trip and bounded eviction", PuzzleTests.CheckMatrixInputStoreRoundTrip),
    ("Matrix input store fill and clone isolation", PuzzleTests.CheckMatrixInputStoreFillAndClone),
    ("Matrix input store storage failure", PuzzleTests.CheckMatrixInputStoreStorageFailure),
    ("Matrix input store bulk and maximum shape", PuzzleTests.CheckMatrixInputStoreBulkAndMaximum),
    ("Matrix input store failed eviction recovery", PuzzleTests.CheckMatrixInputStoreFailedEviction),
    ("Matrix input store reuses cache buffers", PuzzleTests.CheckMatrixInputStoreReusesBuffers),
    ("Matrix input store clone responsiveness", PuzzleTests.CheckMatrixInputStoreCloneResponsiveness),
    ("Matrix file-backed draft editing", PuzzleTests.CheckMatrixFileDraftEditing),
    ("Matrix file-backed transfer ownership", PuzzleTests.CheckMatrixFileTransferOwnership),
    ("Matrix file-backed draft failed import ownership", PuzzleTests.CheckMatrixFileDraftFailedImportOwnership),
    ("Matrix file-backed draft paste", PuzzleTests.CheckMatrixFileDraftPaste),
    ("Matrix file-backed draft import and creation ownership", PuzzleTests.CheckMatrixFileDraftImportAndCreationOwnership),
    ("Matrix file-backed exact kernels and panel packing", PuzzleTests.CheckMatrixFileEngine),
    ("Matrix file-backed uniform calculation and stop", PuzzleTests.CheckMatrixFileUniformCalculation),
    ("Matrix file-backed parallel nonuniform panels", PuzzleTests.CheckMatrixFileParallelPanels),
    ("Matrix recreation releases old inputs result and cache", PuzzleTests.CheckMatrixRecreationRelease),
    ("Matrix recreation collection and nonthrowing stop", PuzzleTests.CheckMatrixRecreationCollectionAndStop),
    ("Matrix disposed dense result releases buffers", PuzzleTests.CheckMatrixDisposedDenseBuffers),
    ("Matrix creation publication keeps requested dimensions", PuzzleTests.CheckMatrixCreationPublication),
    ("Matrix fresh memory headroom and GC snapshot fallback", PuzzleTests.CheckMatrixFreshMemorySnapshots),
    ("Matrix grouped packing exactness and reuse", PuzzleTests.CheckMatrixGroupedPacking),
    ("Matrix paired kernels full-range BigInteger", PuzzleTests.CheckMatrixPairedKernelsMatchBigInteger),
    ("Matrix storage profiling", PuzzleTests.CheckMatrixStorageProfiling),
    ("Matrix storage publication races", PuzzleTests.CheckMatrixStoragePublicationRaces),
    ("Matrix detailed progress terminal and cancellation", PuzzleTests.CheckMatrixDetailedProgress),
    ("Matrix grouped parallel wide storage and bounded progress", PuzzleTests.CheckMatrixParallelGroupedTuning),
    ("Matrix progress observer failure cleanup", PuzzleTests.CheckMatrixProgressObserverFailureCleanup),
    ("Matrix type-to-edit numeric keys and pending focus", PuzzleTests.CheckMatrixTypingKeys),
    ("Matrix type-to-edit selection commit and cancel", PuzzleTests.CheckMatrixTypingInteraction),
    ("Matrix Delete clears single range and all cells", PuzzleTests.CheckMatrixDeleteCells),
    ("Matrix Delete preserves text editing and read-only results", PuzzleTests.CheckMatrixDeleteInteraction),
    ("Matrix stop without cancellation exceptions", PuzzleTests.CheckMatrixStopWithoutExceptions),
    ("Matrix stop preserves prior result and cleans partial files", PuzzleTests.CheckMatrixStopStorage),
    ("Matrix input compact storage and snapshots", PuzzleTests.CheckMatrixInputStorage),
    ("Matrix input memory evidence at desktop ceiling", PuzzleTests.CheckMatrixInputMemoryEvidence),
    ("Matrix paged input engine exact results", PuzzleTests.CheckMatrixPagedEngineExact),
    ("Matrix paged input uniform metadata", PuzzleTests.CheckMatrixPagedEngineUniform),
    ("Matrix paged input cancellation", PuzzleTests.CheckMatrixPagedEngineCancellation),
    ("Matrix streaming import bounded allocation", PuzzleTests.CheckMatrixStreamingImport),
    ("Matrix streaming import bounds and failures", PuzzleTests.CheckMatrixStreamingImportBoundsAndFailures),
    ("Matrix streaming import cancellation", PuzzleTests.CheckMatrixStreamingImportCancellation),
    ("Matrix shared hardware threading preference", PuzzleTests.CheckMatrixThreadingPreference),
    ("Matrix uniform scan exact kernels and tails", PuzzleTests.CheckMatrixUniformScanExact),
    ("Matrix uniform scan parallel cancellation", PuzzleTests.CheckMatrixUniformScanParallel),
    ("Matrix uniform scan integration telemetry", PuzzleTests.CheckMatrixUniformScanIntegration),
    ("Matrix uniform scan performance", PuzzleTests.CheckMatrixUniformScanBenchmark),
    ("Matrix blocked exact products and elementwise edges", PuzzleTests.CheckMatrixBlockedExact),
    ("Matrix blocked workspace budget and worker limits", PuzzleTests.CheckMatrixBlockedBudget),
    ("Matrix blocked uniform exact compact values", PuzzleTests.CheckMatrixBlockedUniform),
    ("Matrix cancellation outcome without exceptions", PuzzleTests.CheckMatrixCancellationOutcome),
    ("Matrix blocked cancellation and result file lifetime", PuzzleTests.CheckMatrixBlockedCancellation),
    ("Matrix block storage exact encodings and bounded cache", PuzzleTests.CheckMatrixBlockStorage),
    ("Matrix block storage concurrent lifetime and cleanup", PuzzleTests.CheckMatrixBlockStorageLifetime),
    ("Matrix platform input limits", PuzzleTests.CheckMatrixPlatformInputLimits),
    ("Matrix large dimension limits", PuzzleTests.CheckMatrixLargeDimensions),
    ("Matrix large virtual viewport", PuzzleTests.CheckMatrixLargeViewport),
    ("Matrix large import and clipboard bounds", PuzzleTests.CheckMatrixLargeImportAndClipboard),
    ("Matrix clipboard oversized selection CSV export", PuzzleTests.CheckMatrixSelectionCsvExport),
    ("Matrix memory allocation budget", PuzzleTests.CheckMatrixMemoryBudget),
    ("Matrix memory calculation preflight without exceptions", PuzzleTests.CheckMatrixCalculationMemoryPreflight),
    ("Matrix parallel exact kernels and cancellation", PuzzleTests.CheckMatrixParallel),
    ("Matrix maximum-length SIMD dot products", PuzzleTests.CheckMatrixLongDot),
    ("Matrix parallel performance samples", PuzzleTests.CheckMatrixParallelBenchmark),
    ("Matrix paste memory preflight without exceptions", PuzzleTests.CheckMatrixPasteMemoryPreflight),
    ("Matrix clipboard single-value fill across selection", PuzzleTests.CheckMatrixPasteFillSelection),
    ("Matrix viewport Shift rectangle selection", PuzzleTests.CheckMatrixRangeSelection),
    ("Matrix viewport touch two-tap range selection", PuzzleTests.CheckMatrixTouchRange),
    ("Matrix clipboard paste cells across drafts", PuzzleTests.CheckMatrixPasteCells),
    ("Matrix viewport select all and clipboard", PuzzleTests.CheckMatrixSelectAll),
    ("Matrix clipboard Excel rows and exact integers", PuzzleTests.CheckMatrixClipboard),
    ("Matrix clipboard busy retries and persistence failure", PuzzleTests.CheckMatrixClipboardPublication),
    ("Matrix viewport select and edit interactions", PuzzleTests.CheckMatrixCellInteraction),
    ("Matrix viewport scrollbars and large table bounds", PuzzleTests.CheckMatrixScrollBars),
    ("Matrix viewport automatic scrollbars", PuzzleTests.CheckMatrixAutomaticScrollBars),
    ("Matrix viewport variable row and column sizes", PuzzleTests.CheckMatrixResizing),
    ("Unit conversion exact decimal factors", PuzzleTests.CheckUnitConversionExactFactors),
    ("Unit conversion ten-place decimal display", PuzzleTests.CheckUnitConversionDecimalDisplay),
    ("Unit conversion ten-place input precision", PuzzleTests.CheckUnitConversionInputPrecision),
    ("Number theory exact GCD LCM and Euclidean steps", PuzzleTests.CheckNumberTheory),
    ("Number theory grade-six prime factor lessons", PuzzleTests.CheckPrimeFactorLessons),
    ("Fraction live preview without formatting exceptions", PuzzleTests.CheckFractionLivePreview),
    ("Shared rational expressions and worked steps", PuzzleTests.CheckRationalExpressions),
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
    ("Responsive solver workspace and compact headers", PuzzleTests.CheckSolverLayoutPolicy),
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

// Longer tuning sweeps are explicit so routine correctness runs stay practical
// on scalar-only machines. Existing test groups keep their default behavior.
(string Name, Action Run)[] tuningBenchmarks =
[
    ("Matrix actual maximum creation", PuzzleTests.CheckMatrixMaximumCreation),
    ("Matrix actual maximum recreation", PuzzleTests.CheckMatrixMaximumRecreation),
    ("Matrix actual maximum file calculation", PuzzleTests.CheckMatrixMaximumFileCalculation),
    ("Matrix tuning performance samples", PuzzleTests.CheckMatrixTuningPerformance),
    ("Matrix policy performance samples", PuzzleTests.CheckMatrixPolicyPerformance)
];

if (args is ["--help"] or ["--list"])
{
    Console.WriteLine("Usage: MathPuzzleUnitTests [test group name filters]. No arguments: all groups.");
    foreach (var test in tests) Console.WriteLine(test.Name);
    foreach (var test in tuningBenchmarks) Console.WriteLine(test.Name + " (explicit filter only)");
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
    tests = tests.Concat(tuningBenchmarks).Where(test => args.Any(filter =>
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
