using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class InverseWorkOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c) => c.Family == BankQuestionFamily.Proportion
        && c.Story?.NarrativeId.StartsWith(InverseWorkPilot.Prefix, StringComparison.Ordinal) == true;
    internal static ArithmeticQuizQuestion Generate(BasicQuestionContract c, ArithmeticQuizMode mode)
    {
        if (!InverseWorkPilot.Supports(c.BankVariant)) throw new ArgumentException("InvalidInverseWorkRelation");
        return new ProportionQuizGenerator(new(c.Story!.Seed)).GenerateOpenInverseWork(mode, c.Language, c.Tier, c.Story.NarrativeId);
    }
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        if (!InverseWorkPilot.Supports(c.BankVariant)) throw new ArgumentException("InvalidInverseWorkRelation");
        var d = InverseWorkPilot.Create(c.Story!.NarrativeId, c.Language, c.Tier, new(c.Story.Seed));
        var q = Generate(c, ArithmeticQuizMode.Essay);
        string[] roles = ["original-worker-count", "original-completion-time", "new-worker-count",
            "plural-worker-unit", "completion-time-unit", "fixed-task", "singular-worker-unit"];
        var quantities = d.Values.Select((v, i) => new NarrativeQuantity(v.Key, roles[i], v.Value)).ToArray();
        NarrativeClause[] facts = [new("fact_0", d.Context.Given), new("fact_1", d.Context.Condition)];
        NarrativeStep[] steps =
        [
            new("step_0", d.Context.SingleLead, $"{d.OriginalWorkers} × {d.OriginalTime} = {d.SingleWorkerTime} {d.Context.TimeUnit}"),
            new("step_1", d.Context.TeamLead, $"{d.SingleWorkerTime} ÷ {d.NewWorkers} = {d.Answer} {d.TimeUnitFor(d.Answer)}")
        ];
        string signature = string.Join("\n", InverseWorkPilot.Relation, d.Profile, c.Language, c.Tier,
            string.Join("/", roles), d.Context);
        return new(q, d.Profile, d.Context.Workers, d.Context.TimeUnit, InverseWorkPilot.N(d.Answer) + " " + d.TimeUnitFor(d.Answer),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature))), quantities, facts, d.Context.Question, steps,
            facts.ToDictionary(f => f.Role, f => new[] { f.Text }), [d.Context.Question],
            steps.ToDictionary(s => s.Id, s => new[] { s.Lead }));
    }
    internal static string PromptRelation(BasicQuestionContract c)
    {
        var context = InverseWorkPilot.Contexts(c.Language).Single(x => InverseWorkPilot.Prefix + x.Id == c.Story!.NarrativeId);
        string bindings = $"{{v0}}={context.Workers}; {{v1}}={context.TimeUnit}; {{v2}}={context.Task}; {{v3}}={context.Worker}.";
        return c.Language == AppLanguage.Vietnamese
            ? "{f0} là số người của đội ban đầu; {f1} là thời gian chính xác đội đó cần để làm xong công việc; {f2} là tổng số người của đội mới. "
                + "Đội mới làm toàn bộ cùng công việc đó từ đầu, thay cho đội cũ; hai đội không làm chung. "
                + "Khối lượng công việc giữ nguyên. Mọi người có năng suất bằng nhau, và năng suất của mỗi người không thay đổi giữa hai đội. "
                + "Hỏi ngắn gọn đội mới cần bao nhiêu thời gian, không hỏi số người. Thời gian ban đầu là chính xác, không phải hạn chót hay thời gian tối đa. "
                + "Nghĩa các biến để hiểu bối cảnh, vẫn giữ biến trong câu trả lời: " + bindings
                + " {v0} là danh từ chỉ nhiều người; {v3} chỉ một người; {v1} là đơn vị thời gian; {v2} là cụm từ chỉ công việc đã chọn."
            : "{f0} is the original team size; {f1} is the exact time that team needs to finish the job; {f2} is the total size of the replacement team. "
                + "The new team does the entire same job from the start, instead of the old team. The teams do not work together. "
                + "The amount of work stays fixed. All workers have equal productivity, and each worker keeps the same productivity between teams. "
                + "Ask naturally how many time units the new team needs. State the original duration exactly; within, at most and deadlines do not give an exact completion time. "
                + "Context meanings only; keep placeholders in the output: " + bindings
                + " {v0} is the plural worker noun; {v3} is one worker; {v1} is the time unit; {v2} is the selected task phrase.";
    }

    internal static string WritingInstructions(AppLanguage language) => language == AppLanguage.Vietnamese ? """
        Viết bài toán tỉ lệ nghịch ngắn, tự nhiên. Có thể đổi cách mở đầu và cấu trúc câu.
        fact_0 nêu rõ {f0} {v0} hoàn thành {v2} trong {f1} {v1}.
        Đây là thời gian hoàn thành chính xác, không phải giới hạn trên hay hạn chót.
        fact_1 phải khẳng định đủ ba ý: cùng khối lượng công việc, mọi người có năng suất bằng nhau, năng suất mỗi {v3} không thay đổi giữa hai đội.
        Không được chỉ nói năng suất không đổi mà bỏ điều kiện bằng nhau, hoặc bỏ khối lượng công việc cố định.
        Câu hỏi nêu {f2} {v0} là tổng số người của đội mới và hỏi bao nhiêu {v1} để hoàn thành cùng công việc đó.
        Đội mới làm toàn bộ công việc từ đầu. Không thêm người giữa chừng, không đổi công việc, không hỏi số người theo hạn định, không nói về lượng thức ăn.
        step_0 là thời gian một {v3} hoàn thành công việc; step_1 là thời gian {f2} {v0} hoàn thành công việc.
        Giữ nguyên các biến và số lần xuất hiện của từng biến trong từng trường; ứng dụng điền số và đơn vị.
        Câu dẫn là cụm danh từ đầy đủ. Không viết phép tính, đáp án, số mới hoặc đơn vị cụ thể. Chỉ trả JSON.
        """ : """
        Write a short natural inverse work problem. Vary the opening and sentence structure.
        fact_0 states that {f0} {v0} complete {v2} in {f1} {v1}.
        This is the exact completion duration. Use in or takes, not within, at most or a deadline.
        fact_1 must assert three ideas: the same fixed amount of work, equal productivity across all workers, and unchanged productivity for each {v3} between teams.
        Constancy alone does not prove equality, and neither replaces the fixed-work condition.
        Ask how many {v1} the new TOTAL team of {f2} {v0} needs to complete the same work.
        The new team does the whole job from the start. Do not add workers partway through, change the job, ask for workers under a deadline, or discuss food supplies.
        step_0 names the time one {v3} needs to complete the work. step_1 names the time {f2} {v0} need to complete the work.
        Keep every placeholder and its multiplicity in its original field. The app fills numeric values and units.
        Headings are complete noun phrases. No calculations, answers, new quantities or filled concrete units. Return only JSON.
        """;
}
