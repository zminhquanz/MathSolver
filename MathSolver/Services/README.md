# Services

[Bản đồ toàn project](../FOLDER_STRUCTURE.md)

## AI

Chạy LLM cục bộ, sinh đề bằng LLM, quản lý và tải model.

- [Gemma4ModelDownloadService.cs](AI/Gemma4ModelDownloadService.cs)
- [LocalLlmQuizGenerator.cs](AI/LocalLlmQuizGenerator.cs)
- [LocalLlmRuntime.Windows.cs](AI/LocalLlmRuntime.Windows.cs)
- [ModelFileLocationService.cs](AI/ModelFileLocationService.cs)
- [QuizLlmModelStore.cs](AI/QuizLlmModelStore.cs)

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
- [GeometryCalculationEngine.cs](Calculations/GeometryCalculationEngine.cs)
- [LinearEquationEngine.cs](Calculations/LinearEquationEngine.cs)
- [LongDivisionCalculator.cs](Calculations/LongDivisionCalculator.cs)
- [MeasurementEngine.cs](Calculations/MeasurementEngine.cs)
- [PowerRootEngine.cs](Calculations/PowerRootEngine.cs)
- [QuadraticEquationEngine.cs](Calculations/QuadraticEquationEngine.cs)

## Formatting

Định dạng nhập/xuất số và sao chép kết quả.

- [IntegerInputFormatter.cs](Formatting/IntegerInputFormatter.cs)
- [RationalDecimalFormatter.cs](Formatting/RationalDecimalFormatter.cs)
- [ResultClipboardService.cs](Formatting/ResultClipboardService.cs)
- [ResultNumberDisplayMode.cs](Formatting/ResultNumberDisplayMode.cs)

## Interaction

Hộp thoại, cuộn tự động và hỗ trợ tương tác control.

- [AndroidPickerVisualHelper.cs](Interaction/AndroidPickerVisualHelper.cs)
- [AutoScrollOnContentGrowthBehavior.cs](Interaction/AutoScrollOnContentGrowthBehavior.cs)
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

