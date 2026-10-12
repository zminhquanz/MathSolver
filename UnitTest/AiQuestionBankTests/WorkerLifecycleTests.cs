using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;

internal static class WorkerLifecycleTests
{
    internal static async Task RunAsync()
    {
        var memoryStore = new MemoryStore();
        var fake = new FakeRuntime(prompt =>
        {
            string marker = prompt.Contains("\nJSON: ") ? "\nJSON: " : "Correct role example: ";
            return prompt.Split(marker)[1].Split('\n')[0];
        });
        var service = new AiQuestionGenerationService(fake, memoryStore);
        service.Start(new(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese, 3, false));
        await service.Completion;
        Check(service.Snapshot.State == AiJobState.Completed && service.Snapshot.Items.Count == 3 && memoryStore.Saved == 0, "Manual batch auto-saved or failed.");
        await service.InsertAsync(2);
        Check(memoryStore.Saved == 1 && service.Snapshot.Items[1].State == AiItemState.Saved, "Manual insert failed.");
        service.Start(new(ArithmeticOperation.Multiply, CurriculumTier.TwoStars, AppLanguage.Vietnamese, 2, true));
        await service.Completion;
        Check(memoryStore.Saved == 3 && service.Snapshot.Items.All(i => i.State == AiItemState.Saved), "Auto batch failed.");
        var invalid = new FakeRuntime(_ => "{}");
        service = new(invalid, memoryStore);
        service.Start(new(ArithmeticOperation.Subtract, CurriculumTier.OneStar, AppLanguage.Vietnamese, 10, true));
        await service.Completion;
        Check(invalid.Calls == 3 && service.Snapshot.State == AiJobState.Failed && memoryStore.Saved == 3, "Retry bound/save isolation failed.");
        var blocking = new BlockingRuntime();
        service = new(blocking, memoryStore);
        service.Start(new(ArithmeticOperation.Divide, CurriculumTier.OneStar, AppLanguage.Vietnamese, 10, true));
        await blocking.Started.Task;
        service.Stop();
        await service.Completion;
        Check(service.Snapshot.State == AiJobState.Stopped && memoryStore.Saved == 3, "Cancelled output saved.");
        var retryStore = new FailingStore();
        service = new(fake, retryStore);
        service.Start(new(ArithmeticOperation.Add, CurriculumTier.OneStar, AppLanguage.Vietnamese, 3, true));
        await service.Completion;
        Check(service.Snapshot.State == AiJobState.Failed && service.Snapshot.Items.Count == 1
            && service.Snapshot.Items[0].State == AiItemState.SaveFailed && service.Snapshot.Items[0].Question is not null,
            "Failed insertion discarded the valid preview or continued the batch.");
        await service.InsertAsync(1);
        Check(service.Snapshot.Items[0].State == AiItemState.Saved && retryStore.Attempts == 2, "Manual save recovery failed.");
        Console.WriteLine("PASS batch, manual/auto insertion, three-attempt limit and cancellation");
    }
}
