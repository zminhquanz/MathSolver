# Controls

[Bản đồ toàn project](../FOLDER_STRUCTURE.md)

## Expressions

Hiển thị phân số và căn thức theo kiểu sách giáo khoa.

- [FractionExpressionView.cs](Expressions/FractionExpressionView.cs): các phân số dạng token và cây biểu thức dùng cùng bố cục cột Auto, đo theo font/nội dung thật thay vì số ký tự. Nhãn có đệm ngang để tránh cắt nét chữ cuối; các phần tử toán học không co hẹp trong FlexLayout khi xuống dòng.
- [TextbookRadicalExpressionView.cs](Expressions/TextbookRadicalExpressionView.cs)
- [TextbookRadicalStrokeView.cs](Expressions/TextbookRadicalStrokeView.cs)

## Icons

Icon vector theo theme và icon sao chép.

- [ThemedCopyIcon.cs](Icons/ThemedCopyIcon.cs)
- [ThemedSvgPathIcon.cs](Icons/ThemedSvgPathIcon.cs)

## Interaction

Hiệu ứng nút và cuộn bằng thao tác kéo chuột.

- [InteractiveButtonAnimation.cs](Interaction/InteractiveButtonAnimation.cs): theo dõi Loaded/Unloaded và thay handler, kiểm tra vòng đời trước/sau mỗi pha nhấn, nhả và ổn định. Khi đóng cửa sổ, `App` gọi `StopForWindow` trước khi hủy dịch vụ MAUI; hủy hiệu ứng sau khi tăng phiên bản để các continuation không chạy tiếp. Cửa sổ khác và nút được tải lại vẫn có hiệu ứng bình thường. Kiểm tra hồi quy dùng source thực tại [InteractionAnimationTests](../../UnitTest/InteractionAnimationTests/README.md).
- [MouseDragScrollView.cs](Interaction/MouseDragScrollView.cs): chỉ tự dàn lại nội dung khi `IsTabLayoutEnabled` được bật rõ ràng ở thanh tab Giải toán/Công thức; bảng SQL giữ nguyên ba hàng tiêu đề/nháp/kết quả. Thanh tab chỉ đo các view đã Loaded, còn handler và thuộc cửa sổ chưa đóng. Kiểm tra lại vòng đời trước mỗi phép đo và trước khi áp dụng kích thước; callback Loaded bị vô hiệu khi unload/thay handler. Trang Giải toán và Công thức cũng kiểm tra trước lệnh cuộn đã xếp hàng. Xem [kiểm thử vòng đời và tải lại SQL](../../UnitTest/TabLayoutLifecycleTests/README.md).

## Wallpaper

Control hiển thị hình nền động.

- [LiveWallpaperView.cs](Wallpaper/LiveWallpaperView.cs)

## Data

Bảng kết quả SQL chỉ đọc, tiêu đề cố định, cuộn hai chiều và tái sử dụng hàng khi cuộn.

- [SqlResultTableView.cs](Data/SqlResultTableView.cs)

## Matrices

- [MatrixInputField.cs](Matrices/MatrixInputField.cs): cả ô kích thước, tọa độ và giá trị dùng Entry trong suốt, với Border bên ngoài nhận màu nền/viền động. Không gắn palette vào Entry.BackgroundColor để tránh gọi lại WinUI TextControlBackground resource mapping trong lúc đổi theme. Text/placeholder vẫn theo theme, giữ nguyên trạng thái và giá trị.

- [MatrixViewport.cs](Matrices/MatrixViewport.cs): chỉ vẽ vùng hàng/cột đang nhìn thấy và tái sử dụng một Entry ngay trên ô được chọn. Cuộn mượt bằng kéo/vuốt, chuột hoặc bàn rê; Shift + cuộn chuột để cuộn ngang trên Windows. Enter/Tab/mũi tên chuyển ô, Escape khôi phục giá trị đã lưu. Không còn hai slider hoặc ô nhập riêng bên dưới. [MatrixViewportState.cs](Matrices/MatrixViewportState.cs) giữ hình học, giới hạn cuộn và xác nhận sửa trước khi đổi ô; partial Input tách xử lý nền tảng. Không tạo control hoặc bitmap theo toàn bộ 1024×1024 phần tử.

