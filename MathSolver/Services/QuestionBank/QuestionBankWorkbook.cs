using MathSolver.Models;
using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Text.Json;
using System.Xml.Linq;

namespace MathSolver.Services.QuestionBank;

public sealed record BankWorkbookIssue(string Column, string ErrorCode, string? Details = null);
public sealed record BankWorkbookRow(int RowNumber, ValidatedBankQuestion? Question, string? ErrorCode)
{
    public IReadOnlyList<BankWorkbookIssue> Issues { get; init; } = [];
}

/// <summary>A small SpreadsheetML interchange format using ZIP/XML, with no Excel installation or native dependencies.</summary>
public static partial class QuestionBankWorkbook
{
    public const long MaxImportBytes = 20 * 1024 * 1024;
    public const int MaxImportRows = 10000;
    private const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Ns = Main;
    public static IReadOnlyList<string> Columns { get; } = Array.AsReadOnly(new[]
    {
        "Version", "Operation", "Stars", "Language", "Left", "Right", "Subject", "Unit", "GroupUnit",
        "GivenA", "GivenB", "Question", "ModelName", "CreatedUtc", "RawJson", "Answer", "ProblemText",
        "Structure", "OtherSubject", "SolutionLead", "UnitId", "TopicId", "SceneId", "PartA", "PartB", "Grade", "KnowledgeGroup", "UnknownRole", "LeftDenominator", "RightDenominator", "StorySeedJson", "FactsJson", "SolutionLeadsJson", "UserAuthored"
    });

