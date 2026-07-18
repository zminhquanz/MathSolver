using MathSolver.Models;
using System.Numerics;

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

    private static FractionCalculationResult AddOrSubtract(BigInteger numerator1, BigInteger denominator1, BigInteger numerator2, BigInteger denominator2, bool subtract)
    {
        BigInteger commonDenominator =
        LeastCommonMultiple(
            denominator1,
            denominator2);

        BigInteger multiplier1 =
            commonDenominator /
            denominator1;

        BigInteger multiplier2 =
            commonDenominator /
            denominator2;

        BigInteger convertedNumerator1 =
            numerator1 *
            multiplier1;

        BigInteger convertedNumerator2 =
            numerator2 *
            multiplier2;

        BigInteger resultNumerator =
            subtract
                ? convertedNumerator1 -
                  convertedNumerator2
                : convertedNumerator1 +
                  convertedNumerator2;

        string operationSymbol =
            subtract ? "−" : "+";

        string simplifiedResult =
            FormatSimplified(
            resultNumerator,
            commonDenominator);

        string operationTitle =
            subtract
                ? "Trừ hai tử số"
                : "Cộng hai tử số";

        string operationDescription =
            subtract
                ? $"Hai phân số đã có cùng mẫu số " +
                  $"{commonDenominator}, nên giữ nguyên mẫu số " +
                  $"và trừ hai tử số:\n" +
                  $"{convertedNumerator1} − " +
                  $"{convertedNumerator2} = " +
                  $"{resultNumerator}."
                : $"Hai phân số đã có cùng mẫu số " +
                  $"{commonDenominator}, nên giữ nguyên mẫu số " +
                  $"và cộng hai tử số:\n" +
                  $"{convertedNumerator1} + " +
                  $"{convertedNumerator2} = " +
                  $"{resultNumerator}.";

        var result =
        Success(
            resultExpression:
                simplifiedResult,

            fullExpression:
                $"{numerator1}/{denominator1} " +
                $"{operationSymbol} " +
                $"{numerator2}/{denominator2} " +
                $"= {simplifiedResult}");

        // Bước 1: Phép tính ban đầu
        result.Steps.Add(
            CreateMathStep(
                title: "Phép tính",

                description:
                    subtract
                        ? "Ta thực hiện phép trừ hai phân số."
                        : "Ta thực hiện phép cộng hai phân số.",

                mathLines:
                [
                    $"{numerator1}/{denominator1} " +
                $"{operationSymbol} " +
                $"{numerator2}/{denominator2}"
                ]));

        // Bước 2: Tìm mẫu số chung
        result.Steps.Add(
            CreateTextStep(
                title:
                    "Tìm mẫu số chung nhỏ nhất",

                description:
                    $"BCNN({denominator1}, " +
                    $"{denominator2}) = " +
                    $"{commonDenominator}."));

        // Bước 3: Quy đồng
        result.Steps.Add(
            CreateMathStep(
                title:
                    "Quy đồng mẫu số",

                description:
                    $"Phân số thứ nhất nhân cả tử và mẫu " +
                    $"với {multiplier1}.\n" +
                    $"Phân số thứ hai nhân cả tử và mẫu " +
                    $"với {multiplier2}.",

                mathLines:
                [
                    $"{numerator1}/{denominator1} " +
                $"= {convertedNumerator1}/" +
                $"{commonDenominator}",

                $"{numerator2}/{denominator2} " +
                $"= {convertedNumerator2}/" +
                $"{commonDenominator}"
                ]));

        // Bước 4: Cộng hoặc trừ
        result.Steps.Add(
            CreateMathStep(
                title:
                    operationTitle,

                description:
                    operationDescription,

                mathLines:
                [
                    $"{convertedNumerator1}/" +
                $"{commonDenominator} " +
                $"{operationSymbol} " +
                $"{convertedNumerator2}/" +
                $"{commonDenominator}",

                $"= {resultNumerator}/" +
                $"{commonDenominator}"
                ]));

        // Bước 5: Rút gọn
        AddSimplificationStep(
            result,
            resultNumerator,
            commonDenominator);

        return result;
    }

    private static FractionSolutionStep CreateMathStep(string title, string description, IEnumerable<string> mathLines, bool important = false)
    {
        var step =
        new FractionSolutionStep
        {
            Title = title,
            Description = description,
            IsImportant = important
        };

        foreach (string line in mathLines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                step.MathLines.Add(line);
            }
        }

        return step;
    }

    private static FractionSolutionStep CreateTextStep(string title, string description, bool important = false)
    {
        return new FractionSolutionStep
        {
            Title = title,
            Description = description,
            IsImportant = important
        };
    }

    private static FractionCalculationResult Multiply(
        BigInteger n1,
        BigInteger d1,
        BigInteger n2,
        BigInteger d2)
    {
        BigInteger numerator = n1 * n2;
        BigInteger denominator = d1 * d2;

        string simplifiedResult =
        FormatSimplified(
            numerator,
            denominator);

        var result =
            Success(
                resultExpression:
                    simplifiedResult,

                fullExpression:
                    $"{n1}/{d1} × {n2}/{d2} " +
                    $"= {simplifiedResult}");

        result.Steps.Add(
            CreateMathStep(
                title: "Phép tính",
                description: "",
                mathLines:
                [
                    $"{n1}/{d1} × {n2}/{d2}"
                ]));

        result.Steps.Add(
            CreateMathStep(
                title: "Thực hiện phép nhân",
                description: "Nhân tử với tử, mẫu với mẫu",
                mathLines:
                [
                    $"({n1} × {n2}) / ({d1} × {d2})\n" +
                    $"= {numerator}/{denominator}"
                ]));

        result.Steps.Add(
            CreateMathStep(
                title: "Kết quả phép nhân",
                description: "",
                mathLines:
                [
                    $"{n1}/{d1} × {n2}/{d2}",
                    $"= {numerator}/{denominator}"
                ]));

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

        string simplifiedResult =
        FormatSimplified(
            numerator,
            denominator);

        var result =
            Success(
                resultExpression:
                    simplifiedResult,

                fullExpression:
                    $"{n1}/{d1} ÷ {n2}/{d2} " +
                    $"= {simplifiedResult}");

        result.Steps.Add(
            CreateMathStep(
                title: "Phép tính",
                description: "",
                mathLines:
                [
                    $"{n1}/{d1} ÷ {n2}/{d2}"
                ]));

        result.Steps.Add(
            CreateMathStep(
                title: "Đảo phân số chia",
                description: "",
                mathLines:
                [
                    $"{n1}/{d1} ÷ {n2}/{d2}\n" +
                    $"= {n1}/{d1} × {d2}/{n2}"
                ]));

        result.Steps.Add(
            CreateMathStep(
                title: "Thực hiện phép nhân",
                description: "",
                mathLines:
                [
                    $"= ({n1} × {d2}) ÷ ({d1} × {n2})\n" +
                    $"= {numerator}/{denominator}"
                ]));
        result.Steps.Add(
            CreateMathStep(
                title: "Kết quả phép chia",

                description:
                    "Đổi phép chia thành phép nhân với phân số nghịch đảo:",

                mathLines:
                [
                    $"{n1}/{d1} ÷ {n2}/{d2}",
                    $"= {n1}/{d1} × {d2}/{n2}",
                    $"= {numerator}/{denominator}"
                ]));

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

        string resultExpression = $"{converted1}/{lcm} và " + $"{converted2}/{lcm}";

        var result =
            Success(
                resultExpression:
                    resultExpression,

                fullExpression:
                    $"{n1}/{d1} và {n2}/{d2}");

        result.Steps.Add(
            CreateMathStep(
                title: "Hai phân số ban đầu",
                description: "",
                mathLines:
                [
                    $"{n1}/{d1} và {n2}/{d2}"
                ]));

        result.Steps.Add(
            CreateTextStep(
            "Tìm mẫu số chung nhỏ nhất",
            $"BCNN({d1}, {d2}) = {lcm}"));

        result.Steps.Add(
            CreateMathStep(
                title: "Quy đồng mẫu số",
                description: "",
                mathLines:
                [
                    $"{n1}/{d1} = ({n1} × {factor1}) / " +
                    $"({d1} × {factor1}) = {converted1}/{lcm}\n" +
                    $"{n2}/{d2} = ({n2} × {factor2}) / " +
                    $"({d2} × {factor2}) = {converted2}/{lcm}"
                ]));

        result.Steps.Add(
            CreateMathStep(
            title: "Kết quả quy đồng",

            description:
                "Hai phân số sau khi quy đồng là:",

            mathLines:
            [
                $"{converted1}/{lcm} và {converted2}/{lcm}"
            ],

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
                CreateMathStep(
                    title: "Kết quả",

                    description:
                        "Phân số có tử số bằng 0 nên kết quả bằng 0.",

                    mathLines:
                    [
                        $"{numerator}/{denominator} = 0"
                    ],

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
                CreateMathStep(
                    title: "Rút gọn phân số",

                    description:
                        $"ƯCLN({numerator}, {denominator}) = {gcd}. " +
                        $"Chia cả tử và mẫu cho {gcd}.",

                    mathLines:
                    [
                        $"{numerator}/{denominator} = " +
                    $"{simplifiedNumerator}/{simplifiedDenominator}"
                    ],

                    important: true));
        }
        else
        {
            result.Steps.Add(
                CreateMathStep(
                    title: "Kết quả",

                    description:
                        "Phân số đã ở dạng tối giản.",

                    mathLines:
                    [
                        $"{numerator}/{denominator}"
                    ],

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
    string resultExpression,
    string fullExpression)
    {
        return new FractionCalculationResult
        {
            IsSuccess = true,
            ResultExpression = resultExpression,
            FullExpression = fullExpression
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
        string description,
        bool important = false)
    {
        return new FractionSolutionStep
        {
            Title = title,
            Description = description,
            IsImportant = important
        };
    }
}
