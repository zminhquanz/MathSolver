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

The numeric-expression group checks all five star levels, integer/fraction
expressions with and without brackets, and all three answer modes. It verifies
the exact 3–7 operand / 1–5 bracket-pair rules, compares answers with the solve
tab's independent postfix engine, checks equivalent fractions and multiline
equalities, rejects invalid brackets and unrelated calculations, and checks
expression selection in both source catalogs and their shared mixed pool.

The AI-expression group checks 960 bilingual contracts across every star,
subtype and answer mode. It decodes verbal plans independently, verifies exact
answers, covers all eight contexts and all Mixed subtypes, and exercises the
production JSON parser, prompt, model-output validator and essay grading.
Changed operations, missing plans, extra numbers, duplicate plans, physical
answer units and answer disclosure in the solution lead are rejected. Empty
JSON answer units are allowed only when expression parsing explicitly opts in.

The geometry-selection group checks every supported shape/measurement across
all star levels, languages and answer modes, including lateral versus total
surface area, squared/cubed units, mixed selections and AI contract validation.

The mixed-proportion group checks that both sources sample direct and inverse
at every skill tier, retain concrete contracts for grading and AI prompts, and
honor fixed selections after mixed mode.
