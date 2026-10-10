using MathSolver.Models;
using MathSolver.Services;

namespace MathSolver.Services.QuestionBank;

/// <summary>The same supported families and variants as AI supplementation, without loading a model.</summary>
public static class QuestionAuthoringChoices
{
    public static string FamilyKey(BankQuestionFamily family) => family switch {
        BankQuestionFamily.Arithmetic => "AiBank.BasicArithmetic", BankQuestionFamily.FindX => "FindXBank.Title", BankQuestionFamily.Fraction => "FractionBank.Title",
        BankQuestionFamily.TwoNumbers => "Quiz.ProblemTwoNumbers", BankQuestionFamily.Average => "Quiz.ProblemAverage", BankQuestionFamily.Percentage => "Quiz.ProblemPercentage",
        BankQuestionFamily.MultiStep => "Quiz.ProblemMultiStep", BankQuestionFamily.Motion => "Quiz.ProblemMotion", BankQuestionFamily.Proportion => "Quiz.ProblemProportion",
        BankQuestionFamily.Decimal => "Quiz.ProblemDecimal", BankQuestionFamily.Measurement => "Quiz.ProblemMeasurement", BankQuestionFamily.Remainder => "Quiz.ProblemRemainder",
        BankQuestionFamily.Time => "Quiz.ProblemTime", BankQuestionFamily.FractionQuantity => "Quiz.ProblemFractionSkills", BankQuestionFamily.Geometry => "Quiz.ProblemGeometry",
        BankQuestionFamily.Data => "Quiz.ProblemData", BankQuestionFamily.Probability => "Quiz.ProblemProbability", _ => throw new ArgumentOutOfRangeException(nameof(family)) };

    public static string FamilyLabel(BankQuestionFamily family, AppLanguage language)
    {
        string[] vi = ["Toán đố cơ bản", "Tìm X", "Phân số", "Tìm hai số", "Trung bình cộng", "Phần trăm", "Bài nhiều bước",
            "Chuyển động", "Tỉ lệ", "Số thập phân", "Đo lường", "Chia có dư", "Thời gian", "Kĩ năng phân số", "Hình học ứng dụng", "Bảng và biểu đồ", "Khả năng xảy ra"];
        string[] en = ["Basic word problems", "Find X", "Fractions", "Find two quantities", "Averages", "Percentages", "Multi-step problems",
            "Motion", "Proportion", "Decimals", "Measurement", "Division with remainder", "Time", "Fraction skills", "Applied geometry", "Tables and charts", "Probability"];
        return (language == AppLanguage.Vietnamese ? vi : en)[(int)family];
    }

    public static string VariantKey(BankQuestionFamily family, int variant) => family switch {
        BankQuestionFamily.TwoNumbers or BankQuestionFamily.MultiStep or BankQuestionFamily.Decimal or BankQuestionFamily.Measurement
            or BankQuestionFamily.Remainder or BankQuestionFamily.Time or BankQuestionFamily.FractionQuantity or BankQuestionFamily.Data or BankQuestionFamily.Probability
            => "Quiz.Elementary." + (ElementaryQuizType)variant,
        BankQuestionFamily.Average => "Quiz.Average" + (AverageQuizType)variant,
        BankQuestionFamily.Percentage => (PercentageQuizType)variant switch {
            PercentageQuizType.FindPercentageRatio => "Quiz.PercentageRatio",
            PercentageQuizType.FindPercentageValue => "Quiz.PercentageValue",
            PercentageQuizType.FindWholeFromPercentageValue => "Quiz.PercentageWhole",
            _ => throw new ArgumentOutOfRangeException(nameof(variant)) },
        BankQuestionFamily.Motion => "Quiz.Motion" + (MotionQuizType)variant,
        BankQuestionFamily.Proportion => "Quiz.Proportion" + (ProportionQuizType)variant,
        BankQuestionFamily.Geometry => "Quiz.Geometry" + ReasoningStoryCatalogue.GeometryProfile(variant).Shape,
        _ => throw new ArgumentOutOfRangeException(nameof(family)) };

    public static BasicQuestionContract Create(BankQuestionFamily family, int variant, CurriculumTier tier, AppLanguage language,
        QuestionKnowledgeGroup group = QuestionKnowledgeGroup.Objects, FindXUnknownRole role = FindXUnknownRole.None, Random? random = null, string? sceneId = null)
        => ReasoningStoryCatalogue.Supports(family) ? ReasoningStoryCatalogue.Create(family, variant, tier, language, random)
        : family == BankQuestionFamily.FindX ? FindXQuestionCatalogue.Create(new(group), (ArithmeticOperation)variant, tier, language, random, role, sceneId)
        : family == BankQuestionFamily.Fraction ? FractionQuestionCatalogue.Create(new(group), (ArithmeticOperation)variant, tier, language, random, sceneId)
        : AppliedQuestionCatalogue.Create(new(group), (ArithmeticOperation)variant, tier, language, random, sceneId);

    public static string[] Scenes(BankQuestionFamily family, ArithmeticOperation operation, CurriculumTier tier, QuestionKnowledgeGroup group,
        FindXUnknownRole role = FindXUnknownRole.None) => family switch {
        BankQuestionFamily.Arithmetic => AppliedQuestionCatalogue.Available(new(group), operation, tier).Select(s => s.Id).ToArray(),
        BankQuestionFamily.FindX => FindXQuestionCatalogue.Available(new(group), operation, tier, role).Select(s => s.Id).ToArray(),
        BankQuestionFamily.Fraction => FractionQuestionCatalogue.Available(new(group), operation, tier).Select(s => s.Id).ToArray(),
        _ => [] };
}
