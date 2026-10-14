using MathSolver.Graphics;
using MathSolver.Models;
using Microsoft.Maui.Graphics;
using PdfSharp;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System.Text.RegularExpressions;

namespace MathSolver.Services.Printing;

public static class QuizPdfWriter
{
    /// <summary>No MAUI view capture, no model inference. Embedded Unicode fonts and vector diagrams.</summary>
    public static byte[] Write(QuizWorksheet worksheet, bool answers, CancellationToken cancellation = default)
    {
        QuizWorksheetBuilder.ValidateCount(worksheet.Questions.Count);
        QuizPdfFontResolver.Initialize();
        using var palette = ThemeResource.UsePrintPalette();
        using var document = new PdfDocument();
        document.Info.Title = worksheet.Title + (answers ? " - Answers" : "");
        document.Info.Author = "Math Solver";
        var writer = new Layout(document, worksheet, answers, cancellation);
        writer.Render();
        using var output = new MemoryStream(); document.Save(output, false); return output.ToArray();
    }
    private sealed class Layout(PdfDocument document, QuizWorksheet sheet, bool answers, CancellationToken cancellation)
    {
        private const double Margin = 42, Width = 511, Bottom = 790;
        private XGraphics _g = null!;
        private double _y;
        private int _page;
        private readonly XFont _font = new("QuizPrint", 12);
        private readonly XFont _bold = new("QuizPrint", 12, XFontStyleEx.Bold);
        private bool Vi => sheet.Language == AppLanguage.Vietnamese;
        private string T(string vi, string en) => Vi ? vi : en;
        private void NewPage()
        {
            _g?.Dispose(); cancellation.ThrowIfCancellationRequested();
            var page = document.AddPage(); page.Size = PageSize.A4; _page++;
            _g = XGraphics.FromPdfPage(page); _y = Margin;
            _g.DrawString($"Math Solver · {sheet.Id} · " + T("Trang ", "Page ") + _page, new XFont("QuizPrint", 9), XBrushes.Gray,
                new XRect(Margin, 810, Width, 16), XStringFormats.TopRight);
            if (_page > 1) Text(sheet.Title + (answers ? " · " + T("Đáp án", "Answers") : ""), _bold);
        }
        private void Ensure(double height) { if (_y + height > Bottom) NewPage(); }
        private void Text(string value, XFont? font = null)
        {
            foreach (var line in QuizPdfText.Wrap(_g, value, font ?? _font, Width))
            { Ensure(line.Height); QuizPdfText.DrawLine(_g, line, Margin, _y); _y += line.Height; }
        }
        internal void Render()
        {
            NewPage();
            Text(sheet.Title, new XFont("QuizPrint", 18, XFontStyleEx.Bold));
            Text(answers ? T("ĐÁP ÁN VÀ LỜI GIẢI", "ANSWERS AND SOLUTIONS") : T("PHIẾU LUYỆN TẬP", "PRACTICE WORKSHEET"), _bold);
            string selection = Regex.Replace(sheet.Selection, "★+", match => match.Length + T(" sao", " stars"));
            selection = Regex.Replace(selection, @"[^\p{L}\p{N}\p{P}\p{Z}\p{Sm}]", "").Trim();
            Text(selection, new XFont("QuizPrint", 10));
            Text($"{sheet.Questions.Count} " + T("câu · ", "questions · ") + (int)sheet.Tier + T(" sao", " stars"), new XFont("QuizPrint", 10));
            if (!answers) Text(T("Họ và tên: ........................................  Ngày: ....................", "Name: ........................................  Date: ...................."));
            _y += 12;
            for (int index = 0; index < sheet.Questions.Count; index++)
            {
                cancellation.ThrowIfCancellationRequested(); var item = sheet.Questions[index];
                double visualHeight = item.Visual is { Kind: "table" } tableVisual ? tableVisual.Labels.Count * 48 + 12
                    : item.Visual is { Kind: "pie" } pie ? 280 + pie.Labels.Count * 17
                    : item.Visual is { Kind: "pictograph" } pic ? Math.Max(220, pic.Labels.Count * 60 + 55)
                    : item.Visual is not null || item.Diagram is not null ? 220 : 0;
                // Keep the number, prompt and essential drawing together when the block fits on a page.
                double promptHeight = QuizPdfText.Wrap(_g, item.Prompt, _font, Width).Sum(line => line.Height);
                int workLines = sheet.Tier >= CurriculumTier.FourStars || item.Source.ElementaryProblem?.Reasoning is not null
                    || item.Source.GeometryProblem?.Reasoning is not null ? 6 : EssayAnswerValidator.RequiresSolution(item.Source) ? 4 : 3;
                double optionsHeight = sheet.Mode == ArithmeticQuizMode.Essay ? workLines * 21 + 12
                    : item.Options.Sum(option => QuizPdfText.Wrap(_g, option, _font, Width).Sum(line => line.Height)) + 30;
                double tableHeight = item.Table is { } factTable ? (factTable.Rows.Count + 1) * 25 : 0;
                double captionHeight = item.Diagram is { } d ? QuizPdfText.Wrap(_g, d.Caption, _font, Width).Sum(line => line.Height) : 0;
                double answerHeight = answers && item.Source.ElementaryProblem is { } time && ElementaryQuizGenerator.IsMixedTime(time.Type)
                    ? QuizPdfText.Wrap(_g, item.Solution, _font, Width).Sum(line => line.Height) + 14 : 0;
                Ensure(Math.Min(700, 28 + (answers ? answerHeight : promptHeight + visualHeight + tableHeight + captionHeight + optionsHeight + 30)));
                Text(T("Câu ", "Question ") + (index + 1) + ".", _bold);
                if (answers)
                {
                    if (sheet.Mode != ArithmeticQuizMode.Essay || !QuizPrintCalculationSolution.AppliesTo(item.Source))
                        Text(T("Đáp án: ", "Answer: ") + item.Answer, _bold);
                    Text(item.Solution); _y += 14; continue;
                }
                Text(item.Prompt); _y += 6;
                if (item.Table is { } table)
                {
                    Text(table.LabelHeader + " — " + table.ValueHeader, _bold);
                    foreach (var row in table.Rows) Text(row.Label + ": " + row.Value);
                }
                if (visualHeight > 0)
                {
                    Ensure(visualHeight + 8);
                    double logicalWidth = item.Visual?.Kind == "pie" ? 360 : (Width - 100) / .75;
                    var state = _g.Save(); _g.TranslateTransform(Margin + (Width - logicalWidth * .75) / 2, _y);
                    // Draw at the same logical dimensions as the on-screen illustrations.
                    _g.ScaleTransform(.75);
                    IDrawable drawable = item.Visual is { } visual ? new ElementaryQuizDrawable(visual) : new QuizDiagramDrawable(item.Diagram!);
                    drawable.Draw(new QuizPdfCanvas(_g), new RectF(0, 0, (float)logicalWidth, (float)(visualHeight / .75)));
                    _g.Restore(state); _y += visualHeight + 8;
                    if (item.Diagram is { } diagram) Text(diagram.Caption);
                }
                if (sheet.Mode != ArithmeticQuizMode.Essay)
                {
                    Text(sheet.Mode == ArithmeticQuizMode.TrueFalse ? T("Đánh dấu lựa chọn:", "Mark your choice:") : T("Khoanh vào đáp án đúng:", "Circle the correct answer:"));
                    for (int option = 0; option < item.Options.Count; option++)
                        Text($"{(char)('A' + option)}. {item.Options[option]}");
                    _y += 10;
                }
                else
                {
                    // Don't orphan a single work line on the next page.
                    Ensure(42);
                    for (int line = 0; line < workLines; line++)
                    { Ensure(21); _y += 20; _g.DrawLine(new XPen(XColors.LightGray, .5), Margin, _y, Margin + Width, _y); }
                    _y += 12;
                }
            }
            _g.Dispose();
        }
    }
}
