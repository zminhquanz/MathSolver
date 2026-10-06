using MathSolver.Services.QuestionBank;
using System.Text.Json;

internal static class QuestionBankTestFixtures
{
    // Only for compatibility/selection tests of existing databases. Production
    // insertion must reject duplicate wording even across different metadata.
    public static void SeedHistorical(string path, ValidatedBankQuestion question)
    {
        var c = question.Contract;
        string draftJson = QuestionBankStore.SerializeDraft(question.Draft);
        if (!BasicQuestionValidator.Validate(draftJson, c).IsValid)
            throw new InvalidOperationException("Invalid historical fixture.");
        using var db = new SQLite.SQLiteConnection(path);
        db.Insert(new QuestionBankStore.Row {
            Hash = "historical-fixture-" + Guid.NewGuid().ToString("N"),
            Operation = (int)c.Operation, Stars = (int)c.Tier, Language = (int)c.Language, Version = c.Version,
            ContractJson = JsonSerializer.Serialize(c), DraftJson = draftJson,
            RawJson = question.RawJson, ModelName = question.ModelName, CreatedUtc = question.CreatedUtc,
            Structure = (int)c.Structure, TopicId = c.TopicId, SceneId = c.SceneId, Grade = c.Grade,
            KnowledgeGroup = (int)c.KnowledgeGroup, ProblemType = (int)c.Family, ProblemVariant = (int)c.UnknownRole });
    }
}
