# Views

[Bản đồ toàn project](../FOLDER_STRUCTURE.md)

## Base

Lớp nền cho các giao diện tính toán được bản địa hóa.

- [LocalizedSolverView.cs](Base/LocalizedSolverView.cs)

## Calculators

Nhập liệu và hiển thị kết quả các phép tính, lũy thừa, phân số, phương trình và hình học.

- [AverageView.xaml](Calculators/AverageView.xaml)
- [CalculationPage.xaml](Calculators/CalculationPage.xaml)
- [FindXView.xaml](Calculators/FindXView.xaml)
- [FractionView.xaml](Calculators/FractionView.xaml)
- [GeometryCalculatorView.xaml](Calculators/GeometryCalculatorView.xaml)
- [PowerRootView.xaml](Calculators/PowerRootView.xaml)
- [PowerRootView.Diagnostics.cs](Calculators/PowerRootView.Diagnostics.cs): tóm tắt kết quả lũy thừa lớn; chế độ nhà phát triển mở bảng hiệu năng và các nhóm kỹ thuật thu gọn. Stage mặc định chỉ hiện 5 mục chậm nhất; phần trăm dùng tổng của nhóm cùng loại, kể cả các mục ẩn. Log sao chép giữ đầy đủ số đo. Thời gian tính, dọn bộ nhớ và tạo nội dung TXT được phân biệt; thời gian chọn nơi lưu/sao chép file không tính vào tạo nội dung TXT.
- [QuadraticEquationView.xaml](Calculators/QuadraticEquationView.xaml)

## Formulas

Tra cứu và tính theo công thức, chuyển đổi đơn vị, bảng cửu chương.

- [AverageFormulaView.xaml](Formulas/AverageFormulaView.xaml)
- [FormulaPage.xaml](Formulas/FormulaPage.xaml)
- [MeasurementFormulaView.xaml](Formulas/MeasurementFormulaView.xaml)
- [MotionFormulaView.xaml](Formulas/MotionFormulaView.xaml)
- [MultiplicationTablePage.xaml](Formulas/MultiplicationTablePage.xaml)
- [ProportionFormulaView.xaml](Formulas/ProportionFormulaView.xaml)

## Hardware

Thông tin phần cứng và benchmark số học CPU.

- [HardwarePerformancePage.xaml](Hardware/HardwarePerformancePage.xaml)

## Quizzes

Giao diện đề toán và bài tập.

- [MathPuzzlePage.xaml](Quizzes/MathPuzzlePage.xaml)
- [AiQuestionBankPage.xaml](Quizzes/AiQuestionBankPage.xaml): màn hình riêng để tạo, xem và lưu đề AI; không có phần làm bài.
  [AiQuestionBankPage.Theme.cs](Quizzes/AiQuestionBankPage.Theme.cs) đồng bộ lại màu nhãn cấu hình và ô số lượng theo palette đã áp dụng khi đổi theme hoặc quay lại trang. `IllustratedQuizPicker` tự đồng bộ màu chữ lúc Loaded và ThemeChanged, hủy đăng ký lúc Unloaded; các lượt thử lại trên Windows bỏ qua callback của theme cũ.
- [AiQuestionReviewPage.cs](Quizzes/AiQuestionReviewPage.cs): đối chiếu đề AI và đề tham khảo theo hai cột khi đủ rộng, xếp dọc trên cửa sổ hẹp hoặc khi tăng cỡ chữ. Nút quay về cố định trên đầu trang, dùng cùng kiểu với màn thêm đề AI và xuất PDF. Vùng cuộn phủ hết màn hình để thanh cuộn nằm sát mép phải; các thẻ giới hạn chiều rộng để dễ đọc. Duyệt/bỏ cập nhật từng thẻ trong hàng chờ, không tải lại cả danh sách.

## Settings

Cài đặt, menu cài đặt và giới thiệu ứng dụng.

- [AboutPage.xaml](Settings/AboutPage.xaml)
- [SettingsMenuPage.xaml](Settings/SettingsMenuPage.xaml)
- [SettingsPage.xaml](Settings/SettingsPage.xaml)
- [DataManagementPage.xaml](Settings/DataManagementPage.xaml): xác nhận trước khi lưu thêm/sửa/xóa dòng, nhập Excel và xóa toàn bộ; Hủy giữ nguyên bản nháp. SQL chỉ hỏi khi SQLite xác định statement có ghi dữ liệu, trước khi thực thi; SELECT không hỏi. Popup chạy trên UI thread và các thao tác dữ liệu bị khóa trong lúc chờ trả lời.

## Startup

Màn hình khởi động.

- [SplashPage.xaml](Startup/SplashPage.xaml)
