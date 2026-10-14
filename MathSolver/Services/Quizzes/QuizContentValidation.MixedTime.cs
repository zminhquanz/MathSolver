using System.Text.Json;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    internal static void CheckMixedTimeLists(QuizContentPack pack)
    {
        if (pack.Lists.TryGetValue("MixedTimePracticeProfiles", out var profiles))
        {
            var stars = new HashSet<int>();
            foreach (var row in profiles.EnumerateArray())
            {
                var p = row.Deserialize<MixedTimePracticeProfile>() ?? throw new InvalidDataException("Invalid mixed time profile.");
                if (!stars.Add(p.Star) || p.Star is < 1 or > 5 || p.MaximumMajor is < 1 or > 48 || p.MaximumFactor is < 2 or > 9
                    || p.UnitModes is not { Length: > 0 } || p.UnitModes.Distinct().Count() != p.UnitModes.Length
                    || p.UnitModes.Any(mode => mode is not ("hours-minutes" or "minutes-seconds" or "hours-minutes-seconds")))
                    throw new InvalidDataException("Invalid mixed time profile: " + p.Star);
            }
            if (stars.Count != 5) throw new InvalidDataException("Missing mixed time difficulty.");
        }
        if (pack.Lists.TryGetValue("MixedTimeTemplates", out var templates))
        {
            var ids = new HashSet<string>();
            var types = new HashSet<string>();
            foreach (var row in templates.EnumerateArray())
            {
                string id = RequiredString(row, "Id"), type = RequiredString(row, "Type"), text = RequiredString(row, "TextId");
                if (!ids.Add(id) || !ElementaryQuizGenerator.MixedTimeTypes.Any(t => t.ToString() == type) || !text.StartsWith("MixedTime.Template.", StringComparison.Ordinal))
                    throw new InvalidDataException("Invalid mixed time template: " + id);
                types.Add(type);
            }
            if (types.Count != 4) throw new InvalidDataException("Incomplete mixed time templates.");
        }
    }
}
