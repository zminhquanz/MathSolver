using MathSolver.Controls;
using MathSolver.Services;

namespace MathSolver.Views;

public partial class DataManagementPage
{
    private readonly List<Label> _importGuideLabels = [];
    private ExcelGuidePlayer _excelGuidePlayer = null!;

    private void ConfigureImportGuide()
    {
        foreach (string asset in new[] { "excel_template_sheet.png", "excel_fill_sheet.png", "excel_review_sheet.png" })
        {
            var label = new Label { FontSize = 14, LineBreakMode = LineBreakMode.WordWrap, HorizontalTextAlignment = TextAlignment.Center };
            label.SetDynamicResource(Label.TextColorProperty, "TextPrimaryColor");
            _importGuideLabels.Add(label);
            if (asset == "excel_fill_sheet.png")
            {
                _excelGuidePlayer = new ExcelGuidePlayer();
                ImportGuideSteps.Children.Add(new VerticalStackLayout
                {
                    Spacing = 12, Children = { _excelGuidePlayer, label }
                });
            }
            else ImportGuideSteps.Children.Add(new IllustratedMessage(asset, label, 108, 72));
        }
        RefreshImportGuide();
    }

    private void OnToggleImportGuideClicked(object? sender, EventArgs e)
    {
        ImportGuideSteps.IsVisible = !ImportGuideSteps.IsVisible;
        if (ImportGuideSteps.IsVisible) _excelGuidePlayer.RequestPlay();
        else _excelGuidePlayer.Stop();
        RefreshImportGuide();
    }

    private void RefreshImportGuide()
    {
        string Key(string suffix) => LocalizationService.TranslateKey("DataManagement.ImportGuide." + suffix);
        ImportGuideToggle.Text = Key(ImportGuideSteps.IsVisible ? "Hide" : "Show");
        SemanticProperties.SetDescription(ImportGuideToggle, ImportGuideToggle.Text);
        for (int i = 0; i < _importGuideLabels.Count; i++) _importGuideLabels[i].Text = Key("Step" + (i + 1));
    }
}
