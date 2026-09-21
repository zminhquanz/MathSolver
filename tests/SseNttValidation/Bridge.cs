using System.Runtime.Intrinsics;
using MathSolver.Services;
namespace MathSolver.Numerics;
internal sealed partial class ParallelBigUnsigned
{
    internal static int ValidateSseKernels()
    {
        int checks = 0;
        var random = new Random(42);
        foreach (uint p in new[] { FirstModulus, SecondModulus })
        {
            uint[] edges = [0, 1, p / 2, p - 2, p - 1];
            for (int i = 0; i < 10000; i++)
            {
                var a = Vector128.Create((uint)random.NextInt64(p), edges[i % 5], p - 1, 0);
                var b = Vector128.Create((uint)random.NextInt64(p), edges[i / 5 % 5], p - 1, 0);
                uint[] shoup = [
                    (uint)(((ulong)b[0] << 32) / p), (uint)(((ulong)b[1] << 32) / p),
                    (uint)(((ulong)b[2] << 32) / p), 0];
                uint[] rootsForProduct = [b[0], b[1], b[2], b[3]];
                uint[] product = [0, 0, 0, 0, a[0], a[1], a[2], a[3]];
                ExecuteCachedGroupSse(product, p, rootsForProduct, shoup, 0, 4, 0, true);
                uint[] sumDifference = [a[0], a[1], a[2], a[3], b[0], b[1], b[2], b[3]];
                uint oneShoup = (uint)((1UL << 32) / p);
                ExecuteCachedGroupSse(sumDifference, p, [1, 1, 1, 1],
                    [oneShoup, oneShoup, oneShoup, oneShoup], 0, 4, 0, false);
                for (int lane = 0; lane < 4; lane++)
                {
                    if (product[lane] != (ulong)a[lane] * b[lane] % p ||
                        sumDifference[lane] != ((ulong)a[lane] + b[lane]) % p ||
                        sumDifference[lane + 4] != ((ulong)a[lane] + p - b[lane]) % p)
                        throw new Exception($"Modular kernel p={p} lane={lane}");
                    checks++;
                }
            }
            using var pool = new NttBufferPool();
            using var roots = new NttTwiddleBufferPool();
            using var plans = new SharedNttTwiddlePlans(roots, false, useSseNtt: true);
            using var team = new FixedWorkerTeam(2, pool, plans);
            var plan = plans.Get(p);
            uint root = p == FirstModulus ? FirstPrimitiveRoot : SecondPrimitiveRoot;
            foreach (int length in new[] { 2, 4, 8, 16, 32, 256, 2048, 16384, 131072 })
            {
                PrepareFusedTwiddleTables(plan, root, p, length, team, default);
                var input = Enumerable.Range(0, length * 2).Select(_ => (uint)random.NextInt64(p)).ToArray();
                var expected = (uint[])input.Clone();
                for (int group = 0; group < input.Length; group += length)
                for (int stage = length; stage >= 2; stage >>= 1)
                for (int g = group; g < group + length; g += stage)
                for (int j = 0; j < stage / 2; j++)
                {
                    uint a = expected[g + j], b = expected[g + stage / 2 + j];
                    uint w = stage == 2 ? 1u : plan.ForwardTwiddles[plan.GetOffset(stage / 2) + j];
                    expected[g + j] = (uint)(((ulong)a + b) % p);
                    expected[g + stage / 2 + j] = (uint)(((ulong)a + p - b) * w % p);
                }
                var actual = (uint[])input.Clone();
                ExecuteCachedTilesSse(actual, p, team, plan, length, Math.Min(length, 16384), Math.Min(length, 2048), false, default);
                if (!actual.SequenceEqual(expected)) throw new Exception($"Forward {p} {length}");
                ExecuteCachedTilesSse(actual, p, team, plan, length, Math.Min(length, 16384), Math.Min(length, 2048), true, default);
                for (int i = 0; i < actual.Length; i++)
                    if (actual[i] != (ulong)input[i] * (uint)length % p) throw new Exception($"Inverse {p} {length}");
                checks += 2;
            }
        }
        return checks;
    }
}
