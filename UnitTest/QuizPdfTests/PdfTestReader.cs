using PdfSharp.Pdf;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;
using PdfSharp.Pdf.IO;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

internal static class PdfTestReader
{
    internal static string ExtractText(byte[] bytes)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var pdf = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        var result = new StringBuilder();
        foreach (PdfPage page in pdf.Pages)
        {
            var fonts = page.Resources.Elements.GetDictionary("/Font")!;
            var maps = new Dictionary<string, Dictionary<int, char>?>();
            foreach (string name in fonts.Elements.Keys)
            {
                var font = fonts.Elements.GetDictionary(name)!;
                // Simple fonts use single-byte character codes even when they have
                // a ToUnicode map. Only Type0 fonts use the two-byte CID path below.
                if (font.Elements.GetName("/Subtype") != "/Type0") { maps[name] = null; continue; }
                var unicodeMap = font.Elements.GetDictionary("/ToUnicode");
                if (unicodeMap is null) { maps[name] = null; continue; }
                string cmap = Encoding.ASCII.GetString(unicodeMap.Stream.UnfilteredValue);
                var map = new Dictionary<int, char>();
                foreach (Match range in Regex.Matches(cmap, @"<([0-9A-Fa-f]{4})>\s*<([0-9A-Fa-f]{4})>\s*<([0-9A-Fa-f]{4})>"))
                {
                    int start = int.Parse(range.Groups[1].Value, NumberStyles.HexNumber), end = int.Parse(range.Groups[2].Value, NumberStyles.HexNumber);
                    int unicode = int.Parse(range.Groups[3].Value, NumberStyles.HexNumber);
                    for (int cid = start; cid <= end; cid++) map[cid] = (char)(unicode + cid - start);
                }
                maps[name] = map;
            }
            string currentFont = "";
            foreach (var op in ContentReader.ReadContent(page).OfType<COperator>())
            {
                if (op.OpCode.Name == "Tf") currentFont = ((CName)op.Operands[0]).Name;
                if (op.OpCode.Name != "Tj" || op.Operands[0] is not CString content) continue;
                if (maps[currentFont] is not { } map)
                {
                    result.Append(Encoding.GetEncoding(1252).GetString(content.Value.Select(c => (byte)c).ToArray()));
                    continue;
                }
                for (int i = 0; i + 1 < content.Value.Length; i += 2)
                {
                    int code = (content.Value[i] << 8) | content.Value[i + 1];
                    if (!map.TryGetValue(code, out char character))
                        throw new InvalidDataException($"Unmapped PDF CID {code:x4}; font={currentFont}; string type={content.CStringType}; text={content.Value}");
                    result.Append(character);
                }
            }
        }
        return result.ToString();
    }
}
