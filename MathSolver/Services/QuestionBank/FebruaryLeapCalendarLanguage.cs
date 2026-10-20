using MathSolver.Models;
using static MathSolver.Services.QuestionBank.RectangleDirectOpenLanguage;

namespace MathSolver.Services.QuestionBank;

/// <summary>February-to-March inclusive day counts in one C#-selected leap year.</summary>
internal static class FebruaryLeapCalendarLanguage
{
    internal const string Relation="; f0=start day in February; f1=end day in March; f2=start month, fixed to 2; f3=the SAME leap year for both dates; f4=end month, fixed to 3; f5=29 days in February of that leap year. v0=actor; v1=item; v2=start action; v3=end action; v4=complete interval noun phrase. INCLUDE both start and end, count every intervening calendar day; target=counted calendar DAYS. C# chooses a valid leap year and dates and keeps the answer; do not fill numeric placeholders. No common year, skipped days, holidays, extra months or year crossing.";
    internal static bool HasContradiction(BasicQuestionDraft d,AppLanguage language)=>d.Facts!.Any(f=>Match(f.Text,language==AppLanguage.Vietnamese
        ?@"tháng \{f2\} có \{f5\} ngày trong (?:một )?năm (?:thường|không nhuận)"
        :@"month \{f2\} has \{f5\} days in a (?:common|non-leap) year"));
}
