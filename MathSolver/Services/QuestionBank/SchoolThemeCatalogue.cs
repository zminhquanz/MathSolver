using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>School activities with fixed units, realistic actors and grouping limits.</summary>
internal sealed record SchoolThemeSubject(string Id, string UnitId, string ViActor, string EnActor,
    string ViGroup, string EnGroup, string ViActivity, string EnActivity,
    string ViRemoval, string EnRemoval, int Capacity, int MaxFactor, int MaxPerGroup);

internal static class SchoolThemeCatalogue
{
    // Original school situations informed by Vietnamese elementary arithmetic exercises:
    // https://loigiaihay.com/giai-cung-em-hoc-toan-lop-3-tap-1-trang-27-tuan-7-tiet-1-c324a50128.html
    // https://loigiaihay.com/toan-lop-3-trang-53-phep-cong-trong-pham-vi-100-000-sgk-canh-dieu-a108045.html
    // https://loigiaihay.com/toan-lop-3-trang-104-thu-thap-phan-loai-ghi-chep-so-lieu-bang-so-lieu-sgk-ket-noi-tri-thuc-a107881.html
    internal static IReadOnlyList<SchoolThemeSubject> Subjects { get; } = QuizContentCatalog.LoadList<SchoolThemeSubject>("SchoolThemeSubjects");

    internal static SchoolThemeSubject? FindScene(string sceneId) => Subjects.FirstOrDefault(s =>
        sceneId.StartsWith($"theme-school-{s.Id}-", StringComparison.Ordinal));

    internal static string Group(SchoolThemeSubject subject, AppLanguage language)
        => language == AppLanguage.Vietnamese ? subject.ViGroup : subject.EnGroup;
}
