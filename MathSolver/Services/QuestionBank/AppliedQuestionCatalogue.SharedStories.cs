using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

public static partial class AppliedQuestionCatalogue
{
    private static void AddSharedStoryScenes(List<AppliedQuestionScene> scenes)
    {
        // Classroom head counts and ages have their own relation models. Adding
        // daily attendance must not silently count the same pupils as different people.
        foreach (var c in QuizStoryContextCatalog.AverageContexts)
        {
            void Add(string suffix, ArithmeticOperation operation, string va, string vb, string vq, string vl,
                string ea, string eb, string eq, string el) => scenes.Add(new(
                    "story-" + c.Id + "-" + suffix, c.Group, operation, 1, c.MaximumPerPeriod, 30, c.UnitId,
                    va, vb, vq, vl, ea, eb, eq, el, StoryContextId: c.Id));
            Add("total", ArithmeticOperation.Add,
                $"Ở {c.ViPeriod} thứ nhất, {c.ViAction} {{a}} {{unit}},",
                $"ở {c.ViPeriod} thứ hai riêng biệt, {c.ViAction} {{b}} {{unit}}.",
                $"Hỏi hai {c.ViPeriod} có tổng cộng bao nhiêu {{unit}}?", "Tổng lượng {unit} của hai phần là:",
                $"In the first {c.EnPeriod}, {c.EnAction} {{a}} {{unit}},",
                $"in a separate second {c.EnPeriod}, {c.EnAction} {{b}} {{unit}}.",
                $"How many {{unit}} are recorded across the two {QuizStoryContextCatalog.PluralPeriod(c.EnPeriod)}?", "The combined quantity of {unit} is:");
            Add("left", ArithmeticOperation.Subtract,
                $"Trong hoạt động {c.ViSetting}, mục tiêu là {{a}} {{unit}},", "đã hoàn thành {b} {unit} trong mục tiêu đó.",
                "Hỏi còn bao nhiêu {unit} để đạt mục tiêu?", "Lượng {unit} còn phải hoàn thành là:",
                $"For {c.EnSetting}, the target is {{a}} {{unit}},", "{b} {unit} of that target have been completed.",
                "How many {unit} are still needed to reach the target?", "The remaining quantity of {unit} is:");
            Add("groups", ArithmeticOperation.Multiply,
                $"Mỗi {c.ViPeriod}, {c.ViAction} {{a}} {{unit_a}},", "có {b} {unit_b} riêng biệt với lượng bằng nhau như vậy.",
                "Hỏi tổng cộng được bao nhiêu {unit}?", "Tổng lượng {unit} là:",
                $"In each {c.EnPeriod}, {c.EnAction} {{a}} {{unit_a}},", "there are {b} separate {unit_b} with the same quantity each.",
                "How many {unit} are there in total?", "The total quantity of {unit} is:");
            Add("share", ArithmeticOperation.Divide,
                $"Trong hoạt động {c.ViSetting}, tổng lượng là {{a}} {{unit_a}},", "lượng đó phân bố đều trong {b} {unit_b} riêng biệt.",
                $"Hỏi mỗi {c.ViPeriod} có bao nhiêu {{unit}}?", $"Lượng {{unit}} mỗi {c.ViPeriod} là:",
                $"For {c.EnSetting}, the total is {{a}} {{unit_a}},", "that total is distributed equally over {b} separate {unit_b}.",
                $"How many {{unit}} are there per {c.EnPeriod}?", $"The quantity of {{unit}} per {c.EnPeriod} is:");
        }
    }
}
