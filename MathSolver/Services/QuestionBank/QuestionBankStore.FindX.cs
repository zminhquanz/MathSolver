using MathSolver.Models;
using SQLite;
using System.Diagnostics;
using System.Text.Json;

namespace MathSolver.Services.QuestionBank;

public sealed partial class QuestionBankStore
{
    private readonly Dictionary<(ArithmeticOperation, CurriculumTier, AppLanguage, QuestionKnowledgeGroup), Queue<string>> _findXHistory = [];

    public async Task<ValidatedBankQuestion?> TakeFindXAsync(ArithmeticOperation operation, CurriculumTier tier,
        AppLanguage language, QuestionLearningProfile profile, CancellationToken cancellationToken = default)
    {
        try
        {
            return await WithDatabaseAsync<ValidatedBankQuestion?>(db =>
            {
                var scenes = FindXQuestionCatalogue.Available(profile, operation, tier).ToDictionary(s => s.Id);
                var key = (operation, tier, language, profile.Group);
                if (!_findXHistory.TryGetValue(key, out var history)) _findXHistory[key] = history = new();
                var buckets = db.Query<SelectionBucket>("SELECT SceneId,MAX(LastUsedUtc) AS LastUsedUtc FROM BasicQuestionBank "
                    + "WHERE ProblemType=1 AND Version=6 AND Operation=? AND Stars=? AND Language=? AND KnowledgeGroup=? GROUP BY SceneId",
                    (int)operation, (int)tier, (int)language, (int)profile.Group);
                foreach (var bucket in buckets.Where(b => scenes.ContainsKey(b.SceneId))
                    .OrderBy(_ => Random.Shared.Next()).OrderBy(b => history.Count(id => scenes[id].Role == scenes[b.SceneId].Role))
                    .ThenBy(b => history.Count(id => id == b.SceneId)).ThenBy(b => b.LastUsedUtc))
                {
                    foreach (var row in db.Query<Row>("SELECT * FROM BasicQuestionBank WHERE ProblemType=1 AND Version=6 "
                        + "AND Operation=? AND Stars=? AND Language=? AND KnowledgeGroup=? AND SceneId=? ORDER BY UseCount,LastUsedUtc LIMIT 32",
                        (int)operation, (int)tier, (int)language, (int)profile.Group, bucket.SceneId))
                    {
                        try
                        {
                            var c = JsonSerializer.Deserialize<BasicQuestionContract>(row.ContractJson, JsonOptions);
                            if (c is null || c.Family != BankQuestionFamily.FindX || row.ProblemType != (int)c.Family
                                || row.ProblemVariant != (int)c.UnknownRole || row.Version != c.Version || c.Operation != operation
                                || c.Tier != tier || c.Language != language || c.KnowledgeGroup != profile.Group
                                || c.SceneId != row.SceneId || c.TopicId != row.TopicId || row.Structure != (int)c.Structure || c.Grade != row.Grade) continue;
                            bool userAuthored = IsUserAuthored(db, row.Hash);
                            var validation = ValidateDraft(row.DraftJson, c, userAuthored);
                            if (!validation.IsValid || validation.Contract != c) continue;
                            db.Execute("UPDATE BasicQuestionBank SET UseCount=UseCount+1,LastUsedUtc=? WHERE Hash=?", DateTime.UtcNow, row.Hash);
                            history.Enqueue(c.SceneId);
                            if (history.Count > 64) history.Dequeue();
                            return new(c, validation.Draft!, row.RawJson, row.ModelName, row.CreatedUtc) { UserAuthored = userAuthored };
                        }
                        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
                        { Debug.WriteLine($"Find-X bank row skipped: {error.GetType().Name}"); }
                    }
                }
                return null;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is SQLiteException or IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Find-X bank unavailable: {error.GetType().Name}");
            return null;
        }
    }
}
