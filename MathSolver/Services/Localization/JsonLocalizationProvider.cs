using MathSolver.Models.Localization;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MathSolver.Services.Localization;

public sealed class JsonLocalizationProvider
{
    private const string PackageFolder =
        "Localization";

    private readonly JsonSerializerOptions _jsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter(
                    JsonNamingPolicy.CamelCase)
            }
        };

    public string UserLanguageFolder =>
        Path.Combine(
            FileSystem.Current.AppDataDirectory,
            PackageFolder);

    public async Task<LocalizationManifest> LoadManifestAsync(
        CancellationToken cancellationToken = default)
    {
        return await ReadPackageJsonAsync<LocalizationManifest>(
            $"{PackageFolder}/manifest.json",
            cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<LocalizationCatalog> LoadCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        return await ReadPackageJsonAsync<LocalizationCatalog>(
            $"{PackageFolder}/catalog.json",
            cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<LanguagePack> LoadLanguagePackAsync(
        string culture,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            culture);

        string normalizedCulture =
            NormalizeCulture(
                culture);

        string userPath =
            Path.Combine(
                UserLanguageFolder,
                $"{normalizedCulture}.json");

        if (File.Exists(
                userPath))
        {
            return await ReadFileJsonAsync<LanguagePack>(
                userPath,
                cancellationToken)
                .ConfigureAwait(false);
        }

        return await ReadPackageJsonAsync<LanguagePack>(
            $"{PackageFolder}/{normalizedCulture}.json",
            cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LanguageOption>>
        GetAvailableLanguagesAsync(
            CancellationToken cancellationToken = default)
    {
        LocalizationManifest manifest =
            await LoadManifestAsync(
                cancellationToken)
                .ConfigureAwait(false);

        var result =
            new Dictionary<string, LanguageOption>(
                StringComparer.OrdinalIgnoreCase);

        foreach (LanguageManifestEntry entry
                 in manifest.Languages)
        {
            try
            {
                LanguagePack pack =
                    await ReadPackageJsonAsync<LanguagePack>(
                        $"{PackageFolder}/{entry.File}",
                        cancellationToken)
                        .ConfigureAwait(false);

                result[pack.Culture] =
                    new LanguageOption(
                        pack.Culture,
                        pack.LanguageName,
                        pack.NativeName,
                        pack.Author,
                        true);
            }
            catch
            {
                // One malformed optional pack must not prevent the app
                // from loading the remaining languages.
            }
        }

        Directory.CreateDirectory(
            UserLanguageFolder);

        foreach (string filePath
                 in Directory.EnumerateFiles(
                     UserLanguageFolder,
                     "*.json",
                     SearchOption.TopDirectoryOnly))
        {
            try
            {
                LanguagePack pack =
                    await ReadFileJsonAsync<LanguagePack>(
                        filePath,
                        cancellationToken)
                        .ConfigureAwait(false);

                result[pack.Culture] =
                    new LanguageOption(
                        pack.Culture,
                        pack.LanguageName,
                        pack.NativeName,
                        pack.Author,
                        false);
            }
            catch
            {
                // Invalid custom packs are ignored here and reported
                // through the import validator instead.
            }
        }

        return result.Values
            .OrderBy(
                language =>
                    language.NativeName,
                StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public async Task<string> SaveImportedPackAsync(
        LanguagePack pack,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            pack);

        string culture =
            NormalizeCulture(
                pack.Culture);

        Directory.CreateDirectory(
            UserLanguageFolder);

        string outputPath =
            Path.Combine(
                UserLanguageFolder,
                $"{culture}.json");

        await using FileStream stream =
            new(
                outputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);

        await JsonSerializer.SerializeAsync(
            stream,
            pack,
            _jsonOptions,
            cancellationToken)
            .ConfigureAwait(false);

        return outputPath;
    }

    public bool DeleteImportedPack(
        string culture)
    {
        string normalizedCulture =
            NormalizeCulture(
                culture);

        string path =
            Path.Combine(
                UserLanguageFolder,
                $"{normalizedCulture}.json");

        if (!File.Exists(
                path))
        {
            return false;
        }

        File.Delete(
            path);

        return true;
    }

    public async Task<LanguagePack> DeserializeLanguagePackAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            stream);

        LanguagePack? pack =
            await JsonSerializer.DeserializeAsync<LanguagePack>(
                stream,
                _jsonOptions,
                cancellationToken)
                .ConfigureAwait(false);

        return pack ??
               throw new InvalidDataException(
                   "The language pack is empty or invalid.");
    }

    private async Task<T> ReadPackageJsonAsync<T>(
        string logicalPath,
        CancellationToken cancellationToken)
    {
        await using Stream stream =
            await FileSystem.Current
                .OpenAppPackageFileAsync(
                    logicalPath)
                .ConfigureAwait(false);

        T? value =
            await JsonSerializer.DeserializeAsync<T>(
                stream,
                _jsonOptions,
                cancellationToken)
                .ConfigureAwait(false);

        return value ??
               throw new InvalidDataException(
                   $"The packaged localization file '{logicalPath}' is invalid.");
    }

    private async Task<T> ReadFileJsonAsync<T>(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using FileStream stream =
            new(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

        T? value =
            await JsonSerializer.DeserializeAsync<T>(
                stream,
                _jsonOptions,
                cancellationToken)
                .ConfigureAwait(false);

        return value ??
               throw new InvalidDataException(
                   $"The localization file '{filePath}' is invalid.");
    }

    public static string NormalizeCulture(
        string culture)
    {
        try
        {
            return CultureInfo
                .GetCultureInfo(
                    culture)
                .Name;
        }
        catch (CultureNotFoundException exception)
        {
            throw new InvalidDataException(
                $"'{culture}' is not a valid culture name.",
                exception);
        }
    }
}
