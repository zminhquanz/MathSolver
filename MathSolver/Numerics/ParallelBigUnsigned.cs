using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
#if ANDROID
using System.Runtime.Intrinsics.Arm;
#endif
using System.Runtime.Intrinsics.X86;

namespace MathSolver.Numerics;

/// <summary>
/// Unsigned arbitrary-precision integer used by the parallel power engine.
/// Digits are stored in base 10,000 so TXT export never needs a giant
/// binary-to-decimal division tree. Large products use two exact NTTs and CRT;
/// the butterfly work inside every transform is shared by the configured
/// logical-processor worker budget.
/// </summary>
internal sealed class ParallelBigUnsigned
{
    private const uint LimbBase = 10_000;
    private const int DigitsPerLimb = 4;
    private const int SchoolbookWorkLimit = 250_000;
    private const int MaximumTransformLength = 1 << 26;

    // The production <=10M engine stays exactly on its existing path. Larger
    // exponents are orchestrated by PowMemoryBounded(): compute <=10M chunks
    // with the proven engine, then merge them with bounded segmented NTTs.
    private const int LegacyMaximumExponent = 10_000_000;
    private const int MaximumMemoryBoundedExponent = 100_000_000;

    // Two full segments produce at most 2^26 - 1 convolution coefficients, so
    // every pair remains compatible with the existing two exact 32-bit NTT
    // primes. Persistent NTT storage therefore stays uint32; only modular
    // products/reconstruction use temporary ulong arithmetic.
    private const int SegmentedNttLimbLength =
        MaximumTransformLength >> 1;

    // Binary BigInteger results (notably the |a| = 2^k bit-shift shortcut)
    // are imported into the same base-10,000 representation used by NTT/CRT
    // before TXT export.  A 1,024-limb leaf is exactly 4,096 decimal digits,
    // matching the export block size and keeping each leaf conversion small.
    private const int BigIntegerImportLeafLimbCount = 1 << 10; // 1024 limbs = 4096 digits
    private const int BigIntegerImportParallelBranchLimbThreshold =
        BigIntegerImportLeafLimbCount * 16;

    // Persistent twiddle storage is adaptive.  Low-thread/smaller systems keep
    // the proven 1,048,576-value cap.  CPUs with at least 8 logical processors
    // cache one additional global NTT stage (2,097,152 values).  That stage is
    // reused across many large products in exponentiation, trading a moderate
    // amount of pooled RAM for fewer runtime twiddle modulo updates.
    private const int DefaultMaximumCachedTwiddleCount = 1 << 20;
    private const int LargePowerMaximumCachedTwiddleCount = 1 << 21;

    // L1 fused-block size is selected from the logical-processor count.  This
    // is deliberately a small heuristic rather than CPU-model detection:
    // modern high-thread CPUs such as the 24-thread HX 370 keep the proven
    // 4096-value (16 KiB) block, while 8-19 thread SMT CPUs such as a 12-thread
    // i7-8700 use 2048 values (8 KiB) to leave more shared L1D room per sibling
    // thread.  A second L2 tile level below keeps several fused blocks resident
    // together so additional DIF/DIT stages avoid full-array memory sweeps.
    // Every choice is a power of two so stage boundaries remain exact.
    private const int SmallSmtFusedNttBlockLength = 1 << 11; // 2048 = 8 KiB
    private const int DefaultFusedNttBlockLength = 1 << 12;  // 4096 = 16 KiB
    private const int LowThreadFusedNttBlockLength = 1 << 13; // 8192 = 32 KiB

    // A second cache-blocking level keeps several L1-sized fused blocks inside
    // one L2-resident tile.  The tile is deliberately sized per logical-thread
    // class so two SMT siblings do not consume the whole private L2 with values
    // plus the largest twiddle stage.
    private const int SmallSmtL2NttTileLength = 1 << 14; // 16384 = 64 KiB values
    private const int MidThreadL2NttTileLength = 1 << 15; // 32768 = 128 KiB values
    private const int HighThreadL2NttTileLength = 1 << 16; // 65536 = 256 KiB values

    // Cache-resident DIF/DIT kernels use the same two-way scalar unroll as the
    // successful global-stage path.  This does not alter modular arithmetic;
    // it only exposes two independent butterflies to the out-of-order core and
    // increments twiddle indices directly while L1/L2/L3 locality is already
    // guaranteed by the hierarchical traversal.

    // A third, last-level-cache tile removes several more full-array sweeps
    // before work reaches the L2 tile.  Keep the tile conservative enough that
    // all active SMT workers can retain useful L3 residency at the same time.
    private const int SmallSmtL3NttTileLength = 1 << 17; // 131072 = 512 KiB values
    private const int MidThreadL3NttTileLength = 1 << 18; // 262144 = 1 MiB values
    private const int HighThreadL3NttTileLength = 1 << 18; // 262144 = 1 MiB values
    private const int LowThreadL3NttTileLength = 1 << 19; // 524288 = 2 MiB values

    // CRT is reconstructed in bounded blocks instead of materializing one
    // ulong coefficient for the complete convolution. 1,048,576 coefficients
    // occupy 8 MiB, large enough to keep every worker busy. v32 normally maps
    // that scratch onto the dead tail of inverse P2; an 8 MiB team-local array
    // is retained only as the fallback when the tail is too short.
    private const int CrtCarryStreamingBlockLength = 1 << 20;

    // Both primes support transforms through 2^26. Their product is large
    // enough to recover every base-10,000 convolution coefficient in the
    // legacy <=10M engine and in each 2^25-limb pair of large segmented NTT.
    private const uint FirstModulus = 2_013_265_921;
    private const uint SecondModulus = 469_762_049;
    private const uint FirstPrimitiveRoot = 31;
    private const uint SecondPrimitiveRoot = 3;

    private static readonly ulong FirstModulusInverseInSecond =
        ModInverse(
            FirstModulus % SecondModulus,
            SecondModulus);

    private readonly uint[] _limbs;
    private readonly int _limbCount;

    private ParallelBigUnsigned(
        uint[] limbs,
        bool takeOwnership,
        int logicalLength = -1)
    {
        ArgumentNullException.ThrowIfNull(
            limbs);

        uint[] ownedLimbs =
            takeOwnership
                ? limbs
                : (uint[])limbs.Clone();

        if (logicalLength < 0)
        {
            _limbs =
                Trim(ownedLimbs);

            _limbCount =
                _limbs.Length;

            return;
        }

        if (logicalLength <= 0 ||
            logicalLength > ownedLimbs.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(logicalLength));
        }

        int trimmedLength =
            logicalLength;

        while (trimmedLength > 1 &&
               ownedLimbs[trimmedLength - 1] == 0)
        {
            trimmedLength--;
        }

        // The explicit logical-length path intentionally keeps spare backing
        // capacity. CRT/carry uses one extra uint slot for the possible final
        // carry, avoiding an enormous Array.Resize copy for full-width NTT
        // products while every arithmetic/formatting path observes only the
        // normalized logical limb count.
        _limbs =
            ownedLimbs;

