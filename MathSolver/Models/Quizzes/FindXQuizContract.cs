using System.Numerics;

namespace MathSolver.Models;

/// <summary>
/// Hợp đồng phương trình một bước dùng chung cho nguồn Thuật toán và AI/LLM.
/// KnownValue là toán hạng đã biết, ResultValue là vế phải và CorrectAnswer
/// là giá trị x đã được FindXEngine giải rồi thay ngược để xác minh.
/// </summary>
public sealed record FindXQuizContract(
    BigInteger KnownValue,
    BigInteger ResultValue,
    ArithmeticOperation Operation,
    bool UnknownIsLeftOperand,
    BigInteger CorrectAnswer,
    IntegerArithmeticExpression SolutionExpression)
{
    public string EquationText
    {
        get
        {
            string symbol = Operation switch
            {
                ArithmeticOperation.Add => "+",
                ArithmeticOperation.Subtract => "−",
                ArithmeticOperation.Multiply => "×",
                ArithmeticOperation.Divide => "÷",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(Operation))
            };

            return UnknownIsLeftOperand
                ? $"x {symbol} {KnownValue} = {ResultValue}"
                : $"{KnownValue} {symbol} x = {ResultValue}";
        }
    }
}
