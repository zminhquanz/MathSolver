using System.Numerics;

namespace MathSolver.Models;

public enum MotionQuizType
{
    Basic,
    Chasing,
    Meeting,
    River
}

public enum MotionQuestionKind
{
    BasicDistance,
    BasicSpeed,
    BasicTime,
    BasicRestDistance,
    CatchUpTime,
    MeetingTime,
    RiverDownstreamSpeed,
    RiverUpstreamSpeed,
    RiverBoatSpeed,
    RiverCurrentSpeed
}

/// <summary>C# math puzzle data and rules.</summary>
public sealed record MotionQuizContract(
    MotionQuizType Type,
    MotionQuestionKind QuestionKind,
    IReadOnlyList<int> Facts,
    BigInteger CorrectAnswer,
    string AnswerUnit,
    string SubjectName,
    string ProblemText,
    string EquationText,
    string SolutionText,
    BigInteger RepresentativeLeft,
    ArithmeticOperation RepresentativeOperation,
    BigInteger RepresentativeRight,
    IReadOnlyList<string> RequiredProblemUnits);
