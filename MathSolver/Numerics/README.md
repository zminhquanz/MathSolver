# Numerics

[Bản đồ toàn project](../FOLDER_STRUCTURE.md)

## BigIntegers

Biểu diễn số nguyên lớn, nhân/bình phương, NTT/CRT, limb nhị phân và chuyển đổi giữa các biểu diễn.

- [BinaryRadix16.cs](BigIntegers/BinaryRadix16.cs)
- [LargeBinaryUnsigned.cs](BigIntegers/LargeBinaryUnsigned.cs)
- [LimbKaratsuba.cs](BigIntegers/LimbKaratsuba.cs)
- [ParallelBigUnsigned.cs](BigIntegers/ParallelBigUnsigned.cs)
- [ParallelBigUnsigned_Binary.cs](BigIntegers/ParallelBigUnsigned_Binary.cs): import limb nhị phân và lũy thừa nhị phân.
- [ParallelBigUnsigned_Schoolbook.cs](BigIntegers/ParallelBigUnsigned_Schoolbook.cs): nhân schoolbook nhỏ và tổng quát.
- [ParallelBigUnsigned_Reconstruction.cs](BigIntegers/ParallelBigUnsigned_Reconstruction.cs): CRT, chuẩn hóa carry và cộng tích phân đoạn.
- [ParallelBigUnsigned_NttKernels.cs](BigIntegers/ParallelBigUnsigned_NttKernels.cs): các stage NTT còn lại, inverse fusion, pointwise và final inverse.
- [ParallelBigUnsigned_TwiddleSimd.cs](BigIntegers/ParallelBigUnsigned_TwiddleSimd.cs): dựng bảng twiddle SIMD.
- [ParallelBigUnsigned_GlobalShoupSimd.cs](BigIntegers/ParallelBigUnsigned_GlobalShoupSimd.cs): dựng Shoup companion cho global stage.
- [ParallelBigUnsigned_Sse.cs](BigIntegers/ParallelBigUnsigned_Sse.cs): fallback SSE2/SSE4.1 cho butterfly NTT trong cache ở nhánh ≤10M; [kết quả đo](../SSE_NTT_SUB10M_NOTES.md).
- [ParallelBigUnsigned_Neon.cs](BigIntegers/ParallelBigUnsigned_Neon.cs): backend NTT/CRT ARM64 NEON cho Android đến số mũ 100M: L1/L2/L3 cache-local, global cached/uncached tail, pointwise và CRT. Dùng AdvSimd.Arm64 khi runtime hỗ trợ, hoặc Vector128 compatibility path trên Mono; [kiểm thử USB](../../tests/NeonNttAndroidValidation/README.md).


## FloatingPoint

DoubleDouble, QuadDouble, OctoDouble: số thực độ chính xác mở rộng.

- [DoubleDouble.cs](FloatingPoint/DoubleDouble.cs)
- [OctoDouble.cs](FloatingPoint/OctoDouble.cs)
- [QuadDouble.cs](FloatingPoint/QuadDouble.cs)

## Powers

Bộ điều khiển lũy thừa BigInteger đơn luồng (runtime, không có kernel SIMD riêng) và lũy thừa cơ số 10.

- [PowerOfTenArithmetic.cs](Powers/PowerOfTenArithmetic.cs)
- [SingleThreadBigIntegerPower.cs](Powers/SingleThreadBigIntegerPower.cs)

## Serialization

Chuyển BigInteger sang dữ liệu thập phân để ghi kết quả.

TXT của `ParallelBigUnsigned` dùng formatter base-10.000: Android ARM64 chọn
AdvSimd (8 limb/lượt), hoặc Vector128 (4 limb/lượt) khi Mono không cung cấp
AdvSimd. Tôn trọng công tắc SIMD; phần đuôi và runtime không hỗ trợ dùng Scalar.
Nhánh export không bị giới hạn bởi mốc số mũ ≤10M của kernel NTT.
Trên Android, NTT, parabol, export TXT và benchmark dùng chung điều kiện chạy:
Debug luôn Scalar; chỉ Release có tối ưu cùng runtime ARM64 hỗ trợ mới dùng
NEON/AdvSIMD (AdvSimd trực tiếp hoặc Vector128). Thông tin CPU vẫn hiển thị
khả năng NEON ngay cả khi cấu hình build đang chạy Scalar.

Windows/x86 xuất TXT theo chế độ SIMD đã chọn: AVX-512F (16 limb/lượt),
AVX2 (16 limb/lượt), hoặc SSE2 (8 limb/lượt, dùng chung trên SSE3/SSSE3/SSE4.1/SSE4.2).
AVX-512 không đòi hỏi BW/DQ; phần đuôi dùng kernel hẹp hơn khi có thể rồi Scalar.
Chọn SSE không gọi AVX2/AVX-512 dù CPU hỗ trợ. Các kernel mới chưa được benchmark.

- [BigIntegerDecimalWriter.cs](Serialization/BigIntegerDecimalWriter.cs)
