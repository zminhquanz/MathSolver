using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

internal static class FoodThemePrompt
{
    // A single venue kind is chosen per question and shared by both owners.
    private static readonly (string Vi, string En)[] Bakeries =
        [("tiệm bánh", "bakery"), ("cửa hàng bánh", "cake shop")];
    private static readonly (string Vi, string En)[] Eateries =
        [("quán ăn", "eatery"), ("nhà hàng", "restaurant"), ("tiệm ăn", "diner")];

    internal static int VenueCount(bool bakery) => bakery ? Bakeries.Length : Eateries.Length;

    internal static string Venue(bool bakery, int choice, AppLanguage language)
    {
        var kinds = bakery ? Bakeries : Eateries;
        var kind = kinds[choice % kinds.Length];
        return language == AppLanguage.Vietnamese ? kind.Vi : kind.En;
    }

    internal static string OwnerVenue(string owner, AppLanguage language, int choice, bool bakery)
        => language == AppLanguage.Vietnamese
            ? Venue(bakery, choice, language) + " của " + owner
            : owner + "'s " + Venue(bakery, choice, language);

    private static string PluralVenue(string venue) => venue switch
    {
        "bakery" => "bakeries", "cake shop" => "cake shops", "eatery" => "eateries",
        "restaurant" => "restaurants", _ => "diners"
    };

