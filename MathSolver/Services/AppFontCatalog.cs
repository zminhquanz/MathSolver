namespace MathSolver.Services;

/// <summary>
/// Mô tả một phông chữ có thể chọn trong phần Cài đặt.
/// Key được lưu trong Preferences.
/// FileName là tên file nằm trong Resources/Fonts.
/// Alias là tên dùng trong thuộc tính FontFamily.
/// </summary>
public sealed record AppFontOption(
    string Key,
    string DisplayName,
    string? FileName,
    string? Alias)
{
    public bool IsSystemDefault =>
        string.IsNullOrWhiteSpace(FileName) ||
        string.IsNullOrWhiteSpace(Alias);

    public string FontFamily =>
        Alias ?? string.Empty;
}

public static class AppFontCatalog
{
    public const string DefaultFontKey =
        "OpenSansRegular";

    /// <summary>
    /// Danh sách duy nhất cần chỉnh khi thêm phông chữ mới.
    ///
    /// Cách thêm:
    /// 1. Chép file .ttf hoặc .otf vào Resources/Fonts/.
    /// 2. Thêm một AppFontOption vào danh sách bên dưới.
    ///
    /// MauiProgram tự đăng ký toàn bộ font trong danh sách này.
    /// </summary>
    public static IReadOnlyList<AppFontOption> Options { get; } =
    [
        new(
            Key: "SystemDefault",
            DisplayName: "Mặc định hệ thống",
            FileName: null,
            Alias: null),

        new(
            Key: "OpenSansRegular",
            DisplayName: "Open Sans",
            FileName: "OpenSans-Regular.ttf",
            Alias: "OpenSansRegular"),

        new(
            Key: "OpenSansSemibold",
            DisplayName: "Open Sans SemiBold",
            FileName: "OpenSans-Semibold.ttf",
            Alias: "OpenSansSemibold"),
        new(
            Key:"RobotoRegular",
            DisplayName: "Roboto",
            FileName: "Roboto-Regular.ttf",
            Alias: "RobotoRegular"),
        new(
            Key:"RobotoItalic",
            DisplayName: "Roboto Italic",
            FileName: "Roboto-Italic.ttf",
            Alias: "RobotoItalic"),
        new(
            Key:"GoogleSans-Regular",
            DisplayName: "Google Sans",
            FileName: "GoogleSans-Regular.ttf",
            Alias: "GoogleSans-Regular"),
        new(
            Key:"GoogleSans-Italic",
            DisplayName: "Google Sans Italic",
            FileName: "GoogleSans-Italic.ttf",
            Alias: "GoogleSans-Italic")

        // Ví dụ thêm font mới:
        //
        // ,new(
        //     Key: "RobotoRegular",
        //     DisplayName: "Roboto",
        //     FileName: "Roboto-Regular.ttf",
        //     Alias: "RobotoRegular")
    ];

    public static void RegisterFonts(
        IFontCollection fonts)
    {
        ArgumentNullException.ThrowIfNull(fonts);

        foreach (AppFontOption option
                 in Options)
        {
            if (option.IsSystemDefault)
            {
                continue;
            }

            fonts.AddFont(
                option.FileName!,
                option.Alias!);
        }
    }

    public static AppFontOption GetByKey(
        string? key)
    {
        AppFontOption? option =
            Options.FirstOrDefault(
                item =>
                    string.Equals(
                        item.Key,
                        key,
                        StringComparison.Ordinal));

        return option ??
               Options.First(
                   item =>
                       item.Key ==
                       DefaultFontKey);
    }
}
