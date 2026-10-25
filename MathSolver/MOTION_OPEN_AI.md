# AI diễn đạt đa dạng: bốn quan hệ chuyển động

Trong Thêm đề AI, chọn **Chuyển động → Một vật chuyển động**, bật **Diễn đạt
đa dạng**. Mỗi vòng bốn yêu cầu lần lượt tìm quãng đường, vận tốc, thời gian
thực sự di chuyển và quãng đường có một lần nghỉ. Bối cảnh ô tô, xe buýt,
xe đạp chọn riêng trong từng quan hệ; số sao không thay đại lượng cần tìm.
Các nhóm đuổi kịp/gặp nhau/dòng nước dùng hồ sơ mở riêng; xem
`MOTION_EXTENDED_OPEN_AI.md` cho phạm vi và cách chọn.

Giữ nguyên enum Basic/Chasing/Meeting/River. Hồ sơ
`motion-open/{distance|speed|time|rest}/{car|bus|bicycle}` dùng các
MotionQuestionKind sẵn có và sinh C# riêng theo đúng hồ sơ. Các mẫu Basic cũ
và thuật toán cũ không có hồ sơ này vẫn chạy như trước.

List<> song ngữ `MotionOpen.Contexts` cung cấp phương tiện và miền vận tốc
hợp lý; `MotionOpen.Relations` cung cấp vai trò, mẫu dữ kiện, đích hỏi và câu
dẫn tham khảo. C# chọn km/h–giờ–km, số nguyên dương, chia hết. Ba quan hệ đầu
liên tục/không nghỉ; quan hệ thứ tư cho vận tốc khi đang di chuyển, tổng thời
gian đã gồm đúng một lần nghỉ bên trong và giữ `0 < nghỉ < tổng thời gian`.
Đơn vị tiếng Anh hours/hour được tách theo giá trị của từng khoảng thời gian.

C# giữ phép tính, đáp số và lời giải: `v*t`, `s/t`, `s/v`, `v*(tổng-nghỉ)`.
Bộ chấm riêng của hồ sơ kiểm tra đã dùng đúng dữ kiện trước khi đánh giá
biểu thức gốc/mọi dấu bằng, ngoặc và đơn vị. Nhận nhân đổi thứ tự và phép
tính gộp phân phối tương đương cho bài nghỉ; không nhận `0+đáp án` để thay
cho phép tính từ dữ kiện. Lượt đầu không đổi đơn vị, nhiều chặng, qua đêm,
hai người, nghỉ nhiều lần hoặc chia có dư.

AI được viết câu mới; GBNF giữ schema JSON, role/step/biến và văn bản có giới
hạn, không liệt kê nguyên câu. Bộ kiểm chứng phân biệt tốc độ/thời gian/
quãng đường, thời gian thực chạy/tổng thời gian, nghỉ bên trong/sau khi đến
nơi. Mẫu sai đã chứng minh được bị từ chối để thử lại; câu còn chưa chứng
minh được hoặc thiếu dấu câu chờ duyệt ngay; không xóa phủ định để ép khớp.
Đề pending không tự đi vào luyện tập/PDF. Không thay luồng lưu/chờ duyệt/thử lại.

```powershell
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --motion-open
dotnet run --project UnitTest/QuizPdfTests -c Release -- --motion-open artifacts/verification/motion-open/pdf
$motionRun = 'artifacts/verification/motion-open/model-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --motion-open-model 'E:\AI Models\google\gemma-4-E4B-it-qat-q4_0-gguf\gemma-4-E4B_q4_0-it.gguf' $motionRun
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --open-prose-evidence $motionRun
```

AI test dùng 16 yêu cầu: bốn quan hệ × ô tô/xe buýt × Việt/Anh; kiểm tra C#
bao phủ thêm xe đạp và cả năm mức sao. Mỗi lượt native phải dùng thư mục mới,
đọc cả câu hỏi và câu dẫn nguyên gốc, ghi rõ câu đúng/rõ, gượng, mơ hồ hoặc
sai quan hệ bên cạnh tỷ lệ tự xác minh, độ trùng/thử lại và tốc độ. Báo cáo:
`artifacts/verification/motion-open/REPORT.md`.

Lượt cuối v2 ngày24/10/2026 hoàn tất16 yêu cầu/16 lần gọi thật:0 tự xác minh,
16 chờ duyệt sau một lần sinh. Đọc riêng4 câu rõ,6 câu đúng quan hệ nhưng
gượng,3 câu mơ hồ,3 câu sai vai trò. Chưa tự đưa các đầu ra này vào luyện
tập. V2 giữ cụm danh từ phương tiện và cụm đơn vị ở câu hỏi để giảm nhầm
vai trò; phần vị ngữ vẫn tự sinh. Không đổi bộ kiểm chứng để nhận câu sai.

Củng cố ngày 25/10/2026: nhận thêm bốn câu rõ bị bỏ sót trong lượt v2,
kiểm tra toàn mệnh đề với phủ định/điều kiện, giữ rõ vận tốc không đổi và
nghỉ nằm trong tổng thời gian. Các câu chỉ nêu đi liên tục chưa chứng minh
được chuyển động đều vẫn chờ duyệt. Trường hợp chắc chắn dùng thời lượng
làm phương tiện bị từ chối để thử lại; câu chưa hiểu rõ vẫn chờ duyệt ngay.

Prompt phân biệt phương tiện với số đo và dùng it/the vehicle trong tiếng
Anh. Grammar gắn số đo thời lượng với cụm đo lường (is/for/là/trong/kéo dài),
không liệt kê nguyên câu; tiêu đề Actual moving duration/Actual travel duration
được bộ chấm nhận cho đúng hồ sơ tìm thời gian. Phép tính, dữ kiện, List<>,
schema và điều kiện lưu/chờ duyệt/thử lại giữ nguyên. Kết quả phát lại v2
và lượt AI mới được ghi riêng tại
`artifacts/verification/motion-open-editorial/REPORT.md`.

Lượt native v3 bằng mã cuối hoàn tất 16 yêu cầu/16 lần gọi: 0 tự xác minh,
16 chờ duyệt, 0 thử lại/trùng; đọc riêng 8 rõ, 6 gượng, 2 mơ hồ, không gặp lại
lỗi dùng thời lượng làm xe hay gọi xe là traveller trong mẫu này. Tám câu rõ
mới vẫn chưa được C# chứng minh: bộ kiểm chứng còn hẹp, chưa đủ chất lượng
để tự đưa các đầu ra đó vào luyện tập. Không sửa matcher theo lượt v3.
