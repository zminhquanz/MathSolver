using System.Numerics;

namespace MathSolver.Models;

public enum ProportionQuizType
{
    Direct,
    Inverse,
    UnitaryGroupCount
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
    public string? SolutionText { get; init; }
    public string NarrativeId { get; init; } = "";
    public UnitaryGroupUnits? GroupCountUnits { get; init; }
    public bool IsDirect => Type == ProportionQuizType.Direct;

    // For these inverse questions C replaces B, rather than A: e.g. a new
    // deadline or a new daily sales rate.
    public bool InverseChangesSecondQuantity => Scenario is
        ProportionScenarioKind.WorkersRequired or
        ProportionScenarioKind.MachinesRequired or
        ProportionScenarioKind.SalesStock or
        ProportionScenarioKind.PackagingCount || AsksForAdditionalPeople;
}

/// <summary>Distinct dimensions for items per group and the unknown group count.</summary>
public sealed record UnitaryGroupUnits(string Items,string Group,string Groups)
{
    public string Rate => Items + "/" + Group;
}
