using System.Numerics;
using MathSolver.Models;

namespace MathSolver.Services;

public static class FractionCalculator
{
    public static FractionCalculationResult Calculate(
        BigInteger numerator1,
        BigInteger denominator1,
        BigInteger numerator2,
        BigInteger denominator2,
        FractionOperation operation)
    {
        if (denominator1.IsZero)
        {
            return Error(
                numerator1.IsZero
                    ? "Phân số thứ nhất là 0/0 nên không xác định."
                    : "Mẫu số của phân số thứ nhất phải khác 0.");
        }

        if (denominator2.IsZero)
        {
            return Error(
                numerator2.IsZero
                    ? "Phân số thứ hai là 0/0 nên không xác định."
                    : "Mẫu số của phân số thứ hai phải khác 0.");
        }

        NormalizeSign(
            ref numerator1,
            ref denominator1);

        NormalizeSign(
            ref numerator2,
            ref denominator2);

        return operation switch
        {
            FractionOperation.Add =>
                AddOrSubtract(
                    numerator1,
                    denominator1,
                    numerator2,
                    denominator2,
                    subtract: false),

            FractionOperation.Subtract =>
                AddOrSubtract(
                    numerator1,
                    denominator1,
                    numerator2,
                    denominator2,
                    subtract: true),

            FractionOperation.Multiply =>
                Multiply(
                    numerator1,
                    denominator1,
                    numerator2,
                    denominator2),

            FractionOperation.Divide =>
                Divide(
                    numerator1,
                    denominator1,
                    numerator2,
                    denominator2),

            FractionOperation.CommonDenominator =>
                CommonDenominator(
                    numerator1,
                    denominator1,
                    numerator2,
                    denominator2),

            _ => Error("Phép toán không được hỗ trợ.")
        };
    }

    private static FractionCalculationResult AddOrSubtract(
        BigInteger n1,
        BigInteger d1,
        BigInteger n2,
        BigInteger d2,
        bool subtract)
    {
        BigInteger lcm = LeastCommonMultiple(d1, d2);
        BigInteger factor1 = lcm / d1;
        BigInteger factor2 = lcm / d2;
        BigInteger converted1 = n1 * factor1;
        BigInteger converted2 = n2 * factor2;
        BigInteger rawNumerator =
            subtract
                ? converted1 - converted2
                : converted1 + converted2;

        string symbol = subtract ? "−" : "+";
        string action = subtract ? "Trừ hai tử số" : "Cộng hai tử số";

        var result = Success(
            FormatSimplified(rawNumerator, lcm));

        result.Steps.Add(
            Step(
                "Phép tính",
                $"{n1}/{d1} {symbol} {n2}/{d2}"));

        result.Steps.Add(
            Step(
                "Tìm mẫu số chung nhỏ nhất",
                $"BCNN({d1}, {d2}) = {lcm}"));

        result.Steps.Add(
            Step(
                "Quy đồng mẫu số",
                $"{n1}/{d1} = ({n1} × {factor1}) / " +
                $"({d1} × {factor1}) = {converted1}/{lcm}\n" +
                $"{n2}/{d2} = ({n2} × {factor2}) / " +
                $"({d2} × {factor2}) = {converted2}/{lcm}"));

        result.Steps.Add(
            Step(
                action,
                $"{converted1}/{lcm} {symbol} {converted2}/{lcm}\n" +
                $"= ({converted1} {symbol} {converted2})/{lcm}\n" +
                $"= {rawNumerator}/{lcm}"));

        AddSimplificationStep(
            result,
            rawNumerator,
            lcm);

        return result;
    }

    private static FractionCalculationResult Multiply(
        BigInteger n1,
        BigInteger d1,
        BigInteger n2,
        BigInteger d2)
    {
        BigInteger numerator = n1 * n2;
        BigInteger denominator = d1 * d2;

        var result = Success(
            FormatSimplified(
                numerator,
                denominator));

        result.Steps.Add(
            Step(
                "Phép tính",
                $"{n1}/{d1} × {n2}/{d2}"));

        result.Steps.Add(
            Step(
                "Nhân tử với tử, mẫu với mẫu",
                $"({n1} × {n2}) / ({d1} × {d2})\n" +
                $"= {numerator}/{denominator}"));

        AddSimplificationStep(
            result,
            numerator,
            denominator);

        return result;
    }

    private static FractionCalculationResult Divide(
        BigInteger n1,
        BigInteger d1,
        BigInteger n2,
        BigInteger d2)
    {
        if (n2.IsZero)
        {
            return Error(
                $"Không thể chia cho {n2}/{d2}, vì phân số này bằng 0.");
        }

        BigInteger numerator = n1 * d2;
        BigInteger denominator = d1 * n2;

        NormalizeSign(
            ref numerator,
            ref denominator);

        var result = Success(
            FormatSimplified(
                numerator,
                denominator));

        result.Steps.Add(
            Step(
                "Phép tính",
                $"{n1}/{d1} ÷ {n2}/{d2}"));

        result.Steps.Add(
            Step(
                "Đảo phân số chia",
                $"{n1}/{d1} ÷ {n2}/{d2}\n" +
                $"= {n1}/{d1} × {d2}/{n2}"));

        result.Steps.Add(
            Step(
                "Thực hiện phép nhân",
                $"= ({n1} × {d2}) / ({d1} × {n2})\n" +
                $"= {numerator}/{denominator}"));

        AddSimplificationStep(
            result,
            numerator,
            denominator);

        return result;
    }

