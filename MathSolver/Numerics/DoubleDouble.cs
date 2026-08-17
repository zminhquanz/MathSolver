using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace MathSolver.Numerics;

/// <summary>
/// Số thực mở rộng gồm hai thành phần <see cref="double"/> không chồng lấp.
/// Kiểu này giữ xấp xỉ 106 bit phần định trị, tương đương khoảng 31-32 chữ số
/// thập phân có nghĩa. Phần sai số của phép nhân được lấy bằng FMA phần cứng
/// khi nền tảng hỗ trợ.
/// </summary>
public readonly struct DoubleDouble :
    IComparable<DoubleDouble>,
    IEquatable<DoubleDouble>
{
    public const int SignificantDigits = 32;

    private const double TwoPow32 = 4_294_967_296d;

    public static DoubleDouble Zero { get; } = new(0d);
    public static DoubleDouble One { get; } = new(1d);
    public static DoubleDouble Two { get; } = new(2d);
    public static DoubleDouble Three { get; } = new(3d);

    // π split into a leading double plus its residual. Together they retain
    // the full DoubleDouble precision needed by complex root calculations.
    public static DoubleDouble Pi { get; } =
        new(
            3.141592653589793116d,
            1.2246467991473532072e-16d,
            alreadyNormalized: true);

    public static DoubleDouble NaN { get; } =
        new(double.NaN, 0d, alreadyNormalized: true);

    public static DoubleDouble PositiveInfinity { get; } =
        new(double.PositiveInfinity);

    public static DoubleDouble NegativeInfinity { get; } =
        new(double.NegativeInfinity);

    /// <summary>Thành phần cao, chứa phần lớn giá trị.</summary>
    public double High { get; }

    /// <summary>Thành phần thấp, giữ phần sai số còn lại.</summary>
    public double Low { get; }

    public bool IsFinite =>
        double.IsFinite(High) &&
        double.IsFinite(Low);

    public bool IsNaN =>
        double.IsNaN(High);

    public bool IsZero =>
        High == 0d &&
        Low == 0d;

    public int Sign
    {
        get
        {
            if (double.IsNaN(High))
            {
                return 0;
            }

            if (High != 0d)
            {
                return Math.Sign(High);
            }

            return Low == 0d
                ? 0
                : Math.Sign(Low);
        }
    }

    public DoubleDouble(double value)
    {
        High = value;
        Low = 0d;
    }

    private DoubleDouble(
        double high,
        double low,
        bool alreadyNormalized)
    {
        High = high;
        Low = low;
    }

    /// <summary>
    /// Chuyển Int128 theo từng limb 32 bit, tránh ép toàn bộ giá trị qua
    /// một double duy nhất.
    /// </summary>
    public static DoubleDouble FromInt128(Int128 value)
    {
        if (value == 0)
        {
            return Zero;
        }

        bool negative = value < 0;

        // Cách viết này xử lý được cả Int128.MinValue mà không làm tràn
        // ở phép đổi dấu.
        UInt128 magnitude = negative
            ? (UInt128)(-(value + 1)) + 1
            : (UInt128)value;

        Span<uint> limbs = stackalloc uint[4];
        int limbCount = 0;

        while (magnitude != 0)
        {
            limbs[limbCount++] = (uint)magnitude;
            magnitude >>= 32;
        }

        DoubleDouble result = Zero;

        for (int index = limbCount - 1;
             index >= 0;
             index--)
        {
            result =
                FusedMultiplyAdd(
                    result,
                    TwoPow32,
                    (double)limbs[index]);
        }

        return negative ? -result : result;
    }

    public static DoubleDouble FromBigInteger(BigInteger value)
    {
        if (value.IsZero)
        {
            return Zero;
        }

        bool negative = value.Sign < 0;
        BigInteger remaining = BigInteger.Abs(value);
        var limbs = new List<uint>();

        while (!remaining.IsZero)
        {
            limbs.Add((uint)(remaining & uint.MaxValue));
            remaining >>= 32;
        }

        DoubleDouble result = Zero;

        for (int index = limbs.Count - 1;
             index >= 0;
             index--)
        {
            result =
                FusedMultiplyAdd(
                    result,
                    TwoPow32,
                    (double)limbs[index]);
        }

        return negative ? -result : result;
    }

    public static DoubleDouble FromDecimal(decimal value)
    {
        int[] bits = decimal.GetBits(value);
        uint low = unchecked((uint)bits[0]);
        uint middle = unchecked((uint)bits[1]);
        uint high = unchecked((uint)bits[2]);
        int scale = (bits[3] >> 16) & 0xFF;
        bool negative = (bits[3] & int.MinValue) != 0;

        DoubleDouble result = new((double)high);
        result = FusedMultiplyAdd(result, TwoPow32, (double)middle);
        result = FusedMultiplyAdd(result, TwoPow32, (double)low);

        if (scale > 0)
        {
            result /= Pow10(scale);
        }

        return negative ? -result : result;
    }

    public static DoubleDouble FromRational(
        BigInteger numerator,
        BigInteger denominator)
    {
        if (denominator.IsZero)
        {
            return new DoubleDouble((double)numerator.Sign / 0d);
        }

        return FromBigInteger(numerator) /
               FromBigInteger(denominator);
    }

    /// <summary>
    /// Tính left × right + addend bằng số học DoubleDouble. Tích high × high
    /// dùng Math.FusedMultiplyAdd để thu lại chính xác phần dư của double.
    /// </summary>
    public static DoubleDouble FusedMultiplyAdd(
        DoubleDouble left,
        DoubleDouble right,
        DoubleDouble addend) =>
        left * right + addend;

    public static DoubleDouble Sqrt(DoubleDouble value)
    {
        if (!value.IsFinite ||
            value.Sign < 0)
        {
            return NaN;
        }

        if (value.IsZero)
        {
            return Zero;
        }

        DoubleDouble estimate =
            new(Math.Sqrt(value.ToDouble()));

        // Hạt giống double có khoảng 53 bit. Một bước Newton nâng lên gần
        // 106 bit; bước thứ hai đóng vai trò guard ở biên làm tròn.
        for (int iteration = 0;
             iteration < 2;
             iteration++)
        {
            estimate +=
                (value - estimate * estimate) /
                (Two * estimate);
        }

        return estimate;
    }

    public static DoubleDouble Cbrt(DoubleDouble value)
    {
        if (!value.IsFinite)
        {
            return new DoubleDouble(
                Math.Cbrt(value.ToDouble()));
        }

        if (value.IsZero)
        {
            return Zero;
        }

        DoubleDouble estimate =
            new(Math.Cbrt(value.ToDouble()));

        // x(k+1) = (2x + a/x²) / 3.
        for (int iteration = 0;
             iteration < 2;
             iteration++)
        {
            estimate =
                (Two * estimate +
                 value / (estimate * estimate)) /
                Three;
        }

        return estimate;
    }

    /// <summary>
    /// Tính căn bậc n dương. Math.Pow tạo nghiệm gần đúng nhanh, sau đó hai
    /// bước Newton khôi phục độ chính xác DoubleDouble.
    /// </summary>
    public static DoubleDouble RootUsingPow(
        DoubleDouble value,
        int degree)
    {
        if (degree < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(degree));
        }

        if (!value.IsFinite ||
            value.Sign < 0)
        {
            return NaN;
        }

        if (value.IsZero)
        {
            return Zero;
        }

        if (degree == 2)
        {
            return Sqrt(value);
        }

        if (degree == 3)
        {
            return Cbrt(value);
        }

        DoubleDouble estimate =
            new(
                Math.Pow(
                    value.ToDouble(),
                    1d / degree));

        DoubleDouble degreeValue = new(degree);
        DoubleDouble degreeMinusOne = new(degree - 1);

        // Newton cho x^degree = value:
        // x(k+1) = ((degree - 1)x + value/x^(degree - 1)) / degree.
        for (int iteration = 0;
             iteration < 2;
             iteration++)
        {
            DoubleDouble previousPower =
                Pow(estimate, degree - 1);

            estimate =
                (degreeMinusOne * estimate +
                 value / previousPower) /
                degreeValue;
        }

        return estimate;
    }

    /// <summary>
    /// Computes sin(angle) and cos(angle) with DoubleDouble arithmetic.
    /// The root engine only calls this for |angle| <= π/2, so a fixed
    /// Taylor expansion converges quickly and keeps the result at the same
    /// ~31-32 digit precision as the real root path.
    /// </summary>
    public static void SinCos(
        DoubleDouble angle,
        out DoubleDouble sine,
        out DoubleDouble cosine)
    {
        if (!angle.IsFinite)
        {
            sine = NaN;
            cosine = NaN;
            return;
        }

        DoubleDouble angleSquared =
            angle * angle;

        DoubleDouble sineTerm = angle;
        DoubleDouble cosineTerm = One;
        sine = angle;
        cosine = One;

        // At |x| <= π/2, 24 terms are far beyond the ~106-bit mantissa
        // requirement. Using a fixed count also avoids a double-based
        // termination test that could silently lower precision.
        for (int index = 1;
             index < 24;
             index++)
        {
            int sineLeft = 2 * index;
            int sineRight = sineLeft + 1;
            sineTerm *=
                -angleSquared /
                new DoubleDouble(
                    (double)sineLeft * sineRight);
            sine += sineTerm;

            int cosineLeft = 2 * index - 1;
            int cosineRight = 2 * index;
            cosineTerm *=
                -angleSquared /
                new DoubleDouble(
                    (double)cosineLeft * cosineRight);
            cosine += cosineTerm;
        }
    }

    public static DoubleDouble Pow(
        DoubleDouble value,
        int exponent)
    {
        if (exponent == 0)
        {
            return One;
        }

        bool negativeExponent = exponent < 0;
        long remaining = negativeExponent
            ? -(long)exponent
            : exponent;

        DoubleDouble result = One;
        DoubleDouble factor = value;

        while (remaining > 0)
        {
            if ((remaining & 1L) != 0)
            {
                result *= factor;
            }

            remaining >>= 1;

            if (remaining > 0)
            {
                factor *= factor;
            }
        }

        return negativeExponent
            ? One / result
            : result;
    }

    public static DoubleDouble Pow10(int exponent)
    {
        if (exponent == 0)
        {
            return One;
        }

        bool negativeExponent = exponent < 0;
        long remaining = negativeExponent
            ? -(long)exponent
            : exponent;

        DoubleDouble result = One;
        DoubleDouble factor = new(10d);

        while (remaining > 0)
        {
            if ((remaining & 1L) != 0)
            {
                result *= factor;
            }

            remaining >>= 1;

            if (remaining > 0)
            {
                factor *= factor;
            }
        }

        return negativeExponent
            ? One / result
            : result;
    }

    public static DoubleDouble Abs(DoubleDouble value) =>
        value.Sign < 0 ? -value : value;

    public static DoubleDouble Max(
        DoubleDouble left,
        DoubleDouble right) =>
        left >= right ? left : right;

    public static DoubleDouble Min(
        DoubleDouble left,
        DoubleDouble right) =>
        left <= right ? left : right;

    public static DoubleDouble CopySign(
        DoubleDouble magnitude,
        DoubleDouble signSource)
    {
        DoubleDouble absoluteMagnitude = Abs(magnitude);
        return signSource.Sign < 0
            ? -absoluteMagnitude
            : absoluteMagnitude;
    }

    public double ToDouble() =>
        High + Low;

    public string ToGeneralString(
        int significantDigits = SignificantDigits,
        int scientificUpperExponent = 18,
        int scientificLowerExponent = -10)
    {
        significantDigits =
            Math.Clamp(significantDigits, 1, SignificantDigits);

        if (!IsFinite)
        {
            if (IsNaN)
            {
                return "NaN";
            }

            return Sign < 0
                ? "-Infinity"
                : "Infinity";
        }

        if (IsZero)
        {
            return "0";
        }

        bool negative = Sign < 0;
        DoubleDouble absoluteValue = Abs(this);
        int exponent =
            (int)Math.Floor(
                Math.Log10(absoluteValue.ToDouble()));

        DoubleDouble normalized =
            absoluteValue / Pow10(exponent);

        while (normalized >= 10d)
        {
            normalized /= 10d;
            exponent++;
        }

        while (normalized < 1d)
        {
            normalized *= 10d;
            exponent--;
        }

        char[] digits = new char[significantDigits + 1];
        DoubleDouble remaining = normalized;

        for (int index = 0;
             index < digits.Length;
             index++)
        {
            int digit = FloorDigit(remaining);
            digits[index] = (char)('0' + digit);
            remaining = (remaining - digit) * 10d;
        }

        bool roundUp = digits[significantDigits] >= '5';
        int keptLength = significantDigits;

        if (roundUp)
        {
            int carryIndex = keptLength - 1;

            while (carryIndex >= 0 &&
                   digits[carryIndex] == '9')
            {
                digits[carryIndex] = '0';
                carryIndex--;
            }

            if (carryIndex >= 0)
            {
                digits[carryIndex]++;
            }
            else
            {
                digits[0] = '1';

                for (int index = 1;
                     index < keptLength;
                     index++)
                {
                    digits[index] = '0';
                }

                exponent++;
            }
        }

        while (keptLength > 1 &&
               digits[keptLength - 1] == '0')
        {
            keptLength--;
        }

        string digitText = new(digits, 0, keptLength);
        string signText = negative ? "-" : string.Empty;

        if (exponent >= scientificUpperExponent ||
            exponent <= scientificLowerExponent)
        {
            string mantissa = digitText.Length == 1
                ? digitText
                : digitText.Insert(1, ".");

            return
                $"{signText}{mantissa}e" +
                exponent.ToString(CultureInfo.InvariantCulture);
        }

        int decimalPointIndex = exponent + 1;
        var builder =
            new StringBuilder(
                signText.Length +
                digitText.Length +
                Math.Abs(decimalPointIndex) +
                4);

        builder.Append(signText);

        if (decimalPointIndex <= 0)
        {
            builder.Append("0.");
            builder.Append('0', -decimalPointIndex);
            builder.Append(digitText);
        }
        else if (decimalPointIndex >= digitText.Length)
        {
            builder.Append(digitText);
            builder.Append('0', decimalPointIndex - digitText.Length);
        }
        else
        {
            builder.Append(digitText.AsSpan(0, decimalPointIndex));
            builder.Append('.');
            builder.Append(digitText.AsSpan(decimalPointIndex));
        }

        return builder.ToString();
    }

    public int CompareTo(DoubleDouble other)
    {
        int highComparison = High.CompareTo(other.High);
        return highComparison != 0
            ? highComparison
            : Low.CompareTo(other.Low);
    }

    public bool Equals(DoubleDouble other) =>
        High.Equals(other.High) &&
        Low.Equals(other.Low);

    public override bool Equals(object? obj) =>
        obj is DoubleDouble other &&
        Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(High, Low);

    public override string ToString() =>
        ToGeneralString();

    public static implicit operator DoubleDouble(double value) =>
        new(value);

    public static implicit operator DoubleDouble(int value) =>
        new(value);

    public static implicit operator DoubleDouble(long value) =>
        new(value);

    public static explicit operator double(DoubleDouble value) =>
        value.ToDouble();

    public static DoubleDouble operator +(
        DoubleDouble left,
        DoubleDouble right)
    {
        if (!left.IsFinite ||
            !right.IsFinite)
        {
            return new DoubleDouble(
                left.ToDouble() + right.ToDouble());
        }

        TwoSum(
            left.High,
            right.High,
            out double highSum,
            out double highError);

        TwoSum(
            left.Low,
            right.Low,
            out double lowSum,
            out double lowError);

        highError += lowSum;

        QuickTwoSum(
            highSum,
            highError,
            out double combinedHigh,
            out double combinedLow);

        combinedLow += lowError;

        QuickTwoSum(
            combinedHigh,
            combinedLow,
            out double resultHigh,
            out double resultLow);

        return new DoubleDouble(
            resultHigh,
            resultLow,
            alreadyNormalized: true);
    }

    public static DoubleDouble operator -(
        DoubleDouble left,
        DoubleDouble right) =>
        left + -right;

    public static DoubleDouble operator -(DoubleDouble value) =>
        new(
            -value.High,
            -value.Low,
            alreadyNormalized: true);

    public static DoubleDouble operator *(
        DoubleDouble left,
        DoubleDouble right)
    {
        if (!left.IsFinite ||
            !right.IsFinite)
        {
            return new DoubleDouble(
                left.ToDouble() * right.ToDouble());
        }

        TwoProduct(
            left.High,
            right.High,
            out double product,
            out double productError);

        productError +=
            left.High * right.Low +
            left.Low * right.High;

        QuickTwoSum(
            product,
            productError,
            out double resultHigh,
            out double resultLow);

        resultLow += left.Low * right.Low;

        QuickTwoSum(
            resultHigh,
            resultLow,
            out resultHigh,
            out resultLow);

        return new DoubleDouble(
            resultHigh,
            resultLow,
            alreadyNormalized: true);
    }

    public static DoubleDouble operator /(
        DoubleDouble left,
        DoubleDouble right)
    {
        if (!left.IsFinite ||
            !right.IsFinite ||
            right.IsZero)
        {
            return new DoubleDouble(
                left.ToDouble() / right.ToDouble());
        }

        double first = left.High / right.High;
        DoubleDouble remainder =
            left - right * first;

        double second = remainder.High / right.High;
        remainder -= right * second;

        double third = remainder.High / right.High;

        return new DoubleDouble(first) +
               new DoubleDouble(second) +
               new DoubleDouble(third);
    }

    public static bool operator <(
        DoubleDouble left,
        DoubleDouble right) =>
        left.CompareTo(right) < 0;

    public static bool operator >(
        DoubleDouble left,
        DoubleDouble right) =>
        left.CompareTo(right) > 0;

    public static bool operator <=(
        DoubleDouble left,
        DoubleDouble right) =>
        left.CompareTo(right) <= 0;

    public static bool operator >=(
        DoubleDouble left,
        DoubleDouble right) =>
        left.CompareTo(right) >= 0;

    public static bool operator ==(
        DoubleDouble left,
        DoubleDouble right) =>
        left.Equals(right);

    public static bool operator !=(
        DoubleDouble left,
        DoubleDouble right) =>
        !left.Equals(right);

    private static int FloorDigit(DoubleDouble value)
    {
        int digit =
            Math.Clamp(
                (int)Math.Floor(value.ToDouble()),
                0,
                9);

        while (digit > 0 &&
               value < digit)
        {
            digit--;
        }

        while (digit < 9 &&
               value >= digit + 1)
        {
            digit++;
        }

        return digit;
    }

    private static void TwoSum(
        double left,
        double right,
        out double sum,
        out double error)
    {
        sum = left + right;
        double virtualRight = sum - left;
        error =
            (left - (sum - virtualRight)) +
            (right - virtualRight);
    }

    private static void QuickTwoSum(
        double left,
        double right,
        out double sum,
        out double error)
    {
        sum = left + right;
        error = right - (sum - left);
    }

    private static void TwoProduct(
        double left,
        double right,
        out double product,
        out double error)
    {
        product = left * right;
        error = Math.FusedMultiplyAdd(left, right, -product);
    }
}
