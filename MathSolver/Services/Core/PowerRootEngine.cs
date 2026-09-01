using MathSolver.Numerics;
using MathSolver.Services;
using System.Numerics;

namespace MathSolver.Services.Core;

public enum PowerRootComputationStrategy
{
    SingleThreadedBigIntegerPower,
    ParallelNttPower,
    BitShift,
    DecimalPowerOfTen
}

public enum PowerRootCalculationMethod
{
    Sqrt,
    Cbrt,
    Pow
}

public sealed record RootCalculationResult(
    Int128 Radicand,
    sbyte Degree,
    bool IsComplex,
    DoubleDouble RealResult,
    DoubleDouble ImaginaryResult,
    PowerRootCalculationMethod Method)
{
    public bool IsFinite =>
        RealResult.IsFinite &&
        ImaginaryResult.IsFinite;
}

/// <summary>
/// Core engine của Lũy thừa–Căn bậc. Các đường tính BigInteger, bit-shift
/// và NTT/CRT được giữ nguyên; lớp không tham chiếu control MAUI.
/// </summary>
public sealed class PowerRootEngine
{
    // Starting with .NET 9, BigInteger is capped at Int32.MaxValue bits.
    // A value 2^k has k + 1 significant bits, so k must be <=
    // Int32.MaxValue - 1 if we want to materialize it as BigInteger.
    public const long MaximumBigIntegerPowerOfTwoExponent =
        (long)int.MaxValue - 1L;

    public static bool CanMaterializePowerOfTwoAsBigInteger(
        long powerOfTwoExponent) =>
        powerOfTwoExponent >= 0L &&
        powerOfTwoExponent <=
        MaximumBigIntegerPowerOfTwoExponent;

    public RootCalculationResult CalculateRoot(
        Int128 radicand,
        sbyte degree)
    {
        if (degree == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(degree),
                "Root degree cannot be zero.");
        }

        if (radicand == 0 &&
            degree < 0)
        {
            throw new DivideByZeroException(
                "A negative-degree root of zero is undefined.");
        }

        int absoluteDegree =
            Math.Abs((int)degree);

        PowerRootCalculationMethod method =
            absoluteDegree switch
            {
                2 => PowerRootCalculationMethod.Sqrt,
                3 => PowerRootCalculationMethod.Cbrt,
                _ => PowerRootCalculationMethod.Pow
            };

        DoubleDouble magnitude =
            DoubleDouble.Abs(
                DoubleDouble.FromInt128(radicand));

        DoubleDouble positiveMagnitudeRoot =
            CalculatePositiveMagnitudeRoot(
                magnitude,
                absoluteDegree,
                method);

        DoubleDouble resultMagnitude =
            degree < 0
                ? DoubleDouble.One /
                  positiveMagnitudeRoot
                : positiveMagnitudeRoot;

        bool isComplex =
            radicand < 0 &&
            (absoluteDegree & 1) == 0;

        if (isComplex)
        {
            DoubleDouble angle =
                DoubleDouble.Pi /
                new DoubleDouble(absoluteDegree);

            if (degree < 0)
            {
                angle = -angle;
            }

            DoubleDouble.SinCos(
                angle,
                out DoubleDouble sine,
                out DoubleDouble cosine);

            return new(
                radicand,
                degree,
                true,
                resultMagnitude * cosine,
                resultMagnitude * sine,
                method);
        }

        DoubleDouble realResult =
            radicand < 0
                ? -resultMagnitude
                : resultMagnitude;

