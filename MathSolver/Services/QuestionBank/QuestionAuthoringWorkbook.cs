using MathSolver.Models;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MathSolver.Services.QuestionBank;

public sealed record QuestionAuthoringMetadata(int Format, BasicQuestionContract Contract, string Title, AppLanguage WorkbookLanguage);
internal sealed record AuthoringField(string Id, string Header, string Example);

/// <summary>User-editable prose columns; the mathematical schema is retained in an OOXML custom property.</summary>
public static class QuestionAuthoringWorkbook
{
    private static readonly Regex Variables = new(@"\{[^{}]+\}", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static string W(bool vi, string vietnamese, string english) => vi ? vietnamese : english;
    private static string InputName(bool vi) => W(vi, "Nhập đề", "Input");

    private static BasicQuestionDraft Example(BasicQuestionContract c) => BasicQuestionTemplates.Example(c);

    private static AuthoringField[] Fields(BasicQuestionContract c, bool vi)
    {
        var draft = Example(c);
        var facts = draft.Facts is null
            ? new[] { new AuthoringField("given_a", W(vi, "Mẫu dữ kiện thứ nhất", "First fact template"), draft.GivenA),
                new AuthoringField("given_b", W(vi, "Mẫu dữ kiện thứ hai", "Second fact template"), draft.GivenB) }
            : draft.Facts.Select((f, i) => new AuthoringField("facts." + f.Role,
                W(vi, "Dữ kiện ", "Fact ") + (i + 1), f.Text)).ToArray();
        var leads = draft.SolutionLeads is null
            ? new[] { new AuthoringField("solution_lead", W(vi, "Câu dẫn lời giải", "Solution introduction"), draft.SolutionLead ?? "") }
            : draft.SolutionLeads.Select((s, i) => new AuthoringField("solution_leads." + s.Role,
                W(vi, "Câu dẫn bước giải ", "Solution step introduction ") + (i + 1), s.Text)).ToArray();
        return [.. facts, new("question", W(vi, "Câu hỏi cần giải", "Question to solve"), draft.Question), .. leads];
    }

    private static Dictionary<string, string> Aliases(BasicQuestionContract c)
    {
        if (c.Version != ReasoningStoryCatalogue.Version) return [];
        bool vi = c.Language == AppLanguage.Vietnamese;
        var aliases = new Dictionary<string, string>();
        var used = new Dictionary<string, int>();
        foreach (var q in ReasoningStoryCatalogue.Lesson(c).Quantities)
        {
            string name = q.Role switch {
                "answer_unit" => W(vi, "don_vi_dap_an", "answer_unit"),
                "removed" => W(vi, "luong_da_lay", "amount_removed"),
                "remaining" => W(vi, "luong_con_lai", "amount_remaining"),
                "added" => W(vi, "luong_them", "amount_added"),
                "count" => W(vi, "so_nhom", "group_count"),
                "each" => W(vi, "luong_moi_nhom", "amount_per_group"),
                "sum" or "total" => W(vi, "tong_luong", "total_amount"),
                "difference" or "diff" => W(vi, "chenh_lech", "difference"),
                "left" or "a" => W(vi, "luong_thu_nhat", "first_amount"),
                "right" or "b" => W(vi, "luong_thu_hai", "second_amount"),
                "hours" => W(vi, "so_gio", "hours"), "minutes" => W(vi, "so_phut", "minutes"),
                "speed" => W(vi, "van_toc", "speed"), "distance" => W(vi, "quang_duong", "distance"),
                "time" => W(vi, "thoi_gian", "duration"), "price" => W(vi, "don_gia", "unit_price"),
                "capacity" => W(vi, "suc_chua", "capacity"),
                _ when q.Role.StartsWith("owner_", StringComparison.Ordinal) => W(vi, "doi_tuong_", "owner_") + (int.Parse(q.Role[6..]) + 1),
                _ when q.Role.StartsWith("chart_category_", StringComparison.Ordinal) => W(vi, "nhom_du_lieu_", "data_category_") + (int.Parse(q.Role[15..]) + 1),
                _ => W(vi, q.Id.StartsWith('f') ? "du_kien_" : "doi_tuong_don_vi_", q.Id.StartsWith('f') ? "fact_" : "owner_or_unit_") + (int.Parse(q.Id[1..]) + 1)
            };
            int occurrence = used.GetValueOrDefault(name) + 1;
            used[name] = occurrence;
            aliases.Add("{" + q.Id + "}", "{" + name + (occurrence == 1 ? "" : "_" + occurrence) + "}");
        }
        return aliases;
    }

    private static string Replace(string text, IReadOnlyDictionary<string, string> map) => Variables.Replace(text, m => map.GetValueOrDefault(m.Value, m.Value));

    public static void Write(Stream output, BasicQuestionContract contract, string title, AppLanguage workbookLanguage)
    {
        if (!contract.IsTemplate || !contract.IsValid) throw new ArgumentException("InvalidContract");
        bool vi = workbookLanguage == AppLanguage.Vietnamese;
        var metadata = new QuestionAuthoringMetadata(1, contract, title, workbookLanguage);
        var fields = Fields(contract, vi);
        var aliases = Aliases(contract);
        string yes = W(vi, "Nhập", "Import"), skip = W(vi, "Bỏ qua", "Skip");
        string[] headers = [W(vi, "Xử lý dòng", "Row action"), .. fields.Select(f => f.Header)];
        string[] example = [skip, .. fields.Select(f => Replace(f.Example, aliases))];
        var input = SpreadsheetWorkbook.Table(headers, [example]);
        input.Add(new XElement(SpreadsheetWorkbook.Ns + "dataValidations", new XAttribute("count", 1),
            new XElement(SpreadsheetWorkbook.Ns + "dataValidation", new XAttribute("type", "list"),
                new XAttribute("allowBlank", 1), new XAttribute("showErrorMessage", 1),
                new XAttribute("errorTitle", W(vi, "Chọn cách xử lý", "Choose a row action")),
                new XAttribute("error", yes + " / " + skip), new XAttribute("sqref", "A2:A10001"),
                new XElement(SpreadsheetWorkbook.Ns + "formula1", "\"" + yes + "," + skip + "\""))));
        List<IReadOnlyList<string>> guide = [
            [W(vi, "Dạng toán", "Problem type"), title],
            [W(vi, "Mức độ", "Difficulty"), new string('★', (int)contract.Tier)],
            [W(vi, "Ngôn ngữ đề", "Question language"), contract.Language == AppLanguage.Vietnamese ? "Tiếng Việt" : "English"],
            [W(vi, "Tình huống để đối chiếu", "Reference situation"), Example(contract).ToWordProblem(contract).ProblemText],
            [W(vi, "Cách dùng", "How to use"), W(vi,
                "Chỉnh lời văn ở trang Nhập đề; chọn Nhập ở cột Xử lý dòng. Dòng ví dụ ban đầu được đặt Bỏ qua. Sao chép dòng để thêm mẫu; không đổi tên cột. Trang Ví dụ không được nhập.",
                "Edit prose on Input and choose Import in Row action. The initial example is set to Skip. Copy a row to add templates; keep column names. Examples is never imported.")],
            [W(vi, "Quan hệ và vai trò", "Relation and roles"), W(vi,
                "Bạn tự kiểm soát lời văn, quan hệ và đơn vị. App chỉ kiểm tra ô bắt buộc, cú pháp biến và đủ biến số của từng dữ kiện; không đối chiếu từ ngữ với mẫu AI. Có thể thêm từ nối như ‘và’, hỏi ‘cả hai’ hoặc viết lại câu. Xem trước để bảo đảm lời văn khớp phép tính C# của tình huống đã chọn.",
                "You control the wording, relation and units. The app checks required cells, valid substitution syntax and the numeric slots of each fact; it does not compare your wording with AI examples. You may add linking words or rephrase freely. Review the result against the selected C# calculation.")],
            [W(vi, "Số và đáp án", "Numbers and answers"), W(vi,
                "Giữ các biến số của từng dữ kiện để C# thay số mới và tính đáp án. Tên và đơn vị có thể lặp lại, chuyển sang câu khác hoặc viết cụ thể. Lời văn không làm thay đổi phép tính hay đáp án C#; người soạn chịu trách nhiệm kiểm tra sự phù hợp.",
                "Keep each fact's numeric slots so C# can substitute fresh values and compute answers. Names and units may be repeated, moved to another sentence or written explicitly. Wording does not change the C# calculation or answer; the author checks consistency.")],
            [W(vi, "Giới hạn", "Limits"), W(vi, "File .xlsx, tối đa 20 MB / 10.000 dòng. Không dùng công thức Excel. Xem trước và xác nhận trong app trước khi lưu.",
                ".xlsx files, up to 20 MB / 10,000 rows. Do not use Excel formulas. Review and confirm in the app before saving.")]
        ];
        var variables = fields.SelectMany(f => Variables.Matches(f.Example).Select(m => m.Value)).Distinct().ToArray();
        foreach (string variable in variables)
        {
            string value = contract.Version == ReasoningStoryCatalogue.Version ? ReasoningStoryCatalogue.Render(variable, contract)
                : BasicQuestionTemplates.Render(variable, contract);
            string meaning = variable switch {
                "{a}" => W(vi, "Dữ kiện số thứ nhất", "First numeric fact"), "{b}" => W(vi, "Dữ kiện số thứ hai", "Second numeric fact"),
                "{name}" => W(vi, "Chủ thể thứ nhất", "First owner"), "{other}" => W(vi, "Chủ thể thứ hai", "Second owner"),
                "{unit}" or "{unit_a}" or "{unit_b}" => W(vi, "Đơn vị của lượng", "Quantity unit"),
                "{group}" => W(vi, "Đơn vị nhóm / bao gói", "Group / container unit"),
                _ => W(vi, "Vai trò được đặt tên trong biến; giữ nguyên như ví dụ", "Named role; keep the variable as in the example") };
            guide.Add([aliases.GetValueOrDefault(variable, variable), meaning + " — " + W(vi, "minh họa: ", "example: ") + value]);
        }
        foreach (var field in fields)
            guide.Add([field.Header, W(vi, "Ô bắt buộc. Biến minh họa: ", "Required cell. Example variables: ")
                + string.Join(", ", Variables.Matches(Replace(field.Example, aliases)).Select(m => m.Value))
                + "\n" + Replace(field.Example, aliases)]);
        var examples = new List<IReadOnlyList<string>>();
        var draft = Example(contract);
        var first = new ValidatedBankQuestion(contract, draft, QuestionBankStore.SerializeDraft(draft), "Excel example", DateTime.UtcNow);
        examples.Add([yes, .. fields.Select(f => Replace(f.Example, aliases)), first.WordProblem.ProblemText, Solution(first), PreviewData(first)]);
        var fresh = contract.FreshFacts(new Random(1751));
        var second = new ValidatedBankQuestion(fresh, draft, QuestionBankStore.SerializeDraft(draft), "Excel example", DateTime.UtcNow);
        examples.Add([yes, .. fields.Select(f => Replace(f.Example, aliases)), second.WordProblem.ProblemText, Solution(second), PreviewData(second)]);
        SpreadsheetWorkbook.Write(output, [
            new(W(vi, "Hướng dẫn", "Guide"), SpreadsheetWorkbook.Table([W(vi, "Mục", "Item"), W(vi, "Giải thích", "Explanation")], guide, false)),
            new(InputName(vi), input),
            new(W(vi, "Ví dụ", "Examples"), SpreadsheetWorkbook.Table([.. headers, W(vi, "Đề minh họa", "Rendered example"), W(vi, "Lời giải do app tính", "App-calculated solution"), W(vi, "Dữ liệu minh họa kèm đề", "Supporting example data")], examples))
        ], JsonSerializer.Serialize(metadata));
    }

    public static string Solution(ValidatedBankQuestion question)
    {
        if (question.Contract.Version == ReasoningStoryCatalogue.Version)
            return ReasoningStoryCatalogue.Solution(question.Contract, question.Draft);
        string solution = question.Contract.Solution;
        string standardLead = question.Contract.SolutionLead;
        int start = solution.IndexOf(standardLead, StringComparison.Ordinal);
        return start < 0 ? solution : solution[..start] + question.WordProblem.SolutionLead + solution[(start + standardLead.Length)..];
    }

    public static string Answer(ValidatedBankQuestion question) => question.Contract.AnswerText
        + (question.Contract.Version == ReasoningStoryCatalogue.Version ? "" : " " + question.Contract.AnswerUnit);

    public static string PreviewData(ValidatedBankQuestion question)
    {
        if (question.WordProblem.FactTable is { } table)
            return table.LabelHeader + " · " + table.ValueHeader + "\n" + string.Join("\n", table.Rows.Select(row => row.Label + ": " + row.Value));
        if (question.Contract.Version != ReasoningStoryCatalogue.Version) return "";
        var lesson = ReasoningStoryCatalogue.Lesson(question.Contract);
        var problem = lesson.QuestionModel.ElementaryProblem;
        if (problem?.DataChart is null || problem.Visual is not { } visual) return "";
        bool vi = question.Contract.Language == AppLanguage.Vietnamese;
        string data = string.Join("\n", visual.Labels.Select((label, i) => label + ": "
            + (visual.HiddenValueIndices?.Contains(i) == true ? "?" : visual.Values[i].ToString(System.Globalization.CultureInfo.InvariantCulture)) + " " + visual.Unit));
        if (visual.PictographKey is { } key) data += "\n" + W(vi, "Mỗi biểu tượng = ", "Each symbol = ") + key + " " + visual.Unit;
        if (problem.DataChart.Observations is { } batches) data += "\n" + string.Join("\n", batches.Select((b, i) =>
            W(vi, "Đợt ghi nhận ", "Observation batch ") + (i + 1) + (b.Excluded ? W(vi, " (không tính)", " (excluded)") : "")
            + ": " + string.Join(", ", b.CategoryIds.Select(id => {
                int index = problem.DataChart.Profile.CategoryIds.ToList().IndexOf(id);
                return index >= 0 ? visual.Labels[index] : id;
            }))));
        return data;
    }

    internal static IReadOnlyList<BankWorkbookRow>? TryRead(Stream input, CancellationToken cancellation)
    {
        long original = input.Position;
        try
        {
            using var archive = new ZipArchive(input, ZipArchiveMode.Read, true);
            SpreadsheetWorkbook.CheckArchive(archive);
            if (archive.GetEntry("docProps/custom.xml") is null) return null;
            string? json = SpreadsheetWorkbook.Load(archive, "docProps/custom.xml").Elements()
                .FirstOrDefault(p => (string?)p.Attribute("name") == "MathSolver.Authoring")?.Elements().FirstOrDefault()?.Value;
            if (json is null) return null;
            QuestionAuthoringMetadata metadata;
            try
            {
                metadata = JsonSerializer.Deserialize<QuestionAuthoringMetadata>(json) ?? throw new JsonException();
                if (metadata.Format != 1 || metadata.Contract is null || !metadata.Contract.IsTemplate || !metadata.Contract.IsValid
                    || !Enum.IsDefined(metadata.WorkbookLanguage)) throw new JsonException();
            }
            catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or NullReferenceException)
            { throw new InvalidDataException("ExcelAuthoringSchemaInvalid", error); }
            var c = metadata.Contract;
            bool vi = metadata.WorkbookLanguage == AppLanguage.Vietnamese;
            var fields = Fields(c, vi);
            var aliases = Aliases(c);
            var reverse = aliases.ToDictionary(p => p.Value, p => p.Key);
            string action = W(vi, "Xử lý dòng", "Row action");
            var result = new List<BankWorkbookRow>();
            Dictionary<string, int>? headers = null;
            foreach (var row in SpreadsheetWorkbook.ReadSheet(archive, InputName(vi), cancellation))
            {
                if (headers is null)
                {
                    if (row.Formulas.Count > 0 || row.Cells.Values.Select(v => v.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != row.Cells.Count)
                        throw new InvalidDataException("ExcelColumnsMissing");
                    headers = row.Cells.ToDictionary(p => p.Value.Trim(), p => p.Key, StringComparer.OrdinalIgnoreCase);
                    if (fields.Select(f => f.Header).Append(action).Any(h => !headers.ContainsKey(h))) throw new InvalidDataException("ExcelColumnsMissing");
                    continue;
                }
                string Get(string name) => row.Cells.GetValueOrDefault(headers[name], "").Trim();
                var issues = new List<BankWorkbookIssue>();
                // A formula must never be trusted as a row-action decision, even with a cached Skip value.
                if (row.Formulas.Contains(headers[action])) issues.Add(new(action, "ExcelFormulaNotAllowed"));
                else if (Get(action).Equals(W(vi, "Bỏ qua", "Skip"), StringComparison.OrdinalIgnoreCase)) continue;
                else if (!Get(action).Equals(W(vi, "Nhập", "Import"), StringComparison.OrdinalIgnoreCase)) issues.Add(new(action, "ExcelChooseImport"));
                var values = new Dictionary<string, string>();
                foreach (var field in fields)
                {
                    string text = Replace(Get(field.Header), reverse);
                    values[field.Id] = text;
                    if (row.Formulas.Contains(headers[field.Header])) issues.Add(new(field.Header, "ExcelFormulaNotAllowed"));
                }
                BasicQuestionDraft Build(IReadOnlyDictionary<string, string> texts)
                {
                    var draft = Example(c);
                    string Text(string id, string fallback) => texts.GetValueOrDefault(id, fallback);
                    return draft with {
                        GivenA = Text("given_a", draft.GivenA), GivenB = Text("given_b", draft.GivenB), Question = Text("question", draft.Question),
                        SolutionLead = Text("solution_lead", draft.SolutionLead ?? ""),
                        Facts = draft.Facts?.Select(f => f with { Text = Text("facts." + f.Role, f.Text) }).ToArray(),
                        SolutionLeads = draft.SolutionLeads?.Select(s => s with { Text = Text("solution_leads." + s.Role, s.Text) }).ToArray()
                    };
                }
                var draft = Build(values);
                foreach (var issue in UserQuestionTemplateValidator.Inspect(draft, c))
                    issues.Add(new(fields.FirstOrDefault(f => f.Id == issue.Field)?.Header ?? W(vi, "Toàn bộ đề", "Whole question"),
                        issue.ErrorCode, issue.Variable is null ? null : aliases.GetValueOrDefault(issue.Variable, issue.Variable)));
                result.Add(new(row.Number, issues.Count > 0 ? null : new(c, draft, QuestionBankStore.SerializeDraft(draft), "Excel authoring", DateTime.UtcNow) { UserAuthored = true },
                    issues.FirstOrDefault()?.ErrorCode) { Issues = issues });
            }
            if (headers is null) throw new InvalidDataException("ExcelColumnsMissing");
            return result;
        }
        finally { input.Position = original; }
    }
}
