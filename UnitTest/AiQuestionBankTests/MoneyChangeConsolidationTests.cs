using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static class MoneyChangeConsolidationTests
{
    internal static Task RunAsync()
    {
        CheckCostIsNotAnExtraFee();
        CheckRetailAndReturnedChangeVocabulary();
        int cases=0;
        foreach(var language in Enum.GetValues<AppLanguage>())
        foreach(string profile in MoneyChangePilot.Profiles(language))
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            bool vi=language==AppLanguage.Vietnamese;
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.MultiStep,MoneyChangePilot.Variant,tier,language,new(913),profile) with {OpenProse=true};
            var reference=ReasoningStoryCatalogue.Draft(c);
            var clear=vi?reference with {
                Facts=[new("fact_0","Mỗi {v1} có giá {f0} {v3}."),new("fact_1","{v0} đã mua {f1} {v2}."),
                    new("fact_2","Khi thanh toán, {v0} đưa cho người bán {f2} {v3}.")],
                Question="Số tiền thừa mà {v0} nhận lại là bao nhiêu {v3}?"
            }:reference with {
                Facts=[new("fact_0","Every {v1} is priced at {f0} {v3}."),new("fact_1","{v0} purchases {f1} {v2}."),
                    new("fact_2","To complete the transaction, {v0} gives the seller {f2} {v3}.")],
                Question="What amount of change will {v0} get back in {v3}?",
                SolutionLeads=[new("step_0","The combined price for all the {v2}"),new("step_1","The leftover amount given back to {v0}")]
            };
            // The English form is the actual previously reviewed clear native draft.
            Check(Validate(c,clear).IsValid,"Clear complete money clauses still require review: "+profile+"/"+language+"/"+tier+": "+Validate(c,clear));
            MoneyChangeOpenTests.CheckPractice(c,clear);
            MoneyChangeOpenTests.CheckPractice(c.FreshFacts(new(1731)),clear);

            if(!vi)
            {
                foreach(var variant in new[]{
                    clear with {Facts=[new("fact_0","Every {v1} costs {f0} {v3}."),clear.Facts![1],clear.Facts[2]]},
                    clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2","To complete this purchase, {v0} hands the cashier {f2} {v3}.")]},
                    clear with {Question="What amount of change does {v0} receive back in {v3}?"},
                    clear with {SolutionLeads=[new("step_0","Combined price of all {v2}"),new("step_1","Leftover amount returned to {v0}")]}})
                {
                    Check(Validate(c,variant).IsValid,"Equivalent full money clause not proved: "+Validate(c,variant));
                    MoneyChangeOpenTests.CheckPractice(c,variant);
                }
            }

            foreach(string paid in vi?new[]{
                "Khi thanh toán, {v0} không đưa cho người bán {f2} {v3}.",
                "Nếu {v0} đưa cho người bán {f2} {v3} để thanh toán.",
                "Chỉ nếu {v0} đưa cho người bán {f2} {v3} để thanh toán.",
                "{v0} có {f2} {v3} tiền tiết kiệm để thanh toán.",
                "Khi thanh toán, {v0} đưa cho người bán {f2} {v3} rồi mua thêm đồ."
            }:new[]{
                "To complete the transaction, {v0} does not give the seller {f2} {v3}.",
                "If {v0} gives the seller {f2} {v3} to complete the transaction.",
                "Only if {v0} gives the seller {f2} {v3} to complete the transaction.",
                "To complete the transaction, {v0} has {f2} {v3} in savings.",
                "To complete the transaction, {v0} gives the seller {f2} {v3} and buys an additional item."})
                Reject(c,clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",paid)]},"Denial/condition/savings/extra purchase");
            Reject(c,clear with {Facts=[new("fact_0",vi?"Mỗi {v1} có giá giảm còn {f0} {v3}.":"Every {v1} is discounted to {f0} {v3}."),clear.Facts![1],clear.Facts[2]]},"Discount");
            Reject(c,clear with {Facts=[clear.Facts![0],new("fact_1",vi?"{v0} bán {f1} {v2}.":"{v0} sells {f1} {v2}."),clear.Facts[2]]},"Seller/buyer reversal");
            Reject(c,clear with {Facts=[clear.Facts![0],clear.Facts[1],new("fact_2",vi?"Khi thanh toán, người bán đưa cho {v0} {f2} {v3}.":"To complete the transaction, the seller gives {v0} {f2} {v3}.")]},"Cash direction reversal");
            Reject(c,clear with {Facts=[new("fact_0",vi?"Tất cả {v1} có tổng giá {f0} {v3}.":"All {v1} together are priced at {f0} {v3}."),clear.Facts![1],clear.Facts[2]]},"Aggregate price instead of unit price");
            Reject(c,clear with {Question=vi?"Số tiền mua hàng của {v0} là bao nhiêu {v3}?":"What amount of purchase cost will {v0} pay in {v3}?"},"Cost instead of change");
            Reject(c,clear with {Question=clear.Question.Replace("{v3}","{v2}")},"Wrong target unit");
            Reject(c,clear with {Facts=[clear.Facts![0] with {Text=clear.Facts[0].Text.Replace("{f0}","{f1}")},
                clear.Facts[1] with {Text=clear.Facts[1].Text.Replace("{f1}","{f0}")},clear.Facts[2]]},"Price/count slot reversal");
            Reject(c,clear with {Facts=[clear.Facts![0] with {Text=clear.Facts[0].Text.Replace("{v3}",vi?"đô la":"dollars")},clear.Facts[1],clear.Facts[2]]},"Literal currency");
            Reject(c,clear with {SolutionLeads=[clear.SolutionLeads![0],new("step_1",vi?"Số tiền {v0} phải trả":"The cost paid by {v0}")]},"Cost heading instead of change");
            Check(Validate(c,clear with {Facts=[clear.Facts![0] with {Text=(vi?"Theo ghi chú, ":"According to a note, ")+clear.Facts[0].Text},clear.Facts[1],clear.Facts[2]]}).NeedsReview,
                "Unknown complete money prose must still require review once.");
            if(vi)
                Reject(c,clear with {SolutionLeads=[new("step_0","Tính tổng số tiền phải trả cho {v2}"),new("step_1","Tìm số tiền thừa được hoàn trả cho {v0}")]},"Instructional native headings");

            string instructions=MoneyChangeOpenCatalogue.WritingInstructions(language);
            Check(instructions.Contains(vi?"cụm danh từ":"noun phrases",StringComparison.OrdinalIgnoreCase),"Money prompt must demand noun-only solution headings.");
            Check(instructions.Contains(vi?"không thêm nhân vật":"invent actors",StringComparison.OrdinalIgnoreCase),"Money prompt must preserve supplied actors.");
            string relation=MoneyChangeOpenCatalogue.PromptRelation(c);
            Check(relation.Contains("complete item slots",StringComparison.OrdinalIgnoreCase),"Money prompt must explain full singular/count item slots.");
            Check(instructions.Contains(vi?"Được thay cấu trúc câu":"Vary sentence",StringComparison.Ordinal),"Money prose must remain freely rephrased.");
            Check(OpenQuestionProse.Grammar(c).Contains("character ::="),"Money grammar must remain open prose.");
            cases++;
        }
        Console.WriteLine($"PASS {cases} consolidated money contexts/languages/tiers; previous native clear prose, equivalent clauses, hostile contrasts, prompt copy and fresh grading.");
        return Task.CompletedTask;
    }
    private static void Reject(BasicQuestionContract c,BasicQuestionDraft d,string label)=>Check(!Validate(c,d).IsValid,label+" accepted.");
    private static void CheckCostIsNotAnExtraFee()
    {
        var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.MultiStep,MoneyChangePilot.Variant,CurriculumTier.ThreeStars,
            AppLanguage.Vietnamese,new(2713),MoneyChangePilot.Prefix+"notebooks") with {OpenProse=true};
        var reference=ReasoningStoryCatalogue.Draft(c);
        foreach(bool alternate in new[]{false,true})
        {
            var clear=reference with
            {
                Facts=[new("fact_0",alternate?"Giá mỗi {v1} là {f0} {v3}.":"Mỗi {v1} có giá {f0} {v3}."),
                    new("fact_1","{v0} mua {f1} {v2}."),new("fact_2",alternate?"{v0} đưa cho người bán {f2} {v3} để trả tiền.":"{v0} đưa người bán {f2} {v3} để thanh toán.")],
                Question="{v0} nhận lại bao nhiêu {v3} tiền thừa?",
                SolutionLeads=[new("step_0",alternate?"Tổng chi phí của {v2}":"Tổng chi phí cho {v2}"),
                    new("step_1",alternate?"Số tiền thừa {v0} nhận được":"Số tiền thừa mà {v0} được nhận")]
            };
            Check(Validate(c,clear).IsValid,"Total purchase cost (chi phí) was mistaken for an added fee or missed as an equivalent noun heading.");
            MoneyChangeOpenTests.CheckPractice(c,clear);
            foreach(string phrase in new[]{"phụ phí","lệ phí","phí giao hàng","phí vận chuyển","phí dịch vụ","phí xử lý"})
            {
                var facts=clear.Facts!.ToArray();facts[0]=facts[0] with {Text=$"Mỗi {{v1}} có giá {{f0}} {{v3}} và có thêm {phrase}."};
                var extra=Validate(c,clear with {Facts=facts});
                Check(!extra.IsValid&&!extra.NeedsReview,"An explicit extra fee must still be rejected: "+phrase);
            }
            var serviceFacts=clear.Facts!.ToArray();serviceFacts[2]=serviceFacts[2] with {Text="{v0} đưa cho người bán {f2} {v3} và trả phí dịch vụ."};
            var service=Validate(c,clear with {Facts=serviceFacts});
            Check(!service.IsValid&&!service.NeedsReview,"Cash tender plus an explicit service fee must remain rejected.");
            var extraLeads=clear.SolutionLeads!.ToArray();extraLeads[0]=extraLeads[0] with {Text="Tổng chi phí phát sinh cho {v2}"};
            var addedCost=Validate(c,clear with {SolutionLeads=extraLeads});
            Check(!addedCost.IsValid&&!addedCost.NeedsReview,"An explicit additional-cost heading must not be treated as the purchase-cost noun.");
            foreach(string prefix in new[]{"Không phải ","Nếu ","Chỉ khi "})
            {
                var leads=clear.SolutionLeads!.ToArray();leads[0]=leads[0] with {Text=prefix+leads[0].Text};
                Check(!Validate(c,clear with {SolutionLeads=leads}).IsValid,"Cost heading proof discarded a qualifier.");
            }
        }
    }
    private static void CheckRetailAndReturnedChangeVocabulary()
    {
        var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.MultiStep,MoneyChangePilot.Variant,CurriculumTier.ThreeStars,
            AppLanguage.English,new(2713),MoneyChangePilot.Prefix+"notebooks") with {OpenProse=true};
        var reference=ReasoningStoryCatalogue.Draft(c);
        Check(Validate(c,reference with {Question="How much change does the seller return to {v0} in {v3}?"}).NeedsReview,
            "Returning cash change is not a product-return event; unproved wording must wait for review.");
        var retail=reference.Facts!.ToArray();retail[0]=retail[0] with {Text="Each {v1} offered for sale is priced at {f0} {v3}."};
        Check(Validate(c,reference with {Facts=retail}).NeedsReview,"Offered for sale does not establish a discount; unproved retail wording must wait for review.");
        var returned=reference.Facts!.ToArray();returned[1]=returned[1] with {Text="{v0} returns {f1} {v2}."};
        var wrong=Validate(c,reference with {Facts=returned});
        Check(!wrong.IsValid&&!wrong.NeedsReview,"Returning the counted items must remain a known purchase-direction contradiction.");
    }
    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static void Check(bool value,string message)=>TestAssert.That(value,message);
}
