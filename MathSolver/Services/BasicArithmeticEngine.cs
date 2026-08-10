using MathSolver.Models;
using MathSolver.Numerics;
using System.Numerics;

namespace MathSolver.Services;

/// <summary>
/// Nguồn sự thật dùng chung cho cộng, trừ, nhân và chia.
/// Lớp này không phụ thuộc vào MAUI hay control giao diện.
/// </summary>
public sealed class BasicArithmeticEngine
{
    public IntegerArithmeticResult CalculateInteger(
        IntegerArithmeticExpression expression)
    {
        BigInteger result;
        BigInteger remainder =
            BigInteger.Zero;

        switch (expression.Operation)
        {
            case ArithmeticOperation.Add:
                result =
                    expression.LeftOperand +
                    expression.RightOperand;
                break;

            case ArithmeticOperation.Subtract:
                result =
                    expression.LeftOperand -
                    expression.RightOperand;
                break;

            case ArithmeticOperation.Multiply:
                result =
                    expression.LeftOperand *
                    expression.RightOperand;
                break;

            case ArithmeticOperation.Divide:
                if (expression.RightOperand.IsZero)
                {
                    throw new DivideByZeroException(
                        "The right operand of a division cannot be zero.");
                }

                result =
                    BigInteger.DivRem(
                        expression.LeftOperand,
                        expression.RightOperand,
                        out remainder);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(expression),
                    expression.Operation,
                    "Unsupported arithmetic operation.");
        }

        return new IntegerArithmeticResult(
            expression,
            result,
            remainder);
    }

    public QuadDouble CalculateDecimal(
        decimal leftOperand,
        ArithmeticOperation operation,
        decimal rightOperand)
    {
        if (operation == ArithmeticOperation.Divide &&
            rightOperand == decimal.Zero)
        {
            throw new DivideByZeroException(
                "The right operand of a division cannot be zero.");
        }

        QuadDouble left =
            QuadDouble.FromDecimal(
                leftOperand);

        QuadDouble right =
            QuadDouble.FromDecimal(
                rightOperand);

        return operation switch
        {
            ArithmeticOperation.Add =>
                left + right,

            ArithmeticOperation.Subtract =>
                left - right,

            ArithmeticOperation.Multiply =>
                left * right,

            ArithmeticOperation.Divide =>
                left / right,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(operation),
                    operation,
                    "Unsupported arithmetic operation.")
        };
    }

    public bool IsEquationCorrect(
        IntegerArithmeticExpression expression,
        BigInteger proposedAnswer)
    {
        IntegerArithmeticResult result =
            CalculateInteger(
                expression);

        // Câu hỏi luyện tập hiện chỉ dùng phép chia hết. Một biểu thức chia
        // có số dư không thể được coi là bằng riêng phần thương.
        return (!result.IsDivision ||
                result.IsExactDivision) &&
               result.Result == proposedAnswer;
    }

    public static string GetSymbol(
        ArithmeticOperation operation)
    {
        return operation switch
        {
            ArithmeticOperation.Add => "+",
            ArithmeticOperation.Subtract => "−",
            ArithmeticOperation.Multiply => "×",
            ArithmeticOperation.Divide => "÷",
            _ => throw new ArgumentOutOfRangeException(
                nameof(operation),
                operation,
                "Unsupported arithmetic operation.")
        };
    }
}
