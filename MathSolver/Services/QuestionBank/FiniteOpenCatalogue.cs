using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>The six explicit relations in the final finite prose expansion.</summary>
internal static class FiniteOpenCatalogue
{
    internal static bool Supports(BankQuestionFamily family, int variant) => family switch
    {
        BankQuestionFamily.Average => AverageRelationsPilot.Supports(variant),
        BankQuestionFamily.Remainder => RemainderGroupsPilot.Supports(variant),
        BankQuestionFamily.Proportion => InverseWorkPilot.Supports(variant),
        _ => false
    };

    internal static IEnumerable<string> Profiles(BankQuestionFamily family, int variant, AppLanguage language) => family switch
    {
        BankQuestionFamily.Average when AverageRelationsPilot.Supports(variant) => AverageRelationsPilot.Profiles(language, variant),
        BankQuestionFamily.Remainder when RemainderGroupsPilot.Supports(variant) => RemainderGroupsPilot.Profiles(language, variant),
        BankQuestionFamily.Proportion when InverseWorkPilot.Supports(variant) => InverseWorkPilot.Profiles(language, variant),
        _ => []
    };

    internal static bool IsProfile(BasicQuestionContract c) => AverageRelationsOpenCatalogue.IsProfile(c)
        || RemainderGroupsOpenCatalogue.IsProfile(c) || InverseWorkOpenCatalogue.IsProfile(c);

    internal static NarrativeLesson Build(BasicQuestionContract c) => AverageRelationsOpenCatalogue.IsProfile(c)
        ? AverageRelationsOpenCatalogue.Build(c) : RemainderGroupsOpenCatalogue.IsProfile(c)
        ? RemainderGroupsOpenCatalogue.Build(c) : InverseWorkOpenCatalogue.IsProfile(c)
        ? InverseWorkOpenCatalogue.Build(c) : throw new ArgumentException("UnknownFiniteOpenProfile");

    internal static ArithmeticQuizQuestion Generate(BasicQuestionContract c, ArithmeticQuizMode mode) => AverageRelationsOpenCatalogue.IsProfile(c)
        ? AverageRelationsOpenCatalogue.Generate(c, mode) : RemainderGroupsOpenCatalogue.IsProfile(c)
        ? RemainderGroupsOpenCatalogue.Generate(c, mode) : InverseWorkOpenCatalogue.IsProfile(c)
        ? InverseWorkOpenCatalogue.Generate(c, mode) : throw new ArgumentException("UnknownFiniteOpenProfile");

    internal static BasicDraftValidation Validate(BasicQuestionContract c, BasicQuestionDraft draft) => AverageRelationsOpenCatalogue.IsProfile(c)
        ? AverageRelationsOpenMeaning.Validate(c, draft) : RemainderGroupsOpenCatalogue.IsProfile(c)
        ? RemainderGroupsOpenMeaning.Validate(c, draft) : InverseWorkOpenCatalogue.IsProfile(c)
        ? InverseWorkOpenMeaning.Validate(c, draft) : new(null, "InvalidContract");

    internal static string WritingInstructions(BasicQuestionContract c) => AverageRelationsOpenCatalogue.IsProfile(c)
        ? AverageRelationsOpenCatalogue.WritingInstructions(c.Language) : RemainderGroupsOpenCatalogue.IsProfile(c)
        ? RemainderGroupsOpenCatalogue.WritingInstructions(c) : InverseWorkOpenCatalogue.WritingInstructions(c.Language);

    internal static string PromptRelation(BasicQuestionContract c) => AverageRelationsOpenCatalogue.IsProfile(c)
        ? AverageRelationsOpenCatalogue.PromptRelation(c) : RemainderGroupsOpenCatalogue.IsProfile(c)
        ? RemainderGroupsOpenCatalogue.PromptRelation(c) : InverseWorkOpenCatalogue.PromptRelation(c);
}
