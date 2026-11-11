using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>
/// Reviewed selling and packing language for the five traditional takeaway cakes.
/// The same clauses drive algorithmic examples, AI validation and exported stories.
/// </summary>
internal static class BakeryDishStoryTemplates
{
    private sealed record Clauses(string GivenA, string GivenB, string Question, string SolutionLead);

    internal static QuizStoryTemplate Create(string id, string family, string suffix, AppLanguage language, string unitId)
    {
        bool vi = language == AppLanguage.Vietnamese;
        var clauses = (family, suffix, vi) switch
        {
            ("Applied", "total", true) => new Clauses(
                "{name} bán {a} {unit},",
                "{other} bán {b} {unit}.",
                "Hỏi cả hai cơ sở bán được tổng cộng bao nhiêu {unit}?",
                "Tổng số {unit} cả hai cửa hàng bán được là:"),
            ("Applied", "remaining", true) => new Clauses(
                "{name} chuẩn bị {a} {unit} để bán,",
                "cửa hàng đã bán {b} {unit}.",
                "Hỏi {name} còn lại bao nhiêu {unit} chưa bán?",
                "Số {unit} chưa bán còn lại là:"),
            ("Applied", "groups", true) => new Clauses(
                "Tại {name}, mỗi khay có {a} {unit_a},",
                "cửa hàng bán hết {b} {unit_b} như vậy.",
                "Hỏi {name} bán được tổng cộng bao nhiêu {unit}?",
                "Tổng số {unit} bán được là:"),
            ("Applied", "share", true) => new Clauses(
                "{name} chuẩn bị {a} {unit_a} để bán,",
                "cửa hàng xếp đều số bánh đó vào {b} {unit_b}.",
                "Hỏi mỗi khay có bao nhiêu {unit}?",
                "Số {unit} trên mỗi khay là:"),
            ("Fraction", "total", true) => new Clauses(
                "{name} bán {a} {unit_a},",
                "sau đó bán thêm {b} {unit_b}.",
                "Hỏi {name} đã bán tổng cộng bao nhiêu {unit}?",
                "Tổng số bánh đã bán là ({unit}):"),
            ("Fraction", "remaining", true) => new Clauses(
                "{name} bán {a} {unit_a} vào buổi sáng,",
                "buổi chiều bán {b} {unit_b}.",
                "Hỏi buổi sáng bán nhiều hơn buổi chiều bao nhiêu {unit}?",
                "Chênh lệch số bánh bán được là ({unit}):"),
            ("Fraction", "groups", true) => new Clauses(
                "{name} chuẩn bị {a} {unit_a} để bán,",
                "số khách đặt mua bằng {b} lượng bánh đã chuẩn bị.",
                "Hỏi khách đã đặt mua bao nhiêu {unit}?",
                "Số bánh khách đặt mua là ({unit}):"),
            ("Fraction", "share", true) => new Clauses(
                "{name} đã bán {a} {unit_a},",
                "lượng bánh đã bán bằng {b} số bánh chuẩn bị ban đầu.",
                "Hỏi {name} đã chuẩn bị bao nhiêu {unit} để bán?",
                "Số bánh chuẩn bị ban đầu là ({unit}):"),
            ("Applied", "total", false) => new Clauses(
                "{name} sells {a} {unit},",
                "{other} sells {b} {unit}.",
                "How many {unit} do the two businesses sell altogether?",
                "The total number of {unit} sold by the two shops is:"),
            ("Applied", "remaining", false) => new Clauses(
                "{name} prepares {a} {unit} for sale,",
                "the shop sells {b} {unit}.",
                "How many {unit} remain unsold at {name}?",
                "The number of {unit} left unsold is:"),
            ("Applied", "groups", false) => new Clauses(
                "At {name}, each tray holds {a} {unit_a},",
                "the shop sells all {b} {unit_b}.",
                "How many {unit} does {name} sell altogether?",
                "The total number of {unit} sold is:"),
            ("Applied", "share", false) => new Clauses(
                "{name} prepares {a} {unit_a} for sale,",
                "the shop places them equally on {b} {unit_b}.",
                "How many {unit} are on each tray?",
                "The number of {unit} on each tray is:"),
            ("Fraction", "total", false) => new Clauses(
                "{name} sells {a} {unit_a},",
                "then sells another {b} {unit_b}.",
                "How many {unit} does {name} sell altogether?",
                "The total amount sold is ({unit}):"),
            ("Fraction", "remaining", false) => new Clauses(
                "{name} sells {a} {unit_a} in the morning,",
                "then sells {b} {unit_b} in the afternoon.",
                "How many more {unit} are sold in the morning than in the afternoon?",
                "The difference between morning and afternoon sales is ({unit}):"),
            ("Fraction", "groups", false) => new Clauses(
                "{name} prepares {a} {unit_a} for sale,",
                "customers order {b} of the prepared amount.",
                "How many {unit} do customers order?",
                "The amount ordered is ({unit}):"),
            ("Fraction", "share", false) => new Clauses(
                "{name} has sold {a} {unit_a},",
                "this is {b} of the amount initially prepared.",
                "How many {unit} did {name} prepare at first?",
                "The amount initially prepared is ({unit}):"),
            _ => throw new InvalidDataException($"Unknown bakery story: {family}/{suffix}")
        };

        var example = new BasicQuestionDraft(clauses.GivenA, clauses.GivenB,
            clauses.Question, clauses.SolutionLead, unitId);
        var prose = new ReviewedQuestionProse([clauses.GivenA], [clauses.GivenB],
            [clauses.Question], [clauses.SolutionLead], unitId);
        return new QuizStoryTemplate(id, family, [example], prose);
    }
}
