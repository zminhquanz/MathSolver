using MathSolver.Controls;
using MathSolver.Numerics;
using MathSolver.Services;
using MathSolver.Services.Core;
using Microsoft.Maui.Controls.Shapes;
using System.Globalization;

namespace MathSolver.Views;

public partial class PowerRootView
{
    private readonly HashSet<string> _expandedPowerDiagnostics = ["Performance"];
    private bool _showAllPowerStages;
    private TimeSpan? _powerCleanupElapsed;
    private TimeSpan? _powerTxtPreparationElapsed;
    private bool _powerTxtReused;
    private static string D(string key) => Translate("PowerRoot.Diagnostics." + key);
    private static string DF(string key, params object?[] args) => Format("PowerRoot.Diagnostics." + key, args);

    private void ResetPowerDiagnostics()
    {
        _expandedPowerDiagnostics.Clear();
        _expandedPowerDiagnostics.Add("Performance");
        _showAllPowerStages = false;
        _powerCleanupElapsed = null;
        _powerTxtPreparationElapsed = null;
        _powerTxtReused = false;
        PowerSummaryBorder.IsVisible = false;
        PowerSummaryRows.Children.Clear();
        PowerDiagnosticsSections.Children.Clear();
    }

    private void UpdatePowerSummary(PowerCalculationState state)
    {
        PowerSummaryRows.Children.Clear();
        var title = DiagnosticText("✅ " + D("Completed"), bold: true);
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level2);
        PowerSummaryRows.Add(title);
        var metrics = new Grid { ColumnSpacing = 20, RowSpacing = 12 };
        AddMetric("CalculationTime", Duration(state.Elapsed));
        AddMetric("Digits", state.DigitCount.ToString("N0", CultureInfo.CurrentCulture));
        bool detailedSummary = state.DigitCount >= ExportDigitThreshold;
        if (detailedSummary)
        {
            AddMetric("Method", D(state.Strategy switch
            {
                PowerComputationStrategy.ParallelNttPower when state.ParallelDiagnostics?.UsedMemoryBoundedLargePower == true => "MethodSegmented",
                PowerComputationStrategy.ParallelNttPower => "MethodNtt",
                PowerComputationStrategy.BitShift => "MethodShift",
                PowerComputationStrategy.FactorizedPowerOfTen => "MethodTen",
                _ => "MethodSequential"
            }));
            AddMetric("Workers", state.ActiveWorkerCount.ToString(CultureInfo.CurrentCulture));
            AddMetric("TxtSize", FormatByteSize(EstimateTxtFileSizeBytes(state)));
        }
        AdaptiveColumns.SetMinimumColumnWidth(metrics, detailedSummary ? 260 : 140);
        PowerSummaryRows.Add(metrics);
        if (detailedSummary) PowerSummaryRows.Add(DiagnosticText(D("TimeNote"), secondary: true));