        _limbCount =
            trimmedLength;
    }

    public int DigitCount
    {
        get
        {
            uint highest =
                _limbs[_limbCount - 1];

            int highestDigits =
                highest >= 1_000
                    ? 4
                    : highest >= 100
                        ? 3
                        : highest >= 10
                            ? 2
                            : 1;

            return checked(
                (_limbCount - 1) *
                DigitsPerLimb +
                highestDigits);
        }
    }

    public long StorageBytes =>
        (long)_limbCount *
        sizeof(uint);

    public bool IsOne =>
        _limbCount == 1 &&
        _limbs[0] == 1;

    public static ParallelBigUnsigned One { get; } =
        new(
            [1],
            takeOwnership: true);

    public static ParallelBigUnsigned FromUInt64(
        ulong value)
    {
        if (value == 0)
        {
            return new ParallelBigUnsigned(
                [0],
                takeOwnership: true);
        }

        var limbs =
            new List<uint>(5);

        while (value > 0)
        {
            limbs.Add(
                (uint)(value % LimbBase));

            value /=
                LimbBase;
        }

        return new ParallelBigUnsigned(
            limbs.ToArray(),
            takeOwnership: true);
    }

    /// <summary>
    /// Converts a non-negative binary BigInteger into this type's base-10,000
    /// limbs without allocating one giant decimal string.  The value is split
    /// at powers of 10,000 on 1,024-limb boundaries; independent branches may
    /// run concurrently, and each leaf formats at most 4,096 decimal digits
    /// into a pooled buffer before parsing them directly into destination limbs.
    /// </summary>
    public static ParallelBigUnsigned FromBigInteger(
        BigInteger value,
        int estimatedDecimalDigitCount,
        int workerCount,
        Action<int, int>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            estimatedDecimalDigitCount);

        cancellationToken.ThrowIfCancellationRequested();

        if (value.Sign < 0)
        {
            value = BigInteger.Abs(
                value);
        }

        if (value.IsZero)
        {
            progress?.Invoke(
                1,
                1);

            return new ParallelBigUnsigned(
                [0],
                takeOwnership: true);
        }

        // One guard limb makes the importer safe even if the logarithmic digit
        // estimate lands one digit below the exact result near a power of ten.
        // Trim() removes the unused high limb after conversion.
        int limbCapacity =
            checked(
                (estimatedDecimalDigitCount +
                 DigitsPerLimb - 1) /
                DigitsPerLimb +
                1);

        var limbs =
            new uint[limbCapacity];

        int totalLeaves =
            GetBigIntegerImportLeafCountFromLimbCapacity(
                limbCapacity);

        int completedLeaves = 0;

        void ReportLeaves(
            int leafCount)
        {
            int completed =
                Interlocked.Add(
                    ref completedLeaves,
                    leafCount);

            progress?.Invoke(
                Math.Min(
                    completed,
                    totalLeaves),
                totalLeaves);
        }

        workerCount =
            Math.Clamp(
                workerCount,
                1,
                Math.Max(
                    1,
                    Environment.ProcessorCount));

        var powersOfBase =
            new ConcurrentDictionary<int, Lazy<BigInteger>>();

        FillBigIntegerLimbsParallel(
            value,
            limbs,
            0,
            limbCapacity,
            workerCount,
            powersOfBase,
            ReportLeaves,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        return new ParallelBigUnsigned(
            limbs,
            takeOwnership: true);
    }

    public static int GetBigIntegerImportLeafCount(
        int estimatedDecimalDigitCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            estimatedDecimalDigitCount);

        int limbCapacity =
            checked(
                (estimatedDecimalDigitCount +
                 DigitsPerLimb - 1) /
                DigitsPerLimb +
                1);

        return GetBigIntegerImportLeafCountFromLimbCapacity(
            limbCapacity);
    }

    private static int GetBigIntegerImportLeafCountFromLimbCapacity(
        int limbCapacity)
    {
        return checked(
            (limbCapacity +
             BigIntegerImportLeafLimbCount - 1) /
            BigIntegerImportLeafLimbCount);
    }

    private static void FillBigIntegerLimbsParallel(
        BigInteger value,
        uint[] destination,
        int destinationStart,
        int limbCount,
        int workerBudget,
        ConcurrentDictionary<int, Lazy<BigInteger>> powersOfBase,
        Action<int> reportLeaves,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (value.IsZero)
        {
            reportLeaves(
                GetBigIntegerImportLeafCountFromLimbCapacity(
                    limbCount));

            return;
        }

        if (limbCount <=
            BigIntegerImportLeafLimbCount)
        {
            FillBigIntegerLimbLeaf(
                value,
                destination,
                destinationStart,
                limbCount,
                cancellationToken);

            reportLeaves(
                1);

            return;
        }

        // Split on an exact 1,024-limb boundary.  This keeps every recursive
        // range aligned to the same 4,096-digit leaf size used by TXT output,
        // so progress accounting and cache locality remain predictable.
        int approximateHalf =
            limbCount / 2;

        int lowLimbCount =
            Math.Max(
                BigIntegerImportLeafLimbCount,
                approximateHalf /
                BigIntegerImportLeafLimbCount *
                BigIntegerImportLeafLimbCount);

        if (lowLimbCount >=
            limbCount)
        {
            lowLimbCount =
                limbCount -
                BigIntegerImportLeafLimbCount;
        }

        int highLimbCount =
            limbCount -
            lowLimbCount;

        BigInteger divisor =
            GetCachedPowerOfLimbBase(
                powersOfBase,
                lowLimbCount);

        BigInteger highValue =
            BigInteger.DivRem(
                value,
                divisor,
                out BigInteger lowValue);

        cancellationToken.ThrowIfCancellationRequested();

        int lowWorkerBudget =
            workerBudget / 2;

        int highWorkerBudget =
            workerBudget -
            lowWorkerBudget;

        bool splitAcrossWorkers =
            lowWorkerBudget > 0 &&
            limbCount >=
                BigIntegerImportParallelBranchLimbThreshold;

        if (!splitAcrossWorkers)
        {
            FillBigIntegerLimbsParallel(
                lowValue,
                destination,
                destinationStart,
                lowLimbCount,
                1,
                powersOfBase,
                reportLeaves,
                cancellationToken);

            FillBigIntegerLimbsParallel(
                highValue,
                destination,
                checked(
                    destinationStart +
                    lowLimbCount),
                highLimbCount,
                1,
                powersOfBase,
                reportLeaves,
                cancellationToken);

            return;
        }

        Task lowTask =
            Task.Factory.StartNew(
                () => FillBigIntegerLimbsParallel(
                    lowValue,
                    destination,
                    destinationStart,
                    lowLimbCount,
                    lowWorkerBudget,
                    powersOfBase,
                    reportLeaves,
                    cancellationToken),
                cancellationToken,
                TaskCreationOptions.LongRunning |
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);

        try
        {
            FillBigIntegerLimbsParallel(
                highValue,
                destination,
                checked(
                    destinationStart +
                    lowLimbCount),
                highLimbCount,
                highWorkerBudget,
                powersOfBase,
                reportLeaves,
                cancellationToken);
        }
        catch
        {
            try
            {
                lowTask.GetAwaiter()
                    .GetResult();
            }
            catch
            {
                // Preserve the exception/cancellation from the current branch.
            }

            throw;
        }

        lowTask.GetAwaiter()
            .GetResult();
    }

    private static BigInteger GetCachedPowerOfLimbBase(
        ConcurrentDictionary<int, Lazy<BigInteger>> powersOfBase,
        int exponent)
    {
        Lazy<BigInteger> lazyPower =
            powersOfBase.GetOrAdd(
                exponent,
                static value =>
                    new Lazy<BigInteger>(
                        () => BigInteger.Pow(
                            new BigInteger(LimbBase),
                            value),
                        LazyThreadSafetyMode.ExecutionAndPublication));

        return lazyPower.Value;
    }

    private static void FillBigIntegerLimbLeaf(
        BigInteger value,
        uint[] destination,
        int destinationStart,
        int limbCount,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int characterCapacity =
            checked(
                limbCount *
                DigitsPerLimb);

        char[] rentedCharacters =
            ArrayPool<char>.Shared.Rent(
                characterCapacity);

        try
        {
            Span<char> characters =
                rentedCharacters.AsSpan(
                    0,
                    characterCapacity);

            if (!value.TryFormat(
                    characters,
                    out int charactersWritten,
                    default,
                    CultureInfo.InvariantCulture))
            {
                throw new InvalidOperationException(
                    "BigInteger decimal leaf exceeded its assigned base-10,000 range.");
            }

            int sourceEnd =
                charactersWritten;

            int destinationIndex =
                destinationStart;

            int destinationEnd =
                checked(
                    destinationStart +
                    limbCount);

            while (sourceEnd > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (destinationIndex >=
                    destinationEnd)
                {
                    throw new InvalidOperationException(
                        "BigInteger decimal leaf produced more base-10,000 limbs than expected.");
                }

                int sourceStart =
                    Math.Max(
                        0,
                        sourceEnd -
                        DigitsPerLimb);

                uint limb = 0;

                for (int sourceIndex = sourceStart;
                     sourceIndex < sourceEnd;
                     sourceIndex++)
                {
                    limb =
                        checked(
                            limb * 10u +
                            (uint)(characters[sourceIndex] - '0'));
                }

                destination[destinationIndex++] =
                    limb;

                sourceEnd =
                    sourceStart;
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(
                rentedCharacters);
        }
    }

    public static ParallelPowerResult Pow(
        ulong baseValue,
        int exponent,
        int workerCount,
        Action<int, int>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(
            exponent);

        workerCount =
            Math.Max(
                1,
                workerCount);

        if (exponent == 0)
        {
            return new ParallelPowerResult(
                One,
                new PowerDiagnosticsCollector().CreateSnapshot(
                    workerCount));
        }

        // One workspace pool lives for the complete exponentiation.  In the
        // split-power path both branches and the final combine share it, so
        // the largest temporary NTT arrays can be recycled across modulus
        // passes and branch/final-combine boundaries instead of repeatedly
        // allocating 100s of MiB on the LOH and waiting for GC.
        using var nttBufferPool =
            new NttBufferPool();

        using var nttTwiddleBufferPool =
            new NttTwiddleBufferPool();

        // v31: both PowSplit branches execute the same NTT moduli and therefore
        // need identical immutable twiddle tables. Keep exactly one Pow-scoped
        // plan per modulus and let every worker team share it instead of each
        // branch owning another 64 MiB-class pair of forward/inverse tables.
        using var sharedNttTwiddlePlans =
            new SharedNttTwiddlePlans(
                nttTwiddleBufferPool);

        if (TryCreateExponentSplit(
                exponent,
                workerCount,
                out int firstExponent,
                out int secondExponent))
        {
            return PowSplit(
                baseValue,
                exponent,
                firstExponent,
                secondExponent,
                workerCount,
                nttBufferPool,
                sharedNttTwiddlePlans,
                progress,
                cancellationToken);
        }

        var diagnostics =
            new PowerDiagnosticsCollector();

        ParallelBigUnsigned magnitude;
        int actualWorkerCount;

        using (var workers =
               new FixedWorkerTeam(
                   workerCount,
                   nttBufferPool,
                   sharedNttTwiddlePlans))
        {
            actualWorkerCount =
                workers.WorkerCount;

            magnitude =
                PowWithTeam(
                    baseValue,
                    exponent,
                    workers,
                    diagnostics,
                    progress,
                    cancellationToken);
        }

        // All transform leases and workers are dead here. Capture pool
        // telemetry, then release both retained NTT workspaces and the shared
        // Pow-scoped twiddle plans before the final result leaves Pow().
        diagnostics.ConfigureNttBufferPool(
            nttBufferPool.CreateStatisticsSnapshot());

        nttBufferPool.ReleaseCachedBuffers();

        // No worker is using twiddles now. Return the four shared arrays to the
        // small Pow-scoped pool and immediately drop those cached references so
        // they cannot remain live while the final magnitude is handed to UI.
        sharedNttTwiddlePlans.ReleasePlans();
        nttTwiddleBufferPool.ReleaseCachedBuffers();

        return new ParallelPowerResult(
            magnitude,
            diagnostics.CreateSnapshot(
                actualWorkerCount));
    }

    /// <summary>
    /// Memory-bounded exact power path for exponents above the production
    /// 10,000,000 limit and up to 100,000,000. It deliberately does not modify
    /// Pow(): the first <=10M chunk (and optional remainder) is calculated by
    /// the proven legacy engine, then a small Int32 quotient is merged through
    /// sequential in-place/segmented NTT multiplications. This prevents the
    /// large PowSplit branches from keeping multiple multi-gigabyte magnitudes
    /// and transform workspaces alive concurrently.
    /// </summary>
    public static ParallelPowerResult PowMemoryBounded(
        ulong baseValue,
        int exponent,
        int workerCount,
        Action<int, int>? progress,
        CancellationToken cancellationToken)
    {
        if (exponent <= LegacyMaximumExponent ||
            exponent > MaximumMemoryBoundedExponent)
        {
            throw new ArgumentOutOfRangeException(
                nameof(exponent),
                $"Memory-bounded NTT power supports exponents from {LegacyMaximumExponent + 1:N0} through {MaximumMemoryBoundedExponent:N0}.");
        }

        workerCount =
            Math.Max(
                1,
                workerCount);

        cancellationToken.ThrowIfCancellationRequested();

        int chunkExponent =
            LegacyMaximumExponent;

        int quotient =
            exponent /
            chunkExponent;

        int remainderExponent =
            exponent %
            chunkExponent;

        int chunkOperationCount =
            GetPowReportedOperationCount(
                chunkExponent,
                workerCount);

        int remainderOperationCount =
            remainderExponent > 0
                ? GetPowReportedOperationCount(
                    remainderExponent,
                    workerCount)
                : 0;

        int mergeOperationCount =
            CountMultiplications(
                quotient);

        int finalRemainderCombineCount =
            remainderExponent > 0
                ? 1
                : 0;

        int totalOperationCount =
            Math.Max(
                1,
                checked(
                    chunkOperationCount +
                    remainderOperationCount +
                    mergeOperationCount +
                    finalRemainderCombineCount));

        int completedOffset = 0;

        void ReportMappedProgress(
            int offset,
            int expectedSubTotal,
            int completed,
            int reportedSubTotal)
        {
            int normalizedTotal =
                Math.Max(
                    1,
                    reportedSubTotal);

            int normalizedCompleted =
                Math.Clamp(
                    completed,
                    0,
                    normalizedTotal);

            int mapped =
                expectedSubTotal == normalizedTotal
                    ? normalizedCompleted
                    : (int)Math.Round(
                        normalizedCompleted /
                        (double)normalizedTotal *
                        expectedSubTotal,
                        MidpointRounding.AwayFromZero);

            progress?.Invoke(
                Math.Min(
                    totalOperationCount,
                    offset + mapped),
                totalOperationCount);
        }

        var diagnostics =
            new PowerDiagnosticsCollector();

        // Compute the reusable a^10,000,000 seed with the existing production
        // engine. No arithmetic, scheduling, primes, DIF/DIT stages or CRT
        // behavior in Pow() is changed for the <=10M range.
        ParallelPowerResult chunkPower =
            Pow(
                baseValue,
                chunkExponent,
                workerCount,
                (completed, total) =>
                    ReportMappedProgress(
                        0,
                        chunkOperationCount,
                        completed,
                        total),
                cancellationToken);

        diagnostics.AccumulateSnapshot(
            chunkPower.Diagnostics);

        completedOffset +=
            chunkOperationCount;

        // Do not let dead legacy NTT workspaces from the 10M seed overlap an
        // optional second <=10M remainder calculation.
        CollectReleasedLargeModeWorkspaces();

        ParallelPowerResult? remainderPower =
            null;

        if (remainderExponent > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int remainderOffset =
                completedOffset;

            remainderPower =
                Pow(
                    baseValue,
                    remainderExponent,
                    workerCount,
                    (completed, total) =>
                        ReportMappedProgress(
                            remainderOffset,
                            remainderOperationCount,
                            completed,
                            total),
                    cancellationToken);

            diagnostics.AccumulateSnapshot(
                remainderPower.Diagnostics);

            completedOffset +=
                remainderOperationCount;
        }

        // Pow() has already dropped every pool reference, but its 128-256 MiB
        // LOH arrays can remain committed until a later Gen2 collection. Large
        // mode intentionally performs one blocking collection at this phase
        // boundary so dead legacy workspaces do not overlap the segmented
        // accumulator/workspaces and inflate the peak by several gigabytes.
        CollectReleasedLargeModeWorkspaces();

        // All <=10M Pow-scoped buffers are already released here. The merge
        // owns one independent pool whose 2^26 uint32 workspaces are recycled
        // across every segment pair and every higher-level multiplication.
        using var nttBufferPool =
            new NttBufferPool();

        using var nttTwiddleBufferPool =
            new NttTwiddleBufferPool();

        using var sharedNttTwiddlePlans =
            new SharedNttTwiddlePlans(
                nttTwiddleBufferPool);

        ParallelBigUnsigned magnitude;

        using (var workers =
               new FixedWorkerTeam(
                   workerCount,
                   nttBufferPool,
                   sharedNttTwiddlePlans))
        {
            int mergeCompleted = 0;

            magnitude =
                PowExistingMagnitudeMemoryBounded(
                    chunkPower.Magnitude,
                    quotient,
                    workers,
                    diagnostics,
                    (completed, total) =>
                    {
                        mergeCompleted =
                            Math.Max(
                                mergeCompleted,
                                completed);

                        progress?.Invoke(
                            Math.Min(
                                totalOperationCount,
                                completedOffset +
                                mergeCompleted),
                            totalOperationCount);
                    },
                    cancellationToken);

            completedOffset +=
                mergeOperationCount;

            if (remainderPower is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                magnitude =
                    MultiplyMemoryBounded(
                        magnitude,
                        remainderPower.Magnitude,
                        workers,
                        diagnostics,
                        cancellationToken);

                completedOffset++;

                progress?.Invoke(
                    Math.Min(
                        totalOperationCount,
                        completedOffset),
                    totalOperationCount);
            }
        }

        diagnostics.ConfigureLargeMemoryBoundedMode(
            chunkExponent);

        diagnostics.ConfigureNttBufferPool(
            nttBufferPool.CreateStatisticsSnapshot());

        nttBufferPool.ReleaseCachedBuffers();
        sharedNttTwiddlePlans.ReleasePlans();
        nttTwiddleBufferPool.ReleaseCachedBuffers();

        // The final magnitude must stay alive, but the 2^26 transform pool,
        // twiddle arrays and reusable segment product are dead now. Large mode
        // pays one last Gen2 boundary so those hundreds of MiB do not overlap
        // the UI/export preparation that follows this method.
        CollectReleasedLargeModeWorkspaces();

        progress?.Invoke(
            totalOperationCount,
            totalOperationCount);

        return new ParallelPowerResult(
            magnitude,
            diagnostics.CreateSnapshot(
                workerCount));
    }

    private static void CollectReleasedLargeModeWorkspaces()
    {
        GC.Collect(
            GC.MaxGeneration,
            GCCollectionMode.Forced,
            blocking: true,
            compacting: false);

        GC.WaitForPendingFinalizers();
    }

    private static ParallelBigUnsigned PowExistingMagnitudeMemoryBounded(
        ParallelBigUnsigned baseMagnitude,
        int exponent,
        FixedWorkerTeam workers,
        PowerDiagnosticsCollector diagnostics,
        Action<int, int>? progress,
        CancellationToken cancellationToken)
    {
        if (exponent <= 0)
        {
            return One;
        }

        ParallelBigUnsigned factor =
            baseMagnitude;

        ParallelBigUnsigned result =
            One;

        bool resultInitialized =
            false;

        int totalOperations =
            CountMultiplications(
                exponent);

        int completedOperations = 0;
        int remainingExponent = exponent;

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
                    result =
                        MultiplyMemoryBounded(
                            result,
                            factor,
                            workers,
                            diagnostics,
                            cancellationToken);

                    progress?.Invoke(
                        ++completedOperations,
                        totalOperations);
                }
            }

            remainingExponent >>= 1;

            if (remainingExponent > 0)
            {
                factor =
                    MultiplyMemoryBounded(
                        factor,
                        factor,
                        workers,
                        diagnostics,
                        cancellationToken);

                progress?.Invoke(
                    ++completedOperations,
                    totalOperations);
            }
        }

        progress?.Invoke(
            totalOperations,
            totalOperations);

        return resultInitialized
            ? result
            : One;
    }

    private static int GetPowReportedOperationCount(
        int exponent,
        int workerCount)
    {
        if (TryCreateExponentSplit(
                exponent,
                workerCount,
                out int firstExponent,
                out int secondExponent))
        {
            return checked(
                CountMultiplications(firstExponent) +
                CountMultiplications(secondExponent) +
                1);
        }

        return CountMultiplications(
            exponent);
    }

    private static ParallelPowerResult PowSplit(
        ulong baseValue,
        int originalExponent,
        int firstExponent,
        int secondExponent,
        int workerCount,
        NttBufferPool nttBufferPool,
        SharedNttTwiddlePlans sharedNttTwiddlePlans,
        Action<int, int>? progress,
        CancellationToken cancellationToken)
    {
        int firstWorkerCount =
            (workerCount + 1) /
            2;

        int secondWorkerCount =
            workerCount -
            firstWorkerCount;

        int firstOperationCount =
            CountMultiplications(
                firstExponent);

        int secondOperationCount =
            CountMultiplications(
                secondExponent);

        int totalOperationCount =
            checked(
                firstOperationCount +
                secondOperationCount +
                1);

        int firstCompleted = 0;
        int secondCompleted = 0;

        var progressGate =
            new object();

        void ReportBranchProgress(
            bool isFirstBranch,
            int completed)
        {
            lock (progressGate)
            {
                if (isFirstBranch)
                {
                    firstCompleted =
                        Math.Max(
                            firstCompleted,
                            completed);
                }
                else
                {
                    secondCompleted =
                        Math.Max(
                            secondCompleted,
                            completed);
                }

                progress?.Invoke(
                    Math.Min(
                        totalOperationCount - 1,
                        firstCompleted +
                        secondCompleted),
                    totalOperationCount);
            }
        }

        using var linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        BranchPowerResult RunBranch(
            int branchExponent,
            int branchWorkerCount,
            bool isFirstBranch)
        {
            var branchDiagnostics =
                new PowerDiagnosticsCollector();

            long branchStarted =
                Stopwatch.GetTimestamp();

            try
            {
                using var branchWorkers =
                    new FixedWorkerTeam(
                        branchWorkerCount,
                        nttBufferPool,
                        sharedNttTwiddlePlans);

                ParallelBigUnsigned branchMagnitude =
                    PowWithTeam(
                        baseValue,
                        branchExponent,
                        branchWorkers,
                        branchDiagnostics,
                        (completed, _) =>
                            ReportBranchProgress(
                                isFirstBranch,
                                completed),
                        linkedCancellation.Token);

                return new BranchPowerResult(
                    branchMagnitude,
                    branchDiagnostics,
                    Stopwatch.GetTimestamp() -
                    branchStarted);
            }
            catch
            {
                linkedCancellation.Cancel();
                throw;
            }
        }

        Task<BranchPowerResult> firstTask =
            Task.Factory.StartNew(
                () => RunBranch(
                    firstExponent,
                    firstWorkerCount,
                    isFirstBranch: true),
                linkedCancellation.Token,
                TaskCreationOptions.LongRunning |
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);

        Task<BranchPowerResult> secondTask =
            Task.Factory.StartNew(
                () => RunBranch(
                    secondExponent,
                    secondWorkerCount,
                    isFirstBranch: false),
                linkedCancellation.Token,
                TaskCreationOptions.LongRunning |
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);

        try
        {
            Task.WhenAll(
                    firstTask,
                    secondTask)
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            linkedCancellation.Cancel();

            try
            {
                Task.WhenAll(
                        firstTask,
                        secondTask)
                    .GetAwaiter()
                    .GetResult();
            }
            catch
            {
                // Preserve the original exception after both worker teams
                // have observed cancellation and released their buffers.
            }

            throw;
        }

        cancellationToken.ThrowIfCancellationRequested();

        BranchPowerResult firstResult =
            firstTask.GetAwaiter().GetResult();

        BranchPowerResult secondResult =
            secondTask.GetAwaiter().GetResult();

        PowerDiagnosticsCollector diagnostics =
            PowerDiagnosticsCollector.CombineParallelBranches(
                firstResult.Diagnostics,
                secondResult.Diagnostics);

        long finalCombineStarted =
            Stopwatch.GetTimestamp();

        ParallelBigUnsigned magnitude;

        using (var finalWorkers =
               new FixedWorkerTeam(
                   workerCount,
                   nttBufferPool,
                   sharedNttTwiddlePlans))
        {
            magnitude =
                Multiply(
                    firstResult.Magnitude,
                    secondResult.Magnitude,
                    finalWorkers,
                    diagnostics,
                    cancellationToken);
        }

        diagnostics.ConfigureExponentSplit(
            originalExponent,
            firstExponent,
            secondExponent,
            firstWorkerCount,
            secondWorkerCount,
            firstResult.ElapsedTicks,
            secondResult.ElapsedTicks,
            Stopwatch.GetTimestamp() -
            finalCombineStarted);

        progress?.Invoke(
            totalOperationCount,
            totalOperationCount);

        diagnostics.ConfigureNttBufferPool(
            nttBufferPool.CreateStatisticsSnapshot());

        // Both split branches and the final-combine worker team have already
        // been disposed. Drop the two cached transform arrays now instead of
        // waiting for the enclosing using statement to leave Pow().
        nttBufferPool.ReleaseCachedBuffers();
        sharedNttTwiddlePlans.ReleasePlans();

        // ReleasePlans() returns the shared forward/inverse tables to the
        // Pow-scoped twiddle pool. That pool is owned by Pow(), so its using
        // declaration disposes it immediately after PowSplit() returns and
        // before the result is handed to the caller. Do not reference the
        // Pow-local nttTwiddleBufferPool from this helper.

        return new ParallelPowerResult(
            magnitude,
            diagnostics.CreateSnapshot(
                workerCount));
    }

    private static ParallelBigUnsigned PowWithTeam(
        ulong baseValue,
        int exponent,
        FixedWorkerTeam workers,
        PowerDiagnosticsCollector diagnostics,
        Action<int, int>? progress,
        CancellationToken cancellationToken)
    {

        ParallelBigUnsigned factor =
            FromUInt64(
                baseValue);

        ParallelBigUnsigned result =
            One;

        bool resultInitialized =
            false;

        int totalOperations =
            CountMultiplications(
                exponent);

        int completedOperations = 0;
        int remainingExponent =
            exponent;

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
                    result =
                        Multiply(
                            result,
                            factor,
                            workers,
                            diagnostics,
                            cancellationToken);

                    progress?.Invoke(
                        ++completedOperations,
                        totalOperations);
                }
            }

            remainingExponent >>= 1;

            if (remainingExponent > 0)
            {
                factor =
                    Multiply(
                        factor,
                        factor,
                        workers,
                        diagnostics,
                        cancellationToken);

                progress?.Invoke(
                    ++completedOperations,
                    totalOperations);
            }
        }

        progress?.Invoke(
            totalOperations,
            totalOperations);

        return resultInitialized
            ? result
            : One;
    }

    public string ToDecimalString(
        bool useSimd = false)
    {
        int digitCount =
            DigitCount;

        var characters =
            new char[digitCount];

        int position = 0;

        string highestText =
            _limbs[_limbCount - 1].ToString(
                CultureInfo.InvariantCulture);

        highestText.CopyTo(
            0,
            characters,
            position,
            highestText.Length);

        position +=
            highestText.Length;

        int fixedLimbCount =
            _limbCount - 1;

        if (fixedLimbCount > 0)
        {
            WriteFixedLimbsDescending(
                _limbs,
                _limbCount - 2,
                fixedLimbCount,
                characters.AsSpan(
                    position,
                    fixedLimbCount *
                    DigitsPerLimb),
                useSimd);
        }

        return new string(
            characters);
    }

    public void WriteDecimalBlocks(
        TextWriter writer,
        int blockDigitCount,
        Action reportBlockWritten,
        CancellationToken cancellationToken,
        bool useSimd = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            blockDigitCount);

        // The NTT magnitude is already base 10,000.  Keep an extra limb of
        // headroom so a SIMD batch may cross the 4,096-character write
        // boundary without falling back to per-character bookkeeping.
        var buffer =
            new char[checked(
                blockDigitCount +
                DigitsPerLimb)];

        int bufferedCharacters = 0;

        void FlushFullBlock()
        {
            cancellationToken.ThrowIfCancellationRequested();

            writer.Write(
                buffer,
                0,
                blockDigitCount);

            int overflow =
                bufferedCharacters -
                blockDigitCount;

            if (overflow > 0)
            {
                Array.Copy(
                    buffer,
                    blockDigitCount,
                    buffer,
                    0,
                    overflow);
            }

            bufferedCharacters =
                overflow;

            reportBlockWritten();
        }

        string highestText =
            _limbs[_limbCount - 1].ToString(
                CultureInfo.InvariantCulture);

        highestText.CopyTo(
            0,
            buffer,
            0,
            highestText.Length);

        bufferedCharacters =
            highestText.Length;

        int limbIndex =
            _limbCount - 2;

        int preferredBatchLimbCount =
            Math.Max(
                1,
                blockDigitCount /
                DigitsPerLimb);

        while (limbIndex >= 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int batchLimbCount =
                Math.Min(
                    preferredBatchLimbCount,
                    limbIndex + 1);

            int batchCharacterCount =
                checked(
                    batchLimbCount *
                    DigitsPerLimb);

            // The normal export block is 4,096 digits (1,024 limbs), so the
            // buffer can receive one complete vector-friendly batch.  For a
            // custom tiny block size, limit the batch so the +4 headroom is
            // still sufficient.
            int maximumBatchCharacters =
                buffer.Length -
                bufferedCharacters;

            if (batchCharacterCount >
                maximumBatchCharacters)
            {
                batchLimbCount =
                    Math.Max(
                        1,
                        maximumBatchCharacters /
                        DigitsPerLimb);

                batchCharacterCount =
                    batchLimbCount *
                    DigitsPerLimb;
            }

            WriteFixedLimbsDescending(
                _limbs,
                limbIndex,
                batchLimbCount,
                buffer.AsSpan(
                    bufferedCharacters,
                    batchCharacterCount),
                useSimd);

            limbIndex -=
                batchLimbCount;

            bufferedCharacters +=
                batchCharacterCount;

            if (bufferedCharacters >=
                blockDigitCount)
            {
                FlushFullBlock();
            }
        }

        if (bufferedCharacters > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            writer.Write(
                buffer,
                0,
                bufferedCharacters);

            reportBlockWritten();
        }
    }

    private static ParallelBigUnsigned Multiply(
        ParallelBigUnsigned left,
        ParallelBigUnsigned right,
        FixedWorkerTeam workers,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (left.IsOne)
        {
            return right;
        }

        if (right.IsOne)
        {
            return left;
        }

        long schoolbookWork =
            (long)left._limbCount *
            right._limbCount;

        if (schoolbookWork <=
            SchoolbookWorkLimit)
        {
            return MultiplySchoolbook(
                left,
                right,
                diagnostics,
                cancellationToken);
        }

        return MultiplyNtt(
            left,
            right,
            workers,
            diagnostics,
            cancellationToken);
    }

    private static ParallelBigUnsigned MultiplyMemoryBounded(
        ParallelBigUnsigned left,
        ParallelBigUnsigned right,
        FixedWorkerTeam workers,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (left.IsOne)
        {
            return right;
        }

        if (right.IsOne)
        {
            return left;
        }

        int coefficientCount =
            checked(
                left._limbCount +
                right._limbCount -
                1);

        if (coefficientCount <=
            MaximumTransformLength)
        {
            // Still use the exact production multiplication kernel while the
            // full transform fits 2^26. The segmented path starts only at the
            // first multiplication that would exceed the legacy prime limit.
            return Multiply(
                left,
                right,
                workers,
                diagnostics,
                cancellationToken);
        }

        return MultiplySegmentedNtt(
            left,
            right,
            workers,
            diagnostics,
            cancellationToken);
    }

    private static ParallelBigUnsigned MultiplySegmentedNtt(
        ParallelBigUnsigned left,
        ParallelBigUnsigned right,
        FixedWorkerTeam workers,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool isSquare =
            ReferenceEquals(
                left,
                right);

        int leftSegmentCount =
            checked(
                (left._limbCount +
                 SegmentedNttLimbLength - 1) /
                SegmentedNttLimbLength);

        int rightSegmentCount =
            isSquare
                ? leftSegmentCount
                : checked(
                    (right._limbCount +
                     SegmentedNttLimbLength - 1) /
                    SegmentedNttLimbLength);

        int maximumSegmentProductLength =
            checked(
                Math.Min(
                    SegmentedNttLimbLength,
                    left._limbCount) +
                Math.Min(
                    SegmentedNttLimbLength,
                    right._limbCount));

        // One reusable compact P1/result buffer is sufficient for every pair.
        // It is overwritten by inverse P1, then CRT/carry normalizes the same
        // prefix to base-10,000 before that product is added to the global
        // result. No per-pair 256 MiB result allocation is left for GC.
        uint[] segmentProduct =
            workers.GetSegmentedProductScratch(
                maximumSegmentProductLength);

        int resultCapacity =
            checked(
                left._limbCount +
                right._limbCount +
                2);

        // The final magnitude is the only large zero-initialized array needed
        // by segmented accumulation. Every NTT pair is added here immediately.
        var resultLimbs =
            new uint[resultCapacity];

        int segmentPairCount = 0;

        for (int leftSegmentIndex = 0;
             leftSegmentIndex < leftSegmentCount;
             leftSegmentIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int leftOffset =
                checked(
                    leftSegmentIndex *
                    SegmentedNttLimbLength);

            int leftLength =
                Math.Min(
                    SegmentedNttLimbLength,
                    left._limbCount -
                    leftOffset);

            int firstRightSegmentIndex =
                isSquare
                    ? leftSegmentIndex
                    : 0;

            for (int rightSegmentIndex = firstRightSegmentIndex;
                 rightSegmentIndex < rightSegmentCount;
                 rightSegmentIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int rightOffset =
                    checked(
                        rightSegmentIndex *
                        SegmentedNttLimbLength);

                int rightLength =
                    Math.Min(
                        SegmentedNttLimbLength,
                        right._limbCount -
                        rightOffset);

                int productLength =
                    ConvolveSegmentIntoReusableBuffer(
                        left._limbs,
                        leftOffset,
                        leftLength,
                        right._limbs,
                        rightOffset,
                        rightLength,
                        segmentProduct,
                        isSquare &&
                        leftSegmentIndex ==
                        rightSegmentIndex,
                        workers,
                        diagnostics,
                        cancellationToken);

                int destinationOffset =
                    checked(
                        leftOffset +
                        rightOffset);

                int multiplicity =
                    isSquare &&
                    leftSegmentIndex !=
                    rightSegmentIndex
                        ? 2
                        : 1;

                AddNormalizedSegmentProduct(
                    resultLimbs,
                    segmentProduct,
                    productLength,
                    destinationOffset,
                    multiplicity,
                    cancellationToken);

                segmentPairCount++;
            }
        }

        diagnostics.ConfigureSegmentedNttMultiplication(
            segmentPairCount);

        return new ParallelBigUnsigned(
            resultLimbs,
            takeOwnership: true,
            logicalLength: resultCapacity);
    }

    private static int ConvolveSegmentIntoReusableBuffer(
        uint[] left,
        int leftOffset,
        int leftLength,
        uint[] right,
        int rightOffset,
        int rightLength,
        uint[] segmentProduct,
        bool isSquare,
        FixedWorkerTeam workers,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        int coefficientCount =
            checked(
                leftLength +
                rightLength -
                1);

        int transformLength = 1;

        while (transformLength <
               coefficientCount)
        {
            transformLength =
                checked(
                    transformLength << 1);
        }

        Debug.Assert(
            transformLength <=
            MaximumTransformLength);

        if (transformLength >
            MaximumTransformLength)
        {
            throw new InvalidOperationException(
                "A segmented NTT pair exceeded the supported 2^26 transform length.");
        }

        if (segmentProduct.Length <
            coefficientCount + 1)
        {
            throw new InvalidOperationException(
                "The reusable segmented NTT result buffer is too small.");
        }

        diagnostics.NttMultiplicationCount++;

        ConvolveModulusCore(
            left,
            leftOffset,
            leftLength,
            right,
            rightOffset,
            rightLength,
            transformLength,
            FirstModulus,
            FirstPrimitiveRoot,
            isSquare,
            true,
            segmentProduct,
            workers,
            diagnostics,
            cancellationToken,
            static (_, _) =>
            {
                // P1 compact output is supplied by segmentProduct, so no
                // returned array needs to be captured here.
            });

        ulong trailingCarry = 0;

        ConvolveModulusCore(
            left,
            leftOffset,
            leftLength,
            right,
            rightOffset,
            rightLength,
            transformLength,
            SecondModulus,
            SecondPrimitiveRoot,
            isSquare,
            false,
            null,
            workers,
            diagnostics,
            cancellationToken,
            (transformedSecond, _) =>
            {
                trailingCarry =
                    ReconstructCarryStreamingIntoFirstResidues(
                        transformedSecond,
                        segmentProduct,
                        coefficientCount,
                        workers,
                        diagnostics,
                        cancellationToken);
            });

        int resultLength =
            coefficientCount;

        if (trailingCarry > 0)
        {
            if (trailingCarry >= LimbBase)
            {
                throw new InvalidOperationException(
                    "Normalized segmented NTT carry exceeded one base-10,000 limb.");
            }

            segmentProduct[resultLength++] =
                (uint)trailingCarry;
        }

        return resultLength;
    }

    private static void AddNormalizedSegmentProduct(
        uint[] destination,
        uint[] product,
        int productLength,
        int destinationOffset,
        int multiplicity,
        CancellationToken cancellationToken)
    {
        Debug.Assert(
            multiplicity is 1 or 2);

        ulong carry = 0;
        int destinationIndex =
            destinationOffset;

        for (int index = 0;
             index < productLength;
             index++, destinationIndex++)
        {
            if ((index & 0xFFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if ((uint)destinationIndex >=
                (uint)destination.Length)
            {
                throw new InvalidOperationException(
                    "Segmented NTT accumulation exceeded the result buffer.");
            }

            ulong value =
                destination[destinationIndex] +
                (ulong)product[index] *
                (uint)multiplicity +
                carry;

            ulong quotient =
                value /
                LimbBase;

            destination[destinationIndex] =
                (uint)(value -
                       quotient *
                       LimbBase);

            carry =
                quotient;
        }

        while (carry > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if ((uint)destinationIndex >=
                (uint)destination.Length)
            {
                throw new InvalidOperationException(
                    "Segmented NTT carry exceeded the result buffer.");
            }

            ulong value =
                destination[destinationIndex] +
                carry;

            ulong quotient =
                value /
                LimbBase;

            destination[destinationIndex] =
                (uint)(value -
                       quotient *
                       LimbBase);

            carry =
                quotient;

            destinationIndex++;
        }
    }

    private static ParallelBigUnsigned MultiplySchoolbook(
        ParallelBigUnsigned left,
        ParallelBigUnsigned right,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        int coefficientCount =
            checked(
                left._limbCount +
                right._limbCount -
                1);

        var coefficients =
            new ulong[coefficientCount];

        for (int leftIndex = 0;
             leftIndex < left._limbCount;
             leftIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ulong leftValue =
                left._limbs[leftIndex];

            for (int rightIndex = 0;
                 rightIndex < right._limbCount;
                 rightIndex++)
            {
                coefficients[leftIndex + rightIndex] +=
                    leftValue *
                    right._limbs[rightIndex];
            }
        }

        return CreateFromCoefficients(
            coefficients,
            diagnostics,
            cancellationToken);
    }

    private static ParallelBigUnsigned MultiplyNtt(
        ParallelBigUnsigned left,
        ParallelBigUnsigned right,
        FixedWorkerTeam workers,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        int coefficientCount =
            checked(
                left._limbCount +
                right._limbCount -
                1);

        int transformLength = 1;

        while (transformLength <
               coefficientCount)
        {
            transformLength =
                checked(
                    transformLength << 1);
        }

        if (transformLength >
            MaximumTransformLength)
        {
            throw new InvalidOperationException(
                "The exact parallel transform exceeds the supported 2^26 length.");
        }

        bool isSquare =
            ReferenceEquals(
                left,
                right);

        diagnostics.NttMultiplicationCount++;

        uint[] firstResidues =
            ConvolveModulus(
                left._limbs,
                left._limbCount,
                right._limbs,
                right._limbCount,
                coefficientCount,
                transformLength,
                FirstModulus,
                FirstPrimitiveRoot,
                isSquare,
                workers,
                diagnostics,
                cancellationToken);

        // v30: fuse CRT -> carry in bounded blocks. The first-modulus residue
        // array becomes the final base-10,000 limb array in place once each
        // block has been reconstructed, so neither a full ulong[] coefficient
        // array nor a second full uint[] result array is required.
        ulong trailingCarry =
            ConvolveSecondModulusAndReconstructCarryStreaming(
                left._limbs,
                left._limbCount,
                right._limbs,
                right._limbCount,
                firstResidues,
                coefficientCount,
                transformLength,
                isSquare,
                workers,
                diagnostics,
                cancellationToken);

        // ConvolveModulus reserves one spare uint slot. Normal base-B
        // multiplication can produce at most one limb beyond coefficientCount,
        // so append the trailing carry without resizing/copying the full result.
        int resultLimbCount =
            coefficientCount;

        if (trailingCarry > 0)
        {
            long carryStarted =
                Stopwatch.GetTimestamp();

            Debug.Assert(
                trailingCarry <
                LimbBase);

            if (trailingCarry >= LimbBase)
            {
                throw new InvalidOperationException(
                    "Normalized NTT carry exceeded one base-10,000 limb.");
            }

            firstResidues[resultLimbCount++] =
                (uint)trailingCarry;

            diagnostics.CarryTicks +=
                Stopwatch.GetTimestamp() -
                carryStarted;
        }

        return new ParallelBigUnsigned(
            firstResidues,
            takeOwnership: true,
            logicalLength: resultLimbCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SelectAdaptiveFourWayHalfLength()
    {
        int logicalProcessorCount =
            Math.Max(
                1,
                Environment.ProcessorCount);

        // Four-way scalar unrolling is intentionally adaptive. A CPU with
        // fewer logical processors benefits from exposing more independent
        // butterfly arithmetic per worker, while a high-thread SMT CPU already
        // has more thread-level parallelism and is more sensitive to register
        // pressure. Keep L1-sized late stages on the proven two-way kernel;
        // use four-way only once the stage is large enough to amortize the
        // extra live values and loop body.
        if (logicalProcessorCount >= 20)
        {
            return 1 << 13; // halfLength 8192: conservative on 20T+ CPUs.
        }

        if (logicalProcessorCount >= 8)
        {
            return 1 << 11; // halfLength 2048: 6C/12T-class CPUs.
        }

        if (logicalProcessorCount >= 4)
        {
            return 1 << 10; // halfLength 1024.
        }

        return 1 << 9;      // halfLength 512 for very small worker budgets.
    }

    private static readonly int AdaptiveFourWayHalfLength =
        SelectAdaptiveFourWayHalfLength();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SelectGlobalAdaptiveEightWayHalfLength()
    {
        int logicalProcessorCount =
            Math.Max(
                1,
                Environment.ProcessorCount);

        // v21 experiment: eight-way is restricted to RAM/global cached stages.
        // L3/L2/L1 stay on the proven v19 kernels.  Use a 4+4 macro-unroll so
        // loop-control is halved without keeping eight butterflies live at once.
        // The thresholds are deliberately conservative on high-thread SMT CPUs.
        if (logicalProcessorCount >= 20)
        {
            return 1 << 17; // halfLength 131072 on 20T+ CPUs.
        }

        if (logicalProcessorCount >= 8)
        {
            return 1 << 15; // halfLength 32768 on 8-19T CPUs (i7-8700 = 12T).
        }

        if (logicalProcessorCount >= 4)
        {
            return 1 << 14; // halfLength 16384.
        }

        return 1 << 13;     // halfLength 8192 on 1-3T CPUs.
    }

    private static readonly int GlobalAdaptiveEightWayHalfLength =
        SelectGlobalAdaptiveEightWayHalfLength();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SelectMaximumCachedTwiddleCount()
    {
        int logicalProcessorCount =
            Math.Max(
                1,
                Environment.ProcessorCount);

        // On 8+ logical processors the large power engine has enough parallel
        // arithmetic throughput that repeatedly advancing an uncached twiddle
        // becomes visible in the global stages.  Cache exactly one extra stage
        // (8 MiB of active uint twiddles) and reuse it for the whole worker-team
        // lifetime.  Avoid growing further: larger tables would compete too
        // aggressively with the value buffers in LLC, especially on DDR4-era
        // desktop CPUs.
        return logicalProcessorCount >= 8
            ? LargePowerMaximumCachedTwiddleCount
            : DefaultMaximumCachedTwiddleCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SelectFusedNttBlockLength()
    {
        int logicalProcessorCount =
            Math.Max(
                1,
                Environment.ProcessorCount);

        // 20+ logical processors: keep the 4096-value block that benchmarks
        // well on high-thread modern CPUs (for example 24-thread HX 370).
        if (logicalProcessorCount >= 20)
        {
            return DefaultFusedNttBlockLength;
        }

        // 8-19 logical processors are commonly 4C/8T through 8C/16T SMT
        // designs with a smaller private-cache budget per sibling.  2048
        // values consume only 8 KiB per worker and are intentionally the
        // conservative choice for a 6C/12T i7-8700-class CPU.
        if (logicalProcessorCount >= 8)
        {
            return SmallSmtFusedNttBlockLength;
        }

        // With fewer workers, block-dispatch overhead matters more than SMT
        // cache pressure.  Keep 4096 for 4-7 logical processors and use 8192
        // only for very small 1-3 processor budgets.
        return logicalProcessorCount >= 4
            ? DefaultFusedNttBlockLength
            : LowThreadFusedNttBlockLength;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SelectL2NttTileLength(
        int fusedNttBlockLength)
    {
        int logicalProcessorCount =
            Math.Max(
                1,
                Environment.ProcessorCount);

        int tileLength;

        if (logicalProcessorCount >= 20)
        {
            // 24-thread HX 370-class CPUs: 256 KiB of values per tile.  With
            // the largest local twiddle stage this stays comfortably below a
            // 1 MiB-class private L2 even with two SMT siblings active.
            tileLength =
                HighThreadL2NttTileLength;
        }
        else if (logicalProcessorCount >= 8)
        {
            // 12-thread Coffee Lake-class CPUs have only 256 KiB L2 per core.
            // 64 KiB values + at most 32 KiB local twiddles per worker leaves
            // useful headroom when both SMT siblings share that cache.
            tileLength =
                SmallSmtL2NttTileLength;
        }
        else if (logicalProcessorCount >= 4)
        {
            tileLength =
                MidThreadL2NttTileLength;
        }
        else
        {
            tileLength =
                HighThreadL2NttTileLength;
        }

        return Math.Max(
            fusedNttBlockLength,
            tileLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SelectL3NttTileLength(
        int l2NttTileLength)
    {
        int logicalProcessorCount =
            Math.Max(
                1,
                Environment.ProcessorCount);

        int tileLength;

        if (logicalProcessorCount >= 20)
        {
            // High-thread modern CPUs usually pair a larger shared LLC with a
            // larger private L2.  One MiB of values removes two global sweeps
            // above the 256 KiB L2 tile used by the 24-thread HX 370 class.
            tileLength =
                HighThreadL3NttTileLength;
        }
        else if (logicalProcessorCount >= 8)
        {
            // A 6C/12T Coffee Lake-class CPU has a 12 MiB shared L3.  A 512
            // KiB value tile per active logical worker leaves room for twiddles
            // and the sibling power branch instead of trying to occupy all LLC.
            tileLength =
                SmallSmtL3NttTileLength;
        }
        else if (logicalProcessorCount >= 4)
        {
            tileLength =
                MidThreadL3NttTileLength;
        }
        else
        {
            tileLength =
                LowThreadL3NttTileLength;
        }

        return Math.Max(
            l2NttTileLength,
            tileLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanUseL2CacheBlocking(
        int transformLength,
        int l2NttTileLength,
        int workerCount)
    {
        if (transformLength <= l2NttTileLength)
        {
            return false;
        }

        int tileCount =
            transformLength /
            l2NttTileLength;

        // Keep at least one independent tile per worker.  Smaller transforms
        // fall back to the proven L1-only v8 path rather than underutilizing
        // the fixed worker team.
        return tileCount >=
               Math.Max(1, workerCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanUseL3CacheBlocking(
        int transformLength,
        int l3NttTileLength,
        int workerCount)
    {
        if (transformLength <= l3NttTileLength)
        {
            return false;
        }

        int tileCount =
            transformLength /
            l3NttTileLength;

        // As with the L2 path, never trade away parallel occupancy merely to
        // gain locality.  Large power transforms expose hundreds of LLC tiles;
        // small transforms simply stay on the proven v9 L2/L1 hierarchy.
        return tileCount >=
               Math.Max(1, workerCount);
    }

    private static uint[] ConvolveModulus(
        uint[] left,
        int leftLength,
        uint[] right,
        int rightLength,
        int coefficientCount,
        int transformLength,
        uint modulus,
        uint primitiveRoot,
        bool isSquare,
        FixedWorkerTeam workers,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        // v33 retains v32's late-allocation rule: the compact P1/result
        // backing is not created while forward NTT can still have both left
        // and right transform workspaces leased.  It is allocated only when
        // inverse DIT reaches its final stage, then that stage writes the valid
        // normalized prefix directly into the compact array.
        uint[]? residues =
            null;

        ConvolveModulusCore(
            left,
            0,
            leftLength,
            right,
            0,
            rightLength,
            transformLength,
            modulus,
            primitiveRoot,
            isSquare,
            true,
            null,
            workers,
            diagnostics,
            cancellationToken,
            (_, compactOutput) =>
            {
                // v33: the final inverse DIT stage writes the valid P1 prefix
                // directly into this compact result backing.  This preserves
                // v32's late allocation boundary while removing the separate
                // coefficientCount-sized Array.Copy pass after inverse NTT.
                residues =
                    compactOutput ??
                    throw new InvalidOperationException(
                        "The compact first-modulus inverse output was not produced.");
            });

        return residues ??
               throw new InvalidOperationException(
                   "The first-modulus inverse transform did not produce residues.");
    }

    private static ulong ConvolveSecondModulusAndReconstructCarryStreaming(
        uint[] left,
        int leftLength,
        uint[] right,
        int rightLength,
        uint[] firstResidues,
        int coefficientCount,
        int transformLength,
        bool isSquare,
        FixedWorkerTeam workers,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        ulong trailingCarry = 0;

        ConvolveModulusCore(
            left,
            0,
            leftLength,
            right,
            0,
            rightLength,
            transformLength,
            SecondModulus,
            SecondPrimitiveRoot,
            isSquare,
            false,
            null,
            workers,
            diagnostics,
            cancellationToken,
            (transformedSecond, _) =>
            {
                int scratchLength =
                    Math.Min(
                        coefficientCount,
                        CrtCarryStreamingBlockLength);

                // v32: after the inverse P2 transform, indices at and above
                // coefficientCount are outside the valid linear-convolution
                // prefix and will never be read again.  When that dead tail is
                // large enough, reinterpret it as the bounded ulong CRT block
                // scratch instead of allocating/retaining another 8 MiB array.
                // Align to an even uint index so ulong elements start on an
                // 8-byte boundary.  Exact/full transforms simply fall back to
                // the team's reusable scratch array.
                int inverseTailScratchStart =
                    checked(
                        (coefficientCount + 1) & ~1);

                int requiredTailUIntCount =
                    checked(
                        scratchLength * 2);

                bool useInverseTailScratch =
                    inverseTailScratchStart <= transformedSecond.Length &&
                    transformedSecond.Length - inverseTailScratchStart >=
                    requiredTailUIntCount;

                if (useInverseTailScratch)
                {
                    // A branch may have grown a fallback scratch during earlier
                    // smaller transforms where the dead tail could not fit it.
                    // Once the current inverse buffer supplies the scratch, drop
                    // that stale team reference immediately rather than carrying
                    // an otherwise-unused 8 MiB array through later NTT stages.
                    workers.ReleaseCrtCarryScratch();
                }

                ulong[]? crtScratch =
                    useInverseTailScratch
                        ? null
                        : workers.GetCrtCarryScratch(
                            scratchLength);

                ulong carry = 0;
                long crtTicks = 0;
                long carryTicks = 0;

                for (int blockStart = 0;
                     blockStart < coefficientCount;
                     blockStart += scratchLength)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int blockCount =
                        Math.Min(
                            scratchLength,
                            coefficientCount -
                            blockStart);

                    long crtStarted =
                        Stopwatch.GetTimestamp();

                    // CRT stays parallel. Each worker writes only its own
                    // range in the bounded scratch block, and the barrier at
                    // ExecuteRanges completion guarantees the source residues
                    // can then be overwritten safely by the sequential carry.
                    ExecuteRanges(
                        blockCount,
                        workers,
                        cancellationToken,
                        (start, end) =>
                        {
                            int count =
                                end - start;

                            int sourceStart =
                                blockStart +
                                start;

                            ReadOnlySpan<uint> firstSpan =
                                firstResidues.AsSpan(
                                    sourceStart,
                                    count);

                            // The inverse P2 transform remains leased only
                            // while its valid convolution prefix is consumed.
                            ReadOnlySpan<uint> secondSpan =
                                transformedSecond.AsSpan(
                                    sourceStart,
                                    count);

                            Span<ulong> scratchSpan;

                            if (useInverseTailScratch)
                            {
                                scratchSpan =
                                    MemoryMarshal.Cast<uint, ulong>(
                                        transformedSecond.AsSpan(
                                            checked(
                                                inverseTailScratchStart +
                                                start * 2),
                                            checked(
                                                count * 2)));
                            }
                            else
                            {
                                scratchSpan =
                                    crtScratch!.AsSpan(
                                        start,
                                        count);
                            }

                            for (int offset = 0;
                                 offset < count;
                                 offset++)
                            {
                                uint first =
                                    firstSpan[offset];

                                uint reducedFirst =
                                    first;

                                if (reducedFirst >= SecondModulus) reducedFirst -= SecondModulus;
                                if (reducedFirst >= SecondModulus) reducedFirst -= SecondModulus;
                                if (reducedFirst >= SecondModulus) reducedFirst -= SecondModulus;
                                if (reducedFirst >= SecondModulus) reducedFirst -= SecondModulus;

                                long difference =
                                    (long)secondSpan[offset] -
                                    reducedFirst;

                                if (difference < 0)
                                {
                                    difference +=
                                        SecondModulus;
                                }

                                // Preserve the scalar modulo expression that
                                // won the previous benchmark experiments.
                                ulong multiplier =
                                    (ulong)difference *
                                    FirstModulusInverseInSecond %
                                    SecondModulus;

                                scratchSpan[offset] =
                                    first +
                                    (ulong)FirstModulus *
                                    multiplier;
                            }
                        });

                    crtTicks +=
                        Stopwatch.GetTimestamp() -
                        crtStarted;

                    long carryStarted =
                        Stopwatch.GetTimestamp();

                    ReadOnlySpan<ulong> source;

                    if (useInverseTailScratch)
                    {
                        source =
                            MemoryMarshal.Cast<uint, ulong>(
                                transformedSecond.AsSpan(
                                    inverseTailScratchStart,
                                    checked(
                                        blockCount * 2)));
                    }
                    else
                    {
                        source =
                            crtScratch!.AsSpan(
                                0,
                                blockCount);
                    }

                    // The CRT values for this block are now fully detached
                    // from P1. Reuse the corresponding P1 storage in place as
                    // the normalized base-10,000 result, avoiding another
                    // coefficientCount-sized uint[] allocation.
                    Span<uint> destination =
                        firstResidues.AsSpan(
                            blockStart,
                            blockCount);

                    for (int offset = 0;
                         offset < blockCount;
                         offset++)
                    {
                        int coefficientIndex =
                            blockStart +
                            offset;

                        if ((coefficientIndex & 0xFFFF) == 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                        }

                        ulong value =
                            source[offset] +
                            carry;

                        ulong quotient =
                            value /
                            LimbBase;

                        destination[offset] =
                            (uint)(value -
                                   quotient *
                                   LimbBase);

                        carry =
                            quotient;
                    }

                    carryTicks +=
                        Stopwatch.GetTimestamp() -
                        carryStarted;
                }

                trailingCarry =
                    carry;

                diagnostics.CrtTicks +=
                    crtTicks;

                diagnostics.CarryTicks +=
                    carryTicks;
            });

        return trailingCarry;
    }

    private static ulong ReconstructCarryStreamingIntoFirstResidues(
        uint[] transformedSecond,
        uint[] firstResidues,
        int coefficientCount,
        FixedWorkerTeam workers,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        ulong trailingCarry = 0;
        int scratchLength =
            Math.Min(
                coefficientCount,
                CrtCarryStreamingBlockLength);

        // v32: after the inverse P2 transform, indices at and above
        // coefficientCount are outside the valid linear-convolution
        // prefix and will never be read again.  When that dead tail is
        // large enough, reinterpret it as the bounded ulong CRT block
        // scratch instead of allocating/retaining another 8 MiB array.
        // Align to an even uint index so ulong elements start on an
        // 8-byte boundary.  Exact/full transforms simply fall back to
        // the team's reusable scratch array.
        int inverseTailScratchStart =
            checked(
                (coefficientCount + 1) & ~1);

        int requiredTailUIntCount =
            checked(
                scratchLength * 2);

        bool useInverseTailScratch =
            inverseTailScratchStart <= transformedSecond.Length &&
            transformedSecond.Length - inverseTailScratchStart >=
            requiredTailUIntCount;

        if (useInverseTailScratch)
        {
            // A branch may have grown a fallback scratch during earlier
            // smaller transforms where the dead tail could not fit it.
            // Once the current inverse buffer supplies the scratch, drop
            // that stale team reference immediately rather than carrying
            // an otherwise-unused 8 MiB array through later NTT stages.
            workers.ReleaseCrtCarryScratch();
        }

        ulong[]? crtScratch =
            useInverseTailScratch
                ? null
                : workers.GetCrtCarryScratch(
                    scratchLength);

        ulong carry = 0;
        long crtTicks = 0;
        long carryTicks = 0;

        for (int blockStart = 0;
             blockStart < coefficientCount;
             blockStart += scratchLength)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int blockCount =
                Math.Min(
                    scratchLength,
                    coefficientCount -
                    blockStart);

            long crtStarted =
                Stopwatch.GetTimestamp();

            // CRT stays parallel. Each worker writes only its own
            // range in the bounded scratch block, and the barrier at
            // ExecuteRanges completion guarantees the source residues
            // can then be overwritten safely by the sequential carry.
            ExecuteRanges(
                blockCount,
                workers,
                cancellationToken,
                (start, end) =>
                {
                    int count =
                        end - start;

                    int sourceStart =
                        blockStart +
                        start;

                    ReadOnlySpan<uint> firstSpan =
                        firstResidues.AsSpan(
                            sourceStart,
                            count);

                    // The inverse P2 transform remains leased only
                    // while its valid convolution prefix is consumed.
                    ReadOnlySpan<uint> secondSpan =
                        transformedSecond.AsSpan(
                            sourceStart,
                            count);

                    Span<ulong> scratchSpan;

                    if (useInverseTailScratch)
                    {
                        scratchSpan =
                            MemoryMarshal.Cast<uint, ulong>(
                                transformedSecond.AsSpan(
                                    checked(
                                        inverseTailScratchStart +
                                        start * 2),
                                    checked(
                                        count * 2)));
                    }
                    else
                    {
                        scratchSpan =
                            crtScratch!.AsSpan(
                                start,
                                count);
                    }

                    for (int offset = 0;
                         offset < count;
                         offset++)
                    {
                        uint first =
                            firstSpan[offset];

                        uint reducedFirst =
                            first;

                        if (reducedFirst >= SecondModulus) reducedFirst -= SecondModulus;
                        if (reducedFirst >= SecondModulus) reducedFirst -= SecondModulus;
                        if (reducedFirst >= SecondModulus) reducedFirst -= SecondModulus;
                        if (reducedFirst >= SecondModulus) reducedFirst -= SecondModulus;

                        long difference =
                            (long)secondSpan[offset] -
                            reducedFirst;

                        if (difference < 0)
                        {
                            difference +=
                                SecondModulus;
                        }

                        // Preserve the scalar modulo expression that
                        // won the previous benchmark experiments.
                        ulong multiplier =
                            (ulong)difference *
                            FirstModulusInverseInSecond %
                            SecondModulus;

                        scratchSpan[offset] =
                            first +
                            (ulong)FirstModulus *
                            multiplier;
                    }
                });

            crtTicks +=
                Stopwatch.GetTimestamp() -
                crtStarted;

            long carryStarted =
                Stopwatch.GetTimestamp();

            ReadOnlySpan<ulong> source;

            if (useInverseTailScratch)
            {
                source =
                    MemoryMarshal.Cast<uint, ulong>(
                        transformedSecond.AsSpan(
                            inverseTailScratchStart,
                            checked(
                                blockCount * 2)));
            }
            else
            {
                source =
                    crtScratch!.AsSpan(
                        0,
                        blockCount);
            }

            // The CRT values for this block are now fully detached
            // from P1. Reuse the corresponding P1 storage in place as
            // the normalized base-10,000 result, avoiding another
            // coefficientCount-sized uint[] allocation.
            Span<uint> destination =
                firstResidues.AsSpan(
                    blockStart,
                    blockCount);

            for (int offset = 0;
                 offset < blockCount;
                 offset++)
            {
                int coefficientIndex =
                    blockStart +
                    offset;

                if ((coefficientIndex & 0xFFFF) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                ulong value =
                    source[offset] +
                    carry;

                ulong quotient =
                    value /
                    LimbBase;

                destination[offset] =
                    (uint)(value -
                           quotient *
                           LimbBase);

                carry =
                    quotient;
            }

            carryTicks +=
                Stopwatch.GetTimestamp() -
                carryStarted;
        }

        trailingCarry =
            carry;

        diagnostics.CrtTicks +=
            crtTicks;

        diagnostics.CarryTicks +=
            carryTicks;
            

        return trailingCarry;
    }

    private static void ConvolveModulusCore(
        uint[] left,
        int leftOffset,
        int leftLength,
        uint[] right,
        int rightOffset,
        int rightLength,
        int transformLength,
        uint modulus,
        uint primitiveRoot,
        bool isSquare,
        bool compactFinalInverseOutput,
        uint[]? compactInverseOutputDestination,
        FixedWorkerTeam workers,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken,
        Action<uint[], uint[]?> consumeInverseTransform)
    {
        int fusedNttBlockLength =
            SelectFusedNttBlockLength();

        int l2NttTileLength =
            SelectL2NttTileLength(
                fusedNttBlockLength);

        int l3NttTileLength =
            SelectL3NttTileLength(
                l2NttTileLength);

        uint[] transformedLeft =
            workers.RentNttBuffer(
                transformLength,
                out _);

        try
        {
            PrepareNttBuffer(
                transformedLeft,
                left,
                leftOffset,
                leftLength);

            NttTwiddlePlan twiddlePlan =
                workers.GetTwiddlePlan(
                    modulus);

            ForwardDifTransform(
                transformedLeft,
                modulus,
                primitiveRoot,
                workers,
                twiddlePlan,
                fusedNttBlockLength,
                l2NttTileLength,
                l3NttTileLength,
                true,
                diagnostics,
                cancellationToken);

            if (isSquare)
            {
                long pointwiseStarted =
                    Stopwatch.GetTimestamp();

                ExecuteRanges(
                    transformLength,
                    workers,
                    cancellationToken,
                    (start, end) =>
                    {
                        for (int index = start;
                             index < end;
                             index++)
                        {
                            ulong value =
                                transformedLeft[index];

                            transformedLeft[index] =
                                (uint)(value *
                                       value %
                                       modulus);
                        }
                    });

                diagnostics.PointwiseTicks +=
                    Stopwatch.GetTimestamp() -
                    pointwiseStarted;
            }
            else
            {
                MultiplyForwardRightSpectrumInPlace(
                    transformedLeft,
                    right,
                    rightOffset,
                    rightLength,
                    transformLength,
                    modulus,
                    primitiveRoot,
                    workers,
                    twiddlePlan,
                    fusedNttBlockLength,
                    l2NttTileLength,
                    l3NttTileLength,
                    diagnostics,
                    cancellationToken);
            }

            int validOutputLength =
                checked(
                    leftLength +
                    rightLength -
                    1);

            uint[]? compactInverseOutput =
                InverseDitTransform(
                    transformedLeft,
                    modulus,
                    primitiveRoot,
                    validOutputLength,
                    compactFinalInverseOutput,
                    compactInverseOutputDestination,
                    workers,
                    twiddlePlan,
                    fusedNttBlockLength,
                    l2NttTileLength,
                    l3NttTileLength,
                    diagnostics,
                    cancellationToken);

            consumeInverseTransform(
                transformedLeft,
                compactInverseOutput);
        }
        finally
        {
            workers.ReturnNttBuffer(
                transformedLeft);
        }
    }

    /// <summary>
    /// Builds the right-hand forward spectrum, multiplies it into the already
    /// transformed left spectrum, and returns the right workspace before this
    /// helper returns. Keeping that lease in a separate method makes its GC
    /// lifetime structurally end before inverse DIT / CRT consumption rather
    /// than relying on a nullable local in ConvolveModulusCore. Arithmetic and
    /// worker scheduling are unchanged.
    /// </summary>
    private static void MultiplyForwardRightSpectrumInPlace(
        uint[] transformedLeft,
        uint[] right,
        int rightOffset,
        int rightLength,
        int transformLength,
        uint modulus,
        uint primitiveRoot,
        FixedWorkerTeam workers,
        NttTwiddlePlan twiddlePlan,
        int fusedNttBlockLength,
        int l2NttTileLength,
        int l3NttTileLength,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        uint[] rightTransform =
            workers.RentNttBuffer(
                transformLength,
                out _);

        try
        {
            PrepareNttBuffer(
                rightTransform,
                right,
                rightOffset,
                rightLength);

            ForwardDifTransform(
                rightTransform,
                modulus,
                primitiveRoot,
                workers,
                twiddlePlan,
                fusedNttBlockLength,
                l2NttTileLength,
                l3NttTileLength,
                false,
                diagnostics,
                cancellationToken);

            long pointwiseStarted =
                Stopwatch.GetTimestamp();

            ExecuteRanges(
                transformLength,
                workers,
                cancellationToken,
                (start, end) =>
                {
                    for (int index = start;
                         index < end;
                         index++)
                    {
                        transformedLeft[index] =
                            (uint)((ulong)transformedLeft[index] *
                                   rightTransform[index] %
                                   modulus);
                    }
                });

            diagnostics.PointwiseTicks +=
                Stopwatch.GetTimestamp() -
                pointwiseStarted;
        }
        finally
        {
            // Right is dead immediately after the pointwise pass. Return it
            // before inverse DIT so another modulus/branch can reuse the same
            // transform array while only transformedLeft remains leased here.
            workers.ReturnNttBuffer(
                rightTransform);
        }
    }

    /// <summary>
    /// Initializes a rented NTT workspace from compact base-10,000 limbs. v32
    /// keeps v31's uninitialized transform allocation, so both fresh and reused
    /// buffers follow the same rule: clear only the zero-padding tail and then
    /// overwrite the complete source prefix. This avoids a redundant CLR
    /// zero-fill of the source prefix on 128-256 MiB fresh workspaces.
    /// </summary>
    private static void PrepareNttBuffer(
        uint[] destination,
        uint[] source,
        int sourceOffset,
        int sourceLength)
    {
        Debug.Assert(
            sourceOffset >= 0 &&
            sourceLength > 0 &&
            sourceOffset <= source.Length - sourceLength);

        Debug.Assert(
            destination.Length >=
            sourceLength);

        if (sourceLength <
            destination.Length)
        {
            Array.Clear(
                destination,
                sourceLength,
                destination.Length -
                sourceLength);
        }

        Array.Copy(
            source,
            sourceOffset,
            destination,
            0,
            sourceLength);
    }

    /// <summary>
    /// Forward decimation-in-frequency transform. Natural-order input becomes
    /// bit-reversed output. The following pointwise product intentionally stays
    /// in that order, so no permutation pass is required.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ForwardDifTransform(
        uint[] values,
        uint modulus,
        uint primitiveRoot,
        FixedWorkerTeam workers,
        NttTwiddlePlan twiddlePlan,
        int fusedNttBlockLength,
        int l2NttTileLength,
        int l3NttTileLength,
        bool buildCachedTwiddles,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        int length =
            values.Length;

        long transformStarted =
            Stopwatch.GetTimestamp();

        for (int stageLength = length;
             stageLength >= 2;
             stageLength >>= 1)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int halfLength =
                stageLength >> 1;

            // Fuse two cached global DIF stages into one memory pass whenever
            // the second stage still lies above the LLC blocking boundary.
            // Four quarter streams are consumed together, so the second stage
            // reuses values that are already in the core/cache instead of
            // sweeping the complete transform again.  The actual butterfly
            // arithmetic remains identical to the proven scalar v16 path.
            int nextStageLength =
                stageLength >> 1;

            if (nextStageLength > l3NttTileLength &&
                CanFuseForwardCachedStagePair(
                    length,
                    stageLength,
                    twiddlePlan,
                    workers.WorkerCount,
                    buildCachedTwiddles))
            {
                int firstTwiddleOffset =
                    EnsureCachedStageTwiddles(
                        twiddlePlan,
                        primitiveRoot,
                        modulus,
                        stageLength,
                        workers,
                        cancellationToken);

                int secondTwiddleOffset =
                    EnsureCachedStageTwiddles(
                        twiddlePlan,
                        primitiveRoot,
                        modulus,
                        nextStageLength,
                        workers,
                        cancellationToken);

                ExecuteForwardCachedStagePairByGroups(
                    values,
                    modulus,
                    twiddlePlan.ForwardTwiddles,
                    firstTwiddleOffset,
                    secondTwiddleOffset,
                    stageLength,
                    workers,
                    cancellationToken);

                // The for-loop update performs the second shift, thereby
                // skipping the stage already completed by the fused kernel.
                stageLength >>= 1;
                continue;
            }

            // Enter the last-level-cache hierarchy first.  Once DIF reaches
            // l3NttTileLength, all remaining butterflies are independent inside
            // each LLC tile, so complete L3 -> L2 -> L1 locally and avoid the
            // corresponding whole-array sweeps and inter-stage barriers.
            if (stageLength == l3NttTileLength &&
                CanUseL3CacheBlocking(
                    length,
                    l3NttTileLength,
                    workers.WorkerCount))
            {
                if (buildCachedTwiddles)
                {
                    PrepareFusedTwiddleTables(
                        twiddlePlan,
                        primitiveRoot,
                        modulus,
                        l3NttTileLength,
                        workers,
                        cancellationToken);
                }

                ExecuteForwardL3CacheBlockedTail(
                    values,
                    modulus,
                    workers,
                    twiddlePlan,
                    fusedNttBlockLength,
                    l2NttTileLength,
                    l3NttTileLength,
                    cancellationToken);

                break;
            }

            // First cache-block at L2 granularity when the transform has
            // enough independent tiles to keep the complete worker team busy.
            // All stages from the tile length down to the L1 fused block then
            // remain local to one tile, avoiding several full-array sweeps.
            if (stageLength == l2NttTileLength &&
                CanUseL2CacheBlocking(
                    length,
                    l2NttTileLength,
                    workers.WorkerCount))
            {
                if (buildCachedTwiddles)
                {
                    PrepareFusedTwiddleTables(
                        twiddlePlan,
                        primitiveRoot,
                        modulus,
                        l2NttTileLength,
                        workers,
                        cancellationToken);
                }

                ExecuteForwardL2CacheBlockedTail(
                    values,
                    modulus,
                    workers,
                    twiddlePlan,
                    fusedNttBlockLength,
                    l2NttTileLength,
                    cancellationToken);

                break;
            }

            // From this point down, every remaining DIF stage operates only
            // inside an independent adaptive fused block.  Fuse the tail
            // so each worker keeps one block hot in L1/L2 rather than sweeping
            // the entire transform once per stage.  This remains the fallback
            // for transforms too small to expose enough L2 tiles.
            if (length > fusedNttBlockLength &&
                stageLength == fusedNttBlockLength)
            {
                if (buildCachedTwiddles)
                {
                    PrepareFusedTwiddleTables(
                        twiddlePlan,
                        primitiveRoot,
                        modulus,
                        fusedNttBlockLength,
                        workers,
                        cancellationToken);
                }

                ExecuteForwardFusedTail(
                    values,
                    modulus,
                    workers,
                    twiddlePlan,
                    fusedNttBlockLength,
                    cancellationToken);

                break;
            }

            // The length-2 stage contains only the twiddle 1.  It is also the
            // stage with the largest group count, so bypass the generic
            // segment-to-group mapping completely and walk adjacent pairs.
            if (stageLength == 2)
            {
                ExecuteLengthTwoButterflies(
                    values,
                    modulus,
                    normalize: false,
                    inverseLength: 0,
                    workers,
                    cancellationToken);

                continue;
            }

            int groupCount =
                length /
                stageLength;

            bool useTwiddleCache =
                groupCount >= 2 &&
                twiddlePlan.CanCache(
                    halfLength);

            int twiddleOffset =
                useTwiddleCache
                    ? twiddlePlan.GetOffset(
                        halfLength)
                    : 0;

            bool needTwiddleBuild =
                useTwiddleCache &&
                buildCachedTwiddles &&
                !twiddlePlan.IsStageReady(
                    halfLength);

            uint root = 0;

            if (!useTwiddleCache ||
                needTwiddleBuild)
            {
                root =
                    (uint)ModPow(
                        primitiveRoot,
                        (modulus - 1u) /
                        (uint)stageLength,
                        modulus);
            }

            if (needTwiddleBuild)
            {
                BuildTwiddleTables(
                    twiddlePlan.ForwardTwiddles,
                    twiddlePlan.InverseTwiddles,
                    twiddleOffset,
                    halfLength,
                    root,
                    modulus,
                    workers,
                    cancellationToken);

                twiddlePlan.MarkStageReady(
                    halfLength);
            }

            int segmentsPerGroup =
                GetSegmentsPerGroup(
                    halfLength,
                    groupCount,
                    workers.WorkerCount);

            // Once there is at least one complete group per worker, keep the
            // global-stage traversal group-native.  The generic segment mapper
            // is valuable only while one huge group must be split across
            // multiple workers; after that it adds divisions/branches around
            // an otherwise contiguous memory walk.
            if (useTwiddleCache &&
                segmentsPerGroup == 1)
            {
                ExecuteForwardCachedStageByGroups(
                    values,
                    modulus,
                    twiddlePlan.ForwardTwiddles,
                    twiddleOffset,
                    stageLength,
                    halfLength,
                    groupCount,
                    workers,
                    cancellationToken);

                continue;
            }

            ExecuteRanges(
                checked(groupCount * segmentsPerGroup),
                workers,
                cancellationToken,
                (segmentStart, segmentEnd) =>
                {
                    for (int segmentIndex = segmentStart;
                         segmentIndex < segmentEnd;
                         segmentIndex++)
                    {
                        GetSegmentBounds(
                            segmentIndex,
                            segmentsPerGroup,
                            halfLength,
                            out int groupIndex,
                            out int butterflyStart,
                            out int butterflyEnd);

                        int groupOffset =
                            groupIndex *
                            stageLength;

                        int butterfly =
                            butterflyStart;

                        if (butterfly == 0 &&
                            butterfly < butterflyEnd)
                        {
                            int leftIndex =
                                groupOffset;

                            int rightIndex =
                                leftIndex +
                                halfLength;

                            uint leftValue =
                                values[leftIndex];

                            uint rightValue =
                                values[rightIndex];

                            uint sum =
                                leftValue +
                                rightValue;

                            if (sum >= modulus)
                            {
                                sum -= modulus;
                            }

                            uint difference =
                                leftValue >= rightValue
                                    ? leftValue - rightValue
                                    : leftValue + modulus - rightValue;

                            values[leftIndex] =
                                sum;

                            values[rightIndex] =
                                difference;

                            butterfly = 1;
                        }

                        if (butterfly >= butterflyEnd)
                        {
                            continue;
                        }

                        if (useTwiddleCache)
                        {
                            uint[] twiddles =
                                twiddlePlan.ForwardTwiddles;

                            for (;
                                 butterfly < butterflyEnd;
                                 butterfly++)
                            {
                                int leftIndex =
                                    groupOffset +
                                    butterfly;

                                int rightIndex =
                                    leftIndex +
                                    halfLength;

                                uint leftValue =
                                    values[leftIndex];

                                uint rightValue =
                                    values[rightIndex];

                                uint sum =
                                    leftValue +
                                    rightValue;

                                if (sum >= modulus)
                                {
                                    sum -= modulus;
                                }

                                uint difference =
                                    leftValue >= rightValue
                                        ? leftValue - rightValue
                                        : leftValue + modulus - rightValue;

                                values[leftIndex] =
                                    sum;

                                values[rightIndex] =
                                    (uint)((ulong)difference *
                                           twiddles[twiddleOffset + butterfly] %
                                           modulus);

                                if (((butterfly - butterflyStart) &
                                     0x7FFF) == 0x7FFF)
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                }
                            }
                        }
                        else
                        {
                            ulong twiddle =
                                butterfly == 1 &&
                                butterflyStart == 0
                                    ? root
                                    : ModPow(
                                        root,
                                        (uint)butterfly,
                                        modulus);

                            for (;
                                 butterfly < butterflyEnd;
                                 butterfly++)
                            {
                                int leftIndex =
                                    groupOffset +
                                    butterfly;

                                int rightIndex =
                                    leftIndex +
                                    halfLength;

                                uint leftValue =
                                    values[leftIndex];

                                uint rightValue =
                                    values[rightIndex];

                                uint sum =
                                    leftValue +
                                    rightValue;

                                if (sum >= modulus)
                                {
                                    sum -= modulus;
                                }

                                uint difference =
                                    leftValue >= rightValue
                                        ? leftValue - rightValue
                                        : leftValue + modulus - rightValue;

                                values[leftIndex] =
                                    sum;

                                values[rightIndex] =
                                    (uint)((ulong)difference *
                                           twiddle %
                                           modulus);

                                if (butterfly + 1 < butterflyEnd)
                                {
                                    twiddle =
                                        twiddle *
                                        root %
                                        modulus;
                                }

                                if (((butterfly - butterflyStart) &
                                     0x7FFF) == 0x7FFF)
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                }
                            }
                        }
                    }
                });
        }

        diagnostics.ForwardTransformTicks +=
            Stopwatch.GetTimestamp() -
            transformStarted;
    }

    /// <summary>
    /// Inverse decimation-in-time transform. It consumes the bit-reversed
    /// pointwise product emitted by the forward DIF transform and returns
    /// natural-order coefficients, again without a permutation pass.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static uint[]? InverseDitTransform(
        uint[] values,
        uint modulus,
        uint primitiveRoot,
        int validOutputLength,
        bool compactFinalOutput,
        uint[]? compactOutputDestination,
        FixedWorkerTeam workers,
        NttTwiddlePlan twiddlePlan,
        int fusedNttBlockLength,
        int l2NttTileLength,
        int l3NttTileLength,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        int length =
            values.Length;

        long transformStarted =
            Stopwatch.GetTimestamp();

        Debug.Assert(
            validOutputLength > 0 &&
            validOutputLength <= length);

        uint[]? compactOutput =
            null;

        long excludedAllocationTicks =
            0;

        uint inverseLength =
            (uint)ModPow(
                (uint)length,
                modulus - 2u,
                modulus);

        uint inversePrimitiveRoot = 0;
        bool inversePrimitiveRootReady =
            false;

        int firstStageLength = 2;

        // DIT starts with the smallest stages.  Prefer the deepest available
        // cache hierarchy: complete L1 and L2 work inside each L3 tile, merge
        // those subtiles through the LLC-local stages, then resume the global
        // stages.  Smaller transforms retain the proven v9 L2/L1 fallbacks.
        if (CanUseL3CacheBlocking(
                length,
                l3NttTileLength,
                workers.WorkerCount))
        {
            ExecuteInverseL3CacheBlockedHead(
                values,
                modulus,
                workers,
                twiddlePlan,
                fusedNttBlockLength,
                l2NttTileLength,
                l3NttTileLength,
                cancellationToken);

            firstStageLength =
                l3NttTileLength << 1;
        }
        else if (CanUseL2CacheBlocking(
                     length,
                     l2NttTileLength,
                     workers.WorkerCount))
        {
            ExecuteInverseL2CacheBlockedHead(
                values,
                modulus,
                workers,
                twiddlePlan,
                fusedNttBlockLength,
                l2NttTileLength,
                cancellationToken);

            firstStageLength =
                l2NttTileLength << 1;
        }
        else if (length > fusedNttBlockLength)
        {
            ExecuteInverseFusedHead(
                values,
                modulus,
                workers,
                twiddlePlan,
                fusedNttBlockLength,
                cancellationToken);

            firstStageLength =
                fusedNttBlockLength << 1;
        }

        for (int stageLength = firstStageLength;
             stageLength <= length;
             stageLength <<= 1)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int halfLength =
                stageLength >> 1;

            bool normalizeOutput =
                stageLength == length;

            // DIT counterpart of the two-stage DIF fusion.  Only cached
            // non-final global stages are paired, so normalization remains on
            // the original final-stage path.  Completing S and 2S together
            // cuts one whole-array pass and one worker-team barrier.
            int nextStageLength =
                stageLength << 1;

            if (!normalizeOutput &&
                nextStageLength < length &&
                CanFuseInverseCachedStagePair(
                    length,
                    stageLength,
                    twiddlePlan,
                    workers.WorkerCount))
            {
                ExecuteInverseCachedStagePairByGroups(
                    values,
                    modulus,
                    twiddlePlan.InverseTwiddles,
                    twiddlePlan.GetOffset(
                        halfLength),
                    twiddlePlan.GetOffset(
                        stageLength),
                    stageLength,
                    workers,
                    cancellationToken);

                // Skip the second stage consumed by the fused DIT kernel.
                stageLength <<= 1;
                continue;
            }

            // v33: the last DIT stage is the only stage whose outputs are
            // externally consumed.  Linear convolution needs only the
            // [0, validOutputLength) prefix.  P1 writes that prefix directly
            // into its compact result backing; P2 stays in-place but skips
            // normalization/stores for the dead tail that CRT immediately
            // reuses as scratch.  Worker partitioning and butterfly arithmetic
            // for every valid coefficient are unchanged.
            if (normalizeOutput &&
                (compactFinalOutput ||
                 validOutputLength < length))
            {
                uint[] finalOutput;

                if (compactFinalOutput)
                {
                    if (compactOutputDestination is not null)
                    {
                        if (compactOutputDestination.Length <
                            validOutputLength + 1)
                        {
                            throw new InvalidOperationException(
                                "The supplied compact inverse output buffer is too small.");
                        }

                        compactOutput =
                            compactOutputDestination;
                    }
                    else
                    {
                        long allocationStarted =
                            Stopwatch.GetTimestamp();

                        // One spare slot is reserved for the only possible final
                        // base-10,000 carry. The final DIT writes every logical
                        // prefix element, so zero initialization is unnecessary.
                        compactOutput =
                            GC.AllocateUninitializedArray<uint>(
                                checked(
                                    validOutputLength + 1));

                        excludedAllocationTicks +=
                            Stopwatch.GetTimestamp() -
                            allocationStarted;
                    }

                    finalOutput =
                        compactOutput;
                }
                else
                {
                    finalOutput =
                        values;
                }

                if (!inversePrimitiveRootReady)
                {
                    inversePrimitiveRoot =
                        (uint)ModPow(
                            primitiveRoot,
                            modulus - 2u,
                            modulus);

                    inversePrimitiveRootReady =
                        true;
                }

                uint finalRoot =
                    (uint)ModPow(
                        inversePrimitiveRoot,
                        (modulus - 1u) /
                        (uint)length,
                        modulus);

                ExecuteFinalInversePrefix(
                    values,
                    finalOutput,
                    validOutputLength,
                    modulus,
                    finalRoot,
                    inverseLength,
                    workers,
                    cancellationToken);

                continue;
            }

            if (stageLength == 2)
            {
                ExecuteLengthTwoButterflies(
                    values,
                    modulus,
                    normalizeOutput,
                    inverseLength,
                    workers,
                    cancellationToken);

                continue;
            }

            int groupCount =
                length /
                stageLength;

            bool useTwiddleCache =
                groupCount >= 2 &&
                twiddlePlan.CanCache(
                    halfLength);

            int twiddleOffset =
                useTwiddleCache
                    ? twiddlePlan.GetOffset(
                        halfLength)
                    : 0;

            uint root = 0;

            if (!useTwiddleCache)
            {
                if (!inversePrimitiveRootReady)
                {
                    inversePrimitiveRoot =
                        (uint)ModPow(
                            primitiveRoot,
                            modulus - 2u,
                            modulus);

                    inversePrimitiveRootReady =
                        true;
                }

                root =
                    (uint)ModPow(
                        inversePrimitiveRoot,
                        (modulus - 1u) /
                        (uint)stageLength,
                        modulus);
            }

            int segmentsPerGroup =
                GetSegmentsPerGroup(
                    halfLength,
                    groupCount,
                    workers.WorkerCount);

            if (useTwiddleCache &&
                segmentsPerGroup == 1)
            {
                ExecuteInverseCachedStageByGroups(
                    values,
                    modulus,
                    twiddlePlan.InverseTwiddles,
                    twiddleOffset,
                    stageLength,
                    halfLength,
                    groupCount,
                    workers,
                    cancellationToken);

                continue;
            }

            ExecuteRanges(
                checked(groupCount * segmentsPerGroup),
                workers,
                cancellationToken,
                (segmentStart, segmentEnd) =>
                {
                    for (int segmentIndex = segmentStart;
                         segmentIndex < segmentEnd;
                         segmentIndex++)
                    {
                        GetSegmentBounds(
                            segmentIndex,
                            segmentsPerGroup,
                            halfLength,
                            out int groupIndex,
                            out int butterflyStart,
                            out int butterflyEnd);

                        int groupOffset =
                            groupIndex *
                            stageLength;

                        int butterfly =
                            butterflyStart;

                        if (butterfly == 0 &&
                            butterfly < butterflyEnd)
                        {
                            int leftIndex =
                                groupOffset;

                            int rightIndex =
                                leftIndex +
                                halfLength;

                            uint leftValue =
                                values[leftIndex];

                            uint rightValue =
                                values[rightIndex];

                            uint sum =
                                leftValue +
                                rightValue;

                            if (sum >= modulus)
                            {
                                sum -= modulus;
                            }

                            uint difference =
                                leftValue >= rightValue
                                    ? leftValue - rightValue
                                    : leftValue + modulus - rightValue;

                            if (normalizeOutput)
                            {
                                values[leftIndex] =
                                    (uint)((ulong)sum *
                                           inverseLength %
                                           modulus);

                                values[rightIndex] =
                                    (uint)((ulong)difference *
                                           inverseLength %
                                           modulus);
                            }
                            else
                            {
                                values[leftIndex] =
                                    sum;

                                values[rightIndex] =
                                    difference;
                            }

                            butterfly = 1;
                        }

                        if (butterfly >= butterflyEnd)
                        {
                            continue;
                        }

                        if (useTwiddleCache)
                        {
                            uint[] twiddles =
                                twiddlePlan.InverseTwiddles;

                            for (;
                                 butterfly < butterflyEnd;
                                 butterfly++)
                            {
                                int leftIndex =
                                    groupOffset +
                                    butterfly;

                                int rightIndex =
                                    leftIndex +
                                    halfLength;

                                uint leftValue =
                                    values[leftIndex];

                                uint rightValue =
                                    (uint)((ulong)values[rightIndex] *
                                           twiddles[twiddleOffset + butterfly] %
                                           modulus);

                                uint sum =
                                    leftValue +
                                    rightValue;

                                if (sum >= modulus)
                                {
                                    sum -= modulus;
                                }

                                uint difference =
                                    leftValue >= rightValue
                                        ? leftValue - rightValue
                                        : leftValue + modulus - rightValue;

                                if (normalizeOutput)
                                {
                                    values[leftIndex] =
                                        (uint)((ulong)sum *
                                               inverseLength %
                                               modulus);

                                    values[rightIndex] =
                                        (uint)((ulong)difference *
                                               inverseLength %
                                               modulus);
                                }
                                else
                                {
                                    values[leftIndex] =
                                        sum;

                                    values[rightIndex] =
                                        difference;
                                }

                                if (((butterfly - butterflyStart) &
                                     0x7FFF) == 0x7FFF)
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                }
                            }
                        }
                        else
                        {
                            ulong twiddle =
                                butterfly == 1 &&
                                butterflyStart == 0
                                    ? root
                                    : ModPow(
                                        root,
                                        (uint)butterfly,
                                        modulus);

                            for (;
                                 butterfly < butterflyEnd;
                                 butterfly++)
                            {
                                int leftIndex =
                                    groupOffset +
                                    butterfly;

                                int rightIndex =
                                    leftIndex +
                                    halfLength;

                                uint leftValue =
                                    values[leftIndex];

                                uint rightValue =
                                    (uint)((ulong)values[rightIndex] *
                                           twiddle %
                                           modulus);

                                uint sum =
                                    leftValue +
                                    rightValue;

                                if (sum >= modulus)
                                {
                                    sum -= modulus;
                                }

                                uint difference =
                                    leftValue >= rightValue
                                        ? leftValue - rightValue
                                        : leftValue + modulus - rightValue;

                                if (normalizeOutput)
                                {
                                    values[leftIndex] =
                                        (uint)((ulong)sum *
                                               inverseLength %
                                               modulus);

                                    values[rightIndex] =
                                        (uint)((ulong)difference *
                                               inverseLength %
                                               modulus);
                                }
                                else
                                {
                                    values[leftIndex] =
                                        sum;

                                    values[rightIndex] =
                                        difference;
                                }

                                if (butterfly + 1 < butterflyEnd)
                                {
                                    twiddle =
                                        twiddle *
                                        root %
                                        modulus;
                                }

                                if (((butterfly - butterflyStart) &
                                     0x7FFF) == 0x7FFF)
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                }
                            }
                        }
                    }
                });
        }

        diagnostics.InverseTransformTicks +=
            Stopwatch.GetTimestamp() -
            transformStarted -
            excludedAllocationTicks;

        return compactOutput;
    }

    /// <summary>
    /// Executes only the final inverse-DIT stage and materializes the valid
    /// linear-convolution prefix.  The first half of the final stage is always
    /// valid because transformLength is the smallest power of two that covers
    /// coefficientCount; only the upper-half suffix can be dead.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteFinalInversePrefix(
        uint[] values,
        uint[] output,
        int validOutputLength,
        uint modulus,
        uint root,
        uint inverseLength,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        int length =
            values.Length;

        int halfLength =
            length >> 1;

        Debug.Assert(
            validOutputLength > halfLength &&
            validOutputLength <= length);

        Debug.Assert(
            output.Length >= validOutputLength);

        int validRightCount =
            validOutputLength -
            halfLength;

        // Small transforms keep the exact v33 scalar final-stage path. The
        // four-lane kernel is intentionally enabled only at the same adaptive
        // threshold that already proved worthwhile for the global NTT path.
        if (halfLength < AdaptiveFourWayHalfLength)
        {
            ExecuteFinalInversePrefixScalar(
                values,
                output,
                halfLength,
                validRightCount,
                modulus,
                root,
                inverseLength,
                workers,
                cancellationToken);

            return;
        }

        // v34: the final inverse prefix has one monotonic boundary: below
        // validRightCount both the left and right outputs are live; above it
        // only the left output is externally observable. Split each worker's
        // contiguous range at that boundary once instead of testing it inside
        // every butterfly. Four independent twiddle lanes then advance by
        // root^4, breaking the long scalar twiddle dependency chain while
        // preserving exactly the same DIT arithmetic and worker partitioning.
        uint rootSquared =
            (uint)((ulong)root *
                   root %
                   modulus);

        uint rootFourth =
            (uint)((ulong)rootSquared *
                   rootSquared %
                   modulus);

        ExecuteRanges(
            halfLength,
            workers,
            cancellationToken,
            (butterflyStart, butterflyEnd) =>
            {
                int bothEnd =
                    Math.Min(
                        butterflyEnd,
                        validRightCount);

                if (butterflyStart < bothEnd)
                {
                    ExecuteFinalInverseBothOutputsRange(
                        values,
                        output,
                        halfLength,
                        butterflyStart,
                        bothEnd,
                        modulus,
                        root,
                        rootSquared,
                        rootFourth,
                        inverseLength,
                        cancellationToken);
                }

                int leftOnlyStart =
                    Math.Max(
                        butterflyStart,
                        validRightCount);

                if (leftOnlyStart < butterflyEnd)
                {
                    ExecuteFinalInverseLeftOnlyRange(
                        values,
                        output,
                        halfLength,
                        leftOnlyStart,
                        butterflyEnd,
                        modulus,
                        root,
                        rootSquared,
                        rootFourth,
                        inverseLength,
                        cancellationToken);
                }
            });
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteFinalInversePrefixScalar(
        uint[] values,
        uint[] output,
        int halfLength,
        int validRightCount,
        uint modulus,
        uint root,
        uint inverseLength,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        ExecuteRanges(
            halfLength,
            workers,
            cancellationToken,
            (butterflyStart, butterflyEnd) =>
            {
                int butterfly =
                    butterflyStart;

                if (butterfly == 0 &&
                    butterfly < butterflyEnd)
                {
                    uint leftValue =
                        values[0];

                    uint rightValue =
                        values[halfLength];

                    uint sum =
                        leftValue +
                        rightValue;

                    if (sum >= modulus)
                    {
                        sum -= modulus;
                    }

                    uint difference =
                        leftValue >= rightValue
                            ? leftValue - rightValue
                            : leftValue + modulus - rightValue;

                    output[0] =
                        (uint)((ulong)sum *
                               inverseLength %
                               modulus);

                    if (validRightCount > 0)
                    {
                        output[halfLength] =
                            (uint)((ulong)difference *
                                   inverseLength %
                                   modulus);
                    }

                    butterfly = 1;
                }

                if (butterfly >= butterflyEnd)
                {
                    return;
                }

                ulong twiddle =
                    butterfly == 1
                        ? root
                        : ModPow(
                            root,
                            (uint)butterfly,
                            modulus);

                for (;
                     butterfly < butterflyEnd;
                     butterfly++)
                {
                    int rightIndex =
                        butterfly +
                        halfLength;

                    uint leftValue =
                        values[butterfly];

                    uint rightValue =
                        (uint)((ulong)values[rightIndex] *
                               twiddle %
                               modulus);

                    uint sum =
                        leftValue +
                        rightValue;

                    if (sum >= modulus)
                    {
                        sum -= modulus;
                    }

                    output[butterfly] =
                        (uint)((ulong)sum *
                               inverseLength %
                               modulus);

                    if (butterfly < validRightCount)
                    {
                        uint difference =
                            leftValue >= rightValue
                                ? leftValue - rightValue
                                : leftValue + modulus - rightValue;

                        output[rightIndex] =
                            (uint)((ulong)difference *
                                   inverseLength %
                                   modulus);
                    }

                    if (butterfly + 1 < butterflyEnd)
                    {
                        twiddle =
                            twiddle *
                            root %
                            modulus;
                    }

                    if (((butterfly - butterflyStart) &
                         0x7FFF) == 0x7FFF)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            });
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteFinalInverseBothOutputsRange(
        uint[] values,
        uint[] output,
        int halfLength,
        int start,
        int end,
        uint modulus,
        uint root,
        uint rootSquared,
        uint rootFourth,
        uint inverseLength,
        CancellationToken cancellationToken)
    {
        const int CancellationStride =
            1 << 15;

        int butterfly =
            start;

        ulong twiddle0 =
            butterfly == 0
                ? 1u
                : ModPow(
                    root,
                    (uint)butterfly,
                    modulus);

        ulong twiddle1 =
            twiddle0 *
            root %
            modulus;

        ulong twiddle2 =
            twiddle0 *
            rootSquared %
            modulus;

        ulong twiddle3 =
            twiddle2 *
            root %
            modulus;

        while (butterfly < end)
        {
            int chunkEnd =
                Math.Min(
                    end,
                    butterfly + CancellationStride);

            for (;
                 butterfly + 3 < chunkEnd;
                 butterfly += 4)
            {
                int rightIndex0 =
                    butterfly +
                    halfLength;

                uint left0 =
                    values[butterfly];

                uint left1 =
                    values[butterfly + 1];

                uint left2 =
                    values[butterfly + 2];

                uint left3 =
                    values[butterfly + 3];

                uint right0 =
                    (uint)((ulong)values[rightIndex0] *
                           twiddle0 %
                           modulus);

                uint right1 =
                    (uint)((ulong)values[rightIndex0 + 1] *
                           twiddle1 %
                           modulus);

                uint right2 =
                    (uint)((ulong)values[rightIndex0 + 2] *
                           twiddle2 %
                           modulus);

                uint right3 =
                    (uint)((ulong)values[rightIndex0 + 3] *
                           twiddle3 %
                           modulus);

                uint sum0 = left0 + right0;
                uint sum1 = left1 + right1;
                uint sum2 = left2 + right2;
                uint sum3 = left3 + right3;

                if (sum0 >= modulus) sum0 -= modulus;
                if (sum1 >= modulus) sum1 -= modulus;
                if (sum2 >= modulus) sum2 -= modulus;
                if (sum3 >= modulus) sum3 -= modulus;

                uint difference0 =
                    left0 >= right0
                        ? left0 - right0
                        : left0 + modulus - right0;

                uint difference1 =
                    left1 >= right1
                        ? left1 - right1
                        : left1 + modulus - right1;

                uint difference2 =
                    left2 >= right2
                        ? left2 - right2
                        : left2 + modulus - right2;

                uint difference3 =
                    left3 >= right3
                        ? left3 - right3
                        : left3 + modulus - right3;

                output[butterfly] =
                    (uint)((ulong)sum0 *
                           inverseLength %
                           modulus);

                output[butterfly + 1] =
                    (uint)((ulong)sum1 *
                           inverseLength %
                           modulus);

                output[butterfly + 2] =
                    (uint)((ulong)sum2 *
                           inverseLength %
                           modulus);

                output[butterfly + 3] =
                    (uint)((ulong)sum3 *
                           inverseLength %
                           modulus);

                output[rightIndex0] =
                    (uint)((ulong)difference0 *
                           inverseLength %
                           modulus);

                output[rightIndex0 + 1] =
                    (uint)((ulong)difference1 *
                           inverseLength %
                           modulus);

                output[rightIndex0 + 2] =
                    (uint)((ulong)difference2 *
                           inverseLength %
                           modulus);

                output[rightIndex0 + 3] =
                    (uint)((ulong)difference3 *
                           inverseLength %
                           modulus);

                twiddle0 =
                    twiddle0 *
                    rootFourth %
                    modulus;

                twiddle1 =
                    twiddle1 *
                    rootFourth %
                    modulus;

                twiddle2 =
                    twiddle2 *
                    rootFourth %
                    modulus;

                twiddle3 =
                    twiddle3 *
                    rootFourth %
                    modulus;
            }

            for (;
                 butterfly < chunkEnd;
                 butterfly++)
            {
                int rightIndex =
                    butterfly +
                    halfLength;

                uint leftValue =
                    values[butterfly];

                uint rightValue =
                    (uint)((ulong)values[rightIndex] *
                           twiddle0 %
                           modulus);

                uint sum =
                    leftValue +
                    rightValue;

                if (sum >= modulus)
                {
                    sum -= modulus;
                }

                uint difference =
                    leftValue >= rightValue
                        ? leftValue - rightValue
                        : leftValue + modulus - rightValue;

                output[butterfly] =
                    (uint)((ulong)sum *
                           inverseLength %
                           modulus);

                output[rightIndex] =
                    (uint)((ulong)difference *
                           inverseLength %
                           modulus);

                twiddle0 =
                    twiddle0 *
                    root %
                    modulus;
            }

            // The four-way loop leaves twiddle0 at root^butterfly.  If the
            // scalar cleanup ran, it advanced the same lane one step at a
            // time, so the next cancellation chunk can continue directly.
            twiddle1 =
                twiddle0 *
                root %
                modulus;

            twiddle2 =
                twiddle0 *
                rootSquared %
                modulus;

            twiddle3 =
                twiddle2 *
                root %
                modulus;

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteFinalInverseLeftOnlyRange(
        uint[] values,
        uint[] output,
        int halfLength,
        int start,
        int end,
        uint modulus,
        uint root,
        uint rootSquared,
        uint rootFourth,
        uint inverseLength,
        CancellationToken cancellationToken)
    {
        const int CancellationStride =
            1 << 15;

        int butterfly =
            start;

        ulong twiddle0 =
            butterfly == 0
                ? 1u
                : ModPow(
                    root,
                    (uint)butterfly,
                    modulus);

        ulong twiddle1 =
            twiddle0 *
            root %
            modulus;

        ulong twiddle2 =
            twiddle0 *
            rootSquared %
            modulus;

        ulong twiddle3 =
            twiddle2 *
            root %
            modulus;

        while (butterfly < end)
        {
            int chunkEnd =
                Math.Min(
                    end,
                    butterfly + CancellationStride);

            for (;
                 butterfly + 3 < chunkEnd;
                 butterfly += 4)
            {
                int rightIndex0 =
                    butterfly +
                    halfLength;

                uint left0 =
                    values[butterfly];

                uint left1 =
                    values[butterfly + 1];

                uint left2 =
                    values[butterfly + 2];

                uint left3 =
                    values[butterfly + 3];

                uint right0 =
                    (uint)((ulong)values[rightIndex0] *
                           twiddle0 %
                           modulus);

                uint right1 =
                    (uint)((ulong)values[rightIndex0 + 1] *
                           twiddle1 %
                           modulus);

                uint right2 =
                    (uint)((ulong)values[rightIndex0 + 2] *
                           twiddle2 %
                           modulus);

                uint right3 =
                    (uint)((ulong)values[rightIndex0 + 3] *
                           twiddle3 %
                           modulus);

                uint sum0 = left0 + right0;
                uint sum1 = left1 + right1;
                uint sum2 = left2 + right2;
                uint sum3 = left3 + right3;

                if (sum0 >= modulus) sum0 -= modulus;
                if (sum1 >= modulus) sum1 -= modulus;
                if (sum2 >= modulus) sum2 -= modulus;
                if (sum3 >= modulus) sum3 -= modulus;

                output[butterfly] =
                    (uint)((ulong)sum0 *
                           inverseLength %
                           modulus);

                output[butterfly + 1] =
                    (uint)((ulong)sum1 *
                           inverseLength %
                           modulus);

                output[butterfly + 2] =
                    (uint)((ulong)sum2 *
                           inverseLength %
                           modulus);

                output[butterfly + 3] =
                    (uint)((ulong)sum3 *
                           inverseLength %
                           modulus);

                twiddle0 =
                    twiddle0 *
                    rootFourth %
                    modulus;

                twiddle1 =
                    twiddle1 *
                    rootFourth %
                    modulus;

                twiddle2 =
                    twiddle2 *
                    rootFourth %
                    modulus;

                twiddle3 =
                    twiddle3 *
                    rootFourth %
                    modulus;
            }

            for (;
                 butterfly < chunkEnd;
                 butterfly++)
            {
                int rightIndex =
                    butterfly +
                    halfLength;

                uint leftValue =
                    values[butterfly];

                uint rightValue =
                    (uint)((ulong)values[rightIndex] *
                           twiddle0 %
                           modulus);

                uint sum =
                    leftValue +
                    rightValue;

                if (sum >= modulus)
                {
                    sum -= modulus;
                }

                output[butterfly] =
                    (uint)((ulong)sum *
                           inverseLength %
                           modulus);

                twiddle0 =
                    twiddle0 *
                    root %
                    modulus;
            }

            twiddle1 =
                twiddle0 *
                root %
                modulus;

            twiddle2 =
                twiddle0 *
                rootSquared %
                modulus;

            twiddle3 =
                twiddle2 *
                root %
                modulus;

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanFuseForwardCachedStagePair(
        int transformLength,
        int stageLength,
        NttTwiddlePlan twiddlePlan,
        int workerCount,
        bool buildCachedTwiddles)
    {
        int halfLength =
            stageLength >> 1;

        int nextStageLength =
            stageLength >> 1;

        int nextHalfLength =
            nextStageLength >> 1;

        int groupCount =
            transformLength /
            stageLength;

        int nextGroupCount =
            transformLength /
            nextStageLength;

        if (groupCount < Math.Max(1, workerCount) ||
            nextGroupCount < 2 ||
            !twiddlePlan.CanCache(halfLength) ||
            !twiddlePlan.CanCache(nextHalfLength))
        {
            return false;
        }

        // The first Forward transform is allowed to construct the two cached
        // stages.  Later transforms only fuse when both immutable tables have
        // already been published by that first pass.
        return buildCachedTwiddles ||
               (twiddlePlan.IsStageReady(halfLength) &&
                twiddlePlan.IsStageReady(nextHalfLength));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanFuseInverseCachedStagePair(
        int transformLength,
        int stageLength,
        NttTwiddlePlan twiddlePlan,
        int workerCount)
    {
        int halfLength =
            stageLength >> 1;

        int nextStageLength =
            stageLength << 1;

        int nextHalfLength =
            stageLength;

        int groupCount =
            transformLength /
            stageLength;

        int nextGroupCount =
            transformLength /
            nextStageLength;

        return groupCount >= 2 &&
               nextGroupCount >= Math.Max(1, workerCount) &&
               twiddlePlan.CanCache(halfLength) &&
               twiddlePlan.CanCache(nextHalfLength) &&
               twiddlePlan.IsStageReady(halfLength) &&
               twiddlePlan.IsStageReady(nextHalfLength);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int EnsureCachedStageTwiddles(
        NttTwiddlePlan twiddlePlan,
        uint primitiveRoot,
        uint modulus,
        int stageLength,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        int halfLength =
            stageLength >> 1;

        int twiddleOffset =
            twiddlePlan.GetOffset(
                halfLength);

        if (twiddlePlan.IsStageReady(
                halfLength))
        {
            return twiddleOffset;
        }

        uint root =
            (uint)ModPow(
                primitiveRoot,
                (modulus - 1u) /
                (uint)stageLength,
                modulus);

        BuildTwiddleTables(
            twiddlePlan.ForwardTwiddles,
            twiddlePlan.InverseTwiddles,
            twiddleOffset,
            halfLength,
            root,
            modulus,
            workers,
            cancellationToken);

        twiddlePlan.MarkStageReady(
            halfLength);

        return twiddleOffset;
    }

    /// <summary>
    /// Fuses two cached global DIF stages S and S/2.  For each butterfly index
    /// one worker consumes the four corresponding quarter streams, performs
    /// both stages while those values are live, and writes the final four
    /// outputs once.  Arithmetic is unchanged; one complete memory sweep and
    /// one stage barrier disappear.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteForwardCachedStagePairByGroups(
        uint[] values,
        uint modulus,
        uint[] twiddles,
        int firstTwiddleOffset,
        int secondTwiddleOffset,
        int stageLength,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        const int CancellationStride =
            1 << 15;

        int halfLength =
            stageLength >> 1;

        int quarterLength =
            halfLength >> 1;

        int groupCount =
            values.Length /
            stageLength;

        ExecuteRanges(
            groupCount,
            workers,
            cancellationToken,
            (groupStart, groupEnd) =>
            {
                for (int groupIndex = groupStart;
                     groupIndex < groupEnd;
                     groupIndex++)
                {
                    int index0 =
                        groupIndex *
                        stageLength;

                    int index1 =
                        index0 +
                        quarterLength;

                    int index2 =
                        index0 +
                        halfLength;

                    int index3 =
                        index2 +
                        quarterLength;

                    int firstTwiddleIndex0 =
                        firstTwiddleOffset;

                    int firstTwiddleIndex1 =
                        firstTwiddleOffset +
                        quarterLength;

                    int secondTwiddleIndex =
                        secondTwiddleOffset;

                    int remaining =
                        quarterLength;

                    while (remaining > 0)
                    {
                        int chunkLength =
                            Math.Min(
                                remaining,
                                CancellationStride);

                        int chunkEnd =
                            index0 +
                            chunkLength;

                        for (;
                             index0 < chunkEnd;
                             index0++,
                             index1++,
                             index2++,
                             index3++,
                             firstTwiddleIndex0++,
                             firstTwiddleIndex1++,
                             secondTwiddleIndex++)
                        {
                            uint value0 =
                                values[index0];

                            uint value1 =
                                values[index1];

                            uint value2 =
                                values[index2];

                            uint value3 =
                                values[index3];

                            uint topSum0 =
                                value0 + value2;

                            uint topSum1 =
                                value1 + value3;

                            if (topSum0 >= modulus)
                            {
                                topSum0 -= modulus;
                            }

                            if (topSum1 >= modulus)
                            {
                                topSum1 -= modulus;
                            }

                            uint topDifference0 =
                                value0 >= value2
                                    ? value0 - value2
                                    : value0 + modulus - value2;

                            uint topDifference1 =
                                value1 >= value3
                                    ? value1 - value3
                                    : value1 + modulus - value3;

                            uint lower0 =
                                (uint)((ulong)topDifference0 *
                                       twiddles[firstTwiddleIndex0] %
                                       modulus);

                            uint lower1 =
                                (uint)((ulong)topDifference1 *
                                       twiddles[firstTwiddleIndex1] %
                                       modulus);

                            uint upperSum =
                                topSum0 +
                                topSum1;

                            if (upperSum >= modulus)
                            {
                                upperSum -= modulus;
                            }

                            uint upperDifference =
                                topSum0 >= topSum1
                                    ? topSum0 - topSum1
                                    : topSum0 + modulus - topSum1;

                            uint lowerSum =
                                lower0 +
                                lower1;

                            if (lowerSum >= modulus)
                            {
                                lowerSum -= modulus;
                            }

                            uint lowerDifference =
                                lower0 >= lower1
                                    ? lower0 - lower1
                                    : lower0 + modulus - lower1;

                            uint secondTwiddle =
                                twiddles[secondTwiddleIndex];

                            values[index0] =
                                upperSum;

                            values[index1] =
                                (uint)((ulong)upperDifference *
                                       secondTwiddle %
                                       modulus);

                            values[index2] =
                                lowerSum;

                            values[index3] =
                                (uint)((ulong)lowerDifference *
                                       secondTwiddle %
                                       modulus);
                        }

                        remaining -=
                            chunkLength;

                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            });
    }

    /// <summary>
    /// Fuses cached inverse DIT stages S and 2S.  Two adjacent S-sized groups
    /// are completed and immediately merged while their four quarter streams
    /// are hot, matching the forward pair fusion in reverse.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteInverseCachedStagePairByGroups(
        uint[] values,
        uint modulus,
        uint[] twiddles,
        int firstTwiddleOffset,
        int secondTwiddleOffset,
        int stageLength,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        const int CancellationStride =
            1 << 15;

        int halfLength =
            stageLength >> 1;

        int parentLength =
            stageLength << 1;

        int parentCount =
            values.Length /
            parentLength;

        ExecuteRanges(
            parentCount,
            workers,
            cancellationToken,
            (parentStart, parentEnd) =>
            {
                for (int parentIndex = parentStart;
                     parentIndex < parentEnd;
                     parentIndex++)
                {
                    int index0 =
                        parentIndex *
                        parentLength;

                    int index1 =
                        index0 +
                        halfLength;

                    int index2 =
                        index0 +
                        stageLength;

                    int index3 =
                        index2 +
                        halfLength;

                    int firstTwiddleIndex =
                        firstTwiddleOffset;

                    int secondTwiddleIndex0 =
                        secondTwiddleOffset;

                    int secondTwiddleIndex1 =
                        secondTwiddleOffset +
                        halfLength;

                    int remaining =
                        halfLength;

                    while (remaining > 0)
                    {
                        int chunkLength =
                            Math.Min(
                                remaining,
                                CancellationStride);

                        int chunkEnd =
                            index0 +
                            chunkLength;

                        for (;
                             index0 < chunkEnd;
                             index0++,
                             index1++,
                             index2++,
                             index3++,
                             firstTwiddleIndex++,
                             secondTwiddleIndex0++,
                             secondTwiddleIndex1++)
                        {
                            uint value0 =
                                values[index0];

                            uint value1 =
                                values[index1];

                            uint value2 =
                                values[index2];

                            uint value3 =
                                values[index3];

                            uint firstTwiddle =
                                twiddles[firstTwiddleIndex];

                            uint right0 =
                                (uint)((ulong)value1 *
                                       firstTwiddle %
                                       modulus);

                            uint right1 =
                                (uint)((ulong)value3 *
                                       firstTwiddle %
                                       modulus);

                            uint firstSum0 =
                                value0 +
                                right0;

                            uint firstSum1 =
                                value2 +
                                right1;

                            if (firstSum0 >= modulus)
                            {
                                firstSum0 -= modulus;
                            }

                            if (firstSum1 >= modulus)
                            {
                                firstSum1 -= modulus;
                            }

                            uint firstDifference0 =
                                value0 >= right0
                                    ? value0 - right0
                                    : value0 + modulus - right0;

                            uint firstDifference1 =
                                value2 >= right1
                                    ? value2 - right1
                                    : value2 + modulus - right1;

                            uint mergedRight0 =
                                (uint)((ulong)firstSum1 *
                                       twiddles[secondTwiddleIndex0] %
                                       modulus);

                            uint mergedRight1 =
                                (uint)((ulong)firstDifference1 *
                                       twiddles[secondTwiddleIndex1] %
                                       modulus);

                            uint finalSum0 =
                                firstSum0 +
                                mergedRight0;

                            uint finalSum1 =
                                firstDifference0 +
                                mergedRight1;

                            if (finalSum0 >= modulus)
                            {
                                finalSum0 -= modulus;
                            }

                            if (finalSum1 >= modulus)
                            {
                                finalSum1 -= modulus;
                            }

                            uint finalDifference0 =
                                firstSum0 >= mergedRight0
                                    ? firstSum0 - mergedRight0
                                    : firstSum0 + modulus - mergedRight0;

                            uint finalDifference1 =
                                firstDifference0 >= mergedRight1
                                    ? firstDifference0 - mergedRight1
                                    : firstDifference0 + modulus - mergedRight1;

                            values[index0] =
                                finalSum0;

                            values[index1] =
                                finalSum1;

                            values[index2] =
                                finalDifference0;

                            values[index3] =
                                finalDifference1;
                        }

                        remaining -=
                            chunkLength;

                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            });
    }

    /// <summary>
    /// Fast path for cached global/RAM DIF stages once every NTT group can remain whole on
    /// one worker.  This keeps each worker on a contiguous address range and
    /// removes the generic segment-to-group mapping from the global hot path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteForwardCachedStageByGroups(
        uint[] values,
        uint modulus,
        uint[] twiddles,
        int twiddleOffset,
        int stageLength,
        int halfLength,
        int groupCount,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        const int CancellationStride =
            1 << 15;

        ExecuteRanges(
            groupCount,
            workers,
            cancellationToken,
            (groupStart, groupEnd) =>
            {
                for (int groupIndex = groupStart;
                     groupIndex < groupEnd;
                     groupIndex++)
                {
                    int groupOffset =
                        groupIndex *
                        stageLength;

                    int rightBase =
                        groupOffset +
                        halfLength;

                    uint leftValue =
                        values[groupOffset];

                    uint rightValue =
                        values[rightBase];

                    uint sum =
                        leftValue +
                        rightValue;

                    if (sum >= modulus)
                    {
                        sum -= modulus;
                    }

                    values[groupOffset] =
                        sum;

                    values[rightBase] =
                        leftValue >= rightValue
                            ? leftValue - rightValue
                            : leftValue + modulus - rightValue;

                    int butterfly = 1;
                    int leftIndex = groupOffset + 1;
                    int rightIndex = rightBase + 1;
                    int twiddleIndex = twiddleOffset + 1;

                    // Keep the cancellation branch out of the per-butterfly
                    // hot loop.  Two independent butterflies are issued per
                    // iteration so the out-of-order core can overlap address,
                    // ALU and integer-remainder work more effectively.
                    while (butterfly < halfLength)
                    {
                        int chunkEnd =
                            Math.Min(
                                halfLength,
                                butterfly + CancellationStride);


                        if (halfLength >= GlobalAdaptiveEightWayHalfLength)
                        {
                            for (;
                                 butterfly + 7 < chunkEnd;
                                 butterfly += 8,
                                 leftIndex += 8,
                                 rightIndex += 8,
                                 twiddleIndex += 8)
                            {
                                // Macro-unroll 8 as two four-butterfly batches.
                                // The nested scopes shorten live ranges and
                                // avoid the register pressure of a literal
                                // eight-butterfly live set on x64.
                                {
                                    uint left0 = values[leftIndex];
                                    uint right0 = values[rightIndex];
                                    uint left1 = values[leftIndex + 1];
                                    uint right1 = values[rightIndex + 1];
                                    uint left2 = values[leftIndex + 2];
                                    uint right2 = values[rightIndex + 2];
                                    uint left3 = values[leftIndex + 3];
                                    uint right3 = values[rightIndex + 3];

                                    uint sum0 = left0 + right0;
                                    uint sum1 = left1 + right1;
                                    uint sum2 = left2 + right2;
                                    uint sum3 = left3 + right3;

                                    if (sum0 >= modulus) sum0 -= modulus;
                                    if (sum1 >= modulus) sum1 -= modulus;
                                    if (sum2 >= modulus) sum2 -= modulus;
                                    if (sum3 >= modulus) sum3 -= modulus;

                                    uint difference0 =
                                        left0 >= right0
                                            ? left0 - right0
                                            : left0 + modulus - right0;

                                    uint difference1 =
                                        left1 >= right1
                                            ? left1 - right1
                                            : left1 + modulus - right1;

                                    uint difference2 =
                                        left2 >= right2
                                            ? left2 - right2
                                            : left2 + modulus - right2;

                                    uint difference3 =
                                        left3 >= right3
                                            ? left3 - right3
                                            : left3 + modulus - right3;

                                    values[leftIndex] = sum0;
                                    values[leftIndex + 1] = sum1;
                                    values[leftIndex + 2] = sum2;
                                    values[leftIndex + 3] = sum3;

                                    values[rightIndex] =
                                        (uint)((ulong)difference0 *
                                               twiddles[twiddleIndex] %
                                               modulus);

                                    values[rightIndex + 1] =
                                        (uint)((ulong)difference1 *
                                               twiddles[twiddleIndex + 1] %
                                               modulus);

                                    values[rightIndex + 2] =
                                        (uint)((ulong)difference2 *
                                               twiddles[twiddleIndex + 2] %
                                               modulus);

                                    values[rightIndex + 3] =
                                        (uint)((ulong)difference3 *
                                               twiddles[twiddleIndex + 3] %
                                               modulus);
                                }

                                {
                                    int left4Index = leftIndex + 4;
                                    int right4Index = rightIndex + 4;
                                    int twiddle4Index = twiddleIndex + 4;

                                    uint left4 = values[left4Index];
                                    uint right4 = values[right4Index];
                                    uint left5 = values[left4Index + 1];
                                    uint right5 = values[right4Index + 1];
                                    uint left6 = values[left4Index + 2];
                                    uint right6 = values[right4Index + 2];
                                    uint left7 = values[left4Index + 3];
                                    uint right7 = values[right4Index + 3];

                                    uint sum4 = left4 + right4;
                                    uint sum5 = left5 + right5;
                                    uint sum6 = left6 + right6;
                                    uint sum7 = left7 + right7;

                                    if (sum4 >= modulus) sum4 -= modulus;
                                    if (sum5 >= modulus) sum5 -= modulus;
                                    if (sum6 >= modulus) sum6 -= modulus;
                                    if (sum7 >= modulus) sum7 -= modulus;

                                    uint difference4 =
                                        left4 >= right4
                                            ? left4 - right4
                                            : left4 + modulus - right4;

                                    uint difference5 =
                                        left5 >= right5
                                            ? left5 - right5
                                            : left5 + modulus - right5;

                                    uint difference6 =
                                        left6 >= right6
                                            ? left6 - right6
                                            : left6 + modulus - right6;

                                    uint difference7 =
                                        left7 >= right7
                                            ? left7 - right7
                                            : left7 + modulus - right7;

                                    values[left4Index] = sum4;
                                    values[left4Index + 1] = sum5;
                                    values[left4Index + 2] = sum6;
                                    values[left4Index + 3] = sum7;

                                    values[right4Index] =
                                        (uint)((ulong)difference4 *
                                               twiddles[twiddle4Index] %
                                               modulus);

                                    values[right4Index + 1] =
                                        (uint)((ulong)difference5 *
                                               twiddles[twiddle4Index + 1] %
                                               modulus);

                                    values[right4Index + 2] =
                                        (uint)((ulong)difference6 *
                                               twiddles[twiddle4Index + 2] %
                                               modulus);

                                    values[right4Index + 3] =
                                        (uint)((ulong)difference7 *
                                               twiddles[twiddle4Index + 3] %
                                               modulus);
                                }
                            }
                        }

                        if (halfLength >= AdaptiveFourWayHalfLength)
                        {
                            for (;
                                 butterfly + 3 < chunkEnd;
                                 butterfly += 4,
                                 leftIndex += 4,
                                 rightIndex += 4,
                                 twiddleIndex += 4)
                            {
                                uint left0 = values[leftIndex];
                                uint right0 = values[rightIndex];
                                uint left1 = values[leftIndex + 1];
                                uint right1 = values[rightIndex + 1];
                                uint left2 = values[leftIndex + 2];
                                uint right2 = values[rightIndex + 2];
                                uint left3 = values[leftIndex + 3];
                                uint right3 = values[rightIndex + 3];

                                uint sum0 = left0 + right0;
                                uint sum1 = left1 + right1;
                                uint sum2 = left2 + right2;
                                uint sum3 = left3 + right3;

                                if (sum0 >= modulus) sum0 -= modulus;
                                if (sum1 >= modulus) sum1 -= modulus;
                                if (sum2 >= modulus) sum2 -= modulus;
                                if (sum3 >= modulus) sum3 -= modulus;

                                uint difference0 =
                                    left0 >= right0
                                        ? left0 - right0
                                        : left0 + modulus - right0;

                                uint difference1 =
                                    left1 >= right1
                                        ? left1 - right1
                                        : left1 + modulus - right1;

                                uint difference2 =
                                    left2 >= right2
                                        ? left2 - right2
                                        : left2 + modulus - right2;

                                uint difference3 =
                                    left3 >= right3
                                        ? left3 - right3
                                        : left3 + modulus - right3;

                                values[leftIndex] = sum0;
                                values[leftIndex + 1] = sum1;
                                values[leftIndex + 2] = sum2;
                                values[leftIndex + 3] = sum3;

                                values[rightIndex] =
                                    (uint)((ulong)difference0 *
                                           twiddles[twiddleIndex] %
                                           modulus);

                                values[rightIndex + 1] =
                                    (uint)((ulong)difference1 *
                                           twiddles[twiddleIndex + 1] %
                                           modulus);

                                values[rightIndex + 2] =
                                    (uint)((ulong)difference2 *
                                           twiddles[twiddleIndex + 2] %
                                           modulus);

                                values[rightIndex + 3] =
                                    (uint)((ulong)difference3 *
                                           twiddles[twiddleIndex + 3] %
                                           modulus);
                            }
                        }

                        for (;
                             butterfly + 1 < chunkEnd;
                             butterfly += 2,
                             leftIndex += 2,
                             rightIndex += 2,
                             twiddleIndex += 2)
                        {
                            uint left0 =
                                values[leftIndex];
                            uint right0 =
                                values[rightIndex];
                            uint left1 =
                                values[leftIndex + 1];
                            uint right1 =
                                values[rightIndex + 1];

                            uint sum0 =
                                left0 + right0;
                            uint sum1 =
                                left1 + right1;

                            if (sum0 >= modulus)
                            {
                                sum0 -= modulus;
                            }

                            if (sum1 >= modulus)
                            {
                                sum1 -= modulus;
                            }

                            uint difference0 =
                                left0 >= right0
                                    ? left0 - right0
                                    : left0 + modulus - right0;

                            uint difference1 =
                                left1 >= right1
                                    ? left1 - right1
                                    : left1 + modulus - right1;

                            values[leftIndex] =
                                sum0;
                            values[leftIndex + 1] =
                                sum1;

                            values[rightIndex] =
                                (uint)((ulong)difference0 *
                                       twiddles[twiddleIndex] %
                                       modulus);

                            values[rightIndex + 1] =
                                (uint)((ulong)difference1 *
                                       twiddles[twiddleIndex + 1] %
                                       modulus);
                        }

                        if (butterfly < chunkEnd)
                        {
                            leftValue =
                                values[leftIndex];
                            rightValue =
                                values[rightIndex];

                            sum =
                                leftValue +
                                rightValue;

                            if (sum >= modulus)
                            {
                                sum -= modulus;
                            }

                            uint difference =
                                leftValue >= rightValue
                                    ? leftValue - rightValue
                                    : leftValue + modulus - rightValue;

                            values[leftIndex] =
                                sum;

                            values[rightIndex] =
                                (uint)((ulong)difference *
                                       twiddles[twiddleIndex] %
                                       modulus);

                            butterfly++;
                            leftIndex++;
                            rightIndex++;
                            twiddleIndex++;
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            });
    }

    /// <summary>
    /// Cached global/RAM DIT counterpart of ExecuteForwardCachedStageByGroups.  Group
    /// ownership stays contiguous for the full stage, improving hardware
    /// prefetch behavior and avoiding segment mapping after groupCount reaches
    /// the worker budget.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteInverseCachedStageByGroups(
        uint[] values,
        uint modulus,
        uint[] twiddles,
        int twiddleOffset,
        int stageLength,
        int halfLength,
        int groupCount,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        const int CancellationStride =
            1 << 15;

        // A cached stage requires groupCount >= 2, therefore it can never be
        // the final DIT stage (stageLength == transform length).  Normalization
        // is consequently impossible on this path, so keep that branch out of
        // every butterfly and leave final-stage normalization to the generic
        // uncached path where it belongs.
        ExecuteRanges(
            groupCount,
            workers,
            cancellationToken,
            (groupStart, groupEnd) =>
            {
                for (int groupIndex = groupStart;
                     groupIndex < groupEnd;
                     groupIndex++)
                {
                    int groupOffset =
                        groupIndex *
                        stageLength;

                    int rightBase =
                        groupOffset +
                        halfLength;

                    uint leftValue =
                        values[groupOffset];

                    uint rightValue =
                        values[rightBase];

                    uint sum =
                        leftValue +
                        rightValue;

                    if (sum >= modulus)
                    {
                        sum -= modulus;
                    }

                    values[groupOffset] =
                        sum;

                    values[rightBase] =
                        leftValue >= rightValue
                            ? leftValue - rightValue
                            : leftValue + modulus - rightValue;

                    int butterfly = 1;
                    int leftIndex = groupOffset + 1;
                    int rightIndex = rightBase + 1;
                    int twiddleIndex = twiddleOffset + 1;

                    while (butterfly < halfLength)
                    {
                        int chunkEnd =
                            Math.Min(
                                halfLength,
                                butterfly + CancellationStride);


                        if (halfLength >= GlobalAdaptiveEightWayHalfLength)
                        {
                            for (;
                                 butterfly + 7 < chunkEnd;
                                 butterfly += 8,
                                 leftIndex += 8,
                                 rightIndex += 8,
                                 twiddleIndex += 8)
                            {
                                {
                                    uint left0 = values[leftIndex];
                                    uint left1 = values[leftIndex + 1];
                                    uint left2 = values[leftIndex + 2];
                                    uint left3 = values[leftIndex + 3];

                                    uint right0 =
                                        (uint)((ulong)values[rightIndex] *
                                               twiddles[twiddleIndex] %
                                               modulus);

                                    uint right1 =
                                        (uint)((ulong)values[rightIndex + 1] *
                                               twiddles[twiddleIndex + 1] %
                                               modulus);

                                    uint right2 =
                                        (uint)((ulong)values[rightIndex + 2] *
                                               twiddles[twiddleIndex + 2] %
                                               modulus);

                                    uint right3 =
                                        (uint)((ulong)values[rightIndex + 3] *
                                               twiddles[twiddleIndex + 3] %
                                               modulus);

                                    uint sum0 = left0 + right0;
                                    uint sum1 = left1 + right1;
                                    uint sum2 = left2 + right2;
                                    uint sum3 = left3 + right3;

                                    if (sum0 >= modulus) sum0 -= modulus;
                                    if (sum1 >= modulus) sum1 -= modulus;
                                    if (sum2 >= modulus) sum2 -= modulus;
                                    if (sum3 >= modulus) sum3 -= modulus;

                                    values[leftIndex] = sum0;
                                    values[leftIndex + 1] = sum1;
                                    values[leftIndex + 2] = sum2;
                                    values[leftIndex + 3] = sum3;

                                    values[rightIndex] =
                                        left0 >= right0
                                            ? left0 - right0
                                            : left0 + modulus - right0;

                                    values[rightIndex + 1] =
                                        left1 >= right1
                                            ? left1 - right1
                                            : left1 + modulus - right1;

                                    values[rightIndex + 2] =
                                        left2 >= right2
                                            ? left2 - right2
                                            : left2 + modulus - right2;

                                    values[rightIndex + 3] =
                                        left3 >= right3
                                            ? left3 - right3
                                            : left3 + modulus - right3;
                                }

                                {
                                    int left4Index = leftIndex + 4;
                                    int right4Index = rightIndex + 4;
                                    int twiddle4Index = twiddleIndex + 4;

                                    uint left4 = values[left4Index];
                                    uint left5 = values[left4Index + 1];
                                    uint left6 = values[left4Index + 2];
                                    uint left7 = values[left4Index + 3];

                                    uint right4 =
                                        (uint)((ulong)values[right4Index] *
                                               twiddles[twiddle4Index] %
                                               modulus);

                                    uint right5 =
                                        (uint)((ulong)values[right4Index + 1] *
                                               twiddles[twiddle4Index + 1] %
                                               modulus);

                                    uint right6 =
                                        (uint)((ulong)values[right4Index + 2] *
                                               twiddles[twiddle4Index + 2] %
                                               modulus);

                                    uint right7 =
                                        (uint)((ulong)values[right4Index + 3] *
                                               twiddles[twiddle4Index + 3] %
                                               modulus);

                                    uint sum4 = left4 + right4;
                                    uint sum5 = left5 + right5;
                                    uint sum6 = left6 + right6;
                                    uint sum7 = left7 + right7;

                                    if (sum4 >= modulus) sum4 -= modulus;
                                    if (sum5 >= modulus) sum5 -= modulus;
                                    if (sum6 >= modulus) sum6 -= modulus;
                                    if (sum7 >= modulus) sum7 -= modulus;

                                    values[left4Index] = sum4;
                                    values[left4Index + 1] = sum5;
                                    values[left4Index + 2] = sum6;
                                    values[left4Index + 3] = sum7;

                                    values[right4Index] =
                                        left4 >= right4
                                            ? left4 - right4
                                            : left4 + modulus - right4;

                                    values[right4Index + 1] =
                                        left5 >= right5
                                            ? left5 - right5
                                            : left5 + modulus - right5;

                                    values[right4Index + 2] =
                                        left6 >= right6
                                            ? left6 - right6
                                            : left6 + modulus - right6;

                                    values[right4Index + 3] =
                                        left7 >= right7
                                            ? left7 - right7
                                            : left7 + modulus - right7;
                                }
                            }
                        }

                        if (halfLength >= AdaptiveFourWayHalfLength)
                        {
                            for (;
                                 butterfly + 3 < chunkEnd;
                                 butterfly += 4,
                                 leftIndex += 4,
                                 rightIndex += 4,
                                 twiddleIndex += 4)
                            {
                                uint left0 = values[leftIndex];
                                uint left1 = values[leftIndex + 1];
                                uint left2 = values[leftIndex + 2];
                                uint left3 = values[leftIndex + 3];

                                uint right0 =
                                    (uint)((ulong)values[rightIndex] *
                                           twiddles[twiddleIndex] %
                                           modulus);

                                uint right1 =
                                    (uint)((ulong)values[rightIndex + 1] *
                                           twiddles[twiddleIndex + 1] %
                                           modulus);

                                uint right2 =
                                    (uint)((ulong)values[rightIndex + 2] *
                                           twiddles[twiddleIndex + 2] %
                                           modulus);

                                uint right3 =
                                    (uint)((ulong)values[rightIndex + 3] *
                                           twiddles[twiddleIndex + 3] %
                                           modulus);

                                uint sum0 = left0 + right0;
                                uint sum1 = left1 + right1;
                                uint sum2 = left2 + right2;
                                uint sum3 = left3 + right3;

                                if (sum0 >= modulus) sum0 -= modulus;
                                if (sum1 >= modulus) sum1 -= modulus;
                                if (sum2 >= modulus) sum2 -= modulus;
                                if (sum3 >= modulus) sum3 -= modulus;

                                values[leftIndex] = sum0;
                                values[leftIndex + 1] = sum1;
                                values[leftIndex + 2] = sum2;
                                values[leftIndex + 3] = sum3;

                                values[rightIndex] =
                                    left0 >= right0
                                        ? left0 - right0
                                        : left0 + modulus - right0;

                                values[rightIndex + 1] =
                                    left1 >= right1
                                        ? left1 - right1
                                        : left1 + modulus - right1;

                                values[rightIndex + 2] =
                                    left2 >= right2
                                        ? left2 - right2
                                        : left2 + modulus - right2;

                                values[rightIndex + 3] =
                                    left3 >= right3
                                        ? left3 - right3
                                        : left3 + modulus - right3;
                            }
                        }

                        for (;
                             butterfly + 1 < chunkEnd;
                             butterfly += 2,
                             leftIndex += 2,
                             rightIndex += 2,
                             twiddleIndex += 2)
                        {
                            uint left0 =
                                values[leftIndex];
                            uint left1 =
                                values[leftIndex + 1];

                            uint right0 =
                                (uint)((ulong)values[rightIndex] *
                                       twiddles[twiddleIndex] %
                                       modulus);

                            uint right1 =
                                (uint)((ulong)values[rightIndex + 1] *
                                       twiddles[twiddleIndex + 1] %
                                       modulus);

                            uint sum0 =
                                left0 + right0;
                            uint sum1 =
                                left1 + right1;

                            if (sum0 >= modulus)
                            {
                                sum0 -= modulus;
                            }

                            if (sum1 >= modulus)
                            {
                                sum1 -= modulus;
                            }

                            values[leftIndex] =
                                sum0;
                            values[leftIndex + 1] =
                                sum1;

                            values[rightIndex] =
                                left0 >= right0
                                    ? left0 - right0
                                    : left0 + modulus - right0;

                            values[rightIndex + 1] =
                                left1 >= right1
                                    ? left1 - right1
                                    : left1 + modulus - right1;
                        }

                        if (butterfly < chunkEnd)
                        {
                            leftValue =
                                values[leftIndex];

                            rightValue =
                                (uint)((ulong)values[rightIndex] *
                                       twiddles[twiddleIndex] %
                                       modulus);

                            sum =
                                leftValue +
                                rightValue;

                            if (sum >= modulus)
                            {
                                sum -= modulus;
                            }

                            values[leftIndex] =
                                sum;

                            values[rightIndex] =
                                leftValue >= rightValue
                                    ? leftValue - rightValue
                                    : leftValue + modulus - rightValue;

                            butterfly++;
                            leftIndex++;
                            rightIndex++;
                            twiddleIndex++;
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            });
    }

    private static void PrepareFusedTwiddleTables(
        NttTwiddlePlan twiddlePlan,
        uint primitiveRoot,
        uint modulus,
        int fusedNttBlockLength,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        for (int stageLength = fusedNttBlockLength;
             stageLength >= 4;
             stageLength >>= 1)
        {
            int halfLength =
                stageLength >> 1;

            if (twiddlePlan.IsStageReady(
                    halfLength))
            {
                continue;
            }

            uint root =
                (uint)ModPow(
                    primitiveRoot,
                    (modulus - 1u) /
                    (uint)stageLength,
                    modulus);

            BuildTwiddleTables(
                twiddlePlan.ForwardTwiddles,
                twiddlePlan.InverseTwiddles,
                twiddlePlan.GetOffset(
                    halfLength),
                halfLength,
                root,
                modulus,
                workers,
                cancellationToken);

            twiddlePlan.MarkStageReady(
                halfLength);
        }
    }

    /// <summary>
    /// Three-level DIF tail. Once a stage reaches the selected last-level-cache
    /// tile size, every later stage is independent inside that tile. Complete
    /// the LLC-local stages, then each L2 tile, then each L1 block before moving
    /// on. This removes additional full-transform sweeps without changing the
    /// proven scalar modular arithmetic.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteForwardL3CacheBlockedTail(
        uint[] values,
        uint modulus,
        FixedWorkerTeam workers,
        NttTwiddlePlan twiddlePlan,
        int fusedNttBlockLength,
        int l2NttTileLength,
        int l3NttTileLength,
        CancellationToken cancellationToken)
    {
        int tileCount =
            values.Length /
            l3NttTileLength;

        uint[] twiddles =
            twiddlePlan.ForwardTwiddles;

        ExecuteRanges(
            tileCount,
            workers,
            cancellationToken,
            (startTile, endTile) =>
            {
                for (int tileIndex = startTile;
                     tileIndex < endTile;
                     tileIndex++)
                {
                    int tileOffset =
                        tileIndex *
                        l3NttTileLength;

                    int tileEnd =
                        tileOffset +
                        l3NttTileLength;

                    // LLC-local DIF stages. Stop before the L2 tile boundary;
                    // each resulting L2 tile is independent afterwards.
                    for (int stageLength = l3NttTileLength;
                         stageLength > l2NttTileLength;
                         stageLength >>= 1)
                    {
                        int halfLength =
                            stageLength >> 1;

                        int twiddleOffset =
                            twiddlePlan.GetOffset(
                                halfLength);

                        for (int groupOffset = tileOffset;
                             groupOffset < tileEnd;
                             groupOffset += stageLength)
                        {
                            ExecuteForwardCachedDifGroup(
                                values,
                                modulus,
                                twiddles,
                                twiddleOffset,
                                groupOffset,
                                halfLength);
                        }
                    }

                    // Complete each L2 tile through the existing L1 hierarchy
                    // while the parent LLC tile is still resident.
                    for (int l2TileOffset = tileOffset;
                         l2TileOffset < tileEnd;
                         l2TileOffset += l2NttTileLength)
                    {
                        ExecuteForwardL2TileSequential(
                            values,
                            modulus,
                            twiddles,
                            twiddlePlan,
                            fusedNttBlockLength,
                            l2NttTileLength,
                            l2TileOffset);
                    }

                    if ((tileIndex & 0x07) == 0x07)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            });
    }

    /// <summary>
    /// Three-level DIT head, inverse of ExecuteForwardL3CacheBlockedTail.
    /// Complete each L2/L1 subtree first, merge those subtrees through the
    /// LLC-local DIT stages, then return to the global transform.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteInverseL3CacheBlockedHead(
        uint[] values,
        uint modulus,
        FixedWorkerTeam workers,
        NttTwiddlePlan twiddlePlan,
        int fusedNttBlockLength,
        int l2NttTileLength,
        int l3NttTileLength,
        CancellationToken cancellationToken)
    {
        int tileCount =
            values.Length /
            l3NttTileLength;

        uint[] twiddles =
            twiddlePlan.InverseTwiddles;

        ExecuteRanges(
            tileCount,
            workers,
            cancellationToken,
            (startTile, endTile) =>
            {
                for (int tileIndex = startTile;
                     tileIndex < endTile;
                     tileIndex++)
                {
                    int tileOffset =
                        tileIndex *
                        l3NttTileLength;

                    int tileEnd =
                        tileOffset +
                        l3NttTileLength;

                    for (int l2TileOffset = tileOffset;
                         l2TileOffset < tileEnd;
                         l2TileOffset += l2NttTileLength)
                    {
                        ExecuteInverseL2TileSequential(
                            values,
                            modulus,
                            twiddles,
                            twiddlePlan,
                            fusedNttBlockLength,
                            l2NttTileLength,
                            l2TileOffset);
                    }

                    // Merge the completed L2 tiles while their parent LLC tile
                    // remains hot. These are exactly the DIT stages inverse to
                    // the LLC-local DIF stages above.
                    for (int stageLength = l2NttTileLength << 1;
                         stageLength <= l3NttTileLength;
                         stageLength <<= 1)
                    {
                        int halfLength =
                            stageLength >> 1;

                        int twiddleOffset =
                            twiddlePlan.GetOffset(
                                halfLength);

                        for (int groupOffset = tileOffset;
                             groupOffset < tileEnd;
                             groupOffset += stageLength)
                        {
                            ExecuteInverseCachedDitGroup(
                                values,
                                modulus,
                                twiddles,
                                twiddleOffset,
                                groupOffset,
                                halfLength);
                        }
                    }

                    if ((tileIndex & 0x07) == 0x07)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            });
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteForwardL2TileSequential(
        uint[] values,
        uint modulus,
        uint[] twiddles,
        NttTwiddlePlan twiddlePlan,
        int fusedNttBlockLength,
        int l2NttTileLength,
        int tileOffset)
    {
        int tileEnd =
            tileOffset +
            l2NttTileLength;

        // Keep this inner L2/L1 kernel expanded, matching the proven v9 hot
        // loops.  Avoid a per-group helper call here: one 2^26 transform has
        // tens of millions of small groups after blocking.
        for (int stageLength = l2NttTileLength;
             stageLength > fusedNttBlockLength;
             stageLength >>= 1)
        {
            int halfLength =
                stageLength >> 1;

            int twiddleOffset =
                twiddlePlan.GetOffset(
                    halfLength);

            for (int groupOffset = tileOffset;
                 groupOffset < tileEnd;
                 groupOffset += stageLength)
            {
                int rightBase =
                    groupOffset +
                    halfLength;

                uint leftValue =
                    values[groupOffset];

                uint rightValue =
                    values[rightBase];

                uint sum =
                    leftValue +
                    rightValue;

                if (sum >= modulus)
                {
                    sum -= modulus;
                }

                values[groupOffset] =
                    sum;

                values[rightBase] =
                    leftValue >= rightValue
                        ? leftValue - rightValue
                        : leftValue + modulus - rightValue;

                int leftIndex =
                    groupOffset + 1;

                int rightIndex =
                    rightBase + 1;

                int butterflyEnd =
                    groupOffset +
                    halfLength;

                int twiddleIndex =
                    twiddleOffset + 1;

                // Cache-resident scalar kernel: expose two independent butterflies
                // per iteration and walk the twiddle table with a direct index.
                                if (halfLength >= AdaptiveFourWayHalfLength)
                {
                    while (leftIndex + 3 < butterflyEnd)
                    {
                        uint left0 = values[leftIndex];
                        uint right0 = values[rightIndex];
                        uint left1 = values[leftIndex + 1];
                        uint right1 = values[rightIndex + 1];
                        uint left2 = values[leftIndex + 2];
                        uint right2 = values[rightIndex + 2];
                        uint left3 = values[leftIndex + 3];
                        uint right3 = values[rightIndex + 3];

                        uint sum0 = left0 + right0;
                        uint sum1 = left1 + right1;
                        uint sum2 = left2 + right2;
                        uint sum3 = left3 + right3;

                        if (sum0 >= modulus) sum0 -= modulus;
                        if (sum1 >= modulus) sum1 -= modulus;
                        if (sum2 >= modulus) sum2 -= modulus;
                        if (sum3 >= modulus) sum3 -= modulus;

                        uint difference0 =
                            left0 >= right0
                                ? left0 - right0
                                : left0 + modulus - right0;

                        uint difference1 =
                            left1 >= right1
                                ? left1 - right1
                                : left1 + modulus - right1;

                        uint difference2 =
                            left2 >= right2
                                ? left2 - right2
                                : left2 + modulus - right2;

                        uint difference3 =
                            left3 >= right3
                                ? left3 - right3
                                : left3 + modulus - right3;

                        values[leftIndex] = sum0;
                        values[leftIndex + 1] = sum1;
                        values[leftIndex + 2] = sum2;
                        values[leftIndex + 3] = sum3;

                        values[rightIndex] =
                            (uint)((ulong)difference0 *
                                   twiddles[twiddleIndex] %
                                   modulus);

                        values[rightIndex + 1] =
                            (uint)((ulong)difference1 *
                                   twiddles[twiddleIndex + 1] %
                                   modulus);

                        values[rightIndex + 2] =
                            (uint)((ulong)difference2 *
                                   twiddles[twiddleIndex + 2] %
                                   modulus);

                        values[rightIndex + 3] =
                            (uint)((ulong)difference3 *
                                   twiddles[twiddleIndex + 3] %
                                   modulus);

                        leftIndex += 4;
                        rightIndex += 4;
                        twiddleIndex += 4;
                    }
                }

while (leftIndex + 1 < butterflyEnd)
                {
                    uint left0 = values[leftIndex];
                    uint right0 = values[rightIndex];
                    uint left1 = values[leftIndex + 1];
                    uint right1 = values[rightIndex + 1];

                    uint sum0 = left0 + right0;
                    uint sum1 = left1 + right1;

                    if (sum0 >= modulus) sum0 -= modulus;
                    if (sum1 >= modulus) sum1 -= modulus;

                    uint difference0 =
                        left0 >= right0
                            ? left0 - right0
                            : left0 + modulus - right0;

                    uint difference1 =
                        left1 >= right1
                            ? left1 - right1
                            : left1 + modulus - right1;

                    values[leftIndex] = sum0;
                    values[leftIndex + 1] = sum1;

                    values[rightIndex] =
                        (uint)((ulong)difference0 *
                               twiddles[twiddleIndex] %
                               modulus);

                    values[rightIndex + 1] =
                        (uint)((ulong)difference1 *
                               twiddles[twiddleIndex + 1] %
                               modulus);

                    leftIndex += 2;
                    rightIndex += 2;
                    twiddleIndex += 2;
                }

                if (leftIndex < butterflyEnd)
                {
                    leftValue = values[leftIndex];
                    rightValue = values[rightIndex];
                    sum = leftValue + rightValue;

                    if (sum >= modulus)
                    {
                        sum -= modulus;
                    }

                    uint difference =
                        leftValue >= rightValue
                            ? leftValue - rightValue
                            : leftValue + modulus - rightValue;

                    values[leftIndex] = sum;
                    values[rightIndex] =
                        (uint)((ulong)difference *
                               twiddles[twiddleIndex] %
                               modulus);
                }
            }
        }

        for (int blockOffset = tileOffset;
             blockOffset < tileEnd;
             blockOffset += fusedNttBlockLength)
        {
            int blockEnd =
                blockOffset +
                fusedNttBlockLength;

            for (int stageLength = fusedNttBlockLength;
                 stageLength >= 4;
                 stageLength >>= 1)
            {
                int halfLength =
                    stageLength >> 1;

                int twiddleOffset =
                    twiddlePlan.GetOffset(
                        halfLength);

                for (int groupOffset = blockOffset;
                     groupOffset < blockEnd;
                     groupOffset += stageLength)
                {
                    int rightBase =
                        groupOffset +
                        halfLength;

                    uint leftValue =
                        values[groupOffset];

                    uint rightValue =
                        values[rightBase];

                    uint sum =
                        leftValue +
                        rightValue;

                    if (sum >= modulus)
                    {
                        sum -= modulus;
                    }

                    values[groupOffset] =
                        sum;

                    values[rightBase] =
                        leftValue >= rightValue
                            ? leftValue - rightValue
                            : leftValue + modulus - rightValue;

                    int leftIndex =
                        groupOffset + 1;

                    int rightIndex =
                        rightBase + 1;

                    int butterflyEnd =
                        groupOffset +
                        halfLength;

                    int twiddleIndex =
                        twiddleOffset + 1;

                    // Cache-resident scalar kernel: expose two independent butterflies
                    // per iteration and walk the twiddle table with a direct index.
                    while (leftIndex + 1 < butterflyEnd)
                    {
                        uint left0 = values[leftIndex];
                        uint right0 = values[rightIndex];
                        uint left1 = values[leftIndex + 1];
                        uint right1 = values[rightIndex + 1];

                        uint sum0 = left0 + right0;
                        uint sum1 = left1 + right1;

                        if (sum0 >= modulus) sum0 -= modulus;
                        if (sum1 >= modulus) sum1 -= modulus;

                        uint difference0 =
                            left0 >= right0
                                ? left0 - right0
                                : left0 + modulus - right0;

                        uint difference1 =
                            left1 >= right1
                                ? left1 - right1
                                : left1 + modulus - right1;

                        values[leftIndex] = sum0;
                        values[leftIndex + 1] = sum1;

                        values[rightIndex] =
                            (uint)((ulong)difference0 *
                                   twiddles[twiddleIndex] %
                                   modulus);

                        values[rightIndex + 1] =
                            (uint)((ulong)difference1 *
                                   twiddles[twiddleIndex + 1] %
                                   modulus);

                        leftIndex += 2;
                        rightIndex += 2;
                        twiddleIndex += 2;
                    }

                    if (leftIndex < butterflyEnd)
                    {
                        leftValue = values[leftIndex];
                        rightValue = values[rightIndex];
                        sum = leftValue + rightValue;

                        if (sum >= modulus)
                        {
                            sum -= modulus;
                        }

                        uint difference =
                            leftValue >= rightValue
                                ? leftValue - rightValue
                                : leftValue + modulus - rightValue;

                        values[leftIndex] = sum;
                        values[rightIndex] =
                            (uint)((ulong)difference *
                                   twiddles[twiddleIndex] %
                                   modulus);
                    }
                }
            }

            for (int leftIndex = blockOffset;
                 leftIndex < blockEnd;
                 leftIndex += 2)
            {
                uint leftValue =
                    values[leftIndex];

                uint rightValue =
                    values[leftIndex + 1];

                uint sum =
                    leftValue +
                    rightValue;

                if (sum >= modulus)
                {
                    sum -= modulus;
                }

                values[leftIndex] =
                    sum;

                values[leftIndex + 1] =
                    leftValue >= rightValue
                        ? leftValue - rightValue
                        : leftValue + modulus - rightValue;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteInverseL2TileSequential(
        uint[] values,
        uint modulus,
        uint[] twiddles,
        NttTwiddlePlan twiddlePlan,
        int fusedNttBlockLength,
        int l2NttTileLength,
        int tileOffset)
    {
        int tileEnd =
            tileOffset +
            l2NttTileLength;

        // Same deliberately expanded inner kernel as v9; only the parent L3
        // traversal is new in v10.
        for (int blockOffset = tileOffset;
             blockOffset < tileEnd;
             blockOffset += fusedNttBlockLength)
        {
            int blockEnd =
                blockOffset +
                fusedNttBlockLength;

            for (int leftIndex = blockOffset;
                 leftIndex < blockEnd;
                 leftIndex += 2)
            {
                uint leftValue =
                    values[leftIndex];

                uint rightValue =
                    values[leftIndex + 1];

                uint sum =
                    leftValue +
                    rightValue;

                if (sum >= modulus)
                {
                    sum -= modulus;
                }

                values[leftIndex] =
                    sum;

                values[leftIndex + 1] =
                    leftValue >= rightValue
                        ? leftValue - rightValue
                        : leftValue + modulus - rightValue;
            }

            for (int stageLength = 4;
                 stageLength <= fusedNttBlockLength;
                 stageLength <<= 1)
            {
                int halfLength =
                    stageLength >> 1;

                int twiddleOffset =
                    twiddlePlan.GetOffset(
                        halfLength);

                for (int groupOffset = blockOffset;
                     groupOffset < blockEnd;
                     groupOffset += stageLength)
                {
                    int rightBase =
                        groupOffset +
                        halfLength;

                    uint leftValue =
                        values[groupOffset];

                    uint rightValue =
                        values[rightBase];

                    uint sum =
                        leftValue +
                        rightValue;

                    if (sum >= modulus)
                    {
                        sum -= modulus;
                    }

                    values[groupOffset] =
                        sum;

                    values[rightBase] =
                        leftValue >= rightValue
                            ? leftValue - rightValue
                            : leftValue + modulus - rightValue;

                    int leftIndex =
                        groupOffset + 1;

                    int rightIndex =
                        rightBase + 1;

                    int butterflyEnd =
                        groupOffset +
                        halfLength;

                    int twiddleIndex =
                        twiddleOffset + 1;

                    while (leftIndex + 1 < butterflyEnd)
                    {
                        uint left0 = values[leftIndex];
                        uint left1 = values[leftIndex + 1];

                        uint right0 =
                            (uint)((ulong)values[rightIndex] *
                                   twiddles[twiddleIndex] %
                                   modulus);

                        uint right1 =
                            (uint)((ulong)values[rightIndex + 1] *
                                   twiddles[twiddleIndex + 1] %
                                   modulus);

                        uint sum0 = left0 + right0;
                        uint sum1 = left1 + right1;

                        if (sum0 >= modulus) sum0 -= modulus;
                        if (sum1 >= modulus) sum1 -= modulus;

                        values[leftIndex] = sum0;
                        values[leftIndex + 1] = sum1;

                        values[rightIndex] =
                            left0 >= right0
                                ? left0 - right0
                                : left0 + modulus - right0;

                        values[rightIndex + 1] =
                            left1 >= right1
                                ? left1 - right1
                                : left1 + modulus - right1;

                        leftIndex += 2;
                        rightIndex += 2;
                        twiddleIndex += 2;
                    }

                    if (leftIndex < butterflyEnd)
                    {
                        leftValue = values[leftIndex];
                        rightValue =
                            (uint)((ulong)values[rightIndex] *
                                   twiddles[twiddleIndex] %
                                   modulus);

                        sum = leftValue + rightValue;

                        if (sum >= modulus)
                        {
                            sum -= modulus;
                        }

                        values[leftIndex] = sum;
                        values[rightIndex] =
                            leftValue >= rightValue
                                ? leftValue - rightValue
                                : leftValue + modulus - rightValue;
                    }
                }
            }
        }

        for (int stageLength = fusedNttBlockLength << 1;
             stageLength <= l2NttTileLength;
             stageLength <<= 1)
        {
            int halfLength =
                stageLength >> 1;

            int twiddleOffset =
                twiddlePlan.GetOffset(
                    halfLength);

            for (int groupOffset = tileOffset;
                 groupOffset < tileEnd;
                 groupOffset += stageLength)
            {
                int rightBase =
                    groupOffset +
                    halfLength;

                uint leftValue =
                    values[groupOffset];

                uint rightValue =
                    values[rightBase];

                uint sum =
                    leftValue +
                    rightValue;

                if (sum >= modulus)
                {
                    sum -= modulus;
                }

                values[groupOffset] =
                    sum;

                values[rightBase] =
                    leftValue >= rightValue
                        ? leftValue - rightValue
                        : leftValue + modulus - rightValue;

                int leftIndex =
                    groupOffset + 1;

                int rightIndex =
                    rightBase + 1;

                int butterflyEnd =
                    groupOffset +
                    halfLength;

                int twiddleIndex =
                    twiddleOffset + 1;

                                if (halfLength >= AdaptiveFourWayHalfLength)
                {
                    while (leftIndex + 3 < butterflyEnd)
                    {
                        uint left0 = values[leftIndex];
                        uint left1 = values[leftIndex + 1];
                        uint left2 = values[leftIndex + 2];
                        uint left3 = values[leftIndex + 3];

                        uint right0 =
                            (uint)((ulong)values[rightIndex] *
                                   twiddles[twiddleIndex] %
                                   modulus);

                        uint right1 =
                            (uint)((ulong)values[rightIndex + 1] *
                                   twiddles[twiddleIndex + 1] %
                                   modulus);

                        uint right2 =
                            (uint)((ulong)values[rightIndex + 2] *
                                   twiddles[twiddleIndex + 2] %
                                   modulus);

                        uint right3 =
                            (uint)((ulong)values[rightIndex + 3] *
                                   twiddles[twiddleIndex + 3] %
                                   modulus);

                        uint sum0 = left0 + right0;
                        uint sum1 = left1 + right1;
                        uint sum2 = left2 + right2;
                        uint sum3 = left3 + right3;

                        if (sum0 >= modulus) sum0 -= modulus;
                        if (sum1 >= modulus) sum1 -= modulus;
                        if (sum2 >= modulus) sum2 -= modulus;
                        if (sum3 >= modulus) sum3 -= modulus;

                        values[leftIndex] = sum0;
                        values[leftIndex + 1] = sum1;
                        values[leftIndex + 2] = sum2;
                        values[leftIndex + 3] = sum3;

                        values[rightIndex] =
                            left0 >= right0
                                ? left0 - right0
                                : left0 + modulus - right0;

                        values[rightIndex + 1] =
                            left1 >= right1
                                ? left1 - right1
                                : left1 + modulus - right1;

                        values[rightIndex + 2] =
                            left2 >= right2
                                ? left2 - right2
                                : left2 + modulus - right2;

                        values[rightIndex + 3] =
                            left3 >= right3
                                ? left3 - right3
                                : left3 + modulus - right3;

                        leftIndex += 4;
                        rightIndex += 4;
                        twiddleIndex += 4;
                    }
                }

while (leftIndex + 1 < butterflyEnd)
                {
                    uint left0 = values[leftIndex];
                    uint left1 = values[leftIndex + 1];

                    uint right0 =
                        (uint)((ulong)values[rightIndex] *
                               twiddles[twiddleIndex] %
                               modulus);

                    uint right1 =
                        (uint)((ulong)values[rightIndex + 1] *
                               twiddles[twiddleIndex + 1] %
                               modulus);

                    uint sum0 = left0 + right0;
                    uint sum1 = left1 + right1;

                    if (sum0 >= modulus) sum0 -= modulus;
                    if (sum1 >= modulus) sum1 -= modulus;

                    values[leftIndex] = sum0;
                    values[leftIndex + 1] = sum1;

                    values[rightIndex] =
                        left0 >= right0
                            ? left0 - right0
                            : left0 + modulus - right0;

                    values[rightIndex + 1] =
                        left1 >= right1
                            ? left1 - right1
                            : left1 + modulus - right1;

                    leftIndex += 2;
                    rightIndex += 2;
                    twiddleIndex += 2;
                }

                if (leftIndex < butterflyEnd)
                {
                    leftValue = values[leftIndex];
                    rightValue =
                        (uint)((ulong)values[rightIndex] *
                               twiddles[twiddleIndex] %
                               modulus);

                    sum = leftValue + rightValue;

                    if (sum >= modulus)
                    {
                        sum -= modulus;
                    }

                    values[leftIndex] = sum;
                    values[rightIndex] =
                        leftValue >= rightValue
                            ? leftValue - rightValue
                            : leftValue + modulus - rightValue;
                }
            }
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExecuteForwardCachedDifGroup(
        uint[] values,
        uint modulus,
        uint[] twiddles,
        int twiddleOffset,
        int groupOffset,
        int halfLength)
    {
        int rightBase =
            groupOffset +
            halfLength;

        uint leftValue =
            values[groupOffset];

        uint rightValue =
            values[rightBase];

        uint sum =
            leftValue +
            rightValue;

        if (sum >= modulus)
        {
            sum -= modulus;
        }

        values[groupOffset] =
            sum;

        values[rightBase] =
            leftValue >= rightValue
                ? leftValue - rightValue
                : leftValue + modulus - rightValue;

        int leftIndex =
            groupOffset + 1;

        int rightIndex =
            rightBase + 1;

        int butterflyEnd =
            groupOffset +
            halfLength;

        int twiddleIndex =
            twiddleOffset + 1;

        // Cache-resident scalar kernel: expose two independent butterflies
        // per iteration and walk the twiddle table with a direct index.
                if (halfLength >= AdaptiveFourWayHalfLength)
        {
            while (leftIndex + 3 < butterflyEnd)
            {
                uint left0 = values[leftIndex];
                uint right0 = values[rightIndex];
                uint left1 = values[leftIndex + 1];
                uint right1 = values[rightIndex + 1];
                uint left2 = values[leftIndex + 2];
                uint right2 = values[rightIndex + 2];
                uint left3 = values[leftIndex + 3];
                uint right3 = values[rightIndex + 3];

                uint sum0 = left0 + right0;
                uint sum1 = left1 + right1;
                uint sum2 = left2 + right2;
                uint sum3 = left3 + right3;

                if (sum0 >= modulus) sum0 -= modulus;
                if (sum1 >= modulus) sum1 -= modulus;
                if (sum2 >= modulus) sum2 -= modulus;
                if (sum3 >= modulus) sum3 -= modulus;

                uint difference0 =
                    left0 >= right0
                        ? left0 - right0
                        : left0 + modulus - right0;

                uint difference1 =
                    left1 >= right1
                        ? left1 - right1
                        : left1 + modulus - right1;

                uint difference2 =
                    left2 >= right2
                        ? left2 - right2
                        : left2 + modulus - right2;

                uint difference3 =
                    left3 >= right3
                        ? left3 - right3
                        : left3 + modulus - right3;

                values[leftIndex] = sum0;
                values[leftIndex + 1] = sum1;
                values[leftIndex + 2] = sum2;
                values[leftIndex + 3] = sum3;

                values[rightIndex] =
                    (uint)((ulong)difference0 *
                           twiddles[twiddleIndex] %
                           modulus);

                values[rightIndex + 1] =
                    (uint)((ulong)difference1 *
                           twiddles[twiddleIndex + 1] %
                           modulus);

                values[rightIndex + 2] =
                    (uint)((ulong)difference2 *
                           twiddles[twiddleIndex + 2] %
                           modulus);

                values[rightIndex + 3] =
                    (uint)((ulong)difference3 *
                           twiddles[twiddleIndex + 3] %
                           modulus);

                leftIndex += 4;
                rightIndex += 4;
                twiddleIndex += 4;
            }
        }

while (leftIndex + 1 < butterflyEnd)
        {
            uint left0 = values[leftIndex];
            uint right0 = values[rightIndex];
            uint left1 = values[leftIndex + 1];
            uint right1 = values[rightIndex + 1];

            uint sum0 = left0 + right0;
            uint sum1 = left1 + right1;

            if (sum0 >= modulus) sum0 -= modulus;
            if (sum1 >= modulus) sum1 -= modulus;

            uint difference0 =
                left0 >= right0
                    ? left0 - right0
                    : left0 + modulus - right0;

            uint difference1 =
                left1 >= right1
                    ? left1 - right1
                    : left1 + modulus - right1;

            values[leftIndex] = sum0;
            values[leftIndex + 1] = sum1;

            values[rightIndex] =
                (uint)((ulong)difference0 *
                       twiddles[twiddleIndex] %
                       modulus);

            values[rightIndex + 1] =
                (uint)((ulong)difference1 *
                       twiddles[twiddleIndex + 1] %
                       modulus);

            leftIndex += 2;
            rightIndex += 2;
            twiddleIndex += 2;
        }

        if (leftIndex < butterflyEnd)
        {
            leftValue = values[leftIndex];
            rightValue = values[rightIndex];
            sum = leftValue + rightValue;

            if (sum >= modulus)
            {
                sum -= modulus;
            }

            uint difference =
                leftValue >= rightValue
                    ? leftValue - rightValue
                    : leftValue + modulus - rightValue;

            values[leftIndex] = sum;
            values[rightIndex] =
                (uint)((ulong)difference *
                       twiddles[twiddleIndex] %
                       modulus);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExecuteInverseCachedDitGroup(
        uint[] values,
        uint modulus,
        uint[] twiddles,
        int twiddleOffset,
        int groupOffset,
        int halfLength)
    {
        int rightBase =
            groupOffset +
            halfLength;

        uint leftValue =
            values[groupOffset];

        uint rightValue =
            values[rightBase];

        uint sum =
            leftValue +
            rightValue;

        if (sum >= modulus)
        {
            sum -= modulus;
        }

        values[groupOffset] =
            sum;

        values[rightBase] =
            leftValue >= rightValue
                ? leftValue - rightValue
                : leftValue + modulus - rightValue;

        int leftIndex =
            groupOffset + 1;

        int rightIndex =
            rightBase + 1;

        int butterflyEnd =
            groupOffset +
            halfLength;

        int twiddleIndex =
            twiddleOffset + 1;

                if (halfLength >= AdaptiveFourWayHalfLength)
        {
            while (leftIndex + 3 < butterflyEnd)
            {
                uint left0 = values[leftIndex];
                uint left1 = values[leftIndex + 1];
                uint left2 = values[leftIndex + 2];
                uint left3 = values[leftIndex + 3];

                uint right0 =
                    (uint)((ulong)values[rightIndex] *
                           twiddles[twiddleIndex] %
                           modulus);

                uint right1 =
                    (uint)((ulong)values[rightIndex + 1] *
                           twiddles[twiddleIndex + 1] %
                           modulus);

                uint right2 =
                    (uint)((ulong)values[rightIndex + 2] *
                           twiddles[twiddleIndex + 2] %
                           modulus);

                uint right3 =
                    (uint)((ulong)values[rightIndex + 3] *
                           twiddles[twiddleIndex + 3] %
                           modulus);

                uint sum0 = left0 + right0;
                uint sum1 = left1 + right1;
                uint sum2 = left2 + right2;
                uint sum3 = left3 + right3;

                if (sum0 >= modulus) sum0 -= modulus;
                if (sum1 >= modulus) sum1 -= modulus;
                if (sum2 >= modulus) sum2 -= modulus;
                if (sum3 >= modulus) sum3 -= modulus;

                values[leftIndex] = sum0;
                values[leftIndex + 1] = sum1;
                values[leftIndex + 2] = sum2;
                values[leftIndex + 3] = sum3;

                values[rightIndex] =
                    left0 >= right0
                        ? left0 - right0
                        : left0 + modulus - right0;

                values[rightIndex + 1] =
                    left1 >= right1
                        ? left1 - right1
                        : left1 + modulus - right1;

                values[rightIndex + 2] =
                    left2 >= right2
                        ? left2 - right2
                        : left2 + modulus - right2;

                values[rightIndex + 3] =
                    left3 >= right3
                        ? left3 - right3
                        : left3 + modulus - right3;

                leftIndex += 4;
                rightIndex += 4;
                twiddleIndex += 4;
            }
        }

while (leftIndex + 1 < butterflyEnd)
        {
            uint left0 = values[leftIndex];
            uint left1 = values[leftIndex + 1];

            uint right0 =
                (uint)((ulong)values[rightIndex] *
                       twiddles[twiddleIndex] %
                       modulus);

            uint right1 =
                (uint)((ulong)values[rightIndex + 1] *
                       twiddles[twiddleIndex + 1] %
                       modulus);

            uint sum0 = left0 + right0;
            uint sum1 = left1 + right1;

            if (sum0 >= modulus) sum0 -= modulus;
            if (sum1 >= modulus) sum1 -= modulus;

            values[leftIndex] = sum0;
            values[leftIndex + 1] = sum1;

            values[rightIndex] =
                left0 >= right0
                    ? left0 - right0
                    : left0 + modulus - right0;

            values[rightIndex + 1] =
                left1 >= right1
                    ? left1 - right1
                    : left1 + modulus - right1;

            leftIndex += 2;
            rightIndex += 2;
            twiddleIndex += 2;
        }

        if (leftIndex < butterflyEnd)
        {
            leftValue = values[leftIndex];
            rightValue =
                (uint)((ulong)values[rightIndex] *
                       twiddles[twiddleIndex] %
                       modulus);

            sum = leftValue + rightValue;

            if (sum >= modulus)
            {
                sum -= modulus;
            }

            values[leftIndex] = sum;
            values[rightIndex] =
                leftValue >= rightValue
                    ? leftValue - rightValue
                    : leftValue + modulus - rightValue;
        }
    }

    /// <summary>
    /// Hierarchical DIF tail: keep one L2-sized tile resident while completing
    /// the stages down to the L1 fused-block boundary, then finish each L1 block
    /// before moving to the next tile.  Arithmetic is identical to the v8
    /// scalar path; only traversal order changes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteForwardL2CacheBlockedTail(
        uint[] values,
        uint modulus,
        FixedWorkerTeam workers,
        NttTwiddlePlan twiddlePlan,
        int fusedNttBlockLength,
        int l2NttTileLength,
        CancellationToken cancellationToken)
    {
        int tileCount =
            values.Length /
            l2NttTileLength;

        uint[] twiddles =
            twiddlePlan.ForwardTwiddles;

        ExecuteRanges(
            tileCount,
            workers,
            cancellationToken,
            (startTile, endTile) =>
            {
                for (int tileIndex = startTile;
                     tileIndex < endTile;
                     tileIndex++)
                {
                    int tileOffset =
                        tileIndex *
                        l2NttTileLength;

                    int tileEnd =
                        tileOffset +
                        l2NttTileLength;

                    // L2-local DIF stages.  Stop before fusedNttBlockLength;
                    // each resulting L1-sized block is independent afterwards.
                    for (int stageLength = l2NttTileLength;
                         stageLength > fusedNttBlockLength;
                         stageLength >>= 1)
                    {
                        int halfLength =
                            stageLength >> 1;

                        int twiddleOffset =
                            twiddlePlan.GetOffset(
                                halfLength);

                        for (int groupOffset = tileOffset;
                             groupOffset < tileEnd;
                             groupOffset += stageLength)
                        {
                            int rightBase =
                                groupOffset +
                                halfLength;

                            uint leftValue =
                                values[groupOffset];

                            uint rightValue =
                                values[rightBase];

                            uint sum =
                                leftValue +
                                rightValue;

                            if (sum >= modulus)
                            {
                                sum -= modulus;
                            }

                            values[groupOffset] =
                                sum;

                            values[rightBase] =
                                leftValue >= rightValue
                                    ? leftValue - rightValue
                                    : leftValue + modulus - rightValue;

                            int leftIndex =
                                groupOffset + 1;

                            int rightIndex =
                                rightBase + 1;

                            int butterflyEnd =
                                groupOffset +
                                halfLength;

                            int twiddleIndex =
                                twiddleOffset + 1;

                            // Cache-resident scalar kernel: expose two independent butterflies
                            // per iteration and walk the twiddle table with a direct index.
                                                        if (halfLength >= AdaptiveFourWayHalfLength)
                            {
                                while (leftIndex + 3 < butterflyEnd)
                                {
                                    uint left0 = values[leftIndex];
                                    uint right0 = values[rightIndex];
                                    uint left1 = values[leftIndex + 1];
                                    uint right1 = values[rightIndex + 1];
                                    uint left2 = values[leftIndex + 2];
                                    uint right2 = values[rightIndex + 2];
                                    uint left3 = values[leftIndex + 3];
                                    uint right3 = values[rightIndex + 3];

                                    uint sum0 = left0 + right0;
                                    uint sum1 = left1 + right1;
                                    uint sum2 = left2 + right2;
                                    uint sum3 = left3 + right3;

                                    if (sum0 >= modulus) sum0 -= modulus;
                                    if (sum1 >= modulus) sum1 -= modulus;
                                    if (sum2 >= modulus) sum2 -= modulus;
                                    if (sum3 >= modulus) sum3 -= modulus;

                                    uint difference0 =
                                        left0 >= right0
                                            ? left0 - right0
                                            : left0 + modulus - right0;

                                    uint difference1 =
                                        left1 >= right1
                                            ? left1 - right1
                                            : left1 + modulus - right1;

                                    uint difference2 =
                                        left2 >= right2
                                            ? left2 - right2
                                            : left2 + modulus - right2;

                                    uint difference3 =
                                        left3 >= right3
                                            ? left3 - right3
                                            : left3 + modulus - right3;

                                    values[leftIndex] = sum0;
                                    values[leftIndex + 1] = sum1;
                                    values[leftIndex + 2] = sum2;
                                    values[leftIndex + 3] = sum3;

                                    values[rightIndex] =
                                        (uint)((ulong)difference0 *
                                               twiddles[twiddleIndex] %
                                               modulus);

                                    values[rightIndex + 1] =
                                        (uint)((ulong)difference1 *
                                               twiddles[twiddleIndex + 1] %
                                               modulus);

                                    values[rightIndex + 2] =
                                        (uint)((ulong)difference2 *
                                               twiddles[twiddleIndex + 2] %
                                               modulus);

                                    values[rightIndex + 3] =
                                        (uint)((ulong)difference3 *
                                               twiddles[twiddleIndex + 3] %
                                               modulus);

                                    leftIndex += 4;
                                    rightIndex += 4;
                                    twiddleIndex += 4;
                                }
                            }

while (leftIndex + 1 < butterflyEnd)
                            {
                                uint left0 = values[leftIndex];
                                uint right0 = values[rightIndex];
                                uint left1 = values[leftIndex + 1];
                                uint right1 = values[rightIndex + 1];

                                uint sum0 = left0 + right0;
                                uint sum1 = left1 + right1;

                                if (sum0 >= modulus) sum0 -= modulus;
                                if (sum1 >= modulus) sum1 -= modulus;

                                uint difference0 =
                                    left0 >= right0
                                        ? left0 - right0
                                        : left0 + modulus - right0;

                                uint difference1 =
                                    left1 >= right1
                                        ? left1 - right1
                                        : left1 + modulus - right1;

                                values[leftIndex] = sum0;
                                values[leftIndex + 1] = sum1;

                                values[rightIndex] =
                                    (uint)((ulong)difference0 *
                                           twiddles[twiddleIndex] %
                                           modulus);

                                values[rightIndex + 1] =
                                    (uint)((ulong)difference1 *
                                           twiddles[twiddleIndex + 1] %
                                           modulus);

                                leftIndex += 2;
                                rightIndex += 2;
                                twiddleIndex += 2;
                            }

                            if (leftIndex < butterflyEnd)
                            {
                                leftValue = values[leftIndex];
                                rightValue = values[rightIndex];
                                sum = leftValue + rightValue;

                                if (sum >= modulus)
                                {
                                    sum -= modulus;
                                }

                                uint difference =
                                    leftValue >= rightValue
                                        ? leftValue - rightValue
                                        : leftValue + modulus - rightValue;

                                values[leftIndex] = sum;
                                values[rightIndex] =
                                    (uint)((ulong)difference *
                                           twiddles[twiddleIndex] %
                                           modulus);
                            }
                        }
                    }

                    // L1-local stages: complete each fused block while its
                    // values and short twiddle rows are still hot.
                    for (int blockOffset = tileOffset;
                         blockOffset < tileEnd;
                         blockOffset += fusedNttBlockLength)
                    {
                        int blockEnd =
                            blockOffset +
                            fusedNttBlockLength;

                        for (int stageLength = fusedNttBlockLength;
                             stageLength >= 4;
                             stageLength >>= 1)
                        {
                            int halfLength =
                                stageLength >> 1;

                            int twiddleOffset =
                                twiddlePlan.GetOffset(
                                    halfLength);

                            for (int groupOffset = blockOffset;
                                 groupOffset < blockEnd;
                                 groupOffset += stageLength)
                            {
                                int rightBase =
                                    groupOffset +
                                    halfLength;

                                uint leftValue =
                                    values[groupOffset];

                                uint rightValue =
                                    values[rightBase];

                                uint sum =
                                    leftValue +
                                    rightValue;

                                if (sum >= modulus)
                                {
                                    sum -= modulus;
                                }

                                values[groupOffset] =
                                    sum;

                                values[rightBase] =
                                    leftValue >= rightValue
                                        ? leftValue - rightValue
                                        : leftValue + modulus - rightValue;

                                int leftIndex =
                                    groupOffset + 1;

                                int rightIndex =
                                    rightBase + 1;

                                int butterflyEnd =
                                    groupOffset +
                                    halfLength;

                                int twiddleIndex =
                                    twiddleOffset + 1;

                                // Cache-resident scalar kernel: expose two independent butterflies
                                // per iteration and walk the twiddle table with a direct index.
                                while (leftIndex + 1 < butterflyEnd)
                                {
                                    uint left0 = values[leftIndex];
                                    uint right0 = values[rightIndex];
                                    uint left1 = values[leftIndex + 1];
                                    uint right1 = values[rightIndex + 1];

                                    uint sum0 = left0 + right0;
                                    uint sum1 = left1 + right1;

                                    if (sum0 >= modulus) sum0 -= modulus;
                                    if (sum1 >= modulus) sum1 -= modulus;

                                    uint difference0 =
                                        left0 >= right0
                                            ? left0 - right0
                                            : left0 + modulus - right0;

                                    uint difference1 =
                                        left1 >= right1
                                            ? left1 - right1
                                            : left1 + modulus - right1;

                                    values[leftIndex] = sum0;
                                    values[leftIndex + 1] = sum1;

                                    values[rightIndex] =
                                        (uint)((ulong)difference0 *
                                               twiddles[twiddleIndex] %
                                               modulus);

                                    values[rightIndex + 1] =
                                        (uint)((ulong)difference1 *
                                               twiddles[twiddleIndex + 1] %
                                               modulus);

                                    leftIndex += 2;
                                    rightIndex += 2;
                                    twiddleIndex += 2;
                                }

                                if (leftIndex < butterflyEnd)
                                {
                                    leftValue = values[leftIndex];
                                    rightValue = values[rightIndex];
                                    sum = leftValue + rightValue;

                                    if (sum >= modulus)
                                    {
                                        sum -= modulus;
                                    }

                                    uint difference =
                                        leftValue >= rightValue
                                            ? leftValue - rightValue
                                            : leftValue + modulus - rightValue;

                                    values[leftIndex] = sum;
                                    values[rightIndex] =
                                        (uint)((ulong)difference *
                                               twiddles[twiddleIndex] %
                                               modulus);
                                }
                            }
                        }

                        for (int leftIndex = blockOffset;
                             leftIndex < blockEnd;
                             leftIndex += 2)
                        {
                            uint leftValue =
                                values[leftIndex];

                            uint rightValue =
                                values[leftIndex + 1];

                            uint sum =
                                leftValue +
                                rightValue;

                            if (sum >= modulus)
                            {
                                sum -= modulus;
                            }

                            values[leftIndex] =
                                sum;

                            values[leftIndex + 1] =
                                leftValue >= rightValue
                                    ? leftValue - rightValue
                                    : leftValue + modulus - rightValue;
                        }
                    }

                    if ((tileIndex & 0x0F) == 0x0F)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            });
    }

    /// <summary>
    /// Hierarchical DIT head, inverse of ExecuteForwardL2CacheBlockedTail.
    /// Finish every L1 block first, then merge those blocks through the
    /// L2-local stages while the complete tile remains cache-resident.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteInverseL2CacheBlockedHead(
        uint[] values,
        uint modulus,
        FixedWorkerTeam workers,
        NttTwiddlePlan twiddlePlan,
        int fusedNttBlockLength,
        int l2NttTileLength,
        CancellationToken cancellationToken)
    {
        int tileCount =
            values.Length /
            l2NttTileLength;

        uint[] twiddles =
            twiddlePlan.InverseTwiddles;

        ExecuteRanges(
            tileCount,
            workers,
            cancellationToken,
            (startTile, endTile) =>
            {
                for (int tileIndex = startTile;
                     tileIndex < endTile;
                     tileIndex++)
                {
                    int tileOffset =
                        tileIndex *
                        l2NttTileLength;

                    int tileEnd =
                        tileOffset +
                        l2NttTileLength;

                    // L1-local DIT stages for every fused block.
                    for (int blockOffset = tileOffset;
                         blockOffset < tileEnd;
                         blockOffset += fusedNttBlockLength)
                    {
                        int blockEnd =
                            blockOffset +
                            fusedNttBlockLength;

                        for (int leftIndex = blockOffset;
                             leftIndex < blockEnd;
                             leftIndex += 2)
                        {
                            uint leftValue =
                                values[leftIndex];

                            uint rightValue =
                                values[leftIndex + 1];

                            uint sum =
                                leftValue +
                                rightValue;

                            if (sum >= modulus)
                            {
                                sum -= modulus;
                            }

                            values[leftIndex] =
                                sum;

                            values[leftIndex + 1] =
                                leftValue >= rightValue
                                    ? leftValue - rightValue
                                    : leftValue + modulus - rightValue;
                        }

                        for (int stageLength = 4;
                             stageLength <= fusedNttBlockLength;
                             stageLength <<= 1)
                        {
                            int halfLength =
                                stageLength >> 1;

                            int twiddleOffset =
                                twiddlePlan.GetOffset(
                                    halfLength);

                            for (int groupOffset = blockOffset;
                                 groupOffset < blockEnd;
                                 groupOffset += stageLength)
                            {
                                int rightBase =
                                    groupOffset +
                                    halfLength;

                                uint leftValue =
                                    values[groupOffset];

                                uint rightValue =
                                    values[rightBase];

                                uint sum =
                                    leftValue +
                                    rightValue;

                                if (sum >= modulus)
                                {
                                    sum -= modulus;
                                }

                                values[groupOffset] =
                                    sum;

                                values[rightBase] =
                                    leftValue >= rightValue
                                        ? leftValue - rightValue
                                        : leftValue + modulus - rightValue;

                                int leftIndex =
                                    groupOffset + 1;

                                int rightIndex =
                                    rightBase + 1;

                                int butterflyEnd =
                                    groupOffset +
                                    halfLength;

                                int twiddleIndex =
                                    twiddleOffset + 1;

                                while (leftIndex + 1 < butterflyEnd)
                                {
                                    uint left0 = values[leftIndex];
                                    uint left1 = values[leftIndex + 1];

                                    uint right0 =
                                        (uint)((ulong)values[rightIndex] *
                                               twiddles[twiddleIndex] %
                                               modulus);

                                    uint right1 =
                                        (uint)((ulong)values[rightIndex + 1] *
                                               twiddles[twiddleIndex + 1] %
                                               modulus);

                                    uint sum0 = left0 + right0;
                                    uint sum1 = left1 + right1;

                                    if (sum0 >= modulus) sum0 -= modulus;
                                    if (sum1 >= modulus) sum1 -= modulus;

                                    values[leftIndex] = sum0;
                                    values[leftIndex + 1] = sum1;

                                    values[rightIndex] =
                                        left0 >= right0
                                            ? left0 - right0
                                            : left0 + modulus - right0;

                                    values[rightIndex + 1] =
                                        left1 >= right1
                                            ? left1 - right1
                                            : left1 + modulus - right1;

                                    leftIndex += 2;
                                    rightIndex += 2;
                                    twiddleIndex += 2;
                                }

                                if (leftIndex < butterflyEnd)
                                {
                                    leftValue = values[leftIndex];
                                    rightValue =
                                        (uint)((ulong)values[rightIndex] *
                                               twiddles[twiddleIndex] %
                                               modulus);

                                    sum = leftValue + rightValue;

                                    if (sum >= modulus)
                                    {
                                        sum -= modulus;
                                    }

                                    values[leftIndex] = sum;
                                    values[rightIndex] =
                                        leftValue >= rightValue
                                            ? leftValue - rightValue
                                            : leftValue + modulus - rightValue;
                                }
                            }
                        }
                    }

                    // L2-local DIT merge stages.
                    for (int stageLength = fusedNttBlockLength << 1;
                         stageLength <= l2NttTileLength;
                         stageLength <<= 1)
                    {
                        int halfLength =
                            stageLength >> 1;

                        int twiddleOffset =
                            twiddlePlan.GetOffset(
                                halfLength);

                        for (int groupOffset = tileOffset;
                             groupOffset < tileEnd;
                             groupOffset += stageLength)
                        {
                            int rightBase =
                                groupOffset +
                                halfLength;

                            uint leftValue =
                                values[groupOffset];

                            uint rightValue =
                                values[rightBase];

                            uint sum =
                                leftValue +
                                rightValue;

                            if (sum >= modulus)
                            {
                                sum -= modulus;
                            }

                            values[groupOffset] =
                                sum;

                            values[rightBase] =
                                leftValue >= rightValue
                                    ? leftValue - rightValue
                                    : leftValue + modulus - rightValue;

                            int leftIndex =
                                groupOffset + 1;

                            int rightIndex =
                                rightBase + 1;

                            int butterflyEnd =
                                groupOffset +
                                halfLength;

                            int twiddleIndex =
                                twiddleOffset + 1;

                                                        if (halfLength >= AdaptiveFourWayHalfLength)
                            {
                                while (leftIndex + 3 < butterflyEnd)
                                {
                                    uint left0 = values[leftIndex];
                                    uint left1 = values[leftIndex + 1];
                                    uint left2 = values[leftIndex + 2];
                                    uint left3 = values[leftIndex + 3];

                                    uint right0 =
                                        (uint)((ulong)values[rightIndex] *
                                               twiddles[twiddleIndex] %
                                               modulus);

                                    uint right1 =
                                        (uint)((ulong)values[rightIndex + 1] *
                                               twiddles[twiddleIndex + 1] %
                                               modulus);

                                    uint right2 =
                                        (uint)((ulong)values[rightIndex + 2] *
                                               twiddles[twiddleIndex + 2] %
                                               modulus);

                                    uint right3 =
                                        (uint)((ulong)values[rightIndex + 3] *
                                               twiddles[twiddleIndex + 3] %
                                               modulus);

                                    uint sum0 = left0 + right0;
                                    uint sum1 = left1 + right1;
                                    uint sum2 = left2 + right2;
                                    uint sum3 = left3 + right3;

                                    if (sum0 >= modulus) sum0 -= modulus;
                                    if (sum1 >= modulus) sum1 -= modulus;
                                    if (sum2 >= modulus) sum2 -= modulus;
                                    if (sum3 >= modulus) sum3 -= modulus;

                                    values[leftIndex] = sum0;
                                    values[leftIndex + 1] = sum1;
                                    values[leftIndex + 2] = sum2;
                                    values[leftIndex + 3] = sum3;

                                    values[rightIndex] =
                                        left0 >= right0
                                            ? left0 - right0
                                            : left0 + modulus - right0;

                                    values[rightIndex + 1] =
                                        left1 >= right1
                                            ? left1 - right1
                                            : left1 + modulus - right1;

                                    values[rightIndex + 2] =
                                        left2 >= right2
                                            ? left2 - right2
                                            : left2 + modulus - right2;

                                    values[rightIndex + 3] =
                                        left3 >= right3
                                            ? left3 - right3
                                            : left3 + modulus - right3;

                                    leftIndex += 4;
                                    rightIndex += 4;
                                    twiddleIndex += 4;
                                }
                            }

while (leftIndex + 1 < butterflyEnd)
                            {
                                uint left0 = values[leftIndex];
                                uint left1 = values[leftIndex + 1];

                                uint right0 =
                                    (uint)((ulong)values[rightIndex] *
                                           twiddles[twiddleIndex] %
                                           modulus);

                                uint right1 =
                                    (uint)((ulong)values[rightIndex + 1] *
                                           twiddles[twiddleIndex + 1] %
                                           modulus);

                                uint sum0 = left0 + right0;
                                uint sum1 = left1 + right1;

                                if (sum0 >= modulus) sum0 -= modulus;
                                if (sum1 >= modulus) sum1 -= modulus;

                                values[leftIndex] = sum0;
                                values[leftIndex + 1] = sum1;

                                values[rightIndex] =
                                    left0 >= right0
                                        ? left0 - right0
                                        : left0 + modulus - right0;

                                values[rightIndex + 1] =
                                    left1 >= right1
                                        ? left1 - right1
                                        : left1 + modulus - right1;

                                leftIndex += 2;
                                rightIndex += 2;
                                twiddleIndex += 2;
                            }

                            if (leftIndex < butterflyEnd)
                            {
                                leftValue = values[leftIndex];
                                rightValue =
                                    (uint)((ulong)values[rightIndex] *
                                           twiddles[twiddleIndex] %
                                           modulus);

                                sum = leftValue + rightValue;

                                if (sum >= modulus)
                                {
                                    sum -= modulus;
                                }

                                values[leftIndex] = sum;
                                values[rightIndex] =
                                    leftValue >= rightValue
                                        ? leftValue - rightValue
                                        : leftValue + modulus - rightValue;
                            }
                        }
                    }

                    if ((tileIndex & 0x0F) == 0x0F)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            });
    }

    /// <summary>
    /// Completes the small DIF stages block-by-block.  Once stageLength has
    /// reached the selected fused block length no butterfly can cross a block boundary, so
    /// the remaining stages need no global barrier between them.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteForwardFusedTail(
        uint[] values,
        uint modulus,
        FixedWorkerTeam workers,
        NttTwiddlePlan twiddlePlan,
        int fusedNttBlockLength,
        CancellationToken cancellationToken)
    {
        int blockCount =
            values.Length /
            fusedNttBlockLength;

        uint[] twiddles =
            twiddlePlan.ForwardTwiddles;

        ExecuteRanges(
            blockCount,
            workers,
            cancellationToken,
            (startBlock, endBlock) =>
            {
                for (int blockIndex = startBlock;
                     blockIndex < endBlock;
                     blockIndex++)
                {
                    int blockOffset =
                        blockIndex *
                        fusedNttBlockLength;

                    int blockEnd =
                        blockOffset +
                        fusedNttBlockLength;

                    for (int stageLength = fusedNttBlockLength;
                         stageLength >= 4;
                         stageLength >>= 1)
                    {
                        int halfLength =
                            stageLength >> 1;

                        int twiddleOffset =
                            twiddlePlan.GetOffset(
                                halfLength);

                        for (int groupOffset = blockOffset;
                             groupOffset < blockEnd;
                             groupOffset += stageLength)
                        {
                            int rightBase =
                                groupOffset +
                                halfLength;

                            uint leftValue =
                                values[groupOffset];

                            uint rightValue =
                                values[rightBase];

                            uint sum =
                                leftValue +
                                rightValue;

                            if (sum >= modulus)
                            {
                                sum -= modulus;
                            }

                            values[groupOffset] =
                                sum;

                            values[rightBase] =
                                leftValue >= rightValue
                                    ? leftValue - rightValue
                                    : leftValue + modulus - rightValue;

                            int leftIndex =
                                groupOffset + 1;

                            int rightIndex =
                                rightBase + 1;

                            int butterflyEnd =
                                groupOffset +
                                halfLength;

                            int twiddleIndex =
                                twiddleOffset + 1;

                            // Cache-resident scalar kernel: expose two independent butterflies
                            // per iteration and walk the twiddle table with a direct index.
                            while (leftIndex + 1 < butterflyEnd)
                            {
                                uint left0 = values[leftIndex];
                                uint right0 = values[rightIndex];
                                uint left1 = values[leftIndex + 1];
                                uint right1 = values[rightIndex + 1];

                                uint sum0 = left0 + right0;
                                uint sum1 = left1 + right1;

                                if (sum0 >= modulus) sum0 -= modulus;
                                if (sum1 >= modulus) sum1 -= modulus;

                                uint difference0 =
                                    left0 >= right0
                                        ? left0 - right0
                                        : left0 + modulus - right0;

                                uint difference1 =
                                    left1 >= right1
                                        ? left1 - right1
                                        : left1 + modulus - right1;

                                values[leftIndex] = sum0;
                                values[leftIndex + 1] = sum1;

                                values[rightIndex] =
                                    (uint)((ulong)difference0 *
                                           twiddles[twiddleIndex] %
                                           modulus);

                                values[rightIndex + 1] =
                                    (uint)((ulong)difference1 *
                                           twiddles[twiddleIndex + 1] %
                                           modulus);

                                leftIndex += 2;
                                rightIndex += 2;
                                twiddleIndex += 2;
                            }

                            if (leftIndex < butterflyEnd)
                            {
                                leftValue = values[leftIndex];
                                rightValue = values[rightIndex];
                                sum = leftValue + rightValue;

                                if (sum >= modulus)
                                {
                                    sum -= modulus;
                                }

                                uint difference =
                                    leftValue >= rightValue
                                        ? leftValue - rightValue
                                        : leftValue + modulus - rightValue;

                                values[leftIndex] = sum;
                                values[rightIndex] =
                                    (uint)((ulong)difference *
                                           twiddles[twiddleIndex] %
                                           modulus);
                            }
                        }
                    }

                    // Final DIF stage: twiddle is one for every adjacent pair.
                    for (int leftIndex = blockOffset;
                         leftIndex < blockEnd;
                         leftIndex += 2)
                    {
                        uint leftValue =
                            values[leftIndex];

                        uint rightValue =
                            values[leftIndex + 1];

                        uint sum =
                            leftValue +
                            rightValue;

                        if (sum >= modulus)
                        {
                            sum -= modulus;
                        }

                        values[leftIndex] =
                            sum;

                        values[leftIndex + 1] =
                            leftValue >= rightValue
                                ? leftValue - rightValue
                                : leftValue + modulus - rightValue;
                    }

                    if ((blockIndex & 0x3F) == 0x3F)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            });
    }

    /// <summary>
    /// Completes the small DIT stages block-by-block.  The transform remains
    /// mathematically identical to the stage-at-a-time implementation, but the
    /// values stay cache-resident throughout the first small stages.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteInverseFusedHead(
        uint[] values,
        uint modulus,
        FixedWorkerTeam workers,
        NttTwiddlePlan twiddlePlan,
        int fusedNttBlockLength,
        CancellationToken cancellationToken)
    {
        int blockCount =
            values.Length /
            fusedNttBlockLength;

        uint[] twiddles =
            twiddlePlan.InverseTwiddles;

        ExecuteRanges(
            blockCount,
            workers,
            cancellationToken,
            (startBlock, endBlock) =>
            {
                for (int blockIndex = startBlock;
                     blockIndex < endBlock;
                     blockIndex++)
                {
                    int blockOffset =
                        blockIndex *
                        fusedNttBlockLength;

                    int blockEnd =
                        blockOffset +
                        fusedNttBlockLength;

                    // First DIT stage: adjacent pairs and twiddle 1 only.
                    for (int leftIndex = blockOffset;
                         leftIndex < blockEnd;
                         leftIndex += 2)
                    {
                        uint leftValue =
                            values[leftIndex];

                        uint rightValue =
                            values[leftIndex + 1];

                        uint sum =
                            leftValue +
                            rightValue;

                        if (sum >= modulus)
                        {
                            sum -= modulus;
                        }

                        values[leftIndex] =
                            sum;

                        values[leftIndex + 1] =
                            leftValue >= rightValue
                                ? leftValue - rightValue
                                : leftValue + modulus - rightValue;
                    }

                    for (int stageLength = 4;
                         stageLength <= fusedNttBlockLength;
                         stageLength <<= 1)
                    {
                        int halfLength =
                            stageLength >> 1;

                        int twiddleOffset =
                            twiddlePlan.GetOffset(
                                halfLength);

                        for (int groupOffset = blockOffset;
                             groupOffset < blockEnd;
                             groupOffset += stageLength)
                        {
                            int rightBase =
                                groupOffset +
                                halfLength;

                            uint leftValue =
                                values[groupOffset];

                            uint rightValue =
                                values[rightBase];

                            uint sum =
                                leftValue +
                                rightValue;

                            if (sum >= modulus)
                            {
                                sum -= modulus;
                            }

                            values[groupOffset] =
                                sum;

                            values[rightBase] =
                                leftValue >= rightValue
                                    ? leftValue - rightValue
                                    : leftValue + modulus - rightValue;

                            int leftIndex =
                                groupOffset + 1;

                            int rightIndex =
                                rightBase + 1;

                            int butterflyEnd =
                                groupOffset +
                                halfLength;

                            int twiddleIndex =
                                twiddleOffset + 1;

                            while (leftIndex + 1 < butterflyEnd)
                            {
                                uint left0 = values[leftIndex];
                                uint left1 = values[leftIndex + 1];

                                uint right0 =
                                    (uint)((ulong)values[rightIndex] *
                                           twiddles[twiddleIndex] %
                                           modulus);

                                uint right1 =
                                    (uint)((ulong)values[rightIndex + 1] *
                                           twiddles[twiddleIndex + 1] %
                                           modulus);

                                uint sum0 = left0 + right0;
                                uint sum1 = left1 + right1;

                                if (sum0 >= modulus) sum0 -= modulus;
                                if (sum1 >= modulus) sum1 -= modulus;

                                values[leftIndex] = sum0;
                                values[leftIndex + 1] = sum1;

                                values[rightIndex] =
                                    left0 >= right0
                                        ? left0 - right0
                                        : left0 + modulus - right0;

                                values[rightIndex + 1] =
                                    left1 >= right1
                                        ? left1 - right1
                                        : left1 + modulus - right1;

                                leftIndex += 2;
                                rightIndex += 2;
                                twiddleIndex += 2;
                            }

                            if (leftIndex < butterflyEnd)
                            {
                                leftValue = values[leftIndex];
                                rightValue =
                                    (uint)((ulong)values[rightIndex] *
                                           twiddles[twiddleIndex] %
                                           modulus);

                                sum = leftValue + rightValue;

                                if (sum >= modulus)
                                {
                                    sum -= modulus;
                                }

                                values[leftIndex] = sum;
                                values[rightIndex] =
                                    leftValue >= rightValue
                                        ? leftValue - rightValue
                                        : leftValue + modulus - rightValue;
                            }
                        }
                    }

                    if ((blockIndex & 0x3F) == 0x3F)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            });
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ExecuteLengthTwoButterflies(
        uint[] values,
        uint modulus,
        bool normalize,
        uint inverseLength,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        int pairCount =
            values.Length >> 1;

        ExecuteRanges(
            pairCount,
            workers,
            cancellationToken,
            (start, end) =>
            {
                int leftIndex =
                    start << 1;

                for (int pairIndex = start;
                     pairIndex < end;
                     pairIndex++, leftIndex += 2)
                {
                    uint leftValue =
                        values[leftIndex];

                    uint rightValue =
                        values[leftIndex + 1];

                    uint sum =
                        leftValue +
                        rightValue;

                    if (sum >= modulus)
                    {
                        sum -= modulus;
                    }

                    uint difference =
                        leftValue >= rightValue
                            ? leftValue - rightValue
                            : leftValue + modulus - rightValue;

                    if (normalize)
                    {
                        values[leftIndex] =
                            (uint)((ulong)sum *
                                   inverseLength %
                                   modulus);

                        values[leftIndex + 1] =
                            (uint)((ulong)difference *
                                   inverseLength %
                                   modulus);
                    }
                    else
                    {
                        values[leftIndex] =
                            sum;

                        values[leftIndex + 1] =
                            difference;
                    }
                }
            });
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void BuildTwiddleTables(
        uint[] forwardTwiddles,
        uint[] inverseTwiddles,
        int offset,
        int halfLength,
        uint root,
        uint modulus,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken)
    {
        forwardTwiddles[offset] = 1;
        inverseTwiddles[offset] = 1;

        if (halfLength <= 1)
        {
            return;
        }

        const int ParallelTwiddleThreshold =
            1 << 15;

        if (workers.WorkerCount == 1 ||
            halfLength < ParallelTwiddleThreshold)
        {
            ulong twiddle =
                root;

            for (int index = 1;
                 index < halfLength;
                 index++)
            {
                uint current =
                    (uint)twiddle;

                forwardTwiddles[offset + index] =
                    current;

                // If w has order 2H, then w^-j = -w^(H-j).  Filling the
                // inverse table while the forward power is already in a
                // register avoids rebuilding an entire inverse twiddle chain.
                inverseTwiddles[
                    offset +
                    halfLength -
                    index] =
                    modulus - current;

                if (index + 1 < halfLength)
                {
                    twiddle =
                        twiddle *
                        root %
                        modulus;
                }

                if ((index & 0xFFFF) == 0xFFFF)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            return;
        }

        ExecuteRanges(
            halfLength - 1,
            workers,
            cancellationToken,
            (start, end) =>
            {
                int firstIndex =
                    start + 1;

                int endIndex =
                    end + 1;

                ulong twiddle =
                    firstIndex == 1
                        ? root
                        : ModPow(
                            root,
                            (uint)firstIndex,
                            modulus);

                for (int index = firstIndex;
                     index < endIndex;
                     index++)
                {
                    uint current =
                        (uint)twiddle;

                    forwardTwiddles[offset + index] =
                        current;

                    inverseTwiddles[
                        offset +
                        halfLength -
                        index] =
                        modulus - current;

                    if (index + 1 < endIndex)
                    {
                        twiddle =
                            twiddle *
                            root %
                            modulus;
                    }
                }
            });
    }

    private sealed class NttTwiddlePlan : IDisposable
    {
        private uint[]? _forwardTwiddles;
        private uint[]? _inverseTwiddles;

        private readonly NttTwiddleBufferPool _bufferPool;

        // Shared by all worker teams in one Pow operation. Stage values are
        // immutable once published. Two split branches are allowed to race
        // while producing the same deterministic table; Volatile publication
        // guarantees a branch that observes Ready also observes every table
        // write from the branch that completed first.
        private readonly int[] _readyStages =
            new int[32];

        public NttTwiddlePlan(
            NttTwiddleBufferPool bufferPool)
        {
            _bufferPool =
                bufferPool ??
                throw new ArgumentNullException(
                    nameof(bufferPool));

            // The plan is reused across all transform lengths handled by one
            // worker team. The underlying arrays come from a Pow-scoped pool:
            // branch teams can hand them to the final-combine team, but no
            // multi-megabyte twiddle table survives the Pow lifetime.
            MaximumHalfLength =
                SelectMaximumCachedTwiddleCount();

            int capacity =
                checked(
                    MaximumHalfLength << 1);

            _forwardTwiddles =
                _bufferPool.Rent(
                    capacity);

            _inverseTwiddles =
                _bufferPool.Rent(
                    capacity);
        }

        public int MaximumHalfLength { get; }

        public uint[] ForwardTwiddles =>
            _forwardTwiddles ??
            throw new InvalidOperationException(
                "Twiddle cache is not available for this transform.");

        public uint[] InverseTwiddles =>
            _inverseTwiddles ??
            throw new InvalidOperationException(
                "Twiddle cache is not available for this transform.");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanCache(
            int halfLength)
        {
            return halfLength >= 2 &&
                   halfLength <= MaximumHalfLength &&
                   _forwardTwiddles is not null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetOffset(
            int halfLength)
        {
            return checked(
                (MaximumHalfLength - halfLength) << 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsStageReady(
            int halfLength)
        {
            return Volatile.Read(
                       ref _readyStages[
                           GetStageIndex(
                               halfLength)]) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkStageReady(
            int halfLength)
        {
            Volatile.Write(
                ref _readyStages[
                    GetStageIndex(
                        halfLength)],
                1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetStageIndex(
            int halfLength)
        {
            // halfLength is always a power of two for NTT stages.  This loop
            // only runs on stage setup/cache checks, never per butterfly.
            int index = 0;

            while ((halfLength >>= 1) != 0)
            {
                index++;
            }

            return index;
        }

        public void Dispose()
        {
            uint[]? forward =
                Interlocked.Exchange(
                    ref _forwardTwiddles,
                    null);

            uint[]? inverse =
                Interlocked.Exchange(
                    ref _inverseTwiddles,
                    null);

            if (forward is not null)
            {
                _bufferPool.Return(
                    forward);
            }

            if (inverse is not null)
            {
                _bufferPool.Return(
                    inverse);
            }
        }
    }

    private static int GetSegmentsPerGroup(
        int halfLength,
        int groupCount,
        int workerCount)
    {
        return Math.Min(
            halfLength,
            Math.Max(
                1,
                (workerCount + groupCount - 1) /
                groupCount));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GetSegmentBounds(
        int segmentIndex,
        int segmentsPerGroup,
        int halfLength,
        out int groupIndex,
        out int butterflyStart,
        out int butterflyEnd)
    {
        // Once groupCount >= workerCount, segmentsPerGroup is one.  This is
        // the common case for most late NTT stages and can represent tens of
        // millions of groups.  Avoid two integer divisions for every group.
        if (segmentsPerGroup == 1)
        {
            groupIndex =
                segmentIndex;

            butterflyStart =
                0;

            butterflyEnd =
                halfLength;

            return;
        }

        groupIndex =
            segmentIndex /
            segmentsPerGroup;

        int segmentInGroup =
            segmentIndex -
            groupIndex *
            segmentsPerGroup;

        butterflyStart =
            segmentInGroup *
            halfLength /
            segmentsPerGroup;

        butterflyEnd =
            (segmentInGroup + 1) *
            halfLength /
            segmentsPerGroup;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static ParallelBigUnsigned CreateFromCoefficients(
        ulong[] coefficients,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        long carryStarted =
            Stopwatch.GetTimestamp();

        var limbs =
            new uint[coefficients.Length + 8];

        // One Span/ReadOnlySpan is created for the whole carry pass.  Do not
        // repeatedly Slice/AsSpan inside the loop: that was part of the
        // previous regression.
        ReadOnlySpan<ulong> source =
            coefficients;

        Span<uint> destination =
            limbs;

        ulong carry = 0;
        int limbCount = 0;

        for (int index = 0;
             index < source.Length;
             index++)
        {
            if ((index & 0xFFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            ulong value =
                source[index] +
                carry;

            // Compute the quotient once.  In optimized x64 code RyuJIT
            // strength-reduces division by the constant 10,000 to a BMI2
            // MULX/shift sequence.  Derive the remainder from that quotient
            // so the carry pass does not need a second reciprocal multiply.
            ulong quotient =
                value /
                LimbBase;

            destination[limbCount++] =
                (uint)(value -
                       quotient *
                       LimbBase);

            carry =
                quotient;
        }

        while (carry > 0)
        {
            ulong quotient =
                carry /
                LimbBase;

            destination[limbCount++] =
                (uint)(carry -
                       quotient *
                       LimbBase);

            carry =
                quotient;
        }

        if (limbCount !=
            limbs.Length)
        {
            Array.Resize(
                ref limbs,
                Math.Max(
                    1,
                    limbCount));
        }

        diagnostics.CarryTicks +=
            Stopwatch.GetTimestamp() -
            carryStarted;

        return new ParallelBigUnsigned(
            limbs,
            takeOwnership: true);
    }

    private sealed class PowerDiagnosticsCollector
    {
        public int NttMultiplicationCount;
        public long BitReversalTicks;
        public long ForwardTransformTicks;
        public long PointwiseTicks;
        public long InverseTransformTicks;
        public long CrtTicks;
        public long CarryTicks;

        private long _nttWorkspacePeakBytes;
        private long _nttPoolPeakRetainedBytes;
        private int _nttBufferRentCount;
        private int _nttBufferReuseCount;

        private bool _usedMemoryBoundedLargePower;
        private int _largePowerChunkExponent;
        private int _segmentedNttMultiplicationCount;
        private int _segmentedNttPairCount;

        private bool _usedExponentSplit;
        private int _firstExponent;
        private int _secondExponent;
        private int _firstBranchWorkerCount;
        private int _secondBranchWorkerCount;
        private long _firstBranchTicks;
        private long _secondBranchTicks;
        private long _finalCombineTicks;

        public static PowerDiagnosticsCollector CombineParallelBranches(
            PowerDiagnosticsCollector first,
            PowerDiagnosticsCollector second)
        {
            // The two branches run at the same time. For each phase, the
            // critical-path estimate is the slower branch rather than the sum
            // of both CPU times; the final combine is added normally later.
            return new PowerDiagnosticsCollector
            {
                NttMultiplicationCount =
                    checked(
                        first.NttMultiplicationCount +
                        second.NttMultiplicationCount),
                BitReversalTicks =
                    Math.Max(
                        first.BitReversalTicks,
                        second.BitReversalTicks),
                ForwardTransformTicks =
                    Math.Max(
                        first.ForwardTransformTicks,
                        second.ForwardTransformTicks),
                PointwiseTicks =
                    Math.Max(
                        first.PointwiseTicks,
                        second.PointwiseTicks),
                InverseTransformTicks =
                    Math.Max(
                        first.InverseTransformTicks,
                        second.InverseTransformTicks),
                CrtTicks =
                    Math.Max(
                        first.CrtTicks,
                        second.CrtTicks),
                CarryTicks =
                    Math.Max(
                        first.CarryTicks,
                        second.CarryTicks)
            };
        }

        public void AccumulateSnapshot(
            ParallelPowerDiagnostics snapshot)
        {
            NttMultiplicationCount =
                checked(
                    NttMultiplicationCount +
                    snapshot.NttMultiplicationCount);

            BitReversalTicks +=
                ToTimestampTicks(
                    snapshot.BitReversal);
            ForwardTransformTicks +=
                ToTimestampTicks(
                    snapshot.ForwardTransform);
            PointwiseTicks +=
                ToTimestampTicks(
                    snapshot.Pointwise);
            InverseTransformTicks +=
                ToTimestampTicks(
                    snapshot.InverseTransform);
            CrtTicks +=
                ToTimestampTicks(
                    snapshot.Crt);
            CarryTicks +=
                ToTimestampTicks(
                    snapshot.Carry);

            _nttWorkspacePeakBytes =
                Math.Max(
                    _nttWorkspacePeakBytes,
                    snapshot.NttWorkspacePeakBytes);

            _nttPoolPeakRetainedBytes =
                Math.Max(
                    _nttPoolPeakRetainedBytes,
                    snapshot.NttPoolPeakRetainedBytes);

            _nttBufferRentCount =
                checked(
                    _nttBufferRentCount +
                    snapshot.NttBufferRentCount);

            _nttBufferReuseCount =
                checked(
                    _nttBufferReuseCount +
                    snapshot.NttBufferReuseCount);
        }

        public void ConfigureSegmentedNttMultiplication(
            int segmentPairCount)
        {
            _segmentedNttMultiplicationCount =
                checked(
                    _segmentedNttMultiplicationCount +
                    1);

            _segmentedNttPairCount =
                checked(
                    _segmentedNttPairCount +
                    segmentPairCount);
        }

        public void ConfigureLargeMemoryBoundedMode(
            int chunkExponent)
        {
            _usedMemoryBoundedLargePower =
                true;

            _largePowerChunkExponent =
                chunkExponent;
        }

        public void ConfigureExponentSplit(
            int originalExponent,
            int firstExponent,
            int secondExponent,
            int firstBranchWorkerCount,
            int secondBranchWorkerCount,
            long firstBranchTicks,
            long secondBranchTicks,
            long finalCombineTicks)
        {
            Debug.Assert(
                firstExponent +
                secondExponent ==
                originalExponent);

            _usedExponentSplit = true;
            _firstExponent = firstExponent;
            _secondExponent = secondExponent;
            _firstBranchWorkerCount =
                firstBranchWorkerCount;
            _secondBranchWorkerCount =
                secondBranchWorkerCount;
            _firstBranchTicks = firstBranchTicks;
            _secondBranchTicks = secondBranchTicks;
            _finalCombineTicks = finalCombineTicks;
        }

        public void ConfigureNttBufferPool(
            NttBufferPoolStatistics statistics)
        {
            _nttWorkspacePeakBytes =
                Math.Max(
                    _nttWorkspacePeakBytes,
                    statistics.PeakLeasedBytes);

            _nttPoolPeakRetainedBytes =
                Math.Max(
                    _nttPoolPeakRetainedBytes,
                    statistics.PeakRetainedBytes);

            _nttBufferRentCount =
                checked(
                    _nttBufferRentCount +
                    statistics.RentCount);

            _nttBufferReuseCount =
                checked(
                    _nttBufferReuseCount +
                    statistics.ReuseCount);
        }

        public ParallelPowerDiagnostics CreateSnapshot(
            int workerCount)
        {
            return new ParallelPowerDiagnostics(
                workerCount,
                NttMultiplicationCount,
                ToTimeSpan(BitReversalTicks),
                ToTimeSpan(ForwardTransformTicks),
                ToTimeSpan(PointwiseTicks),
                ToTimeSpan(InverseTransformTicks),
                ToTimeSpan(CrtTicks),
                ToTimeSpan(CarryTicks),
                _usedExponentSplit,
                _firstExponent,
                _secondExponent,
                _firstBranchWorkerCount,
                _secondBranchWorkerCount,
                ToTimeSpan(_firstBranchTicks),
                ToTimeSpan(_secondBranchTicks),
                ToTimeSpan(_finalCombineTicks),
                _nttWorkspacePeakBytes,
                _nttPoolPeakRetainedBytes,
                _nttBufferRentCount,
                _nttBufferReuseCount,
                _usedMemoryBoundedLargePower,
                _largePowerChunkExponent,
                _segmentedNttMultiplicationCount,
                _segmentedNttPairCount);
        }

        private static long ToTimestampTicks(
            TimeSpan duration)
        {
            return checked(
                (long)Math.Round(
                    duration.TotalSeconds *
                    Stopwatch.Frequency,
                    MidpointRounding.AwayFromZero));
        }

        private static TimeSpan ToTimeSpan(
            long timestampTicks)
        {
            return TimeSpan.FromSeconds(
                timestampTicks /
                (double)Stopwatch.Frequency);
        }
    }

    private static void ExecuteRanges(
        int itemCount,
        FixedWorkerTeam workers,
        CancellationToken cancellationToken,
        Action<int, int> body)
    {
        if (itemCount <= 0)
        {
            return;
        }

        if (workers.WorkerCount == 1)
        {
            cancellationToken.ThrowIfCancellationRequested();
            body(
                0,
                itemCount);
            return;
        }

        workers.Execute(
            itemCount,
            body);
    }

    /// <summary>
    /// Owns exactly one immutable twiddle plan per NTT modulus for one complete
    /// Pow operation. PowSplit worker teams share these plans concurrently.
    /// This removes the duplicate forward/inverse table pairs that v30 kept in
    /// each branch while preserving the same hot-loop array lookups.
    /// </summary>
    private sealed class SharedNttTwiddlePlans : IDisposable
    {
        private readonly object _gate =
            new();

        private readonly NttTwiddleBufferPool _bufferPool;

        private NttTwiddlePlan? _firstPlan;
        private NttTwiddlePlan? _secondPlan;
        private bool _released;

        public SharedNttTwiddlePlans(
            NttTwiddleBufferPool bufferPool)
        {
            _bufferPool =
                bufferPool ??
                throw new ArgumentNullException(
                    nameof(bufferPool));
        }

        public NttTwiddlePlan Get(
            uint modulus)
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(
                    _released,
                    this);

                if (modulus == FirstModulus)
                {
                    return _firstPlan ??=
                        new NttTwiddlePlan(
                            _bufferPool);
                }

                if (modulus == SecondModulus)
                {
                    return _secondPlan ??=
                        new NttTwiddlePlan(
                            _bufferPool);
                }

                throw new ArgumentOutOfRangeException(
                    nameof(modulus),
                    modulus,
                    "Unsupported NTT modulus.");
            }
        }

        public void ReleasePlans()
        {
            NttTwiddlePlan? first;
            NttTwiddlePlan? second;

            lock (_gate)
            {
                if (_released)
                {
                    return;
                }

                _released =
                    true;

                first =
                    _firstPlan;

                second =
                    _secondPlan;

                _firstPlan =
                    null;

                _secondPlan =
                    null;
            }

            // Dispose outside the plan gate: returning arrays takes the small
            // twiddle-pool lock and there is no reason to nest those locks.
            first?.Dispose();
            second?.Dispose();
        }

        public void Dispose()
        {
            ReleasePlans();
        }
    }

    /// <summary>
    /// Small Pow-scoped backing pool for the shared forward/inverse twiddle
    /// tables. v31 needs at most four arrays total (forward + inverse for each
    /// modulus); after SharedNttTwiddlePlans releases them, the cache is dropped
    /// immediately before Pow returns.
    /// </summary>
    private sealed class NttTwiddleBufferPool : IDisposable
    {
        private const int MaximumRetainedBufferCount = 4;

        private readonly object _gate =
            new();

        private readonly Stack<uint[]> _buffers =
            new(MaximumRetainedBufferCount);

        private int _bufferLength;
        private bool _disposed;

        public uint[] Rent(
            int length)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                length);

            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(
                    _disposed,
                    this);

                if (_bufferLength != 0 &&
                    _bufferLength != length)
                {
                    _buffers.Clear();
                }

                _bufferLength =
                    length;

                if (_buffers.Count > 0)
                {
                    return _buffers.Pop();
                }
            }

            // Every stage is fully initialized before its ready flag is set, so
            // unused table capacity does not need zero initialization.
            return GC.AllocateUninitializedArray<uint>(
                length);
        }

        public void Return(
            uint[] buffer)
        {
            ArgumentNullException.ThrowIfNull(
                buffer);

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                if (_bufferLength == 0)
                {
                    _bufferLength =
                        buffer.Length;
                }

                if (buffer.Length ==
                        _bufferLength &&
                    _buffers.Count <
                        MaximumRetainedBufferCount)
                {
                    _buffers.Push(
                        buffer);
                }
            }
        }

        public void ReleaseCachedBuffers()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _buffers.Clear();
                _bufferLength =
                    0;
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed =
                    true;
                _buffers.Clear();
                _bufferLength =
                    0;
            }
        }
    }

    private readonly record struct NttBufferPoolStatistics(
        long PeakLeasedBytes,
        long PeakRetainedBytes,
        int RentCount,
        int ReuseCount);

    /// <summary>
    /// Reuses the two large temporary uint[] workspaces needed by an NTT
    /// convolution. Only arrays of the largest transform length observed so
    /// far are retained, and at most two are cached because a non-square
    /// convolution can have exactly two transforms live at once.
    ///
    /// The pool is scoped to exactly one Pow operation. Split branches and the
    /// final combine share it while the calculation is active; Dispose then
    /// drops every retained reference so 256 MiB-class workspaces become
    /// collectible immediately instead of surviving with application lifetime.
    /// </summary>
    private sealed class NttBufferPool : IDisposable
    {
        private const int MaximumRetainedBufferCount = 2;

        private readonly object _gate =
            new();

        private readonly Stack<uint[]> _buffers =
            new(MaximumRetainedBufferCount);

        private int _retainedLength;
        private long _currentLeasedBytes;
        private long _peakLeasedBytes;
        private long _peakRetainedBytes;
        private int _rentCount;
        private int _reuseCount;
        private int _leasedBufferCount;
        private bool _disposed;

        public uint[] Rent(
            int length,
            out bool reused)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                length);

            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(
                    _disposed,
                    this);

                if (length >
                    _retainedLength)
                {
                    _buffers.Clear();
                    _retainedLength =
                        length;
                }

                uint[] buffer;

                if (length ==
                        _retainedLength &&
                    _buffers.Count > 0)
                {
                    reused =
                        true;

                    buffer =
                        _buffers.Pop();

                    _reuseCount++;
                }
                else
                {
                    reused =
                        false;

                    // PrepareNttBuffer overwrites the source prefix and
                    // explicitly clears only the required zero-padding tail.
                    // Avoid zeroing the entire 128-256 MiB workspace here.
                    buffer =
                        GC.AllocateUninitializedArray<uint>(
                            length);
                }

                _rentCount++;
                _leasedBufferCount++;

                _currentLeasedBytes =
                    checked(
                        _currentLeasedBytes +
                        (long)buffer.Length *
                        sizeof(uint));

                _peakLeasedBytes =
                    Math.Max(
                        _peakLeasedBytes,
                        _currentLeasedBytes);

                return buffer;
            }
        }

        public void Return(
            uint[] buffer)
        {
            ArgumentNullException.ThrowIfNull(
                buffer);

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _leasedBufferCount =
                    Math.Max(
                        0,
                        _leasedBufferCount - 1);

                _currentLeasedBytes =
                    Math.Max(
                        0L,
                        _currentLeasedBytes -
                        (long)buffer.Length *
                        sizeof(uint));

                if (buffer.Length >
                    _retainedLength)
                {
                    _buffers.Clear();
                    _retainedLength =
                        buffer.Length;
                }

                if (buffer.Length ==
                        _retainedLength &&
                    _buffers.Count <
                        MaximumRetainedBufferCount)
                {
                    _buffers.Push(
                        buffer);

                    _peakRetainedBytes =
                        Math.Max(
                            _peakRetainedBytes,
                            (long)_buffers.Count *
                            _retainedLength *
                            sizeof(uint));
                }
            }
        }

        public NttBufferPoolStatistics CreateStatisticsSnapshot()
        {
            lock (_gate)
            {
                return new NttBufferPoolStatistics(
                    _peakLeasedBytes,
                    _peakRetainedBytes,
                    _rentCount,
                    _reuseCount);
            }
        }

        public void ReleaseCachedBuffers()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                Debug.Assert(
                    _leasedBufferCount == 0);

                _buffers.Clear();
                _retainedLength =
                    0;
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                Debug.Assert(
                    _leasedBufferCount == 0);

                _disposed =
                    true;

                _buffers.Clear();
                _retainedLength =
                    0;
            }
        }
    }

    /// <summary>
    /// A fixed set of dedicated workers reused by every NTT stage in one power
    /// calculation. This avoids rebuilding and rescheduling a Parallel.For
    /// graph at every stage of transforms as large as 2^26.
    /// </summary>
    private sealed class FixedWorkerTeam : IDisposable
    {
        private readonly object _gate =
            new();

        private readonly Thread[] _threads;
        private readonly CountdownEvent _completed;

        private Action<int, int>? _body;
        private ExceptionDispatchInfo? _failure;
        private int _generation;
        private int _itemCount;
        private bool _stopping;

        // v31: twiddle plans are Pow-scoped and shared by every worker team.
        // The hot NTT kernels still receive the same NttTwiddlePlan object; only
        // ownership/lifetime changed, so no butterfly arithmetic is affected.
        private readonly SharedNttTwiddlePlans _sharedNttTwiddlePlans;
        private NttTwiddlePlan? _firstTwiddlePlanRef;
        private NttTwiddlePlan? _secondTwiddlePlanRef;

        // v32 normally reuses the dead tail of inverse P2 as CRT scratch. Keep
        // one bounded team-local array only as a fallback for transforms whose
        // unused tail is too short; no shared LOH pool retains it beyond this
        // worker-team lifetime.
        private ulong[]? _crtCarryScratch;

        // Large-mode segmented multiplication reuses one compact P1/result
        // buffer across every segment pair and merge. The legacy <=10M path
        // never requests this scratch, so its memory/lifetime is unchanged.
        private uint[]? _segmentedProductScratch;

        // The temporary value arrays are owned by one pool for the complete
        // Pow operation.  Split branches intentionally share the same pool;
        // the pool itself is synchronized and caps retention to two arrays.
        private readonly NttBufferPool _nttBufferPool;

        public FixedWorkerTeam(
            int workerCount,
            NttBufferPool nttBufferPool,
            SharedNttTwiddlePlans sharedNttTwiddlePlans)
        {
            _nttBufferPool =
                nttBufferPool ??
                throw new ArgumentNullException(
                    nameof(nttBufferPool));

            _sharedNttTwiddlePlans =
                sharedNttTwiddlePlans ??
                throw new ArgumentNullException(
                    nameof(sharedNttTwiddlePlans));

            WorkerCount =
                Math.Max(
                    1,
                    workerCount);

            _threads =
                WorkerCount == 1
                    ? Array.Empty<Thread>()
                    : new Thread[WorkerCount];

            _completed =
                new CountdownEvent(0);

            for (int workerIndex = 0;
                 workerIndex < _threads.Length;
                 workerIndex++)
            {
                int capturedWorkerIndex =
                    workerIndex;

                _threads[workerIndex] =
                    new Thread(
                        () => WorkerLoop(
                            capturedWorkerIndex))
                    {
                        IsBackground = true,
                        Name =
                            $"MathSolver NTT {workerIndex + 1}"
                    };

                _threads[workerIndex].Start();
            }
        }

        public int WorkerCount { get; }

        public uint[] RentNttBuffer(
            int length,
            out bool reused)
        {
            return _nttBufferPool.Rent(
                length,
                out reused);
        }

        public void ReturnNttBuffer(
            uint[] buffer)
        {
            _nttBufferPool.Return(
                buffer);
        }

        public ulong[] GetCrtCarryScratch(
            int minimumLength)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                minimumLength);

            ulong[]? scratch =
                _crtCarryScratch;

            if (scratch is null ||
                scratch.Length < minimumLength)
            {
                // Every CRT block overwrites the complete scratch prefix
                // before carry reads it. Avoid an unnecessary LOH zero-fill
                // when the inverse-transform tail is too small and a dedicated
                // fallback scratch array is actually required.
                scratch =
                    GC.AllocateUninitializedArray<ulong>(
                        minimumLength);

                _crtCarryScratch =
                    scratch;
            }

            return scratch;
        }

        public void ReleaseCrtCarryScratch()
        {
            _crtCarryScratch =
                null;
        }

        public uint[] GetSegmentedProductScratch(
            int minimumLength)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                minimumLength);

            uint[]? scratch =
                _segmentedProductScratch;

            if (scratch is null ||
                scratch.Length < minimumLength)
            {
                scratch =
                    GC.AllocateUninitializedArray<uint>(
                        minimumLength);

                _segmentedProductScratch =
                    scratch;
            }

            return scratch;
        }

        // FixedWorkerTeam itself is private to ParallelBigUnsigned, so this
        // member cannot escape the enclosing type. The returned plan is owned
        // by the Pow-scoped SharedNttTwiddlePlans rather than by this team.
        public NttTwiddlePlan GetTwiddlePlan(
            uint modulus)
        {
            if (modulus == FirstModulus)
            {
                return _firstTwiddlePlanRef ??=
                    _sharedNttTwiddlePlans.Get(
                        modulus);
            }

            if (modulus == SecondModulus)
            {
                return _secondTwiddlePlanRef ??=
                    _sharedNttTwiddlePlans.Get(
                        modulus);
            }

            throw new ArgumentOutOfRangeException(
                nameof(modulus),
                modulus,
                "Unsupported NTT modulus.");
        }

        public void Execute(
            int itemCount,
            Action<int, int> body)
        {
            if (WorkerCount == 1)
            {
                body(
                    0,
                    itemCount);
                return;
            }

            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(
                    _stopping,
                    this);

                _itemCount =
                    itemCount;

                _body =
                    body;

                _failure =
                    null;

                _completed.Reset(
                    WorkerCount);

                _generation++;

                Monitor.PulseAll(
                    _gate);
            }

            // Workers observe cancellation inside the supplied hot loop. Wait
            // for all of them before propagating an exception so no worker can
            // still touch buffers that the caller is about to release.
            _completed.Wait();

            // v32: every worker has finished touching the scheduled buffers at
            // this point. Do not let the team field retain the last closure
            // until the next NTT stage (or until Dispose), because that closure
            // can capture 128-256 MiB transform arrays. Keep only a local
            // exception reference long enough to rethrow it.
            _body =
                null;

            _itemCount =
                0;

            ExceptionDispatchInfo? failure =
                _failure;

            _failure =
                null;

            failure?.Throw();
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_stopping)
                {
                    return;
                }

                _stopping =
                    true;

                _generation++;

                Monitor.PulseAll(
                    _gate);
            }

            foreach (Thread thread in
                     _threads)
            {
                thread.Join();
            }

            // Shared twiddle plans outlive individual branch/final worker
            // teams and are released once the complete Pow operation ends.
            // Drop only this team's cheap cached references.
            _firstTwiddlePlanRef = null;
            _secondTwiddlePlanRef = null;
            _crtCarryScratch = null;
            _segmentedProductScratch = null;

            // The last scheduled lambda can capture a very large transform
            // buffer. Drop scheduler references explicitly before the Gen2/LOH
            // sweep instead of waiting for this team object itself to die.
            _body = null;
            _failure = null;
            _itemCount = 0;

            _completed.Dispose();
        }

        private void WorkerLoop(
            int workerIndex)
        {
            int observedGeneration = 0;

            while (true)
            {
                Action<int, int>? body;
                int itemCount;

                lock (_gate)
                {
                    while (!_stopping &&
                           observedGeneration ==
                           _generation)
                    {
                        Monitor.Wait(
                            _gate);
                    }

                    if (_stopping)
                    {
                        return;
                    }

                    observedGeneration =
                        _generation;

                    body =
                        _body;

                    itemCount =
                        _itemCount;
                }

                try
                {
                    int start =
                        workerIndex *
                        itemCount /
                        WorkerCount;

                    int end =
                        (workerIndex + 1) *
                        itemCount /
                        WorkerCount;

                    if (start < end)
                    {
                        body!(
                            start,
                            end);
                    }
                }
                catch (Exception exception)
                {
                    Interlocked.CompareExchange(
                        ref _failure,
                        ExceptionDispatchInfo.Capture(
                            exception),
                        null);
                }
                finally
                {
                    // Release the per-thread delegate reference before the
                    // completion signal tells the caller it may recycle the
                    // captured NTT buffers. This complements clearing _body in
                    // Execute() and makes the end of every scheduled range a
                    // hard lifetime boundary for large array closures.
                    body =
                        null;

                    _completed.Signal();
                }
            }
        }
    }

    private static int CountMultiplications(
        int exponent)
    {
        int operations = 0;
        bool resultInitialized = false;
        int remaining = exponent;

        while (remaining > 0)
        {
            if ((remaining & 1) != 0)
            {
                if (resultInitialized)
                {
                    operations++;
                }
                else
                {
                    resultInitialized = true;
                }
            }

            remaining >>= 1;

            if (remaining > 0)
            {
                operations++;
            }
        }

        return Math.Max(
            1,
            operations);
    }

    private static bool TryCreateExponentSplit(
        int exponent,
        int workerCount,
        out int firstExponent,
        out int secondExponent)
    {
        firstExponent = 0;
        secondExponent = 0;

        // Each branch needs at least two workers. Very unbalanced exponents
        // are kept on the original single-chain engine because the smaller
        // branch would finish early and leave half the SMT budget idle.
        if (exponent < 3 ||
            workerCount < 4)
        {
            return false;
        }

        for (int bitIndex = 30;
             bitIndex >= 0;
             bitIndex--)
        {
            int component =
                1 << bitIndex;

            if ((exponent & component) == 0)
            {
                continue;
            }

            if (firstExponent <=
                secondExponent)
            {
                firstExponent +=
                    component;
            }
            else
            {
                secondExponent +=
                    component;
            }
        }

        // A pure power of two is already optimal as one squaring chain.
        if (firstExponent == 0 ||
            secondExponent == 0)
        {
            return false;
        }

        int smallerExponent =
            Math.Min(
                firstExponent,
                secondExponent);

        int largerExponent =
            Math.Max(
                firstExponent,
                secondExponent);

        if ((long)smallerExponent *
            100L >=
            (long)largerExponent *
            35L)
        {
            return true;
        }

        // At large exponents, the highest set bit can dwarf all remaining
        // bits (10,000,000 = 8,388,608 + 1,611,392). Keep the two SMT teams
        // useful by switching to an exact near-half m+n split. Avoid equal
        // exponents so the branches remain independently profileable.
        firstExponent =
            exponent / 2;

        secondExponent =
            exponent -
            firstExponent;

        if (firstExponent ==
            secondExponent)
        {
            firstExponent++;
            secondExponent--;
        }

        return secondExponent > 0;
    }

    private static ulong ModPow(
        uint value,
        uint exponent,
        uint modulus)
    {
        ulong result = 1;
        ulong factor = value;
        uint remaining = exponent;

        while (remaining > 0)
        {
            if ((remaining & 1u) != 0)
            {
                result =
                    result *
                    factor %
                    modulus;
            }

            factor =
                factor *
                factor %
                modulus;

            remaining >>= 1;
        }

        return result;
    }

    private static ulong ModInverse(
        uint value,
        uint modulus)
    {
        return ModPow(
            value,
            modulus - 2u,
            modulus);
    }

    private static uint[] Trim(
        uint[] limbs)
    {
        int length =
            limbs.Length;

        while (length > 1 &&
               limbs[length - 1] == 0)
        {
            length--;
        }

        if (length !=
            limbs.Length)
        {
            Array.Resize(
                ref limbs,
                length);
        }

        return limbs;
    }

    private static readonly Vector256<int> ReverseUInt32LaneIndices =
        Vector256.Create(
            7, 6, 5, 4,
            3, 2, 1, 0);

    private static readonly Vector256<ushort> DivideBy10MagicU16 =
        Vector256.Create(
            (ushort)0xCCCD);

    private static readonly Vector256<ushort> TenU16 =
        Vector256.Create(
            (ushort)10);

    private static readonly Vector256<ushort> AsciiZeroU16 =
        Vector256.Create(
            (ushort)'0');

#if ANDROID
    // Android ARM64 NEON uses the same exact reciprocal divide-by-10 as the
    // AVX2 formatter.  Limbs are normalized to 0..9,999, so ushort lanes are
    // sufficient and every conversion remains exact integer arithmetic.
    private static readonly Vector128<ushort> NeonDivideBy10MagicU16 =
        Vector128.Create(
            (ushort)0xCCCD);

    private static readonly Vector128<ushort> NeonTenU16 =
        Vector128.Create(
            (ushort)10);

    private static readonly Vector128<ushort> NeonAsciiZeroU16 =
        Vector128.Create(
            (ushort)'0');
#endif

    /// <summary>
    /// Formats fixed-width base-10,000 limbs in most-significant-first order.
    /// Windows/x86 AVX2 processes 16 limbs -> 64 UTF-16 characters per
    /// iteration. Android ARM64 NEON processes 8 limbs -> 32 UTF-16 characters
    /// per iteration. Both paths use exact reciprocal /10 integer arithmetic;
    /// scalar remains the final fallback when SIMD is disabled or unavailable.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void WriteFixedLimbsDescending(
        uint[] source,
        int highestSourceIndex,
        int limbCount,
        Span<char> destination,
        bool useSimd)
    {
        if (limbCount <= 0)
        {
            return;
        }

        if (destination.Length <
            checked(
                limbCount *
                DigitsPerLimb))
        {
            throw new ArgumentException(
                "Destination is too small for decimal limb formatting.",
                nameof(destination));
        }

        int destinationOffset = 0;
        int remaining =
            limbCount;
        int sourceHigh =
            highestSourceIndex;

        if (useSimd &&
            Avx2.IsSupported &&
            remaining >= 16)
        {
            int vectorizedLimbCount =
                remaining &
                ~15;

            WriteFixedLimbsDescendingAvx2(
                source,
                sourceHigh,
                vectorizedLimbCount,
                destination.Slice(
                    destinationOffset,
                    vectorizedLimbCount *
                    DigitsPerLimb));

            sourceHigh -=
                vectorizedLimbCount;

            remaining -=
                vectorizedLimbCount;

            destinationOffset +=
                vectorizedLimbCount *
                DigitsPerLimb;
        }
#if ANDROID
        else if (useSimd &&
                 AdvSimd.Arm64.IsSupported &&
                 remaining >= 8)
        {
            int vectorizedLimbCount =
                remaining &
                ~7;

            WriteFixedLimbsDescendingNeon(
                source,
                sourceHigh,
                vectorizedLimbCount,
                destination.Slice(
                    destinationOffset,
                    vectorizedLimbCount *
                    DigitsPerLimb));

            sourceHigh -=
                vectorizedLimbCount;

            remaining -=
                vectorizedLimbCount;

            destinationOffset +=
                vectorizedLimbCount *
                DigitsPerLimb;
        }
#endif

        while (remaining > 0)
        {
            WriteFixedLimb(
                source[sourceHigh--],
                destination,
                destinationOffset);

            destinationOffset +=
                DigitsPerLimb;
            remaining--;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void WriteFixedLimbsDescendingAvx2(
        uint[] source,
        int highestSourceIndex,
        int limbCount,
        Span<char> destination)
    {
        ref uint sourceReference =
            ref MemoryMarshal.GetArrayDataReference(
                source);

        Span<ushort> outputCharacters =
            MemoryMarshal.Cast<char, ushort>(
                destination);

        int sourceHigh =
            highestSourceIndex;

        int outputOffset = 0;

        for (int processed = 0;
             processed < limbCount;
             processed += 16)
        {
            int sourceLow =
                sourceHigh - 15;

            Vector256<uint> lower =
                Vector256.LoadUnsafe(
                    ref sourceReference,
                    (nuint)sourceLow);

            Vector256<uint> upper =
                Vector256.LoadUnsafe(
                    ref sourceReference,
                    (nuint)(sourceLow + 8));

            // Limbs are little-endian. Reverse both contiguous 8-limb loads
            // and place the upper block first so lanes become high -> low.
            Vector256<uint> upperDescending =
                Avx2.PermuteVar8x32(
                        upper.AsInt32(),
                        ReverseUInt32LaneIndices)
                    .AsUInt32();

            Vector256<uint> lowerDescending =
                Avx2.PermuteVar8x32(
                        lower.AsInt32(),
                        ReverseUInt32LaneIndices)
                    .AsUInt32();

            // All normalized limbs are <= 9,999, so saturating 32 -> 16
            // packing is exact. PACKUSDW is 128-bit-lane local; permute 64-bit
            // chunks to restore [upperDescending, lowerDescending].
            Vector256<ushort> values =
                Avx2.PackUnsignedSaturate(
                    upperDescending.AsInt32(),
                    lowerDescending.AsInt32());

            values =
                Avx2.Permute4x64(
                        values.AsInt64(),
                        0xD8)
                    .AsUInt16();

            Vector256<ushort> q10 =
                DivideBy10U16(
                    values);

            Vector256<ushort> q100 =
                DivideBy10U16(
                    q10);

            Vector256<ushort> q1000 =
                DivideBy10U16(
                    q100);

            Vector256<ushort> ones =
                Avx2.Subtract(
                    values,
                    MultiplyBy10U16(
                        q10));

            Vector256<ushort> tens =
                Avx2.Subtract(
                    q10,
                    MultiplyBy10U16(
                        q100));

            Vector256<ushort> hundreds =
                Avx2.Subtract(
                    q100,
                    MultiplyBy10U16(
                        q1000));

            Vector256<ushort> thousands =
                q1000;

            thousands =
                Avx2.Add(
                    thousands,
                    AsciiZeroU16);

            hundreds =
                Avx2.Add(
                    hundreds,
                    AsciiZeroU16);

            tens =
                Avx2.Add(
                    tens,
                    AsciiZeroU16);

            ones =
                Avx2.Add(
                    ones,
                    AsciiZeroU16);

            // First interleave 16-bit digit pairs. Reinterpret those pairs as
            // 32-bit values for the second unpack so each pair stays intact.
            Vector256<ushort> firstPairLow =
                Avx2.UnpackLow(
                    thousands,
                    hundreds);

            Vector256<ushort> secondPairLow =
                Avx2.UnpackLow(
                    tens,
                    ones);

            Vector256<ushort> firstPairHigh =
                Avx2.UnpackHigh(
                    thousands,
                    hundreds);

            Vector256<ushort> secondPairHigh =
                Avx2.UnpackHigh(
                    tens,
                    ones);

            Vector256<ushort> limbs01And89 =
                Avx2.UnpackLow(
                        firstPairLow.AsUInt32(),
                        secondPairLow.AsUInt32())
                    .AsUInt16();

            Vector256<ushort> limbs23And1011 =
                Avx2.UnpackHigh(
                        firstPairLow.AsUInt32(),
                        secondPairLow.AsUInt32())
                    .AsUInt16();

            Vector256<ushort> limbs45And1213 =
                Avx2.UnpackLow(
                        firstPairHigh.AsUInt32(),
                        secondPairHigh.AsUInt32())
                    .AsUInt16();

            Vector256<ushort> limbs67And1415 =
                Avx2.UnpackHigh(
                        firstPairHigh.AsUInt32(),
                        secondPairHigh.AsUInt32())
                    .AsUInt16();

            Vector256<ushort> output0 =
                Avx2.Permute2x128(
                        limbs01And89.AsInt64(),
                        limbs23And1011.AsInt64(),
                        0x20)
                    .AsUInt16();

            Vector256<ushort> output1 =
                Avx2.Permute2x128(
                        limbs45And1213.AsInt64(),
                        limbs67And1415.AsInt64(),
                        0x20)
                    .AsUInt16();

            Vector256<ushort> output2 =
                Avx2.Permute2x128(
                        limbs01And89.AsInt64(),
                        limbs23And1011.AsInt64(),
                        0x31)
                    .AsUInt16();

            Vector256<ushort> output3 =
                Avx2.Permute2x128(
                        limbs45And1213.AsInt64(),
                        limbs67And1415.AsInt64(),
                        0x31)
                    .AsUInt16();

            output0.CopyTo(
                outputCharacters.Slice(
                    outputOffset,
                    16));

            output1.CopyTo(
                outputCharacters.Slice(
                    outputOffset + 16,
                    16));

            output2.CopyTo(
                outputCharacters.Slice(
                    outputOffset + 32,
                    16));

            output3.CopyTo(
                outputCharacters.Slice(
                    outputOffset + 48,
                    16));

            sourceHigh -= 16;
            outputOffset += 64;
        }
    }

#if ANDROID
    /// <summary>
    /// ARM64 NEON formatter for Android TXT export. Eight base-10,000 limbs are
    /// expanded into 32 UTF-16 digits per iteration. Source limbs are assembled
    /// directly in descending order, then all /10, digit extraction and digit
    /// interleaving is performed in 128-bit AdvSIMD registers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void WriteFixedLimbsDescendingNeon(
        uint[] source,
        int highestSourceIndex,
        int limbCount,
        Span<char> destination)
    {
        Span<ushort> outputCharacters =
            MemoryMarshal.Cast<char, ushort>(
                destination);

        int sourceHigh =
            highestSourceIndex;

        int outputOffset = 0;

        for (int processed = 0;
             processed < limbCount;
             processed += 8)
        {
            // ParallelBigUnsigned limbs are little-endian. Build the NEON
            // lanes high -> low so the final character vectors can be stored
            // directly without a scalar reversal pass.
            Vector128<ushort> values =
                Vector128.Create(
                    (ushort)source[sourceHigh],
                    (ushort)source[sourceHigh - 1],
                    (ushort)source[sourceHigh - 2],
                    (ushort)source[sourceHigh - 3],
                    (ushort)source[sourceHigh - 4],
                    (ushort)source[sourceHigh - 5],
                    (ushort)source[sourceHigh - 6],
                    (ushort)source[sourceHigh - 7]);

            Vector128<ushort> q10 =
                DivideBy10U16Neon(
                    values);

            Vector128<ushort> q100 =
                DivideBy10U16Neon(
                    q10);

            Vector128<ushort> q1000 =
                DivideBy10U16Neon(
                    q100);

            Vector128<ushort> ones =
                AdvSimd.Subtract(
                    values,
                    AdvSimd.Multiply(
                        q10,
                        NeonTenU16));

            Vector128<ushort> tens =
                AdvSimd.Subtract(
                    q10,
                    AdvSimd.Multiply(
                        q100,
                        NeonTenU16));

            Vector128<ushort> hundreds =
                AdvSimd.Subtract(
                    q100,
                    AdvSimd.Multiply(
                        q1000,
                        NeonTenU16));

            Vector128<ushort> thousands =
                q1000;

            thousands =
                AdvSimd.Add(
                    thousands,
                    NeonAsciiZeroU16);

            hundreds =
                AdvSimd.Add(
                    hundreds,
                    NeonAsciiZeroU16);

            tens =
                AdvSimd.Add(
                    tens,
                    NeonAsciiZeroU16);

            ones =
                AdvSimd.Add(
                    ones,
                    NeonAsciiZeroU16);

            // ZIP 16-bit lanes into [thousands,hundreds] and [tens,ones].
            // Reinterpret each digit-pair as a 32-bit lane, then ZIP again so
            // each output vector contains two complete 4-digit limbs.
            Vector128<ushort> firstPairLow =
                AdvSimd.Arm64.ZipLow(
                    thousands,
                    hundreds);

            Vector128<ushort> secondPairLow =
                AdvSimd.Arm64.ZipLow(
                    tens,
                    ones);

            Vector128<ushort> firstPairHigh =
                AdvSimd.Arm64.ZipHigh(
                    thousands,
                    hundreds);

            Vector128<ushort> secondPairHigh =
                AdvSimd.Arm64.ZipHigh(
                    tens,
                    ones);

            Vector128<ushort> output0 =
                AdvSimd.Arm64.ZipLow(
                        firstPairLow.AsUInt32(),
                        secondPairLow.AsUInt32())
                    .AsUInt16();

            Vector128<ushort> output1 =
                AdvSimd.Arm64.ZipHigh(
                        firstPairLow.AsUInt32(),
                        secondPairLow.AsUInt32())
                    .AsUInt16();

            Vector128<ushort> output2 =
                AdvSimd.Arm64.ZipLow(
                        firstPairHigh.AsUInt32(),
                        secondPairHigh.AsUInt32())
                    .AsUInt16();

            Vector128<ushort> output3 =
                AdvSimd.Arm64.ZipHigh(
                        firstPairHigh.AsUInt32(),
                        secondPairHigh.AsUInt32())
                    .AsUInt16();

            output0.CopyTo(
                outputCharacters.Slice(
                    outputOffset,
                    8));

            output1.CopyTo(
                outputCharacters.Slice(
                    outputOffset + 8,
                    8));

            output2.CopyTo(
                outputCharacters.Slice(
                    outputOffset + 16,
                    8));

            output3.CopyTo(
                outputCharacters.Slice(
                    outputOffset + 24,
                    8));

            sourceHigh -= 8;
            outputOffset += 32;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ushort> DivideBy10U16Neon(
        Vector128<ushort> value)
    {
        // Exact for every normalized base-10,000 limb:
        // floor(x / 10) = (x * 0xCCCD) >> 19.
        // NEON widens four ushort lanes at a time to uint so the product does
        // not overflow, shifts the 32-bit products, then narrows back to ushort.
        Vector128<uint> lowerProduct =
            AdvSimd.MultiplyWideningLower(
                Vector128.GetLower(
                    value),
                Vector128.GetLower(
                    NeonDivideBy10MagicU16));

        Vector128<uint> upperProduct =
            AdvSimd.MultiplyWideningLower(
                Vector128.GetUpper(
                    value),
                Vector128.GetUpper(
                    NeonDivideBy10MagicU16));

        Vector128<uint> lowerQuotient =
            AdvSimd.ShiftRightLogical(
                lowerProduct,
                19);

        Vector128<uint> upperQuotient =
            AdvSimd.ShiftRightLogical(
                upperProduct,
                19);

        Vector64<ushort> lowerNarrow =
            AdvSimd.ExtractNarrowingLower(
                lowerQuotient);

        Vector64<ushort> upperNarrow =
            AdvSimd.ExtractNarrowingLower(
                upperQuotient);

        return Vector128.Create(
            lowerNarrow,
            upperNarrow);
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ushort> DivideBy10U16(
        Vector256<ushort> value)
    {
        // Exact for the full ushort range:
        // floor(x / 10) = high16(x * 0xCCCD) >> 3.
        return Avx2.ShiftRightLogical(
            Avx2.MultiplyHigh(
                value,
                DivideBy10MagicU16),
            3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<ushort> MultiplyBy10U16(
        Vector256<ushort> value)
    {
        return Avx2.MultiplyLow(
                value.AsInt16(),
                TenU16.AsInt16())
            .AsUInt16();
    }

    private static void WriteFixedLimb(
        uint limb,
        Span<char> destination,
        int offset)
    {
        destination[offset] =
            (char)('0' +
                   limb / 1_000);

        destination[offset + 1] =
            (char)('0' +
                   limb / 100 % 10);

        destination[offset + 2] =
            (char)('0' +
                   limb / 10 % 10);

        destination[offset + 3] =
            (char)('0' +
                   limb % 10);
    }

    private sealed record BranchPowerResult(
        ParallelBigUnsigned Magnitude,
        PowerDiagnosticsCollector Diagnostics,
        long ElapsedTicks);
}

internal sealed record ParallelPowerResult(
    ParallelBigUnsigned Magnitude,
    ParallelPowerDiagnostics Diagnostics);

internal sealed record ParallelPowerDiagnostics(
    int WorkerCount,
    int NttMultiplicationCount,
    TimeSpan BitReversal,
    TimeSpan ForwardTransform,
    TimeSpan Pointwise,
    TimeSpan InverseTransform,
    TimeSpan Crt,
    TimeSpan Carry,
    bool UsedExponentSplit,
    int FirstExponent,
    int SecondExponent,
    int FirstBranchWorkerCount,
    int SecondBranchWorkerCount,
    TimeSpan FirstBranchElapsed,
    TimeSpan SecondBranchElapsed,
    TimeSpan FinalCombineElapsed,
    long NttWorkspacePeakBytes,
    long NttPoolPeakRetainedBytes,
    int NttBufferRentCount,
    int NttBufferReuseCount,
    bool UsedMemoryBoundedLargePower,
    int LargePowerChunkExponent,
    int SegmentedNttMultiplicationCount,
    int SegmentedNttPairCount);
