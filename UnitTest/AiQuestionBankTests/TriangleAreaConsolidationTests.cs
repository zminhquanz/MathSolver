using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text.RegularExpressions;

internal static class TriangleAreaConsolidationTests
{
    // These literal fixtures catch incomplete whole-clause proofs. Matching
    // numbers, area arithmetic or a heading cannot supply a missing base role.
    internal static Task RunAsync()
    {
        CheckJoiningDisposition();
        int clearCount=0,rejectCount=0,reviewCount=0,freshCount=0,promptCount=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(string profile in TriangleAreaPilot.Profiles(language))
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            bool vi=language==AppLanguage.Vietnamese;
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Geometry,TriangleAreaPilot.Variant,tier,language,new(631),profile) with {OpenProse=true};
            var reference=ReasoningStoryCatalogue.Draft(c);
            var native=reference with
            {
                Facts=[new("fact_0",vi?"{v0} là một {v1}.":"{v0} is a {v1}."),
                    new("fact_1",vi?"Cạnh đáy của {v0} có độ dài là {f0} {v2}.":"The measurement of the base for {v0} is {f0} {v2}."),
                    new("fact_2",vi?"Chiều cao đo vuông góc với cạnh đáy đó của {v0} là {f1} {v2}.":"The height measured perpendicularly from that specific base to the opposite vertex of {v0} measures {f1} {v2}.")],
                Question=vi?"Diện tích toàn bộ {v0} là bao nhiêu {v3}?":"What is the total area of {v0} expressed in {v3}?",
                SolutionLeads=[new("step_0",vi?"Diện tích của {v0}":"Area of {v0}")]
            };
            var variant=native with
            {
                Facts=[new("fact_0",vi?"{v0} có hình dạng {v1}.":"{v0} is shaped like a {v1}."),
                    new("fact_1",vi?"Cạnh đáy của {v0} có độ dài bằng {f0} {v2}.":"The measurement of the base of {v0} equals {f0} {v2}."),
                    new("fact_2",vi?"Chiều cao đo vuông góc với chính cạnh đáy đó của {v0} bằng {f1} {v2}.":"The altitude measured perpendicularly from the given base to the opposite vertex of {v0} is {f1} {v2}.")],
                Question=vi?"Diện tích toàn bộ của {v0} bằng bao nhiêu {v3}?":"What is the entire area of {v0} in {v3}?",
                SolutionLeads=[new("step_0",vi?"Diện tích toàn bộ {v0}":"The entire area of {v0}")]
            };
            var another=variant with
            {
                Facts=[variant.Facts![0],
                    new("fact_1",vi?"Độ dài cạnh đáy của {v0} đo được {f0} {v2}.":"The base length for {v0} measures {f0} {v2}."),
                    new("fact_2",vi?"{v0} có chiều cao đo vuông góc với cạnh đáy đó dài {f1} {v2}.":"The height measured perpendicularly from this base to the opposite vertex of {v0} equals {f1} {v2}.")],
                Question=vi?"Diện tích toàn bộ của {v0} tính theo {v3} là bao nhiêu?":"In {v3}, what is the total area of {v0}?"
            };
            foreach(var clear in new[]{native,variant,another})
            {
                var proof=Validate(c,clear);
                Check(proof.IsValid,"Clear prior/generalized triangle clause requires proof: "+language+"/"+profile+"/"+tier+": "+proof);
                Check(QuestionBankStore.SerializeDraft(proof.Draft!)==QuestionBankStore.SerializeDraft(clear),"Triangle proof rewrote AI text.");
                TriangleAreaOpenTests.CheckPractice(c,clear);clearCount++;
                for(int seed=0;seed<2;seed++)
                {
                    var fresh=c.FreshFacts(new(941+seed));
                    Check(Validate(fresh,clear).IsValid,"Triangle proof depended on an old number/context binding.");
                    TriangleAreaOpenTests.CheckPractice(fresh,clear);freshCount++;
                }
                foreach(var bad in Wrong(c,clear))
                {
                    var result=Validate(c,bad);
                    Check(!result.IsValid&&!result.NeedsReview,"Explicit wrong triangle role/unit/event must reject: "+QuestionBankStore.SerializeDraft(bad)+" => "+result);rejectCount++;
                }
                // Qualifiers stay attached to the clause. No stripping may turn
                // a denial, hypothesis or unproved assertion into a given.
                foreach(string prefix in vi?new[]{"Không phải ","Nếu ","Chỉ khi ","Theo ghi chép, "}:new[]{"It is not true that ","If ","Only if ","According to the record, "})
                foreach(int field in new[]{0,1,2,3})
                {
                    var qualified=field==3?clear with {Question=prefix+clear.Question}:WithFact(clear,field,prefix+clear.Facts![field].Text);
                    Check(Validate(c,qualified).NeedsReview,"Qualified triangle assertion must wait for review: "+prefix+" field="+field);reviewCount++;
                }
                var unknown=WithFact(clear,2,vi?"Chiều cao của {v0} là {f1} {v2}.":"The height of {v0} is {f1} {v2}.");
                Check(Validate(c,unknown).NeedsReview,"Missing perpendicular/given-base proof must remain review.");reviewCount++;
            }
            var sideAsBase=reference with
            {
                Facts=[new("fact_0",vi?"{v0} là một {v1}.":"The figure {v0} is a {v1}."),
                    new("fact_1",vi?"Một cạnh của {v0} dài {f0} {v2}.":"One side of {v0} measures {f0} {v2}."),
                    new("fact_2",vi?"Chiều cao đi thẳng lên từ cạnh đó đến đỉnh đối diện của {v0} dài {f1} {v2}.":"The height rising straight up from that side to the opposite vertex is {f1} {v2} for {v0}.")],
                Question=vi?"Diện tích toàn bộ {v0} là bao nhiêu {v3}?":"Determine the total space inside {v0} expressed in {v3}?",
                SolutionLeads=[new("step_0",vi?"Diện tích toàn bộ {v0}":"The entire area of {v0}")]
            };
            Check(Validate(c,sideAsBase).NeedsReview,"Generic side-as-base native draft is ambiguous, not verified or definitely wrong.");reviewCount++;
            CheckPrompt(c);promptCount++;
        }
        Console.WriteLine($"PASS triangle consolidation: {clearCount} old/generalized complete clauses, {freshCount} fresh grading fixtures, {rejectCount} definite rejects, {reviewCount} reviews, {promptCount} complete-slot prompts.");
        return Task.CompletedTask;
    }

    private static IEnumerable<BasicQuestionDraft> Wrong(BasicQuestionContract c,BasicQuestionDraft clear)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        yield return WithFact(WithFact(clear,1,vi?"Chiều cao của {v0} là {f0} {v2}.":"The height of {v0} is {f0} {v2}."),2,
            vi?"Cạnh đáy của {v0} có độ dài là {f1} {v2}.":"The measurement of the base for {v0} is {f1} {v2}.");
        foreach(string height in vi?new[]{
            "Chiều cao đo vuông góc với một cạnh đáy khác của {v0} là {f1} {v2}.",
            "Cạnh bên của {v0} dài {f1} {v2}.",
            "Cạnh đáy của {v0} có độ dài là {f1} {v2}."}:new[]{
            "The height measured perpendicularly from another base to the opposite vertex of {v0} measures {f1} {v2}.",
            "A side of {v0} measures {f1} {v2}.",
            "The measurement of the base for {v0} is {f1} {v2}."})yield return WithFact(clear,2,height);
        foreach(string target in vi?new[]{"Chu vi toàn bộ {v0} là bao nhiêu {v3}?","Diện tích phần bị cắt của {v0} là bao nhiêu {v3}?"}
            :new[]{"What is the perimeter of {v0} expressed in {v3}?","What is the cut area of {v0} expressed in {v3}?"})yield return clear with {Question=target};
        yield return clear with {Question=clear.Question.Replace("{v3}","{v2}")};
        yield return WithFact(clear,2,clear.Facts![2].Text.Replace("{v2}","{v3}"));
        yield return clear with {Question=clear.Question.Replace("{v3}","{v3}²")};
        yield return WithFact(clear,1,clear.Facts![1].Text.TrimEnd('.')+(vi?" rồi cắt một phần.":" before cutting a part away."));
        yield return WithFact(clear,2,clear.Facts![2].Text.TrimEnd('.')+(vi?" rồi ghép thêm một phần.":" before joining another piece."));
        yield return clear with {SolutionLeads=[new("step_0",vi?"Chu vi của {v0}":"The perimeter of {v0}")]};
    }

    private static void CheckPrompt(BasicQuestionContract c)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        string prompt=BasicQuestionPrompt.Build(c),instruction=TriangleAreaOpenCatalogue.WritingInstructions(c.Language);
        var context=TriangleAreaPilot.Contexts(c.Language).Single(x=>TriangleAreaPilot.Prefix+x.Id==c.SceneId);
        foreach(var (slot,value) in new[]{("v0",context.Object),("v1",context.Shape)})
            Check(Regex.IsMatch(prompt,Regex.Escape("{"+slot+"}")+@"\s*=\s*"+Regex.Escape(value),RegexOptions.IgnoreCase),"Prompt must expose the complete bound "+slot+" noun/shape phrase.");
        Check(vi?instruction.Contains("không thêm chữ hình",StringComparison.OrdinalIgnoreCase):instruction.Contains("no article",StringComparison.OrdinalIgnoreCase),"Prompt must explicitly protect complete shape/object slots from extra shape/article text.");
        Check(vi?instruction.Contains("cụm danh từ",StringComparison.OrdinalIgnoreCase)&&instruction.Contains("không dùng động từ",StringComparison.OrdinalIgnoreCase)
            :instruction.Contains("noun phrase",StringComparison.OrdinalIgnoreCase)&&instruction.Contains("imperative",StringComparison.OrdinalIgnoreCase),"Prompt must make the area heading a noun phrase rather than a calculation instruction.");
        Check(vi?instruction.Contains("đổi cấu trúc",StringComparison.OrdinalIgnoreCase):instruction.Contains("vary",StringComparison.OrdinalIgnoreCase),"Triangle wording freedom disappeared.");
        Check(!Regex.IsMatch(prompt,@"\b\d+\b"),"Triangle numeric givens leaked into generation prompt.");
    }
    private static BasicQuestionDraft WithFact(BasicQuestionDraft draft,int index,string text)
    {
        var facts=draft.Facts!.ToArray();facts[index]=facts[index] with {Text=text};return draft with {Facts=facts};
    }
    private static void CheckJoiningDisposition()
    {
        var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.Geometry,TriangleAreaPilot.Variant,CurriculumTier.ThreeStars,
            AppLanguage.English,new(631),TriangleAreaPilot.Prefix+"card") with {OpenProse=true};
        var reference=ReasoningStoryCatalogue.Draft(c);
        var facts=reference.Facts!.ToArray();
        facts[2]=facts[2] with {Text="The perpendicular height joining that base to the opposite vertex of {v0} measures {f1} {v2}."};
        Check(Validate(c,reference with {Facts=facts}).NeedsReview,"A height joining its base to the opposite vertex is not a known extra-shape event.");
        facts[2]=facts[2] with {Text="After joining another piece, the perpendicular height of {v0} corresponding to that base is {f1} {v2}."};
        var extra=Validate(c,reference with {Facts=facts});
        Check(!extra.IsValid&&!extra.NeedsReview,"An explicit extra-piece joining event must remain rejected.");
    }
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft draft)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(draft),c);
    private static void Check(bool condition,string message)=>TestAssert.That(condition,message);
}
