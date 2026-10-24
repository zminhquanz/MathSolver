using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services;

public sealed partial class EssayAnswerValidator
{
    private static bool IsUnitaryGroups(ArithmeticQuizQuestion q)=>q.ProportionProblem?.Type==ProportionQuizType.UnitaryGroupCount;
    private static string GroupUnitText(string unit)
    {
        unit=NormalizeUnit(unit);
        return unit.StartsWith('(')&&unit.EndsWith(')')?unit[1..^1].Trim():unit;
    }
    private static bool GroupCountUnitMatches(string unit,string expected)=>UnitaryItemUnitMatches(GroupUnitText(unit),expected);
    private static bool IsGroupRateUnit(string unit,UnitaryGroupUnits expected)
    {
        unit=GroupUnitText(unit);
        // School work may express the per-container count simply in items.
        if(UnitaryItemUnitMatches(unit,expected.Items))return true;
        string[] parts=Regex.Split(unit,@"\s*(?:/|\bper\b|\bmỗi\b)\s*",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
        return parts.Length==2&&UnitaryItemUnitMatches(parts[0],expected.Items)&&UnitaryItemUnitMatches(parts[1],expected.Group);
    }
    private static (bool IsCorrect,EssayAnswerError Error) ValidateUnitaryGroupsCalculations(
        ArithmeticQuizQuestion q,string? text,out IReadOnlyList<EssayStepValidationResult> steps)
    {
        var p=q.ProportionProblem!;
        if(!UnitaryGroupsPilot.ValidContract(p,out int rate,out var answer))
        {steps=[];return(false,EssayAnswerError.WrongOperandsOrOperation);}
        string[] lines=(text??"").Replace("\r\n","\n",StringComparison.Ordinal).Replace('\r','\n')
            .Split('\n',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
        if(lines.Length is <1 or >2){steps=[];return(false,EssayAnswerError.InvalidEquationFormat);}
        var checkedSteps=new List<EssayStepValidationResult>();
        for(int i=0;i<lines.Length;i++)
        {
            bool first=lines.Length==2&&i==0;
            var (calculation,unit)=SplitEquationResult(q,lines[i]);
            int equals=calculation.IndexOf('=');
            string enteredStructure=equals>0?NormalizeProportionEquation(calculation[..equals]):"";
            var forms=first?new[]{$"{p.B} ÷ {p.A} = {rate}"}:new[]{
                $"{p.C} ÷ ({p.B} ÷ {p.A}) = {answer}",$"{p.C} × {p.A} ÷ {p.B} = {answer}",$"{p.A} × {p.C} ÷ {p.B} = {answer}",
                $"{p.C} ÷ {p.B} × {p.A} = {answer}",$"{p.A} ÷ {p.B} × {p.C} = {answer}",$"{p.A} ÷ ({p.B} ÷ {p.C}) = {answer}"};
            bool shape=forms.Any(f=>NormalizeProportionEquation(f[..f.IndexOf('=')])==enteredStructure)
                ||!first&&lines.Length==2&&enteredStructure==NormalizeProportionEquation($"{p.C} ÷ {rate}");
            var result=shape?ValidateCalculatedEquation(first?q with {CorrectAnswer=rate}:q,calculation)
                :(false,EssayAnswerError.WrongOperandsOrOperation);
            bool unitOk=first?IsGroupRateUnit(unit,p.GroupCountUnits!):GroupCountUnitMatches(unit,p.GroupCountUnits!.Groups);
            if(result.Item1&&!unitOk)result=(false,EssayAnswerError.WrongEquationUnit);
            EssayCalculationEvaluator.Value value=default;
            bool hasValue=equals>0&&EssayCalculationEvaluator.TryEvaluate(calculation[..equals],out value,out _);
            string? computed=hasValue?EssayCalculationEvaluator.Format(value):null;
            string? written=equals>=0?calculation[(calculation.LastIndexOf('=')+1)..].Trim():null;
            if(hasValue)
                foreach(string part in calculation.Split('=').Skip(1))
                    if(EssayCalculationEvaluator.TryEvaluate(part,out var stated,out _)&&stated!=value)
                    {written=EssayCalculationEvaluator.Format(stated);break;}
            checkedSteps.Add(new(i+1,lines[i],result.Item1,result.Item2)
                {EnteredUnit=unit,ExpectedUnit=first?p.GroupCountUnits!.Rate:p.GroupCountUnits!.Groups,
                 ComputedValue=computed,WrittenValue=written});
        }
        steps=checkedSteps;var failed=checkedSteps.FirstOrDefault(s=>!s.IsCorrect);
        return failed is null?(true,EssayAnswerError.None):(false,failed.Error);
    }
}
