namespace MathSolver.Services;

public sealed partial class EssayAnswerValidator
{
    private static bool MotionOpenOperands(MathSolver.Models.MotionQuizContract p,string expression)
    {
        if(!MotionOpenPilot.ValidContract(p))return false;
        var f=p.Facts;
        string[] forms=p.QuestionKind switch {
            MathSolver.Models.MotionQuestionKind.BasicDistance=>[$"{f[0]} × {f[1]}",$"{f[1]} × {f[0]}"],
            MathSolver.Models.MotionQuestionKind.BasicSpeed or MathSolver.Models.MotionQuestionKind.BasicTime=>[$"{f[0]} ÷ {f[1]}"],
            _=>[$"{f[0]} × ({f[1]} - {f[2]})",$"({f[1]} - {f[2]}) × {f[0]}",$"{f[0]} × {f[1]} - {f[0]} × {f[2]}"]};
        return forms.Any(form=>NormalizeProportionEquation(form)==NormalizeProportionEquation(expression));
    }
}
