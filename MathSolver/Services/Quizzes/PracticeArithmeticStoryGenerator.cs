using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using MathSolver.Models;
using MathSolver.Services.Core;
using MathSolver.Services.QuestionBank;

namespace MathSolver.Services;

public sealed record PracticeCountContext(string Id, QuestionKnowledgeGroup Group, string Actor,
    string Unit, string GroupUnit, int Capacity, int MaximumPerGroup, string TemplatePrefix = "count",
    bool AllowsMentalLargeNumbers = false);
public sealed record PracticeMoneyItem(string Id, string Name, string Buyer, string Unit,
    int MinimumPrice, int MaximumPrice, int PriceStep, int MaximumCount, int MinimumStars,
    int MaximumCountFourStars = 0, string PluralName = "", int MaximumPurchaseTotal = 0, bool IsInstitutional = false);
public sealed record PracticeArithmeticTemplate(string Id, string Problem, string Lead, string FirstLead = "");

/// <summary>Reviewed JSON prose and bounded C# facts for practice/PDF only; never an AI bank contract.</summary>
public sealed class PracticeArithmeticStoryGenerator(Random? random = null)
{
    private readonly Random _random = random ?? Random.Shared;
    public static bool Supports(QuestionKnowledgeGroup group) => group is QuestionKnowledgeGroup.Objects
        or QuestionKnowledgeGroup.Money or QuestionKnowledgeGroup.Packaging or QuestionKnowledgeGroup.Production or QuestionKnowledgeGroup.Data;

