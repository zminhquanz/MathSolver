# Math puzzle checks

The practice page uses the C# algorithm on Windows and Android. The previous
AI implementation, model management, prompts, response validation, hardware AI
benchmark, live-model harness and captured model fixtures have been removed.

The console harness runs 27 test groups against the actual C# generators,
curriculum rules, exact arithmetic, essay graders and presentation helpers.
It covers every registered problem family, both languages, all five star levels
and all three answer modes. Checks include independent answers, distinct choices,
realistic motion speeds, units, diagrams with hidden values, alternative solution
steps, textbook fractions, combined essay input and recoverable result storage.
Generic word-problem formatting tests remain because C# also generates story text.
Localization checks cover quiz UI keys, bundled Vietnamese/English packs,
runtime fallbacks, placeholders and generated problems, solutions, diagrams
and grading messages. They catch missing strings and mixed-language content.

## Visual Studio

1. Open `MathSolver.slnx` in Visual Studio.
2. In Solution Explorer, right-click `MathPuzzleUnitTests` and select **Set as Startup Project**.
3. Select **Release** (or **Debug** to use breakpoints), then press **Ctrl+F5** to run.
4. Read the console output. Each group prints `PASS` or `FAIL`, followed by a total. A failing group also prints the exception and the source line.

This is a standalone console test harness. Its checks do not appear as individual
tests in Visual Studio's Test Explorer; run the project or use the command below.

## Command line

Run from the repository root:

```powershell
dotnet run --project UnitTest/MathPuzzleUnitTests/MathPuzzleUnitTests.csproj -c Release
```

To run selected groups, pass one or more case-insensitive name fragments:

```powershell
dotnet run --project UnitTest/MathPuzzleUnitTests/MathPuzzleUnitTests.csproj -c Release -- "Motion" "Geometry"
```

A filter that matches no group exits with a nonzero status.

The command returns a nonzero exit code when any check fails. It needs no
AI model, API key or unit-test package. Platform stubs let the shared math and
storage code run outside MAUI. No benchmark is run by this test harness.
