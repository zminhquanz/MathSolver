namespace MathSolver.Services;

/// <summary>Semantic identities for the optional, locally licensed Fluent Systems Regular pack.</summary>
public static class Icons8ActionCatalog
{
    public const string FontAlias = "Icons8Actions";
#if ICONS8_LICENSED_ASSETS
    public static bool IsAvailable => true;
#else
    public static bool IsAvailable => false;
#endif

    public static string? CanonicalFor(string? key) => key switch
    {
        "PowerRoot.ExportTxt" => "Export",
        "AiBank.ChooseModel" => "File",
        "AiBank.OpenFolder" => "Folder",
        "AiBank.EjectModel" or "AiBank.Eject" => "Eject",
        "AiBank.DownloadModel" or "AiBank.Download" => "Download",
        "AiBank.Insert" => "Save",
        "AiBank.Share" => "Share",
        "Data.SaveRow" => "Save",
        "Regenerate" => "Refresh",
        "Import" or "Export" or "Settings" or "Download" or "Folder" or "File" or "Eject"
            or "Share" or "Code" or "Undo" or "Table" or "Search" or "Help" or "Run" or "Add"
            or "Save" or "Refresh" or "Close" or "Columns" or "Cpu" or "Gpu" or "Database"
            or "ClearSelection" or "Sql" or "Edit" => key,
        _ => null
    };

    public static string? GlyphFor(string? key) => !IsAvailable ? null : CanonicalFor(key) switch
    {
        "Import" => "\uea00", "Export" => "\uea01", "Settings" => "\uea02",
        "Download" => "\uea03", "Folder" => "\uea04", "File" => "\uea05",
        "Eject" => "\uea06", "Share" => "\uea07", "Code" => "\uea08",
        "Undo" => "\uea09", "Table" => "\uea0a", "Search" => "\uea0b",
        "Help" => "\uea0c", "Run" => "\uea0d", "Add" => "\uea0e",
        "Save" => "\uea0f", "Refresh" => "\uea10", "Close" => "\uea11",
        "Columns" => "\uea12", "Cpu" => "\uea13", "Gpu" => "\uea14",
        "Database" => "\uea15", "ClearSelection" => "\uea16", "Sql" => "\uea17",
        "Edit" => "\uea18", _ => null
    };
}
