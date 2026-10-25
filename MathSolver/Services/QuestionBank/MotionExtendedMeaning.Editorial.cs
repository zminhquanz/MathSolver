namespace MathSolver.Services.QuestionBank;

internal static partial class MotionExtendedMeaning
{
    // Whole-clause proofs only. Never strip negation, conditions, frames or
    // unknown tails. Slot ownership remains enforced by syntax before this pass.
    private static bool EditorialFact(string kind,int index,string text,bool vi)
    {
        bool inverse=kind is "boat" or "current";
        if(kind is "meeting" or "chasing")return false;
        string direction=index==0?vi?"xuôi dòng":"downstream":vi?"ngược dòng":"upstream";
        string pattern;
        if(inverse&&index<2)
            pattern=vi?@"vận tốc của \{v0\} khi (?:đi )?"+direction+@" so với bờ là \{f"+index+@"\} \{v2\}"
                :@"the "+direction+@" speed of \{v0\} relative to (?:the )?(?:riverbank|bank) is \{f"+index+@"\} \{v2\}";
        else if(inverse)
            pattern=vi?@"cả hai vận tốc này đều được đo trong điều kiện vận tốc của phương tiện so với nước là không đổi và vận tốc dòng nước là hằng số"
                :@"(?:this|the) same craft maintains a constant speed in still water, and the (?:river )?current speed (?:remains uniform throughout both journeys|is consistent for both measurements)";
        else if(index==0)
            pattern=vi?@"(?:vận tốc|tốc độ) của \{v0\} khi (?:nước yên lặng|không có dòng nước) là \{f0\} \{v2\}"
                :@"the speed of \{v0\} (?:when the water is still|in still water) (?:is|amounts to) \{f0\} \{v2\}";
        else if(index==1)
            pattern=vi?@"(?:vận tốc|tốc độ) của dòng nước so với bờ (?:là|được xác định là) \{f1\} \{v2\}"
                :@"the (?:river current|river's current|current) (?:moves|flows) at (?:a rate of )?\{f1\} \{v2\} relative to the (?:bank|riverbank)";
        else
        {
            string movement=kind=="downstream"?vi?"xuôi dòng":"downstream":vi?"ngược dòng":"upstream";
            pattern=vi?@"phương tiện đi "+movement+@", vận tốc khi nước yên lặng và vận tốc dòng nước đều không đổi"
                :@"the craft travels "+movement+@", maintaining (?:steady|constant) speeds for both its movement through still water and the river's flow";
        }
        return Matches(text,pattern);
    }

    private static bool EditorialTarget(string kind,string text,bool vi)
    {
        string pattern;
        if(vi)
            pattern=kind switch {
                "meeting"=>@"(?:hỏi )?thời gian để hai (?:bên|vật) gặp nhau là bao nhiêu \{v3\}",
                "chasing"=>@"(?:hỏi )?thời gian để bên nhanh đuổi kịp bên chậm là bao nhiêu \{v3\}",
                "downstream"=>@"vận tốc xuôi dòng so với bờ (?:là|bằng) bao nhiêu \{v2\}",
                "upstream"=>@"vận tốc ngược dòng so với bờ (?:là|bằng) bao nhiêu \{v2\}",
                "boat"=>@"vận tốc của (?:phương tiện|thuyền) (?:khi nước yên lặng|trong nước yên) (?:là|bằng) bao nhiêu \{v2\}",
                _=>@"vận tốc (?:của )?dòng nước so với bờ (?:là|bằng) bao nhiêu \{v2\}"};
        else
        {
            string quantity=kind switch {
                "meeting"=>@"(?:time|duration) until (?:they|the two travellers) meet",
                "chasing"=>@"(?:time|duration) until the one behind catches up with the one ahead",
                "downstream"=>@"downstream speed (?:of the craft )?relative to (?:the )?(?:riverbank|bank)",
                "upstream"=>@"upstream speed (?:of the craft )?relative to (?:the )?(?:riverbank|bank)",
                "boat"=>@"craft['’]s speed in still water",
                _=>@"(?:river )?current speed relative to (?:the )?(?:riverbank|bank)"};
            string unit=kind is "meeting" or "chasing"?@"\{v3\}":@"\{v2\}";
            // Determine ... ? is ordinary exam language with minor punctuation,
            // distinct from implementation instructions embedded in a fact.
            pattern=@"(?:what is|determine) the "+quantity+@" (?:measured |expressed )?in "+unit;
        }
        return Matches(text,pattern);
    }

    private static bool EditorialHeading(string kind,string text,bool vi)
        =>Matches(text,vi?kind switch {
            "meeting"=>@"thời gian (?:để hai bên )?gặp nhau",
            "chasing"=>@"thời gian (?:để )?bên nhanh đuổi kịp bên chậm",
            "downstream"=>@"vận tốc xuôi dòng so với bờ",
            "upstream"=>@"vận tốc ngược dòng so với bờ",
            "boat"=>@"vận tốc của phương tiện khi nước yên lặng",
            _=>@"vận tốc dòng nước so với bờ"}:kind switch {
            "meeting"=>@"meeting duration",
            "chasing"=>@"(?:duration until catch-up|time needed to catch up)",
            "downstream"=>@"downstream speed relative to (?:the )?(?:riverbank|bank)",
            "upstream"=>@"upstream speed relative to (?:the )?(?:riverbank|bank)",
            "boat"=>@"craft speed in still water",
            _=>@"(?:river )?current speed relative to (?:the )?(?:riverbank|bank)"});
}
