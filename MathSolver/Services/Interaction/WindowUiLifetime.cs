using System.Runtime.CompilerServices;

namespace MathSolver.Services;

/// <summary>A teardown marker that can be queried without resolving MAUI services.</summary>
internal static class WindowUiLifetime
{
    private static readonly ConditionalWeakTable<Window, object> ClosingWindows = new();

    public static void BeginClose(Window window) =>
        ClosingWindows.GetValue(window, static _ => new object());

    public static bool IsClosing(Window? window) =>
        window is not null && ClosingWindows.TryGetValue(window, out _);
}
