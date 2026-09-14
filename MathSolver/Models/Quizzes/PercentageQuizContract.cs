using System.Numerics;

namespace MathSolver.Models;

public enum PercentageQuizType
{
    FindPercentageRatio,
    FindPercentageValue,
    FindWholeFromPercentageValue
}

/// <summary>
/// Hợp đồng C# cho ba dạng phần trăm cơ bản. Model chỉ được diễn đạt lại đúng
/// các facts; phép tính, đơn vị và đáp án do C# sở hữu.
/// </summary>
public sealed record PercentageQuizContract(
    PercentageQuizType Type,
    IReadOnlyList<int> Facts,
    BigInteger CorrectAnswer,
    string AnswerUnit,
    string SubjectName,
    string ProblemText,
    string EquationText,
    string SolutionText,
    BigInteger RepresentativeLeft,
    ArithmeticOperation RepresentativeOperation,
    BigInteger RepresentativeRight);
