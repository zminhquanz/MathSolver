using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>Direct applied relations; C# owns numbers and each semantic proof owns its roles.</summary>
internal static class AppliedOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c) => MapScaleOpenCatalogue.IsProfile(c)
        || DiscountOpenCatalogue.IsProfile(c) || QuadrilateralAreaOpenCatalogue.IsProfile(c);

    internal static IEnumerable<string> Profiles(BankQuestionFamily family,int variant,AppLanguage language) =>
        family==BankQuestionFamily.Measurement&&variant==MapScaleOpenPilot.Variant ? MapScaleOpenPilot.Profiles(language)
        : family==BankQuestionFamily.MultiStep&&variant==DiscountOpenPilot.Variant ? DiscountOpenPilot.Profiles(language)
        : QuadrilateralAreaOpenCatalogue.Profiles(family,variant,language);

    internal static NarrativeLesson Build(BasicQuestionContract c) => MapScaleOpenCatalogue.IsProfile(c)
        ? MapScaleOpenCatalogue.Build(c) : DiscountOpenCatalogue.IsProfile(c)
        ? DiscountOpenCatalogue.Build(c) : QuadrilateralAreaOpenCatalogue.Build(c);

    internal static ArithmeticQuizQuestion Generate(BasicQuestionContract c,ArithmeticQuizMode mode) => MapScaleOpenCatalogue.IsProfile(c)
        ? MapScaleOpenCatalogue.Generate(c,mode) : DiscountOpenCatalogue.IsProfile(c)
        ? DiscountOpenCatalogue.Generate(c,mode) : QuadrilateralAreaOpenCatalogue.Generate(c,mode);

    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)
    {
        var meaning=MapScaleOpenCatalogue.IsProfile(c)?MapScaleOpenMeaning.Validate(c,d)
            :DiscountOpenCatalogue.IsProfile(c)?DiscountOpenMeaning.Validate(c,d):QuadrilateralAreaOpenMeaning.Validate(c,d);
        return PracticalOpenEditorial.Check(c,d,meaning);
    }

    internal static string Instructions(BasicQuestionContract c)
    {
        string domain=MapScaleOpenCatalogue.IsProfile(c)?MapScaleOpenCatalogue.WritingInstructions(c)
            :DiscountOpenCatalogue.IsProfile(c)?DiscountOpenCatalogue.WritingInstructions(c):QuadrilateralAreaOpenCatalogue.WritingInstructions(c);
        string bindings=string.Join("; ",ReasoningStoryCatalogue.Lesson(c).Quantities.Where(q=>q.Id.StartsWith('v')).Select(q=>"{"+q.Id+"}="+q.Value));
        return domain+"\n"+(c.Language==AppLanguage.Vietnamese?"Chỉ đọc để hiểu, đầu ra vẫn dùng biến: ":"Read for meaning only; keep placeholders in output: ")+bindings;
    }

    internal static string Relation(BasicQuestionContract c) => MapScaleOpenCatalogue.IsProfile(c)
        ? MapScaleOpenCatalogue.PromptRelation(c) : DiscountOpenCatalogue.IsProfile(c)
        ? DiscountOpenCatalogue.PromptRelation(c) : QuadrilateralAreaOpenCatalogue.PromptRelation(c);
}
