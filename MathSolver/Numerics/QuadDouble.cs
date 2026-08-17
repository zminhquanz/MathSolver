using System.Globalization;
using System.Text;

namespace MathSolver.Numerics;

/// <summary>
/// Số thực Quad Double được biểu diễn bằng bốn thành phần double
/// không chồng lấp, sắp xếp từ lớn đến nhỏ.
///
/// Bốn thành phần cung cấp khoảng 212 bit phần định trị, tương đương
/// xấp xỉ 63-64 chữ số thập phân có nghĩa. Các tích cơ sở dùng
/// Math.FusedMultiplyAdd để lấy chính xác phần sai số của từng tích double.
/// </summary>
public readonly struct QuadDouble :
    IComparable<QuadDouble>,
    IEquatable<QuadDouble>
{
    public const int SignificantDigits =
        64;

    private const double TwoPow32 =
        4_294_967_296d;

    public static QuadDouble Zero { get; } =
        new(0d);

    public static QuadDouble One { get; } =
        new(1d);

    public static QuadDouble NaN { get; } =
        new(
            double.NaN,
            0d,
            0d,
            0d,
            alreadyNormalized:
            true);

    /// <summary>
    /// Thành phần có độ lớn lớn nhất.
    /// </summary>
    public double Q0 { get; }

    public double Q1 { get; }

    public double Q2 { get; }

    /// <summary>
    /// Thành phần có độ lớn nhỏ nhất.
    /// </summary>
    public double Q3 { get; }

    public bool IsFinite =>
        double.IsFinite(
            Q0) &&
        double.IsFinite(
            Q1) &&
        double.IsFinite(
            Q2) &&
        double.IsFinite(
            Q3);

    public bool IsZero =>
        Q0 == 0d &&
        Q1 == 0d &&
        Q2 == 0d &&
        Q3 == 0d;

    public int Sign
    {
        get
        {
            if (Q0 > 0d)
            {
                return 1;
            }

            if (Q0 < 0d)
            {
                return -1;
            }

            if (Q1 > 0d)
            {
                return 1;
            }

            if (Q1 < 0d)
            {
                return -1;
            }

            if (Q2 > 0d)
            {
                return 1;
            }

            if (Q2 < 0d)
            {
                return -1;
            }

            if (Q3 > 0d)
            {
                return 1;
            }

            if (Q3 < 0d)
            {
                return -1;
            }

            return 0;
        }
    }

    public QuadDouble(
        double value)
    {
        Q0 =
            value;

        Q1 =
            0d;

        Q2 =
            0d;

        Q3 =
            0d;
    }

    private QuadDouble(
        double q0,
        double q1,
        double q2,
        double q3,
        bool alreadyNormalized)
    {
        Q0 =
            q0;

        Q1 =
            q1;

        Q2 =
            q2;

        Q3 =
            q3;
    }

    /// <summary>
    /// Chuyển decimal sang Quad Double mà không đi qua một phép ép kiểu
    /// decimal -> double duy nhất. Giá trị nguyên 96 bit được dựng theo
    /// từng khối 32 bit, sau đó mới chia cho 10^scale.
    /// </summary>
    public static QuadDouble FromDecimal(
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
            0xFF;

        bool isNegative =
            (bits[3] &
             int.MinValue) !=
            0;

        QuadDouble result =
            new(
                (double)high);

        result =
            FusedMultiplyAdd(
                result,
                TwoPow32,
                (double)middle);

        result =
            FusedMultiplyAdd(
                result,
                TwoPow32,
                (double)low);

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

    /// <summary>
    /// Chuyển Int128 sang QuadDouble theo từng limb 32 bit, tránh ép toàn bộ
    /// giá trị qua double và giữ chính xác toàn bộ 128 bit đầu vào.
    /// </summary>
    public static QuadDouble FromInt128(
        Int128 value)
    {
        if (value == 0)
        {
            return Zero;
        }

        bool negative = value < 0;

        // Xử lý được cả Int128.MinValue mà không làm tràn khi đổi dấu.
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

        QuadDouble result = Zero;

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

        return negative
            ? -result
            : result;
    }

    /// <summary>
    /// Tính left * right + addend và chỉ làm tròn về bốn thành phần
    /// sau khi đã gom toàn bộ tích riêng phần và addend.
    /// </summary>
    public static QuadDouble FusedMultiplyAdd(
        QuadDouble left,
        QuadDouble right,
        QuadDouble addend)
    {
        if (!left.IsFinite ||
            !right.IsFinite ||
            !addend.IsFinite)
        {
            return new QuadDouble(
                Math.FusedMultiplyAdd(
                    left.ToDouble(),
                    right.ToDouble(),
                    addend.ToDouble()));
        }

        Span<double> terms =
            stackalloc double[36];

        int termCount =
            CollectProductTerms(
                left,
                right,
                terms);

        termCount =
            AppendComponents(
                addend,
                terms,
                termCount);

        return FromTerms(
            terms[..termCount]);
    }

    public static QuadDouble Sqrt(
        QuadDouble value)
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

        QuadDouble estimate =
            new(
                Math.Sqrt(
                    value.ToDouble()));

        // Mỗi vòng Newton gần như nhân đôi số bit chính xác.
        // Bốn vòng đủ để hội tụ tới giới hạn của biểu diễn Quad Double.
        for (int iteration = 0;
             iteration < 4;
             iteration++)
        {
            QuadDouble correction =
                (value -
                 estimate *
                 estimate) /
                (2d *
                 estimate);

            estimate +=
                correction;
        }

        return estimate;
    }

    public static QuadDouble Abs(
        QuadDouble value)
    {
        return value.Sign < 0
            ? -value
            : value;
    }

    public static QuadDouble Max(
        QuadDouble left,
        QuadDouble right)
    {
        return left >= right
            ? left
            : right;
    }

    public static QuadDouble CopySign(
        QuadDouble magnitude,
        QuadDouble signSource)
    {
        QuadDouble absoluteMagnitude =
            Abs(
                magnitude);

        return signSource.Sign < 0
            ? -absoluteMagnitude
            : absoluteMagnitude;
    }

    public double ToDouble()
    {
        return
            ((Q0 +
              Q1) +
             Q2) +
            Q3;
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
                    Q0))
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

        QuadDouble absoluteValue =
            Abs(
                this);

        int exponent =
            (int)Math.Floor(
                Math.Log10(
                    absoluteValue.ToDouble()));

        QuadDouble normalized =
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

        QuadDouble remaining =
            normalized;

        for (int index = 0;
             index < digits.Length;
             index++)
        {
            int digit =
                FloorDigit(
                    remaining);

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
        QuadDouble other)
    {
        int comparison =
            Q0.CompareTo(
                other.Q0);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison =
            Q1.CompareTo(
                other.Q1);

        if (comparison != 0)
        {
            return comparison;
        }

        comparison =
            Q2.CompareTo(
                other.Q2);

        return comparison != 0
            ? comparison
            : Q3.CompareTo(
                other.Q3);
    }

    public bool Equals(
        QuadDouble other)
    {
        return Q0.Equals(
                   other.Q0) &&
               Q1.Equals(
                   other.Q1) &&
               Q2.Equals(
                   other.Q2) &&
               Q3.Equals(
                   other.Q3);
    }

    public override bool Equals(
        object? obj)
    {
        return obj is QuadDouble other &&
               Equals(
                   other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            Q0,
            Q1,
            Q2,
            Q3);
    }

    public override string ToString()
    {
        return ToGeneralString();
    }

    public static implicit operator QuadDouble(
        double value)
    {
        return new QuadDouble(
            value);
    }

    public static explicit operator double(
        QuadDouble value)
    {
        return value.ToDouble();
    }

    public static QuadDouble operator +(
        QuadDouble left,
        QuadDouble right)
    {
        if (!left.IsFinite ||
            !right.IsFinite)
        {
            return new QuadDouble(
                left.ToDouble() +
                right.ToDouble());
        }

        Span<double> terms =
            stackalloc double[8];

        int termCount =
            AppendComponents(
                left,
                terms,
                0);

        termCount =
            AppendComponents(
                right,
                terms,
                termCount);

        return FromTerms(
            terms[..termCount]);
    }

    public static QuadDouble operator -(
        QuadDouble left,
        QuadDouble right)
    {
        return left +
               -right;
    }

    public static QuadDouble operator -(
        QuadDouble value)
    {
        return new QuadDouble(
            -value.Q0,
            -value.Q1,
            -value.Q2,
            -value.Q3,
            alreadyNormalized:
            true);
    }

    public static QuadDouble operator *(
        QuadDouble left,
        QuadDouble right)
    {
        if (!left.IsFinite ||
            !right.IsFinite)
        {
            return new QuadDouble(
                left.ToDouble() *
                right.ToDouble());
        }

        Span<double> terms =
            stackalloc double[32];

        int termCount =
            CollectProductTerms(
                left,
                right,
                terms);

        return FromTerms(
            terms[..termCount]);
    }

    public static QuadDouble operator /(
        QuadDouble left,
        QuadDouble right)
    {
        if (!left.IsFinite ||
            !right.IsFinite ||
            right.IsZero)
        {
            return new QuadDouble(
                left.ToDouble() /
                right.ToDouble());
        }

        Span<double> quotientTerms =
            stackalloc double[6];

        int quotientCount =
            0;

        QuadDouble remainder =
            left;

        // Năm thành phần tạo kết quả Quad Double, thành phần thứ sáu là guard.
        for (int iteration = 0;
             iteration < quotientTerms.Length;
             iteration++)
        {
            double quotientPart =
                remainder.Q0 /
                right.Q0;

            if (quotientPart == 0d ||
                !double.IsFinite(
                    quotientPart))
            {
                break;
            }

            quotientTerms[quotientCount++] =
                quotientPart;

            remainder =
                FusedMultiplyAdd(
                    -right,
                    new QuadDouble(
                        quotientPart),
                    remainder);

            if (remainder.IsZero)
            {
                break;
            }
        }

        if (quotientCount == 0)
        {
            return Zero;
        }

        return FromTerms(
            quotientTerms[..quotientCount]);
    }

    public static bool operator <(
        QuadDouble left,
        QuadDouble right)
    {
        return left.CompareTo(
                   right) <
               0;
    }

    public static bool operator >(
        QuadDouble left,
        QuadDouble right)
    {
        return left.CompareTo(
                   right) >
               0;
    }

    public static bool operator <=(
        QuadDouble left,
        QuadDouble right)
    {
        return left.CompareTo(
                   right) <=
               0;
    }

    public static bool operator >=(
        QuadDouble left,
        QuadDouble right)
    {
        return left.CompareTo(
                   right) >=
               0;
    }

    public static bool operator ==(
        QuadDouble left,
        QuadDouble right)
    {
        return left.Equals(
            right);
    }

    public static bool operator !=(
        QuadDouble left,
        QuadDouble right)
    {
        return !left.Equals(
            right);
    }

    private static QuadDouble Pow10(
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

        QuadDouble result =
            One;

        QuadDouble factor =
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

    private static int FloorDigit(
        QuadDouble value)
    {
        int digit =
            Math.Clamp(
                (int)Math.Floor(
                    value.ToDouble()),
                0,
                9);

        while (digit > 0 &&
               value <
               (double)digit)
        {
            digit--;
        }

        while (digit < 9 &&
               value >=
               (double)(digit + 1))
        {
            digit++;
        }

        return digit;
    }

    private static int AppendComponents(
        QuadDouble value,
        Span<double> destination,
        int index)
    {
        if (value.Q3 != 0d)
        {
            destination[index++] =
                value.Q3;
        }

        if (value.Q2 != 0d)
        {
            destination[index++] =
                value.Q2;
        }

        if (value.Q1 != 0d)
        {
            destination[index++] =
                value.Q1;
        }

        if (value.Q0 != 0d ||
            index == 0)
        {
            destination[index++] =
                value.Q0;
        }

        return index;
    }

    private static int CollectProductTerms(
        QuadDouble left,
        QuadDouble right,
        Span<double> destination)
    {
        Span<double> leftComponents =
            stackalloc double[4]
            {
                left.Q0,
                left.Q1,
                left.Q2,
                left.Q3
            };

        Span<double> rightComponents =
            stackalloc double[4]
            {
                right.Q0,
                right.Q1,
                right.Q2,
                right.Q3
            };

        int index =
            0;

        for (int leftIndex = 0;
             leftIndex < 4;
             leftIndex++)
        {
            if (leftComponents[leftIndex] ==
                0d)
            {
                continue;
            }

            for (int rightIndex = 0;
                 rightIndex < 4;
                 rightIndex++)
            {
                if (rightComponents[rightIndex] ==
                    0d)
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
                    destination[index++] =
                        error;
                }

                if (product != 0d ||
                    index == 0)
                {
                    destination[index++] =
                        product;
                }
            }
        }

        return index;
    }

    private static QuadDouble FromTerms(
        ReadOnlySpan<double> sourceTerms)
    {
        if (sourceTerms.Length == 0)
        {
            return Zero;
        }

        Span<double> terms =
            stackalloc double[
                sourceTerms.Length];

        int termCount =
            0;

        foreach (double term
                 in sourceTerms)
        {
            if (term != 0d)
            {
                terms[termCount++] =
                    term;
            }
        }

        if (termCount == 0)
        {
            return Zero;
        }

        SortByMagnitude(
            terms[..termCount]);

        Span<double> expansionA =
            stackalloc double[64];

        Span<double> expansionB =
            stackalloc double[64];

        int expansionLength =
            0;

        Span<double> current =
            expansionA;

        Span<double> next =
            expansionB;

        for (int index = 0;
             index < termCount;
             index++)
        {
            int nextLength =
                GrowExpansion(
                    current[..expansionLength],
                    terms[index],
                    next);

            Span<double> temporary =
                current;

            current =
                next;

            next =
                temporary;

            expansionLength =
                nextLength;
        }

        return FromExpansion(
            current[..expansionLength]);
    }

    private static QuadDouble FromExpansion(
        ReadOnlySpan<double> expansion)
    {
        if (expansion.Length == 0)
        {
            return Zero;
        }

        Span<double> compressed =
            stackalloc double[64];

        int compressedLength =
            CompressExpansion(
                expansion,
                compressed);

        if (compressedLength == 0)
        {
            return Zero;
        }

        if (compressedLength <= 4)
        {
            return CreateFromAscending(
                compressed[..compressedLength]);
        }

        // Giữ chính xác ba thành phần lớn nhất. Toàn bộ đuôi còn lại
        // được làm tròn thành thành phần thứ tư.
        int tailLength =
            compressedLength -
            3;

        double tail =
            0d;

        for (int index = 0;
             index < tailLength;
             index++)
        {
            tail +=
                compressed[index];
        }

        Span<double> reducedTerms =
            stackalloc double[4]
            {
                tail,
                compressed[compressedLength - 3],
                compressed[compressedLength - 2],
                compressed[compressedLength - 1]
            };

        Span<double> normalizedA =
            stackalloc double[8];

        Span<double> normalizedB =
            stackalloc double[8];

        int normalizedLength =
            0;

        Span<double> current =
            normalizedA;

        Span<double> next =
            normalizedB;

        for (int index = 0;
             index < reducedTerms.Length;
             index++)
        {
            if (reducedTerms[index] ==
                0d)
            {
                continue;
            }

            int nextLength =
                GrowExpansion(
                    current[..normalizedLength],
                    reducedTerms[index],
                    next);

            Span<double> temporary =
                current;

            current =
                next;

            next =
                temporary;

            normalizedLength =
                nextLength;
        }

        if (normalizedLength == 0)
        {
            return Zero;
        }

        Span<double> finalCompressed =
            stackalloc double[8];

        int finalLength =
            CompressExpansion(
                current[..normalizedLength],
                finalCompressed);

        // reducedTerms chỉ có bốn số double nên expansion chuẩn hóa
        // không thể có nhiều hơn bốn thành phần khác 0.
        return CreateFromAscending(
            finalCompressed[..finalLength]);
    }

    private static QuadDouble CreateFromAscending(
        ReadOnlySpan<double> ascending)
    {
        double q0 =
            ascending.Length >= 1
                ? ascending[^1]
                : 0d;

        double q1 =
            ascending.Length >= 2
                ? ascending[^2]
                : 0d;

        double q2 =
            ascending.Length >= 3
                ? ascending[^3]
                : 0d;

        double q3 =
            ascending.Length >= 4
                ? ascending[^4]
                : 0d;

        return new QuadDouble(
            q0,
            q1,
            q2,
            q3,
            alreadyNormalized:
            true);
    }

    private static int GrowExpansion(
        ReadOnlySpan<double> expansion,
        double value,
        Span<double> result)
    {
        double accumulator =
            value;

        int resultLength =
            0;

        for (int index = 0;
             index < expansion.Length;
             index++)
        {
            TwoSum(
                accumulator,
                expansion[index],
                out double sum,
                out double error);

            if (error != 0d)
            {
                result[resultLength++] =
                    error;
            }

            accumulator =
                sum;
        }

        if (accumulator != 0d ||
            resultLength == 0)
        {
            result[resultLength++] =
                accumulator;
        }

        return resultLength;
    }

    private static int CompressExpansion(
        ReadOnlySpan<double> expansion,
        Span<double> result)
    {
        int length =
            expansion.Length;

        if (length == 0)
        {
            return 0;
        }

        Span<double> temporary =
            stackalloc double[64];

        double accumulator =
            expansion[length - 1];

        int bottom =
            length -
            1;

        for (int index = length - 2;
             index >= 0;
             index--)
        {
            TwoSum(
                accumulator,
                expansion[index],
                out double sum,
                out double error);

            if (error != 0d)
            {
                temporary[bottom--] =
                    sum;

                accumulator =
                    error;
            }
            else
            {
                accumulator =
                    sum;
            }
        }

        temporary[bottom] =
            accumulator;

        int resultLength =
            0;

        accumulator =
            temporary[bottom];

        for (int index = bottom + 1;
             index < length;
             index++)
        {
            TwoSum(
                temporary[index],
                accumulator,
                out double sum,
                out double error);

            if (error != 0d)
            {
                result[resultLength++] =
                    error;
            }

            accumulator =
                sum;
        }

        if (accumulator != 0d ||
            resultLength == 0)
        {
            result[resultLength++] =
                accumulator;
        }

        return resultLength;
    }

    private static void SortByMagnitude(
        Span<double> values)
    {
        for (int index = 1;
             index < values.Length;
             index++)
        {
            double current =
                values[index];

            double currentMagnitude =
                Math.Abs(
                    current);

            int insertionIndex =
                index -
                1;

            while (insertionIndex >= 0 &&
                   Math.Abs(
                       values[insertionIndex]) >
                   currentMagnitude)
            {
                values[insertionIndex + 1] =
                    values[insertionIndex];

                insertionIndex--;
            }

            values[insertionIndex + 1] =
                current;
        }
    }

    private static void TwoProduct(
        double left,
        double right,
        out double product,
        out double error)
    {
        product =
            left *
            right;

        error =
            Math.FusedMultiplyAdd(
                left,
                right,
                -product);
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
