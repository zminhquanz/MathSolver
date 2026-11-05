namespace MathSolver.Services.QuestionBank;

public sealed partial class GgufQuestionRuntime
{
    // Opt-in fixed seed/stage observation for isolated device checks only.
    internal uint? InferenceSeed {get;set;}
    internal Action<string>? PreparationStageObserver {get;set;}
    public AiInferencePreparation? LastPreparation {get;private set;}
}
