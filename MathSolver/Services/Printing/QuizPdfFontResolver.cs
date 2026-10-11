using PdfSharp.Fonts;

namespace MathSolver.Services.Printing;

internal sealed class QuizPdfFontResolver : IFontResolver
{
    private static readonly object Gate = new();
    internal static void Initialize()
    {
        lock (Gate) GlobalFontSettings.FontResolver ??= new QuizPdfFontResolver();
    }
    public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) => new(bold ? "QuizPdf.Bold.ttf" : "QuizPdf.Regular.ttf");
    public byte[] GetFont(string faceName)
    {
        using var stream = typeof(QuizPdfFontResolver).Assembly.GetManifestResourceStream(faceName)
            ?? throw new InvalidOperationException("Missing embedded print font.");
        using var output = new MemoryStream(); stream.CopyTo(output); return output.ToArray();
    }
}
