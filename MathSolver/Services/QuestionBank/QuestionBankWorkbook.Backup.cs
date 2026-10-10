using MathSolver.Models;
using System.IO.Compression;

namespace MathSolver.Services.QuestionBank;

public static partial class QuestionBankWorkbook
{
    public static int WriteBackup(Stream output, IEnumerable<ValidatedBankQuestion> questions,
        AppLanguage language = AppLanguage.Vietnamese, CancellationToken cancellationToken = default)
    {
        var records = questions.ToArray();
        using var raw = new MemoryStream();
        int count = Write(raw, records, cancellationToken);
        raw.Position = 0;
        using var archive = new ZipArchive(raw, ZipArchiveMode.Read);
        var internalSheet = SpreadsheetWorkbook.Load(archive, "xl/worksheets/sheet1.xml");
        bool vi = language == AppLanguage.Vietnamese;
        string W(string a, string b) => vi ? a : b;
        string[] headers = [W("Dạng toán", "Problem type"), W("Mức độ", "Difficulty"), W("Ngôn ngữ", "Language"),
            W("Đề bài minh họa", "Rendered question"), W("Đáp án do app tính", "App-calculated answer"), W("Lời giải", "Solution"), W("Nguồn", "Source"), W("Dữ liệu kèm đề", "Supporting data")];
        IEnumerable<IReadOnlyList<string>> overview = records.Select(q => (IReadOnlyList<string>)new[] {
            QuestionAuthoringChoices.FamilyLabel(q.Contract.Family, language), new string('★', (int)q.Contract.Tier),
            q.Contract.Language == AppLanguage.Vietnamese ? "Tiếng Việt" : "English", q.WordProblem.ProblemText,
            QuestionAuthoringWorkbook.Answer(q), QuestionAuthoringWorkbook.Solution(q), q.ModelName, QuestionAuthoringWorkbook.PreviewData(q) });
        SpreadsheetWorkbook.Write(output, [
            new(W("Ngân hàng đề", "Question bank"), SpreadsheetWorkbook.Table(headers, overview)),
            new(W("Hướng dẫn", "Guide"), SpreadsheetWorkbook.Table([W("Mục", "Item"), W("Giải thích", "Explanation")], new IReadOnlyList<string>[] {
                [W("Mục đích", "Purpose"), W("Sao lưu và khôi phục ngân hàng đề; dùng Nhập Excel trong app để xem trước và khôi phục.", "Back up and restore the question bank; use Import Excel in the app to review and restore.")],
                [W("Trang minh họa", "Overview"), W("Đề bài và đáp án để đọc. Chỉnh sửa trang này không thay đổi dữ liệu sẽ được khôi phục.", "Questions and answers are for reading. Editing this sheet does not change the restored data.")],
                [W("Dữ liệu nội bộ", "Internal data"), W("Trang Questions được ẩn và chứa cấu trúc đầy đủ. Không chỉnh sửa hoặc xóa trang này. Dữ liệu sai không được khôi phục.", "Hidden Questions contains the complete schema. Do not edit or remove it. Invalid data is not restored.")],
                [W("Soạn đề mới", "Author new templates"), W("Dùng Tải mẫu Excel để soạn đề trong app. Không dùng bản sao lưu làm mẫu soạn đề.", "Use Download authoring template in the app. Do not use a backup as an authoring form.")]
            }, false)),
            new("Questions", internalSheet, true)
        ]);
        return count;
    }
}
