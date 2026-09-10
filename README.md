# Math Solver

<p align="center">
  <img src="MathSolver/Resources/AppIcon/appicon.png"
       alt="Math Solver App Icon"
       width="150">
</p>

<p align="center">
  <a href="https://ko-fi.com/quanvu96">
    <img src="https://img.shields.io/badge/Support_on-Ko--fi-FF5E5B?logo=ko-fi&logoColor=white"
         alt="Support Math Solver on Ko-fi">
  </a>
  <a href="LICENSE">
    <img src="https://img.shields.io/badge/License-GPL_v3-blue.svg"
         alt="GNU GPL v3 License">
  </a>
</p>

Math Solver is an offline-first mathematics learning and problem-solving application built with .NET MAUI. It provides step-by-step calculations, reusable formula references, responsive layouts, and high-precision numeric processing for students and anyone who wants to review essential mathematics.

## Current Release

The current stable release is **Math Solver v0.1.1**.

The current public builds are available for Windows x64.

Android, macOS, and iOS releases are planned but are not available yet.

Download the latest version from the
[GitHub Releases](../../releases/latest) page.

## Features

### Solve Math

The **Solve Math** tab contains dedicated tools for:

- Basic arithmetic: addition, subtraction, multiplication, and division
- Integer and decimal calculations
- Long-division presentation
- Fraction addition, subtraction, multiplication, division, simplification, and common denominators
- Finding an unknown value in arithmetic equations
- Quadratic equations and parabola graphs
- Plane and solid geometry calculations

### Geometry Calculator

The geometry calculator reuses a shared `GeometryFormulaItem` catalog so that formulas, diagrams, symbols, and shape metadata remain consistent across the **Solve Math** and **Formulas** tabs.

Supported plane shapes include:

- Square
- Rectangle
- Triangle
- Right triangle
- Equilateral triangle
- Circle
- Trapezoid
- Isosceles trapezoid
- Right trapezoid
- Rhombus
- Parallelogram

Supported solid shapes include:

- Cube
- Rectangular prism
- Sphere
- Cylinder
- Cone

The calculator can determine values such as:

- Perimeter
- Area
- Base area
- Lateral surface area
- Total surface area
- Volume

### Quadratic Equations

Internal calculations use a custom Double-Double numeric structure for approximately 32 significant digits of precision. This improves the calculation of:

- Discriminant
- Square root of the discriminant
- Real roots
- Parabola vertex
- Parabola sampling points

### Formula Reference

The **Formulas** tab includes:

- Rules for finding unknown components in addition, subtraction, multiplication, and division
- Detailed examples and verification steps
- Plane geometry formulas
- Solid geometry formulas
- Reusable diagrams and symbol descriptions

### Multiplication Tables

The **Multiplication Tables** tab provides:

- Multiplication tables from 1 to 20
- Division tables
- Responsive layouts for desktop and mobile screens

## User Interface

Math Solver includes:

- Responsive layouts for desktop, laptop, tablet, and phone screens
- Light and dark themes
- Custom accent colors
- Font customization
- Vietnamese and English localization
- Animated tab transitions
- Reusable vector and `GraphicsView` illustrations
- Adaptive card layouts based on the available screen width

## Offline Operation

The main calculation features work entirely offline. No internet connection or cloud-based AI service is required for standard arithmetic, fractions, equations, multiplication tables, formulas, or geometry calculations.

## Technology

- C#
- .NET MAUI
- XAML

## Project Structure

```text
MathSolver/
├── Controls/             Custom reusable controls
├── Graphics/             Shape, graph, and calculation drawables
├── MarkupExtensions/     Markup Extensions
├── Models/               Shared models and formula catalogs
├── Numerics/             High-precision numeric structures
├── Platforms/            Platforms support
├── Resources/            Images, icons, fonts, styles, and app resources
├── Services/             Localization, settings, and application services
├── Views/                Pages and reusable content views
├── App.xaml
├── App.xaml.cs
├── AppShell.xaml
├── AppShell.xaml.cs
└── MauiProgram.cs
```

## Getting Started

### Requirements

Install the .NET SDK and .NET MAUI workload required by the project. For Windows development, use Visual Studio with the .NET MAUI development tools installed. Android development also requires the Android SDK and an emulator or physical device.

