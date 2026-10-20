using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>Inclusive day counts are distinct from elapsed-day differences.</summary>
internal static class AdjacentMonthInclusiveLanguage
{
    internal static string WritingInstructions(BasicQuestionContract c)
    {
        bool february=SameMonthCalendarPilot.IsFebruaryCommon(c.SceneId);
        string monthScope=c.Language==AppLanguage.Vietnamese
            ?february?"Hai mốc thuộc tháng Hai và tháng Ba của cùng một năm thường do C# chọn; tháng Hai có 28 ngày. Giữ các biến tháng và năm trong đúng trường, không đổi thành năm nhuận."
                :"Hai tháng liền nhau trong cùng năm, đều có 30 hoặc 31 ngày."
            :february?"The dates are in February and March of the same common year chosen by C#; February has 28 days. Keep the month and year placeholders in their own fields; do not change this into a leap year."
                :"The months are consecutive within the same year, both with 30 or 31 days.";
        return c.Language==AppLanguage.Vietnamese?$$"""
        Bạn biên soạn toán đố tiểu học tự nhiên. Được viết câu mới, đổi cách mở đầu và từ nối; ví dụ chỉ để tham khảo.
        Mỗi biến là một cụm hoàn chỉnh. fact_0 gắn {v0} {v2} {v1} với ngày {f0} tháng {f2} năm {f3}; fact_1 gắn {v0} {v3} {v1} với ngày {f1} tháng {f4} năm {f3}. Đặt ngày trước hoặc sau người và hành động theo gợi ý bối cảnh; không thêm động từ vào cụm hành động.
        {{monthScope}} fact_2 chỉ nêu tháng {f2} có {f5} ngày. fact_3 nêu rõ TÍNH CẢ ngày bắt đầu VÀ ngày kết thúc, cùng mọi ngày ở giữa. Đây là quy ước đếm ngày, không được dời sự kiện, bỏ cuối tuần hoặc thêm điều kiện. Hai ngày sát nhau qua ranh giới tháng được tính là hai ngày.
        Câu hỏi dùng MỘT cụm {v4} của {v0} rồi hỏi số ngày được tính. Ví dụ: Hỏi {v4} của {v0} được tính bao nhiêu ngày? Cụm biến đã hoàn chỉnh; không đặt thêm nhãn thời gian trước cụm, không hỏi số ngày thực sự sử dụng đồ vật hay ngày kết thúc.
        Câu dẫn lời giải chỉ gọi đại lượng, ví dụ Số ngày được tính. Mỗi trường một câu, dữ kiện kết thúc dấu chấm, câu hỏi dấu hỏi. Chỉ trả JSON đúng role/step, giữ biến trong đúng trường; không điền tên, số, đáp án hoặc hướng dẫn kỹ thuật.
        """:$$"""
        Write a short, natural elementary word problem. Write new sentences and vary openings; examples are references.
        Every placeholder is already a complete phrase. fact_0 attaches {v0} {v2} {v1} to day {f0} of month {f2} in year {f3}. fact_1 attaches {v0} {v3} {v1} to day {f1} of month {f4} in year {f3}. Use actor-first or date-first openings as suggested by the context; preserve the completed actions.
        {{monthScope}} fact_2 states only that month {f2} has {f5} days. fact_3 explicitly INCLUDES BOTH the start date AND the end date and counts every intervening calendar day. This is a counting convention, not a change to event dates. Skip no weekends or holidays, and add no conditions. Consecutive dates across the month boundary count as two days.
        Ask a number of days, naming the unit days explicitly in the question. Keep calendar attached to days as one quantity phrase. Connect that quantity directly to the ONE complete phrase {v0}'s {v4}, or the {v4} for {v0}. A possessive name needs no article before it. For example: How many calendar days are in {v0}'s {v4}? Or: What is the number of calendar days in {v0}'s {v4}? Other natural questions are welcome if they ask the same day count. Use the complete interval phrase once without extra labels; do not ask actual days of use or the end date.
        Describe month length with ordinary calendar wording, for example Month {f2} has {f5} days. Names and actions are already supplied by the complete phrases; attach their dates directly, without adding labels or extra verbs.
        Use a short quantity name such as Number of calendar days as the solution lead. One sentence per field, periods for facts, a question mark for the question. Return only JSON with correct roles/step and placeholders in their own fields. Fill no names, numbers, answers or technical instructions.
        """;
    }
}
