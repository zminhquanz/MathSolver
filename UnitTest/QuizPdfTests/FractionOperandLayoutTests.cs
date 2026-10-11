using MathSolver.Services.Printing;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

internal static class FractionOperandLayoutTests
{
    internal static void Run(string directory)
    {
        QuizPdfFontResolver.Initialize();
        using var document = new PdfDocument();
        var page = document.AddPage();
        using (var graphics = XGraphics.FromPdfPage(page))
        {
            var font = new XFont("QuizPrint", 12);
            double simpleHeight = QuizPdfText.Wrap(graphics, "3/4", font, 450).Single().Height;
            double y = 50;
            foreach (string text in new[]
            {
                "3/4 × 2/5 = ?", "3/4 ÷ 2/5 = ?", "2/3 × 2/3 = 4/9",
                "3 / 4 × 2 / 5 = 3/10", "2 1/3 × 2/5 = 14/15", "2 1/3 ÷ 2/5 = 35/6"
            })
            {
                var line = QuizPdfText.Wrap(graphics, text, font, 450).Single();
                // Two simple fractions sit side by side, at the same height as
                // one fraction. The original nested layout is taller.
                if (Math.Abs(line.Height - simpleHeight) > .01)
                    throw new Exception("Fraction product/quotient rendered as a nested fraction: " + text);
                QuizPdfText.DrawLine(graphics, line, 50, y); y += line.Height + 24;
            }
            var compound = QuizPdfText.Wrap(graphics, "(1/2 + 1/3)/(2/5)", font, 450).Single();
            if (compound.Height <= simpleHeight)
                throw new Exception("Explicit compound fraction lost its stacked numerator/denominator.");
            QuizPdfText.DrawLine(graphics, compound, 50, y);
        }
        document.Save(Path.Combine(directory, "textbook-fraction-products.pdf"));
        Console.WriteLine("PASS textbook fraction PDF layout: products, quotients, mixed numbers and explicit compound fractions.");
    }
}