### Clone the Repository

```bash
git clone <your-repository-url>
cd MathSolver
```

### Restore Dependencies

```bash
dotnet restore
```

### Build

```bash
dotnet build
```

You can also open the solution in Visual Studio, select **Windows Machine** or an Android target, and run the application.

## Translating Math Solver

The translation workflow is **export a CSV → translate the spreadsheet → validate and generate a JSON language pack → import it into the app**. Translators only need a spreadsheet editor. Developers run the Python tool and manage the language packs.

The app matches translations using stable keys such as `Formula.Tabs.UnknownComponent`. Translators do not need to understand these keys or the app's code: they read the original text and its context, then enter a translation. The tool preserves the mapping automatically, even if spreadsheet rows are reordered.

### Files and Requirements

| File | Purpose |
| --- | --- |
| [Blank translation worksheet](translations/translation-template.csv) | Start a new language; the translation column is empty |
| [English worksheet](translations/en-US.csv) | Review existing English translations or use them as a reference |
| [Localization tool](tools/localization.py) | Export, refresh, validate, and import worksheets |
| [Localization catalog](MathSolver/Resources/Raw/Localization/catalog.json) | Shared context, descriptions, and placeholder explanations |
| [Language pack reference](MathSolver/TRANSLATING.md) | JSON format, mathematical content rules, and submission details |

Developers need **Python 3.9 or newer**. No additional Python packages or .NET build are needed to run the tool. Translators receiving a prepared CSV do not need Python.

Run all commands below from the **repository root**, which contains this README and the `tools` directory. For example, in PowerShell:

```powershell
cd D:\GitHub\MathSolver
python --version
```

Replace the path with your own checkout location. If Windows recognizes `py` instead of `python`, use `py -3` in place of `python` in the commands below and confirm it selects Python 3.9 or newer.

The current source language is Vietnamese, as defined by `sourceCulture` in the catalog. Many context notes are also in Vietnamese. Translators should understand the source language or ask the developer for clarification; the tool does not automatically translate text or notes.

### 1. Prepare a Worksheet

For a **new language**, export a blank worksheet. This example uses French:

```powershell
python tools/localization.py export --output translations/fr-FR.csv
```

This creates a CSV containing the current source strings, context, and empty translation cells. The filename does not select a language inside the app; you specify the language metadata when generating the JSON in step 3.

You can also copy the prepared [blank worksheet](translations/translation-template.csv) and rename the copy. Regenerate it after source text or catalog notes change:

```powershell
python tools/localization.py export --output translations/translation-template.csv
```

To **edit an existing language**, seed the worksheet from its JSON pack:

```powershell
python tools/localization.py export --pack MathSolver/Resources/Raw/Localization/en-US.json --output translations/en-US.csv
```

Use `--pack` when starting from an existing JSON. If a translator already has a worksheet in progress, use `--previous` as described in step 5 to preserve their work. Do not combine these options. Export commands replace their destination file, so choose a new filename when you need to retain an earlier version.

### 2. Translate the Spreadsheet

1. Open Excel and choose **Data → From Text/CSV**. Select the worksheet, use **UTF-8** encoding and a **comma** delimiter, and set the columns to **Text** before loading. If Excel offers **Transform Data**, use it to remove automatic type conversion and set all columns to Text. This preserves mathematical expressions, numbers, and identifiers.
2. Read the `source`, `context`, `description`, `notes`, and `placeholders` columns.
3. Enter your translation in `translation`. After checking it, set `status` to `translated`.
4. Keep the other columns unchanged. You may reorder complete rows, but do not rename keys, change headers, reorder columns, or sort a single column independently.
5. Save as **CSV UTF-8 (comma delimited)**. If you keep an Excel `.xlsx` working copy, export a CSV before sending it back; the tool reads CSV files only.
6. Send the CSV to the developer with the language name and translator's name. Translators do not need to edit JSON or run terminal commands.

| Column | How to use it |
| --- | --- |
| `context` | Screen, topic, or location where the text appears |
| `source` | Original Vietnamese text; do not edit |
| `description` | Explanation of what the text means |
| `notes` | Translation instructions, such as keeping a tab label short |
| `placeholders` | Variables that must be preserved, with explanations where available |
| `screenshot` | Optional image path or URL supplied by the developer |
| `references` | Source-code locations for developers; translators can ignore these |
| `translation` | The text you translate into the target language |
| `status` | `untranslated`, `translated`, or `needs-review` |
| `previous_source` | Earlier wording for a source string that changed |
| `section`, `key`, `source_hash` | Internal mapping and change-detection fields; do not edit |

