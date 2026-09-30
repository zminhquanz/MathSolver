using MathSolver.Models;
using MathSolver.Services;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckMotionRealism()
    {
        var validator = new LlmWordProblemValidator();
        int count = 0;
        bool testedTinyChase = false;
        bool testedTinyMeeting = false;
        bool testedEnglishTiny = false;
        bool testedTrain = false;
        bool testedMeterVehicle = false;
        int namedHumanStories = 0;
        int namedMeterStories = 0;
        var basicMeterKinds = new HashSet<MotionQuestionKind>();

        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        foreach (MotionQuizType type in Enum.GetValues<MotionQuizType>())
        for (int seed = 0; seed < 200; seed++)
        {
            MotionQuizContract contract = new MotionQuizGenerator(new Random(seed))
                .GenerateContract(ArithmeticQuizMode.Essay, language, type)
                .MotionProblem!;
            string label = $"{language}/{type}/{seed}";
            var reference = new LlmWordProblemDraft
            {
                ProblemText = contract.ProblemText,
                SubjectName = contract.SubjectName,
                AnswerUnit = contract.AnswerUnit,
                SolutionLead = string.Empty
            };
            LlmWordProblemValidationResult accepted = validator.ValidateMotion(
                reference, contract, language);
            Require(accepted.IsValid,
                $"{label}: generated motion story failed validation: " +
                $"{accepted.ErrorCode}; {accepted.Feedback}");

            string story = contract.ProblemText.ToLowerInvariant();
            string namedHumanStory = ReplaceHumanSubjectWithName(
                contract.ProblemText, language);
            if (!string.Equals(namedHumanStory, contract.ProblemText,
                    StringComparison.Ordinal))
            {
                LlmWordProblemValidationResult namedHuman = validator.ValidateMotion(
                    new LlmWordProblemDraft
                    {
                        ProblemText = namedHumanStory,
                        SubjectName = contract.SubjectName,
                        AnswerUnit = contract.AnswerUnit,
                        SolutionLead = string.Empty
                    }, contract, language);
                Require(namedHuman.IsValid,
                    $"{label}: valid named-person story was rejected: " +
                    $"{namedHuman.ErrorCode}; {namedHuman.Feedback}; {namedHumanStory}");
                namedHumanStories++;
            }

            if (story.Contains("đi bộ", StringComparison.Ordinal) ||
                story.Contains("pedestrian", StringComparison.Ordinal) ||
                story.Contains("student walking", StringComparison.Ordinal))
            {
                int[] speeds = contract.QuestionKind switch
                {
                    MotionQuestionKind.BasicDistance or MotionQuestionKind.BasicTime or
                    MotionQuestionKind.BasicRestDistance => [contract.Facts[0]],
                    MotionQuestionKind.BasicSpeed => [(int)contract.CorrectAnswer],
                    MotionQuestionKind.CatchUpTime => [contract.Facts[1], contract.Facts[2]],
                    MotionQuestionKind.MeetingTime => [contract.Facts[1], contract.Facts[2]],
                    _ => []
                };
                Require(speeds.All(speed => speed is >= 1 and <= 2),
                    $"{label}: walking speed is implausible: {string.Join(", ", speeds)} m/s.");
            }

            if (story.Contains("tàu hỏa", StringComparison.Ordinal) ||
                story.Contains("train", StringComparison.Ordinal))
            {
                Require(type == MotionQuizType.Basic,
                    $"{label}: a train was paired with a road vehicle.");
                int speed = contract.QuestionKind == MotionQuestionKind.BasicSpeed
                    ? (int)contract.CorrectAnswer
                    : contract.Facts[0];
                int minimum = story.Contains("mph", StringComparison.Ordinal) ? 40 : 60;
                Require(speed >= minimum,
                    $"{label}: train speed is implausibly low: {speed}.");
                testedTrain = true;
            }

            if (story.Contains("m/s", StringComparison.Ordinal) &&
                (story.Contains("ô tô", StringComparison.Ordinal) ||
                 story.Contains("xe máy", StringComparison.Ordinal) ||
                 story.Contains("xe buýt", StringComparison.Ordinal) ||
                 story.Contains("car", StringComparison.Ordinal) ||
                 story.Contains("motorcycle", StringComparison.Ordinal) ||
                 story.Contains("bus", StringComparison.Ordinal)))
            {
                int[] speeds = contract.QuestionKind switch
                {
                    MotionQuestionKind.BasicSpeed => [(int)contract.CorrectAnswer],
                    MotionQuestionKind.CatchUpTime or MotionQuestionKind.MeetingTime =>
                        [contract.Facts[1], contract.Facts[2]],
                    _ => [contract.Facts[0]]
                };
                Require(speeds.All(speed => speed is >= 8 and <= 23) &&
                        contract.RequiredProblemUnits.Contains("m"),
                    $"{label}: vehicle speed/distance is inconsistent with m/s.");
                testedMeterVehicle = true;
            }

            if (type == MotionQuizType.River)
            {
                Require(story.Contains(language == AppLanguage.Vietnamese
                        ? "ca nô" : "motorboat", StringComparison.Ordinal) ||
                    story.Contains(language == AppLanguage.Vietnamese
                        ? "xuồng máy" : "speedboat", StringComparison.Ordinal),
                    $"{label}: river speed was assigned to an unpowered craft.");
                int currentSpeed = contract.QuestionKind switch
                {
                    MotionQuestionKind.RiverDownstreamSpeed or
                    MotionQuestionKind.RiverUpstreamSpeed => contract.Facts[1],
                    _ => (contract.Facts[0] - contract.Facts[1]) / 2
                };
                int maximum = story.Contains("m/s", StringComparison.Ordinal) ? 2 : 5;
                Require(currentSpeed is >= 1 && currentSpeed <= maximum,
                    $"{label}: river current speed is implausible: {currentSpeed}.");

                string genericPoweredCraft = language == AppLanguage.Vietnamese
                    ? contract.ProblemText
                        .Replace("ca nô", "thuyền máy", StringComparison.OrdinalIgnoreCase)
                        .Replace("xuồng máy", "thuyền máy", StringComparison.OrdinalIgnoreCase)
                    : contract.ProblemText
                        .Replace("motorboat", "powered boat", StringComparison.OrdinalIgnoreCase)
                        .Replace("speedboat", "powered boat", StringComparison.OrdinalIgnoreCase);
                Require(validator.ValidateMotion(new LlmWordProblemDraft
                    {
                        ProblemText = genericPoweredCraft,
                        SubjectName = contract.SubjectName,
                        AnswerUnit = contract.AnswerUnit,
                        SolutionLead = string.Empty
                    }, contract, language).IsValid,
                    $"{label}: an equivalent powered river craft was rejected.");

                if (seed == 0)
                {
                    string unpoweredCraft = language == AppLanguage.Vietnamese
                        ? contract.ProblemText
                            .Replace("ca nô", "thuyền chèo", StringComparison.OrdinalIgnoreCase)
                            .Replace("xuồng máy", "thuyền chèo", StringComparison.OrdinalIgnoreCase)
                        : contract.ProblemText
                            .Replace("motorboat", "canoe", StringComparison.OrdinalIgnoreCase)
                            .Replace("speedboat", "canoe", StringComparison.OrdinalIgnoreCase);
                    LlmWordProblemValidationResult rejected = validator.ValidateMotion(
                        new LlmWordProblemDraft
                        {
                            ProblemText = unpoweredCraft,
                            SubjectName = contract.SubjectName,
                            AnswerUnit = contract.AnswerUnit,
                            SolutionLead = string.Empty
                        }, contract, language);
                    Require(!rejected.IsValid && rejected.ErrorCode == "MotionSubjectMismatch",
                        $"{label}: AI changed a powered boat into a canoe.");
                }
            }

            if (language == AppLanguage.Vietnamese &&
                type is MotionQuizType.Chasing or MotionQuizType.Meeting &&
                story.Contains("rùa", StringComparison.Ordinal))
            {
                string gapUnit = story.Contains("mm/s", StringComparison.Ordinal) ? "mm" : "cm";
                string speedUnit = gapUnit + "/s";
                string humanStory = type == MotionQuizType.Chasing
                    ? $"An đi trước và đang cách Bình {contract.Facts[0]} {gapUnit}. " +
                      $"An đi với vận tốc {contract.Facts[1]} {speedUnit}, còn Bình đi cùng chiều " +
                      $"với vận tốc {contract.Facts[2]} {speedUnit}. Hỏi sau bao nhiêu giây " +
                      "thì Bình đuổi kịp An?"
                    : $"An và Bình ở hai điểm cách nhau {contract.Facts[0]} {gapUnit}, " +
                      $"cùng lúc đi ngược chiều về phía nhau. Vận tốc lần lượt là " +
                      $"{contract.Facts[1]} {speedUnit} và {contract.Facts[2]} {speedUnit}. " +
                      "Hỏi sau bao nhiêu giây thì hai người gặp nhau?";
                LlmWordProblemValidationResult rejected = validator.ValidateMotion(
                    new LlmWordProblemDraft
                    {
                        ProblemText = humanStory,
                        SubjectName = contract.SubjectName,
                        AnswerUnit = contract.AnswerUnit,
                        SolutionLead = string.Empty
                    }, contract, language);
                Require(!rejected.IsValid && rejected.ErrorCode == "MotionSubjectMismatch",
                    $"{label}: human motion was accepted for turtle units.");
                testedTinyChase |= type == MotionQuizType.Chasing;
                testedTinyMeeting |= type == MotionQuizType.Meeting;

                string genericTurtle = contract.ProblemText.Replace(
                    "rùa cạn", "rùa", StringComparison.OrdinalIgnoreCase);
                Require(validator.ValidateMotion(new LlmWordProblemDraft
                    {
                        ProblemText = genericTurtle,
                        SubjectName = contract.SubjectName,
                        AnswerUnit = contract.AnswerUnit,
                        SolutionLead = string.Empty
                    }, contract, language).IsValid,
                    $"{label}: a generic turtle story was rejected.");
            }

            if (type is MotionQuizType.Chasing or MotionQuizType.Meeting &&
                contract.RequiredProblemUnits.Contains("m") &&
                contract.RequiredProblemUnits.Contains("m/s"))
            {
                string namedStory = type == MotionQuizType.Chasing
                    ? language == AppLanguage.Vietnamese
                        ? $"An đi trước và cách Bình {contract.Facts[0]} m. An đi với vận tốc " +
                          $"{contract.Facts[1]} m/s, Bình đi cùng chiều với vận tốc " +
                          $"{contract.Facts[2]} m/s. Hỏi sau bao nhiêu giây Bình đuổi kịp An?"
                        : $"Alex is {contract.Facts[0]} m ahead of Liam. Alex moves at " +
                          $"{contract.Facts[1]} m/s, while Liam moves in the same direction " +
                          $"at {contract.Facts[2]} m/s. After how many seconds will Liam catch up?"
                    : language == AppLanguage.Vietnamese
                        ? $"An và Bình ở hai điểm cách nhau {contract.Facts[0]} m, cùng lúc " +
                          $"đi ngược chiều về phía nhau. Vận tốc lần lượt là " +
                          $"{contract.Facts[1]} m/s và {contract.Facts[2]} m/s. " +
                          "Hỏi sau bao nhiêu giây thì họ gặp nhau?"
                        : $"Alex and Liam start {contract.Facts[0]} m apart and move toward " +
                          $"each other at the same time. Their speeds are {contract.Facts[1]} m/s " +
                          $"and {contract.Facts[2]} m/s. After how many seconds will they meet?";
                LlmWordProblemValidationResult named = validator.ValidateMotion(
                    new LlmWordProblemDraft
                    {
                        ProblemText = namedStory,
                        SubjectName = contract.SubjectName,
                        AnswerUnit = contract.AnswerUnit,
                        SolutionLead = string.Empty
                    }, contract, language);
                Require(named.IsValid,
                    $"{label}: meter-scale named story was rejected: " +
                    $"{named.ErrorCode}; {named.Feedback}");
                namedMeterStories++;
            }

            if (type == MotionQuizType.Basic &&
                contract.RequiredProblemUnits.Contains("m") &&
                contract.RequiredProblemUnits.Contains("m/s"))
            {
                int[] facts = contract.Facts.ToArray();
                string namedBasic = (contract.QuestionKind, language) switch
                {
                    (MotionQuestionKind.BasicDistance, AppLanguage.Vietnamese) =>
                        $"An đi đều với vận tốc {facts[0]} m/s trong {facts[1]} giây. " +
                        "Hỏi quãng đường đi được bao nhiêu m?",
                    (MotionQuestionKind.BasicSpeed, AppLanguage.Vietnamese) =>
                        $"An đi được {facts[0]} m trong {facts[1]} giây. " +
                        "Hỏi vận tốc của An là bao nhiêu m/s?",
                    (MotionQuestionKind.BasicTime, AppLanguage.Vietnamese) =>
                        $"An đi đều với vận tốc {facts[0]} m/s và đi được {facts[1]} m. " +
                        "Hỏi An đi trong bao nhiêu giây?",
                    (MotionQuestionKind.BasicRestDistance, AppLanguage.Vietnamese) =>
                        $"An đi với vận tốc {facts[0]} m/s. Tổng thời gian là {facts[1]} " +
                        $"giây, trong đó nghỉ {facts[2]} giây. Hỏi An đi được bao nhiêu m?",
                    (MotionQuestionKind.BasicDistance, _) =>
                        $"Alex moves at {facts[0]} m/s for {facts[1]} seconds. " +
                        "How far does Alex travel in m?",
                    (MotionQuestionKind.BasicSpeed, _) =>
                        $"Alex travels {facts[0]} m in {facts[1]} seconds. " +
                        "What is Alex's speed in m/s?",
                    (MotionQuestionKind.BasicTime, _) =>
                        $"Alex moves at {facts[0]} m/s and covers {facts[1]} m. " +
                        "How many seconds does Alex travel?",
                    _ =>
                        $"Alex moves at {facts[0]} m/s. The total elapsed time is " +
                        $"{facts[1]} seconds, including a rest of {facts[2]} seconds. " +
                        "How far does Alex actually travel in m?"
                };
                LlmWordProblemValidationResult namedBasicResult = validator.ValidateMotion(
                    new LlmWordProblemDraft
                    {
                        ProblemText = namedBasic,
                        SubjectName = contract.SubjectName,
                        AnswerUnit = contract.AnswerUnit,
                        SolutionLead = string.Empty
                    }, contract, language);
                Require(namedBasicResult.IsValid,
                    $"{label}: named basic motion was rejected: " +
                    $"{namedBasicResult.ErrorCode}; {namedBasicResult.Feedback}");
                basicMeterKinds.Add(contract.QuestionKind);
            }

            if (language == AppLanguage.English &&
                type == MotionQuizType.Chasing &&
                (story.Contains("turtle", StringComparison.Ordinal) ||
                 story.Contains("tortoise", StringComparison.Ordinal)))
            {
                string gapUnit = story.Contains("mm/s", StringComparison.Ordinal) ? "mm" : "cm";
                string speedUnit = gapUnit + "/s";
                string humanStory = $"Alex is {contract.Facts[0]} {gapUnit} ahead of Liam. " +
                    $"Alex moves at {contract.Facts[1]} {speedUnit}, while Liam moves in the " +
                    $"same direction at {contract.Facts[2]} {speedUnit}. " +
                    "After how many seconds will Liam catch up?";
                LlmWordProblemValidationResult rejected = validator.ValidateMotion(
                    new LlmWordProblemDraft
                    {
                        ProblemText = humanStory,
                        SubjectName = contract.SubjectName,
                        AnswerUnit = contract.AnswerUnit,
                        SolutionLead = string.Empty
                    }, contract, language);
                Require(!rejected.IsValid && rejected.ErrorCode == "MotionSubjectMismatch",
                    $"{label}: English human motion was accepted for turtle units.");
                testedEnglishTiny = true;
            }

            count++;
        }

        Require(testedTinyChase && testedTinyMeeting && testedEnglishTiny &&
                testedTrain && testedMeterVehicle && namedHumanStories > 0 &&
                namedMeterStories > 0 && basicMeterKinds.Count == 4,
            "The motion sample missed tiny animals, trains, or vehicles using m/s.");

        CheckNamedHumanMotion(validator);
        Console.WriteLine($"  Checked {count} motion contracts, {namedHumanStories} named-person and {namedMeterStories} meter-scale rewrites.");
    }

    private static string ReplaceHumanSubjectWithName(
        string problem,
        AppLanguage language)
    {
        (string Subject, string Name)[] replacements =
            language == AppLanguage.Vietnamese
                ?
                [
                    ("một người đi bộ", "An"),
                    ("một học sinh đi bộ", "Bình"),
                    ("một vận động viên chạy bộ", "An"),
                    ("một người chạy bộ", "Bình")
                ]
                :
                [
                    ("a pedestrian", "Alex"),
                    ("a student walking", "Liam"),
                    ("a runner", "Alex"),
                    ("a jogger", "Liam")
                ];

        foreach ((string subject, string name) in replacements)
        {
            problem = problem.Replace(
                subject, name,
                StringComparison.OrdinalIgnoreCase);
        }

        return problem;
    }

    private static void CheckNamedHumanMotion(LlmWordProblemValidator validator)
    {
        var contract = new MotionQuizContract(
            MotionQuizType.Chasing,
            MotionQuestionKind.CatchUpTime,
            [110, 4, 9],
            22,
            "giây",
            "thời gian đuổi kịp",
            "Một người chạy bộ đi trước và đang cách một vận động viên chạy bộ 110 m. " +
            "Người chạy bộ đi với vận tốc 4 m/s, còn vận động viên chạy bộ đi cùng " +
            "chiều với vận tốc 9 m/s. Hỏi sau bao nhiêu giây thì đuổi kịp?",
            "110 ÷ (9 - 4) = 22",
            string.Empty,
            110,
            ArithmeticOperation.Divide,
            5,
            ["m", "m/s", "giây"]);

        foreach (string problem in new[]
                 {
                     "Bác An đi trước và đang cách bác Bình 110 m. Bác An đi với vận tốc " +
                     "4 m/s, còn bác Bình đi cùng chiều với vận tốc 9 m/s. Hỏi sau bao nhiêu " +
                     "giây thì bác Bình đuổi kịp bác An?",
                     "An đi trước và đang cách Bình 110 m. An đi với vận tốc 4 m/s, còn Bình " +
                     "đi cùng chiều với vận tốc 9 m/s. Hỏi sau bao nhiêu giây thì Bình đuổi kịp An?"
                 })
        {
            var draft = new LlmWordProblemDraft
            {
                ProblemText = problem,
                SubjectName = "thời gian Bình đuổi kịp An",
                AnswerUnit = "giây",
                SolutionLead = "Thời gian Bình đuổi kịp An là:"
            };
            LlmWordProblemValidationResult result = validator.ValidateMotion(
                draft, contract, AppLanguage.Vietnamese);
            Require(result.IsValid,
                $"Named human catch-up story was rejected: {result.ErrorCode}; {result.Feedback}");
        }

        var wrongCategory = new LlmWordProblemDraft
        {
            ProblemText = "Một con rùa đi trước và đang cách một con rùa khác 110 m. " +
                          "Con rùa thứ nhất đi với vận tốc 4 m/s, con còn lại đi cùng chiều " +
                          "với vận tốc 9 m/s. Hỏi sau bao nhiêu giây thì đuổi kịp?",
            SubjectName = "thời gian đuổi kịp",
            AnswerUnit = "giây",
            SolutionLead = "Thời gian đuổi kịp là:"
        };
        Require(validator.ValidateMotion(wrongCategory, contract, AppLanguage.Vietnamese)
                .ErrorCode == "MotionSubjectMismatch",
            "A human motion contract accepted an animal as the moving subject.");

        var vehicleContract = new MotionQuizContract(
            MotionQuizType.Chasing,
            MotionQuestionKind.CatchUpTime,
            [59, 12, 13],
            59,
            "giây",
            "thời gian đuổi kịp",
            "Một ô tô đi trước và đang cách một xe máy 59 m. Ô tô đi với vận tốc " +
            "12 m/s, còn xe máy đi cùng chiều với vận tốc 13 m/s. Hỏi sau bao " +
            "nhiêu giây thì xe máy đuổi kịp?",
            "59 ÷ (13 - 12) = 59",
            string.Empty,
            59,
            ArithmeticOperation.Divide,
            1,
            ["m", "m/s", "giây"]);

        foreach (string distanceUnit in new[] { "m", "mét" })
        {
            var namedStory = new LlmWordProblemDraft
            {
                ProblemText = $"Bác An đi trước và đang cách bác Bình 59 {distanceUnit}. " +
                              "Bác An đi với vận tốc 12 m/s, còn bác Bình đi cùng chiều " +
                              "với vận tốc 13 m/s. Hỏi sau bao nhiêu giây thì bác Bình " +
                              "đuổi kịp bác An?",
                SubjectName = "thời gian để bác Bình đuổi kịp bác An",
                AnswerUnit = "giây",
                SolutionLead = "Thời gian để đuổi kịp là:"
            };
            LlmWordProblemValidationResult result = validator.ValidateMotion(
                namedStory, vehicleContract, AppLanguage.Vietnamese);
            Require(result.IsValid,
                $"Screenshot scenario with '{distanceUnit}' was rejected: " +
                $"{result.ErrorCode}; {result.Feedback}");
        }

        var spelledSpeed = new LlmWordProblemDraft
        {
            ProblemText = "Bác An đi trước và đang cách bác Bình 59 mét. " +
                          "Bác An đi với vận tốc 12 mét mỗi giây, còn bác Bình đi cùng " +
                          "chiều với vận tốc 13 mét mỗi giây. Hỏi sau bao nhiêu giây " +
                          "thì bác Bình đuổi kịp bác An?",
            SubjectName = "thời gian đuổi kịp",
            AnswerUnit = "giây",
            SolutionLead = string.Empty
        };
        Require(validator.ValidateMotion(spelledSpeed, vehicleContract,
                AppLanguage.Vietnamese).IsValid,
            "Equivalent written-out meter and meter-per-second units were rejected.");

        var wrongUnit = new LlmWordProblemDraft
        {
            ProblemText = "Bác An đi trước và đang cách bác Bình 59 mm. " +
                          "Bác An đi với vận tốc 12 m/s, còn bác Bình đi cùng chiều " +
                          "với vận tốc 13 m/s. Hỏi sau bao nhiêu giây thì bác Bình đuổi kịp?",
            SubjectName = "thời gian đuổi kịp",
            AnswerUnit = "giây",
            SolutionLead = string.Empty
        };
        Require(validator.ValidateMotion(wrongUnit, vehicleContract, AppLanguage.Vietnamese)
                .ErrorCode == "MotionUnitMismatch",
            "Changing a 59 m gap to 59 mm was accepted.");

        var hiddenWrongUnit = new LlmWordProblemDraft
        {
            ProblemText = "Bác An đi trước và đang cách bác Bình 59 mm. " +
                          "Bác An đi với vận tốc 12 mét mỗi giây, còn bác Bình đi cùng chiều " +
                          "với vận tốc 13 mét mỗi giây. Hỏi sau bao nhiêu giây thì đuổi kịp?",
            SubjectName = "thời gian đuổi kịp",
            AnswerUnit = "giây",
            SolutionLead = string.Empty
        };
        Require(validator.ValidateMotion(hiddenWrongUnit, vehicleContract,
                AppLanguage.Vietnamese).ErrorCode == "MotionUnitMismatch",
            "The speed phrase concealed a wrong distance unit.");
    }
}
