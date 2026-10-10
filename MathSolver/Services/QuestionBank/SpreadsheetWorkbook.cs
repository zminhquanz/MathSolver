using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace MathSolver.Services.QuestionBank;

internal sealed record SpreadsheetSheet(string Name, XElement Xml, bool Hidden = false);
internal sealed record SpreadsheetRow(int Number, IReadOnlyDictionary<int, string> Cells, IReadOnlySet<int> Formulas);

/// <summary>Bounded SpreadsheetML IO shared by authoring files and bank backups.</summary>
internal static class SpreadsheetWorkbook
{
    internal static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    internal const string Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Package = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace Types = "http://schemas.openxmlformats.org/package/2006/content-types";
    internal static XElement Load(ZipArchive archive, string path)
    {
        var part = archive.GetEntry(path) ?? throw new InvalidDataException("InvalidExcelFile");
        using var reader = XmlReader.Create(part.Open(), new() { DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null, MaxCharactersInDocument = 40 * 1024 * 1024, CloseInput = true });
        return XElement.Load(reader);
    }

    internal static void CheckArchive(ZipArchive archive)
    {
        if (archive.Entries.Count > 1000 || archive.Entries.Sum(e => e.Length) > 40 * 1024 * 1024)
            throw new InvalidDataException("ExcelFileTooLarge");
    }

    internal static string Target(XElement relationship, string owner = "xl/workbook.xml")
    {
        if ((string?)relationship.Attribute("TargetMode") == "External") throw new InvalidDataException("InvalidExcelFile");
        var resolved = new Uri(new Uri("https://workbook.invalid/" + owner), (string?)relationship.Attribute("Target") ?? "");
        if (resolved.Host != "workbook.invalid") throw new InvalidDataException("InvalidExcelFile");
        return Uri.UnescapeDataString(resolved.AbsolutePath).TrimStart('/');
    }

