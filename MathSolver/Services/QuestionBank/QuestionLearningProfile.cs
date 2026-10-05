using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

// Persisted numeric identities are stable; Mass/Length/Transport are legacy subgroups.
public enum QuestionKnowledgeGroup { Objects = 0, Money = 1, Mass = 2, Length = 3, Transport = 4, Motion = 5,
    Time = 6, Measurement = 7, Geometry = 8, Packaging = 9, Production = 10, Data = 11 }

/// <summary>The knowledge group selects dimensions; stars select numeric and reasoning difficulty.</summary>
public sealed record QuestionLearningProfile(QuestionKnowledgeGroup Group)
{
    public bool IsValid => Enum.IsDefined(Group);
    public bool Allows(ArithmeticOperation operation) => IsValid && Enum.IsDefined(operation);
    public static QuestionKnowledgeGroup[] Groups() => [QuestionKnowledgeGroup.Objects, QuestionKnowledgeGroup.Money,
        QuestionKnowledgeGroup.Time, QuestionKnowledgeGroup.Measurement, QuestionKnowledgeGroup.Geometry,
        QuestionKnowledgeGroup.Packaging, QuestionKnowledgeGroup.Production, QuestionKnowledgeGroup.Data, QuestionKnowledgeGroup.Motion];
    public bool Includes(QuestionKnowledgeGroup group) => group == Group || Group == QuestionKnowledgeGroup.Measurement
        && group is QuestionKnowledgeGroup.Mass or QuestionKnowledgeGroup.Length or QuestionKnowledgeGroup.Transport;
    public int ArithmeticCeiling(CurriculumTier tier) => tier switch
    { CurriculumTier.OneStar => 9, CurriculumTier.TwoStars => 99, CurriculumTier.ThreeStars => 999,
        CurriculumTier.FourStars => 9999, CurriculumTier.FiveStars => 99999,
        _ => throw new ArgumentOutOfRangeException(nameof(tier)) };
    public int MaximumFactor(CurriculumTier tier) => (int)tier <= 1 ? 5 : (int)tier <= 3 ? 9 : (int)tier == 4 ? 20 : 99;
}
