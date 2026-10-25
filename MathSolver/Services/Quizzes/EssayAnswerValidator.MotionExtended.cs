using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class EssayAnswerValidator
{
    private static bool MotionExtendedHeading(MotionQuizContract p,string? text)
    {
        string normalized=NormalizeComparisonText(text??"");
        bool Has(params string[] phrases)=>phrases.Any(phrase=>ContainsNormalizedPhrase(normalized,phrase));
        if(Has("không phải","không cần","not","if","nếu"))return false;
        bool time=Has("thời gian","time","duration"),speed=Has("vận tốc","tốc độ","speed");
        bool waterFrame=Has("so với nước","relative to water","relative to the water");
        bool stillWater=Has("nước yên","nước yên lặng","still water","still-water","calm water");
        return p.QuestionKind switch {
            MotionQuestionKind.MeetingTime=>time&&Has("gặp nhau","meet","meeting")&&!Has("đuổi kịp","catch","catch up"),
            MotionQuestionKind.CatchUpTime=>time&&Has("đuổi kịp","catch","catch up","catches","catching up")&&!Has("gặp nhau","meet","meeting"),
            MotionQuestionKind.RiverDownstreamSpeed=>speed&&Has("xuôi dòng","downstream")&&!Has("ngược dòng","upstream")&&!waterFrame&&!stillWater,
            MotionQuestionKind.RiverUpstreamSpeed=>speed&&Has("ngược dòng","upstream")&&!Has("xuôi dòng","downstream")&&!waterFrame&&!stillWater,
            MotionQuestionKind.RiverBoatSpeed=>speed&&Has("nước yên","nước yên lặng","still water","still-water","calm water")&&!Has("dòng nước","current"),
            MotionQuestionKind.RiverCurrentSpeed=>speed&&Has("dòng nước","current")&&!stillWater&&!waterFrame,_=>false};
    }
    private static bool MotionExtendedOperands(MotionQuizContract p,string expression)
    {
        if(!MotionExtendedPilot.ValidContract(p))return false;
        var f=p.Facts;
        string[] forms=p.QuestionKind switch {
            MotionQuestionKind.MeetingTime=>[$"{f[0]} ÷ ({f[1]} + {f[2]})",$"{f[0]} ÷ ({f[2]} + {f[1]})"],
            MotionQuestionKind.CatchUpTime=>[$"{f[0]} ÷ ({f[2]} - {f[1]})"],
            MotionQuestionKind.RiverDownstreamSpeed=>[$"{f[0]} + {f[1]}",$"{f[1]} + {f[0]}"],
            MotionQuestionKind.RiverUpstreamSpeed=>[$"{f[0]} - {f[1]}"],
            MotionQuestionKind.RiverBoatSpeed=>[$"({f[0]} + {f[1]}) ÷ 2",$"({f[1]} + {f[0]}) ÷ 2"],
            MotionQuestionKind.RiverCurrentSpeed=>[$"({f[0]} - {f[1]}) ÷ 2"],_=>[]};
        return forms.Any(form=>NormalizeProportionEquation(form)==NormalizeProportionEquation(expression));
    }
}
