# Hiệu–tỉ: diễn đạt đa dạng bằng AI

Hồ sơ `difference-ratio-open/books|pens|notebooks` dùng quan hệ riêng
`direct-difference-oriented-ratio-find-both`. Biết hiệu của người nhiều trừ
người ít và phân số người ít/người nhiều; tìm riêng hai lượng nguyên cùng đơn vị.
Không mở bài chuyển đồ vật hoặc suy ra hiệu/tỉ số ở mức cao.

`DifferenceRatioOpen.Contexts` trong hai gói JSON cung cấp List<> chủ thể,
đồ vật, đơn vị, câu hiệu/tỉ số, câu hỏi và câu dẫn. C# chọn số phần `0<n<d`
và giá trị mỗi phần `k>0`, cho hiệu `(d-n)*k`, rồi tính `n*k`, `d*k`.
Đề chỉ cung cấp hiệu và phân số; đáp số có nhãn từng người. Sơ đồ có n/d đoạn,
không hiện kết quả. Factory, tự luận, đúng/sai, trắc nghiệm và PDF dùng cùng
hợp đồng C#. Trắc nghiệm có phương án sai một lượng và cả hai lượng; một
phương án giữ hiệu nhưng đổi tỉ số.

AI được viết câu mới, không phải chọn nguyên câu từ List<>. GBNF giữ JSON,
role/step, số lần xuất hiện biến và biên từ; không liệt kê toàn bộ câu văn.
Bộ kiểm chứng riêng yêu cầu đúng hướng hiệu, đúng chủ thể của tỉ số, đích hỏi
riêng từng người và thứ tự câu dẫn. Cấu trúc phủ định/điều kiện không được
xóa để ép khớp. Đảo hiệu/tỉ số hoặc đổi đích hỏi đã nhận diện chắc chắn bị loại.
Cách viết chưa được chứng minh giữ chờ duyệt ngay sau một lần sinh; lỗi rõ
hoặc trùng mới thử lại tối đa ba lần. Không tự đổi điều kiện lưu SQLite.

```powershell
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --difference-ratio-open
dotnet run --project UnitTest/QuizPdfTests -c Release -- --difference-ratio-open artifacts/verification/difference-ratio-open/pdf
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --difference-ratio-open-model 'E:\AI Models\google\gemma-4-E4B-it-qat-q4_0-gguf\gemma-4-E4B_q4_0-it.gguf' artifacts/verification/difference-ratio-open/native-final
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --open-prose-evidence artifacts/verification/difference-ratio-open/native-final
```

Lượt thật gồm ba bối cảnh × hai ngôn ngữ, temperature 0,5. Giữ thư mục mới
cho mỗi lượt; log mọi lần thử, đọc riêng chất lượng câu và so sánh với xác minh
tự động. Báo cáo, thời gian, tốc độ, độ trùng và phát lại đặt trong thư mục
verification ở trên. SQLite test tách khỏi ngân hàng người dùng.

Lượt củng cố ngày 24/10/2026 dùng thư mục mới
`artifacts/verification/difference-ratio-editorial/native-final`, pipeline
`difference-ratio-editorial-v2`. Hướng dẫn hỏi rõ từng người, giữ chữ “của”
trong liên kết sở hữu Việt và chủ ngữ/động từ đầy đủ trong tiếng Anh. Riêng
prompt hiệu–tỉ dùng mô tả đồ vật đếm được thay tên đồ vật/profile cụ thể để
giảm việc điền từ ngoài biến; List<>/schema và dữ kiện thực vẫn giữ nguyên.
Grammar tiếp tục mở câu chữ; câu gượng không được tự chuẩn hóa thành câu đúng.
Đối chiếu bằng chứng cũ là audit khác phiên bản, tách khỏi lượt AI sinh mới.
