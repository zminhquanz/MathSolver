using MathSolver.Models;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text;

namespace MathSolver.Services.QuestionBank;

public interface IQuestionTextRuntime
{
    bool IsLoaded { get; }
    bool CanGenerate => IsLoaded;
    string ModelName { get; }
    Task PrepareAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    Task ReleaseAsync() => Task.CompletedTask;
    // The optional callback receives text deltas synchronously, before generation completes.
    // Metrics, when available, are delivered before their corresponding text delta;
    // an empty delta can still represent a generated UTF-8 byte token.
    Task<string> GenerateAsync(BasicQuestionContract contract, string prompt, CancellationToken cancellationToken,
        Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null);
    Task<string> GenerateNovelAsync(BasicQuestionContract contract, string prompt, IReadOnlySet<string> excludedProse,
        CancellationToken cancellationToken, Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
        => GenerateAsync(contract, prompt, cancellationToken, onText, onMetrics);
}

public sealed record AiGenerationMetrics(int GeneratedTokens, TimeSpan GenerationElapsed)
{
    // The first token marks the end of prompt processing; subsequent tokens are timed.
    public double? TokensPerSecond => GeneratedTokens > 1 && GenerationElapsed.TotalSeconds > 0
        ? (GeneratedTokens - 1) / GenerationElapsed.TotalSeconds : null;
}

public enum AiJobState { Idle, Generating, Validating, Completed, Stopped, Failed }
public enum AiItemState { Generating, Validating, Ready, Saved, Duplicate, Rejected, Stopped, SaveFailed, PendingReview }
public sealed record AiQuestionAttempt(int Number, string Prompt, string RawJson, string? ErrorCode,
    bool IsComplete = true, AiGenerationMetrics? Metrics = null, string? ErrorDetails = null);
public sealed record AiQuestionItem(int Number, BasicQuestionContract Contract, AiItemState State,
    IReadOnlyList<AiQuestionAttempt> Attempts, ValidatedBankQuestion? Question = null, string? Error = null,
    PendingQuestionReview? Review = null);
public sealed record AiJobSnapshot(AiJobState State, AiGenerationOptions? Options,
    IReadOnlyList<AiQuestionItem> Items, string? Error = null)
{
    public bool IsRunning => State is AiJobState.Generating or AiJobState.Validating;
}

/// <summary>App-owned worker; pages subscribe to immutable snapshots and never own cancellation.</summary>
public sealed class AiQuestionGenerationService(IQuestionTextRuntime runtime, IQuestionBankStore store)
{
    private readonly object _sync = new();
    private readonly AdditionQuestionCycle _additionCycle = new();
    private readonly ArithmeticQuestionCycle _arithmeticCycle = new();
    private readonly AppliedQuestionCycle _appliedCycle = new();
    private readonly FindXQuestionCycle _findXCycle = new();
    private readonly FractionQuestionCycle _fractionCycle = new();
    private readonly ReasoningStoryCycle _storyCycle = new();
    private readonly ExpandedArithmeticCycle _expandedArithmeticCycle = new();
    private CancellationTokenSource? _cancellation;
    private Task _work = Task.CompletedTask;
    private int _pendingInserts;
    private bool _deletingAll;
    private AiJobSnapshot _snapshot = new(AiJobState.Idle, null, []);
    public event EventHandler? Changed;
    public AiJobSnapshot Snapshot { get { lock (_sync) return _snapshot; } }
    public Task Completion { get { lock (_sync) return _work; } }
    public bool IsRunning => Snapshot.IsRunning;
    public bool IsDeletingAll { get { lock (_sync) return _deletingAll; } }
    internal void ResolveReview(string id,bool saved)
    {
        lock(_sync)
            _snapshot=_snapshot with {Items=_snapshot.Items.Select(i=>i.Review is { } review && review.Id==id
                ? i with { State=saved?AiItemState.Saved:AiItemState.Rejected,Error=null,Review=null,
                    Question=saved?new ValidatedBankQuestion(review.Contract,review.Draft,review.RawJson,review.ModelName,review.CreatedUtc){UserAuthored=true}:null }
                : i).ToArray()};
        Notify();
    }

