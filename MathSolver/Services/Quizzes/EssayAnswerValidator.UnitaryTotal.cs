using MathSolver.Models;

namespace MathSolver.Services;

public sealed partial class EssayAnswerValidator
{
    private static bool IsUnitaryTotal(ArithmeticQuizQuestion q)=>q.ProportionProblem is {Type:ProportionQuizType.Direct} p
        &&p.NarrativeId.StartsWith(UnitaryTotalPilot.Prefix,StringComparison.Ordinal);

    private static bool UnitaryItemUnitMatches(string entered,string expected)
    {
        // Containers change the quantity being counted. Only harmless item
        // classifiers or English singular/plural forms may be interchanged.
        static string Item(string unit)
        {
            unit=NormalizeUnit(unit);
            foreach(string classifier in new[]{"cây ","cái ","chiếc ","quyển ","cuốn "})
                if(unit.StartsWith(classifier,StringComparison.Ordinal))return unit[classifier.Length..];
            return NormalizeEnglishUnitToSingular(unit);
        }
        return Item(entered)==Item(expected);
    }

    private static (bool IsCorrect,EssayAnswerError Error) ValidateUnitaryTotalCalculations(
        ArithmeticQuizQuestion q,string? text,out IReadOnlyList<EssayStepValidationResult> steps)
    {
        var p=q.ProportionProblem!;
        if(p.A<=0||p.B<=0||p.C<=0||p.B%p.A!=0||p.CorrectAnswer!=(System.Numerics.BigInteger)(p.B/p.A)*p.C)
        {steps=[];return(false,EssayAnswerError.WrongOperandsOrOperation);}
        string[] lines=(text??"").Replace("\r\n","\n",StringComparison.Ordinal).Replace('\r','\n')
            .Split('\n',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
        if(lines.Length is <1 or >2){steps=[];return(false,EssayAnswerError.InvalidEquationFormat);}
        var checkedSteps=new List<EssayStepValidationResult>();
        for(int i=0;i<lines.Length;i++)
        {
            var (calculation,unit)=SplitEquationResult(q,lines[i]);
            bool first=lines.Length==2&&i==0;
            var result=first
                ? (NormalizeProportionEquation(calculation)==NormalizeProportionEquation($"{p.B} ÷ {p.A} = {p.B/p.A}")
                    ? (true,EssayAnswerError.None):(false,EssayAnswerError.WrongOperandsOrOperation))
                : ValidateProportionEquation(p,calculation);
            // Operand matching normalizes notation; evaluate the original grouping as well.
            if(result.Item1)
                result=ValidateCalculatedEquation(first?q with {CorrectAnswer=p.B/p.A}:q,calculation);
            if(result.Item1&&!IsExpectedUnit(q,unit))result=(false,EssayAnswerError.WrongEquationUnit);
            checkedSteps.Add(new(i+1,lines[i],result.Item1,result.Item2));
        }
        steps=checkedSteps;
        var failed=checkedSteps.FirstOrDefault(s=>!s.IsCorrect);
        return failed is null?(true,EssayAnswerError.None):(false,failed.Error);
    }
}
