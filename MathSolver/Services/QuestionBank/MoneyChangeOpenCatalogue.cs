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
        return "relation="+MoneyChangePilot.Relation+"; {f0}=price of EACH {v1} in {v3}; {f1}=count of {v2} bought by {v0}; "
            +"{f2}=cash {v0} actually gives the seller for this purchase in {v3}. Target=change returned to {v0} in {v3}. "
            +"{v1}/{v2}=complete item slots, singular/count form; do not repeat their item or classifier words. "
            +"step_0=total purchase cost; step_1=returned change. One purchase, no other transaction or conditions. "
            +"Bindings for understanding only: {v0}="+d.Context.Buyer+"; {v1}="+d.Context.Item+"; {v2}="+d.Context.Items+"; {v3}="+d.Context.MoneyUnit+". Keep placeholders in output.";
    }
    internal static string WritingInstructions(AppLanguage language)=>language==AppLanguage.Vietnamese?"""
        Viết một bài toán mua một loại đồ dùng và nhận tiền thừa, ngắn gọn, tự nhiên cho học sinh tiểu học.
        Ba dữ kiện riêng: mỗi {v1} giá {f0} {v3}; {v0} mua {f1} {v2}; {v0} thực sự đưa người bán {f2} {v3} để thanh toán.
        Tiền đưa là tiền thực trả cho lần mua này, không phải tiền trong ví, tiết kiệm, tiền nợ hay vay.
        Hỏi tiền thừa {v0} nhận lại bằng {v3}. Câu dẫn là cụm danh từ: step_0 gọi số tiền mua {v2}; step_1 gọi tiền thừa của {v0}. Không mở đầu bằng Tính, Tìm hay hướng dẫn phép tính.
        Được thay cấu trúc câu, cách mở đầu và cách hỏi tự nhiên; ví dụ chỉ tham khảo. Mỗi trường một câu ngắn.
        Giữ mỗi biến đúng một lần trong trường gốc, giữ role và step. Không điền tên, số, đơn vị hoặc đáp án; không thêm nhân vật.
        Không thêm giảm giá, phí, nợ, mặt hàng, lần mua hoặc điều kiện. Giữ đúng vai mua hàng và đưa tiền cho người bán.
        {v1}, {v2} chứa đủ tên món và từ đếm; không lặp các từ ấy quanh biến. {v3} chứa đơn vị tiền; không thêm đơn vị hay ký hiệu.
        Dữ kiện kết thúc bằng dấu chấm, câu hỏi kết thúc bằng dấu hỏi. Chỉ trả JSON theo cấu trúc mẫu; không tính toán hay giải thích kỹ thuật.
        """:"""
        Write a short natural primary-school problem about buying one type of item and receiving change.
        Give three separate facts: each {v1} costs {f0} {v3}; {v0} buys {f1} {v2}; {v0} actually hands the seller {f2} {v3} to pay.
        Cash is actually tendered for this purchase, not savings, a wallet balance, debt or a loan.
        Ask only the change {v0} receives in {v3}. Solution leads are noun phrases: step_0 names total cost of {v2}; step_1 names change returned to {v0}. No Find, Calculate or calculation instructions.
        Vary sentence openings, structure and the question naturally; the example is a reference. Use one short sentence per field.
        Keep each placeholder once in its original field and preserve role/step. Fill no names, numbers, units or answers; never invent actors.
        Add no discount, fee, debt, product, purchase or condition. Preserve buying and cash handed to the seller.
        {v1}/{v2} already contain the full singular/count item words; do not repeat them around slots. {v3} contains the money unit; add no symbol or unit.
        Facts end with periods and the question with a question mark. Return only JSON with the reference schema, without calculations or technical notes.
        """;
}