        void AddMetric(string key, string value) => metrics.Add(new VerticalStackLayout
        {
            Spacing = 4, Children = { DiagnosticText(D(key), secondary: true), DiagnosticText(value, bold: true) }
        });
    }

    private void RenderPowerDiagnostics(PowerCalculationState state)
    {
        if (!DeveloperModeManager.IsEnabled) return;
        int calculationVersion = _calculationVersion;
        PowerDiagnosticsSections.Children.Clear();
        if (state.ParallelDiagnostics is { NttMultiplicationCount: > 0 } diagnostics)
        {
            PowerDiagnosticsSections.Add(Disclosure("Performance", () => PerformanceDetails(diagnostics)));
        }
        PowerDiagnosticsSections.Add(Disclosure("Technical", () => TechnicalSections(state)));
        var copy = new ResultCopyButton { NormalTextKey = "PowerRoot.Diagnostics.CopyLog", AutomationId = "PowerCopyDiagnostics" };
        copy.SetDynamicResource(StyleProperty, "SolverOutlinedActionStyle");
        copy.Clicked += async (_, _) =>
        {
            if (DeveloperModeManager.IsEnabled && !_isCalculating && calculationVersion == _calculationVersion && _calculationState is { } current)
            {
                try { await ResultClipboardService.CopyAsync(copy, CreateLargeResultInformation(current)); }
                catch (Exception error) { System.Diagnostics.Debug.WriteLine(error); ShowError(D("CopyFailed")); }
            }
        };
        var copyCaption = DiagnosticText("");
        copyCaption.SetBinding(Label.TextProperty, new Binding(nameof(ResultCopyButton.DisplayText), source: copy));
        copyCaption.HorizontalTextAlignment = TextAlignment.Center;
        var copyContent = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)], ColumnSpacing = 8,
            Padding = new Thickness(16, 10), InputTransparent = true, VerticalOptions = LayoutOptions.Center };
        copyContent.Add(DiagnosticText("📋"), 0);
        copyContent.Add(copyCaption, 1);
        var copyHost = new Grid();
        copyHost.Add(copy);
        copyHost.Add(copyContent);
        PowerDiagnosticsSections.Add(copyHost);
    }

    private View PerformanceDetails(ParallelPowerDiagnostics diagnostics)
    {
        var rows = new VerticalStackLayout { Spacing = 10 };
        rows.Add(TimingTable(PowerDiagnosticsPresentation.PhaseRows(diagnostics)));
        rows.Add(DiagnosticText(D("TimingNote"), secondary: true));
        var later = PowerDiagnosticsPresentation.Measured([
            new("CleanupTime", _powerCleanupElapsed ?? TimeSpan.Zero),
            new("TxtPreparationTime", _powerTxtPreparationElapsed ?? TimeSpan.Zero)
        ]);
        if (later.Count > 0)
        {
            rows.Add(DiagnosticText(D("AfterCalculation"), bold: true));
            rows.Add(TimingTable(later));
            rows.Add(DiagnosticText(D("TxtTimingNote"), secondary: true));
        }
        if (_powerTxtReused) rows.Add(DiagnosticText(D("TxtReused"), secondary: true));
        return rows;
    }

    private View TechnicalSections(PowerCalculationState state)
    {
        var sections = new VerticalStackLayout { Spacing = 10 };
        sections.Add(Disclosure("Memory", () => MemoryDetails(state)));
        sections.Add(Disclosure("WorkersAndAcceleration", () => WorkerDetails(state)));
        if (state.ParallelDiagnostics is { NttMultiplicationCount: > 0 } diagnostics)
        {
            if (PowerDiagnosticsPresentation.ForwardRows(diagnostics).Count > 0
                || PowerDiagnosticsPresentation.InverseRows(diagnostics).Count > 0
                || PowerDiagnosticsPresentation.InverseL1Rows(diagnostics).Count > 0)
                sections.Add(Disclosure("Transforms", () => TransformDetails(diagnostics)));
            if (diagnostics.GlobalStageProfiles.Any(p => p.Elapsed > TimeSpan.Zero))
                sections.Add(Disclosure("Stages", () => StageDetails(diagnostics)));
        }
        return sections;
    }

    private View MemoryDetails(PowerCalculationState state)
    {
        var rows = new VerticalStackLayout { Spacing = 10 };
        AddBytes("WorkspaceEstimate", state.EstimatedPeakRamBytes);
        AddBytes("ProcessMemory", state.ProcessPrivateMemoryBytes);
        if (state.ProcessPrivateMemoryBeforeCleanupBytes > state.ProcessPrivateMemoryBytes)
            AddBytes("ProcessBeforeCleanup", state.ProcessPrivateMemoryBeforeCleanupBytes);
        if (state.ParallelDiagnostics is { } diagnostics)
        {
            AddBytes("WorkspacePeak", diagnostics.NttWorkspacePeakBytes);
            AddBytes("PoolPeak", diagnostics.NttPoolPeakRetainedBytes);
            if (diagnostics.NttBufferRentCount > 0)
                rows.Add(Metric(D("BufferReuse"), $"{diagnostics.NttBufferReuseCount:N0} / {diagnostics.NttBufferRentCount:N0}"));
        }
        rows.Add(DiagnosticText(D("MemoryNote"), secondary: true));
        return rows;
        void AddBytes(string key, long bytes) { if (bytes > 0) rows.Add(Metric(D(key), FormatByteSize(bytes))); }
    }

    private View WorkerDetails(PowerCalculationState state)
    {
        var rows = new VerticalStackLayout { Spacing = 10 };
        rows.Add(Metric(D("Workers"), state.ActiveWorkerCount.ToString(CultureInfo.CurrentCulture)));
        if (state.ParallelDiagnostics is { } diagnostics)
        {
            rows.Add(Metric(D("WorkerBudget"), $"{diagnostics.WorkerCount} / {CalculationThreadingManager.LogicalProcessorCount}"));
            if (diagnostics.NttMultiplicationCount > 0)
            {
                rows.Add(Metric(D("Backend"), GetRecordedNttBackend(diagnostics)));
                rows.Add(Metric(D("Avx512Runtime"), D(diagnostics.Avx512RuntimeSupported ? "Yes" : "No")));
                rows.Add(Metric(D("Avx512Enabled"), D(diagnostics.Avx512Enabled ? "Yes" : "No")));
                rows.Add(DiagnosticText(D("BackendNote"), secondary: true));
                rows.Add(Disclosure("KernelExecution", () => KernelDetails(diagnostics)));
                rows.Add(Metric(D("NttProducts"), $"{diagnostics.NttMultiplicationCount:N0}"));
            }
            if (diagnostics.UsedExponentSplit)
            {
                rows.Add(DiagnosticText(Format("PowerRoot.InfoExponentSplit", diagnostics.FirstExponent.ToString("N0"),
                    diagnostics.SecondExponent.ToString("N0"), diagnostics.FirstBranchWorkerCount, diagnostics.SecondBranchWorkerCount)));
                rows.Add(TimingTable(PowerDiagnosticsPresentation.Measured([
                    new("BranchOne", diagnostics.FirstBranchElapsed), new("BranchTwo", diagnostics.SecondBranchElapsed),
                    new("Combine", diagnostics.FinalCombineElapsed)
                ])));
            }
            if (state.BinaryTransformLimit > 0)
                rows.Add(Metric(D("TransformLimit"), $"2^{System.Numerics.BitOperations.Log2((uint)state.BinaryTransformLimit)}"));
            AddCount("ChunkExponent", diagnostics.LargePowerChunkExponent);
            AddCount("SegmentProducts", diagnostics.SegmentedNttMultiplicationCount);
            AddCount("SegmentPairs", diagnostics.SegmentedNttPairCount);
            AddCount("SavedForward", diagnostics.LargeForwardTransformSavedCount);
            AddCount("Generations", diagnostics.LargePersistentGenerationCount);
            AddCount("StaticRanges", diagnostics.LargePersistentStaticRangeCount);
            AddCount("BufferLimit", diagnostics.LargeMemoryBudgetBufferLimit);
        }
        return rows;
        void AddCount(string key, long count) { if (count > 0) rows.Add(Metric(D(key), count.ToString("N0", CultureInfo.CurrentCulture))); }
    }

    private View TransformDetails(ParallelPowerDiagnostics diagnostics)
    {
        var rows = new VerticalStackLayout { Spacing = 10 };
        rows.Add(DiagnosticText(D("ChildTimingNote"), secondary: true));
        var forward = PowerDiagnosticsPresentation.ForwardRows(diagnostics);
        var inverse = PowerDiagnosticsPresentation.InverseRows(diagnostics);
        var inverseL1 = PowerDiagnosticsPresentation.InverseL1Rows(diagnostics);
        if (forward.Count > 0) rows.Add(Disclosure("Forward", () => TimingTable(forward)));
        if (inverse.Count > 0 || inverseL1.Count > 0)
            rows.Add(Disclosure("Inverse", () =>
            {
                var detail = new VerticalStackLayout { Spacing = 10 };
                if (inverse.Count > 0) detail.Add(TimingTable(inverse));
                if (inverseL1.Count > 0) detail.Add(Disclosure("InverseL1", () => TimingTable(inverseL1)));
                return detail;
            }));
        return rows;
    }

    private static View KernelDetails(ParallelPowerDiagnostics diagnostics)
    {
        var rows = new VerticalStackLayout { Spacing = 10 };
        rows.Add(DiagnosticText(D("KernelCountNote"), secondary: true));
        var usage = PowerDiagnosticsPresentation.KernelRows(diagnostics);
        if (usage.Count == 0) rows.Add(DiagnosticText(D("NoKernelSamples")));
        foreach (var phase in usage.GroupBy(row => row.Phase))
        {
            rows.Add(DiagnosticText(D(phase.Key.ToString()), bold: true));
            foreach (var row in phase)
                rows.Add(Metric(KernelIsaName(row.Isa), DF("KernelBatches", row.Calls.ToString("N0", CultureInfo.CurrentCulture))));
        }
        return rows;
    }

    private View StageDetails(ParallelPowerDiagnostics diagnostics)
    {
        var container = new VerticalStackLayout { Spacing = 10 };
        Render();
        return container;
        void Render()
        {
            container.Children.Clear();
            container.Add(DiagnosticText(D(_showAllPowerStages ? "AllStages" : "TopStages"), bold: true));
            container.Add(DiagnosticText(D("StageNote"), secondary: true));
            foreach (var row in PowerDiagnosticsPresentation.StageRows(diagnostics.GlobalStageProfiles, _showAllPowerStages ? null : 5))
            {
                var entry = row.Entry;
                string stage = entry.FusedSecondStageLength >= 2 ? $"{entry.StageLength:N0} + {entry.FusedSecondStageLength:N0}" : $"{entry.StageLength:N0}";
                var details = new VerticalStackLayout { Spacing = 6 };
                details.Add(DiagnosticText(D("StageGroup" + entry.Kind), bold: true));
                details.Add(DiagnosticText($"N = {entry.TransformLength:N0} · S = {stage}"));
                details.Add(DiagnosticText(DF("StageTime", Duration(entry.Elapsed), row.GroupSharePercent.ToString("0.0", CultureInfo.CurrentCulture), entry.CallCount.ToString("N0"))));
                var card = new Border { Padding = 12, StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = 10 }, Content = details };
                card.SetDynamicResource(Border.StrokeProperty, "WallpaperBorderBrush");
                container.Add(card);
            }
            if (diagnostics.GlobalStageProfiles.Count(p => p.Elapsed > TimeSpan.Zero) > 5)
            {
                var toggle = new Button { Text = D(_showAllPowerStages ? "ShowTop" : "ShowAll"), AutomationId = "PowerStageLimit" };
                toggle.SetDynamicResource(StyleProperty, "SolverOutlinedActionStyle");
                toggle.LineBreakMode = LineBreakMode.WordWrap;
                toggle.Clicked += (_, _) => { _showAllPowerStages = !_showAllPowerStages; Render(); };
                container.Add(toggle);
            }
        }
    }

    private View Disclosure(string key, Func<View> build)
    {
        var content = new VerticalStackLayout { IsVisible = false, Spacing = 10 };
        var toggle = new Button { MinimumHeightRequest = 48, FontSize = 14, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.WordWrap };
        DisplayDensityManager.BindControl(toggle);
        toggle.SetDynamicResource(BackgroundColorProperty, "WallpaperSurfaceAltColor");
        toggle.SetDynamicResource(Button.TextColorProperty, "WallpaperTextPrimaryColor");
        toggle.AutomationId = "PowerDiagnostics" + key;
        bool built = false;
        void Refresh()
        {
            bool expanded = _expandedPowerDiagnostics.Contains(key);
            toggle.Text = (expanded ? "▾ " : "▸ ") + D(key);
            SemanticProperties.SetDescription(toggle, D(key) + ". " + D(expanded ? "Expanded" : "Collapsed"));
            if (expanded && !built) { content.Add(build()); built = true; }
            content.IsVisible = expanded;
        }
        toggle.Clicked += (_, _) => { if (!_expandedPowerDiagnostics.Add(key)) _expandedPowerDiagnostics.Remove(key); Refresh(); };
        Refresh();
        return new VerticalStackLayout { Spacing = 8, Children = { toggle, content } };
    }

    private static View TimingTable(IReadOnlyList<PowerTimingRow> rows)
    {
        var table = new VerticalStackLayout { Spacing = 10 };
        if (rows.Count == 0) return DiagnosticText(D("NoTimings"), secondary: true);
        table.Add(Metric(D("Phase"), D("Duration"), bold: true));
        foreach (var row in rows) table.Add(Metric(D(row.Key), Duration(row.Elapsed)));
        return table;
    }

    private static View Metric(string caption, string value, bool bold = false)
    {
        var row = new Grid { ColumnDefinitions = [new(new GridLength(3, GridUnitType.Star)), new(new GridLength(2, GridUnitType.Star))], ColumnSpacing = 12 };
        row.Add(DiagnosticText(caption, bold), 0);
        var result = DiagnosticText(value, bold);
        result.HorizontalTextAlignment = TextAlignment.End;
        row.Add(result, 1);
        return row;
    }

    private static Label DiagnosticText(string text, bool bold = false, bool secondary = false)
    {
        var label = new Label { Text = text, FontSize = secondary ? 13 : 15, LineBreakMode = LineBreakMode.WordWrap, LineHeight = 1.2,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None };
        label.SetDynamicResource(Label.TextColorProperty, secondary ? "WallpaperTextSecondaryColor" : "WallpaperTextPrimaryColor");
        return label;
    }

    private static string Duration(TimeSpan elapsed) => elapsed > TimeSpan.Zero && elapsed.TotalMilliseconds < 1
        ? D("UnderMillisecond") : DF("Seconds", elapsed.TotalSeconds.ToString("0.###", CultureInfo.CurrentCulture));

    private static string GetRecordedNttBackend(ParallelPowerDiagnostics diagnostics)
    {
        if (diagnostics.NttMultiplicationCount == 0) return D("NoNtt");
        var recorded = PowerDiagnosticsPresentation.KernelRows(diagnostics)
            .Select(row => row.Isa).Distinct().OrderDescending().Select(KernelIsaName).ToArray();
        return recorded.Length > 0 ? string.Join(" · ", recorded) : D("NoKernelSamples");
    }

    private static string KernelIsaName(NttKernelIsa isa) => isa switch
    {
        NttKernelIsa.Avx512 => "AVX-512",
        NttKernelIsa.Avx2 => "AVX2 (256-bit)",
        NttKernelIsa.Sse => "SSE (128-bit)",
        NttKernelIsa.Neon => "NEON (128-bit)",
        _ => "Scalar"
    };
}
