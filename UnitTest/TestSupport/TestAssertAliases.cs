namespace MathSolver.Testing;

/// <summary>Keep existing readable assertions while sharing one failure implementation.</summary>
public static class TestAssertAliases
{
    public static void Check(bool condition, string message) => TestAssert.That(condition, message);
}
