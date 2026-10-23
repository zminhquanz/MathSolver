using MathSolver.Services.QuestionBank;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static partial class OpenProseTests
{
    internal static async Task VerifyEvidenceAsync(string directory,bool auditChangedSource=false)
    {
        var json = new JsonSerializerOptions { WriteIndented = true };
        json.Converters.Add(new JsonStringEnumConverter());
        var attempts = JsonSerializer.Deserialize<Attempt[]>(await File.ReadAllTextAsync(Path.Combine(directory,"attempts.json")),json)!;
        Check(attempts.Length > 0,"No native inference evidence.");
        var replay = new List<object>();
        var seenByRequest=new Dictionary<(string,bool,float),HashSet<string>>();
        int verified = 0, pending = 0,duplicates=0;
        foreach(var attempt in attempts)
        {
            if(!auditChangedSource&&attempt.Open&&attempt.Number==1&&(SameMonthCalendarCatalogue.IsProfile(attempt.Contract)||OvernightTimeCatalogue.IsProfile(attempt.Contract)||SameDayBreakCatalogue.IsProfile(attempt.Contract)||RectangleAreaOpenCatalogue.IsProfile(attempt.Contract)||RectanglePerimeterOpenCatalogue.IsProfile(attempt.Contract)||DecimalOpenStoryCatalogue.IsProfile(attempt.Contract)||SameDayTimeCatalogue.IsProfile(attempt.Contract)||FractionPartOpenCatalogue.IsProfile(attempt.Contract)||FractionRemainingOpenCatalogue.IsProfile(attempt.Contract)||FractionWholeOpenCatalogue.IsProfile(attempt.Contract)||DifferenceRatioOpenCatalogue.IsProfile(attempt.Contract)||SumRatioOpenCatalogue.IsProfile(attempt.Contract)||SumDifferenceOpenCatalogue.IsProfile(attempt.Contract)||AverageMissingOpenCatalogue.IsProfile(attempt.Contract)||AverageOpenCatalogue.IsProfile(attempt.Contract)||PercentagePartOpenCatalogue.IsProfile(attempt.Contract)||PercentageWholeOpenCatalogue.IsProfile(attempt.Contract)||PercentageRemainingOpenCatalogue.IsProfile(attempt.Contract)))
                Check(attempt.Prompt.Replace("\r\n","\n",StringComparison.Ordinal)==BasicQuestionPrompt.Build(attempt.Contract).Replace("\r\n","\n",StringComparison.Ordinal),"Final pilot prompt differs from native-tested input.");
            var result = BasicQuestionValidator.Validate(attempt.RawJson,attempt.Contract);
            var key=(attempt.Case,attempt.Open,attempt.Temperature);
            if(!seenByRequest.TryGetValue(key,out var seen))
                seenByRequest[key]=seen=new HashSet<string>{QuestionProseIdentity.Hash(attempt.Contract,OpenQuestionProse.Example(attempt.Contract))};
            string? hash=result.Draft is null?null:QuestionProseIdentity.Hash(attempt.Contract,result.Draft);
            bool duplicate=hash is not null&&!seen.Add(hash);
            string outcome=duplicate?"DuplicateProse":result.ErrorCode??"Verified";
            if(!auditChangedSource&&!string.IsNullOrWhiteSpace(attempt.RawJson))
                Check(outcome==attempt.Outcome,"Final generation disposition differs from recorded inference: "+attempt.Case);
            if(attempt.Outcome == "Verified"&&!auditChangedSource)
                Check(result.IsValid,"Previously verified native evidence failed final validation: "+attempt.Case);
            if(result.IsValid)
            {
                CheckPractice(attempt.Contract with {OpenProse=true},result.Draft!);
                if(!duplicate)verified++;
            }
            if(result.NeedsReview&&!duplicate) pending++;
            if(duplicate)duplicates++;
            var syntax=OpenQuestionProse.ValidateSyntax(attempt.RawJson,attempt.Contract);
            replay.Add(new {attempt.Case,attempt.Open,attempt.Temperature,attempt.Number,Recorded=attempt.Outcome,
                Final=outcome,Validation=result.ErrorCode??"Verified",result.ErrorDetails,
                ProseIdentity=hash,
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
        await File.WriteAllTextAsync(Path.Combine(directory,auditChangedSource?"audit-final.json":"final-replay.json"),JsonSerializer.Serialize(replay,json));
        Console.WriteLine($"{(auditChangedSource?"AUDIT changed source":"PASS final-source replay")} of {attempts.Length} real native attempts: {verified} verified new drafts with fresh C# grading, {pending} inconclusive, {duplicates} duplicates; {quarantined.Count} quarantined drafts absent from the active bank.");
    }
}
