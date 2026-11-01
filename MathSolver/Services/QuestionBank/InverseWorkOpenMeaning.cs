using MathSolver.Models;
using System.Text.RegularExpressions;

namespace MathSolver.Services.QuestionBank;

internal static class InverseWorkOpenMeaning
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);
    private static bool Match(string text, string pattern)
    {
        string normalized = Regex.Replace(text.Trim().ToLowerInvariant(), @"\s+", " ", RegexOptions.CultureInvariant, Timeout)
            .TrimEnd(' ', '.', '?', ':', '!');
        return Regex.IsMatch(normalized, @"\A(?:" + pattern + @")\z", RegexOptions.CultureInvariant, Timeout);
    }
    internal static BasicDraftValidation Validate(BasicQuestionContract c, BasicQuestionDraft d)
    {
        BasicDraftValidation Pending() => new(d, "SemanticReviewRequired", c,
            "The original work/time, fixed amount of work, equal unchanged worker productivity, completion-time target or headings could not be proved.");
        if (d.Facts is not { Count: 2 } || d.SolutionLeads is not { Count: 2 }) return Pending();
        bool vi = c.Language == AppLanguage.Vietnamese;
        bool Proof(string text, string vietnamese, string english) => Match(text, vi ? vietnamese : english);
        string source = d.Facts[0].Text, condition = d.Facts[1].Text;
        // Contradictions are whole-field assertions. A negated, conditional or
        // otherwise qualified assertion cannot borrow a matching substring.
        bool changedWork = Proof(condition,
            @"khối lượng công việc (?:tăng lên|giảm đi|thay đổi|không giữ nguyên); năng suất của mỗi \{v3\} bằng nhau và không thay đổi",
            @"the amount of work (?:increases|decreases|changes|does not stay fixed); the productivity of each \{v3\} is equal and unchanged");
        bool changedProductivity = Proof(condition,
            @"khối lượng công việc giữ nguyên; năng suất của mỗi \{v3\} (?:tăng lên|giảm đi|thay đổi|khác nhau|không bằng nhau)",
            @"the amount of work stays fixed; the productivity of each \{v3\} (?:increases|decreases|changes|is different|is unequal)");
        bool direct = Proof(condition,
            @"khối lượng công việc giữ nguyên; năng suất của mỗi \{v3\} bằng nhau và không thay đổi; số người và thời gian hoàn thành tỉ lệ thuận với nhau",
            @"the amount of work stays fixed; the productivity of each \{v3\} is equal and unchanged; the worker count and completion time are directly proportional");
        bool workerTarget = Proof(d.Question,
            @"(?:hỏi )?cần bao nhiêu \{v0\} để hoàn thành (?:cùng )?công việc đó trong \{f2\} \{v1\}",
            @"how many \{v0\} are needed to complete (?:that|the) (?:same )?work in \{f2\} \{v1\}");
        bool addedWorkers = Proof(d.Question,
            @"thêm \{f2\} \{v0\} thì cần bao nhiêu \{v1\} để hoàn thành công việc đó",
            @"how many \{v1\} are needed to complete that work with \{f2\} additional \{v0\}");
        bool combinedTeams = Proof(d.Question,
            @"\{f2\} \{v0\} cùng đội ban đầu cần bao nhiêu \{v1\} để hoàn thành công việc đó",
            @"how many \{v1\} do \{f2\} \{v0\} need to complete that work together with the original team");
        if (changedWork || changedProductivity || direct || workerTarget || addedWorkers || combinedTeams)
            return new(null, "ChangedRelationOrTarget", ErrorDetails:
                "The fixed work, equal unchanged productivity, inverse direction or new-team completion-time target changed.");

        bool given = Proof(source,
            @"\{f0\} \{v0\} (?:hoàn thành|làm xong) \{v2\} trong \{f1\} \{v1\}|để hoàn thành \{v2\}, \{f0\} \{v0\} cần \{f1\} \{v1\}",
            @"\{f0\} \{v0\} (?:complete|finish) \{v2\} in \{f1\} \{v1\}|a team of \{f0\} \{v0\} (?:completes|finishes) \{v2\} in \{f1\} \{v1\}|it takes \{f0\} \{v0\} \{f1\} \{v1\} to complete \{v2\}");
        bool fullCondition = Proof(condition,
            @"khối lượng công việc (?:giữ nguyên|không thay đổi); năng suất của mỗi \{v3\} (?:bằng nhau và không thay đổi|bằng nhau và giữ nguyên)|cùng một khối lượng công việc được giữ nguyên; mỗi \{v3\} có năng suất bằng nhau và không thay đổi",
            @"the amount of work (?:stays fixed|remains unchanged|remains the same); the productivity of each \{v3\} is (?:equal and unchanged|the same and does not change)|the same amount of work is kept fixed; each \{v3\} has equal productivity that remains unchanged");
        // Natural alternatives still assert both equality between workers and
        // constancy across the original and replacement teams.
        fullCondition |= Proof(condition,
            @"khối lượng công việc giữ nguyên, mọi người làm việc với năng suất bằng nhau và năng suất mỗi \{v3\} không thay đổi|khối lượng công việc không đổi; mỗi \{v3\} đều có cùng năng suất và giữ nguyên năng suất đó ở đội mới",
            @"the amount of work stays fixed, all workers have equal productivity and each \{v3\}'s productivity remains unchanged|the amount of work is unchanged; each \{v3\} has the same productivity and keeps that productivity in the new team");
        fullCondition |= vi && Match(condition,
            @"toàn bộ khối lượng công việc là như nhau, và năng suất của từng \{v3\} là bằng nhau và không đổi giữa hai nhóm|khối lượng công việc giữ nguyên, năng suất của mỗi \{v3\} là như nhau và không thay đổi giữa các đội");
        bool target = Proof(d.Question,
            @"\{f2\} \{v0\} cần bao nhiêu \{v1\} để (?:hoàn thành|làm xong) (?:cùng )?công việc đó|(?:hỏi )?(?:cần )?bao nhiêu \{v1\} để \{f2\} \{v0\} hoàn thành cùng công việc đó",
            @"how many \{v1\} do \{f2\} \{v0\} need to (?:complete|finish) (?:that|the) same work|how many \{v1\} will \{f2\} \{v0\} need to complete (?:that|the) same work|in how many \{v1\} can \{f2\} \{v0\} complete (?:that|the) same work");
        target |= vi && Match(d.Question,
            @"\{f2\} \{v0\} sẽ mất bao nhiêu \{v1\} để hoàn thành cùng một khối lượng công việc đó|với \{f2\} \{v0\} làm toàn bộ công việc đó, cần bao nhiêu \{v1\} để hoàn thành");
        bool single = Proof(d.SolutionLeads[0].Text,
            @"thời gian (?:một|mỗi) \{v3\} (?:hoàn thành|làm xong) công việc đó|thời gian để một \{v3\} hoàn thành công việc đó",
            @"(?:the )?time (?:for|needed for) one \{v3\} to complete (?:that|the) work|(?:the )?time one \{v3\} needs to complete (?:that|the) work");
        single |= vi && Match(d.SolutionLeads[0].Text,
            @"thời gian mà một \{v3\} cần để hoàn thành khối lượng công việc đó");
        bool team = Proof(d.SolutionLeads[1].Text,
            @"thời gian \{f2\} \{v0\} (?:hoàn thành|làm xong) công việc đó|thời gian để \{f2\} \{v0\} hoàn thành công việc đó",
            @"(?:the )?time (?:for|needed for) \{f2\} \{v0\} to complete (?:that|the) work|(?:the )?time \{f2\} \{v0\} need to complete (?:that|the) work");
        team |= vi && Match(d.SolutionLeads[1].Text,
            @"thời gian để đội gồm \{f2\} \{v0\} hoàn thành khối lượng công việc đó");
        return given && fullCondition && target && single && team ? new(d, null, c) : Pending();
    }
}
