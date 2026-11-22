using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>Bind reviewed arithmetic patterns to realistic toy-shop inventory and sales stories.</summary>
internal static class ToyShopStoryTemplates
{
    internal static QuizStoryTemplate Create(QuizStoryTemplate source, BasicQuestionContract contract)
    {
        bool vi = contract.Language == AppLanguage.Vietnamese;
        var prose = source.Prose;
        prose = contract.Operation switch
        {
            ArithmeticOperation.Add => prose with
            {
                GivenA = [vi ? "{name} bán được {a} {unit}," : "{name} sold {a} {unit},"],
                GivenB = [vi ? "{other} bán được {b} {unit}." : "{other} sold {b} {unit}."],
                Questions = [vi ? "Hỏi cả hai cửa hàng bán được tổng cộng bao nhiêu {unit}?"
                    : "How many {unit} did the two toy shops sell altogether?"]
            },
            ArithmeticOperation.Subtract => prose with
            {
                GivenA = [vi ? "{name} nhập về {a} {unit}," : "{name} stocked {a} {unit},"],
                GivenB = [vi ? "{name} đã bán {b} {unit}." : "{name} sold {b} {unit}."],
                Questions = [vi ? "Hỏi cửa hàng đó còn lại bao nhiêu {unit}?"
                    : "How many {unit} does that toy shop have left?"]
            },
            ArithmeticOperation.Multiply => prose with
            {
                GivenA = [vi ? "Mỗi {group} tại {name} có {a} {unit},"
                    : "Each {group} at {name} contains {a} {unit},"],
                GivenB = [vi ? "cửa hàng có {b} {group} như vậy."
                    : "and the shop has {b} such {group}."],
                Questions = [vi ? "Hỏi cửa hàng có tất cả bao nhiêu {unit}?"
                    : "How many {unit} does the shop have in total?"]
            },
            ArithmeticOperation.Divide => prose with
            {
                GivenA = [vi ? "{name} có {a} {unit}," : "{name} has {a} {unit},"],
                GivenB = [vi ? "cửa hàng chia đều vào {b} {group}."
                    : "and the shop divides them equally among {b} {group}."],
                Questions = [vi ? "Hỏi mỗi {group} có bao nhiêu {unit}?"
                    : "How many {unit} are in each {group}?"]
            },
            _ => prose
        };

        // Generate the reviewed AI exemplars from the same wording used by C#.
        return new(source.Id, source.Family, prose.Stories().Take(2).ToArray(), prose);
    }
}
