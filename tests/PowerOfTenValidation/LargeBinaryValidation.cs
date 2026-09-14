using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using MathSolver.Numerics;
using MathSolver.Services.Core;

internal static class LargeBinaryValidation
{
    internal static object ValidateWide()
    {
        // Exercise real arrays across both 2^31 and 2^32 bit boundaries without
        // timing a prohibitively large single-threaded dense exponentiation.
        long shift = (1L << 32) + 31;
        var shifted = LargeBinaryUnsigned.ShiftWords(new uint[]{13},shift,default);
        if(shifted.BitLength!=shift+4 || shifted.Words.Span[..(int)(shift/32)].ContainsAnyExcept(0U) ||
            shifted.Words.Span[^2]!=0x80000000U || shifted.Words.Span[^1]!=6)
            throw new Exception("64-bit shift boundary mismatch");
        uint[] source=new uint[(1<<26)+1]; source[^1]=1;
        uint[] square=LargeBinaryUnsigned.Square(source,default);
        if(square.Length!=(1<<27)+1 || square[^1]!=1 || square.AsSpan(0,square.Length-1).ContainsAnyExcept(0U))
            throw new Exception("Single-thread square beyond BigInteger capacity mismatch");
        return new {Kind="wide-boundary-validation",ShiftedBits=shifted.BitLength,SquareBits=(1L<<32)+1,Passed=true};
    }

