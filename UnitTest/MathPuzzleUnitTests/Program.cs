using MathSolver.Tests;

var tests = new (string Name, Action Run)[]
{
    ("Recoverable result storage", PuzzleTests.CheckRecoverableResultStorage),
    ("Catalog", PuzzleTests.CheckCatalog),
    ("Integer and fraction comparisons", PuzzleTests.CheckComparisons),
    ("Quiz diagrams and hidden answers", PuzzleTests.CheckQuizDiagrams),
    ("Elementary contracts and grading matrix", PuzzleTests.CheckElementaryContracts),
    ("Elementary reasoning across five stars", PuzzleTests.CheckElementaryDifficulty),
    ("Textbook fraction presentation", PuzzleTests.CheckFractionPresentation),
    ("Visual geometry variety", PuzzleTests.CheckVisualGeometryVariety),
    ("Side counting, missing sides and composite areas", PuzzleTests.CheckShapeProblemVariety),
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
    ("Detailed essay feedback", PuzzleTests.CheckEssayFeedback),
    ("Combined essay input", PuzzleTests.CheckEssayCombinedInput),
    ("Indirect average solution steps", PuzzleTests.CheckAverageIndirectSteps),
    ("Word problem solutions and formatting", PuzzleTests.CheckWordProblemFormatting),
    ("Flexible essay calculations", PuzzleTests.CheckFlexibleEssayCalculations),
    ("Motion realism", PuzzleTests.CheckMotionRealism),
    ("Structural difficulty across five stars", PuzzleTests.CheckStructuralDifficulty)
};

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
