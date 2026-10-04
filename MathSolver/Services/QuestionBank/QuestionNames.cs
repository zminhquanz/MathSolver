namespace MathSolver.Services.QuestionBank;

/// <summary>Given-name pools, separate from the role assigned to a fictional actor.</summary>
public static class QuestionNames
{
    // First 100 entries in each VNTH01 frequency list, not exclusive gender rules.
    // Nguyen Duc Anh, hoten.org, CC BY 4.0. See README.md in this directory.
    public static IReadOnlyList<string> VietnameseMale { get; } = Array.AsReadOnly(
        "Huy|Anh|Tuấn|Hiếu|Đạt|Nam|Hoàng|Minh|Đức|Dũng|Duy|Long|Sơn|Hùng|Bảo|Thành|Cường|Phúc|Trung|Hưng|Hải|Thắng|Tùng|Quân|Khang|Tiến|Quang|Khoa|Khánh|Vũ|Phong|Trường|Tú|Tài|Thịnh|Nguyên|Dương|Phát|Toàn|Vinh|Linh|Nghĩa|Mạnh|Lâm|Bình|An|Nhân|Kiên|Việt|Thái|Lộc|Phú|Tâm|Thiện|Trí|Phương|Tân|Khôi|Kiệt|Thanh|Sang|Giang|Hiệp|Nhật|Thuận|Công|Hòa|Hậu|Hào|Quý|Đăng|Ngọc|Trọng|Chiến|Hà|Luân|Phước|Thông|Khải|Quốc|Tín|Đại|Đông|Vương|Lợi|Chung|Danh|Ân|Quyền|Hoàn|Thiên|Văn|Hiển|Khanh|Tuân|Thọ|Phi|Quyết|Cương|Cảnh".Split('|'));
    public static IReadOnlyList<string> VietnameseFemale { get; } = Array.AsReadOnly(
        "Anh|Linh|Trang|Thảo|Ngọc|Phương|Huyền|Hương|Nhi|Hà|Ngân|Hằng|Vy|Quỳnh|Hiền|Thư|Yến|Nhung|Mai|Dung|Vân|Thủy|Như|Nga|My|Hạnh|Duyên|Uyên|Hân|Giang|Hoa|Thúy|Trâm|Lan|Trinh|Ánh|Ly|Thanh|Oanh|Thu|Chi|Quyên|Thương|Phượng|Hồng|Loan|Tâm|Tiên|An|Trân|Dương|Thùy|Huệ|Châu|Hường|Nguyên|Trúc|Liên|Nguyệt|Tuyền|Tuyết|Vi|Hoài|Xuân|Tú|Diễm|Minh|Thắm|Ý|Hòa|Nghi|Lệ|Kiều|Thy|Bích|Bình|Thi|Thoa|Hảo|Đào|Nhàn|Trà|Huế|Mỹ|Lam|Lý|Diệu|Phụng|Diệp|Hậu|Khánh|Hải|Kim|Cúc|Thơ|Phúc|Sương|Hiếu|Thơm|Chinh".Split('|'));
    private static readonly string[] EnglishMale = ["James", "John", "Robert", "Michael", "William", "David", "Richard", "Joseph", "Thomas", "Charles", "Daniel", "Matthew", "Anthony", "Mark", "Paul", "Andrew", "Joshua", "Steven", "Kevin", "Brian"];
    private static readonly string[] EnglishFemale = ["Mary", "Patricia", "Jennifer", "Linda", "Elizabeth", "Barbara", "Susan", "Jessica", "Sarah", "Karen", "Nancy", "Lisa", "Margaret", "Betty", "Sandra", "Ashley", "Dorothy", "Kimberly", "Emily", "Donna"];

    internal static string GivenName(string actor) => actor.Replace("'s shop", "").Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1];

    public static string Create(AppLanguage language, Random random, bool largeContext = false)
    {
        bool female = random.Next(2) == 0, vi = language == AppLanguage.Vietnamese;
        var names = vi ? (female ? VietnameseFemale : VietnameseMale) : (female ? EnglishFemale : EnglishMale);
        string name = names[random.Next(names.Count)];
        string[] roles = vi
            ? female ? ["", "bà ", "mẹ ", "chị ", "cô ", "dì ", "mợ "] : ["", "ông ", "cha ", "anh ", "chú ", "bác ", "cậu "]
            : female ? ["", "Grandma ", "Aunt ", "Ms. "] : ["", "Grandpa ", "Uncle ", "Mr. "];
        string actor = roles[random.Next(roles.Length)] + name;
        // Large stock quantities belong to a business, not a child's possessions.
        return largeContext ? vi ? "cửa hàng của " + actor : actor + "'s shop" : actor;
    }
}
