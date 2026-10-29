using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class DiscountOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.MultiStep
        &&c.Story?.NarrativeId.StartsWith(DiscountOpenPilot.Prefix,StringComparison.Ordinal)==true;
    internal static ArithmeticQuizQuestion Generate(BasicQuestionContract c,ArithmeticQuizMode mode)
    {
        if(!DiscountOpenPilot.ValidateContract(c))throw new ArgumentException("InvalidDiscountContract");
        return new ElementaryQuizGenerator(new(c.Story!.Seed)).GenerateOpenDiscount(mode,c.Language,c.Tier,c.Story.NarrativeId);
    }
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        var q=Generate(c,ArithmeticQuizMode.Essay);
        var d=DiscountOpenPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        string[] roles=["original-price","discount-percentage-of-original","single-product","money-unit"];
        var quantities=d.Values.Select((v,i)=>new NarrativeQuantity(v.Key,roles[i],v.Value)).ToArray();
        NarrativeClause[] facts=[new("fact_0",d.Context.PriceFact),new("fact_1",d.Context.DiscountFact)];
        string[] leads=[d.Context.DiscountLead,d.Context.PayableLead];
        var p=q.ElementaryProblem!;
        var steps=p.Reasoning!.Steps.Select((s,i)=>new NarrativeStep("step_"+i,leads[i],
            QuizMathExpressionFormatter.Format(s.Expression)+" = "+s.DisplayValue+" "+s.Unit)).ToArray();
        string schema=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",DiscountOpenPilot.Relation,d.Profile,c.Language,c.Tier,d.Context,string.Join("/",roles)))));
        return new(q,d.Profile,d.Context.Product,d.Context.MoneyUnit,p.AnswerText,schema,quantities,facts,d.Context.Question,steps,
            facts.ToDictionary(f=>f.Role,f=>new[]{f.Text}),[d.Context.Question],steps.ToDictionary(s=>s.Id,s=>new[]{s.Lead}));
    }
    internal static string PromptRelation(BasicQuestionContract c)=>"relation="+DiscountOpenPilot.Relation
        +"; {f0}=original price of the single product {v0} in {v1}; {f1}=discount percentage of that ORIGINAL price, already includes %. "
        +"Target=final payable price in {v1}, after subtracting the discount from the original price. step_0=discount amount; step_1=payable price. "
        +"One product, one unconditional discount. No cash/change, donation, count, extra fee or second reduction.";
    internal static string WritingInstructions(BasicQuestionContract c)=>WritingInstructions(c.Language);
    internal static string WritingInstructions(AppLanguage language)=>language==AppLanguage.Vietnamese?"""
        Viết bài toán ngắn, tự nhiên về giá phải trả cho một món hàng sau một lần giảm giá.
        fact_0 xác định giá gốc trước giảm của {v0}: {f0} là số tiền, {v1} là đơn vị. fact_1 xác định phần giảm {f1} của chính giá gốc ấy.
        Câu hỏi chỉ tìm số tiền cần thanh toán cho {v0} sau giảm giá, với đơn vị {v1}.
        Hai câu dẫn là cụm danh từ rõ nghĩa: step_0 gọi số tiền được giảm trong giá của {v0}; step_1 gọi số tiền phải trả cho {v0}.
        Gọi rõ đại lượng tiền ở step_0, chẳng hạn "Số tiền được giảm của {v0}"; fact_1 luôn nói rõ {f1} là phần giảm so với giá gốc.
        Đổi cách mở đầu, cấu trúc dữ kiện và cách hỏi tự nhiên. Giữ mỗi biến đúng một lần trong trường gốc, giữ role và step.
        {v0} là cả cụm danh từ chỉ một sản phẩm, đã có từ đếm như "một quyển vở"; đặt nguyên cụm vào vai trò sản phẩm, dùng nguyên biến ấy làm chủ thể hoặc sau từ "của", "cho".
        {f0} luôn là giá tiền, {f1} luôn là tỉ lệ giảm đã có dấu phần trăm, {v1} luôn là đơn vị tiền. Viết số và đơn vị chỉ bằng các biến tương ứng.
        Không điền tên, số, đơn vị hay đáp án. Không thêm người, món hàng, số lượng mua, điều kiện, phí, thuế, quà tặng, tiền đưa hay tiền thừa.
        Mỗi trường một câu ngắn; dữ kiện kết thúc bằng dấu chấm, câu hỏi kết thúc bằng dấu hỏi. Chỉ trả JSON theo cấu trúc mẫu.
        """:"""
        Write a short natural primary-school problem about the payable price of one product after one discount.
        fact_0 identifies the original price of {v0} before the discount: {f0} is the money amount and {v1} its unit. fact_1 states a reduction of {f1} of that original price.
        Ask only for the final payment due for {v0} after this reduction, in {v1}.
        Both solution leads are clear noun phrases: step_0 names the MONEY saved from the price of {v0}; step_1 names the final payable amount for {v0}.
        In step_0 use a monetary heading such as "Discount amount on {v0}"; this amount is money saved from the original price. In step_1 use a heading such as "Final payment due for {v0}".
        Vary sentence openings, fact structure and questions naturally. Preserve each placeholder once in its original field, and preserve roles and steps.
        {v0} is a complete product noun phrase including its article, such as "a notebook". Use the whole phrase as the product, directly as a subject or after "of" or "for"; {f0} identifies its price amount.
        {f0} always names money, {f1} always names the percentage already including its sign, and {v1} always names the money unit. Express amounts and units only through their slots.
        Fill no names, numbers, units or answers. Add no actor, purchase count, product, condition, tax, fee, gift, cash tendered or change.
        Use one short sentence per field. End facts with periods and the question with a question mark. Return only JSON with the reference schema.
        """;
}
