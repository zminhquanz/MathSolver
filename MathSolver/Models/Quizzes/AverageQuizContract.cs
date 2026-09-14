using System.Numerics;

namespace MathSolver.Models;

public enum AverageQuizType
{
    Direct,
    TotalToAverage,
    AverageToTotal,
    MissingValue,
    IndirectData,
    TwoGroups
}

/// <summary>
/// Hợp đồng C# cho bài toán trung bình cộng. Facts chỉ chứa những số được phép
/// xuất hiện trực tiếp trong đề; các giá trị suy ra và đáp án luôn do C# giữ.
/// </summary>
public sealed record AverageQuizContract(
    AverageQuizType Type,
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
