using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;

internal static class FractionWholeOpenWorksheetTests
{
    private sealed class AlwaysBank:Random {public override int Next(int maxValue)=>maxValue-1;}
    internal static async Task Run(string directory)
    {
        int cases=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var items=new List<QuizPrintItem>();
            foreach(var context in FractionWholePilot.Contexts(language))
            foreach(var tier in Enum.GetValues<CurriculumTier>())
            {
                string profile=FractionWholePilot.Prefix+context.Id;
                var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.FractionQuantity,(int)ElementaryQuizType.WholeFromFraction,tier,language,new(713),profile) with {OpenProse=true};
                var draft=ReasoningStoryCatalogue.Draft(c);
                draft=draft with {Facts=[new("fact_0",language==AppLanguage.Vietnamese?"{v2} nhận được {f0} {v1} từ {v0}.":"{v2} received {f0} {v1} from {v0}."),draft.Facts![1]]};
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft),c).IsValid,"PDF fixture not verified.");
                var store=new QuestionBankStore(Path.Combine(directory,$"fraction-whole-{language}-{context.Id}-{tier}.db3"));
                var unknown=draft with {Question=draft.Question+(language==AppLanguage.Vietnamese?" Theo cách mới.":" In a new way.")};
                string raw=QuestionBankStore.SerializeDraft(unknown);
                Check(BasicQuestionValidator.Validate(raw,c).NeedsReview,"Unknown fixture must await review.");
                Check(await store.QueueReviewAsync(PendingQuestionReview.Create(c,unknown,raw,"synthetic-pdf","review")),"Cannot queue whole draft.");
                Check(await store.TakeReasoningAsync(c.Family,c.BankVariant,tier,language) is null,"Pending whole leaked into PDF bank.");
                Check(await store.InsertAsync(new(c,draft,QuestionBankStore.SerializeDraft(draft),"synthetic-verified",DateTime.UtcNow)),"Cannot insert verified whole.");
                var factory=new PracticeQuestionFactory(new(store,new AlwaysBank()));
                foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var q=await factory.CreateAsync(new(QuizProblemKind.FractionSkills,ElementaryType:ElementaryQuizType.WholeFromFraction),mode,new(tier,false),language,null,PracticeQuestionFormat.WordProblem,false);
                    var p=q.ElementaryProblem!;
                    Check(p.StoryContextId==profile&&p.Reasoning!.Steps.Count==1,"Provider missed the single-gift whole profile.");
                    var givens=p.Reasoning!.Givens.ToDictionary(g=>g.Role,g=>int.Parse(g.Value));
                    int whole=givens["donated-count"]*givens["denominator"]/givens["numerator"];
                    Check(!givens.ContainsKey("whole")&&p.Answers.Single().Value==new ReducedFraction(whole,1),"Whole answer/givens wrong.");
                    var printed=QuizWorksheetBuilder.Format(q,language);var diagram=printed.Diagram!;var row=diagram.Rows.Single();
                    Check(row.FractionNumerator==givens["numerator"]&&row.FractionDenominator==givens["denominator"]
                        &&diagram.Caption.EndsWith("? "+context.Unit)&&diagram.Explanation is null,"Worksheet exposed the unknown whole or wrong fraction.");
                    Check(row.Label.Contains(givens["donated-count"].ToString())&&row.Label.Contains(context.Unit),"Known donated count missing from diagram.");
                    Check(printed.Prompt.Contains(context.Donor,StringComparison.OrdinalIgnoreCase)&&printed.Prompt.Contains(context.Recipient,StringComparison.OrdinalIgnoreCase)
                        &&printed.Solution.Contains(context.Unit),"PDF omitted a party or unit.");
                    Check(printed.Solution.Contains('÷')&&printed.Solution.Contains('×'),"PDF lost the inverse calculation.");
                    // The explicit profile is also reachable directly through the C# fraction-story API.
                    var direct=new ElementaryQuizGenerator(new(713)).GenerateFractionQuantityStory(mode,ElementaryQuizType.WholeFromFraction,language,tier,profile);
                    Check(direct.ElementaryProblem!.StoryContextId==profile&&QuizWorksheetBuilder.Format(direct,language).Solution.Contains('÷'),"Direct C# story API missed whole pilot.");
                    if(mode==ArithmeticQuizMode.Essay)items.Add(printed);
                    cases++;
                }
            }
            var sheet=new QuizWorksheet("FRACTION-WHOLE",language==AppLanguage.Vietnamese?"Tìm tổng số ban đầu":"Find the original whole",
                "Known donated count and fraction: find the original whole",language,CurriculumTier.ThreeStars,ArithmeticQuizMode.Essay,items.AsReadOnly());
            string snapshot=JsonSerializer.Serialize(sheet);
            foreach(bool answers in new[]{false,true})
            {
                var pdf=QuizPdfWriter.Write(sheet,answers);PdfAssertions.HasEmbeddedText(pdf);
                File.WriteAllBytes(Path.Combine(directory,$"fraction-whole-open-{language}-{(answers?"answers":"worksheet")}.pdf"),pdf);
                string text=PdfTestReader.ExtractText(pdf);
                Check(!text.Contains("{v")&&!text.Contains("{f"),"Unbound whole PDF variables.");
                Check(text.Contains(language==AppLanguage.Vietnamese?"Tìm tổng":"Find the original"),"Whole title missing.");
                if(!answers)Check(!text.Contains(language==AppLanguage.Vietnamese?"Đáp số:":"Answer:"),"Worksheet contains answer text.");
            }
            Check(snapshot==JsonSerializer.Serialize(sheet),"PDF export resampled the preview.");
        }
        Console.WriteLine($"PASS {cases} fraction-whole factory/mode cases; inverse arithmetic, review isolation, unknown-whole diagrams, frozen bilingual worksheet/answer PDFs.");
    }
    private static void Check(bool b,string message)=>TestAssert.That(b,message);
}
