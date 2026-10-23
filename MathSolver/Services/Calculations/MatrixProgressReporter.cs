using System.Diagnostics;

namespace MathSolver.Services.Core;

/// <summary>Serializes workers; detailed progress stays below 100% until publication.</summary>
internal sealed class MatrixProgressReporter(IProgress<double>? legacy,
    IProgress<MatrixCalculationProgress>? detailed) : IProgress<double>
{
    private readonly object gate = new();
    private double fraction;
    private long lastReport;
    private long lastLegacyReport;
    private double lastLegacyFraction = -1;
    private MatrixCalculationStage stage = MatrixCalculationStage.Preparing;
    private bool finished;

    public void Report(double value)
    {
        lock (gate)
        {
            if (finished) return;
            fraction = Math.Max(fraction, Math.Clamp(value, 0, 1));
            long now = Stopwatch.GetTimestamp();
            if (lastLegacyFraction < 0 || fraction >= 1 && lastLegacyFraction < 1
                || fraction - lastLegacyFraction >= .005 || now - lastLegacyReport >= Stopwatch.Frequency / 12)
            {
                lastLegacyFraction = fraction;
                lastLegacyReport = now;
                legacy?.Report(fraction);
            }
            Publish(false);
        }
    }

    internal void Phase(MatrixCalculationStage value, double? progress = null)
    {
        lock (gate)
        {
            if (finished) return;
            stage = value;
            if (progress.HasValue) fraction = Math.Max(fraction, progress.Value);
            Publish(true);
        }
    }

    internal void Activity(MatrixCalculationStage value)
    {
        lock (gate)
        {
            if (finished) return;
            stage = value;
            Publish(false);
        }
    }

    private void Publish(bool force)
    {
        long now = Stopwatch.GetTimestamp();
        if (!force && now - lastReport < Stopwatch.Frequency / 12) return;
        lastReport = now;
        detailed?.Report(new(Math.Min(.99, fraction), stage));
    }

    internal void Complete()
    {
        lock (gate)
        {
            if (finished) return;
            finished = true;
            detailed?.Report(new(1, MatrixCalculationStage.Completed));
        }
    }
}
