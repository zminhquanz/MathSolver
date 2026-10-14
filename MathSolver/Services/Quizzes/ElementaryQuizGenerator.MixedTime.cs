using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

public sealed record MixedTimePracticeProfile(int Star, int MaximumMajor, int MaximumFactor, bool Regroup, string[] UnitModes);
public sealed record MixedTimeTemplate(string Id, ElementaryQuizType Type, string TextId);

public sealed partial class ElementaryQuizGenerator
{
    public static ElementaryQuizType[] MixedTimeTypes => [ElementaryQuizType.MixedTimeAdd, ElementaryQuizType.MixedTimeSubtract,
        ElementaryQuizType.MixedTimeMultiply, ElementaryQuizType.MixedTimeDivide];
    public static bool IsMixedTime(ElementaryQuizType type) => type is ElementaryQuizType.MixedTimeAdd or ElementaryQuizType.MixedTimeSubtract
        or ElementaryQuizType.MixedTimeMultiply or ElementaryQuizType.MixedTimeDivide;

    private ElementaryQuizContract CreateMixedTime(ElementaryQuizType type, AppLanguage language, CurriculumTier tier)
    {
        var profile = QuizContentCatalog.LoadList<MixedTimePracticeProfile>("MixedTimePracticeProfiles").Single(p => p.Star == (int)tier);
        string units = profile.UnitModes[_random.Next(profile.UnitModes.Length)];
        bool hours = units != "minutes-seconds", seconds = units != "hours-minutes";
        int factor = _random.Next(2, profile.MaximumFactor + 1);
        int Major() => _random.Next(1, profile.MaximumMajor + 1);
        int Lower() => profile.Regroup ? _random.Next(30, 60) : _random.Next(1, 60 / factor);
        int Make(int major, int minor, int tail = 0) => hours ? major * 3600 + minor * 60 + tail : major * 60 + minor;
        int left = Make(Major(), Lower(), seconds && hours ? Lower() : 0), right = 0;
        if (type == ElementaryQuizType.MixedTimeAdd)
            right = Make(Major(), profile.Regroup ? _random.Next(30, 60) : _random.Next(1, 25), hours && seconds ? Lower() : 0);
        else if (type == ElementaryQuizType.MixedTimeSubtract)
        {
            // Keep durations nonnegative while deliberately exercising borrowing at higher stars.
            right = Make(Major(), profile.Regroup ? _random.Next(30, 60) : _random.Next(1, 20), hours && seconds ? Lower() : 0);
            left = profile.Regroup
                ? Make((hours ? right / 3600 : right / 60) + Major(), _random.Next(0, 30), hours && seconds ? _random.Next(0, 30) : 0)
                : right + Make(Major(), _random.Next(1, 20));
        }
        else if (type == ElementaryQuizType.MixedTimeDivide)
            left = checked(left * factor); // Construct an exact quotient, including its smaller units.

        int result = type switch
        {
            ElementaryQuizType.MixedTimeAdd => left + right,
            ElementaryQuizType.MixedTimeSubtract => left - right,
            ElementaryQuizType.MixedTimeMultiply => left * factor,
            ElementaryQuizType.MixedTimeDivide => left / factor,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        string F(int value) => MixedTimeAnswerRules.Format(value, hours, seconds, language);
        string T(string key, params (string Name, string Value)[] values) => QuizContentCatalog.Text(language, "MixedTime." + key, values);
        var builder = new DifficultyBuilder(QuizProblemKind.Time, type, language, tier) { RequiresSolution = false };
        string a = builder.Given("left-duration", left, "s");
        bool binary = type is ElementaryQuizType.MixedTimeAdd or ElementaryQuizType.MixedTimeSubtract;
        string b = binary ? builder.Given("right-duration", right, "s") : builder.Given("factor", factor);
        builder.Constant(60, 3600);
        string expression = a + (type switch { ElementaryQuizType.MixedTimeAdd => "+", ElementaryQuizType.MixedTimeSubtract => "-", ElementaryQuizType.MixedTimeMultiply => "*", _ => "/" }) + b;
        builder.Answer(T("Result"), expression);
        builder.Answers[0] = builder.Answers[0] with { DisplayValue = F(result) };
        // The numeric contract is in seconds; the pupil-facing steps retain mixed units.
        builder.Steps.Clear();
        builder.Explanation = MixedTimeAnswerRules.WorkedSolution(type, left, right, factor, hours, seconds, language);
        var templates = QuizContentCatalog.LoadList<MixedTimeTemplate>("MixedTimeTemplates").Where(t => t.Type == type).ToArray();
        var template = templates[_random.Next(templates.Length)];
        string problem = QuizContentCatalog.Text(language, template.TextId, ("left", F(left)), ("right", F(right)),
            ("factor", factor.ToString(CultureInfo.InvariantCulture)));
        int quantum = seconds ? 1 : 60;
        string[] choices = new[] { result, result + quantum, result + 60 * quantum, result + 61 * quantum }.Select(F).ToArray();
        return builder.Build("mixed-time-" + units, problem) with { RequiresCalculation = false, ChoiceTexts = choices };
    }
}
