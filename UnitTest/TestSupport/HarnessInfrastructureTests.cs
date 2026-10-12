namespace MathSolver.Testing;

public static class HarnessInfrastructureTests
{
    public static async Task RunAsync()
    {
        using var neighbour = TestWorkspace.Temporary("Neighbour");
        File.WriteAllText(neighbour.FilePath("keep.txt"), "keep");
        string owned;
        using (var workspace = TestWorkspace.Temporary("Cleanup"))
        {
            owned = workspace.DirectoryPath;
            Directory.CreateDirectory(Path.Combine(owned, "nested"));
            File.WriteAllText(Path.Combine(owned, "nested", "test.txt"), "test");
            TestAssert.Throws<ArgumentException>(() => workspace.FilePath("../escape.db3"), "Traversal accepted");
        }
        TestAssert.That(!Directory.Exists(owned) && File.Exists(neighbour.FilePath("keep.txt")), "Workspace ownership/cleanup broken");
        TestAssert.Throws<ArgumentException>(() => TestWorkspace.Temporary("../escape"), "Invalid workspace label accepted");
        TestAssert.Throws<TestAssertionException>(() => TestAssert.That(false, "expected"), "Assertion failure swallowed");

        int runs = 0;
        TestCommand[] commands = [new(["--fixture", "--alias"], "--fixture <value>", _ => { runs++; return Task.CompletedTask; }, a => a.Length == 2)];
        // Capture expected usage errors so successful suites do not look like failures.
        var original = Console.Error;
        using var diagnostics = new StringWriter();
        Console.SetError(diagnostics);
        try
        {
            TestAssert.That(await TestCommandRunner.RunAsync(["--unknown"], commands, () => { runs++; return Task.CompletedTask; }) == 2, "Unknown command ran suite");
            TestAssert.That(await TestCommandRunner.RunAsync(["--fixture"], commands, () => Task.CompletedTask) == 2 && runs == 0, "Bad arity executed command");
            TestAssert.That(await TestCommandRunner.RunAsync(["--alias", "value"], commands, () => Task.CompletedTask) == 0 && runs == 1, "Alias rejected");
            TestAssert.That(await TestCommandRunner.RunAsync([], commands, () => { runs++; return Task.CompletedTask; }) == 0 && runs == 2, "Default suite missing");
            TestCommand[] failure = [new(["--fail"], "--fail", _ => throw new TestAssertionException("expected"))];
            TestAssert.That(await TestCommandRunner.RunAsync(["--fail"], failure, () => Task.CompletedTask) == 1, "Failure exit code lost");
        }
        finally { Console.SetError(original); }
    }
}
