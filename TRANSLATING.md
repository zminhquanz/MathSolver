# Translating Math Solver

Math Solver language packs are UTF-8 JSON files. Translators do not need to
edit C# or XAML.

For the complete spreadsheet workflow, including terminal setup, examples, and
troubleshooting, start with [Translating Math Solver in the main README](../README.md#translating-math-solver).

## Recommended: translate in a spreadsheet

Ask the developer for a CSV exported by `tools/localization.py`. The prepared
`translations/translation-template.csv` starts with empty translations. Open it
in Excel using **Data → From Text/CSV**, choose UTF-8, comma delimiter, and set
columns to **Text**. This preserves numbers, mathematical symbols and line breaks.
Save the result as **CSV UTF-8 (comma delimited)** with the original headers.

Each row provides:

| Column | Meaning |
| --- | --- |
| `context` | Screen or topic where the text belongs |
| `source` | Original Vietnamese text; do not edit |
| `description`, `notes` | Meaning and translation instructions |
| `placeholders` | Variables to preserve, with explanations/examples when available |
| `screenshot` | Optional image path/URL supplied by the developer |
| `references` | Source locations for the developer; translators can ignore these |
| `translation` | Your translation; this is the main column to edit |
| `status` | `untranslated`, `translated`, or `needs-review` |
| `previous_source` | Previous wording when the source changed |
| `section`, `key`, `source_hash` | Internal identifiers; do not edit |

You may reorder rows. Never rename keys or delete columns. Fill `translation`
and set `status` to `translated` when ready. A `needs-review` row must be checked
against the new `source` before changing its status to `translated`. Empty values
are never counted as translations, regardless of status.

Some math strings have a leading apostrophe in the CSV to prevent spreadsheet
formula evaluation. Preserve that prefix; the importer removes exactly one
escape prefix. When typing new text beginning with `=`, `+`, `-`, `@`, or a literal
apostrophe, prefix it with an extra apostrophe in the saved CSV. Importing all
columns as Text is recommended. Do not let Excel calculate the content.

For text marked “Cần bổ sung ngữ cảnh”, ask the developer for its meaning or a
screenshot. Group descriptions are general guidance, not verified descriptions
of every control. Source references are best-effort and may omit generated text.
There are no bundled screenshots yet. Do not guess the meaning of an unexplained
placeholder such as `{0}`. Preserve the entire token, including `|translate` and
format specifiers; tokens can be reordered for the target language.

## Developer: export, refresh, validate and import

Run from the repository root with Python 3.9 or newer. No extra packages needed.
Choose a source language the translator understands; currently the canonical
source is Vietnamese (`catalog.sourceCulture`).

Create a blank worksheet (regenerate this when source text or metadata changes):

```powershell
python tools/localization.py export --output translations/translation-template.csv
```

Continue an existing language:

```powershell
python tools/localization.py export --pack MathSolver/Resources/Raw/Localization/en-US.json --output translations/en-US.csv
```

Refresh a translator's CSV after the app's source wording changes:

```powershell
python tools/localization.py export --previous translations/en-US.csv --output translations/en-US-updated.csv
```

Refresh preserves translations by `(section, key)`, adds new untranslated rows,
and marks changed source text as `needs-review`. Repeated refreshes retain pending
reviews. Removed/unknown keys stop refresh with an error: archive the old sheet,
then remove those rows explicitly. Existing JSON packs have no source revision
history, so use `--previous` for subsequent updates, not a fresh `--pack` export.

Validate and create an app-compatible language pack:

```powershell
python tools/localization.py import --input translations/en-US-updated.csv --culture en-US --language-name English --native-name English --author "Translator name" --output translations/en-US.json
```

By default all rows require a non-empty translation. Use `--allow-partial` to omit
missing/empty translations and let the app fall back to Vietnamese. Unknown or
duplicate keys, changed source, pending reviews and placeholder mismatches always
block export. Validation completes before the output file is opened. The CLI
checks culture-code syntax; the app's importer additionally validates the culture
with .NET `CultureInfo`. Import the generated JSON in the app for final validation
and check text layout on the target device before bundling it.

The JSON format consumed by the app and the XAML localization keys are unchanged.
Metadata lives once in `catalog.json`:

- `translationGroups`: broad descriptions indexed by key prefix.
- `translationNotes`: per-key `context`, `description`, `translatorNote`, optional
  `screenshot`, and `placeholders` (variable name → explanation and example).
- `placeholderNotes`: shared explanations for named variables; per-key notes
  take precedence. Explain positional variables per key, since `{0}` can mean
  different things in different messages.

Start with the Formula tab entries as examples. Add specific context for ambiguous
strings and legacy keys as they are reviewed; the tool explicitly marks unknown
context/placeholder meanings rather than inventing descriptions. Store screenshot
paths relative to the repository root (or use URLs) and share the images with the
worksheet. Metadata does not become UI text and is not copied into language packs.

Run workflow checks:

```powershell
python -m unittest discover -s tools -p "test_localization.py"
```

## Create a language pack

1. Copy `translation-template.json`.
2. Rename it using a culture code, for example:

```text
fr-FR.json
ja-JP.json
ko-KR.json
```

3. Edit the metadata:

```json
{
  "schemaVersion": 1,
  "culture": "fr-FR",
  "languageName": "French",
  "nativeName": "Français",
  "author": "Translator name",
  "appVersion": "0.1.0"
}
```

4. Translate values in `strings` and `templates`.
5. Do not change the keys on the left.

Example:

```json
"Tabs.Solve": "Solve"
```

becomes:

```json
"Tabs.Solve": "Résoudre"
```

## Placeholders

Keep every placeholder that appears in the source value:

```text
{field}
{digits}
{places}
{result}
{field|translate}
```

The order may be changed to match the grammar of the target language.

Example:

```json
"dynamic.required_field": "Please enter {field|translate}."
```

Japanese may place the field first:

```json
"dynamic.required_field": "{field|translate}を入力してください。"
```

Do not remove or rename placeholders.

## Mathematical content

Do not translate or alter:

```text
a, b, c, x
Δ
π
√
Sxq
Stp
V
operators and numeric values
```

Text surrounding formulas may be translated.

## Validation and fallback

When a key is missing, Math Solver falls back to Vietnamese.

The importer checks:

- JSON syntax
- schema version
- culture code
- missing keys
- unknown keys
- empty translations
- placeholder mismatches

A placeholder mismatch is an error because it can produce an incomplete
calculation message.

## Submitting a translation

A translator may:

- Import the JSON file locally in Math Solver
- Attach it to a GitHub issue
- Submit it through a pull request under `Resources/Raw/Localization`

Add the new language to `manifest.json` only when it will be bundled with the
application.
