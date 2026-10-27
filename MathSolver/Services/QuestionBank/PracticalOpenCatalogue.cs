using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

internal static class PracticalOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>PercentageRatioOpenCatalogue.IsProfile(c)||MoneyChangeOpenCatalogue.IsProfile(c)||TriangleAreaOpenCatalogue.IsProfile(c)||RectangleSideOpenCatalogue.IsProfile(c);
    internal static IEnumerable<string> Profiles(BankQuestionFamily family,int variant,AppLanguage language)
        =>family==BankQuestionFamily.Percentage&&variant==PercentageRatioPilot.Variant?PercentageRatioPilot.Profiles(language)
        :family==BankQuestionFamily.MultiStep&&variant==MoneyChangePilot.Variant?MoneyChangePilot.Profiles(language)
        :family==BankQuestionFamily.Geometry&&variant==TriangleAreaPilot.Variant?TriangleAreaPilot.Profiles(language)
        :family==BankQuestionFamily.Spatial&&variant==RectangleSidePilot.Variant?RectangleSidePilot.Profiles(language):[];
    internal static NarrativeLesson Build(BasicQuestionContract c)=>PercentageRatioOpenCatalogue.IsProfile(c)?PercentageRatioOpenCatalogue.Build(c)
        :MoneyChangeOpenCatalogue.IsProfile(c)?MoneyChangeOpenCatalogue.Build(c):TriangleAreaOpenCatalogue.IsProfile(c)?TriangleAreaOpenCatalogue.Build(c):RectangleSideOpenCatalogue.Build(c);
    internal static ArithmeticQuizQuestion Generate(BasicQuestionContract c,ArithmeticQuizMode mode)=>PercentageRatioOpenCatalogue.IsProfile(c)?PercentageRatioOpenCatalogue.Generate(c,mode)
        :MoneyChangeOpenCatalogue.IsProfile(c)?MoneyChangeOpenCatalogue.Generate(c,mode):TriangleAreaOpenCatalogue.IsProfile(c)?TriangleAreaOpenCatalogue.Generate(c,mode):RectangleSideOpenCatalogue.Generate(c,mode);
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)
    {
        var meaning=PercentageRatioOpenCatalogue.IsProfile(c)?PercentageRatioOpenMeaning.Validate(c,d)
            :MoneyChangeOpenCatalogue.IsProfile(c)?MoneyChangeOpenMeaning.Validate(c,d):TriangleAreaOpenCatalogue.IsProfile(c)?TriangleAreaOpenMeaning.Validate(c,d):RectangleSideOpenMeaning.Validate(c,d);
        return PracticalOpenEditorial.Check(c,d,meaning);
    }
    internal static string Instructions(BasicQuestionContract c)
    {
        string domain=PercentageRatioOpenCatalogue.IsProfile(c)?PercentageRatioOpenCatalogue.WritingInstructions(c.Language)
            :MoneyChangeOpenCatalogue.IsProfile(c)?MoneyChangeOpenCatalogue.WritingInstructions(c.Language)
            :TriangleAreaOpenCatalogue.IsProfile(c)?TriangleAreaOpenCatalogue.WritingInstructions(c.Language):RectangleSideOpenCatalogue.WritingInstructions(c);
        string bindings=string.Join("; ",ReasoningStoryCatalogue.Lesson(c).Quantities.Where(q=>q.Id.StartsWith('v')).Select(q=>"{"+q.Id+"}="+q.Value));
        return domain+"\n"+(c.Language==AppLanguage.Vietnamese
            ?"Chỉ đọc để hiểu, đầu ra vẫn dùng biến: ":"Read for meaning only; keep placeholders in output: ")+bindings;
    }
    internal static string Relation(BasicQuestionContract c)=>PercentageRatioOpenCatalogue.IsProfile(c)?PercentageRatioOpenCatalogue.PromptRelation(c)
        :MoneyChangeOpenCatalogue.IsProfile(c)?MoneyChangeOpenCatalogue.PromptRelation(c):TriangleAreaOpenCatalogue.IsProfile(c)?TriangleAreaOpenCatalogue.PromptRelation(c):RectangleSideOpenCatalogue.PromptRelation(c);
}
