using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.RegularExpressions;

internal static class DifferenceRatioEditorialTests
{
    internal static void Run()
    {
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(string profile in DifferenceRatioPilot.Profiles(language))
        {
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.TwoNumbers,(int)ElementaryQuizType.DifferenceRatio,
                CurriculumTier.ThreeStars,language,new(713),profile) with {OpenProse=true};
            string prompt=BasicQuestionPrompt.Build(c);
            Require(!prompt.Contains("một lượng",StringComparison.OrdinalIgnoreCase),"Prompt must not prime unnatural counted-item prose.");
            Require(!Regex.IsMatch(prompt,@"\b"+Regex.Escape(c.Unit)+@"\b",RegexOptions.IgnoreCase),"Difference-only prompt must keep literal item names behind their placeholder, including the profile hint.");
        }
        int clear=0,mutations=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(string profile in DifferenceRatioPilot.Profiles(language))
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            bool vi=language==AppLanguage.Vietnamese;
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.TwoNumbers,(int)ElementaryQuizType.DifferenceRatio,tier,language,new(713),profile) with {OpenProse=true};
            var d=ReasoningStoryCatalogue.Draft(c);
            var corrected=d with
            {
                Facts=[d.Facts![0],new("fact_1",vi?"Số {v2} mà {v0} có bằng {f1} số {v2} mà {v1} có.":"The number of {v2} that {v0} has equals {f1} times the number of {v2} that {v1} owns.")],
                Question=vi?"{v0} có bao nhiêu {v2} và {v1} có bao nhiêu?":"How many {v2} does {v0} have, and how many does {v1} have?",
                SolutionLeads=vi?d.SolutionLeads:[d.SolutionLeads![0],new("step_1","The number of {v2} that {v0} has."),new("step_2","The number of {v2} that {v1} owns.")]
            };
            Require(Validate(c,corrected).IsValid,"Grammatical owner links and separate targets must be proved: "+profile+" / "+language);clear++;
            DifferenceRatioOpenTests.CheckPractice(c,corrected);
            var possessive=vi?corrected:corrected with {SolutionLeads=[corrected.SolutionLeads![0],new("step_1","{v0}'s count of {v2}."),new("step_2","{v1}'s number of {v2}.")]};
            if(!vi){Require(Validate(c,possessive).IsValid,"Grammatical possessive owner headings were missed.");clear++;}
            Require(Validate(c,corrected with {Facts=[corrected.Facts[0],new("fact_1",Swap(corrected.Facts[1].Text))]}).ErrorCode=="ChangedRelationOrTarget","New relative owner form certified reversed ratio.");mutations++;
            foreach(string prefix in vi?new[]{"Không phải ","Nếu ","Chỉ khi "}:new[]{"It is not true that ","If ","Only if "})
            {
                Require(Validate(c,corrected with {Facts=[corrected.Facts[0],new("fact_1",prefix+corrected.Facts[1].Text)]}).NeedsReview,"Qualified relative ratio was proved.");mutations++;
            }
            string inner=corrected.Facts[1].Text.Replace(vi?" có ":" has ",vi?" không có ":" does not have ");
            Require(!Validate(c,corrected with {Facts=[corrected.Facts[0],new("fact_1",inner)]}).IsValid,"Internal relative-owner negation was ignored.");mutations++;
            var swappedHeadings=possessive with {SolutionLeads=possessive.SolutionLeads!.Select((l,i)=>i==0?l:l with {Text=Swap(l.Text)}).ToArray()};
            Require(!Validate(c,swappedHeadings).IsValid,"Possessive headings changed answer ownership.");mutations++;
            foreach(string prefix in vi?new[]{"Không phải ","Hãy tính "}:new[]{"Not ","Calculate "})
            {
                var heads=possessive.SolutionLeads!.ToArray();heads[1]=heads[1] with {Text=prefix+heads[1].Text};
                Require(!Validate(c,possessive with {SolutionLeads=heads}).IsValid,"Negated or imperative heading was proved.");mutations++;
            }
            foreach(string question in vi?new[]{"Hãy tìm số {v2} của {v0} và {v1} nhé?","Hỏi số {v2} của cả {v0} và {v1} là bao nhiêu?"}
                :new[]{"What is the count of {v2} for {v0} and what is the count of {v1}?","What is the number of {v2} that {v0} has and what is the number of {v1} that has?","What is the count of {v2} for both {v0} and {v1}?"})
            {Require(Validate(c,corrected with {Question=question}).NeedsReview,"Historical malformed/collective question became verified.");mutations++;}
            if(vi)
            {
                foreach(string difference in new[]{"{v0} có ít hơn {v1} một lượng {f0} {v2}.","Số {v2} của {v1} nhiều hơn số {v0} là {f0}."})
                {Require(Validate(c,corrected with {Facts=[new("fact_0",difference),corrected.Facts[1]]}).NeedsReview,"Malformed ownership or stilted quantity was silently repaired.");mutations++;}
            }
            string extra=corrected.Facts[1].Text+(vi?" Sau đó tặng thêm.":" Then another gift occurs.");
            Require(!Validate(c,corrected with {Facts=[corrected.Facts[0],new("fact_1",extra)]}).IsValid,"New proof accepted an extra event.");mutations++;
        }
        Console.WriteLine($"PASS difference-ratio editorial: {clear} clear forms with fresh C# grading, {mutations} unsafe/awkward mutations not verified; prompt ownership and placeholder-only context.");
    }
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static string Swap(string text)=>text.Replace("{v0}","{swap}").Replace("{v1}","{v0}").Replace("{swap}","{v1}");
    private static void Require(bool value,string message)=>TestAssert.That(value,message);
}
