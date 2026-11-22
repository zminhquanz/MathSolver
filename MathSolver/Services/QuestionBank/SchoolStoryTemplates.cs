using MathSolver.Models;

namespace MathSolver.Services.QuestionBank;

/// <summary>Reviewed bilingual prose for school activities, shared by C#, AI and worksheet exports.</summary>
internal static class SchoolStoryTemplates
{
    internal static QuizStoryTemplate Create(SchoolThemeSubject subject, BasicQuestionContract contract)
    {
        bool vi = contract.Language == AppLanguage.Vietnamese;
        string activity = vi ? subject.ViActivity : subject.EnActivity;
        string removal = vi ? subject.ViRemoval : subject.EnRemoval;
        string givenA, givenB, question, alternateQuestion, lead;

        switch (contract.Operation)
        {
            case ArithmeticOperation.Add:
                givenA = vi ? $"{{name}} {activity} {{a}} {{unit}}," : $"{{name}} {activity} {{a}} {{unit}},";
                givenB = vi ? $"còn {{other}} {activity} {{b}} {{unit}}." : $"and {{other}} {activity} {{b}} {{unit}}.";
                question = vi ? "Hỏi tổng số {unit} của cả hai là bao nhiêu?" : "How many {unit} do they have in total?";
                alternateQuestion = vi ? "Cả hai có tất cả bao nhiêu {unit}?" : "What is their combined total of {unit}?";
                lead = vi ? "Tổng số {unit} của cả hai là:" : "Their combined number of {unit} is:";
                break;
            case ArithmeticOperation.Subtract:
                givenA = $"{{name}} {activity} {{a}} {{unit}},";
                givenB = vi ? $"sau đó {removal} {{b}} {{unit}}." : $"and later {removal} {{b}} {{unit}}.";
                question = vi ? "Hỏi {name} còn lại bao nhiêu {unit}?" : "How many {unit} does {name} have left?";
                alternateQuestion = vi ? "Số {unit} còn lại ở {name} là bao nhiêu?" : "What is the remaining number of {unit} at {name}?";
                lead = vi ? "Số {unit} còn lại là:" : "The number of {unit} remaining is:";
                break;
            case ArithmeticOperation.Multiply:
                bool reading = subject.Id == "reading";
                bool bookDrive = subject.Id == "book-drive";
                givenA = reading
                    ? (vi ? "Mỗi {group}, {name} đọc {a} {unit}," : "Each {group}, {name} reads {a} {unit},")
                    : bookDrive
                        ? (vi ? "Mỗi {group} của {name} góp được {a} {unit}," : "Each {group} on {name} collects {a} {unit},")
                        : (vi ? "Mỗi {group} của {name} có {a} {unit}," : "Each {group} at {name} contains {a} {unit},");
                givenB = vi ? "và có {b} {group} như vậy." : "and there are {b} such {groups}.";
                question = vi ? "Hỏi tổng cộng có bao nhiêu {unit}?" : "How many {unit} are there altogether?";
                alternateQuestion = vi ? "Tất cả các {group} có bao nhiêu {unit}?" : "What is the total number of {unit}?";
                lead = vi ? "Tổng số {unit} là:" : "The total number of {unit} is:";
                break;
            case ArithmeticOperation.Divide:
                bool readingPlan = subject.Id == "reading";
                givenA = readingPlan
                    ? (vi ? "{name} dự định đọc {a} {unit}," : "{name} plans to read {a} {unit},")
                    : subject.Id == "book-drive"
                        ? (vi ? "{name} góp được {a} {unit}," : "{name} collects {a} {unit},")
                        : (vi ? "{name} có {a} {unit}," : "{name} has {a} {unit},");
                givenB = readingPlan
                    ? (vi ? "và chia đều số trang cần đọc trong {b} {group}." : "and divides the reading evenly across {b} {groups}.")
                    : (vi ? "rồi phân đều vào {b} {group}." : "and distributes them equally among {b} {groups}.");
                question = vi ? "Hỏi mỗi {group} có bao nhiêu {unit}?" : "How many {unit} are there per {group}?";
                alternateQuestion = vi ? "Mỗi {group} nhận được bao nhiêu {unit}?" : "What is the number of {unit} in each {group}?";
                lead = vi ? "Số {unit} trong mỗi {group} là:" : "The number of {unit} per {group} is:";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(contract));
        }

        var prose = new ReviewedQuestionProse(
            [givenA], [givenB], [question, alternateQuestion], [lead], subject.UnitId);
        return new(contract.SceneId, "Applied", prose.Stories().Take(2).ToArray(), prose);
    }
}
