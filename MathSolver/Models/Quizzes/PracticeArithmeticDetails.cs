using System.Numerics;
using MathSolver.Services.Core;

namespace MathSolver.Models;

/// <summary>Frozen C# practice facts, never a stored/AI template contract.</summary>
public sealed record PracticeArithmeticDetails(string ContextId, int Capacity,
    IReadOnlyList<PracticePurchase> Purchases, IReadOnlyList<PracticeSolutionStep> Steps);

public sealed record PracticePurchase(string ItemId, string ItemName, int UnitPrice, int Count,
    int MinimumPrice, int MaximumPrice)
{
    public int Total => checked(UnitPrice * Count);
}

public sealed record PracticeSolutionStep(string Lead, IntegerArithmeticExpression Expression, string Unit)
{
    public BigInteger Answer => new BasicArithmeticEngine().CalculateInteger(Expression).Result;
}
