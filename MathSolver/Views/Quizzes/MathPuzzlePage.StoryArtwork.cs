using MathSolver.Models;
using MathSolver.Services;

namespace MathSolver.Views;

public partial class MathPuzzlePage
{
    private void UpdateStoryArtwork(bool diagramVisible)
    {
        var word = _currentQuestion?.WordProblem;
        var elementary = _currentQuestion?.ElementaryProblem;
        bool hasStory = word is not null || elementary?.StoryContextId is not null
            || _currentQuestion?.ProportionProblem is not null || _currentQuestion?.AverageProblem is not null
            || _currentQuestion?.PercentageProblem is not null
            || elementary is { Kind: QuizProblemKind.MultiStep, Reasoning: not null };
        IEnumerable<string> units = word is null ? [] : [word.AnswerUnit];
        if (elementary is not null)
            units = units.Concat(elementary.Answers.Select(answer => answer.Unit))
                .Concat(elementary.Reasoning?.Givens.Select(given => given.Unit) ?? []);
        if (_currentQuestion?.ProportionProblem is { } proportion)
            units = units.Concat([proportion.AnswerUnit, proportion.GroupCountUnits?.Items ?? ""]);
        if (_currentQuestion?.AverageProblem is { } average) units = units.Append(average.AnswerUnit);
        if (_currentQuestion?.PercentageProblem is { } percentage) units = units.Append(percentage.AnswerUnit);
        bool hasFactTable = word?.FactTable is not null;
        string? purchasedArtwork=QuizStoryArtworkCatalogue.PurchasedItemArtwork(word,AppLanguageManager.CurrentLanguage);
        string? asset = _currentQuestion?.MotionProblem is { } motion && !hasFactTable
            ? QuizStoryArtwork.SelectMotion(motion.NarrativeActors, QuizStoryArtworkCatalogue.MotionActors(AppLanguageManager.CurrentLanguage), diagramVisible)
            : QuizStoryArtwork.Select(hasStory, hasFactTable, units,
                word?.Quantity == WordProblemQuantity.Money && purchasedArtwork is null, word?.ItemUnit,
                purchasedArtwork ?? word?.StoryArtworkId ?? (_currentQuestion is null ? null : QuizStoryArtworkCatalogue.SourceArtwork(_currentQuestion, AppLanguageManager.CurrentLanguage)));
        // Clear old artwork on unknown/non-story questions; supporting diagrams no longer suppress it.
        if ((QuestionStoryArtwork.Source as FileImageSource)?.File != asset)
            QuestionStoryArtwork.Source = asset is null ? null : ImageSource.FromFile(asset);
        QuestionStoryArtwork.IsVisible = asset is not null;
        ResizeStoryArtwork();
    }

    private void ResizeStoryArtwork()
    {
        bool narrow = QuestionDiagramGrid.Width <= 0 || QuestionDiagramGrid.Width < 600;
        var size = QuizStoryArtwork.Size(QuestionTextPanel.Width, narrow);
        QuestionStoryArtwork.WidthRequest = size.Width;
        QuestionStoryArtwork.HeightRequest = size.Height;
    }

    private void OnQuestionStoryPanelSizeChanged(object? sender, EventArgs e) => ResizeStoryArtwork();
}
