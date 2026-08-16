using System.Numerics;

namespace MathSolver.Models;

public enum ProportionQuizType
{
    Direct,
    Inverse
}

public enum ProportionScenarioKind
{
    Clothing,
    StudentsPlanting,
    Shopping,
    VehiclesCargo,
    VehiclesFuel,
    ContainersLiquid,
    PaintArea,
    ProductionItems,
    RiceBagsWeight,
    FoodWeightGrams,
    EggWeightGrams,
    DistanceTime,
    WorkersDays,
    MachinesHours,
    WorkersJob,
    FoodPeopleDays,
    FoodAdditionalPeople,
    SalesStock
}

/// <summary>
/// Dữ kiện C# sở hữu cho một bài toán tỉ lệ. AI chỉ được phép diễn đạt lại
/// các dữ kiện này thành câu văn, không được tự thay số hay đổi quan hệ.
/// </summary>
public sealed record ProportionQuizContract(
    ProportionQuizType Type,
    ProportionScenarioKind Scenario,
    int A,
    int B,
    int C,
    BigInteger CorrectAnswer,
    string AnswerUnit,
    string SubjectName,
    string ProblemText,
    bool AsksForAdditionalPeople = false)
{
    public bool IsDirect => Type == ProportionQuizType.Direct;
}
