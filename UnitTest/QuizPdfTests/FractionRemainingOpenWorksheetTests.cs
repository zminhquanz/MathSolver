using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;

internal static class FractionRemainingOpenWorksheetTests
{
    private sealed class AlwaysBank:Random {public override int Next(int maxValue)=>maxValue-1;}
    internal static async Task Run(string directory)
    {
        int cases=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var items=new List<QuizPrintItem>();
            foreach(var context in FractionRemainingPilot.Contexts(language))
            foreach(var tier in Enum.GetValues<CurriculumTier>())
            {
                string profile=FractionRemainingPilot.Prefix+context.Id;
                var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.FractionQuantity,(int)ElementaryQuizType.FractionRemaining,tier,language,new(713),profile) with {OpenProse=true};
                var draft=ReasoningStoryCatalogue.Draft(c);
                draft=draft with {Facts=draft.Facts!.Select((f,i)=>i==0?f with {Text=language==AppLanguage.Vietnamese?"Trong kho của {v0} có {f0} {v1}.":"{f0} {v1} belong to {v0}."}:f).ToArray()};
                Check(BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft),c).IsValid,"PDF fixture not verified.");
                var store=new QuestionBankStore(Path.Combine(directory,$"fraction-remaining-{language}-{context.Id}-{tier}.db3"));
                var unknown=draft with {Question=draft.Question+(language==AppLanguage.Vietnamese?" Theo cách mới.":" In a new way.")};
                string raw=QuestionBankStore.SerializeDraft(unknown);
                Check(BasicQuestionValidator.Validate(raw,c).NeedsReview,"Unknown PDF fixture should need review.");
                Check(await store.QueueReviewAsync(PendingQuestionReview.Create(c,unknown,raw,"synthetic-pdf-fixture","review")),"Cannot queue fraction.");
                Check(await store.TakeReasoningAsync(c.Family,c.BankVariant,tier,language) is null,"Pending fraction leaked into PDF.");
                Check(await store.InsertAsync(new(c,draft,QuestionBankStore.SerializeDraft(draft),"verified-pdf-fixture",DateTime.UtcNow)),"Cannot insert fraction.");
                var factory=new PracticeQuestionFactory(new(store,new AlwaysBank()));
                foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var q=await factory.CreateAsync(new(QuizProblemKind.FractionSkills,ElementaryType:ElementaryQuizType.FractionRemaining),mode,new(tier,false),language,null,PracticeQuestionFormat.WordProblem,false);
                    var p=q.ElementaryProblem!;
                    Check(p.StoryContextId==profile&&p.Reasoning!.Steps.Count==1,"PDF provider missed single-donation remainder pilot.");
                    var givens=p.Reasoning!.Givens.ToDictionary(g=>g.Role,g=>int.Parse(g.Value));
                    int result=givens["whole"]*(givens["donated-denominator"]-givens["donated-numerator"])/givens["donated-denominator"];
                    Check(p.Answers.Single().Value==new ReducedFraction(result,1),"PDF answer disagrees with C# arithmetic.");
                    var printed=QuizWorksheetBuilder.Format(q,language);
                    var row=printed.Diagram!.Rows.Single();
                    Check(row.FractionNumerator==givens["donated-numerator"]&&row.FractionDenominator==givens["donated-denominator"]
                        &&printed.Diagram.Caption.Contains(givens["whole"].ToString())&&printed.Diagram.Explanation is null,
                        "Worksheet diagram must show original data and donated fraction without the remaining answer.");
                    Check(printed.Prompt.Contains(context.Donor)&&printed.Solution.Contains(context.Unit),"PDF omitted owner or unit.");
                    Check(printed.Prompt.Contains(context.Recipient)&&printed.Solution.IndexOfAny(['−','-'])>=0,"PDF lost the recipient or subtraction.");
                    var direct=new ElementaryQuizGenerator(new(713)).Generate(mode,QuizProblemKind.FractionSkills,ElementaryQuizType.FractionRemaining,language,tier);
                    Check(QuizWorksheetBuilder.Format(direct,language).Solution.IndexOfAny(['−','-'])>=0,"Algorithm-only PDF lost the remainder calculation.");
                    if(mode==ArithmeticQuizMode.Essay)items.Add(printed);
                    cases++;
                }
            }
            var sheet=new QuizWorksheet("FRACTION-REMAINING",language==AppLanguage.Vietnamese?"Tìm phần còn lại":"Find the remaining quantity",
                "Known whole and donated fraction: find the remainder",language,CurriculumTier.ThreeStars,ArithmeticQuizMode.Essay,items.AsReadOnly());
            string snapshot=JsonSerializer.Serialize(sheet);
            foreach(bool answers in new[]{false,true})
            {
                var pdf=QuizPdfWriter.Write(sheet,answers);PdfAssertions.HasEmbeddedText(pdf);
                File.WriteAllBytes(Path.Combine(directory,$"fraction-remaining-open-{language}-{(answers?"answers":"worksheet")}.pdf"),pdf);
                string text=PdfTestReader.ExtractText(pdf);
                Check(!text.Contains("{v")&&!text.Contains("{f"),"Unbound fraction PDF variables.");
                Check(text.Contains(language==AppLanguage.Vietnamese?"Tìm phần":"Find the remaining"),"Fraction title missing.");
            }
            Check(snapshot==JsonSerializer.Serialize(sheet),"Printing resampled fraction data.");
        }
        Console.WriteLine($"PASS {cases} fraction-remaining factory/mode cases; five tiers, review isolation, frozen bilingual worksheet/answer PDFs.");
    }
    private static void Check(bool b,string message)=>TestAssert.That(b,message);
}
