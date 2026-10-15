using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class ElementaryQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateDecimalPilot(ArithmeticQuizMode mode,ElementaryQuizType type,
        AppLanguage language,CurriculumTier tier,string profile)
    {
        var data=DecimalStoryPilot.Create(profile,type,language,tier,_random);
        var builder=new DifficultyBuilder(QuizProblemKind.Decimal,type,language,tier);
        builder.Given(data.Relation.FirstRole,data.First,data.FirstUnit);
        builder.Given(data.Relation.SecondRole,data.Second,data.SecondUnit);
        builder.Answer(data.Render(data.Relation.Lead).TrimEnd(':',' '),data.Expression,data.AnswerUnit);
        var contract=builder.Build(profile,data.Problem) with {StoryContextId=profile};
        return CompleteQuestion(mode,contract,[],null);
    }
}
