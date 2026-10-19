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

- [InteractiveButtonAnimation.cs](Interaction/InteractiveButtonAnimation.cs)
- [MouseDragScrollView.cs](Interaction/MouseDragScrollView.cs)

## Wallpaper

Control hiển thị hình nền động.

- [LiveWallpaperView.cs](Wallpaper/LiveWallpaperView.cs)

## Data

Bảng kết quả SQL chỉ đọc, tiêu đề cố định, cuộn hai chiều và tái sử dụng hàng khi cuộn.

- [SqlResultTableView.cs](Data/SqlResultTableView.cs)

