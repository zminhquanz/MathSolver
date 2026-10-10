using MathSolver.Models;
using SQLite;
using System.Diagnostics;
using System.Text.Json;

namespace MathSolver.Services.QuestionBank;

public sealed partial class QuestionBankStore
{
    private readonly Dictionary<(BankQuestionFamily, int, CurriculumTier, AppLanguage), Queue<string>> _storyHistory = [];

    public Task<ValidatedBankQuestion?> TakeReasoningAsync(BankQuestionFamily family, int variant,
        CurriculumTier tier, AppLanguage language, CancellationToken cancellationToken = default)
        => TakeReasoningCoreAsync(family, variant, tier, language, null, cancellationToken);

    public Task<ValidatedBankQuestion?> TakeChartAsync(DataChartProfile profile, CancellationToken cancellationToken = default)
        => TakeReasoningCoreAsync(BankQuestionFamily.Data, (int)profile.Type, profile.Tier, profile.Language, profile, cancellationToken);

    private async Task<ValidatedBankQuestion?> TakeReasoningCoreAsync(BankQuestionFamily family, int variant,
        CurriculumTier tier, AppLanguage language, DataChartProfile? profile, CancellationToken cancellationToken)
    {
        if (!ReasoningStoryCatalogue.Supports(family)) throw new ArgumentOutOfRangeException(nameof(family));
        try
        {
            return await WithDatabaseAsync<ValidatedBankQuestion?>(db =>
            {
                var key = (family, variant, tier, language);
                if (!_storyHistory.TryGetValue(key, out var history)) _storyHistory[key] = history = new();
                string query = "SELECT * FROM BasicQuestionBank WHERE Version=8 AND ProblemType=? "
                    + "AND ProblemVariant=? AND Stars=? AND Language=? ORDER BY UseCount,LastUsedUtc LIMIT 128";
                var rows = profile is null
                    ? db.Query<Row>(query, (int)family, variant, (int)tier, (int)language)
                    : db.Query<Row>(query.Replace("ORDER BY", "AND SceneId=? ORDER BY"),
                        (int)family, variant, (int)tier, (int)language, profile.ContextId);
                foreach (var row in rows.OrderBy(_ => Random.Shared.Next())
                    .OrderBy(r => history.Count(id => id == r.SceneId)).ThenBy(r => r.UseCount).ThenBy(r => r.LastUsedUtc))
                {
                    try
                    {
                        var c = JsonSerializer.Deserialize<BasicQuestionContract>(row.ContractJson, JsonOptions);
                        if (c is null || c.Family != family || c.BankVariant != variant || c.Tier != tier || c.Language != language
                            || c.Version != row.Version || c.SceneId != row.SceneId || c.TopicId != row.TopicId
                            || c.Unit is null || row.Operation != (int)c.Operation || row.Structure != (int)c.Structure
                            || row.Grade != c.Grade || row.KnowledgeGroup != (int)c.KnowledgeGroup) continue;
                        if (profile is not null && (c.Story?.ChartProfile is not { } chart
                            || !IQuestionBankStore.ChartProfilesMatch(chart, profile))) continue;
                        bool userAuthored = IsUserAuthored(db, row.Hash);
                        var validation = ValidateDraft(row.DraftJson, c, userAuthored);
                        if (!validation.IsValid) continue;
                        db.Execute("UPDATE BasicQuestionBank SET UseCount=UseCount+1,LastUsedUtc=? WHERE Hash=?", DateTime.UtcNow, row.Hash);
                        history.Enqueue(c.SceneId);
                        if (history.Count > 32) history.Dequeue();
                        return new(c, validation.Draft!, row.RawJson, row.ModelName, row.CreatedUtc) { UserAuthored = userAuthored };
                    }
                    catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
                    { Debug.WriteLine($"Reasoning bank row skipped: {error.GetType().Name}"); }
                }
                return null;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception error) when (error is SQLiteException or IOException or UnauthorizedAccessException)
        { Debug.WriteLine($"Reasoning bank unavailable: {error.GetType().Name}"); return null; }
    }
}
