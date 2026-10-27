using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.RegularExpressions;

internal static class PracticalConsolidationTests
{
    internal static async Task RunAsync()
    {
        SharedChecks();
        await MoneyChangeConsolidationTests.RunAsync();
        await TriangleAreaConsolidationTests.RunAsync();
        await RectangleSideConsolidationTests.RunAsync();
        await PracticalOpenTests.RunAsync();
    }
    internal static void SharedChecks()
    {
        foreach(var (name,c) in PracticalOpenTests.NativeCases())
        {
            var lesson=ReasoningStoryCatalogue.Lesson(c);string prompt=BasicQuestionPrompt.Build(c);
            foreach(var quantity in lesson.Quantities.Where(q=>q.Id.StartsWith('v')))
                Check(prompt.Contains("{"+quantity.Id+"}="+quantity.Value,StringComparison.Ordinal),"Missing actual noun/shape binding in practical prompt: "+name+"/"+quantity.Id);
            var reference=ReasoningStoryCatalogue.Draft(c);
            Check(Validate(c,reference).IsValid,"Editorial routing changed a clear reference: "+name);
            if(c.Family==BankQuestionFamily.Geometry||c.Family==BankQuestionFamily.Spatial)
            {
                var facts=reference.Facts!.ToArray();facts[0]=facts[0] with {Text=facts[0].Text.Replace("{v1}",c.Language==AppLanguage.Vietnamese?"hình {v1}":"rectangle {v1}")};
                Check(!Validate(c,reference with {Facts=facts}).IsValid,"Duplicate full shape became verified: "+name);
            }
            foreach(string prefix in c.Language==AppLanguage.Vietnamese?new[]{"Tính ","Tìm ","Hãy tính "}:new[]{"Calculate ","Determine ","Subtract "})
            {
                var headings=reference.SolutionLeads!.ToArray();headings[^1]=headings[^1] with {Text=prefix+headings[^1].Text};
                Check(!Validate(c,reference with {SolutionLeads=headings}).IsValid,"Calculation instruction promoted as a noun heading.");
            }
            var technicalFacts=reference.Facts!.ToArray();technicalFacts[0]=technicalFacts[0] with {Text=(c.Language==AppLanguage.Vietnamese?"Actor A nói rằng ":"Actor A states that ")+technicalFacts[0].Text};
            var technical=Validate(c,reference with {Facts=technicalFacts});
            Check(!technical.IsValid&&!technical.NeedsReview,"Literal technical actor should be rejected, not saved or silently repaired.");
            string grammar=OpenQuestionProse.Grammar(c);
            Check(grammar.Contains("character ::=")&&!grammar.Contains(System.Text.Json.JsonSerializer.Serialize(reference.Question)),"Consolidation closed prose to whole sentence alternatives.");
        }
        CheckRatios();
        Console.WriteLine("PASS practical shared prompt bindings/editorial checks and percentage whole/subset contrasts.");
    }
    internal static void CheckRatios()
    {
        int clear=0,contrasts=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(string profile in PercentageRatioPilot.Profiles(language))
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            bool vi=language==AppLanguage.Vietnamese;
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Percentage,PercentageRatioPilot.Variant,tier,language,new(2713),profile) with {OpenProse=true};
            var d=ReasoningStoryCatalogue.Draft(c) with
            {
                Facts=[new("fact_0",vi?"{v0} chứa tổng cộng {f0} {v1}.":"{v0} owns a collection of {f0} {v1}."),
                    new("fact_1",vi?"Trong toàn bộ {v2} đó có {f1} {v3}.":"Among the {v2} owned, {f1} are {v3}.")],
                Question=vi?"Trong tổng số {v2} của {v0}, số {v4} chiếm tỉ lệ bao nhiêu phần trăm?":"Of the {v2} owned by {v0}, what percentage are {v4}?",
                SolutionLeads=[new("step_0",vi?"Tỉ lệ phần trăm {v4} so với tổng số {v2}":"Percentage of {v4} within all {v2}")]
            };
            Check(Validate(c,d).IsValid,"A complete static percentage relation with the same denominator was missed.");
            PercentageRatioOpenTests.CheckPractice(c,d);clear++;
            PercentageRatioOpenTests.CheckPractice(c.FreshFacts(new(7281)),d);
            foreach(string prefix in vi?new[]{"Không phải ","Nếu ","Chỉ khi ","Có thể "}:new[]{"It is not true that ","If ","Only if ","Possibly "})
            {
                for(int i=0;i<d.Facts.Count;i++)
                {
                    var facts=d.Facts.ToArray();facts[i]=facts[i] with {Text=prefix+facts[i].Text};
                    Check(!Validate(c,d with {Facts=facts}).IsValid,"Qualified percentage premise lost its qualifier.");contrasts++;
                }
                Check(!Validate(c,d with {Question=prefix+d.Question}).IsValid,"Qualified percentage target was promoted.");contrasts++;
            }
            var wrong=d.Facts.ToArray();wrong[1]=wrong[1] with {Text=vi?"Ngoài số {v2} đó còn có {f1} {v3}.":"In addition to those {v2}, there are {f1} {v3}."};
            Check(!Validate(c,d with {Facts=wrong}).IsValid,"A disjoint collection became a subset.");contrasts++;
            foreach(string target in vi?new[]{"Số {v2} của {v0} chiếm bao nhiêu phần trăm số {v4}?","{v0} có bao nhiêu {v4} trong số {v2}?"}
                :new[]{"What percentage of the {v4} are {v0}'s {v2}?","How many {v4} does {v0} have among the {v2}?"})
            {Check(!Validate(c,d with {Question=target}).IsValid,"Wrong percentage base/count target was verified.");contrasts++;}
        }
        Console.WriteLine($"PASS {clear} percentage consolidation profiles and {contrasts} hostile/target contrasts.");
    }
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static void Check(bool okay,string message)=>TestAssert.That(okay,message);
}
