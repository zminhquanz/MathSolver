using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    private ElementaryQuizContract CreateProbabilityDifficulty(ElementaryQuizType type, AppLanguage language, CurriculumTier tier)
    {
        var t = new DifficultyBuilder(QuizProblemKind.Probability, type, language, tier);
        int level = (int)tier;
        t.RequiresSolution = false;
        if (type == ElementaryQuizType.Likelihood)
        {
            var original = CreateLikelihoodTask(language, _random.Next(3, 12), _random.Next(3, 12));
            int n = original.Scenario.TotalCount, k = original.Scenario.EventCount;
            t.Given("outcomes", n); t.Given("favorable", k);
            string setup = original.SetupText!, classificationEvent = original.Scenario.EventText;
            int total = n, favorable = k;
            if (level == 2)
            {
                classificationEvent = t.L($"không xảy ra sự kiện “{classificationEvent}”", $"the event “{classificationEvent}” does not occur");
                favorable = n - k;
            }
            else if (level == 3)
            {
                setup += t.L(" Thực hiện độc lập hai lần; nếu lấy đồ vật thì trả lại trước lần sau.",
                    " Repeat independently twice; if selecting an item, replace it before the next trial.");
                classificationEvent = t.L($"cả hai lần đều thỏa mãn “{classificationEvent}”", $"both trials satisfy “{classificationEvent}”");
                total = n * n; favorable = k * k;
            }
            else if (level >= 4)
            {
                // Each equally likely original outcome is represented once on a card.
                // The subsequent sample explicitly draws cards, including for die/coin contexts.
                setup += t.L($" Ghi mỗi kết quả có thể của lần thử trên một thẻ riêng: có {n} thẻ, trong đó {k} thẻ thỏa mãn sự kiện trên.",
                    $" Put each possible single-trial outcome on its own card: there are {n} cards, of which {k} satisfy the event above.");
                if (level == 5)
                {
                    int removed = k > 0 && n > 2 ? 1 : 0;
                    string remove = t.Given("removed-favorable", removed);
                    setup += t.L($" Bỏ ra {remove} thẻ thỏa mãn sự kiện trước khi rút.", $" Remove {remove} event-satisfying cards before drawing.");
                    n -= removed; k -= removed;
                }
                setup += t.L(" Rút lần lượt hai thẻ, không trả lại thẻ đã rút.", " Draw two cards without replacement.");
                classificationEvent = t.L($"cả hai thẻ đều thỏa mãn “{classificationEvent}”", $"both cards satisfy “{classificationEvent}”");
                total = n * (n - 1); favorable = k * (k - 1);
            }
            string answer = favorable == 0 ? t.L("Không Thể", "impossible")
                : favorable == total ? t.L("Chắc Chắn", "certain") : t.L("Có Thể", "possible");
            t.TextAnswer(t.L("Khả năng xảy ra", "Likelihood"), answer,
                answer == t.L("Có Thể", "possible") ? t.L("có thể nhưng không chắc chắn", "possible but not certain") : answer);
            t.Explanation = favorable == 0 ? t.L("Không có kết quả phù hợp với sự kiện đã hỏi.", "No outcome satisfies the requested event.")
                : favorable == total ? t.L("Mọi kết quả đều phù hợp với sự kiện đã hỏi.", "Every outcome satisfies the requested event.")
                : t.L("Có kết quả phù hợp và kết quả không phù hợp với sự kiện đã hỏi.", "Some outcomes satisfy the requested event and others do not.");
            string problem = setup + t.L($" Sự kiện “{classificationEvent}” là chắc chắn, có thể nhưng không chắc chắn, hay không thể?",
                $" Is “{classificationEvent}” certain, possible but not certain, or impossible?");
            return t.Build("likelihood-relations-" + level, problem,
                probability: new(original.Scenario.ContextId, classificationEvent, favorable, total, false));
        }

        var source = CreateExperimentalProbabilityTask(language, _random.Next(10, 30));
        int batchCount = level <= 2 ? 1 : level <= 4 ? 2 : 3;
        int boundary = NextContextVariant(type, language, "observed-boundaries", 4);
        string[] totals = new string[batchCount], successes = new string[batchCount];
        int[] actualTotal = new int[batchCount], actualSuccess = new int[batchCount];
        string text = t.L($"Thí nghiệm: {source.ActionText}. ", $"Experiment: {source.ActionText}. ");
        for (int index = 0; index < batchCount; index++)
        {
            int total = _random.Next(10, 31);
            int success = boundary == 0 ? 0 : boundary == 1 ? total : _random.Next(1, total);
            actualTotal[index] = total; actualSuccess[index] = success;
            totals[index] = t.Given("total-" + index, total);
            if (level >= 4 && index == batchCount - 1)
            {
                string failures = t.Given("failures-" + index, total - success);
                successes[index] = $"({totals[index]}-{failures})";
                text += t.L($"Đợt {index + 1} có {totals[index]} lần thử, đã ghi nhận {failures} lần không thỏa mãn “{source.ResultText}”. ",
                    $"Batch {index + 1} has {totals[index]} trials, with {failures} recorded failures of “{source.ResultText}”. ");
                t.Step(t.L("Số lần thỏa mãn ở đợt cuối", "Successes in the last batch"), successes[index]);
            }
            else
            {
                successes[index] = t.Given("success-" + index, success);
                text += t.L($"Đợt {index + 1} có {totals[index]} lần thử, đã ghi nhận {successes[index]} lần “{source.ResultText}”. ",
                    $"Batch {index + 1} has {totals[index]} trials, with {successes[index]} recorded successes of “{source.ResultText}”. ");
            }
        }
        int invalid = level == 5 ? Math.Min(2, actualTotal[0] - actualSuccess[0]) : 0;
        string denominator = "(" + string.Join("+", totals) + ")";
        if (level == 5)
        {
            string removed = t.Given("invalid-failures", invalid);
            denominator = $"({denominator}-{removed})";
            text += t.L($"Trong các lần không thỏa mãn ở đợt đầu, {removed} lần bị ghi sai và phải loại khỏi số lần thử hợp lệ. ",
                $"Of the failed trials in the first batch, {removed} were incorrectly recorded and must be excluded from the valid trial count. ");
        }
        string numerator = "(" + string.Join("+", successes) + ")";
        bool complement = level == 2 || level >= 3 && _random.Next(2) == 0;
        string eventText = complement ? t.L($"sự kiện “{source.ResultText}” không xảy ra", $"the event “{source.ResultText}” does not occur") : source.ResultText!;
        if (batchCount > 1)
        {
            t.Step(t.L("Tổng số lần thử hợp lệ", "Total valid trials"), denominator);
            t.Step(t.L("Tổng số lần thỏa mãn", "Total successful trials"), numerator);
        }
        if (complement) numerator = $"({denominator}-{numerator})";
        t.Answer(t.L("Phân số thực nghiệm", "Experimental fraction"), $"{numerator}/{denominator}");
        int valid = actualTotal.Sum() - invalid, favorableTrials = complement ? valid - actualSuccess.Sum() : actualSuccess.Sum();
        text += t.L($"Dựa vào các kết quả đã ghi nhận hợp lệ, viết phân số chỉ số lần {eventText} so với tổng số lần thử hợp lệ.",
            $"Using the valid recorded results, write the fraction of valid trials where {eventText}.");
        return t.Build("observed-relations-" + level, text,
            probability: new(source.Scenario.ContextId, eventText, favorableTrials, valid, true));
    }
}
