using MathSolver.Graphics;
using MathSolver.Models;
using MathSolver.Services;
using System.Globalization;

#if WINDOWS
using System.Runtime.Intrinsics.X86;
#endif

namespace MathSolver.Views;

public partial class HardwareLlmBenchmarkView : ContentView
{
    private const int SamplesPerCategory = 10;
    private static readonly int CategoryCount =
        Enum.GetValues<LlmBenchmarkCategory>().Length;
    private static readonly int TotalSamples =
        SamplesPerCategory * CategoryCount;

    private const double AccuracyChartRowHeight = 44d;
    private const double AccuracyChartTopAxisHeight = 28d;
    private const double AccuracyChartBottomPadding = 4d;

    private CancellationTokenSource? _benchmarkCancellation;
    private TaskCompletionSource<bool>? _benchmarkCompletion;
    private bool _isRunning;
    private int _progressVersion;
#if WINDOWS
    private LlmBenchmarkRunResult? _lastBenchmarkResult;
#endif

    public bool IsBenchmarkRunning => _isRunning;

    public event Action<bool>? BenchmarkRunningChanged;

    public HardwareLlmBenchmarkView()
    {
        InitializeComponent();
        RefreshState();
    }

    public void RefreshState()
    {
        bool vietnamese =
            AppLanguageManager.CurrentLanguage ==
            AppLanguage.Vietnamese;

        SectionTitleLabel.Text = "Benchmark AI / LLM";
        SectionDescriptionLabel.Text = vietnamese
            ? "Đo tốc độ decode và độ chính xác tạo đề bằng chính validator C# của Math Solver. Mỗi dạng chạy 10 câu độc lập."
            : "Measure decode speed and word-problem accuracy with Math Solver's existing C# validator. Each category runs 10 independent samples.";

        ModelNameLabel.Text = vietnamese ? "Model" : "Model";
        EngineNameLabel.Text = vietnamese ? "Engine" : "Engine";
        IsaNameLabel.Text = vietnamese ? "LLamaSharp CPU ISA khả dụng" : "Available LLamaSharp CPU ISA";
        ThreadsNameLabel.Text = vietnamese ? "Luồng inference" : "Inference threads";
        SampleNameLabel.Text = vietnamese ? "Bộ kiểm thử" : "Test set";
        SampleValueLabel.Text = string.Format(
            CultureInfo.CurrentCulture,
            vietnamese
                ? "{0} dạng × {1} câu = {2} câu"
                : "{0} categories × {1} samples = {2} samples",
            CategoryCount,
            SamplesPerCategory,
            TotalSamples);

        ResultTitleLabel.Text = vietnamese
            ? "Kết quả benchmark AI / LLM"
            : "AI / LLM benchmark results";
        DecodeSpeedNameLabel.Text = vietnamese
            ? "Tốc độ tạo sinh trung bình"
            : "Average decode speed";
        OverallAccuracyNameLabel.Text = vietnamese
            ? "Độ chính xác tổng"
            : "Overall accuracy";
        AccuracyExplanationLabel.Text = vietnamese
            ? "Mỗi câu chỉ sinh đúng 1 lần rồi được validator C# chấm. Không retry để tránh làm sai lệch độ chính xác thực của model."
            : "Each sample is generated exactly once and scored by the C# validator. Retries are disabled so model accuracy is not inflated.";
        AccuracyChartTitleLabel.Text = vietnamese
            ? "Độ chính xác theo dạng toán"
            : "Accuracy by math category";
        CategoryHeaderLabel.Text = vietnamese ? "Dạng đề" : "Category";
        CorrectHeaderLabel.Text = vietnamese ? "Đúng" : "Valid";
        AccuracyHeaderLabel.Text = vietnamese ? "Chính xác" : "Accuracy";
        SpeedHeaderLabel.Text = "token/s";

#if WINDOWS
        string? modelPath =
            new QuizLlmModelStore().GetSavedModelPath();

        ModelValueLabel.Text =
            string.IsNullOrWhiteSpace(modelPath)
                ? (vietnamese
                    ? "Chưa chọn model Gemma 4 GGUF"
                    : "No Gemma 4 GGUF model selected")
                : Path.GetFileName(modelPath);

        EngineValueLabel.Text = "LLamaSharp 0.27 • llama.cpp CPU";
        IsaValueLabel.Text = GetLlamaCpuIsaText();
        ThreadsValueLabel.Text = string.Format(
            CultureInfo.CurrentCulture,
            vietnamese
                ? "{0} decode / {1} batch"
                : "{0} decode / {1} batch",
            LocalLlmQuizGenerator.CpuThreadCount,
            LocalLlmQuizGenerator.CpuBatchThreadCount);

        if (!_isRunning)
        {
            RunLlmBenchmarkButton.IsEnabled =
                QuizLlmModelStore.IsSupportedModelPath(modelPath);
            RunLlmBenchmarkButton.Text = vietnamese
                ? "Chạy benchmark AI / LLM"
                : "Run AI / LLM benchmark";
            RunLlmBenchmarkButton.SetDynamicResource(
                Button.BackgroundColorProperty,
                "PrimaryColor");

            if (!RunLlmBenchmarkButton.IsEnabled)
            {
                LlmBenchmarkStatusLabel.Text = vietnamese
                    ? "Hãy chọn model Gemma 4 trong Toán đố → AI/LLM trước khi benchmark."
                    : "Select a Gemma 4 model in Math Puzzle → AI/LLM before benchmarking.";
            }
            else if (_lastBenchmarkResult is not null)
            {
                RenderResult(_lastBenchmarkResult);
            }
            else if (!LlmBenchmarkResultsBorder.IsVisible)
            {
                LlmBenchmarkStatusLabel.Text = vietnamese
                    ? "Chưa chạy benchmark AI / LLM."
                    : "AI / LLM benchmark has not been run yet.";
            }
        }
#else
        ModelValueLabel.Text = vietnamese
            ? "Không khả dụng trên nền tảng này"
            : "Not available on this platform";
        EngineValueLabel.Text = "—";
        IsaValueLabel.Text = "—";
        ThreadsValueLabel.Text = "—";
        RunLlmBenchmarkButton.IsEnabled = false;
#endif
    }

