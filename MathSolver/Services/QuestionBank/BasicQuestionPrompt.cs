using MathSolver.Models;
using System.Text.Json;

namespace MathSolver.Services.QuestionBank;

public static class BasicQuestionPrompt
{
    public static string Build(BasicQuestionContract contract, string? correction = null)
    {
        if (!contract.IsValid) throw new ArgumentException("Invalid C# contract.", nameof(contract));
        if (contract.Version == AdditionQuestionCatalogue.Version) return BuildAddition(contract, correction);
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

    private static string BuildAddition(BasicQuestionContract c, string? correction)
    {
        var scene = AdditionQuestionCatalogue.Find(c.SceneId)!;
        var scale = scene.Scale(c.Tier)!;
        bool vi = c.Language == AppLanguage.Vietnamese;
        string relationship = c.Structure switch
        {
            BasicQuestionStructure.Increase => vi ? "Lượng ban đầu và lượng thêm vào cùng nơi/chủ thể; hỏi lượng sau đó." : "An initial amount and an increase for the same actor/place; ask the final amount.",
            BasicQuestionStructure.RecoverInitial => vi ? "Lượng còn lại và lượng đã lấy ra trước đó; hỏi lượng ban đầu." : "An amount left and an amount previously removed; ask the original amount.",
            BasicQuestionStructure.AddComparisonMore => vi ? "Biết lượng của {other}; {name} nhiều hơn {other}; hỏi lượng của {name}." : "The amount/result of {other} is known; that of {name} is greater; ask the amount/result of {name}.",
            BasicQuestionStructure.AddComparisonInverse => vi ? "Biết lượng của {other}; {other} ít hơn {name}; hỏi lượng của {name}." : "The amount/result of {other} is known; that of {other} is fewer than that of {name}; ask the amount/result of {name}.",
            _ => scene.Kind switch
            {
                AdditionSceneKind.Periods => vi ? "Kết quả cùng hoạt động qua các thời kỳ riêng; hỏi tổng kết quả, không hỏi đồ vật còn giữ." : "Results of the same activity over separate periods; ask their total, not remaining possessions.",
                AdditionSceneKind.Parts => vi ? "Lượng trong các phần riêng không chứa nhau; hỏi tổng lượng của các phần." : "Amounts in disjoint spatial parts; ask their total.",
                _ => vi ? "Lượng riêng của {name} và {other}; hỏi tổng của cả hai, không chuyển đồ vật giữa hai chủ thể." : "Separate amounts of {name} and {other}; ask their combined amount, not a transfer between them."
            }
        };
        // One complete example defines the roles; no verbs from unrelated scenes,
        // numerical preview values or literal actor names are sent to the model.
        string prompt = vi ? $$"""
            Viết mẫu bài toán có lời văn bằng tiếng Việt cho học sinh tiểu học. Chỉ trả về JSON, không giải bài.
            Bối cảnh: {{c.TopicId}}/{{c.SceneId}}. Hoạt động: {{scene.VietnameseAction}}.
            Quan hệ: {{relationship}}
            Nhiệm vụ từng trường:
            - given_a: một mệnh đề nêu dữ kiện đầu, dùng {a} và chủ thể như ví dụ; kết thúc bằng dấu phẩy.
            - given_b: mệnh đề nối tiếp, bắt đầu bằng chữ thường, kết thúc bằng dấu chấm; dùng {b}, giữ đúng vai trò như ví dụ và giữ nguyên biến tên riêng.
            - question: đúng một câu hỏi ngắn về đại lượng cần tìm. Không nhắc lại dữ kiện, không trả lời hoặc giải thích.
            - solution_lead: một câu dẫn để học sinh viết phép tính, kết thúc bằng dấu hai chấm. Không chứa phép tính hay đáp số.
            - unit_id: chọn một mã phù hợp hoạt động trong danh mục bên dưới.
            C# điền biến sau: {name}/{other} là toàn bộ tên/chủ thể; {a}/{b} là số; {unit} là toàn bộ tên đồ vật/đơn vị.
            Giữ nguyên biến trong ví dụ, không thay bằng tên cụ thể, không thêm danh xưng trước biến, không viết số bằng chữ hoặc chữ số.
            Mỗi dữ kiện dùng số của mình đúng một lần, sát {unit}. Không có {a}/{b} trong question hoặc solution_lead.
            Chỉ viết {unit}, không thêm tên đồ vật sau biến. Mỗi câu chỉ một ý, không thêm nhân vật, dữ kiện hay bước giải.
            Có thể đổi cách mở câu và động từ cùng hoạt động; giữ nguyên quan hệ và đại lượng cần tìm trong ví dụ.
            """ : $$"""
            Write a natural English elementary-school word-problem template. Return JSON only; do not solve it.
            Setting: {{c.TopicId}}/{{c.SceneId}}. Activity: {{scene.EnglishAction}}.
            Relationship: {{relationship}}
            Field tasks:
            - given_a: one clause stating the first fact, using {a} and the example's actor; end with a comma.
            - given_b: the continuing clause, beginning lowercase and ending with a period; use {b}, preserve the example's roles and keep proper-name slots intact.
            - question: one short question asking for the target. Do not repeat the facts, explain or answer it.
            - solution_lead: one prose lead ending with a colon, ready for the pupil's calculation. No formula or answer.
            - unit_id: choose one compatible ID from the catalogue below.
            C# fills the slots later: {name}/{other} are complete actors; {a}/{b} are quantities; {unit} is the complete object/unit noun.
            Keep the example's slots. Never substitute literal names, add actor titles, or write numerical values or number words.
            {name}/{other} already include the full group, business or personal role. Write '{name} makes', never '{name}'s craft group makes'; do not append an actor role to a slot.
            Each given uses its quantity once followed by {unit} (more/fewer may intervene). No {a}/{b} in question or solution_lead.
            Write only {unit}, with no extra object noun after it. Each sentence states one idea; add no actors, facts or solution steps.
            Vary openings and verbs within this activity while keeping the example's relationship and target.
            """;
        string magnitude = ((int)c.Tier, vi) switch {
            (1, true) => "hàng đơn vị", (2, true) => "hàng chục", (3, true) => "hàng trăm", (4, true) => "hàng nghìn", (5, true) => "hàng chục nghìn",
            (1, false) => "ones", (2, false) => "tens", (3, false) => "hundreds", (4, false) => "thousands", _ => "tens of thousands" };
        prompt += vi ? $"\nQuy mô dữ kiện chính: {magnitude}. Vai trò đầy đủ của {{name}}/{{other}}: {scale.VietnameseActor.Replace("{person}", "…")}. {scale.VietnameseScope}"
            : $"\nPrimary quantity scale: {magnitude}. Each {{name}}/{{other}} slot represents an ENTIRE {scale.EnglishActor.Replace("{person}'s ", "").Replace("{person}", "person")}; use it directly as the subject. {scale.EnglishScope}";
        prompt += vi ? "\nHai lượng cùng loại {unit}. Không thêm loại đồ vật khác, tiền, đơn vị đo, quan hệ đổi đơn vị, nhóm hoặc thời kỳ chứa nhau. Giữ quy mô chủ thể; không biến cơ sở lớn thành một người tự làm trong một buổi."
            : "\nBoth quantities count the same kind of {unit}. No additional object kinds, money, measures, conversions or overlapping groups/periods. Keep the selected actor scale; do not turn a large organisation into one person working in a single morning.";
        if (scene.Kind == AdditionSceneKind.Contributions)
            prompt += vi ? "\nCả hai dữ kiện so sánh kết quả góp/thu gom của hoạt động này, không đổi sang số đồ vật đang có."
                : "\nBoth givens describe this activity's contributions/collected amounts. Do not replace an activity result with possessions ('has').";
        if (scene.Kind == AdditionSceneKind.Arrivals)
            prompt += vi ? "\n{name} là địa điểm, không phải người hay con vật. Các {unit} có mặt ở đó hoặc đến đó; không viết chúng đến đậu cùng {name}."
                : "\n{name} is a PLACE, not a person or animal. The {unit} are present there or arrive there; they do not perch together with {name}.";
        if (scene.Kind is AdditionSceneKind.Periods or AdditionSceneKind.Parts)
            prompt += vi ? $"\n{{part_a}} nghĩa là {scale.VietnamesePartA}; {{part_b}} nghĩa là {scale.VietnamesePartB}. Trong dữ kiện dùng biến, không viết các tên này ra. Trong câu hỏi/câu dẫn có thể gọi chung là {scale.VietnameseSpan}; không đổi thành thời kỳ hoặc phần nhỏ khác."
                : $"\n{{part_a}} means {scale.EnglishPartA}; {{part_b}} means {scale.EnglishPartB}. Use the slots, not their literal meanings, in the givens. Questions/leads may refer collectively to {scale.EnglishSpan}; do not change the periods or spatial parts.";
        prompt += (vi ? "\nDanh mục đơn vị (chỉ unit_id là giá trị cụ thể; trong câu vẫn dùng {unit}): "
            : "\nCompatible units (only unit_id is literal; use {unit} in all prose): ")
            + JsonSerializer.Serialize(scale.UnitIds.Select(id => new { unit_id = id, meaning = QuestionUnits.Find(id)!.Item(c.Language) }), JsonOptions)
            + "\nCorrect role example: " + QuestionBankStore.SerializeDraft(AdditionQuestionCatalogue.Example(c));
        if (!string.IsNullOrEmpty(correction))
            prompt += vi ? $"\nLần trước bị từ chối: {correction}. Viết lại toàn bộ JSON ngắn gọn theo ví dụ. Mỗi dữ kiện chỉ một mệnh đề, đúng biến; câu hỏi chỉ hỏi, câu dẫn không giải bài."
                : $"\nPrevious output rejected: {correction}. Rewrite the complete short JSON with the example's roles. Each given states one fact, the question only asks, and the lead does not solve it.";
        return prompt + "\n" + LanguageRule(vi)
            + (correction is "WrongLanguage" or "InvalidText" ? "\n" + Correction(correction) : "");
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
        _ => "Follow the five-field JSON schema and the role example."
    };

    private static string LanguageRule(bool vi) => vi
        ? "Cả hai dữ kiện, câu hỏi và lời dẫn lời giải chỉ dùng tiếng Việt có dấu; không xen từ ngoại ngữ, ký tự lạ hoặc ký tự vô hình. Giữ nguyên biến và mã unit_id."
        : "Both givens, the question and solution lead must use English only, without foreign-language words, stray symbols or invisible characters. Preserve placeholders and unit_id.";

    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
}
