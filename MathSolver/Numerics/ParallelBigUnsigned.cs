using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

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

    // Cache only reusable twiddle stages whose largest half-stage is at most
    // 1,048,576 values.  A per-convolution plan stores all cached forward and
    // inverse stages in two compact pooled buffers (~16 MiB total at the cap).
    // With two exponent branches the peak extra cache is about 32 MiB, still
    // small beside the multi-gigabyte NTT working set.
    private const int MaximumCachedTwiddleCount = 1 << 20;

    // Cache-block size is selected from the logical-processor count.  This is
    // deliberately a small heuristic rather than CPU-model detection: modern
    // high-thread CPUs such as the 24-thread HX 370 keep the proven 4096-value
    // (16 KiB) block, while 8-19 thread SMT CPUs such as a 12-thread i7-8700
    // use 2048 values (8 KiB) to leave more shared L1D room per sibling thread.
    // Very small worker budgets use a larger block to reduce block-dispatch
    // overhead.  Every choice is a power of two so DIF/DIT stage boundaries
    // remain exact.
    private const int SmallSmtFusedNttBlockLength = 1 << 11; // 2048 = 8 KiB
    private const int DefaultFusedNttBlockLength = 1 << 12;  // 4096 = 16 KiB
    private const int LowThreadFusedNttBlockLength = 1 << 13; // 8192 = 32 KiB

    // Both primes support transforms through 2^26. Their product is large
    // enough to recover every base-10,000 convolution coefficient required by
    // an 18-digit base raised to the maximum exponent of 10,000,000.
    private const uint FirstModulus = 2_013_265_921;
    private const uint SecondModulus = 469_762_049;
    private const uint FirstPrimitiveRoot = 31;
    private const uint SecondPrimitiveRoot = 3;

    private static readonly ulong FirstModulusInverseInSecond =
        ModInverse(
            FirstModulus % SecondModulus,
            SecondModulus);

    private readonly uint[] _limbs;

    private ParallelBigUnsigned(
        uint[] limbs,
        bool takeOwnership)
    {
        _limbs =
            takeOwnership
                ? Trim(limbs)
                : Trim((uint[])limbs.Clone());
    }

    public int DigitCount
    {
        get
        {
            uint highest =
                _limbs[^1];

            int highestDigits =
                highest >= 1_000
                    ? 4
                    : highest >= 100
                        ? 3
                        : highest >= 10
                            ? 2
                            : 1;

            return checked(
                (_limbs.Length - 1) *
                DigitsPerLimb +
                highestDigits);
        }
    }

    public long StorageBytes =>
        (long)_limbs.Length *
        sizeof(uint);

    public bool IsOne =>
        _limbs.Length == 1 &&
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
                progress,
                cancellationToken);
        }

        var diagnostics =
            new PowerDiagnosticsCollector();

        using var workers =
            new FixedWorkerTeam(
                workerCount);

        ParallelBigUnsigned magnitude =
            PowWithTeam(
                baseValue,
                exponent,
                workers,
                diagnostics,
                progress,
                cancellationToken);

        return new ParallelPowerResult(
            magnitude,
            diagnostics.CreateSnapshot(
                workers.WorkerCount));
    }

    private static ParallelPowerResult PowSplit(
        ulong baseValue,
        int originalExponent,
        int firstExponent,
        int secondExponent,
        int workerCount,
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
                        branchWorkerCount);

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
                   workerCount))
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

    public string ToDecimalString()
    {
        int digitCount =
            DigitCount;

        var characters =
            new char[digitCount];

        int position = 0;

        string highestText =
            _limbs[^1].ToString(
                CultureInfo.InvariantCulture);

        highestText.CopyTo(
            0,
            characters,
            position,
            highestText.Length);

        position +=
            highestText.Length;

        for (int limbIndex =
                 _limbs.Length - 2;
             limbIndex >= 0;
             limbIndex--)
        {
            WriteFixedLimb(
                _limbs[limbIndex],
                characters,
                position);

            position +=
                DigitsPerLimb;
        }

        return new string(
            characters);
    }

    public void WriteDecimalBlocks(
        TextWriter writer,
        int blockDigitCount,
        Action reportBlockWritten,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            blockDigitCount);

        var buffer =
            new char[blockDigitCount];

        int bufferedCharacters = 0;

        void FlushBuffer()
        {
            if (bufferedCharacters == 0)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            writer.Write(
                buffer,
                0,
                bufferedCharacters);

            bufferedCharacters = 0;
            reportBlockWritten();
        }

        void AppendCharacter(
            char character)
        {
            buffer[bufferedCharacters++] =
                character;

            if (bufferedCharacters ==
                buffer.Length)
            {
                FlushBuffer();
            }
        }

        string highestText =
            _limbs[^1].ToString(
                CultureInfo.InvariantCulture);

        foreach (char character in
                 highestText)
        {
            AppendCharacter(
                character);
        }

        for (int limbIndex =
                 _limbs.Length - 2;
             limbIndex >= 0;
             limbIndex--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            uint limb =
                _limbs[limbIndex];

            AppendCharacter(
                (char)('0' +
                       limb / 1_000));

            AppendCharacter(
                (char)('0' +
                       limb / 100 % 10));

            AppendCharacter(
                (char)('0' +
                       limb / 10 % 10));

            AppendCharacter(
                (char)('0' +
                       limb % 10));
        }

        FlushBuffer();
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
            (long)left._limbs.Length *
            right._limbs.Length;

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

    private static ParallelBigUnsigned MultiplySchoolbook(
        ParallelBigUnsigned left,
        ParallelBigUnsigned right,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        int coefficientCount =
            checked(
                left._limbs.Length +
                right._limbs.Length -
                1);

        var coefficients =
            new ulong[coefficientCount];

        for (int leftIndex = 0;
             leftIndex < left._limbs.Length;
             leftIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ulong leftValue =
                left._limbs[leftIndex];

            for (int rightIndex = 0;
                 rightIndex < right._limbs.Length;
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
                left._limbs.Length +
                right._limbs.Length -
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
                right._limbs,
                coefficientCount,
                transformLength,
                FirstModulus,
                FirstPrimitiveRoot,
                isSquare,
                workers,
                diagnostics,
                cancellationToken);

        uint[] secondResidues =
            ConvolveModulus(
                left._limbs,
                right._limbs,
                coefficientCount,
                transformLength,
                SecondModulus,
                SecondPrimitiveRoot,
                isSquare,
                workers,
                diagnostics,
                cancellationToken);

        var coefficients =
            new ulong[coefficientCount];

        long crtStarted =
            Stopwatch.GetTimestamp();

        ExecuteRanges(
            coefficientCount,
            workers,
            cancellationToken,
            (start, end) =>
            {
                int count =
                    end - start;

                // Span is created once per worker range, not inside the hot
                // coefficient loop.  This keeps the code memory-safe while
                // still giving the JIT a simple contiguous range whose bounds
                // checks can normally be hoisted/eliminated.
                ReadOnlySpan<uint> firstSpan =
                    firstResidues.AsSpan(
                        start,
                        count);

                ReadOnlySpan<uint> secondSpan =
                    secondResidues.AsSpan(
                        start,
                        count);

                Span<ulong> coefficientSpan =
                    coefficients.AsSpan(
                        start,
                        count);

                for (int offset = 0;
                     offset < count;
                     offset++)
                {
                    uint first =
                        firstSpan[offset];

                    // first < FirstModulus and FirstModulus is only about
                    // 4.29 * SecondModulus.  Four predictable conditional
                    // subtractions are cheaper than an integer remainder here
                    // and require no unsafe pointer or custom reciprocal helper.
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

                    // Keep the original scalar modulo expression.  The
                    // previous generic Math.BigMul reciprocal reducer was much
                    // slower in this workload despite looking cheaper on paper.
                    ulong multiplier =
                        (ulong)difference *
                        FirstModulusInverseInSecond %
                        SecondModulus;

                    coefficientSpan[offset] =
                        first +
                        (ulong)FirstModulus *
                        multiplier;
                }
            });

        diagnostics.CrtTicks +=
            Stopwatch.GetTimestamp() -
            crtStarted;

        return CreateFromCoefficients(
            coefficients,
            diagnostics,
            cancellationToken);
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

    private static uint[] ConvolveModulus(
        uint[] left,
        uint[] right,
        int coefficientCount,
        int transformLength,
        uint modulus,
        uint primitiveRoot,
        bool isSquare,
        FixedWorkerTeam workers,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        int fusedNttBlockLength =
            SelectFusedNttBlockLength();

        var transformedLeft =
            new uint[transformLength];

        Array.Copy(
            left,
            transformedLeft,
            left.Length);

        // One compact plan is kept for the lifetime of this modulus
        // convolution.  The first forward transform builds both the forward
        // and inverse twiddle tables.  A second forward transform and the
        // inverse transform reuse them without rebuilding the same powers.
        using var twiddlePlan =
            new NttTwiddlePlan(
                transformLength);

        ForwardDifTransform(
            transformedLeft,
            modulus,
            primitiveRoot,
            workers,
            twiddlePlan,
            fusedNttBlockLength,
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
            var transformedRight =
                new uint[transformLength];

            Array.Copy(
                right,
                transformedRight,
                right.Length);

            ForwardDifTransform(
                transformedRight,
                modulus,
                primitiveRoot,
                workers,
                twiddlePlan,
                fusedNttBlockLength,
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
                                   transformedRight[index] %
                                   modulus);
                    }
                });

            diagnostics.PointwiseTicks +=
                Stopwatch.GetTimestamp() -
                pointwiseStarted;
        }

        InverseDitTransform(
            transformedLeft,
            modulus,
            primitiveRoot,
            workers,
            twiddlePlan,
            fusedNttBlockLength,
            diagnostics,
            cancellationToken);

        var residues =
            new uint[coefficientCount];

        Array.Copy(
            transformedLeft,
            residues,
            coefficientCount);

        return residues;
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

            // From this point down, every remaining DIF stage operates only
            // inside an independent adaptive fused block.  Fuse the tail
            // so each worker keeps one block hot in L1/L2 rather than sweeping
            // the entire transform once per stage.
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

            uint root =
                (uint)ModPow(
                    primitiveRoot,
                    (modulus - 1u) /
                    (uint)stageLength,
                    modulus);

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

            if (useTwiddleCache &&
                buildCachedTwiddles)
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
            }

            int segmentsPerGroup =
                GetSegmentsPerGroup(
                    halfLength,
                    groupCount,
                    workers.WorkerCount);

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
    private static void InverseDitTransform(
        uint[] values,
        uint modulus,
        uint primitiveRoot,
        FixedWorkerTeam workers,
        NttTwiddlePlan twiddlePlan,
        int fusedNttBlockLength,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        int length =
            values.Length;

        long transformStarted =
            Stopwatch.GetTimestamp();

        uint inverseLength =
            (uint)ModPow(
                (uint)length,
                modulus - 2u,
                modulus);

        uint inversePrimitiveRoot = 0;
        bool inversePrimitiveRootReady =
            false;

        int firstStageLength = 2;

        // DIT starts with the smallest stages.  Those stages are independent
        // inside adaptive fused blocks, so finish them locally before the
        // first global stage.  The twiddles were prepared by the first forward
        // transform and are reused here without rebuilding.
        if (length > fusedNttBlockLength)
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
            transformStarted;
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
        }
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

                            int butterfly = 1;

                            for (;
                                 leftIndex < butterflyEnd;
                                 leftIndex++,
                                 rightIndex++,
                                 butterfly++)
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
                                           twiddles[twiddleOffset + butterfly] %
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

                            int butterfly = 1;

                            for (;
                                 leftIndex < butterflyEnd;
                                 leftIndex++,
                                 rightIndex++,
                                 butterfly++)
                            {
                                leftValue =
                                    values[leftIndex];

                                rightValue =
                                    (uint)((ulong)values[rightIndex] *
                                           twiddles[twiddleOffset + butterfly] %
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

        public NttTwiddlePlan(
            int transformLength)
        {
            MaximumHalfLength =
                Math.Min(
                    transformLength >> 2,
                    MaximumCachedTwiddleCount);

            if (MaximumHalfLength < 2)
            {
                return;
            }

            int capacity =
                checked(
                    MaximumHalfLength << 1);

            _forwardTwiddles =
                ArrayPool<uint>.Shared.Rent(
                    capacity);

            _inverseTwiddles =
                ArrayPool<uint>.Shared.Rent(
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
                ArrayPool<uint>.Shared.Return(
                    forward,
                    clearArray: false);
            }

            if (inverse is not null)
            {
                ArrayPool<uint>.Shared.Return(
                    inverse,
                    clearArray: false);
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

            // Keep division/remainder by the compile-time constant 10,000.
            // RyuJIT can strength-reduce constant division and can also share
            // quotient/remainder work.  A hand-written Math.BigMul reciprocal
            // was measurably slower on the HX 370 benchmark.
            destination[limbCount++] =
                (uint)(value %
                       LimbBase);

            carry =
                value /
                LimbBase;
        }

        while (carry > 0)
        {
            destination[limbCount++] =
                (uint)(carry %
                       LimbBase);

            carry /=
                LimbBase;
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
                ToTimeSpan(_finalCombineTicks));
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

        public FixedWorkerTeam(
            int workerCount)
        {
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

            _failure?.Throw();
        }

        public void Dispose()
        {
            if (_threads.Length == 0)
            {
                _completed.Dispose();
                return;
            }

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

    private static void WriteFixedLimb(
        uint limb,
        char[] destination,
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
    TimeSpan FinalCombineElapsed);
