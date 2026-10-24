using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static class UnitaryTotalContinuationTests
{
    internal static void Run()
    {
        int clear=0,mutants=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(string profile in UnitaryTotalPilot.Profiles(language))
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            bool vi=language==AppLanguage.Vietnamese;
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Proportion,(int)ProportionQuizType.Direct,tier,language,new(713),profile) with {OpenProse=true};
            var reference=ReasoningStoryCatalogue.Draft(c);
            string[][] rows=vi?
            [
                [reference.Facts![0].Text,"Số {v1} trong mỗi {v2} là như nhau và không thay đổi khi chuyển sang các nhóm mới.","Tổng số {v1} có trong {f2} {v0} sẽ là bao nhiêu?","Số {v1} có trong một {v2}","Tổng số {v1} trong các {v0} mới"],
                ["Tổng số {v1} được phân bổ trong {f0} {v0} là {f1}.","Số lượng {v1} trong mỗi {v2} là như nhau, và các nhóm mới vẫn giữ nguyên số lượng này.","Tổng số {v1} của {f2} {v0} sẽ là bao nhiêu?","Số {v1} có trong mỗi {v2}","Tổng số {v1} của các {v0} mới"],
                ["Tổng cộng có {f1} {v1} trong {f0} {v0}.","Số {v1} trong mỗi {v2} bằng nhau và không đổi trong các nhóm mới.","Hỏi {f2} {v0} như vậy chứa tổng cộng bao nhiêu {v1}?","Số {v1} trong mỗi {v2}","Tổng số {v1} trong tất cả các {v0} mới"]
            ]:
            [
                ["{f0} {v0} hold a combined total of {f1} {v1}.","Each {v2} has the same number of {v1}, and this count remains unchanged in the new groups.","What is the total number of {v1} in {f2} {v0} like these?","The number of {v1} within one {v2}","The total count of {v1} across all the new {v0}"],
                ["A collection of {f0} {v0} holds a combined total of {f1} {v1}.","The number of {v1} in each {v2} is equal across the original groups and stays unchanged in the new groups.","How many {v1} are there in total in {f2} {v0} like these?","Count of {v1} within each {v2}","Total number of {v1} across the new {v0}"],
                ["The combined total of all the {v1} in {f0} {v0} amounts to {f1}.","Each {v2} contains an identical number of {v1}, and that count stays the same in the new groups.","How many {v1} will {f2} {v0} like these contain in total?","The number of {v1} in one {v2}","The total number of {v1} in all the new {v0}"]
            ];
            foreach(var r in rows)
            {
                var d=reference with {Facts=[new("fact_0",r[0]),new("fact_1",r[1])],Question=r[2],SolutionLeads=[new("step_0",r[3]),new("step_1",r[4])]};
                Require(Validate(c,d).IsValid,"Clear continuation prose was missed: "+language+" / "+r[1]);clear++;
                UnitaryTotalOpenTests.CheckPractice(c.FreshFacts(new(1028)),d);
                foreach(string prefix in vi?new[]{"Không phải ","Nếu ","Chỉ khi "}:new[]{"It is not true that ","If ","Only if "})
                foreach(int field in new[]{0,1})
                {
                    var facts=d.Facts!.ToArray();facts[field]=facts[field] with {Text=prefix+facts[field].Text};
                    Require(!Validate(c,d with {Facts=facts}).IsValid,"Qualified continuation premise proved.");mutants++;
                }
                foreach(string condition in vi?new[]{"Số {v1} trong mỗi {v2} không như nhau và không đổi ở các nhóm mới.",
                    "Số {v1} trong mỗi {v2} là như nhau và thay đổi khi chuyển sang các nhóm mới."}
                    :new[]{"Each {v2} does not have the same number of {v1}, and this count remains unchanged in the new groups.",
                    "Each {v2} contains an identical number of {v1}, and that count changes in the new groups."})
                {Require(!Validate(c,d with {Facts=[d.Facts![0],new("fact_1",condition)]}).IsValid,"Internal negation/change proved.");mutants++;}
                Require(!Validate(c,d with {Question=d.Question.Replace("{f2}","{f0}")}).IsValid,"Continuation target became source groups.");mutants++;
                Require(!Validate(c,d with {SolutionLeads=[d.SolutionLeads![1] with {Role="step_0"},d.SolutionLeads[0] with {Role="step_1"}]}).IsValid,"Continuation heading roles reversed.");mutants++;
                if(!vi)
                {
                    string extra=d.Facts![0].Text.Replace("{f0} {v0}","{f0} groups of {v0}",StringComparison.Ordinal);
                    Require(Validate(c,d with {Facts=[new("fact_0",extra),d.Facts[1]]}).ErrorCode=="ChangedRelationOrTarget","Added container-group level must be a definite role error.");mutants++;
                    Require(Validate(c,d with {Facts=[new("fact_0","If "+extra),d.Facts[1]]}).NeedsReview,"Do not strip qualifiers while rejecting group-level errors.");mutants++;
                }
            }
            if(!vi)
            {
                var bad=reference with {Facts=[new("fact_0","{f0} groups of {v0} hold {f1} {v1} in total."),reference.Facts![1]]};
                Require(Validate(c,bad).ErrorCode=="ChangedRelationOrTarget","Native extra-group source must retry rather than certify/review as a clear form.");mutants++;
            }
            string grammar=OpenQuestionProse.Grammar(c);
            foreach(string slot in new[]{"f0","f2"})
                Require(grammar.Contains("\"{"+slot+"}\" \" \" \"{v0}\"",StringComparison.Ordinal),"Keep the group count directly bound to its immutable group noun.");
            Require(grammar.Contains("character ::=")&&!grammar.Contains(System.Text.Json.JsonSerializer.Serialize(rows[0][2])),"Group binding must not close the whole prose sentence.");
        }
        Console.WriteLine($"PASS unitary-total continuation: {clear} clear forms, {mutants} role/negation/target contrasts; direct group-noun grammar bindings.");
    }
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static void Require(bool value,string message)=>TestAssert.That(value,message);
}