    public void Start(AiGenerationOptions options)
    {
        options.Validate();
        lock (_sync)
        {
            if (_snapshot.IsRunning || _pendingInserts != 0 || _deletingAll) throw new InvalidOperationException("JobAlreadyRunning");
            if (!runtime.CanGenerate) throw new InvalidOperationException("ModelNotLoaded");
            _cancellation?.Dispose();
            _cancellation = new();
            _snapshot = new(AiJobState.Generating, options, []);
            // The entire job, including prompt construction/validation, stays off the UI thread.
            _work = Task.Run(() => RunAsync(options, _cancellation.Token));
        }
        Notify();
    }

    public void Stop() { lock (_sync) _cancellation?.Cancel(); }

    public async Task InsertAsync(int itemNumber)
    {
        AiQuestionItem item;
        lock (_sync)
        {
            if (_deletingAll) throw new InvalidOperationException("JobAlreadyRunning");
            item = _snapshot.Items.Single(i => i.Number == itemNumber);
            if (item.State is not (AiItemState.Ready or AiItemState.SaveFailed) || item.Question is null) return;
            _pendingInserts++;
        }
        try
        {
            bool inserted = await store.InsertAsync(item.Question).ConfigureAwait(false);
            // Merge the state into the latest snapshot; generation may have appended attempts/items.
            UpdateItem(itemNumber, i => i with { State = inserted ? AiItemState.Saved : AiItemState.Duplicate,
                Error = inserted ? null : "DuplicateProse" });
        }
        catch (Exception error)
        {
            UpdateItem(itemNumber, i => i with { State = AiItemState.SaveFailed, Error = error.GetType().Name });
        }
        finally { lock (_sync) _pendingInserts--; }
    }

