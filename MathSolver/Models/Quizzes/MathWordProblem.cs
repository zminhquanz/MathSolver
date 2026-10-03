namespace MathSolver.Models;

/// <summary>Presentation text for a word problem whose facts and answer are owned by C#.</summary>
public sealed record MathWordProblem(
    string ProblemText,
    string SolutionLead,
    string AnswerUnit,
    string SubjectName);

