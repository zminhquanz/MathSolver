# Services

[Bản đồ toàn project](../FOLDER_STRUCTURE.md)

## Appearance

Theme, font, màu và style giao diện.

- [AndroidMaterialYouManager.cs](Appearance/AndroidMaterialYouManager.cs)
- [AppFontCatalog.cs](Appearance/AppFontCatalog.cs)
- [AppFontManager.cs](Appearance/AppFontManager.cs)
- [AppThemeManager.cs](Appearance/AppThemeManager.cs)
- [SelectionButtonStyler.cs](Appearance/SelectionButtonStyler.cs)
- [ThemeResource.cs](Appearance/ThemeResource.cs)

## Calculations

Engine giải toán và điều phối thuật toán theo từng phép tính.

- [ArithmeticMeanEngine.cs](Calculations/ArithmeticMeanEngine.cs)
- [BasicArithmeticEngine.cs](Calculations/BasicArithmeticEngine.cs)
- [FindXEngine.cs](Calculations/FindXEngine.cs)
- [FractionCalculationEngine.cs](Calculations/FractionCalculationEngine.cs)
- [RationalExpressionEngine.cs](Calculations/RationalExpressionEngine.cs): đối tượng `Shared` tính biểu thức hữu tỉ bằng `BigInteger`/`ReducedFraction`, dùng chung cho Giải toán > Phân số và sinh/chấm đề Toán đố. Parser được chuyển từ `EssayCalculationEvaluator`; lớp cũ chỉ còn chuyển đổi kiểu dữ liệu cho các bộ chấm bài. Hỗ trợ ngoặc `()[]{}`, ưu tiên phép tính và tùy chọn ghi từng phép tính thành lời giải. Giới hạn 256 ký tự, 20 mức ngoặc, mỗi số đầu vào 60 ký tự và kết quả trung gian 16.384 bit; không dùng số thực để xấp xỉ đáp án. Dùng `÷` hoặc `:` để chia các phân số viết dưới dạng `a/b`.
- [GeometryCalculationEngine.cs](Calculations/GeometryCalculationEngine.cs)
- [LinearEquationEngine.cs](Calculations/LinearEquationEngine.cs)
- [LongDivisionCalculator.cs](Calculations/LongDivisionCalculator.cs)
- [MeasurementEngine.cs](Calculations/MeasurementEngine.cs)
- [NumberTheoryEngine.cs](Calculations/NumberTheoryEngine.cs): tính UCLN/BCNN bằng `BigInteger`, trả các phép chia Euclid để giao diện trình bày từng bước. Chấp nhận số nguyên không âm và một số bằng 0; từ chối cặp 0, 0. BCNN có một số bằng 0 được lấy bằng 0 theo quy ước. Parser nhập liệu giới hạn 128 chữ số, không dùng số thực.
- [NumberTheoryPrimeFactors.cs](Calculations/NumberTheoryPrimeFactors.cs): lời giải lớp 6 phân tích hai số dương thành lũy thừa nguyên tố, chọn số mũ nhỏ nhất cho UCLN và lớn nhất cho BCNN. Thử các số nguyên tố đến 100.000; phần còn lại chỉ được gắn là nguyên tố khi đã chứng minh đủ bằng giới hạn căn bậc hai. Phân tích chưa xong giữ riêng `UnfactoredPart`, không tạo kết luận theo thừa số; giao diện vẫn có đáp số và lời giải Euclid chính xác. Số 1 có phân tích rỗng, số 0 được giải thích bằng quy tắc riêng. Dữ liệu phân tích được giữ lại khi đổi UCLN/BCNN hoặc ngôn ngữ.
- [PowerRootEngine.cs](Calculations/PowerRootEngine.cs)
- [PowerDiagnosticsPresentation.cs](Calculations/PowerDiagnosticsPresentation.cs): chọn các công đoạn có số đo, giữ riêng mục con và chọn stage NTT theo thời gian; không thay đổi kernel hoặc bộ đếm thuật toán.
- [QuadraticEquationEngine.cs](Calculations/QuadraticEquationEngine.cs)

## QuestionBank

Ngân hàng đề SQLite và tác vụ AI bổ trợ, độc lập với màn hình làm bài.
C# tạo số, tên và đáp án; model viết mẫu đề/câu lời giải có biến thay thế cho bốn phép tính cơ bản.

- [BasicQuestionContract.cs](QuestionBank/BasicQuestionContract.cs)
- [BasicQuestionTemplates.cs](QuestionBank/BasicQuestionTemplates.cs)
- [AdditionQuestionCatalogue.cs](QuestionBank/AdditionQuestionCatalogue.cs): version 3 addition settings, typed actor/activity roles, countable units and relationship/topic rotation.
- [AdditionQuestionValidator.cs](QuestionBank/AdditionQuestionValidator.cs): scene-aware addition fact, relationship and target validation.
- [QuestionNames.cs](QuestionBank/QuestionNames.cs)
- [BasicQuestionPrompt.cs](QuestionBank/BasicQuestionPrompt.cs)
- [BasicQuestionValidator.cs](QuestionBank/BasicQuestionValidator.cs)
- [TemplateQuestionValidator.cs](QuestionBank/TemplateQuestionValidator.cs)
- [QuestionBankStore.cs](QuestionBank/QuestionBankStore.cs)
- [BasicPracticeQuestionProvider.cs](QuestionBank/BasicPracticeQuestionProvider.cs)
- [QuestionBankStore.Data.cs](QuestionBank/QuestionBankStore.Data.cs)
- [QuestionBankWorkbook.cs](QuestionBank/QuestionBankWorkbook.cs)
- [SQLite and Excel instructions](QuestionBank/README.md)
- [AiQuestionGenerationService.cs](QuestionBank/AiQuestionGenerationService.cs)
- [GgufQuestionRuntime.cs](QuestionBank/GgufQuestionRuntime.cs)
- [AiModelLibrary.cs](QuestionBank/AiModelLibrary.cs)
- [AiQuestionBank.cs](QuestionBank/AiQuestionBank.cs)

