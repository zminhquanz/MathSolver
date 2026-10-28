using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using System.Globalization;
using System.Numerics;
using System.Text.Json;

internal static class AppliedOpenWorksheetTests
{
    private sealed class AlwaysBank:Random {public override int Next(int maxValue)=>maxValue-1;}
    private sealed record Target(BankQuestionFamily Family,int Variant,QuizProblemRequest Request);
    private static readonly Target[] Targets=[
        new(BankQuestionFamily.Measurement,MapScaleOpenPilot.Variant,new(QuizProblemKind.Measurement,ElementaryType:ElementaryQuizType.MapScale)),
        new(BankQuestionFamily.MultiStep,DiscountOpenPilot.Variant,new(QuizProblemKind.MultiStep,ElementaryType:ElementaryQuizType.MultiStepRemaining)),
        new(BankQuestionFamily.Geometry,QuadrilateralAreaPilot.ParallelogramVariant,new(QuizProblemKind.Geometry,GeometryShape:GeometryQuizShape.Parallelogram,GeometryMeasurement:GeometryMeasurement.Area)),
        new(BankQuestionFamily.Geometry,QuadrilateralAreaPilot.TrapezoidVariant,new(QuizProblemKind.Geometry,GeometryShape:GeometryQuizShape.Trapezoid,GeometryMeasurement:GeometryMeasurement.Area))];

