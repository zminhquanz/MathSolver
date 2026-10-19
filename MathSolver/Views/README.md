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
- [QuizPdfPage.cs](Quizzes/QuizPdfPage.cs): tiêu đề, dòng phụ và nút lùi nằm trong `Shell.TitleView` như Cài đặt/Thông tin phần cứng. Trang ẩn tab chính và nút cài đặt, dùng chung cơ chế khôi phục khi quay lại; vùng xem trước giữ một chủ thể cuộn sát mép phải.
- [AiQuestionBankPage.xaml](Quizzes/AiQuestionBankPage.xaml): màn hình riêng để tạo, xem và lưu đề AI; không có phần làm bài. Tiêu đề, dòng phụ mô tả quy trình và nút lùi nằm cố định trong `Shell.TitleView`, dùng bố cục như Thông tin phần cứng/Cài đặt. `EmojiLabel` hiển thị biểu tượng robot bên phải nút lùi, gán bằng Unicode trong C#; ẩn tab chính và nút cài đặt của Shell, khôi phục khi quay về.
  [AiQuestionBankPage.Theme.cs](Quizzes/AiQuestionBankPage.Theme.cs) đồng bộ lại màu nhãn cấu hình và ô số lượng theo palette đã áp dụng khi đổi theme hoặc quay lại trang. `IllustratedQuizPicker` tự đồng bộ màu chữ lúc Loaded và ThemeChanged, hủy đăng ký lúc Unloaded; các lượt thử lại trên Windows bỏ qua callback của theme cũ.
- [AiQuestionReviewPage.cs](Quizzes/AiQuestionReviewPage.cs): đối chiếu đề AI và đề tham khảo theo hai cột khi đủ rộng, xếp dọc trên cửa sổ hẹp hoặc khi tăng cỡ chữ. Tiêu đề, dòng phụ và nút quay về nằm trong `Shell.TitleView`, cùng bố cục và cơ chế quay lại như màn thêm đề AI/xuất PDF; ẩn tab chính và nút cài đặt khi trang đang mở. Vùng cuộn phủ hết màn hình để thanh cuộn nằm sát mép phải; các thẻ giới hạn chiều rộng để dễ đọc. Duyệt/bỏ cập nhật từng thẻ trong hàng chờ, không tải lại cả danh sách.

## Settings

Cài đặt, menu cài đặt và giới thiệu ứng dụng.

- [AboutPage.xaml](Settings/AboutPage.xaml)
- [SettingsMenuPage.xaml](Settings/SettingsMenuPage.xaml)
- [SettingsPage.xaml](Settings/SettingsPage.xaml)
- [DataManagementPage.xaml](Settings/DataManagementPage.xaml): xác nhận trước khi lưu thêm/sửa/xóa dòng, nhập Excel và xóa toàn bộ; Hủy giữ nguyên bản nháp. SQL chỉ hỏi khi SQLite xác định statement có ghi dữ liệu, trước khi thực thi; SELECT không hỏi. Popup chạy trên UI thread và các thao tác dữ liệu bị khóa trong lúc chờ trả lời.

## Startup

Màn hình khởi động.

- [SplashPage.xaml](Startup/SplashPage.xaml)
