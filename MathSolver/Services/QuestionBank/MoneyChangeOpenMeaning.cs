using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class MoneyChangeOpenMeaning
{
    private static readonly TimeSpan Timeout=TimeSpan.FromMilliseconds(100);
    private static bool Has(string text,string pattern)=>Regex.IsMatch(text,pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,Timeout);
    private static bool HasExtraFee(string text)
    {
        // Match the complete cost noun first, so its inner word is never
        // interpreted as a separate fee. Keep the raw text for every proof.
        bool fee=Regex.Matches(text,@"\b(?:(?<cost>chi\s+phí)|(?<fee>phí))\b",
            RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,Timeout).Any(m=>m.Groups["fee"].Success);
        return fee||Has(text,@"\bchi\s+phí\s+(?:phụ|phát sinh|bổ sung|dịch vụ|vận chuyển|giao hàng)\b");
    }
    private static bool Match(string text,string pattern)
    {
        text=Regex.Replace(text.Trim().ToLowerInvariant(),@"\s+"," ",RegexOptions.CultureInvariant,Timeout).TrimEnd(' ','.','?',':','!');
        return Regex.IsMatch(text,@"\A(?:"+pattern+@")\z",RegexOptions.CultureInvariant,Timeout);
    }
    internal static bool ProvesReference(MoneyChangePilot.Context context,AppLanguage language)
    {
        if(new[]{context.PriceFact,context.CountFact,context.PaidFact,context.Question,context.CostLead,context.ChangeLead}.Any(string.IsNullOrWhiteSpace))return false;
        var draft=new BasicQuestionDraft("","",context.Question,context.ChangeLead,"story")
        {
            Facts=[new("fact_0",context.PriceFact),new("fact_1",context.CountFact),new("fact_2",context.PaidFact)],
            SolutionLeads=[new("step_0",context.CostLead),new("step_1",context.ChangeLead)]
        };
        return Validate(language,draft,null).IsValid;
    }
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>Validate(c.Language,d,c);
    private static BasicDraftValidation Validate(AppLanguage language,BasicQuestionDraft d,BasicQuestionContract? c)
    {
        bool vi=language==AppLanguage.Vietnamese;
        string all=string.Join(" ",OpenQuestionProse.Fields(d).Select(f=>f.Text));
        string price=d.Facts![0].Text,count=d.Facts[1].Text,paid=d.Facts[2].Text;
        // Seller nouns and passive unit-price wording describe the shop's role.
        // A selling verb in the purchase-count fact reverses the buyer's role.
        if(Has(all,vi?@"\b(?:không|chưa|nếu|giả sử|miễn|trừ khi|giảm giá|chiết khấu|khuyến mại|khuyến mãi|thuế|vay|nợ|tặng|cho thêm|mua thêm|đổi hàng|trả hàng|rồi|sau đó|còn thiếu)\b"
                :@"\b(?:not|no|never|if|unless|suppose|assuming|discount|rebate|coupon|tax|fee|fees|loan|loans|borrow|borrows|debt|owe|owes|owing|gift|free|refund|another|additional|then|afterwards|also)\b")
            // "Chi phí" names the purchase cost; it is not an added fee.
            ||(vi&&HasExtraFee(all))
            ||Has(count,vi?@"\bbán\b":@"\b(?:sell|sells|sold|return|returns|returned|returning)\b")
            ||(!vi&&Has(all,@"\b(?:return|returns|returned|returning) (?:the |a |an |those |these )?(?:items?|products?|goods|purchase|order)\b"))
            ||Has(paid,vi?@"\b(?:trong ví|tiết kiệm|đang có|có sẵn|mang theo|dự định|phải trả|cần trả)\b"
                :@"\b(?:wallet|savings|saved|budget|has|have|owns|carrying|plans?|must pay|needs? to pay|costs?)\b")
            ||Has(d.Question,vi?@"\b(?:phải trả|cần trả|chi phí|giá mua|mua được|bao nhiêu món|số lượng)\b"
                :@"\b(?:spend|spends|spent|total cost|purchase cost|how many|unit price|price of)\b"))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"Keep a single purchase, direct unit price and count, cash actually tendered, and the returned change.");
        if(Has(all,@"[$€£%²³^]|\b(?:dollars?|cents?|euros?|pounds?|usd|vnd|thousand|nghìn|ngàn|triệu|kilograms?|kilôgam)\b"))
            return new(null,"ChangedUnits",ErrorDetails:"Use the supplied item and dong placeholders without conversions or extra units.");
        if(OpenQuestionProse.Fields(d).Any(f=>Has(f.Text.Trim(),@"[.!?:;].+")))
            return new(null,"InvalidText",ErrorDetails:"Use one short sentence per field without extra events.");
        bool priceProved=vi
            ?Match(price,@"mỗi \{v1\} (?:có giá|giá|được bán với giá|có giá bán) (?:là )?\{f0\} \{v3\}")
                ||Match(price,@"giá (?:của )?mỗi \{v1\} (?:là|bằng) \{f0\} \{v3\}")
            :Match(price,@"(?:each|every) \{v1\} (?:costs|is priced at|has a price of|is sold for) \{f0\} \{v3\}")
                ||Match(price,@"the (?:unit )?price of each \{v1\} is \{f0\} \{v3\}")
                ||Match(price,@"a \{v1\} costs \{f0\} \{v3\}");
        bool countProved=vi
            ?Match(count,@"\{v0\} (?:mua|chọn mua|đã mua) (?:tất cả )?\{f1\} \{v2\}")
            :Match(count,@"\{v0\} (?:buys|purchases|bought|purchased) \{f1\} \{v2\}");
        bool paidProved=vi
            ?Match(paid,@"\{v0\} (?:đưa|đưa cho|gửi cho) người bán \{f2\} \{v3\}(?: để thanh toán| để trả tiền)?")
                ||Match(paid,@"để (?:thanh toán|trả tiền), \{v0\} (?:đưa|đưa cho|gửi cho) người bán \{f2\} \{v3\}")
                ||Match(paid,@"khi thanh toán, \{v0\} (?:đưa|đưa cho|gửi cho) người bán \{f2\} \{v3\}")
                ||Match(paid,@"\{v0\} dùng \{f2\} \{v3\} (?:đưa|đưa cho) người bán để thanh toán")
            :Match(paid,@"\{v0\} (?:hands|gives|handed|gave) (?:the seller|the cashier) \{f2\} \{v3\}(?: to pay| for (?:the|this) purchase)?")
                ||Match(paid,@"to pay, \{v0\} (?:hands|gives) (?:the seller|the cashier) \{f2\} \{v3\}")
                ||Match(paid,@"to complete (?:the|this) (?:transaction|purchase), \{v0\} (?:hands|gives|handed|gave) (?:the seller|the cashier) \{f2\} \{v3\}")
                ||Match(paid,@"\{v0\} tenders \{f2\} \{v3\} (?:to pay|for (?:the|this) purchase)");
        bool questionProved=vi
            ?Match(d.Question,@"(?:hỏi )?\{v0\} (?:được trả lại|nhận lại|được nhận lại) bao nhiêu \{v3\} tiền thừa")
                ||Match(d.Question,@"(?:hỏi )?(?:số )?tiền thừa (?:mà )?\{v0\} (?:nhận lại|được trả lại|được nhận lại) (?:là|bằng) bao nhiêu \{v3\}")
                ||Match(d.Question,@"(?:hỏi )?\{v0\} nhận lại bao nhiêu \{v3\}")
            :Match(d.Question,@"how much change (?:does|will) \{v0\} (?:receive|get)(?: back)? in \{v3\}")
                ||Match(d.Question,@"what is (?:the )?change (?:returned|given back) to \{v0\} in \{v3\}")
                ||Match(d.Question,@"what amount of change (?:does|will) \{v0\} (?:receive|get)(?: back)? in \{v3\}")
                ||Match(d.Question,@"in \{v3\}, how much change (?:does|will) \{v0\} (?:receive|get)(?: back)?");
        bool costLead=vi?(Match(d.SolutionLeads![0].Text,@"(?:số |tổng số )?tiền (?:mua|phải trả cho) \{v2\}(?: là)?")
                ||Match(d.SolutionLeads[0].Text,@"(?:tổng )?chi phí (?:mua|cho|của) \{v2\}"))
            :Match(d.SolutionLeads![0].Text,@"(?:the )?(?:total )?cost of (?:the )?\{v2\}")
                ||Match(d.SolutionLeads[0].Text,@"(?:the )?combined price (?:for|of) all (?:the )?\{v2\}");
        bool changeLead=vi?Match(d.SolutionLeads![1].Text,@"(?:số )?tiền thừa (?:của |trả lại cho |mà )?\{v0\}(?: nhận lại| nhận được| được nhận(?: lại)?)?(?: là)?")
            :Match(d.SolutionLeads![1].Text,@"(?:the )?change (?:returned|given back) to \{v0\}")
                ||Match(d.SolutionLeads[1].Text,@"(?:the )?leftover amount (?:returned|given back) to \{v0\}")
                ||Match(d.SolutionLeads[1].Text,@"\{v0\}['’]s change");
        bool punctuation=d.Facts.All(f=>f.Text.TrimEnd().EndsWith('.'))&&d.Question.TrimEnd().EndsWith('?');
        if(priceProved&&countProved&&paidProved&&questionProved&&costLead&&changeLead&&punctuation)return new(d,null,c);
        return new(d,"SemanticReviewRequired",c,"Could not prove unit price, purchase count, tendered cash, returned change, monetary leads or sentence punctuation.");
    }
}
