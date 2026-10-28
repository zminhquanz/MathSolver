using System.Text.RegularExpressions;
using System.Text.Json;
using MathSolver.Models;

namespace MathSolver.Services;

public static partial class QuizContentValidation
{
    private static void CheckDiscountLists(QuizContentPack pack)
    {
        if(!pack.Lists.TryGetValue(DiscountOpenPilot.ListName,out var rows))return;
        if(rows.GetArrayLength()!=2)Fail("Missing one-product discount contexts.");
        var ids=new HashSet<string>();
        foreach(var row in rows.EnumerateArray())
        {
            string id=RequiredString(row,"Id");
            if(!ids.Add(id)||id is not ("notebook" or "backpack"))Fail("Invalid discount context.");
            if(RequiredString(row,"Relation")!=DiscountOpenPilot.Relation||RequiredString(row,"PriceRole")!="original-price"
                ||RequiredString(row,"RateRole")!="discount-percentage-of-original"||RequiredString(row,"TargetRole")!="payable-price")Fail("Changed discount roles.");
            foreach(string field in new[]{"Product","PriceFact","DiscountFact","Question","DiscountLead","PayableLead"})_=RequiredString(row,field);
            if(RequiredString(row,"MoneyUnit")!=(pack.Culture=="vi-VN"?"đồng":"dong"))Fail("Changed discount currency.");
            int min=row.GetProperty("MinimumPrice").GetInt32(),max=row.GetProperty("MaximumPrice").GetInt32();
            if(min<1000||max>500000||min>max||min%1000!=0||max%1000!=0)Fail("Invalid discount price bounds.");
            DiscountOpenPilot.Context? context;
            try{context=row.Deserialize<DiscountOpenPilot.Context>();}
            catch(JsonException){Fail("Invalid discount context fields.");return;}
            if(context is null||!QuestionBank.DiscountOpenMeaning.ProvesReference(context,
                pack.Culture=="vi-VN"?AppLanguage.Vietnamese:AppLanguage.English))Fail("Changed discount reference meaning.");
            foreach(var (field,expected) in new (string,string[])[]{("PriceFact",["{v0}","{f0}","{v1}"]),
                ("DiscountFact",["{v0}","{f1}"]),("Question",["{v0}","{v1}"]),
                ("DiscountLead",["{v0}"]),("PayableLead",["{v0}"])})
                if(!Regex.Matches(RequiredString(row,field),@"\{[^{}]+\}").Select(m=>m.Value).Order().SequenceEqual(expected.Order()))
                    Fail("Changed discount slots: "+field);
        }
    }
}
