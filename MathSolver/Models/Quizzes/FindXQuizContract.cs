using System.Numerics;

namespace MathSolver.Models;

/// <summary>C# math puzzle data and rules.</summary>
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
