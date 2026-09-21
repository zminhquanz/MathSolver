using System.Diagnostics;
using System.Numerics;
using MathSolver.Numerics;

int passed = 0;
foreach (long value in new long[] { 0, 1, -1, 2, -3, 10, 1000, long.MinValue, long.MaxValue, 999999999999999999 })
foreach (int exponent in new[] { 0, 1, 2, 3, 31, 127, 256, 511, 1024, 4097 })
    Verify(value, exponent);
Verify(2, 1_000_000);
Verify(3, 1_000_001);
Verify(999999999999999999, 1_000_000);
using var cancelled = new CancellationTokenSource();
cancelled.Cancel();
Expect<OperationCanceledException>(() => SingleThreadBigIntegerPower.Pow(2, 10, (_, _) => { }, 4, cancelled.Token));
Expect<ArgumentOutOfRangeException>(() => SingleThreadBigIntegerPower.Pow(2, -1, (_, _) => { }, 0, default));
using var mid = new CancellationTokenSource();
Expect<OperationCanceledException>(() => SingleThreadBigIntegerPower.Pow(3, 10000, (_, _) => mid.Cancel(), 20, mid.Token));
Console.WriteLine($"PASS {passed} cases; exact results and legacy scalar progress schedule.");

void Verify(long value, int exponent)
{
    int total = exponent == 0 ? 0 : BitOperations.Log2((uint)exponent) + BitOperations.PopCount((uint)exponent) - 1;
    var currentProgress = new List<(int, int)>();
    var baselineProgress = new List<(int, int)>();
    var watch = Stopwatch.StartNew();
    var actual = SingleThreadBigIntegerPower.Pow(value, exponent, (a, b) => currentProgress.Add((a, b)), total, default);
    double elapsed = watch.Elapsed.TotalSeconds;
    var baseline = BaselinePower.Pow(value, exponent, (a, b) => baselineProgress.Add((a, b)), total, default, false);
    if (actual != baseline || actual != BigInteger.Pow(new BigInteger(value), exponent)) throw new Exception($"Result {value}^{exponent}");
    if (!currentProgress.SequenceEqual(baselineProgress)) throw new Exception($"Progress {value}^{exponent}");
    if (total > 0 && currentProgress[^1] != (total, total)) throw new Exception("Incomplete progress");
    if (exponent >= 1_000_000) Console.WriteLine($"{value}^{exponent}: runtime controller {elapsed:F3}s");
    passed++;
}
void Expect<T>(Action action) where T : Exception
{
    try { action(); } catch (T) { passed++; return; }
    throw new Exception($"Expected {typeof(T).Name}");
}
