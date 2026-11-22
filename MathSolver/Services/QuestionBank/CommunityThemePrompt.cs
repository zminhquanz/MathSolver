using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

internal static class CommunityThemePrompt
{
    internal static string Guidance(BasicQuestionContract c)
    {
        string guidance = c.KnowledgeGroup switch
        {
        QuestionKnowledgeGroup.Animals when PetAnimalThemeCatalogue.FindScene(c.SceneId) is not null => c.Language == AppLanguage.Vietnamese
            ? "\nChủ đề Thú cưng và cá cảnh: giữ đúng loài, số lượng, đơn vị và nơi chăm sóc từ C#. Chỉ dùng tình huống nhận thêm, chuyển tới nơi chăm sóc khác hoặc bố trí theo chuồng/bể. Không viết cảnh săn mồi, động vật hoang dã hay nhốt chung loài không tương thích. Hamster, cá La Hán, cá rồng: mỗi lồng/bể riêng chỉ có 1 cá thể."
            : "\nPets and ornamental fish: preserve the exact species, quantities, units and care facility from C#. Use intake, transfer or appropriate enclosure/aquarium grouping. Avoid hunting, wild predators or incompatible tankmates. Hamsters, flowerhorn cichlids and arowanas must have one individual per separate enclosure/aquarium.",
        QuestionKnowledgeGroup.Toys => c.Language == AppLanguage.Vietnamese
            ? "\nChủ đề Đồ chơi: giữ nguyên chính xác loại đồ chơi, số lượng và đơn vị từ C#. Đề bài có thể nói về cửa hàng đồ chơi, nhập hàng, bán hàng, xếp hộp hay chia đều. Phân biệt bộ thẻ bài với tấm thiệp, Rubik vuông với Rubik rắn, và bộ Origami với từng hình gấp giấy."
            : "\nToys: preserve the exact toy identity, quantities and units from C#. Use toy-shop sales, inventory, packing or equal distribution. Distinguish playing-card decks from greeting cards, cube puzzles from snake puzzles, and origami sets from individual paper figures.",
        QuestionKnowledgeGroup.Healthcare => c.Language == AppLanguage.Vietnamese
            ? "\nChủ đề Y tế: chỉ viết bài toán tồn kho, nhập kho, đóng gói hoặc chia đều vật tư; giữ đúng hộp thuốc, chai siro, băng gạc và đơn vị C#. Không thêm liều dùng, chỉ định điều trị hoặc lời khuyên uống thuốc. Phân số chỉ biểu thị diện tích gạc, không chia lẻ viên thuốc."
            : "\nHealthcare: use stock, incoming inventory, packing or equal distribution only; preserve exact medicine boxes, syrup bottles, bandages, gauze and C# units. Add no dosage, treatment or medication-taking advice. Fractions describe gauze area, never split medicine tablets.",
        QuestionKnowledgeGroup.SchoolSupplies or QuestionKnowledgeGroup.School => c.Language == AppLanguage.Vietnamese
            ? "\nChủ đề Trường học: bám đúng tình huống C# như thư viện, quyên góp vở, góp sách theo tổ, đọc sách, thể thao, câu lạc bộ, lớp học, trồng cây, tái chế hoặc trang trí. Giữ nguyên người, vật, số, đơn vị và phép toán. Phân biệt số sách có, sách cho mượn, số sách quyên góp, số trang dự định đọc và đã đọc. Mọi số người và đồ vật đều nguyên. Phân số giấy chỉ mô tả diện tích."
            : "\nSchool: keep the exact C# setting, units, quantities and operation. Use library books, notebook donations, books collected by school teams, reading plans, sports, clubs, classrooms, planting, recycling or decorations as specified. Distinguish stock, loans, donations and planned pages from pages read. People and objects are whole counts. Paper fractions describe area.",
        _ => ""
        };
        if (guidance.Length == 0) return guidance;
        if (PetAnimalThemeCatalogue.FindScene(c.SceneId) is { } pet)
            guidance += c.Language == AppLanguage.Vietnamese
                ? $"\nLoài cụ thể: {pet.Unit.Vietnamese}; địa điểm: {pet.ViVenue}; loại chuồng/bể: {pet.Unit.VietnameseGroup}; tối đa {pet.MaxPerGroup} cá thể trong một nhóm. Chỉ dùng hoạt động tương ứng với phép toán C#."
                : $"\nSelected species: {pet.Unit.Plural}; facility: {pet.EnVenue}; group: {pet.Unit.GroupSingular}; at most {pet.MaxPerGroup} animals per group. Follow the C# arithmetic operation.";
        if (c.KnowledgeGroup == QuestionKnowledgeGroup.School && SchoolThemeCatalogue.FindScene(c.SceneId) is { } school)
            guidance += c.Language == AppLanguage.Vietnamese
                ? $"\nTình huống cụ thể: {school.ViActor}; đồ vật: {c.Unit}; nhóm chứa hoặc sử dụng: {school.ViGroup}; hoạt động ban đầu: {school.ViActivity}; hoạt động lấy bớt: {school.ViRemoval}. Chỉ sử dụng hoạt động phù hợp với phép toán trong C#."
                : $"\nSelected school setting: {school.EnActor}; object: {c.Unit}; storage or activity group: {school.EnGroup}; initial activity: {school.EnActivity}; removal activity: {school.EnRemoval}. Use the activity appropriate to the C# operation.";
        string unit = c.Family == BankQuestionFamily.Fraction && FractionQuestionCatalogue.Find(c.SceneId) is { } scene
            ? c.Language == AppLanguage.Vietnamese ? scene.ResultUnit : scene.EnResultUnit : c.Unit;
        return guidance + (c.Language == AppLanguage.Vietnamese ? "\nĐơn vị C# cần giữ nguyên: " : "\nPreserve the C# unit: ") + unit;
    }
}
