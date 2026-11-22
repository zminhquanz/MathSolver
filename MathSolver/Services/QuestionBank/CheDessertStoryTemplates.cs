using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>
/// Reviewed dessert-shop stories shared by algorithmic drafts, AI examples,
/// PDF worksheets and Excel round trips for chè, ice cream, and yogurt subjects.
/// </summary>
internal static class CheDessertStoryTemplates
{
    internal static QuizStoryTemplate Create(QuizStoryTemplate reference, BasicQuestionContract contract, string unitId, string dessert = "chè")
    {
        bool vi = contract.Language == AppLanguage.Vietnamese;
        bool fraction = contract.Version == FractionQuestionCatalogue.Version;
        var prose = reference.Prose with { UnitId = unitId };
        if (fraction)
        {
            prose = contract.Operation switch
            {
                ArithmeticOperation.Add => prose with
                {
                    GivenA = [vi ? "{name} có {a} {unit_a}," : "{name} has {a} {unit_a},"],
                    GivenB = [vi ? "{other} có {b} {unit_b}." : "{other} has {b} {unit_b}."],
                    Questions = [vi ? "Hỏi cả hai quán chè có tổng cộng bao nhiêu {unit}?"
                        : "How many {unit} do both dessert shops have altogether?"]
                },
                ArithmeticOperation.Subtract => prose with
                {
                    GivenA = [vi ? "{name} có {a} {unit_a}," : "{name} has {a} {unit_a},"],
                    GivenB = [vi ? "{other} có {b} {unit_b}." : "{other} has {b} {unit_b}."],
                    Questions = [vi ? "Hỏi số chè của cả hai quán chè chênh lệch bao nhiêu {unit}?"
                        : "How many {unit} is the difference between the amounts at both dessert shops?"]
                },
                ArithmeticOperation.Multiply => prose with
                {
                    GivenA = [vi ? "{name} chuẩn bị {a} {unit_a},"
                        : "{name} prepares {a} {unit_a},"],
                    GivenB = [vi ? "phần dành cho khách đặt trước bằng {b} lượng chè đã chuẩn bị."
                        : "the reserved portion is {b} of the amount prepared."],
                    Questions = [vi ? "Hỏi phần dành cho khách đặt trước là bao nhiêu {unit}?"
                        : "How many {unit} are reserved for customers?"]
                },
                _ => prose with
                {
                    GivenA = [vi ? "{name} đã bán {a} {unit_a},"
                        : "{name} sold {a} {unit_a},"],
                    GivenB = [vi ? "lượng này bằng {b} tổng lượng chè quán đã chuẩn bị."
                        : "this is {b} of the total amount of sweet soup prepared."],
                    Questions = [vi ? "Hỏi {name} đã chuẩn bị ban đầu bao nhiêu {unit}?"
                        : "How many {unit} did {name} prepare originally?"]
                }
            };
            // Every fraction scene needs distinct reviewed questions for repeat practice.
            // Preserve the same requested quantity and unit across both phrasings.
            string alternateQuestion = (contract.Operation, vi) switch
            {
                (ArithmeticOperation.Add, true) => "Cả hai quán chè có tất cả bao nhiêu {unit}?",
                (ArithmeticOperation.Subtract, true) => "Lượng chè ở cả hai quán chè chênh lệch nhau bao nhiêu {unit}?",
                (ArithmeticOperation.Multiply, true) => "Có bao nhiêu {unit} được dành cho khách đặt trước?",
                (ArithmeticOperation.Divide, true) => "Ban đầu, {name} đã chuẩn bị tổng cộng bao nhiêu {unit}?",
                (ArithmeticOperation.Add, false) => "What is the combined amount of {unit} at both dessert shops?",
                (ArithmeticOperation.Subtract, false) => "What is the difference in {unit} between both dessert shops?",
                (ArithmeticOperation.Multiply, false) => "What amount of {unit} is set aside for reservations?",
                _ => "What was the original amount of {unit} prepared by {name}?"
            };
            prose = prose with { Questions = [.. prose.Questions, alternateQuestion] };
        }
        else
        {
            prose = contract.Operation switch
            {
                ArithmeticOperation.Add => prose with
                {
                    GivenA = [vi ? "{name} bán được {a} {unit},"
                        : "{name} sold {a} {unit},"],
                    GivenB = [vi ? "{other} bán được {b} {unit}."
                        : "{other} sold {b} {unit}."],
                    Questions = [vi ? "Hỏi cả hai quán chè bán được tổng cộng bao nhiêu {unit}?"
                        : "How many {unit} did both dessert shops sell altogether?"]
                },
                ArithmeticOperation.Subtract => prose with
                {
                    GivenA = [vi ? "{name} chuẩn bị {a} {unit},"
                        : "{name} prepared {a} {unit},"],
                    GivenB = [vi ? "quán chè đã bán {b} {unit}."
                        : "the dessert shop sold {b} {unit}."],
                    Questions = [vi ? "Hỏi {name} còn lại bao nhiêu {unit}?"
                        : "How many {unit} does {name} have left?"]
                },
                ArithmeticOperation.Multiply => prose with
                {
                    GivenA = [vi ? "Tại {name}, mỗi khay có {a} {unit_a},"
                        : "At {name}, each tray has {a} {unit_a},"],
                    GivenB = [vi ? "có tất cả {b} {unit_b} như vậy."
                        : "there are {b} {unit_b} like this."],
                    Questions = [vi ? "Hỏi quán chè có tất cả bao nhiêu {unit}?"
                        : "How many {unit} does the dessert shop have in total?"]
                },
                _ => prose with
                {
                    GivenA = [vi ? "{name} chuẩn bị tổng cộng {a} {unit_a},"
                        : "{name} prepared a total of {a} {unit_a},"],
                    GivenB = [vi ? "quán chè chia đều số chè đó vào {b} {unit_b}."
                        : "the dessert shop divided them equally among {b} {unit_b}."],
                    Questions = [vi ? "Hỏi mỗi khay có bao nhiêu {unit}?"
                        : "How many {unit} are in each tray?"]
                }
            };
        }
        if (dessert != "chè")
        {
            string shopEn = dessert == "kem" ? "ice cream shop" : "yogurt shop";
            string foodEn = dessert == "kem" ? "ice cream" : "yogurt";
            string Reword(string value) => value
                .Replace("quán chè", "quán " + dessert, StringComparison.Ordinal)
                .Replace("số chè", "số " + dessert, StringComparison.Ordinal)
                .Replace("lượng chè", "lượng " + dessert, StringComparison.Ordinal)
                .Replace("dessert shops", shopEn + "s", StringComparison.Ordinal)
                .Replace("dessert shop", shopEn, StringComparison.Ordinal)
                .Replace("sweet soup", foodEn, StringComparison.Ordinal);
            prose = prose with
            {
                GivenA = prose.GivenA.Select(Reword).ToArray(),
                GivenB = prose.GivenB.Select(Reword).ToArray(),
                Questions = prose.Questions.Select(Reword).ToArray()
            };
        }
        return new(contract.SceneId, reference.Family, prose.Stories().Take(2).ToArray(), prose);
    }
}
