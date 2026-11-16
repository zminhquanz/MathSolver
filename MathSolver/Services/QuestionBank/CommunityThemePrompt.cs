using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

internal static class CommunityThemePrompt
{
    internal static string Guidance(BasicQuestionContract c)
    {
        string guidance = c.KnowledgeGroup switch
        {
        QuestionKnowledgeGroup.Toys => c.Language == AppLanguage.Vietnamese
            ? "\nChủ đề Đồ chơi: giữ đúng viên bi/quả bóng hoặc giấy làm đồ chơi trong dữ kiện C#. Không thay loại đồ chơi hay đơn vị."
            : "\nToys: preserve the exact marble/ball identity or toy-making paper and all C# units and quantities.",
        QuestionKnowledgeGroup.Healthcare => c.Language == AppLanguage.Vietnamese
            ? "\nChủ đề Y tế: chỉ viết bài toán tồn kho, nhập kho, đóng gói hoặc chia đều vật tư; giữ đúng hộp thuốc, chai siro, băng gạc và đơn vị C#. Không thêm liều dùng, chỉ định điều trị hoặc lời khuyên uống thuốc. Phân số chỉ biểu thị diện tích gạc, không chia lẻ viên thuốc."
            : "\nHealthcare: use stock, incoming inventory, packing or equal distribution only; preserve exact medicine boxes, syrup bottles, bandages, gauze and C# units. Add no dosage, treatment or medication-taking advice. Fractions describe gauze area, never split medicine tablets.",
        QuestionKnowledgeGroup.School => c.Language == AppLanguage.Vietnamese
            ? "\nChủ đề Trường học: giữ đúng học sinh, giáo viên hoặc giấy dạy học theo dữ kiện C#. Số người là số nguyên; phân số chỉ mô tả diện tích giấy dùng trong lớp học."
            : "\nSchool: preserve the C# student/teacher identities or teaching paper. People are whole counts; fractional quantities describe classroom paper area only.",
        _ => ""
        };
        if (guidance.Length == 0) return guidance;
        string unit = c.Family == BankQuestionFamily.Fraction && FractionQuestionCatalogue.Find(c.SceneId) is { } scene
            ? c.Language == AppLanguage.Vietnamese ? scene.ResultUnit : scene.EnResultUnit : c.Unit;
        return guidance + (c.Language == AppLanguage.Vietnamese ? "\nĐơn vị C# cần giữ nguyên: " : "\nPreserve the C# unit: ") + unit;
    }
}
