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
        fact_0 nêu giá gốc của {v0} là {f0} {v1}; fact_1 nêu giá của {v0} giảm {f1} so với giá gốc.
        Hỏi số tiền phải trả cho {v0} sau giảm giá, bằng {v1}, không hỏi số tiền được giảm hay giá gốc.
        step_0 là cụm danh từ gọi số tiền giảm giá của {v0}; step_1 gọi số tiền phải trả cho {v0}. Không viết chỉ dẫn tính toán.
        Đổi cấu trúc câu và cách hỏi tự nhiên. Giữ mỗi biến đúng một lần trong trường gốc, giữ role và step.
        {v0} chứa đủ tên một món hàng và từ đếm; {v1} chứa đơn vị tiền; {f1} đã chứa dấu phần trăm. Không lặp các từ ấy quanh biến.
        Không điền tên, số, đơn vị hay đáp án. Không thêm người, món hàng, số lượng mua, điều kiện, phí, thuế, quà tặng, tiền đưa hay tiền thừa.
        Mỗi trường một câu ngắn; dữ kiện kết thúc bằng dấu chấm, câu hỏi kết thúc bằng dấu hỏi. Chỉ trả JSON theo cấu trúc mẫu.
        """:"""
        Write a short natural primary-school problem about the payable price of one product after one discount.
        fact_0 gives {v0}'s original price as {f0} {v1}; fact_1 states a discount of {f1} of {v0}'s ORIGINAL price.
        Ask only how much must be paid for {v0} after the discount in {v1}, not the discount amount or original price.
        step_0 names the discount amount on {v0}; step_1 names the payable price of {v0}. Use noun phrases, without calculation instructions.
        Vary sentence openings, structure and questions naturally. Preserve each placeholder once in its original field, and preserve roles and steps.
        {v0} includes a complete single product with its article; {v1} contains the money unit; {f1} already includes the percent sign. Do not duplicate them.
        Fill no names, numbers, units or answers. Add no actor, purchase count, product, condition, tax, fee, gift, cash tendered or change.
        Use one short sentence per field. End facts with periods and the question with a question mark. Return only JSON with the reference schema.
        """;
}
