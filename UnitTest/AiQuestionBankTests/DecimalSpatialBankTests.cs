using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using System.Text.Json;

internal static class DecimalSpatialBankTests
{
    internal static ElementaryQuizType[] Types => [.. ElementaryQuizGenerator.DecimalSkillTypes, .. ElementaryQuizGenerator.SpatialSkillTypes];
    private static BankQuestionFamily Family(ElementaryQuizType type) => ElementaryQuizGenerator.SpatialSkillTypes.Contains(type) ? BankQuestionFamily.Spatial : BankQuestionFamily.Decimal;
    internal static async Task RunAsync()
    {
        int cases = 0;
        foreach (var type in Types)
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        for (int seed = 0; seed < 4; seed++)
        {
            var c = ReasoningStoryCatalogue.Create(Family(type), (int)type, tier, language, new Random(seed));
            Check(c.IsValid, "Invalid skill contract: " + type);
            var drafts = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).Take(2).ToArray();
            Check(drafts.Length == 2, "Insufficient novel facts: " + type);
            foreach (var draft in drafts)
            {
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft), c).IsValid, "Reviewed skill draft rejected: " + type);
                Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft with { Question = draft.Question + " 999?" }), c).IsValid, "Added data accepted.");
                Verify(c.FreshFacts(new Random(717)), draft);
                cases++;
            }
            Check(GgufQuestionRuntime.BuildNovelGrammar(c, new HashSet<string>()).Contains("root ::="), "Missing skill grammar.");
        }
        using var workspace = TestWorkspace.Temporary("DecimalSpatial");
        var store = new QuestionBankStore(workspace.FilePath("bank.db3"));
        var provider = new BasicPracticeQuestionProvider(store, new AlwaysBank());
        foreach (var type in Types)
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var c = ReasoningStoryCatalogue.Create(Family(type), (int)type, CurriculumTier.ThreeStars, language, new Random(713));
            var draft = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).First();
            Check(await store.InsertAsync(new(c, draft, QuestionBankStore.SerializeDraft(draft), "reviewed-test", DateTime.UtcNow)), "Failed skill insertion.");
            var restored = await store.TakeReasoningAsync(c.Family, c.BankVariant, c.Tier, c.Language);
            Check(restored is not null && QuestionBankStore.ValidateSavedQuestion(restored).IsValid, "Failed skill SQLite round-trip.");
            Verify(restored!.Contract.FreshFacts(new Random(719)), restored.Draft);
            var q = new ElementaryQuizGenerator(new Random(743)).GenerateDecimalSpatialSkill(ArithmeticQuizMode.Essay, type, language, c.Tier);
            var selected = await provider.SelectReasoningAsync(q, c.Tier, language);
            Check(selected.ElementaryProblem!.Type == type && selected.ElementaryProblem.StoryContextId == c.SceneId, "Provider lost skill profile.");
            Console.WriteLine($"Checking ten-item batch {type}/{language}; {ReviewedReasoningProse.NovelDrafts(c,new HashSet<string>()).Count()} reviewed combinations.");
            var worker = new AiQuestionGenerationService(new ReviewedRuntime(), new QuestionBankStore(workspace.FilePath(type+"-"+language+".db3")));
            worker.Start(new(ArithmeticOperation.Add, c.Tier, language, 10, true, Family:c.Family, StoryVariant:(int)type));
            await worker.Completion.WaitAsync(TimeSpan.FromMinutes(2));
            Check(worker.Snapshot.State == AiJobState.Completed && worker.Snapshot.Items.Count == 10 && worker.Snapshot.Items.All(i => i.State == AiItemState.Saved),
                "Ten-item skill batch failed: " + type + "/" + language + ": " + worker.Snapshot.Error + "; "
                    + string.Join(", ",worker.Snapshot.Items.Select(i=>i.Number+":"+i.State+":"+i.Error)));
        }
        using var excel = new MemoryStream();
        Check((await store.ExportExcelAsync(excel)).Exported == 20, "Excel skill export lost rows."); excel.Position=0;
        Check((await new QuestionBankStore(workspace.FilePath("import.db3")).ImportExcelAsync(excel)).Inserted == 20, "Excel skill import lost rows.");
        Console.WriteLine($"PASS {cases} bilingual decimal/spatial drafts; replay, grammar, hostile mutations, SQLite/Excel and twenty ten-item batches.");
    }

    private static void Verify(BasicQuestionContract c, BasicQuestionDraft draft)
    {
        foreach (var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var q = ReasoningStoryCatalogue.ToPractice(c,draft,mode);
            Check(!q.ElementaryProblem!.ProblemText.Contains('{'), "Unrendered skill variable.");
            Check(new EssayAnswerValidator(new BasicArithmeticEngine()).ValidateSubmission(q,q.ElementaryProblem.AnswerText).IsCorrect, "Fresh skill answer failed.");
            var baseline = new ElementaryQuizGenerator(new Random(c.Story!.Seed)).GenerateDecimalSpatialSkill(mode,(ElementaryQuizType)c.BankVariant,c.Language,c.Tier,c.Story.NarrativeId);
            Check(q.ElementaryProblem.AnswerText == baseline.ElementaryProblem!.AnswerText, "AI altered C# answer.");
        }
    }
    internal static async Task RunModelAsync(string path)
    {
        using var workspace=TestWorkspace.Artifacts("decimal-spatial-model","artifacts/verification/decimal-spatial");
        await using var model=new ModelTestSession(); await model.LoadAsync(path);
        foreach (var type in Types)
        foreach (var language in Enum.GetValues<AppLanguage>())
        {
            var c=ReasoningStoryCatalogue.Create(Family(type),(int)type,CurriculumTier.ThreeStars,language,new Random(713));
            using var cancellation=new CancellationTokenSource(TimeSpan.FromMinutes(4));
            var native=await model.GenerateAsync(c,BasicQuestionPrompt.Build(c,null),cancellation.Token,new HashSet<string>());
            await File.WriteAllTextAsync(workspace.FilePath(type+"-"+language+".json"),JsonSerializer.Serialize(new {Contract=c,native.Raw,native.Metrics,native.Validation.ErrorCode,native.Validation.ErrorDetails},new JsonSerializerOptions {WriteIndented=true}));
            Check(native.Validation.IsValid,"Native skill rejected: "+type+": "+native.Validation.ErrorDetails);
            Verify(c.FreshFacts(new Random(719)),native.Validation.Draft!);
            Console.WriteLine($"PASS real GGUF {language}/{type}, C# replay and all answer modes.");
        }
        Console.WriteLine("PASS twenty native skill profiles. Evidence: "+workspace.DirectoryPath);
    }
    internal static async Task VerifyEvidenceAsync(string directory)
    {
        string[] files=Directory.GetFiles(directory,"*.json");
        Check(files.Length==20,"Native evidence must cover all ten skills in both languages.");
        foreach(string path in files)
        {
            using var json=JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var c=json.RootElement.GetProperty("Contract").Deserialize<BasicQuestionContract>()!;
            var result=BasicQuestionValidator.Validate(json.RootElement.GetProperty("Raw").GetString()!,c);
            Check(result.IsValid,"Native evidence failed current validation: "+Path.GetFileName(path));
            Verify(c.FreshFacts(new Random(719)),result.Draft!);
        }
        Console.WriteLine("PASS twenty real-model evidence records revalidated with the current generators and fresh facts.");
    }
    private sealed class AlwaysBank:Random {public override int Next(int maxValue)=>maxValue-1;}
    private sealed class ReviewedRuntime:IQuestionTextRuntime
    {
        public bool IsLoaded {get;private set;} public bool CanGenerate=>true; public string ModelName=>"reviewed-decimal-spatial";
        public Task PrepareAsync(CancellationToken token){IsLoaded=true;return Task.CompletedTask;}
        public Task ReleaseAsync(){IsLoaded=false;return Task.CompletedTask;}
        public Task<string> GenerateAsync(BasicQuestionContract c,string prompt,CancellationToken token,Action<string>? onText=null,Action<AiGenerationMetrics>? onMetrics=null)
            =>GenerateNovelAsync(c,prompt,new HashSet<string>(),token,onText,onMetrics);
        public Task<string> GenerateNovelAsync(BasicQuestionContract c,string prompt,IReadOnlySet<string> excluded,CancellationToken token,Action<string>? onText=null,Action<AiGenerationMetrics>? onMetrics=null)
        {token.ThrowIfCancellationRequested();return Task.FromResult(QuestionBankStore.SerializeDraft(ReviewedReasoningProse.NovelDrafts(c,excluded).First()));}
    }
}
