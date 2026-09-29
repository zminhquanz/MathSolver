# Math puzzle checks

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

The command exits with a nonzero status when any check fails. It uses the real
quiz generators, prompts, JSON parser, and AI response validators, linked from
the app source. Small stubs replace only platform services needed to compile
these components outside MAUI. It needs no model download, API key, network
connection, or unit-test package.

The checks cover all supported puzzle subtypes in Vietnamese and English across
true/false, multiple choice, and essay modes. They also check prompts for every
subtype; JSON acceptance and rejection; valid and invalid AI stories for the
geometry, proportion, motion, average, and percentage contracts; and direct AI
validation cases for arithmetic, fractions, and find-x. These checks validate
the app's AI integration contract, not the quality of a live model's writing.