        return new(
            radicand,
            degree,
            false,
            realResult,
            DoubleDouble.Zero,
            method);
    }

    private static DoubleDouble CalculatePositiveMagnitudeRoot(
        DoubleDouble magnitude,
        int degree,
        PowerRootCalculationMethod method)
    {
        if (degree == 1)
        {
            return magnitude;
        }

        return method switch
        {
            PowerRootCalculationMethod.Sqrt =>
                DoubleDouble.Sqrt(magnitude),
            PowerRootCalculationMethod.Cbrt =>
                DoubleDouble.Cbrt(magnitude),
            _ => DoubleDouble.RootUsingPow(
                magnitude,
                degree)
        };
    }

    public PowerRootComputationStrategy SelectPowerStrategy(
        long baseValue,
        int exponent,
        out int decimalExponent)
    {
        decimalExponent = 0;

        if (exponent > 0 &&
            TryGetPowerOfTenExponent(
                baseValue,
                out decimalExponent))
        {
            return PowerRootComputationStrategy.DecimalPowerOfTen;
        }

        if (exponent > 0 &&
            TryGetPowerOfTwoExponent(baseValue, out _))
        {
            return PowerRootComputationStrategy.BitShift;
        }

        return PowerRootComputationStrategy.SingleThreadedBigIntegerPower;
    }

    public Task<BigInteger> ComputeBitShiftPowerAsync(
        long baseValue,
        int exponent,
        long totalBitShift,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!CanMaterializePowerOfTwoAsBigInteger(
                totalBitShift))
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalBitShift),
                "The exact power-of-two result exceeds the .NET BigInteger maximum bit length. Use the virtual bit-shift result path instead.");
        }

        return Task.Factory.StartNew(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                // totalBitShift is now guaranteed to fit the runtime's
                // BigInteger bit-length ceiling, and the shift operand itself
                // also fits Int32. One shift avoids repeated immutable
                // BigInteger copies.
                BigInteger result =
                    BigInteger.One <<
                    checked((int)totalBitShift);

                if (baseValue < 0 && (exponent & 1) != 0)
                {
                    result = BigInteger.Negate(result);
                }

                cancellationToken.ThrowIfCancellationRequested();
                return result;
            },
            cancellationToken,
            TaskCreationOptions.LongRunning |
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);
    }

    public Task<BigInteger> ComputeSingleThreadedPowerAsync(
        long baseValue,
        int exponent,
        Action<int, int> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Factory.StartNew(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (exponent == 0)
                {
                    return BigInteger.One;
                }

                int totalOperations =
                    CountPowerMultiplications(exponent);

                // Experimental single-thread AVX2 path. Keep the exact same
                // exponentiation-by-squaring operation count/progress contract,
                // but use a bounded base-2^16 SIMD schoolbook window before
                // handing large operands back to System.Numerics.BigInteger.
                // The shared Hardware acceleration switch is the only gate.
                if (CalculationAccelerationManager
                        .UseSingleThreadBigIntegerAvx2)
                {
                    return Avx2BigIntegerPower.Pow(
                        baseValue,
                        exponent,
                        progress,
                        totalOperations,
                        cancellationToken);
                }

                BigInteger factor = new(baseValue);
                BigInteger result = BigInteger.One;
                bool resultInitialized = false;
                int remainingExponent = exponent;
                int completedOperations = 0;

                while (remainingExponent > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if ((remainingExponent & 1) != 0)
                    {
                        if (!resultInitialized)
                        {
                            result = factor;
                            resultInitialized = true;
                        }
                        else
                        {
                            result *= factor;
                            progress(++completedOperations, totalOperations);
                        }
                    }

                    remainingExponent >>= 1;

                    if (remainingExponent > 0)
                    {
                        factor *= factor;
                        progress(++completedOperations, totalOperations);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                return result;
            },
            cancellationToken,
            TaskCreationOptions.LongRunning |
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);
    }

    internal Task<ParallelPowerResult> ComputeParallelPowerAsync(
        long baseValue,
        int exponent,
        int workerCount,
        Action<int, int> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        cancellationToken.ThrowIfCancellationRequested();

        ulong magnitude = (ulong)Math.Abs(baseValue);

        return Task.Factory.StartNew(
            () => ParallelBigUnsigned.Pow(
                magnitude,
                exponent,
                workerCount,
                progress,
                cancellationToken),
            cancellationToken,
            TaskCreationOptions.LongRunning |
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);
    }


    internal Task<ParallelPowerResult> ComputeMemoryBoundedParallelPowerAsync(
        long baseValue,
        int exponent,
        int workerCount,
        Action<int, int> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        cancellationToken.ThrowIfCancellationRequested();

        ulong magnitude =
            (ulong)Math.Abs(baseValue);

        return Task.Factory.StartNew(
            () => ParallelBigUnsigned.PowMemoryBounded(
                magnitude,
                exponent,
                workerCount,
                progress,
                cancellationToken),
            cancellationToken,
            TaskCreationOptions.LongRunning |
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);
    }

    public int CountPowerMultiplications(int exponent)
    {
        int operationCount = 0;
        bool resultInitialized = false;
        int remainingExponent = exponent;

        while (remainingExponent > 0)
        {
            if ((remainingExponent & 1) != 0)
            {
                if (resultInitialized)
                {
                    operationCount++;
                }
                else
                {
                    resultInitialized = true;
                }
            }

            remainingExponent >>= 1;

            if (remainingExponent > 0)
            {
                operationCount++;
            }
        }

        return operationCount;
    }

    public bool TryGetPowerOfTenExponent(
        long baseValue,
        out int decimalExponent)
    {
        decimalExponent = 0;
        long magnitude = Math.Abs(baseValue);

        if (magnitude < 10)
        {
            return false;
        }

        while (magnitude % 10 == 0)
        {
            magnitude /= 10;
            decimalExponent++;
        }

        return magnitude == 1;
    }

    public bool TryGetPowerOfTwoExponent(
        long baseValue,
        out int powerOfTwoExponent)
    {
        powerOfTwoExponent = 0;

        ulong magnitude = baseValue < 0
            ? unchecked((ulong)(-(baseValue + 1))) + 1UL
            : (ulong)baseValue;

        if (magnitude == 0UL ||
            (magnitude & (magnitude - 1UL)) != 0UL)
        {
            return false;
        }

        while ((magnitude & 1UL) == 0UL)
        {
            magnitude >>= 1;
            powerOfTwoExponent++;
        }

        return true;
    }

    public bool TryGetExactIntegerRoot(
        BigInteger magnitude,
        int degree,
        out BigInteger root)
    {
        root = BigInteger.Zero;

        if (magnitude.Sign < 0 || degree < 1)
        {
            return false;
        }

        if (magnitude.IsZero || magnitude.IsOne)
        {
            root = magnitude;
            return true;
        }

        long bitLength = magnitude.GetBitLength();

        if (degree >= bitLength)
        {
            return false;
        }

        int upperRootBitCount =
            checked((int)((bitLength + degree - 1L) / degree));

        BigInteger lower = new(2);
        BigInteger upper = BigInteger.One << upperRootBitCount;

        while (lower <= upper)
        {
            BigInteger candidate = (lower + upper) >> 1;
            int comparison =
                BigInteger.Pow(candidate, degree)
                    .CompareTo(magnitude);

            if (comparison == 0)
            {
                root = candidate;
                return true;
            }

            if (comparison < 0)
            {
                lower = candidate + BigInteger.One;
            }
            else
            {
                upper = candidate - BigInteger.One;
            }
        }

        return false;
    }
}
