# Chuẩn bị bài đo lường diện tích trước khi nối AI

Nhánh `AreaConversion` trong luyện tập dùng C# và JSON Việt/Anh. Chưa thêm diện
tích vào `MeasurementBankTypes`, picker tạo đề AI hoặc pipeline lưu của
`BasicQuestionBank`. Phần này chuẩn bị contract, lời văn đã duyệt và kiểm tra
tái tạo để có thể nối AI ở bước sau.

## Phạm vi và mức sao

| Sao | Dữ kiện và yêu cầu |
| --- | --- |
| 1 | Diện tích ở đơn vị lớn → đơn vị nhỏ |
| 2 | Diện tích ở đơn vị nhỏ → đơn vị lớn; giữ kết quả thập phân chính xác |
| 3 | Diện tích ban đầu gồm hai đơn vị, thêm diện tích rồi cộng |
| 4 | Như mức 3, sau đó lấy bớt diện tích hợp lệ |
| 5 | Như mức 4, đổi diện tích còn lại về đơn vị lớn |

C# sở hữu dữ kiện, hệ số đổi đơn vị, đáp án và từng phép tính. `RequiresSolution`
giữ cách nhập kết quả trực tiếp của nhánh đo lường; lời giải đầy đủ vẫn được tạo
và kiểm tra độc lập. Không chuyển bài đổi diện tích thành bài tìm chiều dài,
chu vi hoặc diện tích xung quanh.

## Thêm bối cảnh JSON

Sửa `Resources/Raw/QuizContent/vi-VN.json` và `en-US.json`, trong list
`AreaMeasurementContexts`. Ví dụ:

```json
{
  "Id": "area-fabric",
  "Subject": "vải",
  "LargeUnitId": "m2",
  "SmallUnitId": "dm2",
  "MaximumQuantity": 50
}
```

Giữ cùng `Id`, hai mã đơn vị và giới hạn trong các ngôn ngữ; chỉ dịch `Subject`.
Mã đã sử dụng không được đổi sang đối tượng hoặc cặp đơn vị khác: tạo mã mới.
`MaximumQuantity` giới hạn tổng diện tích theo đơn vị lớn; tối thiểu là 4.
Dùng vật liệu hoặc các phần đất có thể cộng/bớt diện tích: giấy thủ công, vải,
bạt, vật liệu lát sàn, các mảnh đất trồng cây, đất canh tác. Không viết rằng thêm
vải làm chiều dài hoặc chu vi tăng theo cùng một lượng diện tích.

Hai đơn vị phải thuộc nhóm `area`, và đơn vị lớn phải lớn hơn đơn vị nhỏ. Ví dụ
`m2 → dm2` có hệ số 100, `m2 → cm2` có hệ số 10.000, `ha → m2` có hệ số 10.000.
Không dùng `m`, `cm`, `l` hoặc `m3` cho contract diện tích.

## Viết lời văn và phương án tương đương

Các entry `AreaMeasurementStories.*` nằm trong `Texts`; phương án đã duyệt nằm
trong `Narrative.AreaMeasurement.Phrasings` với `Id` trỏ đến entry tương ứng.
Giữ `Variables` và giữ đủ số lần xuất hiện của từng biến ở mỗi phương án.

| Biến | Vai trò |
| --- | --- |
| `{subject}` | Vật liệu/bối cảnh từ JSON |
| `{initial_large}` | Phần diện tích ban đầu ở đơn vị lớn |
| `{initial_small}` | Phần diện tích ban đầu ở đơn vị nhỏ |
| `{added_large}` | Diện tích được thêm ở đơn vị lớn |
| `{removed_small}` | Diện tích lấy bớt ở đơn vị nhỏ, sau khi thêm |
| `{large_unit}`, `{small_unit}` | Ký hiệu đơn vị được C# lấy từ registry |

Ví dụ phương án cho `AreaMeasurementStories.Added`:

```json
{
  "Id": "AreaMeasurementStories.Added",
  "Alternatives": [
    "Sau đó, thêm phần {subject} có diện tích {added_large} {large_unit}."
  ]
}
```

Không điền số vào biến, đảo thêm/bớt, hỏi lượng ban đầu thay cho lượng hiện có,
hoặc đổi "diện tích" thành "chiều dài", "chu vi", "thể tích". Phương án mới cần
được người làm đề duyệt ý nghĩa; kiểm tra cấu trúc biến không tự chứng minh
hai câu tự nhiên tương đương.

`AreaMeasurementVocabulary` khai báo từ chỉ diện tích và từ chỉ đại lượng khác
cho từng ngôn ngữ. Kiểm tra JSON yêu cầu mỗi câu thuộc nhánh này gọi rõ diện tích,
không chứa số được điền sẵn hoặc thuật ngữ của chiều dài/chu vi/thể tích/dung tích.
Khi thêm ngôn ngữ, dịch list từ vựng này cùng các câu; không thêm `if/else` vào C#.

## Contract, tái tạo và chống trùng

`AreaMeasurementQuestionContract` giữ profile, seed, các câu đã chọn và snapshot
của loại đại lượng, đối tượng, mã đơn vị, giới hạn. Khi snapshot không khớp JSON
hiện tại, validator từ chối thay vì âm thầm đổi ý nghĩa mẫu cũ. `FreshAreaMeasurement`
giữ profile/snapshot/lời văn và tạo dữ kiện mới; `RecreateAreaMeasurement` giữ seed.

Validator dựng lại từ contract và đối chiếu lời văn, thứ tự vai trò, đơn vị,
biểu thức, các bước và đáp án. `ProseHash` dựa vào lời đề, ngôn ngữ, đối tượng và
đơn vị; số ngẫu nhiên và câu dẫn lời giải không làm một đề trùng thành đề mới.

`--area-measurement-foundation` lưu các mẫu vào bảng chuẩn bị trong SQLite dưới
`artifacts/verification`, kiểm tra khóa duy nhất và đọc lại với dữ kiện mới.
Đây là kiểm thử lưu trữ trước khi nối AI, không phải migration hay ghi thêm dữ
liệu vào `BasicQuestionBank` của người dùng.

## Thể tích và dung tích

`MeasurementQuantityRules.Convert` mặc định chỉ đổi trong cùng đại lượng.
Thể tích dùng m³/dm³/cm³; dung tích dùng l/ml. Chỉ khi contract tương lai cho
phép, bên gọi mới truyền `allowVolumeCapacity: true`; khi đó áp dụng
`1 dm³ = 1 l`, `1 cm³ = 1 ml`. Không cho đổi chiều dài/khối lượng sang diện tích
hoặc dung tích. Nhánh diện tích hiện tại không sử dụng cầu nối này.

Lời văn đo thể tích có `TextByUnit.volume` riêng, gọi là "thể tích", giữ đúng
thêm/bớt thể tích vật liệu và đơn vị được hỏi. Chưa bổ sung bối cảnh AI cho thể tích.

## Kiểm thử

```powershell
dotnet run --project UnitTest/MathPuzzleUnitTests -c Release -- "Area measurement foundation"
dotnet run --project UnitTest/AiQuestionBankTests -c Release -- --area-measurement-foundation
```

Kiểm tra hai ngôn ngữ, 6 bối cảnh, sao 1–5, ba kiểu câu hỏi, đáp án/đơn vị/lời
giải, câu sai mục tiêu hoặc vai trò, hệ số bình phương/lập phương, cầu nối lít
được phép và bị cấm, chống trùng, SQLite và thay dữ kiện mới. Chưa chạy model
vì bước này chưa nối AI.
