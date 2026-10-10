using CommunityToolkit.Maui.Storage;
using MathSolver.Controls;
using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using System.Globalization;

namespace MathSolver.Views;

public partial class DataManagementPage
{
    private IReadOnlyList<BankWorkbookRow>? _pendingImport;
    private int _importPreviewIndex;
    private bool _choosingExcelOption;

    private string ImportRowErrors(BankWorkbookRow row) => row.Issues.Count == 0
        ? row.ErrorCode is null ? "" : ErrorText(row.ErrorCode)
        : string.Join("\n", row.Issues.Select(issue => string.Format(CultureInfo.CurrentCulture,
            T("ImportColumnError"), row.RowNumber, issue.Column, ErrorText(issue.ErrorCode))
            + (issue.Details is null ? "" : " " + issue.Details)));

    private void RenderImportReview()
    {
        if (_pendingImport is null) return;
        int valid = _pendingImport.Count(row => row.Question is not null);
        DataTransferStatusLabel.Text = string.Format(CultureInfo.CurrentCulture, T("ImportReviewSummary"),
            _pendingImport.Count, valid, _pendingImport.Count - valid);
        ImportReviewPanel.IsVisible = true;
        ImportReviewTable.SetData([T("ImportExcelRow"), T("ImportRowState"), T("ImportProblem"), T("ImportAnswer"), T("ImportErrors")],
            _pendingImport.Select(row => new[] { row.RowNumber.ToString(CultureInfo.CurrentCulture),
                T(row.Question is null ? "ImportInvalid" : "ImportValid"), row.Question?.WordProblem.ProblemText ?? "",
                row.Question is { } q ? QuestionAuthoringWorkbook.Answer(q) : "", ImportRowErrors(row) }).ToArray());
        _importPreviewIndex = Math.Clamp(_importPreviewIndex, 0, Math.Max(0, _pendingImport.Count - 1));
        ShowImportRow();
        UpdateDataActions();
    }

    private void OnImportReviewCellSelected(object? sender, SqlResultCellEventArgs e)
    {
        _importPreviewIndex = e.RowNumber - 1;
        ShowImportRow();
    }

    private void ShowImportRow()
    {
        var row = _pendingImport?.ElementAtOrDefault(_importPreviewIndex);
        ImportSelectedRowLabel.Text = row is null ? T("ImportEmpty")
            : T("ImportExcelRow") + " " + row.RowNumber + " · " + T(row.Question is null ? "ImportInvalid" : "ImportValid");
        ImportProblemPreview.IsVisible = ImportSolutionPreview.IsVisible = row?.Question is not null;
        ImportProblemPreview.Expression = row?.Question?.WordProblem.ProblemText ?? "";
        ImportSupportingDataPreview.Text = row?.Question is { } dataQuestion ? QuestionAuthoringWorkbook.PreviewData(dataQuestion) : "";
        ImportSupportingDataPreview.IsVisible = ImportSupportingDataPreview.Text.Length > 0;
        ImportSolutionPreview.Text = row?.Question is { } question ? QuestionAuthoringWorkbook.Solution(question) : "";
        ImportErrorsPreview.Text = row is null ? "" : ImportRowErrors(row);
        ImportErrorsPreview.IsVisible = ImportErrorsPreview.Text.Length > 0;
    }

    private void OnDiscardImportClicked(object? sender, EventArgs e)
    {
        if (_dataCancellation is not null) return;
        ClearImportReview();
        DataTransferStatusLabel.Text = T("ImportDiscarded");
    }

    private void ClearImportReview()
    {
        _pendingImport = null;
        _importPreviewIndex = 0;
        ImportReviewPanel.IsVisible = false;
        ImportReviewTable.Clear();
        ImportProblemPreview.Expression = "";
        ImportSupportingDataPreview.Text = "";
        ImportSolutionPreview.Text = ImportErrorsPreview.Text = "";
        UpdateDataActions();
    }