    public void CancelBenchmark()
    {
        _benchmarkCancellation?.Cancel();
    }

    public async Task StopAndWaitAsync()
    {
        CancelBenchmark();
        Task? completion = _benchmarkCompletion?.Task;
        if (completion is not null)
        {
            await completion;
        }
    }

    private async void OnRunLlmBenchmarkClicked(
        object? sender,
        EventArgs e)
    {
#if WINDOWS
        if (_isRunning)
        {
            CancelBenchmark();
            return;
        }

        string? modelPath =
            new QuizLlmModelStore().GetSavedModelPath();

        if (!QuizLlmModelStore.IsSupportedModelPath(modelPath))
        {
            RefreshState();
            return;
        }

        _isRunning = true;
        BenchmarkRunningChanged?.Invoke(true);
        _benchmarkCancellation = new CancellationTokenSource();
        _benchmarkCompletion =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        SetRunningState(true);
        _lastBenchmarkResult = null;
        LlmBenchmarkResultsBorder.IsVisible = false;
        CategoryResultsContainer.Children.Clear();
        LlmBenchmarkProgressBar.Progress = 0;
        LlmBenchmarkProgressBar.IsVisible = true;
        LlmBenchmarkActivity.IsVisible = true;
        LlmBenchmarkActivity.IsRunning = true;
        LlmLiveSpeedLabel.IsVisible = true;

        try
        {
            LocalLlmRuntime.Generator.CancelScheduledModelUnload();

            LlmBenchmarkRunResult result =
                await RunBenchmarkAsync(
                    modelPath!,
                    _benchmarkCancellation.Token);

            _lastBenchmarkResult = result;
            RenderResult(result);

            LlmBenchmarkStatusLabel.Text = IsVietnamese
                ? "Benchmark AI / LLM hoàn tất."
                : "AI / LLM benchmark completed.";
        }
        catch (OperationCanceledException)
        {
            LlmBenchmarkStatusLabel.Text = IsVietnamese
                ? "Đã dừng benchmark AI / LLM."
                : "AI / LLM benchmark stopped.";
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"AI/LLM benchmark failed: {exception}");
            LlmBenchmarkStatusLabel.Text = IsVietnamese
                ? $"Benchmark thất bại: {exception.Message}"
                : $"Benchmark failed: {exception.Message}";
        }
        finally
        {
            LlmBenchmarkActivity.IsRunning = false;
            LlmBenchmarkActivity.IsVisible = false;
            LlmBenchmarkProgressBar.IsVisible = false;
            LlmLiveSpeedLabel.IsVisible = false;
            _progressVersion++;
            _isRunning = false;
            BenchmarkRunningChanged?.Invoke(false);
            SetRunningState(false);

            _benchmarkCancellation?.Dispose();
            _benchmarkCancellation = null;
            _benchmarkCompletion?.TrySetResult(true);
            _benchmarkCompletion = null;

            // Keep the shared model warm briefly, exactly like Math Puzzle.
            LocalLlmRuntime.Generator.ScheduleModelUnload();
        }
#endif
    }

