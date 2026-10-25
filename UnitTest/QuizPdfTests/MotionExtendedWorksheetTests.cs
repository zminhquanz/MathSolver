using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.Core;
using MathSolver.Services.Printing;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static class MotionExtendedWorksheetTests
{
    private sealed class AlwaysBank:Random {public override int Next(int maxValue)=>maxValue-1;}
    internal static async Task Run(string directory)
    {
        int count=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        {
            var items=new List<QuizPrintItem>();
            foreach(string profile in MotionExtendedPilot.Profiles(language))
            foreach(var tier in Enum.GetValues<CurriculumTier>())
            {
                var data=MotionExtendedPilot.Create(profile,language,tier,new(713));
                var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Motion,(int)data.Relation.Type,tier,language,new(713),profile) with {OpenProse=true};
                var d=ReasoningStoryCatalogue.Draft(c);data=MotionExtendedPilot.Create(profile,language,tier,new(c.Story!.Seed));
                var store=new QuestionBankStore(Path.Combine(directory,$"motion-extended-{language}-{profile.Split('/')[1]}-{profile.Split('/')[2]}-{tier}.db3"));
                var unknown=d with {Question=d.Question+(language==AppLanguage.Vietnamese?" Theo cách mới.":" Under a new interpretation.")};string raw=QuestionBankStore.SerializeDraft(unknown);
                Check(BasicQuestionValidator.Validate(raw,c).NeedsReview,"Unknown extended motion PDF prose not pending.");
                Check(await store.QueueReviewAsync(PendingQuestionReview.Create(c,unknown,raw,"fixture","review")),"Cannot queue extended motion PDF draft.");
                Check(await store.TakeReasoningAsync(c.Family,c.BankVariant,tier,language) is null,"Pending extended motion draft reached PDF bank.");
                Check(await store.InsertAsync(new(c,d,QuestionBankStore.SerializeDraft(d),"fixture",DateTime.UtcNow)),"Cannot save extended motion PDF reference.");
                var factory=new PracticeQuestionFactory(new(store,new AlwaysBank()));
                foreach(var mode in Enum.GetValues<ArithmeticQuizMode>())
                {
                    var direct=new MotionQuizGenerator(new(713)).GenerateNarrative(mode,language,data.Relation.Type,new(tier,false),profile);
                    Check(direct.MotionProblem!.NarrativeId==profile&&MotionExtendedPilot.ValidContract(direct.MotionProblem),"GenerateNarrative did not route extended profiles.");
                    var q=await factory.CreateAsync(new(QuizProblemKind.Motion,MotionType:data.Relation.Type),mode,new(tier,false),language,null,PracticeQuestionFormat.WordProblem,false);
                    var m=q.MotionProblem!;
                    Check(m.NarrativeId==profile&&m.QuestionKind==data.Relation.Kind&&MotionExtendedPilot.ValidContract(m),"PDF/factory changed extended motion target/profile/facts.");
                    Check(new ArithmeticQuizValidator(new BasicArithmeticEngine()).Validate(q).IsValid,"PDF received an invalid extended motion contract.");
                    int expected=IndependentAnswer(profile.Split('/')[1],m.Facts);
                    Check(m.CorrectAnswer==expected&&q.CorrectAnswer==expected,"PDF motion answer disagrees with physical quantities.");
                    string equation=ExpectedEquation(profile.Split('/')[1],m.Facts,expected);
                    Check(m.EquationText==equation,"Extended PDF source equation lost actual C# operands.");
                    var printed=QuizWorksheetBuilder.Format(q,language);
                    Check(printed.Solution.Contains(equation+" "+m.AnswerUnit,StringComparison.Ordinal),"PDF lost exact C# extended motion equation/unit.");
                    if(mode!=ArithmeticQuizMode.TrueFalse)Check(printed.Answer.EndsWith(expected+" "+m.AnswerUnit,StringComparison.Ordinal),"Wrong extended motion PDF answer unit.");
                    else Check(printed.Answer==(q.PresentedEquationIsCorrect==true?printed.Options[0]:printed.Options[1]),"Extended motion PDF assertion key changed.");
                    Check(!printed.Prompt.Contains("{f",StringComparison.Ordinal)&&!printed.Prompt.Contains("{v",StringComparison.Ordinal),"Extended motion PDF contains raw slots.");
                    Check(printed.Prompt.Contains(data.Context.First,StringComparison.Ordinal)&&printed.Prompt.Contains("km/h",StringComparison.Ordinal),"Extended PDF lost actor/unit text.");
                    if(mode==ArithmeticQuizMode.Essay&&tier==CurriculumTier.ThreeStars)items.Add(printed);count++;
                }
            }
            Check(items.Count==12,"Bilingual PDF sample must have all six targets and both contexts.");
            string title=language==AppLanguage.Vietnamese?"Chuyển động trên đường và trên sông":"Road and river motion";
            var sheet=new QuizWorksheet("MOTION-EXTENDED",title,"Meeting, chasing, downstream, upstream, still water and current",language,CurriculumTier.ThreeStars,ArithmeticQuizMode.Essay,items.AsReadOnly());
            string before=JsonSerializer.Serialize(sheet);
            foreach(bool answers in new[]{false,true})
            {
                byte[] pdf=QuizPdfWriter.Write(sheet,answers);File.WriteAllBytes(Path.Combine(directory,$"motion-extended-{language}-{(answers?"answers":"worksheet")}.pdf"),pdf);
                PdfAssertions.HasEmbeddedText(pdf);string text=PdfTestReader.ExtractText(pdf),flat=Compact(text);
                Check(flat.Contains(Compact(title),StringComparison.Ordinal),"Extended PDF lost its bilingual title.");
                Check(!text.Contains("{f",StringComparison.Ordinal)&&!text.Contains("{v",StringComparison.Ordinal),"PDF contains raw extended motion slots.");
                foreach(var item in items)
                {
                    var m=item.Source.MotionProblem!;
                    if(!answers)Check(flat.Contains(Compact(m.NarrativeActors[0]),StringComparison.Ordinal),"PDF text extraction lost the rendered motion actor.");
                    if(answers)
                    {
                        // Fractions are vector layout, so match the extracted operands in reading order after removing spaces.
                        string arithmetic=Compact(m.EquationText).Replace("÷",string.Empty,StringComparison.Ordinal);
                        string visible=flat.Replace("÷",string.Empty,StringComparison.Ordinal);
                        Check(visible.Contains(arithmetic,StringComparison.Ordinal),"PDF answer text lost an exact C# equation: "+m.EquationText);
                        Check(flat.Contains(Compact(m.CorrectAnswer+" "+m.AnswerUnit),StringComparison.Ordinal),"PDF answer text lost final motion value/unit.");
                    }
                }
            }
            Check(before==JsonSerializer.Serialize(sheet),"Extended motion PDF changed frozen worksheet data.");
        }
        Check(count==360,"Extended motion PDF factory matrix must cover 120 profiles in all three modes.");
        Console.WriteLine($"PASS {count} extended motion factory/mode/PDF cases; six targets, independent arithmetic, pending isolation, exact C# equations, bilingual text and frozen worksheets.");
    }
    private static int IndependentAnswer(string relation,IReadOnlyList<int> f)=>relation switch {
        "meeting"=>Enumerable.Range(1,1000).Single(t=>t*(f[1]+f[2])==f[0]),
        "chasing"=>Enumerable.Range(1,1000).Single(t=>t*(f[2]-f[1])==f[0]),
        "downstream"=>f[0]+f[1],"upstream"=>f[0]-f[1],
        "boat"=>Enumerable.Range(1,1000).Single(b=>2*b==f[0]+f[1]),
        "current"=>Enumerable.Range(1,1000).Single(v=>f[1]+2*v==f[0]),_=>throw new InvalidOperationException()};
    private static string ExpectedEquation(string relation,IReadOnlyList<int> f,int answer)=>relation switch {
        "meeting"=>$"{f[0]} ÷ ({f[1]} + {f[2]}) = {answer}","chasing"=>$"{f[0]} ÷ ({f[2]} - {f[1]}) = {answer}",
        "downstream"=>$"{f[0]} + {f[1]} = {answer}","upstream"=>$"{f[0]} - {f[1]} = {answer}",
        "boat"=>$"({f[0]} + {f[1]}) ÷ 2 = {answer}",_=>$"({f[0]} - {f[1]}) ÷ 2 = {answer}"};
    private static string Compact(string text)=>Regex.Replace(text,@"\s+",string.Empty);
    private static void Check(bool value,string message)=>TestAssert.That(value,message);
}
