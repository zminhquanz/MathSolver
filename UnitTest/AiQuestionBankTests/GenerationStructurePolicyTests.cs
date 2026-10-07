using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;

internal static class GenerationStructurePolicyTests
{
    public static void Run()
    {
        foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
        {
            var c = FractionQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects), ArithmeticOperation.Add,
                CurriculumTier.OneStar, language, new(19), "fraction-bread-Add");
            var d = FractionQuestionCatalogue.Draft(c);
            var valid = d with { GivenA = language == AppLanguage.Vietnamese ? d.GivenA.Replace("có", "sở hữu")
                : d.GivenA.Replace("has", "owns"),
                Question = (language == AppLanguage.Vietnamese ? "Theo báo cáo, " : "According to the report, ") + d.Question };
            if (!BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(valid), c).IsValid)
                throw new Exception("Safe phrase composition was rejected");
            string grammar = GgufQuestionRuntime.BuildGrammar(c);
            if (grammar.Contains("letter*") || grammar.Contains("prose ::=") || !grammar.Contains("{unit_a}"))
                throw new Exception("Free prose bypasses the semantic phrase grammar");
        }
        var screenshot = FractionQuestionCatalogue.Create(new(QuestionKnowledgeGroup.Objects), ArithmeticOperation.Add,
            CurriculumTier.OneStar, AppLanguage.Vietnamese, new(19), "fraction-bread-Add");
        var original = FractionQuestionCatalogue.Draft(screenshot);
        var bad = original with {
            GivenA = "Một người sở hữu {name} có {a} chiếc bánh mì, với đơn vị là {unit_a}.",
            GivenB = "Một cá nhân khác sở hữu {b} chiếc bánh mì, với đơn vị là {unit_b}.",
            Question = "Xin hãy xác định tổng số {unit} bánh mì mà hai cá nhân này cùng có.",
            SolutionLead = "Tổng số bánh mì là ({unit}):"
        };
        var validation = BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(bad), screenshot);
        if (validation.IsValid || validation.ErrorCode != "ChangedRelationOrTarget"
            || !new[] { "given_a", "given_b", "question", "solution_lead" }.All(field => validation.ErrorDetails?.Contains(field) == true))
            throw new Exception("Screenshot regression or field diagnostics failed");
        Console.WriteLine("PASS model structure regression, safe phrase substitutions, bound owner/unit slots and field diagnostics.");
    }
}
