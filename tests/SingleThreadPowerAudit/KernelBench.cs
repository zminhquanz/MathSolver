using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using MathSolver.Numerics;

internal static class KernelBench
{
    private static int sink;
    internal static void Run()
    {
        var random = new Random(8172);
        foreach (int length in new[] { 4,8,15,16,17,31,32,33,64,96,128,194,256,272,384,448,512 })
        {
            ushort[] limbs = new ushort[length];
            for (int i=0;i<length;i++) limbs[i]=(ushort)random.Next(65536);
            limbs[^1] |= 0x8000;
            BigInteger native = Avx2BigIntegerPower.ToBigInteger(limbs);
            var workspace = new ulong[Avx2BigIntegerPower.MaximumAccumulatorLimbCount];
            BigInteger expected = native * native;
            // Repeated all-ones and random squares verify carry and cleared workspace.
            foreach (ushort[] input in new[] { Enumerable.Repeat(ushort.MaxValue,length).ToArray(), limbs, limbs })
            {
                BigInteger n = Avx2BigIntegerPower.ToBigInteger(input);
                foreach (var actual in new[] {
                    Avx2BigIntegerPower.SquareMagnitude(input,workspace,default),
                    ScalarWindowKernel.SquareMagnitude(input,workspace,default) })
                {
                    if (Avx2BigIntegerPower.ToBigInteger(actual) != n*n || workspace.Any(x=>x!=0))
                        throw new Exception("Kernel square/carry/workspace mismatch");
                }
            }
            Action[] actions = [
                () => sink ^= Avx2BigIntegerPower.SquareMagnitude(limbs,workspace,default)[0],
                () => sink ^= ScalarWindowKernel.SquareMagnitude(limbs,workspace,default)[0],
                () => sink ^= (int)((native*native) & 65535),
            ];
            string[] names = ["avx2", "scalar-ushort", "biginteger"];
            foreach (var action in actions) for(int i=0;i<100;i++) action();
            int iterations = Math.Max(100, 1000000 / length);
            for(int round=0;round<7;round++)
            for(int j=0;j<actions.Length;j++)
            {
                int index=(j+round)%actions.Length;
                long bytes=GC.GetAllocatedBytesForCurrentThread();
                long start=Stopwatch.GetTimestamp();
                for(int i=0;i<iterations;i++) actions[index]();
                double ns=Stopwatch.GetElapsedTime(start).TotalNanoseconds/iterations;
                long allocated=GC.GetAllocatedBytesForCurrentThread()-bytes;
                Console.WriteLine(JsonSerializer.Serialize(new { Kind="kernel", Operation="square", Mode=names[index],
                    Limbs=length, Round=round, Iterations=iterations, Nanoseconds=ns, BytesPerOp=(double)allocated/iterations }));
            }
        }
        GC.KeepAlive(sink);
    }
}
