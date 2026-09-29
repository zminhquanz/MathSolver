using MathSolver.Tests;

var tests = new (string Name, Action Run)[]
{
    ("Catalog", PuzzleTests.CheckCatalog),
    ("Algorithm question matrix", PuzzleTests.CheckAlgorithmMatrix),
    ("Essay solution requirements", PuzzleTests.CheckEssaySolutionRequirements),
    ("Essay calculation units", PuzzleTests.CheckEssayCalculationUnits),
    ("Flexible essay calculations", PuzzleTests.CheckFlexibleEssayCalculations),
    ("AI prompt matrix", PuzzleTests.CheckPromptMatrix),
    ("AI JSON parser", PuzzleTests.CheckParser),
    ("AI contract validator", PuzzleTests.CheckValidators)
};

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
