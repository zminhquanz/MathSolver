# Math puzzle checks

Generators read language-specific wording and context lists from the bundled
[quiz JSON content](../../MathSolver/QUIZ_CONTENT_AUTHORING.md). This suite checks
their generated problems and independent answers after content changes. The bank
harness also provides `--quiz-content` and author-file validation commands.

The practice page uses the C# algorithm on Windows and Android. The previous
AI implementation, model management, prompts, response validation, hardware AI
benchmark, live-model harness and captured model fixtures have been removed.
The new optional question-bank enrichment has its own
[checks and model smoke test](../AiQuestionBankTests/README.md); this project
continues to check C# practice and grading.

The console harness runs 38 test groups against the actual C# generators,
curriculum rules, exact arithmetic, essay graders and presentation helpers.
It covers every registered problem family, both languages, all five star levels
and all three answer modes. Checks include independent answers, distinct choices,
realistic motion speeds, units, diagrams with hidden values, alternative solution
steps, textbook fractions, combined essay input and recoverable result storage.
Generic word-problem formatting tests remain because C# also generates story text.
Localization checks cover quiz UI keys, bundled Vietnamese/English packs,
runtime fallbacks, placeholders and generated problems, solutions, diagrams
and grading messages. They catch missing strings and mixed-language content.

Responsive presentation checks cover narrow/wide logical widths and enlarged
text, including tablet two-column choices and diagram reflow. Accessible diagram
checks ensure hidden chart values and worked explanations are not read before
grading. These are layout-policy checks, not screenshots or physical device tests.

`"Data chart"` checks all 27 chart contexts, stable category IDs, pinned fresh
data, serialized profile replay, absolute/directed comparisons, three grading
modes and hidden-data projection. See [the chart contract guide](../../MathSolver/DATA_CHART_FOUNDATION.md).

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

## Primary curriculum and foundation skills

The curriculum check validates 140 grade/objective references, unique routes,
all implemented elementary subtypes, existing calculator quiz routes, and the
three practical activities explicitly marked as planned. Grades describe skill
alignment; they do not constrain every star variant's numbers or reasoning depth.
See [PRIMARY_CURRICULUM_MAP.md](../../MathSolver/PRIMARY_CURRICULUM_MAP.md).

Foundation checks generate 12,960 questions across 27 skills, five stars, both
languages and all three answer modes. They check distinct choices, exact grading,
answer-only input for recognition/reading tasks, rejected wrong answers and units,
independent counting/measurement data, and hidden chart/number-line values.
These checks do not replace visual inspection on Windows and Android devices.

```powershell
dotnet run --project UnitTest/MathPuzzleUnitTests -- "Primary curriculum" "Foundation" "Quiz localization" "Textbook fraction"
```
