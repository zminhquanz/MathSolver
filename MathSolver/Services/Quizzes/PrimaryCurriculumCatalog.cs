using MathSolver.Models;

namespace MathSolver.Services;

public enum CurriculumScope { Core, Extension }
public enum CurriculumCoverage { Skill, Partial, Planned }

/// <summary>Grade alignment describes a skill, not a guarantee about every star's generated numbers.</summary>
public sealed record PrimaryCurriculumSkill(string Id, QuizProblemKind Kind, string Subtype,
    int[] Grades, string RequirementId, CurriculumScope Scope, CurriculumCoverage Coverage,
    string Constraints, string SourcePages);

public static class PrimaryCurriculumCatalog
{
    private static readonly Lazy<IReadOnlyList<PrimaryCurriculumSkill>> Skills = new(() =>
    {
        var rows = QuizContentCatalog.LoadList<PrimaryCurriculumSkill>("PrimaryCurriculum");
        Validate(rows);
        return rows;
    });
    public static IReadOnlyList<PrimaryCurriculumSkill> All => Skills.Value;

    public static void Validate(IReadOnlyList<PrimaryCurriculumSkill> rows)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var routes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Id) || !ids.Add(row.Id) || !Enum.IsDefined(row.Kind)
                || string.IsNullOrWhiteSpace(row.Subtype) || !routes.Add(row.Kind + "." + row.Subtype)
                || row.Grades is not { Length: > 0 } || row.Grades.Any(g => g < 1 || g > 5)
                || row.Grades.Distinct().Count() != row.Grades.Length || string.IsNullOrWhiteSpace(row.RequirementId)
                || !Enum.IsDefined(row.Scope) || !Enum.IsDefined(row.Coverage)
                || string.IsNullOrWhiteSpace(row.Constraints) || string.IsNullOrWhiteSpace(row.SourcePages))
                throw new InvalidDataException("Invalid primary curriculum row: " + row.Id);
        }
        foreach (var kind in Enum.GetValues<QuizProblemKind>().Where(ElementaryQuizGenerator.Supports))
        foreach (var type in ElementaryQuizGenerator.Types(kind))
            if (!rows.Any(row => row.Kind == kind && row.Subtype == type.ToString() && row.Coverage != CurriculumCoverage.Planned))
                throw new InvalidDataException("Missing implemented curriculum skill: " + kind + "." + type);
    }

    public static PrimaryCurriculumSkill? ForQuestion(ArithmeticQuizQuestion question)
    {
        (QuizProblemKind Kind, string Subtype) route = question switch
        {
            { ElementaryProblem: { } e } => (e.Kind, e.Type.ToString()),
            { FractionProblem: { } f } => (QuizProblemKind.Fraction, f.Operation.ToString()),
            { FindXProblem: { } x } => (QuizProblemKind.FindX, x.Operation + (x.UnknownIsLeftOperand ? ".Left" : ".Right")),
            { GeometryProblem: { } g } => (QuizProblemKind.Geometry, g.ShapeId + "." + g.Measurement),
            { MotionProblem: { } m } => (QuizProblemKind.Motion, m.QuestionKind.ToString()),
            { ProportionProblem: { } p } => (QuizProblemKind.Proportion, p.Type.ToString()),
            { AverageProblem: { } a } => (QuizProblemKind.Average, a.Type.ToString()),
            { PercentageProblem: { } p } => (QuizProblemKind.Percentage, p.Type.ToString()),
            { ExpressionProblem: { } e } => (QuizProblemKind.Expression, e.Type.ToString()),
            _ => (QuizProblemKind.Arithmetic, question.Expression.Operation.ToString())
        };
        return All.SingleOrDefault(row => row.Kind == route.Kind && row.Subtype == route.Subtype);
    }

    public static string Describe(ArithmeticQuizQuestion question, AppLanguage language)
    {
        var skill = ForQuestion(question);
        if (skill is null) return "";
        string grades = string.Join(", ", skill.Grades);
        return QuizContentCatalog.Text(language, "Curriculum.Summary", ("grades", grades),
            ("scope", QuizContentCatalog.Text(language, "Curriculum.Scope." + skill.Scope)),
            ("requirement", QuizContentCatalog.Text(language, skill.RequirementId)));
    }
}
