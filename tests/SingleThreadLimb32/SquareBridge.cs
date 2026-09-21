using System.Numerics;
namespace MathSolver.Numerics;
internal static class SquareBridge
{
    internal static readonly List<BigInteger> Inputs=new();
    internal static bool Capture;
    internal static Limb32Square? Candidate;
    internal static int MinimumWords=1024,MaximumWords=int.MaxValue;
    internal static CancellationToken Token;
    private static bool Eligible(BigInteger value)
    {
        long words=(value.GetBitLength()+31)/32;
        return words>=MinimumWords && words<=MaximumWords;
    }
    internal static BigInteger Square(BigInteger value)
    {
        Token.ThrowIfCancellationRequested();
        if(Capture && value.GetBitLength()>=4096)Inputs.Add(value);
        return Candidate is not null && Eligible(value)?Candidate.SquareBigInteger(value,Token):value*value;
    }
    internal static BigInteger Pow(BigInteger value,int exponent)
    {
        if(exponent<=1 || !BitOperations.IsPow2((uint)exponent))return BigInteger.Pow(value,exponent);
        int squares=BitOperations.Log2((uint)exponent);
        if(Capture)
        {
            // Capture exact inputs inside the original runtime square batch.
            var current=value;
            for(int i=0;i<squares;i++)current=Square(current);
            return current;
        }
        bool any=false;
        long bits=value.GetBitLength();
        for(int i=0;i<squares;i++)
        {
            long words=(bits+31)/32;
            any|=words>=MinimumWords && words<=MaximumWords;
            bits*=2;
        }
        if(Candidate is null || !any)return BigInteger.Pow(value,exponent);
        for(int i=0;i<squares;i++)value=Square(value);
        return value;
    }
}
