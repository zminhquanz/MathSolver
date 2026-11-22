using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>
/// The drink scenes share the reviewed numeric facts from QuizContent, but use
/// beverage shops as actors in every generated C#, AI, Excel and PDF story.
/// </summary>
internal static class DrinkShopStoryTemplates
{
    internal static QuizStoryTemplate Create(QuizStoryTemplate source, BasicQuestionContract contract)
    {
        bool vi = contract.Language == AppLanguage.Vietnamese;
        bool fraction = contract.Version == FractionQuestionCatalogue.Version;
        string shops = FoodThemePrompt.DrinkShopActors(
            FoodThemeCatalogue.IsCoffeeDrinkContract(contract),
            FoodThemeCatalogue.IsMilkTeaDrinkContract(contract), contract.Language).Both;
        var prose = source.Prose;

        if (fraction)
        {
            prose = contract.Operation switch
            {
                ArithmeticOperation.Add => prose with
                {
                    GivenA = [vi ? "{name} có {a} {unit_a}," : "{name} has {a} {unit_a},"],
                    GivenB = [vi ? "{other} có {b} {unit_b}." : "{other} has {b} {unit_b}."],
                    Questions = [vi ? $"Hỏi {shops} có tổng cộng bao nhiêu {{unit}}?"
                        : $"How much {{unit}} do {shops} have altogether?"]
                },
                ArithmeticOperation.Subtract => prose with
                {
                    GivenA = [vi ? "{name} có {a} {unit_a}," : "{name} has {a} {unit_a},"],
                    GivenB = [vi ? "{other} có {b} {unit_b}." : "{other} has {b} {unit_b}."],
                    Questions = [vi ? $"Hỏi lượng thức uống của {shops} chênh lệch bao nhiêu {{unit}}?"
                        : $"By how much {{unit}} do the amounts at {shops} differ?"]
                },
                ArithmeticOperation.Multiply => prose with
                {
                    GivenA = [vi ? "{name} có lượng ban đầu là {a} {unit_a},"
                        : "{name} starts with {a} {unit_a},"]
                },
                _ => prose with
                {
                    GivenA = [vi ? "{name} có phần đã biết là {a} {unit_a},"
                        : "{name} has a known part of {a} {unit_a},"]
                }
            };
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
                    Questions = [vi ? $"Hỏi {shops} bán được tổng cộng bao nhiêu {{unit}}?"
                        : $"How many {{unit}} did {shops} sell altogether?"]
                },
                ArithmeticOperation.Multiply => prose with
                {
                    GivenA = [vi ? "Tại {name}, mỗi nhóm có {a} {unit_a},"
                        : "At {name}, each group has {a} {unit_a},"]
                },
                ArithmeticOperation.Divide => prose with
                {
                    GivenA = [vi ? "Tại {name}, có tổng cộng {a} {unit_a},"
                        : "At {name}, there are {a} {unit_a} in total,"]
                },
                _ => prose
            };
        }

        // Keep the exact reviewed leads and units; construct the AI example from
        // the same clauses that semantic validation uses.
        return new(source.Id, source.Family, prose.Stories().Take(2).ToArray(), prose);
    }
}