    internal static void Benchmark(long basis, int exponent, int workers, int rounds, bool export)
    {
        // Deliberately kept at 1M in the audit. Reference work and conversion
        // back to BigInteger for equality are outside the arithmetic timer.
        BigInteger expected = BigInteger.Pow(basis, exponent);
        for(int round=0;round<rounds;round++)
        foreach(string mode in round%2==0 ? new[]{"native-single","packed-single","packed-parallel"} :
            new[]{"packed-parallel","packed-single","native-single"})
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            long allocated=GC.GetTotalAllocatedBytes(true);
            var timer=Stopwatch.StartNew();
            var result=PowerOfTenArithmetic.Pow(basis,exponent,mode=="packed-parallel"?workers:1,null,default,
                forceLargeResult:mode!="native-single");
            double milliseconds=timer.Elapsed.TotalMilliseconds;
            allocated=GC.GetTotalAllocatedBytes(true)-allocated;
            BigInteger actual=result.BinaryValue??result.LargeMagnitude!.ToBigInteger()*(result.IsNegative?-1:1);
            if(actual!=expected)throw new Exception("Packed benchmark mismatch");
            double? exportMs=null;
            if(export && result.LargeMagnitude is {} binary)
            {
                timer.Restart();
                int last=0;
                var decimalValue=ParallelBigUnsigned.FromBinaryMagnitude(binary,result.WorkerCount,(done,total)=>{
                    if(done<last || done>total)throw new Exception("Import progress mismatch");last=done;
                },default);
                using var writer=new StringWriter();
                if(result.IsNegative)writer.Write('-');
                decimalValue.WriteDecimalBlocks(writer,4096,static()=>{},default,true);
                exportMs=timer.Elapsed.TotalMilliseconds;
                if(writer.ToString()!=expected.ToString(CultureInfo.InvariantCulture))throw new Exception("Packed full export mismatch");
            }
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {Kind="packed-benchmark",Base=basis,
                Exponent=exponent,Mode=mode,Round=round,Milliseconds=milliseconds,AllocatedBytes=allocated,
                ExportMs=exportMs,ResultBytes=result.LargeMagnitude?.StorageBytes??result.Value.GetByteCount(),Passed=true}));
        }
    }

    internal static int ValidateSmall(int workers)
    {
        int passed = 0;
        var random = new Random(19251);
        foreach (int length in new[] { 0,1,2,15,16,17,31,32,33,63,64,65,127,128,129,511,512,513,1025,4095,4096,4097 })
        foreach (bool ones in new[] {false,true})
        {
            uint[] words = new uint[length];
            for (int i=0; i<length; i++) words[i] = ones ? uint.MaxValue : (uint)random.NextInt64(1L<<32);
            BigInteger native = new(MemoryMarshal.AsBytes(words.AsSpan()), isUnsigned:true);
            var square = LargeBinaryUnsigned.FromWords(LargeBinaryUnsigned.Square(words, default, 16));
            if (square.ToBigInteger() != native*native) throw new Exception($"Karatsuba square mismatch {length}, {ones}");
            passed++;
            foreach (int shift in new[] {0,1,15,16,17,31,32,33,63,64,65})
            {
                var shifted = LargeBinaryUnsigned.ShiftWords(words,shift,default);
                if (shifted.ToBigInteger() != native<<shift) throw new Exception("Packed shift mismatch");
                uint[] halfWords = new uint[length*2];
                for(int i=0;i<length;i++) {halfWords[i*2]=words[i]&0xffff;halfWords[i*2+1]=words[i]>>16;}
                if(LargeBinaryUnsigned.ShiftRadix16(halfWords,shift,default).ToBigInteger()!=native<<shift)
                    throw new Exception("NTT packing mismatch");
                passed++;
            }
            // Irregular values (including zero and full carry chains), not just
            // powers of ten, prove that conversion consumes actual binary data.
            if (length <= 1025)
            foreach(int w in new[] {1,workers}.Distinct())
            {
                var binary = LargeBinaryUnsigned.FromWords(words);
                var decimalValue = ParallelBigUnsigned.FromBinaryMagnitude(binary,w,null,default,leafWords:16);
                using var writer = new StringWriter();
                decimalValue.WriteDecimalBlocks(writer,4096,static()=>{},default,true);
                if(writer.ToString()!=native.ToString(CultureInfo.InvariantCulture))
                    throw new Exception($"Binary export mismatch {length}, {ones}, {w}");
                passed++;
            }
        }
        foreach(var (b,e) in new (long,int)[] {(10,1),(10,15),(10,16),(10,17),(10,31),(10,32),(10,33),
            (-1000,9999),(1000000000000000000,10000),(10,100000)})
        foreach(int w in new[] {1,workers}.Distinct())
        {
            int last=0,total=PowerOfTenArithmetic.OperationCount(PowerOfTenArithmetic.GetBinaryExponent(b,e));
            var result = PowerOfTenArithmetic.Pow(b,e,w,(done,count)=>{
                if(done<=last || count!=total) throw new Exception("Progress mismatch");last=done;
            },default,forceLargeResult:true);
            var expected=BigInteger.Pow(b,e);
            if(result.LargeMagnitude!.ToBigInteger()!=BigInteger.Abs(expected) || result.IsNegative!=(expected.Sign<0) ||
                result.BinaryValue!=null || last!=total || (w==1 && result.Diagnostics!=null))
                throw new Exception("Forced large result mismatch");
            passed++;
        }
        foreach(int m in new[] {1,15,16,17,31,32,33,10000,20000})
        {
            var result = ParallelBigUnsigned.PowFiveAndShift(m,workers,null,default,1024,true);
            if(result.LargeMagnitude!.ToBigInteger()!=BigInteger.Pow(10,m)) throw new Exception("Segmented packing mismatch");
            passed++;
        }
        foreach(int w in new[] {1,workers}.Distinct())
        foreach(int cancelAt in new[] {0,1,PowerOfTenArithmetic.OperationCount(10000)})
        {
            using var cts = new CancellationTokenSource(); if(cancelAt==0)cts.Cancel();
            try { PowerOfTenArithmetic.Pow(10,10000,w,(done,_)=>{if(done>=cancelAt)cts.Cancel();},cts.Token,true);
                throw new Exception("Large computation cancellation ignored"); }
            catch(OperationCanceledException){passed++;}
        }
        foreach(int w in new[] {1,workers}.Distinct())
        {
            using var cts=new CancellationTokenSource();
            var binary=LargeBinaryUnsigned.ShiftWords(new uint[]{12345},2048,default);
            try { ParallelBigUnsigned.FromBinaryMagnitude(binary,w,(_,_)=>cts.Cancel(),cts.Token,16);
                throw new Exception("Conversion cancellation ignored"); }
            catch(OperationCanceledException){passed++;}
            // Huge inputs must start arithmetic in either mode, never return a
            // decimal radix shortcut. Stop after the first completed square.
            using var stop=new CancellationTokenSource();
            try { PowerOfTenArithmetic.Pow(1000000000000000000,100000000,w,(_,_)=>stop.Cancel(),stop.Token);
                throw new Exception("Huge input failed to start cancellable arithmetic"); }
            catch(OperationCanceledException){passed++;}
        }
        return passed;
    }

    internal static async Task<object> ValidateLarge(PowerRootEngine engine, long basis, int exponent, int workers)
    {
        int m=PowerOfTenArithmetic.GetBinaryExponent(basis,exponent), last=0;
        var timer=Stopwatch.StartNew();
        var result=await engine.ComputePowerOfTenAsync(basis,exponent,workers,(done,total)=>{
            if(done<=last || total!=PowerOfTenArithmetic.OperationCount(m))throw new Exception("Progress mismatch");
            last=done;
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new {Kind="progress",Done=done,Total=total,Seconds=timer.Elapsed.TotalSeconds}));
        },default);
        double arithmeticSeconds=timer.Elapsed.TotalSeconds;
        var binary=result.LargeMagnitude??throw new Exception("Missing large binary result");
        foreach(uint prime in new uint[]{1000000007,1000000009,4294967291})
            if(binary.Remainder(prime,default)!=(uint)BigInteger.ModPow(BigInteger.Abs(basis),exponent,prime))
                throw new Exception("Large modular residue mismatch");
        if(binary.BitLength!=(long)Math.Floor(m*Math.Log2(10))+1 || last!=PowerOfTenArithmetic.OperationCount(m) ||
            result.IsNegative!=(basis<0 && (exponent&1)!=0)) throw new Exception("Large result metadata mismatch");
        if(binary.Words.Span[..(m/32)].ContainsAnyExcept(0U) ||
            (binary.Words.Span[m/32] & ((1U<<(m%32))-1))!=0 ||
            (binary.Words.Span[m/32] & (1U<<(m%32)))==0) throw new Exception("Large shift mismatch");
        return new {Kind="large-binary-validation",Base=basis,Exponent=exponent,Workers=result.WorkerCount,
            binary.BitLength,binary.StorageBytes,ArithmeticSeconds=arithmeticSeconds,
            NttProducts=result.Diagnostics?.NttMultiplicationCount??0,Passed=true};
    }
}
