using System.Text.Json;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckDecimalSpatialLists(QuizContentPack pack)
    {
        if (pack.Lists.TryGetValue("DecimalSkillProfiles", out var profiles))
        {
            var rows = profiles.Deserialize<DecimalSkillProfile[]>() ?? [];
            if (rows.Length != 5 || !rows.Select(r => r.Star).Order().SequenceEqual(Enumerable.Range(1,5))
                || rows.Any(r => r.Places is < 1 or > 3 || r.MaximumWhole is < 1 or > 9999))
                throw new InvalidDataException("Invalid decimal skill profiles.");
        }
        if (pack.Lists.TryGetValue("SpatialPracticeLabels", out var labels))
        {
            var rows = labels.Deserialize<string[]>() ?? [];
            if (rows.Length < 4 || rows.Distinct().Count() != rows.Length || rows.Any(s => s.Length != 1 || !char.IsAsciiLetter(s[0])))
                throw new InvalidDataException("Invalid spatial labels.");
        }
    }
}
