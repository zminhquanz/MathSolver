# AI diễn đạt đa dạng: gặp nhau, đuổi kịp và chuyển động trên sông

Trong Thêm đề AI, chọn Chuyển động rồi bật Diễn đạt đa dạng cho Gặp nhau,
Đuổi kịp hoặc nhóm Xuôi/ngược dòng. Nhóm sông luân phiên bốn đích hỏi:
xuôi dòng → ngược dòng → vận tốc phương tiện khi nước yên → vận tốc dòng nước.
Giữ nguyên enum và các đề cũ; mỗi đích hỏi có hồ sơ riêng
`motion-extended/{meeting|chasing|downstream|upstream|boat|current}/{context}`.

Hai bối cảnh đường bộ: người đi xe đạp, ô tô. Hai bối cảnh sông: ca nô,
thuyền máy. List<> song ngữ giữ chủ thể, vai trò và ví dụ, không đóng cứng
toàn bộ câu AI được phép viết. C# sinh số, phép tính, đáp án và PDF.

| Quan hệ | Dữ kiện và phạm vi | Phép tính C# |
|---|---|---|
| Gặp nhau | Cùng lúc, đi về phía nhau, đều và không nghỉ | khoảng cách ÷ tổng vận tốc |
| Đuổi kịp | Cùng lúc, cùng chiều, bên chậm ở trước | khoảng cách ÷ hiệu vận tốc |
| Xuôi dòng | Biết vận tốc khi nước yên và dòng nước | cộng hai vận tốc |
| Ngược dòng | Vận tốc khi nước yên lớn hơn dòng nước | trừ vận tốc dòng nước |
| Khi nước yên | Biết vận tốc xuôi/ngược của cùng phương tiện, đo so với bờ | tổng hai vận tốc ÷ hai |
| Dòng nước | Cùng dữ kiện quan sát, đích hỏi riêng | hiệu hai vận tốc ÷ hai |

Đợt đầu dùng km/h–giờ–km, số nguyên dương và chia hết; thời gian đường bộ
2–5 giờ. Không mở xuất phát trễ, nghỉ, nhiều chặng, đổi đơn vị hoặc vận tốc
thay đổi. Hai vận tốc đường bộ gắn đúng thứ tự chủ thể; hồ sơ này giữ bên
thứ nhất chậm hơn bên thứ hai. Metadata chủ thể cũng được kiểm tra độc lập.

GBNF giữ JSON, vai trò, biến và cặp số/đơn vị, cho phép đổi thứ tự khối và
tự viết phần câu xung quanh. C# chứng minh toàn mệnh đề: hướng đi/thứ tự,
thời điểm xuất phát, điều kiện vận tốc không đổi, hệ quy chiếu và đích hỏi.
Xác minh được thì cho phép lưu; chưa chứng minh thì chờ duyệt ngay; sai rõ
hoặc trùng mới thử lại. Pending không được tự dùng cho luyện tập hay PDF.

Bộ chấm tự luận của các hồ sơ này kiểm tra đúng đại lượng/hệ quy chiếu trong
câu dẫn trước quy tắc chung, cùng dữ kiện gốc của phép tính và mọi dấu bằng.
Không chấp nhận kết quả số đúng nhưng tiêu đề sai hoặc chỉ ghi phép tính
không dùng dữ kiện của đề. Các nhóm chuyển động cũ giữ quy tắc hiện có.

```powershell
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --motion-extended
dotnet run --project UnitTest/QuizPdfTests -c Release -- --motion-extended-open artifacts/verification/motion-extended-open/pdf
$motionRun = 'artifacts/verification/motion-extended-open/model-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --motion-extended-model 'E:\AI Models\google\gemma-4-E4B-it-qat-q4_0-gguf\gemma-4-E4B_q4_0-it.gguf' $motionRun
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --open-prose-evidence $motionRun
```

AI test: sáu quan hệ × Việt/Anh, bối cảnh người đi xe đạp/ca nô, mức ba sao.
C# kiểm tra thêm hai bối cảnh mỗi quan hệ và cả năm mức sao. Đọc riêng cả
dữ kiện, câu hỏi và câu dẫn; không dùng tỷ lệ xác minh thay cho chất lượng câu.
Báo cáo: `artifacts/verification/motion-extended-open/REPORT.md`.

Lượt GGUF thật ngày 25/10/2026: 12 yêu cầu, 12 lần gọi, 0 tự xác minh,
12 chờ duyệt, không thử lại. Đọc riêng có 3 câu rõ, 2 câu gượng, 6 câu mơ hồ
và 1 câu đổi vai trò chủ thể; không đồng nghĩa cả 12 câu đều sai. Ba câu rõ
vẫn cần mở rộng khả năng nhận diện trước khi tự lưu. SQLite test có 0 đề
active và 12 pending; phát lại không đổi kết luận, 862 hash nguồn/binary
không đổi. Cần củng cố câu hỏi đặt sai vị trí đơn vị, điều kiện và cách viết;
chưa nới điều kiện tự lưu. Xem báo cáo để biết giới hạn mẫu và tải nền.
