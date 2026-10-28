using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static class RectangleSideConsolidationTests
{
    internal static Task RunAsync()
    {
        CheckPriorNativeDraftsRemainUnproved();
        int clearCount=0,reviewCount=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(string profile in RectangleSidePilot.Profiles(language))
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            bool vi=language==AppLanguage.Vietnamese;
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Spatial,RectangleSidePilot.Variant,tier,language,new(829),profile) with {OpenProse=true};
            var data=RectangleSidePilot.Create(profile,language,tier,new(c.Story!.Seed));
            string known=vi?(data.Context.KnownRole=="length"?"chiều dài":"chiều rộng"):data.Context.KnownRole;
            string target=vi?(data.Context.TargetRole=="length"?"chiều dài":"chiều rộng"):data.Context.TargetRole;
            string prompt=BasicQuestionPrompt.Build(c);
            Check(prompt.Contains(vi?$"fact_2: đã biết {known}":$"fact_2: known {known}",StringComparison.Ordinal),
                "Prompt must explicitly name this contract's known dimension: "+profile);
            Check(prompt.Contains(vi?$"question và step_1: tìm {target}":$"question and step_1: find {target}",StringComparison.Ordinal),
                "Prompt must explicitly name the unknown dimension in both question and noun heading: "+profile);
            Check(prompt.Contains(vi?"không viết hình {v1}":"do not write rectangle {v1}",StringComparison.Ordinal),"Full shape slot needs an explicit no-duplicate example.");
            Check(prompt.Contains(vi?"không thêm người, địa điểm":"do not add actors, locations",StringComparison.Ordinal),"Object-only prompt must prohibit invented actors/locations.");
            Check(prompt.Contains(vi?"không đặt {v0} sau đơn vị":"do not append {v0} after the unit",StringComparison.Ordinal),"Prompt must explain that the object is not a dangling suffix.");
            Check(prompt.Contains(vi?"cụm danh từ":"noun phrases",StringComparison.Ordinal),"Solution headings must name quantities with noun phrases.");
            var reference=ReasoningStoryCatalogue.Draft(c);
            var clear=reference with
            {
                Facts=[reference.Facts![0],new("fact_1",vi?"Toàn bộ chu vi của {v0} đo được {f0} {v2}.":"The full perimeter of {v0} measures {f0} {v2}."),
                    new("fact_2",vi?$"{{v0}} có {known} đo được {{f1}} {{v2}}.":$"The known {known} of {{v0}} is {{f1}} {{v2}}.")],
                Question=vi?$"Vậy {target} của {{v0}} là bao nhiêu {{v2}}?":$"What is the unknown {target} of {{v0}} in {{v2}}?",
                SolutionLeads=[new("step_0",vi?"Nửa chu vi của {v0}":"Half the perimeter of {v0}"),
                    new("step_1",vi?$"{char.ToUpperInvariant(target[0])+target[1..]} cần tìm của {{v0}}":$"The unknown {target} of {{v0}}")]
            };
            var result=Validate(c,clear);
            Check(result.IsValid,"Clear named-dimension clauses outside the reference must prove: "+result);
            Check(QuestionBankStore.SerializeDraft(result.Draft!)==QuestionBankStore.SerializeDraft(clear),"Validation replaced generated rectangle prose.");
            foreach(int seed in new[]{1701,1702})RectangleSideOpenTests.CheckPractice(c.FreshFacts(new(seed)),clear);
            clearCount++;
            foreach(var changed in new[]{
                clear with {Facts=[clear.Facts![0],new("fact_1",vi?"Toàn bộ chu vi của {v0} không đo được {f0} {v2}.":"The full perimeter of {v0} does not measure {f0} {v2}."),clear.Facts[2]]},
                clear with {Facts=[clear.Facts![0],new("fact_1",vi?"Nửa chu vi của {v0} đo được {f0} {v2}.":"Half the perimeter of {v0} measures {f0} {v2}."),clear.Facts[2]]},
                clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?$"{{v0}} có {target} đo được {{f1}} {{v2}}.":$"The known {target} of {{v0}} is {{f1}} {{v2}}.")]},
                clear with {Question=vi?$"Vậy {known} của {{v0}} là bao nhiêu {{v2}}?":$"What is the unknown {known} of {{v0}} in {{v2}}?"},
                clear with {Question=vi?"Diện tích của {v0} là bao nhiêu {v2}?":"What is the area of {v0} in {v2}?"},
                clear with {Facts=[clear.Facts![0],new("fact_1",clear.Facts[1].Text.Replace("{v2}","{v2}²")),clear.Facts[2]]},
                clear with {SolutionLeads=[clear.SolutionLeads![0],new("step_1",vi?$"{char.ToUpperInvariant(known[0])+known[1..]} cần tìm của {{v0}}":$"The unknown {known} of {{v0}}")]} })
            {
                var wrong=Validate(c,changed);
                Check(!wrong.IsValid&&!wrong.NeedsReview,"Definite perimeter/role/unit contradiction was not rejected: "+wrong);
            }
            foreach(var changed in new[]{
                clear with {Facts=[clear.Facts![0],new("fact_1",(vi?"Nếu ":"If ")+clear.Facts[1].Text),clear.Facts[2]]},
                clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?$"{{v0}} có thể có {known} đo được {{f1}} {{v2}}.":$"The known {known} of {{v0}} might be {{f1}} {{v2}}.")]},
                clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?"Một cạnh của {v0} dài {f1} {v2}.":"A side length of {v0} is {f1} {v2}.")]},
                clear with {Question=vi?"Cạnh còn lại của {v0} là bao nhiêu {v2}?":"What is the length of the other side of {v0} in {v2}?"},
                clear with {SolutionLeads=[clear.SolutionLeads![0],new("step_1",vi?"Độ dài cạnh còn lại của {v0}":"The other side length of {v0}")]} })
            {
                var uncertain=Validate(c,changed);
                Check(uncertain.NeedsReview&&!uncertain.IsValid,"Conditional or generic side wording must remain pending: "+uncertain);
                reviewCount++;
            }
            foreach(var editorial in new[]{
                clear with {Facts=[new("fact_0",vi?"{v0} là một hình {v1}.":"{v0} is a rectangle {v1}."),clear.Facts![1],clear.Facts[2]]},
                clear with {Facts=[new("fact_0",vi?"{v0} có dạng {v1} của bạn An.":"{v0} is a {v1} in actor A's yard."),clear.Facts![1],clear.Facts[2]]},
                clear with {Facts=[clear.Facts![0],new("fact_1",vi?"Toàn bộ chu vi đo được {f0} {v2} {v0}.":"The full perimeter measures {f0} {v2} {v0}."),clear.Facts[2]]},
                clear with {SolutionLeads=[clear.SolutionLeads![0],new("step_1",vi?$"Tính {target} của {{v0}}":$"Calculate the {target} of {{v0}}")]} })
                Check(!Validate(c,editorial).IsValid,"Duplicated shapes, invented actors, dangling objects and verb headings cannot auto-save.");
        }
        Console.WriteLine($"PASS rectangle consolidation: {clearCount} explicit role forms and {reviewCount} conditional/generic contrasts; fresh grading retained.");
        return Task.CompletedTask;
    }

    private static void CheckPriorNativeDraftsRemainUnproved()
    {
        foreach(bool vi in new[]{true,false})
        foreach(bool length in new[]{true,false})
        {
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Spatial,RectangleSidePilot.Variant,CurriculumTier.ThreeStars,
                vi?AppLanguage.Vietnamese:AppLanguage.English,new(829),RectangleSidePilot.Prefix+(length?"length/garden":"width/garden")) with {OpenProse=true};
            var d=ReasoningStoryCatalogue.Draft(c);
            d=d with
            {
                Facts=[new("fact_0",vi?"{v0} là một hình {v1}.":length?"The object is a rectangle {v1} {v0}.":"The garden is a rectangle that has been built recently in the yard of actor A and actor B's lot {v1} is a {v0}."),
                    new("fact_1",vi?length?"Tổng chu vi của {v0} là {f0} {v2}.":"Chu vi của {v0} là {f0} {v2}.":length?"The total boundary of the object measures {f0} {v2} {v0}.":"The total boundary measurement around the garden amounts to {f0} {v2} for the {v0}."),
                    new("fact_2",vi?length?"Chiều rộng của {v0} được biết là {f1} {v2}.":"Một cạnh của {v0} có độ dài {f1} {v2}.":length?"One side measurement of the object is {f1} {v2} {v0}.":"One side of the garden measures {f1} {v2} for the {v0}.")],
                Question=vi?length?"Cạnh còn lại của {v0} là bao nhiêu {v2}?":"Cạnh còn lại của {v0} dài bao nhiêu {v2}?":length?"What is the other side measurement of {v0} in {v2}?":"How long is the other side of {v0} in {v2}?",
                SolutionLeads=[new("step_0",vi?"Nửa chu vi của {v0}":length?"Half the total boundary of {v0}":"Half the total boundary measurement of {v0}"),
                    new("step_1",vi?length?"Chiều dài của {v0}":"Cạnh còn lại của {v0}":"The unknown side of {v0}")]
            };
            Check(!Validate(c,d).IsValid,"A previously ambiguous native rectangle draft became auto-approved.");
        }
    }
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static void Check(bool value,string message)=>TestAssert.That(value,message);
}