#if WINDOWS
    private async Task<LlmBenchmarkRunResult> RunBenchmarkAsync(
        string modelPath,
        CancellationToken cancellationToken)
    {
        var categoryResults =
            new List<LlmBenchmarkCategoryResult>();

        int totalValid = 0;
        int totalGeneratedTokens = 0;
        double totalDecodeSeconds = 0d;
        int completedSamples = 0;

        foreach (LlmBenchmarkCategory category in
            Enum.GetValues<LlmBenchmarkCategory>())
        {
            int categoryValid = 0;
            int categoryTokens = 0;
            double categoryDecodeSeconds = 0d;

            for (int sample = 0;
                 sample < SamplesPerCategory;
                 sample++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int displaySample = sample + 1;
                UpdateProgressText(
                    category,
                    displaySample,
                    completedSamples,
                    currentSpeed: 0d);

                int progressVersion =
                    ++_progressVersion;

                var progress =
                    new Progress<LlmQuizProgress>(value =>
                    {
                        if (!_isRunning ||
                            progressVersion != _progressVersion)
                        {
                            return;
                        }

                        if (value.TokensPerSecond > 0d)
                        {
                            UpdateProgressText(
                                category,
                                displaySample,
                                completedSamples,
                                value.TokensPerSecond);
                        }
                    });

                LlmQuizGenerationResult generation =
                    await LocalLlmRuntime.Generator.GenerateAsync(
                        modelPath,
                        ArithmeticQuizMode.Essay,
                        CreateRequest(category, sample),
                        AppLanguageManager.CurrentLanguage,
                        progress,
                        cancellationToken,
                        maximumAttempts: 1);

                // Invalidate any Progress<T> callbacks that are still queued
                // on the UI thread before advancing to the next sample.
                _progressVersion++;

                if (generation.ErrorCode is
                    "ModelFileNotFound" or
                    "NotEnoughMemory" or
                    "ModelRuntimeError")
                {
                    throw new InvalidOperationException(
                        generation.ErrorCode);
                }

                if (generation.IsSuccess)
                {
                    categoryValid++;
                    totalValid++;
                }

                categoryTokens += generation.GeneratedTokenCount;
                totalGeneratedTokens += generation.GeneratedTokenCount;

                if (generation.GeneratedTokenCount > 0 &&
                    generation.TokensPerSecond > 0d)
                {
                    double seconds =
                        generation.GeneratedTokenCount /
                        generation.TokensPerSecond;
                    categoryDecodeSeconds += seconds;
                    totalDecodeSeconds += seconds;
                }

                completedSamples++;
                LlmBenchmarkProgressBar.Progress =
                    completedSamples / (double)TotalSamples;
            }

            categoryResults.Add(
                new(
                    category,
                    categoryValid,
                    SamplesPerCategory,
                    CalculateAggregateTokensPerSecond(
                        categoryTokens,
                        categoryDecodeSeconds)));
        }

        return new(
            categoryResults,
            totalValid,
            TotalSamples,
            totalGeneratedTokens,
            CalculateAggregateTokensPerSecond(
                totalGeneratedTokens,
                totalDecodeSeconds));
    }

    private void UpdateProgressText(
        LlmBenchmarkCategory category,
        int sample,
        int completedSamples,
        double currentSpeed)
    {
        string categoryName =
            GetCategoryName(category);

        LlmBenchmarkStatusLabel.Text = IsVietnamese
            ? $"{categoryName} • câu {sample}/{SamplesPerCategory} • tổng {completedSamples + 1}/{TotalSamples}"
            : $"{categoryName} • sample {sample}/{SamplesPerCategory} • total {completedSamples + 1}/{TotalSamples}";

        if (currentSpeed > 0d)
        {
            LlmLiveSpeedLabel.Text = string.Format(
                CultureInfo.CurrentCulture,
                IsVietnamese
                    ? "Tốc độ hiện tại: {0:F1} token/s"
                    : "Current speed: {0:F1} token/s",
                currentSpeed);
        }
        else
        {
            LlmLiveSpeedLabel.Text = IsVietnamese
                ? "Đang xử lý prompt..."
                : "Processing prompt...";
        }
    }

    private void RenderResult(
        LlmBenchmarkRunResult result)
    {
        LlmBenchmarkResultsBorder.IsVisible = true;
        DecodeSpeedValueLabel.Text = string.Format(
            CultureInfo.CurrentCulture,
            "{0:F1} token/s",
            result.TokensPerSecond);

        double accuracy =
            result.TotalSamples == 0
                ? 0d
                : result.ValidSamples * 100d /
                    result.TotalSamples;

        OverallAccuracyValueLabel.Text = string.Format(
            CultureInfo.CurrentCulture,
            "{0}/{1} • {2:F0}%",
            result.ValidSamples,
            result.TotalSamples,
            accuracy);

        RenderAccuracyChart(result.Categories);
        CategoryResultsContainer.Children.Clear();

        foreach (LlmBenchmarkCategoryResult category in
            result.Categories)
        {
            double categoryAccuracy =
                category.Total == 0
                    ? 0d
                    : category.Valid * 100d /
                        category.Total;

            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = new GridLength(72) },
                    new ColumnDefinition { Width = new GridLength(88) },
                    new ColumnDefinition { Width = new GridLength(72) }
                },
                ColumnSpacing = 18
            };

            var name = new Label
            {
                Text = GetCategoryName(category.Category),
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1,
                TextColor = GetResourceColor("TextPrimaryColor")
            };
            var correct = new Label
            {
                Text = $"{category.Valid}/{category.Total}",
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.End,
                TextColor = GetResourceColor("TextPrimaryColor")
            };
            var percent = new Label
            {
                Text = string.Format(
                    CultureInfo.CurrentCulture,
                    "{0:F0}%",
                    categoryAccuracy),
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.End,
                TextColor = GetResourceColor("PrimaryColor")
            };
            var speed = new Label
            {
                Text = string.Format(
                    CultureInfo.CurrentCulture,
                    "{0:F1}",
                    category.TokensPerSecond),
                HorizontalTextAlignment = TextAlignment.End,
                TextColor = GetResourceColor("TextPrimaryColor")
            };

            Grid.SetColumn(correct, 1);
            Grid.SetColumn(percent, 2);
            Grid.SetColumn(speed, 3);
            row.Children.Add(name);
            row.Children.Add(correct);
            row.Children.Add(percent);
            row.Children.Add(speed);
            CategoryResultsContainer.Children.Add(row);
        }
    }

    private void RenderAccuracyChart(
        IReadOnlyList<LlmBenchmarkCategoryResult> categories)
    {
        var items =
            categories
                .Select(category =>
                {
                    double accuracy =
                        category.Total == 0
                            ? 0d
                            : category.Valid * 100d / category.Total;

                    return new LlmAccuracyChartItem(
                        GetCategoryName(category.Category),
                        accuracy);
                })
                .ToArray();

        AccuracyChartView.HeightRequest =
            AccuracyChartTopAxisHeight +
            items.Length * AccuracyChartRowHeight +
            AccuracyChartBottomPadding;

        AccuracyChartView.Drawable =
            new LlmAccuracyHorizontalChartDrawable
            {
                Items = items
            };

        AccuracyChartView.Invalidate();
    }

    private static QuizProblemRequest CreateRequest(
        LlmBenchmarkCategory category,
        int sampleIndex)
    {
        return category switch
        {
            LlmBenchmarkCategory.Arithmetic =>
                new(
                    QuizProblemKind.Arithmetic,
                    ArithmeticOperation:
                        (ArithmeticOperation)(sampleIndex % 4)),

            LlmBenchmarkCategory.Fraction =>
                new(
                    QuizProblemKind.Fraction,
                    FractionOperation:
                        (FractionOperation)(sampleIndex % 4)),

            LlmBenchmarkCategory.FindX =>
                new(QuizProblemKind.FindX),

            LlmBenchmarkCategory.Geometry =>
                new(QuizProblemKind.Geometry),

            LlmBenchmarkCategory.Proportion =>
                new(
                    QuizProblemKind.Proportion,
                    ProportionType:
                        sampleIndex % 2 == 0
                            ? ProportionQuizType.Direct
                            : ProportionQuizType.Inverse),

            LlmBenchmarkCategory.Motion =>
                new(QuizProblemKind.Motion),

            _ => throw new ArgumentOutOfRangeException(
                nameof(category))
        };
    }

    private static double CalculateAggregateTokensPerSecond(
        int tokens,
        double seconds) =>
        tokens <= 0 || seconds <= 0d
            ? 0d
            : tokens / seconds;

    private static string GetLlamaCpuIsaText()
    {
        if (Avx512F.IsSupported)
        {
            return "AVX-512";
        }

        if (Avx2.IsSupported)
        {
            return Fma.IsSupported
                ? "AVX2 + FMA"
                : "AVX2";
        }

        if (Avx.IsSupported)
        {
            return "AVX";
        }

        return Sse2.IsSupported
            ? "SSE2 / Scalar fallback"
            : "Scalar";
    }
