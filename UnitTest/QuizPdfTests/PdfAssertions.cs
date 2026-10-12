using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Content;
using PdfSharp.Pdf.Content.Objects;

internal static class PdfAssertions
{
    internal static void HasEmbeddedText(byte[] bytes)
    {
        using var pdf = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        Check(pdf.PageCount > 0 && pdf.Pages.Cast<PdfPage>().All(p => p.Resources.Elements.GetDictionary("/Font")?.Elements.Count > 0),
            "PDF page has no font resources.");
        var objects = pdf.Internals.GetAllObjects().OfType<PdfDictionary>().ToArray();
        Check(objects.Any(d => d.Elements.ContainsKey("/FontFile2")), "PDF did not embed a TrueType font.");
        // WinAnsi-only English pages legitimately have no CID font. Unicode
        // mapping is required for each composite font, not for simple fonts.
        Check(objects.Where(d => d.Elements.GetName("/Subtype") == "/Type0")
            .All(d => d.Elements.ContainsKey("/ToUnicode")), "PDF lost Unicode text mapping.");
        Check(pdf.Pages.Cast<PdfPage>().All(p => ContentReader.ReadContent(p).OfType<COperator>()
            .Any(op => op.OpCode.Name is "Tj" or "TJ" && op.Operands.Count > 0)), "PDF page has no text drawing operators.");
    }
}
