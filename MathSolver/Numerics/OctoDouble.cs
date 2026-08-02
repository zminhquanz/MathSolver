using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace MathSolver.Numerics;

/// <summary>
/// Số thực mở rộng gồm tám thành phần <see cref="double"/> không chồng lấp,
/// sắp xếp từ thành phần lớn nhất đến nhỏ nhất.
///
/// Tám thành phần cung cấp tối đa khoảng 424 bit phần định trị, tương đương
/// xấp xỉ 127-128 chữ số thập phân có nghĩa. Các tích cơ sở dùng
/// <see cref="Math.FusedMultiplyAdd(double, double, double)"/> để lấy phần
/// sai số chính xác của từng phép nhân double trước khi chuẩn hóa expansion.
///
/// Đây là kiểu số expansion cho tính toán thực độ chính xác cao. Kết quả
/// không được cam kết làm tròn đúng 128 chữ số trong mọi chuỗi phép toán,
/// nhưng thường giữ khoảng 125-128 chữ số có nghĩa khi dữ liệu được điều kiện tốt.
/// </summary>
public readonly struct OctoDouble :
    IComparable<OctoDouble>,
    IEquatable<OctoDouble>
{
    public const int SignificantDigits = 128;

    private const int ComponentCount = 8;
    private const int ExpansionCapacity = 256;
    private const double TwoPow32 = 4_294_967_296d;

    private const string PiText =
        "3.14159265358979323846264338327950288419716939937510582097494459230781640628620899862803482534211706798214808651328230664709384460955";

    private const string SqrtThreeText =
        "1.732050807568877293527446341505872366942805253810380628055806979451933016908800037081146186757248575675626141415406703029969945094998";

    public static OctoDouble Zero { get; } = new(0d);
    public static OctoDouble One { get; } = new(1d);
    public static OctoDouble Two { get; } = new(2d);
    public static OctoDouble Three { get; } = new(3d);

    public static OctoDouble Pi { get; } = Parse(PiText);
    public static OctoDouble SqrtThree { get; } = Parse(SqrtThreeText);

    public static OctoDouble NaN { get; } =
        new(double.NaN, 0d, 0d, 0d, 0d, 0d, 0d, 0d, alreadyNormalized: true);

    public static OctoDouble PositiveInfinity { get; } =
        new(double.PositiveInfinity);

    public static OctoDouble NegativeInfinity { get; } =
        new(double.NegativeInfinity);

    public double O0 { get; }
    public double O1 { get; }
    public double O2 { get; }
    public double O3 { get; }
    public double O4 { get; }
    public double O5 { get; }
    public double O6 { get; }
    public double O7 { get; }

    public bool IsFinite =>
        double.IsFinite(O0) &&
        double.IsFinite(O1) &&
        double.IsFinite(O2) &&
        double.IsFinite(O3) &&
        double.IsFinite(O4) &&
        double.IsFinite(O5) &&
        double.IsFinite(O6) &&
        double.IsFinite(O7);

    public bool IsNaN => double.IsNaN(O0);

    public bool IsZero =>
        O0 == 0d && O1 == 0d && O2 == 0d && O3 == 0d &&
        O4 == 0d && O5 == 0d && O6 == 0d && O7 == 0d;

    public int Sign
    {
        get
        {
            if (double.IsNaN(O0)) return 0;
            if (O0 != 0d) return Math.Sign(O0);
            if (O1 != 0d) return Math.Sign(O1);
            if (O2 != 0d) return Math.Sign(O2);
            if (O3 != 0d) return Math.Sign(O3);
            if (O4 != 0d) return Math.Sign(O4);
            if (O5 != 0d) return Math.Sign(O5);
            if (O6 != 0d) return Math.Sign(O6);
            if (O7 != 0d) return Math.Sign(O7);
            return 0;
        }
    }

    public OctoDouble(double value)
    {
        O0 = value;
        O1 = 0d;
        O2 = 0d;
        O3 = 0d;
        O4 = 0d;
        O5 = 0d;
        O6 = 0d;
        O7 = 0d;
    }

    private OctoDouble(
        double o0,
        double o1,
        double o2,
        double o3,
        double o4,
        double o5,
        double o6,
        double o7,
        bool alreadyNormalized)
    {
        O0 = o0;
        O1 = o1;
        O2 = o2;
        O3 = o3;
        O4 = o4;
        O5 = o5;
        O6 = o6;
        O7 = o7;
    }

    /// <summary>
    /// Chuyển decimal sang OctoDouble mà không gom toàn bộ giá trị vào
    /// một double duy nhất. Phần nguyên 96 bit được dựng theo từng limb 32 bit.
    /// </summary>
    public static OctoDouble FromDecimal(decimal value)
    {
        int[] bits = decimal.GetBits(value);

        uint low = unchecked((uint)bits[0]);
        uint middle = unchecked((uint)bits[1]);
        uint high = unchecked((uint)bits[2]);

        int scale = (bits[3] >> 16) & 0xFF;
        bool negative = (bits[3] & int.MinValue) != 0;

        OctoDouble result = new((double)high);
        result = FusedMultiplyAdd(result, TwoPow32, (double)middle);
        result = FusedMultiplyAdd(result, TwoPow32, (double)low);

        if (scale > 0)
        {
            result /= Pow10(scale);
        }

        return negative ? -result : result;
    }

    /// <summary>
    /// Chuyển Int128 sang OctoDouble mà không đi qua double.
    /// </summary>
    public static OctoDouble FromInt128(Int128 value)
    {
        return FromBigInteger(
            (BigInteger)value);
    }

    /// <summary>
    /// Chuyển BigInteger sang OctoDouble theo từng limb 32 bit. Cách này
    /// tránh làm mất toàn bộ các bit thấp bởi một phép ép BigInteger -> double.
    /// </summary>
    public static OctoDouble FromBigInteger(BigInteger value)
    {
        if (value.IsZero)
        {
            return Zero;
        }

        bool negative = value.Sign < 0;
        BigInteger remaining = BigInteger.Abs(value);
        var limbs = new List<uint>();
        BigInteger mask = uint.MaxValue;

        while (!remaining.IsZero)
        {
            limbs.Add((uint)(remaining & mask));
            remaining >>= 32;
        }

        OctoDouble result = Zero;

        for (int index = limbs.Count - 1; index >= 0; index--)
        {
            result = FusedMultiplyAdd(result, TwoPow32, (double)limbs[index]);
        }

        return negative ? -result : result;
    }

    public static OctoDouble FromRational(
        BigInteger numerator,
        BigInteger denominator)
    {
        if (denominator.IsZero)
        {
            return new OctoDouble((double)numerator.Sign / 0d);
        }

        return FromBigInteger(numerator) /
               FromBigInteger(denominator);
    }

    /// <summary>
    /// Phân tích số thập phân hoặc dạng khoa học bằng BigInteger trước,
    /// sau đó mới chuẩn hóa sang tám thành phần double.
    /// </summary>
    public static OctoDouble Parse(string text)
    {
        if (!TryParse(text, out OctoDouble value))
        {
            throw new FormatException($"'{text}' is not a valid OctoDouble value.");
        }

        return value;
    }

    public static bool TryParse(string? text, out OctoDouble value)
    {
        value = Zero;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string source = text.Trim();
        bool negative = false;
        int index = 0;

        if (source[0] is '+' or '-')
        {
            negative = source[0] == '-';
            index++;
        }

        if (index >= source.Length)
        {
            return false;
        }

        int lowerExponentMarker = source.IndexOf('e', index);
        int upperExponentMarker = source.IndexOf('E', index);

        int exponentMarker = lowerExponentMarker < 0
            ? upperExponentMarker
            : upperExponentMarker < 0
                ? lowerExponentMarker
                : Math.Min(lowerExponentMarker, upperExponentMarker);
        string mantissa = exponentMarker >= 0
            ? source[index..exponentMarker]
            : source[index..];

        int exponent = 0;

        if (exponentMarker >= 0)
        {
            if (!int.TryParse(
                    source[(exponentMarker + 1)..],
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out exponent))
            {
                return false;
            }
        }

        int decimalPoint = mantissa.IndexOf('.');

        if (decimalPoint != mantissa.LastIndexOf('.'))
        {
            return false;
        }

        int fractionalDigits = decimalPoint >= 0
            ? mantissa.Length - decimalPoint - 1
            : 0;

        string digitText = decimalPoint >= 0
            ? mantissa.Remove(decimalPoint, 1)
            : mantissa;

        if (digitText.Length == 0)
        {
            return false;
        }

        foreach (char character in digitText)
        {
            if (character < '0' || character > '9')
            {
                return false;
            }
        }

        if (!BigInteger.TryParse(
                digitText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out BigInteger significand))
        {
            return false;
        }

        long decimalExponent = (long)exponent - fractionalDigits;
        OctoDouble result = FromBigInteger(significand);

        if (decimalExponent != 0)
        {
            if (decimalExponent > int.MaxValue)
            {
                result = PositiveInfinity;
            }
            else if (decimalExponent < int.MinValue)
            {
                result = Zero;
            }
            else
            {
                result *= Pow10((int)decimalExponent);
            }
        }

        value = negative ? -result : result;
        return true;
    }

    /// <summary>
    /// Tính left * right + addend. Tất cả tích riêng phần và phần dư FMA
    /// được gom vào expansion trước khi làm tròn về tám thành phần.
    /// </summary>
    public static OctoDouble FusedMultiplyAdd(
        OctoDouble left,
        OctoDouble right,
        OctoDouble addend)
    {
        if (!left.IsFinite || !right.IsFinite || !addend.IsFinite)
        {
            return new OctoDouble(
                Math.FusedMultiplyAdd(
                    left.ToDouble(),
                    right.ToDouble(),
                    addend.ToDouble()));
        }

        Span<double> terms = stackalloc double[144];
        int termCount = CollectProductTerms(left, right, terms);
        termCount = AppendComponents(addend, terms, termCount);
        return FromTerms(terms[..termCount]);
    }

    public static OctoDouble Sqrt(OctoDouble value)
    {
        if (!value.IsFinite || value.Sign < 0)
        {
            return NaN;
        }

        if (value.IsZero)
        {
            return Zero;
        }

        OctoDouble estimate = new(Math.Sqrt(value.ToDouble()));

        // 53 -> 106 -> 212 -> 424 bit; thêm hai vòng guard để ổn định đuôi.
        for (int iteration = 0; iteration < 5; iteration++)
        {
            estimate +=
                (value - estimate * estimate) /
                (Two * estimate);
        }

        return estimate;
    }

    public static OctoDouble Pow(OctoDouble value, int exponent)
    {
        if (exponent == 0)
        {
            return One;
        }

        bool negativeExponent = exponent < 0;
        long remaining = negativeExponent ? -(long)exponent : exponent;
        OctoDouble result = One;
        OctoDouble factor = value;

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

        return negativeExponent ? One / result : result;
    }

    public static OctoDouble Abs(OctoDouble value) =>
        value.Sign < 0 ? -value : value;

    public static OctoDouble Max(OctoDouble left, OctoDouble right) =>
        left >= right ? left : right;

    public static OctoDouble Min(OctoDouble left, OctoDouble right) =>
        left <= right ? left : right;

    public static OctoDouble CopySign(
        OctoDouble magnitude,
        OctoDouble signSource)
    {
        OctoDouble absoluteMagnitude = Abs(magnitude);
        return signSource.Sign < 0 ? -absoluteMagnitude : absoluteMagnitude;
    }

    public double ToDouble()
    {
        return (((((((O0 + O1) + O2) + O3) + O4) + O5) + O6) + O7);
    }

    public string ToGeneralString(
        int significantDigits = SignificantDigits,
        int scientificUpperExponent = 18,
        int scientificLowerExponent = -10)
    {
        significantDigits = Math.Clamp(significantDigits, 1, SignificantDigits);

        if (!IsFinite)
        {
            if (IsNaN)
            {
                return "NaN";
            }

            return Sign < 0 ? "-Infinity" : "Infinity";
        }

        if (IsZero)
        {
            return "0";
        }

        bool negative = Sign < 0;
        OctoDouble absoluteValue = Abs(this);
        int exponent = (int)Math.Floor(Math.Log10(absoluteValue.ToDouble()));
        OctoDouble normalized = absoluteValue / Pow10(exponent);

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
        OctoDouble remaining = normalized;

        for (int digitIndex = 0; digitIndex < digits.Length; digitIndex++)
        {
            int digit = FloorDigit(remaining);
            digits[digitIndex] = (char)('0' + digit);
            remaining = (remaining - (double)digit) * 10d;
        }

        bool roundUp = digits[significantDigits] >= '5';
        int keptLength = significantDigits;

        if (roundUp)
        {
            int carryIndex = keptLength - 1;

            while (carryIndex >= 0 && digits[carryIndex] == '9')
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

                for (int resetIndex = 1; resetIndex < keptLength; resetIndex++)
                {
                    digits[resetIndex] = '0';
                }

                exponent++;
            }
        }

        while (keptLength > 1 && digits[keptLength - 1] == '0')
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

            return $"{signText}{mantissa}e{exponent.ToString(CultureInfo.InvariantCulture)}";
        }

        int decimalPointIndex = exponent + 1;
        var builder = new StringBuilder(
            signText.Length + digitText.Length + Math.Abs(decimalPointIndex) + 4);

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

    public int CompareTo(OctoDouble other)
    {
        int comparison = O0.CompareTo(other.O0);
        if (comparison != 0) return comparison;
        comparison = O1.CompareTo(other.O1);
        if (comparison != 0) return comparison;
        comparison = O2.CompareTo(other.O2);
        if (comparison != 0) return comparison;
        comparison = O3.CompareTo(other.O3);
        if (comparison != 0) return comparison;
        comparison = O4.CompareTo(other.O4);
        if (comparison != 0) return comparison;
        comparison = O5.CompareTo(other.O5);
        if (comparison != 0) return comparison;
        comparison = O6.CompareTo(other.O6);
        return comparison != 0 ? comparison : O7.CompareTo(other.O7);
    }

    public bool Equals(OctoDouble other)
    {
        return O0.Equals(other.O0) &&
               O1.Equals(other.O1) &&
               O2.Equals(other.O2) &&
               O3.Equals(other.O3) &&
               O4.Equals(other.O4) &&
               O5.Equals(other.O5) &&
               O6.Equals(other.O6) &&
               O7.Equals(other.O7);
    }

    public override bool Equals(object? obj) =>
        obj is OctoDouble other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(O0, O1, O2, O3, O4, O5, O6, O7);

    public override string ToString() => ToGeneralString();

    public static implicit operator OctoDouble(double value) => new(value);
    public static implicit operator OctoDouble(int value) => new(value);
    public static implicit operator OctoDouble(long value) => new(value);
    public static explicit operator double(OctoDouble value) => value.ToDouble();

    public static OctoDouble operator +(OctoDouble left, OctoDouble right)
    {
        if (!left.IsFinite || !right.IsFinite)
        {
            return new OctoDouble(left.ToDouble() + right.ToDouble());
        }

        Span<double> terms = stackalloc double[16];
        int termCount = AppendComponents(left, terms, 0);
        termCount = AppendComponents(right, terms, termCount);
        return FromTerms(terms[..termCount]);
    }

    public static OctoDouble operator -(OctoDouble left, OctoDouble right) =>
        left + -right;

    public static OctoDouble operator -(OctoDouble value)
    {
        return new OctoDouble(
            -value.O0,
            -value.O1,
            -value.O2,
            -value.O3,
            -value.O4,
            -value.O5,
            -value.O6,
            -value.O7,
            alreadyNormalized: true);
    }

    public static OctoDouble operator *(OctoDouble left, OctoDouble right)
    {
        if (!left.IsFinite || !right.IsFinite)
        {
            return new OctoDouble(left.ToDouble() * right.ToDouble());
        }

        Span<double> terms = stackalloc double[128];
        int termCount = CollectProductTerms(left, right, terms);
        return FromTerms(terms[..termCount]);
    }

    public static OctoDouble operator /(OctoDouble left, OctoDouble right)
    {
        if (!left.IsFinite || !right.IsFinite || right.IsZero)
        {
            return new OctoDouble(left.ToDouble() / right.ToDouble());
        }

        Span<double> quotientTerms = stackalloc double[10];
        int quotientCount = 0;
        OctoDouble remainder = left;

        // Tám thành phần kết quả, hai thành phần guard để làm tròn đuôi.
        for (int iteration = 0; iteration < quotientTerms.Length; iteration++)
        {
            double quotientPart = remainder.O0 / right.O0;

            if (quotientPart == 0d || !double.IsFinite(quotientPart))
            {
                break;
            }

            quotientTerms[quotientCount++] = quotientPart;
            remainder = FusedMultiplyAdd(-right, new OctoDouble(quotientPart), remainder);

            if (remainder.IsZero)
            {
                break;
            }
        }

        return quotientCount == 0
            ? Zero
            : FromTerms(quotientTerms[..quotientCount]);
    }

    public static bool operator <(OctoDouble left, OctoDouble right) =>
        left.CompareTo(right) < 0;

    public static bool operator >(OctoDouble left, OctoDouble right) =>
        left.CompareTo(right) > 0;

    public static bool operator <=(OctoDouble left, OctoDouble right) =>
        left.CompareTo(right) <= 0;

    public static bool operator >=(OctoDouble left, OctoDouble right) =>
        left.CompareTo(right) >= 0;

    public static bool operator ==(OctoDouble left, OctoDouble right) =>
        left.Equals(right);

    public static bool operator !=(OctoDouble left, OctoDouble right) =>
        !left.Equals(right);

    public static OctoDouble Pow10(int exponent)
    {
        if (exponent == 0)
        {
            return One;
        }

        bool negative = exponent < 0;
        long remaining = negative ? -(long)exponent : exponent;
        OctoDouble result = One;
        OctoDouble factor = 10d;

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

        return negative ? One / result : result;
    }

    private static int FloorDigit(OctoDouble value)
    {
        int digit = Math.Clamp((int)Math.Floor(value.ToDouble()), 0, 9);

        while (digit > 0 && value < (double)digit)
        {
            digit--;
        }

        while (digit < 9 && value >= (double)(digit + 1))
        {
            digit++;
        }

        return digit;
    }

    private static int AppendComponents(
        OctoDouble value,
        Span<double> destination,
        int index)
    {
        if (value.O7 != 0d) destination[index++] = value.O7;
        if (value.O6 != 0d) destination[index++] = value.O6;
        if (value.O5 != 0d) destination[index++] = value.O5;
        if (value.O4 != 0d) destination[index++] = value.O4;
        if (value.O3 != 0d) destination[index++] = value.O3;
        if (value.O2 != 0d) destination[index++] = value.O2;
        if (value.O1 != 0d) destination[index++] = value.O1;

        if (value.O0 != 0d || index == 0)
        {
            destination[index++] = value.O0;
        }

        return index;
    }

    private static int CollectProductTerms(
        OctoDouble left,
        OctoDouble right,
        Span<double> destination)
    {
        Span<double> leftComponents = stackalloc double[ComponentCount]
        {
            left.O0, left.O1, left.O2, left.O3,
            left.O4, left.O5, left.O6, left.O7
        };

        Span<double> rightComponents = stackalloc double[ComponentCount]
        {
            right.O0, right.O1, right.O2, right.O3,
            right.O4, right.O5, right.O6, right.O7
        };

        int index = 0;

        for (int leftIndex = 0; leftIndex < ComponentCount; leftIndex++)
        {
            if (leftComponents[leftIndex] == 0d)
            {
                continue;
            }

            for (int rightIndex = 0; rightIndex < ComponentCount; rightIndex++)
            {
                if (rightComponents[rightIndex] == 0d)
                {
                    continue;
                }

                TwoProduct(
                    leftComponents[leftIndex],
                    rightComponents[rightIndex],
                    out double product,
                    out double error);

                if (error != 0d)
                {
                    destination[index++] = error;
                }

                if (product != 0d || index == 0)
                {
                    destination[index++] = product;
                }
            }
        }

        return index;
    }

    private static OctoDouble FromTerms(ReadOnlySpan<double> sourceTerms)
    {
        if (sourceTerms.Length == 0)
        {
            return Zero;
        }

        Span<double> terms = stackalloc double[ExpansionCapacity];
        int termCount = 0;

        foreach (double term in sourceTerms)
        {
            if (term != 0d)
            {
                terms[termCount++] = term;
            }
        }

        if (termCount == 0)
        {
            return Zero;
        }

        SortByMagnitude(terms[..termCount]);

        Span<double> expansionA = stackalloc double[ExpansionCapacity];
        Span<double> expansionB = stackalloc double[ExpansionCapacity];
        int expansionLength = 0;
        Span<double> current = expansionA;
        Span<double> next = expansionB;

        for (int index = 0; index < termCount; index++)
        {
            int nextLength = GrowExpansion(
                current[..expansionLength],
                terms[index],
                next);

            Span<double> temporary = current;
            current = next;
            next = temporary;
            expansionLength = nextLength;
        }

        return FromExpansion(current[..expansionLength]);
    }

    private static OctoDouble FromExpansion(ReadOnlySpan<double> expansion)
    {
        if (expansion.Length == 0)
        {
            return Zero;
        }

        Span<double> compressed = stackalloc double[ExpansionCapacity];
        int compressedLength = CompressExpansion(expansion, compressed);

        if (compressedLength == 0)
        {
            return Zero;
        }

        if (compressedLength <= ComponentCount)
        {
            return CreateFromAscending(compressed[..compressedLength]);
        }

        // Giữ bảy thành phần lớn nhất; expansion đuôi được làm tròn thành
        // thành phần thứ tám trước khi chuẩn hóa lại.
        int tailLength = compressedLength - (ComponentCount - 1);
        double tail = SumExpansionToDouble(compressed[..tailLength]);

        Span<double> reducedTerms = stackalloc double[ComponentCount]
        {
            tail,
            compressed[compressedLength - 7],
            compressed[compressedLength - 6],
            compressed[compressedLength - 5],
            compressed[compressedLength - 4],
            compressed[compressedLength - 3],
            compressed[compressedLength - 2],
            compressed[compressedLength - 1]
        };

        Span<double> normalizedA = stackalloc double[16];
        Span<double> normalizedB = stackalloc double[16];
        int normalizedLength = 0;
        Span<double> current = normalizedA;
        Span<double> next = normalizedB;

        for (int index = 0; index < reducedTerms.Length; index++)
        {
            if (reducedTerms[index] == 0d)
            {
                continue;
            }

            int nextLength = GrowExpansion(
                current[..normalizedLength],
                reducedTerms[index],
                next);

            Span<double> temporary = current;
            current = next;
            next = temporary;
            normalizedLength = nextLength;
        }

        if (normalizedLength == 0)
        {
            return Zero;
        }

        Span<double> finalCompressed = stackalloc double[16];
        int finalLength = CompressExpansion(
            current[..normalizedLength],
            finalCompressed);

        // reducedTerms chỉ có tám double nên expansion chuẩn hóa
        // không thể có nhiều hơn tám thành phần khác 0.
        return CreateFromAscending(finalCompressed[..finalLength]);
    }

    private static OctoDouble CreateFromAscending(ReadOnlySpan<double> ascending)
    {
        return new OctoDouble(
            ascending.Length >= 1 ? ascending[^1] : 0d,
            ascending.Length >= 2 ? ascending[^2] : 0d,
            ascending.Length >= 3 ? ascending[^3] : 0d,
            ascending.Length >= 4 ? ascending[^4] : 0d,
            ascending.Length >= 5 ? ascending[^5] : 0d,
            ascending.Length >= 6 ? ascending[^6] : 0d,
            ascending.Length >= 7 ? ascending[^7] : 0d,
            ascending.Length >= 8 ? ascending[^8] : 0d,
            alreadyNormalized: true);
    }

    private static int GrowExpansion(
        ReadOnlySpan<double> expansion,
        double value,
        Span<double> result)
    {
        double accumulator = value;
        int resultLength = 0;

        for (int index = 0; index < expansion.Length; index++)
        {
            TwoSum(
                accumulator,
                expansion[index],
                out double sum,
                out double error);

            if (error != 0d)
            {
                result[resultLength++] = error;
            }

            accumulator = sum;
        }

        if (accumulator != 0d || resultLength == 0)
        {
            result[resultLength++] = accumulator;
        }

        return resultLength;
    }

    private static int CompressExpansion(
        ReadOnlySpan<double> expansion,
        Span<double> result)
    {
        int length = expansion.Length;

        if (length == 0)
        {
            return 0;
        }

        Span<double> temporary = stackalloc double[ExpansionCapacity];
        double accumulator = expansion[length - 1];
        int bottom = length - 1;

        for (int index = length - 2; index >= 0; index--)
        {
            TwoSum(
                accumulator,
                expansion[index],
                out double sum,
                out double error);

            if (error != 0d)
            {
                temporary[bottom--] = sum;
                accumulator = error;
            }
            else
            {
                accumulator = sum;
            }
        }

        temporary[bottom] = accumulator;
        int resultLength = 0;
        accumulator = temporary[bottom];

        for (int index = bottom + 1; index < length; index++)
        {
            TwoSum(
                temporary[index],
                accumulator,
                out double sum,
                out double error);

            if (error != 0d)
            {
                result[resultLength++] = error;
            }

            accumulator = sum;
        }

        if (accumulator != 0d || resultLength == 0)
        {
            result[resultLength++] = accumulator;
        }

        return resultLength;
    }

    private static double SumExpansionToDouble(ReadOnlySpan<double> expansion)
    {
        if (expansion.Length == 0)
        {
            return 0d;
        }

        double sum = 0d;

        // Expansion đang tăng dần theo độ lớn; cộng từ nhỏ đến lớn giảm
        // mất mát trước khi làm tròn đuôi về một double.
        for (int index = 0; index < expansion.Length; index++)
        {
            sum += expansion[index];
        }

        return sum;
    }

    private static void SortByMagnitude(Span<double> values)
    {
        for (int index = 1; index < values.Length; index++)
        {
            double current = values[index];
            double currentMagnitude = Math.Abs(current);
            int insertionIndex = index - 1;

            while (insertionIndex >= 0 &&
                   Math.Abs(values[insertionIndex]) > currentMagnitude)
            {
                values[insertionIndex + 1] = values[insertionIndex];
                insertionIndex--;
            }

            values[insertionIndex + 1] = current;
        }
    }

    private static void TwoProduct(
        double left,
        double right,
        out double product,
        out double error)
    {
        product = left * right;

        // FMA thực hiện left * right - product chỉ với một lần làm tròn,
        // vì vậy error là phần dư chính xác của tích double khi tích hữu hạn.
        error = Math.FusedMultiplyAdd(left, right, -product);
    }

    private static void TwoSum(
        double left,
        double right,
        out double sum,
        out double error)
    {
        sum = left + right;

        double rightVirtual = sum - left;
        double leftVirtual = sum - rightVirtual;
        double rightRoundoff = right - rightVirtual;
        double leftRoundoff = left - leftVirtual;

        error = leftRoundoff + rightRoundoff;
    }
}