    private async void OnConfirmImportClicked(object? sender, EventArgs e)
    {
        if (!ConfirmImportButton.IsEnabled || _pendingImport is not { } reviewed) return;
        bool committed = false;
        await RunDataOperationAsync(true, async cancellation =>
        {
            var report = await _bank.Store.ImportReviewedAsync(reviewed, cancellation);
            if (!_appeared) return;
            ClearImportReview();
            ClearSqlResults();
            _loadedTable = false;
            DataTransferStatusLabel.Text = string.Format(CultureInfo.CurrentCulture, T("ImportResult"), report.Inserted, report.Duplicates, report.Rejected);
            committed = true;
        });
        if (committed && _appeared && !_closing)
        {
            await LoadEditableSqlAsync();
            _loadedTable = true;
        }
    }

    private async Task<int?> ChooseExcelOptionAsync(string title, IReadOnlyList<string> choices,
        CancellationToken cancellation, IReadOnlyList<string>? keys = null, IReadOnlyList<string>? descriptions = null)
    {
        if (choices.Count == 0) return null;
        var options = choices.Select((label, index) =>
        {
            var option = keys is null ? new QuizChoiceOption(index, "", label, "", "", false)
                : QuizChoiceCatalog.Create(index, keys[index], label, AppLanguageManager.CurrentLanguage, false);
            return descriptions is null ? option : option with { Description = descriptions[index] };
        }).ToArray();
        _choosingExcelOption = true;
        try
        {
            int? selected = await IllustratedQuizPicker.ChooseAsync(Navigation, title, options, cancellation);
            if (!_appeared || _closing) throw new OperationCanceledException(cancellation);
            return selected;
        }
        finally { _choosingExcelOption = false; }
    }

