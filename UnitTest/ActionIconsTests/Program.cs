using MathSolver.Services;

static void Check(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}
foreach (string action in new[] { "Calculate", "Stop", "ClearAll", "Workspace.Copy", "Workspace.Paste", "AiBank.Open", "QuizPdf.Export", "AiBank.Review.Open" })
    Check(ActionIconCatalog.GlyphFor(action) is not null, "Missing action: " + action);
Check(ActionIconCatalog.GlyphFor("Actions.Calculate") == ActionIconCatalog.GlyphFor("Calculate"), "Calculate alias");
Check(ActionIconCatalog.GlyphFor("Actions.Clear") == ActionIconCatalog.GlyphFor("Workspace.Clear"), "Clear alias");
Check(ActionIconCatalog.GlyphFor("Calculation.Tabs.Fraction") is null, "Math navigation is separate");
Check(ActionIconCatalog.LabelFor("Calculate", "🧮 Tính toán") == "Tính toán", "Legacy Vietnamese caption");
Check(ActionIconCatalog.LabelFor("Actions.Clear", "🗑️ Clear") == "Clear", "Variation selector prefix");
Check(ActionIconCatalog.LabelFor("AiBank.Open", "🤖 Add AI questions") == "Add AI questions", "AI caption");
Check(ActionIconCatalog.LabelFor("Calculate", "Tính x + ½") == "Tính x + ½", "Meaningful math preserved");
Check(ActionIconCatalog.LabelFor("Unknown", "🧮 lesson") == "🧮 lesson", "Unmapped text preserved");
Check(ActionIconCatalog.LabelFor("Stop", "") == "", "Icon-only control");
Check(ActionIconCatalog.ActionSize == 20 && ActionIconCatalog.NavigationSize == 24, "Logical size roles");
Console.WriteLine("PASS ALL ACTION CATALOG CHECKS");
