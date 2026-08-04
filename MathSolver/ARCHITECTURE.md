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
