using MathSolver.Models;
using SQLite;
using System.Diagnostics;
using System.Text.Json;

namespace MathSolver.Services.QuestionBank;

public sealed partial class QuestionBankStore
{
    private readonly Dictionary<(ArithmeticOperation, CurriculumTier, AppLanguage, QuestionKnowledgeGroup), Queue<string>> _fractionHistory = [];

    public async Task<ValidatedBankQuestion?> TakeFractionAsync(ArithmeticOperation operation, CurriculumTier tier,
        AppLanguage language, QuestionLearningProfile profile, CancellationToken cancellationToken = default)
    {
        try
        {
            return await WithDatabaseAsync<ValidatedBankQuestion?>(db =>
            {
                var scenes = FractionQuestionCatalogue.Available(profile, operation, tier).ToDictionary(s => s.Id);
                var key = (operation, tier, language, profile.Group);
                if (!_fractionHistory.TryGetValue(key, out var history)) _fractionHistory[key] = history = new();
                var buckets = db.Query<SelectionBucket>("SELECT SceneId,MAX(LastUsedUtc) AS LastUsedUtc FROM BasicQuestionBank "
                    + "WHERE ProblemType=2 AND Version=7 AND Operation=? AND Stars=? AND Language=? AND KnowledgeGroup=? GROUP BY SceneId",
                    (int)operation, (int)tier, (int)language, (int)profile.Group);
                foreach (var bucket in buckets.Where(b => scenes.ContainsKey(b.SceneId))
                    .OrderBy(_ => Random.Shared.Next())
                    .OrderBy(b => history.Count(id => id == b.SceneId)).ThenBy(b => b.LastUsedUtc))
                {
                    foreach (var row in db.Query<Row>("SELECT * FROM BasicQuestionBank WHERE ProblemType=2 AND Version=7 "
                        + "AND Operation=? AND Stars=? AND Language=? AND KnowledgeGroup=? AND SceneId=? ORDER BY UseCount,LastUsedUtc LIMIT 32",
                        (int)operation, (int)tier, (int)language, (int)profile.Group, bucket.SceneId))
                    {
                        try
                        {
                            var c = JsonSerializer.Deserialize<BasicQuestionContract>(row.ContractJson, JsonOptions);
                            if (c is null || c.Family != BankQuestionFamily.Fraction || row.ProblemType != (int)c.Family
                                || row.ProblemVariant != (int)c.UnknownRole || row.Version != c.Version || c.Operation != operation
                                || c.Tier != tier || c.Language != language || c.KnowledgeGroup != profile.Group
                                || c.SceneId != row.SceneId || c.TopicId != row.TopicId || row.Structure != (int)c.Structure || c.Grade != row.Grade) continue;
                            var validation = BasicQuestionValidator.Validate(row.DraftJson, c);
                            if (!validation.IsValid || validation.Contract != c) continue;
                            db.Execute("UPDATE BasicQuestionBank SET UseCount=UseCount+1,LastUsedUtc=? WHERE Hash=?", DateTime.UtcNow, row.Hash);
                            history.Enqueue(c.SceneId);
                            if (history.Count > 64) history.Dequeue();
                            return new(c, validation.Draft!, row.RawJson, row.ModelName, row.CreatedUtc);
                        }
                        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
                        { Debug.WriteLine($"Fraction bank row skipped: {error.GetType().Name}"); }
                    }
                }
                return null;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is SQLiteException or IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Fraction bank unavailable: {error.GetType().Name}");
            return null;
        }
    }
}
