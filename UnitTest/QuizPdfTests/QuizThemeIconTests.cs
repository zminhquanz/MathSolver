using System.Globalization;
using System.Reflection;
using System.Security;
using System.Text;
using System.Text.Json;
using MathSolver.Graphics;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using Microsoft.Maui.Graphics;

internal static class QuizThemeIconTests
{
    internal static int Run(string[] args)
    {
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            string root = Directory.GetCurrentDirectory();
            string output = Path.Combine(root, "artifacts/design/quiz-theme-group-icons/2026-11-12");
            Directory.CreateDirectory(output);
            var groups = QuizChoiceCatalog.Definitions.Values.Where(x => x.Key.StartsWith("Learning.Group.", StringComparison.Ordinal)).ToArray();
            var expectedMapping = new Dictionary<string, string>
            {
                ["Mixed"] = "mixed", ["Objects"] = "knowledge-objects", ["Animals"] = "knowledge-animals",
                ["SchoolSupplies"] = "knowledge-school-supplies", ["Food"] = "food", ["Drinks"] = "drinks", ["Fruit"] = "fruit",
                ["Money"] = "knowledge-money", ["Time"] = "clock", ["Measurement"] = "measurement", ["Geometry"] = "geometry",
                ["Packaging"] = "knowledge-packaging", ["Production"] = "knowledge-production", ["Data"] = "data-table", ["Motion"] = "motion"
            };
            Require(groups.Length == expectedMapping.Count && groups.All(g => expectedMapping.TryGetValue(g.Key["Learning.Group.".Length..], out var id) && g.IllustrationId == id), "Stable15-group key→ID mapping");
            var aliases = QuizChoiceCatalog.Definitions.Values.Where(x => !x.Key.StartsWith("Learning.Group.", StringComparison.Ordinal)
                && groups.Any(g => g.IllustrationId == x.IllustrationId)).ToArray();
            var streams = aliases.ToDictionary(x => x.Key, x => Capture(x.IllustrationId, 54, false).Trace.ToArray());
            string baseline = Path.Combine(output, "baseline-draws.json");
            if (args.Contains("--baseline"))
            {
                Require(!File.Exists(baseline), "Never overwrite original draw baseline");
                File.WriteAllText(baseline, JsonSerializer.Serialize(streams, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"PASS baseline: {aliases.Length} actual non-group draw call streams");
                return 0;
            }
            var original = JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(baseline))!;
            Require(aliases.Length == 10 && groups.Length == 15, "Expected15 theme choices/10 shared aliases");
            foreach (var alias in streams) Require(original[alias.Key].SequenceEqual(alias.Value), "Non-group changed: " + alias.Key);
            Directory.CreateDirectory(Path.Combine(output, "vectors"));
            var metrics = new List<object>();
            foreach (var group in groups)
            foreach (float size in new[] { 32f, 44f, 54f })
            {
                var draw = Capture(group.IllustrationId, size, true);
                Require(!draw.Trace.Any(x => x.StartsWith("DrawString")), "Group uses font: " + group.Key);
                float expected = size == 32 ? 1.5f : size == 44 ? 1.75f : 2;
                Require(draw.Strokes.Count > 0 && draw.Strokes.All(x => Math.Abs(x - expected) < .15), "Scaled stroke: " + group.Key);
                var b = draw.Bounds;
                float margin = size * .06f;
                Require(b.Left >= margin - .01 && b.Top >= margin - .01 && b.Right <= size - margin + .01 && b.Bottom <= size - margin + .01,
                    $"Paint margin {group.Key}/{size}: {b}");
                float extent = Math.Max(b.Width, b.Height) / size;
                Require(extent >= .70 && extent <= .88, $"Optical extent {group.Key}/{size}: {extent}");
                File.WriteAllText(Path.Combine(output, "vectors", group.IllustrationId + "-" + size + ".svg"), draw.Svg(size));
                metrics.Add(new { group.Key, Size = size, Bounds = new { b.Left, b.Top, b.Width, b.Height }, Stroke = draw.Strokes.First() });
            }
            foreach (var language in new[] { AppLanguage.Vietnamese, AppLanguage.English })
            {
                var options = groups.Select((g, i) => QuizChoiceCatalog.Create(i, g.Key, g.Key, language, false)).ToArray();
                Require(options.All(x => !string.IsNullOrWhiteSpace(x.Description)), "Missing description");
                foreach (string term in language == AppLanguage.Vietnamese ? new[] { "ban chai", "compa", "cuc tay" } : new[] { "toothbrush", "compass", "eraser" })
                    Require(QuizChoiceCatalog.Filter(options, term).Count > 0, "Full metadata search lost: " + term);
                foreach (var option in options.Where(x => x.Key is "Learning.Group.Objects" or "Learning.Group.SchoolSupplies"))
                {
                    string? summary = option.GetType().GetProperty("Summary")?.GetValue(option) as string;
                    Require(!string.IsNullOrWhiteSpace(summary) && summary.Length < option.Description.Length, "Missing concise group summary: " + option.Key);
                }
                Require(options.Where(x => x.Key is not ("Learning.Group.Objects" or "Learning.Group.SchoolSupplies"))
                    .All(x => x.Summary is null), "Optional summary fallback preserves full descriptions");
                var found = QuizChoiceCatalog.Filter(options, language == AppLanguage.Vietnamese ? "ban chai" : "toothbrush").Single();
                Require(found.Index == Array.FindIndex(options, x => x.Key == found.Key), "Filter preserves original index");
            }
            Require(!QuizThemeIconPolicy.IsGroupKey("Quiz.OperationMixed") && QuizThemeIconPolicy.IsGroupKey("Learning.Group.Mixed"), "Scope is based on key, not ID");
            var keyboardLayout = QuizChoiceLayout.Calculate(753, 130, 15, headerHeight: 48, forceSingleColumn: true);
            Require(keyboardLayout.Columns == 1 && keyboardLayout.Height <= 130
                && keyboardLayout.Height - 2 * keyboardLayout.Padding - 2 - 48 - 8 >= 48,
                "Landscape IME viewport retains search and at least48DIP of list");
            var minimalKeyboard = QuizChoiceLayout.Calculate(753, 98, 15, headerHeight: 48, forceSingleColumn: true);
            Require(minimalKeyboard.Height >= 96 && minimalKeyboard.Padding == 0,
                "Observed98DIP keyboard viewport reserves two48DIP touch regions");
            foreach (float size in new[] { 0f, -1, float.NaN, float.PositiveInfinity })
            {
                Require(QuizThemeIconPolicy.StrokeForSize(size) == 0, "Invalid size stroke guard");
                Require(Capture("mixed", size, true).Trace.Count == 0, "Invalid viewport must not draw");
            }
            foreach (var invalid in new[] { new RectF(0, 0, float.PositiveInfinity, 54), new RectF(0, 0, 54, float.NaN) })
            {
                var canvas = DispatchProxy.Create<ICanvas, ThemeIconCanvas>();
                new QuizChoiceDrawable("mixed", Colors.Blue, true).Draw(canvas, invalid);
                Require(((ThemeIconCanvas)(object)canvas).Trace.Count == 0, "Both viewport axes must be finite");
            }
            foreach (var group in groups)
            {
                var fillOps = Capture(group.IllustrationId, 54, true).Trace.Where(x => x.StartsWith("Fill")).ToArray();
                Require(!fillOps.Any(x => x.StartsWith("FillRectangle(0|0|100|100")), "Transparent exterior");
                var colors = Capture(group.IllustrationId, 54, true).FillColors;
                Require(colors.All(x => x.Alpha == 1 || group.IllustrationId == "data-table" && x.Alpha > 0 && x.Alpha <= .25f), "Only Data uses translucent fill");
            }
            foreach (string surface in new[] { "#FFFFFF", "#F8FAFC", "#E5ECFD", "#111827", "#172033" })
            foreach (string color in new[] { "#6D28D9", "#2563EB", "#FDE047", "#64748B" })
            {
                ReadableRgb Rgb(string hex) { var c = Color.FromArgb(hex); return new(c.Red, c.Green, c.Blue); }
                var bg = Rgb(surface);
                Require(ReadableColorPolicy.Contrast(QuizThemeIconPolicy.InkOn(Rgb(color), bg), bg) >= 4.5, "Foreground contrast: " + color + "/" + surface);
            }
            File.WriteAllText(Path.Combine(output, "mapping-and-aliases.json"), JsonSerializer.Serialize(new { Groups = groups, AliasKeys = aliases.Select(x => x.Key), Metrics = metrics }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("PASS theme icons:15 groups×3 sizes;10 unchanged aliases; bounds/stroke/font isolation; VI/EN full search");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("FAIL theme icons: " + e); return 1; }
    }

    internal static ThemeIconCanvas Capture(string id, float size, bool group)
    {
        var canvas = DispatchProxy.Create<ICanvas, ThemeIconCanvas>();
        IDrawable drawable;
        if (!group) drawable = new QuizChoiceDrawable(id, Color.FromArgb("#2563EB"));
        else
        {
            var ctor = typeof(QuizChoiceDrawable).GetConstructor([typeof(string), typeof(Color), typeof(bool)]);
            Require(ctor is not null, "Missing isolated group rendering context");
            drawable = (IDrawable)ctor!.Invoke([id, Color.FromArgb("#2563EB"), true]);
        }
        drawable.Draw(canvas, new RectF(0, 0, size, size));
        return (ThemeIconCanvas)(object)canvas;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

// Records the actual ICanvas contract; exports are inspection aids, never native screenshots.
public class ThemeIconCanvas : DispatchProxy
{
    public List<string> Trace = new();
    public List<float> Strokes = new();
    public List<Color> FillColors = new();
    public RectF Bounds;
    private bool hasBounds;
    private float sx = 1, sy = 1, tx, ty, stroke = 1;
    private Color ink = Colors.Black, fill = Colors.Black;
    private StringBuilder body = new();
    private Stack<(float, float, float, float, float, Color, Color)> stack = new();
    private static float N(object? x) => Convert.ToSingle(x, CultureInfo.InvariantCulture);
    private static string V(object? x) => x switch { PathF p => p.ToDefinitionString(1), Color c => c.ToArgbHex(), float f => f.ToString("R", CultureInfo.InvariantCulture), _ => x?.ToString() ?? "null" };
    public string Svg(float size) => $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {size} {size}\">{body}</svg>";
    protected override object? Invoke(MethodInfo? method, object?[]? a)
    {
        a ??= []; string m = method!.Name;
        Trace.Add(m + "(" + string.Join("|", a.Select(V)) + ")");
        string color(Color c) => $"rgba({(int)(c.Red * 255)},{(int)(c.Green * 255)},{(int)(c.Blue * 255)},{c.Alpha.ToString(CultureInfo.InvariantCulture)})";
        string style = $"transform=\"translate({tx} {ty}) scale({sx} {sy})\" fill=\"none\" stroke=\"{color(ink)}\" stroke-width=\"{stroke}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"";
        void Paint(RectF r, bool filled)
        {
            float pad = filled ? 0 : stroke / 2;
            var b = new RectF(tx + (r.X - pad) * sx, ty + (r.Y - pad) * sy, (r.Width + pad * 2) * sx, (r.Height + pad * 2) * sy);
            if (!hasBounds) { Bounds = b; hasBounds = true; }
            else Bounds = new RectF(Math.Min(Bounds.Left, b.Left), Math.Min(Bounds.Top, b.Top), Math.Max(Bounds.Right, b.Right) - Math.Min(Bounds.Left, b.Left), Math.Max(Bounds.Bottom, b.Bottom) - Math.Min(Bounds.Top, b.Top));
            if (!filled) Strokes.Add(stroke * sx);
        }
        switch (m)
        {
            case "SaveState": stack.Push((sx, sy, tx, ty, stroke, ink, fill)); break;
            case "RestoreState": (sx, sy, tx, ty, stroke, ink, fill) = stack.Pop(); break;
            case "Translate": tx += N(a[0]) * sx; ty += N(a[1]) * sy; break;
            case "Scale": sx *= N(a[0]); sy *= N(a[1]); break;
            case "set_StrokeSize": stroke = N(a[0]); break;
            case "set_StrokeColor": ink = (Color)a[0]!; break;
            case "set_FillColor": fill = (Color)a[0]!; FillColors.Add(fill); break;
            case "DrawPath": case "FillPath":
                var path = (PathF)a[0]!; Paint(path.Bounds, m == "FillPath");
                body.Append($"<path d=\"{path.ToDefinitionString(1)}\" {style}" + (m == "FillPath" ? $" style=\"fill:{color(fill)};stroke:none\"" : "") + "/>"); break;
            case "DrawLine":
                Paint(new RectF(Math.Min(N(a[0]), N(a[2])), Math.Min(N(a[1]), N(a[3])), Math.Abs(N(a[2]) - N(a[0])), Math.Abs(N(a[3]) - N(a[1]))), false);
                body.Append($"<path d=\"M{a[0]} {a[1]}L{a[2]} {a[3]}\" {style}/>"); break;
            case "DrawRectangle": case "FillRectangle": case "DrawRoundedRectangle": case "DrawEllipse": case "FillEllipse":
                var r = new RectF(N(a[0]), N(a[1]), N(a[2]), N(a[3])); bool filled = m.StartsWith("Fill"); Paint(r, filled);
                string element = m.Contains("Ellipse") ? $"ellipse cx=\"{r.Center.X}\" cy=\"{r.Center.Y}\" rx=\"{r.Width / 2}\" ry=\"{r.Height / 2}\"" : $"rect x=\"{r.X}\" y=\"{r.Y}\" width=\"{r.Width}\" height=\"{r.Height}\"" + (m == "DrawRoundedRectangle" ? $" rx=\"{a[4]}\"" : "");
                body.Append($"<{element} {style}" + (filled ? $" style=\"fill:{color(fill)};stroke:none\"" : "") + "/>"); break;
        }
        return method.ReturnType == typeof(void) ? null : method.ReturnType == typeof(bool) ? true : Activator.CreateInstance(method.ReturnType);
    }
}
