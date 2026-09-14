# Numerics

[Bản đồ toàn project](../FOLDER_STRUCTURE.md)

## BigIntegers

Biểu diễn số nguyên lớn, nhân/bình phương, NTT/CRT, limb nhị phân và chuyển đổi giữa các biểu diễn.

- [BinaryRadix16.cs](BigIntegers/BinaryRadix16.cs)
- [LargeBinaryUnsigned.cs](BigIntegers/LargeBinaryUnsigned.cs)
- [LimbKaratsuba.cs](BigIntegers/LimbKaratsuba.cs)
- [ParallelBigUnsigned.cs](BigIntegers/ParallelBigUnsigned.cs)
- [ParallelBigUnsigned_BinaryImport.cs](BigIntegers/ParallelBigUnsigned_BinaryImport.cs)
- [ParallelBigUnsigned_BinaryPower.cs](BigIntegers/ParallelBigUnsigned_BinaryPower.cs)

## FloatingPoint

DoubleDouble, QuadDouble, OctoDouble: số thực độ chính xác mở rộng.

- [DoubleDouble.cs](FloatingPoint/DoubleDouble.cs)
- [OctoDouble.cs](FloatingPoint/OctoDouble.cs)
- [QuadDouble.cs](FloatingPoint/QuadDouble.cs)

## Powers

Bộ điều khiển lũy thừa đơn luồng, kernel AVX2 và lũy thừa cơ số 10.

- [Avx2BigIntegerPower.cs](Powers/Avx2BigIntegerPower.cs)
- [PowerOfTenArithmetic.cs](Powers/PowerOfTenArithmetic.cs)
- [SingleThreadBigIntegerPower.cs](Powers/SingleThreadBigIntegerPower.cs)

## Serialization

Chuyển BigInteger sang dữ liệu thập phân để ghi kết quả.

- [BigIntegerDecimalWriter.cs](Serialization/BigIntegerDecimalWriter.cs)

