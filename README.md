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

Math Solver is a mathematics learning and problem-solving application built with .NET MAUI. It combines worked calculations, practice questions, formula references, and hardware tools in a responsive interface. Core mathematics features work offline.

## Current Release

The current release is **Math Solver v0.2.0** for **Android** and **Windows**. Download the available builds from [GitHub Releases](https://github.com/zminhquanz/MathSolver/releases/latest).

| Platform | v0.2.0 build and scope |
| --- | --- |
| Android | ARM64 APK; the project sets Android 7.0 (API 24) as its minimum version. Includes the core calculators, quizzes, formula references, tables, settings, and hardware benchmarks. |
| Windows | Windows x64 build; the project sets Windows 10 version 1809 (build 17763) as its minimum version. Includes the core features and CPU arithmetic benchmarks. |

The project also contains iOS and Mac Catalyst targets, but this release announcement covers Android and Windows only.

## Features

### Solve Math

The **Solve Math** tab provides seven tools:

| Tool | What it does |
| --- | --- |
| Basic arithmetic | Adds, subtracts, multiplies, and divides integers or decimals; evaluates expressions with operator precedence and brackets; shows calculation steps and long division. |
| Average | Calculates the arithmetic mean of a list of integers or decimals and explains the sum and division. |
| Powers and roots | Calculates integer powers with large-number strategies, previews long results, and exports large results to TXT; calculates roots, including supported complex results for negative radicands. |
| Fractions | Adds, subtracts, multiplies, divides, simplifies, and finds common denominators with worked steps. |
| Find x | Solves for an unknown component of addition, subtraction, multiplication, or division. |
| Equations | Solves linear and quadratic equations, explains the result, and draws the corresponding line or parabola. |
| Geometry | Calculates measurements for plane and solid shapes with formulas and diagrams. |

Results can be copied to the clipboard. Settings include an option to show long numeric results in full instead of the default compact display.

The powers and roots tool accepts these input ranges:

- Powers: an integer base from -1,000,000,000,000,000,000 to 1,000,000,000,000,000,000 and an exponent from 0 to 100,000,000. `0^0` is undefined.
- Roots: a radicand that fits in a signed 128-bit integer and a root degree from -2,147,483,648 to 2,147,483,647, excluding zero. A negative degree gives the reciprocal of the corresponding positive-degree root. A negative radicand with an even degree can produce a complex result.

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

### Equations and Numeric Precision

The equation tool supports both linear and quadratic modes. Quadratic calculations use a custom Double-Double numeric structure for approximately 32 significant digits of precision. This improves the calculation of:

- Discriminant
- Square root of the discriminant
- Real roots
- Parabola vertex
- Parabola sampling points

The codebase also uses higher-precision and big-integer types where appropriate for other calculations. Precision and supported input ranges depend on the tool.

### Math Puzzles

The **Math Puzzles** tab generates practice questions for basic arithmetic, fractions, geometry, finding x, direct or inverse proportion, motion, averages, and percentages. Choose a specific type or a mixed set, then choose a difficulty from one to five stars. Answer using true/false, multiple choice, or a written response; the app checks the answer and shows a solution.

Questions are generated offline by C# on both Android and Windows. The practice screen has no AI/LLM source selector, model management, inference progress, or AI diagnostics. Creating, regenerating, and advancing questions uses the existing algorithm generators, curriculum and graders without loading a model.

The previous AI implementation, model management, AI prompt/response validation and hardware AI benchmark have been removed. SQLite question storage and a new background AI implementation are not connected yet; the current screen serves C# questions only.

Probability practice includes ten contexts for classifying events and ten for experimental fractions, including colored objects, dice, numbered cards, spinners, coins, and target games. Each subtype cycles through shuffled contexts before repeating, with separate progress for each language. Questions vary the event, counts, colors, and wording. Classification distinguishes certain, impossible, and possible but not certain; experimental fractions use recorded trials and may ask about the event or its complement, including zero/all observed successes. The algorithm generates these scenarios and answers in C#.

Visual geometry practice rotates through ten side-counting shapes, nine missing-side scenarios, and eight composite-area layouts, with a separate shuffled rotation for each language and star level. Counting includes convex and concave polygons with varied orientations. Missing sides use areas, perimeters, or known total/partial lengths. Composite areas include adjoining rectangles, a square and rectangle, L/T shapes, a rectangular frame, a board with two openings, and a rectangle joined to a right triangle. The algorithm uses the C# facts, polygon drawings, units, and answers. Unknown dimensions remain marked with a question mark; the diagrams are illustrative rather than a basis for measuring lengths.

Difficulty for **proportion, motion, averages, percentages, and visual geometry** now changes the mathematical structure as well as the numbers. The algorithm uses `QuizDifficultyPolicy`; narration does not own mathematical difficulty. A fixed subtype remains available at all five levels. Selecting Mixed inside a family chooses relations appropriate to the star level, while the existing whole-curriculum Mixed pool still determines which families are eligible.

| Level | Progression within the supported families |
| --- | --- |
| 1 star | Whole-number scale factors, motion with matching units, averages of two supplied quantities, 50%, familiar polygons, and direct side/area formulas. |
| 2 stars | More scale factors, finding speed or time, averages of three quantities, familiar percentages such as 25%, and perimeter-based missing sides. |
| 3 stars | Unit-rate reasoning, relative-motion problems, missing average values, a broader percentage set, rotated polygons, and L/T area decomposition. |
| 4 stars | Rational unit rates, minutes-to-hours conversions, indirect or weighted averages, combining two quantities before a percentage calculation, concave outlines, and frame areas. |
| 5 stars | Quarter-based unit rates, inverse proportions asking for additional quantities or a changed deadline, rest-time deduction plus conversion, combining three quantities before percentages, a side inferred from both perimeter and a partial length, and two-opening or rectangle/triangle areas. |

This is a progression within each family, not a claim that every 1-star subtype has the same absolute difficulty. A deliberately selected advanced subtype retains its essential relations at lower levels. Test scores stay within 1-10 and motion speeds retain their subject-specific realistic ranges; larger numbers alone do not determine difficulty. Answers, units, and flexible grading of equivalent calculations remain owned by C#.

Geometry measurement practice also has five reasoning levels for every supported plane and solid shape. The chosen shape and measurement (perimeter, area, volume, lateral area, or total area) stay selected in Skill Mode.

| Level | Geometry measurement progression |
| --- | --- |
| 1 star | Apply the selected formula to supplied dimensions. |
| 2 stars | Infer a dimension first, for example a side from a perimeter, a radius from a diameter, or a length from two consecutive segments. |
| 3 stars | Calculate intermediate quantities before the final measurement, such as half a perimeter and then a missing length, or combine three consecutive segments. |
| 4 stars | Combine linked dimensions or sum/difference relations before applying the formula. |
| 5 stars | Combine those relations with conversion of a given length into the formula's length unit. |

C# keeps supplied facts separate from solved dimensions. The algorithm owns these facts, inference steps, and exact answers. The separate narration validator accepts equivalent written length-unit names and rejects changed quantities, reversed relations, and wrong answer units. Unknown dimensions stay marked `?` until the solution is revealed. Triangle, trapezoid, and rhombus dimensions are generated with consistent geometric relationships. Circle questions retain the elementary convention of π = 3.14 and dimensions that produce whole-number answers.

Written geometry work may merge, split, or reorder calculations, including fractional intermediate results. Every written equality is checked, along with the final calculation unit and answer unit; a correct final answer does not hide an incorrect earlier equality. The existing whole-curriculum Mixed eligibility rules remain in place.

**Two-number problems, tables/charts, time, unit conversion, probability, and fraction skills** also have five reasoning levels. C# owns the givens, intermediate relations, units, and answers. Increasing stars introduces indirect facts and additional relationships; it does not just increase the size of the numbers.

| Family | 1–2 stars | 3 stars | 4 stars | 5 stars |
| --- | --- | --- | --- | --- |
| Sum–difference, sum–ratio, difference–ratio | Direct relations, then infer a sum/difference from its double. | Recover the required sum/difference from two quantities. | Undo a change; infer a difference after a transfer between both numbers. | Undo two changes and infer either the original difference or a missing ratio component. |
| Tables and bar charts | Read/calculate quantities, then account for a later addition. | Infer a missing category from a total or another category. | Combine chart data with a ratio or multiplicative relation. | Recover a total from two batches and use sum–difference, or combine two category relationships. |
| Pie charts | Read a percentage, then convert it into a quantity. | Infer the missing percentage before calculating the quantity. | Recover the total from the other categories' combined quantity. | Recover the total from two known category counts and percentages, then calculate the missing category. |
| Time | Read clocks, convert intervals, or cross an hour/month boundary. | Combine intervals, deduct rest, cross midnight, or include a full intervening month. | Carry clock minutes into hours, deduct rest, or account for leap February. | Combine active sessions, convert back to hours, calculate a changed clock interval, or deduct paused days from a multi-month leap-year interval. |
| Unit conversion | Direct/reverse conversion; combine mixed units. | Convert before adding quantities. | Convert, add, and subtract. | Convert the remaining quantity back into the larger unit. Area and volume use squared/cubed conversion factors. |
| Likelihood | Classify an event, then its complement. | Consider two independent trials with replacement when applicable. | Consider two outcome cards drawn without replacement. | Update the outcome-card set before drawing without replacement. |
| Experimental fractions | Use recorded results, then their complement. | Aggregate two batches of recorded trials. | Infer one batch's successes from failures. | Aggregate three batches and exclude explicitly invalid failed records. |
| Fraction skills | Familiar fractions, reduction, comparison, mixed numbers, and a supplied common denominator. | Infer a quantity, compare unlike denominators, combine fractions, or find the least common denominator. | Infer fraction components or a remaining quantity; compare a sum of fractions or combine unlike denominators. | Combine successive fractions, infer both numerator and denominator, compare two sums, or combine addition/subtraction before writing a mixed number. |

Hidden table/bar-chart values are marked `?` until grading; unknown bar heights are placeholders rather than clues to their values. Pie-chart percentage labels follow the same rule. Written work can merge, split, or reorder valid calculations and reuse intermediate results. Intermediate units may differ from the final answer unit; arithmetic and units are checked separately. Fraction tasks retain their required final form, including lowest terms, a specified common denominator, or a reduced mixed number.

Angle recognition starts with clear acute/right examples, adds all four angle types and rotated drawings, then uses displayed decimal measures close to 90° at the higher levels. The displayed measure is authoritative; the figure is illustrative. Parallel/perpendicular practice progresses from two-line relationships to searches among three, four, and five lines. Pair questions have at most one valid pair, can include a no-pair answer, and accept reversed line names.

Written responses use one input box for the solution sentence, calculation, and final answer on both platforms. Put calculations on their own lines and finish with `Answer: …` (or `Đáp số: …` in Vietnamese), including the required unit. The app checks each part separately; numeric questions retain their existing exemptions from requiring a solution sentence.

Calculator sub-tabs and the proportion, motion, average, and measurement formula views are created when first opened. Their views then remain available to preserve inputs and results.

Completed power results occupying at least **32 MiB** of managed numeric storage are written to a private temporary TXT file after **60 seconds** away from the power tab, or when the app enters the background. The full file is completed before the in-memory number is released. The preview, solution, and TXT export remain available; later exports stream the retained file without reloading the number. A failed or canceled archive keeps the in-memory result. Clearing the result, starting a new calculation, or closing the app normally removes its temporary file. Temporary files left after an interrupted app session are pruned after seven days when result storage is first used in a new process. These files do not restore a session after the app process exits.

Background cleanup releases live-wallpaper resources, while active calculations and exports finish before their recoverable data is considered for cleanup. Android also handles the system's supported `onTrimMemory` background signals, following the [Android memory lifecycle guidance](https://developer.android.com/topic/performance/memory/manage-app-memory). Garbage collection is requested only after a large number has been released, rather than on every tab switch.

### Formula Reference

The **Formulas** tab includes:

- Rules for finding unknown components in addition, subtraction, multiplication, and division
- Detailed examples and verification steps
- Direct, inverse, and compound proportion, with interactive diagrams
- Motion and average formulas with worked examples
- A measurement reference and converter for length, mass, time, area, volume, capacity, speed, and temperature
- Plane geometry formulas
- Solid geometry formulas
- Reusable diagrams and symbol descriptions

The unit converter groups thousands with commas and uses a dot for the decimal part in its results, regardless of the selected app language (for example, `1,000 m` or `1,000 kg`).

### Multiplication Tables

The **Multiplication Tables** tab provides:

- Multiplication and division tables, selectable in the 1–10 and 11–20 ranges
- Responsive layouts for desktop and mobile screens

### Hardware and Performance

Open **Settings → Hardware information** to inspect device, CPU, memory, runtime, and supported instruction-set information. The page includes benchmarks for Int32, Int64, Float, and Double, with controls for SIMD acceleration and multithreading. Android has ARM/NEON-specific benchmark paths; Windows exposes supported x86 SIMD comparisons.

## User Interface

Math Solver includes:

- Responsive layouts for desktop, laptop, tablet, and phone screens; Android sub-tabs also support swipe navigation
- System, light, and dark themes; Material You dynamic colors are available on Android 12 and later
- Preset or custom accent colors, including HEX and RGB controls
- Font customization
- Vietnamese and English built-in localization
- Optional animated mathematics background or an imported H.264 MP4 live wallpaper
- Animated tab transitions
- Reusable vector and `GraphicsView` illustrations
- Adaptive card layouts based on the available screen width
- Settings for full-length numeric results and developer diagnostics

Imported MP4 wallpaper clips require a compatible H.264 decoder and can be up to 120 seconds long. On Android, the video is limited to 3,686,400 pixels (equivalent to 2560 × 1440).

## Offline Operation

Calculators, algorithm-generated math puzzles, formulas, and multiplication tables work offline. Opening external links and obtaining a release build require a network connection.

## Technology

- C#
- .NET 10 and .NET MAUI
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

Install the .NET 10 SDK and the .NET MAUI workload. For Windows development, use Visual Studio with the .NET MAUI development tools installed. Android development also requires the Android SDK and an emulator or physical device.

### Clone the Repository

```bash
git clone https://github.com/zminhquanz/MathSolver.git
cd MathSolver
```

### Restore Dependencies

```bash
dotnet restore
```

### Build

```bash
dotnet build MathSolver/MathSolver.csproj -f net10.0-android
```

On Windows, build the Windows target with:

```powershell
dotnet build MathSolver/MathSolver.csproj -f net10.0-windows10.0.19041.0
```

You can also open the solution in Visual Studio, select **Windows Machine** or an Android target, and run the application. Release APK builds target Android ARM64 and require a signing setup.

## Translating Math Solver

The translation workflow is **export a CSV → translate the spreadsheet → validate and generate a JSON language pack → integrate the pack into the app**. Translators only need a spreadsheet editor. Developers run the Python tool and manage the language packs.

The app matches translations using stable keys such as `Formula.Tabs.UnknownComponent`. Translators do not need to understand these keys or the app's code: they read the original text and its context, then enter a translation. The tool preserves the mapping automatically, even if spreadsheet rows are reordered.

### Files and Requirements

| File | Purpose |
| --- | --- |
| [Blank translation worksheet](translations/translation-template.csv) | Start a new language; the translation column is empty |
| [English worksheet](translations/en-US.csv) | Review existing English translations or use them as a reference |
| [Localization tool](tools/localization.py) | Export, refresh, validate, and import worksheets |
| [Localization catalog](MathSolver/Resources/Raw/Localization/catalog.json) | Shared context, descriptions, and placeholder explanations |
| [Language pack reference](TRANSLATING.md) | JSON format, mathematical content rules, and submission details |

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

The current language picker exposes the two built-in languages, Vietnamese and English. There is no user-facing JSON import control in v0.2.0. The app has a `LocalizationService.ImportLanguagePackAsync` API for developer integration, while the Python `import` command only creates the JSON file.

The Python tool checks culture-code syntax. The app additionally validates it with .NET `CultureInfo`. Check the translated screens for clipped labels, readability, correct mathematical terminology, and messages containing placeholders. These visual and language checks require review in the app.

To bundle an approved language with the application, place its JSON under `MathSolver/Resources/Raw/Localization`, add it to `manifest.json`, and expose it in the language selection UI. Developer-initiated local imports do not require a manifest change. See the [language pack reference](TRANSLATING.md) for submission details.

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

For instructions on adding per-string context, placeholder explanations, and screenshot references, see [Developer metadata guidance](TRANSLATING.md#developer-export-refresh-validate-and-import).

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
