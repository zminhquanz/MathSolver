using MathSolver.Models;
using MathSolver.Services;
using MathSolver.Services.QuestionBank;
using MathSolver.Testing;
using System.Text;
using System.Text.Json;

internal static class BankDraftFixture
{
    internal static string Draft(BasicQuestionContract c)
    {
        if (c.IsTemplate) return QuestionBankStore.SerializeDraft(BasicQuestionTemplates.Example(c));
        bool vi = c.Language == AppLanguage.Vietnamese;
        string a = c.Operation == ArithmeticOperation.Multiply
            ? vi ? $"Mỗi thùng chứa {c.Left} quyển sách." : $"Each box holds {c.Left} books."
            : vi ? $"{c.Subject} có {c.Left} quyển sách." : $"{c.Subject} has {c.Left} books.";
        string b = c.Operation switch
        {
            ArithmeticOperation.Add => vi ? $"{c.Subject} nhận thêm {c.Right} quyển sách." : $"{c.Subject} receives {c.Right} more books.",
            ArithmeticOperation.Subtract => vi ? $"{c.Subject} cho đi {c.Right} quyển sách." : $"{c.Subject} gives away {c.Right} books.",
            ArithmeticOperation.Multiply => vi ? $"{c.Subject} có {c.Right} thùng như nhau." : $"{c.Subject} has {c.Right} identical boxes.",
            _ => vi ? $"{c.Subject} xếp đều sách vào {c.Right} thùng." : $"{c.Subject} distributes the books equally into {c.Right} boxes."
        };
        string q = c.Operation switch
        {
            ArithmeticOperation.Subtract => vi ? $"Hỏi {c.Subject} còn lại bao nhiêu quyển sách?" : $"How many books does {c.Subject} have left?",
            ArithmeticOperation.Divide => vi ? $"Hỏi mỗi thùng của {c.Subject} chứa bao nhiêu quyển sách?" : $"How many books are in each box of {c.Subject}?",
            _ => vi ? $"Hỏi {c.Subject} có tất cả bao nhiêu quyển sách?" : $"How many books does {c.Subject} have in total?"
        };
        return JsonSerializer.Serialize(new { given_a = a, given_b = b, question = q });
    }
}