    internal static IEnumerable<SpreadsheetRow> ReadSheet(ZipArchive archive, string name, CancellationToken cancellation)
    {
        var workbook = Load(archive, "xl/workbook.xml");
        var sheet = workbook.Descendants(Ns + "sheet").SingleOrDefault(s => (string?)s.Attribute("name") == name)
            ?? throw new InvalidDataException("ExcelColumnsMissing");
        var relationships = Load(archive, "xl/_rels/workbook.xml.rels").Elements().ToArray();
        var matching = relationships.Where(r => (string?)r.Attribute("Id") == (string?)sheet.Attribute(XName.Get("id", Relationships))).ToArray();
        if (matching.Length != 1) throw new InvalidDataException("InvalidExcelFile");
        var relationship = matching[0];
        string Text(XElement cell) => string.Concat(cell.Descendants(Ns + "t")
            .Where(t => !t.Ancestors(Ns + "rPh").Any()).Select(t => t.Value));
        var shared = relationships.FirstOrDefault(r => ((string?)r.Attribute("Type"))?.EndsWith("/sharedStrings", StringComparison.Ordinal) == true);
        string[] strings = shared is null ? [] : Load(archive, Target(shared)).Elements(Ns + "si").Select(Text).ToArray();
        var entry = archive.GetEntry(Target(relationship)) ?? throw new InvalidDataException("InvalidExcelFile");
        using var reader = XmlReader.Create(entry.Open(), new() { DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null, MaxCharactersInDocument = 40 * 1024 * 1024, CloseInput = true });
        int count = 0, nonempty = 0;
        while (reader.Read())
        {
            cancellation.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "row" || reader.NamespaceURI != Ns.NamespaceName) continue;
            count++;
            using var subtree = reader.ReadSubtree();
            var row = XElement.Load(subtree);
            var cells = new Dictionary<int, string>();
            var formulas = new HashSet<int>();
            foreach (var cell in row.Elements(Ns + "c"))
            {
                string reference = (string?)cell.Attribute("r") ?? throw new InvalidDataException("InvalidExcelFile");
                int column = 0;
                foreach (char ch in reference.TakeWhile(char.IsLetter)) column = checked(column * 26 + char.ToUpperInvariant(ch) - 'A' + 1);
                if (column is < 1 or > 128 || cells.ContainsKey(column - 1)) throw new InvalidDataException("InvalidExcelFile");
                string value = (string?)cell.Element(Ns + "v") ?? "";
                string type = (string?)cell.Attribute("t") ?? "n";
                if (type == "inlineStr") value = Text(cell);
                if (type == "s")
                {
                    if (!int.TryParse(value, out int index) || index < 0 || index >= strings.Length) throw new InvalidDataException("InvalidExcelFile");
                    value = strings[index];
                }
                if (value.Length > 32767) throw new InvalidDataException("ExcelCellTooLong");
                cells.Add(column - 1, value);
                if (cell.Element(Ns + "f") is not null) formulas.Add(column - 1);
            }
            if (cells.Values.Any(v => !string.IsNullOrWhiteSpace(v)) || formulas.Count > 0)
            {
                if (++nonempty > QuestionBankWorkbook.MaxImportRows + 1) throw new InvalidDataException("ExcelImportRowLimit");
                yield return new((int?)row.Attribute("r") ?? count, cells, formulas);
            }
        }
    }

    internal static string Column(int index)
    {
        string result = "";
        for (index++; index > 0; index = (index - 1) / 26) result = (char)('A' + (index - 1) % 26) + result;
        return result;
    }

    internal static XElement Table(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows, bool filter = true)
    {
        var data = new XElement(Ns + "sheetData");
        int number = 1;
        foreach (var row in rows.Prepend(headers))
        {
            if (number > 1048576) throw new InvalidDataException("ExcelRowLimit");
            data.Add(new XElement(Ns + "row", new XAttribute("r", number), row.Select((value, i) => {
                if (value.Length > 32767) throw new InvalidDataException("ExcelCellTooLong");
                return new XElement(Ns + "c", new XAttribute("r", Column(i) + number), new XAttribute("t", "inlineStr"),
                    new XAttribute("s", number == 1 ? 1 : 2), new XElement(Ns + "is", new XElement(Ns + "t",
                        new XAttribute(XNamespace.Xml + "space", "preserve"), value)));
            })));
            number++;
        }
        return new(Ns + "worksheet",
            new XElement(Ns + "sheetViews", new XElement(Ns + "sheetView", new XAttribute("workbookViewId", 0),
                new XElement(Ns + "pane", new XAttribute("ySplit", 1), new XAttribute("topLeftCell", "A2"),
                    new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")))),
            new XElement(Ns + "cols", headers.Select((h, i) => new XElement(Ns + "col", new XAttribute("min", i + 1),
                new XAttribute("max", i + 1), new XAttribute("width", i == 0 ? 24 : 60), new XAttribute("customWidth", 1)))),
            data, filter ? new XElement(Ns + "autoFilter", new XAttribute("ref", "A1:" + Column(headers.Count - 1) + Math.Max(1, number - 1))) : null);
    }

    internal static void Write(Stream output, IReadOnlyList<SpreadsheetSheet> sheets, string? authoringMetadata = null)
    {
        using var archive = new ZipArchive(output, ZipArchiveMode.Create, true);
        void Part(string path, string text)
        {
            using var writer = new StreamWriter(archive.CreateEntry(path, CompressionLevel.Fastest).Open(), new UTF8Encoding(false));
            writer.Write(text);
        }
        var types = new XElement(Types + "Types",
            new XElement(Types + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
            new XElement(Types + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")));
        void Override(string path, string type) => types.Add(new XElement(Types + "Override", new XAttribute("PartName", "/" + path), new XAttribute("ContentType", type)));
        Override("xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
        Override("xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
        var rootRelationships = new XElement(Package + "Relationships", new XElement(Package + "Relationship", new XAttribute("Id", "workbook"),
            new XAttribute("Type", Relationships + "/officeDocument"), new XAttribute("Target", "xl/workbook.xml")));
        if (authoringMetadata is not null)
        {
            const string custom = "http://schemas.openxmlformats.org/officeDocument/2006/custom-properties";
            XNamespace vt = "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes";
            Part("docProps/custom.xml", new XElement(XName.Get("Properties", custom), new XAttribute(XNamespace.Xmlns + "vt", vt),
                new XElement(XName.Get("property", custom), new XAttribute("fmtid", "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}"),
                    new XAttribute("pid", 2), new XAttribute("name", "MathSolver.Authoring"), new XElement(vt + "lpwstr", authoringMetadata))).ToString());
            Override("docProps/custom.xml", "application/vnd.openxmlformats-officedocument.custom-properties+xml");
            rootRelationships.Add(new XElement(Package + "Relationship", new XAttribute("Id", "authoring"),
                new XAttribute("Type", Relationships + "/custom-properties"), new XAttribute("Target", "docProps/custom.xml")));
        }
        var workbook = new XElement(Ns + "workbook", new XAttribute(XNamespace.Xmlns + "r", Relationships), new XElement(Ns + "sheets"));
        var rels = new XElement(Package + "Relationships");
        for (int i = 0; i < sheets.Count; i++)
        {
            string id = "sheet" + (i + 1), path = "xl/worksheets/sheet" + (i + 1) + ".xml";
            var sheet = sheets[i];
            workbook.Element(Ns + "sheets")!.Add(new XElement(Ns + "sheet", new XAttribute("name", sheet.Name),
                new XAttribute("sheetId", i + 1), new XAttribute(XName.Get("id", Relationships), id),
                sheet.Hidden ? new XAttribute("state", "hidden") : null));
            rels.Add(new XElement(Package + "Relationship", new XAttribute("Id", id), new XAttribute("Type", Relationships + "/worksheet"),
                new XAttribute("Target", "worksheets/sheet" + (i + 1) + ".xml")));
            Part(path, sheet.Xml.ToString());
            Override(path, "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
        }
        rels.Add(new XElement(Package + "Relationship", new XAttribute("Id", "styles"), new XAttribute("Type", Relationships + "/styles"), new XAttribute("Target", "styles.xml")));
        Part("xl/styles.xml", "<styleSheet xmlns=\"" + Ns + "\"><fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills><borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"3\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"49\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyAlignment=\"1\"><alignment vertical=\"top\" wrapText=\"1\"/></xf><xf numFmtId=\"49\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyAlignment=\"1\"><alignment vertical=\"top\" wrapText=\"1\"/></xf></cellXfs></styleSheet>");
        Part("[Content_Types].xml", types.ToString());
        Part("_rels/.rels", rootRelationships.ToString());
        Part("xl/workbook.xml", workbook.ToString());
        Part("xl/_rels/workbook.xml.rels", rels.ToString());
    }
}
