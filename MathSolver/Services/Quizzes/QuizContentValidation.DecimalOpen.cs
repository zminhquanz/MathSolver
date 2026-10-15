namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckDecimalOpenLists(QuizContentPack pack)
    {
        if(!pack.Lists.TryGetValue(DecimalStoryPilot.ContextList,out var contexts))return;
        if(!pack.Lists.TryGetValue(DecimalStoryPilot.RelationList,out var relations)||contexts.GetArrayLength()<1||relations.GetArrayLength()<1)
            Fail("Missing decimal pilot contexts or relations.");
        var ids=new HashSet<string>();
        foreach(var row in contexts.EnumerateArray())
        {
            if(!ids.Add(RequiredString(row,"Id")))Fail("Duplicate decimal pilot context.");
            foreach(string field in new[]{"Actor","Other","Item","Unit","Group"})_ = RequiredString(row,field);
            string unit=RequiredString(row,"Unit");
            if(row.GetProperty("Money").GetBoolean()?unit is not ("nghìn đồng" or "dollars"):unit is not ("kg" or "m" or "l" or "litres"))
                Fail("Decimal pilot money/material unit mismatch.");
            decimal per=row.GetProperty("MaximumPerGroup").GetDecimal(),total=row.GetProperty("MaximumTotal").GetDecimal();
            if(per<1||total<per*13||total>100000)Fail("Invalid decimal pilot capacity.");
        }
        ids.Clear();
        foreach(var row in relations.EnumerateArray())
        {
            if(!ids.Add(RequiredString(row,"Id")))Fail("Duplicate decimal pilot relation.");
            if(RequiredString(row,"Type") is not ("DecimalAdd" or "DecimalSubtract" or "DecimalMultiply" or "DecimalDivide"))
                Fail("Invalid decimal pilot operation.");
            foreach(string field in new[]{"FirstRole","SecondRole","TargetRole","Question","Lead","AlternativeQuestion"})_ = RequiredString(row,field);
            string id=RequiredString(row,"Id"),kind=id.EndsWith("-money",StringComparison.Ordinal)?id[..^6]:id;
            string signature=string.Join('|',new[]{"Type","FirstRole","SecondRole","TargetRole"}.Select(key=>RequiredString(row,key)));
            if(signature!=DecimalStoryPilot.RelationSignature(kind)
                ||row.GetProperty("MoneyOnly").GetBoolean()!=(id.EndsWith("-money",StringComparison.Ordinal)||kind=="price"))
                Fail("Decimal pilot relation roles do not match its C# operation.");
            var facts=row.GetProperty("Facts");var alternatives=row.GetProperty("AlternativeFacts");
            if(facts.GetArrayLength()!=2||alternatives.GetArrayLength()!=2)Fail("Decimal pilot needs two facts.");
            IEnumerable<string> Tokens(string s)=>System.Text.RegularExpressions.Regex.Matches(s,@"\{[^{}]+\}").Select(m=>m.Value).Order();
            var allowed=new HashSet<string>{"{f0}","{f1}","{v0}","{v1}","{v2}","{v3}","{v4}","{v5}","{v6}"};
            var wording=facts.EnumerateArray().Concat(alternatives.EnumerateArray()).Select(v=>v.GetString()!)
                .Concat(new[]{"Question","Lead","AlternativeQuestion"}.Select(key=>RequiredString(row,key)));
            if(wording.Any(text=>Tokens(text).Any(token=>!allowed.Contains(token))))Fail("Unknown decimal pilot variable.");
            for(int i=0;i<2;i++)if(!Tokens(facts[i].GetString()!).SequenceEqual(Tokens(alternatives[i].GetString()!)))
                Fail("Decimal pilot reference changed variables.");
            if(!Tokens(row.GetProperty("Question").GetString()!).SequenceEqual(Tokens(row.GetProperty("AlternativeQuestion").GetString()!)))
                Fail("Decimal pilot question changed variables.");
        }
    }
}
