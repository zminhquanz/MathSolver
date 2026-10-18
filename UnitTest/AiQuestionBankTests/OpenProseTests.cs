using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

internal static partial class OpenProseTests
{
    internal static IEnumerable<(string Name, BasicQuestionContract Contract)> Cases()
    {
        yield return ("OneStep-Add-VI", AppliedQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects), ArithmeticOperation.Add, CurriculumTier.ThreeStars, AppLanguage.Vietnamese, new(713)));
        var expanded = Enumerable.Range(0, 64).Select(seed => ExpandedArithmeticCatalogue.Create(new(QuestionKnowledgeGroup.Objects), ArithmeticOperation.Subtract,
            CurriculumTier.FourStars, AppLanguage.English, new(seed), style: PracticeArithmeticStyle.Written)).First(c => c.Practice!.StepCount == 1);
        yield return ("OneStep-Subtract-EN", expanded);
        yield return ("FindX-Divisor-VI", FindXQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Packaging), ArithmeticOperation.Divide, CurriculumTier.ThreeStars, AppLanguage.Vietnamese, new(713), FindXUnknownRole.Divisor));
        yield return ("FindX-Minuend-EN", FindXQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects), ArithmeticOperation.Subtract, CurriculumTier.ThreeStars, AppLanguage.English, new(713), FindXUnknownRole.Minuend));
        yield return ("Measurement-Length-VI", ReasoningStoryCatalogue.Create(BankQuestionFamily.Measurement, (int)ElementaryQuizType.LengthConversion, CurriculumTier.OneStar, AppLanguage.Vietnamese, new(713)));
        yield return ("Measurement-Mass-EN", ReasoningStoryCatalogue.Create(BankQuestionFamily.Measurement, (int)ElementaryQuizType.MassConversion, CurriculumTier.TwoStars, AppLanguage.English, new(713)));
    }

    internal static void CheckPractice(BasicQuestionContract c, BasicQuestionDraft draft)
    {
        for (int seed = 0; seed < 3; seed++)
        {
            var fresh = c.FreshFacts(new(891 + seed));
            Check(fresh.OpenProse && BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft), fresh).IsValid, "Fresh facts lost the prose policy or its verification.");
            if (OvernightTimeCatalogue.IsProfile(fresh)) { OvernightTimeTests.CheckPractice(fresh,draft); continue; }
            if (SameDayBreakCatalogue.IsProfile(fresh)) { SameDayBreakTests.CheckPractice(fresh,draft); continue; }
            if (RectangleAreaOpenCatalogue.IsProfile(fresh)) { RectangleAreaOpenTests.CheckPractice(fresh,draft); continue; }
            if (RectanglePerimeterOpenCatalogue.IsProfile(fresh)) { RectanglePerimeterOpenTests.CheckPractice(fresh,draft); continue; }
            if (PercentageWholeOpenCatalogue.IsProfile(fresh)) { PercentageWholeOpenTests.CheckPractice(fresh,draft); continue; }
            if (PercentageRemainingOpenCatalogue.IsProfile(fresh)) { PercentageRemainingOpenTests.CheckPractice(fresh,draft); continue; }
            if (FractionWholeOpenCatalogue.IsProfile(fresh)) { FractionWholeOpenTests.CheckPractice(fresh,draft); continue; }
            if (FractionRemainingOpenCatalogue.IsProfile(fresh)) { FractionRemainingOpenTests.CheckPractice(fresh,draft); continue; }
            if (FractionPartOpenCatalogue.IsProfile(fresh)) { FractionPartOpenTests.CheckPractice(fresh,draft); continue; }
            if (AverageOpenCatalogue.IsProfile(fresh)) { AverageOpenTests.CheckPractice(fresh,draft); continue; }
            if (PercentagePartOpenCatalogue.IsProfile(fresh)) { PercentagePartOpenTests.CheckPractice(fresh,draft); continue; }
            if (fresh.Version == ReasoningStoryCatalogue.Version) { ProseExpansionTests.VerifyPractice(fresh,draft); continue; }
            foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
            {
                var q = fresh.Version == ReasoningStoryCatalogue.Version ? ReasoningStoryCatalogue.ToPractice(fresh, draft, mode)
                    : fresh.ToPracticeQuestion(draft.ToWordProblem(fresh), mode, new(713));
                Check(new ArithmeticQuizValidator(new BasicArithmeticEngine()).Validate(q).IsValid, "Fresh C# arithmetic failed: " + c.Family + "/" + c.SceneId + "/" + mode);
                Check(!Regex.IsMatch(q.WordProblem?.ProblemText ?? q.ElementaryProblem!.ProblemText, @"\{[a-z0-9_]+\}"), "Unrendered variable in fresh practice.");
            }
        }
    }

    internal static async Task RunAsync()
    {
        int cases = 0, novel = 0;
        foreach (var (_, seed) in Cases())
        {
            var c = seed with { OpenProse = true };
            Check(OpenQuestionProse.Supports(c), "Missing pilot route.");
            var example = OpenQuestionProse.Example(c);
            string raw = QuestionBankStore.SerializeDraft(example);
            Check(BasicQuestionValidator.Validate(raw, c).IsValid, "Reviewed reference failed open validation.");
            CheckPractice(c, example);
            string grammar = GgufQuestionRuntime.BuildNovelGrammar(c, OpenQuestionProse.Examples(c).Select(d => QuestionProseIdentity.Hash(c,d)).ToHashSet());
            Check(grammar.Contains("character ::=") && !grammar.Contains(JsonSerializer.Serialize(raw)), "Open grammar still enumerates full sentences or exhausts references.");
            string intro = c.Language == AppLanguage.Vietnamese ? "Trong lần kiểm kê, " : "While checking supplies, ";
            var changed = example.Facts is null ? example with { GivenA = intro + example.GivenA }
                : example with { Facts = example.Facts.Select((f,i) => i == 0 ? f with { Text = intro + f.Text } : f).ToArray() };
            var validation = BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(changed), c);
            Check(validation.IsValid, "Equivalent novel opening was not verified: "+seed.Family+"/"+seed.SceneId+"/"+validation.ErrorCode+"/"+validation.ErrorDetails);
            Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(changed),c with {OpenProse=false}).IsValid,"The novel wording fixture was already allowed by the baseline.");
            CheckPractice(c, changed); novel++;
            string[] invalid = [raw.Replace("{a}", "17"), raw.Replace("{f0}", "17"), raw.Replace("{b}", "{a}"), raw.Replace("{v0}", "{v99}")];
            foreach (string candidate in invalid.Where(candidate => candidate != raw))
                Check(!BasicQuestionValidator.Validate(candidate, c).IsValid, "Changed facts or placeholders passed open validation.");
            var unknown = example.Facts is null ? example with { GivenA = "Zephyrus, " + example.GivenA }
                : example with { Facts = example.Facts.Select((f,i) => i == 0 ? f with { Text = "Zephyrus, " + f.Text } : f).ToArray() };
            var pending = BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(unknown), c);
            Check(pending.NeedsReview && !pending.IsValid, "Unverified meaning must require review.");
            cases++;
        }
        var add = Cases().First().Contract with { OpenProse = true };
        var source = OpenQuestionProse.Example(add);
        var reversed = source with { GivenB = source.GivenB.Replace("nhận thêm", "cho đi").Replace("được bổ sung thêm", "cho đi") };
        if (reversed != source) Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(reversed),add).IsValid, "Reversed gain/loss passed.");
        var received=new BasicQuestionDraft("{name} có {a} {unit}.","{name} được tặng {b} {unit}.","Hỏi {name} có tất cả bao nhiêu {unit}?","Số {unit} của {name} là:","books");
        Check(OpenProseMeaning.HasContradiction(received with {GivenB=received.GivenB.Replace("được tặng","tặng")},received,AppLanguage.Vietnamese),"Dropping a passive voice marker reversed the event unnoticed.");
        foreach (float value in new[] { float.NaN, float.PositiveInfinity, 0.1f, 0.9f })
        {
            bool rejected = false; try { new QuestionSamplingSettings(Temperature:value).Validate(); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected,"Invalid sampling temperature accepted.");
        }
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var excluded=new HashSet<string>();
            foreach(var context in ElementaryQuizGenerator.MeasurementContexts(language,ElementaryQuizType.LengthConversion))
            {
                var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Measurement,(int)ElementaryQuizType.LengthConversion,
                    CurriculumTier.OneStar,language,new(713),narrativeId:context.Id);
                foreach(var draft in ReviewedReasoningProse.NovelDrafts(c,new HashSet<string>()))
                    excluded.Add(QuestionProseIdentity.Hash(c,draft));
            }
            var selected=new ReasoningStoryCycle().Next(BankQuestionFamily.Measurement,(int)ElementaryQuizType.LengthConversion,
                CurriculumTier.OneStar,language,excluded,creativeProse:true);
            Check(OpenQuestionProse.Supports(selected) && !ReviewedReasoningProse.NovelDrafts(selected,excluded).Any(),
                "The pilot still depends on an unexhausted finite measurement example.");
        }
        await CheckReviewStorageAndWorkerAsync();
        await MatrixAsync();
        Console.WriteLine($"PASS {cases} open-prose profiles, {novel} new equivalent openings, schema/placeholder mutations, fresh C# grading, review routing and exhausted-reference grammar.");
    }

    private sealed record Attempt(string Case, string Family, bool Open, float Temperature, int Number, string Outcome,
        bool NovelVerified, double WallSeconds, double? TokensPerSecond, int Tokens, string RawJson, string Prompt, BasicQuestionContract Contract,int ContextTokens);

    internal static async Task RunModelAsync(string path, string? output = null,
        IEnumerable<(string Name,BasicQuestionContract Contract)>? customCases=null)
    {
        string directory = Path.GetFullPath(output ?? "artifacts/verification/open-prose/model-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(directory);
        var json = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        json.Converters.Add(new JsonStringEnumConverter());
        var attempts = new List<Attempt>();
        var accepted = new List<(BasicQuestionContract Contract, BasicQuestionDraft Draft)>();
        var store = new QuestionBankStore(Path.Combine(directory,"verified.db3"));
        await using var session = new ModelTestSession();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(90));
        Console.WriteLine("Open-prose native benchmark PID="+Environment.ProcessId+" evidence: " + directory);
        await session.LoadAsync(path, deadline.Token);
        await File.WriteAllTextAsync(Path.Combine(directory,"run-info.json"),JsonSerializer.Serialize(new {SchemaVersion=1,StartedUtc=DateTime.UtcNow,session.Runtime.ModelName,
            session.Runtime.InferenceThreadCount,session.Runtime.PromptThreadCount,SystemVietnamese=BasicQuestionPrompt.SystemInstruction(AppLanguage.Vietnamese),SystemEnglish=BasicQuestionPrompt.SystemInstruction(AppLanguage.English),
            Pipeline=customCases is null?"open-variables-bounded-v3":customCases.Any(x=>OvernightTimeCatalogue.IsProfile(x.Contract))?"overnight-open-v1":customCases.Any(x=>SameDayBreakCatalogue.IsProfile(x.Contract))?"time-break-open-v7":customCases.Any(x=>RectangleAreaOpenCatalogue.IsProfile(x.Contract))?"rectangle-area-open-v3":customCases.Any(x=>RectanglePerimeterOpenCatalogue.IsProfile(x.Contract))?"rectangle-perimeter-open-v4":customCases.Any(x=>PercentageWholeOpenCatalogue.IsProfile(x.Contract))?"percentage-whole-open-v5":customCases.Any(x=>PercentageRemainingOpenCatalogue.IsProfile(x.Contract))?"percentage-remaining-english-v7":customCases.Any(x=>FractionWholeOpenCatalogue.IsProfile(x.Contract))?"fraction-whole-open-v5":customCases.Any(x=>FractionRemainingOpenCatalogue.IsProfile(x.Contract))?"fraction-remaining-open-editorial-v5":customCases.Any(x=>PercentagePartOpenCatalogue.IsProfile(x.Contract))?"percentage-part-open-v2":customCases.Any(x=>AverageOpenCatalogue.IsProfile(x.Contract))?"average-open-editorial-v4":customCases.Any(x=>FractionPartOpenCatalogue.IsProfile(x.Contract))?"fraction-part-open-v1":customCases.Any(x=>SameDayTimeCatalogue.IsProfile(x.Contract))?"time-open-v2":"decimal-open-v1",CasesPerSetting=customCases?.Count()??6,
            Sampling=session.Runtime.Sampling,SeedPolicy="Native sampling seed unset; C# fixtures fixed",
            RetryPolicy="Stop on verified or semantic review; retry invalid/duplicate output up to three attempts"},json));
        try
        {
            foreach (var setting in customCases is null?new[] { (Open:false, Temperature:0.5f), (Open:true, Temperature:0.4f), (Open:true, Temperature:0.5f), (Open:true, Temperature:0.6f) }:new[]{(Open:true,Temperature:0.5f)})
            foreach (var (name, seed) in customCases??Cases())
            {
                var c = seed with { OpenProse = setting.Open };
                session.Runtime.Sampling = new(Temperature:setting.Temperature);
                var original = OpenQuestionProse.Example(c);
                var excluded = new HashSet<string> { QuestionProseIdentity.Hash(c, original) };
                string? correction = null, details = null;
                var duplicateProse = new List<string>();
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                    timeout.CancelAfter(TimeSpan.FromMinutes(4));
                    string prompt = BasicQuestionPrompt.Build(c, correction, duplicateProse, excluded, details);
                    var watch = Stopwatch.StartNew();
                    string raw; AiGenerationMetrics? metrics = null;
                    try
                    {
                        raw = await session.Runtime.GenerateNovelAsync(c,prompt,excluded,timeout.Token,onMetrics:m=>metrics=m);
                    }
                    catch (Exception e) when (e is InvalidDataException or OperationCanceledException)
                    {
                        raw = ""; correction = e is OperationCanceledException ? "GenerationTimeout" : e.Message;
                    }
                    watch.Stop();
                    var result = BasicQuestionValidator.Validate(raw,c);
                    correction = raw.Length == 0 ? correction : result.ErrorCode;
                    details = result.ErrorDetails;
                    if (result.Draft is not null)
                    {
                        string hash = QuestionProseIdentity.Hash(c,result.Draft);
                        if (excluded.Contains(hash)) { correction = "DuplicateProse"; duplicateProse.Add(result.Draft.ProblemText); }
                        else excluded.Add(hash);
                    }
                    bool novel = result.IsValid && !BasicQuestionValidator.Validate(raw,c with {OpenProse=false}).IsValid;
                    attempts.Add(new(name,name.Split('-')[0],setting.Open,setting.Temperature,attempt,correction??"Verified",novel,
                        watch.Elapsed.TotalSeconds,metrics?.TokensPerSecond,metrics?.GeneratedTokens??0,raw,prompt,c,session.Runtime.LastContextTokens));
                    if(c.OpenProse && result.NeedsReview && correction=="SemanticReviewRequired")
                        await store.QueueReviewAsync(PendingQuestionReview.Create(c,result.Draft!,raw,session.Runtime.ModelName,result.ErrorDetails??"review"),deadline.Token);
                    await File.WriteAllTextAsync(Path.Combine(directory,"attempts.json"),JsonSerializer.Serialize(attempts,json));
                    Console.WriteLine($"{(setting.Open?"OPEN":"BASE")} {setting.Temperature:F1} {name} #{attempt}: {correction??"Verified"}, {watch.Elapsed.TotalSeconds:F1}s, {metrics?.TokensPerSecond:F2} token/s");
                    if(result.NeedsReview&&correction=="SemanticReviewRequired")break;
                    if (correction is null)
                    {
                        CheckPractice(c with {OpenProse=true},result.Draft!);
                        // Independent settings have independent duplicate pools; persistence still rejects duplicates globally.
                        await store.InsertAsync(new(c,result.Draft!,raw,session.Runtime.ModelName,DateTime.UtcNow),deadline.Token);
                        accepted.Add((c,result.Draft!));
                        break;
                    }
                }
            }
        }
        finally
        {
            var summary = attempts.GroupBy(a=>new {a.Open,a.Temperature,a.Family}).Select(group=> new
            {
                group.Key,Requests=group.Select(a=>a.Case).Distinct().Count(),Attempts=group.Count(),
                Verified=group.Count(a=>a.Outcome=="Verified"),NovelVerified=group.Count(a=>a.Outcome=="Verified"&&a.NovelVerified),
                PendingReview=group.Count(a=>a.Outcome=="SemanticReviewRequired"),Duplicates=group.Count(a=>a.Outcome=="DuplicateProse"),
                Rejected=group.Count(a=>a.Outcome is not ("Verified" or "SemanticReviewRequired" or "DuplicateProse")),
                MeanRetries=(double)group.Count()/group.Select(a=>a.Case).Distinct().Count()-1,
                MeanWallSeconds=group.Average(a=>a.WallSeconds),MeanTokensPerSecond=group.Average(a=>a.TokensPerSecond)
            }).ToArray();
            await File.WriteAllTextAsync(Path.Combine(directory,"summary.json"),JsonSerializer.Serialize(summary,json));
            Console.WriteLine("Native benchmark finished. Evidence: "+directory);
        }
    }

    private static void Check(bool value,string message) { if(!value)throw new InvalidOperationException(message); }
}
