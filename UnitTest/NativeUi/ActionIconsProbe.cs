using System.Reflection;
using MathSolver.Controls;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Views;

namespace MathSolver;

// Opt-in integration runner: actual Shell, actual page controls, no inference or clipboard writes.
public partial class App
{
    protected override void OnStart()
    {
        base.OnStart();
        Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(2), RunActionIconsProbe);
    }
    private async void RunActionIconsProbe()
    {
#if ANDROID
        string folder = global::Android.App.Application.Context.GetExternalFilesDir(null)!.AbsolutePath;
#else
        string folder = AppContext.BaseDirectory;
#endif
        string log = Path.Combine(folder, "action-icons.log");
        var originalTheme = AppThemeManager.CurrentMode;
        string culture = MathSolver.Services.Localization.LocalizationManager.Instance.CurrentCulture;
        try
        {
            File.WriteAllText(log, "START ORIGINAL ACTION ICONS\n");
            var shell = (Shell)Windows[0].Page!;
            foreach (var theme in new[] { AppThemeMode.Light, AppThemeMode.Dark })
            {
                AppThemeManager.SetThemeMode(theme, persist: false);
                await LocalizationService.SetCultureAsync(theme == AppThemeMode.Light ? "vi-VN" : "en-US");
                await shell.GoToAsync("//CalculationPage"); await Task.Delay(400);
                var calculation = shell.CurrentPage;
                var select = calculation.GetType().GetMethod("SwitchSubTabAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
                foreach (string tab in new[] { "Basic", "NumberTheory", "Average", "PowerRoot", "Fraction", "FindX", "Quadratic", "Geometry", "Matrix" })
                {
                    await (Task)select.Invoke(calculation, [Enum.Parse(select.GetParameters()[0].ParameterType, tab)])!;
                    await Task.Delay(300);
                    if (tab == "Matrix")
                    {
                        var matrix = (MatrixView)calculation.FindByName<LazyContentView>("MatrixSolverHost").Content!;
                        matrix.SetTabActive(true);
                        var setup = (VisualElement)typeof(MatrixView).GetField("_setupPanel", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(matrix)!;
                        if (setup.IsVisible)
                        {
                            var toggle = (Button)typeof(MatrixView).GetField("_setupToggle", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(matrix)!;
                            ((IButtonController)toggle).SendClicked(); await Task.Delay(100);
                        }
                        var viewport = (MatrixViewport)typeof(MatrixView).GetField("_viewport", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(matrix)!;
                        for (int ready = 0; ready < 100 && !viewport.IsLoaded; ready++) await Task.Delay(50);
                        var expand = (Button)typeof(MatrixView).GetField("_expandTable", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(matrix)!;
                        foreach (string expected in new[] { "Workspace.RestoreTable", "Workspace.ExpandTable" })
                        {
                            ((IButtonController)expand).SendClicked(); await Task.Delay(80);
                            if (ActionIcon.GetKey(expand) != expected) throw new InvalidOperationException("Stale Matrix expand/restore key: " + ActionIcon.GetKey(expand)
                                + $", loaded={matrix.IsLoaded}/{viewport.IsLoaded}, expanded=" + typeof(MatrixView).GetField("_tableExpanded", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(matrix));
#if ANDROID
                            var mirror = (Button)typeof(MatrixView).GetField("_phoneExpandToggle", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(matrix)!;
                            if (ActionIcon.GetKey(mirror) != expected) throw new InvalidOperationException("Stale phone expand/restore key: " + ActionIcon.GetKey(mirror));
#endif
                        }
                    }
                    Verify(calculation, log, theme + "/" + tab);
                    if (tab is "Basic" or "Matrix") await Capture(shell, folder, theme + "-" + tab);
                }
                await shell.GoToAsync("//MathPuzzlePage"); await Task.Delay(350);
                var aiEntry = shell.CurrentPage.FindByName<Button>("OpenAiQuestionBankButton");
                var aiEntryIcon = Walk(shell.CurrentPage).OfType<ActionIconView>().Single(icon => icon.Key == "Ai");
                aiEntry.Opacity = .5; await Task.Delay(50);
                if (Math.Abs(aiEntryIcon.Opacity - aiEntry.Opacity) > .001)
                    throw new InvalidOperationException("AI entry overlay does not follow button opacity");
                aiEntry.Opacity = 1;
                Verify(shell.CurrentPage, log, theme + "/Quiz");
                var ai = new AiQuestionBankPage();
                await shell.Navigation.PushAsync(ai); await Task.Delay(400);
                Verify(ai, log, theme + "/AI"); await Capture(shell, folder, theme + "-AI");
                await shell.Navigation.PopAsync();
                var review = new AiQuestionReviewPage(AiQuestionBank.Current.Store);
                await shell.Navigation.PushAsync(review); await Task.Delay(350);
                Verify(review, log, theme + "/Review");
                await shell.Navigation.PopAsync();
                var pdf = new QuizPdfPage(new MathSolver.Services.Printing.QuizWorksheetSettings(0, null,
                    MathSolver.Models.CurriculumTier.OneStar, default), new PracticeQuestionFactory(AiQuestionBank.Current.Practice));
                await shell.Navigation.PushAsync(pdf); await Task.Delay(350);
                Verify(pdf, log, theme + "/PDF"); await Capture(shell, folder, theme + "-PDF");
                await shell.Navigation.PopAsync();
            }
            // Dynamic key/color replacement on the same button, including icon removal.
            var button = new EmojiActionButton { VectorIconKey = "Calculate", Caption = "🧮 Calculate", TextColor = Colors.Magenta };
            if (button.Text != "Calculate") throw new InvalidOperationException("Legacy caption remains");
            CheckButton(button);
            button.VectorIconKey = "Stop"; button.Caption = "Stop"; button.TextColor = Colors.White; CheckButton(button);
            button.IsEnabled = false; CheckButton(button);
            button.VectorIconKey = null;
            if (button.ImageSource is not null) throw new InvalidOperationException("Stale icon after key removal");
            File.AppendAllText(log, "PASS dynamic caption/key/color/disabled/removal\nPASS ALL ORIGINAL ACTION ICON CHECKS\n");
        }
        catch (Exception error) { File.AppendAllText(log, "FAIL " + error + "\n"); }
        finally
        {
            await LocalizationService.SetCultureAsync(culture);
            AppThemeManager.SetThemeMode(originalTheme, persist: false);
#if WINDOWS
            Application.Current!.CloseWindow(Windows[0]);
#endif
        }
    }
    private static IEnumerable<Element> Walk(Element element)
    {
        yield return element;
        if (element is Microsoft.Maui.IVisualTreeElement tree)
            foreach (var child in tree.GetVisualChildren().OfType<Element>())
                foreach (var item in Walk(child)) yield return item;
    }
    private static void Verify(Element root, string log, string stage)
    {
        int count = 0;
        foreach (var button in Walk(root).OfType<Button>())
        {
            if (ActionIconCatalog.GlyphFor(ActionIcon.GetKey(button)) is null) continue;
            CheckButton(button); count++;
        }
        if (count == 0) throw new InvalidOperationException("No actual action buttons: " + stage);
        foreach (var icon in Walk(root).OfType<ActionIconView>())
            if (icon.Source is not FontImageSource source || source.Size != icon.IconSize || !SameColor(source.Color, icon.Color))
                throw new InvalidOperationException("Heading/overlay icon mismatch: " + stage);
        File.AppendAllText(log, $"PASS {stage}: {count} actual buttons, exact color/size\n");
    }
    private static bool SameColor(Color a, Color b) => Math.Abs(a.Red - b.Red) < .00001f && Math.Abs(a.Green - b.Green) < .00001f
        && Math.Abs(a.Blue - b.Blue) < .00001f && Math.Abs(a.Alpha - b.Alpha) < .00001f;
    private static void CheckButton(Button button)
    {
        if (button.ImageSource is not FontImageSource source || source.Size != 20
            || source.Glyph != ActionIconCatalog.GlyphFor(ActionIcon.GetKey(button)) || !SameColor(source.Color, button.TextColor))
            throw new InvalidOperationException($"Action source mismatch: {button.Text}, key={ActionIcon.GetKey(button)}, "
                + $"textColor={button.TextColor}, source={button.ImageSource}, "
                + (button.ImageSource is FontImageSource f ? $"glyph={f.Glyph}, size={f.Size}, color={f.Color}" : "not font"));
    }
    private static async Task Capture(Shell shell, string folder, string name)
    {
#if WINDOWS
        var bitmap = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();
        await bitmap.RenderAsync((Microsoft.UI.Xaml.FrameworkElement)shell.Handler!.PlatformView!);
        using var stream = File.Create(Path.Combine(folder, name + ".png"));
        using var native = System.IO.WindowsRuntimeStreamExtensions.AsRandomAccessStream(stream);
        var encoder = await global::Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(global::Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, native);
        var buffer = await bitmap.GetPixelsAsync();
        using var reader = global::Windows.Storage.Streams.DataReader.FromBuffer(buffer);
        byte[] bytes = new byte[buffer.Length]; reader.ReadBytes(bytes);
        encoder.SetPixelData(global::Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, global::Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
            (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, bytes); await encoder.FlushAsync();
#elif ANDROID
        var screenshot = await Screenshot.Default.CaptureAsync();
        using var stream = File.Create(Path.Combine(folder, name + ".png"));
        await screenshot.CopyToAsync(stream);
#endif
    }
}
