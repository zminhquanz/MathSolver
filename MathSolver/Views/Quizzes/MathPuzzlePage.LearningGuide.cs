using MathSolver.Controls;
using MathSolver.Models;
using MathSolver.Services;

namespace MathSolver.Views;

public partial class MathPuzzlePage
{
    private readonly QuizLearningGuideView _learningGuide = new();
    private void UpdateLearningGuide()
    {
        var contract = _currentQuestion?.ElementaryProblem;
        bool direct = contract is { Type: ElementaryQuizType.ReadClock, RequiresSolution: false }
            or { Type: ElementaryQuizType.FractionPicture };
        var scene = _questionAnswered && direct && contract?.Visual is { } visual
            ? LearningGuideScene.TryCreate(visual.Kind, visual.Values) : null;
        _learningGuide.Configure(scene, TranslateQuiz);
    }
}
