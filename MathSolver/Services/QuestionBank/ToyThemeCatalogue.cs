namespace MathSolver.Services.QuestionBank;

/// <summary>Stable toy subjects shared by question generation, AI, and illustrated worksheets.</summary>
internal sealed record ToyThemeSubject(QuestionUnit Unit, string ArtworkId);

internal static class ToyThemeCatalogue
{
    internal static IReadOnlyList<ToyThemeSubject> Subjects { get; } = QuizContentCatalog.LoadList<ToyThemeSubject>("ToyThemeSubjects");

    internal static ToyThemeSubject? Find(string unitId) => Subjects.FirstOrDefault(subject => subject.Unit.Id == unitId);
}
