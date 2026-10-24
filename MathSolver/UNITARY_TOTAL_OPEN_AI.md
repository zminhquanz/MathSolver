# Rút về đơn vị: tìm tổng của số nhóm mới

Trong Thêm đề AI, chọn tỉ lệ thuận và bật diễn đạt đa dạng. Hồ sơ riêng
`unitary-total-open/pens|gifts|notebooks` chỉ mở số nhóm ban đầu, tổng lượng
ban đầu và số nhóm mới; mỗi nhóm có lượng bằng nhau và không đổi. Không mở
tỉ lệ nghịch, tìm số nhóm hoặc các bài hao hụt/nhóm không đầy.

`UnitaryTotalOpen.Contexts` là List<> song ngữ gồm hộp bút, túi quà và chồng vở,
đơn vị nhóm số ít/số nhiều, đơn vị đồ vật, dữ kiện, điều kiện và hai câu dẫn.
C# chọn A nhóm, lượng mỗi nhóm k, C nhóm khác A; cung cấp A, B=A*k, C rồi
tính k=B/A và C*k. Các số nguyên dương, chia hết và trong dung lượng bối cảnh.
Ví dụ A=4, B=24, C=7: 24÷4=6; 6×7=42. Không đưa k hoặc đáp án vào dữ kiện.

AI được viết câu mới trong schema JSON và các biến bắt buộc. GBNF ràng buộc
cấu trúc/role/step/biến/biên từ; không liệt kê toàn bộ câu. Kiểm chứng riêng
phân biệt tổng chung với lượng mỗi nhóm, kiểm tra bằng nhau và không đổi,
đích hỏi tổng của số nhóm mới. Phủ định/điều kiện/câu thêm không bị xóa để ép
khớp. Xác minh được thì cho phép lưu; chưa chứng minh thì chờ duyệt ngay;
sai rõ hoặc trùng mới thử lại tối đa ba lần. Không tự sửa câu AI trước khi lưu.

Tái sử dụng ProportionQuizContract/Direct và factory/PDF. Lời giải có hai
bước; bộ chấm riêng của hồ sơ giữ cả hai phép tính, kiểm tra dữ kiện và đơn
vị từng bước. Vẫn nhận phép tính gộp tương đương hợp lệ. Các bài Direct và
Inverse thường giữ nguyên schema và bộ chấm cũ.

```powershell
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --unitary-total-open
dotnet run --project UnitTest/QuizPdfTests -c Release -- --unitary-total-open artifacts/verification/unitary-total-open/pdf
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --unitary-total-open-model 'E:\AI Models\google\gemma-4-E4B-it-qat-q4_0-gguf\gemma-4-E4B_q4_0-it.gguf' artifacts/verification/unitary-total-open/native-final
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --open-prose-evidence artifacts/verification/unitary-total-open/native-final
```

Mỗi lượt native dùng thư mục mới và SQLite test riêng. Đọc chất lượng từng
lần thử độc lập với tỷ lệ xác minh; ghi trùng, thử lại, tốc độ và đối chiếu
nguồn không đổi khi phát lại. Báo cáo tại `artifacts/verification/unitary-total-open/REPORT.md`.

L??t AI th?t 24/10/2026: s?u y?u c?u Vi?t?Anh, s?u l?n g?i; 0/6 t? x?c minh, 6/6 ch? duy?t sau m?t l?n sinh, kh?ng th? l?i hay tr?ng. ??c c? ?? v? c?u d?n: b?n c?u g??ng, hai c?u m? h?; kh?ng coi ch? duy?t l? sai to?n. 33.21 gi?y/??, 8.46 token/gi?y. B? s? v? hai b??c C# kh?p oracle ??c l?p; ph?t l?i kh?ng ??i ngu?n. Chi ti?t v? PDF trong b?o c?o n?u tr?n.

Củng cố 24/10/2026 (V4): prompt phân biệt bằng nhau/giữ nguyên, câu dẫn hoàn chỉnh; sửa riêng tail grammar39->159 vì ví dụ đầy đủ cần44; giữ số nhóm cùng đơn vị để không thêm cấp nhóm. Không đổi List<>/schema/phép tính/ngưỡng tự lưu. Câu Việt rõ từng bị bỏ sót được nhận trong audit, nhưng lượt AI mới cuối vẫn0/6 tự xác minh,6 pending:3 gượng,2 mơ hồ,1 đổi sai vai trò. Không đưa các đề này vào luyện tập. Báo cáo: artifacts/verification/unitary-total-editorial/REPORT-v4.md.
