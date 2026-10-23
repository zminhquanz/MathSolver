using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class SumRatioOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.TwoNumbers
        &&c.Story?.NarrativeId.StartsWith(SumRatioPilot.Prefix,StringComparison.Ordinal)==true;
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        if(c.BankVariant!=(int)ElementaryQuizType.SumRatio)throw new ArgumentException("InvalidSumRatioRelation");
        var d=SumRatioPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        var q=new ElementaryQuizGenerator(new(c.Story.Seed)).GenerateOpenSumRatio(ArithmeticQuizMode.Essay,c.Language,c.Tier,d.Profile);
        string[] roles=["combined-total","smaller-to-larger-ratio","smaller-owner","larger-owner","common-counted-unit"];
        var quantities=d.Values.Select((v,i)=>new NarrativeQuantity(v.Key,roles[i],v.Value)).ToArray();
        NarrativeClause[] facts=[new("fact_0",d.Context.Total),new("fact_1",d.Context.Ratio)];
        string[] leads=[d.Context.PartLead,d.Context.SmallerLead,d.Context.LargerLead];
        var p=q.ElementaryProblem!;
        var steps=p.Reasoning!.Steps.Select((s,i)=>new NarrativeStep("step_"+i,leads[i],
            QuizMathExpressionFormatter.Format(s.Expression)+" = "+s.DisplayValue+" "+s.Unit)).ToArray();
        string signature=string.Join("\n",SumRatioPilot.Relation,d.Profile,c.Language,c.Tier,string.Join("/",roles),d.Context);
        return new(q,d.Profile,d.Context.SmallerOwner+" / "+d.Context.LargerOwner,d.Context.Unit,p.AnswerText,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature))),quantities,facts,d.Context.Question,steps,
            facts.ToDictionary(f=>f.Role,f=>new[]{f.Text}),[d.Context.Question],steps.ToDictionary(s=>s.Id,s=>new[]{s.Lead}));
    }
    internal const string Relation="relation=direct-total-oriented-ratio-find-both; {f0} is the COMBINED TOTAL of {v0} and {v1}; {f1} is an immutable fraction: number owned by {v0} DIVIDED BY number owned by {v1}. Numerator belongs to {v0} (smaller), denominator to {v1} (larger). {v2} is the SAME counted-item unit for both owners. No stock transfers, changes, differences, extra people or inferred data. Ask for BOTH owner-specific amounts. C# alone solves and supplies answers. step_0 names the item count per equal ratio part; step_1 names {v0}'s amount; step_2 names {v1}'s amount. No calculations in leads.";
    internal static string WritingInstructions(AppLanguage language)=>language==AppLanguage.Vietnamese?"""
        Bạn là giáo viên viết đề tổng–tỉ ngắn, tự nhiên cho học sinh tiểu học.
        Chỉ biết tổng số đồ vật của hai người và tỉ số giữa hai lượng đó. Hỏi riêng số đồ vật của mỗi người.
        Ví dụ chỉ tham khảo, không phải toàn bộ câu được phép viết. Được đổi cách mở đầu, cấu trúc câu và từ nối; đổi ít nhất một câu dữ kiện.
        Câu tổng nói tổng chung của {v0} và {v1}, không phải số của mỗi người.
        Câu tỉ số nói số {v2} của {v0} bằng {f1} số {v2} của {v1}. Không đảo chủ thể, không đổi thành nhiều hơn, tăng thêm hay số đã tặng.
        {f1} đã là phân số đầy đủ; giữ nguyên biến này, không viết dấu chia, tử số hoặc mẫu số bên ngoài biến.
        Biến {v2} xuất hiện đúng hai lần trong câu tỉ số để gọi rõ đồ vật của từng người; các biến khác giữ số lần như ví dụ, đúng trường.
        Dùng trực tiếp biến người làm chủ thể hoặc người sở hữu, không thay bằng người thứ nhất/người thứ hai rồi gắn biến ở cuối câu. Không dính chữ vào biến.
        Câu hỏi phải hỏi cả hai người, ví dụ Hỏi số {v2} của {v0} và {v1} lần lượt là bao nhiêu?
        Câu dẫn chỉ gọi số đồ vật ứng với mỗi phần rồi số đồ vật của từng người, theo đúng thứ tự step. Không viết đáp án hay phép tính.
        Mỗi trường một câu ngắn, kết thúc dữ kiện bằng dấu chấm, câu hỏi bằng dấu hỏi. Không thêm hành động, điều kiện hoặc chỉ dẫn kỹ thuật.
        Dùng từ đơn giản, giữ các biến, không điền tên, số hoặc đơn vị. Chỉ trả JSON.
        """:"""
        You are a teacher writing a short, natural total-and-ratio problem for primary pupils.
        Only the combined total and the ratio of two item counts are known. Ask for each owner's actual count.
        Examples are references, not exhaustive sentence choices. Vary the opening, structure and links; change at least one fact sentence.
        State the combined total of {v0} and {v1}, not that each person has that total.
        The number of {v2} owned by {v0} equals {f1} of the number of {v2} owned by {v1}. Preserve this ratio direction.
        {f1} already contains the complete fraction. Keep this placeholder intact; add no slash, numerator, denominator, difference or increase.
        The item placeholder {v2} occurs twice in the ratio fact, once for each owner's item count. Preserve the reference's placeholder multiplicities in each field.
        Use {v0} and {v1} as the grammatical owners in each noun phrase and in the question; refer to them by these placeholders throughout the JSON.
        Every concrete item name and numeric value is supplied through its placeholder. Keep words separate from placeholders; an English possessive is {v0}'s or {v1}'s.
        Ask How many for BOTH owners; do not ask for a total, difference or ratio. Prefer number/count for countable items, not amount.
        step_0 names the number of items per equal ratio part; step_1 names {v0}'s count; step_2 names {v1}'s count. No calculations or answers.
        Write one short sentence per field; end facts with periods and the question with a question mark. Keep prose suitable for pupils.
        Do not add transfers, events, conditions, technical instructions or filled names/numbers/units. Return only JSON.
        """;
    internal static string PromptRelation(BasicQuestionContract c)
    {
        var d=SumRatioPilot.Create(c.SceneId,c.Language,c.Tier,new(c.Story!.Seed));
        string focus=c.SceneId[SumRatioPilot.Prefix.Length..] switch
        {
            "books"=>c.Language==AppLanguage.Vietnamese?"Ưu tiên mở câu tổng bằng hai người; câu tỉ số mở bằng số đồ vật của người có lượng bé."
                :"Favor the two owners as the total sentence subject; start the ratio fact with the smaller owner's item count.",
            "pens"=>c.Language==AppLanguage.Vietnamese?"Ưu tiên mở câu tổng bằng tổng số đồ vật; câu tỉ số có thể mở bằng người có lượng bé."
                :"Favor the combined number of items as the total subject; the ratio fact may use the owners' possessive item counts.",
            _=>c.Language==AppLanguage.Vietnamese?"Thay cách mở câu tổng và dùng từ bằng khi nói rõ tỉ số hai lượng."
                :"Vary the total opening and use a clear equality between the two owner-specific item counts."
        };
        return Relation+"\nContext only: {v0}="+d.Context.SmallerOwner+"; {v1}="+d.Context.LargerOwner+"; {v2}="+d.Context.Unit
            +". Never put these values in the JSON.\n"+focus;
    }
}
