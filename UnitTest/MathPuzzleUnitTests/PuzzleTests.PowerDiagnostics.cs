using MathSolver.Numerics;
using MathSolver.Services.Core;
using System.Text.RegularExpressions;

namespace MathSolver.Tests;

internal static partial class PuzzleTests
{
    internal static void CheckPowerDiagnostics()
    {
        var empty = ParallelBigUnsigned.Pow(2, 0, 1, null, CancellationToken.None).Diagnostics;
        var measured = empty with
        {
            ForwardTransform = TimeSpan.FromSeconds(8), ForwardLocalL1 = TimeSpan.FromSeconds(3),
            InverseTransform = TimeSpan.FromSeconds(5), InverseLocalL1 = TimeSpan.FromSeconds(2),
            Pointwise = TimeSpan.FromSeconds(1), Crt = TimeSpan.FromSeconds(2), Carry = TimeSpan.FromSeconds(1)
        };
        var phases = PowerDiagnosticsPresentation.PhaseRows(measured);
        Require(phases.Count == 5 && phases.Sum(p => p.Elapsed.TotalSeconds) == 17,
            "Overview must show the five measured phases without adding their L1 child counters again.");
        Require(PowerDiagnosticsPresentation.PhaseRows(empty).Count == 0,
            "Unmeasured or zero-duration phases must not appear as measured work.");
        var kernelRows = PowerDiagnosticsPresentation.KernelRows(empty with
        {
            UsedAvx2NttButterflies = true, Avx512RuntimeSupported = true, Avx512Enabled = true,
            KernelUsage = [new(NttKernelPhase.Forward, NttKernelIsa.Avx2, 3),
                new(NttKernelPhase.Forward, NttKernelIsa.Avx2, 4), new(NttKernelPhase.Crt, NttKernelIsa.Avx512, 0)]
        });
        Require(kernelRows.Count == 1 && kernelRows[0].Calls == 7 && kernelRows[0].Isa == NttKernelIsa.Avx2,
            "Presentation must merge positive execution samples without inferring AVX-512 from capability or configuration.");

        NttGlobalStageProfileEntry Entry(NttGlobalStageKind kind, int stage, double seconds) =>
            new(kind, 1024, stage, 0, 2, 1024, TimeSpan.FromSeconds(seconds));
        NttGlobalStageProfileEntry[] entries = [
            Entry(NttGlobalStageKind.ForwardCached, 64, 8),
            Entry(NttGlobalStageKind.ForwardCached, 32, 2),
            Entry(NttGlobalStageKind.InverseUncached, 64, 18),
            Entry(NttGlobalStageKind.InverseUncached, 32, 2),
            Entry(NttGlobalStageKind.ForwardUncached, 16, 1),
            Entry(NttGlobalStageKind.ForwardUncached, 8, .5),
            Entry(NttGlobalStageKind.InverseCached, 16, 0)
        ];
        var top = PowerDiagnosticsPresentation.StageRows(entries);
        Require(top.Count == 5 && top[0].Entry.Elapsed.TotalSeconds == 18,
            "Default stage overview must select the five hottest measured rows across all groups.");
        Require(top.Single(p => p.Entry.Kind == NttGlobalStageKind.ForwardCached && p.Entry.StageLength == 64).GroupSharePercent == 80,
            "Stage percentages must use the full matching group's time, not the selected rows or whole calculation.");
        Require(top[0].GroupSharePercent == 90,
            "Independent inverse group must retain its own percentage denominator.");
        var all = PowerDiagnosticsPresentation.StageRows(entries, limit: null);
        Require(all.Count == 6 && entries.Length == 7,
            "Show all must retain every measured stage without mutating or deleting source diagnostics.");
        Require(PowerDiagnosticsPresentation.StageRows(entries, 0).Count == 0,
            "An explicit zero limit must not expose stages.");

        var vietnamese = ReadQuizPack("QuizLocalization.vi-VN.json");
        var english = ReadQuizPack("QuizLocalization.en-US.json");
        foreach (var pair in vietnamese.Where(p => p.Key.StartsWith("PowerRoot.Diagnostics.", StringComparison.Ordinal)))
        {
            Require(english.TryGetValue(pair.Key, out var translated) && !string.IsNullOrWhiteSpace(translated),
                $"Missing English power diagnostic text: {pair.Key}");
            string[] Slots(string text) => Regex.Matches(text, @"\{[^{}]+\}").Select(m => m.Value).Order().ToArray();
            Require(Slots(pair.Value).SequenceEqual(Slots(translated!)), $"Diagnostic translation changed placeholders: {pair.Key}");
        }
        foreach (var kind in Enum.GetValues<NttGlobalStageKind>())
            Require(vietnamese.ContainsKey("PowerRoot.Diagnostics.StageGroup" + kind), $"Unlabelled NTT stage group: {kind}");
    }
}
