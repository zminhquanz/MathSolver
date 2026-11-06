# Original action SVGs: implementation and verification

The user replaced the paid Icons8 acquisition requirement with original SVG drawing using Logo Design. Icons8 was used to analyze metaphors/consistency only. No stock SVG, traced preview or paid download is part of this implementation. Work is on `feat/action-icons-icons8-20261106`; it has not been merged/pushed.

## Implementation

- Original source geometry in `tools/branding/generate-navigation-icons.mjs`; 7 appended glyphs (32 total) retain all existing navigation codepoints.
- Common `ActionIconCatalog`, `ActionIcon` native-button adapter and `ActionIconView` heading/overlay adapter.
- Actions use 20 logical units and follow exact text color; navigation/heading decoration uses 24.
- Calculator Calculate/Find x/Find roots/Clear/Stop/Copy/TXT controls, Matrix controls and AI/PDF/Review entries/headings use the shared source.
- Existing educational math symbols and app logo remain unchanged.
- Existing emoji captions are cleaned only for explicitly mapped action keys; unknown content is preserved.
- Original SVG contact sheet rendered and inspected at 20/24 on light/dark: `artifacts/action-icons-original-20261106/original-icons.png`.

## Evidence so far

- Action catalog regression: RED on missing Calculate mapping, GREEN `PASS ALL ACTION CATALOG CHECKS` after implementation.
- PDF console regression: PASS all reported groups, including 630 family/star/language/mode and 630 elementary subtype/language/mode cases.
- Initial Windows production build: PASS, zero warnings/errors.
- Initial Android x64 probe build: PASS, zero warnings/errors.
- Actual Shell probe confirmed icon mappings/sizes/colors on all nine Windows solver pages and AI/Review before an invalid PDF test fixture was corrected (Tier must start at OneStar).
- Color assertions were corrected to compare channel values rather than object identity; no production color-binding bug was established by that false-positive assertion.

## Independent review and remaining checks

Read-only independent review found an Android Matrix dynamic-key bug (Expand/Restore still updated vector keys only on Windows), plus a removed opacity binding on the AI entry overlay. Both were fixed with native regression evidence:

- Android RED: `Stale Matrix expand/restore key: Workspace.ExpandTable, loaded=True/True, expanded=True`. GREEN after updating the main and phone-mirror keys on both platforms. The probe closes setup and explicitly activates the selected tab before interacting; the initial fixture tried a grid blocked by setup and was corrected rather than changing production behavior.
- Windows RED: `AI entry overlay does not follow button opacity`. GREEN after restoring the binding to the actual entry button's Opacity. This restores the original press behavior and was verified on Android as well.

## Final results

- Windows native Shell probe: **PASS ALL ORIGINAL ACTION ICON CHECKS**. All nine solver tabs and Quiz/AI/Review/PDF in both light/Vietnamese and dark/English configurations. Matrix expansion/restoration twice, live caption/key/color replacement, disabled source and icon removal all pass.
- Android API 36 x64 emulator, isolated package: **PASS ALL ORIGINAL ACTION ICON CHECKS** with the same route/state coverage, including the phone mirror's expand/restore key.
- Latest Windows and Android probe builds: **0 warnings, 0 errors**.
- Windows logical 20/24 at 100/150/200/300% density: 8/8 native render checks passed in the preceding renderer-size preparation pass (see `2026-11-06-action-icons-icons8.md`).
- Catalog regression rerun: **PASS ALL ACTION CATALOG CHECKS**.
- PDF regressions: **PASS**, output recorded in `artifacts/action-icons-pdf-tests.log`.
- `git diff --check`: passed.
- Inspected the original SVG size/theme sheet and actual Shell screenshots, including Windows Calculate/Clear and Android dark Matrix/light AI. The educational symbols and original logo remain visible.

Native artifacts: `artifacts/action-icons-original-20261106/windows-probe/` and `android-captures/`. The opt-in `UnitTest/NativeUi/ActionIconsProbe.targets` is not imported by ordinary app builds. It performs no inference, clipboard writes or stored-question mutations. Testing used the emulator, not a physical Android device.

The emulator's original userdata did not have enough installation space. The test uses a separate userdata file in `artifacts/action-icons-original-20261106/`, initialized from the AVD base image, with the isolated package `com.quanvu.mathsolver.actionicons`. The production Android package is not replaced.
