namespace MathSolver.Services;

/// <summary>Hardware eligibility for large powers, independent of worker count and SIMD.</summary>
public sealed class NttPowerMemoryGuard
{
    public const int SmallMaximumExponent = 10_000_000;
    public const int LargeMaximumExponent = 100_000_000;
    public const long MinimumLargePowerPhysicalBytes = 12L * 1024 * 1024 * 1024;

    public static NttPowerMemoryGuard Shared { get; } = new(PhysicalMemoryInfo.ReadTotalBytes);

    private readonly Func<long?> _readTotalPhysicalBytes;

    public NttPowerMemoryGuard(Func<long?> readTotalPhysicalBytes)
    {
        ArgumentNullException.ThrowIfNull(readTotalPhysicalBytes);
        _readTotalPhysicalBytes = readTotalPhysicalBytes;
    }

    public void EnsureAllowed(int exponent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(exponent);
        if (exponent > LargeMaximumExponent)
            throw new ArgumentOutOfRangeException(nameof(exponent),
                "NTT/CRT chỉ hỗ trợ số mũ tối đa 100.000.000.");

        // No OS query or new restriction on the existing <=10M path.
        if (exponent <= SmallMaximumExponent) return;

        long? bytes = _readTotalPhysicalBytes();
        if (bytes >= MinimumLargePowerPhysicalBytes) return;

        string detected = bytes is > 0
            ? $"RAM vật lý nhận diện: {bytes.Value / (1024d * 1024 * 1024):F2} GB."
            : "Không xác định được dung lượng RAM vật lý của máy.";
        throw new InvalidOperationException(
            $"Nhánh NTT/CRT với số mũ trên 10.000.000 đến 100.000.000 cần ít nhất 12 GB RAM vật lý. {detected} " +
            "Vui lòng nhập số mũ không quá 10.000.000.");
    }
}
