using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class PercentageRatioOpenMeaning
{
    private static string Normal(string x)=>Regex.Replace(x.Trim().ToLowerInvariant(),@"\s+"," ",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)).TrimEnd(' ','.','?',':');
    private static bool Match(string x,string p)=>Regex.IsMatch(Normal(x),@"\A(?:"+p+@")\z",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        string[] patterns=vi?[
            @"\{v0\} (?:có|sở hữu|đang có|chứa) (?:tổng cộng |tất cả )?\{f0\} \{v1\}|trong kho của \{v0\} có \{f0\} \{v1\}",
            @"trong (?:số|toàn bộ) \{v2\} đó có \{f1\} \{v3\}|trong tổng số \{v2\} đó, \{f1\} là \{v3\}",
            @"(?:hỏi )?số \{v4\} chiếm bao nhiêu phần trăm tổng số \{v2\} của \{v0\}|trong tổng số \{v2\} của \{v0\}, số \{v4\} chiếm tỉ lệ bao nhiêu phần trăm",
            @"tỉ (?:số|lệ) phần trăm \{v4\} so với tổng số \{v2\}|tỉ lệ phần trăm \{v4\} trong tổng số \{v2\}"]:[
            @"\{v0\} (?:has|owns|holds) (?:a total of )?\{f0\} \{v1\}|\{f0\} \{v1\} belong to \{v0\}|\{v0\} owns a collection of \{f0\} \{v1\}",
            @"(?:of|among) those \{v2\}, \{f1\} are \{v3\}|among the \{v2\} owned, \{f1\} are \{v3\}",
            @"what percentage of \{v0\}['’]s \{v2\} are \{v4\}|what percentage of the \{v2\} owned by \{v0\} are \{v4\}|of the \{v2\} owned by \{v0\}, what percentage are \{v4\}",
            @"percentage of \{v4\} (?:among|within) all \{v2\}|percentage of \{v4\} relative to all \{v2\}"];
        var fields=new[]{d.Facts![0].Text,d.Facts[1].Text,d.Question,d.SolutionLeads![0].Text};
        bool Proves(int i,string t)=>Match(t,patterns[i]);
        BasicDraftValidation Wrong(string why)=>new(null,"ChangedRelationOrTarget",ErrorDetails:why);
        for(int i=0;i<fields.Length;i++)
        {
            string text=Normal(fields[i]);string deny=vi?"không phải ":"it is not true that ";
            if(text.StartsWith(deny,StringComparison.Ordinal)&&Proves(i,text[deny.Length..]))return Wrong("A required ratio premise or target was denied.");
        }
        if(Match(d.Facts[1].Text,vi?@"ngoài số \{v2\} đó còn có \{f1\} \{v3\}":@"in addition to those \{v2\}, there are \{f1\} \{v3\}"))return Wrong("The subset became a separate collection outside the whole.");
        if(Match(d.Question,vi?@"số \{v2\} của \{v0\} chiếm bao nhiêu phần trăm số \{v4\}":@"what percentage of the \{v4\} are \{v0\}['’]s \{v2\}"))return Wrong("The percentage denominator was reversed.");
        bool complete=d.Facts.All(f=>f.Text.TrimEnd().EndsWith('.'))&&d.Question.TrimEnd().EndsWith('?');
        return complete&&fields.Select((f,i)=>Proves(i,f)).All(x=>x)?new(d,null,c):new(d,"SemanticReviewRequired",c,"Could not prove static subset membership, the original percentage base and the percentage target.");
    }
}
