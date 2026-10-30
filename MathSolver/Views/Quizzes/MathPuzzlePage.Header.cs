using MathSolver.Services;

namespace MathSolver.Views;

public partial class MathPuzzlePage
{
    private bool _openingPracticeActions;
    private (bool Expanded, bool Compact)? _practiceHeroLayout;

    private void UpdatePracticeHeader()
    {
        bool compact = ResponsiveLayoutPolicy.ForSolver(
            MathPuzzlePageContentRoot.Width, MathPuzzlePageContentRoot.Height, CurrentTextScale).CompactHeader;
        if (_practiceHeroLayout == (_practiceSettingsExpanded, compact)) return;
        _practiceHeroLayout = (_practiceSettingsExpanded, compact);
        PracticeHero.IsVisible = _practiceSettingsExpanded;
        PracticeHeroEmojiLabel.IsVisible = !compact;
        PracticeHeroSubtitle.IsVisible = !compact;
        if (compact) PracticeHeroTitle.FontSize = 18;
        else PracticeHeroTitle.ClearValue(Label.FontSizeProperty);
    }

    private async void OnMorePracticeActionsClicked(object? sender, EventArgs e)
    {
        if (_openingPracticeActions || !CanUpdatePracticeLayout) return;
        _openingPracticeActions = true;
        try
        {
            string ai = Translate("AiBank.OpenCompact");
            string pdf = Translate("QuizPdf.Export");
            string? action = await DisplayActionSheetAsync(TranslateQuiz("Quiz.MoreActions"),
                Translate("AiBank.Cancel"), null, ai, pdf);
            if (!CanUpdatePracticeLayout || Shell.Current?.CurrentPage != this) return;
            if (action == ai) OnOpenAiQuestionBankClicked(sender, e);
            else if (action == pdf) OnExportQuizPdfClicked(sender, e);
        }
        finally { _openingPracticeActions = false; }
    }
}