    private async void OnDownloadTemplateClicked(object? sender, EventArgs e)
    {
        await RunDataOperationAsync(true, async cancellation =>
        {
            string L(string key) => LocalizationService.TranslateKey(key);
            var families = Enum.GetValues<BankQuestionFamily>();
            var familyKeys = families.Select(QuestionAuthoringChoices.FamilyKey).ToArray();
            int? familyIndex = await ChooseExcelOptionAsync(T("TemplateProblem"), familyKeys.Select(L).ToArray(), cancellation, familyKeys);
            if (familyIndex is null) { DataTransferStatusLabel.Text = ""; return; }
            var family = families[familyIndex.Value];
            var starLabels = IllustratedQuizPicker.DifficultyLabels();
            int? stars = await ChooseExcelOptionAsync(T("TemplateStars"), starLabels, cancellation);
            if (stars is null) { DataTransferStatusLabel.Text = ""; return; }
            var tier = (CurriculumTier)(stars.Value + 1);
            string[] languageKeys = ["Language.Vietnamese", "Language.English"];
            int? languageIndex = await ChooseExcelOptionAsync(T("TemplateLanguage"), languageKeys.Select(L).ToArray(), cancellation, languageKeys);
            if (languageIndex is null) { DataTransferStatusLabel.Text = ""; return; }
            var language = languageIndex == 0 ? AppLanguage.Vietnamese : AppLanguage.English;
            int variant;
            string? sceneId = null;
            QuestionKnowledgeGroup group = QuestionKnowledgeGroup.Objects;
            FindXUnknownRole role = FindXUnknownRole.None;
            string variantLabel;
            if (ReasoningStoryCatalogue.Supports(family))
            {
                var variants = ReasoningStoryCatalogue.Variants(family, tier);
                string[] labels = variants.Select(v => L(QuestionAuthoringChoices.VariantKey(family, v)) + (family == BankQuestionFamily.Geometry
                    ? " · " + GeometryReasoningText.MeasurementName(ReasoningStoryCatalogue.GeometryProfile(v).Measurement, AppLanguageManager.CurrentLanguage) : "")).ToArray();
                int? index = await ChooseExcelOptionAsync(T("TemplateSubtype"), labels, cancellation,
                    variants.Select(v => QuestionAuthoringChoices.VariantKey(family, v)).ToArray());
                if (index is null) { DataTransferStatusLabel.Text = ""; return; }
                variant = variants[index.Value];
                variantLabel = labels[index.Value];
            }
            else
            {
                var groups = QuestionLearningProfile.Groups().Where(g => Enum.GetValues<ArithmeticOperation>().Any(op =>
                    QuestionAuthoringChoices.Scenes(family, op, tier, g).Length > 0)).ToArray();
                var groupKeys = groups.Select(g => "Learning.Group." + g).ToArray();
                int? groupIndex = await ChooseExcelOptionAsync(T("TemplateGroup"), groupKeys.Select(L).ToArray(), cancellation, groupKeys);
                if (groupIndex is null) { DataTransferStatusLabel.Text = ""; return; }
                group = groups[groupIndex.Value];
                var operations = Enum.GetValues<ArithmeticOperation>().Where(op => QuestionAuthoringChoices.Scenes(family, op, tier, group).Length > 0).ToArray();
                int? operationIndex = await ChooseExcelOptionAsync(T("TemplateSubtype"), operations.Select(op => T(op.ToString())).ToArray(), cancellation,
                    operations.Select(op => "AiBank." + op).ToArray());
                if (operationIndex is null) { DataTransferStatusLabel.Text = ""; return; }
                variant = (int)operations[operationIndex.Value];
                variantLabel = T(operations[operationIndex.Value].ToString()) + " · " + L("Learning.Group." + group);
                if (family == BankQuestionFamily.FindX)
                {
                    var roles = FindXQuestionCatalogue.Available(new(group), (ArithmeticOperation)variant, tier).Select(scene => scene.Role).Distinct().ToArray();
                    var roleKeys = roles.Select(r => "FindXBank.Role." + r).ToArray();
                    int? roleIndex = await ChooseExcelOptionAsync(T("TemplateRole"), roleKeys.Select(L).ToArray(), cancellation, roleKeys);
                    if (roleIndex is null) { DataTransferStatusLabel.Text = T("TemplateUnavailable"); return; }
                    role = roles[roleIndex.Value];
                    variantLabel += " · " + L("FindXBank.Role." + role);
                }
                var scenes = QuestionAuthoringChoices.Scenes(family, (ArithmeticOperation)variant, tier, group, role);
                var sceneLabels = await Task.Run(() => scenes.Select((id, i) => {
                    var sample = QuestionAuthoringChoices.Create(family, variant, tier, language, group, role, new Random(718), id);
                    return BasicQuestionTemplates.Example(sample).ToWordProblem(sample).ProblemText;
                }).ToArray(), cancellation);
                int? sceneIndex = await ChooseExcelOptionAsync(T("TemplateScene"),
                    scenes.Select((_, i) => variantLabel + " · " + (i + 1)).ToArray(), cancellation,
                    scenes.Select(_ => "AiBank." + (ArithmeticOperation)variant).ToArray(), sceneLabels);
                if (sceneIndex is null) { DataTransferStatusLabel.Text = ""; return; }
                sceneId = scenes[sceneIndex.Value];
            }
            var contract = await Task.Run(() => QuestionAuthoringChoices.Create(family, variant, tier, language, group, role, sceneId: sceneId), cancellation);
            using var output = new MemoryStream();
            QuestionAuthoringWorkbook.Write(output, contract, L(QuestionAuthoringChoices.FamilyKey(family)) + " · " + variantLabel, AppLanguageManager.CurrentLanguage);
            output.Position = 0;
            var saved = await FileSaver.Default.SaveAsync($"MathSolver-authoring-{family}-{tier}.xlsx", output, cancellation);
            if (!saved.IsSuccessful)
            {
                if (saved.Exception is OperationCanceledException) throw new OperationCanceledException(cancellation);
                throw saved.Exception ?? new IOException(T("ExportFailed"));
            }
            DataTransferStatusLabel.Text = T("TemplateDownloaded");
        });
    }
}