For example, `Formula.Tabs.UnknownComponent` has the source text **“𝑥  Tìm thành phần chưa biết”**. Its context explains that this is a tab on the Formulas page for finding unknown numbers in arithmetic. An English label could be **“𝑥  Find the unknown”**. Preserve the mathematical symbol and keep the label concise.

Preserve complete placeholders such as `{0}`, `{value}`, and `{field|translate}`, including any format specifiers. You may move them within a sentence to match the target language's grammar. Ask the developer about unexplained placeholders instead of guessing their meaning.

Some CSV cells have a leading apostrophe (`'`) to prevent spreadsheet formula evaluation. Preserve this escape prefix in the saved CSV; the importer removes one leading escape apostrophe. For new text starting with `=`, `+`, `-`, `@`, or a literal apostrophe, add an extra apostrophe in the saved CSV. For example, the desired text `= 12` is stored as `'= 12`. Excel may hide an apostrophe used as a text prefix, so check the saved CSV in a text editor if unsure.

The note **“Cần bổ sung ngữ cảnh”** means **“More context is needed.”** Ask the developer for a description or screenshot before translating those entries. Group descriptions are broad guidance, and source references are best-effort. Screenshots are not currently bundled with the worksheets.

### 3. Validate and Generate a Language Pack

After receiving the completed French worksheet, run:

```powershell
python tools/localization.py import --input translations/fr-FR.csv --culture fr-FR --language-name French --native-name Français --author "Translator name" --output translations/fr-FR.json
```

| Option | Meaning |
| --- | --- |
| `--input` | The translated CSV file |
| `--culture` | Target culture code, such as `fr-FR`, `en-US`, or `ja-JP` |
| `--language-name` | Language name in English, such as `French` |
| `--native-name` | Language name as written in that language, such as `Français` |
| `--author` | Optional translator credit; quote names containing spaces |
| `--output` | Destination JSON file; replaced if it already exists and validation succeeds |

By default, every expected row must have a non-empty translation. The tool checks keys, source changes, pending reviews, and placeholders before opening the output file. If validation fails, fix the reported rows and run the command again. An existing output file is left unchanged on validation failure.

For a **partially translated** pack, add `--allow-partial`:

```powershell
python tools/localization.py import --input translations/fr-FR.csv --culture fr-FR --language-name French --native-name Français --author "Translator name" --allow-partial --output translations/fr-FR.json
```

Missing rows and empty translations are omitted from the JSON, allowing the app to fall back to Vietnamese. This option does not bypass duplicate or unknown keys, source changes, pending reviews, or placeholder errors. A blank translation is never considered complete, even if its status says `translated`.

For an edited English worksheet, use the corresponding language metadata:

```powershell
python tools/localization.py import --input translations/en-US.csv --culture en-US --language-name English --native-name English --author "Translator name" --output translations/en-US.json
```

### 4. Check the Language in the App

Use the app's language-pack import feature to select the generated JSON, then select that language. CSV import in the Python tool creates a file; it does not install the language into the running app.

The Python tool checks culture-code syntax. The app additionally validates it with .NET `CultureInfo`. Check the translated screens for clipped labels, readability, correct mathematical terminology, and messages containing placeholders. These visual and language checks require review in the app.

To bundle an approved language with the application, place its JSON under `MathSolver/Resources/Raw/Localization` and add the language to `manifest.json`. Local imports do not require a manifest change. See the [language pack reference](MathSolver/TRANSLATING.md) for submission details.

### 5. Update an In-Progress Translation After Source Changes

When the app's source wording or translation notes change, refresh the translator's latest CSV:

```powershell
python tools/localization.py export --previous translations/fr-FR.csv --output translations/fr-FR-updated.csv
```

The tool matches rows by `(section, key)`, preserves existing translations, and adds newly introduced strings with empty translations. Changed source text receives the status `needs-review`; its earlier wording appears in `previous_source`. Repeated refreshes retain pending reviews.