## Formatting

Định dạng nhập/xuất số và sao chép kết quả.

- [IntegerInputFormatter.cs](Formatting/IntegerInputFormatter.cs)
- [RationalDecimalFormatter.cs](Formatting/RationalDecimalFormatter.cs)
- [ResultClipboardService.cs](Formatting/ResultClipboardService.cs)
- [ResultNumberDisplayMode.cs](Formatting/ResultNumberDisplayMode.cs)

## Interaction

Hộp thoại, cuộn tự động và hỗ trợ tương tác control.

- [AndroidPickerVisualHelper.cs](Interaction/AndroidPickerVisualHelper.cs)
- [CalculationAutoScroll.cs](Interaction/CalculationAutoScroll.cs): chỉ cuộn sau khi bấm Tính toán trong tab Giải toán. Tab Toán đố xử lý riêng việc cuộn đến sơ đồ khi bấm Xem sơ đồ; các phần mở rộng khác không tự cuộn.
- [MaterialDialogService.cs](Interaction/MaterialDialogService.cs)

## Localization

Chọn ngôn ngữ, đọc/kiểm tra gói dịch và định dạng chuỗi dịch.

- [AppLanguageManager.cs](Localization/AppLanguageManager.cs)
- [JsonLocalizationProvider.cs](Localization/JsonLocalizationProvider.cs)
- [LanguagePackValidator.cs](Localization/LanguagePackValidator.cs)
- [LocalizationKeys.cs](Localization/LocalizationKeys.cs)
- [LocalizationManager.cs](Localization/LocalizationManager.cs)
- [LocalizationService.cs](Localization/LocalizationService.cs)
- [LocalizedTemplateFormatter.cs](Localization/LocalizedTemplateFormatter.cs)
- [QuizLocalizationOverrides.cs](Localization/QuizLocalizationOverrides.cs)

## Performance

Chính sách SIMD và ngân sách luồng CPU dùng chung.

- [CalculationAccelerationManager.cs](Performance/CalculationAccelerationManager.cs)
- [CalculationThreadingManager.cs](Performance/CalculationThreadingManager.cs)

## Quizzes

Sinh đề theo quy tắc, chương trình học, kiểm tra đáp án và lời giải.

- [ArithmeticQuizGenerator.cs](Quizzes/ArithmeticQuizGenerator.cs)
- [AverageQuizGenerator.cs](Quizzes/AverageQuizGenerator.cs)
- [ElementaryWordProblemContextCatalog.cs](Quizzes/ElementaryWordProblemContextCatalog.cs)
- [ElementaryWordProblemSolutionFormatter.cs](Quizzes/ElementaryWordProblemSolutionFormatter.cs)
- [EssayAnswerValidator.cs](Quizzes/EssayAnswerValidator.cs)
- [FindXQuizGenerator.cs](Quizzes/FindXQuizGenerator.cs)
- [FractionQuizGenerator.cs](Quizzes/FractionQuizGenerator.cs)
- [GeometryQuizGenerator.cs](Quizzes/GeometryQuizGenerator.cs)
- [MotionQuizGenerator.cs](Quizzes/MotionQuizGenerator.cs)
- [PercentageQuizGenerator.cs](Quizzes/PercentageQuizGenerator.cs)
- [ProportionQuizGenerator.cs](Quizzes/ProportionQuizGenerator.cs)
- [QuizCurriculumLayer.cs](Quizzes/QuizCurriculumLayer.cs)
- [QuizProblemTypeCatalog.cs](Quizzes/QuizProblemTypeCatalog.cs)
- [WordProblemUnitEquivalence.cs](Quizzes/WordProblemUnitEquivalence.cs)

## Settings

Tùy chọn dành cho nhà phát triển.

- [DeveloperModeManager.cs](Settings/DeveloperModeManager.cs)

## Wallpaper

Quản lý, phân tích và điều phối phát hình nền động.

- [LiveWallpaperFrameAnalysis.cs](Wallpaper/LiveWallpaperFrameAnalysis.cs)
- [LiveWallpaperManager.cs](Wallpaper/LiveWallpaperManager.cs)
- [LiveWallpaperPlaybackCoordinator.cs](Wallpaper/LiveWallpaperPlaybackCoordinator.cs)
- [LiveWallpaperVideoInspector.cs](Wallpaper/LiveWallpaperVideoInspector.cs)
