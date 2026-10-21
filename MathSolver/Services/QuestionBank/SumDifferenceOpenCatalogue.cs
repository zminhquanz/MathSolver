using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class SumDifferenceOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.TwoNumbers
        &&c.Story?.NarrativeId.StartsWith(SumDifferencePilot.Prefix,StringComparison.Ordinal)==true;
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        if(c.BankVariant!=(int)ElementaryQuizType.SumDifference)throw new ArgumentException("InvalidSumDifferenceRelation");
        var d=SumDifferencePilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        var q=new ElementaryQuizGenerator(new(c.Story.Seed)).GenerateOpenSumDifference(ArithmeticQuizMode.Essay,c.Language,c.Tier,d.Profile);
        string[] roles=["combined-total","larger-minus-smaller","larger-owner","smaller-owner","common-counted-unit"];
        var quantities=d.Values.Select((v,i)=>new NarrativeQuantity(v.Key,roles[i],v.Value)).ToArray();
        NarrativeClause[] facts=[new("fact_0",d.Context.Total),new("fact_1",d.Context.Difference)];
        string[] leads=[d.Context.SmallerLead,d.Context.LargerLead];
        var p=q.ElementaryProblem!;
        var steps=p.Reasoning!.Steps.Select((s,i)=>new NarrativeStep("step_"+i,leads[i],
            QuizMathExpressionFormatter.Format(s.Expression)+" = "+s.DisplayValue+" "+s.Unit)).ToArray();
        string signature=string.Join("\n",SumDifferencePilot.Relation,d.Profile,c.Language,c.Tier,string.Join("/",roles),d.Context);
        return new(q,d.Profile,d.Context.SmallerOwner+" / "+d.Context.LargerOwner,d.Context.Unit,p.AnswerText,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature))),quantities,facts,d.Context.Question,steps,
            facts.ToDictionary(f=>f.Role,f=>new[]{f.Text}),[d.Context.Question],
            steps.ToDictionary(s=>s.Id,s=>new[]{s.Lead}));
    }
    internal const string Relation="relation=direct-total-oriented-difference-find-both; {f0} is the COMBINED TOTAL owned by {v0} and {v1}; {f1} is {v0}'s amount MINUS {v1}'s amount. {v0} has MORE, {v1} has LESS. {v2} is their common counted-item unit, not an owner. Both amounts are unknown. Ask for BOTH amounts separately, retaining their owners. No transfer, ratio, change over time, extra people or inferred total/difference. C# solves smaller=(total-difference)/two, larger=(total+difference)/two; never reveal answers or calculate. step_0 names the amount for {v1} (smaller), step_1 names the amount for {v0} (larger).";
    internal static string WritingInstructions(AppLanguage language)=>language==AppLanguage.Vietnamese?"""
        Bạn là giáo viên viết bài toán tổng–hiệu tự nhiên cho học sinh tiểu học.
        Chỉ biết tổng số đồ vật của hai bên và số đồ vật bên này nhiều hơn bên kia. Hỏi riêng số đồ vật của cả hai bên.
        Ví dụ chỉ để tham khảo; được thay cấu trúc câu, cách mở đầu, từ nối và cách nói so sánh. Viết mới ít nhất một câu dữ kiện.
        Câu tổng phải nói tổng của cả {v0} và {v1}, không phải số của mỗi bên.
        Ví dụ: 'Tổng số {v2} của {v0} và {v1} là {f0}.' hoặc '{v0} và {v1} có tất cả {f0} {v2}.'.
        {v0} có nhiều hơn {v1} đúng {f1} {v2}. Có thể nói tương đương '{v1} có ít hơn {v0} {f1} {v2}.'. Không đảo người nhiều/người ít.
        Giữ động từ 'có', 'giữ' hoặc 'sở hữu' trước 'nhiều hơn/ít hơn' khi so sánh số đồ vật; không viết như đang so sánh trực tiếp hai tên người/nhóm.
        So sánh trực tiếp số đồ vật bằng 'nhiều hơn' hoặc 'ít hơn', không thêm lời đánh giá hay cụm chỉ số lượng dài dòng.
        Chỉ dùng một cụm chỉ tổng như 'tổng cộng' hoặc 'tất cả'; không ghép thành 'có chung tổng cộng'.
        Không chỉ nói hiệu giữa hai bên mà bỏ mất ai nhiều hơn ai. Không biến hiệu thành tỉ số, số đã tặng hoặc lượng thêm vào.
        Câu hỏi phải yêu cầu riêng từng bên, ví dụ 'Hỏi số {v2} của {v0} và {v1} lần lượt là bao nhiêu?'. Không hỏi lại tổng, hiệu hay chỉ một bên.
        Câu dẫn lời giải chỉ gọi lượng cần tìm: step_0 thuộc {v1}, step_1 thuộc {v0}. Giữ thứ tự này, không tính toán.
        Mỗi trường là một câu ngắn, tối đa 24 từ. Giữ từng biến đúng một lần trong đúng trường và nguyên role/step.
        Không điền tên/số/đơn vị vào biến, không thêm sự việc, điều kiện hoặc chỉ dẫn kỹ thuật. Chỉ trả JSON.
        """:"""
        You are a teacher writing a natural total-and-difference problem for primary pupils.
        Only the combined total of two stocks and their oriented difference are known; ask for each owner's actual amount.
        Examples are references, not exhaustive sentence choices. Vary openings, structure and comparative wording; write at least one new fact sentence.
        State the COMBINED total, not that each owner has that amount. For example: 'Together, {v0} and {v1} have {f0} {v2}.' or 'The total number of {v2} belonging to {v0} and {v1} is {f0}.'.
        {v0} has MORE than {v1}, by {f1} {v2}. Natural equivalents: '{v0} has {f1} more {v2} than {v1}.' or '{v1} has {f1} fewer {v2} than {v0}.'. Keep the direction unchanged.
        Prefer everyday words such as has, have, total, more and fewer. Do not replace simple words merely to sound different.
        Do not just state an unsigned difference; make clear which owner has more. Do not turn the difference into a ratio, gift or increase.
        Ask for BOTH amounts separately, for example 'How many {v2} do {v0} and {v1} have respectively?'. Do not ask again for the total/difference or only one owner.
        step_0 names only the amount for {v1} (smaller); step_1 names only the amount for {v0} (larger). Preserve this order; do not calculate.
        For countable items, prefer number or count in the solution headings. Keep the comparison short and natural.
        Write one short sentence per field, at most 24 words. Keep each placeholder exactly once in its field and preserve role/step.
        Do not fill names, numbers or units into placeholders, add events/conditions, or include technical instructions. Return only JSON.
        """;
    internal static string WritingFocus(BasicQuestionContract c)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        // Vary emphasis between contexts without enumerating sentences in the
        // grammar or weakening the owner-bound meaning check.
        string focus=c.Story!.NarrativeId[SumDifferencePilot.Prefix.Length..] switch
        {
            "notebooks"=>vi?"Lượt này ưu tiên mở câu tổng bằng hai chủ thể; diễn đạt hiệu từ phía bên có ít hơn. Hỏi riêng hai bên bằng câu ngắn."
                :"For this request, favor the two owners as the subject of the combined total; describe the comparison from the owner with fewer items.",
            "books"=>vi?"Lượt này ưu tiên mở câu tổng bằng tên đại lượng, đưa hai chủ thể sau đó; diễn đạt hiệu từ phía bên có nhiều hơn."
                :"For this request, favor the combined number of items as the total sentence's subject; describe the comparison from the owner with more items.",
            _=>vi?"Lượt này ưu tiên mở câu tổng bằng tên đại lượng; câu so sánh có thể đưa bên được so sánh lên trước bằng cụm so với, giữ rõ ai nhiều hơn."
                :"For this request, favor the combined count as the total sentence's subject; the comparison may state the additional items compared to the other owner."
        };
        return "\n"+focus;
    }
    internal static string PromptRelation(BasicQuestionContract c)
    {
        var d=SumDifferencePilot.Create(c.SceneId,c.Language,c.Tier,new(c.Story!.Seed));
        return Relation+"\nContext for understanding only: {v0}="+d.Context.LargerOwner+"; {v1}="+d.Context.SmallerOwner
            +"; {v2}="+d.Context.Unit+". Keep the placeholders, not their values, in the output. Both owners hold stocks; no transfer occurs.";
    }
}
