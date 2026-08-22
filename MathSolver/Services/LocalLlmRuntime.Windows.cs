#if WINDOWS
using MathSolver.Services.Core;

namespace MathSolver.Services;

/// <summary>
/// Shared Windows-only LLamaSharp runtime. Math Puzzle and Hardware benchmark
/// reuse the same model cache so a benchmark does not load a second copy of
/// the GGUF weights into RAM.
/// </summary>
public static class LocalLlmRuntime
{
    private static readonly BasicArithmeticEngine SharedArithmeticEngine = new();
    private static readonly FractionCalculationEngine SharedFractionEngine = new();
    private static readonly GeometryCalculationEngine SharedGeometryEngine = new();
    private static readonly FindXEngine SharedFindXEngine = new();

    public static LocalLlmQuizGenerator Generator { get; } =
        new(
            new ArithmeticQuizGenerator(SharedArithmeticEngine),
            SharedArithmeticEngine,
            new FractionQuizGenerator(SharedFractionEngine),
            new GeometryQuizGenerator(SharedGeometryEngine),
            new FindXQuizGenerator(SharedFindXEngine),
            new ProportionQuizGenerator(),
            new MotionQuizGenerator(),
            new AverageQuizGenerator(),
            new PercentageQuizGenerator());
}
#endif
