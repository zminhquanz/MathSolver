using MathSolver.Testing;
internal static partial class AiTestCommands
{
    internal static IReadOnlyList<TestCommand> All { get; } = [.. Offline(), .. Model(), .. Evidence()];
}