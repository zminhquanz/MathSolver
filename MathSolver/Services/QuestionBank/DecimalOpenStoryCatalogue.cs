using MathSolver.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class DecimalOpenStoryCatalogue
{
    internal static bool IsProfile(BasicQuestionContract c)=>c.Family==BankQuestionFamily.Decimal
        && c.Story?.NarrativeId.StartsWith(DecimalStoryPilot.Prefix,StringComparison.Ordinal)==true;
    internal static bool HasContradiction(BasicQuestionContract c,BasicQuestionDraft draft)
    {
        var d=DecimalStoryPilot.Create(c.SceneId,(ElementaryQuizType)c.BankVariant,c.Language,c.Tier,new(c.Story!.Seed));
        bool Has(string text,string pattern)=>Regex.IsMatch(text,pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
        string all=draft.ProblemText;
        if(Has(all,c.Language==AppLanguage.Vietnamese?@"\b(?:giảm giá|chiết khấu|thuế|phụ phí|tiền thừa|mượn|nợ)\b":@"\b(?:discount|tax|fee|change returned|borrow|debt)\b"))return true;
        string second=draft.Facts![1].Text;
        string gain=c.Language==AppLanguage.Vietnamese?@"\b(?:nhận thêm|được nhận|được tặng|bổ sung thêm)\b":@"\b(?:receives?|is given|gains?)\b";
        string loss=c.Language==AppLanguage.Vietnamese?@"\b(?:dùng|sử dụng|chi|trả|cho đi|bán đi)\b":@"\b(?:uses?|spends?|pays?|gives? away|sells?)\b";
        return d.Relation.Kind=="increase"&&Has(second,loss)
            || d.Relation.Kind=="remaining"&&Has(second,gain)
            || d.Relation.Kind=="combine"&&(Has(second,gain)||Has(second,loss));
    }
    internal static NarrativeLesson Build(BasicQuestionContract c)
    {
        var d=DecimalStoryPilot.Create(c.Story!.NarrativeId,(ElementaryQuizType)c.BankVariant,c.Language,c.Tier,new(c.Story.Seed));
        var q=new ElementaryQuizGenerator(new(c.Story.Seed)).GenerateDecimalPilot(ArithmeticQuizMode.Essay,
            (ElementaryQuizType)c.BankVariant,c.Language,c.Tier,d.Profile);
        var p=q.ElementaryProblem!;
        string[] roles=[d.Relation.FirstRole,d.Relation.SecondRole,"answer_unit","owner_a","owner_b","material","first_unit","second_unit","group_unit"];
        var values=d.Values.ToArray();
        var quantities=values.Select((v,i)=>new NarrativeQuantity(v.Key,roles[i],v.Value)).ToArray();
        var facts=d.Relation.Facts.Select((text,i)=>new NarrativeClause("fact_"+i,text)).ToArray();
        var step=p.Reasoning!.Steps.Single();
        NarrativeStep[] steps=[new("step_0",d.Relation.Lead,
            QuizMathExpressionFormatter.Format(step.Expression)+" = "+step.DisplayValue+" "+step.Unit)];
        string signature=string.Join("\n",new[]{d.Profile,c.Language.ToString(),c.Tier.ToString(),d.Relation.TargetRole,
            string.Join("/",roles),string.Join("/",values.Where(v=>v.Key.StartsWith('v')).Select(v=>v.Value)),
            string.Join(" ",d.Relation.Facts),d.Relation.Question,d.Relation.Lead});
        string schema=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
        return new(q,d.Profile,d.Render(d.Relation.Lead),d.AnswerUnit,p.AnswerText,schema,quantities,facts,d.Relation.Question,steps,
            facts.Select((f,i)=>(f.Role,Choices:new[]{f.Text,d.Relation.AlternativeFacts[i]})).ToDictionary(x=>x.Role,x=>x.Choices),
            [d.Relation.Question,d.Relation.AlternativeQuestion],new Dictionary<string,string[]>{{"step_0",[d.Relation.Lead]}});
    }
    internal static string PromptRelation(BasicQuestionContract c)
    {
        var d=DecimalStoryPilot.Create(c.Story!.NarrativeId,(ElementaryQuizType)c.BankVariant,c.Language,c.Tier,new(c.Story.Seed));
        return "relation="+d.Relation.Kind+"; {f0}="+d.Relation.FirstRole+"; {f1}="+d.Relation.SecondRole
            +"; target="+d.Relation.TargetRole+"; first_unit={v4}; second_unit={v5}; answer_unit={v0}; groups={v6}. "
            +"{v1}=owner_a; {v2}=owner_b; {v3}=material. "
            +(d.Relation.Kind switch{
                "combine"=>"Two separate owners; combine their stocks. No transfer between them.",
                "increase"=>"Same owner receives an additional amount; ask the final stock.",
                "remaining"=>"Same owner uses/removes an amount; ask what remains.",
                "groups"=>"Amount in EACH group times integer number of groups; ask total amount.",
                "price"=>"Price of EACH item times integer quantity purchased; ask total cost. No discount or change.",
                "share"=>"TOTAL divided equally among a known INTEGER GROUP COUNT; ask amount per group, not group count.",
                _=>"TOTAL divided by known AMOUNT IN EACH GROUP; ask INTEGER GROUP COUNT, not amount per group. Exact division, no leftovers."
            });
    }
}
