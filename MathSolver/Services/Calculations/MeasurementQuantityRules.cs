namespace MathSolver.Services;

/// <summary>Quiz contracts bind units to a physical quantity; numeric equality cannot change that quantity.</summary>
public static class MeasurementQuantityRules
{
    public static decimal Convert(decimal value, string fromCategory, string fromUnitId,
        string toCategory, string toUnitId, bool allowVolumeCapacity = false)
    {
        var source = MeasurementEngine.GetCategory(fromCategory).Units.SingleOrDefault(u => u.Id == fromUnitId)
            ?? throw new ArgumentException("Unit does not belong to the supplied quantity.");
        var target = MeasurementEngine.GetCategory(toCategory).Units.SingleOrDefault(u => u.Id == toUnitId)
            ?? throw new ArgumentException("Unit does not belong to the supplied quantity.");
        if (fromCategory == toCategory) return MeasurementEngine.Convert(value, source, target);
        if (!allowVolumeCapacity || !(fromCategory == "volume" && toCategory == "capacity"
            || fromCategory == "capacity" && toCategory == "volume"))
            throw new ArgumentException("The contract does not allow changing the physical quantity.");
        // Volume uses m³ as its base; capacity uses litres. The bridge is explicit.
        decimal baseValue = source.ToBase(value);
        return target.FromBase(fromCategory == "volume" ? baseValue * 1000m : baseValue / 1000m);
    }
}