The translator should compare the new `source` with `previous_source`, revise or confirm the translation, and set `status` to `translated`. Do not edit `source` or `source_hash` to dismiss a review. Then generate the JSON from the updated worksheet:

```powershell
python tools/localization.py import --input translations/fr-FR-updated.csv --culture fr-FR --language-name French --native-name Français --author "Translator name" --output translations/fr-FR.json
```

For the next update, pass the latest reviewed worksheet to `--previous`. Existing JSON packs do not record source revisions, so exporting again with `--pack` cannot replace this review workflow.

If refresh reports removed or unknown keys, archive the old worksheet, confirm those keys were removed from the app, remove those rows from a working copy, and refresh again. The tool stops rather than silently dropping those translations.

### Troubleshooting

| Message or symptom | What to do |
| --- | --- |
| `python` is not recognized | Check your Python installation or try `py -3 --version` on Windows |
| Cannot open `tools/localization.py` | Run the command from the repository root |
| CSV headers were changed / invalid CSV row | Preserve all original columns in their original order; save UTF-8 CSV with commas, not semicolons |
| Duplicate CSV key / unknown key | Restore the original key and section, and remove accidental duplicate rows |
| Missing rows / empty translation | Complete the worksheet, or deliberately use `--allow-partial` |
| Source changed | Refresh with `export --previous`, then review the affected rows |
| Review required | Check the new source text, update the translation, and set its status to `translated` |
| Placeholder mismatch | Restore the exact source placeholders, including modifiers and format specifiers |
| Invalid status | Use exactly `untranslated`, `translated`, or `needs-review`; do not translate these labels |
| Garbled accents or altered numbers | Re-import the original CSV as UTF-8 with every column set to Text |

Show available command options:

```powershell
python tools/localization.py --help
python tools/localization.py export --help
python tools/localization.py import --help
```

Developers can run the workflow checks with:

```powershell
python -m unittest discover -s tools -p "test_localization.py"
```

For instructions on adding per-string context, placeholder explanations, and screenshot references, see [Developer metadata guidance](MathSolver/TRANSLATING.md#developer-export-refresh-validate-and-import).

## Cleaning Build Artifacts

After replacing resources, XAML files, icons, or generated assets, remove the old build output before rebuilding:

```bash
dotnet clean
```

You may also delete the `bin` and `obj` folders, then rebuild the solution.

## Design Goals

Math Solver is developed with the following goals:

- Keep core mathematics features available offline
- Present calculations clearly instead of showing only final answers
- Preserve user input accurately
- Use appropriate numeric types for each calculation
- Share formulas and diagrams between learning and solving tools
- Maintain a clean and responsive interface across different screen sizes
- Avoid unnecessary subscriptions, advertisements, and online dependencies

## Educational Notice

Math Solver is intended to support learning, checking results, and understanding calculation steps. It should not replace independent practice or the guidance of a teacher.

## Support the Project

Math Solver is free to use, and all core features remain available without payment. Donations are completely optional and do not unlock additional features, subscriptions, or extra software rights.

If Math Solver is useful to you and you would like to support its continued development, you can leave a one-time tip on Ko-fi:

<p align="center">
  <a href="https://ko-fi.com/quanvu96">
    <img src="https://img.shields.io/badge/Support_Math_Solver_on-Ko--fi-FF5E5B?logo=ko-fi&logoColor=white"
         alt="Support Math Solver on Ko-fi">
  </a>
</p>

Thank you for supporting the development, testing, documentation, and continued improvement of Math Solver.

## License

The Math Solver source code is free software licensed under the
[GNU General Public License version 3](LICENSE)

You may use, study, modify, and redistribute the GPL-covered source code.
If you distribute a modified version or a compiled binary based on Math Solver,
you must also make the complete corresponding source code available under the
same GNU GPL v3 license.

Copyright © 2026 Quan Vu.

The **Math Solver** name, application icon, logo, screenshots, and original
branding assets are not licensed for independent reuse or for branding modified
distributions. Forks and modified distributions must use their own name and
branding unless separate written permission is granted by the project author.
This branding restriction does not limit the rights granted for the
GPL-covered source code.

## Status

The application is under active development. Additional formulas, geometry problems, calculation explanations, platform improvements, and interface refinements may be added over time.
