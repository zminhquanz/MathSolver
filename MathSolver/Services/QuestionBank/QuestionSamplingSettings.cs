namespace MathSolver.Services.QuestionBank;

public sealed record QuestionSamplingSettings(float Temperature = 0.5f, float TopP = 0.9f, int TopK = 40,
    float MinP = 0.05f, float RepeatPenalty = 1.05f)
{
    public void Validate()
    {
        if (!float.IsFinite(Temperature) || Temperature is < 0.3f or > 0.6f || !float.IsFinite(TopP) || TopP is <= 0 or > 1
            || TopK is < 1 or > 100 || !float.IsFinite(MinP) || MinP is < 0 or > 1
            || !float.IsFinite(RepeatPenalty) || RepeatPenalty is < 1 or > 1.2f)
            throw new ArgumentOutOfRangeException(nameof(Temperature), "Invalid question sampling settings.");
    }
}
