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
    SalesStock,
    WorkersRequired,
    MachinesRequired,
    TapsTime,
    TravelSpeedTime,
    TransportTrips,
    PackagingCount
}

/// <summary>C# math puzzle data and rules.</summary>
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

    // For these inverse questions C replaces B, rather than A: e.g. a new
    // deadline or a new daily sales rate.
    public bool InverseChangesSecondQuantity => Scenario is
        ProportionScenarioKind.WorkersRequired or
        ProportionScenarioKind.MachinesRequired or
        ProportionScenarioKind.SalesStock or
        ProportionScenarioKind.PackagingCount || AsksForAdditionalPeople;
}
