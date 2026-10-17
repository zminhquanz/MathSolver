using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class PercentageRemainingOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Percentage
        &&(c.BankVariant==(int)PercentageQuizType.FindRemainingAfterDonation||c.Story?.NarrativeId.StartsWith(PercentageRemainingPilot.Prefix,StringComparison.Ordinal)==true);
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        if(c.BankVariant!=(int)PercentageQuizType.FindRemainingAfterDonation)throw new ArgumentException("InvalidPercentageRemainingVariant");
        var d=PercentageRemainingPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        var q=new PercentageQuizGenerator(new(c.Story.Seed)).GenerateOpenRemaining(ArithmeticQuizMode.Essay,c.Language,c.Tier,c.Story.NarrativeId);
        NarrativeQuantity[] quantities=[new("f0","original-whole-at-100-percent",d.Values["f0"]),
            new("f1","donated-percentage-of-original-whole",d.Values["f1"]),new("v0","donor",d.Context.Donor),
            new("v1","counted-unit",d.Context.Unit),new("v2","recipient",d.Context.Recipient)];
        NarrativeClause[] facts=[new("fact_0",d.Context.Whole),new("fact_1",d.Context.Share)];
        NarrativeStep[] steps=[new("step_0",d.Context.Lead,q.PercentageProblem!.EquationText+" "+d.Context.Unit)];
        string schema=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",PercentageRemainingPilot.Relation,d.Profile,c.Language,c.Tier,d.Context))));
        bool vi=c.Language==AppLanguage.Vietnamese;
        var phrasings=new Dictionary<string,string[]>
        {
            ["fact_0"]=[d.Context.Whole,vi?"Số {v1} ban đầu của {v0} là {f0}.":"The original number of {v1} that {v0} had was {f0}."],
            ["fact_1"]=[d.Context.Share,vi?"{v2} nhận được {f1} tổng số {v1} ban đầu từ {v0}.":"{v2} received {f1} of the original {v1} from {v0}."]
        };
        return new(q,d.Profile,d.Render(d.Context.Lead),d.Context.Unit,d.Answer+" "+d.Context.Unit,schema,quantities,facts,d.Context.Question,steps,
            phrasings,[d.Context.Question],new Dictionary<string,string[]>{{"step_0",[d.Context.Lead]}});
    }
    internal static string PromptRelation(AppLanguage language)=>language==AppLanguage.Vietnamese
        ? "Quan hệ=known-original-whole-donated-percentage-find-remaining. {f0} là tổng số ban đầu của người tặng {v0}, mốc 100%. {f1} là PHẦN TRĂM ĐÃ TẶNG cho {v2} trên chính tổng số ban đầu đó. {v1} là đơn vị đồ vật. Hỏi số đồ vật {v0} còn lại sau một lần tặng. Không đổi phần đã tặng thành phần còn lại, không đổi lượng gốc."
        : "Relation=known-original-whole-donated-percentage-find-remaining. {f0} is giver {v0}'s original whole, the 100% base. {f1} is the PERCENTAGE GIVEN to {v2} of that SAME original stock, not the remaining percentage. {v1} is the item unit. Ask how many items the giver has left after this single gift. Preserve the original base and both people's roles.";
    internal static string WritingInstructions(AppLanguage language)=>language==AppLanguage.Vietnamese?"""
        Viết đề mới tự nhiên cho học sinh tiểu học. Ví dụ là tham khảo, không phải toàn bộ câu được phép viết.
        Được đổi cấu trúc, cách mở đầu, từ nối hoặc vị trí người nhận; đổi ít nhất một câu dữ kiện.
        Có đúng hai NGƯỜI: {v0} là người tặng, {v2} là người nhận. {v1} chỉ là cụm danh từ đếm đồ vật, không phải tên người.
        Đặt {f0} {v1} cạnh nhau khi nêu số lượng; dùng bao nhiêu {v1} khi hỏi. Không viết lại loại đồ vật bằng chữ cố định bên cạnh biến.
        Câu đầu nêu tổng số đồ vật ban đầu. Câu thứ hai nêu người tặng tặng người nhận phần trăm của chính tổng số đó.
        Gắn rõ tổng số ban đầu với người tặng bằng biến tương ứng; tránh đại từ họ. Hỏi số đồ vật người tặng còn lại.
        Mỗi trường một câu ngắn. Giữ từng biến trong trường tương ứng đúng một lần. {f1} đã gồm cả số và dấu %, dùng nguyên biến đó là đủ; không thêm dấu % hay chữ phần trăm phía sau.
        Không viết thêm bất kỳ số lượng nào bằng chữ; chỉ dùng biến số được cung cấp.
        Không giải thích biến, thêm người, thêm lần tặng, nhận thêm, giảm giá hay giải ra đáp án. Dùng câu dẫn ngắn: Số {v1} còn lại.
        Chỉ trả JSON theo cấu trúc ví dụ. C# điền tên, số, tính toán và viết đáp số.
        """:"""
        Write a new natural problem for primary-school pupils. The example is a reference, not every permitted sentence.
        Vary sentence structure, openings, links or the recipient's position; change at least one fact.
        There are exactly two PEOPLE: {v0} is the giver and {v2} is the recipient. {v1} is a plural item noun, NEVER a person or owner.
        Place {f0} {v1} together for a count; ask how many {v1}. Do not replace or duplicate the item noun with hardcoded words.
        The first fact states the original item count. The second states the percentage of that same stock given by the giver to the recipient.
        Bind the original stock explicitly to the giver, avoiding ambiguous their. Ask how many items the giver has left.
        Use one short sentence per field. Preserve each field's placeholders exactly once. {f1} includes both the number and %, so add neither another percent sign nor the word percent after it.
        Add no counts written in words; use only the supplied numeric placeholders.
        Write the facts in ordinary pupil-facing words, without function notation, letter aliases, variable definitions or commentary about the data.
        The question must be a complete interrogative sentence with a verb, asking how many items the giver has after the gift. Only the solution lead may be a short noun phrase.
        End each fact with a period and the question with a question mark, so the assembled problem reads as separate sentences.
        Add no people, gifts, receipts, discounts, placeholder explanations or answers. Use how many for counted items. Use the short lead: Number of {v1} remaining.
        Return only JSON with the example's schema. C# fills names and numbers and owns the calculation and answer.
        """;
    internal static string RetryInstructions(AppLanguage language,string error,string? details)=>language==AppLanguage.Vietnamese
        ? "\nLượt trước chưa đạt: "+error+". "+details+" Viết lại câu dữ kiện; giữ tổng số ban đầu, phần trăm đã tặng, hướng tặng và câu hỏi về phần còn lại."
        : "\nPrevious attempt: "+error+". "+details+" Rephrase a fact, preserving original whole, donated percentage, gift direction and remaining-count question.";
}
