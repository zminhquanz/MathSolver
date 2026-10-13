using System.Text.Json;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckNumberNameLists(QuizContentPack pack)
    {
        if (!pack.Lists.TryGetValue("Foundation.NumberNameGrammar", out var grammar)) return;
        if (grammar.ValueKind != JsonValueKind.Array || grammar.GetArrayLength() != 1)
            throw new InvalidDataException("Number-name grammar must contain exactly one grammar.");
        PracticeNumberNames.Validate(JsonSerializer.Deserialize<NumberNameGrammar>(grammar[0].GetRawText())!);
        if (!pack.Lists.TryGetValue("Foundation.NumberNames.Extensions", out var names) || names.GetArrayLength() < 4)
            throw new InvalidDataException("Missing reviewed large number names.");
        var ids = new HashSet<int>();
        foreach (var row in names.EnumerateArray())
        {
            int number = row.GetProperty("Value").GetInt32();
            if (number < 1_000_000 || number > PracticeNumberNames.Maximum || !ids.Add(number))
                throw new InvalidDataException("Invalid/duplicate reviewed large number.");
            _ = RequiredString(row, "Words");
        }
    }
}
