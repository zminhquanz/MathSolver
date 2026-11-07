using MathSolver.Controls;

namespace MathSolver.Views;

public partial class DataManagementPage
{
    private void ConfigureActionIcons()
    {
        foreach (var (button, emoji, key) in new (EmojiActionButton Button, string Emoji, string Key)[]
        {
            (DownloadTemplateButton, "\U0001F4E5", "Download"), // Download
            (ImportExcelButton, "\U0001F4C2", "Import"), // Open/import
            (ExportExcelButton, "\U0001F4E4", "Export"), // Export
            (ConfirmImportButton, "\u2705", "Apply"),
            (DiscardImportButton, "\u21A9\uFE0F", "Undo"),
            (DeleteAllButton, "\U0001F5D1\uFE0F", "Clear"),
            (CancelDataButton, "\u23F9\uFE0F", "Stop"),
            (OpenSqlTableButton, "\U0001F5C3\uFE0F", "Table"),
            (InquiryToggleButton, "\U0001F50E", "Sql"),
            (SqlHelpToggleButton, "\u2754", "Help"),
            (QueryButton, "\u25B6\uFE0F", "Run"),
            (AddSqlRowButton, "\u2795", "Add"),
            (EditSqlRowButton, "\u270F\uFE0F", "Edit"),
            (SaveNewSqlRowButton, "\U0001F4BE", "Save"),
            (CancelNewSqlRowButton, "\u21A9\uFE0F", "Undo"),
            (SaveSqlRowButton, "\U0001F4BE", "Save"),
            (CancelSqlRowButton, "\u21A9\uFE0F", "Undo"),
            (DeleteSqlRowButton, "\U0001F5D1\uFE0F", "Clear"),
            (ReloadSqlGridButton, "\U0001F504", "Refresh"),
            (LoadEditableSqlButton, "\U0001F5C3\uFE0F", "Table"),
            (CopySqlCellButton, "\U0001F4CB", "Copy"),
            (CloseSqlCellButton, "\u2716\uFE0F", "Close")
        })
        {
            button.Emoji = emoji;
            button.VectorIconKey = key;
        }

        foreach (var button in SqlHelpTopics.Children.OfType<EmojiActionButton>())
        {
            button.Emoji = button.CommandParameter switch
            {
                "SqlHelpQuery" => "\U0001F50E",
                "SqlHelpEdit" => "\u270F\uFE0F",
                "SqlHelpColumns" => "\U0001F9FE",
                _ => "\u2139\uFE0F"
            };
            button.VectorIconKey = button.CommandParameter switch
            {
                "SqlHelpQuery" => "Search", "SqlHelpEdit" => "Edit",
                "SqlHelpColumns" => "Columns", _ => "TechnicalDetails"
            };
        }
    }
}
