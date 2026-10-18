using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class PercentageWholeOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Percentage
        &&c.Story?.NarrativeId.StartsWith(PercentageWholePilot.Prefix,StringComparison.Ordinal)==true;
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        if(c.BankVariant!=(int)PercentageQuizType.FindWholeFromPercentageValue)throw new ArgumentException("InvalidPercentageWholeVariant");
        var d=PercentageWholePilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        var q=new PercentageQuizGenerator(new(c.Story.Seed)).GenerateOpenWhole(ArithmeticQuizMode.Essay,c.Language,c.Tier,c.Story.NarrativeId);
        var quantities=new NarrativeQuantity[]{new("f0","donated-count",d.Values["f0"]),new("f1","donated-percentage-of-original-whole",d.Values["f1"]),
            new("v0","donor",d.Context.Donor),new("v1","counted-unit",d.Context.Unit),new("v2","recipient",d.Context.Recipient)};
        NarrativeClause[] facts=[new("fact_0",d.Context.Gift),new("fact_1",d.Context.Share)];
        var p=q.PercentageProblem!;
        NarrativeStep[] steps=[new("step_0",d.Context.Lead,p.EquationText+" "+p.AnswerUnit)];
        string signature=string.Join("\n",PercentageWholePilot.Relation,d.Profile,c.Language,c.Tier,d.Context,string.Join("/",quantities.Select(x=>x.Role)));
        string schema=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
        bool vi=c.Language==AppLanguage.Vietnamese;
        var phrasings=new Dictionary<string,string[]>
        {
            ["fact_0"]=[d.Context.Gift,vi?"{v2} nhận được {f0} {v1} từ {v0}.":"{v2} received {f0} {v1} from {v0}."],
            ["fact_1"]=[d.Context.Share]
        };
        return new(q,d.Profile,d.Render(d.Context.Lead),d.Context.Unit,p.CorrectAnswer+" "+p.AnswerUnit,schema,quantities,facts,d.Context.Question,steps,
            phrasings,[d.Context.Question],new Dictionary<string,string[]>{{"step_0",[d.Context.Lead]}});
    }
    internal static string PromptRelation(AppLanguage language)=>language==AppLanguage.Vietnamese
        ? "Quan hệ=known-donated-percentage-part-find-original-whole. {v0} là người tặng, {v2} là người nhận, {v1} là đơn vị đồ vật. {f0} là SỐ ĐÃ TẶNG, không phải tổng số hay số còn lại. Số đã tặng bằng {f1} tổng số BAN ĐẦU của {v0}. Hỏi tổng số ban đầu chưa biết. Chỉ có một lần tặng; C# giữ phép tính và đáp số."
        : "Relation=known-donated-percentage-part-find-original-whole. {v0} is the giver, {v2} the recipient and {v1} the item unit. {f0} is the GIVEN-AWAY count, not the original or remaining count. The donated count is {f1} of the giver's ORIGINAL total. Ask for that unknown original total. Only one gift; C# owns the calculation and answer.";
    internal static string WritingInstructions(AppLanguage language)=>language==AppLanguage.Vietnamese?"""
        Bạn viết đề mới, tự nhiên cho học sinh tiểu học. Ví dụ là tài liệu tham khảo, không giới hạn mọi câu được phép viết.
        Đổi cấu trúc ít nhất một câu dữ kiện: được đổi cách mở đầu, từ nối, chủ động/bị động hoặc đưa người nhận lên đầu.
        Câu đầu nói {v0} đã tặng {v2} đúng {f0} {v1}. Câu thứ hai nói số đã tặng bằng {f1} tổng số ban đầu của {v0}; chỉ dùng {v1} một lần trong câu này.
        Trong câu tỉ lệ, số đã tặng là vế trái, tổng số ban đầu là lượng gốc ở vế phải. Không đảo thành tổng số ban đầu bằng một phần trăm của số đã tặng.
        Câu hỏi tìm tổng số ban đầu của người tặng, trước khi tặng. Câu dẫn lời giải chỉ gọi tên tổng số đồ vật ban đầu.
        Nêu rõ người sở hữu tổng số ban đầu bằng biến tương ứng; tránh “họ”, “người này” hoặc “chính mình” khiến lượng gốc thiếu rõ ràng.
        Mỗi trường là một câu ngắn. {f1} đã bao gồm số và dấu %, không thêm chữ phần trăm; không viết số bằng chữ. Giữ nguyên từng biến, người cho/người nhận, đồ vật và một lần tặng. Không tự điền số, giải thích biến hoặc thêm dữ kiện; không đổi thành phần còn lại.
        Kết thúc mỗi câu dữ kiện bằng dấu chấm và câu hỏi bằng dấu hỏi. Chỉ trả JSON như cấu trúc ví dụ. C# sẽ điền tên và số, tính toán và viết đáp số.
        """:"""
        Write a new natural problem for elementary pupils. The example is a reference, not every permitted sentence.
        Change the structure of at least one fact: vary openings, links, active/passive voice or put the recipient first.
        The first fact says {v0} gave exactly {f0} {v1} to {v2}. The second says the donated count equals {f1} of {v0}'s original total; use {v1} only once in this field.
        In the rate statement, the donated count is the left side of the equality and the original stock is the base on the right. Never reverse this into the original whole being a percentage of the given-away count.
        Ask for the giver's total BEFORE the gift. The solution lead names only that original item count.
        Bind the original stock to the giver explicitly, not an ambiguous “their”. For counted items use number or count, not amount.
        Use subject-verb agreement: the number is or equals; plural items are or represent. Name the original stock of the configured items, not an unspecified collection of other items.
        Use one short sentence per field. {f1} already includes the number and percent sign: do not append percent or fill counts in words. Preserve placeholders, people, objects and the single gift. Fill no values, define no placeholders and add no events or conditions. Do not use the remaining count as the known part or percentage base.
        Write ordinary pupil-facing prose, without functions, aliases or definitions. End facts with a period and the question with a question mark. The question must be a full interrogative with a verb.
        Return only JSON with the example's schema. C# fills names and numbers and owns calculations and answers.
        """;
    internal static string RetryInstructions(AppLanguage language,string error,string? details)=>language==AppLanguage.Vietnamese
        ? "\nLượt trước chưa đạt: "+error+". "+details+" Viết lại một câu dữ kiện. Giữ số đã tặng và tỉ lệ phần trăm trên tổng số ban đầu; hỏi tổng số trước khi tặng."
        : "\nPrevious attempt: "+error+". "+details+" Rephrase a fact. Preserve the donated count and its percentage of the original total; ask for the total before the gift.";
}
