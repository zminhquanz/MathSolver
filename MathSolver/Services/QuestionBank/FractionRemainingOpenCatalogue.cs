using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class FractionRemainingOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c) => c.Family==BankQuestionFamily.FractionQuantity
        && (c.BankVariant==(int)ElementaryQuizType.FractionRemaining
            || c.Story?.NarrativeId.StartsWith(FractionRemainingPilot.Prefix,StringComparison.Ordinal)==true);
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        if(c.BankVariant!=(int)ElementaryQuizType.FractionRemaining)throw new ArgumentException("InvalidFractionRemainingRelation");
        var d=FractionRemainingPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        var q=new ElementaryQuizGenerator(new(c.Story.Seed)).GenerateFractionRemaining(ArithmeticQuizMode.Essay,c.Language,c.Tier,c.Story.NarrativeId);
        string[] roles=["original-whole","donated-fraction-of-original-whole","donor","counted-unit","recipient"];
        var quantities=d.Values.Select((v,i)=>new NarrativeQuantity(v.Key,roles[i],v.Value)).ToArray();
        NarrativeClause[] facts=[new("fact_0",d.Context.Whole),new("fact_1",d.Context.Share)];
        var p=q.ElementaryProblem!;
        var s=p.Reasoning!.Steps.Single();
        NarrativeStep[] steps=[new("step_0",d.Context.Lead,QuizMathExpressionFormatter.Format(s.Expression)+" = "+s.DisplayValue+" "+s.Unit)];
        string signature=string.Join("\n",FractionRemainingPilot.Relation,d.Profile,c.Language,c.Tier,string.Join("/",roles),d.Context);
        string schema=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
        bool vi=c.Language==AppLanguage.Vietnamese;
        var phrasings=new Dictionary<string,string[]>
        {
            ["fact_0"]=[d.Context.Whole,vi?"Trong kho của {v0} có {f0} {v1}.":"{f0} {v1} belong to {v0}."],
            ["fact_1"]=[d.Context.Share,vi?"{v0} đã cho {v2} {f1} số {v1} đó.":"{v0} gives {v2} {f1} of those {v1}."]
        };
        return new(q,d.Profile,d.Render(d.Context.Lead),d.Context.Unit,p.AnswerText,schema,quantities,facts,d.Context.Question,steps,
            phrasings,[d.Context.Question],new Dictionary<string,string[]>{{"step_0",[d.Context.Lead]}});
    }
    internal static string PromptRelation(AppLanguage language) => language==AppLanguage.Vietnamese
        ? "Quan hệ=known-whole-donated-find-remaining. {v0} là người tặng, {v2} là người nhận, {v1} là đơn vị đồ vật. {f0} là tổng số đồ vật ban đầu của {v0}. {f1} là phần đã tặng cho {v2}, tính trên chính tổng số ban đầu ấy. Hỏi số đồ vật {v0} còn lại sau một lần tặng. Giữ nguyên lượng làm gốc và vai trò hai người; chỉ có một lần tặng. C# giữ phép tính và đáp số."
        : "Relation=known-whole-donated-find-remaining. {v0} is the giver, {v2} the recipient and {v1} the counted item unit. {f0} is the original number of items the giver has. {f1} is the fraction of that original stock given to the recipient. Ask how many items the giver has left after this single gift. Keep the original base and both people's roles unchanged; there is only one gift. C# owns the calculation and answer.";

    internal static string RetryInstructions(AppLanguage language,string error,string? details) => language==AppLanguage.Vietnamese
        ? "\nLượt trước chưa đạt: "+error+". "+details+" Viết lại câu dữ kiện bằng cấu trúc khác: có thể đưa số đồ vật hoặc người nhận lên đầu câu. Giữ đúng hướng tặng, lượng ban đầu, các biến và câu hỏi về số còn lại."
        : "\nPrevious attempt: "+error+". "+details+" Change a factual sentence's structure, for example by putting the item count or recipient first. Preserve the gift direction, original stock, placeholders and remaining-count question.";

    internal static string WritingInstructions(AppLanguage language) => language==AppLanguage.Vietnamese ? """
        Viết một đề MỚI bằng tiếng Việt tự nhiên, phù hợp học sinh tiểu học. Đổi cấu trúc ít nhất một câu dữ kiện so với ví dụ; chỉ đổi câu dẫn lời giải chưa tạo thành đề mới.
        Mỗi trường là một câu ngắn. Có thể đổi cách mở đầu, từ nối, câu chủ động hoặc bị động và vị trí người nhận. Dùng từ quen thuộc như có, tặng, cho.
        Câu dữ kiện đầu phải nói rõ số đồ vật BAN ĐẦU, trước khi tặng. Có thể đặt người sở hữu hoặc tổng số đồ vật lên đầu câu. Dùng trực tiếp {f1} của tổng số đồ vật ban đầu để nói số đã tặng.
        Hỏi số đồ vật người tặng còn lại hoặc giữ lại. Câu dẫn lời giải chỉ gọi tên số đồ vật còn lại.
        Gợi ý cách đặt câu tự nhiên: Số {v1} ban đầu của {v0} là {f0}. Hoặc đổi câu tặng sang bị động: {v2} được {v0} tặng {f1} tổng số {v1} ban đầu. Đây chỉ là gợi ý cấu trúc; được viết cách khác với cùng dữ kiện.
        Giữ từng biến bắt buộc đúng một lần trong trường tương ứng. Giữ nguyên chủ thể, người nhận, đơn vị, lượng làm gốc và một lần tặng; không thêm người, điều kiện hay hành động.
        Chỉ trả JSON theo cấu trúc ví dụ. Giữ biến, role và step; để C# điền số, tính toán và viết đáp số. Ví dụ là tài liệu tham khảo, không phải toàn bộ cách viết được phép.
        """ : """
        Write a NEW, natural problem for primary-school children. Change the structure of at least one fact sentence from the example; changing only the solution lead does not make a new problem.
        Use one short sentence per field and everyday gift wording such as gives or was given. You may change openings, links, active/passive voice and the recipient's position.
        The first fact must explicitly state the ORIGINAL item count BEFORE the gift. You may put the owner or original count first. Use {f1} of the original items for the amount given.
        Ask how many items the giver has left or still keeps. The solution lead names only the remaining item count.
        Natural structure suggestions: The original number of {v1} that {v0} had was {f0}. Or use a passive gift: {v2} was given {f1} of the original {v1} by {v0}. These are examples of structure, not a required wording or an exhaustive list.
        Preserve every required placeholder exactly once in its field. Keep the giver, recipient, unit, original base and single gift unchanged. Add no people, events or conditions.
        Return only JSON with the example's fields, role and step values. Keep placeholders for C# to fill; C# owns calculations and answers. The example is a reference, not an exhaustive list of allowed sentences.
        """;
}
