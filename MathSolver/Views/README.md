# Views

[Bản đồ toàn project](../FOLDER_STRUCTURE.md)

## Base

Lớp nền cho các giao diện tính toán được bản địa hóa.

- [LocalizedSolverView.cs](Base/LocalizedSolverView.cs)

## Calculators

Nhập liệu và hiển thị kết quả các phép tính, lũy thừa, phân số, phương trình và hình học.

- [AverageView.xaml](Calculators/AverageView.xaml)
- [MatrixView.cs](Calculators/MatrixView.cs): tab Ma trận tạo lười, nhập kích thước A/B riêng, sửa từng ô hoặc dán/nhập CSV/TSV, chủ động điền ô trống bằng 0. Một viewport dùng cho A/B/C, đi tới tọa độ, lời giải của ô kết quả, xuất CSV chính xác. Các partial Actions/Files/Lifetime tách thao tác, nhập/xuất và vòng đời. Khóa sửa khi đang tính, hủy khi rời tab/đổi cấu hình/đóng cửa sổ và chặn callback cũ. Tôn trọng cài đặt tăng tốc chung; có tiếng Việt/Anh và theme động. Android tắt swipe đổi tab tại Ma trận để thao tác kéo bảng không chuyển sang tab khác.
- [NumberTheoryView.xaml](Calculators/NumberTheoryView.xaml): tab Số học có hai chế độ UCLN/BCNN, hai số nguyên không âm tối đa 128 chữ số. Hiển thị cả cách phân tích thừa số nguyên tố lớp 6 và Euclid: đặt cạnh nhau khi vùng lời giải rộng từ 900 đơn vị, xếp dọc khi hẹp hơn. Cách lớp 6 có dạng tích khai triển, lũy thừa, quy tắc chọn số mũ và phép nhân kết luận; xử lý riêng số 0, số 1, hai số nguyên tố cùng nhau và phân tích số lớn chưa hoàn tất. Giữ đầu vào khi đổi chế độ; thay đầu vào sẽ ẩn kết quả cũ. Dùng tài nguyên giao diện chung, bố cục ô nhập xếp dọc trên màn hình hẹp và bản địa hóa Việt/Anh.
- [CalculationPage.xaml](Calculators/CalculationPage.xaml): chế độ Biểu thức hiển thị bản xem trước cập nhật theo từng ký tự bằng `FractionExpressionView`. Nội dung lấy từ chuỗi nhập chính xác, đồng bộ khi đổi chế độ, xóa và chuyển focus; dấu chia được hiển thị là `÷` để giữ đúng nghĩa phép chia tuần tự của tab cơ bản.
- [FindXView.xaml](Calculators/FindXView.xaml)
- [FractionView.xaml](Calculators/FractionView.xaml)
- [FractionView.Expression.cs](Calculators/FractionView.Expression.cs): chế độ Biểu thức giữ riêng dữ liệu với chế độ hai phân số; nhập ví dụ `(1/2 + 1/3) × 3/4`, xem trước cách viết, tính đáp số rút gọn và các bước theo thứ tự ưu tiên bằng `RationalExpressionEngine.Shared`. Sao chép giữ nguyên phân số chính xác, nút Xóa áp dụng cho chế độ đang chọn.
- [GeometryCalculatorView.xaml](Calculators/GeometryCalculatorView.xaml)
- [PowerRootView.xaml](Calculators/PowerRootView.xaml)
- [PowerRootView.Diagnostics.cs](Calculators/PowerRootView.Diagnostics.cs): tóm tắt kết quả lũy thừa lớn; chế độ nhà phát triển mở bảng hiệu năng và các nhóm kỹ thuật thu gọn. Stage mặc định chỉ hiện 5 mục chậm nhất; phần trăm dùng tổng của nhóm cùng loại, kể cả các mục ẩn. Log sao chép giữ đầy đủ số đo. Thời gian tính, dọn bộ nhớ và tạo nội dung TXT được phân biệt; thời gian chọn nơi lưu/sao chép file không tính vào tạo nội dung TXT.
- [QuadraticEquationView.xaml](Calculators/QuadraticEquationView.xaml)

## Formulas

Tra cứu và tính theo công thức, chuyển đổi đơn vị, bảng cửu chương.

- [AverageFormulaView.xaml](Formulas/AverageFormulaView.xaml)
- [FormulaPage.xaml](Formulas/FormulaPage.xaml)
- [MeasurementFormulaView.xaml](Formulas/MeasurementFormulaView.xaml): ô quy đổi dùng parser `decimal` giới hạn 10 chữ số thập phân, có gợi ý và lỗi Việt/Anh. Kết quả và phương trình cùng dùng định dạng tối đa 10 chữ số; hiện dấu ≈ khi đã làm tròn, còn 1 giờ → phút hiển thị chính xác 60 phút (Việt) / 60 min (Anh). Cả hai dòng dùng `FormatQuantity` để dịch đơn vị viết bằng chữ theo ngôn ngữ hiện tại (1 giờ = 60 phút / 1 h = 60 min; 24 giờ = 1 ngày / 24 h = 1 day); đổi ngôn ngữ dựng lại cả danh sách và kết quả, giữ lựa chọn đang dùng.
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

Các trang Quản lý dữ liệu, Thông tin phần cứng, Giới thiệu và Cài đặt đặt icon giữa nút lùi và tiêu đề; dùng chung SVG path với menu qua `SettingsDestinationIconPaths` và tự đổi màu theo theme. Trang xuất PDF và Đề chờ duyệt dùng `EmojiLabel` với Unicode gán trong C# (📄 và 🔎), cùng vị trí như icon 🤖 của trang thêm đề AI.

- [AboutPage.xaml](Settings/AboutPage.xaml)
- [SettingsMenuPage.xaml](Settings/SettingsMenuPage.xaml)
- [SettingsPage.xaml](Settings/SettingsPage.xaml)
- [DataManagementPage.xaml](Settings/DataManagementPage.xaml): xác nhận trước khi lưu thêm/sửa/xóa dòng, nhập Excel và xóa toàn bộ; Hủy giữ nguyên bản nháp. SQL chỉ hỏi khi SQLite xác định statement có ghi dữ liệu, trước khi thực thi; SELECT không hỏi. Popup chạy trên UI thread và các thao tác dữ liệu bị khóa trong lúc chờ trả lời.

## Startup

Màn hình khởi động.

- [SplashPage.xaml](Startup/SplashPage.xaml)
