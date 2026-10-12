internal static class ModelHarnessTests
{
    internal static async Task RunAsync()
    {
        using var workspace = TestWorkspace.Temporary("ModelLoadFailure");
        var session = new ModelTestSession();
        await using (session)
        {
            await TestAssert.ThrowsAsync<TestUsageException>(
                () => session.LoadAsync(workspace.FilePath("missing.gguf")), "Missing GGUF file accepted.");
            Check(!session.Runtime.IsLoaded && !session.Runtime.CanGenerate, "Failed load retained model state.");
        }
        // Explicit and scope-based cleanup can both occur in evidence tests.
        await session.DisposeAsync();
        Check(!session.Runtime.IsLoaded && !session.Runtime.CanGenerate, "Model teardown retained native state.");
    }
}
