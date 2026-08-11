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
            ["Quiz.LlmSettingsDescription"] = "AI viết đề theo chương trình tiểu học Việt Nam. Ứng dụng tự tính đáp án và kiểm tra lại trước khi hiển thị.",
            ["Quiz.DownloadGemma4"] = "Tải Gemma 4 từ Hugging Face",
            ["Quiz.StopModelDownload"] = "Dừng tải model",
            ["Quiz.ChooseDownloadModelTitle"] = "Chọn model Gemma 4",
            ["Quiz.DownloadE2BOption"] = "E2B • Nhẹ hơn • 3,35 GB",
            ["Quiz.DownloadE4BOption"] = "E4B • Chất lượng tốt hơn • 5,15 GB",
            ["Quiz.OpenModelWebsiteOption"] = "Mở trang {0} trên Hugging Face",
            ["Quiz.OpenModelWebsiteFailed"] = "Không thể mở trang model trên Hugging Face.",
            ["Quiz.Cancel"] = "Hủy",
            ["Quiz.DownloadConfirmTitle"] = "Xác nhận tải model",
            ["Quiz.DownloadConfirmMessage"] = "Tải {0} (khoảng {1} GB) từ Hugging Face? Nên dùng Wi-Fi và bảo đảm thiết bị còn đủ dung lượng trống.",
            ["Quiz.DownloadAction"] = "Tải xuống",
            ["Quiz.DownloadingModel"] = "Đang bắt đầu tải {0}...",
            ["Quiz.DownloadingModelProgress"] = "Đang tải {0}: {1}% • {2}/{3} GB",
            ["Quiz.DownloadComplete"] = "Đã tải và chọn {0}. File GGUF đã sẵn sàng.",
            ["Quiz.DownloadCancelled"] = "Đã dừng tải. Phần đã tải được giữ lại để tiếp tục lần sau.",
            ["Quiz.DownloadInvalid"] = "File tải về không phải model Gemma 4 GGUF hợp lệ. Hãy thử tải lại.",
            ["Quiz.DownloadAccessDenied"] = "Hugging Face không cho phép tải model này. Hãy kiểm tra kết nối hoặc quyền truy cập rồi thử lại.",
            ["Quiz.DownloadFailed"] = "Không thể tải model. Hãy kiểm tra kết nối mạng; lần tải sau sẽ tiếp tục từ phần đã có.",
            ["Quiz.SelectModel"] = "Chọn model",
            ["Quiz.EjectModel"] = "Eject model",
            ["Quiz.OpenInFileExplorer"] = "Mở trong File Explorer",
            ["Quiz.ModelLocationTitle"] = "Vị trí AI model",
            ["Quiz.ModelLocationUnavailable"] = "Nền tảng này không cho phép mở trực tiếp thư mục riêng của ứng dụng. AI model đang nằm tại:\n{0}",
            ["Quiz.NoModelSelected"] = "Chưa chọn model GGUF",
            ["Quiz.ModelRecommendation"] = "Ứng dụng chỉ hỗ trợ Gemma 4. Nên chọn E2B/E4B Instruct bản Q4 vì tạo câu tiếng Việt tự nhiên, đủ tốt cho toán tiểu học và nhẹ hơn để tiết kiệm pin.",
            ["Quiz.RecommendedModelDetected"] = "Gemma 4 bản gọn nhẹ đã sẵn sàng để tạo đề.",
            ["Quiz.CreateWithAi"] = "Tạo đề bằng AI",
            ["Quiz.SelectModelPickerTitle"] = "Chọn file Gemma 4 GGUF",
            ["Quiz.ImportingModel"] = "Đang kiểm tra file Gemma 4...",
            ["Quiz.ModelReady"] = "File model GGUF đúng định dạng.",
            ["Quiz.ModelEjected"] = "Đã Eject model và đặt lại toàn bộ phiên luyện tập.",
            ["Quiz.UnsupportedModelFamily"] = "Chỉ hỗ trợ model Gemma 4 dành cho tạo đề. Hãy chọn đúng file Gemma 4 GGUF.",
            ["Quiz.ModelTooLarge"] = "Model này quá nặng (trên 5,5 GB) nên có thể làm máy nóng và hao pin. Hãy chọn Gemma 4 E2B/E4B bản Q4 nhỏ hơn.",
            ["Quiz.InvalidModelFile"] = "File đã chọn không phải là file GGUF hợp lệ.",
            ["Quiz.ModelImportError"] = "Không thể mở hoặc lưu model đã chọn.",
            ["Quiz.SelectModelFirst"] = "Hãy chọn model GGUF trước khi tạo đề bằng AI.",
            ["Quiz.LlmReady"] = "Chọn phép tính rồi bấm Tạo đề bằng AI.",
            ["Quiz.FirstModelGreeting"] = "Chào em! Giáo viên AI đang chuẩn bị bài toán đầu tiên nhé...",
            ["Quiz.LoadingModel"] = "Đang tạo sinh đề bài bằng AI...",
            ["Quiz.ModelLoaded"] = "AI đang chuẩn bị nội dung câu hỏi...",
            ["Quiz.GeneratingAttempt"] = "AI đang viết đề bài, lần {0}/{1}...",
            ["Quiz.ValidatingProblem"] = "Đang kiểm tra lại đề bài và đáp án...",
            ["Quiz.RetryingProblem"] = "Đề chưa hợp lệ. AI đang tự tạo lại...",
            ["Quiz.DisposingModel"] = "Đang Eject model...",
            ["Quiz.GenerationSucceeded"] = "Đề bài đã sẵn sàng.",
            ["Quiz.GenerationCancelled"] = "Đã dừng tạo đề.",
            ["Quiz.GenerationFailedAfterRetries"] = "AI đã tạo sai dữ kiện 3 lần. Hãy bấm Tạo đề bằng AI để thử lại.",
            ["Quiz.NotEnoughMemory"] = "Model này quá nặng đối với thiết bị. Hãy chọn Gemma 4 E2B/E4B bản Q4 nhỏ hơn.",
            ["Quiz.ModelRuntimeError"] = "Không thể dùng model này trên thiết bị. Hãy chọn Gemma 4 E2B/E4B bản Q4 nhỏ hơn.",
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
            ["Quiz.LlmSettingsDescription"] = "AI writes elementary math problems. The app calculates the answer and checks each problem before showing it.",
            ["Quiz.DownloadGemma4"] = "Download Gemma 4 from Hugging Face",
            ["Quiz.StopModelDownload"] = "Stop model download",
            ["Quiz.ChooseDownloadModelTitle"] = "Choose a Gemma 4 model",
            ["Quiz.DownloadE2BOption"] = "E2B • Lighter • 3.35 GB",
            ["Quiz.DownloadE4BOption"] = "E4B • Better quality • 5.15 GB",
            ["Quiz.OpenModelWebsiteOption"] = "Open the {0} page on Hugging Face",
            ["Quiz.OpenModelWebsiteFailed"] = "The model page on Hugging Face could not be opened.",
            ["Quiz.Cancel"] = "Cancel",
            ["Quiz.DownloadConfirmTitle"] = "Confirm model download",
            ["Quiz.DownloadConfirmMessage"] = "Download {0} (about {1} GB) from Hugging Face? Wi-Fi is recommended, and the device needs enough free storage.",
            ["Quiz.DownloadAction"] = "Download",
            ["Quiz.DownloadingModel"] = "Starting the {0} download...",
            ["Quiz.DownloadingModelProgress"] = "Downloading {0}: {1}% • {2}/{3} GB",
            ["Quiz.DownloadComplete"] = "{0} was downloaded and selected. The GGUF file is ready.",
            ["Quiz.DownloadCancelled"] = "Download stopped. The completed portion was kept so it can resume next time.",
            ["Quiz.DownloadInvalid"] = "The downloaded file is not a valid Gemma 4 GGUF model. Try downloading it again.",
            ["Quiz.DownloadAccessDenied"] = "Hugging Face did not allow this model download. Check the connection or access permission, then try again.",
            ["Quiz.DownloadFailed"] = "The model could not be downloaded. Check the network connection; the next download will resume from the completed portion.",
            ["Quiz.SelectModel"] = "Choose model",
            ["Quiz.EjectModel"] = "Eject model",
            ["Quiz.OpenInFileExplorer"] = "Open in File Explorer",
            ["Quiz.ModelLocationTitle"] = "AI model location",
            ["Quiz.ModelLocationUnavailable"] = "This platform cannot open the app's private folder directly. The AI model is stored at:\n{0}",
            ["Quiz.NoModelSelected"] = "No GGUF model selected",
            ["Quiz.ModelRecommendation"] = "The app supports Gemma 4 only. E2B/E4B Instruct Q4 is recommended because it writes natural questions, is strong enough for elementary math, and uses less battery.",
            ["Quiz.RecommendedModelDetected"] = "A lightweight Gemma 4 model is ready to create questions.",
            ["Quiz.CreateWithAi"] = "Create with AI",
            ["Quiz.SelectModelPickerTitle"] = "Choose a Gemma 4 GGUF file",
            ["Quiz.ImportingModel"] = "Checking the Gemma 4 file...",
            ["Quiz.ModelReady"] = "The GGUF model file is valid.",
            ["Quiz.ModelEjected"] = "The model was ejected and the practice session was reset.",
            ["Quiz.UnsupportedModelFamily"] = "Only Gemma 4 models are supported for question creation. Choose a Gemma 4 GGUF file.",
            ["Quiz.ModelTooLarge"] = "This model is too large (over 5.5 GB) and may heat the device or drain the battery. Choose a smaller Gemma 4 E2B/E4B Q4 model.",
            ["Quiz.InvalidModelFile"] = "The selected file is not a valid GGUF file.",
            ["Quiz.ModelImportError"] = "The selected model could not be opened or saved.",
            ["Quiz.SelectModelFirst"] = "Choose a GGUF model before creating a question with AI.",
            ["Quiz.LlmReady"] = "Choose an operation, then select Create with AI.",
            ["Quiz.FirstModelGreeting"] = "Hello! Your AI teacher is preparing the first math problem...",
            ["Quiz.LoadingModel"] = "Creating a math problem with AI...",
            ["Quiz.ModelLoaded"] = "AI is preparing the question...",
            ["Quiz.GeneratingAttempt"] = "AI is writing the problem, attempt {0}/{1}...",
            ["Quiz.ValidatingProblem"] = "Checking the problem and answer...",
            ["Quiz.RetryingProblem"] = "That problem was not valid. AI is trying again...",
            ["Quiz.DisposingModel"] = "Ejecting the model...",
            ["Quiz.GenerationSucceeded"] = "The problem is ready.",
            ["Quiz.GenerationCancelled"] = "Question generation stopped.",
            ["Quiz.GenerationFailedAfterRetries"] = "AI produced invalid facts 3 times. Select Create with AI to try again.",
            ["Quiz.NotEnoughMemory"] = "This model is too large for the device. Choose a smaller Gemma 4 E2B/E4B Q4 model.",
            ["Quiz.ModelRuntimeError"] = "This model could not run on the device. Choose a smaller Gemma 4 E2B/E4B Q4 model.",
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
