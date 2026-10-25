using System.Text.RegularExpressions;
using System.Text.Json;
using MathSolver.Models;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckMoneyChangeLists(QuizContentPack pack)
    {
        if(!pack.Lists.TryGetValue(MoneyChangePilot.ListName,out var rows))return;
        if(rows.GetArrayLength()!=2)Fail("Missing money-change contexts.");
        var ids=new HashSet<string>();
        foreach(var row in rows.EnumerateArray())
        {
            string id=RequiredString(row,"Id");
            if(!ids.Add(id)||id is not ("notebooks" or "pencils"))Fail("Invalid money-change context.");
            if(RequiredString(row,"Relation")!=MoneyChangePilot.Relation||RequiredString(row,"PriceRole")!="unit-price"
                ||RequiredString(row,"CountRole")!="purchase-count"||RequiredString(row,"PaidRole")!="cash-tendered"
                ||RequiredString(row,"TargetRole")!="change")Fail("Changed money-change roles.");
            foreach(string field in new[]{"Buyer","Item","Items","PriceFact","CountFact","PaidFact","Question","CostLead","ChangeLead"})_=RequiredString(row,field);
            if(RequiredString(row,"MoneyUnit")!=(pack.Culture=="vi-VN"?"đồng":"dong"))Fail("Changed money-change currency.");
            int min=row.GetProperty("MinimumPrice").GetInt32(),max=row.GetProperty("MaximumPrice").GetInt32();
            if(min<1000||max>50000||min>max||min%1000!=0||max%1000!=0||row.GetProperty("MaximumCount").GetInt32() is <2 or >8)
                Fail("Invalid money-change price/count bounds.");
            MoneyChangePilot.Context? context;
            try{context=row.Deserialize<MoneyChangePilot.Context>();}
            catch(JsonException){Fail("Invalid money-change context fields.");return;}
            if(context is null||!QuestionBank.MoneyChangeOpenMeaning.ProvesReference(context,
                pack.Culture=="vi-VN"?AppLanguage.Vietnamese:AppLanguage.English))Fail("Changed money-change reference meaning.");
            foreach(var (field,expected) in new (string,string[])[]{("PriceFact",["{v1}","{f0}","{v3}"]),
                ("CountFact",["{v0}","{f1}","{v2}"]),("PaidFact",["{v0}","{f2}","{v3}"]),
                ("Question",["{v0}","{v3}"]),("CostLead",["{v2}"]),("ChangeLead",["{v0}"])})
                if(!Regex.Matches(RequiredString(row,field),@"\{[^{}]+\}").Select(m=>m.Value).Order().SequenceEqual(expected.Order()))
                    Fail("Changed money-change slots: "+field);
        }
    }
}
