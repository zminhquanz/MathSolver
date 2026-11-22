using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>
/// Reviewed bilingual fraction scenarios for measurable materials. The unit of
/// division is a standard portion, so fractional answers remain meaningful.
/// </summary>
internal static class FractionThemeStoryTemplates
{
    internal static QuizStoryTemplate Create(FractionThemeSubject subject, BasicQuestionContract contract)
    {
        bool vi = contract.Language == AppLanguage.Vietnamese;
        string item = vi ? subject.VietnameseItem : subject.EnglishItem;
        string givenA = vi
            ? $"{{name}} có {{a}} {{unit_a}} {item},"
            : $"{{name}} has {{a}} {{unit_a}} of {item},";
        string givenB;
        string[] questions;
        string lead;

        switch (contract.Operation)
        {
            case ArithmeticOperation.Add:
                givenB = vi
                    ? $"sau đó bổ sung thêm {{b}} {{unit_b}} {item}."
                    : $"then adds another {{b}} {{unit_b}} of {item}.";
                questions = vi
                    ? [$"Hỏi {{name}} hiện có tất cả bao nhiêu {{unit}} {item}?",
                       $"Tổng lượng {item} của {{name}} sau khi bổ sung là bao nhiêu {{unit}}?"]
                    : [$"How many {{unit}} of {item} does {{name}} have altogether?",
                       $"What is the total amount of {item} in {{unit}}?"];
                lead = vi ? $"Tổng lượng {item} ({{unit}}) là:" : $"The total amount of {item} in {{unit}} is:";
                break;
            case ArithmeticOperation.Subtract:
                givenB = vi
                    ? $"sau đó đã sử dụng {{b}} {{unit_b}} {item}."
                    : $"then uses {{b}} {{unit_b}} of {item}.";
                questions = vi
                    ? [$"Hỏi {{name}} còn lại bao nhiêu {{unit}} {item}?",
                       $"Lượng {item} còn lại là bao nhiêu {{unit}}?"]
                    : [$"How many {{unit}} of {item} remain?",
                       $"What amount of {item} in {{unit}} is left?"];
                lead = vi ? $"Lượng {item} còn lại ({{unit}}) là:" : $"The remaining amount of {item} in {{unit}} is:";
                break;
            case ArithmeticOperation.Multiply:
                givenB = vi
                    ? $"và sử dụng một phần bằng {{b}} lượng {item} đó."
                    : $"and uses {{b}} of that amount.";
                questions = vi
                    ? [$"Hỏi {{name}} đã sử dụng bao nhiêu {{unit}} {item}?",
                       $"Phần {item} được sử dụng là bao nhiêu {{unit}}?"]
                    : [$"How many {{unit}} of {item} does {{name}} use?",
                       $"What amount of {item} in {{unit}} is used?"];
                lead = vi ? $"Lượng {item} đã sử dụng ({{unit}}) là:" : $"The amount of {item} used in {{unit}} is:";
                break;
            case ArithmeticOperation.Divide:
                givenB = vi
                    ? $"mỗi suất định mức cần {{b}} {{unit_b}} {item}."
                    : $"each standard portion requires {{b}} {{unit_b}} of {item}.";
                questions = vi
                    ? [$"Lượng {item} này tương đương bao nhiêu {{unit}} (tính cả phần lẻ)?",
                       $"Có thể quy đổi lượng {item} này thành bao nhiêu {{unit}}?"]
                    : [$"How many {{unit}} does this amount of {item} represent, including partial portions?",
                       $"What is this amount of {item} expressed in {{unit}}?"];
                lead = vi ? "Số suất định mức tương đương là:" : "The equivalent number of standard portions is:";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(contract));
        }

        var prose = new ReviewedQuestionProse([givenA], [givenB], questions, [lead], contract.SceneId);
        return new QuizStoryTemplate(contract.SceneId, "Fraction", prose.Stories().Take(2).ToArray(), prose);
    }
}
