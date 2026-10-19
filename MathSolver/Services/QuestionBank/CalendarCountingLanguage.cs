using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>Role-bound calendar phrases, composed without deleting negation or qualifiers.</summary>
internal static class CalendarCountingLanguage
{
    private const string StartEn=@"(?:the )?(?:start|initial|first) (?:date|day)";
    // The second supplied DATE is the end; the second DAY need not be.
    private const string EndEn=@"(?:the )?(?:(?:end|final|last) (?:date|day)|second date)";
    private const string StartVi=@"ngày (?:bắt đầu|đầu)",EndVi=@"ngày (?:kết thúc|cuối)";
    private const string EveryEn=@"(?:all (?:calendar )?days|every (?:calendar )?day)";
    private const string AfterEn=@"the day (?:immediately )?(?:after|following) "+StartEn;

    internal static string ExcludeStart(AppLanguage language)=>language==AppLanguage.Vietnamese
        ?@"không tính "+StartVi+@", (?:có )?tính "+EndVi
            +@"|(?:tính số|đếm tất cả các) ngày từ ngày (?:liền sau ngày (?:bắt đầu|đầu)|sau khi bắt đầu) đến hết "+EndVi
        :@"(?:the counting (?:convention|rule)|the (?:day )?count) excludes "+StartEn+@" and includes "+EndEn
            +@"|the count (?:begins|starts) (?:on |from )?"+AfterEn+@" and (?:ends|finishes|concludes) (?:on|with) "+EndEn
            +@"|count "+EveryEn+@" from "+AfterEn+@" (?:through|up to and including) "+EndEn+@"(?:, inclusive)?";

    internal static string IncludeBoth(AppLanguage language)=>language==AppLanguage.Vietnamese
        ?@"tính mọi ngày, (?:bao )?gồm cả "+StartVi+@" và "+EndVi
            +@"|tính cả "+StartVi+@" và "+EndVi
            +@"|"+StartVi+@" và "+EndVi+@" đều được tính"
            +@"|đếm (?:mọi|tất cả các) ngày từ "+StartVi+@" đến hết "+EndVi
        :@"include both "+StartEn+@" and "+EndEn
            +@"|count "+EveryEn+@", including both "+StartEn+@" and "+EndEn
            +@"|both "+StartEn+@" and "+EndEn+@" are included"
            +@"|every calendar day is counted, starting (?:with|from) "+StartEn+@" and ending (?:with|on) "+EndEn
            +@"|all (?:calendar )?days within the (?:span|period|interval) are (?:included|counted), starting from "+StartEn+@" and ending with "+EndEn;

    internal static string WrongScope(AppLanguage language)=>language==AppLanguage.Vietnamese
        ?@"không tính "+EndVi+@", (?:có )?tính "+StartVi
        :@"(?:the counting (?:convention|rule)|the (?:day )?count) excludes "+EndEn+@" and includes "+StartEn
            +@"|exclude both "+StartEn+@" and "+EndEn
            +@"|the count (?:begins|starts) (?:on |from )?the day (?:after|following) "+EndEn
                +@" and (?:ends|finishes|concludes) (?:on|with) "+StartEn;
}
