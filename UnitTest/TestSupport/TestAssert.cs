namespace MathSolver.Testing;

public sealed class TestAssertionException(string message) : Exception(message);

public static class TestAssert
{
    public static void That(bool condition, string message)
    {
        if (!condition) throw new TestAssertionException(message);
    }

    public static T Throws<T>(Action action, string message) where T : Exception
    {
        try { action(); }
        catch (T error) { return error; }
        throw new TestAssertionException(message);
    }

    public static async Task<T> ThrowsAsync<T>(Func<Task> action, string message) where T : Exception
    {
        try { await action(); }
        catch (T error) { return error; }
        throw new TestAssertionException(message);
    }
}
