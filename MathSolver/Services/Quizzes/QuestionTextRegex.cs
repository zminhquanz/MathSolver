using System.Text.RegularExpressions;

namespace MathSolver.Services;

/// <summary>Linear matching for short prose; leave time for a cold, busy UI process to be scheduled.</summary>
internal static class QuestionTextRegex
{
    internal const int MaximumClauseLength = 1400;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);
    internal static Regex Create(string pattern) => new(pattern,
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, Timeout);

    internal static string TimeoutDetails(RegexMatchTimeoutException error, string stage) =>
        $"stage={stage}; input-length={error.Input?.Length ?? 0}; timeout-ms={error.MatchTimeout.TotalMilliseconds}; pattern={error.Pattern}";
}
