using MathSolver.Services.QuestionBank;
using System.Runtime.ExceptionServices;

internal static class SqlWriteConfirmationTests
{
    public static async Task RunAsync(string directory, ValidatedBankQuestion sample)
    {
        var store = new QuestionBankStore(Path.Combine(directory, "sql-confirmation.db3"));
        Check(await store.InsertAsync(sample), "Confirmation test seed failed.");
        Func<string, Func<Task<bool>>, CancellationToken, Task<BankQueryResult>> query = store.QueryWithConfirmationAsync;
        int prompts = 0;
        Task<bool> Reject() { prompts++; return Task.FromResult(false); }
        Task<bool> Accept() { prompts++; return Task.FromResult(true); }
        const string insert = "INSERT INTO BasicQuestionBank(Hash, ModelName) VALUES('confirmed-row', 'new') RETURNING Hash";
        const string update = "/* preview */ WITH c AS (SELECT 'confirmed-row' AS h) UPDATE BasicQuestionBank SET ModelName='edited' WHERE Hash IN (SELECT h FROM c) RETURNING ModelName";
        const string delete = "DELETE FROM BasicQuestionBank WHERE Hash='confirmed-row' RETURNING Hash";
        string[] writes = [insert, update, delete];
        foreach (string sql in new[] { "SELECT 'DELETE UPDATE INSERT'", "WITH c AS (SELECT 1) SELECT * FROM c" })
            Check((await query(sql, Reject, CancellationToken.None)).IsSuccess, "Read-only inquiry failed.");
        Check(prompts == 0, "Read-only SQL must not show write confirmations.");
        Check(!(await query("UPDATE MissingTable SET Missing=1", Accept, CancellationToken.None)).IsSuccess && prompts == 0,
            "Invalid SQL should report its error before prompting.");
        int cancellationExceptions = 0;
        EventHandler<FirstChanceExceptionEventArgs> observe = (_, e) =>
        {
            if (e.Exception is OperationCanceledException && e.Exception.TargetSite?.Module.Assembly == typeof(QuestionBankStore).Assembly)
                Interlocked.Increment(ref cancellationExceptions);
        };
        AppDomain.CurrentDomain.FirstChanceException += observe;
        try
        {
            foreach (string sql in writes)
            {
                // Declining the dialog must also be quiet for a debugger that
                // breaks on thrown exceptions, even if one is caught internally.
                var declined = await query(sql, Reject, CancellationToken.None);
                Check(declined.ConfirmationDeclined && !declined.IsSuccess && declined.ErrorCode is null && declined.AffectedRows == 0 && declined.Rows.Count == 0,
                    "Rejected SQL must return without an error or any execution: " + sql);
            }
        }
        finally { AppDomain.CurrentDomain.FirstChanceException -= observe; }
        Check(cancellationExceptions == 0, "Declining a confirmation threw a first-chance cancellation exception.");
        Check(prompts == 3 && (await store.QueryAsync("SELECT COUNT(*) FROM BasicQuestionBank")).Rows[0][0] == "1",
            "Rejected insert/update/delete changed the database.");
        Console.WriteLine("PASS declined INSERT/UPDATE/DELETE: no first-chance cancellation exceptions and no database changes");
        foreach (string sql in writes)
        {
            var result = await query(sql, Accept, CancellationToken.None);
            Check(result.IsSuccess && result.IsWrite && result.AffectedRows == 1,
                "Confirmed SQL did not execute exactly once: " + sql);
        }
        Check(prompts == 6 && (await store.QueryAsync("SELECT COUNT(*) FROM BasicQuestionBank")).Rows[0][0] == "1",
            "Confirmation callback count or final database state is wrong.");

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var pending = query("UPDATE BasicQuestionBank SET ModelName='not-approved'", async () =>
        {
            entered.TrySetResult();
            return await answer.Task;
        }, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Check((await store.QueryAsync("SELECT ModelName FROM BasicQuestionBank")).Rows[0][0] == sample.ModelName,
            "SQL executed before the confirmation was answered.");
        cancellation.Cancel(); answer.TrySetResult(true);
        bool wasCancelled = false;
        try { await pending; } catch (OperationCanceledException) { wasCancelled = true; }
        Check(wasCancelled && (await store.QueryAsync("SELECT ModelName FROM BasicQuestionBank")).Rows[0][0] == sample.ModelName,
            "Late approval bypassed cancellation or closing the data page.");

        var delayed = await query("UPDATE BasicQuestionBank SET ModelName='approved-after-wait'", async () =>
        {
            await Task.Delay(3200);
            return true;
        }, CancellationToken.None);
        Check(delayed.IsSuccess && delayed.AffectedRows == 1,
            "Time spent waiting for the user must not consume the SQL execution timeout.");
        Console.WriteLine("SQL confirmation: rejected/accepted INSERT, UPDATE, DELETE, CTE, RETURNING, pending response and cancellation verified.");
    }

    private static void Check(bool condition, string message) => MathSolver.Testing.TestAssert.That(condition, message);
}
