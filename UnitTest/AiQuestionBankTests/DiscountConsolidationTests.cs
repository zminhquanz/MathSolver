using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;

internal static class DiscountConsolidationTests
{
    internal static Task RunAsync()
    {
        // Reproduces the manually clear backpack attempt from native-final/attempts.json.
        // The proof must bind every original-price, rate, product, money and target role.
        foreach(string profile in DiscountOpenPilot.Profiles(AppLanguage.English))
        foreach(var tier in Enum.GetValues<CurriculumTier>())
        {
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.MultiStep,DiscountOpenPilot.Variant,
                tier,AppLanguage.English,new(913),profile) with {OpenProse=true};
            var reference=ReasoningStoryCatalogue.Draft(c);
            var historical=reference with
            {
                Facts=[new("fact_0","The initial cost for {v0} is recorded as {f0} {v1}."),
                    new("fact_1","A reduction of {f1} applies to the starting price of {v0}.")],
                Question="How much money must be given for {v0} after applying the reduction in {v1}?",
                SolutionLeads=[new("step_0","The value of the price reduction on {v0}"),
                    new("step_1","The final payment due for {v0}")]
            };
            Check(Validate(c,historical).IsValid,"Historical clear starting-price/final-payment discount must verify: "+Validate(c,historical));
            DiscountOpenTests.CheckPractice(c,historical);

            foreach(string price in new[]{"The original cost for {v0} is {f0} {v1}.",
                "The starting price of {v0} is {f0} {v1}."})
                Check(Validate(c,historical with {Facts=[new("fact_0",price),historical.Facts![1]]}).IsValid,
                    "Role-bound original/starting price synonym must verify: "+price);
            foreach(string question in new[]{"What is the final payment due for {v0} after the discount in {v1}?",
                "What is the final amount due for {v0} following the price reduction in {v1}?"})
                Check(Validate(c,historical with {Question=question}).IsValid,"Final payment must retain product, money and after-discount roles: "+question);

            foreach(string discount in new[]{
                "A reduction of {f1} does not apply to the starting price of {v0}.",
                "A reduction of {f1} applies to the final price of {v0}.",
                "A reduction of {f1} applies to the remaining price of {v0}.",
                "A reduction of {f1} applies to the starting price of {v0} twice.",
                "A reduction of {f1} applies to the starting price of {v0} with tax."})
                Check(Validate(c,historical with {Facts=[historical.Facts![0],new("fact_1",discount)]}).ErrorCode=="ChangedRelationOrTarget",
                    "Wrong base, negation or extra event must reject for retry: "+discount);
            foreach(string price in new[]{"The final payment due for {v0} is recorded as {f0} {v1}.",
                "The final cost for {v0} is recorded as {f0} {v1}."})
                Check(Validate(c,historical with {Facts=[new("fact_0",price),historical.Facts![1]]}).ErrorCode=="ChangedRelationOrTarget",
                    "A final payment cannot occupy the original-price role: "+price);
            foreach(string question in new[]{"What is the starting price of {v0} in {v1}?",
                "What is the initial cost for {v0} in {v1}?",
                "What is the value of the price reduction on {v0} in {v1}?"})
                Check(Validate(c,historical with {Question=question}).ErrorCode=="ChangedRelationOrTarget",
                    "Original-price and reduction-amount targets must reject for retry: "+question);

            Check(Validate(c,historical with {Facts=[historical.Facts![0],new("fact_1",
                "If a reduction of {f1} applies to the starting price of {v0}.")]}).NeedsReview,
                "An unproved conditional must review without retry.");
            Check(Validate(c,historical with {Facts=[new("fact_0",
                "The initial cost for {v0} is recorded as {f0} dollars {v1}."),historical.Facts![1]]}).ErrorCode=="ChangedUnits",
                "Literal money units must still reject for retry.");
            Check(Validate(c,historical with {Facts=[historical.Facts![0] with {Text=historical.Facts[0].Text.Replace("{f0}","{f1}")},historical.Facts[1]]}).ErrorCode=="InvalidPlaceholders",
                "Money/rate slots must stay in their original fact.");
            Check(Validate(c,historical with {Question="How much money must be given for {v1} after applying the reduction in {v0}?"}).NeedsReview,
                "Product and money words alone cannot prove reversed roles.");
            Check(!Validate(c,historical with {SolutionLeads=[historical.SolutionLeads![1] with {Role="step_0"},historical.SolutionLeads[0] with {Role="step_1"}]}).IsValid,
                "Final-payment and discount-amount headings cannot swap roles.");
            Check(Validate(c,historical with {SolutionLeads=[new("step_0","Amount subtracted from {v0}"),historical.SolutionLeads![1]]}).NeedsReview,
                "Subtracting from the product fails to name a monetary discount.");
            Check(Validate(c,historical with {Facts=[new("fact_0","A note records the initial cost for {v0} as {f0} {v1}."),historical.Facts![1]]}).NeedsReview,
                "Unproved original-price wording must remain reviewable.");
        }

        foreach(string profile in DiscountOpenPilot.Profiles(AppLanguage.Vietnamese))
        {
            var c=ReasoningStoryCatalogue.Create(BankQuestionFamily.MultiStep,DiscountOpenPilot.Variant,
                CurriculumTier.ThreeStars,AppLanguage.Vietnamese,new(913),profile) with {OpenProse=true};
            var d=ReasoningStoryCatalogue.Draft(c);
            Check(Validate(c,d with {SolutionLeads=[new("step_0","Mức chiết khấu áp dụng cho {v0}"),d.SolutionLeads![1]]}).NeedsReview,
                "Mức chiết khấu is ambiguous between rate and money and must stay under review.");
        }
        Console.WriteLine("PASS discount consolidation: historical proof, role/base/target/negation/unit guards, conservative headings and review.");
        return Task.CompletedTask;
    }

    private static BasicDraftValidation Validate(BasicQuestionContract c,BasicQuestionDraft d)=>BasicQuestionValidator.Validate(QuestionBankStore.SerializeDraft(d),c);
    private static void Check(bool value,string message)=>TestAssert.That(value,message);
}
