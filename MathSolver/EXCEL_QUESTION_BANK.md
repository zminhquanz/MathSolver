# Soạn đề và sao lưu bằng Excel

Trong **Cài đặt → Quản lý dữ liệu**, ba thao tác có mục đích riêng:

- **Tải mẫu Excel để soạn đề**: chọn bài toán, số sao, ngôn ngữ và dạng toán. Với phép tính cơ bản, Tìm X và phân số, chọn thêm nhóm kiến thức, vai trò X nếu có và tình huống. Không cần tải model AI.
- **Nhập Excel · xem trước**: đọc file, kiểm tra từng dòng, xem đề và đáp án trước khi quyết định lưu.
- **Xuất ngân hàng đề để sao lưu**: xuất toàn bộ mẫu hợp lệ để khôi phục trên máy khác hoặc giữ bản dự phòng.

Khi tải mẫu, app dùng cùng bộ chọn CollectionView với Toán đố và Thêm đề AI: có tìm kiếm, hình minh họa cho dạng toán và bố cục tự điều chỉnh theo màn hình. Bước chọn tình huống có đề ví dụ để phân biệt các mẫu. Bấm **Đóng** hoặc quay lại để hủy lượt chọn.

## File mẫu soạn đề

| Trang | Cách dùng |
| --- | --- |
| Hướng dẫn | Đọc mục đích, tình huống, mức sao, biến và ví dụ tương ứng từng cột. |
| Nhập đề | Chỉnh lời văn, sao chép dòng để thêm mẫu, chọn **Nhập** ở cột **Xử lý dòng**. Dòng mẫu ban đầu được đặt **Bỏ qua**. |
| Ví dụ | Xem mẫu lời văn, đề hoàn chỉnh và lời giải do C# tính với các bộ số khác nhau. Trang này không được nhập. |

Giữ nguyên tên cột. Các biến như `{a}`, `{b}`, `{name}`, `{other}`, `{unit}` đại diện cho số, tên và đơn vị do app thay. Bài nhiều bước dùng tên biến theo vai trò như `{luong_da_lay}`, `{luong_con_lai}`, `{luong_moi_nhom}`, cùng cột **Dữ kiện 1…** và **Câu dẫn bước giải 1…**. Trang Hướng dẫn liệt kê chính xác biến của tình huống đã chọn và giá trị minh họa.

Bạn tự kiểm soát lời văn: có thể thêm từ nối như “và”, hỏi “cả hai”, dùng tên hoặc đơn vị cụ thể, lặp lại biến hoặc viết câu khác mẫu. Validation nhập Excel chỉ kiểm tra ô bắt buộc, dấu ngoặc/biến hợp lệ và đủ biến số của từng dữ kiện (`{a}`, `{b}` hoặc biến số theo vai trò). Không áp dụng danh sách câu đã duyệt hay kiểm tra ngôn ngữ/quan hệ dành cho AI.

Số và đáp án vẫn theo mô hình C# đã chọn. Bạn chịu trách nhiệm bảo đảm lời văn, đơn vị và đại lượng hỏi khớp với phép tính, rồi xem trước trước khi xác nhận. Muốn đổi phép tính hoặc cấu trúc toán, tải mẫu tương ứng. Nguồn mẫu người dùng được giữ khi lưu SQLite, lấy lại để luyện tập và sao lưu/khôi phục; mẫu AI tiếp tục dùng validator nghiêm ngặt.

Không phải tự nhập `FactsJson`, `StorySeedJson` hoặc cấu trúc nội bộ. File giữ cấu trúc C# trong thuộc tính tài liệu Excel. Tránh dùng công cụ xuất lại file làm mất thuộc tính này; nếu app báo mất cấu trúc, tải mẫu mới và chép lời văn vào đó.

## Xem trước và lưu

1. Chọn file `.xlsx`, tối đa 20 MB / 10.000 dòng.
2. Xem số dòng hợp lệ và có lỗi. Bấm vào dòng để đọc toàn bộ đề, lời giải hoặc lỗi.
3. Ví dụ lỗi: **Dòng 8, cột Mẫu dữ kiện thứ hai: thiếu biến bắt buộc `{b}`**. Sửa trong Excel, hủy lượt nhập đang xem và nhập lại.
4. Bấm **Nhập các dòng hợp lệ** để lưu. Các dòng có lỗi bị bỏ qua; đề trùng không được thêm lại. **Hủy nhập** giữ nguyên ngân hàng đề.

Không có dữ liệu được lưu trong bước xem trước. App lưu đúng nội dung đã kiểm tra, không mở lại một file có thể đã thay đổi. Số và đáp án vẫn được C# tính; Excel không được ghi đè chúng bằng một đáp án người dùng tự nhập.

## File sao lưu

Trang **Ngân hàng đề** hiển thị tên dạng toán, mức sao, ngôn ngữ, đề minh họa, đáp án, lời giải và nguồn. Trang **Hướng dẫn** giải thích cách khôi phục. Trang **Questions** được ẩn và giữ toàn bộ dữ liệu cần khôi phục; không sửa hoặc xóa trang này.

Chỉnh trang minh họa không làm thay đổi dữ liệu khôi phục. Muốn soạn mẫu mới, dùng file soạn đề riêng. File sao lưu cũ có một trang `Questions` vẫn được hỗ trợ và cũng đi qua bước xem trước trước khi lưu trong app.
