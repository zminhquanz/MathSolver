using MathSolver.Models;
using System.Globalization;

namespace MathSolver.Services;

/// <summary>Complete observations for three independent groups, with no weighting or missing data.</summary>
internal static class AverageOpenPilot
{
    internal const string Prefix="average-open/", ListName="AverageOpen.Contexts";
    internal sealed record Context(string Id,string[] Groups,string Unit,string GroupUnit,string GroupPlural,string Verb,string BaseVerb,int Capacity,
        string Fact,string Scope,string Question,string Lead);
    internal sealed record Data(Context Context,string Profile,int[] Counts)
    {
        internal int GroupCount=>Counts.Length;
        internal int Total=>Counts.Sum();
        internal int Average=>Total/GroupCount;
        internal string Expression=>"("+string.Join(" + ",Counts)+") ÷ "+GroupCount;
        internal Dictionary<string,string> Values=>new()
        {
            ["f0"]=N(Counts[0]),["f1"]=N(Counts[1]),["f2"]=N(Counts[2]),["f3"]=N(GroupCount),
            ["v0"]=Context.Groups[0],["v1"]=Context.Groups[1],["v2"]=Context.Groups[2],
            ["v3"]=Context.Unit,["v4"]=Context.GroupUnit,["v5"]=Context.Verb,["v6"]=Context.BaseVerb,["v7"]=Context.GroupPlural
        };
        internal string Fact(int i)=>Context.Fact.Replace("{owner}","{v"+i+"}").Replace("{count}","{f"+i+"}");
        internal string Render(string text)
        {foreach(var (key,value) in Values)text=text.Replace("{"+key+"}",value,StringComparison.Ordinal);return text;}
        internal string Problem=>Render(string.Join(" ",Enumerable.Range(0,GroupCount).Select(Fact).Append(Context.Scope).Append(Context.Question)));
    }
    internal static string N(int value)=>value.ToString(CultureInfo.InvariantCulture);
    internal static IReadOnlyList<Context> Contexts(AppLanguage language)=>QuizContentCatalog.LoadList<Context>(ListName,QuizContentCatalog.Culture(language));
    internal static IEnumerable<string> Profiles(AppLanguage language)=>Contexts(language).Select(c=>Prefix+c.Id);
    internal static Data Create(string profile,AppLanguage language,CurriculumTier tier,Random random)
    {
        if(!Enum.IsDefined(tier)||!profile.StartsWith(Prefix,StringComparison.Ordinal))throw new ArgumentException("InvalidAverageOpenProfile");
        var context=Contexts(language).SingleOrDefault(c=>Prefix+c.Id==profile)??throw new ArgumentException("InvalidAverageOpenContext");
        if(context.Groups.Length!=3||context.Groups.Distinct().Count()!=3||context.Capacity<20)throw new InvalidDataException("InvalidAverageOpenGroups");
        int ceiling=Math.Min(context.Capacity-2,15*(int)tier);
        int average=random.Next(5,ceiling+1),offset=random.Next(1,Math.Min(average-1,context.Capacity-average)+1);
        int[] counts=[average-offset,average,average+offset];random.Shuffle(counts);
        return new(context,profile,counts);
    }
}

public sealed partial class AverageQuizGenerator
{
    internal ArithmeticQuizQuestion GenerateOpenDirect(ArithmeticQuizMode mode,AppLanguage language,CurriculumTier tier,string profile)
    {
        var d=AverageOpenPilot.Create(profile,language,tier,_random);
        string equation=d.Expression+" = "+d.Average;
        var contract=new AverageQuizContract(AverageQuizType.Direct,[d.GroupCount,..d.Counts],d.Average,d.Context.Unit,
            d.Render(d.Context.Lead),d.Problem,equation,FormatSolution(d.Render(d.Context.Lead)+":",equation,d.Context.Unit,d.Average,language),
            d.Total,ArithmeticOperation.Divide,d.GroupCount){StoryContextId=profile};
        return CreateQuestion(mode,contract,includeWordProblem:false);
    }
}
