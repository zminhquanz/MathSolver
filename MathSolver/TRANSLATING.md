# Translating Math Solver

Math Solver language packs are UTF-8 JSON files. Translators do not need to
edit C# or XAML.

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