    // AI drafts must keep the same venue in the question as in both C# owner facts.
    internal static bool CombinedQuestionNamesVenue(string questionTemplate, BasicQuestionContract c)
    {
        if (c.KnowledgeGroup != QuestionKnowledgeGroup.Food || c.Structure != BasicQuestionStructure.Combine)
            return true;

        bool vi = c.Language == AppLanguage.Vietnamese;
        bool bakery = FoodThemeCatalogue.IsBakeryContract(c);
        foreach (int choice in Enumerable.Range(0, VenueCount(bakery)))
        {
            string venue = Venue(bakery, choice, c.Language);
            string ownerPrefix = vi ? venue + " của " : "'s " + venue;
            if (!c.Subject.Contains(ownerPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (!c.OtherSubject.Contains(ownerPrefix, StringComparison.OrdinalIgnoreCase)) return false;

            string question = AppliedQuestionCatalogue.Render(questionTemplate, c);
            string pair = vi ? "cả hai " + venue : "the two " + PluralVenue(venue);
            return question.Contains(pair, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    internal static string AlignCombinedQuestion(string text, BasicQuestionContract c)
    {
        if (c.KnowledgeGroup != QuestionKnowledgeGroup.Food || c.Structure != BasicQuestionStructure.Combine
            || !text.Contains('?')) return text;

        bool bakery = FoodThemeCatalogue.IsBakeryContract(c);
        var kinds = bakery ? Bakeries : Eateries;
        string? venue = null;
        foreach (var kind in kinds)
        {
            if (c.Language == AppLanguage.Vietnamese
                ? c.Subject.StartsWith(kind.Vi + " của ", StringComparison.OrdinalIgnoreCase)
                : c.Subject.EndsWith("'s " + kind.En, StringComparison.OrdinalIgnoreCase))
            {
                venue = c.Language == AppLanguage.Vietnamese ? kind.Vi : kind.En;
                break;
            }
        }
        if (venue is null) return text;
        if (c.Language == AppLanguage.Vietnamese)
            return text.Replace("cả hai cơ sở", "cả hai " + venue, StringComparison.OrdinalIgnoreCase)
                .Replace("Hai cơ sở", "Hai " + venue, StringComparison.OrdinalIgnoreCase);

        string plural = PluralVenue(venue);
        return text.Replace("the two businesses", "the two " + plural, StringComparison.OrdinalIgnoreCase)
            .Replace("both places", "both " + plural, StringComparison.OrdinalIgnoreCase);
    }

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
        string venueGuidance = FoodThemeCatalogue.IsBakeryContract(c)
            ? vi
                ? " Bánh được làm hoặc bán tại tiệm bánh/cửa hàng bánh có chủ được nêu tên; riêng bánh cuốn và bánh xèo là món phục vụ tại quán ăn/nhà hàng. {name} và {other} đã bao gồm tên tiệm và chủ tiệm; không thêm tiền tố chủ sở hữu. Với phép nhân/chia số nguyên, dùng khay bánh để chia số bánh, không dùng bàn ăn hoặc nhóm chung chung. Với phân số, giữ nguyên quan hệ lượng phần và lượng toàn phần của bánh."
                : " Cakes, pastries and bakery items are made or sold at a named owner's bakery or cake shop. Steamed rice rolls and Vietnamese sizzling pancakes remain restaurant dishes. {name} and {other} already include the bakery and owner; never add a duplicate owner. For whole-number multiplication/division, use trays rather than tables or generic groups. For fractions, retain the given part-to-whole relation."
            : c.KnowledgeGroup == QuestionKnowledgeGroup.Food
            ? vi
                ? " Tất cả món ăn đã chế biến được phục vụ hoặc bán tại quán ăn/nhà hàng/tiệm ăn có chủ được nêu tên; bao gồm bánh chưng, bánh dày, bánh tét, bánh bèo, bánh khọt, bánh bột lọc, bánh giò, bánh bao nhân thịt và trứng cút, há cảo. {name} và {other} đã bao gồm tên quán và chủ quán; không thêm tiền tố chủ sở hữu. Với phép nhân/chia số nguyên, dùng bàn ăn để chia suất; không dùng 'nhóm' chung chung. Với phân số, giữ nguyên quan hệ lượng phần và lượng toàn phần của món đã làm."
                : " All prepared dishes are served or sold at a named owner's eatery, restaurant or diner; including Vietnamese sticky rice cakes, steamed rice cakes, rice pancakes, dumplings and pork-and-quail-egg steamed buns. {name} and {other} already include the business and owner; do not add another owner prefix. For whole-number multiplication/division, distribute servings across tables, not generic groups. For fractions, retain the given part-to-whole relation of prepared dishes."
            : "";
        string pairGuidance = c.KnowledgeGroup == QuestionKnowledgeGroup.Food
            && c.Structure == BasicQuestionStructure.Combine && !string.IsNullOrWhiteSpace(c.OtherSubject)
            ? vi
                ? " Hai cửa hàng trong cùng một đề phải cùng loại địa điểm: hai tiệm bánh, hai cửa hàng bánh, hai quán ăn, hai nhà hàng hoặc hai tiệm ăn. "
                    + "Hai chủ sở hữu đã được C# gán cùng loại: " + c.Subject + " và " + c.OtherSubject
                    + ". Câu hỏi phải nói 'cả hai' kèm đúng tên loại địa điểm này, không dùng 'cơ sở' chung chung."
                : " Both owners run the same kind of venue, already fixed by C#: " + c.Subject + " and " + c.OtherSubject
                    + ". Ask about both bakeries, both cake shops, both eateries, both restaurants or both diners as appropriate; avoid generic businesses or places."
            : "";
        return "\n"+(vi
            ?"Tên đầy đủ của đơn vị đếm do C# điền: "+c.Unit+". {unit}, {unit_a}, {unit_b} đã gồm cả tên vật và từ chỉ loại; không viết lặp tên món/quả/nước sau các biến đó. {name} và {other} là chủ thể sở hữu, không phải số lượng hay đồ vật. Không đổi món đã hoàn thành thành nguyên liệu. Mỗi dữ kiện chỉ là một câu mô tả, không chèn câu hỏi hoặc lời chỉ dẫn vào dữ kiện. "
            :"The complete counted unit supplied by C# is: "+c.Unit+". {unit}, {unit_a}, {unit_b} already include the object and its counter; never append the food, fruit, drink or group noun again after these slots. {name} and {other} identify owners, never amounts or items. Keep prepared food distinct from ingredients. Each fact is one declarative sentence; do not insert questions or author instructions inside facts. ")
            +relation+venueGuidance+pairGuidance;
    }
}
