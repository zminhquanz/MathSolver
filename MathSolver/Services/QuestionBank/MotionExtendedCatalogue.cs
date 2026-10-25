using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class MotionExtendedCatalogue
{
    internal static bool IsProfile(BasicQuestionContract contract) => contract.Family == BankQuestionFamily.Motion
        && MotionExtendedPilot.IsProfile(contract.Story?.NarrativeId)
        && MotionExtendedPilot.Relations(contract.Language).Any(r => r.Type == (MotionQuizType)contract.BankVariant
            && contract.Story!.NarrativeId.Split('/').ElementAtOrDefault(1) == r.Id);

    internal static NarrativeLesson Build(BasicQuestionContract contract)
    {
        var data = MotionExtendedPilot.Create(contract.Story!.NarrativeId, contract.Language, contract.Tier, new(contract.Story.Seed));
        var question = new MotionQuizGenerator(new(contract.Story.Seed)).GenerateExtendedNarrative(ArithmeticQuizMode.Essay,
            contract.Language, data.Relation.Type, new(contract.Tier, false), data.Profile);
        var roles = data.IsRoad
            ? new Dictionary<string, string>
            {
                ["f0"] = "initial-separation-distance", ["f1"] = data.Relation.Id == "chasing" ? "slower-leading-actor-speed" : "first-actor-speed",
                ["f2"] = data.Relation.Id == "chasing" ? "faster-following-actor-speed" : "second-actor-speed",
                ["v0"] = data.Relation.Id == "chasing" ? "slower-leading-actor" : "first-actor",
                ["v1"] = data.Relation.Id == "chasing" ? "faster-following-actor" : "second-actor",
                ["v2"] = "speed-unit", ["v3"] = "elapsed-time-unit", ["v4"] = "distance-unit"
            }
            : new Dictionary<string, string>
            {
                ["f0"] = data.Relation.Id is "boat" or "current" ? "downstream-speed-relative-to-bank" : "craft-speed-in-still-water",
                ["f1"] = data.Relation.Id is "boat" or "current" ? "upstream-speed-relative-to-bank" : "current-speed-relative-to-bank",
                ["v0"] = "watercraft", ["v2"] = "speed-unit"
            };
        var quantities = data.Values.Select(v => new NarrativeQuantity(v.Key, roles[v.Key], v.Value)).ToArray();
        var facts = data.Relation.Facts.Select((fact, index) => new NarrativeClause("fact_" + index, fact)).ToArray();
        NarrativeStep[] steps = [new("step_0", data.Relation.Lead, data.Equation + " " + data.AnswerUnit)];
        // Numeric values and seed do not change the prose schema.
        string schema = string.Join("\n", data.Profile, contract.Language, contract.Tier, data.Context.First, data.Context.Second,
            string.Join("/", quantities.Select(q => q.Id + "=" + q.Role)), string.Join("\n", data.Relation.Facts),
            string.Join("\n", data.Relation.AlternateFacts), data.Relation.Question, data.Relation.Lead);
        return new(question, data.Profile, data.Render(data.Relation.Lead), data.AnswerUnit, data.Answer + " " + data.AnswerUnit,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(schema))), quantities, facts, data.Relation.Question, steps,
            facts.Select((fact, index) => (fact, index)).ToDictionary(p => p.fact.Role,
                p => new[] { p.fact.Text, data.Relation.AlternateFacts[p.index] }),
            [data.Relation.Question], steps.ToDictionary(step => step.Id, step => new[] { step.Lead }));
    }

    internal static string Instructions(BasicQuestionContract contract)
    {
        string id = contract.SceneId.Split('/')[1];
        bool vi = contract.Language == AppLanguage.Vietnamese;
        string condition = id switch
        {
            "meeting" => vi
                ? "Hai bên xuất phát cùng lúc từ hai vị trí cách nhau, đi ngược chiều về phía nhau trên cùng một đoạn đường. Giữ đúng chủ sở hữu từng vận tốc; chỉ hỏi thời gian đến khi gặp nhau."
                : "Both start simultaneously from separate positions and move towards each other along one road segment. Preserve each one's own speed; ask only for the duration until they meet.",
            "chasing" => vi
                ? "Chủ thể {v0} chậm hơn và ở phía trước {v1} đúng khoảng cách đã cho. Hai bên bắt đầu cùng lúc, đi cùng chiều trên cùng một đoạn đường; chỉ hỏi thời gian để bên nhanh đuổi kịp bên chậm."
                : "{v0} is slower and initially ahead of {v1} by the supplied gap. Both start simultaneously in the same direction along one road segment; ask only for the duration until the one behind catches up with the one ahead.",
            "downstream" => vi
                ? "{f0} là vận tốc của {v0} khi nước yên lặng; {f1} là vận tốc dòng nước so với bờ. Chuyển động xuôi dòng; chỉ hỏi vận tốc xuôi dòng so với bờ."
                : "{f0} is {v0}'s speed in still water; {f1} is the current speed relative to the bank. Travel is downstream; ask only for the downstream speed relative to the riverbank.",
            "upstream" => vi
                ? "{f0} là vận tốc của {v0} khi nước yên lặng; {f1} là vận tốc dòng nước so với bờ. Chuyển động ngược dòng; chỉ hỏi vận tốc ngược dòng so với bờ."
                : "{f0} is {v0}'s speed in still water; {f1} is the current speed relative to the bank. Travel is upstream; ask only for the upstream speed relative to the riverbank.",
            "boat" => vi
                ? "{f0} là vận tốc xuôi dòng, {f1} là vận tốc ngược dòng của cùng {v0}, cả hai đo so với bờ. Chỉ hỏi vận tốc của phương tiện khi nước yên lặng."
                : "{f0} is the downstream speed and {f1} is the upstream speed of the same {v0}, both relative to the riverbank. Ask only for the craft's speed in still water.",
            _ => vi
                ? "{f0} là vận tốc xuôi dòng, {f1} là vận tốc ngược dòng của cùng {v0}, cả hai đo so với bờ. Chỉ hỏi vận tốc dòng nước so với bờ."
                : "{f0} is the downstream speed and {f1} is the upstream speed of the same {v0}, both relative to the riverbank. Ask only for the current speed relative to the riverbank."
        };
        return vi ? $$"""
            Viết bài chuyển động tự nhiên, có thể đổi cách mở đầu và nối câu. {{condition}}
            Giữ nguyên từng biến, đúng vai trò và đúng chủ thể. Các vận tốc không đổi; không dừng nghỉ, xuất phát muộn hay thêm chặng.
            Dữ kiện và câu hỏi là câu đầy đủ. Câu dẫn lời giải là tên đại lượng cần tìm, không phải chỉ dẫn tính toán.
            Không thêm tên, số, đơn vị hay điều kiện ngoài biến. Không tính toán hoặc tiết lộ đáp án. Chỉ trả JSON.
            """ : $$"""
            Write a natural motion problem; vary openings and linking words. {{condition}}
            Keep every placeholder, its role and its owner. Subject variables are complete noun phrases, with the article already included in English; never add a/an/the before them. Speeds are constant; no stops, later starts or extra legs.
            Facts and the question are complete sentences. The solution heading names the requested quantity, with no calculation instructions.
            Add no names, numbers, units or conditions outside the variables. No calculations or answer disclosure. Return only JSON.
            """;
    }

    internal static string Relation(BasicQuestionContract contract) => contract.SceneId.Split('/')[1] switch
    {
        "meeting" => "simultaneous-towards-each-other-meeting: f0=initial separation; f1=v0 speed; f2=v1 speed; TARGET=meeting duration; constant speeds, no stops.",
        "chasing" => "simultaneous-same-direction-catch-up: f0=initial lead of slower v0 ahead of faster v1; f1=v0 speed; f2=v1 speed; TARGET=catch-up duration; constant speeds, no stops.",
        "downstream" => "river-downstream: f0=craft speed in still water; f1=current speed relative to bank; TARGET=downstream speed relative to riverbank; invariant craft and current speeds.",
        "upstream" => "river-upstream: f0=craft speed in still water; f1=current speed relative to bank; TARGET=upstream speed relative to riverbank; invariant craft and current speeds.",
        "boat" => "river-still-water: f0=downstream speed relative to bank; f1=upstream speed relative to bank; TARGET=craft speed in still water; same craft and constant current.",
        _ => "river-current: f0=downstream speed relative to bank; f1=upstream speed relative to bank; TARGET=current speed relative to bank; same craft and invariant speeds."
    };

    internal static string ContextHint(AppLanguage language) => language == AppLanguage.Vietnamese
        ? "{v0}, {v1} là các chủ thể được cung cấp; giữ đúng vận tốc của từng bên. {v2} là đơn vị vận tốc, {v3} là đơn vị thời gian, {v4} là đơn vị khoảng cách khi có mặt. Dùng nguyên biến và không điền giá trị."
        : "{v0} and {v1} name the two supplied subjects; preserve each speed's owner. Where present, {v2} is a speed unit, {v3} a duration unit and {v4} a distance unit. Keep placeholders without filling values; English subject variables already include the article.";
}
