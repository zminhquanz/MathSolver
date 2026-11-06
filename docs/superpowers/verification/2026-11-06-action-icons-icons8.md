# Icons8 action icons: preparation and Windows size verification

Date: 2026-11-06. Branch: `feat/action-icons-icons8-20261106`.

## Asset selection

Icons8 MCP searches succeeded with `platform=fluent-systems-regular`, static icons only. Selected results were previewed together at 20, 24 and 48 pixels in `artifacts/action-icons-icons8-20261106/candidates.png`; IDs and common names are in the root `icons8.json`. The review metaphor is a checked document rather than the unrelated discussion/popularity symbols returned by the initial “review” search.

SVG download for Copy (`pNYOTp5DinZ3`) was refused because the account's plan does not include MCP API access. No further SVG downloads were attempted. The user explicitly chose to keep SVG and will provide access before continuation. No paid access was purchased, no alternate download route used, and no Icons8 artwork has been shipped/integrated.

## Native Windows regression

Production file: `MathSolver/Platforms/Windows/NavigationFontImageSourceService.cs`.

The original renderer ignored `FontImageSource.Size` and always used the math-navigation constant 24. Existing matrix utility sources request 20, so their actual Win2D canvas was too large.

Opt-in probe: `UnitTest/NativeUi/ActionIconSizeProbe.{targets,cs}`. It is injected with `CustomAfterMicrosoftCommonTargets`; normal production builds do not include it. It renders real `CanvasImageSource` objects and checks logical dimensions, DPI and resolution dependence.

RED before the production change:

```text
FAIL Expected logical 20x20 at 100% DPI; actual: 24,24, 96 DPI
```

GREEN after honoring the requested positive finite source size:

```text
PASS 20x20, 100% DPI
PASS 20x20, 150% DPI
PASS 20x20, 200% DPI
PASS 20x20, 300% DPI
PASS 24x24, 100% DPI
PASS 24x24, 150% DPI
PASS 24x24, 200% DPI
PASS 24x24, 300% DPI
PASS ALL ACTION ICON SIZE CHECKS
```

Both pre-fix and post-fix Windows probe builds succeeded with **0 warnings and 0 errors**. Each probe ran and closed its own process. Logs are under `artifacts/action-icons-icons8-20261106/`.

Build command:

```powershell
dotnet build MathSolver/MathSolver.csproj -c Debug -f net10.0-windows10.0.19041.0 --no-restore -p:OutputPath=C:/Users/Quan/source/repos/MathSolver/artifacts/action-icons-icons8-20261106/windows/ -p:CustomAfterMicrosoftCommonTargets=C:/Users/Quan/source/repos/MathSolver/UnitTest/NativeUi/ActionIconSizeProbe.targets -nr:false -v:quiet
```

## Remaining verification

This does **not** verify Icons8 artwork on real buttons: that integration remains pending SVG entitlement. The shared action catalog, page/menu/header updates, light/dark/disabled visual checks and Android emulator checks from the implementation plan are still outstanding. No Android build/device check was claimed for this preparation pass.
