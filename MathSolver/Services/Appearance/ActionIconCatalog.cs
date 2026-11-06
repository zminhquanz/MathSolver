namespace MathSolver.Services;

/// <summary>Application-owned action identities, separate from educational math symbols.</summary>
public static class ActionIconCatalog
{
    public const double ActionSize = 20;
    public const double NavigationSize = 24;
    public static string? GlyphFor(string? key) => key switch
    {
        "Copy" or "Workspace.Copy" or "PowerRoot.CopyResult" => "\ue90c",
        "Workspace.Paste" or "Paste" or "PasteCells" => "\ue90d",
        "Clear" or "ClearAll" or "Actions.Clear" or "Workspace.Clear" or "Reject" or "RejectAll" or "RejectSelected" => "\ue90e",
        "Workspace.ExpandTable" => "\ue90f",
        "Workspace.RestoreTable" => "\ue910",
        "Workspace.MoreTools" or "Workspace.FewerTools" => "\ue911",
        "TechnicalDetails" or "Workspace.Details" => "\ue912",
        "Import" => "\ue913",
        "Export" or "PowerRoot.ExportTxt" => "\ue914",
        "Go" => "\ue915",
        "Previous" or "Back" or "Workspace.CloseDetails" => "\ue916",
        "Next" => "\ue917",
        "Workspace.EditSetup" or "Workspace.HideSetup" => "\ue918",
        "Calculate" or "Actions.Calculate" or "FindX.Action" or "Equation.Action" or "Quadratic.Action" => "\ue919",
        "Stop" or "PowerRoot.Stop" or "PowerRoot.ExportStop" or "PowerRoot.ExportStopping" or "AiBank.Stop" or "AiBank.CancelModelAction" => "\ue91a",
        "Ai" or "AiBank.Open" or "AiBank.OpenCompact" or "AiBank.Generate" => "\ue91b",
        "Pdf" or "QuizPdf.Export" or "QuizPdf.Generate" => "\ue91c",
        "Review" or "AiBank.Review.Open" => "\ue91d",
        "ApproveAction" or "ApproveSelected" or "ApproveAll" or "Apply" => "\ue91e",
        "SelectAll" or "Workspace.SelectAll" => "\ue91f",
        _ => null
    };

    /// <summary>Remove only legacy decorative prefixes from mapped actions; never strip math content.</summary>
    public static string LabelFor(string? key, string text)
    {
        if (GlyphFor(key) is null) return text;
        foreach (string prefix in new[] { "🗑️", "⏹️", "☑️", "🧮", "🗑", "📋", "⏹", "■", "✨", "🤖", "🔎", "🔍", "✅", "📄", "📥", "📤", "💾" })
            if (text.StartsWith(prefix, StringComparison.Ordinal)
                && (text.Length == prefix.Length || char.IsWhiteSpace(text[prefix.Length])))
                return text[prefix.Length..].TrimStart();
        return text;
    }
}
