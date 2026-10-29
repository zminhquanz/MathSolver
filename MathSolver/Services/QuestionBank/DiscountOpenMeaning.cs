using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class DiscountOpenMeaning
{
    private static readonly TimeSpan Timeout=TimeSpan.FromMilliseconds(100);
    private static bool Has(string text,string pattern)=>Regex.IsMatch(text,pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,Timeout);
    private static bool Match(string text,string pattern)
    {
        text=Regex.Replace(text.Trim().ToLowerInvariant(),@"\s+"," ",RegexOptions.CultureInvariant,Timeout).TrimEnd(' ','.','?',':','!');
        return Regex.IsMatch(text,@"\A(?:"+pattern+@")\z",RegexOptions.CultureInvariant,Timeout);
    }
    internal static bool ProvesReference(DiscountOpenPilot.Context context,AppLanguage language)
    {
        if(new[]{context.PriceFact,context.DiscountFact,context.Question,context.DiscountLead,context.PayableLead}.Any(string.IsNullOrWhiteSpace))return false;
        var draft=new BasicQuestionDraft("","",context.Question,context.PayableLead,"story")
        {
            Facts=[new("fact_0",context.PriceFact),new("fact_1",context.DiscountFact)],
            SolutionLeads=[new("step_0",context.DiscountLead),new("step_1",context.PayableLead)]
        };
        return Validate(language,draft,null).IsValid;
    }
    internal static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>Validate(c.Language,d,c);
    private static BasicDraftValidation Validate(AppLanguage language,BasicQuestionDraft d,BasicQuestionContract? c)
    {
        if(d.Facts is not {Count:2}||d.SolutionLeads is not {Count:2})return new(null,"InvalidFields");
        bool vi=language==AppLanguage.Vietnamese;
        string all=string.Join(" ",OpenQuestionProse.Fields(d).Select(f=>f.Text));
        string price=d.Facts[0].Text,discount=d.Facts[1].Text;
        if(Has(all,vi?@"\b(?:nếu|giả sử|miễn|trừ khi|chỉ khi|có thể)\b"
                :@"\b(?:if|unless|suppose|assuming|might|may)\b"))
            return new(d,"SemanticReviewRequired",c,"Qualified discount assertions do not establish an unconditional original-price reduction.");
        if(Has(all,vi?@"\b(?:không|chưa|tăng|phí|thuế|vay|nợ|tặng|mua thêm|tiền thừa|đưa tiền|lần nữa|hai lần|tiếp tục|rồi|sau đó)\b"
                :@"\b(?:not|no|never|without|except|coupon|increase|increases|increased|rise|rises|rose|fee|fees|tax|taxes|loan|borrow|debt|donate|donates|donated|gift|free|change|cash|tender|tendered|another|additional|again|twice|then|afterwards|also)\b")
            ||Has(discount,vi?@"\bgiảm (?:xuống )?còn\b":@"\b(?:reduced|discounted|lowered) to\b")
            ||Has(discount,vi?@"\b(?:giá còn lại|giá mới|giá đã giảm|giá sau giảm|giá phải trả|số tiền phải trả)\b"
                :@"\b(?:remaining|new|reduced|discounted|payable|sale|final) price\b")
            ||Has(price,vi?@"\b(?:giá sau giảm|giá đã giảm|giá phải trả|số tiền được giảm)\b"
                :@"\b(?:(?:sale|payable|reduced|discounted|final) (?:price|cost)|final (?:payment|amount due))\b")
            ||Has(d.Question,vi?@"\b(?:giá gốc|giá ban đầu|tiền giảm giá|tiền được giảm|giảm được|tiết kiệm|bao nhiêu món|bao nhiêu phần trăm)\b"
                :@"\b(?:(?:original|initial|starting) (?:price|cost)|before the discount|discount amount|(?:the )?value of the price reduction|amount (?:of|saved)|how much (?:is|was) (?:the )?discount|how (?:many|much).*saved|how many|what percentage)\b"))
            return new(null,"ChangedRelationOrTarget",ErrorDetails:"Keep one unconditional reduction of the original product price; ask only the final payable price.");
        if(Has(all,@"[$€£%²³^]|\b(?:dollars?|cents?|euros?|pounds?|usd|vnd|thousand|nghìn|ngàn|triệu|kilograms?|kilôgam)\b"))
            return new(null,"ChangedUnits",ErrorDetails:"Use the supplied money and percentage slots without conversions or extra units.");
        if(OpenQuestionProse.Fields(d).Any(f=>Has(f.Text.Trim(),@"[.!?:;].+")))
            return new(null,"InvalidText",ErrorDetails:"Use one short sentence per field without extra transactions.");
        bool priceProved=vi
            ?Match(price,@"giá (?:gốc|ban đầu) của \{v0\} (?:là|bằng) \{f0\} \{v1\}")
                ||Match(price,@"\{v0\} có giá (?:gốc|ban đầu) (?:là )?\{f0\} \{v1\}")
                ||Match(price,@"trước khi giảm giá, \{v0\} có giá \{f0\} \{v1\}")
            :Match(price,@"the (?:original|initial|starting) (?:price|cost) (?:of|for) \{v0\} is (?:recorded as )?\{f0\} \{v1\}")
                ||Match(price,@"\{v0\} (?:has an original price of|originally costs|originally cost|is originally priced at) \{f0\} \{v1\}")
                ||Match(price,@"before the discount, \{v0\} (?:costs|cost|is priced at) \{f0\} \{v1\}");
        bool discountProved=vi
            ?Match(discount,@"giá của \{v0\} (?:được )?giảm \{f1\} (?:so với|của) giá (?:gốc|ban đầu)")
                ||Match(discount,@"mức giảm giá (?:cho|của) \{v0\} (?:là|bằng) \{f1\} giá (?:gốc|ban đầu)")
                ||Match(discount,@"\{v0\} được giảm giá \{f1\} (?:so với|của) giá (?:gốc|ban đầu)")
            :Match(discount,@"the price of \{v0\} is reduced by \{f1\} of (?:its|the) (?:original|initial) price")
                ||Match(discount,@"the (?:original|initial) price of \{v0\} is (?:discounted|reduced) by \{f1\}")
                ||Match(discount,@"\{v0\} (?:has|receives|gets) a discount of \{f1\} (?:on|off) (?:its|the) (?:original|initial) price")
                ||Match(discount,@"a discount of \{f1\} (?:of|on) (?:the|its) (?:original|initial) price (?:is applied to|applies to) \{v0\}")
                ||Match(discount,@"a reduction of \{f1\} applies to the (?:original|initial|starting) price of \{v0\}");
        bool questionProved=vi
            ?Match(d.Question,@"(?:hỏi )?(?:số tiền|giá) phải trả (?:cho|để mua) \{v0\} sau (?:khi )?giảm giá (?:là|bằng) bao nhiêu \{v1\}")
                ||Match(d.Question,@"sau (?:khi )?giảm giá, (?:cần|phải) trả bao nhiêu \{v1\} (?:cho|để mua) \{v0\}")
                ||Match(d.Question,@"(?:hỏi )?\{v0\} có giá bao nhiêu \{v1\} sau (?:khi )?giảm giá")
            :Match(d.Question,@"(?:after the discount, )?how much (?:must|needs to) be paid for \{v0\}(?: after the discount)? in \{v1\}")
                ||Match(d.Question,@"what is the (?:price payable|payable price|final price|discounted price) (?:for|of) \{v0\} after the discount in \{v1\}")
                ||Match(d.Question,@"how much money must be given for \{v0\} after applying the reduction in \{v1\}")
                ||Match(d.Question,@"what is the final (?:payment due|amount due) for \{v0\} (?:after the discount|following the price reduction) in \{v1\}")
                ||Match(d.Question,@"how much does \{v0\} cost after the discount in \{v1\}")
                ||Match(d.Question,@"in \{v1\}, how much must be paid for \{v0\} after the discount");
        bool discountLead=vi?Match(d.SolutionLeads[0].Text,@"(?:số )?tiền (?:giảm giá|được giảm) (?:của|cho) \{v0\}(?: là)?")
            :Match(d.SolutionLeads[0].Text,@"(?:the )?(?:discount|discount amount|amount discounted|value of the price reduction) (?:on|for|of) \{v0\}");
        bool payableLead=vi?Match(d.SolutionLeads[1].Text,@"(?:số tiền|giá) phải trả (?:cho|để mua) \{v0\}(?: là)?")
            :Match(d.SolutionLeads[1].Text,@"(?:the )?(?:payable price|price payable|final price|amount payable|final payment due) (?:for|of) \{v0\}");
        bool punctuation=d.Facts.All(f=>f.Text.TrimEnd().EndsWith('.'))&&d.Question.TrimEnd().EndsWith('?');
        if(priceProved&&discountProved&&questionProved&&discountLead&&payableLead&&punctuation)return new(d,null,c);
        return new(d,"SemanticReviewRequired",c,"Could not prove the original product price, its single discount percentage, payable-price target, money headings or sentence punctuation.");
    }
}