    private static FractionCalculationResult CommonDenominator(
        BigInteger n1,
        BigInteger d1,
        BigInteger n2,
        BigInteger d2)
    {
        BigInteger lcm = LeastCommonMultiple(d1, d2);
        BigInteger factor1 = lcm / d1;
        BigInteger factor2 = lcm / d2;
        BigInteger converted1 = n1 * factor1;
        BigInteger converted2 = n2 * factor2;

        string answer =
            $"{converted1}/{lcm} và {converted2}/{lcm}";

        var result = Success(answer);

        result.Steps.Add(
            Step(
                "Hai phân số ban đầu",
                $"{n1}/{d1} và {n2}/{d2}"));

        result.Steps.Add(
            Step(
                "Tìm mẫu số chung nhỏ nhất",
                $"BCNN({d1}, {d2}) = {lcm}"));

        result.Steps.Add(
            Step(
                "Quy đồng mẫu số",
                $"{n1}/{d1} = ({n1} × {factor1}) / " +
                $"({d1} × {factor1}) = {converted1}/{lcm}\n" +
                $"{n2}/{d2} = ({n2} × {factor2}) / " +
                $"({d2} × {factor2}) = {converted2}/{lcm}"));

        result.Steps.Add(
            Step(
                "Kết quả quy đồng",
                answer,
                important: true));

        return result;
    }

    private static void AddSimplificationStep(
        FractionCalculationResult result,
        BigInteger numerator,
        BigInteger denominator)
    {
        if (numerator.IsZero)
        {
            result.Steps.Add(
                Step(
                    "Kết quả",
                    $"{numerator}/{denominator} = 0",
                    important: true));

            return;
        }

        BigInteger gcd =
            BigInteger.GreatestCommonDivisor(
                BigInteger.Abs(numerator),
                BigInteger.Abs(denominator));

        BigInteger simplifiedNumerator =
            numerator / gcd;

        BigInteger simplifiedDenominator =
            denominator / gcd;

        NormalizeSign(
            ref simplifiedNumerator,
            ref simplifiedDenominator);

        if (gcd > BigInteger.One)
        {
            result.Steps.Add(
                Step(
                    "Rút gọn phân số",
                    $"ƯCLN(|{numerator}|, {denominator}) = {gcd}\n" +
                    $"{numerator}/{denominator}\n" +
                    $"= ({numerator} ÷ {gcd}) / " +
                    $"({denominator} ÷ {gcd})\n" +
                    $"= {FormatFraction(simplifiedNumerator, simplifiedDenominator)}",
                    important: true));
        }
        else
        {
            result.Steps.Add(
                Step(
                    "Kết quả",
                    $"{FormatFraction(numerator, denominator)} " +
                    "đã là phân số tối giản.",
                    important: true));
        }
    }

    private static string FormatSimplified(
        BigInteger numerator,
        BigInteger denominator)
    {
        if (numerator.IsZero)
        {
            return "0";
        }

        BigInteger gcd =
            BigInteger.GreatestCommonDivisor(
                BigInteger.Abs(numerator),
                BigInteger.Abs(denominator));

        numerator /= gcd;
        denominator /= gcd;

        NormalizeSign(
            ref numerator,
            ref denominator);

        return FormatFraction(
            numerator,
            denominator);
    }

    private static string FormatFraction(
        BigInteger numerator,
        BigInteger denominator)
    {
        return denominator == BigInteger.One
            ? numerator.ToString()
            : $"{numerator}/{denominator}";
    }

    private static BigInteger LeastCommonMultiple(
        BigInteger first,
        BigInteger second)
    {
        first = BigInteger.Abs(first);
        second = BigInteger.Abs(second);

        return first /
               BigInteger.GreatestCommonDivisor(first, second) *
               second;
    }

    private static void NormalizeSign(
        ref BigInteger numerator,
        ref BigInteger denominator)
    {
        if (denominator.Sign >= 0)
        {
            return;
        }

        numerator =
            BigInteger.Negate(numerator);

        denominator =
            BigInteger.Abs(denominator);
    }

    private static FractionCalculationResult Success(
        string resultText)
    {
        return new FractionCalculationResult
        {
            IsSuccess = true,
            ResultText = resultText
        };
    }

    private static FractionCalculationResult Error(
        string message)
    {
        return new FractionCalculationResult
        {
            IsSuccess = false,
            ErrorMessage = message
        };
    }

    private static FractionSolutionStep Step(
        string title,
        string content,
        bool important = false)
    {
        return new FractionSolutionStep
        {
            Title = title,
            Content = content,
            IsImportant = important
        };
    }
}