    public ArithmeticQuizQuestion Generate(ArithmeticOperation operation, CurriculumTier tier, AppLanguage language,
        QuestionKnowledgeGroup group, ArithmeticQuizMode mode)
    {
        if (!Supports(group) || !Enum.IsDefined(operation) || !Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(group));
        if (tier is not (CurriculumTier.FourStars or CurriculumTier.FiveStars))
            throw new ArgumentOutOfRangeException(nameof(tier), "Expanded stories start at four stars.");
        string culture = QuizContentCatalog.Culture(language);
        var templates = QuizContentCatalog.LoadList<PracticeArithmeticTemplate>("PracticeArithmeticTemplates", culture);
        var values = new Dictionary<string, string>();
        var purchases = new List<PracticePurchase>();
        var steps = new List<PracticeSolutionStep>();
        string contextId, templateId, unit, actor;
        int cap, a, b, answerStep = 1;
        PracticeArithmeticStyle? style = null;
        if (group == QuestionKnowledgeGroup.Money)
        {
            cap = PracticeArithmeticDifficulty.MoneyMaximum(tier, language);
            var items = QuizContentCatalog.LoadList<PracticeMoneyItem>("PracticeMoneyItems", culture)
                .Where(i => i.MinimumStars <= (int)tier && i.MinimumPrice * 2L <= cap).ToArray();
            if (items.Length == 0) throw new InvalidDataException("No affordable practice money item.");
            var item = items[_random.Next(items.Length)];
            Validate(item);
            answerStep = item.PriceStep;
            unit = item.Unit; actor = item.Buyer; contextId = item.Id;
            int countCap = (int)tier == 4 && item.MaximumCountFourStars > 0 ? item.MaximumCountFourStars : item.MaximumCount;
            int purchaseCap = Math.Min(cap, item.MaximumPurchaseTotal == 0 ? cap : item.MaximumPurchaseTotal);
            countCap = Math.Min(countCap, purchaseCap / item.MinimumPrice);
            countCap = Math.Min(countCap, cap / item.MinimumPrice / ((int)tier == 5 && operation == ArithmeticOperation.Subtract ? 2 : 1));
            int count = operation is ArithmeticOperation.Multiply or ArithmeticOperation.Divide
                ? _random.Next(2, countCap + 1) : (int)tier == 5 && (operation != ArithmeticOperation.Subtract || item.Id == "school-laptop")
                    ? _random.Next(1, countCap + 1) : 1;
            int highPrice = Math.Min(purchaseCap / count, Math.Min(item.MaximumPrice, operation == ArithmeticOperation.Subtract
                ? (cap - item.PriceStep) / (((int)tier == 5 ? 2 : 1) * count) : cap / (operation == ArithmeticOperation.Add ? 2 * count : Math.Max(2, count))));
            int lowTick = (item.MinimumPrice + item.PriceStep - 1) / item.PriceStep;
            int price = _random.Next(lowTick, highPrice / item.PriceStep + 1) * item.PriceStep;
            purchases.Add(new(item.Id, item.Name, price, count, item.MinimumPrice, item.MaximumPrice));
            values["item"] = item.Name;
            values["items"] = NameFor(count);
            values["count"] = N(count); values["price"] = N(price); values["cost"] = N(price * count);
            if (operation == ArithmeticOperation.Add)
            {
                // Two identical purchases on separate days, not a fictitious million-dong pen.
                var eligiblePrices = Enumerable.Range(lowTick, highPrice / item.PriceStep - lowTick + 1)
                    .Select(tick => tick * item.PriceStep).Where(p => PracticeArithmeticDifficulty.HasAppropriateRegrouping(price * count, p * count, operation, tier)).ToArray();
                int secondPrice = eligiblePrices[_random.Next(eligiblePrices.Length)];
                purchases.Add(new(item.Id, item.Name, secondPrice, count, item.MinimumPrice, item.MaximumPrice));
                a = price * count; b = secondPrice * count; templateId = "money-add";
                values["second_price"] = N(secondPrice);
                if (count > 1)
                {
                    string batchLead = templates.Single(t => t.Id == "money-add").FirstLead;
                    steps.Add(new(Render(batchLead, new Dictionary<string, string> { ["unit"] = unit, ["batch"] = "1" }), new(price, ArithmeticOperation.Multiply, count), unit));
                    steps.Add(new(Render(batchLead, new Dictionary<string, string> { ["unit"] = unit, ["batch"] = "2" }), new(secondPrice, ArithmeticOperation.Multiply, count), unit));
                }
            }
            else if (operation == ArithmeticOperation.Subtract)
            {
                int cost = price * count;
                if ((int)tier == 5)
                {
                    purchases.Add(new(item.Id, item.Name, price, count, item.MinimumPrice, item.MaximumPrice));
                    cost += price * count;
                }
                // Cash stays close to the purchase; buying a pen never requires a million-dong budget.
                int extraTicks = Math.Max(1, Math.Min((cap - cost) / item.PriceStep, Math.Max(1, cost / item.PriceStep / 2)));
                var cashTicks = Enumerable.Range(1, extraTicks).Where(tick =>
                    PracticeArithmeticDifficulty.HasAppropriateRegrouping(cost + tick * item.PriceStep, cost, operation, tier)).ToArray();
                a = cost + cashTicks[_random.Next(cashTicks.Length)] * item.PriceStep; b = cost;
                templateId = purchases.Count == 2 ? count > 1 ? "money-subtract-batches" : "money-subtract-two" : "money-subtract";
                if (item.IsInstitutional) templateId = templateId.Replace("money-", "budget-", StringComparison.Ordinal);
                if (count == 1 && purchases.Count == 2) values["items"] = NameFor(2);
            }
            else if (operation == ArithmeticOperation.Multiply)
            { a = price; b = count; templateId = "money-multiply"; }
            else
            { a = price * count; b = count; templateId = "money-divide"; }
            if (templateId.EndsWith("-subtract-batches", StringComparison.Ordinal))
            {
                var lead = templates.Single(t => t.Id == templateId).FirstLead;
                steps.Add(new(Render(lead, new Dictionary<string, string> { ["unit"] = unit }), new(price, ArithmeticOperation.Multiply, count), unit));
            }

            string NameFor(int quantity) => quantity == 1 || item.PluralName.Length == 0 ? item.Name : item.PluralName;
        }
        else
        {
            var contexts = QuizContentCatalog.LoadList<PracticeCountContext>("PracticeArithmeticContexts", culture)
                .Where(c => c.Group == group).ToArray();
            if (contexts.Length == 0) throw new InvalidDataException("Missing practice count context.");
            var context = contexts[_random.Next(contexts.Length)];
            if (context.Capacity < 2 || context.MaximumPerGroup < 1) throw new InvalidDataException("Invalid practice context bounds.");
            style = context.AllowsMentalLargeNumbers && operation is ArithmeticOperation.Add or ArithmeticOperation.Subtract && _random.Next(3) == 0
                ? PracticeArithmeticStyle.Mental : PracticeArithmeticStyle.Written;
            cap = Math.Min(context.Capacity, PracticeArithmeticDifficulty.Maximum(tier, style));
            unit = context.Unit; actor = context.Actor; contextId = context.Id;
            values["group"] = context.GroupUnit;
            int factor = (int)tier >= 4 ? 99 : (int)tier <= 1 ? 5 : 9;
            if (style == PracticeArithmeticStyle.Mental)
            {
                var mental = PracticeArithmeticDifficulty.CreateMentalExpression(_random, tier, operation, maximum: cap);
                a = (int)mental.LeftOperand; b = (int)mental.RightOperand;
            }
            else if (operation == ArithmeticOperation.Multiply)
            {
                b = _random.Next(2, Math.Min(factor, cap) + 1);
                a = PracticeArithmeticDifficulty.NextPrimary(_random, tier, Math.Min(context.MaximumPerGroup, cap / b));
            }
            else if (operation == ArithmeticOperation.Divide)
            {
                b = _random.Next(2, Math.Min(factor, cap) + 1);
                int each = PracticeArithmeticDifficulty.NextPrimary(_random, tier, Math.Min(context.MaximumPerGroup, cap / b));
                a = each * b;
            }
            else
            {
                a = PracticeArithmeticDifficulty.NextPrimary(_random, tier, cap - 1);
                b = PracticeArithmeticDifficulty.NextRegroupingPartner(_random, tier, a, operation, operation == ArithmeticOperation.Add ? cap - a : a);
                if (b == 0) { a = Math.Max(1, a / 10 * 10); b = 1; }
            }
            templateId = context.TemplatePrefix + "-" + operation.ToString().ToLowerInvariant();
            if (context.TemplatePrefix == "count" && (int)tier == 5 && operation is ArithmeticOperation.Add or ArithmeticOperation.Subtract && _random.Next(2) == 0)
            {
                if (operation == ArithmeticOperation.Add)
                {
                    int removed = _random.Next(1, Math.Min(a, 99) + 1);
                    values["initial"] = N(a + removed); values["change"] = N(removed);
                    // Limit the initial quantity as well as the result to the context capacity.
                    if (a + removed <= cap) templateId += "-two";
                }
                else
                {
                    int received = _random.Next(1, Math.Min(a, 99) + 1);
                    values["initial"] = N(a - received); values["change"] = N(received);
                    templateId += "-two";
                }
            }
        }
        var template = templates.Single(t => t.Id == templateId);
        values["actor"] = actor; values["unit"] = unit; values["a"] = N(a); values["b"] = N(b);
        if (templateId is "money-subtract-two" or "budget-subtract-two")
            steps.Add(new(Render(template.FirstLead, values), new(purchases[0].Total, ArithmeticOperation.Add, purchases[1].Total), unit));
        else if (templateId.EndsWith("-subtract-batches", StringComparison.Ordinal))
            steps.Add(new(Render(templates.Single(t => t.Id == "money-add").Lead, values), new(purchases[0].Total, ArithmeticOperation.Multiply, 2), unit));
        else if (templateId.EndsWith("-two", StringComparison.Ordinal))
            steps.Add(new(Render(template.FirstLead, values), new(Parse("initial"), operation == ArithmeticOperation.Add
                ? ArithmeticOperation.Subtract : ArithmeticOperation.Add, Parse("change")), unit));
        var expression = new IntegerArithmeticExpression(a, operation, b);
        steps.Add(new(Render(template.Lead, values), expression, unit));
        var answer = steps[^1].Answer;
        var details = new PracticeArithmeticDetails(contextId, cap, purchases.AsReadOnly(), steps.AsReadOnly());
        var word = new MathWordProblem(Render(template.Problem, values), Render(template.Lead, values), unit, actor,
            group == QuestionKnowledgeGroup.Money ? WordProblemQuantity.Money : WordProblemQuantity.Count, PracticeDetails: details);
        var choices = new List<BigInteger>();
        BigInteger? presented = null; bool? assertion = null;
        if (mode == ArithmeticQuizMode.MultipleChoice)
        {
            choices.Add(answer);
            for (int offset = 1; choices.Count < 4; offset++) choices.Add(answer + offset * answerStep);
            for (int i = choices.Count - 1; i > 0; i--) { int j = _random.Next(i + 1); (choices[i], choices[j]) = (choices[j], choices[i]); }
        }
        if (mode == ArithmeticQuizMode.TrueFalse)
        { assertion = _random.Next(2) == 0; presented = assertion.Value ? answer : answer + answerStep; }
        var question = new ArithmeticQuizQuestion(expression, mode, answer, presented, assertion, choices.AsReadOnly(), word, ArithmeticStyle: style);
        if (!new ArithmeticQuizValidator(new BasicArithmeticEngine()).Validate(question).IsValid
            || answer < 0 || answer > cap || purchases.Sum(p => (long)p.Total) > cap
            || steps.Any(s => s.Answer < 0 || s.Answer > cap))
            throw new InvalidOperationException("Invalid practice arithmetic facts.");
        return question;

        string N(int number) => number.ToString("N0", CultureInfo.GetCultureInfo(culture));
        int Parse(string key) => int.Parse(values[key], NumberStyles.Number, CultureInfo.GetCultureInfo(culture));
    }

    public static void Validate(PracticeMoneyItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Name)
            || string.IsNullOrWhiteSpace(item.Buyer) || string.IsNullOrWhiteSpace(item.Unit) || item.PluralName is null || item.MinimumPrice < 1
            || item.MaximumPrice < item.MinimumPrice || item.PriceStep < 1 || item.MaximumCount is < 2 or > 99
            || item.MaximumCountFourStars < 0 || item.MaximumCountFourStars > item.MaximumCount
            || item.MinimumStars is < 1 or > 5 || item.MinimumPrice % item.PriceStep != 0 || item.MaximumPrice % item.PriceStep != 0
            || item.MaximumPurchaseTotal < 0 || item.MaximumPurchaseTotal > 999_999_999
            || item.MaximumPurchaseTotal > 0 && item.MaximumPurchaseTotal < 2L * item.MinimumPrice)
            throw new InvalidDataException("Invalid practice item price bounds.");
    }

    private static string Render(string text, IReadOnlyDictionary<string, string> values) =>
        Regex.Replace(text, @"\{([a-z_]+)\}", m => values.TryGetValue(m.Groups[1].Value, out var value)
            ? value : throw new InvalidDataException($"Unknown practice variable: {m.Value}"), RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
}
