# Chạy và bảo trì bộ kiểm thử

Ba chương trình console chạy phần C# thật của ứng dụng. Chúng không khởi động MAUI.

| Dự án | Trách nhiệm |
| --- | --- |
| `TestSupport` | Assertion, mã thoát runner, thư mục tạm và kiểm tra chính harness |
| `TestHost` | Biên dịch một lần models, thuật toán, generator, JSON và ngân hàng SQLite dùng chung |
| `MathPuzzleUnitTests` | Đáp án độc lập, chấm bài, dữ kiện, đơn vị, nội dung và bộ nhớ |
| `AiQuestionBankTests` | Prompt, validator, chống trùng, SQLite/Excel, worker và model thật |
| `QuizPdfTests` | Nội dung đề/in, font nhúng, vector, A4, phân trang và snapshot bộ đề |

`TestHost` có stub cho ngôn ngữ và cấu hình tăng tốc của ứng dụng. Các test này
không chứng minh giao diện trên thiết bị hoặc mọi nhánh SIMD hoạt động đúng.
LLamaSharp và backend CPU chỉ thuộc dự án AI; PDF không tham chiếu chương trình AI.

Nhóm `NTT kernel execution` bật từng chế độ Scalar/SSE/AVX2/AVX-512 trong stub, chạy kernel Numerics thật và đối chiếu với BigInteger. AVX-512 chỉ chạy khi runtime hỗ trợ; máy thiếu ISA sẽ ghi SKIP. Stub mặc định vẫn tắt AVX-512 cho các nhóm khác. Nhóm này kiểm tra cả telemetry theo công đoạn, gộp nhánh, tính phân đoạn/persistent, job đồng thời và dọn worker sau hủy; không thay cho benchmark tải 100M hay kiểm thử giao diện.

Nhóm `Function graph SIMD` chạy evaluator thật dùng chung cho đường thẳng/parabol với SIMD tắt, AVX bị cấm và backend tốt nhất của runtime. Kiểm tra làm tròn FMA, phần đuôi mẫu parabol, hệ số Int128 lớn, triệt tiêu gần nghiệm và fallback QuadDouble theo sai số pixel. Không khởi động MAUI hoặc kiểm tra raster trên thiết bị.

## Lệnh thường dùng

```powershell
dotnet run --project UnitTest/MathPuzzleUnitTests -c Release
dotnet run --project UnitTest/AiQuestionBankTests -c Release
dotnet run --project UnitTest/QuizPdfTests -c Release
```

Không có tham số: chạy suite đầy đủ offline. `--list`/`--help` hiển thị các lệnh.
MathPuzzle vẫn nhận bộ lọc tên nhóm, ví dụ `"Numeric measurement"`.
AI vẫn giữ các tên lệnh và alias cũ; nhập sai lệnh hoặc thiếu tham số trả mã 2,
không tự chạy toàn bộ suite. Test lỗi trả mã 1; thành công trả mã 0.

```powershell
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --model 'C:\models\model.gguf'
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --harness
dotnet run --project UnitTest/QuizPdfTests -c Release -- --artifacts
dotnet run --project UnitTest/QuizPdfTests -c Release -- --artifacts 'artifacts/verification/pdf-review'
```

Model thật chỉ chạy qua lệnh tường minh và file GGUF có sẵn. Các bài model nằm
trong `AiQuestionBankTests/Model`; `ModelTestSession` sở hữu runtime, giới hạn
thời gian nạp và giải phóng native weights cả khi nạp thất bại. Giữ các giới hạn
generation, evidence, checkpoint và resume riêng của từng bài test.

PDF mặc định viết vào thư mục tạm riêng rồi dọn sau khi chạy. `--artifacts` giữ
file trong thư mục con có GUID; đường dẫn output kiểu cũ vẫn được hỗ trợ và cũng
tạo thư mục con riêng. Lệnh model giữ evidence như trước; không dọn thư mục
checkpoint được truyền vào. Các fixture SQL offline dùng `TestWorkspace.Temporary`;
helper chỉ dọn thư mục mới do chính lượt chạy tạo, không dọn thư mục của người dùng.

`AiQuestionBankTests --sql-grid` kiểm tra thêm bước xác nhận SQL: từ chối INSERT/UPDATE/DELETE không làm đổi database, chấp nhận chỉ thực thi một lần, SELECT không hỏi, CTE/RETURNING được phân loại bởi SQLite, hủy khi đang đợi không ghi dữ liệu và thời gian đợi popup không tính vào timeout SQL. Các test chạy trong database tạm, không mở dữ liệu người dùng.

`AiQuestionBankTests --bank-data` chạy riêng các kiểm tra SQL/Excel, timeout, giới hạn dữ liệu, rollback câu lệnh ghi và xóa toàn bộ, không nạp model.

`AiQuestionBankTests --generation-lifecycle` kiểm tra Stop ở các checkpoint bằng runtime trả về bình thường, theo dõi exception first-chance từ worker, giữ phần nội dung cuối và preview đã xong, không lưu phần bị dừng và giải phóng model một lần. Lệnh còn chạy các test streaming, retry, lỗi runtime và vòng đời bộ nhớ hiện có; không thực hiện inference GGUF.

## Thêm hoặc sửa test

- Dùng `TestAssert.That`, `Throws`/`ThrowsAsync`. Assertion có kiểu lỗi riêng,
  để một `catch (InvalidOperationException)` không nuốt lỗi kiểm tra.
- Runner chỉ phân phối lệnh; đưa dữ liệu mẫu/fake vào `Fixtures` và test model
  vào `Model`. Các fake có hành vi khác nhau nên có tên riêng.
- Giữ công thức kiểm chứng độc lập với thuật toán đang được test. Có thể dùng
  `PracticeGrading` để gom bước parse/chấm, nhưng vẫn giữ các mẫu sai đáp án,
  sai đơn vị, sai quan hệ và lỗi đã gặp ở từng dạng.
- Tính toán nhiều seed một lần ở chế độ tự luận; các mode trình bày dùng seed
  đại diện khi dữ kiện toán không đổi. Đổi đơn vị giữ 40 seed cho tự luận và 4
  seed/mode còn lại: 2.400 trường hợp, đủ hai ngôn ngữ, năm mức và năm đại lượng.
  `LearningProfileTests` vẫn kiểm tra đủ 12 seed và mọi cách chấm; grammar và
  các mẫu sai lời văn chỉ chạy một lần cho mỗi tổ hợp lời văn, bối cảnh, quan hệ,
  mức sao, ngôn ngữ và đơn vị khác nhau.
- Test PDF kiểm tra cấu trúc font, ToUnicode, lệnh vẽ chữ và nội dung cần có;
  tránh dùng kích thước byte của file làm bằng chứng nội dung đúng.

Chi tiết từng suite: [thuật toán](MathPuzzleUnitTests/README.md),
[ngân hàng và model](AiQuestionBankTests/README.md).
