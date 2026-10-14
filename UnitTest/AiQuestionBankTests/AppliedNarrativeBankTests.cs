using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;
using System.Globalization;
using System.Text.Json;

internal static class AppliedNarrativeBankTests
{
    internal static ElementaryQuizType[] Types => ElementaryQuizGenerator.AppliedNarrativeTypes;
    internal static BankQuestionFamily Family(ElementaryQuizType type) => type switch
    {
        ElementaryQuizType.AreaConversion or ElementaryQuizType.VolumeConversion or ElementaryQuizType.MixedLength => BankQuestionFamily.Measurement,
        ElementaryQuizType.RectangleSide or ElementaryQuizType.CompositeArea => BankQuestionFamily.Spatial,
        ElementaryQuizType.Calendar or ElementaryQuizType.ReadClock => BankQuestionFamily.Time,
        _ => BankQuestionFamily.Decimal
    };
    internal static QuizProblemKind Kind(ElementaryQuizType type) => Family(type) switch
    { BankQuestionFamily.Measurement => QuizProblemKind.Measurement, BankQuestionFamily.Spatial => QuizProblemKind.VisualGeometry,
      BankQuestionFamily.Time => QuizProblemKind.Time, _ => QuizProblemKind.Decimal };

    internal static async Task RunAsync()
    {
        int cases = 0;
        var shapes = new Dictionary<ElementaryQuizType, HashSet<string>>();
        foreach (var type in Types)
        foreach (var language in Enum.GetValues<AppLanguage>())
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        for (int seed = 0; seed < 40; seed++)
        {
            var c = ReasoningStoryCatalogue.Create(Family(type), (int)type, tier, language, new Random(seed));
            Check(c.IsValid, "Invalid applied contract: " + type);
            var drafts = ReviewedReasoningProse.NovelDrafts(c, new HashSet<string>()).Take(2).ToArray();
            Check(drafts.Length == 2, "Insufficient alternative factual clauses: " + type);
            foreach (var draft in drafts)
            {
                var validation = BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft), c);
                Check(validation.IsValid, $"Rejected {type}/{language}/{tier}: {validation.ErrorCode}: {validation.ErrorDetails}");
                Check(!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft with { Question = draft.Question + " 999?" }), c).IsValid, "Accepted invented data.");
                Verify(c, draft);
                var fresh=c.FreshFacts(new Random(717 + seed));
                Verify(fresh, draft);
                if (Family(type)==BankQuestionFamily.Spatial)
                    Check(!ReasoningStoryCatalogue.ToPractice(c,draft,ArithmeticQuizMode.Essay).ElementaryProblem!.Facts
                        .SequenceEqual(ReasoningStoryCatalogue.ToPractice(fresh,draft,ArithmeticQuizMode.Essay).ElementaryProblem!.Facts),"Geometry reused identical facts.");
                cases++;
            }
            if (type is ElementaryQuizType.CompositeArea or ElementaryQuizType.RectangleSide)
            {
                if (!shapes.TryGetValue(type,out var scenarios)) shapes[type] = scenarios = [];
                scenarios.Add(ReasoningStoryCatalogue.ToPractice(c,drafts[0],ArithmeticQuizMode.Essay).ElementaryProblem!.Visual!.ScenarioId!);
            }
            Check(GgufQuestionRuntime.BuildNovelGrammar(c, new HashSet<string>()).Contains("root ::="), "Missing grammar.");
        }
        // Persistent generators rotate through all members of each finite pool.
        foreach (var type in new[]{ElementaryQuizType.RectangleSide,ElementaryQuizType.CompositeArea})
        foreach (var tier in Enum.GetValues<CurriculumTier>())
        {
            var generator = new ElementaryQuizGenerator(new Random(7919));
            for(int i=0;i<12;i++)
            {
                var q=generator.GenerateAppliedNarrative(ArithmeticQuizMode.Essay,type,AppLanguage.Vietnamese,tier);
                shapes[type].Add(q.ElementaryProblem!.Visual!.ScenarioId!);
            }
        }
        Check(shapes[ElementaryQuizType.CompositeArea].Count == 8 && shapes[ElementaryQuizType.RectangleSide].Count == 9,
            "Missing composite/cutout or missing-side scenario: " + string.Join("; ", shapes.Select(p=>p.Key+":"+string.Join(",",p.Value))));
        using var workspace = TestWorkspace.Temporary("AppliedNarratives");
        var store = new QuestionBankStore(workspace.FilePath("bank.db3"));
        var provider = new BasicPracticeQuestionProvider(store,new AlwaysBank());
        foreach(var type in Types)
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var c = ReasoningStoryCatalogue.Create(Family(type),(int)type,CurriculumTier.ThreeStars,language,new Random(713));
            var draft = ReviewedReasoningProse.NovelDrafts(c,new HashSet<string>()).First();
            Check(await store.InsertAsync(new(c,draft,QuestionBankStore.SerializeDraft(draft),"reviewed-test",DateTime.UtcNow)),"Insert failed.");
            var restored = await store.TakeReasoningAsync(c.Family,c.BankVariant,c.Tier,c.Language);
            Check(restored is not null && QuestionBankStore.ValidateSavedQuestion(restored).IsValid,"SQLite round-trip failed.");
            var generated = new ElementaryQuizGenerator(new Random(743)).GenerateAppliedNarrative(ArithmeticQuizMode.Essay,type,language,c.Tier);
            var selected = await provider.SelectReasoningAsync(generated,c.Tier,language);
            Check(selected.ElementaryProblem!.Type == type && selected.ElementaryProblem.StoryContextId == c.SceneId,"Bank provider lost requested type.");
            var factory = new PracticeQuestionFactory(provider);
            foreach (var format in Enum.GetValues<PracticeQuestionFormat>())
            {
                var q = await factory.CreateAsync(new(Kind(type),ElementaryType:type),ArithmeticQuizMode.Essay,new(c.Tier,false),language,null,format,false);
                Check(q.ElementaryProblem!.Type == type,"Practice/PDF factory changed requested type.");
            }
            var worker = new AiQuestionGenerationService(new ReviewedRuntime(),new QuestionBankStore(workspace.FilePath(type+"-"+language+".db3")));
            worker.Start(new(ArithmeticOperation.Add,c.Tier,language,10,true,Family:c.Family,StoryVariant:(int)type));
            await worker.Completion.WaitAsync(TimeSpan.FromMinutes(2));
            Check(worker.Snapshot.State == AiJobState.Completed && worker.Snapshot.Items.Count == 10 && worker.Snapshot.Items.All(i=>i.State == AiItemState.Saved),
                $"Batch failed {type}/{language}: {worker.Snapshot.Error}; "+string.Join(", ",worker.Snapshot.Items.Select(i=>i.State+":"+i.Error)));
            Console.WriteLine($"PASS ten-item applied batch: {type}/{language}");
        }
        using var excel = new MemoryStream();
        Check((await store.ExportExcelAsync(excel)).Exported == 18,"Excel export lost rows.");excel.Position=0;
        Check((await new QuestionBankStore(workspace.FilePath("import.db3")).ImportExcelAsync(excel)).Inserted == 18,"Excel import lost rows.");
        Console.WriteLine($"PASS {cases} bilingual applied drafts, independent arithmetic/diagram checks, fresh facts, all modes, SQLite/Excel and 18 ten-item batches.");
    }

    private static void Verify(BasicQuestionContract c,BasicQuestionDraft draft)
    {
        foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
        {
            var q = ReasoningStoryCatalogue.ToPractice(c,draft,mode);
            var e = q.ElementaryProblem!;
            Check(!e.ProblemText.Contains('{'),"Unrendered variables.");
            string submission = e.Type == ElementaryQuizType.CompositeArea || e.Type == ElementaryQuizType.ReadClock && (int)c.Tier >= 4
                ? e.SolutionText : e.AnswerText;
            var grade = new EssayAnswerValidator(new BasicArithmeticEngine()).ValidateSubmission(q,submission);
            Check(grade.IsCorrect,$"C# solution rejected {e.Type}/{c.Language}/{c.Tier}: {string.Join("; ",grade.Details??[])}; {submission}");
            if (e.Type == ElementaryQuizType.CompositeArea)
            {
                decimal area = 0;
                foreach(var polygon in e.Visual!.Polygons!)
                {
                    decimal twice = 0;
                    for(int i=0;i<polygon.Vertices.Count;i++)
                    {
                        var a=polygon.Vertices[i];var b=polygon.Vertices[(i+1)%polygon.Vertices.Count];
                        twice += a.X*b.Y-b.X*a.Y;
                    }
                    area += Math.Abs(twice)/2*(polygon.IsCutout?-1:1);
                }
                Check((decimal)e.Answers[0].Value.Numerator/(decimal)e.Answers[0].Value.Denominator == area,"C# composite answer differs from polygon area.");
            }
            if(e.Type == ElementaryQuizType.DecimalRound)
            {
                decimal original=decimal.Parse(e.Reasoning!.Givens[0].Value,CultureInfo.InvariantCulture);
                Check((decimal)e.Answers[0].Value.Numerator/(decimal)e.Answers[0].Value.Denominator == decimal.Round(original,e.RoundingDecimalPlaces!.Value,MidpointRounding.AwayFromZero),"Wrong decimal rounding.");
            }
            if(e.Type == ElementaryQuizType.Calendar)
            {
                var g=e.Reasoning!.Givens.ToDictionary(v=>v.Role,v=>int.Parse(v.Value,CultureInfo.InvariantCulture));
                int days=(new DateTime(g["year"],g["end-month"],g["end-day"])-new DateTime(g["year"],g["start-month"],g["start-day"])).Days-g.GetValueOrDefault("paused-days");
                Check(e.Answers[0].Value.Numerator == days,"Wrong calendar interval/leap day.");
            }
            if(e.Type is ElementaryQuizType.AreaConversion or ElementaryQuizType.VolumeConversion or ElementaryQuizType.MixedLength)
            {
                int power=e.Type==ElementaryQuizType.AreaConversion?2:e.Type==ElementaryQuizType.VolumeConversion?3:1;
                int Exponent(string unit)=>unit.TrimEnd('²','³') switch {"m"=>0,"dm"=>-1,"cm"=>-2,_=>throw new Exception("Unexpected metric unit.")};
                decimal expected=0;
                foreach(var given in e.Reasoning!.Givens)
                {
                    int exponent=(Exponent(given.Unit)-Exponent(e.Answers[0].Unit))*power;
                    decimal factor=1;
                    for(int i=0;i<Math.Abs(exponent);i++) factor*=10;
                    decimal quantity=decimal.Parse(given.Value,CultureInfo.InvariantCulture);
                    expected+=(given.Role=="removed-quantity"?-1:1)*quantity*(exponent>=0?factor:1/factor);
                }
                Check((decimal)e.Answers[0].Value.Numerator/(decimal)e.Answers[0].Value.Denominator==expected,"Wrong metric conversion/dimension or removed amount.");
            }
            if(e.Type==ElementaryQuizType.DecimalCompare)
            {
                decimal left=decimal.Parse(e.Reasoning!.Givens[0].Value,CultureInfo.InvariantCulture),right=decimal.Parse(e.Reasoning.Givens[1].Value,CultureInfo.InvariantCulture);
                Check(e.Answers[0].Text==(left<right?"<":left>right?">":"="),"Comparison reversed the two jugs.");
            }
            if(e.Type==ElementaryQuizType.RectangleSide)
            {
                var values=e.Facts.Select(v=>decimal.Parse(v,CultureInfo.InvariantCulture)).ToArray();
                decimal expected=e.Visual!.ScenarioId switch
                {
                    "rectangle-area"=>values[0]/values[1],
                    "rectangle-perimeter" or "parallelogram-perimeter"=>values[0]/2-values[1],
                    "square-perimeter" or "rhombus-perimeter"=>values[0]/4,
                    "triangle-perimeter"=>values[0]-values[1]-values[2],
                    "split-side-perimeter"=>values[0]/2-values[1]-values[2],
                    "split-side" or "l-shape-side"=>values[0]-values[1],
                    _=>throw new Exception("Unverified missing-side scenario.")
                };
                Check((decimal)e.Answers[0].Value.Numerator/(decimal)e.Answers[0].Value.Denominator==expected,"Wrong missing side relation.");
            }
            if(e.Type == ElementaryQuizType.ReadClock)
            {
                var g=e.Reasoning!.Givens.ToDictionary(v=>v.Role,v=>int.Parse(v.Value,CultureInfo.InvariantCulture));
                int minutes=g["start-hour"]*60+g["start-minute"]+g.GetValueOrDefault("advance-hours")*60+g.GetValueOrDefault("advance-minutes")-g.GetValueOrDefault("subtract-minutes");
                Check(e.Answers[0].Value.Numerator == minutes/60 && e.Answers[1].Value.Numerator == minutes%60,"Clock answer changed.");
                Check(e.Visual!.Values.SequenceEqual(new decimal[]{g["start-hour"],g["start-minute"]}),"AI changed clock hands.");
            }
        }
    }

    internal static async Task RunModelAsync(string path)
    {
        using var workspace=TestWorkspace.Artifacts("applied-model","artifacts/verification/applied-narratives");
        await using var model=new ModelTestSession();await model.LoadAsync(path);
        foreach(var type in Types)
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var c=ReasoningStoryCatalogue.Create(Family(type),(int)type,CurriculumTier.ThreeStars,language,new Random(713));
            using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(4));
            var native=await model.GenerateAsync(c,BasicQuestionPrompt.Build(c,null),timeout.Token,new HashSet<string>());
            await File.WriteAllTextAsync(workspace.FilePath(type+"-"+language+".json"),JsonSerializer.Serialize(new {Contract=c,native.Raw,native.Metrics,native.Validation.ErrorCode,native.Validation.ErrorDetails},new JsonSerializerOptions{WriteIndented=true}));
            Check(native.Validation.IsValid,$"Native applied draft rejected {type}/{language}: {native.Validation.ErrorCode}: {native.Validation.ErrorDetails}");
            Verify(c.FreshFacts(new Random(719)),native.Validation.Draft!);
            Console.WriteLine($"PASS real GGUF {type}/{language}");
        }
        Console.WriteLine("PASS eighteen real-model profiles. Evidence: "+workspace.DirectoryPath);
    }
    internal static async Task VerifyEvidenceAsync(string directory)
    {
        var files=Directory.GetFiles(directory,"*.json");
        Check(files.Length==18,"Evidence must cover all nine types in both languages.");
        foreach(string path in files)
        {
            using var json=JsonDocument.Parse(await File.ReadAllTextAsync(path));
            var c=json.RootElement.GetProperty("Contract").Deserialize<BasicQuestionContract>()!;
            var result=BasicQuestionValidator.Validate(json.RootElement.GetProperty("Raw").GetString()!,c);
            Check(result.IsValid,"Native evidence rejected by current code: "+Path.GetFileName(path));
            Verify(c.FreshFacts(new Random(719)),result.Draft!);
        }
        Console.WriteLine("PASS eighteen real-model evidence records with current validation, fresh data and independent mathematical checks.");
    }
    private sealed class AlwaysBank:Random {public override int Next(int maxValue)=>maxValue-1;}
    private sealed class ReviewedRuntime:IQuestionTextRuntime
    {
        public bool IsLoaded{get;private set;}public bool CanGenerate=>true;public string ModelName=>"reviewed-applied";
        public Task PrepareAsync(CancellationToken token){IsLoaded=true;return Task.CompletedTask;}
        public Task ReleaseAsync(){IsLoaded=false;return Task.CompletedTask;}
        public Task<string> GenerateAsync(BasicQuestionContract c,string prompt,CancellationToken token,Action<string>? onText=null,Action<AiGenerationMetrics>? onMetrics=null)
            =>GenerateNovelAsync(c,prompt,new HashSet<string>(),token,onText,onMetrics);
        public Task<string> GenerateNovelAsync(BasicQuestionContract c,string prompt,IReadOnlySet<string> excluded,CancellationToken token,Action<string>? onText=null,Action<AiGenerationMetrics>? onMetrics=null)
        {token.ThrowIfCancellationRequested();return Task.FromResult(QuestionBankStore.SerializeDraft(ReviewedReasoningProse.NovelDrafts(c,excluded).First()));}
    }
}
