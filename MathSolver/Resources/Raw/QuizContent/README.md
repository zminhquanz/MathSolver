# Nội dung toán đố

- `vi-VN.json`, `en-US.json`: lời văn theo ngôn ngữ và các danh sách bối cảnh.
- `catalogues.json`: metadata toán học dùng chung.

Đọc [hướng dẫn soạn JSON](../../../QUIZ_CONTENT_AUTHORING.md) trước khi sửa. Giữ ID, biến và quan hệ toán học; chạy kiểm tra cấu trúc và sinh đề trước khi build app.

`Lists.Narrative.MultiStep.Phrasings` chứa các cách diễn đạt đã duyệt cho bài toán nhiều bước. Xem [hướng dẫn bài nhiều bước và test model](../../../MULTISTEP_AI.md); giữ thứ tự biến và số câu, kiểm tra cả ý nghĩa toán học trước khi thêm bản dịch.

`Lists.Narrative.Motion.Phrasings` chứa các cách diễn đạt đã duyệt cho bốn dạng chuyển động. Xem [hướng dẫn chuyển động AI](../../../MOTION_AI.md); giữ hướng đi, vai trò từng đối tượng, thời gian nghỉ và đơn vị của câu gốc.

## Proportion wording

Lists.Narrative.Proportion.Phrasings references Texts IDs ProportionQuizGenerator.Narrative.001–047. Each source matches its positional Templates row through Variables ordered as A/B/C. Keep per-sentence placeholder order, assumptions, units and target. See [PROPORTION_AI.md](../../../PROPORTION_AI.md) for an authoring example.

## Lời văn thập phân

`Lists.DecimalStoryContexts` chứa bối cảnh số đo và metadata đổi đơn vị.
`Lists.Narrative.Decimal.Phrasings` chứa cách diễn đạt tương đương đã duyệt,
tham chiếu ID trong `Texts`. Giữ mục tiêu mỗi phần/tất cả và thứ tự dùng/thêm
lượng; giữ nguyên hệ số đổi đơn vị giữa các ngôn ngữ. Xem
[DECIMAL_AI.md](../../../DECIMAL_AI.md) để soạn JSON và chạy kiểm tra.
