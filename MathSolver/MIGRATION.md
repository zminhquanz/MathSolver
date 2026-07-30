# Math Solver JSON Localization Refactor

## What this package changes

The previous `LocalizationService.cs` stored Vietnamese-to-English dictionaries,
phrase replacements, field-name translations, and dynamic regular expressions
inside one C# file.

This package moves language content to JSON files and keeps the public
`LocalizationService` API compatible with the existing application.

Existing code such as:

```csharp
LocalizationService.Translate("Giải toán");
LocalizationService.Attach(this);
```

continues to work.

New code can use stable keys:

```csharp
LocalizationService.TranslateKey(
    LocalizationKeys.Tabs.Solve);
```

## Files to copy

```text
Models/Localization/LanguagePack.cs

Services/Localization/JsonLocalizationProvider.cs
Services/Localization/LanguagePackValidator.cs
Services/Localization/LocalizedTemplateFormatter.cs
Services/Localization/LocalizationManager.cs
Services/Localization/LocalizationKeys.cs
Services/LocalizationService.cs

MarkupExtensions/TranslateExtension.cs

Resources/Raw/Localization/manifest.json
Resources/Raw/Localization/catalog.json
Resources/Raw/Localization/vi-VN.json
Resources/Raw/Localization/en-US.json
Resources/Raw/Localization/translation-template.json
Resources/Raw/Localization/language-pack.schema.json
```

Replace the old:

```text
Services/LocalizationService.cs
```

with the new file from this package.

## Project file

A standard .NET MAUI project normally already contains:

```xml
<MauiAsset Include="Resources\Raw\**"
           LogicalName="%(RecursiveDir)%(Filename)%(Extension)" />
```

When this line already exists, do not add another one.

When the project removed the default `MauiAsset` declaration, restore it so the
JSON files are packaged with the application.

## Startup

The new service preserves the existing synchronous call:

```csharp
LocalizationService.Initialize();
```

No startup change is required when the application already calls it.

## Existing XAML

Existing Vietnamese text can remain temporarily:

```xml
<Label Text="Giải toán" />
```

The compatibility tracker reads the Vietnamese source text and looks up its
translation from the JSON catalog.

This allows the migration to be completed gradually instead of rewriting every
XAML and C# file at once.

## New XAML

Add the namespace:

```xml
xmlns:localization="clr-namespace:MathSolver.MarkupExtensions"
```

Then bind to a stable key:

```xml
<Label Text="{localization:Translate Tabs.Solve}" />
```

The markup extension returns a binding to `LocalizationManager.Instance`, so
the text updates when the selected culture changes.

## New C# code

Prefer stable keys:

```csharp
string title =
    LocalizationService.TranslateKey(
        "Geometry.Title");
```

or constants:

```csharp
string title =
    LocalizationService.TranslateKey(
        LocalizationKeys.Geometry.Title);
```

For named placeholders:

```csharp
string message =
    LocalizationService.FormatKey(
        "Validation.Required",
        new Dictionary<string, object?>
        {
            ["field"] =
                LocalizationService.TranslateKey(
                    "Geometry.Height")
        });
```

Add new semantic keys to `vi-VN.json` and every translated language pack.

## Dynamic legacy messages

The old C# method contained language-specific regular expressions. Those rules
are now stored in:

```text
Resources/Raw/Localization/catalog.json
```

The regular expressions remain shared technical metadata. Translators only edit
the corresponding values in the `templates` section of their language pack.

A placeholder such as:

```text
{field|translate}
```

means that the captured field name is translated before being inserted into the
message.

## Language selection

The existing `AppLanguageManager` still maps its Vietnamese and English enum
values to `vi-VN` and `en-US`.

The new system also supports any valid culture code directly:

```csharp
await LocalizationService.SetCultureAsync(
    "fr-FR");
```

Available built-in and imported languages:

```csharp
IReadOnlyList<LanguageOption> languages =
    await LocalizationService
        .GetAvailableLanguagesAsync();
```

The selected culture is saved in MAUI `Preferences`.

## Importing a community language pack

Use a file picker in the Settings page, open the selected JSON file as a stream,
then call:

```csharp
LanguagePackValidationResult validation =
    await LocalizationService
        .ImportLanguagePackAsync(
            stream);
```

Check:

```csharp
validation.IsValid
validation.Errors
validation.Warnings
```

Valid imported packs are copied to:

```text
FileSystem.AppDataDirectory/Localization/
```

An imported pack overrides a packaged pack with the same culture code.

## Recommended next migration steps

1. Keep the compatibility tracker while the application is being migrated.
2. Convert frequently edited XAML strings to stable keys.
3. Convert dynamically generated messages to `FormatKey`.
4. Add a language-pack import button to Settings.
5. After all pages use stable keys, remove the visual-tree compatibility tracker.

## Clean build

After copying the files:

```text
Close the application
Delete bin and obj
Clean Solution
Rebuild Solution
```
