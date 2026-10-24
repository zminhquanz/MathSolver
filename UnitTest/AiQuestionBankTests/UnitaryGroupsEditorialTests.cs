using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static class UnitaryGroupsEditorialTests
{
    internal static void Run()
    {
        string[] schemas=["25EDC39507D1CC7608DB1E30A91BDF050082D6A9682C56BADDD6739DED538C86",
            "835E58581FCE9C4743C0B2EA232669FFA1CBFEBEC4B53BD703C85A39A244C778",
            "CEFC198E1C0DFCDE29CBEC4BDF5479B2B5624C450E8AB79A67DADCC3B5F4B280",
            "E6A62051DF47A132639B2BB136AAB10611F66AA7384CFF02BBFFD1D07D651ECD",
            "EBD506348B41AD5ADD59FC3EF151281752DC55B0BE300B505EB6FAC618928ABE",
            "A96A6E065A209FA70B52BA228838076E9B7DF2B4C3BCDB496FC3356EAE03ED7E"];
        foreach(var (entry,schema) in UnitaryGroupsOpenTests.NativeCases().Zip(schemas))
            Require((entry.Contract with {Story=entry.Contract.Story! with {Schema=schema}}).IsValid,"Editorial repair changed stored group-count schema.");
        int clear=0,mutations=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(string profile in UnitaryGroupsPilot.Profiles(language))
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            bool vi=language==AppLanguage.Vietnamese;
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Proportion,(int)ProportionQuizType.UnitaryGroupCount,tier,language,new(713),profile) with {OpenProse=true};
            var reference=ReasoningStoryCatalogue.Draft(c);
            var correct=vi?reference with {Facts=[reference.Facts![0],new("fact_1","Mỗi {v2} có cùng số {v1}, và số đó giữ nguyên khi đóng các nhóm mới.")]}
                :reference with {
                    Facts=[new("fact_0","The combined amount of {v1} across {f0} {v0} is {f1}."),
                        new("fact_1","Every single {v2} holds an identical count of {v1} which remains constant for the new containers.")],
                    Question="How many {v0} are required to hold a total of {f2} {v1} using these same arrangements?",
                    SolutionLeads=[new("step_0","The number of {v1} inside one {v2}."),new("step_1","The total count of {v0} needed.")]};
            // Exact complete English output from the previous native run, not
            // just its condition isolated from the other unrecognized fields.
            Require(Validate(c,correct).IsValid,"Complete clear equal-and-unchanged group-count wording was missed: "+language+"/"+profile);
            UnitaryGroupsOpenTests.CheckPractice(c.FreshFacts(new(928)),correct);clear++;
            foreach(string prefix in vi?new[]{"Không phải ","Nếu ","Chỉ khi "}:new[]{"It is not true that ","If ","Only if "})
            foreach(int field in new[]{0,1})
            {
                var facts=correct.Facts!.ToArray();facts[field]=facts[field] with {Text=prefix+facts[field].Text};
                Require(!Validate(c,correct with {Facts=facts}).IsValid,"Qualified full-field premise was proved.");mutations++;
            }
            string[] incomplete=vi?[
                "Số {v1} trong mỗi {v2} được giữ nguyên cho các nhóm mới.",
                "Mỗi {v2} có số lượng {v1} như các nhóm ban đầu.",
                "Số {v1} trong mỗi {v2} được giữ nguyên khi chia thành các nhóm mới.",
                "Mỗi {v2} có cùng số {v1}.",
                "Mỗi {v2} không có cùng số {v1}, và số đó giữ nguyên khi đóng các nhóm mới.",
                "Mỗi {v2} có cùng số {v1}, và số đó không giữ nguyên khi đóng các nhóm mới.",
                "Mỗi {v2} có cùng số {v1}, và số đó giữ nguyên nếu có các nhóm mới.",
                "Mỗi {v2} có cùng số {v1}, và số đó giữ nguyên khi đóng các nhóm mới, trừ một nhóm."]:[
                "The number of {v1} inside each {v2} stays the same for the new groups.",
                "Each {v2} holds the same number of {v1} as before, and that amount stays the same for the new ones.",
                "Every single {v2} holds an identical count of {v1}.",
                "Every single {v2} does not hold an identical count of {v1} which remains constant for the new containers.",
                "Every single {v2} holds an identical count of {v1} which does not remain constant for the new containers.",
                "Every single {v2} holds an identical count of {v1} which remains constant if new containers are used.",
                "Every single {v2} holds an identical count of {v1} which remains constant for the new containers, except one.",
                "Every single {v2} holds an identical count of {v1} which remains constant before the new containers are packed."];
            foreach(string condition in incomplete)
            {
                Require(Validate(c,correct with {Facts=[correct.Facts![0],new("fact_1",condition)]}).NeedsReview,
                    "Missing, negated or qualified equality/invariance was automatically proved.");mutations++;
            }
            foreach(var bad in new[]{correct with {Question=correct.Question.Replace("{f2}","{f1}",StringComparison.Ordinal)},
                correct with {Question=vi?"{f2} {v0} như vậy có bao nhiêu {v1}?":"How many {v1} do {f2} {v0} like these contain in total?"},
                correct with {Facts=[new("fact_0",vi?"Trong {f0} {v0}, mỗi nhóm có {f1} {v1}.":"Each of the {f0} {v0} contains {f1} {v1}."),correct.Facts![1]]},
                correct with {SolutionLeads=correct.SolutionLeads!.Reverse().Select((s,i)=>s with {Role="step_"+i}).ToArray()},
                correct with {Question=(vi?"Không cần biết ":"It is not necessary to know ")+correct.Question}})
            {Require(!Validate(c,bad).IsValid,"Changed item/group roles, target or headings was proved.");mutations++;}
            string prompt=BasicQuestionPrompt.Build(c);
            Require(prompt.Contains(vi?"hai ý độc lập":"two independent clauses",StringComparison.Ordinal),"Prompt does not distinguish equality from invariance.");
            Require(!prompt.Contains("one complete short sentence per field",StringComparison.Ordinal),"Sentence-only rule contradicts noun headings.");
            string grammar=OpenQuestionProse.Grammar(c);
            Require(grammar.Split('\n').Single(x=>x.StartsWith("word-tail ::=",StringComparison.Ordinal)).Contains("{1,159}",StringComparison.Ordinal),"Existing bounded grammar unexpectedly changed.");
            foreach(var field in OpenQuestionProse.Fields(correct).Concat(OpenQuestionProse.Fields(reference)))
            {
                var slots=System.Text.RegularExpressions.Regex.Matches(field.Text,@"\{[fv]\d+\}");
                string tail=field.Text[(slots[^1].Index+slots[^1].Length)..];
                if(tail.StartsWith(", ",StringComparison.Ordinal))
                    Require(grammar.Split('\n').Single(x=>x.StartsWith("word-tail ::=",StringComparison.Ordinal)).Contains("\", \"",StringComparison.Ordinal),
                        "Group grammar cannot emit the final-slot comma in its prompt/List reference.");
                int trailing=tail.Trim().Length;
                Require(trailing<=159,"Repaired wording is not expressible within existing grammar tail.");
            }
        }
        var unrelated=UnitaryTotalOpenTests.NativeCases().First().Contract with {OpenProse=true};
        Require(!OpenQuestionProse.Grammar(unrelated).Split('\n').Single(x=>x.StartsWith("word-tail ::=",StringComparison.Ordinal)).Contains("\", \"",StringComparison.Ordinal),
            "Group-count punctuation fix altered the other unitary relation.");
        Console.WriteLine($"PASS group-count editorial: {clear} complete bilingual profiles; {mutations} negation/incomplete-condition/role mutations, schema and prompt/grammar checks.");
    }
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static void Require(bool value,string message)=>TestAssert.That(value,message);
}