#endif

    private void SetRunningState(
        bool running)
    {
        RunLlmBenchmarkButton.IsEnabled = true;
        RunLlmBenchmarkButton.Text = running
            ? (IsVietnamese
                ? "■ Dừng benchmark AI / LLM"
                : "■ Stop AI / LLM benchmark")
            : (IsVietnamese
                ? "Chạy benchmark AI / LLM"
                : "Run AI / LLM benchmark");

        if (running)
        {
            RunLlmBenchmarkButton.BackgroundColor =
                GetResourceColor("DangerColor");
        }
        else
        {
            RunLlmBenchmarkButton.SetDynamicResource(
                Button.BackgroundColorProperty,
                "PrimaryColor");
            RefreshState();
        }
    }

    private string GetCategoryName(
        LlmBenchmarkCategory category) =>
        (category, IsVietnamese) switch
        {
            (LlmBenchmarkCategory.Arithmetic, true) => "Phép tính cơ bản",
            (LlmBenchmarkCategory.Fraction, true) => "Phân số",
            (LlmBenchmarkCategory.FindX, true) => "Tìm x",
            (LlmBenchmarkCategory.Geometry, true) => "Hình học",
            (LlmBenchmarkCategory.Proportion, true) => "Tỉ lệ thuận / nghịch",
            (LlmBenchmarkCategory.Motion, true) => "Chuyển động",
            (LlmBenchmarkCategory.Arithmetic, false) => "Basic arithmetic",
            (LlmBenchmarkCategory.Fraction, false) => "Fractions",
            (LlmBenchmarkCategory.FindX, false) => "Find x",
            (LlmBenchmarkCategory.Geometry, false) => "Geometry",
            (LlmBenchmarkCategory.Proportion, false) => "Direct / inverse proportion",
            (LlmBenchmarkCategory.Motion, false) => "Motion",
            _ => category.ToString()
        };

    private static Color GetResourceColor(
        string key)
    {
        if (Application.Current?.Resources.TryGetValue(
                key,
                out object? value) == true &&
            value is Color color)
        {
            return color;
        }

        return Colors.Black;
    }

    private static bool IsVietnamese =>
        AppLanguageManager.CurrentLanguage ==
        AppLanguage.Vietnamese;

    private enum LlmBenchmarkCategory
    {
        Arithmetic,
        Fraction,
        FindX,
        Geometry,
        Proportion,
        Motion
    }

    private sealed record LlmBenchmarkCategoryResult(
        LlmBenchmarkCategory Category,
        int Valid,
        int Total,
        double TokensPerSecond);

    private sealed record LlmBenchmarkRunResult(
        IReadOnlyList<LlmBenchmarkCategoryResult> Categories,
        int ValidSamples,
        int TotalSamples,
        int GeneratedTokens,
        double TokensPerSecond);
}
