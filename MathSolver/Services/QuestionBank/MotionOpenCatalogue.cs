using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;

namespace MathSolver.Services.QuestionBank;

internal static class MotionOpenCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Motion&&c.BankVariant==(int)MotionQuizType.Basic&&MotionOpenPilot.IsProfile(c.Story?.NarrativeId);
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        var d=MotionOpenPilot.Create(c.Story!.NarrativeId,c.Language,c.Tier,new(c.Story.Seed));
        var q=new MotionQuizGenerator(new(c.Story.Seed)).GenerateNarrative(ArithmeticQuizMode.Essay,c.Language,MotionQuizType.Basic,new(c.Tier,false),d.Profile);
        string first=d.Relation.Kind is MotionQuestionKind.BasicSpeed or MotionQuestionKind.BasicTime?"distance":"uniform-moving-speed";
        string second=d.Relation.Kind==MotionQuestionKind.BasicTime?"uniform-moving-speed":d.HasRest?"total-elapsed-including-rest":"actual-travel-duration";
        var roles=new Dictionary<string,string>{{"f0",first},{"f1",second},{"f2","single-included-rest-duration"},{"v0","single-traveller"},
            {"v1","speed-unit"},{"v2","travel-or-elapsed-time-unit"},{"v3","distance-unit"},{"v4","rest-time-unit"}};
        var quantities=d.Values.Select(v=>new NarrativeQuantity(v.Key,roles[v.Key],v.Value)).ToArray();
        var facts=new List<NarrativeClause>{new("fact_0",d.Relation.Fact0),new("fact_1",d.Relation.Fact1)};
        if(d.HasRest)facts.Add(new("fact_2",d.Relation.Fact2!));
        NarrativeStep[] steps=[new("step_0",d.Relation.Lead,d.Equation+" "+d.AnswerUnit)];
        string schema=string.Join("\n",d.Profile,c.Language,c.Tier,d.Context,d.Relation,string.Join("/",quantities.Select(v=>v.Role)));
        return new(q,d.Profile,d.Render(d.Relation.Lead),d.AnswerUnit,d.Answer+" "+d.AnswerUnit,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(schema))),quantities,facts,d.Relation.Question,steps,
            facts.ToDictionary(f=>f.Role,f=>f.Role=="fact_0"?new[]{f.Text,d.Relation.AlternateFact0}:new[]{f.Text}),
            [d.Relation.Question],steps.ToDictionary(s=>s.Id,s=>new[]{s.Lead}));
    }
    internal static string Instructions(BasicQuestionContract c)
    {
        bool vi=c.Language==AppLanguage.Vietnamese;
        string kind=c.SceneId.Split('/')[1];
        string target=kind switch {"speed"=>vi?"vận tốc":"speed","time"=>vi?"thời gian thực sự di chuyển":"actual travel time",_=>vi?"quãng đường":"distance"};
        string placement=kind=="time"
            ?"Không viết tốc độ của {f1}; hãy viết vận tốc là {f1} {v1}."
            :"Đặt số đo thời gian sau là, trong hoặc kéo dài; không đặt sau chiếc hay của chiếc. {f1} {v2} đo thời gian, không phải vận tốc hay phương tiện.";
        string condition=kind=="rest"
            ?vi?"Tổng thời gian gồm đúng một lần nghỉ bên trong; vận tốc chỉ áp dụng khi đang di chuyển. Không biến tổng thời gian thành thời gian chạy xe hay dời nghỉ ra sau chuyến đi."
                :"The supplied elapsed time INCLUDES exactly one stop INSIDE the journey; speed applies only while moving. Do not call elapsed time actual travel time or move the stop after arrival."
            :kind=="time"
                ?vi?"Chưa biết thời gian di chuyển. Chỉ cho quãng đường và vận tốc không đổi; chuyến đi liên tục, không nghỉ."
                    :"Travel time is UNKNOWN. Only distance and constant speed are supplied; the journey is continuous with no stops."
                :vi?"Thời gian cho biết là thời gian thực sự di chuyển liên tục, không có dừng nghỉ. Vận tốc không đổi trong suốt chuyến đi."
                    :"The supplied time is the actual continuous travel time, with no rest. The speed remains constant throughout the journey.";
        return vi?$$"""
            Viết bài chuyển động mới, chỉ có một chủ thể. Ví dụ là tham khảo, được đổi cách mở đầu và cách nối câu.
            Giữ đúng vai trò từng biến, vận tốc không đổi khi di chuyển, cùng hệ đơn vị đã chọn. Không điền giá trị vào biến.
            Câu hỏi chỉ tìm {{target}}, không đổi sang đại lượng khác. Không hỏi giờ trên đồng hồ, vận tốc trung bình nhiều chặng hay lượt về.
            {{condition}}
            Dữ kiện và câu hỏi là câu hoàn chỉnh. Câu dẫn lời giải chỉ là tên đại lượng, không mở bằng Tìm/Tính hay ghi chỉ dẫn kỹ thuật.
            Dùng chủ thể và đơn vị trong biến; không thêm tên người, phương tiện, điều kiện, số hoặc đơn vị viết ngoài biến. Không tính toán/tiết lộ đáp án. Chỉ trả JSON.
            {v0} là danh từ chỉ phương tiện: một {v0} hoặc chiếc {v0}. Câu hỏi kết thúc bằng bao nhiêu rồi biến đơn vị; đơn vị không phải người hay xe.
            Đi liên tục không đồng nghĩa với vận tốc không đổi. Phải nêu rõ vận tốc không đổi khi di chuyển, kể cả bài hỏi vận tốc.
            {{placement}}
            Sau lần giới thiệu {v0}, gọi là xe hoặc dùng câu không lặp chủ thể. Được thay cách mở đầu, thứ tự cụm và động từ để câu tự nhiên.
            """:$$"""
            Write a new natural motion problem about one vehicle. References do not limit sentence wording.
            Keep each quantity role and the constant speed while moving; the supplied measurement units already agree. Fill no placeholders.
            Ask only for {{target}}, not another quantity, a clock time, a multi-leg average or a return journey.
            {{condition}}
            Facts and question are full sentences; the solution heading is a noun phrase naming the target, not Find/Determine or a procedural instruction.
            Keep the vehicle and units in their placeholders. Do not add names, vehicles, facts, conditions or filled units. No calculations or answers. Return only JSON.
            {v0} is a vehicle noun: a {v0} or the {v0}. End the question with in followed by its unit variable. Units are measurements, never actors.
            Continuous travel does not imply constant speed. Explicitly state constant speed while moving, also when speed is the unknown.
            Put a duration after is, for, takes or lasts; never use a duration as a vehicle or owner. Put a speed after at, remains or speed of, not after speed belonging to.
            Do not call the vehicle a traveller, traveler or person. After introducing {v0}, use it or the vehicle. Vary openings, clause order and verbs naturally without changing roles.
            """;
    }
    internal static string Relation(BasicQuestionContract c)
    {
        string kind=c.SceneId.Split('/')[1];
        return kind switch {
            "distance"=>"motion-uniform-distance: f0=speed, f1=actual moving duration; TARGET=distance; continuous, no stops.",
            "speed"=>"motion-uniform-speed: f0=distance, f1=actual moving duration; TARGET=speed; speed explicitly constant, continuous, no stops.",
            "time"=>"motion-uniform-travel-time: f0=distance, f1=speed; TARGET=actual moving duration; continuous, no stops.",
            _=>"motion-one-included-stop-distance: f0=speed while moving, f1=total elapsed INCLUDING the stop, f2=one rest INSIDE elapsed; TARGET=distance; C# subtracts rest before using speed."};
    }
    internal static string ContextHint(AppLanguage language)=>language==AppLanguage.Vietnamese
        ?"{v0} gọi phương tiện, đặt trong cụm một {v0} hoặc chiếc {v0}. {v1}, {v2}, {v3}, {v4} là các đơn vị đo, không phải chủ thể. Giữ nguyên biến và không viết giá trị bằng số hay bằng chữ."
        :"{v0} names the supplied vehicle; use a {v0} or the {v0}. {v1}, {v2}, {v3}, {v4} are measurement units, not actors. Keep placeholders instead of writing values as numerals or number words.";
}
