# Lời văn AI cho thời gian trong cùng ngày

Đợt này mở cách diễn đạt của ba quan hệ: tìm thời gian trôi qua (`elapsed`),
tìm giờ bắt đầu (`start`) và tìm giờ kết thúc (`end`). Chưa mở hoạt động có nghỉ,
qua nửa đêm hoặc lịch. Phân số của một đại lượng là bước kế tiếp, sau khi đánh
giá chất lượng thời gian; không tự bật các nhóm còn lại trong lộ trình.

`TimeOpen.Contexts` và `TimeOpen.Relations` trong hai gói QuizContent cung cấp
dữ kiện, quan hệ và ví dụ. Có năm bối cảnh: đọc sách, tập thể thao, làm thủ công,
đi xe và sinh hoạt câu lạc bộ. Mỗi bối cảnh có giờ sớm nhất, giờ muộn nhất và
thời lượng tối đa. `SameDayTimePilot` sinh số liệu mới; bắt đầu luôn trước kết
thúc, trong cùng ngày. C# tính đáp án và lời giải, AI chỉ viết câu văn.

Bật **Viết đa dạng (thử nghiệm)** ở thêm đề AI để dùng chế độ này. Ba dạng đã
có trong toán đố và cấu hình xuất PDF. Phiếu PDF dùng bộ câu đang xem, không
tự lấy lại số liệu khi xuất.

## Ràng buộc và kiểm chứng

- Prompt giải thích riêng vai trò từng mốc giờ, thời lượng và câu hỏi đích;
  `{v0}` là người, `{v1}` là cụm danh từ chỉ toàn bộ hoạt động.
- GBNF giữ JSON, role/step, biến bắt buộc và các cụm người–hoạt động,
  giờ–phút. Grammar không liệt kê toàn bộ câu; AI vẫn có thể mở bằng mốc giờ
  hoặc hoạt động và thay cách diễn đạt. Hai cấu trúc mệnh đề giữ mốc giờ gắn
  với hoạt động của đúng người; mỗi trường chỉ có một câu, dấu kết câu ở cuối.
- `SameDayTimeMeaning` kiểm tra toàn bộ từng trường, giữ vị trí biến theo
  vai trò. Không dùng việc có đủ biến hoặc có một động từ đúng làm bằng chứng.
  Mỗi dữ kiện, điều kiện liên tục cùng ngày, câu hỏi và câu dẫn lời giải đều
  phải chứng minh được.
- Các cách nói được kiểm chứng có giới hạn. Câu khác chưa hiểu được vào hàng
  chờ duyệt, không tự dùng trong luyện tập hoặc PDF. Đây không phải bộ hiểu
  ngôn ngữ tổng quát. JSON đúng và phép tính C# đúng chưa chứng minh câu văn đúng.
- Trùng lời văn vẫn được kiểm tra riêng. Đổi số, tên, dấu câu hoặc riêng câu
  dẫn lời giải không tạo một đề mới.

## Kiểm thử

```powershell
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --time-open
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --time-open-model '<model.gguf>' '<thư mục evidence mới>'
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --open-prose-evidence '<thư mục evidence>'
dotnet run --project UnitTest/QuizPdfTests -c Release -- --artifacts '<thư mục PDF>'
```

Offline kiểm tra 150 cấu hình (5 bối cảnh × 3 quan hệ × 5 mức × 2 ngôn ngữ),
8 bộ số mới mỗi cấu hình, ba chế độ trả lời, chấm kết quả trực tiếp và lời giải,
đổi vai trò/sai điều kiện, chống trùng, worker, hàng chờ duyệt và sao lưu Excel.
PDF kiểm tra cả năm bối cảnh với lời văn mới đã xác minh.

Lượt native gồm 10 yêu cầu song ngữ, đủ ba quan hệ và năm bối cảnh, temperature
0.5, tối đa ba lần sinh/yêu cầu. Evidence giữ prompt, JSON thô, trạng thái từng
lần thử, thời gian và tốc độ. Ngân hàng thử là SQLite riêng; không ghi vào dữ
liệu cài đặt của người dùng. Đọc nội dung từng câu và báo cáo riêng tỷ lệ xác
minh, cần duyệt, trùng và thử lại; không suy ra độ chính xác chung từ mẫu nhỏ.
