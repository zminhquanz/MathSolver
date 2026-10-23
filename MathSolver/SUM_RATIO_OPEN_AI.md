# Tổng–tỉ trong chế độ diễn đạt đa dạng

Hồ sơ `sum-ratio-open/` chỉ dùng tổng trực tiếp và tỉ số có hướng giữa hai lượng.
Không thay thế các bài tổng–tỉ suy ra dữ kiện ở mức sao cao của thuật toán cũ.
Hiệu–tỉ chưa được mở trong chế độ này.

`SumRatioOpen.Contexts` là danh sách có kiểu, gồm sách, bút và vở bằng tiếng Việt
và tiếng Anh. Danh sách cung cấp người sở hữu, đơn vị, giới hạn số lượng và ví dụ;
không phải danh sách đóng của những câu AI được phép viết.

| Biến | Vai trò |
|---|---|
| `{f0}` | Tổng số đồ vật của cả hai người |
| `{f1}` | Phân số bất biến do C# tạo, tử số thuộc `{v0}`, mẫu số thuộc `{v1}` |
| `{v0}` | Người có lượng bé |
| `{v1}` | Người có lượng lớn |
| `{v2}` | Cùng đơn vị đồ vật của cả hai người |

Câu tỉ số có `{v2}` hai lần để gọi đầy đủ hai lượng, ví dụ:
`Số {v2} của {v0} bằng {f1} số {v2} của {v1}.`
AI giữ nguyên số lần xuất hiện của biến trong từng trường; không tự viết dấu
chia hoặc điền tử/mẫu bên ngoài `{f1}`. GBNF cho đổi thứ tự các khối biến và
viết câu mới; C# kiểm tra ý nghĩa riêng, không xóa phủ định/điều kiện để làm câu khớp.

Bộ kiểm chứng tiếng Anh nhận thêm mệnh đề sở hữu rõ (`the number ... that ...
possesses`), quan hệ `times`, câu hỏi tách hai người có hoặc không có dấu phẩy,
và câu dẫn `one equal share`. Câu chỉ hỏi số lượng `for both` vẫn chờ duyệt.
Các ví dụ tham khảo tiếng Anh trong prompt khác nhau theo bối cảnh; không đổi
schema, dữ kiện hay ví dụ đóng của các hợp đồng đã lưu.

C# chọn các tỉ số tối giản dương, tổng chia hết cho tổng số phần và hai kết quả
nguyên. Ba câu dẫn lời giải lần lượt gọi lượng ứng với mỗi phần, lượng của
`{v0}`, lượng của `{v1}`. Sơ đồ dùng số đoạn bằng nhau theo tỉ số; mọi đoạn chưa
biết đều ghi `?`. Đáp số luôn có đúng nhãn người sở hữu.

Luồng không đổi: xác minh được thì cho phép lưu; đủ dữ kiện nhưng chưa chứng
minh được thì chờ duyệt ngay sau một lần sinh; sai rõ hoặc trùng mới thử lại.
Đề chờ không được chọn vào luyện tập hoặc PDF. Câu đúng quan hệ vẫn cần được
đọc riêng về độ tự nhiên khi đánh giá chất lượng model.

Lệnh kiểm tra:

```powershell
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --sum-ratio-open
dotnet run --project UnitTest/QuizPdfTests -c Release -- --sum-ratio-open artifacts/verification/sum-ratio-open/pdf
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --sum-ratio-open-model 'E:\AI Models\google\gemma-4-E4B-it-qat-q4_0-gguf\gemma-4-E4B_q4_0-it.gguf' artifacts/verification/sum-ratio-open/native-final
```

Lệnh model chạy sáu yêu cầu thật, ba bối cảnh × hai ngôn ngữ, temperature 0,5;
lưu prompt, JSON gốc, trạng thái, số lần thử và tốc độ vào thư mục chỉ định.
Mỗi lượt mới nên dùng thư mục mới để giữ nguyên bằng chứng cũ. Có thể phát lại
bằng `--open-prose-evidence <directory>` trên đúng mã cuối.

Lượt củng cố tiếng Anh ngày 23/10/2026 dùng thư mục
`artifacts/verification/sum-ratio-english/native-final` và pipeline
`sum-ratio-english-v4`. Xem báo cáo trong thư mục cha để phân biệt lượt sinh mới
với phát lại bằng chứng cũ trên bộ kiểm chứng mới.
