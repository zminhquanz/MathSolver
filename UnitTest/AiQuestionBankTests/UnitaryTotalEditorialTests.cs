using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static class UnitaryTotalEditorialTests
{
    internal static void Run()
    {
        // These schemas came from persisted v1 native contracts, before the
        // editorial repair. Their mathematical roles must remain compatible.
        string[] legacySchemas=["A3054B2751B14E977AE6AA5CB03F9A7BE50090DB61FCE74CA920FAD58D868FFB",
            "B3CA6CA0B9A3CD685459A2AE281C451785F3D4833201C10D6D0B2C4A2C65746F",
            "E8794E48EA582A85808EE740751B07C8F53BEEFC88ED4DE78644FE8E2ECF8583",
            "1457A855A7684F4072CDE740204D57F80E11093F9F4340285B0C6797DA3BF6A5",
            "60E571B976C1D98F425D11DCECC725A27B64F8B01970D0578202673B43AC5EFB",
            "D6B7AF484B042E02768A9AEA19F68CA8C5A6B4A937C960FBF4681A2C0B9EA15D"];
        foreach(var (entry,schema) in UnitaryTotalOpenTests.NativeCases().Zip(legacySchemas))
            Require((entry.Contract with {Story=entry.Contract.Story! with {Schema=schema}}).IsValid,
                "Editorial change invalidated an existing unitary contract: "+entry.Name);
        var other=ReasoningStoryCatalogue.Create(BankQuestionFamily.TwoNumbers,(int)ElementaryQuizType.DifferenceRatio,
            CurriculumTier.ThreeStars,AppLanguage.English,new(713),DifferenceRatioPilot.Profiles(AppLanguage.English).First()) with {OpenProse=true};
        Require(OpenQuestionProse.Grammar(other).Split('\n').Single(s=>s.StartsWith("word-tail ::=",StringComparison.Ordinal)).Contains("{1,39}",StringComparison.Ordinal),
            "Unitary tail fix changed another family's prose limits.");
        int clear=0,mutants=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(string profile in UnitaryTotalPilot.Profiles(language))
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            bool vi=language==AppLanguage.Vietnamese;
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Proportion,(int)ProportionQuizType.Direct,
                tier,language,new(713),profile) with {OpenProse=true};
            var reference=ReasoningStoryCatalogue.Draft(c);
            CheckGrammarCanExpressPrompt(c,reference);
            foreach(var d in ClearForms(reference,vi))
            {
                Require(Validate(c,d).IsValid,"Clear equal-and-unchanged unitary prose was missed: "+profile+" / "+language);
                UnitaryTotalOpenTests.CheckPractice(c.FreshFacts(new(928)),d);clear++;
                foreach(string prefix in vi?new[]{"Không phải ","Nếu ","Chỉ khi "}:new[]{"It is not true that ","If ","Only if "})
                foreach(int field in new[]{0,1})
                {
                    var facts=d.Facts!.ToArray();facts[field]=facts[field] with {Text=prefix+facts[field].Text};
                    Require(!Validate(c,d with {Facts=facts}).IsValid,"Qualified premise was automatically proved.");mutants++;
                }
                foreach(string bad in vi?new[]{"Mỗi {v2} không có cùng số {v1} và số đó không đổi ở các nhóm mới.",
                    "Mỗi {v2} có cùng số {v1} nhưng số đó thay đổi ở các nhóm mới.",
                    "Các nhóm mới có số {v1} khác với mỗi {v2} ban đầu."}
                    :new[]{"Each {v2} does not have the same number of {v1}, and the new groups keep that count.",
                    "Each {v2} has the same number of {v1}, but that count changes for the new groups.",
                    "Each new {v2} has a different number of {v1} from an original group."})
                {Require(!Validate(c,d with {Facts=[d.Facts![0],new("fact_1",bad)]}).IsValid,"Changed or negated new-group count proved.");mutants++;}
                foreach(var bad in new[]{d with {Question=d.Question.Replace("{f2}","{f0}",StringComparison.Ordinal)},
                    d with {Question=d.Question.Replace("{v1}","{v0}",StringComparison.Ordinal)},
                    d with {Facts=[d.Facts![0] with {Text=d.Facts[0].Text.Replace("{f0}","{f2}",StringComparison.Ordinal)},d.Facts[1]]},
                    d with {SolutionLeads=d.SolutionLeads!.Reverse().Select((s,i)=>s with {Role="step_"+i}).ToArray()}})
                {Require(!Validate(c,bad).IsValid,"Changed group roles, target unit or heading order proved.");mutants++;}
                string unequalSource=d.Facts![0].Text.Replace(vi?"chia đều":"shared equally",vi?"chia không đều":"shared unequally",StringComparison.Ordinal);
                if(unequalSource!=d.Facts[0].Text)
                {
                    Require(!Validate(c,d with {Facts=[d.Facts[0] with {Text=unequalSource},d.Facts[1]]}).IsValid,
                        "New-group equality was proved without equality of the original groups.");mutants++;
                    var noOriginalEquality=d with {Facts=[reference.Facts![0],d.Facts[1]]};
                    Require(Validate(c,noOriginalEquality).NeedsReview,"Equality with one original group does not establish equal original groups.");mutants++;
                }
            }
            foreach(string weak in vi?new[]{"Mỗi {v2} đều có số lượng {v1} không đổi.",
                "Số {v1} trong mỗi {v2} bằng nhau.","Số {v1} trong mỗi {v2} không thay đổi."}
                :new[]{"The quantity of {v1} within every individual {v2} remains constant.",
                "Each {v2} contains the same number of {v1}.","The number of {v1} in each {v2} does not change."})
            {
                Require(Validate(c,reference with {Facts=[reference.Facts![0],new("fact_1",weak)]}).NeedsReview,
                    "Equality-only or constancy-only condition must remain pending.");mutants++;
            }
            var complete=ClearForms(reference,vi).First();
            foreach(string heading in vi?new[]{"Tổng số {v1} trong {v0} mới", "Tổng số {v1} trong các {v0} có kích thước",
                "Tổng số {v1} trong các {v0} mới JSON"}
                :new[]{"Calculate the total {v1} for {v0} groups of size ","Total count of {v1} across the target {v0} quantity",
                "Total number of {v1} in each new {v0}","Total number of {v1} in the new {v0} JSON"})
            {
                var result=Validate(c,complete with {SolutionLeads=[complete.SolutionLeads![0],new("step_1",heading)]});
                Require(!result.IsValid,"Singular, technical or unfinished heading was automatically proved: "+heading);
                if(heading.Contains(vi?"kích thước":"groups of size",StringComparison.Ordinal))
                    Require(result.NeedsReview,"Unfinished prose must remain pending rather than trigger retries.");
                mutants++;
            }
            string prompt=BasicQuestionPrompt.Build(c);
            Require(prompt.Contains(vi?"hai ý độc lập":"equal across groups",StringComparison.Ordinal),"Prompt still conflates equality with constancy.");
            Require(prompt.Contains(vi?"cụm danh từ hoàn chỉnh":"complete noun phrases",StringComparison.Ordinal),"Prompt does not require complete counted-item headings.");
            Require(prompt.Contains(vi?"các {v0} mới":"new {v0}",StringComparison.Ordinal),"Prompt lost plural target group headings.");
            Require(!prompt.Contains(c.Unit,StringComparison.OrdinalIgnoreCase),"Literal unit leaked into unitary prose prompt.");
            var promptReference=UnitaryTotalOpenCatalogue.PromptExample(c,reference);
            Require(promptReference.Facts![1].Text.Contains(vi?"nhóm mới":"new groups",StringComparison.Ordinal),"Prompt reference must explicitly preserve the count in new groups.");
            Require(Validate(c,promptReference).IsValid,"Clarified prompt reference must be proved.");
            Require(promptReference.Facts[0]==reference.Facts![0]&&promptReference.Question==reference.Question
                &&promptReference.SolutionLeads!.SequenceEqual(reference.SolutionLeads!),"Prompt example changed source, target or headings.");
        }
        Console.WriteLine($"PASS unitary-total editorial: {clear} clear bilingual forms with fresh C# grading, {mutants} unsafe/ambiguous headings and premise mutations; explicit new-group invariance and prompt guidance.");
    }

    private static IEnumerable<BasicQuestionDraft> ClearForms(BasicQuestionDraft d,bool vi)
    {
        string[][] forms=vi?
        [
            ["{f0} {v0} có tổng cộng {f1} {v1}.","Mỗi {v2} có số {v1} bằng nhau và giữ nguyên số đó ở các nhóm mới.",
                "Vậy {f2} {v0} như vậy có tất cả bao nhiêu {v1}?","Số {v1} trong một {v2}","Tổng số {v1} trong tất cả các {v0} mới"],
            ["Có tất cả {f1} {v1} được chia đều vào {f0} {v0}.","Ở mỗi {v2} mới, số {v1} vẫn bằng số trong một nhóm ban đầu.",
                "Hỏi tổng số {v1} trong {f2} {v0} như vậy là bao nhiêu?","Số {v1} của mỗi {v2}","Tổng số {v1} của các {v0} mới"],
            ["Tổng số {v1} trong {f0} {v0} là {f1}.","Số {v1} trong mỗi {v2} đều bằng nhau và không đổi khi xét các nhóm mới.",
                "Hỏi tổng số {v1} trong {f2} {v0} mới như vậy là bao nhiêu?","Số {v1} trong mỗi {v2}","Tổng số {v1} trong các {v0} mới"]
        ]:
        [
            ["{f0} {v0} hold a combined total of {f1} {v1}.","Each {v2} has the same number of {v1}, and the new groups keep that count.",
                "What is the total number of {v1} in {f2} {v0} like these?","Number of {v1} in one {v2}","Total number of {v1} in all the new {v0}"],
            ["A total of {f1} {v1} is shared equally among {f0} {v0}.","Each new {v2} has the same number of {v1} as an original group.",
                "How many {v1} would {f2} {v0} like these hold altogether?","Count of {v1} in each {v2}","Total count of {v1} in the new {v0}"],
            ["The total number of {v1} in {f0} {v0} is {f1}.","Each {v2} holds an equal number of {v1}, and this count remains unchanged for the new groups.",
                "Altogether, how many {v1} are in {f2} {v0} like these?","Number of {v1} per {v2}","Number of {v1} in all the new {v0}"]
        ];
        foreach(var f in forms)yield return d with {Facts=[new("fact_0",f[0]),new("fact_1",f[1])],Question=f[2],
            SolutionLeads=[new("step_0",f[3]),new("step_1",f[4])]};
    }
    private static void CheckGrammarCanExpressPrompt(BasicQuestionContract c,BasicQuestionDraft reference)
    {
        string grammar=OpenQuestionProse.Grammar(c);
        string tailRule=grammar.Split('\n').Single(line=>line.StartsWith("word-tail ::=",StringComparison.Ordinal));
        var bound=System.Text.RegularExpressions.Regex.Match(tailRule,@"character\{1,(\d+)\}");
        Require(bound.Success,"Missing bounded open-prose tail rule.");
        int limit=int.Parse(bound.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture);
        foreach(var field in OpenQuestionProse.Fields(UnitaryTotalOpenCatalogue.PromptExample(c,reference)))
        {
            var slots=System.Text.RegularExpressions.Regex.Matches(field.Text,@"\{[fv]\d+\}");
            var last=slots[^1];
            int needed=field.Text[(last.Index+last.Length)..].TrimStart(' ').TrimEnd('.','!',':','?').Length;
            Require(needed<=limit,$"Unitary grammar cannot express the complete prompt reference {field.Id}: needs {needed} characters after the last slot, permits {limit}.");
        }
        Require(limit<700,"Unitary prose must remain bounded by the existing field limit.");
    }
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static void Require(bool value,string message)=>TestAssert.That(value,message);
}
