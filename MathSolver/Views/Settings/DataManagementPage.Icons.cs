using MathSolver.Controls;

namespace MathSolver.Views;

public partial class DataManagementPage
{
    private void ConfigureActionIcons()
    {
        foreach (var (button, emoji) in new (EmojiActionButton Button, string Emoji)[]
        {
            (DownloadTemplateButton, "\U0001F4E5"), // Download
            (ImportExcelButton, "\U0001F4C2"), // Open/import
            (ExportExcelButton, "\U0001F4E4"), // Export
            (ConfirmImportButton, "\u2705"),
            (DiscardImportButton, "\u21A9\uFE0F"),
            (DeleteAllButton, "\U0001F5D1\uFE0F"),
            (CancelDataButton, "\u23F9\uFE0F"),
            (OpenSqlTableButton, "\U0001F5C3\uFE0F"),
            (InquiryToggleButton, "\U0001F50E"),
            (SqlHelpToggleButton, "\u2754"),
            (QueryButton, "\u25B6\uFE0F"),
            (AddSqlRowButton, "\u2795"),
            (EditSqlRowButton, "\u270F\uFE0F"),
            (SaveNewSqlRowButton, "\U0001F4BE"),
            (CancelNewSqlRowButton, "\u21A9\uFE0F"),
            (SaveSqlRowButton, "\U0001F4BE"),
            (CancelSqlRowButton, "\u21A9\uFE0F"),
            (DeleteSqlRowButton, "\U0001F5D1\uFE0F"),
            (ReloadSqlGridButton, "\U0001F504"),
            (LoadEditableSqlButton, "\U0001F5C3\uFE0F"),
            (CopySqlCellButton, "\U0001F4CB"),
            (CloseSqlCellButton, "\u2716\uFE0F")
        })
            button.Emoji = emoji;

        foreach (var button in SqlHelpTopics.Children.OfType<EmojiActionButton>())
            button.Emoji = button.CommandParameter switch
            {
                "SqlHelpQuery" => "\U0001F50E",
                "SqlHelpEdit" => "\u270F\uFE0F",
                "SqlHelpColumns" => "\U0001F9FE",
                _ => "\u2139\uFE0F"
            };
    }
}
