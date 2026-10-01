using MathSolver.Models;
using MathSolver.Services;
using System.Text.Json;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckPromptMatrix()
    {
        int count = 0;
        foreach ((QuizProblemKind kind, object subtype) in AllSubtypes())
        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        {
            ArithmeticQuizQuestion question = Generate(
                kind, subtype, ArithmeticQuizMode.Essay, language, 4000 + count);
            WordProblemStoryContext story =
                WordProblemStoryContextCatalog.GetProfile(language).Items[0];
            string prompt = kind switch
            {
                QuizProblemKind.Arithmetic => LlmQuizPromptBuilder.BuildUserPrompt(
                    question.Expression, language, null, story, null),
                QuizProblemKind.Fraction => LlmQuizPromptBuilder.BuildFractionUserPrompt(
                    question.FractionProblem!, language, null, story, null),
                QuizProblemKind.Geometry => LlmQuizPromptBuilder.BuildGeometryUserPrompt(
                    question.GeometryProblem!, language, null, null),
                QuizProblemKind.FindX => LlmQuizPromptBuilder.BuildFindXUserPrompt(
                    question.FindXProblem!, language, null, story, null),
                QuizProblemKind.Proportion => LlmQuizPromptBuilder.BuildProportionUserPrompt(
                    question.ProportionProblem!, language, null),
                QuizProblemKind.Motion => LlmQuizPromptBuilder.BuildMotionUserPrompt(
                    question.MotionProblem!, language, null),
                QuizProblemKind.Average => LlmQuizPromptBuilder.BuildAverageUserPrompt(
                    question.AverageProblem!, language, null),
                QuizProblemKind.Percentage => LlmQuizPromptBuilder.BuildPercentageUserPrompt(
                    question.PercentageProblem!, language, null),
                _ => throw new ArgumentOutOfRangeException()
            };

            string label = $"{kind}/{subtype}/{language}";
            Require(prompt.Length > 100, $"{label}: prompt is unexpectedly short.");
            foreach (string field in new[] { "problem_text", "subject_name", "answer_unit", "solution_lead" })
                Require(prompt.Contains(field, StringComparison.Ordinal),
                    $"{label}: prompt is missing JSON field {field}.");

            IReadOnlyList<int>? facts = kind switch
            {
                QuizProblemKind.Proportion => new[]
                {
                    question.ProportionProblem!.A,
                    question.ProportionProblem!.B,
                    question.ProportionProblem!.C
                },
                QuizProblemKind.Motion => question.MotionProblem!.Facts,
                QuizProblemKind.Average => question.AverageProblem!.Facts,
                QuizProblemKind.Percentage => question.PercentageProblem!.Facts,
                _ => null
            };
            if (facts is not null)
                foreach (int fact in facts)
                    Require(prompt.Contains(fact.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            StringComparison.Ordinal),
                        $"{label}: prompt lost contract fact {fact}.");
            count++;
        }
        Console.WriteLine($"  Checked {count} subtype/language AI prompts.");
    }

    internal static void CheckParser()
    {
        const string valid = """
            {"problem_text":"Emma has 3 pens and gets 2 more pens. How many pens does she have?","subject_name":"Emma","answer_unit":"pens","solution_lead":"The number of pens Emma has is:"}
            """;
        Require(LlmWordProblemParser.TryParse(valid, out LlmWordProblemDraft? draft,
                out string successCode, out _) && draft?.AnswerUnit == "pens",
            $"Valid AI JSON rejected: {successCode}");
        Require(LlmWordProblemParser.TryParse("```json\n" + valid + "\n```", out _, out _, out _),
            "Fenced AI JSON should be accepted.");

        var failures = new (string Name, string Text, string Error)[]
        {
            ("empty", "  ", "EmptyModelOutput"),
            ("control tokens", "<turn|>", "EmptyModelOutput"),
            ("truncated", "{\"problem_text\":\"incomplete", "IncompleteJson"),
            ("multiple objects", valid + valid, "MultipleJsonObjects"),
            ("outside text", "Here is the answer: " + valid, "InvalidJson"),
            ("extra field", valid[..^1] + ",\"answer\":\"5\"}", "UnexpectedJsonFields"),
            ("duplicate field", valid[..^1] + ",\"answer_unit\":\"pens\"}", "DuplicateJsonFields"),
            ("missing field", "{\"problem_text\":\"A problem\"}", "MissingJsonFields")
        };
        foreach ((string name, string raw, string expected) in failures)
        {
            bool parsed = LlmWordProblemParser.TryParse(raw, out _, out string code, out _);
            Require(!parsed && code == expected,
                $"{name}: expected {expected}, got {(parsed ? "accepted" : code)}.");
        }
        string fractionJson = JsonSerializer.Serialize(new
        {
            problem_text = "Emma has $\\frac{1}{2}$ pens. How many pens?",
            subject_name = "Emma", answer_unit = "pens",
            solution_lead = "The number of pens is:"
        });
        Require(LlmWordProblemParser.TryParse(fractionJson, out LlmWordProblemDraft? fractionDraft,
                out _, out _) && fractionDraft?.ProblemText?.Contains("1/2", StringComparison.Ordinal) == true,
            "LaTeX fraction was not normalized in AI output.");
    }

    internal static void CheckValidators()
    {
        var validator = new LlmWordProblemValidator();
        int count = 0;
        foreach ((QuizProblemKind kind, object subtype) in AllSubtypes())
        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        {
            if (kind is QuizProblemKind.Arithmetic or QuizProblemKind.Fraction or QuizProblemKind.FindX)
                continue;
            ArithmeticQuizQuestion question = Generate(
                kind, subtype, ArithmeticQuizMode.Essay, language, 8000 + count);
            string label = $"{kind}/{subtype}/{language}";
            LlmWordProblemDraft draft = DraftFromAlgorithm(kind, question);
            LlmWordProblemValidationResult result = ValidateQuestion(
                validator, kind, question, draft, language);
            Require(result.IsValid && result.WordProblem is not null,
                $"{label}: algorithm reference story rejected: {result.ErrorCode} — {result.Feedback}");

            var badUnit = new LlmWordProblemDraft
            {
                ProblemText = kind == QuizProblemKind.Geometry
                    ? draft.ProblemText!.Replace(
                        question.GeometryProblem!.Dimensions.Values.First().ToString(),
                        "987654321", StringComparison.Ordinal)
                    : draft.ProblemText,
                SubjectName = draft.SubjectName,
                AnswerUnit = "incorrect unit",
                SolutionLead = draft.SolutionLead
            };
            LlmWordProblemValidationResult rejected = ValidateQuestion(
                validator, kind, question, badUnit, language);
            Require(!rejected.IsValid && rejected.WordProblem is null,
                $"{label}: validator accepted an incorrect answer unit.");
            count++;
        }
        Console.WriteLine($"  Checked {count} valid and invalid AI contracts.");
        CheckArithmeticAndFractionValidators();
    }

    private static void CheckArithmeticAndFractionValidators()
    {
        var validator = new LlmWordProblemValidator();
        int count = 0;
        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        {
            bool english = language == AppLanguage.English;
            WordProblemStoryContext context = english
                ? new(WordProblemContextCategory.SchoolSupply, "pens", "pens")
                : new(WordProblemContextCategory.SchoolSupply, "cây bút", "cây bút");
            foreach (ArithmeticOperation operation in Enum.GetValues<ArithmeticOperation>())
            {
                (int left, int right, string en, string vi) = operation switch
                {
                    ArithmeticOperation.Add => (3, 2,
                        "Emma has 3 pens and receives 2 more pens. How many pens does Emma have altogether?",
                        "Lan có 3 cây bút và nhận thêm 2 cây bút. Hỏi Lan có tất cả bao nhiêu cây bút?"),
                    ArithmeticOperation.Subtract => (5, 2,
                        "Emma has 5 pens and uses 2 pens. How many pens does Emma have left?",
                        "Lan có 5 cây bút và dùng 2 cây bút. Hỏi Lan còn lại bao nhiêu cây bút?"),
                    ArithmeticOperation.Multiply => (3, 2,
                        "Emma has 3 boxes with 2 pens in each box. How many pens does Emma have altogether?",
                        "Lan có 3 hộp, mỗi hộp có 2 cây bút. Hỏi Lan có tất cả bao nhiêu cây bút?"),
                    _ => (6, 2,
                        "Emma shares 6 pens equally among 2 friends. What is the number of pens received by one friend?",
                        "Lan chia đều 6 cây bút cho 2 bạn. Hỏi số cây bút một bạn nhận được là bao nhiêu?")
                };
                var expression = new IntegerArithmeticExpression(left, operation, right);
                var draft = new LlmWordProblemDraft
                {
                    ProblemText = english ? en : vi,
                    SubjectName = english ? "Emma" : "Lan",
                    AnswerUnit = context.AnswerUnit,
                    SolutionLead = english ? "The number of pens is:" : "Số cây bút là:"
                };
                LlmWordProblemValidationResult result = validator.Validate(
                    draft, expression, Evaluate(left, operation, right), context, language);
                Require(result.IsValid,
                    $"Arithmetic/{operation}/{language}: {result.ErrorCode} — {result.Feedback}");
                var changedFact = new LlmWordProblemDraft
                {
                    ProblemText = draft.ProblemText!.Replace($"{left} ", "987 ", StringComparison.Ordinal),
                    SubjectName = draft.SubjectName,
                    AnswerUnit = draft.AnswerUnit,
                    SolutionLead = draft.SolutionLead
                };
                Require(validator.Validate(changedFact, expression,
                    Evaluate(left, operation, right), context, language).ErrorCode == "ProblemNumbersMismatch",
                    $"Arithmetic/{operation}/{language}: changed operand was not detected.");
                count++;
            }

            foreach (FractionOperation operation in new[]
                { FractionOperation.Add, FractionOperation.Subtract,
                  FractionOperation.Multiply, FractionOperation.Divide })
            {
                var contract = new FractionQuizContract(
                    new ReducedFraction(1, 2), operation, new ReducedFraction(1, 4),
                    operation switch
                    {
                        FractionOperation.Add => new ReducedFraction(3, 4),
                        FractionOperation.Subtract => new ReducedFraction(1, 4),
                        FractionOperation.Multiply => new ReducedFraction(1, 8),
                        _ => new ReducedFraction(2, 1)
                    }, null, []);
                string story = (operation, english) switch
                {
                    (FractionOperation.Add, true) =>
                        "Emma has 1/2 pens and receives 1/4 pens. How much pens does she have altogether?",
                    (FractionOperation.Subtract, true) =>
                        "Emma has 1/2 pens and uses 1/4 pens. How much pens remain?",
                    (FractionOperation.Multiply, true) =>
                        "Emma has 1/2 pens and takes 1/4 of those pens. How much pens does she take?",
                    (FractionOperation.Divide, true) =>
                        "Emma has 1/2 pens divided into groups of 1/4 pens. How many groups of pens are there?",
                    (FractionOperation.Add, false) =>
                        "Lan có 1/2 cây bút và nhận thêm 1/4 cây bút. Hỏi Lan có tất cả bao nhiêu cây bút?",
                    (FractionOperation.Subtract, false) =>
                        "Lan có 1/2 cây bút và dùng 1/4 cây bút. Hỏi Lan còn lại bao nhiêu cây bút?",
                    (FractionOperation.Multiply, false) =>
                        "Lan có 1/2 cây bút và lấy 1/4 của số đó. Hỏi Lan lấy bao nhiêu cây bút?",
                    _ =>
                        "Lan chia 1/2 cây bút thành các phần 1/4 cây bút. Hỏi có bao nhiêu phần cây bút?"
                };
                var draft = new LlmWordProblemDraft
                {
                    ProblemText = story,
                    SubjectName = english ? "Emma" : "Lan",
                    AnswerUnit = context.AnswerUnit,
                    SolutionLead = english ? "The number of pens is:" : "Số cây bút là:"
                };
                LlmWordProblemValidationResult result = validator.ValidateFraction(
                    draft, contract, context, language);
                Require(result.IsValid,
                    $"Fraction/{operation}/{language}: {result.ErrorCode} — {result.Feedback}");
                var changedFact = new LlmWordProblemDraft
                {
                    ProblemText = story.Replace("1/2", "2/3", StringComparison.Ordinal),
                    SubjectName = draft.SubjectName,
                    AnswerUnit = draft.AnswerUnit,
                    SolutionLead = draft.SolutionLead
                };
                Require(validator.ValidateFraction(changedFact, contract, context, language)
                        .ErrorCode == "FractionFactsMismatch",
                    $"Fraction/{operation}/{language}: changed numerator was not detected.");
                count++;
            }
        }
        Console.WriteLine($"  Checked {count} arithmetic/fraction AI stories and changed-fact rejections.");
        CheckFindXValidator();
    }

    private static void CheckFindXValidator()
    {
        var validator = new LlmWordProblemValidator();
        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        {
            bool english = language == AppLanguage.English;
            string item = english ? "pens" : "cây bút";
            var addition = new FindXQuizContract(
                2, 5, ArithmeticOperation.Add, true, 3,
                new IntegerArithmeticExpression(5, ArithmeticOperation.Subtract, 2));
            var validDraft = new LlmWordProblemDraft
            {
                ProblemText = english
                    ? "Emma had some pens. She received 2 more pens and has in total 5 pens. How many pens did Emma have at first?"
                    : "Lan có một số cây bút. Lan nhận thêm 2 cây bút và có tất cả 5 cây bút. Hỏi ban đầu Lan có bao nhiêu cây bút?",
                SubjectName = english ? "Emma" : "Lan",
                AnswerUnit = item,
                SolutionLead = english ? "The unknown number of pens is:" : "Số cây bút chưa biết là:"
            };
            LlmWordProblemValidationResult positive = validator.ValidateFindX(
                validDraft, addition, item, item, language);
            Require(positive.IsValid,
                $"FindX/Add/{language}: {positive.ErrorCode} — {positive.Feedback}");

            var wrongFact = new LlmWordProblemDraft
            {
                ProblemText = validDraft.ProblemText!.Replace(" 2 ", " 9 ", StringComparison.Ordinal),
                SubjectName = validDraft.SubjectName,
                AnswerUnit = validDraft.AnswerUnit,
                SolutionLead = validDraft.SolutionLead
            };
            Require(validator.ValidateFindX(wrongFact, addition, item, item, language)
                    .ErrorCode == "ProblemNumbersMismatch",
                $"FindX/Add/{language}: changed equation fact was not detected.");

            foreach (ArithmeticOperation operation in Enum.GetValues<ArithmeticOperation>())
            {
                FindXQuizContract contract = Generate(
                    QuizProblemKind.FindX, operation, ArithmeticQuizMode.Essay,
                    language, 9000 + (int)operation).FindXProblem!;
                var incorrect = new LlmWordProblemDraft
                {
                    ProblemText = english
                        ? $"Emma has 999999991 pens and then {contract.ResultValue} pens. How many pens?"
                        : $"Lan có 999999991 cây bút và sau đó có {contract.ResultValue} cây bút. Hỏi Lan có bao nhiêu cây bút?",
                    SubjectName = english ? "Emma" : "Lan",
                    AnswerUnit = item,
                    SolutionLead = english ? "The number of pens is:" : "Số cây bút là:"
                };
                Require(validator.ValidateFindX(incorrect, contract, item, item, language)
                        .ErrorCode == "ProblemNumbersMismatch",
                    $"FindX/{operation}/{language}: incorrect known value was not detected.");
            }
        }
        Console.WriteLine("  Checked Find-X AI story and fact rejection for every operation in both languages.");
    }

    private static LlmWordProblemDraft DraftFromAlgorithm(
        QuizProblemKind kind, ArithmeticQuizQuestion question)
    {
        MathWordProblem? story = question.WordProblem;
        string? problem = kind switch
        {
            QuizProblemKind.Geometry => GeometryReferenceText(question.GeometryProblem!),
            QuizProblemKind.Average => question.AverageProblem!.ProblemText,
            QuizProblemKind.Percentage => question.PercentageProblem!.ProblemText,
            QuizProblemKind.Proportion => question.ProportionProblem!.ProblemText,
            QuizProblemKind.Motion => question.MotionProblem!.ProblemText,
            _ => story?.ProblemText
        };
        string? subject = kind switch
        {
            QuizProblemKind.Average => question.AverageProblem!.SubjectName,
            QuizProblemKind.Percentage => question.PercentageProblem!.SubjectName,
            QuizProblemKind.Proportion => question.ProportionProblem!.SubjectName,
            QuizProblemKind.Motion => question.MotionProblem!.SubjectName,
            _ => story?.SubjectName
        };
        string? unit = kind switch
        {
            QuizProblemKind.Average => question.AverageProblem!.AnswerUnit,
            QuizProblemKind.Percentage => question.PercentageProblem!.AnswerUnit,
            QuizProblemKind.Proportion => question.ProportionProblem!.AnswerUnit,
            QuizProblemKind.Motion => question.MotionProblem!.AnswerUnit,
            _ => story?.AnswerUnit
        };
        Require(problem is not null, $"{kind}: no reference problem text.");
        return new LlmWordProblemDraft
        {
            ProblemText = problem,
            SubjectName = subject,
            AnswerUnit = unit,
            SolutionLead = story?.SolutionLead ??
                (AppLanguageManager.CurrentLanguage == AppLanguage.English
                    ? "The requested quantity is:"
                    : "Đại lượng cần tìm là:")
        };
    }

    private static string GeometryReferenceText(GeometryQuizContract contract)
    {
        bool english = AppLanguageManager.CurrentLanguage == AppLanguage.English;
        string measurement = (contract.Measurement, english) switch
        {
            (GeometryMeasurement.Perimeter, true) => "perimeter",
            (GeometryMeasurement.Area, true) => "area",
            (GeometryMeasurement.TotalArea, true) => "total surface area",
            (GeometryMeasurement.LateralArea, true) => "lateral surface area",
            (GeometryMeasurement.Volume, true) => "volume",
            (GeometryMeasurement.Perimeter, false) => "chu vi",
            (GeometryMeasurement.Area, false) => "diện tích",
            (GeometryMeasurement.TotalArea, false) => "diện tích toàn phần",
            (GeometryMeasurement.LateralArea, false) => "diện tích xung quanh",
            _ => "thể tích"
        };
        string dimensions = string.Join(", ", contract.Dimensions.Select(pair =>
            $"{pair.Value} {contract.LengthUnitSymbol}"));
        string circleRule = contract.ShapeId == "circle"
            ? english ? " Use π = 3.14." : " Lấy π = 3,14."
            : string.Empty;
        return english
            ? $"A {contract.ObjectName} is shaped like a {contract.ShapeName} with {dimensions}." +
              $" Calculate its {measurement}?{circleRule}"
            : $"Một {contract.ObjectName} có dạng {contract.ShapeName} với {dimensions}." +
              $" Tính {measurement} của nó?{circleRule}";
    }

    private static LlmWordProblemValidationResult ValidateQuestion(
        LlmWordProblemValidator validator, QuizProblemKind kind,
        ArithmeticQuizQuestion question, LlmWordProblemDraft draft,
        AppLanguage language)
    {
        WordProblemStoryContext context = WordProblemStoryContextCatalog
            .GetProfile(language).Items[0];
        return kind switch
        {
            QuizProblemKind.Arithmetic => validator.Validate(
                draft, question.Expression, question.CorrectAnswer, context, language),
            QuizProblemKind.Fraction => validator.ValidateFraction(
                draft, question.FractionProblem!, context, language),
            QuizProblemKind.Geometry => validator.ValidateGeometry(
                draft, question.GeometryProblem!, language),
            QuizProblemKind.FindX => validator.ValidateFindX(
                draft, question.FindXProblem!, context.NaturalReference,
                LlmQuizPromptBuilder.GetFindXAnswerUnit(
                    question.FindXProblem!, context, language), language),
            QuizProblemKind.Proportion => validator.ValidateProportion(
                draft, question.ProportionProblem!, language),
            QuizProblemKind.Motion => validator.ValidateMotion(
                draft, question.MotionProblem!, language),
            QuizProblemKind.Average => validator.ValidateAverage(
                draft, question.AverageProblem!, language),
            QuizProblemKind.Percentage => validator.ValidatePercentage(
                draft, question.PercentageProblem!, language),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}
