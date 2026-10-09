using System.Collections.Concurrent;

namespace MathSolver.Services;

/// <summary>Append new situations without changing the seed replay of existing SQLite templates.</summary>
internal static class NarrativeContextExpansion
{
    internal const int Version = 2;
    internal static IReadOnlyList<T> Load<T>(string name, AppLanguage language, bool expanded)
        => expanded ? Combined<T>.Values.GetOrAdd((name, language), key =>
            QuizContentCatalog.LoadList<T>(key.Item1, QuizContentCatalog.Culture(key.Item2))
                .Concat(QuizContentCatalog.LoadList<T>(key.Item1 + ".Extensions", QuizContentCatalog.Culture(key.Item2))).ToArray())
            : QuizContentCatalog.LoadList<T>(name, QuizContentCatalog.Culture(language));

    private static class Combined<T>
    {
        internal static readonly ConcurrentDictionary<(string, AppLanguage), IReadOnlyList<T>> Values = new();
    }
}
