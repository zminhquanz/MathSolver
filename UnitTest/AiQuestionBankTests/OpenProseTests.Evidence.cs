using MathSolver.Services.QuestionBank;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static partial class OpenProseTests
{
    internal static async Task VerifyEvidenceAsync(string directory)
    {
        var json = new JsonSerializerOptions { WriteIndented = true };
        json.Converters.Add(new JsonStringEnumConverter());
        var attempts = JsonSerializer.Deserialize<Attempt[]>(await File.ReadAllTextAsync(Path.Combine(directory,"attempts.json")),json)!;
        Check(attempts.Length > 0,"No native inference evidence.");
        var replay = new List<object>();
        int verified = 0, pending = 0;
        foreach(var attempt in attempts)
        {
            if(attempt.Open&&attempt.Number==1&&(DecimalOpenStoryCatalogue.IsProfile(attempt.Contract)||SameDayTimeCatalogue.IsProfile(attempt.Contract)))
                Check(attempt.Prompt.Replace("\r\n","\n",StringComparison.Ordinal)==BasicQuestionPrompt.Build(attempt.Contract).Replace("\r\n","\n",StringComparison.Ordinal),"Final pilot prompt differs from native-tested input.");
            var result = BasicQuestionValidator.Validate(attempt.RawJson,attempt.Contract);
            if(attempt.Outcome == "Verified")
                Check(result.IsValid,"Previously verified native evidence failed final validation: "+attempt.Case);
            if(result.IsValid)
            {
                CheckPractice(attempt.Contract with {OpenProse=true},result.Draft!);
                verified++;
            }
            if(result.NeedsReview) pending++;
            var syntax=OpenQuestionProse.ValidateSyntax(attempt.RawJson,attempt.Contract);
            replay.Add(new {attempt.Case,attempt.Open,attempt.Temperature,attempt.Number,Recorded=attempt.Outcome,
                Final=result.ErrorCode??"Verified",result.ErrorDetails,
                ProseIdentity=result.Draft is null?null:QuestionProseIdentity.Hash(attempt.Contract,result.Draft),
                Profile=attempt.Contract.SceneId,
                Reference=OpenQuestionProse.Example(attempt.Contract).ToWordProblem(attempt.Contract).ProblemText,
                Candidate=syntax.Draft?.ToWordProblem(attempt.Contract).ProblemText,
                Solution=attempt.Contract.Solution});
        }
        var store = new QuestionBankStore(Path.Combine(directory,"verified.db3"));
        var quarantined = await store.GetPendingReviewsAsync();
        foreach(var row in quarantined)
            Check(!await store.ContainsProseAsync(new(row.Contract,row.Draft,row.RawJson,row.ModelName,row.CreatedUtc)),
                "Native unverified draft leaked into the active bank.");
        await File.WriteAllTextAsync(Path.Combine(directory,"final-replay.json"),JsonSerializer.Serialize(replay,json));
        Console.WriteLine($"PASS final-source replay of {attempts.Length} real native attempts: {verified} verified with fresh C# grading, {pending} inconclusive; {quarantined.Count} quarantined drafts absent from the active bank.");
    }
}