    public async Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_snapshot.IsRunning || _pendingInserts != 0 || _deletingAll)
                throw new InvalidOperationException("JobAlreadyRunning");
            _deletingAll = true;
        }
        try
        {
            Notify();
            int deleted = await store.DeleteAllAsync(cancellationToken).ConfigureAwait(false);
            lock (_sync)
                _snapshot = _snapshot with { Items = _snapshot.Items.Select(i =>
                    i.State==AiItemState.PendingReview ? i with {State=AiItemState.Rejected,Review=null,Error=null}
                    : i.Question is not null && i.State is AiItemState.Saved or AiItemState.Duplicate
                        ? i with { State = AiItemState.Ready, Error = null } : i).ToArray() };
            return deleted;
        }
        finally
        {
            lock (_sync) _deletingAll = false;
            Notify();
        }
    }

    private async Task RunAsync(AiGenerationOptions options, CancellationToken cancellationToken)
    {
        AiJobState finalState = AiJobState.Failed;
        string? finalError = null;
        try
        {
            if (cancellationToken.IsCancellationRequested) finalState = AiJobState.Stopped;
            else
            {
                await runtime.PrepareAsync(cancellationToken).ConfigureAwait(false);
                (finalState, finalError) = cancellationToken.IsCancellationRequested
                    ? (AiJobState.Stopped, null)
                    : await RunQuestionsAsync(options, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            finalState = AiJobState.Stopped;
        }
        catch (Exception error)
        {
            finalError = error is RegexMatchTimeoutException ? "TextProcessingTimeout" : error.GetType().Name + ": " + error.Message;
            lock (_sync)
                _snapshot = _snapshot with {
                    Items = _snapshot.Items.Select(i => i.Question is null && i.State is AiItemState.Generating or AiItemState.Validating
                        ? i with { State = AiItemState.Rejected, Error = finalError } : i).ToArray() };
        }
        finally
        {
            // Both cooperative returns and cancellation from runtime/store APIs
            // retain partial output and mark unfinished items consistently.
            if (finalState == AiJobState.Stopped)
                lock (_sync)
                    _snapshot = _snapshot with {
                        Items = _snapshot.Items.Select(i => i.Question is null && i.State is not (AiItemState.Rejected or AiItemState.PendingReview)
                            ? i with { State = AiItemState.Stopped } : i).ToArray() };
            // Release once per single/batch job, including load failure and cancellation.
            // Keep the job running until cleanup finishes so model management/new jobs
            // cannot race native disposal. Cancellation must not skip cleanup.
            try { await runtime.ReleaseAsync().ConfigureAwait(false); }
            catch (Exception error)
            {
                finalState = AiJobState.Failed;
                finalError = (finalError is null ? "" : finalError + "\n") + error.GetType().Name + ": " + error.Message;
            }
            SetState(finalState, finalError);
        }
    }

    private async Task<(AiJobState State, string? Error)> RunQuestionsAsync(AiGenerationOptions options, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return (AiJobState.Stopped, null);
        var batchProse = new HashSet<string>(StringComparer.Ordinal);
        var excludedProse = new HashSet<string>(await store.GetProseHashesAsync(cancellationToken).ConfigureAwait(false), StringComparer.Ordinal);
        if (cancellationToken.IsCancellationRequested) return (AiJobState.Stopped, null);
        if(options.CreativeProse) excludedProse.UnionWith(await store.GetPendingProseHashesAsync(cancellationToken).ConfigureAwait(false));
        for (int number = 1; number <= options.Count; number++)
        {
            if (cancellationToken.IsCancellationRequested) return (AiJobState.Stopped, null);
            var contract = options.Family == BankQuestionFamily.Arithmetic
                && ExpandedArithmeticCatalogue.Supports(options.Profile?.Group ?? QuestionKnowledgeGroup.Objects, options.Tier)
                ? _expandedArithmeticCycle.Next(options.Profile ?? new(QuestionKnowledgeGroup.Objects), options.Operation, options.Tier, options.Language, excludedProse, options.CreativeProse)
                : ReasoningStoryCatalogue.Supports(options.Family)
                ? _storyCycle.Next(options.Family, options.StoryVariant, options.Tier, options.Language, excludedProse, options.CreativeProse)
                : options.Family == BankQuestionFamily.Fraction
                ? _fractionCycle.Next(options.Profile ?? new(QuestionKnowledgeGroup.Objects), options.Operation, options.Tier, options.Language)
                : options.Family == BankQuestionFamily.FindX
                ? _findXCycle.Next(options.Profile ?? new(QuestionKnowledgeGroup.Objects), options.Operation, options.Tier, options.Language, options.UnknownRole)
                : options.Profile is { } profile
                ? _appliedCycle.Next(profile, options.Operation, options.Tier, options.Language)
                : options.Operation == ArithmeticOperation.Add
                ? _additionCycle.Next(options.Tier, options.Language)
                : _arithmeticCycle.Next(options.Operation, options.Tier, options.Language);
            contract = contract with { OpenProse = options.CreativeProse && OpenQuestionProse.Supports(contract) };
            Append(new(number, contract, AiItemState.Generating, []));
            string? correction = null;
            string? errorDetails = null;
            var duplicateProse = new List<string>();
            PendingQuestionReview? pendingReview = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                if (cancellationToken.IsCancellationRequested) return (AiJobState.Stopped, null);
                SetState(AiJobState.Generating);
                string prompt = BasicQuestionPrompt.Build(contract, correction, duplicateProse, excludedProse, errorDetails);
                UpdateItem(number, i => i with { State = AiItemState.Generating,
                    Attempts = [.. i.Attempts, new(attempt, prompt, "", null, false)] });
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromMinutes(5));
                var partial = new StringBuilder();
                var refresh = Stopwatch.StartNew();
                AiGenerationMetrics? metrics = null;
                bool published = false, acceptingText = true;
                void OnText(string text)
                {
                    if (!acceptingText || timeout.IsCancellationRequested) return;
                    partial.Append(text);
                    if (partial.Length > 12_000) throw new InvalidDataException("ModelOutputTooLong");
                    // Publish the first chunk immediately, then at most ten updates/second.
                    if (published && refresh.ElapsedMilliseconds < 100) return;
                    UpdateAttempt(number, attempt, a => a with { RawJson = partial.ToString(), Metrics = metrics });
                    published = true;
                    refresh.Restart();
                }
                string raw;
                void OnMetrics(AiGenerationMetrics value)
                {
                    if (acceptingText && !timeout.IsCancellationRequested) metrics = value;
                }
                if (cancellationToken.IsCancellationRequested) return (AiJobState.Stopped, null);
                try { raw = await runtime.GenerateNovelAsync(contract, prompt, excludedProse, timeout.Token, OnText, OnMetrics).ConfigureAwait(false); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    UpdateAttempt(number, attempt, a => a with { RawJson = partial.ToString(), Metrics = metrics });
                    return (AiJobState.Stopped, null);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    correction = "GenerationTimeout";
                    errorDetails = null;
                    UpdateAttempt(number, attempt, a => a with { RawJson = partial.ToString(), ErrorCode = correction, IsComplete = true, Metrics = metrics });
                    UpdateItem(number, i => i with { Error = correction });
                    continue;
                }
                catch (RegexMatchTimeoutException error)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        UpdateAttempt(number, attempt, a => a with { RawJson = partial.ToString(), Metrics = metrics });
                        return (AiJobState.Stopped, null);
                    }
                    correction = "TextProcessingTimeout";
                    errorDetails = QuestionTextRegex.TimeoutDetails(error, "generation");
                    UpdateAttempt(number, attempt, a => a with { RawJson = partial.ToString(), ErrorCode = correction,
                        ErrorDetails = errorDetails, IsComplete = true, Metrics = metrics });
                    UpdateItem(number, i => i with { Error = correction });
                    continue;
                }
                catch
                {
                    UpdateAttempt(number, attempt, a => a with { RawJson = partial.ToString(), Metrics = metrics });
                    throw;
                }
                finally { acceptingText = false; }
                // Some executors finish normally when cancelled between token yields.
                // Retain their final buffered text without treating it as validated.
                UpdateAttempt(number, attempt, a => a with { RawJson = raw, Metrics = metrics });
                if (cancellationToken.IsCancellationRequested) return (AiJobState.Stopped, null);
                SetState(AiJobState.Validating);
                UpdateItem(number, i => i with { State = AiItemState.Validating,
                    Attempts = i.Attempts.Select(a => a.Number == attempt ? a with { RawJson = raw, Metrics = metrics } : a).ToArray() });
                if (cancellationToken.IsCancellationRequested) return (AiJobState.Stopped, null);
                var validation = BasicQuestionValidator.Validate(raw, contract);
                if (cancellationToken.IsCancellationRequested) return (AiJobState.Stopped, null);
                correction = validation.ErrorCode;
                errorDetails = validation.ErrorDetails;
                UpdateAttempt(number, attempt, a => a with { RawJson = raw, ErrorCode = correction, ErrorDetails = validation.ErrorDetails, IsComplete = true });
                UpdateItem(number, i => i with { Error = correction });
                if (!validation.IsValid)
                {
                    if (validation.NeedsReview)
                    {
                        var candidate=PendingQuestionReview.Create(contract,validation.Draft!,raw,runtime.ModelName,validation.ErrorDetails??"SemanticReviewRequired");
                        if(!excludedProse.Add(candidate.Id))
                        {
                            correction="DuplicateProse";duplicateProse.Add(validation.Draft!.ProblemText);
                            UpdateAttempt(number,attempt,a=>a with {ErrorCode=correction});
                        }
                        else
                        {
                            // Complete facts with inconclusive meaning go to review
                            // after this attempt; only invalid/duplicate output retries.
                            pendingReview=candidate;
                            break;
                        }
                    }
                    continue;
                }
                var question = new ValidatedBankQuestion(validation.Contract ?? contract, validation.Draft!, raw, runtime.ModelName, DateTime.UtcNow);
                string proseHash = QuestionProseIdentity.Hash(question.Contract, question.Draft);
                if (batchProse.Contains(proseHash) || contract.OpenProse && excludedProse.Contains(proseHash)
                    || await store.ContainsProseAsync(question, cancellationToken).ConfigureAwait(false))
                {
                    RejectDuplicate();
                    continue;
                }
                if (cancellationToken.IsCancellationRequested) return (AiJobState.Stopped, null);
                UpdateItem(number, i => i with { State = AiItemState.Ready, Question = question, Contract = question.Contract });
                if (options.AutoInsert)
                {
                    if (cancellationToken.IsCancellationRequested) return (AiJobState.Stopped, null);
                    await InsertAsync(number).ConfigureAwait(false);
                    if (Snapshot.Items.Single(i => i.Number == number).State == AiItemState.SaveFailed)
                    { return (AiJobState.Failed, "DatabaseSaveFailed"); }
                    if (Snapshot.Items.Single(i => i.Number == number).State == AiItemState.Duplicate)
                    {
                        // A different writer may insert the wording after the precheck.
                        RejectDuplicate();
                        continue;
                    }
                }
                batchProse.Add(proseHash);
                excludedProse.Add(proseHash);
                break;

                void RejectDuplicate()
                {
                    correction = "DuplicateProse";
                    errorDetails = null;
                    excludedProse.Add(proseHash);
                    duplicateProse.Add(question.Draft.ProblemText);
                    UpdateAttempt(number, attempt, a => a with { ErrorCode = correction });
                    UpdateItem(number, i => i with { State = AiItemState.Duplicate, Question = null, Error = correction });
                }
            }
            if (cancellationToken.IsCancellationRequested) return (AiJobState.Stopped, null);
            var result = Snapshot.Items.Single(i => i.Number == number);
            if (result.Question is null)
            {
                if (pendingReview is not null)
                {
                    // Quarantined drafts live in a separate table and never enter
                    // the Ready/Saved path, even when automatic saving is enabled.
                    bool queued = await store.QueueReviewAsync(pendingReview,cancellationToken).ConfigureAwait(false);
                    if (!queued)
                    {
                        if (cancellationToken.IsCancellationRequested) return (AiJobState.Stopped, null);
                        UpdateItem(number,i=>i with {State=AiItemState.Duplicate,Error="DuplicateProse"});
                        return (AiJobState.Failed,"DuplicateProseRetriesExhausted");
                    }
                    UpdateItem(number,i=>i with { State=AiItemState.PendingReview,Review=pendingReview,Error="SemanticReviewRequired" });
                    batchProse.Add(pendingReview.Id);
                    excludedProse.Add(pendingReview.Id);
                    continue;
                }
                UpdateItem(number, i => i with { State = correction == "DuplicateProse" ? AiItemState.Duplicate : AiItemState.Rejected, Error = correction });
                return (AiJobState.Failed, correction == "DuplicateProse" ? "DuplicateProseRetriesExhausted" : "RetriesExhausted");
            }
        }
        return (cancellationToken.IsCancellationRequested ? AiJobState.Stopped : AiJobState.Completed, null);
    }

    private void Append(AiQuestionItem item)
    { lock (_sync) _snapshot = _snapshot with { Items = [.. _snapshot.Items, item] }; Notify(); }
    private void UpdateItem(int number, Func<AiQuestionItem, AiQuestionItem> update)
    { lock (_sync) _snapshot = _snapshot with { Items = _snapshot.Items.Select(i => i.Number == number ? update(i) : i).ToArray() }; Notify(); }
    private void UpdateAttempt(int number, int attempt, Func<AiQuestionAttempt, AiQuestionAttempt> update)
        => UpdateItem(number, i => i with { Attempts = i.Attempts.Select(a => a.Number == attempt ? update(a) : a).ToArray() });
    private void SetState(AiJobState state, string? error = null)
    { lock (_sync) _snapshot = _snapshot with { State = state, Error = error }; Notify(); }
    private void Notify()
    {
        // A closed/recreated page or a diagnostics listener must not break the worker.
        foreach (EventHandler listener in Changed?.GetInvocationList() ?? [])
            try { listener(this, EventArgs.Empty); } catch { }
    }
}
