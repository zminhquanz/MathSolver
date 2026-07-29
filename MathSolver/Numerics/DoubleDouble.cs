using System.Globalization;
using System.Text;

namespace MathSolver.Numerics;

/// <summary>
/// Số thực Double Double gồm hai giá trị double đã chuẩn hóa.
/// Tổng Hi + Lo cung cấp xấp xỉ 106 bit phần định trị,
/// tương đương khoảng 31-32 chữ số thập phân có nghĩa.
/// </summary>
public readonly struct DoubleDouble :
    IComparable<DoubleDouble>,
    IEquatable<DoubleDouble>
{
    public const int SignificantDigits =
        32;

    private const double TwoPow32 =
        4_294_967_296d;

    public static DoubleDouble Zero { get; } =
        new(0d);

    public static DoubleDouble One { get; } =
        new(1d);

    public static DoubleDouble NaN { get; } =
        new(
            double.NaN,
            0d,
            alreadyNormalized:
            true);

    public double Hi { get; }

    public double Lo { get; }

    public bool IsFinite =>
        double.IsFinite(
            Hi) &&
        double.IsFinite(
            Lo);

    public bool IsZero =>
        Hi == 0d &&
        Lo == 0d;

    public int Sign
    {
        get
        {
            if (Hi > 0d)
            {
                return 1;
            }

            if (Hi < 0d)
            {
                return -1;
            }

            if (Lo > 0d)
            {
                return 1;
            }

            if (Lo < 0d)
            {
                return -1;
            }

            return 0;
        }
    }

    public DoubleDouble(
        double value)
    {
        Hi =
            value;

        Lo =
            0d;
    }

    private DoubleDouble(
        double hi,
        double lo,
        bool alreadyNormalized)
    {
        Hi =
            hi;

        Lo =
            lo;
    }

    public static DoubleDouble FromDecimal(
        decimal value)
    {
        int[] bits =
            decimal.GetBits(
                value);

        uint low =
            unchecked(
                (uint)bits[0]);

        uint middle =
            unchecked(
                (uint)bits[1]);

        uint high =
            unchecked(
                (uint)bits[2]);

        int scale =
            (bits[3] >> 16) &
            0x7F;

        bool isNegative =
            (bits[3] &
             int.MinValue) !=
            0;

        DoubleDouble result =
            new(
                (double)high);

        result =
            result *
            TwoPow32 +
            (double)middle;

        result =
            result *
            TwoPow32 +
            (double)low;

        if (scale > 0)
        {
            result /=
                Pow10(
                    scale);
        }

        return isNegative
            ? -result
            : result;
    }

    public static DoubleDouble FusedMultiplyAdd(
        DoubleDouble left,
        DoubleDouble right,
        DoubleDouble addend)
    {
        // Phép nhân Double Double dùng Math.FusedMultiplyAdd để lấy chính xác
        // phần sai số của tích hai thành phần high. Sau đó mới cộng addend.
        return left *
               right +
               addend;
    }

    public static DoubleDouble Sqrt(
        DoubleDouble value)
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
            new(
                Math.Sqrt(
                    value.ToDouble()));

        // Nâng nghiệm khởi tạo double lên độ chính xác Double Double.
        for (int iteration = 0;
             iteration < 3;
             iteration++)
        {
            estimate =
                0.5d *
                (estimate +
                 value /
                 estimate);
        }

        return estimate;
    }

    public static DoubleDouble Abs(
        DoubleDouble value)
    {
        return value.Sign < 0
            ? -value
            : value;
    }

    public static DoubleDouble Max(
        DoubleDouble left,
        DoubleDouble right)
    {
        return left >= right
            ? left
            : right;
    }

    public static DoubleDouble CopySign(
        DoubleDouble magnitude,
        DoubleDouble signSource)
    {
        DoubleDouble absoluteMagnitude =
            Abs(
                magnitude);

        return signSource.Sign < 0
            ? -absoluteMagnitude
            : absoluteMagnitude;
    }

    public double ToDouble()
    {
        return Hi +
               Lo;
    }

    /// <summary>
    /// Trả về chuỗi tổng quát với tối đa significantDigits chữ số có nghĩa.
    /// Dạng khoa học dùng ký hiệu e để lớp giao diện đổi sang số mũ.
    /// </summary>
    public string ToGeneralString(
        int significantDigits =
            SignificantDigits,
        int scientificUpperExponent =
            18,
        int scientificLowerExponent =
            -10)
    {
        significantDigits =
            Math.Clamp(
                significantDigits,
                1,
                SignificantDigits);

        if (!IsFinite)
        {
            if (double.IsNaN(
                    Hi))
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

        bool isNegative =
            Sign < 0;

        DoubleDouble absoluteValue =
            Abs(
                this);

        int exponent =
            (int)Math.Floor(
                Math.Log10(
                    absoluteValue.ToDouble()));

        DoubleDouble normalized =
            absoluteValue /
            Pow10(
                exponent);

        while (normalized >=
               10d)
        {
            normalized /=
                10d;

            exponent++;
        }

        while (normalized <
               1d)
        {
            normalized *=
                10d;

            exponent--;
        }

        var digits =
            new char[
                significantDigits +
                1];

        DoubleDouble remaining =
            normalized;

        for (int index = 0;
             index < digits.Length;
             index++)
        {
            int digit =
                (int)Math.Floor(
                    remaining.ToDouble());

            digit =
                Math.Clamp(
                    digit,
                    0,
                    9);

            digits[index] =
                (char)('0' +
                       digit);

            remaining =
                (remaining -
                 (double)digit) *
                10d;
        }

        bool roundUp =
            digits[significantDigits] >=
            '5';

        int keptLength =
            significantDigits;

        if (roundUp)
        {
            int index =
                keptLength -
                1;

            while (index >= 0 &&
                   digits[index] ==
                   '9')
            {
                digits[index] =
                    '0';

                index--;
            }

            if (index >= 0)
            {
                digits[index]++;
            }
            else
            {
                digits[0] =
                    '1';

                for (int resetIndex = 1;
                     resetIndex <
                     keptLength;
                     resetIndex++)
                {
                    digits[resetIndex] =
                        '0';
                }

                exponent++;
            }
        }

        while (keptLength > 1 &&
               digits[keptLength - 1] ==
               '0')
        {
            keptLength--;
        }

        string digitText =
            new(
                digits,
                0,
                keptLength);

        string signText =
            isNegative
                ? "-"
                : string.Empty;

        if (exponent >=
                scientificUpperExponent ||
            exponent <=
                scientificLowerExponent)
        {
            string mantissa =
                digitText.Length == 1
                    ? digitText
                    : digitText.Insert(
                        1,
                        ".");

            return
                $"{signText}{mantissa}e" +
                exponent.ToString(
                    CultureInfo.InvariantCulture);
        }

        int decimalPointIndex =
            exponent +
            1;

        var builder =
            new StringBuilder(
                signText.Length +
                digitText.Length +
                Math.Abs(
                    decimalPointIndex) +
                4);

        builder.Append(
            signText);

        if (decimalPointIndex <= 0)
        {
            builder.Append(
                "0.");

            builder.Append(
                '0',
                -decimalPointIndex);

            builder.Append(
                digitText);
        }
        else if (decimalPointIndex >=
                 digitText.Length)
        {
            builder.Append(
                digitText);

            builder.Append(
                '0',
                decimalPointIndex -
                digitText.Length);
        }
        else
        {
            builder.Append(
                digitText.AsSpan(
                    0,
                    decimalPointIndex));

            builder.Append(
                '.');

            builder.Append(
                digitText.AsSpan(
                    decimalPointIndex));
        }

        return builder.ToString();
    }

    public int CompareTo(
        DoubleDouble other)
    {
        if (Hi <
            other.Hi)
        {
            return -1;
        }

        if (Hi >
            other.Hi)
        {
            return 1;
        }

        return Lo.CompareTo(
            other.Lo);
    }

    public bool Equals(
        DoubleDouble other)
    {
        return Hi.Equals(
                   other.Hi) &&
               Lo.Equals(
                   other.Lo);
    }

    public override bool Equals(
        object? obj)
    {
        return obj is DoubleDouble other &&
               Equals(
                   other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            Hi,
            Lo);
    }

    public override string ToString()
    {
        return ToGeneralString();
    }

    public static implicit operator DoubleDouble(
        double value)
    {
        return new DoubleDouble(
            value);
    }

    public static explicit operator double(
        DoubleDouble value)
    {
        return value.ToDouble();
    }

    public static DoubleDouble operator +(
        DoubleDouble left,
        DoubleDouble right)
    {
        TwoSum(
            left.Hi,
            right.Hi,
            out double sum,
            out double error);

        error +=
            left.Lo +
            right.Lo;

        return Renormalize(
            sum,
            error);
    }

    public static DoubleDouble operator -(
        DoubleDouble left,
        DoubleDouble right)
    {
        return left +
               -right;
    }

    public static DoubleDouble operator -(
        DoubleDouble value)
    {
        return new DoubleDouble(
            -value.Hi,
            -value.Lo,
            alreadyNormalized:
            true);
    }

    public static DoubleDouble operator *(
        DoubleDouble left,
        DoubleDouble right)
    {
        double product =
            left.Hi *
            right.Hi;

        double error =
            Math.FusedMultiplyAdd(
                left.Hi,
                right.Hi,
                -product);

        error =
            Math.FusedMultiplyAdd(
                left.Hi,
                right.Lo,
                error);

        error =
            Math.FusedMultiplyAdd(
                left.Lo,
                right.Hi,
                error);

        error =
            Math.FusedMultiplyAdd(
                left.Lo,
                right.Lo,
                error);

        return Renormalize(
            product,
            error);
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
                left.ToDouble() /
                right.ToDouble());
        }

        double firstQuotient =
            left.Hi /
            right.Hi;

        DoubleDouble quotient =
            new(
                firstQuotient);

        DoubleDouble remainder =
            left -
            right *
            quotient;

        double secondQuotient =
            remainder.ToDouble() /
            right.Hi;

        quotient +=
            secondQuotient;

        remainder =
            left -
            right *
            quotient;

        double thirdQuotient =
            remainder.ToDouble() /
            right.Hi;

        return quotient +
               thirdQuotient;
    }

    public static bool operator <(
        DoubleDouble left,
        DoubleDouble right)
    {
        return left.CompareTo(
                   right) <
               0;
    }

    public static bool operator >(
        DoubleDouble left,
        DoubleDouble right)
    {
        return left.CompareTo(
                   right) >
               0;
    }

    public static bool operator <=(
        DoubleDouble left,
        DoubleDouble right)
    {
        return left.CompareTo(
                   right) <=
               0;
    }

    public static bool operator >=(
        DoubleDouble left,
        DoubleDouble right)
    {
        return left.CompareTo(
                   right) >=
               0;
    }

    public static bool operator ==(
        DoubleDouble left,
        DoubleDouble right)
    {
        return left.Equals(
            right);
    }

    public static bool operator !=(
        DoubleDouble left,
        DoubleDouble right)
    {
        return !left.Equals(
            right);
    }

    private static DoubleDouble Pow10(
        int exponent)
    {
        if (exponent == 0)
        {
            return One;
        }

        bool isNegative =
            exponent < 0;

        int remainingExponent =
            Math.Abs(
                exponent);

        DoubleDouble result =
            One;

        DoubleDouble factor =
            10d;

        while (remainingExponent > 0)
        {
            if ((remainingExponent &
                 1) !=
                0)
            {
                result *=
                    factor;
            }

            remainingExponent >>=
                1;

            if (remainingExponent > 0)
            {
                factor *=
                    factor;
            }
        }

        return isNegative
            ? One /
              result
            : result;
    }

    private static DoubleDouble Renormalize(
        double high,
        double low)
    {
        if (!double.IsFinite(
                high))
        {
            return new DoubleDouble(
                high,
                0d,
                alreadyNormalized:
                true);
        }

        double sum =
            high +
            low;

        double error =
            low -
            (sum -
             high);

        return new DoubleDouble(
            sum,
            error,
            alreadyNormalized:
            true);
    }

    private static void TwoSum(
        double left,
        double right,
        out double sum,
        out double error)
    {
        sum =
            left +
            right;

        double rightVirtual =
            sum -
            left;

        double leftVirtual =
            sum -
            rightVirtual;

        double rightRoundoff =
            right -
            rightVirtual;

        double leftRoundoff =
            left -
            leftVirtual;

        error =
            leftRoundoff +
            rightRoundoff;
    }
}
