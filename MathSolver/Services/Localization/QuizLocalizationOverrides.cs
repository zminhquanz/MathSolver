namespace MathSolver.Services.Localization;

/// <summary>
/// Stable strings for the optional local-LLM quiz surface. Keeping these
/// isolated prevents an older imported language pack from replacing new AI
/// controls with raw [Quiz.*] keys. Non-Vietnamese cultures use English as the
/// safe fallback until a custom pack adds its own translations.
/// </summary>
internal static class QuizLocalizationOverrides
{
    private static readonly IReadOnlyDictionary<string, string> Vietnamese =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Quiz.PracticeSubtitle"] = "Thuật toán • Giáo viên AI • Đúng/Sai • 4 đáp án",
            ["Quiz.SourceTitle"] = "1. Chọn cách tạo đề",
            ["Quiz.AlgorithmSource"] = "Thuật toán",
            ["Quiz.LocalLlmSource"] = "AI / LLM",
            ["Quiz.ModeTitle"] = "2. Chọn kiểu câu hỏi",
            ["Quiz.OperationTitle"] = "3. Chọn phép tính",
            ["Quiz.LlmSettingsTitle"] = "Giáo viên AI cục bộ",
            ["Quiz.LlmSettingsDescription"] = "AI viết đề theo chương trình tiểu học Việt Nam. Engine giữ dữ kiện, tính đáp án và kiểm tra lại đề trước khi hiển thị.",
            ["Quiz.SelectModel"] = "Chọn model",
            ["Quiz.RejectModel"] = "Reject model",
            ["Quiz.NoModelSelected"] = "Chưa chọn model GGUF",
            ["Quiz.ModelRecommendation"] = "Khuyên dùng Gemma 4 E2B Instruct Q4_K_M hoặc IQ4_XS để cân bằng RAM và chất lượng.",
            ["Quiz.RecommendedModelDetected"] = "Đã nhận diện mức lượng tử gọn nhẹ được khuyên dùng.",
            ["Quiz.CreateWithAi"] = "Tạo đề bằng AI",
            ["Quiz.LlmResourceHint"] = "4 luồng CPU • tối đa 240 token • giữ model trong RAM và giải phóng sau 60 giây khi rời Toán đố",
            ["Quiz.SelectModelPickerTitle"] = "Chọn model GGUF cho giáo viên AI",
            ["Quiz.ImportingModel"] = "Đang kiểm tra file model GGUF...",
            ["Quiz.ModelReady"] = "File model GGUF đúng định dạng. Bấm Tạo đề bằng AI để tải model.",
            ["Quiz.ModelRejected"] = "Đã reject model. Hãy chọn file GGUF khác.",
            ["Quiz.InvalidModelFile"] = "File model phải có đuôi .gguf và header GGUF hợp lệ.",
            ["Quiz.ModelImportError"] = "Không thể mở hoặc lưu model đã chọn.",
            ["Quiz.SelectModelFirst"] = "Hãy chọn model GGUF trước khi tạo đề bằng AI.",
            ["Quiz.LlmReady"] = "Chọn phép tính rồi bấm Tạo đề bằng AI.",
            ["Quiz.FirstModelGreeting"] = "Chào em! Giáo viên AI đang chuẩn bị model lần đầu để tạo một bài toán thật gần gũi nhé...",
            ["Quiz.LoadingModel"] = "Đang tải model bằng 4 luồng CPU...",
            ["Quiz.ModelLoaded"] = "Model đã tải xong. Đang chuẩn bị dữ kiện...",
            ["Quiz.GeneratingAttempt"] = "Đang tạo đề, lần {0}/{1}...",
            ["Quiz.ValidatingProblem"] = "Engine đang kiểm tra dữ kiện và đáp án...",
            ["Quiz.RetryingProblem"] = "Đề chưa hợp lệ. AI đang tự tạo lại...",
            ["Quiz.DisposingModel"] = "Đang giải phóng model khỏi RAM...",
            ["Quiz.GenerationSucceeded"] = "Đề đã được engine kiểm tra và xác nhận hợp lệ.",
            ["Quiz.GenerationCancelled"] = "Đã dừng tạo đề và giải phóng tài nguyên.",
            ["Quiz.GenerationFailedAfterRetries"] = "AI đã tạo sai dữ kiện 3 lần. Hãy bấm Tạo đề bằng AI để thử lại.",
            ["Quiz.NotEnoughMemory"] = "Thiết bị không đủ RAM để tải model này. Hãy chọn bản Q4_K_M hoặc IQ4_XS nhỏ hơn.",
            ["Quiz.ModelRuntimeError"] = "Không thể chạy model trên thiết bị này. Hãy kiểm tra model GGUF và backend CPU.",
            ["Quiz.WordProblemTitle"] = "Bài toán do giáo viên AI tạo",
            ["Quiz.PresentedAnswer"] = "Đáp án được đưa ra: {0} {1}",
            ["Quiz.SolutionTitle"] = "Lời giải"
        };

    private static readonly IReadOnlyDictionary<string, string> English =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Quiz.PracticeSubtitle"] = "Algorithm • AI teacher • True/False • 4 choices",
            ["Quiz.SourceTitle"] = "1. Choose how to create questions",
            ["Quiz.AlgorithmSource"] = "Algorithm",
            ["Quiz.LocalLlmSource"] = "AI / LLM",
            ["Quiz.ModeTitle"] = "2. Choose a question mode",
            ["Quiz.OperationTitle"] = "3. Choose an operation",
            ["Quiz.LlmSettingsTitle"] = "Local AI teacher",
            ["Quiz.LlmSettingsDescription"] = "AI writes problems for an English-language elementary curriculum. The engine owns the facts, calculates the answer, and validates every problem before display.",
            ["Quiz.SelectModel"] = "Choose model",
            ["Quiz.RejectModel"] = "Reject model",
            ["Quiz.NoModelSelected"] = "No GGUF model selected",
            ["Quiz.ModelRecommendation"] = "Gemma 4 E2B Instruct Q4_K_M or IQ4_XS is recommended for a good balance of memory and quality.",
            ["Quiz.RecommendedModelDetected"] = "A recommended lightweight quantization was detected.",
            ["Quiz.CreateWithAi"] = "Create with AI",
            ["Quiz.LlmResourceHint"] = "4 CPU threads • up to 240 output tokens • model kept in RAM and released 60 seconds after leaving Quiz",
            ["Quiz.SelectModelPickerTitle"] = "Choose a GGUF model for the AI teacher",
            ["Quiz.ImportingModel"] = "Checking the GGUF model file...",
            ["Quiz.ModelReady"] = "The GGUF model file is valid. Select Create with AI to load it.",
            ["Quiz.ModelRejected"] = "The model was rejected. Choose another GGUF file.",
            ["Quiz.InvalidModelFile"] = "The model must have a .gguf extension and a valid GGUF header.",
            ["Quiz.ModelImportError"] = "The selected model could not be opened or saved.",
            ["Quiz.SelectModelFirst"] = "Choose a GGUF model before creating a question with AI.",
            ["Quiz.LlmReady"] = "Choose an operation, then select Create with AI.",
            ["Quiz.FirstModelGreeting"] = "Hello! Your AI teacher is preparing the model for the first time so we can make a friendly math problem together...",
            ["Quiz.LoadingModel"] = "Loading the model with 4 CPU threads...",
            ["Quiz.ModelLoaded"] = "The model is loaded. Preparing the required facts...",
            ["Quiz.GeneratingAttempt"] = "Creating a problem, attempt {0}/{1}...",
            ["Quiz.ValidatingProblem"] = "The engine is validating the facts and answer...",
            ["Quiz.RetryingProblem"] = "That problem was not valid. AI is trying again...",
            ["Quiz.DisposingModel"] = "Releasing the model from RAM...",
            ["Quiz.GenerationSucceeded"] = "The problem was checked and approved by the engine.",
            ["Quiz.GenerationCancelled"] = "Question generation stopped and its resources were released.",
            ["Quiz.GenerationFailedAfterRetries"] = "AI produced invalid facts 3 times. Select Create with AI to try again.",
            ["Quiz.NotEnoughMemory"] = "This device does not have enough memory for that model. Choose a smaller Q4_K_M or IQ4_XS file.",
            ["Quiz.ModelRuntimeError"] = "The model could not run on this device. Check the GGUF model and CPU backend.",
            ["Quiz.WordProblemTitle"] = "Problem created by your AI teacher",
            ["Quiz.PresentedAnswer"] = "Proposed answer: {0} {1}",
            ["Quiz.SolutionTitle"] = "Solution"
        };

    public static bool TryGetValue(
        string key,
        string culture,
        out string value)
    {
        IReadOnlyDictionary<string, string> strings =
            culture.StartsWith(
                "vi",
                StringComparison.OrdinalIgnoreCase)
                ? Vietnamese
                : English;

        return strings.TryGetValue(
            key,
            out value!);
    }
}
