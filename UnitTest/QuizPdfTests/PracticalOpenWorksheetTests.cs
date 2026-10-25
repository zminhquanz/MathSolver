using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static class PracticalOpenWorksheetTests
{
    private sealed class AlwaysBank:Random {public override int Next(int maxValue)=>maxValue-1;}
    private sealed record Target(BankQuestionFamily Family,int Variant,QuizProblemRequest Request);
    private sealed record Expected(string Profile,string Target,BigInteger Answer,string Unit,string Actor,string[] Equations);
    private static readonly Target[] Targets=[
        new(BankQuestionFamily.Percentage,PercentageRatioPilot.Variant,new(QuizProblemKind.Percentage,PercentageType:PercentageQuizType.FindPercentageRatio)),
        new(BankQuestionFamily.MultiStep,MoneyChangePilot.Variant,new(QuizProblemKind.MultiStep,ElementaryType:ElementaryQuizType.MultiStepRemaining)),
        new(BankQuestionFamily.Geometry,TriangleAreaPilot.Variant,new(QuizProblemKind.Geometry,GeometryShape:GeometryQuizShape.Triangle,GeometryMeasurement:GeometryMeasurement.Area)),
        new(BankQuestionFamily.Spatial,RectangleSidePilot.Variant,new(QuizProblemKind.VisualGeometry,ElementaryType:ElementaryQuizType.RectangleSide))];
    internal static async Task Run(string directory)
    {
        int cases=0,pdfs=0;var traces=new List<object>();
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var batches=Enum.GetValues<ArithmeticQuizMode>().ToDictionary(mode=>mode,_=>new List<(QuizPrintItem Item,Expected Expected)>());
            foreach(var target in Targets)
            foreach(string profile in PracticalOpenCatalogue.Profiles(target.Family,target.Variant,language))
            foreach(var tier in Enum.GetValues<CurriculumTier>())
            {
                var c=ReasoningStoryCatalogue.Create(target.Family,target.Variant,tier,language,new(829),profile) with {OpenProse=true};
                var draft=ReasoningStoryCatalogue.Draft(c);string raw=QuestionBankStore.SerializeDraft(draft);
                Check(BasicQuestionValidator.Validate(raw,c).IsValid,"Practical PDF reference failed semantic proof: "+profile);
                string filename=$"practical-{language}-{profile.Replace('/','-')}-{tier}.db3";
                var store=new QuestionBankStore(Path.Combine(directory,filename));
                var unknown=draft with {Question=(language==AppLanguage.Vietnamese?"Theo ghi chép, ":"According to the record, ")+draft.Question};
                string pending=QuestionBankStore.SerializeDraft(unknown);
                Check(BasicQuestionValidator.Validate(pending,c).NeedsReview,"Unknown practical PDF prose did not require review: "+profile);
                Check(await store.QueueReviewAsync(PendingQuestionReview.Create(c,unknown,pending,"practical-pdf","review")),"Cannot queue practical PDF review.");
                Check(await store.TakeReasoningAsync(c.Family,c.BankVariant,tier,language) is null,"Pending practical draft entered the active PDF bank.");
                Check(await store.InsertAsync(new(c,draft,raw,"practical-pdf",DateTime.UtcNow)),"Cannot save practical PDF reference.");
                var factory=new PracticeQuestionFactory(new(store,new AlwaysBank()));
                foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var q=await factory.CreateAsync(target.Request,mode,new(tier,false),language,null,PracticeQuestionFormat.WordProblem,false);
                    Check(new ArithmeticQuizValidator(new BasicArithmeticEngine()).Validate(q).IsValid,"Invalid practical model reached the PDF factory: "+profile);
                    var expected=CheckSource(q,profile,language);
                    var printed=QuizWorksheetBuilder.Format(q,language);
                    Check(!printed.Prompt.Contains("{f",StringComparison.Ordinal)&&!printed.Prompt.Contains("{v",StringComparison.Ordinal),"Unbound practical PDF slots.");
                    Check(printed.Prompt.Contains(expected.Actor,StringComparison.OrdinalIgnoreCase),"Practical PDF lost its bound owner/object.");
                    foreach(string equation in expected.Equations)
                        Check(CompactMath(printed.Solution,language).Contains(CompactMath(equation,language),StringComparison.Ordinal),"Practical PDF working lost actual operands: "+equation+" in "+printed.Solution);
                    if(mode==ArithmeticQuizMode.TrueFalse)
                        Check(printed.Answer==(q.PresentedEquationIsCorrect==true?printed.Options[0]:printed.Options[1]),"Practical PDF true/false key changed.");
                    else
                        Check(CompactMath(printed.Answer,language).EndsWith(CompactMath(expected.Answer+" "+expected.Unit,language),StringComparison.Ordinal),"Practical PDF answer value/unit changed.");
                    if(mode==ArithmeticQuizMode.MultipleChoice)
                    {
                        var canonicalOptions=printed.Options.Select(x=>CompactMath(x,language)).ToArray();
                        Check(canonicalOptions.Length==4&&canonicalOptions.Distinct().Count()==4
                            &&canonicalOptions.Count(x=>x==CompactMath(expected.Answer+" "+expected.Unit,language))==1,"Practical PDF choices lost their unique exact answer.");
                    }
                    if(expected.Target.StartsWith("rectangle-",StringComparison.Ordinal))
                        Check(printed.Visual is {Kind:"missing-side"}&&printed.Visual.Annotations!.Count(x=>x.Text=="?")==1
                            &&printed.Visual.AccessibleDescription is null&&printed.Visual.Labels.Count==0,"Practical worksheet leaked a missing-side answer.");
                    batches[mode].Add((printed,expected));
                    traces.Add(new{Language=language.ToString(),Tier=tier.ToString(),Mode=mode.ToString(),expected.Profile,expected.Target,
                        Answer=expected.Answer.ToString(),expected.Unit,expected.Equations,printed.Identity});cases++;
                }
            }
            foreach(var (mode,items) in batches)
            {
                Check(items.Count==50&&items.Select(x=>x.Expected.Target).Distinct().Count()==5,"Practical PDF matrix missed a context, tier or target.");
                string title=language==AppLanguage.Vietnamese?"Toán thực hành":"Practical mathematics";
                var sheet=new QuizWorksheet("PRACTICAL-OPEN",title,
                    language==AppLanguage.Vietnamese?"Tỉ số phần trăm, tiền thừa, diện tích tam giác, chiều dài và chiều rộng":"Percentage ratio, change, triangle area, rectangle length and width",
                    language,CurriculumTier.ThreeStars,mode,items.Select(x=>x.Item).ToArray());
                string before=JsonSerializer.Serialize(sheet);
                foreach(bool answers in new[]{false,true})
                {
                    byte[] pdf=QuizPdfWriter.Write(sheet,answers);PdfAssertions.HasEmbeddedText(pdf);
                    string text=PdfTestReader.ExtractText(pdf),flat=CompactMath(text,language);
                    Check(flat.Contains(CompactMath(title,language),StringComparison.Ordinal),"Practical PDF lost its bilingual title.");
                    Check(!text.Contains("{f",StringComparison.Ordinal)&&!text.Contains("{v",StringComparison.Ordinal),"Practical PDF text contains raw placeholders.");
                    if(!answers)
                    {
                        Check(!text.Contains(language==AppLanguage.Vietnamese?"Đáp số:":"Answer:",StringComparison.Ordinal),"Practical worksheet printed the answer key.");
                        foreach(var item in items)
                            Check(flat.Contains(CompactMath(item.Expected.Actor,language),StringComparison.OrdinalIgnoreCase),"Practical worksheet lost its bound actor/object in embedded text.");
                    }
                    else
                        foreach(var item in items)
                        {
                            foreach(string equation in item.Expected.Equations)
                                Check(flat.Contains(CompactMath(equation,language),StringComparison.Ordinal),"Practical answer PDF lost actual C# working: "+equation);
                            Check(flat.Contains(CompactMath(item.Expected.Answer+" "+item.Expected.Unit,language),StringComparison.Ordinal),"Practical answer PDF lost its final value/unit.");
                        }
                    File.WriteAllBytes(Path.Combine(directory,$"practical-open-{language}-{mode}-{(answers?"answers":"worksheet")}.pdf"),pdf);pdfs++;
                }
                Check(before==JsonSerializer.Serialize(sheet),"Practical PDF export mutated frozen facts, choices, visual or working.");
            }
        }
        Check(cases==300&&pdfs==12,"Practical PDF matrix must cover ten contexts, two languages, five tiers and three modes.");
        File.WriteAllText(Path.Combine(directory,"practical-open-cases.json"),JsonSerializer.Serialize(traces,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS {cases} practical factory/mode cases and {pdfs} bilingual PDFs; five targets, independent arithmetic, pending isolation, actual C# equations/units and frozen worksheets.");
    }
    private static Expected CheckSource(ArithmeticQuizQuestion q,string profile,AppLanguage language)
    {
        if(profile.StartsWith(PercentageRatioPilot.Prefix,StringComparison.Ordinal))
        {
            var p=q.PercentageProblem!;Check(p.StoryContextId==profile&&PercentageRatioPilot.ValidContract(p),"PDF percentage ratio profile changed.");
            int whole=p.Facts[0],part=p.Facts[1];
            int ratio=Enumerable.Range(1,99).Single(rate=>part*100==whole*rate);
            Check(p.CorrectAnswer==ratio&&p.AnswerUnit=="%"&&part<whole,"Independent percentage subset ratio failed.");
            string actor=PercentageRatioPilot.Contexts(language).Single(x=>PercentageRatioPilot.Prefix+x.Id==profile).Owner;
            return new(profile,"percentage-ratio",ratio,"%",actor,[$"{part} ÷ {whole} × 100 = {ratio} %"]);
        }
        if(profile.StartsWith(MoneyChangePilot.Prefix,StringComparison.Ordinal))
        {
            var p=q.ElementaryProblem!;Check(p.StoryContextId==profile&&MoneyChangePilot.IsValidModel(p),"PDF money change profile changed.");
            var given=p.Reasoning!.Givens.ToDictionary(x=>x.Role,x=>int.Parse(x.Value));
            int price=given["unit-price"],count=given["purchase-count"],cash=given["cash-tendered"],cost=Enumerable.Repeat(price,count).Sum(),change=cash-cost;
            var context=MoneyChangePilot.Contexts(language).Single(x=>MoneyChangePilot.Prefix+x.Id==profile);
            Check(p.Answers.Single().Value==new ReducedFraction(change,1)&&change>0,"Independent cash minus repeated purchase cost failed.");
            return new(profile,"money-change",change,context.MoneyUnit,context.Buyer,
                [$"{price} × {count} = {cost} {context.MoneyUnit}",$"{cash} − ({price} × {count}) = {change} {context.MoneyUnit}"]);
        }
        if(profile.StartsWith(TriangleAreaPilot.Prefix,StringComparison.Ordinal))
        {
            var g=q.GeometryProblem!;Check(TriangleAreaPilot.ValidContract(g),"PDF triangle base/height profile changed.");
            var context=TriangleAreaPilot.Contexts(language).Single(x=>TriangleAreaPilot.Prefix+x.Id==profile);
            Check(g.ObjectName==context.Object&&g.LengthUnitSymbol==GeometryUnitFormatter.GetLengthSymbol(context.Unit),"PDF triangle context/unit changed.");
            BigInteger basis=g.Dimensions["a"],height=g.Dimensions["h"],doubled=0;
            for(int row=0;row<height;row++)doubled+=basis;
            Check(doubled.IsEven&&g.CorrectAnswer*2==doubled&&g.Reasoning!.Givens.Count==2&&g.UnitPower==2,"Independent triangle doubled-area count failed.");
            return new(profile,"triangle-area",doubled/2,g.AnswerUnit,g.ObjectName,[$"({basis} × {height}) ÷ 2 = {doubled/2} {g.AnswerUnit}"]);
        }
        var side=q.ElementaryProblem!;
        Check(side.StoryContextId==profile&&RectangleSidePilot.IsValidModel(side),"PDF rectangle-side target/profile changed.");
        var contextSide=RectangleSidePilot.Contexts(language).Single(x=>RectangleSidePilot.Prefix+x.Id==profile);
        var sides=side.Reasoning!.Givens.ToDictionary(x=>x.Role,x=>int.Parse(x.Value));
        int perimeter=sides["rectangle-perimeter"],known=sides["rectangle-known-"+contextSide.KnownRole];
        int missing=Enumerable.Range(1,perimeter).Single(value=>value+known+value+known==perimeter);
        string unit=GeometryUnitFormatter.GetLengthSymbol(contextSide.Unit);
        Check(side.Answers.Single().Value==new ReducedFraction(missing,1),"Independent rectangle perimeter walk failed.");
        return new(profile,"rectangle-"+contextSide.TargetRole,missing,unit,contextSide.Object,
            [$"{perimeter} : 2 = {perimeter/2} {unit}",$"{perimeter/2} − {known} = {missing} {unit}"]);
    }
    // Fraction bars are vectors. Text extraction retains numerator then
    // denominator, so remove division glyphs while preserving all actual
    // operands, grouping, remaining operations, result and unit in order.
    private static string CompactMath(string text,AppLanguage language)
    {
        // Only ungroup complete integer tokens in the output language. Decimal
        // punctuation (including English 2.970 and Vietnamese 2.970,5) remains
        // significant; deleting dots or commas globally would conceal errors.
        string separator=language==AppLanguage.Vietnamese?".":",";
        text=Regex.Replace(text,@"(?<![\d.,])\d{1,3}(?:"+Regex.Escape(separator)+@"\d{3})+(?![\d.,])",
            match=>match.Value.Replace(separator,string.Empty,StringComparison.Ordinal));
        return Regex.Replace(text,@"\s+",string.Empty)
            .Replace("−","-",StringComparison.Ordinal).Replace("*","×",StringComparison.Ordinal)
            .Replace("÷",string.Empty,StringComparison.Ordinal).Replace(":",string.Empty,StringComparison.Ordinal).Replace("/",string.Empty,StringComparison.Ordinal);
    }
    private static void Check(bool value,string message)=>TestAssert.That(value,message);
}
