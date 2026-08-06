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

- `CalculationThreadingManager` là nguồn trạng thái duy nhất của công tắc **Đa luồng**. Trên Windows, manager đọc topology CPU và chọn số nhân vật lý làm ngân sách worker; không lấy thẳng toàn bộ luồng SMT.
- Khi tắt đa luồng, cơ số thông thường dùng `BigInteger.Pow` trên một background worker.
- Khi bật đa luồng và kết quả ước tính có ít nhất 100.000 chữ số, `ParallelBigUnsigned` dùng lũy thừa nhị phân. Không chia số mũ thành nhiều lũy thừa con.
- `ParallelBigUnsigned` lưu magnitude theo little-endian, cơ số 10.000. Phép nhân nhỏ dùng schoolbook; phép nhân lớn dùng hai NTT chính xác, CRT song song và một lượt carry tuyến tính.
- Một nhóm worker cố định được tạo một lần cho toàn phép lũy thừa và sống xuyên suốt mọi tầng butterfly. Các tầng chỉ đồng bộ qua barrier nội bộ; không dựng lại `Parallel.For` 23 lần cho mỗi transform.
- Hai modulo được xử lý lần lượt để tránh tranh chấp cache/băng thông và khống chế peak RAM; bên trong từng modulo vẫn dùng toàn bộ ngân sách worker vật lý đã chụp.
- NTT thuận dùng decimation-in-frequency (DIF), tạo dữ liệu bit-reversed. Nhân điểm giữ nguyên thứ tự đó và NTT nghịch dùng decimation-in-time (DIT) để trả thẳng về thứ tự tự nhiên. Vì vậy không còn lượt bit-reversal riêng.
- Thông tin kết quả vẫn ghi thời gian hoán vị bit (phải bằng 0), NTT thuận/nghịch, pointwise, CRT và carry để benchmark A/B trên máy thật.
- Kết quả được chuẩn hóa sau từng phép bình phương/nhân và dùng ngay ở bước kế tiếp. Không có pha ghép kết quả con cuối cùng.
- `±2` vẫn dùng dịch bit; `±10^k` vẫn dùng biểu diễn ký hiệu và sinh thập phân trực tiếp khi xuất TXT.
- Nhánh `BigInteger.Pow` đếm chữ số và tạo mantissa bằng logarithm `decimal` 28 chữ số thay cho `double`, tránh làm tròn `(10^18 - 1)^1.000.000` thành `10^18.000.000`.
- Kết quả của engine song song đã ở cơ số thập phân 10.000, vì vậy xuất TXT theo block 4.096 chữ số mà không cần cây `BigInteger.DivRem`.
