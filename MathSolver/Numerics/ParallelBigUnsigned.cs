using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;

namespace MathSolver.Numerics;

/// <summary>
/// Unsigned arbitrary-precision integer used by the parallel power engine.
/// Digits are stored in base 10,000 so TXT export never needs a giant
/// binary-to-decimal division tree. Large products use two exact NTTs and CRT;
/// the butterfly work inside every transform is shared by the configured
/// physical-core worker budget.
/// </summary>
internal sealed class ParallelBigUnsigned
{
    private const uint LimbBase = 10_000;
    private const int DigitsPerLimb = 4;
    private const int SchoolbookWorkLimit = 250_000;
    private const int MaximumTransformLength = 1 << 23;

    // Both primes have primitive root 3. The first supports transforms up to
    // 2^23 and the second up to 2^26. Their product is large enough to recover
    // every base-10,000 convolution coefficient used by this module exactly.
    private const uint FirstModulus = 998_244_353;
    private const uint SecondModulus = 469_762_049;
    private const uint PrimitiveRoot = 3;

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

        using var workers =
            new FixedWorkerTeam(
                workerCount);

        var diagnostics =
            new PowerDiagnosticsCollector();

        if (exponent == 0)
        {
            return new ParallelPowerResult(
                One,
                diagnostics.CreateSnapshot(
                    workerCount));
        }

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

        return new ParallelPowerResult(
            resultInitialized
                ? result
                : One,
            diagnostics.CreateSnapshot(
                workers.WorkerCount));
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
                "The exact parallel transform exceeds the supported 2^23 length.");
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
                for (int index = start;
                     index < end;
                     index++)
                {
                    ulong first =
                        firstResidues[index];

                    long difference =
                        (long)secondResidues[index] -
                        (long)(first %
                               SecondModulus);

                    if (difference < 0)
                    {
                        difference +=
                            SecondModulus;
                    }

                    ulong multiplier =
                        (ulong)difference *
                        FirstModulusInverseInSecond %
                        SecondModulus;

                    coefficients[index] =
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

    private static uint[] ConvolveModulus(
        uint[] left,
        uint[] right,
        int coefficientCount,
        int transformLength,
        uint modulus,
        bool isSquare,
        FixedWorkerTeam workers,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        var transformedLeft =
            new uint[transformLength];

        Array.Copy(
            left,
            transformedLeft,
            left.Length);

        ForwardDifTransform(
            transformedLeft,
            modulus,
            workers,
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
                workers,
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
            workers,
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
    private static void ForwardDifTransform(
        uint[] values,
        uint modulus,
        FixedWorkerTeam workers,
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

            uint root =
                (uint)ModPow(
                    PrimitiveRoot,
                    (modulus - 1u) /
                    (uint)stageLength,
                    modulus);

            int groupCount =
                length /
                stageLength;

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

                        ulong twiddle =
                            butterflyStart == 0
                                ? 1UL
                                : ModPow(
                                    root,
                                    (uint)butterflyStart,
                                    modulus);

                        int groupOffset =
                            groupIndex *
                            stageLength;

                        for (int butterfly = butterflyStart;
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

                            twiddle =
                                twiddle *
                                root %
                                modulus;

                            if (((butterfly - butterflyStart) &
                                 0x3FFF) == 0)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
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
    private static void InverseDitTransform(
        uint[] values,
        uint modulus,
        FixedWorkerTeam workers,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        int length =
            values.Length;

        long transformStarted =
            Stopwatch.GetTimestamp();

        for (int stageLength = 2;
             stageLength <= length;
             stageLength <<= 1)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int halfLength =
                stageLength >> 1;

            uint root =
                (uint)ModPow(
                    PrimitiveRoot,
                    (modulus - 1u) /
                    (uint)stageLength,
                    modulus);

            root =
                (uint)ModPow(
                    root,
                    modulus - 2u,
                    modulus);

            int groupCount =
                length /
                stageLength;

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

                        ulong twiddle =
                            butterflyStart == 0
                                ? 1UL
                                : ModPow(
                                    root,
                                    (uint)butterflyStart,
                                    modulus);

                        int groupOffset =
                            groupIndex *
                            stageLength;

                        for (int butterfly = butterflyStart;
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

                            values[leftIndex] =
                                sum;

                            values[rightIndex] =
                                difference;

                            twiddle =
                                twiddle *
                                root %
                                modulus;

                            if (((butterfly - butterflyStart) &
                                 0x3FFF) == 0)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                        }
                    }
                });
        }

        uint inverseLength =
            (uint)ModPow(
                (uint)length,
                modulus - 2u,
                modulus);

        ExecuteRanges(
            length,
            workers,
            cancellationToken,
            (start, end) =>
            {
                for (int index = start;
                     index < end;
                     index++)
                {
                    values[index] =
                        (uint)((ulong)values[index] *
                               inverseLength %
                               modulus);
                }
            });

        diagnostics.InverseTransformTicks +=
            Stopwatch.GetTimestamp() -
            transformStarted;
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

    private static void GetSegmentBounds(
        int segmentIndex,
        int segmentsPerGroup,
        int halfLength,
        out int groupIndex,
        out int butterflyStart,
        out int butterflyEnd)
    {
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

    private static ParallelBigUnsigned CreateFromCoefficients(
        ulong[] coefficients,
        PowerDiagnosticsCollector diagnostics,
        CancellationToken cancellationToken)
    {
        long carryStarted =
            Stopwatch.GetTimestamp();

        var limbs =
            new uint[coefficients.Length + 8];

        ulong carry = 0;
        int limbCount = 0;

        for (int index = 0;
             index < coefficients.Length;
             index++)
        {
            if ((index & 0xFFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            ulong value =
                coefficients[index] +
                carry;

            limbs[limbCount++] =
                (uint)(value %
                       LimbBase);

            carry =
                value /
                LimbBase;
        }

        while (carry > 0)
        {
            limbs[limbCount++] =
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
                ToTimeSpan(CarryTicks));
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
    /// graph at all 23 stages of every 2^23 transform.
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
    TimeSpan Carry);
