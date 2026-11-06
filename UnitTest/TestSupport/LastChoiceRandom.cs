namespace MathSolver.Testing;

/// <summary>Chooses the final candidate to exercise stored-question selection.</summary>
public sealed class LastChoiceRandom : Random
{
    public override int Next(int maxValue) => maxValue - 1;
}
