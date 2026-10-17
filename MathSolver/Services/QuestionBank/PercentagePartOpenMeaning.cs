using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class PercentagePartOpenMeaning
{
    private static readonly TimeSpan Timeout=TimeSpan.FromMilliseconds(100);
    private static bool Has(string text,string pattern)=>Regex.IsMatch(text,pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,Timeout);
    private static bool Match(string text,string pattern)
    {
        text=Regex.Replace(text.Trim().ToLowerInvariant(),@"\s+"," ",RegexOptions.CultureInvariant,Timeout).TrimEnd(' ','.','?',':','!');
        return Regex.IsMatch(text,@"\A(?:"+pattern+@")\z",RegexOptions.CultureInvariant,Timeout);
    }
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        string all=d.ProblemText+" "+d.SolutionLeads![0].Text;
        string change=vi?@"\b(?:tăng|giảm|còn lại|còn bao nhiêu|nhận thêm|mua thêm|cho đi|đã bán|đã dùng|gấp|so với|không|chưa|của người khác)\b"
            :@"\b(?:increases?|increased|decreases?|decreased|remaining|remain|left|added|more|fewer|buys?|sells?|sold|used|discount|interest|relative to|not|no|except|someone else's)\b";
        bool reversed=vi?Match(d.Facts![1].Text,@"(?:số )?\{v2\} chiếm \{f1\} (?:tổng )?số \{v3\}(?: đó)?")
            :Match(d.Facts![1].Text,@"(?:the )?\{v2\} (?:make up|account for|are) \{f1\} of (?:the |those )?\{v3\}")
                ||Match(d.Facts[1].Text,@"\{f1\} of (?:those|the) \{v3\} are \{v2\}");
        if(Has(all,change)||reversed||Has(d.Question,vi?@"\b(?:tỉ lệ|tỷ lệ|bao nhiêu phần trăm|tổng số ban đầu|toàn bộ ban đầu)\b"
            :@"\b(?:what percentage|what percent|original total|original whole)\b"))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"Percentage base, static subset or requested subset count changed.");
        bool whole=vi?Match(d.Facts![0].Text,@"(?:ban đầu, )?\{v0\} (?:có|có tất cả|có tổng cộng|sở hữu(?: tổng cộng)?) \{f0\} \{v1\}")
            ||Match(d.Facts[0].Text,@"trong kho của \{v0\} có \{f0\} \{v1\}")
            :Match(d.Facts![0].Text,@"\{v0\} (?:has|owns|holds|possesses|has a total of) \{f0\} \{v1\}")
                ||Match(d.Facts[0].Text,@"\{f0\} \{v1\} belong to \{v0\}");
        bool share=vi?Match(d.Facts![1].Text,@"trong số \{v2\} (?:đó|này), (?:có )?\{f1\} là (?:loại )?\{v3\}")
                ||Match(d.Facts[1].Text,@"(?:số )?\{v3\} chiếm \{f1\} (?:tổng )?số \{v2\} (?:đó|này)")
            :Match(d.Facts![1].Text,@"\{f1\} of (?:those|these|the) \{v2\} are \{v3\}")
                ||Match(d.Facts[1].Text,@"of (?:those|these|the) \{v2\}, \{f1\} are \{v3\}")
                ||Match(d.Facts[1].Text,@"(?:the )?\{v3\} (?:make up|account for) \{f1\} of (?:those|these|the) \{v2\}");
        bool question=vi?Match(d.Question,@"(?:hỏi )?\{v0\} có (?:tất cả )?bao nhiêu \{v4\}")
                ||Match(d.Question,@"(?:hỏi )?(?:số|số lượng) \{v4\} của \{v0\} là bao nhiêu")
            :Match(d.Question,@"how many \{v4\} does \{v0\} have")
                ||Match(d.Question,@"how many \{v4\} belong to \{v0\}")
                ||Match(d.Question,@"what is the (?:number|count) of \{v4\} (?:that )?\{v0\} (?:has|holds|owns)");
        bool lead=Match(d.SolutionLeads![0].Text,vi?@"(?:số|số lượng) \{v4\}":@"(?:the )?(?:number|count) of \{v4\}");
        return whole&&share&&question&&lead?new(d,null,c):new(d,"SemanticReviewRequired",c,
            "Whole ownership at 100%, percentage of that same whole, subset identity or requested count could not be proved.");
    }
}
