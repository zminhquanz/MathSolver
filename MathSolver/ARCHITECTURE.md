# Kiến trúc MathSolver

## Nguyên tắc phân lớp

- `Views/`: chỉ quản lý trạng thái giao diện, điều hướng, responsive layout và trình bày lời giải.
- `Views/Base/LocalizedSolverView.cs`: lớp nền cho các tab giải toán dạng `ContentView`; quản lý vòng đời đăng ký/hủy đăng ký đổi ngôn ngữ.
- `Services/`: logic ứng dụng dùng chung như định dạng dữ liệu nhập, tính toán phân số, đa luồng, localization và style trạng thái nút.
- `Numerics/`: kiểu số và phép toán chính xác không phụ thuộc UI (`BigRational`, `QuadDouble`, `OctoDouble`).
- `Models/`: dữ liệu truyền giữa calculator/service và view.
- `Resources/Styles/SolverStyles.xaml`: hệ thống style dùng riêng cho toàn bộ tab trong **Giải toán**.

## Tạo một tab giải toán mới

1. Kế thừa `LocalizedSolverView` và gọi `InitializeLocalization()` sau `InitializeComponent()`.
2. Override `RefreshLocalizedContent()` nếu kết quả động cần dựng lại khi đổi ngôn ngữ.
3. Đặt thuật toán có thể tái sử dụng vào `Services/` hoặc `Numerics/`; không đặt trong event handler.
4. Dùng các style `SolverContentStyle`, `SolverHero*`, `SolverCardStyle`, `SolverSectionTitleStyle`, `SolverChoiceButtonStyle`, `SolverPrimaryActionStyle` và `SolverSecondaryActionStyle`.
5. Dùng `IntegerInputFormatter` cho phân nhóm hàng nghìn và giữ vị trí con trỏ; truyền `allowDecimal: true` khi ô cho phép phần thập phân.
6. Dùng `SelectionButtonStyler` để trạng thái selected/unselected đồng nhất giữa các tab.

## Ranh giới tái sử dụng

Không gom các thuật toán khác bản chất vào một “calculator chung”. Phần dùng chung nên là kiểu số, validation, formatting, threading, localization và mô hình kết quả; thuật toán riêng của từng bài toán vẫn nằm trong module tương ứng để dễ kiểm thử và thay đổi độc lập.

## Core lũy thừa đa luồng

- `CalculationThreadingManager` là nguồn trạng thái duy nhất của công tắc **Đa luồng**. Ngân sách tổng dựa trên toàn bộ bộ xử lý logic để tận dụng SMT; hai nhánh cùng chia sẻ ngân sách này và không được tự nhân đôi số worker.
- Khi tắt đa luồng, cơ số thông thường dùng bình phương–nhân `BigInteger` tuần tự trên đúng một background worker. Mỗi phép nhân lớn phát callback tiến trình và là một điểm kiểm tra hủy, nên UI dùng cùng cách báo bước với engine đa luồng.
- Khi bật đa luồng và kết quả ước tính có ít nhất 100.000 chữ số, `ParallelBigUnsigned` dùng lũy thừa nhị phân kết hợp hai nhánh `a^(m+n) = a^m × a^n`. Lũy thừa của 2 và trường hợp không có lợi vẫn dùng một chuỗi bình phương.
- `ParallelBigUnsigned` lưu magnitude theo little-endian, cơ số 10.000. Phép nhân nhỏ dùng schoolbook; phép nhân lớn dùng hai NTT chính xác, CRT song song và một lượt carry tuyến tính.
- Mỗi nhánh dùng một nhóm worker cố định xuyên suốt mọi tầng butterfly. Phép nhân ghép cuối giải phóng hai nhóm nhánh rồi dùng lại toàn bộ ngân sách logic; không dựng lại `Parallel.For` ở từng tầng.
- Hai modulo hỗ trợ NTT tới `2^26` được xử lý lần lượt để tránh tranh chấp cache/băng thông và khống chế peak RAM; bên trong từng modulo dùng đúng ngân sách worker của nhóm hiện tại.
- NTT thuận dùng decimation-in-frequency (DIF), tạo dữ liệu bit-reversed. Nhân điểm giữ nguyên thứ tự đó và NTT nghịch dùng decimation-in-time (DIT) để trả thẳng về thứ tự tự nhiên. Vì vậy không còn lượt bit-reversal riêng.
- Cache blocking NTT dùng ba tầng thích nghi theo số luồng logic: fused block L1, tile L2 và tile last-level cache (L3/LLC). Với 12T kiểu i7-8700 là 2.048 / 16.384 / 131.072 phần tử; với 24T kiểu HX 370 là 4.096 / 65.536 / 262.144 phần tử. Các stage DIF/DIT được hoàn tất cục bộ từ LLC → L2 → L1 để giảm số lần quét toàn bộ transform qua L3/RAM mà không thay đổi phép toán modulo scalar.
- Thông tin kết quả vẫn ghi thời gian hoán vị bit (phải bằng 0), NTT thuận/nghịch, pointwise, CRT và carry để benchmark A/B trên máy thật.
- Kết quả được chuẩn hóa sau từng phép bình phương/nhân và dùng ngay ở bước kế tiếp. Hai nhánh được ghép bằng một phép nhân NTT/CRT cuối cùng dùng toàn bộ worker.
- `±2` vẫn dùng dịch bit; `±10^k` vẫn dùng biểu diễn ký hiệu và sinh thập phân trực tiếp khi xuất TXT.
- Nhánh `BigInteger` đơn luồng đếm chữ số và tạo mantissa bằng logarithm `decimal` 28 chữ số thay cho `double`, tránh làm tròn `(10^18 - 1)^10.000.000` thành `10^180.000.000`.
- Vùng tiến trình tính lũy thừa luôn xuất hiện ở cả bài nhỏ và lớn. `ActivityIndicator` chạy từ lúc chuẩn bị đến khi hoàn tất, còn số phép nhân và số worker được cập nhật theo engine thực tế.
- Số mũ nhập tối đa `10.000.000`; với cơ số 18 chữ số, TXT có thể chứa xấp xỉ 180 triệu chữ số. Giao diện hiển thị dung lượng TXT ước tính và thông báo dung lượng thực sau khi lưu.
- Kết quả của engine song song đã ở cơ số thập phân 10.000, vì vậy xuất TXT theo block 4.096 chữ số mà không cần cây `BigInteger.DivRem`.

