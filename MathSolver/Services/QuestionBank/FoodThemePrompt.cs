using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

internal static class FoodThemePrompt
{
    internal static string OwnerVenue(string owner, AppLanguage language, bool restaurant)
        => language == AppLanguage.Vietnamese
            ? (restaurant ? "nhà hàng của " : "quán ăn của ") + owner
            : owner + (restaurant ? "'s restaurant" : "'s eatery");

    internal static string Guidance(BasicQuestionContract c)
    {
        if(!FoodThemeCatalogue.IsTheme(c.KnowledgeGroup))return "";
        bool vi=c.Language==AppLanguage.Vietnamese;
        var operation=c.Version==FindXQuestionCatalogue.Version?FindXQuestionCatalogue.Find(c.SceneId)!.SourceScene.Operation:c.Operation;
        string relation=operation switch
        {
            ArithmeticOperation.Add=>vi?"Hai chủ thể có hai lượng riêng; hỏi tổng của cả hai.":"Two owners hold separate quantities; ask their combined total.",
            ArithmeticOperation.Subtract=>vi?"Cùng một chủ thể có lượng ban đầu và dùng bớt một phần; hỏi lượng còn lại.":"One owner has an initial stock and uses part of it; ask the amount left.",
            ArithmeticOperation.Multiply=>vi?"Mỗi nhóm có cùng lượng; số nhóm đã cho; hỏi tổng tất cả các nhóm.":"Every group has the same amount; the group count is given; ask the total across all groups.",
            _=>vi?"Tổng lượng được chia đều cho số nhóm đã cho; hỏi lượng của mỗi nhóm.":"The total is shared equally across the given group count; ask the amount per group."
        };
        if(c.Version==FractionQuestionCatalogue.Version)
            relation=operation switch {
                ArithmeticOperation.Subtract=>vi?"So sánh hai lượng riêng của hai chủ thể; hỏi chênh lệch, không phải lấy bớt hay phần còn lại.":"Compare two separate amounts held by two owners; ask their difference, not removal or remaining stock.",
                ArithmeticOperation.Multiply=>vi?"Biết lượng ban đầu và phân số được chọn; hỏi giá trị phần được chọn.":"The original amount and selected fraction are given; ask the selected amount.",
                ArithmeticOperation.Divide=>vi?"Biết giá trị một phần và phân số tương ứng; hỏi lượng ban đầu.":"The value of one part and its fraction are given; ask the original whole amount.",
                _=>relation };
        string venueGuidance = c.KnowledgeGroup == QuestionKnowledgeGroup.Food
            ? vi
                ? " Tất cả món ăn đã chế biến được phục vụ hoặc bán tại quán ăn/nhà hàng có chủ được nêu tên. {name} và {other} đã bao gồm tên quán và chủ quán; không thêm tiền tố chủ sở hữu. Với phép nhân/chia số nguyên, dùng bàn ăn để chia suất; không dùng 'nhóm' chung chung. Với phân số, giữ nguyên quan hệ lượng phần và lượng toàn phần của món đã làm."
                : " All prepared dishes are served or sold at a named owner's eatery or restaurant. {name} and {other} already include the business and owner; do not add another owner prefix. For whole-number multiplication/division, distribute servings across tables, not generic groups. For fractions, retain the given part-to-whole relation of prepared dishes."
            : "";
        return "\n"+(vi
            ?"Tên đầy đủ của đơn vị đếm do C# điền: "+c.Unit+". {unit}, {unit_a}, {unit_b} đã gồm cả tên vật và từ chỉ loại; không viết lặp tên món/quả/nước sau các biến đó. {name} và {other} là chủ thể sở hữu, không phải số lượng hay đồ vật. Không đổi món đã hoàn thành thành nguyên liệu. Mỗi dữ kiện chỉ là một câu mô tả, không chèn câu hỏi hoặc lời chỉ dẫn vào dữ kiện. "
            :"The complete counted unit supplied by C# is: "+c.Unit+". {unit}, {unit_a}, {unit_b} already include the object and its counter; never append the food, fruit, drink or group noun again after these slots. {name} and {other} identify owners, never amounts or items. Keep prepared food distinct from ingredients. Each fact is one declarative sentence; do not insert questions or author instructions inside facts. ")
            +relation+venueGuidance;
    }
}
