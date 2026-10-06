using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private ElementaryQuizContract CreateMapScale(AppLanguage language, CurriculumTier tier)
    {
        var t = new DifficultyBuilder(QuizProblemKind.Measurement, ElementaryQuizType.MapScale, language, tier);
        int level = (int)tier;
        int denominator = level == 1 ? 100 : level == 2 ? _random.Next(5, 21) * 100
            : _random.Next(1, 6) * 100_000;
        if (level == 5) denominator = new[] { 100_000, 200_000, 500_000 }[_random.Next(3)];
        string scale = t.Given("scale-denominator", denominator);
        string first = t.Given(level == 5 ? "actual-km" : "map-cm", _random.Next(2, 7), level == 5 ? "km" : "cm");
        string expression, problem, unit;
        if (level == 5)
        {
            string next = t.Given("actual-m", _random.Next(2, 8) * 100, "m");
            t.Constant(1000, 100_000);
            string km = $"{first}+{next}/1000";
            t.Step(t.L("Đổi độ dài chặng sau sang km", "Convert the second leg to km"), $"{next}/1000", "km");
            t.Step(t.L("Tổng quãng đường thực tế", "Total actual distance"), km, "km");
            expression = $"({km})*100000/{scale}"; unit = "cm";
            problem = t.L($"Bản đồ có tỉ lệ 1 : {scale}. Đoàn tham quan đi hai chặng liên tiếp dài {first} km và {next} m. Hỏi cả tuyến đường dài bao nhiêu cm trên bản đồ?",
                $"A map has scale 1 : {scale}. A tour follows two consecutive legs of {first} km and {next} m. How long is the whole route in cm on the map?");
        }
        else
        {
            string length = first;
            problem = t.L($"Bản đồ có tỉ lệ 1 : {scale}. ", $"A map has scale 1 : {scale}. ");
            if (level == 4)
            {
                string second = t.Given("second-map-cm", _random.Next(1, 5), "cm");
                length = $"({first}+{second})";
                t.Step(t.L("Độ dài cả tuyến trên bản đồ", "Whole route on the map"), length, "cm");
                problem += t.L($"Hai chặng liên tiếp đo trên bản đồ dài {first} cm và {second} cm. ", $"Two consecutive legs measure {first} cm and {second} cm on the map. ");
            }
            else problem += level <= 2
                ? t.L($"Đoạn đường từ cổng vườn đến ghế nghỉ dài {first} cm trên bản đồ. ", $"The path from the garden gate to a bench measures {first} cm on the map. ")
                : t.L($"Đoạn đường từ trường đến công viên dài {first} cm trên bản đồ. ", $"The route from the school to the park measures {first} cm on the map. ");
            unit = level <= 2 ? "m" : "km";
            int factor = level <= 2 ? 100 : 100_000;
            t.Constant(factor);
            t.Step(t.L("Độ dài thực tế theo cm", "Actual length in cm"), $"{length}*{scale}", "cm");
            expression = $"{length}*{scale}/{factor}";
            problem += t.L($"Hỏi quãng đường thực tế dài bao nhiêu {unit}?", $"What is the actual distance in {unit}?");
        }
        t.Answer(t.L("Độ dài cần tìm", "Required length"), expression, unit);
        return t.Build("map-scale-" + level, problem) with { StoryContextId = "tourism" };
    }

    private ElementaryQuizContract CreatePictograph(AppLanguage language, CurriculumTier tier)
    {
        var t = new DifficultyBuilder(QuizProblemKind.Data, ElementaryQuizType.ReadPictograph, language, tier);
        int level = (int)tier;
        string[] contexts = ["library", "craft", "community"];
        var context = QuizStoryContextCatalog.Find(contexts[NextContextVariant(t.Type, language, "pictograph", contexts.Length)]);
        string unit = context.Unit(language);
        int key = new[] { 1, 2, 5, 10, 20 }[level - 1];
        int[] counts = [_random.Next(3, 8), _random.Next(3, 8), _random.Next(2, 5)];
        string[] labels = language == AppLanguage.Vietnamese ? ["Đợt đầu", "Đợt giữa", "Đợt cuối"] : ["First batch", "Middle batch", "Last batch"];
        string[] symbols = counts.Select((count, i) => t.Given("icons-" + i, count)).ToArray();
        string multiplier = t.Given("key", key, unit);
        string expression, question;
        if (level <= 2)
        {
            expression = $"{symbols[0]}*{multiplier}";
            question = t.L($"Đợt đầu có bao nhiêu {unit}?", $"How many {unit} are in the first batch?");
        }
        else
        {
            string total = level == 3 ? $"{symbols[0]}+{symbols[1]}" : $"{symbols[0]}+{symbols[1]}+{symbols[2]}";
            if (level == 5) total = $"({symbols[0]}+{symbols[1]})-{symbols[2]}";
            t.Step(t.L(level == 5 ? "Chênh lệch số hình" : "Tổng số hình cần đọc", level == 5 ? "Difference in symbols" : "Total relevant symbols"), total);
            expression = $"({total})*{multiplier}";
            question = level == 3 ? t.L($"Hai đợt đầu có tổng cộng bao nhiêu {unit}?", $"How many {unit} are in the first two batches combined?")
                : level == 4 ? t.L($"Cả ba đợt có tổng cộng bao nhiêu {unit}?", $"How many {unit} are in all three batches combined?")
                : t.L($"Tổng hai đợt đầu nhiều hơn đợt cuối bao nhiêu {unit}?", $"How many more {unit} are in the first two batches combined than in the last batch?");
        }
        t.Answer(t.L("Số lượng cần tìm", "Requested quantity"), expression, unit);
        string problem = t.L($"Biểu đồ tranh ghi kết quả ba đợt riêng biệt tại {context.Setting(language)}. Mỗi hình tròn biểu thị {multiplier} {unit}. {question}",
            $"The pictograph records three separate batches at {context.Setting(language)}. Each circle represents {multiplier} {unit}. {question}");
        return t.Build("pictograph-" + level, problem, new("pictograph", labels,
            counts.Select(count => (decimal)(count * key)).ToArray(), unit, PictographKey: key)) with { StoryContextId = context.Id };
    }

    private ElementaryQuizContract CreateShapeRecognition(AppLanguage language, CurriculumTier tier)
    {
        var t = new DifficultyBuilder(QuizProblemKind.VisualGeometry, ElementaryQuizType.RecognizeShape, language, tier);
        int level = (int)tier;
        string[] ids = ["circle", "triangle", "square", "rectangle", "trapezoid", "parallelogram", "rhombus", "cube", "cuboid", "cylinder", "sphere"];
        string[] names = language == AppLanguage.Vietnamese
            ? ["Hình tròn", "Hình tam giác", "Hình vuông", "Hình chữ nhật", "Hình thang", "Hình bình hành", "Hình thoi", "Hình lập phương", "Hình hộp chữ nhật", "Hình trụ", "Hình cầu"]
            : ["Circle", "Triangle", "Square", "Rectangle", "Trapezoid", "Parallelogram", "Rhombus", "Cube", "Rectangular prism", "Cylinder", "Sphere"];
        int[] pool = level <= 2 ? [0, 1, 2, 3] : level == 3 ? [1, 2, 3, 4, 5, 6] : level == 4 ? [7, 8, 9, 10] : Enumerable.Range(0, ids.Length).ToArray();
        int index = pool[_random.Next(pool.Length)];
        t.TextAnswer(t.L("Tên hình", "Shape name"), names[index], names[index].Replace("Hình ", ""), index == 8 ? t.L("hộp chữ nhật", "cuboid") : names[index]);
        // Orientation varies; the answer is never placed in the figure's labels.
        decimal rotation = level == 1 || index >= 7 ? 0 : _random.Next(1, 7) * 15;
        t.Explanation = t.L($"Hình trong tranh là {names[index].ToLowerInvariant()}.", $"The figure is a {names[index].ToLowerInvariant()}.");
        string[] distractors = names.Where((_, i) => i != index).ToArray();
        _random.Shuffle(distractors);
        return t.Build("recognize-" + ids[index], t.L("Quan sát hình và gọi tên hình đó.", "Look at the figure and name the shape."),
            new("shape", [], [], "", RotationDegrees: rotation, ScenarioId: ids[index]))
            with { ChoiceTexts = [names[index], .. distractors.Take(3)] };
    }
}