## v21 Global/RAM Adaptive 8-way experiment

- Baseline: v19 Stage Fusion.
- Adaptive 8-way scalar macro-unroll (4+4) is enabled only in cached Global/RAM Forward/Inverse stage kernels.
- L3, L2 and L1 kernels are unchanged from v19.
- Threshold by `Environment.ProcessorCount`: >=20T: halfLength 131072; 8-19T: 32768; 4-7T: 16384; 1-3T: 8192.
- Stage Fusion, persistent twiddle cache, adaptive 4-way/2-way, and Carry quotient reuse remain unchanged.
- This is intentionally an A/B benchmark experiment to isolate whether Global/RAM 8-way caused the v17 regression.



## v22 SIMD decimal formatter

- Baseline: v21 Global/RAM Adaptive 8-way + v19 Stage Fusion.
- Forward/Inverse NTT, pointwise, CRT and Carry are unchanged; production NTT/CRT remains scalar.
- After Carry, the magnitude is already normalized as independent base-10,000 limbs (`0..9999`).  This is the SIMD boundary.
- On AVX2 x64, the formatter processes 16 limbs (64 UTF-16 decimal characters) per iteration.  It packs normalized limbs to `ushort`, performs exact `/10` with unsigned high multiply by `0xCCCD` plus shift, derives thousands/hundreds/tens/ones, interleaves them, and stores four 256-bit character vectors.
- No floating-point conversion, no custom modulo-prime reduction, no `unsafe` pointer code, and no changes to NTT arithmetic.
- TXT export keeps the existing 4,096-digit progress/write blocks.  Formatting now fills those blocks in bulk instead of appending four characters per limb.
- `HardwareAccelerationSwitch` controls this formatter through `CalculationAccelerationManager.UseSimd`; unsupported CPUs fall back to the scalar formatter.
- The displayed NTT benchmark elapsed time intentionally remains computation-only because `PowerCalculationState.Elapsed` is captured before preview formatting.  v22 should therefore be benchmarked on preview/export formatting time separately from the ~49.x s NTT compute time.

## v25 eager base-10,000 preparation for bit-shift powers

- `ExportDigitThreshold` remains `100_001`: only results with more than 100,000 decimal digits expose TXT export.
- The arithmetic shortcut is unchanged: `|a| = 2^k` still computes the exact binary result using `BigInteger.One << (k*n)` on one worker.
- The important change is timing: binary `BigInteger -> base-10,000` conversion is now completed during calculation phase 3 (Formatting), before the result UI enables TXT export. Export never performs this conversion.
- When multithreading is enabled, decimal preparation may use the bounded BigInteger-import worker budget; this does not change the single-threaded bit-shift arithmetic itself.
- The prepared magnitude is stored directly in `PowerCalculationState.ParallelMagnitude`, exactly like the normalized magnitude produced by NTT/CRT. There is no lazy decimal cache and no separate bit-shift exporter.
- Therefore TXT export for bit-shift and NTT/CRT now enters the same `ParallelBigUnsigned.WriteDecimalBlocks()` pipeline immediately. The AVX2 formatter from v22 is used when hardware acceleration is enabled.
- `10^n` keeps its even faster direct `1 + zero blocks` path because no magnitude conversion is necessary.
- Formatting progress now maps phase progress from 90% to 99%, so a large bit-shift decimal preparation does not look frozen.

## v26 BitShift + parallel decimal preparation

- `|a| = 2^k` keeps the exact single-threaded `BigInteger.One << (k*n)` arithmetic shortcut.
- For results with at least `100_001` decimal digits, phase 3 no longer converts that giant binary `BigInteger` through the recursive decimal `DivRem` importer.
- Phase 3 instead prepares the reusable base-10,000 magnitude through the same `ParallelBigUnsigned.Pow(|a|, n, workers, ...)` NTT/CRT/PowSplit pipeline used by normal parallel powers.
- When multithreading is enabled, this preparation uses the configured logical worker budget; when disabled it uses one worker. The bit-shift arithmetic itself remains single-threaded.
- TXT export still enters the common `ParallelMagnitude.WriteDecimalBlocks()` path immediately and retains the optional AVX2 decimal formatter from v22.
