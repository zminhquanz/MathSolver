using MathSolver.Models;
using System.Text.Json;

namespace MathSolver.Services.QuestionBank;

public static class BasicQuestionPrompt
{
    public static string Build(BasicQuestionContract contract, string? correction = null)
    {
        if (!contract.IsValid) throw new ArgumentException("Invalid C# contract.", nameof(contract));
        if (contract.Version == AdditionQuestionCatalogue.Version) return BuildAddition(contract, correction);
        if (contract.Version == ArithmeticQuestionCatalogue.Version) return BuildArithmetic(contract, correction);
        if (contract.IsTemplate) return BuildTemplate(contract, correction);
        bool vi = contract.Language == AppLanguage.Vietnamese;
        string rule = contract.Operation switch
        {
            ArithmeticOperation.Add => vi
                ? "given_a: chủ thể có Left sách. given_b: chính chủ thể nhận/mua thêm Right sách. question: hỏi tổng số sách chủ thể có sau đó."
                : "given_a: the subject has Left books. given_b: the same subject receives/buys Right more books. question: ask how many books the subject has in total afterwards.",
            ArithmeticOperation.Subtract => vi
                ? "given_a: chủ thể có Left sách. given_b: chính chủ thể cho đi/bán Right sách. question: hỏi số sách chủ thể còn lại."
                : "given_a: the subject has Left books. given_b: the same subject gives away/sells Right books. question: ask how many books the subject has left.",
            ArithmeticOperation.Multiply => vi
                ? "given_a: mỗi thùng chứa Left sách. given_b: chủ thể có Right thùng như nhau. question: hỏi tổng số sách trong các thùng của chủ thể."
                : "given_a: each box contains Left books. given_b: the subject has Right identical boxes. question: ask for the total books in the subject's boxes.",
            _ => vi
                ? "given_a: chủ thể có Left sách. given_b: chủ thể chia đều/xếp đều sách vào Right thùng. question: hỏi số sách trong mỗi thùng."
                : "given_a: the subject has Left books. given_b: the subject distributes the books equally into Right boxes. question: ask how many books are in each box."
        };
        string instruction = vi
            ? "Viết đề toán ngắn bằng tiếng Việt. Viết lại ví dụ bên dưới bằng câu văn tự nhiên; được thêm bối cảnh ngắn không có số. Giữ nguyên chủ thể, chữ số, đơn vị và quan hệ của ví dụ. Không giải bài. Mỗi given chỉ có một số nguyên viết bằng chữ số; question không có số."
            : "Write a natural English arithmetic word problem from the C# facts below. A brief nonnumeric context is allowed. Do not solve it, reveal an answer, or change quantities, units, subject or relationships. Each given has exactly one integer written as digits without thousands separators. The question has no numbers. Name the subject in both givens (except the per-box multiplication given) and the question. Use books and box/boxes as units.";
        string a = contract.Operation == ArithmeticOperation.Multiply
            ? vi ? $"Mỗi thùng chứa {contract.Left} quyển sách." : $"Each box contains {contract.Left} books."
            : vi ? $"{contract.Subject} có {contract.Left} quyển sách." : $"{contract.Subject} has {contract.Left} books.";
        string b = contract.Operation switch
        {
            ArithmeticOperation.Add => vi ? $"{contract.Subject} nhận thêm {contract.Right} quyển sách." : $"{contract.Subject} receives {contract.Right} more books.",
            ArithmeticOperation.Subtract => vi ? $"{contract.Subject} cho đi {contract.Right} quyển sách." : $"{contract.Subject} gives away {contract.Right} books.",
            ArithmeticOperation.Multiply => vi ? $"{contract.Subject} có {contract.Right} thùng như nhau." : $"{contract.Subject} has {contract.Right} identical boxes.",
            _ => vi ? $"{contract.Subject} xếp đều sách vào {contract.Right} thùng." : $"{contract.Subject} distributes the books equally into {contract.Right} boxes."
        };
        string q = contract.Operation switch
        {
            ArithmeticOperation.Subtract => vi ? $"Hỏi {contract.Subject} còn lại bao nhiêu quyển sách?" : $"How many books does {contract.Subject} have left?",
            ArithmeticOperation.Divide => vi ? $"Hỏi mỗi thùng của {contract.Subject} chứa bao nhiêu quyển sách?" : $"How many books are in each box of {contract.Subject}?",
            _ => vi ? $"Hỏi {contract.Subject} có tất cả bao nhiêu quyển sách?" : $"How many books does {contract.Subject} have in total?"
        };
        // The visible problem is assembled from these validated clauses. There is no
        // unvalidated free-text problem_text alongside a correct-but-irrelevant facts array.
        return instruction + "\n" + LanguageRule(vi) + "\n" + rule + "\nFacts: " + JsonSerializer.Serialize(new
        {
            contract.Left, contract.Right, contract.Subject, contract.Unit,
            Operation = contract.Operation.ToString(), Stars = (int)contract.Tier
        }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\nCorrect example for these facts: " + QuestionBankStore.SerializeDraft(new(a, b, q))
        + (vi ? "\nViết lại mẫu trên thành câu văn ngắn tự nhiên, tối đa 20 từ mỗi câu. Giữ nguyên chữ số; không viết số bằng chữ. Không bình luận hoặc giải thích yêu cầu."
            : "\nReword this example into short natural sentences, at most 20 words each. Preserve digit quantities, never spell out numbers. Do not discuss the instructions.")
        + "\nReturn only a JSON object with exactly these three string fields: given_a, given_b, question."
        + (string.IsNullOrEmpty(correction) ? "" : "\nPrevious output was rejected: " + correction + ". Rewrite the complete JSON.");
    }

    private static string BuildTemplate(BasicQuestionContract c, string? correction)
    {
        bool vi = c.Language == AppLanguage.Vietnamese;
        string instruction = vi
            ? "Viết MẪU đề toán có lời văn và câu lời giải bằng tiếng Việt. C# sẽ thay tên, số và tính đáp án khi học sinh lấy đề. Không sinh số, phép tính hoặc đáp án. Chọn unit_id từ danh mục dưới đây; bối cảnh phải phù hợp đồ vật đó. Dùng đúng các biến có dấu ngoặc nhọn của ví dụ, không thay biến bằng tên hoặc đơn vị cụ thể. Có thể thay câu chữ, thêm bối cảnh sinh động ngắn; giữ vai trò từng dữ kiện và đại lượng cần tìm. Không thêm dữ kiện, phủ định hay điều kiện khác. solution_lead chỉ là câu dẫn lời giải, không có số hay phép tính."
            : "Write an English word-problem TEMPLATE and a prose solution lead. C# will supply fresh names and integer quantities and compute the answer during practice. Never generate numbers, formulas or answers. Choose unit_id from the catalogue; match your context to that object. Keep the exact brace placeholders used in the example, never replace them with actual names or unit words. Vary the wording and brief story context without changing the roles of facts or the requested quantity. No additional facts, negation or conditions. solution_lead is a prose lead only.";
        string role = c.Structure switch
        {
            BasicQuestionStructure.Increase => "a = initial stock of name; b = increase to the same owner; ask final stock (a+b).",
            BasicQuestionStructure.Combine => "a = stock of name; b = separate stock of other; ask their combined stock (a+b).",
            BasicQuestionStructure.RecoverInitial => "a = stock left after giving away b; ask name's original stock (a+b).",
            BasicQuestionStructure.Remaining => "a = initial stock of name; b = stock removed from name; ask stock remaining (a-b).",
            BasicQuestionStructure.Difference => "a = stock of name; b = stock of other (a>=b); ask how many more name has (a-b).",
            BasicQuestionStructure.MissingPart => "a = amount name needs in total; b = amount already available; ask amount still needed (a-b).",
            BasicQuestionStructure.EqualGroups => "a = objects in EACH identical group; b = number of groups owned by name; ask total objects (a*b).",
            BasicQuestionStructure.TimesAsMany => "a = objects owned by other; name has b TIMES as many; ask objects owned by name (a*b).",
            BasicQuestionStructure.EqualShare => "a = total objects of name; b = number of groups they are shared equally into; ask objects per group (a/b).",
            BasicQuestionStructure.CountGroups => "a = total objects of name; b = objects packed into EACH group; ask NUMBER OF GROUPS (a/b), not objects per group.",
            _ => "a = objects owned by other; OTHER has b times as many objects as NAME; ask objects owned by name (a/b)."
        };
        return instruction + "\n" + LanguageRule(vi) + "\nC# mathematical roles (do not output the formulas): " + role
            + (vi ? "\ngiven_a kết thúc bằng dấu phẩy; given_b bắt đầu bằng chữ thường, kết thúc bằng dấu chấm. Giữ nguyên biến tên riêng."
                : "\nEnd given_a with a comma; begin given_b lowercase and end it with a period. Keep proper-name slots intact.")
            + (vi ? "\n{name} và {other} đã là tên/chủ thể đầy đủ, có thể gồm vai gia đình hoặc cửa hàng. Không thêm bạn/cô giáo/cửa hàng trước các biến này. Nếu thêm lời dẫn, dùng bối cảnh chung như Trong buổi chuẩn bị quà."
                : "\n{name}/{other} already contain the complete actor and role, possibly a business. Do not wrap them in extra names, titles or shop descriptions.")
            + (vi ? "\nĐa dạng lời dẫn: có thể đặt trong việc chuẩn bị quà, hội chợ, quyên góp, chuẩn bị đồ dùng hoặc kiểm kê. Chỉ chọn bối cảnh phù hợp unit_id và giữ nguyên các quan hệ toán."
                : "\nVary the story context: gift preparation, a fair, a donation drive, gathering supplies or checking stock. Choose a context that fits unit_id, preserving all mathematical relations.")
            + "\n{name} and {other} include any family title or role; do not add gendered pronouns or extra titles around them."
            + "\nNames are owners/people or businesses, never objects inside a container. Say a container belongs to {name}, never that it contains {name}."
            + "\n{unit} = counted objects; {group} = containers in plural; {group_one} = a single container. Use these placeholders even in solution_lead."
            + "\nUnit catalogue: " + JsonSerializer.Serialize(QuestionUnits.All.Select(u => new {
                unit_id = u.Id, unit = u.Item(c.Language), group = u.Group(c.Language) }), JsonOptions)
            + "\nTemplate metadata: " + JsonSerializer.Serialize(new { Structure = c.Structure.ToString(), Stars = (int)c.Tier, Language = c.Language.ToString() })
            + "\nCorrect role example: " + QuestionBankStore.SerializeDraft(BasicQuestionTemplates.Example(c))
            + "\nReturn ONLY JSON with exactly five string fields: given_a, given_b, question, solution_lead, unit_id."
            + (string.IsNullOrEmpty(correction) ? "" : "\nRejected previous template: " + correction + ". " + Correction(correction) + " Rewrite the complete JSON.");
    }

    private static string BuildArithmetic(BasicQuestionContract c, string? correction)
    {
        var scale = ArithmeticQuestionCatalogue.Scale(c.SceneId, c.Operation, c.Tier)!;
        var unit = QuestionUnits.Find(c)!;
        bool vi = c.Language == AppLanguage.Vietnamese;
        string role = c.Structure switch {
            BasicQuestionStructure.Remaining => vi ? "{a}: lượng có ban đầu; {b}: lượng lấy ra của cùng chủ thể; hỏi còn lại"
                : "{a}: initial stock; {b}: removed from the same owner; ask what remains",
            BasicQuestionStructure.Difference => vi ? "{a}: lượng của {name}; {b}: lượng riêng của {other}; hỏi {name} nhiều hơn bao nhiêu"
                : "{a}: stock of {name}; {b}: separate stock of {other}; ask how many more {name} has",
            BasicQuestionStructure.MissingPart => vi ? "{a}: tổng lượng cần; {b}: lượng đã có; hỏi lượng còn thiếu"
                : "{a}: total needed; {b}: already available; ask the additional amount needed",
            BasicQuestionStructure.EqualGroups => vi ? "{a}: số vật trong MỖI nhóm; {b}: số nhóm như nhau; hỏi tổng số vật"
                : "{a}: objects in EACH group; {b}: number of identical groups; ask total objects",
            BasicQuestionStructure.TimesAsMany => vi ? "{other} có {a}; {name} có gấp {b} lần {other}; hỏi {name}"
                : "{other} has {a}; {name} has {b} times as many as {other}; ask for {name}",
            BasicQuestionStructure.EqualShare => vi ? "{a}: tổng số vật; chia ĐỀU vào {b} nhóm; hỏi số vật MỖI nhóm"
                : "{a}: total objects; share EQUALLY into {b} groups; ask objects in EACH group",
            BasicQuestionStructure.CountGroups => vi ? "{a}: tổng số vật; MỖI nhóm có {b} vật; hỏi SỐ NHÓM, đơn vị đáp số là {group}"
                : "{a}: total objects; EACH group holds {b} objects; ask NUMBER OF GROUPS, answer unit is {group}",
            _ => vi ? "{other} có {a}; {other} có gấp {b} lần {name}; hỏi {name}, không đảo chủ thể"
                : "{other} has {a}; OTHER has {b} times as many as NAME; ask for {name}, do not reverse the actors" };
        string actor = scale.ActorPattern(c.Language).Replace("{person}", vi ? "…" : "a person");
        string scope = c.SceneId is "harvest" or "crop-harvest" or "bakery" or "product-production" or "craft" or "notebook-production"
            ? vi ? "Hàng cùng loại đã thu hoạch/làm xong, nay đang có để bán hoặc chia nhóm."
                : "Finished goods or harvested produce now held as stock for sale or grouping."
            : c.SceneId is "green-planting" or "garden" ? vi ? "Cây cùng loại đang chờ trồng hoặc chuyển đi; không cộng cây đã trồng vào kho cây chờ trồng."
                : "Plants of one kind awaiting planting or transfer, not plants already planted."
            : vi ? "Đếm đồ vật cùng loại của các chủ thể hoặc các nhóm riêng biệt." : "Count objects of one kind belonging to distinct owners or groups.";
        string prompt = vi ? $$"""
            Viết lại mẫu toán một bước bằng tiếng Việt có dấu, tự nhiên. Chỉ trả JSON cùng 5 trường, không giải bài.
            Bối cảnh: {{c.TopicId}}/{{c.SceneId}}. {name}/{other} là chủ thể đầy đủ: {{actor}}. {{scope}}
            Vai trò: {{role}}.
            unit_id="{{unit.Id}}"; {unit}={{unit.Vietnamese}}; {group}={{c.GroupUnit}}; {group_one}={{unit.GroupFor(c, true)}}.
            Giữ đúng biến và vai trò; không thêm tên, danh xưng, đồ vật quanh biến, số cụ thể, dữ kiện, phủ định hoặc bước giải.
            given_a: một mệnh đề kết thúc dấu phẩy; given_b: nối tiếp đầu chữ thường, cuối dấu chấm.
            {a} chỉ trong given_a, {b} chỉ trong given_b, mỗi biến đúng một lần. question chỉ hỏi; solution_lead là câu dẫn đúng đại lượng, cuối dấu hai chấm.
            """ : $$"""
            Rewrite this one-step template in natural English. Return only the same five-field JSON, no calculation.
            Setting: {{c.TopicId}}/{{c.SceneId}}. {name}/{other} are complete actors: {{actor}}. {{scope}}
            Roles: {{role}}.
            unit_id="{{unit.Id}}"; {unit}={{unit.Plural}}; {group}={{c.GroupUnit}}; {group_one}={{unit.GroupFor(c, true)}}.
            Keep slots and roles; add no names, titles, nouns around slots, concrete numbers, facts, negations or solution steps.
            given_a: one clause ending with a comma; given_b: continuing lowercase clause ending with a period.
            {a} occurs once only in given_a; {b} once only in given_b. question asks only; solution_lead names the same target and ends with a colon.
            """;
        prompt += "\nJSON: " + QuestionBankStore.SerializeDraft(ArithmeticQuestionCatalogue.Example(c));
        if (!string.IsNullOrEmpty(correction)) prompt += "\n" + correction + ": " + Correction(correction) + " Rewrite the complete JSON.";
        return prompt;
    }

    private static string BuildAddition(BasicQuestionContract c, string? correction)
    {
        var scene = AdditionQuestionCatalogue.Find(c.SceneId)!;
        var scale = scene.Scale(c.Tier)!;
        var unit = QuestionUnits.Find(c)!;
        bool vi = c.Language == AppLanguage.Vietnamese;
        string relationship = c.Structure switch {
            BasicQuestionStructure.Increase => vi ? "lượng ban đầu và lượng thêm vào; hỏi tổng sau đó" : "initial amount plus increase; ask the final total",
            BasicQuestionStructure.RecoverInitial => vi ? "còn lại và đã lấy ra; hỏi lượng ban đầu" : "remaining amount plus an earlier removal; ask the original amount",
            BasicQuestionStructure.AddComparisonMore => vi ? "{name} nhiều hơn {other}; hỏi {name}" : "{name} has more than {other}; ask for {name}",
            BasicQuestionStructure.AddComparisonInverse => vi ? "{other} ít hơn {name}; hỏi {name}" : "{other} has fewer than {name}; ask for {name}",
            _ => vi ? "hai lượng riêng không trùng nhau; hỏi tổng" : "two separate nonoverlapping amounts; ask their total"
        };
        // C# has already selected one context, scale, relation and unit. Never send
        // the whole catalogue or preview numbers/names to the model.
        string actor = scale.ActorPattern(c.Language).Replace("{person}", vi ? "…" : "a person");
        string prompt = vi ? $$"""
            Viết lại mẫu toán tiểu học dưới đây bằng tiếng Việt có dấu, tự nhiên. Chỉ trả JSON cùng 5 trường, không giải bài.
            Bối cảnh: {{c.TopicId}}/{{c.SceneId}}; hoạt động: {{scene.VietnameseAction}}.
            Chủ thể đầy đủ {name}/{other}: {{actor}}. {{scale.VietnameseScope}}
            Quan hệ: {{relationship}}. unit_id="{{unit.Id}}"; {unit}={{unit.Vietnamese}}.
            Giữ biến của mẫu; không thêm tên, danh xưng, đồ vật quanh biến, số cụ thể, dữ kiện, phủ định hay quan hệ mới.
            given_a: một mệnh đề kết thúc dấu phẩy. given_b: mệnh đề nối tiếp đầu chữ thường, cuối dấu chấm.
            question: một câu hỏi, không lặp dữ kiện. solution_lead: câu dẫn phép tính, cuối dấu hai chấm, không đáp số.
            {a} chỉ trong given_a, {b} chỉ trong given_b, mỗi biến đúng một lần. Đổi câu chữ/động từ cùng hoạt động.
            """ : $$"""
            Rewrite this elementary word-problem template in natural English. Return only the same five-field JSON; do not solve it.
            Setting: {{c.TopicId}}/{{c.SceneId}}; activity: {{scene.EnglishAction}}.
            {name}/{other} each represent an entire {{actor}}. {{scale.EnglishScope}}
            Relation: {{relationship}}. unit_id="{{unit.Id}}"; {unit}={{unit.Plural}}.
            Preserve slots; add no names, titles, object nouns around slots, numerical values, facts, negations or relationships.
            given_a: one clause ending with a comma. given_b: a continuing lowercase clause ending with a period.
            question: one question, no repeated facts. solution_lead: one calculation lead ending with a colon, no answer.
            {a} occurs once in given_a, {b} once in given_b, neither elsewhere. Vary wording/verbs within this activity.
            """;
        if (scene.Kind is AdditionSceneKind.Periods or AdditionSceneKind.Parts)
        {
            var parts = scale.Parts(c.Language);
            prompt += $"\n{{part_a}}={parts.A}; {{part_b}}={parts.B}; " + scale.Span(c.Language) + ".";
        }
        prompt += "\nJSON: " + QuestionBankStore.SerializeDraft(AdditionQuestionCatalogue.Example(c));
        if (!string.IsNullOrEmpty(correction))
            prompt += "\n" + correction + ": " + Correction(correction) + " Rewrite the complete JSON.";
        return prompt;
    }

    private static string Correction(string code) => code switch
    {
        "WrongLanguage" => "Use only the requested prose language in both givens, the question and solution_lead. Remove foreign-language words; preserve brace placeholders and unit_id.",
        "InvalidText" => "Use clean prose without replacement glyphs, invisible characters, stray combining marks or symbols. Preserve brace placeholders.",
        "InvalidPlaceholders" => "Use {name}/{other} as the actors shown in the example, not a literal name/title. given_a uses {a}, given_b uses {b}, exactly once each; the question/solution contain neither number slot.",
        "ChangedRelationOrTarget" => "Preserve the actor who owns/gains/loses the objects and the exact target in the role example. The solution_lead must describe that same target.",
        "ChangedUnits" => "Choose a listed unit_id and use {unit}, {group}, {group_one} for their exact item/container roles.",
        "InvalidContext" => "People or businesses own containers; do not put the actor inside a container or treat an actor as a counted object.",
        "ChangedQuantities" => "No literal numeric or spelled-out quantities, formulas or answers. Keep only {a} and {b} in their respective givens.",
        "ExtraRelations" => "Use affirmative facts, no negation (including don't/doesn't), extra conditions or extra mathematical relationships.",
        _ => "Follow the five-field JSON schema and the role example."
    };

    private static string LanguageRule(bool vi) => vi
        ? "Cả hai dữ kiện, câu hỏi và lời dẫn lời giải chỉ dùng tiếng Việt có dấu; không xen từ ngoại ngữ, ký tự lạ hoặc ký tự vô hình. Giữ nguyên biến và mã unit_id."
        : "Both givens, the question and solution lead must use English only, without foreign-language words, stray symbols or invisible characters. Preserve placeholders and unit_id.";

    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
}
