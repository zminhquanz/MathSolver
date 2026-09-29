# Math puzzle unit checks

Run from the repository root:

```powershell
dotnet run --project tests/MathPuzzleUnitTests/MathPuzzleUnitTests.csproj -c Release
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