    public static int Write(Stream output, IEnumerable<ValidatedBankQuestion> questions, CancellationToken cancellationToken = default)
    {
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, true);
        void Part(string name, string text)
        {
            using var target = archive.CreateEntry(name).Open();
            using var writer = new StreamWriter(target, new System.Text.UTF8Encoding(false));
            writer.Write(text);
        }
        Part("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
        Part("_rels/.rels", $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"{Rel}/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        Part("xl/workbook.xml", $"<workbook xmlns=\"{Main}\" xmlns:r=\"{Rel}\"><sheets><sheet name=\"Questions\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
        Part("xl/_rels/workbook.xml.rels", $"<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"{Rel}/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
        using var sheet = archive.CreateEntry("xl/worksheets/sheet1.xml", CompressionLevel.Fastest).Open();
        using var xml = XmlWriter.Create(sheet, new() { Encoding = new System.Text.UTF8Encoding(false), CloseOutput = false });
        xml.WriteStartDocument();
        xml.WriteStartElement("worksheet", Main);
        xml.WriteStartElement("sheetViews", Main);
        xml.WriteStartElement("sheetView", Main); xml.WriteAttributeString("workbookViewId", "0");
        xml.WriteStartElement("pane", Main); xml.WriteAttributeString("ySplit", "1"); xml.WriteAttributeString("topLeftCell", "A2"); xml.WriteAttributeString("activePane", "bottomLeft"); xml.WriteAttributeString("state", "frozen"); xml.WriteEndElement();
        xml.WriteEndElement(); xml.WriteEndElement();
        xml.WriteStartElement("cols", Main);
        for (int i = 0; i < Columns.Count; i++)
        {
            xml.WriteStartElement("col", Main);
            xml.WriteAttributeString("min", (i + 1).ToString(CultureInfo.InvariantCulture)); xml.WriteAttributeString("max", (i + 1).ToString(CultureInfo.InvariantCulture));
            xml.WriteAttributeString("width", i is >= 9 and <= 11 or 14 or 16 ? "60" : "22"); xml.WriteAttributeString("customWidth", "1"); xml.WriteEndElement();
        }
        xml.WriteEndElement();
        xml.WriteStartElement("sheetData", Main);
        int rowNumber = 1;
        void Row(IEnumerable<string> values)
        {
            xml.WriteStartElement("row", Main); xml.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
            int column = 0;
            foreach (string value in values)
            {
                if (value.Length > 32767) throw new InvalidDataException("ExcelCellTooLong");
                xml.WriteStartElement("c", Main); xml.WriteAttributeString("r", ColumnName(column++) + rowNumber); xml.WriteAttributeString("t", "inlineStr");
                xml.WriteStartElement("is", Main); xml.WriteStartElement("t", Main); xml.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
                xml.WriteString(value); xml.WriteEndElement(); xml.WriteEndElement(); xml.WriteEndElement();
            }
            xml.WriteEndElement(); rowNumber++;
        }
        Row(Columns);
        foreach (var question in questions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (rowNumber > 1048576) throw new InvalidDataException("ExcelRowLimit");
            var c = question.Contract;
            Row([c.Version.ToString(CultureInfo.InvariantCulture), c.Operation.ToString(), ((int)c.Tier).ToString(CultureInfo.InvariantCulture),
                c.Language == AppLanguage.Vietnamese ? "vi-VN" : "en-US", c.Left.ToString(CultureInfo.InvariantCulture), c.Right.ToString(CultureInfo.InvariantCulture),
                c.Subject, c.Unit, c.GroupUnit, question.Draft.GivenA, question.Draft.GivenB, question.Draft.Question, question.ModelName,
                DateTime.SpecifyKind(question.CreatedUtc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture), question.RawJson,
                c.AnswerText, question.WordProblem.ProblemText,
                c.IsTemplate ? c.Structure.ToString() : "", c.OtherSubject, question.Draft.SolutionLead ?? "", question.Draft.UnitId ?? "",
                c.TopicId, c.SceneId, c.PartA, c.PartB, c.Grade.ToString(CultureInfo.InvariantCulture), c.KnowledgeGroup.ToString(), c.UnknownRole.ToString(), c.LeftDenominator.ToString(CultureInfo.InvariantCulture), c.RightDenominator.ToString(CultureInfo.InvariantCulture),
                c.Story is null ? "" : JsonSerializer.Serialize(c.Story),
                question.Draft.Facts is null ? "" : JsonSerializer.Serialize(question.Draft.Facts),
                question.Draft.SolutionLeads is null ? "" : JsonSerializer.Serialize(question.Draft.SolutionLeads),
                question.UserAuthored ? "true" : "false"]);
        }
        xml.WriteEndElement();
        xml.WriteStartElement("autoFilter", Main); xml.WriteAttributeString("ref", $"A1:{ColumnName(Columns.Count - 1)}{rowNumber - 1}"); xml.WriteEndElement();
        xml.WriteEndElement(); xml.WriteEndDocument();
        return rowNumber - 2;
    }

    public static IReadOnlyList<BankWorkbookRow> Read(Stream input, CancellationToken cancellationToken = default)
    {
        if (!input.CanSeek || input.Length > MaxImportBytes) throw new InvalidDataException("ExcelFileTooLarge");
        if (QuestionAuthoringWorkbook.TryRead(input, cancellationToken) is { } authored) return authored;
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, true);
        if (archive.Entries.Sum(e => e.Length) > 40 * 1024 * 1024 || archive.Entries.Count > 1000) throw new InvalidDataException("ExcelFileTooLarge");
        XmlReader Reader(string path)
        {
            var entry = archive.GetEntry(path) ?? throw new InvalidDataException("InvalidExcelFile");
            return XmlReader.Create(entry.Open(), new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = 40 * 1024 * 1024, CloseInput = true });
        }
        XElement Document(string path) { using var reader = Reader(path); return XElement.Load(reader); }
        string Target(XElement relationship)
        {
            if ((string?)relationship.Attribute("TargetMode") == "External") throw new InvalidDataException("InvalidExcelFile");
            var resolved = new Uri(new Uri("https://workbook.invalid/xl/workbook.xml"), (string?)relationship.Attribute("Target") ?? "");
            if (resolved.Host != "workbook.invalid") throw new InvalidDataException("InvalidExcelFile");
            return Uri.UnescapeDataString(resolved.AbsolutePath).TrimStart('/');
        }
        var workbook = Document("xl/workbook.xml");
        var sheets = workbook.Descendants(Ns + "sheet").ToArray();
        var selected = sheets.FirstOrDefault(s => (string?)s.Attribute("name") == "Questions") ?? sheets.FirstOrDefault()
            ?? throw new InvalidDataException("InvalidExcelFile");
        var relationships = Document("xl/_rels/workbook.xml.rels").Elements().ToArray();
        var sheetRelationship = relationships.FirstOrDefault(r => (string?)r.Attribute("Id") == (string?)selected.Attribute(XName.Get("id", Rel)))
            ?? throw new InvalidDataException("InvalidExcelFile");
        string Text(XElement node) => string.Concat(node.Descendants(Ns + "t").Where(t => !t.Ancestors(Ns + "rPh").Any()).Select(t => t.Value));
        var sharedStrings = new List<string>();
        var shared = relationships.FirstOrDefault(r => ((string?)r.Attribute("Type"))?.EndsWith("/sharedStrings", StringComparison.Ordinal) == true);
        if (shared is not null)
        {
            using var reader = Reader(Target(shared));
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "si" || reader.NamespaceURI != Main) continue;
                using var subtree = reader.ReadSubtree();
                sharedStrings.Add(Text(XElement.Load(subtree)));
            }
        }
        var result = new List<BankWorkbookRow>();
        Dictionary<int, string>? header = null;
        using var sheetReader = Reader(Target(sheetRelationship));
        while (sheetReader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sheetReader.NodeType != XmlNodeType.Element || sheetReader.LocalName != "row" || sheetReader.NamespaceURI != Main) continue;
            using var subtree = sheetReader.ReadSubtree();
            var row = XElement.Load(subtree);
            int number = (int?)row.Attribute("r") ?? result.Count + 2;
            var cells = new Dictionary<int, string>();
            bool formula = false;
            string formulaColumn = "";
            foreach (var cell in row.Elements(Ns + "c"))
            {
                string reference = (string?)cell.Attribute("r") ?? throw new InvalidDataException("InvalidExcelFile");
                int column = 0;
                foreach (char character in reference.TakeWhile(char.IsLetter)) column = checked(column * 26 + char.ToUpperInvariant(character) - 'A' + 1);
                if (column is < 1 or > 128 || cells.ContainsKey(column - 1)) throw new InvalidDataException("InvalidExcelFile");
                string type = (string?)cell.Attribute("t") ?? "n";
                string value = cell.Element(Ns + "v")?.Value ?? "";
                if (type == "s")
                {
                    if (!int.TryParse(value, out int index) || index < 0 || index >= sharedStrings.Count) throw new InvalidDataException("InvalidExcelFile");
                    value = sharedStrings[index];
                }
                else if (type == "inlineStr") value = cell.Element(Ns + "is") is { } inline ? Text(inline) : "";
                if (value.Length > 32767) throw new InvalidDataException("ExcelCellTooLong");
                cells[column - 1] = value;
                if (cell.Element(Ns + "f") is not null && header?.GetValueOrDefault(column - 1)?.ToLowerInvariant() is not ("answer" or "problemtext"))
                {
                    formula = true;
                    if (formulaColumn.Length == 0) formulaColumn = header?.GetValueOrDefault(column - 1) ?? reference;
                }
            }
            if (cells.Values.All(string.IsNullOrWhiteSpace)) continue;
            if (header is null)
            {
                header = cells.ToDictionary(c => c.Key, c => c.Value.Trim());
                if (formula || header.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != header.Count
                    || Columns.Take(12).Any(required => !header.Values.Contains(required, StringComparer.OrdinalIgnoreCase)))
                    throw new InvalidDataException("ExcelColumnsMissing");
                continue;
            }
            if (result.Count >= MaxImportRows) throw new InvalidDataException("ExcelImportRowLimit");
            var values = header.ToDictionary(h => h.Value, h => cells.GetValueOrDefault(h.Key, ""), StringComparer.OrdinalIgnoreCase);
            if (formula)
            {
                result.Add(new(number, null, "ExcelFormulaNotAllowed") { Issues = [new(formulaColumn, "ExcelFormulaNotAllowed")] });
                continue;
            }
            string readingColumn = "";
            try
            {
                string Get(string key) { readingColumn = key; return values.GetValueOrDefault(key, ""); }
                int Integer(string key)
                {
                    decimal value = decimal.Parse(Get(key), NumberStyles.Float, CultureInfo.InvariantCulture);
                    if (value != decimal.Truncate(value)) throw new FormatException();
                    return checked((int)value);
                }
                var language = Get("Language") switch { "vi-VN" => AppLanguage.Vietnamese, "en-US" => AppLanguage.English, _ => throw new FormatException() };
                var contract = new BasicQuestionContract(Integer("Version"), Enum.Parse<ArithmeticOperation>(Get("Operation"), true),
                    (CurriculumTier)Integer("Stars"), language, Integer("Left"), Integer("Right"), Get("Subject"), Get("Unit"), Get("GroupUnit"),
                    Integer("Version") == 1 ? BasicQuestionStructure.Increase : Enum.Parse<BasicQuestionStructure>(Get("Structure"), true), Get("OtherSubject"),
                    Get("TopicId"), Get("SceneId"), Get("PartA"), Get("PartB"),
                    string.IsNullOrWhiteSpace(Get("Grade")) ? 0 : Integer("Grade"),
                    string.IsNullOrWhiteSpace(Get("KnowledgeGroup")) ? QuestionKnowledgeGroup.Objects
                        : Enum.Parse<QuestionKnowledgeGroup>(Get("KnowledgeGroup"), true),
                    string.IsNullOrWhiteSpace(Get("UnknownRole")) ? FindXUnknownRole.None : Enum.Parse<FindXUnknownRole>(Get("UnknownRole"), true),
                    string.IsNullOrWhiteSpace(Get("LeftDenominator")) ? 1 : int.Parse(Get("LeftDenominator"), CultureInfo.InvariantCulture),
                    string.IsNullOrWhiteSpace(Get("RightDenominator")) ? 1 : int.Parse(Get("RightDenominator"), CultureInfo.InvariantCulture));
                if (contract.Version == ReasoningStoryCatalogue.Version)
                    contract = contract with { Story = JsonSerializer.Deserialize<ReasoningStorySeed>(Get("StorySeedJson")) };
                var draft = new BasicQuestionDraft(Get("GivenA"), Get("GivenB"), Get("Question"),
                    contract.IsTemplate ? Get("SolutionLead") : null, contract.IsTemplate ? Get("UnitId") : null);
                if (contract.Version == ReasoningStoryCatalogue.Version)
                    draft = draft with {
                        Facts = JsonSerializer.Deserialize<NarrativeClause[]>(Get("FactsJson")),
                        SolutionLeads = JsonSerializer.Deserialize<NarrativeClause[]>(Get("SolutionLeadsJson"))
                    };
                if (draft.Facts?.Any(f => f is null) == true || draft.SolutionLeads?.Any(s => s is null) == true)
                    throw new FormatException("Invalid narrative clause");
                string json = QuestionBankStore.SerializeDraft(draft);
                // New backups preserve provenance. Historical template files are user imports.
                bool userAuthored = contract.IsTemplate && (string.IsNullOrWhiteSpace(Get("UserAuthored")) || bool.Parse(Get("UserAuthored")));
                var validation = QuestionBankStore.ValidateDraft(json, contract, userAuthored);
                if (!validation.IsValid)
                {
                    result.Add(new(number, null, validation.ErrorCode) { Issues = [new(validation.ErrorDetails ?? "GivenA / GivenB / Question", validation.ErrorCode!)] });
                    continue;
                }
                if (validation.Contract is { } resolved && resolved != contract)
                { result.Add(new(number, null, "ChangedUnits")); continue; }
                DateTime created = string.IsNullOrWhiteSpace(Get("CreatedUtc")) ? DateTime.UtcNow
                    : DateTime.Parse(Get("CreatedUtc"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                result.Add(new(number, new(contract, validation.Draft!, string.IsNullOrWhiteSpace(Get("RawJson")) ? json : Get("RawJson"),
                    string.IsNullOrWhiteSpace(Get("ModelName")) ? "Excel import" : Get("ModelName"), created) { UserAuthored = userAuthored }, null));
            }
            catch (Exception error) when (error is FormatException or OverflowException or ArgumentException or JsonException)
            { result.Add(new(number, null, "InvalidExcelRow") { Issues = [new(readingColumn, "InvalidExcelRow")] }); }
        }
        if (header is null) throw new InvalidDataException("ExcelColumnsMissing");
        return result;
    }

    private static string ColumnName(int index)
    {
        string name = "";
        for (index++; index > 0; index = (index - 1) / 26) name = (char)('A' + (index - 1) % 26) + name;
        return name;
    }
}
