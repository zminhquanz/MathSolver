using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>Reviewed pet-care scenarios reused by C#, LLM and print/export paths.</summary>
internal static class PetAnimalStoryTemplates
{
    internal static QuizStoryTemplate Create(PetAnimalSubject subject, BasicQuestionContract contract)
    {
        bool vi = contract.Language == AppLanguage.Vietnamese;
        string givenA, givenB, question, alternate, lead;
        switch (contract.Operation)
        {
            case ArithmeticOperation.Add:
                givenA = vi ? "{name} đang chăm sóc {a} {unit}," : "{name} cares for {a} {unit},";
                givenB = vi ? "còn {other} đang chăm sóc {b} {unit}." : "and {other} cares for {b} {unit}.";
                question = vi ? "Hỏi cả hai nơi đang chăm sóc tất cả bao nhiêu {unit}?" : "How many {unit} do the two facilities care for in total?";
                alternate = vi ? "Tổng số {unit} ở hai nơi là bao nhiêu?" : "What is the combined number of {unit} at both facilities?";
                lead = vi ? "Tổng số {unit} là:" : "The total number of {unit} is:";
                break;
            case ArithmeticOperation.Subtract:
                givenA = vi ? "{name} đang chăm sóc {a} {unit}," : "{name} cares for {a} {unit},";
                givenB = vi ? "sau đó chuyển {b} {unit} đến cơ sở chăm sóc khác phù hợp." : "then transfers {b} {unit} to another suitable care facility.";
                question = vi ? "Hỏi {name} còn chăm sóc bao nhiêu {unit}?" : "How many {unit} does {name} still care for?";
                alternate = vi ? "Số {unit} còn lại ở {name} là bao nhiêu?" : "How many {unit} remain at {name}?";
                lead = vi ? "Số {unit} còn lại là:" : "The number of {unit} remaining is:";
                break;
            case ArithmeticOperation.Multiply:
                givenA = vi ? "Mỗi {group} tại {name} có {a} {unit}," : "Each {group} at {name} houses {a} {unit},";
                givenB = vi ? "và có {b} {group} như vậy." : "and there are {b} such {groups}.";
                question = vi ? "Hỏi có tất cả bao nhiêu {unit}?" : "How many {unit} are there altogether?";
                alternate = vi ? "Tổng số {unit} tại {name} là bao nhiêu?" : "What is the total number of {unit} at {name}?";
                lead = vi ? "Tổng số {unit} là:" : "The total number of {unit} is:";
                break;
            case ArithmeticOperation.Divide:
                givenA = vi ? "{name} đang chăm sóc {a} {unit}," : "{name} cares for {a} {unit},";
                givenB = vi ? "và bố trí đều vào {b} {group} phù hợp." : "and places them equally in {b} suitable {groups}.";
                question = vi ? "Hỏi mỗi {group} có bao nhiêu {unit}?" : "How many {unit} are there in each {group}?";
                alternate = vi ? "Số {unit} trong mỗi {group} là bao nhiêu?" : "What is the number of {unit} per {group}?";
                lead = vi ? "Số {unit} trong mỗi {group} là:" : "The number of {unit} in each {group} is:";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(contract));
        }

        var prose = new ReviewedQuestionProse([givenA], [givenB], [question, alternate], [lead], subject.Unit.Id);
        return new(contract.SceneId, "Applied", prose.Stories().Take(2).ToArray(), prose);
    }
}
