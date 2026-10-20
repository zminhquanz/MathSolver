using MathSolver.Models;
using static MathSolver.Services.QuestionBank.RectangleDirectOpenLanguage;

namespace MathSolver.Services.QuestionBank;

/// <summary>Explicit calendar scope for February in a common year.</summary>
internal static class FebruaryCommonCalendarLanguage
{
    internal const string Relation="; f0=start day in February; f1=end day in March; f2=start month, fixed to 2; f3=the SAME common (non-leap) year for both dates; f4=end month, fixed to 3; f5=28 days in February of that common year. v0=actor; v1=item; v2=start action; v3=end action; v4=complete interval noun phrase. INCLUDE both start and end, count every intervening calendar day; target=counted calendar DAYS. C# chooses a valid common year and dates and keeps the answer; do not fill numeric placeholders. No leap year, skipped days, holidays, extra months or year crossing.";
    internal static bool HasContradiction(BasicQuestionDraft d,AppLanguage language)=>d.Facts!.Any(f=>Match(f.Text,language==AppLanguage.Vietnamese
        ?@"tháng \{f2\} có \{f5\} ngày trong (?:một )?năm nhuận"
        :@"month \{f2\} has \{f5\} days in a leap year"));
}
