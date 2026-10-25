using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class MoneyChangeOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.MultiStep
        &&c.Story?.NarrativeId.StartsWith(MoneyChangePilot.Prefix,StringComparison.Ordinal)==true;
    internal static ArithmeticQuizQuestion Generate(BasicQuestionContract c,ArithmeticQuizMode mode)
    {
        if(!MoneyChangePilot.ValidateContract(c))throw new ArgumentException("InvalidMoneyChangeContract");
        return new ElementaryQuizGenerator(new(c.Story!.Seed)).GenerateOpenMoneyChange(mode,c.Language,c.Tier,c.Story.NarrativeId);
    }
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        var q=Generate(c,ArithmeticQuizMode.Essay);
        var d=MoneyChangePilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        string[] roles=["unit-price","purchase-count","cash-tendered","buyer","single-purchased-item","purchased-item-count-unit","money-unit"];
        var quantities=d.Values.Select((v,i)=>new NarrativeQuantity(v.Key,roles[i],v.Value)).ToArray();
        NarrativeClause[] facts=[new("fact_0",d.Context.PriceFact),new("fact_1",d.Context.CountFact),new("fact_2",d.Context.PaidFact)];
        string[] leads=[d.Context.CostLead,d.Context.ChangeLead];
        var p=q.ElementaryProblem!;
        var steps=p.Reasoning!.Steps.Select((s,i)=>new NarrativeStep("step_"+i,leads[i],
            QuizMathExpressionFormatter.Format(s.Expression)+" = "+s.DisplayValue+" "+s.Unit)).ToArray();
        string schema=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",MoneyChangePilot.Relation,d.Profile,c.Language,c.Tier,d.Context,string.Join("/",roles)))));
        return new(q,d.Profile,d.Context.Buyer,d.Context.MoneyUnit,p.AnswerText,schema,quantities,facts,d.Context.Question,steps,
            facts.ToDictionary(f=>f.Role,f=>new[]{f.Text}),[d.Context.Question],steps.ToDictionary(s=>s.Id,s=>new[]{s.Lead}));
    }
    internal static string PromptRelation(BasicQuestionContract c)
    {
        var d=MoneyChangePilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        return "relation="+MoneyChangePilot.Relation+"; {f0}=price of EACH {v1}, in {v3}; {f1}=number of {v2} bought by {v0}; "
            +"{f2}=cash actually handed to the seller by {v0} to pay for this purchase, in {v3}, NOT savings or a wallet balance. "
            +"{v1}=singular item, {v2}=counted purchase items, {v3}=Vietnamese dong. Ask ONLY the change returned to {v0}, in {v3}. "
            +"step_0 names total purchase cost; step_1 names change returned. C# computes cost=unit-price times count and change=cash minus cost. "
            +"One product, no discounts, fees, debt, extra purchase, other cash or conditions. Do not calculate or disclose answers. "
            +"Bindings for understanding only: {v0}="+d.Context.Buyer+"; {v1}="+d.Context.Item+"; {v2}="+d.Context.Items+"; {v3}="+d.Context.MoneyUnit+". Keep placeholders in output.";
    }
    internal static string WritingInstructions(AppLanguage language)=>language==AppLanguage.Vietnamese?"""
        Viết một bài toán mua một loại đồ dùng và nhận tiền thừa, ngắn gọn, tự nhiên cho học sinh tiểu học.
        Ba dữ kiện riêng: mỗi {v1} giá {f0} {v3}; {v0} mua {f1} {v2}; {v0} thực sự đưa người bán {f2} {v3} để thanh toán.
        Tiền đưa là tiền thanh toán cho lần mua này, không phải tiền đang có trong ví, tiền tiết kiệm hay tiền vay.
        Hỏi số tiền thừa {v0} được trả lại, tính bằng {v3}. Câu dẫn step_0 gọi số tiền mua {v2}; step_1 gọi tiền thừa của {v0}.
        Được thay cấu trúc câu, cách mở đầu và cách hỏi. Ví dụ chỉ để tham khảo; dùng ngôn ngữ phổ thông, một câu ngắn trong mỗi trường.
        Giữ nguyên mỗi biến đúng một lần trong trường của nó, giữ role và step. Không điền tên, số, đơn vị hay kết quả thật.
        Không thêm giảm giá, phí, nợ, loại hàng khác, lần mua thêm, điều kiện hoặc sự kiện khác. Không đổi mua thành bán hay tiền đưa thành số tiền phải trả.
        {v1} là tên một món, {v2} là đơn vị đếm món mua, {v3} là đơn vị tiền; không tự thêm đơn vị hay ký hiệu.
        Dữ kiện kết thúc bằng dấu chấm, câu hỏi kết thúc bằng dấu hỏi. Chỉ trả JSON theo cấu trúc mẫu; không tính toán hay giải thích kỹ thuật.
        """:"""
        Write a short natural primary-school problem about buying one type of item and receiving change.
        Give three separate facts: each {v1} costs {f0} {v3}; {v0} buys {f1} {v2}; {v0} actually hands the seller {f2} {v3} to pay.
        The cash is tendered for this purchase, not savings, a wallet balance, an amount owed or a loan.
        Ask only how much change {v0} receives in {v3}. step_0 names the total cost of {v2}; step_1 names the change returned to {v0}.
        Vary sentence openings, structure and the question naturally. The example is a reference, not a fixed sentence list. Use one short sentence per field.
        Keep each placeholder once in its original field and preserve role/step. Fill no actual names, numbers, units or answers.
        Add no discount, fee, debt, second product, extra purchase, condition or other event. Do not reverse buying/selling or tendered cash/amount owed.
        {v1} is a singular item, {v2} is the counted purchase unit and {v3} is the money unit. Add no currency symbol or extra unit.
        Facts end with periods and the question with a question mark. Return only JSON with the reference schema, without calculations or technical notes.
        """;
}
