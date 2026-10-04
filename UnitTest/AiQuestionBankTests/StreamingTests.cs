using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;

internal static class StreamingTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    public static async Task RunAsync(Func<BasicQuestionContract, string> prose)
    {
        CheckPreviewReader();
        var runtime = new StreamingRuntime(prose);
        var store = new RecordingStore();
        var service = new AiQuestionGenerationService(runtime, store);
        service.Start(new(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese, 1, true));
        await runtime.PartialReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var initial = service.Snapshot;
        Check(initial.IsRunning && initial.Items[0].Attempts.Count == 1
            && !initial.Items[0].Attempts[0].IsComplete && initial.Items[0].Attempts[0].RawJson.Length > 0
            && initial.Items[0].Question is null && store.Saved == 0,
            "Stream did not expose partial JSON before completion, or partial output was saved.");
        var initialAttempt = initial.Items[0].Attempts[0];
        Check(StreamingQuestionPreview.Read(initialAttempt.RawJson).Length > 0
            && initialAttempt.Metrics?.GeneratedTokens == 2 && initialAttempt.Metrics.TokensPerSecond == 4,
            "Live prose or token metrics were unavailable before completion; empty token output was not counted.");
        Action<string> staleCallback = runtime.Callback!;
        runtime.Finish.TrySetResult();
        await service.Completion;
        var complete = service.Snapshot;
        Check(complete.State == AiJobState.Completed && store.Saved == 1
            && complete.Items[0].Attempts.Count == 1 && complete.Items[0].Attempts[0].IsComplete
            && complete.Items[0].Attempts[0].RawJson == prose(complete.Items[0].Contract),
            "Final stream was truncated, duplicated or not validated before saving.");
        Check(!initial.Items[0].Attempts[0].IsComplete, "Streaming mutated an earlier snapshot.");
        Check(complete.Items[0].Attempts[0].Metrics?.GeneratedTokens == runtime.Tokens
            && complete.Items[0].Attempts[0].Metrics?.TokensPerSecond == 4
            && initialAttempt.Metrics?.GeneratedTokens == 2, "Final token metrics were lost or mutated past snapshots.");
        staleCallback("late text");
        runtime.MetricsCallback!(new(999, TimeSpan.FromSeconds(1)));
        Check(ReferenceEquals(complete, service.Snapshot), "Late stream text changed a finished job.");

        runtime = new(prose, rejectFirst: true);
        store = new();
        service = new(runtime, store);
        service.Start(new(ArithmeticOperation.Subtract, CurriculumTier.OneStar, AppLanguage.Vietnamese, 1, true));
        await runtime.PartialReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var retry = service.Snapshot.Items[0];
        Check(retry.Attempts.Count == 2 && retry.Attempts[0].IsComplete && retry.Attempts[0].ErrorCode == "InvalidFields"
            && retry.Error == "InvalidFields" && !retry.Attempts[1].IsComplete && store.Saved == 0,
            "Retry did not retain the precise validation error and separate streamed attempts.");
        Check(retry.Attempts[0].Metrics?.GeneratedTokens == 1 && retry.Attempts[0].Metrics?.TokensPerSecond is null
            && retry.Attempts[1].Metrics?.GeneratedTokens == 2, "Retry token counts or speed did not reset.");
        runtime.Finish.TrySetResult();
        await service.Completion;
        Check(service.Snapshot.State == AiJobState.Completed && service.Snapshot.Items[0].Error is null
            && store.Saved == 1 && service.Snapshot.Items[0].Attempts.Count == 2,
            "Successful retry kept a stale error or lost its attempt history.");

        runtime = new(prose);
        store = new();
        service = new(runtime, store);
        service.Start(new(ArithmeticOperation.Divide, CurriculumTier.OneStar, AppLanguage.English, 1, true));
        await runtime.PartialReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
        service.Stop();
        await service.Completion;
        var stopped = service.Snapshot;
        Check(stopped.State == AiJobState.Stopped && !stopped.IsRunning && store.Saved == 0
            && stopped.Items[0].Attempts[0].RawJson == runtime.Partial
            && !stopped.Items[0].Attempts[0].IsComplete,
            "Stopping lost buffered JSON, marked it valid, or inserted it into SQLite.");
        runtime.Callback!("late text");
        runtime.MetricsCallback!(new(999, TimeSpan.FromSeconds(1)));
        Check(ReferenceEquals(stopped, service.Snapshot), "Late stream text changed a stopped job.");

        runtime = new(prose, rejectAll: true);
        store = new();
        service = new(runtime, store);
        service.Start(new(ArithmeticOperation.Multiply, CurriculumTier.OneStar, AppLanguage.Vietnamese, 2, true));
        await service.Completion;
        var failed = service.Snapshot;
        Check(failed.State == AiJobState.Failed && !failed.IsRunning && failed.Error == "RetriesExhausted"
            && failed.Items.Count == 1 && failed.Items[0].Error == "InvalidFields" && store.Saved == 0
            && failed.Items[0].Attempts.Count == 3
            && failed.Items[0].Attempts.All(a => a.IsComplete && a.RawJson == "{}" && a.ErrorCode == "InvalidFields"),
            "Rejected streaming output lost its concrete error, exceeded three attempts, or was saved.");
        runtime = new(prose, finishNormallyOnStop: true);
        store = new();
        service = new(runtime, store);
        service.Start(new(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese, 1, true));
        await runtime.PartialReady.Task.WaitAsync(TimeSpan.FromSeconds(5));
        service.Stop();
        await service.Completion;
        Check(service.Snapshot.State == AiJobState.Stopped && store.Saved == 0
            && service.Snapshot.Items[0].Attempts[0].RawJson == runtime.Partial,
            "Cancellation between native token yields lost the final buffer or saved an unvalidated question.");
        Console.WriteLine("PASS live prose/JSON, escaped Unicode prefixes, token metrics, retry resets, final flush, cancellation and no partial insertion");
    }

    private static void CheckPreviewReader()
    {
        const string json = """{"given_a":"An c\u00f3 \"sách\".\nDòng hai.","given_b":"Emoji \uD83D\uDE00 và \\ đường dẫn.","question":"Hỏi bao nhiêu?"}""";
        const string expected = "An có \"sách\".\nDòng hai. Emoji 😀 và \\ đường dẫn. Hỏi bao nhiêu?";
        Check(StreamingQuestionPreview.Read(json) == expected, "Complete display prose did not decode JSON escapes.");
        for (int length = 0; length <= json.Length; length++)
        {
            string preview = StreamingQuestionPreview.Read(json[..length]);
            Check(expected.StartsWith(preview, StringComparison.Ordinal)
                && (preview.Length == 0 || !char.IsHighSurrogate(preview[^1])), "Partial Unicode/escape output leaked JSON or a surrogate half.");
        }
        Check(StreamingQuestionPreview.Read("""{"question":"Q?","given_b":"B.","given_a":"A."}""") == "A. B. Q?",
            "Reordered JSON clauses changed preview order.");
        Check(StreamingQuestionPreview.Read("""{"metadata":"hidden","given_a":"An có""") == "An có"
            && StreamingQuestionPreview.Read("""{"given_a":"A.","given_b":123}""") == "A."
            && StreamingQuestionPreview.Read(null) == "" && StreamingQuestionPreview.Read("not JSON") == "",
            "Preview exposed metadata or could not handle malformed/non-string fields.");
        Check(new AiGenerationMetrics(1, TimeSpan.Zero).TokensPerSecond is null
            && new AiGenerationMetrics(2, TimeSpan.Zero).TokensPerSecond is null,
            "Token speed divided by zero or included first-token prompt latency.");
    }

    private sealed class StreamingRuntime(Func<BasicQuestionContract, string> prose, bool rejectFirst = false,
        bool rejectAll = false, bool finishNormallyOnStop = false) : IQuestionTextRuntime
    {
        public bool IsLoaded => true;
        public string ModelName => "streaming-test";
        public TaskCompletionSource PartialReady { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Finish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Action<string>? Callback { get; private set; }
        public Action<AiGenerationMetrics>? MetricsCallback { get; private set; }
        public int Tokens { get; private set; }
        public string Partial { get; private set; } = "";
        private int _calls;
        public async Task<string> GenerateAsync(BasicQuestionContract contract, string prompt, CancellationToken cancellationToken,
            Action<string>? onText = null, Action<AiGenerationMetrics>? onMetrics = null)
        {
            _calls++;
            if (rejectAll || rejectFirst && _calls == 1)
            { onMetrics?.Invoke(new(1, TimeSpan.Zero)); onText?.Invoke("{}"); return "{}"; }
            string json = prose(contract);
            Callback = onText;
            MetricsCallback = onMetrics;
            void Emit(string text)
            {
                Tokens++;
                onMetrics?.Invoke(new(Tokens, TimeSpan.FromSeconds((Tokens - 1) * 0.25)));
                onText?.Invoke(text);
            }
            Partial = json[..20];
            Emit(Partial);
            await Task.Delay(110, cancellationToken);
            // UTF-8 byte tokens can produce no text; they still contribute to throughput.
            Emit("");
            PartialReady.TrySetResult();
            try { await Finish.Task.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) when (finishNormallyOnStop)
            {
                Partial += "tail";
                return Partial;
            }
            // All remaining deltas arrive quickly; final output must flush the throttled tail.
            foreach (char character in json[20..]) Emit(character.ToString());
            return json;
        }
    }

    private sealed class RecordingStore : IQuestionBankStore
    {
        public int Saved { get; private set; }
        public Task<bool> InsertAsync(ValidatedBankQuestion question, CancellationToken cancellationToken = default)
        { Saved++; return Task.FromResult(true); }
        public Task<ValidatedBankQuestion?> TakeAsync(ArithmeticOperation operation, CurriculumTier tier,
            AppLanguage language, CancellationToken cancellationToken = default)
            => Task.FromResult<ValidatedBankQuestion?>(null);
    }
}
