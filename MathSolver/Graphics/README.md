# Graphics

[Bản đồ toàn project](../FOLDER_STRUCTURE.md)

## Arithmetic

Minh họa phép tính cơ bản, chia dài và trung bình cộng.

- [AverageDistributionDrawable.cs](Arithmetic/AverageDistributionDrawable.cs)
- [BasicArithmeticDrawable.cs](Arithmetic/BasicArithmeticDrawable.cs)
- [LongDivisionDrawable.cs](Arithmetic/LongDivisionDrawable.cs)

## Backgrounds

Nền động và hợp đồng drawable cập nhật theo thời gian.

- [ITimeDrivenDrawable.cs](Backgrounds/ITimeDrivenDrawable.cs)
- [MathAnimatedBackgroundDrawable.cs](Backgrounds/MathAnimatedBackgroundDrawable.cs)
- [MathNeuralBackgroundDrawable.cs](Backgrounds/MathNeuralBackgroundDrawable.cs)

## Benchmarks

Biểu đồ kết quả benchmark CPU và LLM.

- [BenchmarkVerticalChartDrawable.cs](Benchmarks/BenchmarkVerticalChartDrawable.cs)
- [LlmAccuracyHorizontalChartDrawable.cs](Benchmarks/LlmAccuracyHorizontalChartDrawable.cs)

## Equations

Vẽ đồ thị tuyến tính, parabol và đánh giá điểm vẽ bằng SIMD.

- [LinearEquationGraphDrawable.cs](Equations/LinearEquationGraphDrawable.cs)
- [ParabolaGraphDrawable.cs](Equations/ParabolaGraphDrawable.cs)

## Formulas

Minh họa chuyển động, tỉ lệ và hình học.

- [CompoundProportionDrawable.cs](Formulas/CompoundProportionDrawable.cs)
- [GeometryShapeDrawable.cs](Formulas/GeometryShapeDrawable.cs)
- [MotionAverageSpeedDrawable.cs](Formulas/MotionAverageSpeedDrawable.cs)
- [ProportionComparisonDrawable.cs](Formulas/ProportionComparisonDrawable.cs)

## Quizzes

Bảng, biểu đồ cột/tròn, đồng hồ và hình học trực quan dùng dữ kiện của câu hỏi.
Hai nguồn Thuật toán và AI/LLM dùng cùng drawable trên Windows và Android.

- [ElementaryQuizDrawable.cs](Quizzes/ElementaryQuizDrawable.cs)
- [QuizDiagramDrawable.cs](Quizzes/QuizDiagramDrawable.cs): hình học có số đo,
  sơ đồ đoạn thẳng tìm hai số, chuyển động, phân số và trung bình cộng gián tiếp.
  `QuizDiagramBuilder` chọn dữ kiện từ hợp đồng C#, không đọc câu văn AI;
  kết quả và diễn giải chỉ được đưa vào sơ đồ sau khi chấm.