    internal static async Task Run(string directory)
    {
        int cases=0,pdfs=0;var traces=new List<object>();
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var batches=Enum.GetValues<ArithmeticQuizMode>().ToDictionary(mode=>mode,_=>new List<QuizPrintItem>());
            foreach(var target in Targets)
            foreach(string profile in AppliedOpenCatalogue.Profiles(target.Family,target.Variant,language))
            foreach(var tier in Enum.GetValues<CurriculumTier>())
            {
                var c=ReasoningStoryCatalogue.Create(target.Family,target.Variant,tier,language,new(2810),profile) with {OpenProse=true};
                var d=ReasoningStoryCatalogue.Draft(c);string raw=QuestionBankStore.SerializeDraft(d);
                Check(BasicQuestionValidator.Validate(raw,c).IsValid,"PDF reference failed meaning proof: "+profile);
                var store=new QuestionBankStore(Path.Combine(directory,$"applied-{language}-{profile.Replace('/','-')}-{tier}.db3"));
                var unknown=d with {Question=(language==AppLanguage.Vietnamese?"Theo ghi chép, ":"According to the record, ")+d.Question};
                string pending=QuestionBankStore.SerializeDraft(unknown);
                Check(BasicQuestionValidator.Validate(pending,c).NeedsReview,"Unproved applied PDF prose trusted.");
                await store.QueueReviewAsync(PendingQuestionReview.Create(c,unknown,pending,"pdf","review"));
                Check(await store.TakeReasoningAsync(c.Family,c.BankVariant,tier,language) is null,"Review entered PDF active bank.");
                Check(await store.InsertAsync(new(c,d,raw,"pdf",DateTime.UtcNow)),"Cannot persist applied PDF reference.");
                var factory=new PracticeQuestionFactory(new(store,new AlwaysBank()));
                foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var q=await factory.CreateAsync(target.Request,mode,new(tier,false),language,null,PracticeQuestionFormat.WordProblem,false);
                    Check(new ArithmeticQuizValidator(new BasicArithmeticEngine()).Validate(q).IsValid,"Applied PDF model invalid.");
                    var (answer,unit)=CheckArithmetic(q,profile);
                    var printed=QuizWorksheetBuilder.Format(q,language);
                    Check(!printed.Prompt.Contains("{f")&&!printed.Prompt.Contains("{v"),"Unbound PDF placeholders.");
                    if(mode==ArithmeticQuizMode.TrueFalse)
                        Check(printed.Answer==(q.PresentedEquationIsCorrect==true?printed.Options[0]:printed.Options[1]),"Applied PDF truth key mismatch.");
                    else Check(Compact(printed.Answer).EndsWith(Compact(answer+" "+unit),StringComparison.Ordinal),"PDF final value/unit mismatch.");
                    if(mode==ArithmeticQuizMode.MultipleChoice)
                        Check(printed.Options.Count==4&&printed.Options.Select(Compact).Distinct().Count()==4,"Applied PDF duplicate options.");
                    batches[mode].Add(printed);traces.Add(new{Profile=profile,Language=language.ToString(),Tier=tier.ToString(),Mode=mode.ToString(),Answer=answer.ToString(),Unit=unit});cases++;
                }
            }
            foreach(var (mode,items) in batches)
            {
                Check(items.Count==40,"Applied PDF missed context/tier.");
                string title=language==AppLanguage.Vietnamese?"Bản đồ, giảm giá và diện tích":"Maps, discounts and areas";
                var sheet=new QuizWorksheet("APPLIED-OPEN",title,title,language,CurriculumTier.ThreeStars,mode,items.ToArray());
                string frozen=JsonSerializer.Serialize(sheet);
                foreach(bool answers in new[]{false,true})
                {
                    byte[] pdf=QuizPdfWriter.Write(sheet,answers);PdfAssertions.HasEmbeddedText(pdf);
                    string text=PdfTestReader.ExtractText(pdf);
                    Check(!text.Contains("{f")&&!text.Contains("{v"),"PDF contains raw slots.");
                    Check(Compact(text).Contains(Compact(title),StringComparison.Ordinal),"PDF lost bilingual title.");
                    if(!answers)Check(!text.Contains(language==AppLanguage.Vietnamese?"Đáp số:":"Answer:"),"Worksheet leaked answer key.");
                    File.WriteAllBytes(Path.Combine(directory,$"applied-open-{language}-{mode}-{(answers?"answers":"worksheet")}.pdf"),pdf);pdfs++;
                }
                Check(frozen==JsonSerializer.Serialize(sheet),"Applied PDF export mutated worksheet.");
            }
        }
        Check(cases==240&&pdfs==12,"Expected 240 applied mode cases and 12 PDFs.");
        File.WriteAllText(Path.Combine(directory,"applied-open-cases.json"),JsonSerializer.Serialize(traces,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS {cases} applied bank/factory/mode cases, independent arithmetic/unit checks, pending isolation and {pdfs} bilingual PDFs.");
    }

    private static (BigInteger Answer,string Unit) CheckArithmetic(ArithmeticQuizQuestion q,string profile)
    {
        if(q.ElementaryProblem is { } e)
        {
            Check(e.StoryContextId==profile,"Factory lost applied elementary profile.");
            var givens=e.Reasoning!.Givens.ToDictionary(g=>g.Role);BigInteger expected;
            if(profile.StartsWith(MapScaleOpenPilot.Prefix,StringComparison.Ordinal))
            {
                int scale=int.Parse(givens["scale-denominator"].Value,CultureInfo.InvariantCulture),length=int.Parse(givens["map-cm"].Value,CultureInfo.InvariantCulture);
                BigInteger cm=0;for(int i=0;i<length;i++)cm+=scale;
                expected=cm/100;
                Check(cm%100==0&&e.Answers[0].Unit=="m"&&e.Reasoning.Steps[0].Unit=="cm"&&MapScaleOpenPilot.IsValidModel(e),"PDF map direction/units changed.");
            }
            else
            {
                int price=int.Parse(givens["original-price"].Value,CultureInfo.InvariantCulture),rate=int.Parse(givens["discount-percentage-of-original"].Value,CultureInfo.InvariantCulture);
                expected=(BigInteger)price*(100-rate)/100;
                Check(DiscountOpenPilot.IsValidModel(e),"PDF original-price/discount roles changed.");
            }
            Check(e.Answers.Count==1&&e.Answers[0].Value==new ReducedFraction(expected,1),"PDF applied arithmetic mismatch.");
            return (expected,e.Answers[0].Unit);
        }
        var g=q.GeometryProblem!;Check(QuadrilateralAreaPilot.ValidContract(g),"Invalid direct area PDF contract.");
        BigInteger result=g.ShapeId=="parallelogram"?g.Dimensions["a"]*g.Dimensions["h"]:(g.Dimensions["a"]+g.Dimensions["b"])*g.Dimensions["h"]/2;
        Check(g.CorrectAnswer==result&&g.UnitPower==2,"PDF area arithmetic/power mismatch.");
        return (result,g.AnswerUnit);
    }
    private static string Compact(string s)=>string.Concat(s.Where(c=>!char.IsWhiteSpace(c))).Replace(".","").Replace(",","");
    private static void Check(bool okay,string message){if(!okay)throw new InvalidOperationException(message);}
}
