using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class RemainderGroupsOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c) => c.Family == BankQuestionFamily.Remainder
        && c.Story?.NarrativeId.StartsWith(RemainderGroupsPilot.Prefix, StringComparison.Ordinal) == true;
    internal static ArithmeticQuizQuestion Generate(BasicQuestionContract c, ArithmeticQuizMode mode) =>
        new ElementaryQuizGenerator(new(c.Story!.Seed)).GenerateRemainderGroupsOpen(mode, c.Language, c.Tier, c.BankVariant, c.Story.NarrativeId);
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        var d = RemainderGroupsPilot.Create(c.Story!.NarrativeId, c.Language, c.Tier, c.BankVariant, new(c.Story.Seed));
        var q = Generate(c, ArithmeticQuizMode.Essay);
        var p = q.ElementaryProblem!;
        string[] roles = ["total-item-count", "items-per-container-capacity", "plural-container-unit", "counted-item-unit", "singular-container-unit"];
        var quantities = d.Values.Select((v, i) => new NarrativeQuantity(v.Key, roles[i], v.Value)).ToArray();
        NarrativeClause[] facts = [new("fact_0", d.Context.Total), new("fact_1", d.Context.Capacity)];
        var steps = p.Reasoning!.Steps.Select((s, i) => new NarrativeStep("step_" + i,
            i == 0 ? d.Context.FullLead : d.Context.MinimumLead,
            i == 0 ? d.DivisionCalculation(c.Language)
                : $"{d.Full} + {d.Extra} = {d.Answer} {d.AnswerUnit}")).ToArray();
        string signature = string.Join("\n", d.Profile, c.BankVariant, c.Language, c.Tier, string.Join("/", roles), d.Context);
        return new(q, d.Profile, d.Context.Containers, d.AnswerUnit, p.AnswerText,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature))), quantities, facts, d.Context.Question, steps,
            new Dictionary<string, string[]> { ["fact_0"] = [d.Context.Total, d.Context.TotalAlternate], ["fact_1"] = [d.Context.Capacity, d.Context.CapacityAlternate] },
            [d.Context.Question, d.Context.QuestionAlternate], steps.ToDictionary(s => s.Id, s => new[] { s.Lead }));
    }
    internal static string WritingInstructions(AppLanguage language) => language == AppLanguage.Vietnamese ? """
        Viết bài toán xếp đồ vật vào đồ đựng. Dùng các ví dụ làm tham khảo, được thay đổi cách diễn đạt.
        {f0} {v1} là tổng số đồ vật cần xếp, không phải sức chứa hay số đồ đựng.
        {f1} {v1} là sức chứa của mỗi {v2}. Giữ đúng mục tiêu duy nhất được nêu trong hợp đồng.
        Các câu dẫn lời giải là cụm danh từ chỉ đại lượng của đúng bước đó.
        Viết mới ít nhất một câu dữ kiện bằng cách đổi cấu trúc câu, giữ nguyên ý nghĩa và các biến.
        Dùng đúng từng biến và số lần xuất hiện trong mỗi trường. Không điền số, đơn vị hoặc đáp án vào biến.
        Không thêm chỗ dự phòng, hao hụt, chuyển đồ vật, thay đổi sức chứa, điều kiện hoặc phủ định. Không tiết lộ thương, số dư hay kết quả.
        Câu dữ kiện và câu hỏi là câu hoàn chỉnh. Chỉ trả JSON.
        """ : """
        Write a short packing problem. Examples guide meaning while sentence wording may vary.
        {f0} {v1} is the total item count to pack, never capacity or a container count.
        {f1} {v1} is the capacity of each {v2}. Preserve the single target specified by the contract.
        Solution headings are noun phrases naming the quantity for that step.
        Write at least one fact with a new sentence structure while preserving its meaning and placeholders.
        Preserve every field's placeholder multiplicities. Do not fill numbers, units or answers into placeholders.
        Add no reserved capacity, losses, transfers, changed capacities, conditions or negation. Do not reveal the quotient, remainder or answer.
        Facts and the question must be complete sentences. Return only JSON.
        """;
    internal static string WritingInstructions(BasicQuestionContract c)
    {
        if (!IsProfile(c) || !RemainderGroupsPilot.Supports(c.BankVariant)) throw new ArgumentException("InvalidRemainderGroupsRelation");
        bool minimum = c.BankVariant == (int)ElementaryQuizType.MinimumGroups;
        string target = c.Language == AppLanguage.Vietnamese
            ? minimum ? """
                MỤC TIÊU DUY NHẤT: tìm số {v0} tối thiểu cần dùng để xếp hết đồ vật.
                Dữ kiện sức chứa nói rõ mỗi {v2} chứa được nhiều nhất {f1} {v1}.
                Câu hỏi phải hỏi rõ ít nhất hoặc tối thiểu bao nhiêu {v0} để chứa hết.
                Có hai câu dẫn: số {v0} đầy, rồi số {v0} tối thiểu cần dùng. Mỗi câu dẫn chỉ là cụm danh từ.
                """ : """
                MỤC TIÊU DUY NHẤT: tìm số {v0} ĐẦY có thể xếp được.
                Dữ kiện sức chứa nói rõ mỗi {v2} ĐẦY chứa đúng {f1} {v1}.
                Câu hỏi chỉ hỏi bao nhiêu {v0} được xếp ĐẦY. Giữ từ đầy trong câu hỏi.
                Chỉ có một câu dẫn: số {v0} đầy. Câu dẫn chỉ là cụm danh từ.
                """
            : minimum ? """
                ONLY TARGET: the minimum number of {v0} needed to hold every item.
                The capacity fact must say each {v2} holds at most {f1} {v1}.
                The question must explicitly ask the minimum or fewest {v0} required to hold all items.
                There are two noun headings: number of full {v0}, then minimum number of {v0} needed.
                """ : """
                ONLY TARGET: the number of COMPLETELY FULL {v0} that can be filled.
                The capacity fact must say a FULL {v2} contains exactly {f1} {v1}.
                Ask only how many {v0} can be COMPLETELY FILLED. Keep full or completely filled in the question.
                There is one noun heading: number of full {v0}.
                """;
        return target + "\n" + WritingInstructions(c.Language);
    }
    internal static string PromptRelation(BasicQuestionContract c)
    {
        var d = RemainderGroupsPilot.Create(c.Story!.NarrativeId, c.Language, c.Tier, c.BankVariant, new(c.Story.Seed));
        return "relation=" + (c.BankVariant == (int)ElementaryQuizType.MinimumGroups ? "minimum-containers-ceiling" : "full-containers-floor")
            + "; {f0}=total item count; {f1}=items per container capacity; {v0}=plural containers; {v1}=counted contents; {v2}=one container. "
            + (c.BankVariant == (int)ElementaryQuizType.MinimumGroups
                ? "ONLY TARGET=minimum container count to hold ALL items. Ask for the minimum needed; do not describe a calculation."
                : "ONLY TARGET=number of COMPLETELY FULL containers. Leftover items do not form another full container. The question and its single heading explicitly say full. Do not describe a calculation.")
            + $" Context bindings for understanding only: {{v0}}={d.Context.Containers}; {{v1}}={d.Context.Items}; {{v2}}={d.Context.Container}. Keep every noun in its placeholder in the output.";
    }
}
