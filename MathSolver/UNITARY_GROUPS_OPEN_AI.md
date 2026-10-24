# Rút về đơn vị: tìm số nhóm

Trong luyện tập, Thêm đề AI và xuất PDF, chọn **Rút về đơn vị · tìm số nhóm**
trong nhóm tỉ lệ. Đây là subtype `UnitaryGroupCount` riêng (giá trị enum 2);
Direct/Inverse giữ giá trị 0/1 và các hồ sơ cũ.

`UnitaryGroupsOpen.Contexts` là List<> song ngữ cho hộp bút, túi quà và chồng
vở. C# chọn A nhóm, k đồ vật mỗi nhóm, G nhóm cần tìm; chỉ đưa A, B=A*k,
C=G*k vào đề. A và G khác nhau, các số nguyên dương, chia hết và trong
dung lượng bối cảnh. Quan hệ `source-groups-and-total-new-item-total-find-groups`
khác với quan hệ tìm tổng mới. Ví dụ 4 hộp chứa 24 cây bút; đóng 42 cây bút
theo cùng quy cách: 24÷4=6 cây bút/hộp, 42÷6=7 hộp.

Vai trò dữ kiện: f0 số nhóm ban đầu, f1 tổng đồ vật ban đầu, f2 tổng đồ vật
mới; v0 nhóm số nhiều, v1 đồ vật, v2 nhóm số ít. Điều kiện phải giữ cả hai:
các nhóm bằng nhau và lượng trong mỗi nhóm không đổi khi đóng lượng mới.
Không mở dư đồ vật, làm tròn lên, hao hụt hay nhiều cấp nhóm.

AI được thay câu chữ; GBNF giữ cấu trúc JSON, role/step và biến bắt buộc,
không liệt kê toàn bộ câu văn. Bộ kiểm chứng riêng phân biệt tổng với lượng
mỗi nhóm, lượng mới với số nhóm và đích hỏi số nhóm. Phủ định/điều kiện không
bị xóa để ép khớp. Xác minh được thì cho phép lưu; chưa chứng minh được thì
chờ duyệt ngay sau một lần sinh; sai rõ hoặc trùng mới thử lại, tối đa ba lần.
Đề chờ duyệt không được tự dùng cho luyện tập/PDF.

C# giữ hai bước, đáp số và PDF. Bộ chấm kiểm tra riêng đơn vị đồ vật/nhóm
ở bước đầu và đơn vị nhóm ở bước cuối/đáp số. Chấp nhận đơn vị đồ vật đơn
thuần ở bước đầu theo cách trình bày tiểu học, đơn vị trong ngoặc, cùng phép
tính gộp tương đương dùng đủ ba dữ kiện. Kiểm tra biểu thức gốc và mọi dấu
bằng để không bỏ qua ngoặc sai hoặc kết quả trung gian sai. Phép tính chỉ
dùng đáp án đã biết hoặc bỏ bước suy ra lượng mỗi nhóm không đủ chứng minh.

```powershell
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --unitary-groups-open
dotnet run --project UnitTest/QuizPdfTests -c Release -- --unitary-groups-open artifacts/verification/unitary-groups-open/pdf
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --unitary-groups-open-model 'E:\AI Models\google\gemma-4-E4B-it-qat-q4_0-gguf\gemma-4-E4B_q4_0-it.gguf' artifacts/verification/unitary-groups-open/native-final
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --open-prose-evidence artifacts/verification/unitary-groups-open/native-final
```

Lượt native dùng SQLite test riêng; đọc cả đề và câu dẫn nguyên gốc, tách
chất lượng câu khỏi kết quả C# xác minh. Báo cáo và PDF của lượt này tại
`artifacts/verification/unitary-groups-open/REPORT.md`.

Củng cố lời văn v2: prompt tách bằng nhau giữa các nhóm ban đầu với giữ
nguyên lượng trong từng nhóm mới, đồng thời phân biệt câu dữ kiện và câu dẫn
dạng cụm danh từ. Bộ kiểm chứng nhận thêm cách viết đầy đủ `identical count
... remains constant`, vẫn giữ nguyên phủ định/điều kiện và chặn trường hợp
chỉ nói không đổi. Grammar cho riêng hồ sơ này nhận dấu phẩy ngay sau biến
đơn vị để biểu đạt đủ hai mệnh đề; List<>, schema, toán, đơn vị và luồng
lưu/chờ duyệt/thử lại giữ nguyên. Báo cáo lượt AI mới và kiểm tra hồi quy tại
`artifacts/verification/unitary-groups-editorial/REPORT.md`.
