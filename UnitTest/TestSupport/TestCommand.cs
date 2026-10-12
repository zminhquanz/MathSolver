namespace MathSolver.Testing;

public sealed record TestCommand(string[] Names, string Usage, Func<string[], Task> Execute,
    Func<string[], bool>? ValidateArguments = null);

public sealed class TestUsageException(string message) : Exception(message);

public static class TestCommandRunner
{
    public static async Task<int> RunAsync(string[] args, IReadOnlyList<TestCommand> commands, Func<Task> defaultSuite)
    {
        var lookup = new Dictionary<string, TestCommand>(StringComparer.Ordinal);
        foreach (var command in commands)
        foreach (string name in command.Names)
            if (!lookup.TryAdd(name, command)) throw new InvalidOperationException("Duplicate command: " + name);

        if (args is ["--help"] or ["--list"])
        {
            Console.WriteLine("No arguments: run the complete offline suite. Model commands require an explicit GGUF path.");
            foreach (var command in commands.OrderBy(c => c.Names[0])) Console.WriteLine(command.Usage);
            return 0;
        }
        if (args.Length > 0 && !lookup.ContainsKey(args[0]))
        {
            Console.Error.WriteLine("Unknown test command: " + args[0] + ". Use --list.");
            return 2;
        }
        try
        {
            if (args.Length == 0) await defaultSuite();
            else
            {
                var command = lookup[args[0]];
                if (command.ValidateArguments?.Invoke(args) == false)
                    throw new TestUsageException("Usage: " + command.Usage);
                await command.Execute(args);
            }
            return 0;
        }
        catch (TestUsageException error) { Console.Error.WriteLine(error.Message); return 2; }
        catch (Exception error) { Console.Error.WriteLine($"FAIL {args.FirstOrDefault() ?? "offline suite"}: {error}"); return 1; }
    }
}
